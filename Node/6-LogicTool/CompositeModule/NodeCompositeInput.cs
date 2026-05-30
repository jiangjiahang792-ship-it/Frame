using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输入节点，在被封装流程内部声明可由外部组合模块传入的变量。
    /// </summary>
    public class NodeCompositeInput : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 外部组合模块运行时注入的输入变量值。
        /// </summary>
        private Dictionary<string, object> _runtimeValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 创建组合输入节点。
        /// </summary>
        /// <param name="nodeId">节点 ID。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeCompositeInput(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormCompositeInput();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultCompositeInput();
        }

        /// <summary>
        /// 设置外部组合模块传入的输入值。
        /// </summary>
        /// <param name="values">输入变量值表。</param>
        public void SetRuntimeInputValues(Dictionary<string, object> values)
        {
            _runtimeValues = values == null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(values, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取配置阶段可见的动态输入变量名。
        /// </summary>
        /// <returns>动态变量名集合。</returns>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamCompositeInput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeInput;
            if (param != null && param.Ports != null)
            {
                foreach (CompositeInputPortDefinition port in param.Ports)
                {
                    if (port == null || string.IsNullOrWhiteSpace(port.Name))
                        continue;

                    names.Add(port.Name.Trim());
                }
            }

            NodeResultCompositeInput result = Result as NodeResultCompositeInput;
            if (result != null)
            {
                foreach (string name in result.GetDynamicVariableNames())
                    names.Add(name);
            }

            return names.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 获取输入变量的声明类型。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="valueType">值类型。</param>
        /// <returns>找到变量定义时返回 true。</returns>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            NodeParamCompositeInput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeInput;
            if (param != null && param.Ports != null)
            {
                foreach (CompositeInputPortDefinition port in param.Ports)
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

            NodeResultCompositeInput result = Result as NodeResultCompositeInput;
            return result != null && result.TryGetDynamicResultVariableType(name, out valueType);
        }

        /// <summary>
        /// 发布当前组合输入变量。
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

            NodeParamCompositeInput param = ParamForm == null ? null : ParamForm.Params as NodeParamCompositeInput;
            if (param == null || param.Ports == null || param.Ports.Count == 0)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})未配置组合输入端口！", true);
                Result = NodeResultCompositeInput.BuildFailure("未配置组合输入端口。");
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})未配置组合输入端口！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                NodeResultCompositeInput result = BuildResult(param);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                result.RunTime = time;
                Result = result;

                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})组合输入发布成功！变量：{result.ValuesText}，{time} ms", true);

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
                Result = NodeResultCompositeInput.BuildFailure(ex.Message);
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据端口定义和运行时注入值构建输入节点结果。
        /// </summary>
        /// <param name="param">输入端口参数。</param>
        /// <returns>输入节点结果。</returns>
        private NodeResultCompositeInput BuildResult(NodeParamCompositeInput param)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var valueTypes = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase);

            foreach (CompositeInputPortDefinition port in param.Ports)
            {
                if (port == null || string.IsNullOrWhiteSpace(port.Name))
                    continue;

                string name = port.Name.Trim();
                object value;
                if (!_runtimeValues.TryGetValue(name, out value))
                    value = CompositePortValueHelper.ConvertConstant(port.DefaultValue, port.ValueType, "输入端口“" + name + "”默认值");
                else
                    value = CompositePortValueHelper.ConvertValue(value, port.ValueType, "输入端口“" + name + "”");

                values[name] = value;
                valueTypes[name] = port.ValueType;
            }

            return new NodeResultCompositeInput
            {
                IsOk = true,
                Message = "输入发布成功",
                Values = values,
                ValueTypes = valueTypes,
                ValuesText = NodeResultCompositeInput.BuildValuesText(values)
            };
        }
    }
}
