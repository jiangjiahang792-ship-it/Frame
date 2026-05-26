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
        public List<PointF> Points { get; set; } = new List<PointF>();
        public List<PointF> PrimaryContour { get; set; } = new List<PointF>();
        public List<List<PointF>> Contours { get; set; } = new List<List<PointF>>();
        public List<List<PointF>> Regions { get; set; } = new List<List<PointF>>();
    }

    internal static class FindPointAlgorithm
    {
        public static FindPointMeasureResult Execute(Mat source, NodeParamFindPoint param, CancellationToken token)
        {
            if (source == null || source.Empty())
                throw new Exception("输入图像为空。");
            if (param == null)
                throw new Exception("找点参数为空。");
            if (param.Regions == null || param.Regions.Count == 0)
                throw new Exception("请先绘制并确认 ROI 区域。");

            Stopwatch stopwatch = Stopwatch.StartNew();
            var result = new FindPointMeasureResult
            {
                Regions = CloneRegions(param.Regions)
            };

            using (Mat gray = CaliperMeasurementAlgorithm.ToGray(source))
            using (Mat blurred = BuildBlurred(gray, param.BlurSize))
            using (Mat edges = new Mat())
            using (Mat mask = BuildRoiMask(gray.Size(), param.Regions))
            using (Mat roiEdges = new Mat())
            {
                token.ThrowIfCancellationRequested();
                int low = Math.Max(0, Math.Min(param.LowThreshold, param.HighThreshold - 1));
                int high = Math.Max(low + 1, param.HighThreshold);
                Cv2.Canny(blurred, edges, low, high);
                Cv2.BitwiseAnd(edges, mask, roiEdges);

                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(roiEdges, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);

                int minContourPoints = Math.Max(1, param.MinContourPoints);
                int sampleStep = Math.Max(1, param.SampleStep);
                int remaining = param.MaxPointCount <= 0 ? int.MaxValue : param.MaxPointCount;

                foreach (OpenCvSharp.Point[] contour in contours.OrderByDescending(c => c.Length))
                {
                    token.ThrowIfCancellationRequested();
                    if (contour == null || contour.Length < minContourPoints || remaining <= 0)
                        continue;

                    List<PointF> points = SampleContour(contour, sampleStep, remaining);
                    if (points.Count == 0)
                        continue;

                    remaining -= points.Count;
                    result.Contours.Add(points);
                    result.Points.AddRange(points);
                }
            }

            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            result.PointCount = result.Points.Count;
            result.ContourCount = result.Contours.Count;
            result.PrimaryContour = result.Contours.Count > 0 ? result.Contours[0].ToList() : new List<PointF>();
            result.Success = result.PointCount > 0;
            result.Message = result.Success ? "OK" : "未找到有效边缘点";
            return result;
        }

        private static Mat BuildBlurred(Mat gray, int blurSize)
        {
            int kernel = Math.Max(1, blurSize);
            if (kernel % 2 == 0)
                kernel++;

            Mat blurred = new Mat();
            if (kernel <= 1)
                gray.CopyTo(blurred);
            else
                Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(kernel, kernel), 0);
            return blurred;
        }

        private static Mat BuildRoiMask(OpenCvSharp.Size size, IEnumerable<List<PointF>> regions)
        {
            Mat mask = new Mat(size, MatType.CV_8UC1, Scalar.Black);
            foreach (List<PointF> region in regions)
            {
                if (region == null || region.Count < 3)
                    continue;

                OpenCvSharp.Point[] polygon = region
                    .Select(point => new OpenCvSharp.Point(
                        Clamp((int)Math.Round(point.X), 0, size.Width - 1),
                        Clamp((int)Math.Round(point.Y), 0, size.Height - 1)))
                    .ToArray();
                Cv2.FillPoly(mask, new[] { polygon }, Scalar.White);
            }
            return mask;
        }

        private static List<PointF> SampleContour(OpenCvSharp.Point[] contour, int sampleStep, int maxCount)
        {
            var points = new List<PointF>();
            for (int index = 0; index < contour.Length && points.Count < maxCount; index += sampleStep)
                points.Add(new PointF(contour[index].X, contour[index].Y));
            return points;
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
    }
}
