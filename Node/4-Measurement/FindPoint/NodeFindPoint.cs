using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Stopwatch nodeRunWatch = Stopwatch.StartNew();
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                var form = ParamForm as NodeParamFormFindPoint;
                var param = ParamForm.Params as NodeParamFindPoint;
                if (form == null || param == null)
                    throw new Exception("找点参数异常。");

                Stopwatch executeMeasureWatch = Stopwatch.StartNew();
                FindPointMeasureResult measureResult = form.ExecuteMeasure(param, token);
                executeMeasureWatch.Stop();
                if (measureResult == null)
                    throw new Exception("找点算法没有返回结果。");

                Stopwatch buildResultWatch = Stopwatch.StartNew();
                NodeResultFindPoint nodeResult = BuildResult(measureResult);
                buildResultWatch.Stop();

                Stopwatch resultPublishWatch = Stopwatch.StartNew();
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;
                resultPublishWatch.Stop();
                nodeRunWatch.Stop();

                if (showLog)
                    WritePerformanceLog(
                        this,
                        form.LastTimingInfo,
                        executeMeasureWatch.Elapsed.TotalMilliseconds,
                        buildResultWatch.Elapsed.TotalMilliseconds,
                        resultPublishWatch.Elapsed.TotalMilliseconds,
                        nodeRunWatch.Elapsed.TotalMilliseconds,
                        time);

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

        /// <summary>
        /// 打印找点工具的分段性能诊断日志，重点观察灰度图复用、算法和算法外耗时。
        /// </summary>
        private static void WritePerformanceLog(NodeFindPoint node, FindPointTimingInfo timing, double executeMeasureMs, double buildResultMs, double resultPublishMs, double nodeRunMs, int reportedRunTime)
        {
            if (timing == null)
            {
                LogHelper.AddLog(MsgLevel.Info, $"【性能诊断-找点】节点({node.ID}.{node.NodeName}) 图像=未知；ExecuteMeasure={executeMeasureMs:F2}ms；结果组装={buildResultMs:F2}ms；结果发布={resultPublishMs:F2}ms；节点总耗时={nodeRunMs:F2}ms；状态耗时={reportedRunTime}ms", true);
                return;
            }

            double measuredExecuteMeasureMs = timing.ExecuteMeasureMs > 0 ? timing.ExecuteMeasureMs : executeMeasureMs;
            double executeOtherMs = measuredExecuteMeasureMs
                - timing.SubscriptionReadMs
                - timing.GrayAcquireMs
                - timing.RuntimeParamMs
                - timing.AlgorithmMs;
            if (executeOtherMs < 0)
                executeOtherMs = 0;

            string inputSource = string.IsNullOrWhiteSpace(timing.InputSource) ? "未知" : timing.InputSource;
            string imageInfo = timing.InputWidth > 0 && timing.InputHeight > 0
                ? $"{timing.InputWidth}x{timing.InputHeight}x{timing.InputChannels}，约{timing.InputMegaBytes:F2}MB，来源={inputSource}"
                : $"未知，来源={inputSource}";
            string temporaryGray = timing.TemporaryGrayCreated ? "是" : "否";

            LogHelper.AddLog(
                MsgLevel.Info,
                $"【性能诊断-找点】节点({node.ID}.{node.NodeName}) 图像={imageInfo}；订阅读取={timing.SubscriptionReadMs:F2}ms；深拷贝图=0.00ms；灰度取图={timing.GrayAcquireMs:F2}ms；临时灰度={temporaryGray}；ROI区域={timing.ProcessedRegionCount}个；ROI像素约={timing.ProcessedMegaPixels:F2}MP；位置修正/参数准备={timing.RuntimeParamMs:F2}ms；算法={timing.AlgorithmMs:F2}ms；输出图准备=0.00ms；执行其它={executeOtherMs:F2}ms；结果组装={buildResultMs:F2}ms；结果发布={resultPublishMs:F2}ms；ExecuteMeasure={measuredExecuteMeasureMs:F2}ms；节点总耗时={nodeRunMs:F2}ms；状态耗时={reportedRunTime}ms",
                true);
        }

        internal static NodeResultFindPoint BuildResult(FindPointMeasureResult measureResult)
        {
            var result = new NodeResultFindPoint
            {
                IsOk = measureResult.Success,
                PointCount = measureResult.PointCount,
                ContourCount = measureResult.ContourCount,
                Points = MeasurementResultRounder.RoundPoints(measureResult.Points),
                RegionPoints = MeasurementResultRounder.RoundPoints(measureResult.PrimaryContour),
                Contours = MeasurementResultRounder.RoundPointGroups(measureResult.Contours),
                Message = measureResult.Message,
                AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs)
            };

            if (result.Points.Count > 0)
            {
                result.CenterX = MeasurementResultRounder.Round(result.Points.Average(point => point.X));
                result.CenterY = MeasurementResultRounder.Round(result.Points.Average(point => point.Y));
            }

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
