using System;
using System.Drawing;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>图像显示的临时缩放与平移，独立于布局、撤销及方案持久化。</summary>
    public sealed class RuntimeImageViewport
    {
        /// <summary>内容区边框宽度。</summary>
        private const int Border = 2;
        /// <summary>相对内容区宽度的中心偏移。</summary>
        private double offsetX;
        /// <summary>相对内容区高度的中心偏移。</summary>
        private double offsetY;
        /// <summary>相对于等比适应控件的倍率。</summary>
        public float Zoom { get; private set; } = 1f;
        /// <summary>用户是否已清除当前显示，居中操作不取消此标记。</summary>
        public bool IsCleared { get; private set; }

        /// <summary>以控件本地鼠标坐标为锚点缩放，倍率限制为0.1至32。</summary>
        public void ZoomAt(float factor, PointF localPoint, Size controlSize)
        {
            if (!Finite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor));
            if (!Finite(localPoint.X) || !Finite(localPoint.Y)) throw new ArgumentOutOfRangeException(nameof(localPoint));
            RectangleF area = ContentBounds(controlSize);
            if (area.IsEmpty) return;
            float next = (float)Math.Max(0.1, Math.Min(32, (double)Zoom * factor));
            double ratio = next / (double)Zoom;
            double anchorX = (localPoint.X - area.X - area.Width / 2) / area.Width;
            double anchorY = (localPoint.Y - area.Y - area.Height / 2) / area.Height;
            offsetX = anchorX + (offsetX - anchorX) * ratio;
            offsetY = anchorY + (offsetY - anchorY) * ratio;
            Zoom = next;
        }

        /// <summary>将本地像素平移量归一化，控件改尺寸时保留相对中心偏移。</summary>
        public void Pan(PointF delta, Size controlSize)
        {
            if (!Finite(delta.X) || !Finite(delta.Y)) throw new ArgumentOutOfRangeException(nameof(delta));
            RectangleF area = ContentBounds(controlSize);
            if (area.IsEmpty) return;
            offsetX += delta.X / (double)area.Width;
            offsetY += delta.Y / (double)area.Height;
        }

        /// <summary>恢复等比适应及居中，不恢复已清除图像的显示。</summary>
        public void Reset() { Zoom = 1; offsetX = 0; offsetY = 0; }
        /// <summary>清除显示并恢复初始几何，下一张有效图像之前保持空白。</summary>
        public void Clear() { IsCleared = true; Reset(); }
        /// <summary>新有效图像允许显示，保留当前缩放和平移。</summary>
        public void NewImage() { IsCleared = false; }

        /// <summary>返回图像实际显示矩形，始终保持原图比例；无效尺寸返回空矩形。</summary>
        public RectangleF GetImageBounds(Size imageSize, Size controlSize)
        {
            RectangleF area = ContentBounds(controlSize);
            if (area.IsEmpty || imageSize.Width <= 0 || imageSize.Height <= 0) return RectangleF.Empty;
            double scale = Math.Min(area.Width / imageSize.Width, area.Height / imageSize.Height) * Zoom;
            double width = imageSize.Width * scale, height = imageSize.Height * scale;
            return new RectangleF((float)(area.X + (area.Width - width) / 2 + offsetX * area.Width),
                (float)(area.Y + (area.Height - height) / 2 + offsetY * area.Height), (float)width, (float)height);
        }

        /// <summary>静态图像、实时图像及ROI共用的内边框裁剪区域。</summary>
        internal static RectangleF ContentBounds(Size controlSize) => controlSize.Width <= Border * 2 || controlSize.Height <= Border * 2
            ? RectangleF.Empty : new RectangleF(Border, Border, controlSize.Width - Border * 2, controlSize.Height - Border * 2);
        /// <summary>拒绝能污染后续几何的非有限输入。</summary>
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
