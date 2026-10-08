using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>复制框架显示结果到私有ROI图元，绘制沿用ShowImageControl，不持有生产结果列表。</summary>
    internal sealed class RuntimeRoiSnapshot : IRuntimeRoiOverlay
    {
        /// <summary>每份快照限制复杂度，避免异常结果无限占用收件槽。</summary>
        private const int MaximumShapes = 4096;
        /// <summary>全部轮廓点的最大数量。</summary>
        private const int MaximumPoints = 65536;
        /// <summary>单份快照的保守内存上限。</summary>
        private const long MaximumBytes = 2L * 1024 * 1024;
        /// <summary>仅显示方访问的独立图元；释放后替换为空数组。</summary>
        private ShowImageControl.IRoiShape[] shapes;
        /// <summary>复制完成时固化的占用估算。</summary>
        public long EstimatedBytes { get; }

        /// <summary>接管已经复制完成且有界的图元集合。</summary>
        private RuntimeRoiSnapshot(List<ShowImageControl.IRoiShape> shapes, long bytes)
        { this.shapes = shapes.ToArray(); EstimatedBytes = bytes; }

        /// <summary>在流程完成通知中复制，后续生产结果清空或修改不会改变显示快照。</summary>
        internal static RuntimeRoiSnapshot Create(AlgorithmResult source)
        {
            if (source == null) return null;
            var builder = new Builder();
            if (source.Rects != null)
                foreach (var rect in source.Rects) builder.AddRectangle(rect);
            if (source.RectsNgMap != null)
                foreach (var group in source.RectsNgMap.Values)
                    if (group != null) foreach (var rect in group) builder.AddRectangle(rect);
            if (source.Lines != null)
                foreach (var line in source.Lines)
                {
                    if (line == null) continue;
                    CheckNumbers(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y, line.LineWidth);
                    builder.Add(new ShowImageControl.RoiLine(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y)
                    { ShapeColor = line.Color, StrokeWidth = line.LineWidth, ShowCenterCross = line.ShowCenterCross, IsStatic = true });
                }
            if (source.Circles != null)
                foreach (var circle in source.Circles)
                {
                    if (circle == null) continue;
                    CheckNumbers(circle.Center.X, circle.Center.Y, circle.Radius, circle.LineWidth);
                    builder.Add(new ShowImageControl.RoiCircle(circle.Center.X, circle.Center.Y, circle.Radius)
                    { ShapeColor = circle.Color, StrokeWidth = circle.LineWidth, IsStatic = true });
                }
            if (source.Arcs != null)
                foreach (var arc in source.Arcs)
                {
                    if (arc == null) continue;
                    CheckNumbers(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.SweepAngle, arc.LineWidth);
                    builder.Add(new ShowImageControl.RoiArc(arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.SweepAngle)
                    { ShapeColor = arc.Color, StrokeWidth = arc.LineWidth, IsStatic = true });
                }
            if (source.Ellipses != null)
                foreach (var ellipse in source.Ellipses)
                {
                    if (ellipse == null) continue;
                    CheckNumbers(ellipse.Center.X, ellipse.Center.Y, ellipse.Width, ellipse.Height, ellipse.Angle, ellipse.LineWidth);
                    builder.Add(new ShowImageControl.RoiEllipse(ellipse.Center.X, ellipse.Center.Y, ellipse.Width, ellipse.Height, ellipse.Angle)
                    { ShapeColor = ellipse.Color, StrokeWidth = ellipse.LineWidth, IsStatic = true });
                }
            if (source.Contours != null)
                foreach (var contour in source.Contours)
                {
                    if (contour == null) continue;
                    var points = contour.Points;
                    int count = points?.Count ?? 0;
                    builder.ReservePoints(count);
                    var copy = new List<PointF>(count);
                    if (points != null)
                        foreach (var point in points) { CheckNumbers(point.X, point.Y); copy.Add(point); }
                    CheckNumbers(contour.LineWidth);
                    builder.Add(new ShowImageControl.RoiContour(copy, contour.Color) { StrokeWidth = contour.LineWidth });
                }
            if (source.Texts != null)
            {
                if (source.Texts.Count > MaximumShapes) throw new InvalidDataException("ROI文本数量超出显示上限。");
                var texts = source.Texts.Where(text => text != null).ToArray();
                foreach (var text in texts)
                {
                    builder.ReserveText(text.Text); builder.ReserveText(text.Title);
                    CheckNumbers(text.ImagePosition.X, text.ImagePosition.Y);
                    if (text.FontSize > 512) throw new InvalidDataException("ROI字号超出显示上限。");
                    if (text.UseImagePosition)
                        builder.Add(new ShowImageControl.RoiTextBlock(text.Title,
                            new List<(string text, Color color)> { (text.Text, text.Color) },
                            text.ImagePosition.X, text.ImagePosition.Y, Math.Max(1, text.FontSize)));
                }
                foreach (var group in texts.Where(text => !text.UseImagePosition)
                    .GroupBy(text => new { text.Position, text.FontSize, text.Margin, text.Title, text.CoordinateMode }))
                    builder.Add(new ShowImageControl.RoiTextBlock(group.Key.Title,
                        group.Select(text => (text.Text, text.Color)).ToList(), 10, 10, Math.Max(1, group.Key.FontSize))
                    {
                        Position = group.Key.Position, Margin = Math.Max(0, group.Key.Margin),
                        ImageWidth = 1, ImageHeight = 1,
                        UseControlCoordinate = group.Key.CoordinateMode == DisplayTextCoordinateMode.Control
                    });
            }
            return new RuntimeRoiSnapshot(builder.Shapes, builder.Bytes);
        }

        /// <summary>复用框架图元的角度、线宽、中心十字和文本布局；仅处理图像坐标到显示坐标变换。</summary>
        public void Draw(Graphics graphics, RectangleF imageBounds, Size sourceImageSize, Size controlSize)
        {
            if (sourceImageSize.Width <= 0 || sourceImageSize.Height <= 0 || imageBounds.Width <= 0 || imageBounds.Height <= 0) return;
            float scaleY = imageBounds.Height / sourceImageSize.Height;
            float ratio = imageBounds.Width / sourceImageSize.Width / scaleY;
            foreach (var shape in shapes)
            {
                var state = graphics.Save();
                try
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var text = shape as ShowImageControl.RoiTextBlock;
                    if (text != null && text.UseControlCoordinate)
                    {
                        text.SetControlBounds(controlSize.Width, controlSize.Height);
                        graphics.SetClip(new RectangleF(2, 2, controlSize.Width - 4, controlSize.Height - 4), CombineMode.Intersect);
                        text.Draw(graphics, scaleY, imageBounds.Location);
                    }
                    else
                    {
                        if (text != null && text.ImageWidth > 0)
                        { text.ImageWidth = sourceImageSize.Width; text.ImageHeight = sourceImageSize.Height; }
                        graphics.SetClip(imageBounds, CombineMode.Intersect);
                        graphics.TranslateTransform(imageBounds.X, imageBounds.Y);
                        // 降采样的整数取整可能带来微小横纵比例差，按实际位图边界精确对齐。
                        graphics.ScaleTransform(ratio, 1);
                        shape.Draw(graphics, scaleY, PointF.Empty);
                    }
                }
                finally { graphics.Restore(state); }
            }
        }

        /// <summary>释放对复制点集的引用；图元不持有生产资源或常驻GDI句柄。</summary>
        public void Dispose() { Interlocked.Exchange(ref shapes, new ShowImageControl.IRoiShape[0]); }

        /// <summary>拒绝不能安全交给GDI的坐标或尺寸，不尝试纠正为任意位置。</summary>
        private static void CheckNumbers(params float[] values)
        {
            if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 10000000))
                throw new InvalidDataException("ROI包含非法坐标或尺寸。");
        }

        /// <summary>统一约束复制数量和内存，不把过大结果部分绘制成误导性叠加层。</summary>
        private sealed class Builder
        {
            /// <summary>待接管的私有图元。</summary>
            internal readonly List<ShowImageControl.IRoiShape> Shapes = new List<ShowImageControl.IRoiShape>();
            /// <summary>保守占用估算。</summary>
            internal long Bytes = 128;
            /// <summary>已经复制的轮廓点数量。</summary>
            private int pointCount;
            /// <summary>追加一个绘制图元。</summary>
            internal void Add(ShowImageControl.IRoiShape shape)
            {
                if (Shapes.Count >= MaximumShapes) throw new InvalidDataException("ROI图元数量超出显示上限。");
                Reserve(256); Shapes.Add(shape);
            }
            /// <summary>在分配点集前拒绝过大轮廓。</summary>
            internal void ReservePoints(int count)
            {
                if (count < 0 || count > MaximumPoints - pointCount) throw new InvalidDataException("ROI轮廓点数量超出显示上限。");
                pointCount += count; Reserve((long)count * 16);
            }
            /// <summary>限制文本引用与绘制成本。</summary>
            internal void ReserveText(string text)
            {
                if (text != null && text.Length > 4096) throw new InvalidDataException("ROI文本长度超出显示上限。");
                Reserve((text?.Length ?? 0) * 2L + 32);
            }
            /// <summary>合并图元及点集预算。</summary>
            private void Reserve(long bytes)
            {
                if (Bytes + bytes > MaximumBytes) throw new InvalidDataException("ROI快照超出显示内存上限。");
                Bytes += bytes;
            }
            /// <summary>沿用框架旋转矩形的实际颜色和角度。</summary>
            internal void AddRectangle(ColorRotatedRect rect)
            {
                if (rect == null) return;
                var value = rect.RotatedRect;
                CheckNumbers(value.center.x, value.center.y, value.size.width, value.size.height, value.angle, rect.LineWidth);
                Add(new ShowImageControl.RoiRotatedRect(value.center.x, value.center.y, value.size.width, value.size.height, value.angle)
                { ShapeColor = rect.Color, StrokeWidth = rect.LineWidth, IsStatic = true });
            }
        }
    }
}
