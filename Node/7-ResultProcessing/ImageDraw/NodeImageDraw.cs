using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    public class NodeImageDraw : NodeBase
    {
        public static event EventHandler<DatashowData> DataShow;

        public NodeImageDraw(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormImageDraw();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultImageDraw();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            IImageResourceLease inputLease = null;
            OutputImage pendingOutputImage = null;

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

            var param = (NodeParamImageDraw)ParamForm.Params;
            if (ParamForm is NodeParamFormImageDraw form)
            {
                try
                {
                    // 初始化运行状态
                    SetStatus(NodeStatus.Unexecuted, "*");
                    base.CheckTokenCancel(token);

                    Stopwatch performanceStopwatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                    //执行绘制
                    Mat image = form.GetImage(out OutputImage inputOwner);
                    inputLease = inputOwner.AcquireLease();
                    long afterGetImage = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                    AlgorithmResult aiResult = null;
                    AlgorithmResult colorResult=null;
                    try
                    {
                        aiResult = form.GetAIResults();
                        colorResult = form.GetColorResults();
                    }
                    catch { 
                        
                    }
                    long afterGetResults = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);

                    using (Bitmap imageBitmap = BitmapConverter.ToBitmap(image))
                    {
                        long afterToBitmap = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        ImageDrawer.DrawDetectionRectangles(imageBitmap, aiResult, param, colorResult);
                        long afterDraw = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);

                        // 输出绘制后的图像
                        Mat outputMat = BitmapConverter.ToMat(imageBitmap);
                        try
                        {
                            pendingOutputImage = new OutputImage
                            {
                                Bitmaps = new List<Mat> { outputMat }
                            };
                            pendingOutputImage.TakeOwnership(outputMat);
                            outputMat = null;
                        }
                        finally
                        {
                            outputMat?.Dispose();
                        }
                        long afterToMat = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(performanceStopwatch);
                        PerformanceSpikeDiagnostics.LogIfEnabled(
                            MsgLevel.Debug,
                            () => $"【性能诊断-AI结果绘制】流程={Process?.ProcessName}；TraceId={Process?.CurrentPerformanceTraceId}；RunId={Process?.CurrentRunId}；节点={ID}.{NodeName}；图像={GetMatDiagnosticText(image)}；AI结果={GetAlgorithmResultDiagnosticText(aiResult)}；颜色结果={GetAlgorithmResultDiagnosticText(colorResult)}；取图={afterGetImage}ms；取结果={afterGetResults - afterGetImage}ms；ToBitmap={afterToBitmap - afterGetResults}ms；绘制={afterDraw - afterToBitmap}ms；ToMat={afterToMat - afterDraw}ms；绘制总耗时={afterToMat}ms",
                            true);
                    }

                    var time = SetRunResult(startTime, NodeStatus.Successful);
                    var nodeResult = new NodeResultImageDraw
                    {
                        RunTime = time,
                        OutputImage = pendingOutputImage
                    };
                    Result = nodeResult;
                    pendingOutputImage = null;
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
                finally
                {
                    pendingOutputImage?.Dispose();
                    inputLease?.Dispose();
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }

        /// <summary>
        /// 生成 Mat 图像尺寸诊断文本。
        /// </summary>
        /// <param name="mat">待诊断图像。</param>
        /// <returns>图像尺寸和通道信息。</returns>
        private static string GetMatDiagnosticText(Mat mat)
        {
            if (mat == null)
                return "空";
            if (mat.Empty())
                return "空Mat";

            double megaBytes = mat.Width * mat.Height * Math.Max(1, mat.Channels()) / 1024D / 1024D;
            return $"{mat.Width}x{mat.Height}x{mat.Channels()}，约{megaBytes:F2}MB";
        }

        /// <summary>
        /// 生成算法显示结果数量诊断文本。
        /// </summary>
        /// <param name="result">算法显示结果。</param>
        /// <returns>各类绘制元素数量。</returns>
        private static string GetAlgorithmResultDiagnosticText(AlgorithmResult result)
        {
            if (result == null)
                return "空";

            int ngRectCount = result.RectsNgMap == null ? 0 : result.RectsNgMap.Values.Where(v => v != null).Sum(v => v.Count);
            return $"矩形={result.Rects?.Count ?? 0},NG矩形={ngRectCount},线={result.Lines?.Count ?? 0},轮廓={result.Contours?.Count ?? 0},文本={result.Texts?.Count ?? 0}";
        }
    }
    /// <summary>
    /// 图像绘制类
    /// </summary>
    internal static class ImageDrawer
    {
        /// <summary>
        /// 文字距离图像边缘的默认边距。
        /// </summary>
        private const int TextMargin = 30;

        /// <summary>
        /// 在图像上根据检测结果绘制 ROI 和文字，实际渲染复用 ShowImageControl 的 ROI 绘制方法。
        /// </summary>
        /// <param name="sourceBitmap">需要写入标注的源位图。</param>
        /// <param name="detectResults">AI 检测结果。</param>
        /// <param name="parm">绘制参数。</param>
        /// <param name="colorResults">颜色识别结果。</param>
        public static void DrawDetectionRectangles(Bitmap sourceBitmap, AlgorithmResult detectResults, NodeParamImageDraw parm, AlgorithmResult colorResults=null)
        {
            if (sourceBitmap == null)
                throw new ArgumentNullException(nameof(sourceBitmap));
            if (parm == null)
                throw new ArgumentNullException(nameof(parm));

            AlgorithmResult displayResult = BuildDisplayResult(detectResults, colorResults, parm);
            ShowImageControl.DrawDisplayResultToBitmap(sourceBitmap, displayResult);
        }

        /// <summary>
        /// 合并 AI 与颜色识别结果，统一套用当前节点的文字位置、字号和线宽设置。
        /// </summary>
        /// <param name="detectResults">AI 检测结果。</param>
        /// <param name="colorResults">颜色识别结果。</param>
        /// <param name="parm">绘制参数。</param>
        /// <returns>供 ShowImageControl 渲染的显示结果。</returns>
        private static AlgorithmResult BuildDisplayResult(AlgorithmResult detectResults, AlgorithmResult colorResults, NodeParamImageDraw parm)
        {
            AlgorithmResult result = new AlgorithmResult();
            AppendAlgorithmResult(result, colorResults, parm);
            AppendAlgorithmResult(result, detectResults, parm);
            return result;
        }

        /// <summary>
        /// 追加单个上游算法结果中的可绘制元素。
        /// </summary>
        /// <param name="target">合并后的目标结果。</param>
        /// <param name="source">上游算法结果。</param>
        /// <param name="parm">绘制参数。</param>
        private static void AppendAlgorithmResult(AlgorithmResult target, AlgorithmResult source, NodeParamImageDraw parm)
        {
            if (target == null || source == null)
                return;

            float lineWidth = Math.Max(1, parm.LineWidth);
            DisplayTextPosition textPosition = ToDisplayTextPosition(parm.TextLoc);

            if (source.Rects != null)
            {
                foreach (ColorRotatedRect rect in source.Rects)
                    AppendRotatedRect(target.Rects, rect, lineWidth);
            }

            if (source.RectsNgMap != null)
            {
                foreach (KeyValuePair<string, List<ColorRotatedRect>> pair in source.RectsNgMap)
                {
                    if (pair.Value == null)
                        continue;

                    if (!target.RectsNgMap.TryGetValue(pair.Key, out List<ColorRotatedRect> rects))
                    {
                        rects = new List<ColorRotatedRect>();
                        target.RectsNgMap[pair.Key] = rects;
                    }

                    foreach (ColorRotatedRect rect in pair.Value)
                        AppendRotatedRect(rects, rect, lineWidth);
                }
            }

            if (source.Lines != null)
            {
                foreach (ColorLine line in source.Lines)
                {
                    if (line == null)
                        continue;

                    target.Lines.Add(new ColorLine(line.P1, line.P2, line.Color)
                    {
                        LineWidth = lineWidth
                    });
                }
            }

            if (source.Texts != null)
            {
                foreach (ColorText textInfo in source.Texts)
                {
                    if (textInfo == null)
                        continue;

                    target.Texts.Add(new ColorText(textInfo.Text, textInfo.Color)
                    {
                        FontSize = Math.Max(1, parm.FontSize),
                        Margin = TextMargin,
                        Position = textPosition,
                        Title = string.Empty
                    });
                }
            }

            if (source.Circles != null)
            {
                foreach (ColorCircle circleInfo in source.Circles)
                {
                    if (circleInfo == null)
                        continue;

                    target.Circles.Add(new ColorCircle(circleInfo.Center, (int)Math.Round(circleInfo.Radius), circleInfo.Color)
                    {
                        Radius = circleInfo.Radius,
                        LineWidth = lineWidth
                    });
                }
            }

            if (source.Ellipses != null)
            {
                foreach (ColorEllipse ellipseInfo in source.Ellipses)
                {
                    if (ellipseInfo == null)
                        continue;

                    target.Ellipses.Add(new ColorEllipse(ellipseInfo.Center, ellipseInfo.Width, ellipseInfo.Height, ellipseInfo.Angle, ellipseInfo.Color)
                    {
                        LineWidth = lineWidth
                    });
                }
            }

            if (source.Contours != null)
            {
                foreach (ColorContour contourInfo in source.Contours)
                {
                    if (contourInfo == null)
                        continue;

                    List<PointF> points = contourInfo.Points == null ? new List<PointF>() : new List<PointF>(contourInfo.Points);
                    target.Contours.Add(new ColorContour(points, contourInfo.Color)
                    {
                        LineWidth = lineWidth
                    });
                }
            }
        }

        /// <summary>
        /// 追加旋转矩形并套用当前节点线宽。
        /// </summary>
        /// <param name="target">目标矩形列表。</param>
        /// <param name="source">源矩形。</param>
        /// <param name="lineWidth">绘制线宽。</param>
        private static void AppendRotatedRect(List<ColorRotatedRect> target, ColorRotatedRect source, float lineWidth)
        {
            if (target == null || source == null)
                return;

            target.Add(new ColorRotatedRect(source.RotatedRect)
            {
                Color = source.Color,
                LineWidth = lineWidth
            });
        }

        /// <summary>
        /// 将旧绘制节点的文字位置转换为显示控件的四角位置。
        /// </summary>
        /// <param name="textLoc">旧绘制节点文字位置。</param>
        /// <returns>显示控件文字位置。</returns>
        private static DisplayTextPosition ToDisplayTextPosition(TextLoc textLoc)
        {
            return textLoc == TextLoc.RightDown ? DisplayTextPosition.BottomRight : DisplayTextPosition.TopLeft;
        }
    }
}
