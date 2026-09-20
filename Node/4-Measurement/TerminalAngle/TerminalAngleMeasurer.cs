using System;
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

    /// <summary>移植已验证的 OpenCV 最大主体分割与最小面积矩形算法，仅处理端子 ROI。</summary>
    public sealed class OpenCvTerminalAngleMeasurer : ITerminalAngleMeasurer
    {
        /// <summary>整体区域求矩形，不逐行卡尺，不用 ROI 的方向抵消工件真实倾斜。</summary>
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
                using (var labels = new Mat())
                using (var stats = new Mat())
                using (var centers = new Mat())
                using (var main = new Mat())
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
                    int count = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centers, PixelConnectivity.Connectivity8, MatType.CV_32S);
                    int best = 0, area = 0;
                    for (int i = 1; i < count; i++)
                    {
                        int candidate = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                        if (candidate > area) { best = i; area = candidate; }
                    }
                    token.ThrowIfCancellationRequested();
                    if (best == 0 || area < 20) throw new InvalidOperationException("端子 ROI 内没有有效主体。");
                    Cv2.InRange(labels, new Scalar(best), new Scalar(best), main);
                    OpenCvSharp.Point[][] contours; HierarchyIndex[] hierarchy;
                    Cv2.FindContours(main, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    // 内孔不改变最小外接矩形，直接使用外轮廓等价于填孔后再求矩形，省去额外掩膜。
                    var points = contours.SelectMany(p => p).ToArray();
                    Rect bounds = Cv2.BoundingRect(points);
                    if (bounds.Height / (double)rect.Height < settings.MinimumHeightCoverage) throw new InvalidOperationException("端子主体高度覆盖不足，请缩紧上下 ROI 或检查分割断裂。");
                    if (bounds.Left < 3 || bounds.Right - 1 > rect.Width - 4) throw new InvalidOperationException("端子触及 ROI 左右边界，请扩大搜索框。");
                    RotatedRect box = Cv2.MinAreaRect(points);
                    Point2f[] corners = box.Points();
                    int longest = 0;
                    for (int i = 1; i < 4; i++) if (Length(corners[i], corners[(i + 1) % 4]) > Length(corners[longest], corners[(longest + 1) % 4])) longest = i;
                    Point2f a = corners[longest], b = corners[(longest + 1) % 4];
                    double length = Length(a, b), width = Length(b, corners[(longest + 2) % 4]);
                    if (width < settings.MinimumWidth || width > settings.MaximumWidth || length < 3 * width) throw new InvalidOperationException("端子矩形尺寸或长宽比不符合要求。");
                    double dr = (b.Y - a.Y) / length, dc = (b.X - a.X) / length;
                    if (dr < 0) { dr = -dr; dc = -dc; }
                    if (dr < 0.7) throw new InvalidOperationException("端子长轴方向不符合竖直工位。");
                    double x = rect.X + box.Center.X, y = rect.Y + box.Center.Y;
                    result.Angle = Math.Atan2(-dc, dr) * 180 / Math.PI;
                    result.Corners = corners.Select(p => new PointF(p.X + rect.X, p.Y + rect.Y)).ToArray();
                    result.Top = new PointF((float)(x - dc * length / 2), (float)(y - dr * length / 2));
                    result.Bottom = new PointF((float)(x + dc * length / 2), (float)(y + dr * length / 2));
                    result.Success = true; result.Message = "测量完成；固定水平 180°，相对垂线 90°，右倾为正。";
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { result.Message = exception.Message; }
            result.AlgorithmMilliseconds = timer.Elapsed.TotalMilliseconds;
            return result;
        }
        /// <summary>像素中心间的欧氏距离。</summary>
        private static double Length(Point2f a, Point2f b) { return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)); }
    }
}
