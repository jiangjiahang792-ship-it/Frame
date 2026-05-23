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

                CaliperLineMeasureResult measureResult = form.ExecuteMeasure(param, out Mat output);
                if (!measureResult.Success)
                    throw new Exception($"卡尺找线失败，边缘点数量：{measureResult.PointCount}");

                NodeResultCaliperLine nodeResult = BuildResult(measureResult, output);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，边缘点：{measureResult.PointCount})", true);

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

        internal static NodeResultCaliperLine BuildResult(CaliperLineMeasureResult measureResult, Mat output)
        {
            var result = new NodeResultCaliperLine();
            result.IsOk = measureResult.Success;
            result.EdgePointCount = measureResult.PointCount;
            result.StartX = measureResult.LineStart.X;
            result.StartY = measureResult.LineStart.Y;
            result.EndX = measureResult.LineEnd.X;
            result.EndY = measureResult.LineEnd.Y;
            result.Length = Distance(measureResult.LineStart, measureResult.LineEnd);
            result.Angle = Math.Atan2(measureResult.LineEnd.Y - measureResult.LineStart.Y, measureResult.LineEnd.X - measureResult.LineStart.X) * 180.0 / Math.PI;
            result.AlgorithmMs = measureResult.AlgorithmMs;
            result.OutputImage.Bitmaps = new List<Mat> { output };
            result.Result.Clear();
            result.Result.Lines.Add(new ColorLine(measureResult.LineStart, measureResult.LineEnd, Color.Lime));
            result.Result.Texts.Add(new ColorText($"卡尺找线：点数 {measureResult.PointCount}，长度 {result.Length:F2}px，角度 {result.Angle:F2}°", Color.Lime));
            return result;
        }

        private static double Distance(PointF p1, PointF p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
