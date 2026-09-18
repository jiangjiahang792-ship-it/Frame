using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.MatchTemplate;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>独立轮廓模板匹配节点，保留桌面Demo参数与原生算法。</summary>
    public sealed class NodeContourMatch : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        /// <summary>初始化节点及Designer参数页，不在构造时加载原生DLL。</summary>
        public NodeContourMatch(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormContourMatch(); form.SetNodeBelong(this);
            ParamForm = form; Result = new NodeResultContourMatch();
        }

        /// <summary>运行期间借用上游Mat，保护输入租约并在失败或取消时清除旧结果。</summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime started = DateTime.Now;
            Result = new NodeResultContourMatch();
            if (!Active) { SetRunResult(started, NodeStatus.Unexecuted); return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun)); }
            NodeResultContourMatch pending = null;
            try
            {
                token.ThrowIfCancellationRequested(); SetStatus(NodeStatus.Running, "*");
                var form = (NodeParamFormContourMatch)ParamForm;
                NodeParamContourMatch parameters = form.SavedParameters;
                if (parameters == null) throw new InvalidOperationException("请先配置输入图像和轮廓模板。");
                OutputImage owner = form.ResolveInput(parameters);
                using (owner.AcquireLease())
                {
                    Mat source = GetSourceMat(owner);
                    ContourMatchExecution execution = form.Execute(source, parameters, token);
                    pending = BuildResult(execution, owner, source);
                    token.ThrowIfCancellationRequested();
                    pending.RunTime = SetRunResult(started, NodeStatus.Successful);
                    Result = pending; pending = null;
                    if (showLog) LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})轮廓匹配完成：{execution.Matches.Count}个目标，搜索{execution.Milliseconds:F2}毫秒。", true);
                }
                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException) { SetRunResult(started, NodeStatus.Unexecuted); throw; }
            catch (Exception exception)
            {
                SetRunResult(started, NodeStatus.Failed);
                if (showLog) LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})轮廓匹配失败：{exception.Message}", true);
                throw;
            }
            finally { NodeResultResourceManager.Release(pending); }
        }

        /// <summary>选择上游原始图像，不隐式合并多图、不修改上游图像。</summary>
        internal static Mat GetSourceMat(OutputImage image)
        {
            if (OutputImage.HasValidImage(image?.SrcImg)) return image.SrcImg;
            if (image?.Bitmaps != null && image.Bitmaps.Count > 0 && OutputImage.HasValidImage(image.Bitmaps[0])) return image.Bitmaps[0];
            if (OutputImage.HasValidImage(image?.GrayImg)) return image.GrayImg;
            throw new InvalidOperationException("订阅图像为空，请先运行上游图像节点。");
        }

        /// <summary>输出Demo原始0～1得分及顺时针角度，并提供兼容位置修正的位姿列表。</summary>
        internal static NodeResultContourMatch BuildResult(ContourMatchExecution execution, OutputImage owner, Mat source)
        {
            var result = new NodeResultContourMatch();
            try
            {
                result.OutputImage.Dispose();
                result.OutputImage = OutputImage.FromBorrowedSingleImage(owner, source,
                    OutputImage.HasValidImage(owner.GrayImg) ? owner.GrayImg : null);
                result.MatchCount = execution.Matches.Count; result.IsOk = result.MatchCount > 0;
                result.MatchingTime = execution.Milliseconds; result.Result.IsAllOk = result.IsOk;
                result.Result.Texts.Add(new ColorText($"轮廓模板匹配：{result.MatchCount}个目标，搜索{execution.Milliseconds:F2}毫秒", result.IsOk ? Color.LimeGreen : Color.Red));
                for (int index = 0; index < execution.Matches.Count; index++)
                {
                    ShapeMatchResult match = execution.Matches[index];
                    result.TemplateNames.Add(match.TemplateName ?? "模板1");
                    result.TemplateIds.Add(match.TemplateId ?? "legacy-1");
                    result.Poses.Add(new TemplateMatchPose { TargetIndex = index + 1, CenterX = match.CenterX, CenterY = match.CenterY,
                        Angle = match.AngleDegrees, ScaleX = 1, ScaleY = 1, Score = match.Score, Width = match.Width, Height = match.Height, IsValid = true });
                    result.Result.Rects.Add(new ColorRotatedRect((float)match.CenterX, (float)match.CenterY, (float)match.Width, (float)match.Height, (float)match.AngleDegrees, Color.LimeGreen));
                    result.Result.Texts.Add(new ColorText($"目标{index + 1}：X={match.CenterX:F3}，Y={match.CenterY:F3}，角度={match.AngleDegrees:F3}°，得分={match.Score:F4}", Color.LimeGreen));
                    double radians = match.AngleDegrees * Math.PI / 180;
                    double cosine = Math.Cos(radians), sine = Math.Sin(radians);
                    // 同时提供轴对齐外接矩形，保持下游裁剪等节点的图像输出契约。
                    double halfWidth = (Math.Abs(cosine) * match.Width + Math.Abs(sine) * match.Height) / 2;
                    double halfHeight = (Math.Abs(sine) * match.Width + Math.Abs(cosine) * match.Height) / 2;
                    int left = (int)Math.Floor(match.CenterX - halfWidth), top = (int)Math.Floor(match.CenterY - halfHeight);
                    result.OutputImage.Rectangles.Add(new OpenCvSharp.Rect(left, top,
                        (int)Math.Ceiling(match.CenterX + halfWidth) - left, (int)Math.Ceiling(match.CenterY + halfHeight) - top));
                    foreach (PointF[] contour in match.ModelContours ?? execution.Contours)
                    {
                        var points = new List<PointF>(contour.Length);
                        foreach (PointF point in contour)
                        {
                            double x = point.X - match.Width / 2, y = point.Y - match.Height / 2;
                            points.Add(new PointF((float)(match.CenterX + cosine * x - sine * y), (float)(match.CenterY + sine * x + cosine * y)));
                        }
                        result.Result.Contours.Add(new ColorContour(points, Color.LimeGreen));
                    }
                }
                if (result.MatchCount > 0)
                {
                    ShapeMatchResult first = execution.Matches[0]; result.MatchX = first.CenterX; result.MatchY = first.CenterY;
                    result.Angle = first.AngleDegrees; result.Score = first.Score;
                }
                result.OutputImage.DisplayResult = result.Result;
                return result;
            }
            catch { NodeResultResourceManager.Release(result); throw; }
        }

        /// <summary>按当前最大匹配数声明目标中心动态输出，无需先运行模型。</summary>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var parameters = ((NodeParamFormContourMatch)ParamForm).SavedParameters;
            return MatchTemplateTargetVariableNames.BuildTargetVariableNames(parameters?.FindOptions?.MaximumMatches ?? 5);
        }

        /// <summary>提供动态中心坐标的真实数值类型。</summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            int index; MatchTemplateTargetCoordinate coordinate;
            bool valid = MatchTemplateTargetVariableNames.TryParse(variableName, out index, out coordinate);
            valueType = valid ? typeof(double) : null; return valid;
        }
    }

    /// <summary>兼容模板位姿及绘制订阅的轮廓匹配结果，得分保持Demo的0～1范围。</summary>
    public sealed class NodeResultContourMatch : NodeResultMatchTemplate
    {
        /// <summary>不包含首次建模、绘制和订阅读取的算法搜索耗时。</summary>
        [SubscriptionOutput]
        [DisplayName("匹配耗时")]
        public double MatchingTime { get; set; }
        /// <summary>与位姿列表顺序一致的来源模板名称。</summary>
        [SubscriptionOutput]
        [DisplayName("匹配模板名称列表")]
        public List<string> TemplateNames { get; set; } = new List<string>();
        /// <summary>与位姿列表顺序一致的稳定模板标识。</summary>
        [SubscriptionOutput]
        [DisplayName("匹配模板标识列表")]
        public List<string> TemplateIds { get; set; } = new List<string>();
    }
}
