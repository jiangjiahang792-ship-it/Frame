using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    internal static class MeasurementNodeHelper
    {
        /// <summary>
        /// 获取第一张有效图像的深拷贝，供需要修改像素的旧节点继续使用。
        /// </summary>
        public static Mat GetFirstMat(OutputImage outputImage)
        {
            if (outputImage == null)
                throw new Exception("订阅图像为空！");

            if (outputImage.Bitmaps != null && outputImage.Bitmaps.Count > 0 && outputImage.Bitmaps[0] != null && !outputImage.Bitmaps[0].Empty())
                return outputImage.Bitmaps[0].Clone();

            if (outputImage.SrcImg != null && !outputImage.SrcImg.Empty())
                return outputImage.SrcImg.Clone();

            throw new Exception("订阅节点没有输出有效图像！");
        }

        /// <summary>
        /// 获取只读预览图像引用，优先返回彩色原图，不接管 Mat 生命周期。
        /// </summary>
        public static Mat GetReadOnlyPreviewMat(OutputImage outputImage)
        {
            if (outputImage == null)
                throw new Exception("订阅图像为空！");

            if (outputImage.Bitmaps != null && outputImage.Bitmaps.Count > 0 && outputImage.Bitmaps[0] != null && !outputImage.Bitmaps[0].Empty())
                return outputImage.Bitmaps[0];

            if (outputImage.SrcImg != null && !outputImage.SrcImg.Empty())
                return outputImage.SrcImg;

            if (outputImage.GrayImg != null && !outputImage.GrayImg.Empty())
                return outputImage.GrayImg;

            throw new Exception("订阅节点没有输出有效图像！");
        }

        /// <summary>
        /// 获取只读灰度图像，优先复用图像源的 GrayImg；无灰度缓存时才临时转换。
        /// </summary>
        public static Mat GetReadOnlyGrayMat(OutputImage outputImage, out bool disposeAfterUse)
        {
            disposeAfterUse = false;
            if (outputImage == null)
                throw new Exception("订阅图像为空！");

            if (outputImage.GrayImg != null && !outputImage.GrayImg.Empty())
                return outputImage.GrayImg;

            Mat source = GetReadOnlyPreviewMat(outputImage);
            if (source.Channels() == 1)
                return source;

            disposeAfterUse = true;
            return CaliperMeasurementAlgorithm.ToGray(source);
        }

        public static Bitmap ToPreviewBitmap(Mat image)
        {
            if (image == null || image.Empty())
                return null;
            return BitmapConverter.ToBitmap(image);
        }

        public static Mat EnsureBgr(Mat image)
        {
            if (image == null || image.Empty())
                throw new Exception("绘制图像为空！");

            Mat bgr = new Mat();
            if (image.Channels() == 1)
                Cv2.CvtColor(image, bgr, ColorConversionCodes.GRAY2BGR);
            else if (image.Channels() == 4)
                Cv2.CvtColor(image, bgr, ColorConversionCodes.BGRA2BGR);
            else
                image.CopyTo(bgr);
            return bgr;
        }

        public static void DrawEdgePoints(Mat image, System.Collections.Generic.IEnumerable<PointF> points, Scalar color)
        {
            foreach (PointF point in points)
                Cv2.Circle(image, new OpenCvSharp.Point((int)Math.Round(point.X), (int)Math.Round(point.Y)), 2, color, -1);
        }

        /// <summary>
        /// 把单个模板目标的叠加图形追加到节点总叠加结果中。
        /// </summary>
        public static void AppendAlgorithmResult(AlgorithmResult target, AlgorithmResult source)
        {
            if (target == null || source == null)
                return;

            target.Rects.AddRange(source.Rects);
            target.Texts.AddRange(source.Texts);
            target.Lines.AddRange(source.Lines);
            target.Circles.AddRange(source.Circles);
            target.Arcs.AddRange(source.Arcs);
            target.Ellipses.AddRange(source.Ellipses);
            target.Contours.AddRange(source.Contours);
        }

        /// <summary>
        /// 计算一组测量几何点的轴对齐包围盒中心，用于判断唯一ROI属于哪个模板目标。
        /// </summary>
        public static PointF CalculateBoundsCenter(IEnumerable<PointF> points)
        {
            if (points == null)
                throw new ArgumentNullException("points");

            bool hasPoint = false;
            float minX = 0;
            float minY = 0;
            float maxX = 0;
            float maxY = 0;
            foreach (PointF point in points)
            {
                if (!hasPoint)
                {
                    minX = maxX = point.X;
                    minY = maxY = point.Y;
                    hasPoint = true;
                    continue;
                }

                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            if (!hasPoint)
                throw new InvalidOperationException("测量几何为空，无法计算ROI包围盒中心。");
            return new PointF((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }
    }
}
