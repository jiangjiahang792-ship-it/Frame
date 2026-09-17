using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>模板取样区域类型，选择工具不产生区域。</summary>
    public enum TemplateRegionKind
    {
        /// <summary>选择和变换已有区域。</summary>
        选择,
        /// <summary>可旋转矩形。</summary>
        矩形,
        /// <summary>椭圆或圆形。</summary>
        椭圆,
        /// <summary>可调整起始角和张角的扇形。</summary>
        扇形,
        /// <summary>逐点闭合的多边形。</summary>
        多边形,
        /// <summary>自由画笔覆盖区域。</summary>
        画笔
    }

    /// <summary>可持久化的原图区域；形状与位姿分离，便于增加新的绘制工具。</summary>
    public sealed class TemplateRegion
    {
        /// <summary>区域类型。</summary>
        public TemplateRegionKind Kind { get; set; }
        /// <summary>区域名称。</summary>
        public string Name { get; set; }
        /// <summary>是否参与区域并集。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>原图中心横坐标。</summary>
        public float CenterX { get; set; }
        /// <summary>原图中心纵坐标。</summary>
        public float CenterY { get; set; }
        /// <summary>旋转前宽度。</summary>
        public float Width { get; set; }
        /// <summary>旋转前高度。</summary>
        public float Height { get; set; }
        /// <summary>图像坐标系顺时针角度。</summary>
        public float Angle { get; set; }
        /// <summary>扇形起始角。</summary>
        public float StartAngle { get; set; } = -45;
        /// <summary>扇形张角。</summary>
        public float SweepAngle { get; set; } = 90;
        /// <summary>相对于宽高的归一化多边形顶点或画笔中心线。</summary>
        public List<PointF> Points { get; set; } = new List<PointF>();
        /// <summary>画笔宽度相对于区域较短边的比例。</summary>
        public float StrokeRatio { get; set; }

        /// <summary>复制可修改数据，隔离撤销、编辑与生产快照。</summary>
        public TemplateRegion Copy()
        {
            var copy = (TemplateRegion)MemberwiseClone();
            copy.Points = Points?.ToList() ?? new List<PointF>();
            return copy;
        }

        /// <summary>从局部坐标转换到原图坐标。</summary>
        public PointF Transform(float x, float y)
        {
            double angle = Angle * Math.PI / 180;
            return new PointF(CenterX + (float)(x * Math.Cos(angle) - y * Math.Sin(angle)),
                CenterY + (float)(x * Math.Sin(angle) + y * Math.Cos(angle)));
        }

        /// <summary>原图坐标转换到旋转前的局部坐标。</summary>
        public PointF Inverse(PointF point)
        {
            double angle = Angle * Math.PI / 180;
            double x = point.X - CenterX, y = point.Y - CenterY;
            return new PointF((float)(x * Math.Cos(angle) + y * Math.Sin(angle)),
                (float)(-x * Math.Sin(angle) + y * Math.Cos(angle)));
        }

        /// <summary>生成填充与命中测试共用的路径；调用方负责释放。</summary>
        public GraphicsPath CreatePath()
        {
            Validate();
            var path = new GraphicsPath(FillMode.Winding);
            try
            {
                var box = new RectangleF(-Width / 2, -Height / 2, Width, Height);
                if (Kind == TemplateRegionKind.矩形) path.AddRectangle(box);
                else if (Kind == TemplateRegionKind.椭圆) path.AddEllipse(box);
                else if (Kind == TemplateRegionKind.扇形) path.AddPie(box.X, box.Y, box.Width, box.Height, StartAngle, SweepAngle);
                else
                {
                    PointF[] points = Points.Select(p => new PointF(p.X * Width, p.Y * Height)).ToArray();
                    if (Kind == TemplateRegionKind.多边形) path.AddPolygon(points);
                    else
                    {
                        float diameter = StrokeRatio * Math.Min(Width, Height);
                        if (points.Length == 1) path.AddEllipse(points[0].X - diameter / 2, points[0].Y - diameter / 2, diameter, diameter);
                        else
                        {
                            path.AddLines(points);
                            using (var pen = new Pen(Color.White, diameter) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                                path.Widen(pen);
                        }
                    }
                }
                using (var matrix = new Matrix())
                {
                    matrix.Rotate(Angle, MatrixOrder.Append);
                    matrix.Translate(CenterX, CenterY, MatrixOrder.Append);
                    path.Transform(matrix);
                }
                return path;
            }
            catch { path.Dispose(); throw; }
        }

        /// <summary>拒绝损坏的交换数据，避免非有限数值进入 GDI 或原生接口。</summary>
        public void Validate()
        {
            float[] values = { CenterX, CenterY, Width, Height, Angle, StartAngle, SweepAngle, StrokeRatio };
            if (values.Any(v => !ContourCompatibility.IsFinite(v)) || Width <= 0 || Height <= 0 ||
                Width > 1000000 || Height > 1000000 || Math.Abs(CenterX) > 1000000 || Math.Abs(CenterY) > 1000000 ||
                Math.Abs(Angle) > 360 || Math.Abs(StartAngle) > 360 ||
                Kind == TemplateRegionKind.选择 || !Enum.IsDefined(typeof(TemplateRegionKind), Kind) ||
                SweepAngle <= 0 || SweepAngle > 360 || Points == null || Points.Count > 20000 ||
                Points.Any(p => !ContourCompatibility.IsFinite(p.X) || !ContourCompatibility.IsFinite(p.Y) || Math.Abs(p.X) > 2 || Math.Abs(p.Y) > 2) ||
                (Kind == TemplateRegionKind.多边形 && Points.Count < 3) ||
                (Kind == TemplateRegionKind.画笔 && (Points.Count == 0 || StrokeRatio <= 0 || StrokeRatio > 2)))
                throw new ArgumentException("模板区域数据无效。");
        }
    }

    /// <summary>可选的掩膜建模能力；不破坏已有匹配器接口。</summary>
    public interface IMaskedShapeMatcher : IShapeMatcher
    {
        /// <summary>非零掩膜像素参与建模，掩膜与完整来源图同尺寸。</summary>
        void CreateMaskedModel(Mat image, Rectangle roi, CreateModelOptions options, byte[] mask);
    }

    /// <summary>将可编辑区域的并集转换成原生掩膜，仅在创建或恢复时计算。</summary>
    internal static class TemplateRegionMask
    {
        /// <summary>计算区域与图像相交的共同外接框，空区域不能退回全图。</summary>
        internal static Rectangle Bounds(IReadOnlyList<TemplateRegion> regions, int width, int height)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            foreach (var region in regions)
            {
                if (region == null) throw new ArgumentException("模板区域不能为空。");
                region.Validate();
            }
            RectangleF union = RectangleF.Empty;
            foreach (var region in regions.Where(r => r.Enabled))
            using (var path = region.CreatePath())
            {
                RectangleF clipped = RectangleF.Intersect(path.GetBounds(), new RectangleF(0, 0, width, height));
                if (clipped.Width > 0 && clipped.Height > 0) union = union.IsEmpty ? clipped : RectangleF.Union(union, clipped);
            }
            if (union.IsEmpty) throw new InvalidOperationException("请至少绘制并启用一个位于图像内的区域。");
            return Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(union.Left), (int)Math.Floor(union.Top),
                (int)Math.Ceiling(union.Right), (int)Math.Ceiling(union.Bottom)), new Rectangle(0, 0, width, height));
        }

        /// <summary>按整图像素生成二值并集，不修改原图，因此不会制造 ROI 边界轮廓。</summary>
        internal static byte[] Rasterize(IReadOnlyList<TemplateRegion> regions, int width, int height)
        {
            var mask = new byte[checked(width * height)];
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    graphics.SmoothingMode = SmoothingMode.None;
                    foreach (var region in regions.Where(r => r.Enabled))
                    using (var path = region.CreatePath()) graphics.FillPath(Brushes.White, path);
                }
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var row = new byte[checked(width * 4)];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = 0; x < width; x++) mask[y * width + x] = row[x * 4];
                    }
                }
                finally { bitmap.UnlockBits(data); }
            }
            return mask;
        }

        /// <summary>旧模板保持矩形行为，新模板使用区域并集；生产恢复复用相同路径。</summary>
        internal static void Create(IShapeMatcher matcher, Mat image, Rectangle roi, CreateModelOptions options, IReadOnlyList<TemplateRegion> regions)
        {
            if (regions == null) { matcher.CreateModel(image, roi, options); return; }
            if (!(matcher is IMaskedShapeMatcher masked)) throw new NotSupportedException("当前匹配器不支持组合区域建模。");
            Rectangle bounds = Bounds(regions, image.Width, image.Height);
            if (bounds != roi) throw new ArgumentException("模板外接框与组合区域不一致，请重新创建模板。");
            masked.CreateMaskedModel(image, roi, options, Rasterize(regions, image.Width, image.Height));
        }
    }
}
