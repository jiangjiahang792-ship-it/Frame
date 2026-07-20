using System;
using System.Drawing;
using TDJS_Vision.Node._3_Detection.MatchTemplate;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    public class PositionCorrectionInfo
    {
        /// <summary>
        /// 初始化位置修正信息，并把基准与当前尺度设置为不缩放的一。
        /// </summary>
        public PositionCorrectionInfo()
        {
            BaseScaleX = 1.0;
            BaseScaleY = 1.0;
            CurrentScaleX = 1.0;
            CurrentScaleY = 1.0;
        }

        /// <summary>
        /// 获取或设置位置修正信息是否有效。
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 获取或设置当前目标序号。
        /// </summary>
        public int TargetIndex { get; set; }

        /// <summary>
        /// 获取或设置基准中心 X 坐标。
        /// </summary>
        public double BaseX { get; set; }

        /// <summary>
        /// 获取或设置基准中心 Y 坐标。
        /// </summary>
        public double BaseY { get; set; }

        /// <summary>
        /// 获取或设置基准角度。
        /// </summary>
        public double BaseAngle { get; set; }

        /// <summary>
        /// 获取或设置基准 X 方向尺度。
        /// </summary>
        public double BaseScaleX { get; set; }

        /// <summary>
        /// 获取或设置基准 Y 方向尺度。
        /// </summary>
        public double BaseScaleY { get; set; }

        /// <summary>
        /// 获取或设置当前中心 X 坐标。
        /// </summary>
        public double CurrentX { get; set; }

        /// <summary>
        /// 获取或设置当前中心 Y 坐标。
        /// </summary>
        public double CurrentY { get; set; }

        /// <summary>
        /// 获取或设置当前角度。
        /// </summary>
        public double CurrentAngle { get; set; }

        /// <summary>
        /// 获取或设置当前 X 方向尺度。
        /// </summary>
        public double CurrentScaleX { get; set; }

        /// <summary>
        /// 获取或设置当前 Y 方向尺度。
        /// </summary>
        public double CurrentScaleY { get; set; }

        /// <summary>
        /// 获取或设置当前目标匹配框宽度。
        /// </summary>
        public double TargetWidth { get; set; }

        /// <summary>
        /// 获取或设置当前目标匹配框高度。
        /// </summary>
        public double TargetHeight { get; set; }

        /// <summary>
        /// 获取或设置当前目标匹配得分。
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 获取当前 X 方向平移量。
        /// </summary>
        public double DeltaX { get { return CurrentX - BaseX; } }

        /// <summary>
        /// 获取当前 Y 方向平移量。
        /// </summary>
        public double DeltaY { get { return CurrentY - BaseY; } }

        /// <summary>
        /// 获取规范化后的角度差。
        /// </summary>
        public double DeltaAngle { get { return PositionCorrectionHelper.NormalizeAngle(CurrentAngle - BaseAngle); } }

        /// <summary>
        /// 根据基准位姿和当前位姿创建位置修正信息。
        /// </summary>
        /// <param name="basePose">创建基准时使用的模板目标位姿。</param>
        /// <param name="currentPose">当前需要测量的模板目标位姿。</param>
        /// <returns>可用于正向和逆向变换的位置修正信息。</returns>
        public static PositionCorrectionInfo FromPoses(TemplateMatchPose basePose, TemplateMatchPose currentPose)
        {
            if (basePose == null)
                throw new ArgumentNullException("basePose");
            if (currentPose == null)
                throw new ArgumentNullException("currentPose");

            return new PositionCorrectionInfo
            {
                IsValid = basePose.IsValid && currentPose.IsValid,
                TargetIndex = currentPose.TargetIndex,
                BaseX = basePose.CenterX,
                BaseY = basePose.CenterY,
                BaseAngle = basePose.Angle,
                BaseScaleX = NormalizeScale(basePose.ScaleX),
                BaseScaleY = NormalizeScale(basePose.ScaleY),
                CurrentX = currentPose.CenterX,
                CurrentY = currentPose.CenterY,
                CurrentAngle = currentPose.Angle,
                CurrentScaleX = NormalizeScale(currentPose.ScaleX),
                CurrentScaleY = NormalizeScale(currentPose.ScaleY),
                TargetWidth = currentPose.Width,
                TargetHeight = currentPose.Height,
                Score = currentPose.Score
            };
        }

        /// <summary>
        /// 把无效尺度规范化为不缩放的一。
        /// </summary>
        /// <param name="scale">待检查的尺度。</param>
        /// <returns>大于零的有效尺度。</returns>
        public static double NormalizeScale(double scale)
        {
            return scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale) ? 1.0 : scale;
        }

        /// <summary>
        /// 把基准坐标系中的点变换到当前目标坐标系。
        /// </summary>
        /// <param name="x">基准点 X 坐标。</param>
        /// <param name="y">基准点 Y 坐标。</param>
        /// <returns>变换后的当前目标点。</returns>
        public PointF TransformPoint(float x, float y)
        {
            if (!IsValid)
                throw new InvalidOperationException("位置修正信息无效。");

            double radians = DeltaAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = (x - BaseX) * NormalizeScale(CurrentScaleX) / NormalizeScale(BaseScaleX);
            double localY = (y - BaseY) * NormalizeScale(CurrentScaleY) / NormalizeScale(BaseScaleY);
            double correctedX = CurrentX + localX * cos - localY * sin;
            double correctedY = CurrentY + localX * sin + localY * cos;
            return new PointF((float)correctedX, (float)correctedY);
        }

        /// <summary>
        /// 把当前目标坐标系中的点逆变换到基准坐标系。
        /// </summary>
        /// <param name="x">当前目标点 X 坐标。</param>
        /// <param name="y">当前目标点 Y 坐标。</param>
        /// <returns>逆变换后的基准点。</returns>
        public PointF InverseTransformPoint(float x, float y)
        {
            if (!IsValid)
                throw new InvalidOperationException("位置修正信息无效。");

            double radians = DeltaAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double localX = x - CurrentX;
            double localY = y - CurrentY;
            double unrotatedX = localX * cos + localY * sin;
            double unrotatedY = -localX * sin + localY * cos;
            double baseX = BaseX + unrotatedX * NormalizeScale(BaseScaleX) / NormalizeScale(CurrentScaleX);
            double baseY = BaseY + unrotatedY * NormalizeScale(BaseScaleY) / NormalizeScale(CurrentScaleY);
            return new PointF((float)baseX, (float)baseY);
        }
    }

    public static class PositionCorrectionHelper
    {
        /// <summary>
        /// 确保位置修正信息有效。
        /// </summary>
        /// <param name="info">待检查的位置修正信息。</param>
        public static void EnsureValid(PositionCorrectionInfo info)
        {
            if (info == null || !info.IsValid)
                throw new Exception("位置修正信息无效，请先运行位置修正节点并创建基准。");
        }

        /// <summary>
        /// 把角度规范化到负一百八十度到一百八十度范围。
        /// </summary>
        /// <param name="angle">待规范化角度。</param>
        /// <returns>规范化后的角度。</returns>
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
