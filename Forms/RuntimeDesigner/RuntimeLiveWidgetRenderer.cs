using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>使用显示侧最新结果绘图，不实现静态缓存接口，避免缓存上一帧。</summary>
    internal sealed class RuntimeLiveWidgetRenderer : RuntimeWidgetRenderer
    {
        /// <summary>此实例负责的控件类型。</summary>
        private readonly RuntimeWidgetKind kind;
        /// <summary>仅由界面线程读取的结果查询。</summary>
        private readonly Func<string, RuntimeValueUpdate> find;
        /// <summary>复用现有内凹边框与等比显示。</summary>
        private readonly RuntimeImageRenderer image;
        /// <summary>复用现有耗时外观。</summary>
        private readonly RuntimeElapsedRenderer elapsed = new RuntimeElapsedRenderer();
        /// <summary>静态图仅缓存尺寸，位图仍由既有图像绘制器持有。</summary>
        private readonly Dictionary<string, Size> staticSizes = new Dictionary<string, Size>();
        /// <summary>建立只读运行渲染器。</summary>
        public RuntimeLiveWidgetRenderer(RuntimeWidgetKind kind, Func<string, RuntimeValueUpdate> find)
            : this(kind, find, null) { }
        /// <summary>实时图与静态图共用画布视口；不提供回调时行为保持兼容。</summary>
        public RuntimeLiveWidgetRenderer(RuntimeWidgetKind kind, Func<string, RuntimeValueUpdate> find, Func<string, RuntimeImageViewport> findViewport)
        { this.kind = kind; this.find = find; image = new RuntimeImageRenderer(findViewport); }
        /// <summary>控件类型。</summary>
        public override RuntimeWidgetKind Kind => kind;

        /// <summary>未绑定时显示静态内容，已绑定但无结果时明确显示等待状态。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            if (kind == RuntimeWidgetKind.Image && image.IsCleared(element.Id))
            { image.DrawImage(graphics, element, null, true); return; }
            if (element.Binding == null && (kind != RuntimeWidgetKind.Image || element.RoiBinding == null))
            {
                if (kind == RuntimeWidgetKind.Image) image.Draw(graphics, element);
                else elapsed.Draw(graphics, element);
                return;
            }
            var value = find(element.Id);
            string status = element.Binding == null ? null : value == null ? "等待数据" : value.Status;
            if (kind == RuntimeWidgetKind.Image)
            {
                bool live = element.Binding != null;
                image.DrawImage(graphics, element, live ? value?.Image : null, live);
                if (element.RoiBinding != null)
                {
                    string roiStatus = value == null ? "等待ROI" : value.RoiStatus;
                    if (value?.Roi != null && string.IsNullOrEmpty(roiStatus))
                    {
                        var state = graphics.Save();
                        try
                        {
                            Size size = live ? value.Image?.Size ?? Size.Empty : StaticSize(element.ImageData);
                            if (size.Width > 0 && size.Height > 0)
                            {
                                var controlSize = new Size(element.Width, element.Height);
                                var bounds = image.GetImageBounds(element.Id, size, controlSize);
                                graphics.SetClip(RuntimeImageViewport.ContentBounds(controlSize), CombineMode.Intersect);
                                Size original = live && value.ImageSourceSize.Width > 0 && value.ImageSourceSize.Height > 0
                                    ? value.ImageSourceSize : size;
                                value.Roi.Draw(graphics, bounds, original, new Size(element.Width, element.Height));
                            }
                        }
                        catch (Exception ex)
                        {
                            // 隔离绘制失败，不销毁独立图像，也不在每次重绘时重复调用坏图元。
                            roiStatus = value.RoiStatus = "ROI绘制失败";
                            System.Diagnostics.Trace.TraceWarning("运行界面ROI绘制失败：" + ex.Message);
                        }
                        finally { graphics.Restore(state); }
                    }
                    if (string.IsNullOrEmpty(status)) status = roiStatus;
                }
            }
            else if (value?.ElapsedMilliseconds != null && string.IsNullOrEmpty(status))
            {
                var draft = element.Copy();
                draft.ElapsedMilliseconds = value.ElapsedMilliseconds.Value;
                elapsed.Draw(graphics, draft);
            }
            else Background(graphics, element);
            if (!string.IsNullOrEmpty(status))
            {
                var area = new RectangleF(4, Math.Max(4, element.Height / 2f - 16), element.Width - 8, Math.Min(32, element.Height - 8));
                using (var brush = new SolidBrush(Color.FromArgb(225, 48, 50, 53))) graphics.FillRectangle(brush, area);
                Text(graphics, status, element, area, 14, Color.FromArgb(190, 195, 201), true);
            }
        }

        /// <summary>同步未绑定的静态图像缓存。</summary>
        public override void Synchronize(RuntimeLayoutDocument document)
        {
            image.Synchronize(document);
            var retained = new HashSet<string>(document.Elements.Where(e => e.Kind == RuntimeWidgetKind.Image
                && e.Binding == null && e.RoiBinding != null && e.ImageData != null).Select(e => e.ImageData));
            foreach (string key in staticSizes.Keys.Where(key => !retained.Contains(key)).ToArray()) staticSizes.Remove(key);
        }
        /// <summary>静态图首次叠加时解码一次取得原图尺寸，不保留第二份位图。</summary>
        private Size StaticSize(string data)
        {
            if (string.IsNullOrEmpty(data)) return Size.Empty;
            Size size;
            if (!staticSizes.TryGetValue(data, out size))
            {
                using (var bitmap = RuntimeLayoutSerializer.DecodeImage(data)) size = bitmap.Size;
                staticSizes.Add(data, size);
            }
            return size;
        }
        /// <summary>释放内部静态图片。</summary>
        public override void Dispose() { image.Dispose(); elapsed.Dispose(); staticSizes.Clear(); }
    }
}
