using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.GlobalSignalSettings;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    public class NodeFlagRead : NodeBase
    {
        public NodeFlagRead(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormFlagRead();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultFlagRead();
        }

        /// <summary>
        /// 节点运行：等待指定监听信号为 true。
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

            if (ParamForm.Params is NodeParamFlagRead param && Result is NodeResultFlagRead result)
            {
                try
                {
                    SetStatus(NodeStatus.Unexecuted, "*");
                    base.CheckTokenCancel(token);

                    SingleGlobalSignalSettings signal = ResolveSignal(param);
                    if (signal == null)
                        throw new Exception($"未找到监听信号：{param.SignalName}");

                    await WaitSignalAsync(signal, token);

                    bool signalValue = signal.SignalValue;
                    result.ReadResult = new FlagReadResult(signalValue);

                    if (param.Reset)
                    {
                        signal.SignalValue = false;
                    }

                    var time = SetRunResult(startTime, NodeStatus.Successful);
                    result.RunTime = time;
                    Result = result;

                    if (showLog)
                        LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms, 监听信号：{param.SignalName}, 读取结果：{signalValue})", true);

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
                    throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
                }
            }

            return new NodeReturn(NodeRunFlag.StopRun);
        }

        private async Task WaitSignalAsync(SingleGlobalSignalSettings signal, CancellationToken token)
        {
            while (!signal.SignalValue)
            {
                base.CheckTokenCancel(token);
                await Task.Delay(1, token);
            }
        }

        private SingleGlobalSignalSettings ResolveSignal(NodeParamFlagRead param)
        {
            if (param.Signal != null)
                return param.Signal;

            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals == null)
                return null;

            foreach (var signal in signals)
            {
                if (ParamFormFlagRead.BuildSignalKey(signal) == param.SignalKey)
                {
                    param.Signal = signal;
                    return signal;
                }
            }

            return null;
        }
    }
}
