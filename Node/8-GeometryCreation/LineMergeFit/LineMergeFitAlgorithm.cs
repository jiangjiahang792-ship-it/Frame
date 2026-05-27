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

            FittedLine fitted = FitLineByPca(points);
            if (!fitted.IsValid)
            {
                MarkFail(result, "线拟合失败。");
                return;
            }

            result.SourcePoints = points;
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
        /// 平行中线拟合，输出位于两条源线中间的基准线段。
        /// </summary>
        private static void FitParallelCenterLine(
            LineMergeFitMeasureResult result,
            IEnumerable<PointF> line1Points,
            IEnumerable<PointF> line2Points,
            NodeParamLineMergeFit param)
        {
            PointF u1 = Normalize(Subtract(result.Line1.End, result.Line1.Start));
            PointF u2 = Normalize(Subtract(result.Line2.End, result.Line2.Start));
            if (Length(u1) < 1e-8 || Length(u2) < 1e-8)
            {
                MarkFail(result, "源线方向无效。");
                return;
            }

            if (Dot(u1, u2) < 0)
                u2 = Scale(u2, -1);

            PointF direction = Normalize(Add(u1, u2));
            if (Length(direction) < 1e-8)
                direction = u1;

            PointF normal = new PointF(-direction.Y, direction.X);
            List<PointF> extentPoints = BuildExtentPoints(result.Line1, result.Line2, line1Points, line2Points, param);
            if (extentPoints.Count < 2)
            {
                MarkFail(result, "参与拟合的点数不足。");
                return;
            }

            double line1Offset = AverageOffset(result.Line1, normal);
            double line2Offset = AverageOffset(result.Line2, normal);
            double centerOffset = (line1Offset + line2Offset) / 2.0;
            double minProjection = extentPoints.Min(point => Dot(point, direction));
            double maxProjection = extentPoints.Max(point => Dot(point, direction));
            PointF start = Add(Scale(direction, minProjection), Scale(normal, centerOffset));
            PointF end = Add(Scale(direction, maxProjection), Scale(normal, centerOffset));

            result.SourcePoints = extentPoints.Select(point => GeometryMeasurementAlgorithm.ApplyMode(point, param.MeasureMode)).ToList();
            result.OutputLine = new MeasuredLine
            {
                Start = GeometryMeasurementAlgorithm.ApplyMode(start, param.MeasureMode),
                End = GeometryMeasurementAlgorithm.ApplyMode(end, param.MeasureMode),
                Source = "LineMergeFit"
            };
            result.LineDistance = Math.Abs(line2Offset - line1Offset);
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
        /// 计算线段在指定法向量上的平均偏移。
        /// </summary>
        private static double AverageOffset(MeasuredLine line, PointF normal)
        {
            return (Dot(line.Start, normal) + Dot(line.End, normal)) / 2.0;
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
