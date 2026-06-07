using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    /// <summary>
    /// 点到线距离测量节点。
    /// </summary>
    public class NodePointLineDistance : NodeBase
    {
        /// <summary>
        /// 创建点到线距离测量节点。
        /// </summary>
        public NodePointLineDistance(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormPointLineDistance(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultPointLineDistance();
        }

        /// <summary>
        /// 运行点到线距离测量。
        /// </summary>
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

                var form = ParamForm as NodeParamFormPointLineDistance;
                var param = ParamForm.Params as NodeParamPointLineDistance;
                if (form == null || param == null)
                    throw new Exception("点到线距离参数异常！");

                PointLineDistanceMeasureResult measureResult = form.ExecuteMeasure(param);
                NodeResultPointLineDistance nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，点到线距离：{measureResult.DistanceResult.Distance:F3}px", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})点到线距离失败：{measureResult.Message}，{time} ms", true);

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

        /// <summary>
        /// 将算法结果转换为节点结果。
        /// </summary>
        internal static NodeResultPointLineDistance BuildResult(PointLineDistanceMeasureResult measureResult)
        {
            var result = new NodeResultPointLineDistance
            {
                IsOk = measureResult.Success,
                AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs)
            };

            if (measureResult.Success && measureResult.DistanceResult != null)
            {
                PointLineDistanceResult distance = measureResult.DistanceResult;
                result.Distance = MeasurementResultRounder.Round(distance.Distance);
                result.PointX = MeasurementResultRounder.Round(distance.TargetPoint.X);
                result.PointY = MeasurementResultRounder.Round(distance.TargetPoint.Y);
                result.StartX = MeasurementResultRounder.Round(distance.LineStart.X);
                result.StartY = MeasurementResultRounder.Round(distance.LineStart.Y);
                result.EndX = MeasurementResultRounder.Round(distance.LineEnd.X);
                result.EndY = MeasurementResultRounder.Round(distance.LineEnd.Y);
                result.FootX = MeasurementResultRounder.Round(distance.FootPoint.X);
                result.FootY = MeasurementResultRounder.Round(distance.FootPoint.Y);
            }

            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>
        /// 创建预览和运行输出叠加图形。
        /// </summary>
        internal static AlgorithmResult BuildDisplayResult(PointLineDistanceMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            foreach (PointF point in measureResult.PointEdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
            foreach (PointF point in measureResult.LineEdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Cyan));

            if (measureResult.Success && measureResult.DistanceResult != null)
            {
                PointLineDistanceResult distance = measureResult.DistanceResult;
                result.Circles.Add(new ColorCircle(distance.TargetPoint, 4, Color.Lime));
                result.Circles.Add(new ColorCircle(distance.FootPoint, 4, Color.Orange));
                result.Lines.Add(new ColorLine(distance.LineStart, distance.LineEnd, Color.DeepSkyBlue));
                result.Lines.Add(new ColorLine(distance.TargetPoint, distance.FootPoint, Color.Orange));
                result.Texts.Add(new ColorText($"点到线距离：{distance.Distance:F3}px", Color.Lime));
                result.Texts.Add(new ColorText($"垂足=({distance.FootPoint.X:F3},{distance.FootPoint.Y:F3})", Color.Orange));
            }
            else
            {
                result.Texts.Add(new ColorText($"点到线距离失败：{measureResult.Message}", Color.Red));
            }

            return result;
        }
    }

    /// <summary>
    /// 点到线距离测量中间结果。
    /// </summary>
    internal class PointLineDistanceMeasureResult
    {
        /// <summary>
        /// 测量是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 失败信息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 算法耗时。
        /// </summary>
        public double AlgorithmMs { get; set; }

        /// <summary>
        /// 点到线距离结果。
        /// </summary>
        public PointLineDistanceResult DistanceResult { get; set; }

        /// <summary>
        /// 找点圆卡尺边缘点。
        /// </summary>
        public List<PointF> PointEdgePoints { get; set; } = new List<PointF>();

        /// <summary>
        /// 找线卡尺边缘点。
        /// </summary>
        public List<PointF> LineEdgePoints { get; set; } = new List<PointF>();
    }
}
