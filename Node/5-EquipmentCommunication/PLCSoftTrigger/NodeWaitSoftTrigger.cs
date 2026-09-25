using HslCommunication;
using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger
{
    public class NodeWaitSoftTrigger : NodeBase
    {
        public NodeWaitSoftTrigger(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormWaitSoftTrigger();
            Result = new NodeResultWaitSoftTrigger();
        }


        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormWaitSoftTrigger form)
            {
                if (form.Params is NodeParamWaitSoftTrigger param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        //如果没有连接则不运行
                        if (param.Plc == null || !param.Plc.IsConnect)
                            throw new Exception("PLC设备资源已释放或尚未连接！");//如果没有连接则不运行

                        // 监听拍照信号,支持取消
                        await Task.Run(() => GetShotSignalFromPlcAsync(param, token));

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog)
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

        private async Task GetShotSignalFromPlcAsync(NodeParamWaitSoftTrigger param, CancellationToken token)
        {
            try
            {
                OperateResult writeResult;
                string dataType = string.IsNullOrWhiteSpace(param.DataType) ? typeof(bool).Name : param.DataType;
                string compareValue = string.IsNullOrWhiteSpace(param.CompareValue) ? "True" : param.CompareValue;
                // 读取拍照信号
                do
                {
                    base.CheckTokenCancel(token);
                    OperateResult<object> readResult = await ReadTriggerValueAsync(param, dataType);
                    if (readResult.IsSuccess && PlcSoftTriggerValueMatcher.IsMatch(readResult.Content, dataType, compareValue))
                        break;
                    Thread.Sleep(3);
                } while (true);  // 读取失败或读取值未等于触发值均需要重试

                // 只对布尔触发地址执行复位，避免误清寄存器或字符串地址。
                if (param.Reset && dataType == typeof(bool).Name)
                {
                    do
                    {
                        base.CheckTokenCancel(token);
                        writeResult = await param.Plc.WriteBoolAsync(param.Address, new bool[] { false });
                        Thread.Sleep(3);
                    } while (!writeResult.IsSuccess);
                }
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>按用户配置的数据类型读取单个PLC触发值。</summary>
        private static async Task<OperateResult<object>> ReadTriggerValueAsync(NodeParamWaitSoftTrigger param, string dataType)
        {
            if (dataType == typeof(bool).Name)
            {
                var result = await param.Plc.ReadBoolAsync(param.Address);
                return ToObjectResult(result);
            }
            if (dataType == typeof(short).Name)
            {
                var result = await param.Plc.ReadInt16Async(param.Address);
                return ToObjectResult(result);
            }
            if (dataType == typeof(int).Name)
            {
                var result = await param.Plc.ReadInt32Async(param.Address);
                return ToObjectResult(result);
            }
            if (dataType == typeof(long).Name)
            {
                var result = await param.Plc.ReadInt64Async(param.Address);
                return ToObjectResult(result);
            }
            if (dataType == typeof(float).Name)
            {
                var result = await param.Plc.ReadFloatAsync(param.Address);
                return ToObjectResult(result);
            }
            if (dataType == typeof(string).Name)
            {
                ushort length = param.StringLength <= 0 ? (ushort)1 : param.StringLength;
                var result = await param.Plc.ReadStringAsync(param.Address, length);
                return ToObjectResult(result);
            }
            throw new InvalidOperationException($"PLC软触发不支持读取数据类型：{dataType}。");
        }

        /// <summary>把HslCommunication泛型读取结果统一转为对象结果，便于复用对比逻辑。</summary>
        private static OperateResult<object> ToObjectResult<T>(OperateResult<T> result)
        {
            if (result == null)
                return new OperateResult<object>("PLC读取返回空结果。");
            if (!result.IsSuccess)
                return new OperateResult<object>(result.Message);
            return OperateResult.CreateSuccessResult((object)result.Content);
        }
    }
}
