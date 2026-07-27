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

                List<PointPointDistanceTargetResult> items = form.ExecuteMeasures(param, token);
                NodeResultPointPointDistance nodeResult = BuildResult(items);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})点到点距离完成！({time} ms，目标：{items.Count}，总体：{(nodeResult.IsOk ? "OK" : "NG")})", true);

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
        internal static NodeResultPointPointDistance BuildResult(List<PointPointDistanceTargetResult> items)
        {
            var result = new NodeResultPointPointDistance();
            result.Items = items ?? new List<PointPointDistanceTargetResult>();
            result.IsOk = result.Items.Count > 0 && result.Items.All(item => item.IsOk);
            result.JudgeOk = result.IsOk;
            result.AlgorithmMs = MeasurementResultRounder.Round(result.Items.Sum(item => item.AlgorithmMs));
            PointPointDistanceTargetResult first = result.Items.FirstOrDefault();
            if (first != null)
            {
                result.Distance = first.Distance;
                result.Point1X = first.Point1X;
                result.Point1Y = first.Point1Y;
                result.Point2X = first.Point2X;
                result.Point2Y = first.Point2Y;
            }

            result.Result = BuildDisplayResult(result.Items);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>合并全部模板目标的点到点距离叠加结果。</summary>
        internal static AlgorithmResult BuildDisplayResult(IReadOnlyList<PointPointDistanceTargetResult> items)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = items != null && items.Count > 0 && items.All(item => item.IsOk);
            if (items == null)
                return result;

            foreach (PointPointDistanceTargetResult item in items)
            {
                MeasurementNodeHelper.AppendAlgorithmResult(result, BuildDisplayResult(item.RawResult));
                result.Texts.Add(new ColorText(
                    item.IsOk
                        ? $"目标{item.TargetIndex} 点到点距离：{item.Distance:F3}px"
                        : $"目标{item.TargetIndex} 点到点距离失败：数值0，原因：{item.ErrorMessage}",
                    item.IsOk ? Color.Lime : Color.Red));
            }
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
