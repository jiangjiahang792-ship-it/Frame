using Logger;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate
{
    /// <summary>
    /// 图像旋转节点，负责从上游订阅图像并输出旋转后的图像。
    /// </summary>
    public class NodeImageRotate : NodeBase
    {
        /// <summary>
        /// 初始化图像旋转节点及其参数窗体、运行结果对象。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeImageRotate(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormImageRotate(process, this);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageRotate();
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

            if (ParamForm is NodeParamFormImageRotate form)
            {
                if (ParamForm.Params is NodeParamImageRotate param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);
                        NodeResultImageRotate nodeResultImageRotate = new NodeResultImageRotate();
                        Result = nodeResultImageRotate;

                        Stopwatch performanceStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                        OutputImage inputImage = form.GetInputOutputImage();
                        long afterGetOutputImage = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        Mat srcImg = form.GetInputMat(inputImage); 
                        long afterGetSource = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        Mat rotatedImage = form.ImageRotateForRun(srcImg, param.Angle);
                        nodeResultImageRotate.OutputImage.TakeOwnership(rotatedImage);
                        long afterRotateColor = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        Mat inputGrayImage = form.GetInputGrayMat(inputImage);
                        long afterGetGray = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        Mat rotatedGrayImage = srcImg.Channels() == 1
                            ? rotatedImage
                            : OutputImage.HasValidImage(inputGrayImage)
                                ? form.ImageRotateForRun(inputGrayImage, param.Angle)
                                : OutputImage.BuildGrayImage(rotatedImage);
                        nodeResultImageRotate.OutputImage.TakeOwnership(rotatedGrayImage);
                        long afterRotateGray = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        nodeResultImageRotate.OutputImage.SrcImg = rotatedImage;
                        nodeResultImageRotate.OutputImage.Bitmaps = new System.Collections.Generic.List<Mat> { rotatedImage };
                        nodeResultImageRotate.OutputImage.GrayImg = rotatedGrayImage;
                        long afterBuildOutput = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        nodeResultImageRotate.RunTime = time;
                        Result = nodeResultImageRotate;
                        if (performanceStopwatch != null)
                        {
                            PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                                MsgLevel.Debug,
                                PerformanceSpikeDiagnostics.CommonSlowMs,
                                () => $"【慢诊断-图像旋转】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；角度={param.Angle}；输入={PerformanceSpikeDiagnostics.GetOutputImageText(inputImage)}；源图={PerformanceSpikeDiagnostics.GetMatText(srcImg)}；灰度图={PerformanceSpikeDiagnostics.GetMatText(inputGrayImage)}；取订阅={afterGetOutputImage}ms；取源图={afterGetSource - afterGetOutputImage}ms；旋转彩图={afterRotateColor - afterGetSource}ms；取灰度={afterGetGray - afterRotateColor}ms；灰度处理={afterRotateGray - afterGetGray}ms；构建输出={afterBuildOutput - afterRotateGray}ms；节点总耗时={afterBuildOutput}ms；状态耗时={time}ms；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                                true,
                                afterBuildOutput,
                                afterGetOutputImage,
                                afterGetSource - afterGetOutputImage,
                                afterRotateColor - afterGetSource,
                                afterGetGray - afterRotateColor,
                                afterRotateGray - afterGetGray,
                                afterBuildOutput - afterRotateGray);
                        }
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        Result = new NodeResultImageRotate();
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        Result = new NodeResultImageRotate();
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }
    }
}
