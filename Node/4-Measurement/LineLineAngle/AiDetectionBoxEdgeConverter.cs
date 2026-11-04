using System;
using System.Drawing;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    /// <summary>将 AI 检测旋转框的指定边转换为测量直线。</summary>
    internal static class AiDetectionBoxEdgeConverter
    {
        /// <summary>从 AI 算法结果的第一个检测框读取指定框边。</summary>
        public static bool TryReadFirstBoxEdge(AlgorithmResult algorithmResult, AiDetectionBoxEdge edge, out MeasuredLine line)
        {
            line = null;
            if (algorithmResult?.Rects == null || algorithmResult.Rects.Count == 0 || algorithmResult.Rects[0] == null)
                return false;

            var rect = algorithmResult.Rects[0].RotatedRect;
            float halfWidth = rect.size.width * 0.5F;
            float halfHeight = rect.size.height * 0.5F;
            if (halfWidth <= 0F || halfHeight <= 0F)
                return false;

            double radians = rect.angle * Math.PI / 180D;
            var widthVector = new PointF((float)Math.Cos(radians), (float)Math.Sin(radians));
            var heightVector = new PointF(-widthVector.Y, widthVector.X);
            var center = new PointF(rect.center.x, rect.center.y);
            PointF start;
            PointF end;
            switch (edge)
            {
                case AiDetectionBoxEdge.Bottom:
                    start = Add(center, Scale(heightVector, halfHeight), Scale(widthVector, -halfWidth));
                    end = Add(center, Scale(heightVector, halfHeight), Scale(widthVector, halfWidth));
                    break;
                case AiDetectionBoxEdge.Left:
                    start = Add(center, Scale(widthVector, -halfWidth), Scale(heightVector, -halfHeight));
                    end = Add(center, Scale(widthVector, -halfWidth), Scale(heightVector, halfHeight));
                    break;
                case AiDetectionBoxEdge.Right:
                    start = Add(center, Scale(widthVector, halfWidth), Scale(heightVector, -halfHeight));
                    end = Add(center, Scale(widthVector, halfWidth), Scale(heightVector, halfHeight));
                    break;
                default:
                    start = Add(center, Scale(heightVector, -halfHeight), Scale(widthVector, -halfWidth));
                    end = Add(center, Scale(heightVector, -halfHeight), Scale(widthVector, halfWidth));
                    break;
            }

            line = new MeasuredLine { Start = start, End = end, Source = "AI首个检测框" };
            return line.IsValid;
        }

        /// <summary>累加中心点和两个方向向量。</summary>
        private static PointF Add(PointF center, PointF first, PointF second) =>
            new PointF(center.X + first.X + second.X, center.Y + first.Y + second.Y);

        /// <summary>按指定长度缩放单位方向向量。</summary>
        private static PointF Scale(PointF vector, float length) =>
            new PointF(vector.X * length, vector.Y * length);
    }
}
