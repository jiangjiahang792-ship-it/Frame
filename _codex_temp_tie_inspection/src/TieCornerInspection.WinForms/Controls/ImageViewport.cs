namespace TieCornerInspection.WinForms.Controls;

/// <summary>
/// 维护图像在客户端区域中的适应比例、用户缩放和平移坐标变换。
/// </summary>
public sealed class ImageViewport
{
    /// <summary>
    /// 定义允许的最小用户缩放倍数。
    /// </summary>
    public const double MinimumUserZoomFactor = 0.1;

    /// <summary>
    /// 定义允许的最大用户缩放倍数。
    /// </summary>
    public const double MaximumUserZoomFactor = 20.0;

    /// <summary>
    /// 保存当前客户端区域尺寸。
    /// </summary>
    private Size _clientSize;

    /// <summary>
    /// 保存当前图像尺寸。
    /// </summary>
    private Size _imageSize;

    /// <summary>
    /// 保存图像左上角在客户端坐标系中的位置。
    /// </summary>
    private PointF _offset;

    /// <summary>
    /// 获取适应窗口时的基础缩放比例。
    /// </summary>
    public double FitScale { get; private set; } = 1.0;

    /// <summary>
    /// 获取用户在适应比例基础上施加的缩放倍数。
    /// </summary>
    public double UserZoomFactor { get; private set; } = 1.0;

    /// <summary>
    /// 获取最终用于绘制和坐标换算的总缩放比例。
    /// </summary>
    public double Scale => FitScale * UserZoomFactor;

    /// <summary>
    /// 获取图像在客户端区域中的实际绘制矩形。
    /// </summary>
    public RectangleF ImageRectangle => new(
        _offset.X,
        _offset.Y,
        (float)(_imageSize.Width * Scale),
        (float)(_imageSize.Height * Scale));

    /// <summary>
    /// 获取当前是否已经设置有效的客户端和图像尺寸。
    /// </summary>
    public bool HasValidLayout => _clientSize.Width > 0 && _clientSize.Height > 0 &&
                                  _imageSize.Width > 0 && _imageSize.Height > 0;

    /// <summary>
    /// 设置客户端和图像尺寸；首次设置或更换图像时自动适应窗口。
    /// 调整客户端大小时保持原窗口中心对应的图像点不变。
    /// </summary>
    /// <param name="clientSize">控件客户区尺寸。</param>
    /// <param name="imageSize">原始图像尺寸。</param>
    public void SetViewport(Size clientSize, Size imageSize)
    {
        if (clientSize.Width <= 0 || clientSize.Height <= 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            _clientSize = clientSize;
            _imageSize = imageSize;
            FitScale = 1.0;
            UserZoomFactor = 1.0;
            _offset = PointF.Empty;
            return;
        }

        bool hadValidLayout = HasValidLayout;
        bool imageChanged = _imageSize != imageSize;
        PointF previousCenterImagePoint = hadValidLayout
            ? ClientToImage(new PointF(_clientSize.Width / 2F, _clientSize.Height / 2F))
            : PointF.Empty;

        _clientSize = clientSize;
        _imageSize = imageSize;
        FitScale = Math.Min(
            clientSize.Width / (double)imageSize.Width,
            clientSize.Height / (double)imageSize.Height);

        if (!hadValidLayout || imageChanged)
        {
            Reset();
            return;
        }

        PointF currentClientCenter = new(clientSize.Width / 2F, clientSize.Height / 2F);
        _offset = new PointF(
            currentClientCenter.X - (float)(previousCenterImagePoint.X * Scale),
            currentClientCenter.Y - (float)(previousCenterImagePoint.Y * Scale));
    }

    /// <summary>
    /// 以指定客户端坐标为锚点调整用户缩放倍数。
    /// </summary>
    /// <param name="clientAnchor">鼠标所在的客户端坐标。</param>
    /// <param name="factor">相对于当前倍数的缩放因子。</param>
    public void ZoomAt(PointF clientAnchor, double factor)
    {
        if (!HasValidLayout || factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor))
        {
            return;
        }

        PointF imageAnchor = ClientToImage(clientAnchor);
        UserZoomFactor = Math.Clamp(
            UserZoomFactor * factor,
            MinimumUserZoomFactor,
            MaximumUserZoomFactor);
        _offset = new PointF(
            clientAnchor.X - (float)(imageAnchor.X * Scale),
            clientAnchor.Y - (float)(imageAnchor.Y * Scale));
    }

    /// <summary>
    /// 按客户端像素偏移量平移图像。
    /// </summary>
    /// <param name="deltaX">水平方向位移。</param>
    /// <param name="deltaY">垂直方向位移。</param>
    public void PanBy(float deltaX, float deltaY)
    {
        if (!HasValidLayout)
        {
            return;
        }

        _offset = new PointF(_offset.X + deltaX, _offset.Y + deltaY);
    }

    /// <summary>
    /// 清除用户缩放和平移，使图像重新完整适应并居中显示。
    /// </summary>
    public void Reset()
    {
        UserZoomFactor = 1.0;
        if (!HasValidLayout)
        {
            _offset = PointF.Empty;
            return;
        }

        _offset = new PointF(
            (_clientSize.Width - (float)(_imageSize.Width * Scale)) / 2F,
            (_clientSize.Height - (float)(_imageSize.Height * Scale)) / 2F);
    }

    /// <summary>
    /// 把客户端坐标转换成原图坐标。
    /// </summary>
    /// <param name="clientPoint">客户端坐标。</param>
    /// <returns>对应的原图浮点坐标。</returns>
    public PointF ClientToImage(PointF clientPoint)
    {
        if (!HasValidLayout || Scale <= double.Epsilon)
        {
            return PointF.Empty;
        }

        return new PointF(
            (float)((clientPoint.X - _offset.X) / Scale),
            (float)((clientPoint.Y - _offset.Y) / Scale));
    }

    /// <summary>
    /// 把原图坐标转换成客户端坐标。
    /// </summary>
    /// <param name="imagePoint">原图坐标。</param>
    /// <returns>对应的客户端浮点坐标。</returns>
    public PointF ImageToClient(PointF imagePoint)
    {
        if (!HasValidLayout)
        {
            return PointF.Empty;
        }

        return new PointF(
            _offset.X + (float)(imagePoint.X * Scale),
            _offset.Y + (float)(imagePoint.Y * Scale));
    }
}
