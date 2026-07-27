using System;
using System.Collections.Generic;
using System.Drawing;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 定义多目标测量所需的点集正向变换、逆向变换和 ROI 目标归属能力。
    /// </summary>
    public interface IMultiTargetTransformService
    {
        /// <summary>
        /// 把一个基准点变换到指定目标。
        /// </summary>
        PointF TransformPoint(PointF point, PositionCorrectionInfo correction);

        /// <summary>
        /// 把一个目标点逆变换到基准坐标系。
        /// </summary>
        PointF InverseTransformPoint(PointF point, PositionCorrectionInfo correction);

        /// <summary>
        /// 把一组基准点变换到指定目标。
        /// </summary>
        List<PointF> TransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction);

        /// <summary>
        /// 把一组目标点逆变换到基准坐标系。
        /// </summary>
        List<PointF> InverseTransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction);

        /// <summary>
        /// 根据 ROI 中心解析它所属的当前模板目标索引。
        /// </summary>
        int ResolveAnchorIndex(PointF roiCenter, IReadOnlyList<PositionCorrectionInfo> corrections);
    }

    /// <summary>
    /// 使用目标中心、角度和尺度实现默认多目标二维变换。
    /// </summary>
    public sealed class MultiTargetTransformService : IMultiTargetTransformService
    {
        /// <inheritdoc />
        public PointF TransformPoint(PointF point, PositionCorrectionInfo correction)
        {
            PositionCorrectionHelper.EnsureValid(correction);
            return correction.TransformPoint(point.X, point.Y);
        }

        /// <inheritdoc />
        public PointF InverseTransformPoint(PointF point, PositionCorrectionInfo correction)
        {
            PositionCorrectionHelper.EnsureValid(correction);
            return correction.InverseTransformPoint(point.X, point.Y);
        }

        /// <inheritdoc />
        public List<PointF> TransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction)
        {
            if (points == null)
                throw new ArgumentNullException("points");

            var result = new List<PointF>();
            foreach (PointF point in points)
                result.Add(TransformPoint(point, correction));
            return result;
        }

        /// <inheritdoc />
        public List<PointF> InverseTransformPoints(IEnumerable<PointF> points, PositionCorrectionInfo correction)
        {
            if (points == null)
                throw new ArgumentNullException("points");

            var result = new List<PointF>();
            foreach (PointF point in points)
                result.Add(InverseTransformPoint(point, correction));
            return result;
        }

        /// <inheritdoc />
        public int ResolveAnchorIndex(PointF roiCenter, IReadOnlyList<PositionCorrectionInfo> corrections)
        {
            if (corrections == null || corrections.Count == 0)
                throw new InvalidOperationException("没有可用的模板目标，无法确定测量 ROI 所属目标。");

            int containedIndex = -1;
            double containedDistance = double.MaxValue;
            int nearestIndex = -1;
            double nearestDistance = double.MaxValue;

            for (int i = 0; i < corrections.Count; i++)
            {
                PositionCorrectionInfo correction = corrections[i];
                if (correction == null || !correction.IsValid)
                    continue;

                double dx = roiCenter.X - correction.CurrentX;
                double dy = roiCenter.Y - correction.CurrentY;
                double distance = dx * dx + dy * dy;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }

                if (!ContainsPoint(correction, dx, dy) || distance >= containedDistance)
                    continue;

                containedDistance = distance;
                containedIndex = i;
            }

            if (containedIndex >= 0)
                return containedIndex;
            if (nearestIndex >= 0)
                return nearestIndex;
            throw new InvalidOperationException("模板目标位姿无效，无法确定测量 ROI 所属目标。");
        }

        /// <summary>
        /// 判断相对目标中心的点是否位于旋转匹配框内部。
        /// </summary>
        private static bool ContainsPoint(PositionCorrectionInfo correction, double dx, double dy)
        {
            if (correction.TargetWidth <= 0 || correction.TargetHeight <= 0)
                return false;

            double radians = correction.CurrentAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;
            return Math.Abs(localX) <= correction.TargetWidth / 2.0 &&
                Math.Abs(localY) <= correction.TargetHeight / 2.0;
        }
    }
}
