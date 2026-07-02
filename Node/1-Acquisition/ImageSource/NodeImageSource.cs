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
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeImageSource : NodeBase, ISkippedNodeRunResultProvider
    {
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
        /// 相机回调触发运行状态，0 表示空闲，1 表示已有一帧正在转换或驱动流程运行。
        /// </summary>
        private int _callbackRunState;
        /// <summary>
        /// 待写入当前流程批次结果的相机回调图像。
        /// </summary>
        private Mat _pendingCallbackMat;
        /// <summary>
        /// 相机回调流程专用工作线程唤醒信号，避免每帧通过线程池调度。
        /// </summary>
        private readonly AutoResetEvent _callbackWorkerSignal = new AutoResetEvent(false);
        /// <summary>
        /// 相机回调流程专用工作线程创建锁。
        /// </summary>
        private readonly object _callbackWorkerLock = new object();
        /// <summary>
        /// 相机回调流程专用工作线程。
        /// </summary>
        private Thread _callbackWorkerThread;
        /// <summary>
        /// 相机回调流程专用工作线程是否正在停止。
        /// </summary>
        private volatile bool _callbackWorkerStopping;
        /// <summary>
        /// 当前已入队帧的高精度计时器时间戳，用于统计等待专用线程耗时。
        /// </summary>
        private long _callbackQueuedTimestamp;
        /// <summary>
        /// 相机回调收到的有效帧数量。
        /// </summary>
        private long _callbackReceivedCount;
        /// <summary>
        /// 相机回调接受并准备处理的帧数量。
        /// </summary>
        private long _callbackAcceptedCount;
        /// <summary>
        /// 相机回调因流程忙碌而拒绝的帧数量。
        /// </summary>
        private long _callbackBusyRejectedCount;
        /// <summary>
        /// 相机回调专用线程实际启动流程运行的次数。
        /// </summary>
        private long _callbackRunCount;

        public NodeImageSource(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
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

            Mat callbackMat = null;
            try
            {
                callbackMat = bitmap.ToMat();
                LogHelper.AddLog(MsgLevel.Info, $"流程【{Process?.ProcessName}】Bitmap转Mat完毕, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
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
            Stopwatch enqueueWatch = Stopwatch.StartNew();
            if (mat == null || mat.Empty() || Process == null)
            {
                mat?.Dispose();
                return;
            }

            if (!Process.IsCameraCallbackTriggered)
            {
                mat.Dispose();
                return;
            }

            if (!Active || !Process.Enable)
            {
                mat.Dispose();
                return;
            }

            if (!Solution.Instance.IsRunning && !Process.IsHandRun)
            {
                mat.Dispose();
                return;
            }

            long receivedCount = Interlocked.Increment(ref _callbackReceivedCount);
            if (Process.IsRuning)
            {
                mat.Dispose();
                LogBusyRejectedFrame(receivedCount, "流程正在运行");
                return;
            }

            if (!TryEnterCallbackRunState())
            {
                mat.Dispose();
                LogBusyRejectedFrame(receivedCount, "专用线程正在处理上一帧");
                return;
            }

            try
            {
                EnsureCallbackWorkerStarted();
                long acceptedCount = Interlocked.Increment(ref _callbackAcceptedCount);
                LogHelper.AddLog(MsgLevel.Info, $"流程【{Process.ProcessName}】接收Mat图像成功, 尺寸={mat.Width}x{mat.Height}, 通道={mat.Channels()}, 接收帧={receivedCount}, 处理帧={acceptedCount}, 忙碌丢帧={Volatile.Read(ref _callbackBusyRejectedCount)}, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
                StorePendingCallbackMat(mat);
                mat = null;
                Interlocked.Exchange(ref _callbackQueuedTimestamp, Stopwatch.GetTimestamp());
                _callbackWorkerSignal.Set();

                LogHelper.AddLog(MsgLevel.Info, $"流程【{Process.ProcessName}】相机回调帧已交给专用线程, 入队耗时={enqueueWatch.ElapsedMilliseconds}ms, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
            }
            catch (Exception ex)
            {
                mat?.Dispose();
                ClearPendingCallbackMat();
                ReleaseCallbackRunState();
                LogHelper.AddLog(MsgLevel.Exception, $"相机回调Mat图像处理失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 确保相机回调流程专用工作线程已启动。
        /// </summary>
        private void EnsureCallbackWorkerStarted()
        {
            if (_callbackWorkerThread != null && _callbackWorkerThread.IsAlive)
                return;

            lock (_callbackWorkerLock)
            {
                if (_callbackWorkerThread != null && _callbackWorkerThread.IsAlive)
                    return;

                _callbackWorkerStopping = false;
                _callbackWorkerThread = new Thread(CallbackWorkerLoop)
                {
                    IsBackground = true,
                    Name = $"图像源回调流程-{Process?.ProcessName}-{ID}"
                };
                _callbackWorkerThread.Start();
            }
        }

        /// <summary>
        /// 相机回调流程专用工作线程循环，收到信号后同步执行当前流程。
        /// </summary>
        private void CallbackWorkerLoop()
        {
            while (!_callbackWorkerStopping)
            {
                _callbackWorkerSignal.WaitOne();
                if (_callbackWorkerStopping)
                    break;

                RunPendingCallbackFrame();
            }
        }

        /// <summary>
        /// 执行已入队的相机回调帧，并在结束后释放忙碌状态。
        /// </summary>
        private void RunPendingCallbackFrame()
        {
            Stopwatch runWatch = Stopwatch.StartNew();
            try
            {
                long queuedTimestamp = Interlocked.Exchange(ref _callbackQueuedTimestamp, 0);
                long waitMs = CalculateElapsedMilliseconds(queuedTimestamp, Stopwatch.GetTimestamp());
                long runCount = Interlocked.Increment(ref _callbackRunCount);
                LogHelper.AddLog(MsgLevel.Warn, $"【版本诊断-专用线程执行入口】{PerformanceSpikeDiagnostics.GetExecutableIdentityText()}；流程={Process?.ProcessName}；节点=({ID}.{NodeName})；排队等待={waitMs}ms；执行次数={runCount}", true);
                LogHelper.AddLog(MsgLevel.Info, $"流程【{Process.ProcessName}】专用线程执行Run方法, 排队等待={waitMs}ms, 执行次数={runCount}, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
                Process.Run(this, false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"相机回调流程【{Process?.ProcessName}】运行异常：{ex.Message}", true);
            }
            finally
            {
                long beforeClear = runWatch.ElapsedMilliseconds;
                ClearPendingCallbackMat();
                long afterClear = runWatch.ElapsedMilliseconds;
                ReleaseCallbackRunState();
                long afterRelease = runWatch.ElapsedMilliseconds;
                runWatch.Stop();
                LogHelper.AddLog(MsgLevel.Info, $"【链路诊断-专用线程收尾】流程={Process?.ProcessName}；节点=({ID}.{NodeName})；清理待处理Mat={afterClear - beforeClear}ms；释放运行状态={afterRelease - afterClear}ms；总耗时={runWatch.ElapsedMilliseconds}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}", true);
                LogHelper.AddLog(MsgLevel.Info, $"流程【{Process?.ProcessName}】专用线程Run结束, 耗时={runWatch.ElapsedMilliseconds}ms, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
            }
        }

        /// <summary>
        /// 记录忙碌拒绝帧日志，当前帧由调用方在记录前释放。
        /// </summary>
        /// <param name="receivedCount">当前累计收到帧数。</param>
        /// <param name="reason">拒绝原因。</param>
        private void LogBusyRejectedFrame(long receivedCount, string reason)
        {
            long rejectedCount = Interlocked.Increment(ref _callbackBusyRejectedCount);
            LogHelper.AddLog(MsgLevel.Warn, $"流程【{Process.ProcessName}】忙碌拒绝相机回调帧，原因：{reason}；接收帧={receivedCount}，处理帧={Volatile.Read(ref _callbackAcceptedCount)}，忙碌丢帧={rejectedCount}，Process.IsRuning={Process.IsRuning}，RunState={Volatile.Read(ref _callbackRunState)}。", true);
        }

        /// <summary>
        /// 计算两个高精度时间戳之间的毫秒差。
        /// </summary>
        /// <param name="startTimestamp">开始时间戳。</param>
        /// <param name="endTimestamp">结束时间戳。</param>
        /// <returns>毫秒差，开始时间戳为空时返回 0。</returns>
        private static long CalculateElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (startTimestamp <= 0 || endTimestamp <= startTimestamp)
                return 0;

            return (endTimestamp - startTimestamp) * 1000 / Stopwatch.Frequency;
        }

        /// <summary>
        /// 节点删除时停止相机回调专用线程并释放未消费帧。
        /// </summary>
        private void NodeImageSource_NodeDeletedEvent(object sender, NodeBase node)
        {
            if (!ReferenceEquals(node, this))
                return;

            StopCallbackWorker();
        }

        /// <summary>
        /// 节点释放时清理相机回调专用线程和静态事件订阅。
        /// </summary>
        private void NodeImageSource_Disposed(object sender, EventArgs e)
        {
            NodeBase.NodeDeletedEvent -= NodeImageSource_NodeDeletedEvent;
            Disposed -= NodeImageSource_Disposed;
            StopCallbackWorker();
        }

        /// <summary>
        /// 停止相机回调流程专用工作线程。
        /// </summary>
        private void StopCallbackWorker()
        {
            Thread workerThread;
            lock (_callbackWorkerLock)
            {
                _callbackWorkerStopping = true;
                workerThread = _callbackWorkerThread;
                _callbackWorkerSignal.Set();
                _callbackWorkerThread = null;
            }

            if (workerThread != null && workerThread.IsAlive && !ReferenceEquals(Thread.CurrentThread, workerThread))
            {
                if (!workerThread.Join(500))
                    LogHelper.AddLog(MsgLevel.Warn, $"流程【{Process?.ProcessName}】相机回调专用线程停止等待超时。", true);
            }

            ClearPendingCallbackMat();
            ReleaseCallbackRunState();
        }

        /// <summary>
        /// 为跳过执行的图像源节点准备当前流程批次的回调图像结果。
        /// </summary>
        public bool TryPrepareSkippedRunResult(DateTime startTime, out string message)
        {
            message = string.Empty;
            Stopwatch performanceStopwatch = Stopwatch.StartNew();
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【链路诊断-图像源回调结果入口】{GetCallbackResultContextText()}；主程序={PerformanceSpikeDiagnostics.GetCurrentProcessPathText()}；节点程序集={PerformanceSpikeDiagnostics.GetAssemblyText(this)}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                true);
            Mat callbackMat = TakePendingCallbackMat();
            long afterTakeMat = performanceStopwatch.ElapsedMilliseconds;
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【链路诊断-图像源取回调Mat】{GetCallbackResultContextText()}；取回调Mat={afterTakeMat}ms；Mat={PerformanceSpikeDiagnostics.GetMatText(callbackMat)}",
                true);
            if (callbackMat == null || callbackMat.Empty())
            {
                callbackMat?.Dispose();
                message = "没有可用的相机回调图像。";
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                long afterFailedStatus = performanceStopwatch.ElapsedMilliseconds;
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"【链路诊断-图像源回调结果失败出口】{GetCallbackResultContextText()}；取回调Mat={afterTakeMat}ms；状态={afterFailedStatus - afterTakeMat}ms；总耗时={afterFailedStatus}ms；状态耗时={failedTime}ms；原因={message}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                    true);
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"【性能诊断-图像源回调结果】节点({ID}.{NodeName}) 准备失败；取回调Mat={afterTakeMat}ms；状态={afterFailedStatus - afterTakeMat}ms；总耗时={afterFailedStatus}ms；状态耗时={failedTime}ms；原因={message}",
                    true);
                return false;
            }

            string callbackMatText = PerformanceSpikeDiagnostics.GetMatText(callbackMat);
            NodeResultImageSource result = Result as NodeResultImageSource;
            if (result == null)
            {
                result = new NodeResultImageSource();
                Result = result;
            }
            long afterEnsureResult = performanceStopwatch.ElapsedMilliseconds;

            result.OutputImage = BuildOutputImage(callbackMat);
            long afterBuildOutput = performanceStopwatch.ElapsedMilliseconds;
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【链路诊断-图像源构建输出】{GetCallbackResultContextText()}；结果对象={afterEnsureResult - afterTakeMat}ms；构建输出={afterBuildOutput - afterEnsureResult}ms；输入={callbackMatText}；输出={PerformanceSpikeDiagnostics.GetOutputImageText(result.OutputImage)}",
                true);
            int time = SetRunResult(startTime, NodeStatus.Successful);
            long afterSetStatus = performanceStopwatch.ElapsedMilliseconds;
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【链路诊断-图像源状态写入】{GetCallbackResultContextText()}；状态={afterSetStatus - afterBuildOutput}ms；状态耗时={time}ms；ResultBeforeAssign={PerformanceSpikeDiagnostics.GetAssemblyText(result)}",
                true);
            result.RunTime = time;
            Result = result;
            long afterAssignResult = performanceStopwatch.ElapsedMilliseconds;
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【链路诊断-图像源回调结果出口】{GetCallbackResultContextText()}；总耗时={afterAssignResult}ms；状态耗时={time}ms；结果={PerformanceSpikeDiagnostics.GetAssemblyText(Result)}；输出={PerformanceSpikeDiagnostics.GetOutputImageText(result.OutputImage)}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                true);
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【性能诊断-图像源回调结果】节点({ID}.{NodeName}) 取回调Mat={afterTakeMat}ms；结果对象={afterEnsureResult - afterTakeMat}ms；构建输出={afterBuildOutput - afterEnsureResult}ms；状态={afterSetStatus - afterBuildOutput}ms；结果赋值={afterAssignResult - afterSetStatus}ms；总耗时={afterAssignResult}ms；状态耗时={time}ms；输入={callbackMatText}；输出={PerformanceSpikeDiagnostics.GetOutputImageText(result.OutputImage)}",
                true);
            if (PerformanceSpikeDiagnostics.ShouldLog(
                PerformanceSpikeDiagnostics.CommonSlowMs,
                afterAssignResult,
                afterTakeMat,
                afterEnsureResult - afterTakeMat,
                afterBuildOutput - afterEnsureResult,
                afterSetStatus - afterBuildOutput,
                afterAssignResult - afterSetStatus))
            {
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【慢诊断-图像源回调结果】节点({ID}.{NodeName}) 取回调Mat={afterTakeMat}ms；结果对象={afterEnsureResult - afterTakeMat}ms；构建输出={afterBuildOutput - afterEnsureResult}ms；状态={afterSetStatus - afterBuildOutput}ms；结果赋值={afterAssignResult - afterSetStatus}ms；总耗时={afterAssignResult}ms；状态耗时={time}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                    true);
            }

            return true;
        }

        /// <summary>
        /// 生成图像源回调结果链路上下文，方便把同一次流程运行中的多条日志串起来。
        /// </summary>
        /// <returns>流程、批次和节点上下文文本。</returns>
        private string GetCallbackResultContextText()
        {
            return $"流程={Process?.ProcessName}；RunId={Process?.CurrentRunId}；节点=({ID}.{NodeName})";
        }

        /// <summary>
        /// 构建图像源输出，统一提供原图、兼容旧节点的图像列表和灰度图缓存。
        /// </summary>
        private static OutputImage BuildOutputImage(Mat source)
        {
            return OutputImage.FromSingleImage(source);
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
                return BuildOutputImage(mat);

            if (data is Bitmap bitmap)
                return BuildOutputImage(bitmap.ToMat());

            if (data is IEnumerable<Mat> matList)
                return BuildOutputImageList(matList);

            if (data is IEnumerable<Bitmap> bitmapList)
                return BuildOutputImageList(bitmapList);

            string typeName = data == null ? "空值" : data.GetType().Name;
            throw new Exception($"共享变量“{variableName}”的类型为{typeName}，不是可用图像类型！");
        }

        /// <summary>
        /// 修正共享变量中已有的图像源输出，保证原图、图像列表和灰度缓存可用。
        /// </summary>
        /// <param name="outputImage">共享变量中的图像源输出。</param>
        /// <returns>归一化后的图像源输出。</returns>
        private static OutputImage NormalizeOutputImage(OutputImage outputImage)
        {
            if (outputImage == null)
                return new OutputImage();

            if (outputImage.Bitmaps == null)
                outputImage.Bitmaps = new List<Mat>();

            if (outputImage.Bitmaps.Count == 0)
            {
                if (OutputImage.HasValidImage(outputImage.SrcImg))
                    outputImage.Bitmaps.Add(outputImage.SrcImg);
            }
            else if (!OutputImage.HasValidImage(outputImage.Bitmaps[0]) && OutputImage.HasValidImage(outputImage.SrcImg))
            {
                outputImage.Bitmaps[0] = outputImage.SrcImg;
            }

            if (!OutputImage.HasValidImage(outputImage.SrcImg) && outputImage.Bitmaps.Count > 0)
                outputImage.SrcImg = outputImage.Bitmaps[0];

            if (!OutputImage.HasValidImage(outputImage.GrayImg))
                outputImage.GrayImg = OutputImage.BuildGrayImage(outputImage.SrcImg);

            if (outputImage.Rectangles == null)
                outputImage.Rectangles = new List<Rect>();

            return outputImage;
        }

        /// <summary>
        /// 用 Mat 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Mat 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Mat> images)
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

            return new OutputImage
            {
                SrcImg = mats[0],
                Bitmaps = mats,
                GrayImg = OutputImage.BuildGrayImage(mats[0])
            };
        }

        /// <summary>
        /// 用 Bitmap 列表构建图像源输出。
        /// </summary>
        /// <param name="images">Bitmap 图像列表。</param>
        /// <returns>图像源输出对象。</returns>
        private static OutputImage BuildOutputImageList(IEnumerable<Bitmap> images)
        {
            List<Mat> mats = new List<Mat>();
            if (images != null)
            {
                foreach (Bitmap image in images)
                {
                    if (image != null)
                        mats.Add(image.ToMat());
                }
            }

            return BuildOutputImageList(mats);
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
        /// 保存待处理回调图像，并释放未消费的旧帧。
        /// </summary>
        private void StorePendingCallbackMat(Mat mat)
        {
            Mat oldMat = Interlocked.Exchange(ref _pendingCallbackMat, mat);
            oldMat?.Dispose();
        }

        /// <summary>
        /// 取走待处理回调图像，调用方接管 Mat 生命周期。
        /// </summary>
        private Mat TakePendingCallbackMat()
        {
            return Interlocked.Exchange(ref _pendingCallbackMat, null);
        }

        /// <summary>
        /// 清理未被流程消费的回调图像。
        /// </summary>
        private void ClearPendingCallbackMat()
        {
            Mat mat = Interlocked.Exchange(ref _pendingCallbackMat, null);
            mat?.Dispose();
        }

        /// <summary>
        /// 尝试进入相机回调运行态，不等待上一帧，失败时调用方直接丢弃当前帧。
        /// </summary>
        /// <returns>成功进入运行态返回 true。</returns>
        private bool TryEnterCallbackRunState()
        {
            return Interlocked.CompareExchange(ref _callbackRunState, 1, 0) == 0;
        }

        /// <summary>
        /// 释放相机回调运行态。
        /// </summary>
        private void ReleaseCallbackRunState()
        {
            Volatile.Write(ref _callbackRunState, 0);
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
                            if (param.Camera == null) throw new Exception("相机对象无效！");
                            if (!param.Camera.IsOpen) throw new Exception("相机尚未连接！");

                            // 是否需要每次设置相机参数
                            if (param.IsEveryTime)
                            {
                                //TODO: param.Camera.SetTriggerMode(true);
                                param.Camera.SetTriggerDelay(param.TriggerDelay);
                                param.Camera.SetExposureTime(param.ExposureTime);
                                param.Camera.SetGain(param.Gain);
                                param.Camera.GetImageTimeOut = param.TimeOut;
                                param.Camera.SetTriggerSource(param.TriggerSource);
                                param.Camera.SetTriggerEdge(param.TriggerEdge);
                            }
                            try
                            {
                                // 获取一帧图像并设置结果
                                using (var img = param.Camera.GetOneFrameImage())
                                {
                                    res.OutputImage = BuildOutputImage(img?.ToMat());
                                }
                            }
                            catch (Exception ex)
                            {
                                // 获取一帧图像并设置结果
                                res.OutputImage = BuildOutputImage(null);
                            }
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
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception e)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{e.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败！");
                    }
                }
            }

            return new NodeReturn(NodeRunFlag.StopRun);
        }
    }
}
