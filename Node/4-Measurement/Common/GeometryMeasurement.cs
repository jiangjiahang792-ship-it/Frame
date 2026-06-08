using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    public enum GeometryMeasureMode
    {
        SubPixel,
        Pixel
    }

    public enum MeasurementDataSourceMode
    {
        Subscribe,
        Draw
    }

    public enum MeasurementPointRole
    {
        Auto,
        Center,
        StartPoint,
        EndPoint
    }

    public class MeasuredLine
    {
        public PointF Start { get; set; }
        public PointF End { get; set; }
        public string Source { get; set; }
        public bool IsValid { get { return GeometryMeasurementAlgorithm.Distance(Start, End) > 0.0001; } }
    }

    public class PointRegionDistanceResult
    {
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }
        public bool IsInsideRegion { get; set; }
        public PointF TargetPoint { get; set; }
        public PointF NearestPoint { get; set; }
        public PointF FarthestPoint { get; set; }
        public List<PointF> RegionPoints { get; set; } = new List<PointF>();
    }

    /// <summary>
    /// 点到线距离计算结果。
    /// </summary>
    public class PointLineDistanceResult
    {
        /// <summary>
        /// 点到无限直线的垂直距离。
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// 参与计算的目标点。
        /// </summary>
        public PointF TargetPoint { get; set; }

        /// <summary>
        /// 参与计算的直线起点。
        /// </summary>
        public PointF LineStart { get; set; }

        /// <summary>
        /// 参与计算的直线终点。
        /// </summary>
        public PointF LineEnd { get; set; }

        /// <summary>
        /// 目标点投影到无限直线上的垂足。
        /// </summary>
        public PointF FootPoint { get; set; }
    }

    public static class GeometryMeasurementAlgorithm
    {
        public static double CalculateLineLineAngle(
            PointF line1Start,
            PointF line1End,
            PointF line2Start,
            PointF line2End,
            GeometryMeasureMode mode,
            out PointF usedLine1Start,
            out PointF usedLine1End,
            out PointF usedLine2Start,
            out PointF usedLine2End,
            out PointF? intersectionPoint)
        {
            usedLine1Start = ApplyMode(line1Start, mode);
            usedLine1End = ApplyMode(line1End, mode);
            usedLine2Start = ApplyMode(line2Start, mode);
            usedLine2End = ApplyMode(line2End, mode);
            intersectionPoint = null;

            double a1 = usedLine1End.Y - usedLine1Start.Y;
            double b1 = usedLine1Start.X - usedLine1End.X;
            double c1 = a1 * usedLine1Start.X + b1 * usedLine1Start.Y;

            double a2 = usedLine2End.Y - usedLine2Start.Y;
            double b2 = usedLine2Start.X - usedLine2End.X;
            double c2 = a2 * usedLine2Start.X + b2 * usedLine2Start.Y;

            double determinant = a1 * b2 - a2 * b1;
            if (Math.Abs(determinant) > 1e-8)
            {
                intersectionPoint = new PointF(
                    (float)((b2 * c1 - b1 * c2) / determinant),
                    (float)((a1 * c2 - a2 * c1) / determinant));
            }

            double dx1 = usedLine1End.X - usedLine1Start.X;
            double dy1 = usedLine1End.Y - usedLine1Start.Y;
            double dx2 = usedLine2End.X - usedLine2Start.X;
            double dy2 = usedLine2End.Y - usedLine2Start.Y;
            double mag1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
            double mag2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
            if (mag1 < 1e-8 || mag2 < 1e-8)
                return 0;

            double cosTheta = Math.Abs((dx1 * dx2 + dy1 * dy2) / (mag1 * mag2));
            cosTheta = Math.Max(-1.0, Math.Min(1.0, cosTheta));
            return Math.Acos(cosTheta) * 180.0 / Math.PI;
        }

        public static double CalculatePointDistance(
            PointF point1,
            PointF point2,
            GeometryMeasureMode mode,
            out PointF usedPoint1,
            out PointF usedPoint2)
        {
            usedPoint1 = ApplyMode(point1, mode);
            usedPoint2 = ApplyMode(point2, mode);
            return Distance(usedPoint1, usedPoint2);
        }

        /// <summary>
        /// 计算点到无限直线的垂直距离。
        /// </summary>
        public static PointLineDistanceResult CalculatePointLineDistance(
            PointF targetPoint,
            PointF lineStart,
            PointF lineEnd,
            GeometryMeasureMode mode)
        {
            PointF usedPoint = ApplyMode(targetPoint, mode);
            PointF usedStart = ApplyMode(lineStart, mode);
            PointF usedEnd = ApplyMode(lineEnd, mode);
            double dx = usedEnd.X - usedStart.X;
            double dy = usedEnd.Y - usedStart.Y;
            double lengthSq = dx * dx + dy * dy;
            if (lengthSq < 1e-12)
                throw new ArgumentException("直线起点和终点不能重合。");

            double t = ((usedPoint.X - usedStart.X) * dx + (usedPoint.Y - usedStart.Y) * dy) / lengthSq;
            PointF foot = new PointF((float)(usedStart.X + t * dx), (float)(usedStart.Y + t * dy));
            return new PointLineDistanceResult
            {
                Distance = Distance(usedPoint, foot),
                TargetPoint = usedPoint,
                LineStart = usedStart,
                LineEnd = usedEnd,
                FootPoint = foot
            };
        }

        public static PointRegionDistanceResult CalculatePointRegionDistance(
            IList<PointF> regionPoints,
            PointF targetPoint,
            GeometryMeasureMode mode)
        {
            if (regionPoints == null || regionPoints.Count < 3)
                throw new ArgumentException("区域至少需要 3 个点。");

            List<PointF> usedRegion = regionPoints.Select(point => ApplyMode(point, mode)).ToList();
            PointF usedTarget = ApplyMode(targetPoint, mode);
            bool inside = IsPointInsidePolygon(usedRegion, usedTarget);

            double minDistance = double.MaxValue;
            PointF nearestPoint = usedRegion[0];
            for (int index = 0; index < usedRegion.Count; index++)
            {
                PointF a = usedRegion[index];
                PointF b = usedRegion[(index + 1) % usedRegion.Count];
                double distance = PointToSegmentDistance(usedTarget, a, b, out PointF closestPoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestPoint = closestPoint;
                }
            }

            if (inside)
                minDistance = 0;

            double maxDistance = 0;
            PointF farthestPoint = usedRegion[0];
            foreach (PointF point in usedRegion)
            {
                double distance = Distance(usedTarget, point);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthestPoint = point;
                }
            }

            return new PointRegionDistanceResult
            {
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                IsInsideRegion = inside,
                TargetPoint = usedTarget,
                NearestPoint = nearestPoint,
                FarthestPoint = farthestPoint,
                RegionPoints = usedRegion
            };
        }

        public static PointF ApplyMode(PointF point, GeometryMeasureMode mode)
        {
            if (mode == GeometryMeasureMode.Pixel)
                return new PointF((float)Math.Round(point.X), (float)Math.Round(point.Y));

            return point;
        }

        public static double Distance(PointF point1, PointF point2)
        {
            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static List<PointF> TransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correctionInfo)
        {
            if (points == null)
                return new List<PointF>();
            if (correctionInfo == null)
                return points.ToList();

            return points.Select(point => correctionInfo.TransformPoint(point.X, point.Y)).ToList();
        }

        public static List<PointF> InverseTransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correctionInfo)
        {
            if (points == null)
                return new List<PointF>();
            if (correctionInfo == null)
                return points.ToList();

            return points.Select(point => correctionInfo.InverseTransformPoint(point.X, point.Y)).ToList();
        }

        private static bool IsPointInsidePolygon(IList<PointF> polygon, PointF point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                PointF pi = polygon[i];
                PointF pj = polygon[j];
                bool crosses = (pi.Y > point.Y) != (pj.Y > point.Y);
                if (crosses)
                {
                    double x = (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X;
                    if (point.X < x)
                        inside = !inside;
                }
            }

            return inside;
        }

        private static double PointToSegmentDistance(PointF point, PointF a, PointF b, out PointF closestPoint)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-12)
            {
                closestPoint = a;
                return Distance(point, a);
            }

            double t = ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lenSq;
            t = Math.Max(0, Math.Min(1, t));
            closestPoint = new PointF((float)(a.X + t * dx), (float)(a.Y + t * dy));
            return Distance(point, closestPoint);
        }
    }

    public static class MeasurementResultReader
    {
        public static bool TryReadLine(INodeResult result, out MeasuredLine line)
        {
            line = null;
            if (result == null)
                return false;
            if (IsExplicitNgResult(result))
                return false;

            if (TryReadPointPair(result, "StartX", "StartY", out PointF start) &&
                TryReadPointPair(result, "EndX", "EndY", out PointF end))
            {
                line = new MeasuredLine { Start = start, End = end, Source = result.GetType().Name };
                return line.IsValid;
            }

            AlgorithmResult algorithmResult = TryGetAlgorithmResult(result);
            if (algorithmResult != null && algorithmResult.Lines.Count > 0)
            {
                ColorLine colorLine = algorithmResult.Lines[0];
                line = new MeasuredLine { Start = colorLine.P1, End = colorLine.P2, Source = result.GetType().Name };
                return line.IsValid;
            }

            return false;
        }

        /// <summary>
        /// 读取测量结果中的线点集，只读取确实落在输出线上的点，避免把几何生成节点的辅助显示点误当成线点。
        /// </summary>
        public static bool TryReadLinePoints(INodeResult result, out List<PointF> points)
        {
            points = null;
            if (result == null)
                return false;
            if (IsExplicitNgResult(result))
                return false;

            if (TryReadPointListProperty(result, "EdgePoints", 2, out points) ||
                TryReadPointListProperty(result, "Points", 2, out points))
            {
                return true;
            }

            return false;
        }

        public static bool TryReadPoint(INodeResult result, MeasurementPointRole role, out PointF point)
        {
            point = PointF.Empty;
            if (result == null)
                return false;
            if (IsExplicitNgResult(result))
                return false;

            if ((role == MeasurementPointRole.Center || role == MeasurementPointRole.Auto) &&
                (TryReadPointPair(result, "CenterX", "CenterY", out point) ||
                 TryReadPointPair(result, "MatchX", "MatchY", out point) ||
                 TryReadPointPair(result, "PointX", "PointY", out point) ||
                 TryReadPointPair(result, "X", "Y", out point)))
            {
                return true;
            }

            if ((role == MeasurementPointRole.StartPoint || role == MeasurementPointRole.Auto) &&
                TryReadPointPair(result, "StartX", "StartY", out point))
            {
                return true;
            }

            if ((role == MeasurementPointRole.EndPoint || role == MeasurementPointRole.Auto) &&
                TryReadPointPair(result, "EndX", "EndY", out point))
            {
                return true;
            }

            // FindPoint 等点集型结果会输出 Points，订阅为点时按角色读取首点、末点或中心。
            if (TryReadPointListProperty(result, "Points", out List<PointF> resultPoints) &&
                TryPickPointFromList(resultPoints, role, out point))
            {
                return true;
            }

            AlgorithmResult algorithmResult = TryGetAlgorithmResult(result);
            if (algorithmResult != null)
            {
                if (algorithmResult.Circles.Count > 0)
                {
                    List<PointF> circlePoints = algorithmResult.Circles.Select(circle => circle.Center).ToList();
                    if (TryPickPointFromList(circlePoints, role, out point))
                        return true;
                }

                if (algorithmResult.Lines.Count > 0)
                {
                    ColorLine line = algorithmResult.Lines[0];
                    if (role == MeasurementPointRole.StartPoint)
                        point = line.P1;
                    else if (role == MeasurementPointRole.EndPoint)
                        point = line.P2;
                    else
                        point = new PointF((line.P1.X + line.P2.X) / 2F, (line.P1.Y + line.P2.Y) / 2F);
                    return true;
                }

                if (algorithmResult.Contours.Count > 0 && algorithmResult.Contours[0].Points.Count > 0)
                {
                    List<PointF> points = algorithmResult.Contours[0].Points;
                    if (TryPickPointFromList(points, role, out point))
                        return true;
                }
            }

            return false;
        }

        public static bool TryReadRegion(INodeResult result, out List<PointF> regionPoints)
        {
            regionPoints = null;
            if (result == null)
                return false;
            if (IsExplicitNgResult(result))
                return false;

            if (TryReadPointListProperty(result, "RegionPoints", out regionPoints) ||
                TryReadPointListProperty(result, "Points", out regionPoints))
            {
                return regionPoints.Count >= 3;
            }

            AlgorithmResult algorithmResult = TryGetAlgorithmResult(result);
            if (algorithmResult != null && algorithmResult.Contours.Count > 0)
            {
                regionPoints = algorithmResult.Contours[0].Points.ToList();
                return regionPoints.Count >= 3;
            }

            return false;
        }

        /// <summary>
        /// 从点集合中按订阅角色选出一个点。
        /// </summary>
        private static bool TryPickPointFromList(IList<PointF> points, MeasurementPointRole role, out PointF point)
        {
            point = PointF.Empty;
            if (points == null || points.Count == 0)
                return false;

            switch (role)
            {
                case MeasurementPointRole.Center:
                    point = new PointF(points.Average(p => p.X), points.Average(p => p.Y));
                    return true;
                case MeasurementPointRole.EndPoint:
                    point = points[points.Count - 1];
                    return true;
                case MeasurementPointRole.StartPoint:
                case MeasurementPointRole.Auto:
                default:
                    point = points[0];
                    return true;
            }
        }

        private static AlgorithmResult TryGetAlgorithmResult(object source)
        {
            if (source == null)
                return null;

            AlgorithmResult direct = source as AlgorithmResult;
            if (direct != null)
                return direct;

            Type type = source.GetType();
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0 ||
                    !typeof(AlgorithmResult).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                AlgorithmResult value = property.GetValue(source, null) as AlgorithmResult;
                if (value != null)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// 测量节点显式输出 NG 时，下游不再从显示图形里回退读取几何点，避免把边缘点误当作有效测量值。
        /// </summary>
        private static bool IsExplicitNgResult(INodeResult result)
        {
            if (TryReadBoolean(result, "IsOk", out bool isOk))
                return !isOk;

            return false;
        }

        private static bool TryReadPointPair(object source, string xName, string yName, out PointF point)
        {
            point = PointF.Empty;
            if (TryReadDouble(source, xName, out double x) &&
                TryReadDouble(source, yName, out double y))
            {
                point = new PointF((float)x, (float)y);
                return true;
            }

            return false;
        }

        private static bool TryReadBoolean(object source, string memberName, out bool value)
        {
            value = false;
            if (source == null)
                return false;

            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                return false;

            object rawValue = property.GetValue(source, null);
            if (rawValue == null)
                return false;

            try
            {
                value = Convert.ToBoolean(rawValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadDouble(object source, string memberName, out double value)
        {
            value = 0;
            if (source == null)
                return false;

            Type type = source.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                return false;

            object rawValue = property.GetValue(source, null);
            if (rawValue == null)
                return false;

            try
            {
                value = Convert.ToDouble(rawValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadPointListProperty(object source, string propertyName, out List<PointF> points)
        {
            return TryReadPointListProperty(source, propertyName, 3, out points);
        }

        private static bool TryReadPointListProperty(object source, string propertyName, int minCount, out List<PointF> points)
        {
            points = null;
            PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanRead || property.GetIndexParameters().Length > 0)
                return false;

            object value = property.GetValue(source, null);
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
                return false;

            points = new List<PointF>();
            foreach (object item in enumerable)
            {
                if (item is PointF)
                {
                    points.Add((PointF)item);
                    continue;
                }

                if (TryReadPointPair(item, "X", "Y", out PointF point))
                    points.Add(point);
            }

            return points.Count >= minCount;
        }
    }
}
