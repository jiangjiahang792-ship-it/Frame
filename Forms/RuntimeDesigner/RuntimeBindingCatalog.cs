using System;
using System.Collections.Generic;
using System.Linq;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>复用框架输出契约，把真实流程目录转换为设计器的数据源树。</summary>
    public sealed class RuntimeBindingCatalog : IRuntimeRoiBindingCatalog, IRuntimeLogProcessCatalog
    {
        /// <summary>目录打开时取得当前方案流程；运行时使用固定的方案快照。</summary>
        private readonly Func<IEnumerable<Process>> getProcesses;
        /// <summary>注入方案来源，避免设计器体验入口初始化生产设备。</summary>
        public RuntimeBindingCatalog(Func<IEnumerable<Process>> getProcesses)
        {
            this.getProcesses = getProcesses ?? throw new ArgumentNullException(nameof(getProcesses));
        }

        /// <summary>日志订阅不执行流程，目录保留禁用及被动触发流程。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetLogProcesses()
        {
            return getProcesses().Where(process => process != null)
                .Select(process => new RuntimeProcessChoice { Id = process.ID, Name = process.ID + "." + process.ProcessName }).ToArray();
        }

        /// <summary>按控件类型生成当前可绑定的完整作用域树。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind)
        { return GetSources(kind, false); }

        /// <summary>ROI使用独立目录，只接受明确的AlgorithmResult显示契约。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetRoiSources()
        { return GetSources(RuntimeWidgetKind.Image, true); }

        /// <summary>复用根流程与组合模块完整路径，保持两类图像订阅互相独立。</summary>
        private IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind, bool roi)
        {
            var sources = new List<RuntimeBindingChoice>();
            if (kind != RuntimeWidgetKind.Image && kind != RuntimeWidgetKind.Elapsed) return sources;
            foreach (var process in getProcesses())
            {
                var root = new RuntimeBindingChoice { Name = process.ID + "." + process.ProcessName };
                if (kind == RuntimeWidgetKind.Elapsed)
                    root.Children.Add(Leaf("流程运行耗时", RuntimeBindingKind.ProcessElapsed, process.ID,
                        new int[0], string.Empty, root.Name + " / 流程运行耗时"));
                AddNodes(root.Children, process, process.ID, new int[0], root.Name, kind, new HashSet<Process>(), roi);
                sources.Add(root);
            }
            return sources;
        }

        /// <summary>递归展开组合模块，节点编号只在对应的完整父路径内使用。</summary>
        private static void AddNodes(List<RuntimeBindingChoice> choices, Process process, int rootId,
            int[] parentPath, string parentName, RuntimeWidgetKind kind, HashSet<Process> visited, bool roi)
        {
            if (process == null || parentPath.Length >= 16 || !visited.Add(process)) return;
            foreach (var node in process.Nodes)
            {
                var path = parentPath.Concat(new[] { node.ID }).ToArray();
                string name = node.ID + "." + node.NodeName;
                string fullName = parentName + " / " + name;
                var group = new RuntimeBindingChoice { Name = name };
                if (kind == RuntimeWidgetKind.Elapsed)
                    group.Children.Add(Leaf("节点运行耗时", RuntimeBindingKind.NodeElapsed, rootId, path, string.Empty, fullName + " / 节点运行耗时"));
                else
                    foreach (var output in roi ? GetRoiOutputs(node) : GetImageOutputs(node))
                        group.Children.Add(Leaf(output.DisplayName, roi ? RuntimeBindingKind.Roi : RuntimeBindingKind.Image, rootId, path,
                            output.PropertyPath, fullName + " / " + output.DisplayName));
                var module = node as NodeCompositeModule;
                if (module != null)
                    AddNodes(group.Children, module.GetEditableProcess(), rootId, path, fullName, kind, visited, roi);
                if (group.Children.Count > 0) choices.Add(group);
            }
            visited.Remove(process);
        }

        /// <summary>只公开可转换的 OutputImage，不把普通数值或未知变量列为图片。</summary>
        internal static IEnumerable<SubscriptionOutputDescriptor> GetImageOutputs(NodeBase node)
        {
            return SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.ForType(typeof(OutputImage)), true, null)
                .Where(item => !item.IsMissing && !item.IsLegacySelection);
        }

        /// <summary>复用框架的静态及动态类型声明，不把未知对象、图像或普通点集猜成ROI。</summary>
        internal static IEnumerable<SubscriptionOutputDescriptor> GetRoiOutputs(NodeBase node)
        {
            return SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.ForType(typeof(AlgorithmResult)), true, null)
                .Where(item => !item.IsMissing && !item.IsLegacySelection);
        }

        /// <summary>创建独立叶子值，供选择器无副作用地读取。</summary>
        private static RuntimeBindingChoice Leaf(string name, RuntimeBindingKind kind, int processId,
            int[] path, string property, string display)
        {
            return new RuntimeBindingChoice { Name = name, Binding = new RuntimeDataBinding
                { Kind = kind, ProcessId = processId, NodePath = (int[])path.Clone(), PropertyPath = property, DisplayPath = display } };
        }

        /// <summary>以编号解析名称；原来源不存在时保留原路径并明确标注失效。</summary>
        public string Describe(RuntimeDataBinding binding)
        {
            if (binding == null) return "未绑定";
            Process root = getProcesses().FirstOrDefault(p => p.ID == binding.ProcessId);
            if (root == null) return "已失效：" + binding.DisplayPath;
            string name = root.ID + "." + root.ProcessName;
            if (binding.Kind == RuntimeBindingKind.ProcessElapsed) return name + " / 流程运行耗时";
            Process scope = root;
            for (int index = 0; index < binding.NodePath.Length; index++)
            {
                var node = scope?.Nodes.FirstOrDefault(n => n.ID == binding.NodePath[index]);
                if (node == null) return "已失效：" + binding.DisplayPath;
                name += " / " + node.ID + "." + node.NodeName;
                if (index == binding.NodePath.Length - 1)
                {
                    if (binding.Kind == RuntimeBindingKind.NodeElapsed) return name + " / 节点运行耗时";
                    var output = (binding.Kind == RuntimeBindingKind.Roi ? GetRoiOutputs(node) : GetImageOutputs(node))
                        .FirstOrDefault(o => o.PropertyPath == binding.PropertyPath);
                    return output == null ? "已失效：" + binding.DisplayPath : name + " / " + output.DisplayName;
                }
                scope = (node as NodeCompositeModule)?.GetEditableProcess();
            }
            return "已失效：" + binding.DisplayPath;
        }

        /// <summary>运行阶段仅定位已有实例，组合模块重建后不会误读旧节点，也不会创建新流程。</summary>
        internal static NodeBase ResolveNode(Process root, RuntimeDataBinding binding)
        {
            Process scope = root;
            for (int index = 0; index < binding.NodePath.Length; index++)
            {
                var node = scope?.Nodes.FirstOrDefault(n => n.ID == binding.NodePath[index]);
                if (node == null || node.IsDisposed) return null;
                if (index == binding.NodePath.Length - 1) return node;
                scope = (node as NodeCompositeModule)?.ExistingRuntimeProcess;
            }
            return null;
        }
    }
}
