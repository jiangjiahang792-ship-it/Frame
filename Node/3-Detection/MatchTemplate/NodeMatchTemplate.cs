using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public class NodeMatchTemplate : NodeBase
    {
        public NodeMatchTemplate(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormMatchTemplate(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultMatchTemplate();
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is NodeParamFormMatchTemplate form)
            {
                if (ParamForm.Params is NodeParamMatchTemplate param)
                {
                    try
                    {
                        // 初始化状态
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        // 获取图像
                        form.UpdataImage();

                        // 执行模版匹配
                        var matchResult = form.MatchTemplate(false);
                        // 输出结果
                        var nodeResult = BuildResult(matchResult, param);
                        Result = nodeResult;

                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog && matchResult != null && matchResult.IsOk)
                        {
                            double minScore = matchResult.Matches.Count == 0 ? 0 : matchResult.Matches.Min(m => m.Score) * 100.0;
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms，当前结果最低匹配得分：{minScore:F2}, 匹配是否成功: {matchResult.IsOk}", true);
                        }
                        else if (showLog)
                        {
                            int matchCount = matchResult == null ? 0 : matchResult.Matches.Count;
                            LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})未匹配到有效模板结果！({time} ms，匹配数量：{matchCount})", true);
                        }
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);

        }

        internal void PublishPreviewResult(FastTemplateMatchResult matchResult, NodeParamMatchTemplate param)
        {
            var nodeResult = BuildResult(matchResult, param);
            nodeResult.RunTime = matchResult == null ? 0 : (int)Math.Round(matchResult.AlgorithmMs);
            Result = nodeResult;
        }

        internal static NodeResultMatchTemplate BuildResult(FastTemplateMatchResult matchResult, NodeParamMatchTemplate param)
        {
            var nodeResult = new NodeResultMatchTemplate();
            if (matchResult == null)
                return nodeResult;

            if (matchResult.OutputBitmap != null)
                nodeResult.OutputImage.Bitmaps = new List<Mat>() { matchResult.OutputBitmap.ToMat() };
            nodeResult.OutputImage.Rectangles = matchResult.Matches.Select(m => m.Box.ToBoundingRect()).ToList();
            nodeResult.Result.Clear();
            nodeResult.Result.IsAllOk = matchResult.IsOk;
            nodeResult.IsOk = matchResult.IsOk;
            nodeResult.MatchCount = matchResult.Matches.Count;
            nodeResult.Poses = matchResult.Matches.Select((match, index) => new TemplateMatchPose
            {
                TargetIndex = index + 1,
                CenterX = match.Box.CenterX,
                CenterY = match.Box.CenterY,
                Angle = match.Box.Angle,
                ScaleX = 1.0,
                ScaleY = 1.0,
                Score = match.Score * 100.0,
                Width = match.Box.Width,
                Height = match.Box.Height,
                IsValid = true
            }).ToList();
            if (matchResult.Matches.Count > 0)
            {
                var firstMatch = matchResult.Matches[0];
                nodeResult.MatchX = firstMatch.Box.CenterX;
                nodeResult.MatchY = firstMatch.Box.CenterY;
                nodeResult.Angle = firstMatch.Box.Angle;
                nodeResult.Score = firstMatch.Score * 100.0;
            }

            nodeResult.Result.Texts.Add(new ColorText($"模版匹配结果个数：{matchResult.Matches.Count}个，耗时 {matchResult.AlgorithmMs:F2} ms", matchResult.IsOk ? Color.Green : Color.Red));
            for (int i = 0; i < matchResult.Matches.Count; i++)
            {
                var match = matchResult.Matches[i];
                if (param == null || param.ShowOutRegionStatus)
                {
                    nodeResult.Result.Rects.Add(new ColorRotatedRect(
                        match.Box.CenterX,
                        match.Box.CenterY,
                        match.Box.Width,
                        match.Box.Height,
                        match.Box.Angle,
                        matchResult.IsOk ? Color.Lime : Color.Red));
                }

                nodeResult.Result.Texts.Add(new ColorText(
                    $"模版匹配结果{i + 1}：中心（{match.Box.CenterX:F1}, {match.Box.CenterY:F1}） 角度{match.Box.Angle:F2}° 得分{match.Score * 100.0:F2}",
                    matchResult.IsOk ? Color.Green : Color.Red));
            }

            if (param == null || param.ShowOutlineStatus)
            {
                foreach (var outline in matchResult.Outlines)
                    nodeResult.Result.Contours.Add(new ColorContour(outline.Points, Color.Cyan));
            }
            nodeResult.OutputImage.DisplayResult = nodeResult.Result;
            return nodeResult;
        }
    }
}
