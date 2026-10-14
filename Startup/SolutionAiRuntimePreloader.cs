using Logger;
using System;
using System.Collections.Generic;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 在方案恢复完成后顺序预加载已启用 AI 节点的运行时资源。
    /// </summary>
    internal static class SolutionAiRuntimePreloader
    {
        /// <summary>
        /// 预加载阶段在启动页中的起始进度。
        /// </summary>
        private const int PreloadProgressStart = 95;

        /// <summary>
        /// 预加载阶段在启动页中的结束进度。
        /// </summary>
        private const int PreloadProgressEnd = 99;

        /// <summary>
        /// 组合模块递归扫描的最大层级，防止异常快照形成过深或循环结构时拖住启动。
        /// </summary>
        private const int MaxCompositePreloadDepth = 16;

        /// <summary>
        /// 按流程和节点顺序预加载所有已启用且已配置的 AI 节点。
        /// </summary>
        public static void PreloadEnabledNodes()
        {
            List<RuntimePreloadTarget> targets = CollectTargets();
            if (targets.Count == 0)
            {
                StartupProgressContext.ReportItem(
                    "正在预加载 AI 模型",
                    "没有需要预加载的模型",
                    0,
                    0,
                    PreloadProgressStart,
                    PreloadProgressEnd);
                return;
            }

            int successCount = 0;
            int failureCount = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                RuntimePreloadTarget target = targets[index];
                StartupProgressContext.ReportItem(
                    "正在预加载 AI 模型",
                    $"{target.ProcessName} / {target.Node.ID}.{target.Node.NodeName}",
                    index + 1,
                    targets.Count,
                    PreloadProgressStart,
                    PreloadProgressEnd);

                try
                {
                    // 必须逐个等待，避免多个 GPU 模型同时初始化造成显存峰值和 native 依赖竞争。
                    target.Preloader.PreloadSavedRuntimeAsync().GetAwaiter().GetResult();
                    successCount++;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Exception preloadException = new InvalidOperationException(
                        $"流程“{target.ProcessName}”的节点“{target.Node.ID}.{target.Node.NodeName}”AI 模型预加载失败：{ex.Message}",
                        ex);
                    StartupProgressContext.ReportFailure("预加载 AI 模型", preloadException);
                    LogHelper.AddLog(MsgLevel.Exception, preloadException.ToString(), true);
                }
            }

            LogHelper.AddLog(
                failureCount == 0 ? MsgLevel.Info : MsgLevel.Warn,
                $"方案 AI 模型预加载完成，总数={targets.Count}，成功={successCount}，失败={failureCount}。",
                true);
            if (failureCount > 0)
                throw new InvalidOperationException($"方案 AI 模型预加载失败：{failureCount}个节点未准备完成。");
        }

        /// <summary>
        /// 收集需要预加载的节点，并保持方案中的流程和节点原始顺序。
        /// </summary>
        /// <returns>待顺序预加载的节点集合。</returns>
        private static List<RuntimePreloadTarget> CollectTargets()
        {
            List<RuntimePreloadTarget> targets = new List<RuntimePreloadTarget>();
            if (Solution.Instance.AllProcesses == null)
                return targets;

            foreach (Process process in Solution.Instance.AllProcesses)
            {
                if (process == null)
                    continue;
                if (!process.Enable)
                    continue;

                CollectProcessTargets(process, process.ProcessName, targets, 0);
            }

            return targets;
        }

        /// <summary>
        /// 递归收集流程及组合模块内部流程中的预加载节点。
        /// </summary>
        /// <param name="process">当前扫描流程。</param>
        /// <param name="processName">当前流程显示名称。</param>
        /// <param name="targets">待预加载目标集合。</param>
        /// <param name="depth">组合模块递归深度。</param>
        private static void CollectProcessTargets(Process process, string processName, List<RuntimePreloadTarget> targets, int depth)
        {
            if (process == null || process.Nodes == null)
                return;

            foreach (NodeBase node in process.Nodes)
            {
                if (node == null)
                    continue;
                if (!node.Active)
                    continue;

                INodeRuntimePreloader preloader = node as INodeRuntimePreloader;
                if (preloader != null && preloader.HasSavedRuntimeConfiguration)
                    targets.Add(new RuntimePreloadTarget(processName, node, preloader));

                NodeCompositeModule compositeModule = node as NodeCompositeModule;
                if (compositeModule != null)
                    CollectCompositeModuleTargets(compositeModule, processName, targets, depth);
            }
        }

        /// <summary>
        /// 构建组合模块内部流程并继续收集内部预加载节点。
        /// </summary>
        /// <param name="module">组合模块节点。</param>
        /// <param name="ownerProcessName">外层流程显示名称。</param>
        /// <param name="targets">待预加载目标集合。</param>
        /// <param name="depth">当前组合模块递归深度。</param>
        private static void CollectCompositeModuleTargets(NodeCompositeModule module, string ownerProcessName, List<RuntimePreloadTarget> targets, int depth)
        {
            if (depth >= MaxCompositePreloadDepth)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"组合模块 AI 预加载扫描已达到最大层级 {MaxCompositePreloadDepth}，已跳过：{BuildNodeText(ownerProcessName, module)}",
                    true);
                return;
            }

            try
            {
                Process innerProcess = module.PrepareRuntimeProcessForStartupPreload();
                if (innerProcess == null)
                    return;

                string innerProcessName = BuildNodeText(ownerProcessName, module);
                CollectProcessTargets(innerProcess, innerProcessName, targets, depth + 1);
            }
            catch (Exception ex)
            {
                Exception preloadException = new InvalidOperationException(
                    $"流程“{ownerProcessName}”的组合模块“{module.ID}.{module.NodeName}”内部 AI 预加载扫描失败：{ex.Message}",
                    ex);
                StartupProgressContext.ReportFailure("扫描组合模块 AI 模型", preloadException);
                LogHelper.AddLog(MsgLevel.Exception, preloadException.ToString(), true);
            }
        }

        /// <summary>
        /// 构建包含外层流程和组合模块节点的显示文本。
        /// </summary>
        /// <param name="ownerProcessName">外层流程显示名称。</param>
        /// <param name="node">当前组合模块节点。</param>
        /// <returns>用于进度和日志显示的节点路径。</returns>
        private static string BuildNodeText(string ownerProcessName, NodeBase node)
        {
            string processName = string.IsNullOrWhiteSpace(ownerProcessName) ? "未命名流程" : ownerProcessName;
            string nodeName = string.IsNullOrWhiteSpace(node.NodeName) ? "未命名节点" : node.NodeName;
            return $"{processName} / {node.ID}.{nodeName}";
        }

        /// <summary>
        /// 保存一个需要顺序预加载的流程节点及其预加载接口。
        /// </summary>
        private sealed class RuntimePreloadTarget
        {
            /// <summary>
            /// 初始化运行时预加载目标。
            /// </summary>
            /// <param name="processName">所属流程名称。</param>
            /// <param name="node">节点对象。</param>
            /// <param name="preloader">节点预加载接口。</param>
            public RuntimePreloadTarget(string processName, NodeBase node, INodeRuntimePreloader preloader)
            {
                ProcessName = string.IsNullOrWhiteSpace(processName) ? "未命名流程" : processName;
                Node = node ?? throw new ArgumentNullException(nameof(node));
                Preloader = preloader ?? throw new ArgumentNullException(nameof(preloader));
            }

            /// <summary>
            /// 获取所属流程名称。
            /// </summary>
            public string ProcessName { get; }

            /// <summary>
            /// 获取需要预加载的节点。
            /// </summary>
            public NodeBase Node { get; }

            /// <summary>
            /// 获取节点提供的预加载接口。
            /// </summary>
            public INodeRuntimePreloader Preloader { get; }
        }
    }
}
