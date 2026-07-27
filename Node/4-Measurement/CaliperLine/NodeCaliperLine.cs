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

                List<CaliperLineTargetResult> items = form.ExecuteMeasures(param, token);
                NodeResultCaliperLine nodeResult = BuildResult(items);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})卡尺找线完成！({time} ms，目标：{items.Count}，总体：{(nodeResult.IsOk ? "OK" : "NG")})", true);

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

        /// <summary>根据全部目标项构建节点汇总结果。</summary>
        internal static NodeResultCaliperLine BuildResult(List<CaliperLineTargetResult> items)
        {
            var result = new NodeResultCaliperLine();
            result.Items = items ?? new List<CaliperLineTargetResult>();
            result.IsOk = result.Items.Count > 0 && result.Items.All(item => item.IsOk);
            result.JudgeOk = result.IsOk;
            result.EdgePointCount = result.Items.Sum(item => item.EdgePointCount);
            result.EdgePoints = result.Items.SelectMany(item => item.EdgePoints ?? new List<PointF>()).ToList();
            CaliperLineTargetResult first = result.Items.FirstOrDefault();
            if (first != null)
            {
                result.StartX = first.StartX;
                result.StartY = first.StartY;
                result.EndX = first.EndX;
                result.EndY = first.EndY;
                result.Length = first.Length;
                result.Angle = first.Angle;
                result.AlgorithmMs = result.Items.Sum(item => item.AlgorithmMs);
            }
            result.Result = BuildDisplayResult(result.Items);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>合并全部模板目标的找线绘制结果。</summary>
        internal static AlgorithmResult BuildDisplayResult(IReadOnlyList<CaliperLineTargetResult> items)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = items != null && items.Count > 0 && items.All(item => item.IsOk);
            if (items == null)
                return result;

            foreach (CaliperLineTargetResult item in items)
            {
                foreach (PointF point in item.EdgePoints ?? new List<PointF>())
                    result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
                if (item.IsOk)
                {
                    var start = new PointF((float)item.StartX, (float)item.StartY);
                    var end = new PointF((float)item.EndX, (float)item.EndY);
                    result.Lines.Add(new ColorLine(start, end, Color.Lime));
                    result.Texts.Add(new ColorText($"目标{item.TargetIndex} 卡尺找线：点数 {item.EdgePointCount}，长度 {item.Length:F3}px，角度 {item.Angle:F3}°", Color.Lime));
                }
                else
                {
                    result.Texts.Add(new ColorText($"目标{item.TargetIndex} 卡尺找线失败：数值0，原因：{item.ErrorMessage}", Color.Red));
                }
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
