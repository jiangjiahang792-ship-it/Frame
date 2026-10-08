using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.Login;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.Forms.SolRunParam
{
    /// <summary>按捕获的根流程身份调用原调参入口，不执行流程或创建学习算法。</summary>
    public sealed class ProcessTuningService
    {
        /// <summary>窗口创建时捕获的根流程，不根据全局编号重新寻找其它方案。</summary>
        private readonly Process[] roots;
        /// <summary>宿主提供的窗口、方案及布局有效性检查。</summary>
        private readonly Func<bool> isCurrent;

        /// <summary>捕获明确的根流程目录，不访问生产单例。</summary>
        public ProcessTuningService(IEnumerable<Process> roots, Func<bool> isCurrent)
        {
            this.roots = (roots ?? throw new ArgumentNullException(nameof(roots))).ToArray();
            this.isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        }

        /// <summary>只检查身份和宿主有效性，定时刷新不遍历节点。</summary>
        public bool CanUse => isCurrent() && UserPermissionContext.CanUseManualTuning;

        /// <summary>打开指定流程的完整参数，或选择该流程内部的AI并复用原学习入口。</summary>
        public void Execute(IWin32Window owner, bool learning, int? processId)
        {
            Process root = ResolveRoot(processId);
            if (!learning)
            {
                ShowManualParameters(owner, root, null, null, () => ResolveRoot(processId));
                return;
            }

            List<NodeTDAI> candidates = GetAiNodes(root);
            if (candidates.Count == 0)
                throw new InvalidOperationException("指定流程未配置AI节点，无法一键学习。");
            NodeTDAI selected = candidates.Count == 1 ? candidates[0] :
                FrmSelectAiNode.SelectNode(owner, root.ProcessName, candidates);
            if (selected == null) return;

            // 模态选择期间可能切换方案、启动流程或重建模块，必须重新验证实例。
            ResolveRoot(processId);
            Process selectedOwner = FindOwner(root, selected);
            if (selectedOwner == null || !candidates.Any(node => ReferenceEquals(node, selected)))
                throw new InvalidOperationException("所选AI节点已失效，请重新选择。");
            Action validate = () =>
            {
                ResolveRoot(processId);
                if (!ReferenceEquals(FindOwner(root, selected), selectedOwner))
                    throw new InvalidOperationException("所选AI节点所属模块已变更，请重新选择。");
                EnsureStopped(selectedOwner);
                // 内部目录存在重复编号时拒绝操作，避免后续订阅和配置保存产生歧义。
                if (GetAiNodes(selectedOwner).Count(node => node.ID == selected.ID) != 1)
                    throw new InvalidOperationException("所选AI节点编号在内部流程中不唯一，无法安全执行一键学习。");
            };
            if (!StartOneClickLearning(selectedOwner, selected, validate))
                throw new InvalidOperationException("所选AI节点当前无法开始一键学习，请检查AI参数配置。");
        }

        /// <summary>重新验证绑定的唯一根流程及停止状态，绝不回退为全部流程。</summary>
        private Process ResolveRoot(int? processId)
        {
            if (!CanUse)
                throw new InvalidOperationException("当前没有调参权限或运行界面已失效，请检查登录状态及方案。");
            if (!processId.HasValue)
                throw new InvalidOperationException("请先为按钮绑定指定流程。");
            Process[] matches = roots.Where(process => process != null && process.ID == processId.Value).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("指定流程已失效或编号不唯一，请重新绑定。");
            EnsureStopped(matches[0]);
            return matches[0];
        }

        /// <summary>拒绝目标及其任意组合模块子流程正在运行的操作，不改变执行或取消状态。</summary>
        private static void EnsureStopped(Process process)
        {
            if (process == null) throw new InvalidOperationException("指定流程已失效。");
            foreach (Process current in EnumerateProcesses(process, new HashSet<Process>()))
                if (current.IsRuning)
                    throw new InvalidOperationException("当前流程或其组合模块仍在运行，请等待运行结束后再使用手动调参与一键学习！");
        }

        /// <summary>原图像窗口与运行按钮共用的权限及目标运行状态校验。</summary>
        private static void EnsureAllowed(Process process, Action validate)
        {
            if (!UserPermissionContext.CanUseManualTuning)
                throw new InvalidOperationException("当前用户没有权限使用手动调参与一键学习，请登录最高权限账号！");
            validate?.Invoke();
            EnsureStopped(process);
        }

        /// <summary>调用原参数窗体；图像窗口的ROI过滤引用原样转交，运行按钮传空表示完整流程。</summary>
        public static void ShowManualParameters(IWin32Window owner, Process process, NodeTDAI target,
            RunParamDisplayFilter filter, Action validate = null)
        {
            EnsureAllowed(process, validate);
            using (var form = new FormSolRunParam(process, target, filter))
            {
                EnsureAllowed(process, validate);
                form.ShowDialog(owner);
            }
        }

        /// <summary>校验目标后直接开启学习，不为学习按钮构造完整调参控件。</summary>
        public static bool StartOneClickLearning(Process process, NodeTDAI target, Action validate = null)
        {
            EnsureAllowed(process, validate);
            if (target == null) return false;
            return SolRunParamControl.StartAutoLearningForNode(process, target);
        }

        /// <summary>收集目标根流程及组合模块的AI实例，不按名称或编号合并。</summary>
        private static List<NodeTDAI> GetAiNodes(Process root)
        {
            return EnumerateProcesses(root, new HashSet<Process>())
                .Where(process => process.Nodes != null)
                .SelectMany(process => process.Nodes.OfType<NodeTDAI>()).Distinct().ToList();
        }

        /// <summary>从当前模块目录查找节点的真实所属流程，不依赖可能过期的节点缓存。</summary>
        private static Process FindOwner(Process root, NodeTDAI target)
        {
            return EnumerateProcesses(root, new HashSet<Process>()).FirstOrDefault(process =>
                process.Nodes != null && process.Nodes.Any(node => ReferenceEquals(node, target)));
        }

        /// <summary>按实例遍历组合模块，循环引用只访问一次，解析异常直接拒绝操作。</summary>
        private static IEnumerable<Process> EnumerateProcesses(Process process, HashSet<Process> visited)
        {
            if (process == null || !visited.Add(process)) yield break;
            yield return process;
            if (process.Nodes == null) yield break;
            foreach (NodeCompositeModule module in process.Nodes.OfType<NodeCompositeModule>())
                foreach (Process child in EnumerateProcesses(module.GetEditableProcess(), visited))
                    yield return child;
        }
    }
}
