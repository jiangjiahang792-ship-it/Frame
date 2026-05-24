using System;
using System.Drawing;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    public class PositionCorrectionInfo
    {
        public bool IsValid { get; set; }
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double BaseAngle { get; set; }
        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public double CurrentAngle { get; set; }
        public double DeltaX { get { return CurrentX - BaseX; } }
        public double DeltaY { get { return CurrentY - BaseY; } }
        public double DeltaAngle { get { return PositionCorrectionHelper.NormalizeAngle(CurrentAngle - BaseAngle); } }

        public PointF TransformPoint(float x, float y)
        {
            if (!IsValid)
                throw new InvalidOperationException("位置修正信息无效。");

            double radians = DeltaAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = x - BaseX;
            double localY = y - BaseY;
            double correctedX = CurrentX + localX * cos - localY * sin;
            double correctedY = CurrentY + localX * sin + localY * cos;
            return new PointF((float)correctedX, (float)correctedY);
        }

        public PointF InverseTransformPoint(float x, float y)
        {
            if (!IsValid)
                throw new InvalidOperationException("Position correction info is invalid.");

            double radians = DeltaAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = x - CurrentX;
            double localY = y - CurrentY;
            double baseX = BaseX + localX * cos + localY * sin;
            double baseY = BaseY - localX * sin + localY * cos;
            return new PointF((float)baseX, (float)baseY);
        }
    }

    public static class PositionCorrectionHelper
    {
        public static void EnsureValid(PositionCorrectionInfo info)
        {
            if (info == null || !info.IsValid)
                throw new Exception("位置修正信息无效，请先运行位置修正节点并创建基准。");
        }

        public static double NormalizeAngle(double angle)
        {
            while (angle > 180.0)
                angle -= 360.0;
            while (angle <= -180.0)
                angle += 360.0;
            return angle;
        }
    }
}
