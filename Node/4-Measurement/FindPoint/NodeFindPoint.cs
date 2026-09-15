using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
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
                Stopwatch nodeRunWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                var form = ParamForm as NodeParamFormFindPoint;
                var param = ParamForm.Params as NodeParamFindPoint;
                if (form == null || param == null)
                    throw new Exception("找点参数异常。");

                Stopwatch executeMeasureWatch = nodeRunWatch == null ? null : Stopwatch.StartNew();
                List<FindPointTargetResult> items = form.ExecuteMeasures(param, token);
                executeMeasureWatch?.Stop();

                Stopwatch buildResultWatch = nodeRunWatch == null ? null : Stopwatch.StartNew();
                NodeResultFindPoint nodeResult = BuildResult(items);
                buildResultWatch?.Stop();

                Stopwatch resultPublishWatch = nodeRunWatch == null ? null : Stopwatch.StartNew();
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;
                resultPublishWatch?.Stop();
                nodeRunWatch?.Stop();

                if (nodeRunWatch != null)
                    WritePerformanceLog(
                        this,
                        form.LastTimingInfo,
                        executeMeasureWatch?.Elapsed.TotalMilliseconds ?? 0D,
                        buildResultWatch?.Elapsed.TotalMilliseconds ?? 0D,
                        resultPublishWatch?.Elapsed.TotalMilliseconds ?? 0D,
                        nodeRunWatch.Elapsed.TotalMilliseconds,
                        time);

                if (showLog)
                    LogHelper.AddLog(nodeResult.IsOk ? MsgLevel.Info : MsgLevel.Warn,
                        $"节点({ID}.{NodeName})找点完成！耗时：{time} ms，目标：{items.Count}，总体：{(nodeResult.IsOk ? "OK" : "NG")}", true);

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
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => $"【性能诊断-找点】流程={node.Process?.ProcessName}；TraceId={node.Process?.CurrentPerformanceTraceId}；RunId={node.Process?.CurrentRunId}；节点={node.ID}.{node.NodeName}；图像=未知；ExecuteMeasure={executeMeasureMs:F2}ms；结果组装={buildResultMs:F2}ms；结果发布={resultPublishMs:F2}ms；节点总耗时={nodeRunMs:F2}ms；状态耗时={reportedRunTime}ms",
                    true);
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

            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => $"【性能诊断-找点】流程={node.Process?.ProcessName}；TraceId={node.Process?.CurrentPerformanceTraceId}；RunId={node.Process?.CurrentRunId}；节点={node.ID}.{node.NodeName}；图像={imageInfo}；订阅读取={timing.SubscriptionReadMs:F2}ms；深拷贝图=0.00ms；灰度取图={timing.GrayAcquireMs:F2}ms；临时灰度={temporaryGray}；ROI区域={timing.ProcessedRegionCount}个；ROI像素约={timing.ProcessedMegaPixels:F2}MP；位置修正/参数准备={timing.RuntimeParamMs:F2}ms；算法={timing.AlgorithmMs:F2}ms；输出图准备=0.00ms；执行其它={executeOtherMs:F2}ms；结果组装={buildResultMs:F2}ms；结果发布={resultPublishMs:F2}ms；ExecuteMeasure={measuredExecuteMeasureMs:F2}ms；节点总耗时={nodeRunMs:F2}ms；状态耗时={reportedRunTime}ms",
                true);
        }

        /// <summary>根据全部目标项构建找点节点汇总结果。</summary>
        internal static NodeResultFindPoint BuildResult(List<FindPointTargetResult> items)
        {
            List<FindPointTargetResult> safeItems = items ?? new List<FindPointTargetResult>();
            var result = new NodeResultFindPoint
            {
                Items = safeItems,
                IsOk = safeItems.Count > 0 && safeItems.All(item => item.IsOk),
                PointCount = safeItems.Sum(item => item.PointCount),
                ContourCount = safeItems.Sum(item => item.ContourCount),
                Points = safeItems.SelectMany(item => item.Points ?? new List<PointF>()).ToList(),
                RegionPoints = safeItems.SelectMany(item => item.RegionPoints ?? new List<PointF>()).ToList(),
                Contours = safeItems.SelectMany(item => item.Contours ?? new List<List<PointF>>()).ToList(),
                Message = string.Join("；", safeItems.Select(item => $"目标{item.TargetIndex}:{(item.IsOk ? "OK" : item.ErrorMessage)}")),
                AlgorithmMs = MeasurementResultRounder.Round(safeItems.Sum(item => item.AlgorithmMs))
            };
            result.JudgeOk = result.IsOk;

            FindPointTargetResult first = safeItems.FirstOrDefault();
            if (first != null)
            {
                result.CenterX = first.CenterX;
                result.CenterY = first.CenterY;
            }

            result.Result = BuildDisplayResult(safeItems);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        /// <summary>合并全部模板目标的找点区域、轮廓和状态文本。</summary>
        internal static AlgorithmResult BuildDisplayResult(IReadOnlyList<FindPointTargetResult> items)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = items != null && items.Count > 0 && items.All(item => item.IsOk);
            if (items == null)
                return result;
            foreach (FindPointTargetResult item in items)
            {
                foreach (List<PointF> region in item.Regions ?? new List<List<PointF>>())
                {
                    result.Contours.Add(new ColorContour(region, Color.DodgerBlue)
                    {
                        LineWidth = 1.2F
                    });
                }
                foreach (List<PointF> contour in item.Contours ?? new List<List<PointF>>())
                {
                    result.Contours.Add(new ColorContour(contour, Color.Lime)
                    {
                        LineWidth = 1.8F
                    });
                }
                Color textColor = item.IsOk ? Color.Lime : Color.Red;
                string text = item.IsOk
                    ? $"目标{item.TargetIndex} 找点：{item.PointCount}点 / {item.ContourCount}组"
                    : $"目标{item.TargetIndex} 找点失败：数值0，原因：{item.ErrorMessage}";
                result.Texts.Add(new ColorText(text, textColor)
                {
                    FontSize = 14,
                    Position = DisplayTextPosition.TopLeft,
                    Title = $"FindPoint{item.TargetIndex}"
                });
            }
            return result;
        }
    }
}
