using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    /// <summary>
    /// 线线夹角测量节点，负责执行测量并生成可叠加到图像上的 ROI 结果。
    /// </summary>
    public class NodeLineLineAngle : NodeBase
    {
        /// <summary>
        /// 图像尺寸缺失时，延长显示线的单侧最小长度，保证交点附近能看出两线趋势。
        /// </summary>
        private const float FallbackDisplayLineHalfLength = 600F;

        /// <summary>
        /// 图像尺寸缺失时，按实测线段长度放大的显示倍率。
        /// </summary>
        private const double FallbackDisplayLineScale = 4D;

        /// <summary>
        /// 交点圆形标记半径。
        /// </summary>
        private const int IntersectionMarkerRadius = 8;

        /// <summary>
        /// 交点十字标记的半长度。
        /// </summary>
        private const float IntersectionCrossHalfLength = 16F;

        /// <summary>
        /// 夹角弧线最小显示半径。
        /// </summary>
        private const float AngleArcMinRadius = 32F;

        /// <summary>
        /// 夹角弧线最大显示半径，避免大图上标注离交点过远。
        /// </summary>
        private const float AngleArcMaxRadius = 120F;

        /// <summary>
        /// 夹角文字距离弧线的外扩偏移。
        /// </summary>
        private const float AngleLabelOffset = 18F;

        /// <summary>
        /// 夹角文字字号。
        /// </summary>
        private const int AngleAnnotationFontSize = 18;

        /// <summary>
        /// 几何计算中用于过滤退化直线的误差阈值。
        /// </summary>
        private const double GeometryEpsilon = 1e-8;

        /// <summary>
        /// 图像边界裁剪时允许的浮点误差。
        /// </summary>
        private const double BoundaryTolerance = 0.01;

        /// <summary>
        /// 边界交点去重使用的平方距离阈值。
        /// </summary>
        private const double BoundaryDuplicateDistanceSquared = 0.25;

        public NodeLineLineAngle(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            var form = new NodeParamFormLineLineAngle(process, this);
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultLineLineAngle();
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

                var form = ParamForm as NodeParamFormLineLineAngle;
                var param = ParamForm.Params as NodeParamLineLineAngle;
                if (form == null || param == null)
                    throw new Exception("线到线夹角参数异常！");

                LineLineAngleMeasureResult measureResult = form.ExecuteMeasure(param);
                NodeResultLineLineAngle nodeResult = BuildResult(measureResult);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog && measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！{time} ms，夹角：{measureResult.Angle:F3}°", true);

                if (showLog && !measureResult.Success)
                    LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})线到线夹角失败：{measureResult.Message}（{time} ms）", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        internal static NodeResultLineLineAngle BuildResult(LineLineAngleMeasureResult measureResult)
        {
            var result = new NodeResultLineLineAngle();
            result.IsOk = measureResult.Success;
            result.AlgorithmMs = MeasurementResultRounder.Round(measureResult.AlgorithmMs);
            if (measureResult.Success)
            {
                result.Angle = MeasurementResultRounder.Round(measureResult.Angle);
                result.Line1StartX = MeasurementResultRounder.Round(measureResult.Line1.Start.X);
                result.Line1StartY = MeasurementResultRounder.Round(measureResult.Line1.Start.Y);
                result.Line1EndX = MeasurementResultRounder.Round(measureResult.Line1.End.X);
                result.Line1EndY = MeasurementResultRounder.Round(measureResult.Line1.End.Y);
                result.Line2StartX = MeasurementResultRounder.Round(measureResult.Line2.Start.X);
                result.Line2StartY = MeasurementResultRounder.Round(measureResult.Line2.Start.Y);
                result.Line2EndX = MeasurementResultRounder.Round(measureResult.Line2.End.X);
                result.Line2EndY = MeasurementResultRounder.Round(measureResult.Line2.End.Y);
                if (measureResult.IntersectionPoint.HasValue)
                {
                    result.IntersectionX = MeasurementResultRounder.Round(measureResult.IntersectionPoint.Value.X);
                    result.IntersectionY = MeasurementResultRounder.Round(measureResult.IntersectionPoint.Value.Y);
                }

                result.AverageDistance = MeasurementResultRounder.Round(measureResult.AverageDistance);
                result.MinDistance = MeasurementResultRounder.Round(measureResult.MinDistance);
                result.MaxDistance = MeasurementResultRounder.Round(measureResult.MaxDistance);
                result.DistancePointCount = measureResult.DistancePointCount;
            }

            result.Result = BuildDisplayResult(measureResult);
            result.OutputImage.DisplayResult = result.Result;
            return result;
        }

        internal static AlgorithmResult BuildDisplayResult(LineLineAngleMeasureResult measureResult)
        {
            var result = new AlgorithmResult();
            result.IsAllOk = measureResult.Success;

            foreach (PointF point in measureResult.Line1EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Yellow));
            foreach (PointF point in measureResult.Line2EdgePoints)
                result.Circles.Add(new ColorCircle(point, 2, Color.Cyan));

            AddMeasuredLineOverlay(result, measureResult.Line1, measureResult, measureResult.Success ? Color.Lime : Color.Red);
            AddMeasuredLineOverlay(result, measureResult.Line2, measureResult, measureResult.Success ? Color.DeepSkyBlue : Color.Red);
            if (measureResult.IntersectionPoint.HasValue)
                AddIntersectionOverlay(result, measureResult.IntersectionPoint.Value);

            if (measureResult.Success)
            {
                AddAngleAnnotationOverlay(result, measureResult);
                result.Texts.Add(new ColorText($"线到线夹角：{measureResult.Angle:F3}°", Color.Lime));
                if (measureResult.DistancePointCount > 0)
                    result.Texts.Add(new ColorText($"平均线距：{measureResult.AverageDistance:F3}px，点数：{measureResult.DistancePointCount}", Color.Cyan));
                if (measureResult.IntersectionPoint.HasValue)
                    result.Texts.Add(new ColorText($"交点：X={measureResult.IntersectionPoint.Value.X:F3}, Y={measureResult.IntersectionPoint.Value.Y:F3}", Color.Orange));
            }
            else
            {
                result.Texts.Add(new ColorText($"线到线夹角失败：{measureResult.Message}", Color.Red));
            }

            return result;
        }

        /// <summary>
        /// 添加交点附近的夹角弧线和角度文字，形成类似“α=60°”的空间标注。
        /// </summary>
        private static void AddAngleAnnotationOverlay(AlgorithmResult result, LineLineAngleMeasureResult measureResult)
        {
            if (result == null ||
                measureResult == null ||
                !measureResult.Success ||
                !measureResult.IntersectionPoint.HasValue ||
                measureResult.Line1 == null ||
                measureResult.Line2 == null ||
                !measureResult.Line1.IsValid ||
                !measureResult.Line2.IsValid)
            {
                return;
            }

            double line1Angle = GetLineAngle(measureResult.Line1);
            double line2Angle = GetLineAngle(measureResult.Line2);
            if (!TryBuildIncludedAngleArc(line1Angle, line2Angle, out double startAngle, out double sweepAngle, out double labelAngle))
                return;

            PointF center = measureResult.IntersectionPoint.Value;
            float radius = CalculateAngleArcRadius(measureResult);
            result.Arcs.Add(new ColorArc(center, radius, (float)startAngle, (float)sweepAngle, Color.Red)
            {
                LineWidth = 4F
            });

            PointF labelPoint = BuildAngleLabelPoint(center, labelAngle, radius, measureResult.DisplayImageWidth, measureResult.DisplayImageHeight);
            result.Texts.Add(new ColorText(FormatAngleAnnotationText(measureResult.Angle), Color.Red)
            {
                FontSize = AngleAnnotationFontSize,
                Title = string.Empty,
                UseImagePosition = true,
                ImagePosition = labelPoint
            });
        }

        /// <summary>
        /// 添加测量线显示 ROI；优先延长到图像边界，让相交趋势和交点位置更容易判断。
        /// </summary>
        private static void AddMeasuredLineOverlay(AlgorithmResult result, MeasuredLine line, LineLineAngleMeasureResult measureResult, Color color)
        {
            if (result == null || line == null || !line.IsValid)
                return;

            // 原始测量线保持在延长线之前，避免下游按第一条 Lines 读取时拿到显示用延长线。
            result.Lines.Add(new ColorLine(line.Start, line.End, color) { LineWidth = 2F });

            PointF displayStart;
            PointF displayEnd;
            bool hasDisplayLine = TryBuildImageBoundaryLine(line, measureResult.DisplayImageWidth, measureResult.DisplayImageHeight, out displayStart, out displayEnd) ||
                                  TryBuildFallbackExtendedLine(line, measureResult.IntersectionPoint, out displayStart, out displayEnd);

            if (hasDisplayLine)
                result.Lines.Add(new ColorLine(displayStart, displayEnd, color) { LineWidth = 3F });
        }

        /// <summary>
        /// 添加交点圆形和十字标记，避免交点只靠短线段端点判断。
        /// </summary>
        private static void AddIntersectionOverlay(AlgorithmResult result, PointF intersectionPoint)
        {
            if (result == null)
                return;

            result.Circles.Add(new ColorCircle(intersectionPoint, IntersectionMarkerRadius, Color.Orange) { LineWidth = 3F });
            result.Lines.Add(new ColorLine(
                new PointF(intersectionPoint.X - IntersectionCrossHalfLength, intersectionPoint.Y),
                new PointF(intersectionPoint.X + IntersectionCrossHalfLength, intersectionPoint.Y),
                Color.Orange) { LineWidth = 2F });
            result.Lines.Add(new ColorLine(
                new PointF(intersectionPoint.X, intersectionPoint.Y - IntersectionCrossHalfLength),
                new PointF(intersectionPoint.X, intersectionPoint.Y + IntersectionCrossHalfLength),
                Color.Orange) { LineWidth = 2F });
        }

        /// <summary>
        /// 计算直线在图像坐标系中的角度。
        /// </summary>
        private static double GetLineAngle(MeasuredLine line)
        {
            return NormalizeAngle(Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X) * 180.0 / Math.PI);
        }

        /// <summary>
        /// 从两条无向直线的四个射线组合中挑选较小夹角，返回可直接绘制的弧线角度。
        /// </summary>
        private static bool TryBuildIncludedAngleArc(double line1Angle, double line2Angle, out double startAngle, out double sweepAngle, out double labelAngle)
        {
            startAngle = 0D;
            sweepAngle = 0D;
            labelAngle = 0D;

            double[] line1Candidates = { NormalizeAngle(line1Angle), NormalizeAngle(line1Angle + 180D) };
            double[] line2Candidates = { NormalizeAngle(line2Angle), NormalizeAngle(line2Angle + 180D) };
            double bestAbsSweep = double.MaxValue;

            foreach (double candidate1 in line1Candidates)
            {
                foreach (double candidate2 in line2Candidates)
                {
                    double candidateSweep = NormalizeSignedAngle(candidate2 - candidate1);
                    double absSweep = Math.Abs(candidateSweep);
                    if (absSweep < GeometryEpsilon || absSweep >= bestAbsSweep)
                        continue;

                    bestAbsSweep = absSweep;
                    startAngle = candidate1;
                    sweepAngle = candidateSweep;
                    labelAngle = NormalizeAngle(candidate1 + candidateSweep / 2D);
                }
            }

            return bestAbsSweep < double.MaxValue && bestAbsSweep > GeometryEpsilon;
        }

        /// <summary>
        /// 根据线段和图像尺寸估算弧线半径，兼顾小线段可见性和大图紧凑性。
        /// </summary>
        private static float CalculateAngleArcRadius(LineLineAngleMeasureResult measureResult)
        {
            double lineLength = Math.Min(
                Math.Sqrt(DistanceSquared(measureResult.Line1.Start, measureResult.Line1.End)),
                Math.Sqrt(DistanceSquared(measureResult.Line2.Start, measureResult.Line2.End)));
            double radiusByLine = lineLength <= GeometryEpsilon ? AngleArcMinRadius : lineLength * 0.35D;
            double radiusByImage = AngleArcMinRadius;
            if (measureResult.DisplayImageWidth > 0 && measureResult.DisplayImageHeight > 0)
                radiusByImage = Math.Min(measureResult.DisplayImageWidth, measureResult.DisplayImageHeight) * 0.08D;

            return (float)Clamp(Math.Max(AngleArcMinRadius, Math.Min(Math.Max(radiusByLine, radiusByImage), AngleArcMaxRadius)), AngleArcMinRadius, AngleArcMaxRadius);
        }

        /// <summary>
        /// 根据弧线中线角度生成文字坐标，并在有图像尺寸时限制到图像范围内。
        /// </summary>
        private static PointF BuildAngleLabelPoint(PointF center, double labelAngle, float radius, int imageWidth, int imageHeight)
        {
            double radian = labelAngle * Math.PI / 180.0;
            double distance = radius + AngleLabelOffset;
            double x = center.X + Math.Cos(radian) * distance;
            double y = center.Y + Math.Sin(radian) * distance;

            if (imageWidth > 1)
                x = Clamp(x, 0D, imageWidth - 1D);
            if (imageHeight > 1)
                y = Clamp(y, 0D, imageHeight - 1D);

            return new PointF((float)x, (float)y);
        }

        /// <summary>
        /// 格式化夹角标注文本，整数角度不显示多余小数。
        /// </summary>
        private static string FormatAngleAnnotationText(double angle)
        {
            double integerAngle = Math.Round(angle);
            return Math.Abs(angle - integerAngle) < 0.05D ? $"α={integerAngle:F0}°" : $"α={angle:F1}°";
        }

        /// <summary>
        /// 将角度归一到 [0, 360) 范围。
        /// </summary>
        private static double NormalizeAngle(double angle)
        {
            double value = angle % 360D;
            return value < 0D ? value + 360D : value;
        }

        /// <summary>
        /// 将角度差归一到 (-180, 180] 范围，便于绘制较小夹角。
        /// </summary>
        private static double NormalizeSignedAngle(double angle)
        {
            double value = NormalizeAngle(angle);
            return value > 180D ? value - 360D : value;
        }

        /// <summary>
        /// 将无限直线裁剪到当前图像边界，生成可直接显示的边界到边界线段。
        /// </summary>
        private static bool TryBuildImageBoundaryLine(MeasuredLine line, int imageWidth, int imageHeight, out PointF displayStart, out PointF displayEnd)
        {
            displayStart = PointF.Empty;
            displayEnd = PointF.Empty;

            if (line == null || !line.IsValid || imageWidth <= 1 || imageHeight <= 1)
                return false;

            double dx = line.End.X - line.Start.X;
            double dy = line.End.Y - line.Start.Y;
            if (dx * dx + dy * dy < GeometryEpsilon)
                return false;

            double maxX = imageWidth - 1D;
            double maxY = imageHeight - 1D;
            var points = new List<PointF>(4);

            if (Math.Abs(dx) > GeometryEpsilon)
            {
                AddBoundaryPoint(points, (0D - line.Start.X) / dx, line.Start, dx, dy, maxX, maxY);
                AddBoundaryPoint(points, (maxX - line.Start.X) / dx, line.Start, dx, dy, maxX, maxY);
            }

            if (Math.Abs(dy) > GeometryEpsilon)
            {
                AddBoundaryPoint(points, (0D - line.Start.Y) / dy, line.Start, dx, dy, maxX, maxY);
                AddBoundaryPoint(points, (maxY - line.Start.Y) / dy, line.Start, dx, dy, maxX, maxY);
            }

            return TryPickFarthestPair(points, out displayStart, out displayEnd);
        }

        /// <summary>
        /// 在没有图像尺寸时，围绕交点或线段中心生成足够长的显示线。
        /// </summary>
        private static bool TryBuildFallbackExtendedLine(MeasuredLine line, PointF? preferredAnchor, out PointF displayStart, out PointF displayEnd)
        {
            displayStart = PointF.Empty;
            displayEnd = PointF.Empty;

            if (line == null || !line.IsValid)
                return false;

            double dx = line.End.X - line.Start.X;
            double dy = line.End.Y - line.Start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < GeometryEpsilon)
                return false;

            PointF anchor = preferredAnchor ?? new PointF((line.Start.X + line.End.X) / 2F, (line.Start.Y + line.End.Y) / 2F);
            double halfLength = Math.Max(FallbackDisplayLineHalfLength, length * FallbackDisplayLineScale);
            double ux = dx / length;
            double uy = dy / length;

            displayStart = new PointF((float)(anchor.X - ux * halfLength), (float)(anchor.Y - uy * halfLength));
            displayEnd = new PointF((float)(anchor.X + ux * halfLength), (float)(anchor.Y + uy * halfLength));
            return true;
        }

        /// <summary>
        /// 尝试添加直线与图像边界的一处交点，并对相同角点做去重。
        /// </summary>
        private static void AddBoundaryPoint(List<PointF> points, double t, PointF lineStart, double dx, double dy, double maxX, double maxY)
        {
            double x = lineStart.X + t * dx;
            double y = lineStart.Y + t * dy;
            if (x < -BoundaryTolerance || x > maxX + BoundaryTolerance || y < -BoundaryTolerance || y > maxY + BoundaryTolerance)
                return;

            PointF point = new PointF((float)Clamp(x, 0D, maxX), (float)Clamp(y, 0D, maxY));
            if (!ContainsNearPoint(points, point))
                points.Add(point);
        }

        /// <summary>
        /// 从候选边界点中选取距离最远的一对作为显示线端点。
        /// </summary>
        private static bool TryPickFarthestPair(List<PointF> points, out PointF displayStart, out PointF displayEnd)
        {
            displayStart = PointF.Empty;
            displayEnd = PointF.Empty;
            if (points == null || points.Count < 2)
                return false;

            double maxDistanceSquared = 0D;
            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distanceSquared = DistanceSquared(points[i], points[j]);
                    if (distanceSquared > maxDistanceSquared)
                    {
                        maxDistanceSquared = distanceSquared;
                        displayStart = points[i];
                        displayEnd = points[j];
                    }
                }
            }

            return maxDistanceSquared > BoundaryDuplicateDistanceSquared;
        }

        /// <summary>
        /// 判断候选点是否已在列表中，避免图像角点被重复加入。
        /// </summary>
        private static bool ContainsNearPoint(List<PointF> points, PointF candidate)
        {
            if (points == null)
                return false;

            foreach (PointF point in points)
            {
                if (DistanceSquared(point, candidate) <= BoundaryDuplicateDistanceSquared)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 计算两点平方距离，避免显示裁剪热路径中额外开方。
        /// </summary>
        private static double DistanceSquared(PointF a, PointF b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// 将数值限制在指定闭区间内，兼容旧版 .NET Framework。
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }

    /// <summary>
    /// 线线夹角本次测量的中间结果和显示辅助数据。
    /// </summary>
    internal class LineLineAngleMeasureResult
    {
        /// <summary>
        /// 本次测量是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 失败时的提示信息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 两条直线的夹角。
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的平均绝对垂直距离。
        /// </summary>
        public double AverageDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最小绝对垂直距离。
        /// </summary>
        public double MinDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最大绝对垂直距离。
        /// </summary>
        public double MaxDistance { get; set; }

        /// <summary>
        /// 参与线距统计的点数量。
        /// </summary>
        public int DistancePointCount { get; set; }

        /// <summary>
        /// 算法执行耗时，单位毫秒。
        /// </summary>
        public double AlgorithmMs { get; set; }

        /// <summary>
        /// 第一条参与夹角计算的直线。
        /// </summary>
        public MeasuredLine Line1 { get; set; }

        /// <summary>
        /// 第二条参与夹角计算的直线。
        /// </summary>
        public MeasuredLine Line2 { get; set; }

        /// <summary>
        /// 两条无限直线的交点，平行时为空。
        /// </summary>
        public PointF? IntersectionPoint { get; set; }

        /// <summary>
        /// 第一条线参与拟合或找线的边缘点。
        /// </summary>
        public List<PointF> Line1EdgePoints { get; set; } = new List<PointF>();

        /// <summary>
        /// 第二条线参与拟合或找线的边缘点。
        /// </summary>
        public List<PointF> Line2EdgePoints { get; set; } = new List<PointF>();

        /// <summary>
        /// 当前输入图像宽度，用于将显示延长线裁剪到图像边界。
        /// </summary>
        public int DisplayImageWidth { get; set; }

        /// <summary>
        /// 当前输入图像高度，用于将显示延长线裁剪到图像边界。
        /// </summary>
        public int DisplayImageHeight { get; set; }
    }
}
