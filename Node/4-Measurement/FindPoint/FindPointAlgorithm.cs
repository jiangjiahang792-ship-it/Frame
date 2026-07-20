using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    internal class FindPointMeasureResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PointCount { get; set; }
        public int ContourCount { get; set; }
        public double AlgorithmMs { get; set; }
        /// <summary>
        /// 实际参与局部算法处理的 ROI 数量。
        /// </summary>
        public int ProcessedRegionCount { get; set; }
        /// <summary>
        /// 实际参与局部算法处理的像素数量，按 ROI 包围矩形估算。
        /// </summary>
        public long ProcessedPixelCount { get; set; }
        public List<PointF> Points { get; set; } = new List<PointF>();
        public List<PointF> PrimaryContour { get; set; } = new List<PointF>();
        public List<List<PointF>> Contours { get; set; } = new List<List<PointF>>();
        public List<List<PointF>> Regions { get; set; } = new List<List<PointF>>();
    }

    internal static class FindPointAlgorithm
    {
        /// <summary>
        /// 在只读灰度图上提取边缘点；调用方负责提供单通道图像。
        /// </summary>
        public static FindPointMeasureResult Execute(Mat gray, NodeParamFindPoint param, CancellationToken token)
        {
            if (gray == null || gray.Empty())
                throw new Exception("输入图像为空。");
            if (gray.Channels() != 1)
                throw new Exception("找点算法需要单通道灰度图像。");
            if (param == null)
                throw new Exception("找点参数为空。");
            if (param.Regions == null || param.Regions.Count == 0)
                throw new Exception("请先绘制并确认 ROI 区域。");

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<List<PointF>> regions = CloneRegions(param.Regions);
            var result = new FindPointMeasureResult
            {
                Regions = regions
            };

            int low = Math.Max(0, Math.Min(param.LowThreshold, param.HighThreshold - 1));
            int high = Math.Max(low + 1, param.HighThreshold);
            int minContourPoints = Math.Max(1, param.MinContourPoints);
            int sampleStep = Math.Max(1, param.SampleStep);
            int remaining = param.MaxPointCount <= 0 ? int.MaxValue : param.MaxPointCount;
            int padding = GetProcessingPadding(param.BlurSize);
            var contourCandidates = new List<ContourCandidate>();

            foreach (List<PointF> region in regions)
            {
                token.ThrowIfCancellationRequested();

                OpenCvSharp.Rect bounds;
                if (!TryBuildRegionBounds(region, gray.Width, gray.Height, padding, out bounds))
                    continue;

                result.ProcessedRegionCount++;
                result.ProcessedPixelCount += (long)bounds.Width * bounds.Height;

                using (Mat localGray = new Mat(gray, bounds))
                using (Mat blurred = BuildBlurred(localGray, param.BlurSize))
                using (Mat edges = new Mat())
                using (Mat mask = BuildLocalRoiMask(bounds.Size, region, bounds.X, bounds.Y))
                using (Mat roiEdges = new Mat())
                {
                    Cv2.Canny(blurred, edges, low, high);
                    Cv2.BitwiseAnd(edges, mask, roiEdges);

                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(roiEdges, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);

                    foreach (OpenCvSharp.Point[] contour in contours)
                    {
                        token.ThrowIfCancellationRequested();
                        if (contour == null || contour.Length < minContourPoints)
                            continue;

                        contourCandidates.Add(new ContourCandidate(contour.Length, OffsetContour(contour, bounds.X, bounds.Y)));
                    }
                }
            }

            foreach (ContourCandidate contour in contourCandidates.OrderByDescending(c => c.RawPointCount))
            {
                token.ThrowIfCancellationRequested();
                if (remaining <= 0)
                    break;

                List<PointF> points = SamplePoints(contour.Points, sampleStep, remaining);
                if (points.Count == 0)
                    continue;

                remaining -= points.Count;
                result.Contours.Add(points);
                result.Points.AddRange(points);
            }

            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            result.PointCount = result.Points.Count;
            result.ContourCount = result.Contours.Count;
            result.PrimaryContour = result.Contours.Count > 0 ? result.Contours[0].ToList() : new List<PointF>();
            result.Success = result.PointCount > 0;
            result.Message = result.Success ? "OK" : result.ProcessedRegionCount == 0 ? "ROI区域无效或超出图像范围" : "未找到有效边缘点";
            return result;
        }

        private static Mat BuildBlurred(Mat gray, int blurSize)
        {
            int kernel = NormalizeBlurKernel(blurSize);

            Mat blurred = new Mat();
            if (kernel <= 1)
                gray.CopyTo(blurred);
            else
                Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(kernel, kernel), 0);
            return blurred;
        }

        /// <summary>
        /// 构建单个 ROI 局部 mask，坐标会从原图坐标平移到局部包围矩形坐标。
        /// </summary>
        private static Mat BuildLocalRoiMask(OpenCvSharp.Size size, IEnumerable<PointF> region, int offsetX, int offsetY)
        {
            Mat mask = new Mat(size, MatType.CV_8UC1, Scalar.Black);
            if (region == null)
                return mask;

            OpenCvSharp.Point[] polygon = region
                .Select(point => new OpenCvSharp.Point(
                    Clamp((int)Math.Round(point.X) - offsetX, 0, size.Width - 1),
                    Clamp((int)Math.Round(point.Y) - offsetY, 0, size.Height - 1)))
                .ToArray();
            if (polygon.Length >= 3)
                Cv2.FillPoly(mask, new[] { polygon }, Scalar.White);
            return mask;
        }

        /// <summary>
        /// 计算 ROI 的局部处理范围，并额外保留少量边界像素，避免 Canny 在裁剪边缘产生明显偏差。
        /// </summary>
        private static bool TryBuildRegionBounds(IEnumerable<PointF> region, int imageWidth, int imageHeight, int padding, out OpenCvSharp.Rect bounds)
        {
            bounds = new OpenCvSharp.Rect();
            if (region == null || imageWidth <= 0 || imageHeight <= 0)
                return false;

            List<PointF> points = region.ToList();
            if (points.Count < 3)
                return false;

            double minX = points.Min(point => point.X);
            double minY = points.Min(point => point.Y);
            double maxX = points.Max(point => point.X);
            double maxY = points.Max(point => point.Y);
            int left = Clamp((int)Math.Floor(minX) - padding, 0, imageWidth - 1);
            int top = Clamp((int)Math.Floor(minY) - padding, 0, imageHeight - 1);
            int right = Clamp((int)Math.Ceiling(maxX) + padding, 0, imageWidth - 1);
            int bottom = Clamp((int)Math.Ceiling(maxY) + padding, 0, imageHeight - 1);
            int width = right - left + 1;
            int height = bottom - top + 1;
            if (width <= 1 || height <= 1)
                return false;

            bounds = new OpenCvSharp.Rect(left, top, width, height);
            return true;
        }

        /// <summary>
        /// 将局部轮廓点平移回原图坐标。
        /// </summary>
        private static List<PointF> OffsetContour(OpenCvSharp.Point[] contour, int offsetX, int offsetY)
        {
            return contour
                .Select(point => new PointF(point.X + offsetX, point.Y + offsetY))
                .ToList();
        }

        /// <summary>
        /// 按采样步长限制输出点数，避免找点结果在噪声边缘上产生过多点。
        /// </summary>
        private static List<PointF> SamplePoints(List<PointF> contour, int sampleStep, int maxCount)
        {
            var points = new List<PointF>();
            if (contour == null)
                return points;

            for (int index = 0; index < contour.Count && points.Count < maxCount; index += sampleStep)
                points.Add(new PointF(contour[index].X, contour[index].Y));
            return points;
        }

        /// <summary>
        /// 标准化高斯核大小，保证奇数核并兼容关闭平滑的 1。
        /// </summary>
        private static int NormalizeBlurKernel(int blurSize)
        {
            int kernel = Math.Max(1, blurSize);
            if (kernel % 2 == 0)
                kernel++;
            return kernel;
        }

        /// <summary>
        /// 计算局部处理范围的扩展像素，给高斯和 Canny 保留邻域。
        /// </summary>
        private static int GetProcessingPadding(int blurSize)
        {
            int kernel = NormalizeBlurKernel(blurSize);
            return Math.Max(2, kernel / 2 + 2);
        }

        private static List<List<PointF>> CloneRegions(IEnumerable<List<PointF>> regions)
        {
            return regions
                .Where(region => region != null && region.Count >= 3)
                .Select(region => region.Select(point => new PointF(point.X, point.Y)).ToList())
                .ToList();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        /// <summary>
        /// ROI 局部提取出的轮廓候选，保留原始点数用于模拟旧逻辑的全局长度排序。
        /// </summary>
        private sealed class ContourCandidate
        {
            /// <summary>
            /// 初始化轮廓候选。
            /// </summary>
            public ContourCandidate(int rawPointCount, List<PointF> points)
            {
                RawPointCount = rawPointCount;
                Points = points ?? new List<PointF>();
            }

            /// <summary>
            /// 采样前的轮廓点数。
            /// </summary>
            public int RawPointCount { get; private set; }

            /// <summary>
            /// 已平移回原图坐标的轮廓点。
            /// </summary>
            public List<PointF> Points { get; private set; }
        }
    }
}
