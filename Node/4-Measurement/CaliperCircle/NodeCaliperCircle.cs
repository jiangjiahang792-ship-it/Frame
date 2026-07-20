using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperCircle
{
    public class NodeCaliperCircle : NodeBase
    {
        public NodeCaliperCircle(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormCaliperCircle(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultCaliperCircle();
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

                var form = ParamForm as NodeParamFormCaliperCircle;
                var param = ParamForm.Params as NodeParamCaliperCircle;
                if (form == null || param == null)
                    throw new Exception("卡尺找圆参数异常！");

                CaliperCircleMeasureResult measureResult = form.ExecuteMeasure(param);
                if (measureResult == null)
                    throw new Exception("Caliper circle measurement returned no result.");

                NodeResultCaliperCircle nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，半径：{nodeResult.Radius:F3}px)", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"Node({ID}.{NodeName}) caliper circle found no valid circle. ({time} ms, edge points: {measureResult.PointCount})", true);

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

        internal static NodeResultCaliperCircle BuildResult(CaliperCircleMeasureResult measureResult)
        {
            var result = new NodeResultCaliperCircle();
            result.IsOk = measureResult.Success;
            result.EdgePointCount = measureResult.PointCount;
            if (measureResult.Success)
            {
                result.CenterX = MeasurementResultRounder.Round(measureResult.Center.X);
                result.CenterY = MeasurementResultRounder.Round(measureResult.Center.Y);
                result.Radius = MeasurementResultRounder.Round(measureResult.Radius);
                result.Diameter = MeasurementResultRounder.Round(measureResult.Radius * 2.0);
            }
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(CaliperCircleMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;
            foreach (PointF point in measureResult.EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));

            if (measureResult.Success)
            {
                result.Circles.Add(new ColorCircle(measureResult.Center, (int)Math.Round(measureResult.Radius), Color.Lime));
                result.Circles.Add(new ColorCircle(measureResult.Center, 3, Color.Red));
                result.Texts.Add(new ColorText($"卡尺找圆：点数 {measureResult.PointCount}，圆心({measureResult.Center.X:F3}, {measureResult.Center.Y:F3})，半径 {measureResult.Radius:F3}px", Color.Lime));
            }
            else
            {
                string message = string.IsNullOrWhiteSpace(measureResult.ErrorMessage)
                    ? $"卡尺找圆失败：点数 {measureResult.PointCount}"
                    : measureResult.ErrorMessage;
                result.Texts.Add(new ColorText(message, Color.Red));
            }

            return result;
        }
    }
}
