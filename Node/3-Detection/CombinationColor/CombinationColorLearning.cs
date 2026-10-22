using System;
using System.Collections.Generic;
using OpenCvSharp;
namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>沿用左右转线组合模式的主色学习、环形色相和通道范围算法。</summary>
    internal static class CombinationColorLearning
    {
        /// <summary>一次学习四种空间，切换空间无需重新取色。</summary>
        internal static List<GuideColorRangeModel> Learn(IReadOnlyList<Vec3b> pixels)
        {
            var samples = GuideColorSampleSelector.Select(pixels);
            return new List<GuideColorRangeModel> { LearnColorRange(samples,"HSV",ColorConversionCodes.BGR2HSV),
                LearnHsiRange(samples), LearnColorRange(samples,"LAB",ColorConversionCodes.BGR2Lab), LearnRgbRange(samples) };
        }
        /// <summary>将通道数值限制到有效范围。</summary>
        private static int ClampToInt(int value,int low,int high) => Math.Max(low,Math.Min(high,value));
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static GuideColorRangeModel LearnHsiRange(IReadOnlyList<Vec3b> samples)
        {
            using (Mat image = CreateSampleImage(samples))
            using (Mat hsi = GuideColorSpaceConverter.Convert(image, "HSI"))
            {
                GuideColorRangeModel model = BuildHsvRangeModel(hsi);
                model.ColorSpaceName = "HSI";
                return model;
            }
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static GuideColorRangeModel LearnColorRange(IReadOnlyList<Vec3b> samples, string colorSpaceName, ColorConversionCodes conversionCode)
        {
            using (Mat sampleImage = CreateSampleImage(samples))
            using (Mat converted = new Mat())
            {
                Cv2.CvtColor(sampleImage, converted, conversionCode);
                if (string.Equals(colorSpaceName, "HSV", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildHsvRangeModel(converted);
                }

                return BuildRangeModel(colorSpaceName, converted);
            }
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static GuideColorRangeModel LearnRgbRange(IReadOnlyList<Vec3b> samples)
        {
            using (Mat sampleImage = CreateSampleImage(samples))
            using (Mat rgb = new Mat())
            {
                Cv2.CvtColor(sampleImage, rgb, ColorConversionCodes.BGR2RGB);
                return BuildRangeModel("RGB", rgb);
            }
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static Mat CreateSampleImage(IReadOnlyList<Vec3b> samples)
        {
            Mat sampleImage = new Mat(1, samples.Count, MatType.CV_8UC3);
            for (int index = 0; index < samples.Count; index++)
            {
                sampleImage.Set(0, index, samples[index]);
            }
            return sampleImage;
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static GuideColorRangeModel BuildRangeModel(string colorSpaceName, Mat image)
        {
            int c1Low = 255, c1High = 0, c2Low = 255, c2High = 0, c3Low = 255, c3High = 0;
            for (int x = 0; x < image.Cols; x++)
            {
                Vec3b value = image.At<Vec3b>(0, x);
                c1Low = Math.Min(c1Low, value.Item0);
                c1High = Math.Max(c1High, value.Item0);
                c2Low = Math.Min(c2Low, value.Item1);
                c2High = Math.Max(c2High, value.Item1);
                c3Low = Math.Min(c3Low, value.Item2);
                c3High = Math.Max(c3High, value.Item2);
            }

            return new GuideColorRangeModel
            {
                ColorSpaceName = colorSpaceName,
                HasSampleRange = true,
                SampleChannel1Low = c1Low, SampleChannel1High = c1High,
                SampleChannel2Low = c2Low, SampleChannel2High = c2High,
                SampleChannel3Low = c3Low, SampleChannel3High = c3High,
                Channel1Low = Math.Max(0, c1Low - 8),
                Channel1High = Math.Min(255, c1High + 8),
                Channel2Low = Math.Max(0, c2Low - 16),
                Channel2High = Math.Min(255, c2High + 16),
                Channel3Low = Math.Max(0, c3Low - 16),
                Channel3High = Math.Min(255, c3High + 16)
            };
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        private static GuideColorRangeModel BuildHsvRangeModel(Mat image)
        {
            List<int> hues = new List<int>(image.Cols);
            int sLow = 255, sHigh = 0, vLow = 255, vHigh = 0;
            for (int x = 0; x < image.Cols; x++)
            {
                Vec3b value = image.At<Vec3b>(0, x);
                hues.Add(value.Item0);
                sLow = Math.Min(sLow, value.Item1);
                sHigh = Math.Max(sHigh, value.Item1);
                vLow = Math.Min(vLow, value.Item2);
                vHigh = Math.Max(vHigh, value.Item2);
            }

            hues.Sort();
            int startHue = hues[0];
            int endHue = hues[hues.Count - 1];
            int maxGap = -1;
            for (int index = 0; index < hues.Count; index++)
            {
                int current = hues[index];
                int next = index == hues.Count - 1 ? hues[0] + 180 : hues[index + 1];
                int gap = next - current;
                if (gap > maxGap)
                {
                    maxGap = gap;
                    startHue = next % 180;
                    endHue = current;
                }
            }

            int hueLow = startHue - 8;
            int hueHigh = endHue + 8;
            // 扩展后覆盖整个色相环时不能再取模，否则会错误缩成一个窄区间。
            if (180 - maxGap + 16 >= 179)
            {
                hueLow = 0;
                hueHigh = 179;
            }
            if (hueLow < 0) hueLow += 180;
            if (hueHigh > 179) hueHigh -= 180;

            return new GuideColorRangeModel
            {
                ColorSpaceName = "HSV",
                HasSampleRange = true,
                SampleChannel1Low = startHue, SampleChannel1High = endHue,
                SampleChannel2Low = sLow, SampleChannel2High = sHigh,
                SampleChannel3Low = vLow, SampleChannel3High = vHigh,
                Channel1Low = hueLow,
                Channel1High = hueHigh,
                Channel2Low = Math.Max(0, sLow - 24),
                Channel2High = Math.Min(255, sHigh + 24),
                Channel3Low = Math.Max(0, vLow - 24),
                Channel3High = Math.Min(255, vHigh + 24)
            };
        }
        /// <summary>保持参考项目的颜色空间学习及阈值语义。</summary>
        internal static void BuildStep2HsvMask(Mat hsvImage, GuideColorRangeModel hsvModel, Mat mask)
        {
            int hueLow = ClampToInt(hsvModel.Channel1Low, 0, 179);
            int hueHigh = ClampToInt(hsvModel.Channel1High, 0, 179);
            int saturationLow = ClampToInt(hsvModel.Channel2Low, 0, 255);
            int saturationHigh = ClampToInt(hsvModel.Channel2High, 0, 255);
            int valueLow = ClampToInt(hsvModel.Channel3Low, 0, 255);
            int valueHigh = ClampToInt(hsvModel.Channel3High, 0, 255);

            if (hueLow <= hueHigh)
            {
                Cv2.InRange(
                    hsvImage,
                    new Scalar(hueLow, saturationLow, valueLow),
                    new Scalar(hueHigh, saturationHigh, valueHigh),
                    mask);
                return;
            }

            using (Mat lowHueMask = new Mat())
            using (Mat highHueMask = new Mat())
            {
                Cv2.InRange(
                    hsvImage,
                    new Scalar(0, saturationLow, valueLow),
                    new Scalar(hueHigh, saturationHigh, valueHigh),
                    lowHueMask);
                Cv2.InRange(
                    hsvImage,
                    new Scalar(hueLow, saturationLow, valueLow),
                    new Scalar(179, saturationHigh, valueHigh),
                    highHueMask);
                Cv2.BitwiseOr(lowHueMask, highHueMask, mask);
            }
        }
    }
}
