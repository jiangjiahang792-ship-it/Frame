using Logger;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.CameraIO
{
    /// <summary>到达节点后直接输出相机线路脉冲，保留同步和异步执行模式。</summary>
    public class NodeCameraIO: NodeBase
    {
        /// <summary>允许配置的最大脉冲保持时间，防止错误参数长期占住物理端点。</summary>
        private const int MaximumHoldTimeMicroseconds = 1000000;

        /// <summary>创建相机IO节点并初始化参数与结果对象。</summary>
        public NodeCameraIO(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormCameraIO();
            Result = new NodeResultCameraIO();
            ParamForm.SetNodeBelong(this);
        }

        /// <summary>直接开始脉冲；同步模式等待完成，异步模式由独立观察任务记录结果。</summary>
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is ParamFormCameraIO)
            {
                if (ParamForm.Params is NodeParamCameraIO)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        await base.CheckTokenCancel(token);
                        CameraIoSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
                        Task<OrderedSignalSendResult> sendTask = ExecuteDirectPulseSnapshotAsync(signalSnapshot, token);
                        bool completesAsynchronously = signalSnapshot.RunAsynchronously && !sendTask.IsCompleted;
                        if (completesAsynchronously)
                        {
                            _ = ObserveDirectAsyncSendAsync(
                                sendTask,
                                $"{Process?.ProcessName ?? "未知流程"}/{ID}.{NodeName}；工件={Solution.Instance.WorkpieceContextAccessor.Current?.Identity.ToString() ?? "无"}",
                                showLog);
                        }
                        else
                        {
                            OrderedSignalSendResult sendResult = await sendTask;
                            EnsureSuccessfulResult(sendResult, token);
                        }
                        
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, completesAsynchronously
                                ? $"节点({ID}.{NodeName})相机IO脉冲已直接发起，异步模式不等待保持及复位。({time} ms)"
                                : $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
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
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>冻结相机、线路、模式和保持时间，立即开始脉冲并返回实际完成任务。</summary>
        public Task<OrderedSignalSendResult> ExecuteDirectPulseAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            CameraIoSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
            return ExecuteDirectPulseSnapshotAsync(signalSnapshot, token);
        }

        /// <summary>不申请设备发送权；异步脉冲仅登记工件在途生命周期，防止提前结束运行会话。</summary>
        private static async Task<OrderedSignalSendResult> ExecuteDirectPulseSnapshotAsync(
            CameraIoSignalSnapshot signalSnapshot,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            IWorkpieceExecutionLease executionLease = null;
            WorkpieceExecutionContext context = Solution.Instance.WorkpieceContextAccessor.Current;
            if (signalSnapshot.RunAsynchronously && context != null &&
                !context.TryAcquireExecutionLease(out executionLease))
            {
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("当前工件已结束或取消，不能发起新的异步相机IO脉冲。");
            }
            WorkpieceTerminalState terminalState = WorkpieceTerminalState.Faulted;
            try
            {
                token.ThrowIfCancellationRequested();
                // 已拉高的脉冲不因取消提前退出，必须继续尝试复位。
                OrderedSignalSendResult result = await ExecutePulseSnapshotAsync(
                    signalSnapshot.Camera,
                    signalSnapshot.LineSelector,
                    signalSnapshot.LineMode,
                    signalSnapshot.HoldTimeMicroseconds).ConfigureAwait(false);
                EnsureSuccessfulResult(result, token);
                terminalState = WorkpieceTerminalState.Succeeded;
                return result;
            }
            finally
            {
                executionLease?.Complete(terminalState);
            }
        }

        /// <summary>只读取一次当前参数对象，并验证后生成本轮不可变信号快照。</summary>
        private CameraIoSignalSnapshot CaptureSignalSnapshot()
        {
            NodeParamCameraIO paramSnapshot = ParamForm?.Params as NodeParamCameraIO;
            if (paramSnapshot == null)
                throw new InvalidOperationException("相机IO参数尚未设置。");

            ICamera cameraSnapshot = paramSnapshot.Camera;
            if (cameraSnapshot == null)
                throw new InvalidOperationException("相机IO设备尚未绑定。");
            if (!cameraSnapshot.IsOpen)
                throw new InvalidOperationException("相机IO设备尚未连接。");
            string lineSelectorSnapshot = paramSnapshot.LineSelector;
            string lineModeSnapshot = paramSnapshot.LineMode;
            int holdTimeSnapshot = paramSnapshot.HoldTime;
            bool runAsynchronouslySnapshot = paramSnapshot.IsAsay;
            if (string.IsNullOrWhiteSpace(lineSelectorSnapshot))
                throw new InvalidOperationException("相机IO线路不能为空。");
            if (string.IsNullOrWhiteSpace(lineModeSnapshot))
                throw new InvalidOperationException("相机IO线路模式不能为空。");
            if (lineModeSnapshot != "输出" &&
                !string.Equals(lineModeSnapshot, "Output", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("相机IO信号节点只允许使用输出线路。");
            }
            if (holdTimeSnapshot < 0 || holdTimeSnapshot > MaximumHoldTimeMicroseconds)
            {
                throw new InvalidOperationException(
                    $"相机IO保持时间必须在0～{MaximumHoldTimeMicroseconds}微秒之间。");
            }

            return new CameraIoSignalSnapshot(
                cameraSnapshot,
                lineSelectorSnapshot,
                lineModeSnapshot,
                holdTimeSnapshot,
                runAsynchronouslySnapshot,
                CreateEndpointKey(cameraSnapshot));
        }

        /// <summary>直接完成一次从低到高再回低的完整脉冲，不占有跨节点发送租约。</summary>
        private static async Task<OrderedSignalSendResult> ExecutePulseSnapshotAsync(
            ICamera camera,
            string lineSelector,
            string lineMode,
            int holdTimeMicroseconds)
        {
            if (!camera.IsOpen)
            {
                return new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Rejected,
                    "相机在实际输出前已经断开连接。");
            }

            bool outputMayBeRaised = false;
            try
            {
                camera.SetLineSelector(lineSelector);
                camera.SetLineMode(lineMode);
                camera.SetLineInverter(false);
                outputMayBeRaised = true;
                camera.SetLineInverter(true);
                await DelayMicrosecondsAsync(holdTimeMicroseconds).ConfigureAwait(false);
                // 等待期间其他节点可能切换线路，复位前重新选择本次快照的线路。
                camera.SetLineSelector(lineSelector);
                camera.SetLineInverter(false);
                outputMayBeRaised = false;
                return new OrderedSignalSendResult(
                    OrderedSignalSendStatus.LocalCallCompleted,
                    "相机IO完整脉冲API已正常返回。");
            }
            finally
            {
                if (outputMayBeRaised)
                {
                    try
                    {
                        camera.SetLineSelector(lineSelector);
                        camera.SetLineInverter(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>使用粗粒度异步等待加末段自旋，保证脉冲保持时间不短于配置值。</summary>
        private static async Task DelayMicrosecondsAsync(int microseconds)
        {
            if (microseconds <= 0)
                return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            if (microseconds >= 2000)
            {
                int coarseDelayMilliseconds = Math.Max(1, microseconds / 1000 - 1);
                await Task.Delay(coarseDelayMilliseconds).ConfigureAwait(false);
            }
            double targetMilliseconds = microseconds / 1000.0;
            while (stopwatch.Elapsed.TotalMilliseconds < targetMilliseconds)
                Thread.SpinWait(16);
        }

        /// <summary>观察直接执行的异步脉冲，不修改可能已被下一轮复用的节点结果。</summary>
        private static async Task ObserveDirectAsyncSendAsync(
            Task<OrderedSignalSendResult> sendTask,
            string nodeText,
            bool showLog)
        {
            try
            {
                OrderedSignalSendResult result = await sendTask.ConfigureAwait(false);
                EnsureSuccessfulResult(result, CancellationToken.None);
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"相机IO节点({nodeText})异步脉冲完成，保持及复位API已返回。", true);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(
                    MsgLevel.Fatal,
                    $"相机IO节点({nodeText})异步发送异常：{ex.Message}",
                    true);
            }
        }

        /// <summary>把实际脉冲执行结果转换为节点同步运行结果。</summary>
        private static void EnsureSuccessfulResult(
            OrderedSignalSendResult sendResult,
            CancellationToken token)
        {
            if (sendResult.Status == OrderedSignalSendStatus.CancelledBeforeStart)
                throw new OperationCanceledException(sendResult.Message, token);
            if (sendResult.Status != OrderedSignalSendStatus.DeviceAcknowledged &&
                sendResult.Status != OrderedSignalSendStatus.LocalCallCompleted)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(sendResult.Message)
                        ? $"相机IO发送返回{sendResult.Status}。"
                        : sendResult.Message);
            }
        }

        /// <summary>按统一相机生产身份生成跨节点共享的稳定端点键。</summary>
        private static string CreateEndpointKey(ICamera camera)
        {
            if (camera == null)
                throw new InvalidOperationException("相机IO设备尚未绑定。");
            return WorkpieceExecutionContext.NormalizeKey(
                $"CAMERA-IO:{CameraProductionIdentity.GetStableKey(camera)}",
                nameof(camera));
        }

        /// <summary>保存单个工件到达相机IO节点时冻结的全部动态值。</summary>
        private sealed class CameraIoSignalSnapshot
        {
            /// <summary>创建不可变相机IO信号快照。</summary>
            public CameraIoSignalSnapshot(
                ICamera camera,
                string lineSelector,
                string lineMode,
                int holdTimeMicroseconds,
                bool runAsynchronously,
                string endpointKey)
            {
                Camera = camera;
                LineSelector = lineSelector;
                LineMode = lineMode;
                HoldTimeMicroseconds = holdTimeMicroseconds;
                RunAsynchronously = runAsynchronously;
                EndpointKey = endpointKey;
            }

            /// <summary>获取冻结的相机对象。</summary>
            public ICamera Camera { get; }

            /// <summary>获取冻结的输出线路。</summary>
            public string LineSelector { get; }

            /// <summary>获取冻结的输出模式。</summary>
            public string LineMode { get; }

            /// <summary>获取冻结的脉冲保持时间。</summary>
            public int HoldTimeMicroseconds { get; }

            /// <summary>获取冻结的节点异步执行策略。</summary>
            public bool RunAsynchronously { get; }

            /// <summary>获取冻结的物理相机端点键。</summary>
            public string EndpointKey { get; }
        }
    }
}
