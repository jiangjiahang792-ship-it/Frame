using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using OpenCvSharp;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>与节点和界面解耦的端子角度算法接口；输入 Mat 只读且不接管所有权。</summary>
    public interface ITerminalAngleMeasurer
    {
        /// <summary>从两个原图 ROI 配置计算端子相对固定垂线的角度。</summary>
        TerminalAngleMeasurement Measure(Mat image, NodeParamTerminalAngle settings, CancellationToken token);
    }

    /// <summary>纯几何测量结果，失败角度为空。</summary>
    public sealed class TerminalAngleMeasurement
    {
        /// <summary>是否取得有效矩形。</summary>
        public bool Success { get; set; }
        /// <summary>相对垂线角度，顶部右倾为正。</summary>
        public double? Angle { get; set; }
        /// <summary>成功说明或失败原因。</summary>
        public string Message { get; set; }
        /// <summary>原图坐标中的矩形四角。</summary>
        public PointF[] Corners { get; set; } = new PointF[0];
        /// <summary>长轴上端中点。</summary>
        public PointF Top { get; set; }
        /// <summary>长轴下端中点。</summary>
        public PointF Bottom { get; set; }
        /// <summary>局部处理耗时，排除取图及 UI 绘制。</summary>
        public double AlgorithmMilliseconds { get; set; }
    }

    /// <summary>基于二值外轮廓的左右边界拟合算法，仅处理端子 ROI。</summary>
    public sealed class OpenCvTerminalAngleMeasurer : ITerminalAngleMeasurer
    {
        /// <summary>整体区域求左右边界中线，不逐行卡尺，不用 ROI 的方向抵消工件真实倾斜。</summary>
        public TerminalAngleMeasurement Measure(Mat image, NodeParamTerminalAngle settings, CancellationToken token)
        {
            var timer = Stopwatch.StartNew();
            var result = new TerminalAngleMeasurement();
            try
            {
                token.ThrowIfCancellationRequested();
                if (image == null || image.Empty() || image.Depth() != MatType.CV_8U) throw new ArgumentException("输入必须是有效八位图像。");
                if (settings == null) throw new ArgumentNullException("settings");
                settings.Validate(image.Width, image.Height);
                Rect rect = settings.TerminalRoi.ToRect(image.Width, image.Height);
                using (var crop = new Mat(image, rect))
                using (var gray = new Mat())
                using (var smooth = new Mat())
                using (var mask = new Mat())
                {
                    if (crop.Channels() == 1) crop.CopyTo(gray);
                    else if (crop.Channels() == 3 || crop.Channels() == 4)
                    {
                        if (settings.UseBlueChannel) Cv2.ExtractChannel(crop, gray, 0);
                        else Cv2.CvtColor(crop, gray, crop.Channels() == 3 ? ColorConversionCodes.BGR2GRAY : ColorConversionCodes.BGRA2GRAY);
                    }
                    else throw new ArgumentException("图像通道数不受支持。");
                    // HALCON GaussFilter 文档对应的 sigma，避免 OpenCV 默认核强度差异。
                    double[] sigmas = { 0.600, 1.075, 1.550, 2.025, 2.550 };
                    double sigma = sigmas[(settings.GaussianSize - 3) / 2];
                    Cv2.GaussianBlur(gray, smooth, new OpenCvSharp.Size(settings.GaussianSize, settings.GaussianSize), sigma, sigma, BorderTypes.Reflect | BorderTypes.Isolated);
                    Cv2.Threshold(smooth, mask, settings.Threshold, 255, ThresholdTypes.BinaryInv);
                    token.ThrowIfCancellationRequested();
                    OpenCvSharp.Point[][] contours; HierarchyIndex[] hierarchy;
                    Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxNone);
                    SideEdges edges = ExtractSideEdges(contours, settings);
                    if (edges.Bounds.Width <= 0 || edges.Bounds.Height <= 0) throw new InvalidOperationException("端子 ROI 内没有有效轮廓。");
                    Rect bounds = edges.Bounds;
                    if (bounds.Height / (double)rect.Height < settings.MinimumHeightCoverage) throw new InvalidOperationException("端子主体高度覆盖不足，请缩紧上下 ROI 或检查分割断裂。");
                    if (edges.Left.Count < Math.Max(20, bounds.Height / 4) || edges.Right.Count < Math.Max(20, bounds.Height / 4)) throw new InvalidOperationException("端子左右轮廓有效点不足，请检查分割或缩紧 ROI。");
                    if (bounds.Left < 3 || bounds.Right - 1 > rect.Width - 4) throw new InvalidOperationException("端子触及 ROI 左右边界，请扩大搜索框。");
                    SideLine leftLine = FitSideLine(edges.Left);
                    SideLine rightLine = FitSideLine(edges.Right);
                    if (!leftLine.IsValid || !rightLine.IsValid) throw new InvalidOperationException("端子左右轮廓直线拟合失败。");
                    double dr = leftLine.Dr + rightLine.Dr, dc = leftLine.Dc + rightLine.Dc;
                    double directionLength = Math.Sqrt(dr * dr + dc * dc);
                    if (directionLength < 0.0001) throw new InvalidOperationException("端子左右轮廓方向不一致。");
                    dr /= directionLength; dc /= directionLength;
                    if (dr < 0) { dr = -dr; dc = -dc; }
                    double centerX = edges.Centers.Sum(p => p.X) / edges.Centers.Count;
                    double centerY = edges.Centers.Sum(p => p.Y) / edges.Centers.Count;
                    ProjectionRange range = ProjectRange(edges.Centers, centerX, centerY, dc, dr);
                    double length = range.Maximum - range.Minimum, width = Median(edges.Widths);
                    if (width < settings.MinimumWidth || width > settings.MaximumWidth || length < 3 * width) throw new InvalidOperationException("端子矩形尺寸或长宽比不符合要求。");
                    if (dr < 0.7) throw new InvalidOperationException("端子长轴方向不符合竖直工位。");
                    Point2f top = new Point2f((float)(centerX + dc * range.Minimum), (float)(centerY + dr * range.Minimum));
                    Point2f bottom = new Point2f((float)(centerX + dc * range.Maximum), (float)(centerY + dr * range.Maximum));
                    Point2f[] corners = BuildCorners(top, bottom, dc, dr, width);
                    result.Angle = Math.Atan2(-dc, dr) * 180 / Math.PI;
                    result.Corners = corners.Select(p => new PointF(p.X + rect.X, p.Y + rect.Y)).ToArray();
                    result.Top = new PointF(top.X + rect.X, top.Y + rect.Y);
                    result.Bottom = new PointF(bottom.X + rect.X, bottom.Y + rect.Y);
                    result.Success = true; result.Message = "测量完成；固定水平 180°，相对垂线 90°，右倾为正。";
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { result.Message = exception.Message; }
            result.AlgorithmMilliseconds = timer.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>按每一行轮廓点提取左、右边界，减少端子中间断裂对方向的影响。</summary>
        private static SideEdges ExtractSideEdges(OpenCvSharp.Point[][] contours, NodeParamTerminalAngle settings)
        {
            var edges = new SideEdges();
            if (contours == null || contours.Length == 0) return edges;
            OpenCvSharp.Point[] points = contours.Where(c => c != null && c.Length >= 2).SelectMany(c => c).ToArray();
            if (points.Length < 4) return edges;
            Rect bounds = Cv2.BoundingRect(points);
            edges.Bounds = bounds;
            int[] left = Enumerable.Repeat(int.MaxValue, bounds.Bottom).ToArray();
            int[] right = Enumerable.Repeat(int.MinValue, bounds.Bottom).ToArray();
            foreach (OpenCvSharp.Point point in points)
            {
                if (point.Y < 0 || point.Y >= left.Length) continue;
                if (point.X < left[point.Y]) left[point.Y] = point.X;
                if (point.X > right[point.Y]) right[point.Y] = point.X;
            }
            double maximumRowWidth = settings.MaximumWidth * 1.8;
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                if (left[y] == int.MaxValue || right[y] == int.MinValue) continue;
                double width = right[y] - left[y] + 1;
                if (width < 2 || width > maximumRowWidth) continue;
                edges.Left.Add(new Point2f(left[y], y));
                edges.Right.Add(new Point2f(right[y], y));
                edges.Centers.Add(new Point2f((float)((left[y] + right[y]) * 0.5), y));
                edges.Widths.Add(width);
            }
            return edges;
        }

        /// <summary>使用 OpenCV 拟合单侧轮廓直线，并统一为向下方向。</summary>
        private static SideLine FitSideLine(IList<Point2f> points)
        {
            if (points == null || points.Count < 2) return new SideLine();
            using (InputArray input = InputArray.Create(points.ToArray()))
            using (Mat line = new Mat())
            {
                Cv2.FitLine(input, line, DistanceTypes.L2, 0, 0.01, 0.01);
                double dc = line.At<float>(0, 0), dr = line.At<float>(1, 0);
                double length = Math.Sqrt(dc * dc + dr * dr);
                if (length < 0.0001) return new SideLine();
                dc /= length; dr /= length;
                if (dr < 0) { dr = -dr; dc = -dc; }
                return new SideLine { IsValid = true, Dc = dc, Dr = dr };
            }
        }

        /// <summary>计算点集沿拟合方向的投影范围，用于截取结果中线。</summary>
        private static ProjectionRange ProjectRange(IList<Point2f> points, double centerX, double centerY, double dc, double dr)
        {
            var range = new ProjectionRange { Minimum = double.MaxValue, Maximum = double.MinValue };
            foreach (Point2f point in points)
            {
                double value = (point.X - centerX) * dc + (point.Y - centerY) * dr;
                if (value < range.Minimum) range.Minimum = value;
                if (value > range.Maximum) range.Maximum = value;
            }
            return range;
        }

        /// <summary>由中线和宽度生成显示用矩形四角。</summary>
        private static Point2f[] BuildCorners(Point2f top, Point2f bottom, double dc, double dr, double width)
        {
            double half = width * 0.5;
            float nx = (float)(dr * half), ny = (float)(-dc * half);
            return new[]
            {
                new Point2f(top.X - nx, top.Y - ny),
                new Point2f(top.X + nx, top.Y + ny),
                new Point2f(bottom.X + nx, bottom.Y + ny),
                new Point2f(bottom.X - nx, bottom.Y - ny)
            };
        }

        /// <summary>求宽度中位数，避免少量毛刺行改变尺寸判断。</summary>
        private static double Median(IList<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            double[] ordered = values.OrderBy(value => value).ToArray();
            int middle = ordered.Length / 2;
            return ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) * 0.5;
        }
        /// <summary>每行左右轮廓点和对应宽度。</summary>
        private sealed class SideEdges
        {
            /// <summary>左侧轮廓点。</summary>
            public List<Point2f> Left { get; } = new List<Point2f>();
            /// <summary>右侧轮廓点。</summary>
            public List<Point2f> Right { get; } = new List<Point2f>();
            /// <summary>左右轮廓的行中心点。</summary>
            public List<Point2f> Centers { get; } = new List<Point2f>();
            /// <summary>逐行轮廓宽度。</summary>
            public List<double> Widths { get; } = new List<double>();
            /// <summary>全部候选轮廓点的包围框。</summary>
            public Rect Bounds { get; set; }
        }

        /// <summary>拟合后的单侧边界直线。</summary>
        private sealed class SideLine
        {
            /// <summary>拟合是否有效。</summary>
            public bool IsValid { get; set; }
            /// <summary>列方向分量。</summary>
            public double Dc { get; set; }
            /// <summary>行方向分量。</summary>
            public double Dr { get; set; }
        }

        /// <summary>投影范围。</summary>
        private struct ProjectionRange
        {
            /// <summary>最小投影。</summary>
            public double Minimum { get; set; }
            /// <summary>最大投影。</summary>
            public double Maximum { get; set; }
        }
    }
}
