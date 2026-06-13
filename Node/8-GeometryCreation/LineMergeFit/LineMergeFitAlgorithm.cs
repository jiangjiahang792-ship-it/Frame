using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    /// <summary>
    /// 线组合拟合的中间计算结果。
    /// </summary>
    internal class LineMergeFitMeasureResult
    {
        /// <summary>
        /// 组合拟合是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 失败或状态说明。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 第一条源线。
        /// </summary>
        public MeasuredLine Line1 { get; set; }

        /// <summary>
        /// 第二条源线。
        /// </summary>
        public MeasuredLine Line2 { get; set; }

        /// <summary>
        /// 拟合输出线。
        /// </summary>
        public MeasuredLine OutputLine { get; set; }

        /// <summary>
        /// 参与拟合或显示的源点。
        /// </summary>
        public List<PointF> SourcePoints { get; set; } = new List<PointF>();

        /// <summary>
        /// 两条源线的夹角差，单位为度。
        /// </summary>
        public double AngleDifference { get; set; }

        /// <summary>
        /// 平行中线模式下两条源线的距离。
        /// </summary>
        public double? LineDistance { get; set; }

        /// <summary>
        /// 输出线的拟合误差，单位为像素。
        /// </summary>
        public double FitError { get; set; }

        /// <summary>
        /// 算法耗时，单位为毫秒。
        /// </summary>
        public double AlgorithmMs { get; set; }
    }

    /// <summary>
    /// 线组合拟合算法。
    /// </summary>
    internal static class LineMergeFitAlgorithm
    {
        /// <summary>
        /// 执行两条源线的组合拟合。
        /// </summary>
        public static LineMergeFitMeasureResult Execute(
            MeasuredLine line1,
            MeasuredLine line2,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            if (param == null)
                throw new ArgumentNullException(nameof(param));
            if (line1 == null || !line1.IsValid)
                return Fail("第一条直线无效。");
            if (line2 == null || !line2.IsValid)
                return Fail("第二条直线无效。");

            var result = new LineMergeFitMeasureResult
            {
                Line1 = ApplyMode(line1, param.MeasureMode),
                Line2 = ApplyMode(line2, param.MeasureMode)
            };
            result.AngleDifference = CalculateAngleDifference(result.Line1, result.Line2);

            if (param.FitMode == LineMergeFitMode.ParallelCenterLine)
                FitParallelCenterLine(result, line1Points, line2Points, param);
            else
                FitCollinearMerge(result, line1Points, line2Points, param);

            return result;
        }

        /// <summary>
        /// 共线合并拟合，优先使用上游卡尺边缘点。
        /// </summary>
        private static void FitCollinearMerge(
            LineMergeFitMeasureResult result,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            List<PointF> points = BuildFitPoints(result.Line1, result.Line2, line1Points, line2Points, param);
            if (points.Count < 2)
            {
                MarkFail(result, "参与拟合的点数不足。");
                return;
            }

            List<PointF> stablePoints = BuildRobustLinePoints(points);
            if (stablePoints.Count < 2)
            {
                MarkFail(result, "有效拟合点数不足。");
                return;
            }

            FittedLine fitted = FitLineByPca(stablePoints);
            if (!fitted.IsValid)
            {
                MarkFail(result, "线拟合失败。");
                return;
            }

            result.SourcePoints = stablePoints;
            result.OutputLine = new MeasuredLine
            {
                Start = fitted.Start,
                End = fitted.End,
                Source = "LineMergeFit"
            };
            result.FitError = fitted.RmsError;
            result.Success = result.OutputLine.IsValid;
            result.Message = result.Success ? "OK" : "输出线段长度无效。";
        }

        /// <summary>
        /// 平行中线拟合，按两条线的对应端点取中点生成中线。
        /// </summary>
        private static void FitParallelCenterLine(
            LineMergeFitMeasureResult result,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            MeasuredLine line1 = result.Line1;
            MeasuredLine line2 = AlignLineDirection(line1, result.Line2);
            if (line1 == null || line2 == null || !line1.IsValid || !line2.IsValid)
            {
                MarkFail(result, "源线方向无效。");
                return;
            }

            PointF start = Midpoint(line1.Start, line2.Start);
            PointF end = Midpoint(line1.End, line2.End);
            List<PointF> sourcePoints = BuildEndpointPoints(line1, line2);
            result.OutputLine = new MeasuredLine
            {
                Start = GeometryMeasurementAlgorithm.ApplyMode(start, param.MeasureMode),
                End = GeometryMeasurementAlgorithm.ApplyMode(end, param.MeasureMode),
                Source = "LineMergeFit"
            };
            result.SourcePoints = sourcePoints.Select(point => GeometryMeasurementAlgorithm.ApplyMode(point, param.MeasureMode)).ToList();
            result.LineDistance = CalculateAverageEndpointDistance(line1, line2);
            result.FitError = CalculateRmsDistance(result.SourcePoints, result.OutputLine);
            result.Success = result.OutputLine.IsValid;
            result.Message = result.Success ? "OK" : "输出线段长度无效。";
        }

        /// <summary>
        /// 生成共线拟合点。
        /// </summary>
        private static List<PointF> BuildFitPoints(
            MeasuredLine line1,
            MeasuredLine line2,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            var points = new List<PointF>();
            if (param.PreferEdgePoints)
            {
                AddValidPoints(points, line1Points, param.MeasureMode);
                AddValidPoints(points, line2Points, param.MeasureMode);
            }

            if (points.Count >= 2)
                return points;

            return BuildEndpointPoints(line1, line2);
        }

        /// <summary>
        /// 生成平行中线的投影范围点。
        /// </summary>
        private static List<PointF> BuildExtentPoints(
            MeasuredLine line1,
            MeasuredLine line2,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            var points = new List<PointF>();
            if (param.PreferEdgePoints)
            {
                AddValidPoints(points, line1Points, param.MeasureMode);
                AddValidPoints(points, line2Points, param.MeasureMode);
            }

            if (points.Count >= 2)
                return points;

            return BuildEndpointPoints(line1, line2);
        }

        /// <summary>
        /// 使用源线端点生成拟合点。
        /// </summary>
        private static List<PointF> BuildEndpointPoints(MeasuredLine line1, MeasuredLine line2)
        {
            return new List<PointF>
            {
                line1.Start,
                line1.End,
                line2.Start,
                line2.End
            };
        }

        /// <summary>
        /// 添加有效点并按精度模式处理。
        /// </summary>
        private static void AddValidPoints(List<PointF> points, IEnumerable<PointF> source, GeometryMeasureMode mode)
        {
            if (source == null)
                return;

            foreach (PointF point in source)
            {
                if (float.IsNaN(point.X) || float.IsNaN(point.Y) ||
                    float.IsInfinity(point.X) || float.IsInfinity(point.Y))
                {
                    continue;
                }

                points.Add(GeometryMeasurementAlgorithm.ApplyMode(point, mode));
            }
        }

        /// <summary>
        /// PCA 拟合一条覆盖输入点投影范围的线段。
        /// </summary>
        private static FittedLine FitLineByPca(IList<PointF> points)
        {
            if (points == null || points.Count < 2)
                return new FittedLine();

            double centerX = points.Average(point => point.X);
            double centerY = points.Average(point => point.Y);
            double xx = 0;
            double xy = 0;
            double yy = 0;

            foreach (PointF point in points)
            {
                double dx = point.X - centerX;
                double dy = point.Y - centerY;
                xx += dx * dx;
                xy += dx * dy;
                yy += dy * dy;
            }

            double angle = 0.5 * Math.Atan2(2.0 * xy, xx - yy);
            PointF direction = new PointF((float)Math.Cos(angle), (float)Math.Sin(angle));
            PointF center = new PointF((float)centerX, (float)centerY);
            double minProjection = double.MaxValue;
            double maxProjection = double.MinValue;

            foreach (PointF point in points)
            {
                double projection = Dot(Subtract(point, center), direction);
                minProjection = Math.Min(minProjection, projection);
                maxProjection = Math.Max(maxProjection, projection);
            }

            PointF start = Add(center, Scale(direction, minProjection));
            PointF end = Add(center, Scale(direction, maxProjection));
            var line = new MeasuredLine { Start = start, End = end };
            return new FittedLine
            {
                IsValid = line.IsValid,
                Start = start,
                End = end,
                RmsError = CalculateRmsDistance(points, line)
            };
        }

        /// <summary>
        /// 对共线拟合点做残差剔除，避免少量误检边缘点拉偏输出线角度。
        /// </summary>
        private static List<PointF> BuildRobustLinePoints(List<PointF> sourcePoints)
        {
            List<PointF> points = sourcePoints == null ? new List<PointF>() : sourcePoints.ToList();
            int minimumCount = Math.Max(2, (int)Math.Ceiling(points.Count * 0.6));
            for (int iteration = 0; iteration < 2 && points.Count >= 3; iteration++)
            {
                FittedLine fit = FitLineByPca(points);
                if (!fit.IsValid)
                    break;

                var line = new MeasuredLine { Start = fit.Start, End = fit.End };
                List<double> distances = points.Select(point => DistanceToLine(point, line)).ToList();
                double threshold = BuildRobustDistanceThreshold(distances);
                List<PointF> filtered = points.Where((point, index) => distances[index] <= threshold).ToList();
                if (filtered.Count < minimumCount || filtered.Count == points.Count)
                    break;

                points = filtered;
            }

            return points;
        }

        /// <summary>
        /// 平行中线模式下保留靠近任意一条源线的点，过滤反光造成的离群边缘点。
        /// </summary>
        private static List<PointF> FilterPointsNearEitherLine(List<PointF> sourcePoints, MeasuredLine line1, MeasuredLine line2)
        {
            if (sourcePoints == null || sourcePoints.Count < 4)
                return sourcePoints ?? new List<PointF>();

            List<double> distances = sourcePoints
                .Select(point => Math.Min(DistanceToLine(point, line1), DistanceToLine(point, line2)))
                .ToList();
            double threshold = BuildRobustDistanceThreshold(distances);
            int minimumCount = Math.Max(2, (int)Math.Ceiling(sourcePoints.Count * 0.6));
            List<PointF> filtered = sourcePoints.Where((point, index) => distances[index] <= threshold).ToList();
            return filtered.Count >= minimumCount ? filtered : sourcePoints;
        }

        /// <summary>
        /// 按中位数绝对偏差生成鲁棒距离阈值。
        /// </summary>
        private static double BuildRobustDistanceThreshold(List<double> distances)
        {
            if (distances == null || distances.Count == 0)
                return 0;

            double median = Median(distances);
            List<double> deviations = distances.Select(distance => Math.Abs(distance - median)).ToList();
            double sigma = 1.4826 * Median(deviations);
            return Math.Max(1.5, median + 3.0 * sigma);
        }

        /// <summary>
        /// 计算点到线的均方根距离。
        /// </summary>
        private static double CalculateRmsDistance(IList<PointF> points, MeasuredLine line)
        {
            if (points == null || points.Count == 0 || line == null || !line.IsValid)
                return 0;

            PointF direction = Normalize(Subtract(line.End, line.Start));
            PointF normal = new PointF(-direction.Y, direction.X);
            double sum = 0;
            foreach (PointF point in points)
            {
                double distance = Dot(Subtract(point, line.Start), normal);
                sum += distance * distance;
            }

            return Math.Sqrt(sum / points.Count);
        }

        /// <summary>
        /// 计算点到直线的垂直距离。
        /// </summary>
        private static double DistanceToLine(PointF point, MeasuredLine line)
        {
            if (line == null || !line.IsValid)
                return double.MaxValue;

            PointF direction = Subtract(line.End, line.Start);
            double length = Length(direction);
            if (length < 1e-8)
                return double.MaxValue;

            return Math.Abs((point.X - line.Start.X) * direction.Y - (point.Y - line.Start.Y) * direction.X) / length;
        }

        /// <summary>
        /// 计算中位数。
        /// </summary>
        private static double Median(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;

            List<double> sorted = values.OrderBy(value => value).ToList();
            int middle = sorted.Count / 2;
            if (sorted.Count % 2 == 1)
                return sorted[middle];

            return (sorted[middle - 1] + sorted[middle]) / 2.0;
        }

        /// <summary>
        /// 计算两条线的最小方向差。
        /// </summary>
        private static double CalculateAngleDifference(MeasuredLine line1, MeasuredLine line2)
        {
            double angle1 = Math.Atan2(line1.End.Y - line1.Start.Y, line1.End.X - line1.Start.X) * 180.0 / Math.PI;
            double angle2 = Math.Atan2(line2.End.Y - line2.Start.Y, line2.End.X - line2.Start.X) * 180.0 / Math.PI;
            double diff = Math.Abs(angle1 - angle2) % 180.0;
            return diff > 90.0 ? 180.0 - diff : diff;
        }

        /// <summary>
        /// 按精度模式处理源线端点。
        /// </summary>
        private static MeasuredLine ApplyMode(MeasuredLine line, GeometryMeasureMode mode)
        {
            return new MeasuredLine
            {
                Start = GeometryMeasurementAlgorithm.ApplyMode(line.Start, mode),
                End = GeometryMeasurementAlgorithm.ApplyMode(line.End, mode),
                Source = line.Source
            };
        }

        /// <summary>
        /// 将第二条线的方向调整为与参考线同向，保证起点配起点、终点配终点。
        /// </summary>
        private static MeasuredLine AlignLineDirection(MeasuredLine referenceLine, MeasuredLine targetLine)
        {
            if (referenceLine == null || targetLine == null || !referenceLine.IsValid || !targetLine.IsValid)
                return targetLine;

            PointF referenceDirection = Normalize(Subtract(referenceLine.End, referenceLine.Start));
            PointF targetDirection = Normalize(Subtract(targetLine.End, targetLine.Start));
            if (Length(referenceDirection) < 1e-8 || Length(targetDirection) < 1e-8 || Dot(referenceDirection, targetDirection) >= 0)
                return targetLine;

            return new MeasuredLine
            {
                Start = targetLine.End,
                End = targetLine.Start,
                Source = targetLine.Source
            };
        }

        /// <summary>
        /// 计算两个对应端点的中点。
        /// </summary>
        private static PointF Midpoint(PointF point1, PointF point2)
        {
            return new PointF((point1.X + point2.X) / 2F, (point1.Y + point2.Y) / 2F);
        }

        /// <summary>
        /// 计算两条对应端点连线的平均距离，用作平行中线模式的线间距。
        /// </summary>
        private static double CalculateAverageEndpointDistance(MeasuredLine line1, MeasuredLine line2)
        {
            if (line1 == null || line2 == null || !line1.IsValid || !line2.IsValid)
                return 0;

            return (GeometryMeasurementAlgorithm.Distance(line1.Start, line2.Start) +
                    GeometryMeasurementAlgorithm.Distance(line1.End, line2.End)) / 2.0;
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        private static LineMergeFitMeasureResult Fail(string message)
        {
            var result = new LineMergeFitMeasureResult();
            MarkFail(result, message);
            return result;
        }

        /// <summary>
        /// 标记结果失败。
        /// </summary>
        private static void MarkFail(LineMergeFitMeasureResult result, string message)
        {
            result.Success = false;
            result.Message = message;
        }

        private static PointF Add(PointF a, PointF b)
        {
            return new PointF(a.X + b.X, a.Y + b.Y);
        }

        private static PointF Subtract(PointF a, PointF b)
        {
            return new PointF(a.X - b.X, a.Y - b.Y);
        }

        private static PointF Scale(PointF point, double scale)
        {
            return new PointF((float)(point.X * scale), (float)(point.Y * scale));
        }

        private static double Dot(PointF a, PointF b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        private static double Length(PointF point)
        {
            return Math.Sqrt(point.X * point.X + point.Y * point.Y);
        }

        private static PointF Normalize(PointF point)
        {
            double length = Length(point);
            if (length < 1e-8)
                return PointF.Empty;

            return new PointF((float)(point.X / length), (float)(point.Y / length));
        }

        /// <summary>
        /// 拟合线段临时数据。
        /// </summary>
        private class FittedLine
        {
            /// <summary>
            /// 是否拟合成功。
            /// </summary>
            public bool IsValid { get; set; }

            /// <summary>
            /// 拟合线段起点。
            /// </summary>
            public PointF Start { get; set; }

            /// <summary>
            /// 拟合线段终点。
            /// </summary>
            public PointF End { get; set; }

            /// <summary>
            /// 均方根误差。
            /// </summary>
            public double RmsError { get; set; }
        }
    }
}
