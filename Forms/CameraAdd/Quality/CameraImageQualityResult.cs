using System;

namespace TDJS_Vision.Forms.CameraAdd.Quality
{
    /// <summary>
    /// 亮度判定等级。
    /// </summary>
    public enum BrightnessLevel
    {
        /// <summary>
        /// 画面偏暗。
        /// </summary>
        暗,

        /// <summary>
        /// 画面亮度正常。
        /// </summary>
        正常,

        /// <summary>
        /// 画面偏亮。
        /// </summary>
        亮
    }

    /// <summary>
    /// 清晰度判定等级。
    /// </summary>
    public enum SharpnessLevel
    {
        /// <summary>
        /// 图像模糊。
        /// </summary>
        模糊,

        /// <summary>
        /// 图像清晰。
        /// </summary>
        清晰
    }

    /// <summary>
    /// 保存一次相机预览图像的亮度和清晰度分析结果。
    /// </summary>
    public sealed class CameraImageQualityResult
    {
        /// <summary>
        /// 创建亮度和清晰度分析结果。
        /// </summary>
        /// <param name="sharpness">清晰度分数。</param>
        /// <param name="meanBrightness">平均亮度。</param>
        /// <param name="underexposedRatio">欠曝像素比例。</param>
        /// <param name="overexposedRatio">过曝像素比例。</param>
        /// <param name="sharpnessLevel">清晰度等级。</param>
        /// <param name="brightnessLevel">亮度等级。</param>
        public CameraImageQualityResult(double sharpness, double meanBrightness, double underexposedRatio,
            double overexposedRatio, SharpnessLevel sharpnessLevel, BrightnessLevel brightnessLevel)
        {
            Sharpness = sharpness;
            MeanBrightness = meanBrightness;
            UnderexposedRatio = underexposedRatio;
            OverexposedRatio = overexposedRatio;
            SharpnessLevel = sharpnessLevel;
            BrightnessLevel = brightnessLevel;
        }

        /// <summary>
        /// 清晰度分数。
        /// </summary>
        public double Sharpness { get; }

        /// <summary>
        /// 平均亮度，范围为 0 至 255。
        /// </summary>
        public double MeanBrightness { get; }

        /// <summary>
        /// 欠曝像素比例。
        /// </summary>
        public double UnderexposedRatio { get; }

        /// <summary>
        /// 过曝像素比例。
        /// </summary>
        public double OverexposedRatio { get; }

        /// <summary>
        /// 清晰度等级。
        /// </summary>
        public SharpnessLevel SharpnessLevel { get; }

        /// <summary>
        /// 亮度等级。
        /// </summary>
        public BrightnessLevel BrightnessLevel { get; }

        /// <summary>
        /// 使用平滑后的清晰度生成新的结果。
        /// </summary>
        /// <param name="smoothSharpness">平滑后的清晰度分数。</param>
        /// <param name="thresholds">质量判定阈值。</param>
        /// <returns>包含平滑清晰度的新结果。</returns>
        public CameraImageQualityResult WithSmoothSharpness(double smoothSharpness, CameraImageQualityThresholds thresholds)
        {
            if (thresholds == null)
                throw new ArgumentNullException(nameof(thresholds));

            return new CameraImageQualityResult(
                smoothSharpness,
                MeanBrightness,
                UnderexposedRatio,
                OverexposedRatio,
                smoothSharpness >= thresholds.ClearThreshold ? SharpnessLevel.清晰 : SharpnessLevel.模糊,
                BrightnessLevel);
        }
    }
}
