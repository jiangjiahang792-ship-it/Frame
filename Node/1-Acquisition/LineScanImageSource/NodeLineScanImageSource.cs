using Logger;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera.LineScan;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._1_Acquisition.LineScanImageSource
{
    /// <summary>从独立线扫相机取得一幅完整拼接图像的流程节点。</summary>
    public sealed class NodeLineScanImageSource : NodeBase
    {
        /// <summary>创建线扫图像源节点。</summary>
        public NodeLineScanImageSource(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormLineScanImageSource();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultLineScanImageSource();
        }

        /// <summary>应用参数并等待一帧完整线扫图像。</summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime start = DateTime.Now;
            if (!Active)
            {
                SetRunResult(start, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            NodeParamLineScanImageSource param = ParamForm.Params as NodeParamLineScanImageSource;
            if (param == null)
                throw new InvalidOperationException("线扫图像源参数尚未保存。");
            try
            {
                ILineScanCamera camera = param.Camera ?? Solution.Instance.LineScanCameraDevices
                    .FirstOrDefault(item => item.UserDefinedName == param.CameraName);
                if (camera == null)
                    throw new InvalidOperationException("未找到线扫相机：" + param.CameraName);
                param.Camera = camera;
                if (!camera.IsOpen)
                    camera.Open();
                if (camera.IsGrabbing)
                    camera.StopGrabbing();
                // 流程执行只写入界面明确配置的参数，不预读相机的完整参数树。
                LineScanCameraSettings settings = new LineScanCameraSettings
                {
                    Width = param.Width,
                    Height = param.Height,
                    ExposureTime = param.ExposureTime,
                    Gain = param.Gain,
                    TriggerSelector = param.TriggerSelector,
                    TriggerMode = param.TriggerMode,
                    TriggerSource = param.TriggerSource,
                    TriggerEdge = param.TriggerEdge
                };
                camera.ApplyAcquisitionSettings(settings);
                OpenCvSharp.Mat image = await WaitForCallbackFrameAsync(
                    camera,
                    token,
                    param.TriggerMode == Device.Camera.TriggerModel.On &&
                    param.TriggerSource == Device.Camera.TriggerSource.SOFT).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                NodeResultLineScanImageSource result = new NodeResultLineScanImageSource
                {
                    OutputImage = OutputImage.FromOwnedSingleImage(image)
                };
                result.RunTime = SetRunResult(start, NodeStatus.Successful);
                Result = result;
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})线扫采图成功，尺寸={image.Width}x{image.Height}，耗时={result.RunTime}ms。", true);
                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                SetRunResult(start, NodeStatus.Unexecuted);
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})线扫采图已停止。", true);
                throw;
            }
            catch (Exception exception)
            {
                SetRunResult(start, NodeStatus.Failed);
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})线扫采图失败：{exception.Message}", true);
                throw;
            }
        }

        /// <summary>建立一次性线扫帧回调等待，并在收到完整帧后立即解绑。</summary>
        private static async Task<OpenCvSharp.Mat> WaitForCallbackFrameAsync(
            ILineScanCamera camera,
            CancellationToken token,
            bool executeSoftwareTrigger)
        {
            TaskCompletionSource<OpenCvSharp.Mat> completion =
                new TaskCompletionSource<OpenCvSharp.Mat>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<OpenCvSharp.Mat> handler = null;
            handler = image =>
            {
                if (!completion.TrySetResult(image))
                    image?.Dispose();
            };

            camera.FrameReceived += handler;
            bool ownershipTransferred = false;
            try
            {
                camera.StartGrabbing();
                // 软件触发必须在回调订阅和启动取流之后执行，否则SDK不会产生待取图像。
                if (executeSoftwareTrigger)
                    camera.ExecuteSoftwareTrigger();
                // 外部线触发可能长时间没有工件，必须一直等待；只由方案停止令牌退出。
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    OpenCvSharp.Mat image = await completion.Task.ConfigureAwait(false);
                    ownershipTransferred = true;
                    return image;
                }
            }
            finally
            {
                camera.FrameReceived -= handler;
                // 本节点启动的取流必须由本节点结束。无论收到图像、用户停止还是发生异常，
                // 都要停止SDK取流，避免下次打开参数窗体时仍显示“相机正在取流”。
                if (camera.IsGrabbing)
                {
                    try
                    {
                        camera.StopGrabbing();
                    }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, "停止线扫相机取流失败：" + exception.Message, true);
                    }
                }
                if (!ownershipTransferred && completion.Task.Status == TaskStatus.RanToCompletion)
                    completion.Task.Result?.Dispose();
            }
        }
    }
}
