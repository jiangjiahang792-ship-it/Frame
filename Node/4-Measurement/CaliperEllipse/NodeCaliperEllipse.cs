using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperEllipse
{
    public class NodeCaliperEllipse : NodeBase
    {
        public NodeCaliperEllipse(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormCaliperEllipse(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultCaliperEllipse();
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

                var form = ParamForm as NodeParamFormCaliperEllipse;
                var param = ParamForm.Params as NodeParamCaliperEllipse;
                if (form == null || param == null)
                    throw new Exception("卡尺找椭圆参数异常！");

                CaliperEllipseMeasureResult measureResult = form.ExecuteMeasure(param);
                if (measureResult == null)
                    throw new Exception("Caliper ellipse measurement returned no result.");

                NodeResultCaliperEllipse nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，点数：{measureResult.PointCount})", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"Node({ID}.{NodeName}) caliper ellipse found no valid ellipse. ({time} ms, edge points: {measureResult.PointCount})", true);

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

        internal static NodeResultCaliperEllipse BuildResult(CaliperEllipseMeasureResult measureResult)
        {
            var result = new NodeResultCaliperEllipse();
            result.IsOk = measureResult.Success;
            result.EdgePointCount = measureResult.PointCount;
            if (measureResult.Success)
            {
                result.CenterX = MeasurementResultRounder.Round(measureResult.Center.X);
                result.CenterY = MeasurementResultRounder.Round(measureResult.Center.Y);
                result.Width = MeasurementResultRounder.Round(measureResult.Size.Width);
                result.Height = MeasurementResultRounder.Round(measureResult.Size.Height);
                result.MajorAxis = MeasurementResultRounder.Round(Math.Max(measureResult.Size.Width, measureResult.Size.Height));
                result.MinorAxis = MeasurementResultRounder.Round(Math.Min(measureResult.Size.Width, measureResult.Size.Height));
                result.Angle = MeasurementResultRounder.Round(measureResult.Angle);
            }
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(CaliperEllipseMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;
            foreach (PointF point in measureResult.EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
            foreach (PointF point in measureResult.FailedPoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Red));

            if (measureResult.Success)
            {
                var center = new PointF(measureResult.Center.X, measureResult.Center.Y);
                result.Ellipses.Add(new ColorEllipse(center, measureResult.Size.Width, measureResult.Size.Height, measureResult.Angle, Color.Lime));
                result.Circles.Add(new ColorCircle(center, 3, Color.Red));
                result.Texts.Add(new ColorText($"卡尺找椭圆：点数 {measureResult.PointCount}，中心({measureResult.Center.X:F3}, {measureResult.Center.Y:F3})，宽高({measureResult.Size.Width:F3}, {measureResult.Size.Height:F3})，角度 {measureResult.Angle:F3}°", Color.Lime));
            }
            else
            {
                string message = string.IsNullOrWhiteSpace(measureResult.ErrorMessage)
                    ? $"卡尺找椭圆失败：点数 {measureResult.PointCount}"
                    : measureResult.ErrorMessage;
                result.Texts.Add(new ColorText(message, Color.Red));
            }

            return result;
        }
    }
}
