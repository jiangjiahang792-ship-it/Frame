using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>向设计器宿主请求以指定鼠标位置为锚点缩放画布。</summary>
    public sealed class RuntimeCanvasZoomEventArgs : EventArgs
    {
        /// <summary>滚轮增量，正数放大。</summary>
        public int Delta { get; }
        /// <summary>鼠标在画布客户区的位置。</summary>
        public Point Location { get; }
        /// <summary>创建不依赖系统当前鼠标状态的缩放请求。</summary>
        public RuntimeCanvasZoomEventArgs(int delta, Point location) { Delta = delta; Location = location; }
    }

    /// <summary>图像视图输入与布局编辑分离，不把缩放平移写入方案。</summary>
    public sealed partial class RuntimeDesignCanvas
    {
        /// <summary>各图像控件独立的显示变换。</summary>
        private readonly Dictionary<string, RuntimeImageViewport> imageViews = new Dictionary<string, RuntimeImageViewport>();
        /// <summary>检测静态图变化，让撤销清图或加载新图能够恢复显示。</summary>
        private readonly Dictionary<string, string> imageSources = new Dictionary<string, string>();
        /// <summary>正在平移的图像编号。</summary>
        private string panningImageId;
        /// <summary>平移时捕获的鼠标按键。</summary>
        private MouseButtons imagePanButton;
        /// <summary>上一次平移指针位置，使用画布客户区坐标。</summary>
        private Point imagePanPoint;
        /// <summary>菜单打开时固定的目标，命令不依赖随后变化的选择或鼠标位置。</summary>
        private string contextImageId;
        /// <summary>宿主负责同步缩放工具栏和滚动条。</summary>
        public event EventHandler<RuntimeCanvasZoomEventArgs> ZoomRequested;
        /// <summary>实时控制器清除其持有的显示副本，不清除生产数据。</summary>
        public event Action<string> ImageClearRequested;
        /// <summary>当前设计画布缩放倍率。</summary>
        public float Zoom => zoom;

        /// <summary>转换坐标时保留小数，以便滚轮缩放保持鼠标锚点稳定。</summary>
        public PointF DocumentPoint(Point location) => new PointF((location.X - PageOrigin.X) / zoom, (location.Y - PageOrigin.Y) / zoom);
        /// <summary>由文档坐标计算当前画布客户区位置。</summary>
        public PointF CanvasPoint(PointF documentPoint) => new PointF(PageOrigin.X + documentPoint.X * zoom, PageOrigin.Y + documentPoint.Y * zoom);

        /// <summary>渲染器共用同一个控件显示变换，不共享跨窗口状态。</summary>
        internal RuntimeImageViewport GetImageViewport(string id)
        {
            RuntimeImageViewport view;
            if (!imageViews.TryGetValue(id, out view)) imageViews[id] = view = new RuntimeImageViewport();
            return view;
        }

        /// <summary>有效新图恢复显示；批次失效仍由控制器统一发起，避免重复重绘。</summary>
        internal void NotifyImageFrame(string id)
        {
            GetImageViewport(id).NewImage();
            surfaces.Invalidate(id);
        }

        /// <summary>删除/换页释放孤立视图，静态图片变化解除清除标记。</summary>
        private void SynchronizeImageViews()
        {
            if (session == null) return;
            var images = session.Document.Elements.Where(item => item.Kind == RuntimeWidgetKind.Image).ToArray();
            var ids = new HashSet<string>(images.Select(item => item.Id));
            foreach (string id in imageViews.Keys.Where(id => !ids.Contains(id)).ToArray()) imageViews.Remove(id);
            foreach (string id in imageSources.Keys.Where(id => !ids.Contains(id)).ToArray()) imageSources.Remove(id);
            foreach (var element in images)
            {
                string previous;
                if (imageSources.TryGetValue(element.Id, out previous) && !string.Equals(previous, element.ImageData, StringComparison.Ordinal))
                {
                    var view = GetImageViewport(element.Id);
                    view.Reset();
                    if (!string.IsNullOrEmpty(element.ImageData)) view.NewImage();
                    surfaces.Invalidate(element.Id);
                }
                imageSources[element.Id] = element.ImageData;
            }
        }

        /// <summary>按真实顶层命中测试，不穿过覆盖在图像上方的其它控件。</summary>
        private RuntimeLayoutElement HitImage(Point point)
        {
            if (session == null) return null;
            var hit = session.Document.Elements.LastOrDefault(item => ImageControlBounds(item).Contains(point));
            return hit?.Kind == RuntimeWidgetKind.Image ? hit : null;
        }

        /// <summary>获得运行填充或设计缩放后的控件屏幕边界。</summary>
        private RectangleF ImageControlBounds(RuntimeLayoutElement element) => PreviewMode ? GetPreviewBounds(element) : ToScreen(element.Bounds);

        /// <summary>图像渲染使用的客户区尺寸，设计模式采用文档坐标而非屏幕像素。</summary>
        private Size ImageViewSize(RuntimeLayoutElement element) => PreviewMode ? GetPreviewBounds(element).Size : element.Bounds.Size;

        /// <summary>将输入位置转成图像渲染器的局部坐标。</summary>
        private PointF ImageLocalPoint(RuntimeLayoutElement element, Point point)
        {
            RectangleF bounds = ImageControlBounds(element);
            float scale = PreviewMode ? 1 : zoom;
            return new PointF((point.X - bounds.X) / scale, (point.Y - bounds.Y) / scale);
        }

        /// <summary>只让目标图像缓存失效，其它图像和原生运行控件保持不动。</summary>
        private void RefreshImageView(string id)
        {
            surfaces.Invalidate(id);
            InvalidateElements(new[] { id });
        }

        /// <summary>滚轮默认缩放命中图像，Ctrl或空白位置交给设计画布缩放。</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (HandleImageMouseWheel(e, ModifierKeys) && e is HandledMouseEventArgs handled) handled.Handled = true;
            base.OnMouseWheel(e);
        }

        /// <summary>显式修饰键入口便于独立验证，不发送全局按键。</summary>
        internal bool HandleImageMouseWheel(MouseEventArgs e, Keys modifiers)
        {
            if (session == null || e.Delta == 0) return false;
            if (dragging || panningImageId != null) return true;
            var element = HitImage(e.Location);
            if (!PreviewMode && ((modifiers & Keys.Control) != 0 || element == null))
            {
                if (ZoomRequested != null) ZoomRequested(this, new RuntimeCanvasZoomEventArgs(e.Delta, e.Location));
                else SetZoom(zoom * (float)Math.Pow(1.2, Math.Max(-8, Math.Min(8, e.Delta / 120.0))));
                return true;
            }
            if (element == null) return false;
            GetImageViewport(element.Id).ZoomAt((float)Math.Pow(1.2, Math.Max(-8, Math.Min(8, e.Delta / 120.0))),
                ImageLocalPoint(element, e.Location), ImageViewSize(element));
            RefreshImageView(element.Id);
            return true;
        }

        /// <summary>双击恢复整图适应居中，不更改控件几何或恢复已清除的帧。</summary>
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            var element = HitImage(e.Location);
            if (element == null) return;
            StopImagePan();
            CancelPointerInteraction(false);
            GetImageViewport(element.Id).Reset();
            RefreshImageView(element.Id);
        }

        /// <summary>运行左键拖图；设计中保留左键编辑位置，使用中键拖图。</summary>
        private bool TryBeginImagePan(MouseEventArgs e)
        {
            if (dragging || (e.Button != MouseButtons.Middle && !(PreviewMode && e.Button == MouseButtons.Left))) return false;
            var element = HitImage(e.Location);
            if (element == null) return false;
            Focus();
            panningImageId = element.Id;
            imagePanButton = e.Button;
            imagePanPoint = e.Location;
            Capture = true;
            Cursor = Cursors.Hand;
            return true;
        }

        /// <summary>平移仅更新两个偏移量与一块失效区域。</summary>
        private bool TryMoveImage(MouseEventArgs e)
        {
            if (panningImageId == null) return false;
            var element = session?.Find(panningImageId);
            if (element == null) { StopImagePan(); return true; }
            float scale = PreviewMode ? 1 : zoom;
            GetImageViewport(element.Id).Pan(new PointF((e.X - imagePanPoint.X) / scale, (e.Y - imagePanPoint.Y) / scale), ImageViewSize(element));
            imagePanPoint = e.Location;
            RefreshImageView(element.Id);
            return true;
        }

        /// <summary>鼠标抬起结束拖图或显示命中图像的菜单，不干扰多选复制手势。</summary>
        private bool TryEndImagePointer(MouseEventArgs e)
        {
            if (panningImageId != null)
            {
                if (e.Button == imagePanButton) { TryMoveImage(e); StopImagePan(); }
                return true;
            }
            if (dragging || e.Button != MouseButtons.Right) return false;
            var element = HitImage(e.Location);
            if (element == null) return false;
            contextImageId = element.Id;
            imageMenu.Show(this, e.Location);
            return true;
        }

        /// <summary>先清空状态再释放捕获，避免捕获变化事件递归。</summary>
        private void StopImagePan()
        {
            if (panningImageId == null) return;
            panningImageId = null;
            Capture = false;
            Cursor = Cursors.Default;
        }

        /// <summary>运行清除只影响显示副本；设计清除示例图片通过现有撤销事务。</summary>
        internal void ClearImageDisplay(string id)
        {
            var element = session?.Find(id);
            if (element == null || element.Kind != RuntimeWidgetKind.Image) return;
            StopImagePan();
            if (!PreviewMode && !string.IsNullOrEmpty(element.ImageData)) session.Edit(doc => doc.Elements.First(item => item.Id == id).ImageData = null);
            GetImageViewport(id).Clear();
            if (PreviewMode) ImageClearRequested?.Invoke(id);
            RefreshImageView(id);
        }

        /// <summary>固定右键目标执行命令，异常沿用宿主提示。</summary>
        private void ClearImageMenuClicked(object sender, EventArgs e)
        {
            try { ClearImageDisplay(contextImageId); }
            catch (Exception ex) { EditError?.Invoke(ex.Message); }
            finally { contextImageId = null; }
        }

        /// <summary>命令点击会先关闭菜单再触发Click，此时目标由Click的finally释放。</summary>
        private void ImageMenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked) contextImageId = null;
        }
    }
}
