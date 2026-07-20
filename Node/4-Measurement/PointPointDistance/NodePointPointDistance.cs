using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointPointDistance
{
    public class NodePointPointDistance : NodeBase
    {
        public NodePointPointDistance(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormPointPointDistance(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultPointPointDistance();
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

                var form = ParamForm as NodeParamFormPointPointDistance;
                var param = ParamForm.Params as NodeParamPointPointDistance;
                if (form == null || param == null)
                    throw new Exception("点到点距离参数异常！");

                PointPointDistanceMeasureResult measureResult = form.ExecuteMeasure(param);
                NodeResultPointPointDistance nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，距离：{measureResult.Distance:F3}px", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})点到点距离失败：{measureResult.Message}（{time} ms）", true);

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

        internal static NodeResultPointPointDistance BuildResult(PointPointDistanceMeasureResult measureResult)
        {
            var result = new NodeResultPointPointDistance();
            result.IsOk = measureResult.Success;
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            if (measureResult.Success)
            {
                result.Distance = MeasurementResultRounder.Round(measureResult.Distance);
                result.Point1X = MeasurementResultRounder.Round(measureResult.Point1.X);
                result.Point1Y = MeasurementResultRounder.Round(measureResult.Point1.Y);
                result.Point2X = MeasurementResultRounder.Round(measureResult.Point2.X);
                result.Point2Y = MeasurementResultRounder.Round(measureResult.Point2.Y);
            }

            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(PointPointDistanceMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            foreach (PointF point in measureResult.Point1EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
            foreach (PointF point in measureResult.Point2EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Cyan));

            if (measureResult.Success)
            {
                result.Circles.Add(new ColorCircle(measureResult.Point1, 4, Color.Lime));
                result.Circles.Add(new ColorCircle(measureResult.Point2, 4, Color.DeepSkyBlue));
                result.Lines.Add(new ColorLine(measureResult.Point1, measureResult.Point2, Color.Orange));
                result.Texts.Add(new ColorText($"点到点距离：{measureResult.Distance:F3}px", Color.Lime));
                result.Texts.Add(new ColorText($"P1=({measureResult.Point1.X:F3},{measureResult.Point1.Y:F3}) P2=({measureResult.Point2.X:F3},{measureResult.Point2.Y:F3})", Color.White));
            }
            else
            {
                result.Texts.Add(new ColorText($"点到点距离失败：{measureResult.Message}", Color.Red));
            }

            return result;
        }
    }

    internal class PointPointDistanceMeasureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public double Distance { get; set; }
        public double AlgorithmMs { get; set; }
        public PointF Point1 { get; set; }
        public PointF Point2 { get; set; }
        public List<PointF> Point1EdgePoints { get; set; } = new List<PointF>();
        public List<PointF> Point2EdgePoints { get; set; } = new List<PointF>();
    }
}
