using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>缓存控件在当前缩放下的绘制结果，移动时只合成位图，保持原有图层顺序。</summary>
    internal sealed class RuntimeWidgetSurfaceCache : IDisposable
    {
        /// <summary>每个画布最多保留 32 MB 表面，超大单个控件直接绘制。</summary>
        private const long MaximumBytes = 32L * 1024 * 1024;
        /// <summary>按控件短标识索引，避免每帧对大图片字符串计算哈希。</summary>
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        /// <summary>当前已保留的位图字节数。</summary>
        private long bytes;
        /// <summary>当前帧序号，保护本帧已使用的表面不被循环淘汰。</summary>
        private long sequence;

        /// <summary>开始一帧合成；超过预算的组件直接绘制，不挤出本帧工作集。</summary>
        public void BeginFrame() { sequence++; }

        /// <summary>单个控件的只读绘图结果和失效依据。</summary>
        private sealed class Entry : IDisposable
        {
            /// <summary>生成表面时的隔离属性副本。</summary>
            public RuntimeLayoutElement Element;
            /// <summary>实际像素表面。</summary>
            public Bitmap Bitmap;
            /// <summary>生成表面的缩放倍率。</summary>
            public float Zoom;
            /// <summary>是否为缩放拖动期间的快速预览。</summary>
            public bool Fast;
            /// <summary>最近使用次序。</summary>
            public long Used;
            /// <summary>表面占用字节数。</summary>
            public long Bytes => (long)Bitmap.Width * Bitmap.Height * 4;
            /// <summary>释放 GDI 位图。</summary>
            public void Dispose() => Bitmap.Dispose();
        }

        /// <summary>复用未改变的内容；尺寸、外观或图片改变时重新绘制，坐标变化不重建。</summary>
        public void Draw(Graphics graphics, IRuntimeWidgetRenderer renderer, RuntimeLayoutElement element,
            Size size, float zoom, bool fast)
        {
            if (!(renderer is IRuntimeCacheableWidgetRenderer))
            {
                DrawDirect(graphics, renderer, element, size, fast);
                return;
            }
            Entry entry;
            if (entries.TryGetValue(element.Id, out entry) &&
                (entry.Zoom != zoom || entry.Fast != fast || !Matches(entry.Element, element, size)))
            { Remove(element.Id); entry = null; }
            if (entry == null)
            {
                int width = Math.Max(1, (int)Math.Ceiling(size.Width * zoom));
                int height = Math.Max(1, (int)Math.Ceiling(size.Height * zoom));
                long required = (long)width * height * 4;
                var draft = element.Copy();
                draft.Width = size.Width; draft.Height = size.Height;
                if (required > MaximumBytes)
                {
                    DrawDirect(graphics, renderer, draft, size, fast);
                    return;
                }
                while (bytes + required > MaximumBytes && entries.Count > 0)
                {
                    var candidate = entries.Where(pair => pair.Value.Used != sequence).OrderBy(pair => pair.Value.Used).FirstOrDefault();
                    if (candidate.Value == null)
                    {
                        DrawDirect(graphics, renderer, draft, size, fast);
                        return;
                    }
                    Remove(candidate.Key);
                }
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var target = Graphics.FromImage(bitmap))
                    {
                        target.ScaleTransform(zoom, zoom);
                        target.SmoothingMode = SmoothingMode.AntiAlias;
                        target.InterpolationMode = fast ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear;
                        renderer.Draw(target, draft);
                    }
                }
                catch { bitmap.Dispose(); throw; }
                entry = new Entry { Element = draft, Bitmap = bitmap, Zoom = zoom, Fast = fast };
                entries.Add(element.Id, entry);
                bytes += entry.Bytes;
            }
            entry.Used = sequence;
            // 位图已按屏幕分辨率绘制，合成时不再做高质量图片缩放。
            var state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(entry.Bitmap, new RectangleF(0, 0, size.Width, size.Height),
                new RectangleF(0, 0, entry.Bitmap.Width, entry.Bitmap.Height), GraphicsUnit.Pixel);
            graphics.Restore(state);
        }

        /// <summary>不缓存动态扩展或超预算控件，仍使用临时尺寸且不修改原文档。</summary>
        private static void DrawDirect(Graphics graphics, IRuntimeWidgetRenderer renderer, RuntimeLayoutElement element, Size size, bool fast)
        {
            var draft = element.Copy();
            draft.Width = size.Width; draft.Height = size.Height;
            var state = graphics.Save();
            try
            {
                graphics.InterpolationMode = fast ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBilinear;
                renderer.Draw(graphics, draft);
            }
            finally { graphics.Restore(state); }
        }

        /// <summary>仅比较影响外观的值；图片字符串保持引用，编辑帧不扫描其内容。</summary>
        private static bool Matches(RuntimeLayoutElement cached, RuntimeLayoutElement value, Size size)
        {
            return cached.Kind == value.Kind && cached.Width == size.Width && cached.Height == size.Height &&
                cached.Name == value.Name && cached.Text == value.Text && cached.FontSize == value.FontSize && cached.Bold == value.Bold &&
                cached.TextAlignment == value.TextAlignment &&
                cached.Foreground == value.Foreground && cached.Background == value.Background &&
                cached.ElapsedMilliseconds == value.ElapsedMilliseconds && cached.SignalOn == value.SignalOn &&
                cached.OnColor == value.OnColor && cached.OffColor == value.OffColor &&
                ReferenceEquals(cached.ImageData, value.ImageData);
        }

        /// <summary>删除或导入后及时释放离开文档的表面。</summary>
        public void Synchronize(RuntimeLayoutDocument document)
        {
            var ids = new HashSet<string>(document.Elements.Select(element => element.Id));
            foreach (string id in entries.Keys.Where(id => !ids.Contains(id)).ToArray()) Remove(id);
        }

        /// <summary>单个图像视图缩放、平移或清除后只释放其旧表面，不清空其它控件缓存。</summary>
        public void Invalidate(string id)
        {
            if (id != null && entries.ContainsKey(id)) Remove(id);
        }

        /// <summary>释放单个表面并更新预算。</summary>
        private void Remove(string id)
        {
            var entry = entries[id];
            bytes -= entry.Bytes;
            entry.Dispose();
            entries.Remove(id);
        }

        /// <summary>绘制器替换或画布关闭时清空所有缓存。</summary>
        public void Dispose()
        {
            foreach (var entry in entries.Values) entry.Dispose();
            entries.Clear();
            bytes = 0;
        }
    }
}
