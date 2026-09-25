using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TieCornerInspection.WinForms.Controls;

/// <summary>
/// 提供以鼠标位置为中心缩放、右键拖动平移和双击复位的 WinForms 图像查看控件。
/// </summary>
public sealed class ZoomableImageBox : Control
{
    /// <summary>
    /// 定义单次放大或缩小使用的倍率。
    /// </summary>
    private const double ZoomStep = 1.2;

    /// <summary>
    /// 保存可测试的图像视口坐标变换。
    /// </summary>
    private readonly ImageViewport _viewport = new();

    /// <summary>
    /// 保存当前显示的位图；位图生命周期由调用方管理。
    /// </summary>
    private Bitmap? _image;

    /// <summary>
    /// 表示用户当前是否正在使用鼠标右键平移图像。
    /// </summary>
    private bool _isPanning;

    /// <summary>
    /// 保存上一次平移鼠标坐标，用于计算增量。
    /// </summary>
    private Point _lastPanPoint;

    /// <summary>
    /// 保存进入平移状态前的鼠标指针。
    /// </summary>
    private Cursor? _cursorBeforePanning;

    /// <summary>
    /// 创建启用了双缓冲和键盘焦点的图像查看控件。
    /// </summary>
    public ZoomableImageBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.FromArgb(30, 33, 38);
        TabStop = true;
    }

    /// <summary>
    /// 获取或设置当前显示的位图。
    /// 设置新位图后自动恢复适应窗口显示。
    /// </summary>
    public Bitmap? Image
    {
        get => _image;
        set
        {
            if (ReferenceEquals(_image, value))
            {
                return;
            }

            _image = value;
            UpdateViewport(resetView: true);
            Invalidate();
        }
    }

    /// <summary>
    /// 获取或设置没有图像时显示的中文提示文字。
    /// </summary>
    public string EmptyText { get; set; } = "暂无图像";

    /// <summary>
    /// 获取当前相对于适应窗口状态的用户缩放倍数。
    /// </summary>
    public double ZoomFactor => _viewport.UserZoomFactor;

    /// <summary>
    /// 以控件中心为锚点放大一级。
    /// </summary>
    public void ZoomIn()
    {
        ZoomAtClientPoint(new PointF(ClientSize.Width / 2F, ClientSize.Height / 2F), ZoomStep);
    }

    /// <summary>
    /// 以控件中心为锚点缩小一级。
    /// </summary>
    public void ZoomOut()
    {
        ZoomAtClientPoint(new PointF(ClientSize.Width / 2F, ClientSize.Height / 2F), 1.0 / ZoomStep);
    }

    /// <summary>
    /// 清除用户缩放和平移，使图像重新完整适应窗口。
    /// </summary>
    public void ResetView()
    {
        _viewport.Reset();
        Invalidate();
    }

    /// <summary>
    /// 把客户端整数坐标转换为原图整数坐标，并判断该点是否落在图像内部。
    /// </summary>
    /// <param name="clientPoint">客户端坐标。</param>
    /// <param name="imagePoint">转换后的原图坐标。</param>
    /// <returns>客户端点是否位于图像显示区域内。</returns>
    public bool TryClientToImage(Point clientPoint, out Point imagePoint)
    {
        imagePoint = Point.Empty;
        if (_image is null || !_viewport.HasValidLayout)
        {
            return false;
        }

        PointF converted = _viewport.ClientToImage(clientPoint);
        if (converted.X < 0 || converted.Y < 0 || converted.X >= _image.Width || converted.Y >= _image.Height)
        {
            return false;
        }

        imagePoint = new Point(
            Math.Clamp((int)Math.Round(converted.X), 0, _image.Width - 1),
            Math.Clamp((int)Math.Round(converted.Y), 0, _image.Height - 1));
        return true;
    }

    /// <summary>
    /// 把原图矩形转换成客户端绘制矩形，供 ROI 等叠加层使用。
    /// </summary>
    /// <param name="imageRectangle">原图坐标系中的矩形。</param>
    /// <returns>客户端坐标系中的矩形。</returns>
    public RectangleF ImageToClientRectangle(RectangleF imageRectangle)
    {
        PointF topLeft = _viewport.ImageToClient(imageRectangle.Location);
        PointF bottomRight = _viewport.ImageToClient(new PointF(imageRectangle.Right, imageRectangle.Bottom));
        return RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    /// <summary>
    /// 绘制背景、当前图像和外部订阅者提供的叠加层。
    /// </summary>
    /// <param name="e">绘图事件参数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        if (_image is null || !_viewport.HasValidLayout)
        {
            DrawEmptyText(e.Graphics);
            base.OnPaint(e);
            return;
        }

        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
        e.Graphics.InterpolationMode = _viewport.Scale >= 1.0
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBilinear;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.DrawImage(
            _image,
            _viewport.ImageRectangle,
            new RectangleF(0, 0, _image.Width, _image.Height),
            GraphicsUnit.Pixel);

        e.Graphics.CompositingMode = CompositingMode.SourceOver;
        base.OnPaint(e);
    }

    /// <summary>
    /// 控件尺寸变化时更新视口，并保持当前中心图像点。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateViewport(resetView: false);
        Invalidate();
    }

    /// <summary>
    /// 鼠标进入控件时获取焦点，使滚轮事件立即生效。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Focus();
    }

    /// <summary>
    /// 鼠标滚轮转动时以当前光标位置为中心缩放图像。
    /// </summary>
    /// <param name="e">鼠标事件参数。</param>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Delta == 0)
        {
            return;
        }

        ZoomAtClientPoint(e.Location, e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep);
    }

    /// <summary>
    /// 按下鼠标右键时进入图像平移状态。
    /// </summary>
    /// <param name="e">鼠标事件参数。</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Right && _image is not null)
        {
            _isPanning = true;
            _lastPanPoint = e.Location;
            _cursorBeforePanning = Cursor;
            Cursor = Cursors.Hand;
            Capture = true;
        }

        base.OnMouseDown(e);
    }

    /// <summary>
    /// 右键按住移动时按鼠标增量平移图像。
    /// </summary>
    /// <param name="e">鼠标事件参数。</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isPanning)
        {
            _viewport.PanBy(e.X - _lastPanPoint.X, e.Y - _lastPanPoint.Y);
            _lastPanPoint = e.Location;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    /// <summary>
    /// 松开鼠标右键时结束图像平移状态。
    /// </summary>
    /// <param name="e">鼠标事件参数。</param>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            EndPanning();
        }

        base.OnMouseUp(e);
    }

    /// <summary>
    /// 双击控件时恢复适应窗口显示。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnDoubleClick(EventArgs e)
    {
        ResetView();
        base.OnDoubleClick(e);
    }

    /// <summary>
    /// 控件失去鼠标捕获时安全结束平移状态。
    /// </summary>
    /// <param name="e">事件参数。</param>
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            EndPanning();
        }

        base.OnMouseCaptureChanged(e);
    }

    /// <summary>
    /// 更新视口尺寸，并按需恢复适应窗口状态。
    /// </summary>
    /// <param name="resetView">是否清除当前用户缩放和平移。</param>
    private void UpdateViewport(bool resetView)
    {
        Size imageSize = _image?.Size ?? Size.Empty;
        _viewport.SetViewport(ClientSize, imageSize);
        if (resetView)
        {
            _viewport.Reset();
        }
    }

    /// <summary>
    /// 以指定客户端点为锚点缩放并请求重绘。
    /// </summary>
    /// <param name="clientPoint">缩放锚点。</param>
    /// <param name="factor">相对缩放因子。</param>
    private void ZoomAtClientPoint(PointF clientPoint, double factor)
    {
        if (_image is null)
        {
            return;
        }

        _viewport.ZoomAt(clientPoint, factor);
        Invalidate();
    }

    /// <summary>
    /// 结束平移并恢复进入平移前的鼠标指针。
    /// </summary>
    private void EndPanning()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        Capture = false;
        Cursor = _cursorBeforePanning ?? Cursors.Default;
        _cursorBeforePanning = null;
    }

    /// <summary>
    /// 在没有图像时居中绘制中文提示文字。
    /// </summary>
    /// <param name="graphics">目标绘图对象。</param>
    private void DrawEmptyText(Graphics graphics)
    {
        TextRenderer.DrawText(
            graphics,
            EmptyText,
            Font,
            ClientRectangle,
            Color.Gainsboro,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }
}
