using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeImageSource : NodeBase
    {
        /// <summary>相机连接后首个软件触发生产帧追加的冷启动宽限，单位为毫秒。</summary>
        private const int InitialProductionFrameStartupGraceMilliseconds = 500;

        /// <summary>生产工件图像使用流程图像总预算的比例，剩余预算留给原始缓冲和下游派生图。</summary>
        private const double WorkpieceFrameMemoryBudgetRatio = 0.50D;

        /// <summary>尚未生成硬件资源档案时的生产工件图像预算。</summary>
        private const int FallbackWorkpieceFrameMemoryBudgetMb = 256;

        /// <summary>没有历史图像样本时触发前预留的保守单帧字节数。</summary>
        private const long DefaultEstimatedCameraFrameBytes = 8L * 1024L * 1024L;

        /// <summary>
        /// 用于保存上次访问的图片索引
        /// </summary>
        public int LastIndex = 0;

        /// <summary>
        /// 当前图片索引
        /// </summary>
        private string _nextImagePath;

        /// <summary>
        /// 当前图片
        /// </summary>
        private Mat _mat;
        /// <summary>
        /// 当前图像源的一次性相机回调等待器。
        /// </summary>
        private readonly ICameraFrameAwaiter _cameraFrameAwaiter;

        /// <summary>
        /// 保护单个图像源节点的一次性相机订阅，禁止并发运行破坏上一轮等待。
        /// </summary>
        private readonly object _cameraCallbackBindingLock = new object();

        /// <summary>
        /// 当前图像源是否已经为某一轮等待绑定相机回调。
        /// </summary>
        private bool _cameraCallbackBound;

        /// <summary>当前生产票据等待的独立取消源，节点删除和释放可以立即撤销局部票据。</summary>
        private CancellationTokenSource _productionCameraWaitCancellation;

        /// <summary>1表示节点已经删除或释放，禁止竞态中的运行线程重新安装相机等待。</summary>
        private int _isUnavailable;

        /// <summary>
        /// 创建使用默认相机帧等待器的图像源节点。
        /// </summary>
        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType)
            : this(nodeId, nodeName, process, nodeType, new CameraFrameAwaiter())
        {
        }

        /// <summary>
        /// 创建使用指定相机帧等待器的图像源节点，便于替换和测试回调协作实现。
        /// </summary>
        internal NodeImageSource(
            int nodeId,
            string nodeName,
            Process process,
            NodeType nodeType,
            ICameraFrameAwaiter cameraFrameAwaiter) : base(nodeId, nodeName, process, nodeType)
        {
            _cameraFrameAwaiter = cameraFrameAwaiter ?? throw new ArgumentNullException(nameof(cameraFrameAwaiter));
            ParamForm = new ParamFormImageSource(this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageSource();
            NodeBase.NodeDeletedEvent += NodeImageSource_NodeDeletedEvent;
            Disposed += NodeImageSource_Disposed;
        }

        public string LoadTestImage(NodeParamImageSoucre param, NodeResultImageSource res)
        {
            if (!string.IsNullOrEmpty(param.ImagePath))
            {
                // 单图模式
                _mat = LoadImage(param.ImagePath);
                res.OutputImage = BuildOutputImage(_mat);
                return param.ImagePath;
            }

            // 多图模式
            if (param.ImagePaths == null || param.ImagePaths.Count == 0)
                throw new ArgumentException("未设置图像路径列表");

            string nextImagePath;

            if (param.IsAutoLoop)
            {
                // 自动循环模式：依次取下一张图像
                nextImagePath = param.ImagePaths[LastIndex];
                LastIndex = (LastIndex + 1) % param.ImagePaths.Count;
            }
            else
            {
                // 手动模式：重复使用上一张图像
                int index = LastIndex == 0 ? 0 : LastIndex - 1;
                nextImagePath = param.ImagePaths[index];
            }

            _mat = LoadImage(nextImagePath);
            res.OutputImage = BuildOutputImage(_mat);
            return nextImagePath;
        }

        // 封装图像加载逻辑
        private Mat LoadImage(string imagePath)
        {
            try
            {
                return Cv2.ImRead(imagePath, ImreadModes.Color);
            }
            catch (Exception ex)
            {
                throw new IOException($"无法加载图像：{imagePath}", ex);
            }
        }

        /// <summary>
        /// 处理相机回调图像。回调已经绑定到当前图像源节点，不需要再全局查找节点。
        /// </summary>
        /// <param name="bitmap">相机回调帧。</param>
        public void HandleCameraCallbackFrame(Bitmap bitmap)
        {
            if (bitmap == null)
                return;

            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            long conversionStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
            Mat callbackMat = null;
            try
            {
                callbackMat = bitmap.ToMat();
                long conversionCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                long conversionMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                    conversionStartedTimestamp,
                    conversionCompletedTimestamp);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=兼容Bitmap转Mat完毕；耗时={conversionMilliseconds}ms；尺寸={bitmap.Width}x{bitmap.Height}",
                    true);
                HandleCameraCallbackFrame(callbackMat);
                callbackMat = null;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"相机回调图像转换失败：{ex.Message}", true);
            }
            finally
            {
                callbackMat?.Dispose();
            }
        }

        /// <summary>
        /// 处理相机回调 Mat 图像，Mat 由当前方法接管生命周期，忙碌或异常时会立即释放。
        /// </summary>
        /// <param name="mat">相机回调 Mat 帧。</param>
        public void HandleCameraCallbackFrame(Mat mat)
        {
            lock (_cameraCallbackBindingLock)
            {
                if (mat == null || mat.Empty())
                {
                    mat?.Dispose();
                    return;
                }

                CameraFrameTraceInfo frameTrace;
                CameraFrameTraceRegistry.TryGet(mat, out frameTrace);
                long nodeReceivedTimestamp = frameTrace == null ? 0L : Stopwatch.GetTimestamp();
                if (_cameraFrameAwaiter.TrySupplyFrame(mat))
                {
                    PerformanceSpikeDiagnostics.LogIfEnabled(
                        MsgLevel.Debug,
                        () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=回调帧交付等待器；{GetCameraFrameTraceText(frameTrace, nodeReceivedTimestamp)}",
                        true);
                    return;
                }

                mat.Dispose();
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=丢弃迟到回调帧；{GetCameraFrameTraceText(frameTrace, nodeReceivedTimestamp)}",
                    true);
            }
        }

        /// <summary>
        /// 生成相机帧编号和回调阶段耗时文本。
        /// </summary>
        /// <param name="frameTrace">相机帧诊断元数据。</param>
        /// <param name="currentTimestamp">当前阶段的高精度计时器刻度。</param>
        /// <returns>可直接追加到完整耗时日志的帧关联文本。</returns>
        private static string GetCameraFrameTraceText(CameraFrameTraceInfo frameTrace, long currentTimestamp)
        {
            if (frameTrace == null)
                return "相机=未知；FrameId=无；回调耗时=无";

            return $"相机={frameTrace.CameraName}；FrameId={frameTrace.FrameId}；回调到当前={frameTrace.GetElapsedFromCallbackStartMilliseconds(currentTimestamp)}ms；转换到当前={frameTrace.GetElapsedFromConversionMilliseconds(currentTimestamp)}ms；尺寸={frameTrace.Width}x{frameTrace.Height}；PixelType={frameTrace.SourcePixelType}";
        }

        /// <summary>
        /// 节点删除时取消尚未完成的相机回调等待。
        /// </summary>
        private void NodeImageSource_NodeDeletedEvent(object sender, NodeBase node)
        {
            if (!ReferenceEquals(node, this))
                return;

            Interlocked.Exchange(ref _isUnavailable, 1);
            _cameraFrameAwaiter.CancelPendingWait();
            CancelProductionCameraWait();
        }

        /// <summary>
        /// 节点释放时清理相机回调等待器和静态事件订阅。
        /// </summary>
        private void NodeImageSource_Disposed(object sender, EventArgs e)
        {
            NodeBase.NodeDeletedEvent -= NodeImageSource_NodeDeletedEvent;
            Disposed -= NodeImageSource_Disposed;
            Interlocked.Exchange(ref _isUnavailable, 1);
            CancelProductionCameraWait();
            _cameraFrameAwaiter.Dispose();
        }

        /// <summary>线程安全取消当前生产相机等待，不在节点事件线程内等待票据或设备。</summary>
        private void CancelProductionCameraWait()
        {
            CancellationTokenSource cancellation;
            lock (_cameraCallbackBindingLock)
                cancellation = _productionCameraWaitCancellation;
            try { cancellation?.Cancel(); } catch { }
        }

        /// <summary>
        /// 构建图像源输出，统一提供原图、兼容旧节点的图像列表和灰度图缓存。
        /// </summary>
        private static OutputImage BuildOutputImage(Mat source)
        {
            return OutputImage.FromOwnedSingleImage(source);
        }

        /// <summary>
        /// 从方案共享变量读取图像，并转换为图像源节点统一输出。
        /// </summary>
        /// <param name="variableName">共享变量名称。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage LoadSharedVariableImage(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName) || variableName == "[未选择]")
                throw new Exception("共享变量名称未设置！");

            SharedVarValue sharedValue = Solution.Instance.SharedVariable.GetValue(variableName);
            object data = UnwrapSharedVariableData(sharedValue);
            OutputImage outputImage = ConvertSharedVariableToOutputImage(variableName, data);
            if (!HasRunnableOutputImage(outputImage))
                throw new Exception($"共享变量“{variableName}”中没有有效图像！");

            return outputImage;
        }

        /// <summary>
        /// 解除嵌套的共享变量值包装，兼容共享变量再次写入共享变量结果的场景。
        /// </summary>
        /// <param name="sharedValue">共享变量值包装对象。</param>
        /// <returns>实际数据对象。</returns>
        private static object UnwrapSharedVariableData(SharedVarValue sharedValue)
        {
            object data = sharedValue?.Data;
            while (data is SharedVarValue nestedValue)
            {
                data = nestedValue.Data;
            }

            return data;
        }

        /// <summary>
        /// 将共享变量中的常见图像类型转换为图像源输出。
        /// </summary>
        /// <param name="variableName">共享变量名称，用于异常提示。</param>
        /// <param name="data">共享变量数据。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage ConvertSharedVariableToOutputImage(string variableName, object data)
        {
            if (data is OutputImage outputImage)
                return NormalizeOutputImage(outputImage);

            if (data is Mat mat)
                return OutputImage.FromBorrowedSingleImage(mat);

            if (data is Bitmap bitmap)
                return OutputImage.FromOwnedSingleImage(bitmap.ToMat());

            if (data is IEnumerable<Mat> matList)
                return BuildOutputImageList(matList, false);

            if (data is IEnumerable<Bitmap> bitmapList)
                return BuildOutputImageList(bitmapList);

            string typeName = data == null ? "空值" : data.GetType().Name;
            throw new Exception($"共享变量“{variableName}”的类型为{typeName}，不是可用图像类型！");
        }

        /// <summary>
        /// 复制共享变量中已有图像的借用视图，保证原图、图像列表和灰度缓存可用。
        /// </summary>
        /// <param name="outputImage">共享变量中的图像源输出。</param>
        /// <returns>归一化后的图像源输出。</returns>
        private static OutputImage NormalizeOutputImage(OutputImage outputImage)
        {
            if (outputImage == null)
                return new OutputImage();

            List<Mat> borrowedImages = new List<Mat>();
            if (outputImage.Bitmaps != null)
            {
                foreach (Mat image in outputImage.Bitmaps)
                {
                    if (OutputImage.HasValidImage(image))
                        borrowedImages.Add(image);
                }
            }

            Mat source = OutputImage.HasValidImage(outputImage.SrcImg)
                ? outputImage.SrcImg
                : borrowedImages.Count > 0 ? borrowedImages[0] : null;
            if (borrowedImages.Count == 0 && OutputImage.HasValidImage(source))
                borrowedImages.Add(source);

            Mat borrowedGray = OutputImage.HasValidImage(outputImage.GrayImg) ? outputImage.GrayImg : null;
            OutputImage normalized = OutputImage.FromBorrowedSingleImage(outputImage, source, borrowedGray);
            normalized.Bitmaps = borrowedImages;
            normalized.Rectangles = outputImage.Rectangles == null
                ? new List<Rect>()
                : new List<Rect>(outputImage.Rectangles);
            normalized.DisplayResult = outputImage.DisplayResult;
            return normalized;
        }

        /// <summary>
        /// 用 Mat 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Mat 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Mat> images, bool takeOwnership)
        {
            List<Mat> mats = new List<Mat>();
            if (images != null)
            {
                foreach (Mat image in images)
                {
                    if (OutputImage.HasValidImage(image))
                        mats.Add(image);
                }
            }

            if (mats.Count == 0)
                return new OutputImage();

            Mat grayImage = null;
            try
            {
                grayImage = OutputImage.BuildGrayImage(mats[0]);
                OutputImage outputImage = new OutputImage
                {
                    SrcImg = mats[0],
                    Bitmaps = mats,
                    GrayImg = grayImage
                };
                if (takeOwnership)
                {
                    outputImage.TakeOwnership(mats).TakeOwnership(grayImage);
                }
                else if (!ReferenceEquals(grayImage, mats[0]))
                {
                    outputImage.TakeOwnership(grayImage);
                }

                return outputImage;
            }
            catch
            {
                if (takeOwnership)
                {
                    foreach (Mat image in mats)
                        image?.Dispose();
                }
                else if (!ReferenceEquals(grayImage, mats[0]))
                {
                    grayImage?.Dispose();
                }

                throw;
            }
        }

        /// <summary>
        /// 用 Bitmap 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Bitmap 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Bitmap> images)
        {
            List<Mat> mats = new List<Mat>();
            try
            {
                if (images != null)
                {
                    foreach (Bitmap image in images)
                    {
                        if (image != null)
                            mats.Add(image.ToMat());
                    }
                }

                List<Mat> ownedMats = mats;
                mats = null;
                return BuildOutputImageList(ownedMats, true);
            }
            catch
            {
                if (mats != null)
                {
                    foreach (Mat mat in mats)
                        mat?.Dispose();
                }

                throw;
            }
        }

        /// <summary>
        /// 判断图像源输出是否有可继续流转的主图。
        /// </summary>
        /// <param name="outputImage">图像源输出对象。</param>
        /// <returns>主图有效时返回 true。</returns>
        private static bool HasRunnableOutputImage(OutputImage outputImage)
        {
            return outputImage != null &&
                outputImage.Bitmaps != null &&
                outputImage.Bitmaps.Count > 0 &&
                OutputImage.HasValidImage(outputImage.Bitmaps[0]);
        }

        /// <summary>
        /// 在当前流程批次中等待相机回调图像，软触发时只发送一次采图命令。
        /// </summary>
        /// <param name="param">当前图像源相机参数。</param>
        /// <param name="camera">本轮固定使用的相机对象。</param>
        /// <param name="token">流程停止时使用的取消令牌。</param>
        /// <returns>由相机回调帧构建的标准图像输出。</returns>
        private async Task<OutputImage> AcquireCameraImageAsync(
            NodeParamImageSoucre param,
            ICamera camera,
            CancellationToken token)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));
            if (camera == null)
                throw new Exception("相机对象无效！");
            if (!camera.IsOpen)
                throw new Exception("相机尚未连接！");
            if (Volatile.Read(ref _isUnavailable) != 0)
                throw new ObjectDisposedException(nameof(NodeImageSource), "图像源节点已经删除或释放，禁止继续等待相机帧。 ");

            WorkpieceExecutionContext workpieceContext =
                Solution.Instance.WorkpieceContextAccessor.Current;
            if (workpieceContext != null)
            {
                return await AcquireProductionCameraImageAsync(
                    param,
                    camera,
                    workpieceContext,
                    token).ConfigureAwait(false);
            }

            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            long waitStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
            Mat callbackMat = null;
            bool callbackBound = false;
            bool callbackOwnerRegistered = false;
            bool waitOwnershipAcquired = false;
            Task<Mat> frameTask = null;
            try
            {
                // 回调订阅只覆盖本轮有效等待，防止已结束流程继续参与每帧分发和图像克隆。
                lock (_cameraCallbackBindingLock)
                {
                    if (Volatile.Read(ref _isUnavailable) != 0)
                        throw new ObjectDisposedException(nameof(NodeImageSource));
                    if (_cameraCallbackBound)
                        throw new InvalidOperationException("当前图像源已经在等待相机回调帧。");

                    callbackBound = true;
                    camera.OnMatReceived += HandleCameraCallbackFrame;
                    camera.RegisterImageCallbackOwner(this);
                    callbackOwnerRegistered = true;
                    _cameraCallbackBound = true;
                    waitOwnershipAcquired = true;
                    frameTask = _cameraFrameAwaiter.BeginWaitAsync(token);
                }

                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=等待器就绪；相机={camera.DevName}；触发源={param.TriggerSource}",
                    true);
                if (param.TriggerModel == TriggerModel.On &&
                    GetEffectiveTriggerSource(param.TriggerSource) == TriggerSource.SOFT)
                    camera.GrabOne();

                callbackMat = await frameTask.ConfigureAwait(false);
                long waitCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                long waitMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                    waitStartedTimestamp,
                    waitCompletedTimestamp);
                CameraFrameTraceInfo frameTrace;
                CameraFrameTraceRegistry.TryGet(callbackMat, out frameTrace);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=等待恢复；等待耗时={waitMilliseconds}ms；{GetCameraFrameTraceText(frameTrace, waitCompletedTimestamp)}",
                    true);

                long outputBuildStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                Mat outputMat = callbackMat;
                callbackMat = null;
                OutputImage outputImage = BuildOutputImage(outputMat);
                long outputBuildCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                long outputBuildMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                    outputBuildStartedTimestamp,
                    outputBuildCompletedTimestamp);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=图像源输出构建完成；耗时={outputBuildMilliseconds}ms；FrameId={frameTrace?.FrameId.ToString() ?? "无"}",
                    true);
                return outputImage;
            }
            catch
            {
                callbackMat?.Dispose();
                if (waitOwnershipAcquired)
                    _cameraFrameAwaiter.CancelPendingWait();
                throw;
            }
            finally
            {
                if (callbackBound)
                {
                    lock (_cameraCallbackBindingLock)
                    {
                        camera.OnMatReceived -= HandleCameraCallbackFrame;
                        if (callbackOwnerRegistered)
                            camera.UnregisterImageCallbackOwner(this);
                        _cameraCallbackBound = false;
                    }
                }
            }
        }

        /// <summary>在真实触发前登记工件票据，并把Mat和实际字节预算成对转给图像输出。</summary>
        /// <param name="param">当前图像源方案参数。</param>
        /// <param name="camera">本轮固定使用的相机对象。</param>
        /// <param name="workpieceContext">当前工件执行上下文。</param>
        /// <param name="token">流程停止令牌。</param>
        /// <returns>携带实际图像预算生命周期的输出图像。</returns>
        private async Task<OutputImage> AcquireProductionCameraImageAsync(
            NodeParamImageSoucre param,
            ICamera camera,
            WorkpieceExecutionContext workpieceContext,
            CancellationToken token)
        {
            if (workpieceContext == null)
                throw new ArgumentNullException(nameof(workpieceContext));

            CameraTriggerTicketRegistry registry = Solution.Instance.CameraTriggerTicketRegistry;
            string cameraKey = CameraProductionIdentity.GetStableKey(camera);
            int configuredTimeoutMilliseconds = GetCameraTimeoutMilliseconds(param, camera);
            int timeoutMilliseconds = configuredTimeoutMilliseconds;
            long memoryBudgetBytes = GetWorkpieceFrameMemoryBudgetBytes();
            long estimatedFrameBytes = GetEstimatedCameraFrameBytes(camera);
            CameraTriggerKind triggerKind = param.TriggerModel == TriggerModel.Off
                ? CameraTriggerKind.Continuous
                : GetProductionCameraTriggerKind(param.TriggerSource);
            bool isSoftwareTrigger = triggerKind == CameraTriggerKind.Software;
            bool initialFrameStartupGraceApplied = false;

            CameraTriggerTicket ticket = null;
            CameraFrameEnvelope envelope = null;
            IProductionCameraStartupGraceReservation startupGraceReservation = null;
            bool callbackOwnerRegistered = false;
            bool callbackBound = false;
            CancellationTokenSource productionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                lock (_cameraCallbackBindingLock)
                {
                    if (Volatile.Read(ref _isUnavailable) != 0)
                        throw new ObjectDisposedException(nameof(NodeImageSource));
                    if (_cameraCallbackBound)
                        throw new InvalidOperationException("当前图像源已经在等待相机生产帧。 ");
                    callbackBound = true;
                    _cameraCallbackBound = true;
                    _productionCameraWaitCancellation = productionCancellation;
                }

                productionCancellation.Token.ThrowIfCancellationRequested();
                camera.RegisterImageCallbackOwner(this);
                callbackOwnerRegistered = true;
                productionCancellation.Token.ThrowIfCancellationRequested();

                IProductionCameraStartupGraceProvider startupGraceProvider =
                    camera as IProductionCameraStartupGraceProvider;
                if (isSoftwareTrigger && startupGraceProvider != null)
                {
                    startupGraceReservation =
                        startupGraceProvider.TryReserveInitialProductionFrameGrace();
                }
                initialFrameStartupGraceApplied = startupGraceReservation != null;
                timeoutMilliseconds = AddInitialProductionFrameStartupGrace(
                    configuredTimeoutMilliseconds,
                    initialFrameStartupGraceApplied);
                // 线路硬触发等待外部信号；连续采集和软件触发均受节点超时约束。
                DateTime deadlineUtc = triggerKind == CameraTriggerKind.Hardware
                    ? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

                ticket = isSoftwareTrigger
                    ? registry.Register(
                        cameraKey,
                        workpieceContext,
                        triggerKind,
                        deadlineUtc,
                        estimatedFrameBytes,
                        memoryBudgetBytes)
                    : triggerKind == CameraTriggerKind.Continuous
                    ? registry.RegisterContinuousWait(
                        cameraKey,
                        workpieceContext,
                        deadlineUtc,
                        estimatedFrameBytes,
                        memoryBudgetBytes)
                    : registry.RegisterHardwareWait(
                        cameraKey,
                        workpieceContext,
                        deadlineUtc,
                        estimatedFrameBytes,
                        memoryBudgetBytes);
                startupGraceReservation?.Commit();
                startupGraceReservation?.Dispose();
                startupGraceReservation = null;

                if (initialFrameStartupGraceApplied)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"相机【{camera.UserDefinedName}】当前连接的首个成功登记生产票据启用启动宽限：" +
                        $"方案超时={configuredTimeoutMilliseconds}ms，附加宽限={InitialProductionFrameStartupGraceMilliseconds}ms，" +
                        $"本次等待上限={timeoutMilliseconds}ms；后续帧恢复方案超时。",
                        true);
                }
                productionCancellation.Token.ThrowIfCancellationRequested();

                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=生产票据就绪；工件={workpieceContext.Identity}；相机={camera.DevName}；触发源={param.TriggerSource}；估算字节={estimatedFrameBytes}；预算字节={memoryBudgetBytes}",
                    true);

                SoftwareTriggerDispatchState triggerDispatchState = isSoftwareTrigger
                    ? new SoftwareTriggerDispatchState()
                    : null;
                Task triggerTask = isSoftwareTrigger
                    ? Task.Factory.StartNew(
                        state =>
                        {
                            SoftwareTriggerDispatchState dispatchState =
                                (SoftwareTriggerDispatchState)state;
                            ticket.IssueSoftwareTrigger(() =>
                            {
                                if (dispatchState.TryBeginSdkCommand())
                                    camera.GrabOne();
                            });
                        },
                        triggerDispatchState,
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default)
                    : Task.CompletedTask;

                envelope = await WaitForProductionFrameAsync(
                    registry,
                    ticket,
                    triggerTask,
                    timeoutMilliseconds,
                    productionCancellation.Token).ConfigureAwait(false);
                productionCancellation.Token.ThrowIfCancellationRequested();
                CameraFrameTraceInfo frameTrace = envelope.TraceInfo;
                Mat outputMat;
                IWorkpieceFrameMemoryLease memoryLease;
                envelope.TransferOwnership(out outputMat, out memoryLease);
                envelope.Dispose();
                envelope = null;

                // 工厂从调用开始即接管两个对象，构造失败也会同时释放，调用方不得重复释放。
                OutputImage outputImage = OutputImage.FromOwnedSingleImageWithLifetime(
                    outputMat,
                    memoryLease);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【完整耗时-相机帧】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；阶段=生产图像输出完成；工件={workpieceContext.Identity}；FrameId={frameTrace?.FrameId.ToString() ?? "无"}；实际预算字节={outputImage.GetRetainedImageBytesEstimateForAdmission()}",
                    true);
                return outputImage;
            }
            catch (TimeoutException timeoutException) when (
                triggerKind != CameraTriggerKind.Hardware &&
                !(timeoutException is SoftwareTriggerCommandTimeoutException) &&
                !productionCancellation.IsCancellationRequested)
            {
                try
                {
                    RecoverProductionCameraAfterFrameTimeout(
                        camera,
                        productionCancellation.Token);
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        $"相机【{camera.UserDefinedName}】本次采图等待超过{timeoutMilliseconds}ms" +
                        (initialFrameStartupGraceApplied
                            ? $"（方案超时{configuredTimeoutMilliseconds}ms+首次启动宽限{InitialProductionFrameStartupGraceMilliseconds}ms）"
                            : string.Empty) + "，" +
                        $"工件{workpieceContext.Identity}按取帧失败结束；该相机已停流排空并重新取流，方案继续运行。",
                        true);
                }
                catch (OperationCanceledException) when (productionCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception recoveryException)
                {
                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        $"相机【{camera.UserDefinedName}】本次采图等待超过{timeoutMilliseconds}ms，" +
                        $"随后执行单相机停流恢复失败：{recoveryException.Message}；方案未因普通取帧超时主动停止。",
                        true);
                    throw new InvalidOperationException(
                        $"相机{cameraKey}本次取帧超时，并且单相机清流恢复失败。",
                        new AggregateException(timeoutException, recoveryException));
                }
                throw;
            }
            finally
            {
                try { startupGraceReservation?.Dispose(); } catch { }
                if (callbackOwnerRegistered)
                {
                    try { camera.UnregisterImageCallbackOwner(this); } catch { }
                }
                try { envelope?.Dispose(); } catch { }
                try { ticket?.Dispose(); } catch { }
                if (callbackBound)
                {
                    lock (_cameraCallbackBindingLock)
                    {
                        if (ReferenceEquals(_productionCameraWaitCancellation, productionCancellation))
                            _productionCameraWaitCancellation = null;
                        _cameraCallbackBound = false;
                    }
                }
                productionCancellation.Dispose();
            }
        }

        /// <summary>普通取帧超时后只重启当前相机，排空可能迟到的旧帧并恢复下一轮采集。</summary>
        private static void RecoverProductionCameraAfterFrameTimeout(
            ICamera camera,
            CancellationToken token)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            token.ThrowIfCancellationRequested();
            if (!camera.IsOpen)
                throw new InvalidOperationException("相机已经断开，无法执行取帧超时恢复。 ");

            if (camera.GetGrabStatus())
                camera.StopGrabbing();
            token.ThrowIfCancellationRequested();
            camera.StartGrabbing();
        }

        /// <summary>等待票据完成或流程取消；连续采集和软件触发受节点采图超时约束。</summary>
        private static async Task<CameraFrameEnvelope> WaitForProductionFrameAsync(
            CameraTriggerTicketRegistry registry,
            CameraTriggerTicket ticket,
            Task triggerTask,
            int timeoutMilliseconds,
            CancellationToken token)
        {
            using (CancellationTokenSource timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                bool waitsUntilStopped = ticket.TriggerKind == CameraTriggerKind.Hardware;
                Task timeoutTask = waitsUntilStopped
                    ? Task.Delay(Timeout.Infinite, timeoutCancellation.Token)
                    : Task.Delay(timeoutMilliseconds, timeoutCancellation.Token);
                Task<CameraFrameEnvelope> frameTask = ticket.Completion;
                Task completedTask = await Task.WhenAny(
                    triggerTask,
                    frameTask,
                    timeoutTask).ConfigureAwait(false);

                if (ReferenceEquals(completedTask, triggerTask))
                {
                    await triggerTask.ConfigureAwait(false);
                    if (!frameTask.IsCompleted)
                    {
                        completedTask = await Task.WhenAny(frameTask, timeoutTask).ConfigureAwait(false);
                        if (ReferenceEquals(completedTask, timeoutTask) && !frameTask.IsCompleted)
                            ThrowProductionCameraWaitFailure(registry, ticket, triggerTask, frameTask, timeoutMilliseconds, token);
                    }
                }
                else if (ReferenceEquals(completedTask, timeoutTask) && !frameTask.IsCompleted)
                {
                    ThrowProductionCameraWaitFailure(registry, ticket, triggerTask, frameTask, timeoutMilliseconds, token);
                }

                CameraFrameEnvelope envelope = await frameTask.ConfigureAwait(false);
                try
                {
                    if (!triggerTask.IsCompleted)
                    {
                        completedTask = await Task.WhenAny(triggerTask, timeoutTask).ConfigureAwait(false);
                        if (ReferenceEquals(completedTask, timeoutTask))
                            ThrowProductionCameraWaitFailure(registry, ticket, triggerTask, frameTask, timeoutMilliseconds, token);
                    }

                    await triggerTask.ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    timeoutCancellation.Cancel();
                    return envelope;
                }
                catch
                {
                    try { envelope.Dispose(); } catch { }
                    throw;
                }
            }
        }

        /// <summary>统一处理节点取消或触发/帧共同超时，绝不把超时点后的已到达帧当作成功。</summary>
        private static void ThrowProductionCameraWaitFailure(
            CameraTriggerTicketRegistry registry,
            CameraTriggerTicket ticket,
            Task triggerTask,
            Task<CameraFrameEnvelope> frameTask,
            int timeoutMilliseconds,
            CancellationToken token)
        {
            ObserveFaultedTask(triggerTask);
            bool triggerStillRunning = !triggerTask.IsCompleted;
            SoftwareTriggerDispatchState triggerDispatchState =
                triggerTask.AsyncState as SoftwareTriggerDispatchState;
            bool cancelledBeforeSdk = triggerStillRunning &&
                triggerDispatchState != null &&
                triggerDispatchState.TryCancelBeforeSdkCommand();
            bool sdkTriggerMayBeRunning = triggerStillRunning &&
                !cancelledBeforeSdk &&
                (triggerDispatchState == null || triggerDispatchState.HasStartedSdkCommand);
            Exception failure = token.IsCancellationRequested
                ? (Exception)new OperationCanceledException(
                    $"相机{ticket.CameraKey}等待工件{ticket.Context.Identity}生产帧已取消。",
                    token)
                : sdkTriggerMayBeRunning
                ? new SoftwareTriggerCommandTimeoutException(
                    $"相机{ticket.CameraKey}为工件{ticket.Context.Identity}执行的软件触发SDK命令在{timeoutMilliseconds}ms内未返回。")
                : new TimeoutException(
                    $"相机{ticket.CameraKey}等待工件{ticket.Context.Identity}生产帧和触发命令共同完成超时（{timeoutMilliseconds}ms）。");

            if (sdkTriggerMayBeRunning)
                registry.FailTriggeredTicket(ticket, failure);
            else if (!token.IsCancellationRequested)
            {
                int failedCount = registry.FailTimedOutTicket(
                    ticket,
                    failure as TimeoutException);
                // 超时与帧完成发生竞态时，注册表是唯一裁决点；票据已经成功脱队则继续读取该成功结果。
                if (failedCount == 0 && frameTask.Status == TaskStatus.RanToCompletion)
                    return;
            }

            DisposeFrameWhenCompleted(frameTask);

            if (token.IsCancellationRequested)
                token.ThrowIfCancellationRequested();
            throw failure;
        }

        /// <summary>在线程池排队、SDK命令开始和超时取消之间提供原子裁决。</summary>
        private sealed class SoftwareTriggerDispatchState
        {
            /// <summary>0表示等待进入SDK，1表示SDK命令已经开始，2表示超时已禁止进入SDK。</summary>
            private int _phase;

            /// <summary>获取真实SDK命令是否已经开始；仅在线程池排队或票据武装不算开始。</summary>
            public bool HasStartedSdkCommand => Volatile.Read(ref _phase) == 1;

            /// <summary>SDK命令调用前原子取得执行权；超时已经获胜时返回false并跳过SDK。</summary>
            public bool TryBeginSdkCommand()
            {
                return Interlocked.CompareExchange(ref _phase, 1, 0) == 0;
            }

            /// <summary>超时原子禁止尚未开始的SDK命令；命令已开始时返回false。</summary>
            public bool TryCancelBeforeSdkCommand()
            {
                return Interlocked.CompareExchange(ref _phase, 2, 0) == 0;
            }
        }

        /// <summary>表示真实软件触发SDK命令已经开始但未返回，禁止按普通无帧超时并发重启相机。</summary>
        private sealed class SoftwareTriggerCommandTimeoutException : TimeoutException
        {
            /// <summary>创建携带SDK触发在途事实的超时异常。</summary>
            /// <param name="message">用于停机诊断的中文原因。</param>
            public SoftwareTriggerCommandTimeoutException(string message)
                : base(message)
            {
            }
        }

        /// <summary>无论帧已完成还是在失败清场后竞态完成，都在唯一成功结果上幂等释放信封。</summary>
        private static void DisposeFrameWhenCompleted(Task<CameraFrameEnvelope> frameTask)
        {
            frameTask.ContinueWith(
                completed =>
                {
                    try { completed.GetAwaiter().GetResult().Dispose(); } catch { }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>超时返回后继续观察仍在SDK中的触发任务异常，避免形成未观察任务。</summary>
        private static void ObserveFaultedTask(Task task)
        {
            task.ContinueWith(
                completed =>
                {
                    AggregateException ignored = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>读取节点配置的相机等待上限，缺省时回退本轮固定相机参数，最终至少1毫秒。</summary>
        /// <param name="param">当前图像源方案参数。</param>
        /// <param name="camera">本轮固定使用的相机对象。</param>
        /// <returns>至少1毫秒的等待上限。</returns>
        private static int GetCameraTimeoutMilliseconds(NodeParamImageSoucre param, ICamera camera)
        {
            uint configuredTimeout = param.TimeOut > 0U
                ? param.TimeOut
                : camera.GetImageTimeOut;
            if (configuredTimeout == 0U)
                configuredTimeout = 2000U;
            return configuredTimeout > int.MaxValue ? int.MaxValue : Math.Max(1, (int)configuredTimeout);
        }

        /// <summary>在不溢出的前提下，为相机连接后的首个软件触发生产帧追加一次启动宽限。</summary>
        /// <param name="configuredTimeoutMilliseconds">图像源保存的稳态超时。</param>
        /// <param name="applyStartupGrace">当前票据是否取得连接会话的唯一宽限资格。</param>
        /// <returns>当前票据实际使用的等待上限。</returns>
        private static int AddInitialProductionFrameStartupGrace(
            int configuredTimeoutMilliseconds,
            bool applyStartupGrace)
        {
            if (!applyStartupGrace)
                return configuredTimeoutMilliseconds;
            long effectiveTimeout = (long)configuredTimeoutMilliseconds +
                InitialProductionFrameStartupGraceMilliseconds;
            return effectiveTimeout > int.MaxValue
                ? int.MaxValue
                : (int)effectiveTimeout;
        }

        /// <summary>把旧默认Auto归一为软触发，只把明确线路源归类为硬触发。</summary>
        /// <param name="triggerSource">节点保存的触发源。</param>
        /// <returns>生产票据使用的明确触发类别。</returns>
        private static CameraTriggerKind GetProductionCameraTriggerKind(TriggerSource triggerSource)
        {
            if (triggerSource == TriggerSource.Auto || triggerSource == TriggerSource.SOFT)
                return CameraTriggerKind.Software;
            if (triggerSource >= TriggerSource.LINE0 && triggerSource <= TriggerSource.LINE4)
                return CameraTriggerKind.Hardware;
            throw new ArgumentOutOfRangeException(nameof(triggerSource), "图像源触发方式无效。 ");
        }

        /// <summary>获取真正写入相机SDK的触发源，Auto按参数界面既有语义归一为软触发。</summary>
        private static TriggerSource GetEffectiveTriggerSource(TriggerSource triggerSource)
        {
            return triggerSource == TriggerSource.Auto
                ? TriggerSource.SOFT
                : triggerSource;
        }

        /// <summary>按自动资源档案计算全部在途工件相机Mat共同遵守的实际字节硬预算。</summary>
        private static long GetWorkpieceFrameMemoryBudgetBytes()
        {
            int flowBudgetMb = Solution.Instance.CurrentResourceProfile?.FlowImageMemoryBudgetMb ?? 0;
            if (flowBudgetMb <= 0)
                return FallbackWorkpieceFrameMemoryBudgetMb * 1024L * 1024L;
            return Math.Max(
                1L,
                (long)Math.Floor(flowBudgetMb * 1024D * 1024D * WorkpieceFrameMemoryBudgetRatio));
        }

        /// <summary>优先使用运行档案的历史平均图像字节，首轮使用8MB保守估算。</summary>
        private static long GetEstimatedCameraFrameBytes(ICamera camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            long cameraBytes = camera.GetProductionFrameMemoryEstimateBytes();
            if (cameraBytes <= 0L)
                throw new InvalidOperationException("相机返回的生产帧内存估算无效。 ");
            long sampledBytes = Solution.Instance.CurrentResourceProfile?.Workload?.AverageImageBytes ?? 0L;
            return Math.Max(
                cameraBytes,
                sampledBytes > 0L ? sampledBytes : DefaultEstimatedCameraFrameBytes);
        }

        /// <summary>
        /// 节点运行方法
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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm.Params is NodeParamImageSoucre param)
            {
                Result = new NodeResultImageSource();
                if (Result is NodeResultImageSource res)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        string fileName = ", 当前图像：";
                        if (param.ImageSource == "本地图像")
                        {
                            var ss = LoadTestImage(param, res);
#if DEBUG
                            fileName += ss;
#endif
                        }
                        else if (param.ImageSource == "相机")
                        {
                            ICamera camera = Solution.Instance.ResolveImageSourceCamera(param);
                            if (camera == null)
                                throw new Exception("相机对象无效！");
                            await Solution.Instance.WaitForCameraReconnectAsync(camera, token).ConfigureAwait(false);
                            if (!camera.IsOpen)
                                throw new Exception("相机尚未连接！");

                            // 是否需要每次设置相机参数
                            if (param.IsEveryTime)
                            {
                                TriggerSource effectiveTriggerSource = GetEffectiveTriggerSource(param.TriggerSource);
                                camera.SetTriggerMode(param.TriggerModel);
                                if (param.TriggerModel == TriggerModel.On)
                                {
                                    camera.SetTriggerSource(effectiveTriggerSource);
                                    if (effectiveTriggerSource >= TriggerSource.LINE0 && effectiveTriggerSource <= TriggerSource.LINE4)
                                        camera.SetTriggerEdge(param.TriggerEdge);
                                    camera.SetTriggerDelay(param.TriggerDelay);
                                }
                                camera.SetExposureTime(param.ExposureTime);
                                camera.SetGain(param.Gain);
                                camera.GetImageTimeOut = param.TimeOut;
                            }

                            res.OutputImage = await AcquireCameraImageAsync(param, camera, token).ConfigureAwait(false);
                        }
                        else if (param.ImageSource == "共享变量")
                        {
                            res.OutputImage = LoadSharedVariableImage(param.SharedVariableName);
#if DEBUG
                            fileName += param.SharedVariableName;
#endif
                        }
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        //Console.WriteLine("时间: "+ DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);

                        if (!HasRunnableOutputImage(res.OutputImage))
                            return new NodeReturn(NodeRunFlag.StopRun);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        Result = new NodeResultImageSource();
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception e)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{e.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        Result = new NodeResultImageSource();
                        throw new Exception($"节点({ID}.{NodeName})运行失败！");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.StopRun);
        }
    }
}
