using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 测量结果输出舍入工具，统一控制下游订阅结果的小数位。
    /// </summary>
    public static class MeasurementResultRounder
    {
        /// <summary>
        /// 测量结果对外输出时保留的小数位数。
        /// </summary>
        public const int DecimalPlaces = 3;

        /// <summary>
        /// 将双精度数值按统一小数位舍入。
        /// </summary>
        public static double Round(double value)
        {
            return Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 将可空双精度数值按统一小数位舍入。
        /// </summary>
        public static double? Round(double? value)
        {
            return value.HasValue ? (double?)Round(value.Value) : null;
        }

        /// <summary>
        /// 将点坐标按统一小数位舍入，用于对外发布点集合。
        /// </summary>
        public static PointF RoundPoint(PointF point)
        {
            return new PointF((float)Round(point.X), (float)Round(point.Y));
        }

        /// <summary>
        /// 将点集合按统一小数位舍入。
        /// </summary>
        public static List<PointF> RoundPoints(IEnumerable<PointF> points)
        {
            if (points == null)
                return new List<PointF>();

            return points.Select(RoundPoint).ToList();
        }

        /// <summary>
        /// 将多组点集合按统一小数位舍入。
        /// </summary>
        public static List<List<PointF>> RoundPointGroups(IEnumerable<IEnumerable<PointF>> pointGroups)
        {
            if (pointGroups == null)
                return new List<List<PointF>>();

            return pointGroups.Select(RoundPoints).ToList();
        }
    }
}
