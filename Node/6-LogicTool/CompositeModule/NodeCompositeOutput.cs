using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输出节点，在被封装流程内部声明外部组合模块可订阅的输出变量。
    /// </summary>
    public class NodeCompositeOutput : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider, INodeSubscriptionDependencyProvider
    {
        /// <summary>
        /// 创建组合输出节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCompositeOutput(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormCompositeOutput();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultCompositeOutput();
        }

        /// <summary>
        /// 获取输出端口依赖的内部源节点 ID。
        /// </summary>
        /// <returns>源节点 ID 集合。</returns>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            NodeParamCompositeOutput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeOutput;
            if (param == null || param.Ports == null)
                return nodeIds;

            foreach (CompositeOutputPortDefinition port in param.Ports)
            {
                if (port == null || port.SourceNodeId <= 0 || port.SourceNodeId == ID)
                    continue;

                nodeIds.Add(port.SourceNodeId);
            }

            return nodeIds;
        }

        /// <summary>
        /// 获取配置阶段可见的动态输出变量名。
        /// </summary>
        /// <returns>动态变量名集合。</returns>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamCompositeOutput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeOutput;
            if (param != null && param.Ports != null)
            {
                foreach (CompositeOutputPortDefinition port in param.Ports)
                {
                    if (port == null || string.IsNullOrWhiteSpace(port.Name))
                        continue;

                    names.Add(port.Name.Trim());
                }
            }

            NodeResultCompositeOutput result = Result as NodeResultCompositeOutput;
            if (result != null)
            {
                foreach (string name in result.GetDynamicVariableNames())
                    names.Add(name);
            }

            return names.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 获取输出变量声明类型。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="valueType">值类型。</param>
        /// <returns>找到变量定义时返回 true。</returns>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            NodeParamCompositeOutput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeOutput;
            if (param != null && param.Ports != null)
            {
                foreach (CompositeOutputPortDefinition port in param.Ports)
                {
                    if (port == null ||
                        !string.Equals(port.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    valueType = CompositePortValueHelper.GetRuntimeType(port.ValueType);
                    return true;
                }
            }

            NodeResultCompositeOutput result = Result as NodeResultCompositeOutput;
            return result != null && result.TryGetDynamicResultVariableType(name, out valueType);
        }

        /// <summary>
        /// 从内部源节点读取输出端口值并发布。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出运行日志。</param>
        /// <returns>节点运行返回值。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                int inactiveTime = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = inactiveTime;
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            NodeParamCompositeOutput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeOutput;
            if (param == null || param.Ports == null || param.Ports.Count == 0)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})未配置组合输出端口！", true);
                Result = NodeResultCompositeOutput.BuildFailure("未配置组合输出端口。");
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})未配置组合输出端口！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                NodeResultCompositeOutput result = BuildResult(param);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})组合输出发布成功！变量：{result.ValuesText}，{time} ms", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                Result = NodeResultCompositeOutput.BuildFailure(ex.Message);
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据输出端口定义读取内部结果并构建节点结果。
        /// </summary>
        /// <param name="param">组合输出参数。</param>
        /// <returns>输出节点结果。</returns>
        private NodeResultCompositeOutput BuildResult(NodeParamCompositeOutput param)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var valueTypes = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase);

            foreach (CompositeOutputPortDefinition port in param.Ports)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.Name))
                    continue;

                string name = port.Name.Trim();
                object rawValue = CompositePortValueHelper.ReadSourceValue(
                    this,
                    port.SourceNodeId,
                    port.SourceNodeText,
                    port.PropertyPath,
                    port.PropertyDisplayName);

                object value = CompositePortValueHelper.ConvertValue(rawValue, port.ValueType, "输出端口“" + name + "”");
                values[name] = value;
                valueTypes[name] = port.ValueType;
            }

            return new NodeResultCompositeOutput
            {
                IsOk = true,
                Message = "输出发布成功",
                Values = values,
                ValueTypes = valueTypes,
                ValuesText = NodeResultCompositeOutput.BuildValuesText(values)
            };
        }
    }
}
