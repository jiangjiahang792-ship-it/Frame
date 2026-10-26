using System;
using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>沿明确的模块输出端口追踪原始结果，避免按编号跨模块误选节点。</summary>
    public static class CompositeSubscriptionResolver
    {
        /// <summary>按端口检查可用性；模块失败时仅放行本轮已独立发布的端口，禁止使用旧快照。</summary>
        public static bool CanRead(NodeBase source, string property, bool requireCurrentRun)
        {
            if (source == null || !source.Active || source.Result == null) return false;
            if (source.RuntimeStatus != NodeStatus.Failed &&
                (!requireCurrentRun || source.HasSuccessfulResultForRun(source.Process.CurrentRunId))) return true;
            var result = source.Result as NodeResultCompositeModule;
            return source is NodeCompositeModule && source.RuntimeStatus == NodeStatus.Failed &&
                result != null && source.Process != null && source.Process.CurrentRunId > 0 &&
                result.OutputRunId == source.Process.CurrentRunId &&
                result.TryGetDynamicVariable(NormalizeName(property), out _);
        }

        /// <summary>只沿快照读取来源参数，供端口目录刷新使用，避免在连线事件中递归创建内部窗体。</summary>
        public static INodeParam ResolveParameter(NodeBase source, string property)
        {
            INodeParam parameter = source?.ParamForm?.Params;
            var visited = new HashSet<INodeParam>();
            while (parameter is NodeParamCompositeModule module)
            {
                if (!visited.Add(parameter)) return null;
                string name = NormalizeName(property);
                var definitions = module.UsesDirectOutputs ? module.OutputPorts : module.NodeInfos
                    .Where(node => node.Active).Select(node => node.NodeParam as NodeParamCompositeOutput)
                    .Where(output => output?.Ports != null).SelectMany(output => output.Ports).ToList();
                var ports = definitions.Where(item => item.Name == name).Take(2).ToList();
                if (ports.Count != 1) return null;
                var port = ports[0];
                var nodes = module.NodeInfos.Where(node => node.ID == port.SourceNodeId).Take(2).ToList();
                if (nodes.Count != 1) return null;
                parameter = nodes[0].NodeParam;
                property = port.PropertyPath;
            }
            return parameter;
        }

        /// <summary>统一解析属性路径和界面变量名称。</summary>
        private static string NormalizeName(string property)
        {
            string name = property ?? string.Empty;
            if (name.StartsWith("$variable:", StringComparison.Ordinal)) return name.Substring(10);
            return name.StartsWith("变量.", StringComparison.Ordinal) ? name.Substring(3) : name;
        }

        /// <summary>解析指定输出；普通节点直接返回，模块必须有唯一匹配端口，循环或断开返回空。</summary>
        public static NodeBase Resolve(NodeBase source, string property, out string sourceProperty)
        {
            sourceProperty = property;
            var visited = new HashSet<NodeBase>();
            while (source is NodeCompositeModule module)
            {
                if (!visited.Add(source)) return null;
                var param = module.ParamForm?.Params as NodeParamCompositeModule;
                if (param == null) return null;
                string name = NormalizeName(sourceProperty);
                var process = module.ExistingRuntimeProcess ?? module.GetEditableProcess();
                var definitions = param.UsesDirectOutputs ? param.OutputPorts : process.Nodes
                    .Where(node => node.Active).Select(node => node.ParamForm?.Params as NodeParamCompositeOutput)
                    .Where(output => output?.Ports != null).SelectMany(output => output.Ports).ToList();
                var ports = definitions.Where(item => item.Name == name).Take(2).ToList();
                if (ports.Count != 1) return null;
                var port = ports[0];
                source = process.Nodes.SingleOrDefault(node => node.ID == port.SourceNodeId);
                sourceProperty = port.PropertyPath;
            }
            return source;
        }

        /// <summary>在指定流程作用域内定位节点，绝不递归匹配重复编号。</summary>
        public static NodeBase Find(Process process, int id, string text)
        {
            if (process?.Nodes == null) return null;
            if (id <= 0 && !string.IsNullOrWhiteSpace(text))
                int.TryParse(text.Split('.')[0], out id);
            var matches = process.Nodes.Where(node => node.ID == id).Take(2).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
