using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    /// <summary>
    /// 线组合拟合节点。
    /// </summary>
    public class NodeLineMergeFit : NodeBase
    {
        /// <summary>
        /// 创建线组合拟合节点。
        /// </summary>
        public NodeLineMergeFit(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormLineMergeFit(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultLineMergeFit();
        }

        /// <summary>
        /// 运行线组合拟合。
        /// </summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            NodeResultLineMergeFit pendingResult = null;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                var form = ParamForm as NodeParamFormLineMergeFit;
                var param = ParamForm.Params as NodeParamLineMergeFit;
                if (form == null || param == null)
                    throw new Exception("线组合拟合参数异常！");

                LineMergeFitMeasureResult measureResult = form.ExecuteMeasure(param, token, false, out Mat output);
                pendingResult = BuildResult(measureResult, output);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                pendingResult.RunTime = time;
                Result = pendingResult;
                NodeResultLineMergeFit nodeResult = pendingResult;
                pendingResult = null;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，线长：{nodeResult.Length:F3}px，角度：{nodeResult.Angle:F3}°", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})线组合拟合失败：{measureResult.Message}，{time} ms", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
            finally
            {
                NodeResultResourceManager.Release(pendingResult);
            }
        }

        /// <summary>
        /// 将算法结果转换为节点结果。
        /// </summary>
        internal static NodeResultLineMergeFit BuildResult(LineMergeFitMeasureResult measureResult, Mat output)
        {
            var result = new NodeResultLineMergeFit();
            try
            {
                result.OutputImage = BuildOutputImage(output);
                output = null;
                result.IsOk = measureResult.Success;
                result.Message = measureResult.Message;
                result.AlgorithmMs = measureResult.AlgorithmMs;
                result.AngleDifference = measureResult.AngleDifference;
                result.LineDistance = measureResult.LineDistance;
                result.FitError = measureResult.FitError;
                result.SourcePoints = MeasurementResultRounder.RoundPoints(measureResult.SourcePoints);

                if (measureResult.Success && measureResult.OutputLine != null)
                {
                    MeasuredLine line = measureResult.OutputLine;
                    result.StartX = line.Start.X;
                    result.StartY = line.Start.Y;
                    result.EndX = line.End.X;
                    result.EndY = line.End.Y;
                    result.CenterX = (line.Start.X + line.End.X) / 2.0;
                    result.CenterY = (line.Start.Y + line.End.Y) / 2.0;
                    result.Length = Distance(line.Start, line.End);
                    result.Angle = Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X) * 180.0 / Math.PI;
                }

                result.Result = BuildDisplayResult(measureResult);
                result.OutputImage.DisplayResult = result.Result;
                return result;
            }
            catch
            {
                NodeResultResourceManager.Release(result);
                output?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 创建预览和运行输出叠加图形。
        /// </summary>
        internal static AlgorithmResult BuildDisplayResult(LineMergeFitMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            if (measureResult.Line1 != null && measureResult.Line1.IsValid)
                result.Lines.Add(new ColorLine(measureResult.Line1.Start, measureResult.Line1.End, Color.DeepSkyBlue) { LineWidth = 1.5F });
            if (measureResult.Line2 != null && measureResult.Line2.IsValid)
                result.Lines.Add(new ColorLine(measureResult.Line2.Start, measureResult.Line2.End, Color.Cyan) { LineWidth = 1.5F });

            foreach (PointF point in measureResult.SourcePoints.Take(200))
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));

            if (measureResult.Success && measureResult.OutputLine != null)
            {
                result.Lines.Add(new ColorLine(measureResult.OutputLine.Start, measureResult.OutputLine.End, Color.Lime) { LineWidth = 3F });
                result.Texts.Add(new ColorText($"线组合拟合：误差 {measureResult.FitError:F3}px，角度差 {measureResult.AngleDifference:F3}°", Color.Lime)
                {
                    FontSize = 14,
                    Position = DisplayTextPosition.TopLeft,
                    Title = "LineMergeFit"
                });

                if (measureResult.LineDistance.HasValue)
                {
                    result.Texts.Add(new ColorText($"线间距：{measureResult.LineDistance.Value:F3}px", Color.Cyan)
                    {
                        FontSize = 13,
                        Position = DisplayTextPosition.TopLeft,
                        Margin = 34,
                        Title = "LineMergeFit.Distance"
                    });
                }
            }
            else
            {
                result.Texts.Add(new ColorText($"线组合拟合失败：{measureResult.Message}", Color.Red)
                {
                    FontSize = 14,
                    Position = DisplayTextPosition.TopLeft,
                    Title = "LineMergeFit"
                });
            }

            return result;
        }

        /// <summary>
        /// 构建输出图像容器。
        /// </summary>
        private static OutputImage BuildOutputImage(Mat output)
        {
            var image = new OutputImage();
            if (output != null)
            {
                if (!output.Empty())
                    image.Bitmaps = new List<Mat> { output };
                image.TakeOwnership(output);
            }
            return image;
        }

        /// <summary>
        /// 计算两点距离。
        /// </summary>
        private static double Distance(PointF p1, PointF p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
