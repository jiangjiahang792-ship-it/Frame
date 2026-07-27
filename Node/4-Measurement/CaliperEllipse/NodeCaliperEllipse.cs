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

                List<CaliperEllipseTargetResult> items = form.ExecuteMeasures(param, token);
                NodeResultCaliperEllipse nodeResult = BuildResult(items);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})卡尺找椭圆完成！({time} ms，目标：{items.Count}，总体：{(nodeResult.IsOk ? "OK" : "NG")})", true);

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
        internal static NodeResultCaliperEllipse BuildResult(List<CaliperEllipseTargetResult> items)
        {
            var result = new NodeResultCaliperEllipse();
            result.Items = items ?? new List<CaliperEllipseTargetResult>();
            result.IsOk = result.Items.Count > 0 && result.Items.All(item => item.IsOk);
            result.JudgeOk = result.IsOk;
            result.EdgePointCount = result.Items.Sum(item => item.EdgePointCount);
            CaliperEllipseTargetResult first = result.Items.FirstOrDefault();
            if (first != null)
            {
                result.CenterX = first.CenterX;
                result.CenterY = first.CenterY;
                result.Width = first.Width;
                result.Height = first.Height;
                result.MajorAxis = first.MajorAxis;
                result.MinorAxis = first.MinorAxis;
                result.Angle = first.Angle;
                result.AlgorithmMs = result.Items.Sum(item => item.AlgorithmMs);
            }
            result.Result = BuildDisplayResult(result.Items);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>合并全部模板目标的找椭圆绘制结果。</summary>
        internal static AlgorithmResult BuildDisplayResult(IReadOnlyList<CaliperEllipseTargetResult> items)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = items != null && items.Count > 0 && items.All(item => item.IsOk);
            if (items == null)
                return result;
            foreach (CaliperEllipseTargetResult item in items)
            {
                foreach (PointF point in item.EdgePoints ?? new List<PointF>())
                    result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
                foreach (PointF point in item.FailedPoints ?? new List<PointF>())
                    result.Circles.Add(new ColorCircle(point, 2, Color.Red));
                if (item.IsOk)
                {
                    var center = new PointF((float)item.CenterX, (float)item.CenterY);
                    result.Ellipses.Add(new ColorEllipse(center, (float)item.Width, (float)item.Height, (float)item.Angle, Color.Lime));
                    result.Circles.Add(new ColorCircle(center, 3, Color.Red));
                    result.Texts.Add(new ColorText($"目标{item.TargetIndex} 卡尺找椭圆：点数 {item.EdgePointCount}，中心({item.CenterX:F3}, {item.CenterY:F3})，宽高({item.Width:F3}, {item.Height:F3})，角度 {item.Angle:F3}°", Color.Lime));
                }
                else
                {
                    result.Texts.Add(new ColorText($"目标{item.TargetIndex} 卡尺找椭圆失败：数值0，原因：{item.ErrorMessage}", Color.Red));
                }
            }

            return result;
        }
    }
}
