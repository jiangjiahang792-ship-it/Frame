using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// 模板匹配节点。
    /// </summary>
    public class NodeMatchTemplate : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 结果显示名称，原模板匹配和NCC模板匹配共用结果结构时用于区分界面文案。
        /// </summary>
        private readonly string resultDisplayName;

        /// <summary>
        /// 创建原模板匹配节点，保留既有参数界面、结果文案和算法路径。
        /// </summary>
        public NodeMatchTemplate(int nodeId, string nodeName, Process process, NodeType nodeType)
            : this(nodeId, nodeName, process, nodeType, false, "模版匹配")
        {
        }

        /// <summary>
        /// 创建模板匹配节点基类实例，可由NCC节点指定native demo算法路径和显示名称。
        /// </summary>
        protected NodeMatchTemplate(
            int nodeId,
            string nodeName,
            Process process,
            NodeType nodeType,
            bool forceNativeDemoAlgorithm,
            string resultDisplayName) : base(nodeId, nodeName, process, nodeType)
        {
            this.resultDisplayName = string.IsNullOrWhiteSpace(resultDisplayName) ? "模版匹配" : resultDisplayName;
            var form = new NodeParamFormMatchTemplate(process, this, forceNativeDemoAlgorithm);
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
                        var nodeResult = BuildResult(matchResult, param, resultDisplayName);
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
            var nodeResult = BuildResult(matchResult, param, resultDisplayName);
            nodeResult.RunTime = matchResult == null ? 0 : (int)Math.Round(matchResult.AlgorithmMs);
            Result = nodeResult;
        }

        internal static NodeResultMatchTemplate BuildResult(FastTemplateMatchResult matchResult, NodeParamMatchTemplate param)
        {
            return BuildResult(matchResult, param, "模版匹配");
        }

        internal static NodeResultMatchTemplate BuildResult(FastTemplateMatchResult matchResult, NodeParamMatchTemplate param, string displayName)
        {
            return BuildResult(matchResult, param, displayName, null, null);
        }

        /// <summary>
        /// 构建模板匹配节点结果，可由NCC快路径直接复用上游Mat输出，避免整图Bitmap往返。
        /// </summary>
        internal static NodeResultMatchTemplate BuildResult(FastTemplateMatchResult matchResult, NodeParamMatchTemplate param, string displayName, Mat outputMat, Mat grayMat)
        {
            var nodeResult = new NodeResultMatchTemplate();
            if (matchResult == null)
                return nodeResult;

            List<FastTemplateMatchInfo> orderedMatches = OrderMatches(matchResult.Matches, param == null
                ? MatchTemplateSortMode.ColumnAscending
                : param.SortMode);

            if (OutputImage.HasValidImage(outputMat))
            {
                nodeResult.OutputImage.SrcImg = outputMat;
                nodeResult.OutputImage.Bitmaps = new List<Mat>() { outputMat };
                if (OutputImage.HasValidImage(grayMat))
                    nodeResult.OutputImage.GrayImg = grayMat;
            }
            else if (matchResult.OutputBitmap != null)
            {
                nodeResult.OutputImage.Bitmaps = new List<Mat>() { matchResult.OutputBitmap.ToMat() };
            }
            nodeResult.OutputImage.Rectangles = orderedMatches.Select(m => m.Box.ToBoundingRect()).ToList();
            nodeResult.Result.Clear();
            nodeResult.Result.IsAllOk = matchResult.IsOk;
            nodeResult.IsOk = matchResult.IsOk;
            nodeResult.MatchCount = orderedMatches.Count;
            nodeResult.Poses = orderedMatches.Select((match, index) =>
            {
                double angleOffset = NormalizeAngleOffset(match.Box.Angle);
                return new TemplateMatchPose
                {
                    TargetIndex = index + 1,
                    CenterX = match.Box.CenterX,
                    CenterY = match.Box.CenterY,
                    Angle = angleOffset,
                    ScaleX = 1.0,
                    ScaleY = 1.0,
                    Score = match.Score * 100.0,
                    Width = match.Box.Width,
                    Height = match.Box.Height,
                    IsValid = true
                };
            }).ToList();
            if (orderedMatches.Count > 0)
            {
                var firstMatch = orderedMatches[0];
                nodeResult.MatchX = firstMatch.Box.CenterX;
                nodeResult.MatchY = firstMatch.Box.CenterY;
                nodeResult.Angle = NormalizeAngleOffset(firstMatch.Box.Angle);
                nodeResult.Score = firstMatch.Score * 100.0;
            }

            string safeDisplayName = string.IsNullOrWhiteSpace(displayName) ? "模版匹配" : displayName;
            nodeResult.Result.Texts.Add(new ColorText($"{safeDisplayName}结果个数：{orderedMatches.Count}个，耗时 {matchResult.AlgorithmMs:F2} ms", matchResult.IsOk ? Color.Green : Color.Red));
            for (int i = 0; i < orderedMatches.Count; i++)
            {
                var match = orderedMatches[i];
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

                double angleOffset = NormalizeAngleOffset(match.Box.Angle);
                nodeResult.Result.Texts.Add(new ColorText(
                    $"{safeDisplayName}结果{i + 1}：中心（{match.Box.CenterX:F1}, {match.Box.CenterY:F1}） 角度偏差{angleOffset:F2}° 得分{match.Score * 100.0:F2}",
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

        /// <summary>
        /// 获取当前参数声明的排序目标中心变量名，供四则运算配置阶段选择。
        /// </summary>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            NodeParamMatchTemplate param = ParamForm == null ? null : ParamForm.Params as NodeParamMatchTemplate;
            int targetCount = param == null || param.ResultNum <= 0 ? 1 : param.ResultNum;
            return MatchTemplateTargetVariableNames.BuildTargetVariableNames(targetCount);
        }

        /// <summary>
        /// 获取模板目标中心动态变量的真实类型。
        /// </summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = null;
            int targetIndex;
            MatchTemplateTargetCoordinate coordinate;
            if (!MatchTemplateTargetVariableNames.TryParse(variableName, out targetIndex, out coordinate))
                return false;

            valueType = typeof(double);
            return true;
        }

        /// <summary>
        /// 根据配置对 native 返回的匹配结果排序，让目标编号在下游订阅中保持稳定。
        /// </summary>
        private static List<FastTemplateMatchInfo> OrderMatches(
            IEnumerable<FastTemplateMatchInfo> matches,
            MatchTemplateSortMode sortMode)
        {
            IEnumerable<FastTemplateMatchInfo> safeMatches = matches ?? Enumerable.Empty<FastTemplateMatchInfo>();
            switch (sortMode)
            {
                case MatchTemplateSortMode.RowAscending:
                    return safeMatches
                        .OrderBy(m => m.Box.CenterY)
                        .ThenBy(m => m.Box.CenterX)
                        .ToList();
                case MatchTemplateSortMode.ColumnAscending:
                default:
                    return safeMatches
                        .OrderBy(m => m.Box.CenterX)
                        .ThenBy(m => m.Box.CenterY)
                        .ToList();
            }
        }

        /// <summary>
        /// 把模板匹配返回的旋转量规范到负一百八十度到一百八十度，作为相对基准模板的角度偏差输出。
        /// </summary>
        private static double NormalizeAngleOffset(double angle)
        {
            while (angle > 180.0)
                angle -= 360.0;
            while (angle <= -180.0)
                angle += 360.0;
            return angle;
        }
    }
}
