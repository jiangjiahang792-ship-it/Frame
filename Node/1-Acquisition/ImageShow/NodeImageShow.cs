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

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{

    public partial class NodeImageShow : NodeBase
    {
        public static event EventHandler<ImageShowPamra> ImageShowChanged;
        public static event Action<Process, string> ImageShowWindowNameChanged;

        /// <summary>
        /// 发布图像刷新事件，供普通图像显示节点和 3D 深度图显示节点复用同一套图像窗口。
        /// </summary>
        /// <param name="sender">事件来源节点。</param>
        /// <param name="windowName">图像窗口名称。</param>
        /// <param name="bitmap">待显示图像。</param>
        /// <param name="displayResult">可选的显示叠加结果。</param>
        public static void PublishImageShowChanged(object sender, string windowName, Bitmap bitmap, AlgorithmResult displayResult = null)
        {
            ImageShowChanged?.Invoke(sender, new ImageShowPamra(windowName, bitmap, displayResult));
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

                        Stopwatch performanceStopwatch = Stopwatch.StartNew();
                        OutputImage outputImage = form.GetOutputImage();
                        long afterGetOutput = performanceStopwatch.ElapsedMilliseconds;
                        OpenCvSharp.Mat firstMat = outputImage?.Bitmaps != null && outputImage.Bitmaps.Count > 0 ? outputImage.Bitmaps[0] : null;
                        string imageInfo = GetMatDiagnosticText(firstMat);
                        Bitmap image = firstMat.ToBitmap();
                        long afterToBitmap = performanceStopwatch.ElapsedMilliseconds;
                        PublishImageShowChanged(this, param.WindowName, image, outputImage.DisplayResult);
                        long afterImageEvent = performanceStopwatch.ElapsedMilliseconds;
                        PublishImageShowWindowNameChanged(Process, param.WindowName);
                        long afterWindowEvent = performanceStopwatch.ElapsedMilliseconds;
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        long afterSetResult = performanceStopwatch.ElapsedMilliseconds;
                        ((NodeResultImageShow)Result).RunTime = time;
                        if (showLog)
                        {
                            LogHelper.AddLog(
                                MsgLevel.Debug,
                                $"【性能诊断-图像显示】节点({ID}.{NodeName}) 图像={imageInfo}；订阅读取={afterGetOutput}ms；ToBitmap={afterToBitmap - afterGetOutput}ms；图像事件={afterImageEvent - afterToBitmap}ms；窗口事件={afterWindowEvent - afterImageEvent}ms；状态={afterSetResult - afterWindowEvent}ms；节点总耗时={afterSetResult}ms；状态耗时={time}ms",
                                true);
                            if (PerformanceSpikeDiagnostics.ShouldLog(
                                PerformanceSpikeDiagnostics.CommonSlowMs,
                                afterSetResult,
                                afterGetOutput,
                                afterToBitmap - afterGetOutput,
                                afterImageEvent - afterToBitmap,
                                afterWindowEvent - afterImageEvent,
                                afterSetResult - afterWindowEvent))
                            {
                                LogHelper.AddLog(
                                    MsgLevel.Debug,
                                    $"【慢诊断-图像显示】节点({ID}.{NodeName}) 窗口={param.WindowName}；输出图像={PerformanceSpikeDiagnostics.GetOutputImageText(outputImage)}；ToBitmap图像={PerformanceSpikeDiagnostics.GetMatText(firstMat)}；显示结果={PerformanceSpikeDiagnostics.GetAlgorithmResultText(outputImage.DisplayResult)}；订阅读取={afterGetOutput}ms；ToBitmap={afterToBitmap - afterGetOutput}ms；图像事件={afterImageEvent - afterToBitmap}ms；窗口事件={afterWindowEvent - afterImageEvent}ms；状态={afterSetResult - afterWindowEvent}ms；节点总耗时={afterSetResult}ms；状态耗时={time}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                                    true);
                            }

                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
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
