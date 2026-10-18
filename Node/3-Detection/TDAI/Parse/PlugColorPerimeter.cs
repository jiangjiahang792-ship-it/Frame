using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>随检测项保存的HSV颜色范围；色相使用OpenCV的0～179范围，支持红色跨零点。</summary>
    public sealed class PlugColorModel
    {
        /// <summary>主色相中心。</summary>
        public int Hue { get; set; }
        /// <summary>色相允许偏差；180代表不限制色相。</summary>
        public int HueTolerance { get; set; }
        /// <summary>饱和度下限。</summary>
        public int MinS { get; set; }
        /// <summary>饱和度上限。</summary>
        public int MaxS { get; set; }
        /// <summary>亮度下限。</summary>
        public int MinV { get; set; }
        /// <summary>亮度上限。</summary>
        public int MaxV { get; set; }
        /// <summary>生成独立副本，避免产品模板与编辑窗口共享可变参数。</summary>
        public PlugColorModel Clone() { return (PlugColorModel)MemberwiseClone(); }
    }

    /// <summary>单个独立轮廓的测量结果，坐标与模型输出一致。</summary>
    public sealed class PlugPerimeterRegion
    {
        /// <summary>闭运算后的外轮廓；空轮廓表示该目标没有有效颜色区域。</summary>
        public Point[] Contour { get; set; } = new Point[0];
        /// <summary>像素周长，不累加其它独立区域。</summary>
        public double Perimeter { get; set; }
        /// <summary>当前轮廓或缺失目标的外接框。</summary>
        public Rect Bounds { get; set; }
    }

    /// <summary>可替换的颜色学习和轮廓测量接口。</summary>
    public interface IPlugColorPerimeter
    {
        /// <summary>从模型前后防水栓框内学习颜色，并逐区域返回周长；颜色为空时学习。</summary>
        List<PlugPerimeterRegion> Measure(OutputImage input, List<DetResult> boxes, ref PlugColorModel color);
    }

    /// <summary>仅处理模型目标框：HSV颜色分割、面积过滤、闭运算、独立外轮廓测量。</summary>
    public sealed class PlugColorPerimeter : IPlugColorPerimeter
    {
        /// <summary>面积门槛为原始像素面积，包含恰好2000的区域。</summary>
        public const int MinimumArea = 2000;

        /// <summary>在每个目标框内处理颜色；使用实际推理图及其偏移，避免裁剪后坐标错位。</summary>
        public List<PlugPerimeterRegion> Measure(OutputImage input, List<DetResult> boxes, ref PlugColorModel color)
        {
            var patches = new List<Mat>();
            try
            {
                foreach (var box in boxes) patches.Add(ReadHsv(input, box.Box));
                if (color == null && patches.Count > 0) color = Learn(patches);
                var regions = new List<PlugPerimeterRegion>();
                for (int i = 0; i < patches.Count; i++)
                {
                    var found = Extract(patches[i], color, boxes[i].Box);
                    if (found.Count == 0) found.Add(new PlugPerimeterRegion { Bounds = boxes[i].Box });
                    regions.AddRange(found);
                }
                return regions;
            }
            finally { foreach (var patch in patches) patch.Dispose(); }
        }

        /// <summary>选取包含整个目标框的实际推理图；拒绝灰度输入，不伪造颜色信息。</summary>
        private static Mat ReadHsv(OutputImage input, Rect box)
        {
            if (input == null || input.Bitmaps == null) throw new InvalidOperationException("防水栓颜色识别需要彩色输入图像。");
            for (int i = 0; i < input.Bitmaps.Count; i++)
            {
                Mat image = input.Bitmaps[i];
                if (!OutputImage.HasValidImage(image)) continue;
                Rect offset = input.Rectangles != null && i < input.Rectangles.Count ? input.Rectangles[i] : new Rect();
                var local = new Rect(box.X - offset.X, box.Y - offset.Y, box.Width, box.Height);
                if (local.X < 0 || local.Y < 0 || local.Right > image.Width || local.Bottom > image.Height) continue;
                if (image.Type() != MatType.CV_8UC3 && image.Type() != MatType.CV_8UC4)
                    throw new InvalidOperationException("防水栓颜色识别需要8位彩色图，当前输入为灰度图或不支持的格式。");
                var hsv = new Mat();
                try
                {
                    using (var crop = new Mat(image, local))
                    using (var bgr = new Mat())
                    {
                        if (image.Channels() == 4) Cv2.CvtColor(crop, bgr, ColorConversionCodes.BGRA2BGR);
                        Cv2.CvtColor(image.Channels() == 4 ? bgr : crop, hsv, ColorConversionCodes.BGR2HSV);
                    }
                    return hsv;
                }
                catch { hsv.Dispose(); throw; }
            }
            throw new InvalidOperationException("防水栓颜色识别无法在输入图中定位完整目标框，请检查ROI偏移。");
        }

        /// <summary>从首个有效学习帧的所有目标框提取主色；定步采样限制开销，随后固定颜色学习周长。</summary>
        private static PlugColorModel Learn(List<Mat> patches)
        {
            var pixels = new List<Vec3b>();
            long area = patches.Sum(p => (long)p.Width * p.Height);
            int step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(area / 100000.0)));
            foreach (var patch in patches)
                for (int y = 0; y < patch.Height; y += step)
                    for (int x = 0; x < patch.Width; x += step) pixels.Add(patch.At<Vec3b>(y, x));
            var chromatic = pixels.Where(p => p.Item1 >= 35 && p.Item2 >= 30).ToList();
            bool colored = chromatic.Count >= Math.Max(1, pixels.Count / 10);
            int hue = 0;
            List<Vec3b> selected;
            if (colored)
            {
                var bins = new int[180];
                foreach (var p in chromatic) bins[p.Item0]++;
                hue = Enumerable.Range(0, 180).OrderByDescending(h =>
                    Enumerable.Range(-10, 21).Sum(d => bins[(h + d + 180) % 180])).ThenByDescending(h => bins[h]).ThenBy(h => h).First();
                selected = chromatic.Where(p => HueDistance(p.Item0, hue) <= 10).ToList();
            }
            else
            {
                var gray = pixels.Where(p => p.Item1 < 35).ToList();
                if (gray.Count == 0) throw new InvalidOperationException("防水栓颜色学习没有可用的颜色样本，请检查曝光。");
                int peak = gray.GroupBy(p => p.Item2 / 16).OrderByDescending(g => g.Count()).First().Key * 16 + 8;
                selected = gray.Where(p => Math.Abs(p.Item2 - peak) <= 32).ToList();
            }
            var saturation = selected.Select(p => (int)p.Item1).OrderBy(v => v).ToArray();
            var value = selected.Select(p => (int)p.Item2).OrderBy(v => v).ToArray();
            return new PlugColorModel { Hue = hue, HueTolerance = colored ? 10 : 180,
                MinS = colored ? Math.Max(20, saturation[(int)((saturation.Length - 1) * .01)] - 20) : 0,
                MaxS = colored ? 255 : 50,
                MinV = Math.Max(0, value[(int)((value.Length - 1) * .01)] - 25),
                MaxV = Math.Min(255, value[(int)((value.Length - 1) * .99)] + 25) };
        }

        /// <summary>计算环形色相距离。</summary>
        private static int HueDistance(int a, int b) { int d = Math.Abs(a - b); return Math.Min(d, 180 - d); }

        /// <summary>先按连通域实际像素数去小区域，再闭运算；每个外轮廓独立计算，不累计孔洞周长。</summary>
        private static List<PlugPerimeterRegion> Extract(Mat hsv, PlugColorModel color, Rect offset)
        {
            if (color.Hue < 0 || color.Hue > 179 || color.HueTolerance < 0 || color.HueTolerance > 180 ||
                color.MinS < 0 || color.MaxS > 255 || color.MinS > color.MaxS ||
                color.MinV < 0 || color.MaxV > 255 || color.MinV > color.MaxV)
                throw new InvalidOperationException("防水栓颜色参数无效，请重新一键学习。");
            using (var mask = new Mat())
            using (var extra = new Mat())
            using (var labels = new Mat())
            using (var stats = new Mat())
            using (var centers = new Mat())
            using (var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)))
            {
                int low = color.Hue - color.HueTolerance, high = color.Hue + color.HueTolerance;
                Cv2.InRange(hsv, new Scalar(Math.Max(0, low), color.MinS, color.MinV),
                    new Scalar(Math.Min(179, high), color.MaxS, color.MaxV), mask);
                if (color.HueTolerance < 180 && (low < 0 || high > 179))
                {
                    Cv2.InRange(hsv, new Scalar(low < 0 ? low + 180 : 0, color.MinS, color.MinV),
                        new Scalar(high > 179 ? high - 180 : 179, color.MaxS, color.MaxV), extra);
                    Cv2.BitwiseOr(mask, extra, mask);
                }
                int count = Cv2.ConnectedComponentsWithStats(mask, labels, stats, centers);
                var keep = new bool[count];
                for (int i = 1; i < count; i++) keep[i] = stats.At<int>(i, (int)ConnectedComponentsTypes.Area) >= MinimumArea;
                var labelAccess = labels.GetGenericIndexer<int>();
                var maskAccess = mask.GetGenericIndexer<byte>();
                for (int y = 0; y < mask.Height; y++)
                    for (int x = 0; x < mask.Width; x++) maskAccess[y, x] = keep[labelAccess[y, x]] ? (byte)255 : (byte)0;
                Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
                Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                return contours.Select(c => new PlugPerimeterRegion { Perimeter = Cv2.ArcLength(c, true),
                    Contour = c.Select(p => new Point(p.X + offset.X, p.Y + offset.Y)).ToArray(),
                    Bounds = Move(Cv2.BoundingRect(c), offset) }).OrderBy(r => r.Bounds.Y).ThenBy(r => r.Bounds.X).ToList();
            }
        }

        /// <summary>把局部轮廓外接框转换为模型坐标。</summary>
        private static Rect Move(Rect box, Rect offset) { return new Rect(box.X + offset.X, box.Y + offset.Y, box.Width, box.Height); }
    }
}
