using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    /// <summary>
    /// 图像预处理算法集合。
    /// </summary>
    internal static class ImagePreprocessAlgorithm
    {
        /// <summary>
        /// 根据参数执行图像预处理。
        /// </summary>
        /// <param name="source">输入图像。</param>
        /// <param name="param">预处理参数。</param>
        /// <returns>处理后的新图像。</returns>
        public static Mat Execute(Mat source, NodeParamImagePreprocess param)
        {
            if (!OutputImage.HasValidImage(source))
                throw new ArgumentException("输入图像为空。", nameof(source));
            if (param == null)
                throw new ArgumentNullException(nameof(param));

            switch (param.Mode)
            {
                case ImagePreprocessMode.Emphasize:
                    return Emphasize(source, param.EmphasizeMaskWidth, param.EmphasizeMaskHeight, param.EmphasizeFactor);
                case ImagePreprocessMode.TextureLaws:
                    return TextureLaws(source, param.LawsKernel, param.LawsEnergySize);
                case ImagePreprocessMode.Median:
                    return Median(source, param.MedianKernelSize);
                default:
                    throw new NotSupportedException("未支持的图像预处理模式：" + param.Mode);
            }
        }

        /// <summary>
        /// 执行 emphasize 风格边缘增强，使用原图减局部均值的高频分量增强边缘。
        /// </summary>
        /// <param name="source">输入图像。</param>
        /// <param name="maskWidth">局部均值窗口宽度。</param>
        /// <param name="maskHeight">局部均值窗口高度。</param>
        /// <param name="factor">高频增强系数。</param>
        /// <returns>边缘增强后的图像。</returns>
        public static Mat Emphasize(Mat source, int maskWidth, int maskHeight, double factor)
        {
            int width = NormalizeOddKernel(maskWidth, 1);
            int height = NormalizeOddKernel(maskHeight, 1);
            double safeFactor = Math.Max(0.0, factor);

            using (Mat source32 = new Mat())
            using (Mat mean32 = new Mat())
            using (Mat enhanced32 = new Mat())
            {
                source.ConvertTo(source32, MatType.CV_32F);
                Cv2.Blur(source32, mean32, new Size(width, height));
                Cv2.AddWeighted(source32, 1.0 + safeFactor, mean32, -safeFactor, 0.0, enhanced32);

                Mat output = new Mat();
                enhanced32.ConvertTo(output, source.Type());
                return output;
            }
        }

        /// <summary>
        /// 执行 Laws 纹理滤波并输出 8 位灰度纹理能量图。
        /// </summary>
        /// <param name="source">输入图像。</param>
        /// <param name="kernel">纹理核类型。</param>
        /// <param name="energySize">纹理能量平滑窗口。</param>
        /// <returns>纹理能量图。</returns>
        public static Mat TextureLaws(Mat source, LawsTextureKernel kernel, int energySize)
        {
            int energyKernel = NormalizeOddKernel(energySize, 1);
            using (Mat gray = BuildGray8(source))
            using (Mat gray32 = new Mat())
            using (Mat mean32 = new Mat())
            using (Mat zeroMean32 = new Mat())
            {
                gray.ConvertTo(gray32, MatType.CV_32F);
                Cv2.Blur(gray32, mean32, new Size(energyKernel, energyKernel));
                Cv2.Subtract(gray32, mean32, zeroMean32);

                using (Mat energy32 = BuildLawsEnergy(zeroMean32, kernel, energyKernel))
                {
                    Mat output = new Mat();
                    Cv2.Normalize(energy32, output, 0, 255, NormTypes.MinMax, MatType.CV_8U);
                    return output;
                }
            }
        }

        /// <summary>
        /// 执行中值滤波。
        /// </summary>
        /// <param name="source">输入图像。</param>
        /// <param name="kernelSize">中值滤波窗口大小。</param>
        /// <returns>中值滤波后的图像。</returns>
        public static Mat Median(Mat source, int kernelSize)
        {
            int kernel = NormalizeOddKernel(kernelSize, 3);
            Mat output = new Mat();
            Cv2.MedianBlur(source, output, kernel);
            return output;
        }

        /// <summary>
        /// 将图像转换为 8 位灰度图，单通道 8 位图像直接克隆。
        /// </summary>
        /// <param name="source">输入图像。</param>
        /// <returns>8 位灰度图。</returns>
        private static Mat BuildGray8(Mat source)
        {
            Mat gray = new Mat();
            if (source.Channels() == 1)
            {
                if (source.Depth() == MatType.CV_8U)
                    return source.Clone();

                source.ConvertTo(gray, MatType.CV_8U);
                return gray;
            }

            if (source.Channels() == 4)
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
            else
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

            return gray;
        }

        /// <summary>
        /// 根据 Laws 核生成纹理能量图。
        /// </summary>
        /// <param name="zeroMean32">已去局部均值的 32 位灰度图。</param>
        /// <param name="kernel">纹理核类型。</param>
        /// <param name="energyKernel">能量平滑窗口。</param>
        /// <returns>32 位纹理能量图。</returns>
        private static Mat BuildLawsEnergy(Mat zeroMean32, LawsTextureKernel kernel, int energyKernel)
        {
            List<Mat> responses = new List<Mat>();
            try
            {
                foreach (Tuple<float[], float[]> pair in GetKernelPairs(kernel))
                {
                    Mat response = FilterWithLawsKernel(zeroMean32, pair.Item1, pair.Item2, energyKernel);
                    responses.Add(response);
                }

                if (responses.Count == 1)
                    return responses[0].Clone();

                Mat combined = new Mat(zeroMean32.Size(), MatType.CV_32F, Scalar.All(0));
                foreach (Mat response in responses)
                    Cv2.Add(combined, response, combined);

                combined.ConvertTo(combined, MatType.CV_32F, 1.0 / responses.Count);
                return combined;
            }
            finally
            {
                foreach (Mat response in responses)
                    response.Dispose();
            }
        }

        /// <summary>
        /// 用指定 Laws 一维核组合执行滤波并计算局部能量。
        /// </summary>
        /// <param name="source32">32 位源图。</param>
        /// <param name="rowKernel">行方向一维核。</param>
        /// <param name="colKernel">列方向一维核。</param>
        /// <param name="energyKernel">能量平滑窗口。</param>
        /// <returns>局部纹理能量。</returns>
        private static Mat FilterWithLawsKernel(Mat source32, float[] rowKernel, float[] colKernel, int energyKernel)
        {
            using (Mat kernel = BuildKernel(rowKernel, colKernel))
            using (Mat response32 = new Mat())
            using (Mat abs32 = new Mat())
            {
                Cv2.Filter2D(source32, response32, MatType.CV_32F, kernel);
                Cv2.Absdiff(response32, Scalar.All(0), abs32);

                Mat energy32 = new Mat();
                Cv2.Blur(abs32, energy32, new Size(energyKernel, energyKernel));
                return energy32;
            }
        }

        /// <summary>
        /// 创建 Laws 二维卷积核。
        /// </summary>
        /// <param name="rowKernel">行方向一维核。</param>
        /// <param name="colKernel">列方向一维核。</param>
        /// <returns>归一化后的二维卷积核。</returns>
        private static Mat BuildKernel(float[] rowKernel, float[] colKernel)
        {
            Mat kernel = new Mat(rowKernel.Length, colKernel.Length, MatType.CV_32F);
            double norm = Math.Max(1.0, rowKernel.Sum(item => Math.Abs(item)) * colKernel.Sum(item => Math.Abs(item)));
            for (int row = 0; row < rowKernel.Length; row++)
            {
                for (int col = 0; col < colKernel.Length; col++)
                {
                    kernel.Set(row, col, (float)(rowKernel[row] * colKernel[col] / norm));
                }
            }

            return kernel;
        }

        /// <summary>
        /// 获取指定 Laws 模式对应的一维核组合。
        /// </summary>
        /// <param name="kernel">纹理核类型。</param>
        /// <returns>一维核组合列表。</returns>
        private static IEnumerable<Tuple<float[], float[]>> GetKernelPairs(LawsTextureKernel kernel)
        {
            float[] l5 = { 1, 4, 6, 4, 1 };
            float[] e5 = { -1, -2, 0, 2, 1 };
            float[] s5 = { -1, 0, 2, 0, -1 };
            float[] r5 = { 1, -4, 6, -4, 1 };

            switch (kernel)
            {
                case LawsTextureKernel.EdgeEnergy:
                    yield return Tuple.Create(l5, e5);
                    yield return Tuple.Create(e5, l5);
                    break;
                case LawsTextureKernel.L5E5:
                    yield return Tuple.Create(l5, e5);
                    break;
                case LawsTextureKernel.E5L5:
                    yield return Tuple.Create(e5, l5);
                    break;
                case LawsTextureKernel.E5E5:
                    yield return Tuple.Create(e5, e5);
                    break;
                case LawsTextureKernel.S5S5:
                    yield return Tuple.Create(s5, s5);
                    break;
                case LawsTextureKernel.R5R5:
                    yield return Tuple.Create(r5, r5);
                    break;
                default:
                    yield return Tuple.Create(l5, e5);
                    yield return Tuple.Create(e5, l5);
                    break;
            }
        }

        /// <summary>
        /// 将窗口大小规整为正奇数。
        /// </summary>
        /// <param name="value">输入窗口大小。</param>
        /// <param name="minimum">最小窗口大小。</param>
        /// <returns>规整后的奇数窗口大小。</returns>
        private static int NormalizeOddKernel(int value, int minimum)
        {
            int kernel = Math.Max(minimum, value);
            if (kernel % 2 == 0)
                kernel += 1;

            return kernel;
        }
    }
}
