using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;

namespace TDJS_Vision.Node._6_LogicTool.SharedVariable
{
    /// <summary>读写方案级共享变量，并发布可被下游订阅的实际值。</summary>
    public class NodeSharedVariable : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        /// <summary>创建共享变量节点及参数窗体。</summary>
        public NodeSharedVariable(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormSharedVariable();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultSharedVariable();
        }

        /// <summary>只有配置完成的读取节点才发布实际值，写入节点不冒充读取节点。</summary>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var param = ParamForm?.Params as NodeParamSharedVariable;
            return param != null && param.IsRead && !string.IsNullOrWhiteSpace(param.ReadName) && param.ReadName != "[未选择]"
                ? new[] { NodeResultSharedVariable.ActualValueName } : new string[0];
        }

        /// <summary>配置阶段识别实际值类型；未知时明确返回对象，避免公共解析器误认为数值。</summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            if (!NodeResultSharedVariable.IsActualValue(variableName)) return false;
            valueType = ResolveReadType(new HashSet<NodeSharedVariable>()) ?? typeof(object);
            // 动态端口约定为单值，集合保留给原包装输出；字符串集合不能误归类为单个文本。
            if (valueType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(valueType))
                valueType = typeof(object);
            return true;
        }

        /// <summary>优先从写入配置推导类型，首次运行前即可订阅；仅配置查询扫描节点，不进入逐帧执行。</summary>
        private Type ResolveReadType(HashSet<NodeSharedVariable> visited)
        {
            var param = ParamForm?.Params as NodeParamSharedVariable;
            if (param == null || !param.IsRead || string.IsNullOrWhiteSpace(param.ReadName) || !visited.Add(this))
                return null;
            try
            {
                Type valueType = null;
                bool hasWriter = false;
                var nodes = Solution.Instance.AllProcesses.SelectMany(process => process.Nodes)
                    .Concat(Solution.Instance.Nodes)
                    .Concat(Process == null ? Enumerable.Empty<NodeBase>() : Process.Nodes)
                    .OfType<NodeSharedVariable>().Distinct();
                foreach (NodeSharedVariable writer in nodes)
                {
                    var writeParam = writer.ParamForm?.Params as NodeParamSharedVariable;
                    if (writeParam == null || writeParam.IsRead ||
                        !string.Equals(writeParam.WriteName, param.ReadName, StringComparison.Ordinal)) continue;
                    hasWriter = true;
                    Type sourceType = writer.ResolveWriteType(writeParam, visited);
                    // 同名变量有不确定或冲突来源时不猜测类型，也不使用旧运行结果掩盖配置变化。
                    if (sourceType == null || sourceType == typeof(object) || (valueType != null && valueType != sourceType))
                        return null;
                    valueType = sourceType;
                }
                if (!hasWriter)
                {
                    try
                    {
                        object value;
                        NodeResultSharedVariable.TryUnwrap(Solution.Instance.SharedVariable.GetValue(param.ReadName), out value, out valueType);
                    }
                    catch (KeyNotFoundException) { return null; }
                }
                if (!param.Flag) return valueType;
                if (valueType == null || param.Index < 0) return null;
                return NodeResultSharedVariable.GetElementType(valueType);
            }
            finally { visited.Remove(this); }
        }

        /// <summary>统一配置推导与窗体恢复的来源定位规则，兼容上游节点改名。</summary>
        internal NodeBase ResolveWriteSource(NodeParamSharedVariable param)
        {
            // 已保存文本带有稳定节点编号；来源改名后仍按编号定位同一上游节点。
            int sourceId;
            string sourceText = param.Text1 ?? string.Empty;
            int separator = sourceText.IndexOf('.');
            bool hasSourceId = int.TryParse(separator < 0 ? sourceText : sourceText.Substring(0, separator), out sourceId);
            return Process?.GetUpstreamNodes(this)
                .FirstOrDefault(node => hasSourceId ? node.ID == sourceId : node.ID + "." + node.NodeName == sourceText);
        }

        /// <summary>通过统一输出目录推导写入来源，支持 PLC 动态值以及共享变量之间的传递。</summary>
        private Type ResolveWriteType(NodeParamSharedVariable param, HashSet<NodeSharedVariable> visited)
        {
            NodeBase source = ResolveWriteSource(param);
            if (source == null || string.IsNullOrWhiteSpace(param.Text2)) return null;
            if (source is NodeSharedVariable shared)
            {
                if (param.Text2 == "读到的共享变量" || param.Text2 == "Value" || NodeResultSharedVariable.IsActualValue(param.Text2))
                    return shared.ResolveReadType(visited);
                return null;
            }
            SubscriptionOutputDescriptor output = SubscriptionPortCatalog
                .GetOutputs(source, SubscriptionInputContract.AnyVisible(), true, param.Text2)
                .FirstOrDefault(item => !item.IsMissing && (item.DisplayName == param.Text2 || item.PropertyPath == param.Text2));
            // 旧方案常订阅PLC的整包“读取结果”；实际值在Data中，配置阶段按PLC参数识别其类型。
            if (output?.ValueType == typeof(PlcReadResult) && source.ParamForm?.Params is NodeParamPlcRead plcParam)
            {
                Type elementType = PlcReadDynamicVariable.GetValueType(plcParam.DataType);
                int count = PlcReadDynamicVariable.BuildNames(plcParam.Address, plcParam.DataType).Count();
                return count == 0 ? null : count == 1 ? elementType : elementType.MakeArrayType();
            }
            return output?.ValueType;
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            // 失败、取消、禁用和切换为写入时都不允许下游继续读取上一次快照。
            if (Result is NodeResultSharedVariable previousResult) previousResult.Value = null;

            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is NodeParamFormSharedVariable form)
            {
                if (form.Params is NodeParamSharedVariable param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        await base.CheckTokenCancel(token);

                        string resStr = "";
                        var result = ((NodeResultSharedVariable)Result);

                        // 判断读还是写
                        if (param.IsRead)
                        {
                            if (param.Flag)
                            {
                                result.Value = NodeResultSharedVariable.GetIndexedValue(
                                    Solution.Instance.SharedVariable.GetValue(param.ReadName), param.Index);
                            }
                            else
                            {
                                result.Value = Solution.Instance.SharedVariable.GetValue(param.ReadName);
                            }
                        }
                        else
                        {
                            // 订阅源只取一次，保证数据与类型属于同一份结果。
                            object value = form.GetSubValue();
                            if (value == null) throw new InvalidOperationException("写入的共享变量值为空！");
                            Solution.Instance.SharedVariable.SetValue(param.WriteName, new SharedVarValue(value, value.GetType()));
                        }

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        Result = result;
                        if(showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>将对象转换为图像列表。</summary>
        private static List<Bitmap> ConvertToBitmapArray(object obj)
        {
            if (obj is List<Bitmap> bitmapList)
            {
                return bitmapList;
            }
            else
            {
                throw new InvalidCastException("对象不是 List<Bitmap> 类型");
            }
        }
    }
}
