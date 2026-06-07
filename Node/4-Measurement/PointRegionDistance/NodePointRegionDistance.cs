using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointRegionDistance
{
    public class NodePointRegionDistance : NodeBase
    {
        public NodePointRegionDistance(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormPointRegionDistance(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultPointRegionDistance();
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

                var form = ParamForm as NodeParamFormPointRegionDistance;
                var param = ParamForm.Params as NodeParamPointRegionDistance;
                if (form == null || param == null)
                    throw new Exception("点到区域距离参数异常！");

                PointRegionDistanceMeasureResult measureResult = form.ExecuteMeasure(param);
                NodeResultPointRegionDistance nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，最小距离：{measureResult.DistanceResult.MinDistance:F3}px", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})点到区域距离失败：{measureResult.Message}（{time} ms）", true);

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

        internal static NodeResultPointRegionDistance BuildResult(PointRegionDistanceMeasureResult measureResult)
        {
            var result = new NodeResultPointRegionDistance();
            result.IsOk = measureResult.Success;
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            if (measureResult.Success && measureResult.DistanceResult != null)
            {
                PointRegionDistanceResult distanceResult = measureResult.DistanceResult;
                result.MinDistance = MeasurementResultRounder.Round(distanceResult.MinDistance);
                result.MaxDistance = MeasurementResultRounder.Round(distanceResult.MaxDistance);
                result.IsInsideRegion = distanceResult.IsInsideRegion;
                result.TargetX = MeasurementResultRounder.Round(distanceResult.TargetPoint.X);
                result.TargetY = MeasurementResultRounder.Round(distanceResult.TargetPoint.Y);
                result.NearestX = MeasurementResultRounder.Round(distanceResult.NearestPoint.X);
                result.NearestY = MeasurementResultRounder.Round(distanceResult.NearestPoint.Y);
                result.FarthestX = MeasurementResultRounder.Round(distanceResult.FarthestPoint.X);
                result.FarthestY = MeasurementResultRounder.Round(distanceResult.FarthestPoint.Y);
                result.RegionPointCount = distanceResult.RegionPoints.Count;
            }

            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(PointRegionDistanceMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            foreach (PointF point in measureResult.PointEdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));

            if (measureResult.Success && measureResult.DistanceResult != null)
            {
                PointRegionDistanceResult distance = measureResult.DistanceResult;
                for (int index = 0; index < distance.RegionPoints.Count; index++)
                {
                    PointF a = distance.RegionPoints[index];
                    PointF b = distance.RegionPoints[(index + 1) % distance.RegionPoints.Count];
                    result.Lines.Add(new ColorLine(a, b, Color.Lime));
                    result.Circles.Add(new ColorCircle(a, 3, Color.Lime));
                }

                result.Circles.Add(new ColorCircle(distance.TargetPoint, 4, Color.Red));
                if (!distance.IsInsideRegion)
                {
                    result.Lines.Add(new ColorLine(distance.TargetPoint, distance.NearestPoint, Color.Orange));
                    result.Circles.Add(new ColorCircle(distance.NearestPoint, 3, Color.Orange));
                }

                result.Lines.Add(new ColorLine(distance.TargetPoint, distance.FarthestPoint, Color.Magenta));
                result.Circles.Add(new ColorCircle(distance.FarthestPoint, 3, Color.Magenta));
                result.Texts.Add(new ColorText($"点到区域：最小 {distance.MinDistance:F3}px，最大 {distance.MaxDistance:F3}px", Color.Lime));
                result.Texts.Add(new ColorText($"点在区域内：{(distance.IsInsideRegion ? "是" : "否")}", distance.IsInsideRegion ? Color.Lime : Color.Orange));
            }
            else
            {
                result.Texts.Add(new ColorText($"点到区域距离失败：{measureResult.Message}", Color.Red));
            }

            return result;
        }
    }

    internal class PointRegionDistanceMeasureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public double AlgorithmMs { get; set; }
        public PointRegionDistanceResult DistanceResult { get; set; }
        public List<PointF> PointEdgePoints { get; set; } = new List<PointF>();
    }
}
