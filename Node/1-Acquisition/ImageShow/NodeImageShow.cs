using Logger;
using OpenCvSharp.Extensions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{

    public partial class NodeImageShow : NodeBase
    {
        /// <summary>所有图像显示节点共享、但按窗口分别计时的可替换帧率门控器。</summary>
        private static IUiFrameRateLimiter _uiFrameRateLimiter = new PerWindowUiFrameRateLimiter();

        public static event EventHandler<ImageShowPamra> ImageShowChanged;
        public static event Action<Process, string> ImageShowWindowNameChanged;

        /// <summary>
        /// 获取或替换图像显示帧率策略，便于测试和后续插件扩展。
        /// </summary>
        public static IUiFrameRateLimiter UiFrameRateLimiter
        {
            get => Volatile.Read(ref _uiFrameRateLimiter);
            set => Interlocked.Exchange(
                ref _uiFrameRateLimiter,
                value ?? new PerWindowUiFrameRateLimiter());
        }

        /// <summary>
        /// 发布图像刷新事件，供普通图像显示节点和 3D 深度图显示节点复用同一套图像窗口。
        /// </summary>
        /// <param name="sender">事件来源节点。</param>
        /// <param name="windowName">图像窗口名称。</param>
        /// <param name="bitmap">待显示图像。</param>
        /// <param name="displayResult">可选的显示叠加结果。</param>
        /// <param name="traceContext">可选的跨线程流程诊断上下文。</param>
        /// <returns>目标窗口确认接管Bitmap返回 true；否则返回 false并由发布器释放。</returns>
        public static bool PublishImageShowChanged(
            object sender,
            string windowName,
            Bitmap bitmap,
            AlgorithmResult displayResult = null,
            PerformanceTraceContext traceContext = null)
        {
            if (traceContext == null && sender is NodeBase sourceNode)
                traceContext = PerformanceTraceContext.CreateIfEnabled(sourceNode.Process, sourceNode.ID, sourceNode.NodeName);

            ImageShowPamra eventArgs = null;
            try
            {
                eventArgs = new ImageShowPamra(windowName, bitmap, displayResult, traceContext);
                bitmap = null;
                EventHandler<ImageShowPamra> handlers = ImageShowChanged;
                if (handlers == null)
                    return false;

                foreach (EventHandler<ImageShowPamra> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(sender, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        if (eventArgs.HasUnconfirmedBitmapClaim)
                            eventArgs.DisposeUnclaimedBitmap();
                        LogHelper.AddLog(MsgLevel.Warn, "图像显示订阅者刷新失败：" + ex.Message, true);
                    }
                }

                return eventArgs.IsBitmapClaimed;
            }
            finally
            {
                eventArgs?.DisposeUnclaimedBitmap();
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 在Bitmap转换前尝试取得指定窗口本次显示许可。
        /// </summary>
        /// <param name="windowName">目标图像窗口名称。</param>
        /// <param name="maximumFramesPerSecond">本轮采用的最大显示帧率。</param>
        /// <returns>允许转换并发布返回 true。</returns>
        public static bool TryAcquireWindowRefresh(string windowName, out int maximumFramesPerSecond)
        {
            maximumFramesPerSecond = Solution.Instance.CurrentResourceProfile?.UiMaxRefreshFps
                ?? PerWindowUiFrameRateLimiter.DefaultMaximumFramesPerSecond;
            IUiFrameRateLimiter limiter = UiFrameRateLimiter;
            return limiter == null || limiter.TryAcquire(
                FrmSingleImage.NormalizeWindowKey(windowName),
                maximumFramesPerSecond);
        }

        /// <summary>
        /// 清除指定窗口的帧率状态，使窗口重新打开后第一帧立即显示。
        /// </summary>
        /// <param name="windowName">图像窗口名称。</param>
        public static void ResetWindowRefreshState(string windowName)
        {
            UiFrameRateLimiter?.Reset(FrmSingleImage.NormalizeWindowKey(windowName));
        }

        /// <summary>
        /// 发布图像窗口绑定流程事件，用于同步窗口标题和后续手动调参上下文。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="windowName">图像窗口名称。</param>
        public static void PublishImageShowWindowNameChanged(Process process, string windowName)
        {
            ImageShowWindowNameChanged?.Invoke(process, FrmSingleImage.NormalizeWindowKey(windowName));
        }

        public NodeImageShow(int id, string nodeName, Process process, NodeType nodeType) : base(id, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormImageShow();
            Result = new NodeResultImageShow();
            ParamForm.SetNodeBelong(this);

            //if (ParamForm is ParamFormImageShow form)
            //{
            //    if (ParamForm.Params is NodeParamImageShow param)
            //    {
                    
            //    }
            //}
            
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            // 参数合法性校验
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

            if(ParamForm is ParamFormImageShow form)
            {
                if(ParamForm.Params is NodeParamImageShow param)
                {
                    try
                    {
                        param.WindowName = FrmSingleImage.NormalizeWindowKey(param.WindowName);
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        Stopwatch performanceStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                        OutputImage outputImage = form.GetOutputImage();
                        long afterGetOutput = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        OpenCvSharp.Mat firstMat = outputImage?.Bitmaps != null && outputImage.Bitmaps.Count > 0 ? outputImage.Bitmaps[0] : null;
                        if (firstMat == null || firstMat.Empty())
                            throw new Exception("未获取到可显示的图像！");

                        PerformanceTraceContext traceContext = PerformanceTraceContext.CreateIfEnabled(Process, ID, NodeName);
                        bool refreshGranted = TryAcquireWindowRefresh(param.WindowName, out int maximumFramesPerSecond);
                        string displayStrategy;
                        long afterToBitmap;
                        long afterImageEvent;
                        long afterWindowEvent;
                        if (refreshGranted)
                        {
                            Bitmap image;
                            using (ICpuWorkLease conversionLease = await Solution.Instance.AcquireCpuWorkAsync(
                                CpuWorkloadKind.ImageConversion,
                                token))
                            {
                                image = firstMat.ToBitmap();
                            }
                            afterToBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                            bool bitmapClaimed = PublishImageShowChanged(
                                this,
                                param.WindowName,
                                image,
                                outputImage.DisplayResult,
                                traceContext);
                            afterImageEvent = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                            if (bitmapClaimed)
                                PublishImageShowWindowNameChanged(Process, param.WindowName);
                            afterWindowEvent = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                            displayStrategy = bitmapClaimed ? "已发布" : "无人接管并释放";
                        }
                        else
                        {
                            afterToBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                            afterImageEvent = afterToBitmap;
                            afterWindowEvent = afterToBitmap;
                            displayStrategy = $"刷新限速跳过({maximumFramesPerSecond}FPS)";
                        }

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        long afterSetResult = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        ((NodeResultImageShow)Result).RunTime = time;
                        if (traceContext != null)
                        {
                            PerformanceSpikeDiagnostics.LogIfEnabled(
                                MsgLevel.Debug,
                                () => $"【性能诊断-图像显示】【完整耗时】{traceContext.ToLogText()}；阶段=显示节点投递完成；显示策略={displayStrategy}；图像={GetMatDiagnosticText(firstMat)}；订阅读取={afterGetOutput}ms；ToBitmap={afterToBitmap - afterGetOutput}ms；图像事件={afterImageEvent - afterToBitmap}ms；窗口事件={afterWindowEvent - afterImageEvent}ms；状态={afterSetResult - afterWindowEvent}ms；节点总耗时={afterSetResult}ms；状态耗时={time}ms",
                                true);
                            PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                                MsgLevel.Debug,
                                PerformanceSpikeDiagnostics.CommonSlowMs,
                                () => $"【慢诊断-图像显示】{traceContext.ToLogText()}；窗口={param.WindowName}；显示策略={displayStrategy}；输出图像={PerformanceSpikeDiagnostics.GetOutputImageText(outputImage)}；ToBitmap图像={PerformanceSpikeDiagnostics.GetMatText(firstMat)}；显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(outputImage.DisplayResult)}；订阅读取={afterGetOutput}ms；ToBitmap={afterToBitmap - afterGetOutput}ms；图像事件={afterImageEvent - afterToBitmap}ms；窗口事件={afterWindowEvent - afterImageEvent}ms；状态={afterSetResult - afterWindowEvent}ms；节点总耗时={afterSetResult}ms；状态耗时={time}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                                true,
                                afterSetResult,
                                afterGetOutput,
                                afterToBitmap - afterGetOutput,
                                afterImageEvent - afterToBitmap,
                                afterWindowEvent - afterImageEvent,
                                afterSetResult - afterWindowEvent);
                        }
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

        /// <summary>
        /// 生成图像显示性能诊断所需的Mat尺寸、通道和估算大小文本。
        /// </summary>
        /// <param name="mat">待显示图像。</param>
        /// <returns>图像诊断文本。</returns>
        private static string GetMatDiagnosticText(OpenCvSharp.Mat mat)
        {
            if (mat == null)
                return "空";

            try
            {
                if (mat.Empty())
                    return "空Mat";

                int channels = Math.Max(1, mat.Channels());
                double megaBytes = mat.Width * mat.Height * channels / 1024D / 1024D;
                return $"{mat.Width}x{mat.Height}x{channels}，约{megaBytes:F2}MB";
            }
            catch (Exception ex)
            {
                return "读取失败：" + ex.Message;
            }
        }
    }
}
