using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    public class NodeCaliperLine : NodeBase
    {
        public NodeCaliperLine(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormCaliperLine(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultCaliperLine();
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

                var form = ParamForm as NodeParamFormCaliperLine;
                var param = ParamForm.Params as NodeParamCaliperLine;
                if (form == null || param == null)
                    throw new Exception("卡尺找线参数异常！");

                CaliperLineMeasureResult measureResult = form.ExecuteMeasure(param);
                if (measureResult == null)
                    throw new Exception("Caliper line measurement returned no result.");

                NodeResultCaliperLine nodeResult = BuildResult(measureResult);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，边缘点：{measureResult.PointCount})", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"Node({ID}.{NodeName}) caliper line found no valid line. ({time} ms, edge points: {measureResult.PointCount})", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        internal static NodeResultCaliperLine BuildResult(CaliperLineMeasureResult measureResult)
        {
            var result = new NodeResultCaliperLine();
            result.IsOk = measureResult.Success;
            result.JudgeOk = measureResult.Success;
            result.EdgePointCount = measureResult.PointCount;
            result.EdgePoints = MeasurementResultRounder.RoundPoints(measureResult.EdgePoints);
            if (measureResult.Success)
            {
                result.StartX = MeasurementResultRounder.Round(measureResult.LineStart.X);
                result.StartY = MeasurementResultRounder.Round(measureResult.LineStart.Y);
                result.EndX = MeasurementResultRounder.Round(measureResult.LineEnd.X);
                result.EndY = MeasurementResultRounder.Round(measureResult.LineEnd.Y);
                result.Length = MeasurementResultRounder.Round(Distance(measureResult.LineStart, measureResult.LineEnd));
                result.Angle = MeasurementResultRounder.Round(CalculateAbsoluteAngle(measureResult.LineStart, measureResult.LineEnd));
            }
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(CaliperLineMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            foreach (PointF point in measureResult.EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));

            if (measureResult.Success)
            {
                double length = Distance(measureResult.LineStart, measureResult.LineEnd);
                double angle = CalculateAbsoluteAngle(measureResult.LineStart, measureResult.LineEnd);
                result.Lines.Add(new ColorLine(measureResult.LineStart, measureResult.LineEnd, Color.Lime));
                result.Texts.Add(new ColorText($"卡尺找线：点数 {measureResult.PointCount}，长度 {length:F3}px，角度 {angle:F3}°", Color.Lime));
            }
            else
            {
                result.Texts.Add(new ColorText($"卡尺找线失败：点数 {measureResult.PointCount}", Color.Red));
            }

            return result;
        }

        /// <summary>
        /// 计算卡尺找线的绝对角度，避免订阅输出出现正负号跳变。
        /// </summary>
        private static double CalculateAbsoluteAngle(PointF start, PointF end)
        {
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI;
            return Math.Abs(angle);
        }

        private static double Distance(PointF p1, PointF p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
