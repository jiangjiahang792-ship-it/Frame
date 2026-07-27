using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

                List<PointRegionDistanceTargetResult> items = form.ExecuteMeasures(param, token);
                NodeResultPointRegionDistance nodeResult = BuildResult(items);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})点到区域距离完成！({time} ms，目标：{items.Count}，总体：{(nodeResult.IsOk ? "OK" : "NG")})", true);

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

        /// <summary>根据全部模板目标结果构建节点汇总结果。</summary>
        internal static NodeResultPointRegionDistance BuildResult(List<PointRegionDistanceTargetResult> items)
        {
            var result = new NodeResultPointRegionDistance();
            result.Items = items ?? new List<PointRegionDistanceTargetResult>();
            result.IsOk = result.Items.Count > 0 && result.Items.All(item => item.IsOk);
            result.JudgeOk = result.IsOk;
            result.AlgorithmMs = MeasurementResultRounder.Round(result.Items.Sum(item => item.AlgorithmMs));
            PointRegionDistanceTargetResult first = result.Items.FirstOrDefault();
            if (first != null)
            {
                result.MinDistance = first.MinDistance;
                result.MaxDistance = first.MaxDistance;
                result.IsInsideRegion = first.IsInsideRegion;
                result.TargetX = first.TargetX;
                result.TargetY = first.TargetY;
                result.NearestX = first.NearestX;
                result.NearestY = first.NearestY;
                result.FarthestX = first.FarthestX;
                result.FarthestY = first.FarthestY;
                result.RegionPointCount = first.RegionPointCount;
            }

            result.Result = BuildDisplayResult(result.Items);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>合并全部模板目标的点到区域距离叠加结果。</summary>
        internal static AlgorithmResult BuildDisplayResult(IReadOnlyList<PointRegionDistanceTargetResult> items)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = items != null && items.Count > 0 && items.All(item => item.IsOk);
            if (items == null)
                return result;

            foreach (PointRegionDistanceTargetResult item in items)
            {
                MeasurementNodeHelper.AppendAlgorithmResult(result, BuildDisplayResult(item.RawResult));
                result.Texts.Add(new ColorText(
                    item.IsOk
                        ? $"目标{item.TargetIndex} 点到区域：最小 {item.MinDistance:F3}px，最大 {item.MaxDistance:F3}px"
                        : $"目标{item.TargetIndex} 点到区域失败：数值0，原因：{item.ErrorMessage}",
                    item.IsOk ? Color.Lime : Color.Red));
            }
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
