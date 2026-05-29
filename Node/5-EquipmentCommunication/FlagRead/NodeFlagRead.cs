using Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Forms.GlobalSignalSettings;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    /// <summary>
    /// 监听全局标志位的流程节点，支持等待读取和立即判定两种运行方式。
    /// </summary>
    public class NodeFlagRead : NodeBase, IParallelSignalWaitNode
    {
        /// <summary>
        /// 多个流程线程同时读取同一监听信号时使用的同步锁。
        /// </summary>
        private static readonly object SignalReadStateLock = new object();

        /// <summary>
        /// 每条监听信号的并发读取状态，避免并排 FlagRead 节点互相抢先复位。
        /// </summary>
        private static readonly Dictionary<SingleGlobalSignalSettings, SignalReadState> SignalReadStates =
            new Dictionary<SingleGlobalSignalSettings, SignalReadState>();

        /// <summary>
        /// 当前节点并行预登记名额的同步锁。
        /// </summary>
        private readonly object _parallelSignalWaitLock = new object();

        /// <summary>
        /// 当前节点尚未被运行消费的并行预登记数量。
        /// </summary>
        private int _parallelSignalWaitReservationCount;

        /// <summary>
        /// 当前节点并行预登记绑定的监听信号对象。
        /// </summary>
        private SingleGlobalSignalSettings _parallelSignalWaitReservationSignal;

        /// <summary>
        /// 单条监听信号在流程运行中的并发读取状态。
        /// </summary>
        private sealed class SignalReadState
        {
            /// <summary>
            /// 当前正在等待该信号的 FlagRead 节点数量。
            /// </summary>
            public int WaitingReaderCount { get; set; }

            /// <summary>
            /// 是否已有节点要求复位，但仍需等待并排读取者先通过。
            /// </summary>
            public bool ResetPending { get; set; }
        }

        /// <summary>
        /// 创建 FlagRead 节点并初始化参数窗体和结果对象。
        /// </summary>
        public NodeFlagRead(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormFlagRead();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultFlagRead();
        }

        /// <summary>
        /// 当前 FlagRead 是否需要让流程调度器提前登记并行等待名额。
        /// </summary>
        public bool CanPrepareParallelSignalWait
        {
            get
            {
                NodeParamFlagRead param = GetCurrentParam();
                return param != null && param.WaitForSignal && !string.IsNullOrEmpty(GetParallelSignalWaitKey(param));
            }
        }

        /// <summary>
        /// 当前 FlagRead 监听信号的唯一键。
        /// </summary>
        public string ParallelSignalWaitKey
        {
            get
            {
                NodeParamFlagRead param = GetCurrentParam();
                return GetParallelSignalWaitKey(param);
            }
        }

        /// <summary>
        /// 并行批次启动前登记一个等待名额，确保同批同信号节点不会被提前复位影响。
        /// </summary>
        public void BeginParallelSignalWait()
        {
            NodeParamFlagRead param = GetCurrentParam();
            if (param == null || !param.WaitForSignal)
                return;

            SingleGlobalSignalSettings signal = ResolveSignal(param);
            if (signal == null)
                return;

            RegisterWaitingReader(signal);
            lock (_parallelSignalWaitLock)
            {
                _parallelSignalWaitReservationSignal = signal;
                _parallelSignalWaitReservationCount++;
            }
        }

        /// <summary>
        /// 并行批次结束后释放未被 Run 消费的等待名额。
        /// </summary>
        public void EndParallelSignalWait()
        {
            SingleGlobalSignalSettings signal;
            int reservationCount;
            lock (_parallelSignalWaitLock)
            {
                signal = _parallelSignalWaitReservationSignal;
                reservationCount = _parallelSignalWaitReservationCount;
                _parallelSignalWaitReservationSignal = null;
                _parallelSignalWaitReservationCount = 0;
            }

            for (int i = 0; i < reservationCount; i++)
            {
                if (signal != null)
                    UnregisterWaitingReader(signal);
            }
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

                    if (param.WaitForSignal)
                    {
                        await WaitSignalAsync(signal, token);
                    }

                    bool signalValue = param.WaitForSignal ? true : signal.SignalValue;
                    LogHelper.AddLog(MsgLevel.Exception, $"进入到读取节点,地址为:{signal.Address}   当前值为:{signalValue}   监听值为:{signal.Value}", true);
                    result.ReadResult = new FlagReadResult(signalValue);

                    if (param.Reset)
                    {
                        ResetSignalValue(signal);
                    }

                    var time = SetRunResult(startTime, NodeStatus.Successful);
                    result.RunTime = time;
                    Result = result;

                    if (showLog)
                        LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms, 监听信号：{param.SignalName}, 读取结果：{signalValue})", true);

                    if (!param.WaitForSignal && !signalValue)
                    {
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})不等待监听信号，当前标志位为 false，当前分支或流程提前结束。", true);

                        return new NodeReturn(NodeRunFlag.StopBranch);
                    }

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

        /// <summary>
        /// 等待监听信号变为 true，并登记当前等待者以支持并排 FlagRead 节点一起通过。
        /// </summary>
        private async Task WaitSignalAsync(SingleGlobalSignalSettings signal, CancellationToken token)
        {
            bool usesPreparedReservation = TryConsumeParallelSignalWait(signal);
            if (!usesPreparedReservation)
                RegisterWaitingReader(signal);

            try
            {
                while (!signal.SignalValue)
                {
                    base.CheckTokenCancel(token);
                    await Task.Delay(1, token);
                }
            }
            finally
            {
                UnregisterWaitingReader(signal);
            }
        }

        /// <summary>
        /// 解析参数中保存的监听信号引用，反序列化后会按唯一键重新绑定。
        /// </summary>
        private SingleGlobalSignalSettings ResolveSignal(NodeParamFlagRead param)
        {
            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals == null)
                return null;

            foreach (var signal in signals)
            {
                if (signal == null)
                    continue;

                if (ReferenceEquals(signal, param.Signal))
                    return signal;
            }

            foreach (var signal in signals)
            {
                if (signal == null)
                    continue;

                if (ParamFormFlagRead.BuildSignalKey(signal) == param.SignalKey)
                {
                    param.Signal = signal;
                    return signal;
                }
            }

            param.Signal = null;
            return null;
        }

        /// <summary>
        /// 获取当前保存的 FlagRead 参数。
        /// </summary>
        private NodeParamFlagRead GetCurrentParam()
        {
            return ParamForm == null ? null : ParamForm.Params as NodeParamFlagRead;
        }

        /// <summary>
        /// 获取并行预登记用的监听信号键，兼容旧参数中未保存 SignalKey 的情况。
        /// </summary>
        private static string GetParallelSignalWaitKey(NodeParamFlagRead param)
        {
            if (param == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(param.SignalKey))
                return param.SignalKey;

            if (param.Signal != null)
                return ParamFormFlagRead.BuildSignalKey(param.Signal);

            return param.SignalName ?? string.Empty;
        }

        /// <summary>
        /// 消费流程调度器提前登记的等待名额。
        /// </summary>
        private bool TryConsumeParallelSignalWait(SingleGlobalSignalSettings signal)
        {
            lock (_parallelSignalWaitLock)
            {
                if (_parallelSignalWaitReservationCount <= 0 ||
                    !ReferenceEquals(_parallelSignalWaitReservationSignal, signal))
                {
                    return false;
                }

                _parallelSignalWaitReservationCount--;
                if (_parallelSignalWaitReservationCount == 0)
                    _parallelSignalWaitReservationSignal = null;

                return true;
            }
        }

        /// <summary>
        /// 登记一个正在等待当前监听信号的节点。
        /// </summary>
        private static void RegisterWaitingReader(SingleGlobalSignalSettings signal)
        {
            lock (SignalReadStateLock)
            {
                GetSignalReadState(signal).WaitingReaderCount++;
            }
        }

        /// <summary>
        /// 注销一个等待者，并在最后一个并排等待者通过后执行延迟复位。
        /// </summary>
        private static void UnregisterWaitingReader(SingleGlobalSignalSettings signal)
        {
            lock (SignalReadStateLock)
            {
                SignalReadState state = GetSignalReadState(signal);
                if (state.WaitingReaderCount > 0)
                    state.WaitingReaderCount--;

                if (state.WaitingReaderCount == 0 && state.ResetPending)
                {
                    signal.SignalValue = false;
                    state.ResetPending = false;
                }
            }
        }

        /// <summary>
        /// 根据当前等待者数量立即复位或延迟复位监听信号。
        /// </summary>
        private static void ResetSignalValue(SingleGlobalSignalSettings signal)
        {
            lock (SignalReadStateLock)
            {
                SignalReadState state = GetSignalReadState(signal);
                if (state.WaitingReaderCount > 0)
                {
                    state.ResetPending = true;
                    return;
                }

                signal.SignalValue = false;
                state.ResetPending = false;
            }
        }

        /// <summary>
        /// 获取指定监听信号的并发读取状态。
        /// </summary>
        private static SignalReadState GetSignalReadState(SingleGlobalSignalSettings signal)
        {
            SignalReadState state;
            if (!SignalReadStates.TryGetValue(signal, out state))
            {
                state = new SignalReadState();
                SignalReadStates[signal] = state;
            }

            return state;
        }
    }
}
