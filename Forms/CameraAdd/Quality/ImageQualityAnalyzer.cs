using System;
using System.Drawing;

namespace TDJS_Vision.Forms.CameraAdd.Quality
{
    /// <summary>
    /// 使用 Tenengrad 梯度能量和灰度统计评价预览图像质量。
    /// </summary>
    public sealed class ImageQualityAnalyzer
    {
        /// <summary>
        /// 分析灰度图像指定 ROI 内的亮度和清晰度。
        /// </summary>
        /// <param name="grayData">连续灰度数据。</param>
        /// <param name="width">图像宽度。</param>
        /// <param name="height">图像高度。</param>
        /// <param name="stride">灰度数据步长。</param>
        /// <param name="roi">待分析 ROI。</param>
        /// <param name="thresholds">判定阈值。</param>
        /// <returns>图像质量分析结果。</returns>
        public CameraImageQualityResult Analyze(byte[] grayData, int width, int height, int stride,
            Rectangle roi, CameraImageQualityThresholds thresholds)
        {
            if (grayData == null)
                throw new ArgumentNullException(nameof(grayData));
            if (thresholds == null)
                throw new ArgumentNullException(nameof(thresholds));
            if (width <= 0 || height <= 0 || stride < width || grayData.Length < stride * height)
                throw new ArgumentException("灰度图像参数无效。");

            Rectangle valid = Rectangle.Intersect(new Rectangle(0, 0, width, height), roi);
            if (valid.Width <= 0 || valid.Height <= 0)
                throw new ArgumentException("ROI 区域无效。");

            long brightnessSum = 0;
            long darkCount = 0;
            long brightCount = 0;
            long pixelCount = (long)valid.Width * valid.Height;
            for (int y = valid.Top; y < valid.Bottom; y++)
            {
                int row = y * stride;
                for (int x = valid.Left; x < valid.Right; x++)
                {
                    byte value = grayData[row + x];
                    brightnessSum += value;
                    if (value <= 10)
                        darkCount++;
                    if (value >= 245)
                        brightCount++;
                }
            }

            double gradientSum = 0;
            long gradientCount = 0;
            for (int y = valid.Top + 1; y < valid.Bottom - 1; y++)
            {
                int previous = (y - 1) * stride;
                int current = y * stride;
                int next = (y + 1) * stride;
                for (int x = valid.Left + 1; x < valid.Right - 1; x++)
                {
                    int gx = -grayData[previous + x - 1] + grayData[previous + x + 1]
                           - 2 * grayData[current + x - 1] + 2 * grayData[current + x + 1]
                           - grayData[next + x - 1] + grayData[next + x + 1];
                    int gy = -grayData[previous + x - 1] - 2 * grayData[previous + x] - grayData[previous + x + 1]
                           + grayData[next + x - 1] + 2 * grayData[next + x] + grayData[next + x + 1];
                    gradientSum += (double)gx * gx + (double)gy * gy;
                    gradientCount++;
                }
            }

            double sharpness = gradientCount == 0 ? 0 : gradientSum / gradientCount;
            double meanBrightness = (double)brightnessSum / pixelCount;
            BrightnessLevel brightnessLevel = meanBrightness < thresholds.DarkThreshold
                ? BrightnessLevel.暗
                : meanBrightness > thresholds.BrightThreshold
                    ? BrightnessLevel.亮
                    : BrightnessLevel.正常;
            SharpnessLevel sharpnessLevel = sharpness >= thresholds.ClearThreshold
                ? SharpnessLevel.清晰
                : SharpnessLevel.模糊;

            return new CameraImageQualityResult(
                sharpness,
                meanBrightness,
                (double)darkCount / pixelCount,
                (double)brightCount / pixelCount,
                sharpnessLevel,
                brightnessLevel);
        }
    }
}
