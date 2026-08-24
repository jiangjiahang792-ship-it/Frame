using Logger;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.CameraIO
{
    /// <summary>通过生产会话协调器按工件顺序输出相机线路脉冲。</summary>
    public class NodeCameraIO: NodeBase, IOrderedExternalSignalNode
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

        /// <summary>获取有序外发白名单中的相机IO类别。</summary>
        public OrderedSignalNodeKind OrderedSignalKind => OrderedSignalNodeKind.CameraIO;

        /// <summary>获取当前配置绑定的稳定物理相机端点键。</summary>
        public string OrderedSignalEndpointKey
        {
            get
            {
                NodeParamCameraIO param = ParamForm?.Params as NodeParamCameraIO;
                return CreateEndpointKey(param?.Camera);
            }
        }

        /// <summary>运行节点并按同步或受管异步模式提交唯一相机IO信号。</summary>
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
                        WorkpieceExecutionContext context = Solution.Instance.WorkpieceContextAccessor.Current;
                        if (context == null)
                            throw new InvalidOperationException("相机IO节点缺少当前工件上下文，已拒绝无序发送。");
                        CameraIoSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
                        Task<OrderedSignalSendResult> sendTask = SubmitOrderedSignalSnapshotAsync(
                            context,
                            $"{Process?.ProcessName ?? "未知流程"}/{ID}.{NodeName}",
                            Solution.Instance.OrderedSignalCoordinator,
                            signalSnapshot,
                            token);
                        if (signalSnapshot.RunAsynchronously)
                        {
                            _ = ObserveManagedAsyncSendAsync(
                                sendTask,
                                context.Identity,
                                $"{ID}.{NodeName}");
                        }
                        else
                        {
                            OrderedSignalSendResult sendResult = await sendTask;
                            EnsureSuccessfulResult(sendResult, token);
                        }
                        
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
                        throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>复制本轮相机、线路、模式和保持时间并提交唯一IO动作。</summary>
        public Task<OrderedSignalSendResult> SubmitOrderedSignalAsync(
            WorkpieceExecutionContext context,
            string nodePath,
            IOrderedSignalCoordinator coordinator,
            CancellationToken waitingCancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (coordinator == null)
                throw new ArgumentNullException(nameof(coordinator));
            CameraIoSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
            return SubmitOrderedSignalSnapshotAsync(
                context,
                nodePath,
                coordinator,
                signalSnapshot,
                waitingCancellationToken);
        }

        /// <summary>使用同一次参数读取形成的不可变快照提交相机IO动作。</summary>
        private Task<OrderedSignalSendResult> SubmitOrderedSignalSnapshotAsync(
            WorkpieceExecutionContext context,
            string nodePath,
            IOrderedSignalCoordinator coordinator,
            CameraIoSignalSnapshot signalSnapshot,
            CancellationToken waitingCancellationToken)
        {
            return context.SubmitOrderedSignalAsync(
                coordinator,
                nodePath,
                OrderedSignalKind,
                signalSnapshot.EndpointKey,
                () => ExecutePulseSnapshotAsync(
                    signalSnapshot.Camera,
                    signalSnapshot.LineSelector,
                    signalSnapshot.LineMode,
                    signalSnapshot.HoldTimeMicroseconds),
                waitingCancellationToken);
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

        /// <summary>在持有相机端点租约期间完成一次从低到高再回低的完整脉冲。</summary>
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
                    "相机在取得发送权后已经断开连接。");
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

        /// <summary>观察受管异步信号，最终工件终态仍由上下文中的在途意图决定。</summary>
        private static async Task ObserveManagedAsyncSendAsync(
            Task<OrderedSignalSendResult> sendTask,
            WorkpieceIdentity identity,
            string nodeText)
        {
            try
            {
                OrderedSignalSendResult result = await sendTask.ConfigureAwait(false);
                if (result.Status != OrderedSignalSendStatus.DeviceAcknowledged &&
                    result.Status != OrderedSignalSendStatus.LocalCallCompleted)
                {
                    LogHelper.AddLog(
                        MsgLevel.Fatal,
                        $"工件{identity}的相机IO节点({nodeText})异步发送返回{result.Status}：{result.Message}",
                        true);
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(
                    MsgLevel.Fatal,
                    $"工件{identity}的相机IO节点({nodeText})异步发送异常：{ex.Message}",
                    true);
            }
        }

        /// <summary>把协调器结果转换为节点同步运行结果。</summary>
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
                        ? $"相机IO有序发送返回{sendResult.Status}。"
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
