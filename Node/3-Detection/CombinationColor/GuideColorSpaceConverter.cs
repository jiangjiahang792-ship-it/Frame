using System;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>学习与识别共用的无状态颜色转换服务，保证通道定义一致，不访问窗体。</summary>
    internal static class GuideColorSpaceConverter
    {
        /// <summary>从原始采样范围重新扩展容差，不累计修改模型；50 对应原有默认范围。</summary>
        public static GuideColorRangeModel ApplySensitivity(GuideColorRangeModel model, decimal sensitivity)
        {
            if (model == null || !model.HasSampleRange || sensitivity == 50m) return model;
            double factor = 0.25 + (double)Math.Max(0m, Math.Min(100m, sensitivity)) * 0.015;
            bool circular = model.ColorSpaceName == "HSV" || model.ColorSpaceName == "HSI";
            int margin1 = (int)Math.Round(8 * factor);
            int margin23 = (int)Math.Round((circular ? 24 : 16) * factor);
            int low = model.SampleChannel1Low - margin1;
            int high = model.SampleChannel1High + margin1;
            if (circular)
            {
                int span = (model.SampleChannel1High - model.SampleChannel1Low + 180) % 180;
                if (span + 2 * margin1 >= 179) { low = 0; high = 179; }
                else { low = (low + 180) % 180; high %= 180; }
            }
            else { low = Math.Max(0, low); high = Math.Min(255, high); }
            return new GuideColorRangeModel
            {
                ColorSpaceName = model.ColorSpaceName,
                Channel1Low = low, Channel1High = high,
                Channel2Low = Math.Max(0, model.SampleChannel2Low - margin23),
                Channel2High = Math.Min(255, model.SampleChannel2High + margin23),
                Channel3Low = Math.Max(0, model.SampleChannel3Low - margin23),
                Channel3High = Math.Min(255, model.SampleChannel3High + margin23)
            };
        }

        /// <summary>将 BGR 三通道图像转换为指定空间；返回值由调用方释放。</summary>
        public static Mat Convert(Mat image, string mode)
        {
            if (mode == "HSI") return ConvertHsi(image);
            Mat result = new Mat();
            try
            {
                ColorConversionCodes code;
                switch (mode)
                {
                    case "HSV": code = ColorConversionCodes.BGR2HSV; break;
                    case "LAB": code = ColorConversionCodes.BGR2Lab; break;
                    case "RGB": code = ColorConversionCodes.BGR2RGB; break;
                    default: throw new ArgumentException("不支持的颜色空间：" + mode);
                }
                Cv2.CvtColor(image, result, code);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        /// <summary>沿用参考项目 HSI 定义，H 为 0～179，S/I 为 0～255；逐行指针避免像素级封送。</summary>
        private static unsafe Mat ConvertHsi(Mat image)
        {
            Mat result = new Mat(image.Rows, image.Cols, MatType.CV_8UC3);
            for (int y = 0; y < image.Rows; y++)
            {
                byte* input = (byte*)image.Ptr(y);
                byte* output = (byte*)result.Ptr(y);
                for (int x = 0; x < image.Cols; x++)
                {
                    double b = input[x * 3], g = input[x * 3 + 1], r = input[x * 3 + 2];
                    double sum = r + g + b;
                    double saturation = sum == 0 ? 0 : 1 - 3 * Math.Min(r, Math.Min(g, b)) / sum;
                    double denominator = Math.Sqrt((r - g) * (r - g) + (r - b) * (g - b));
                    double angle = denominator == 0 ? 0 : Math.Acos(Math.Max(-1, Math.Min(1, (r - (g + b) / 2) / denominator)));
                    double hue = b <= g ? angle : 2 * Math.PI - angle;
                    output[x * 3] = (byte)Math.Min(179, Math.Round(hue * 90 / Math.PI));
                    output[x * 3 + 1] = (byte)Math.Round(saturation * 255);
                    output[x * 3 + 2] = (byte)Math.Round(sum / 3);
                }
            }
            return result;
        }
    }
}
