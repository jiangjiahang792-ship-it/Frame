using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    public class NodeLineLineAngle : NodeBase
    {
        public NodeLineLineAngle(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormLineLineAngle(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultLineLineAngle();
        }

        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
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

                var form = ParamForm as NodeParamFormLineLineAngle;
                var param = ParamForm.Params as NodeParamLineLineAngle;
                if (form == null || param == null)
                    throw new Exception("线到线夹角参数异常！");

                LineLineAngleMeasureResult measureResult = form.ExecuteMeasure(param, out Mat output);
                NodeResultLineLineAngle nodeResult = BuildResult(measureResult, output);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，夹角：{measureResult.Angle:F3}°", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})线到线夹角失败：{measureResult.Message}（{time} ms）", true);

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
        }

        internal static NodeResultLineLineAngle BuildResult(LineLineAngleMeasureResult measureResult, Mat output)
        {
            var result = new NodeResultLineLineAngle();
            result.IsOk = measureResult.Success;
            result.AlgorithmMs = measureResult.AlgorithmMs;
            if (measureResult.Success)
            {
                result.Angle = measureResult.Angle;
                result.Line1StartX = measureResult.Line1.Start.X;
                result.Line1StartY = measureResult.Line1.Start.Y;
                result.Line1EndX = measureResult.Line1.End.X;
                result.Line1EndY = measureResult.Line1.End.Y;
                result.Line2StartX = measureResult.Line2.Start.X;
                result.Line2StartY = measureResult.Line2.Start.Y;
                result.Line2EndX = measureResult.Line2.End.X;
                result.Line2EndY = measureResult.Line2.End.Y;
                if (measureResult.IntersectionPoint.HasValue)
                {
                    result.IntersectionX = measureResult.IntersectionPoint.Value.X;
                    result.IntersectionY = measureResult.IntersectionPoint.Value.Y;
                }
            }

            result.OutputImage.Bitmaps = new List<Mat> { output };
            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(LineLineAngleMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            foreach (PointF point in measureResult.Line1EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
            foreach (PointF point in measureResult.Line2EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Cyan));

            if (measureResult.Line1 != null && measureResult.Line1.IsValid)
                result.Lines.Add(new ColorLine(measureResult.Line1.Start, measureResult.Line1.End, measureResult.Success ? Color.Lime : Color.Red));
            if (measureResult.Line2 != null && measureResult.Line2.IsValid)
                result.Lines.Add(new ColorLine(measureResult.Line2.Start, measureResult.Line2.End, measureResult.Success ? Color.DeepSkyBlue : Color.Red));
            if (measureResult.IntersectionPoint.HasValue)
                result.Circles.Add(new ColorCircle(measureResult.IntersectionPoint.Value, 4, Color.Orange));

            if (measureResult.Success)
            {
                result.Texts.Add(new ColorText($"线到线夹角：{measureResult.Angle:F3}°", Color.Lime));
                if (measureResult.IntersectionPoint.HasValue)
                    result.Texts.Add(new ColorText($"交点：X={measureResult.IntersectionPoint.Value.X:F2}, Y={measureResult.IntersectionPoint.Value.Y:F2}", Color.Orange));
            }
            else
            {
                result.Texts.Add(new ColorText($"线到线夹角失败：{measureResult.Message}", Color.Red));
            }

            return result;
        }
    }

    internal class LineLineAngleMeasureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public double Angle { get; set; }
        public double AlgorithmMs { get; set; }
        public MeasuredLine Line1 { get; set; }
        public MeasuredLine Line2 { get; set; }
        public PointF? IntersectionPoint { get; set; }
        public List<PointF> Line1EdgePoints { get; set; } = new List<PointF>();
        public List<PointF> Line2EdgePoints { get; set; } = new List<PointF>();
    }
}
