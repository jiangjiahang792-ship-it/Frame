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

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    public class NodeFindPoint : NodeBase
    {
        public NodeFindPoint(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormFindPoint(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultFindPoint();
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

                var form = ParamForm as NodeParamFormFindPoint;
                var param = ParamForm.Params as NodeParamFindPoint;
                if (form == null || param == null)
                    throw new Exception("找点参数异常。");

                Mat output;
                FindPointMeasureResult measureResult = form.ExecuteMeasure(param, token, out output);
                if (measureResult == null)
                    throw new Exception("找点算法没有返回结果。");

                NodeResultFindPoint nodeResult = BuildResult(measureResult, output);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！耗时：{time} ms，边缘点：{measureResult.PointCount}", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})未找到有效边缘点。耗时：{time} ms", true);

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
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        internal static NodeResultFindPoint BuildResult(FindPointMeasureResult measureResult, Mat output)
        {
            var result = new NodeResultFindPoint
            {
                IsOk = measureResult.Success,
                PointCount = measureResult.PointCount,
                ContourCount = measureResult.ContourCount,
                Points = measureResult.Points.ToList(),
                RegionPoints = measureResult.PrimaryContour.ToList(),
                Contours = measureResult.Contours.Select(contour => contour.ToList()).ToList(),
                Message = measureResult.Message,
                AlgorithmMs = measureResult.AlgorithmMs
            };

            if (result.Points.Count > 0)
            {
                result.CenterX = result.Points.Average(point => point.X);
                result.CenterY = result.Points.Average(point => point.Y);
            }

            result.OutputImage.Bitmaps = new List<Mat> { output };
            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(FindPointMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            foreach (List<PointF> region in measureResult.Regions)
            {
                result.Contours.Add(new ColorContour(region, Color.DodgerBlue)
                {
                    LineWidth = 1.2F
                });
            }

            foreach (List<PointF> contour in measureResult.Contours)
            {
                result.Contours.Add(new ColorContour(contour, Color.Lime)
                {
                    LineWidth = 1.8F
                });
            }

            Color textColor = measureResult.Success ? Color.Lime : Color.Red;
            result.Texts.Add(new ColorText($"找点：{measureResult.PointCount} 点 / {measureResult.ContourCount} 组", textColor)
            {
                FontSize = 14,
                Position = DisplayTextPosition.TopLeft,
                Title = "FindPoint"
            });
            return result;
        }
    }
}
