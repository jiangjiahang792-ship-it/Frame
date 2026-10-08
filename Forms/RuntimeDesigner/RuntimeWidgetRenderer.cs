using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>组件绘制扩展接口，后续新增组件无需改变画布交互逻辑。</summary>
    public interface IRuntimeWidgetRenderer : IDisposable
    {
        /// <summary>此绘制器支持的组件类型。</summary>
        RuntimeWidgetKind Kind { get; }
        /// <summary>在组件本地逻辑坐标内绘制内容。</summary>
        void Draw(Graphics graphics, RuntimeLayoutElement element);
        /// <summary>文档变化后释放不再使用的资源。</summary>
        void Synchronize(RuntimeLayoutDocument document);
    }

    /// <summary>声明绘制结果仅取决于组件外观属性和本地尺寸，可复用表面；动态扩展绘制器不实现此接口。</summary>
    public interface IRuntimeCacheableWidgetRenderer : IRuntimeWidgetRenderer { }

    /// <summary>内置组件共享的背景和文字绘制。</summary>
    public abstract class RuntimeWidgetRenderer : IRuntimeWidgetRenderer
    {
        /// <summary>派生类声明支持的组件类型。</summary>
        public abstract RuntimeWidgetKind Kind { get; }
        /// <summary>绘制组件内容。</summary>
        public abstract void Draw(Graphics graphics, RuntimeLayoutElement element);
        /// <summary>无缓存的组件不需要同步资源。</summary>
        public virtual void Synchronize(RuntimeLayoutDocument document) { }
        /// <summary>无托管绘图缓存时无需释放。</summary>
        public virtual void Dispose() { }

        /// <summary>绘制平直的组件背景和细边框。</summary>
        protected static void Background(Graphics graphics, RuntimeLayoutElement element)
        {
            using (var brush = new SolidBrush(RuntimeLayoutSerializer.ParseColor(element.Background)))
                graphics.FillRectangle(brush, 0, 0, element.Width, element.Height);
            using (var pen = new Pen(Color.FromArgb(76, 80, 85)))
                graphics.DrawRectangle(pen, 0, 0, element.Width - 1, element.Height - 1);
        }

        /// <summary>在限定区域内绘制中文，字号过大时自动缩小且不越界。</summary>
        protected static void Text(Graphics graphics, string value, RuntimeLayoutElement element,
            RectangleF bounds, float size, Color color, bool center = false, StringAlignment? alignment = null)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var format = new StringFormat
            {
                Alignment = alignment ?? (center ? StringAlignment.Center : StringAlignment.Near),
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var brush = new SolidBrush(color))
            {
                float fitted = Math.Min(size, bounds.Height * 0.7f);
                using (var measureFont = new Font("Microsoft YaHei UI", Math.Max(6, fitted), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    SizeF measured = graphics.MeasureString(value, measureFont, (int)Math.Max(1, bounds.Width), format);
                    if (measured.Height > bounds.Height) fitted *= Math.Max(0.35f, bounds.Height / measured.Height);
                }
                using (var font = new Font("Microsoft YaHei UI", Math.Max(6, fitted), element.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                    graphics.DrawString(value, font, brush, bounds, format);
            }
        }
    }

    /// <summary>显示内嵌图片并管理位图缓存。</summary>
    public sealed class RuntimeImageRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>由画布提供的临时视口，不写入组件属性。</summary>
        private readonly Func<string, RuntimeImageViewport> findViewport;
        /// <summary>未提供视口时保持既有等比居中行为。</summary>
        private readonly RuntimeImageViewport defaultViewport = new RuntimeImageViewport();
        /// <summary>图片内容相同的组件共享只读位图。</summary>
        private readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
        /// <summary>图像组件类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Image;

        /// <summary>保留无参数构造，兼容旧扩展与测试。</summary>
        public RuntimeImageRenderer() : this(null) { }
        /// <summary>接入按控件编号独立管理的临时视口。</summary>
        public RuntimeImageRenderer(Func<string, RuntimeImageViewport> findViewport) { this.findViewport = findViewport; }
        /// <summary>查询清除状态，供实时渲染器一起隐藏ROI和状态文字。</summary>
        internal bool IsCleared(string id) => findViewport?.Invoke(id)?.IsCleared ?? false;
        /// <summary>与ROI共用完全相同的缩放及平移几何。</summary>
        internal RectangleF GetImageBounds(string id, Size imageSize, Size controlSize) =>
            (findViewport?.Invoke(id) ?? defaultViewport).GetImageBounds(imageSize, controlSize);

        /// <summary>整个控件内部等比显示图片，不绘制标题；无图片时显示棋盘背景和占位图标。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            DrawImage(graphics, element, null, false);
        }

        /// <summary>运行模式直接显示独立位图，不编码到模板，也不把实时帧加入静态图片缓存。</summary>
        internal void DrawImage(Graphics graphics, RuntimeLayoutElement element, Bitmap liveImage, bool live)
        {
            var area = RuntimeImageViewport.ContentBounds(new Size(element.Width, element.Height));
            var state = graphics.Save();
            try
            {
                // 上、左侧压暗，下、右侧提亮；实心细条使缩放后的凹边保持清晰。
                graphics.SmoothingMode = SmoothingMode.None;
                DrawInsetEdge(graphics, new RectangleF(0, 0, element.Width, element.Height),
                    Color.FromArgb(28, 28, 28), Color.FromArgb(80, 80, 80));
                DrawInsetEdge(graphics, new RectangleF(1, 1, element.Width - 2, element.Height - 2),
                    Color.FromArgb(41, 41, 41), Color.FromArgb(61, 61, 61));
                bool cleared = IsCleared(element.Id);
                using (var brush = new SolidBrush(cleared ? Color.FromArgb(48, 48, 48) : RuntimeLayoutSerializer.ParseColor(element.Background)))
                    graphics.FillRectangle(brush, area);
                if (cleared || area.IsEmpty) return;
                graphics.SetClip(area, CombineMode.Intersect);
                if (liveImage != null || (!live && !string.IsNullOrEmpty(element.ImageData)))
                {
                    Bitmap bitmap = liveImage;
                    if (bitmap == null && !images.TryGetValue(element.ImageData, out bitmap))
                    {
                        bitmap = RuntimeLayoutSerializer.DecodeImage(element.ImageData);
                        images.Add(element.ImageData, bitmap);
                    }
                    var target = GetImageBounds(element.Id, bitmap.Size, new Size(element.Width, element.Height));
                    graphics.DrawImage(bitmap, target);
                }
                else
                {
                    using (var checker = new SolidBrush(Color.FromArgb(47, 49, 52)))
                        for (int y = 0; y < element.Height; y += 16)
                            for (int x = 0; x < element.Width; x += 16)
                                if (((x / 16) + (y / 16)) % 2 == 0)
                                    graphics.FillRectangle(checker, x, y, 16, 16);
                    using (var icon = RuntimeDesignerIcons.Create(0xEB9F, Color.FromArgb(134, 141, 149), 42))
                        graphics.DrawImage(icon, area.X + (area.Width - 42) / 2, area.Y + (area.Height - 42) / 2, 42, 42);
                }
            }
            finally { graphics.Restore(state); }
        }

        /// <summary>绘制一层一像素的内凹边缘，转角由对应的阴影边或亮边连续衔接。</summary>
        private static void DrawInsetEdge(Graphics graphics, RectangleF bounds, Color shadow, Color highlight)
        {
            using (var dark = new SolidBrush(shadow))
            using (var light = new SolidBrush(highlight))
            {
                graphics.FillRectangle(dark, bounds.X, bounds.Y, bounds.Width, 1);
                graphics.FillRectangle(dark, bounds.X, bounds.Y, 1, bounds.Height);
                graphics.FillRectangle(light, bounds.X + 1, bounds.Bottom - 1, bounds.Width - 1, 1);
                graphics.FillRectangle(light, bounds.Right - 1, bounds.Y + 1, 1, bounds.Height - 1);
            }
        }

        /// <summary>撤销、删除或导入后释放已离开文档的图像。</summary>
        public override void Synchronize(RuntimeLayoutDocument document)
        {
            var retained = new HashSet<string>(document.Elements.Where(e => e.Kind == Kind && e.ImageData != null).Select(e => e.ImageData));
            foreach (string key in images.Keys.Where(key => !retained.Contains(key)).ToArray())
            { images[key].Dispose(); images.Remove(key); }
        }

        /// <summary>关闭画布时释放所有位图。</summary>
        public override void Dispose()
        {
            foreach (var bitmap in images.Values) bitmap.Dispose();
            images.Clear();
        }
    }

    /// <summary>运行耗时预览组件。</summary>
    public sealed class RuntimeElapsedRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>耗时组件类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Elapsed;
        /// <summary>显示标题和独立的毫秒数值。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            Text(graphics, element.Text, element, new RectangleF(14, 5, element.Width - 28, element.Height * 0.32f),
                13, Color.FromArgb(172, 178, 185));
            Text(graphics, element.ElapsedMilliseconds.ToString("0.##") + " ms", element,
                new RectangleF(14, element.Height * 0.34f, element.Width - 28, element.Height * 0.58f),
                element.FontSize, RuntimeLayoutSerializer.ParseColor(element.Foreground));
        }
    }

    /// <summary>多行文本预览组件。</summary>
    public sealed class RuntimeTextRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>文本组件类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Text;
        /// <summary>显示由属性栏配置的文本内容。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            StringAlignment alignment = element.TextAlignment == RuntimeTextAlignment.Center ? StringAlignment.Center :
                element.TextAlignment == RuntimeTextAlignment.Right ? StringAlignment.Far : StringAlignment.Near;
            Text(graphics, element.Text, element, new RectangleF(12, 6, element.Width - 24, element.Height - 12),
                element.FontSize, RuntimeLayoutSerializer.ParseColor(element.Foreground), alignment: alignment);
        }
    }

    /// <summary>布尔信号灯预览组件。</summary>
    public sealed class RuntimeSignalRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>信号灯组件类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Signal;
        /// <summary>用亮灭颜色表达状态，旁边显示配置的信号名称。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            float diameter = Math.Min(30, Math.Min(element.Height - 12, element.Width / 3f));
            using (var brush = new SolidBrush(RuntimeLayoutSerializer.ParseColor(element.SignalOn ? element.OnColor : element.OffColor)))
                graphics.FillEllipse(brush, 12, (element.Height - diameter) / 2, diameter, diameter);
            Text(graphics, element.Text, element, new RectangleF(diameter + 24, 6, element.Width - diameter - 36, element.Height - 12),
                element.FontSize, RuntimeLayoutSerializer.ParseColor(element.Foreground));
        }
    }

    /// <summary>使用 Windows 内置图标字体提供统一的工具图标。</summary>
    internal static class RuntimeDesignerIcons
    {
        /// <summary>生成指定尺寸的透明图标，调用方负责释放。</summary>
        public static Bitmap Create(int glyph, Color color, int size = 20)
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe MDL2 Assets", size * 0.72f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.DrawString(char.ConvertFromUtf32(glyph), font, brush, new RectangleF(0, 0, size, size), format);
            }
            return bitmap;
        }
    }
}
