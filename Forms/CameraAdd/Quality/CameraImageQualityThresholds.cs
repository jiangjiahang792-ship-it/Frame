namespace TDJS_Vision.Forms.CameraAdd.Quality
{
    /// <summary>
    /// 相机预览图像亮度和清晰度判定阈值。
    /// </summary>
    public sealed class CameraImageQualityThresholds
    {
        /// <summary>
        /// 创建默认判定阈值。
        /// </summary>
        public CameraImageQualityThresholds()
        {
            ClearThreshold = 1200;
            DarkThreshold = 50;
            BrightThreshold = 200;
            UnderexposedWarningRatio = 0.2;
            OverexposedWarningRatio = 0.1;
        }

        /// <summary>
        /// 清晰阈值，大于等于该值判定为清晰。
        /// </summary>
        public double ClearThreshold { get; set; }

        /// <summary>
        /// 偏暗阈值，小于该平均亮度判定为暗。
        /// </summary>
        public double DarkThreshold { get; set; }

        /// <summary>
        /// 偏亮阈值，大于该平均亮度判定为亮。
        /// </summary>
        public double BrightThreshold { get; set; }

        /// <summary>
        /// 欠曝像素比例警告阈值。
        /// </summary>
        public double UnderexposedWarningRatio { get; set; }

        /// <summary>
        /// 过曝像素比例警告阈值。
        /// </summary>
        public double OverexposedWarningRatio { get; set; }
    }
}
