using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{

/// <summary>画布仅有查看、框选建模和删除轮廓三种状态。</summary>
public enum CanvasEditorMode
{
    /// <summary>查看图像或匹配结果。</summary>
    查看,
    /// <summary>拖动框选 ROI，等待右键建模。</summary>
    创建模板,
    /// <summary>涂抹标记待删除的特征，等待右键提交。</summary>
    涂抹
}

/// <summary>单图显示、ROI 框选、真实模型特征预览与删除交互。</summary>
public sealed partial class ImageCanvas : Control
{
    /// <summary>借用窗体拥有的图像。</summary>
    private ImageFrame _frame;
    /// <summary>已确认的模型 ROI。</summary>
    private Rectangle _modelRoi;
    /// <summary>当前框选的临时 ROI。</summary>
    private Rectangle _draftRoi;
    /// <summary>真实模型特征，使用 ROI 局部坐标。</summary>
    private IReadOnlyList<PointF> _features = Array.Empty<PointF>();
    /// <summary>当前编辑会话的删除遮罩，非零表示待删除。</summary>
    private byte[] _eraseMask;
    /// <summary>待删除特征标记，用于增量统计和颜色预览。</summary>
    private bool[] _pendingFeatures = Array.Empty<bool>();
    /// <summary>当前编辑状态。</summary>
    private CanvasEditorMode _mode;
    /// <summary>鼠标是否正在拖动。</summary>
    private bool _dragging;
    /// <summary>拖框的起点。</summary>
    private Point _origin;
    /// <summary>上一笔的图像坐标。</summary>
    private Point _lastBrushPoint;
    /// <summary>用于绘制画笔光标的客户区位置。</summary>
    private Point? _pointer;
    /// <summary>当前匹配结果。</summary>
    private IReadOnlyList<ShapeMatchResult> _matches = Array.Empty<ShapeMatchResult>();
    /// <summary>各匹配目标的有效模板轮廓，缓存为搜索图坐标，避免每次重绘重复旋转。</summary>
    private PointF[][] _matchContours = Array.Empty<PointF[]>();
    /// <summary>完整模型的有序轮廓，不同路径不能相互连接。</summary>
    private PointF[][] _modelPaths = Array.Empty<PointF[]>();
    /// <summary>按目标姿态变换后的完整路径，仅在结果变化时生成。</summary>
    private PointF[][][] _matchPaths = Array.Empty<PointF[][]>();
    /// <summary>相对适应画布的放大倍数，限制极值以避免浮点溢出。</summary>
    private double _zoom = 1D;
    /// <summary>相对于居中显示的屏幕平移量。</summary>
    private PointF _pan;
    /// <summary>中键导航与左键编辑互斥，导航不写入遮罩。</summary>
    private bool _panning;
    /// <summary>上一帧中键位置，使用客户区坐标。</summary>
    private Point _lastPanPoint;

    /// <summary>初始化双缓冲画布。</summary>
    public ImageCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(32, 35, 40);
        ForeColor = Color.Gainsboro;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    /// <summary>右键请求提交当前编辑。</summary>
    public event EventHandler ConfirmRequested;
    /// <summary>Esc 取消临时修改。</summary>
    public event EventHandler EditCancelled;
    /// <summary>框选或待删除数量改变。</summary>
    public event EventHandler EditChanged;
    /// <summary>缩放、平移或画布尺寸变化后通知显示比例。</summary>
    public event EventHandler ViewChanged;
    /// <summary>每个原图像素对应的屏幕像素数，未加载图像时为零。</summary>
    public double ImageScale => _frame == null ? 0 : ImageBounds().Width / _frame.Width;
    /// <summary>当前交互状态。</summary>
    public CanvasEditorMode EditorMode => _mode;
    /// <summary>正在框选或已确认的 ROI。</summary>
    public Rectangle Roi => _mode == CanvasEditorMode.创建模板 ? _draftRoi : _modelRoi;
    /// <summary>画笔直径，使用原图像素。</summary>
    public int BrushSize { get; set; } = 16;
    /// <summary>待删除的真实特征数量。</summary>
    public int PendingFeatureCount { get; private set; }

    /// <summary>替换显示图像，并清除旧编辑状态。</summary>
    public void SetImage(ImageFrame frame)
    {
        bool changed = !ReferenceEquals(_frame, frame);
        _frame = frame;
        if (changed && RegionEditingEnabled) SetRegions(null);
        _modelRoi = Rectangle.Empty;
        _modelPaths = Array.Empty<PointF[]>();
        _matchPaths = Array.Empty<PointF[][]>();
        _features = Array.Empty<PointF>();
        _matches = Array.Empty<ShapeMatchResult>();
        _matchContours = Array.Empty<PointF[]>();
        EndEdit();
        if (changed) ResetView();
    }

    /// <summary>恢复整图适应，不改变已确认ROI或本次未提交的涂抹。</summary>
    public void ResetView()
    {
        StopPointerGesture();
        _zoom = 1D; _pan = PointF.Empty; _pointer = null;
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>围绕指定屏幕点缩放，使该点下的原图位置保持不动。</summary>
    public void ZoomAt(Point anchor, double factor)
    {
        RectangleF before = ImageBounds();
        if (_frame == null || before.IsEmpty || factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor)) return;
        StopPointerGesture();
        double next = Math.Max(0.1D, Math.Min(64D, _zoom * factor));
        if (Math.Abs(next - _zoom) < 0.000001D) return;
        double x = (anchor.X - before.X) / before.Width, y = (anchor.Y - before.Y) / before.Height;
        _zoom = next;
        RectangleF after = ImageBounds();
        _pan.X += (float)(anchor.X - (after.X + x * after.Width));
        _pan.Y += (float)(anchor.Y - (after.Y + y * after.Height));
        _pointer = anchor;
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>结束笔画或导航，防止变换后把两个不同视口的点连接成删除线。</summary>
    private void StopPointerGesture()
    {
        bool pendingRegion = HasRegionGesture;
        CancelRegionGesture();
        _dragging = false; _panning = false; Capture = false;
        Cursor = _mode == CanvasEditorMode.查看 ? Cursors.Default : Cursors.Cross;
        if (pendingRegion) RegionSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>滚轮以鼠标为中心缩放；不把滚轮传播到外层参数滚动容器。</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        base.OnMouseWheel(e);
        ZoomAt(e.Location, Math.Pow(1.25D, Math.Max(-12D, Math.Min(12D, e.Delta / 120D))));
    }

    /// <summary>设置已提交模型的 ROI 和真实匹配特征。</summary>
    public void SetModel(Rectangle roi, IReadOnlyList<PointF> features, IReadOnlyList<PointF[]> contours = null)
    {
        _modelRoi = roi;
        _modelPaths = contours?.Select(path => path.ToArray()).ToArray() ?? Array.Empty<PointF[]>();
        // 编辑命中采用完整轮廓像素，而不是只允许涂中稀疏搜索点。
        _features = _modelPaths.Length > 0 ? _modelPaths.SelectMany(path => path).Distinct().ToArray() : features;
        EndEdit();
    }

    /// <summary>进入重新框选流程；旧模型保留，直到右键成功提交。</summary>
    public void BeginCreate()
    {
        _draftRoi = Rectangle.Empty;
        _matches = Array.Empty<ShapeMatchResult>();
        _matchContours = Array.Empty<PointF[]>();
        ChangeMode(CanvasEditorMode.创建模板);
    }

    /// <summary>进入删除流程，每次使用独立的临时遮罩。</summary>
    public void BeginErase()
    {
        if (_modelRoi.IsEmpty)
            return;
        _eraseMask = new byte[checked(_modelRoi.Width * _modelRoi.Height)];
        _pendingFeatures = new bool[_features.Count];
        PendingFeatureCount = 0;
        _matches = Array.Empty<ShapeMatchResult>();
        _matchContours = Array.Empty<PointF[]>();
        ChangeMode(CanvasEditorMode.涂抹);
    }

    /// <summary>返回独立的删除遮罩快照，供后台提交。</summary>
    public byte[] CopyEraseMask() => _eraseMask?.ToArray() ?? Array.Empty<byte>();

    /// <summary>结束编辑，清理临时状态而保留已提交模型。</summary>
    public void EndEdit()
    {
        _eraseMask = null;
        _pendingFeatures = Array.Empty<bool>();
        PendingFeatureCount = 0;
        _draftRoi = Rectangle.Empty;
        ChangeMode(CanvasEditorMode.查看);
    }

    /// <summary>设置匹配结果，并把当前模型有效特征转换成每个目标的轮廓。</summary>
    public void SetMatches(
        IReadOnlyList<ShapeMatchResult> matches,
        IReadOnlyList<PointF> modelFeatures = null, IReadOnlyList<PointF[]> modelContours = null)
    {
        if (matches == null) throw new ArgumentNullException(nameof(matches));
        _matches = matches.ToArray();
        _matchContours = new PointF[_matches.Count][];
        _matchPaths = new PointF[_matches.Count][][];
        for (int index = 0; index < _matches.Count; index++)
        {
            ShapeMatchResult match = _matches[index];
            PointF[] contour = new PointF[modelFeatures?.Count ?? 0];
            double radians = match.AngleDegrees * Math.PI / 180D;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            for (int pointIndex = 0; pointIndex < contour.Length; pointIndex++)
            {
                // DLL 特征相对 ROI 左上角；先移到模板中心，再按产品角度旋转和平移。
                // 使用模型尺寸而非来源 ROI 的图像位置，换搜索图后仍能正确叠加。
                PointF feature = modelFeatures[pointIndex];
                double x = feature.X - match.Width / 2D;
                double y = feature.Y - match.Height / 2D;
                contour[pointIndex] = new PointF(
                    (float)(match.CenterX + cosine * x - sine * y),
                    (float)(match.CenterY + sine * x + cosine * y));
            }
            _matchContours[index] = contour;
            _matchPaths[index] = (match.ModelContours ?? modelContours)?.Select(path => path.Select(point => new PointF(
                (float)(match.CenterX + cosine * (point.X - match.Width / 2D) - sine * (point.Y - match.Height / 2D)),
                (float)(match.CenterY + sine * (point.X - match.Width / 2D) + cosine * (point.Y - match.Height / 2D))))
                .ToArray()).ToArray() ?? Array.Empty<PointF[]>();
        }
        Invalidate();
    }

    /// <summary>切换模式并更新鼠标反馈。</summary>
    private void ChangeMode(CanvasEditorMode mode)
    {
        _mode = mode;
        StopPointerGesture();
        Focus();
        Invalidate();
    }

    /// <summary>左键开始编辑，右键只触发当前操作的确认。</summary>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_frame == null)
            return;
        if (e.Button == MouseButtons.Middle)
        {
            StopPointerGesture();
            _panning = true; _lastPanPoint = e.Location; Capture = true; Cursor = Cursors.SizeAll;
            return;
        }
        if (RegionEditingEnabled && _mode == CanvasEditorMode.查看 && !_panning) { RegionMouseDown(e); return; }
        if (_panning || _mode == CanvasEditorMode.查看) return;
        if (e.Button == MouseButtons.Right)
        {
            _dragging = false;
            Capture = false;
            ConfirmRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (e.Button != MouseButtons.Left || !TryImagePoint(e.Location, false, out Point point))
            return;
        _dragging = true;
        Capture = true;
        if (_mode == CanvasEditorMode.创建模板)
        {
            _origin = point;
            _draftRoi = Rectangle.Empty;
        }
        else
        {
            _lastBrushPoint = point;
            PaintLine(point, point);
        }
        Invalidate();
    }

    /// <summary>连续框选或涂抹；拖到图像外时将位置限制在边界。</summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _pointer = e.Location;
        if (_panning)
        {
            _pan.X += e.X - _lastPanPoint.X; _pan.Y += e.Y - _lastPanPoint.Y;
            _lastPanPoint = e.Location;
            // 保留少量可见边缘，避免将整幅图拖出画布后无法找到。
            RectangleF bounds = ImageBounds();
            float visibleX = Math.Min(32F, bounds.Width / 2), visibleY = Math.Min(32F, bounds.Height / 2);
            _pan.X += Math.Max(visibleX - bounds.Width, Math.Min(ClientSize.Width - visibleX, bounds.X)) - bounds.X;
            _pan.Y += Math.Max(visibleY - bounds.Height, Math.Min(ClientSize.Height - visibleY, bounds.Y)) - bounds.Y;
            ViewChanged?.Invoke(this, EventArgs.Empty); Invalidate(); return;
        }
        if (_dragging && TryImagePoint(e.Location, true, out Point point))
            UpdateDrag(point);
        if (RegionEditingEnabled && _mode == CanvasEditorMode.查看) RegionMouseMove(e);
        if (_mode == CanvasEditorMode.涂抹)
            Invalidate();
    }

    /// <summary>松开左键只结束笔画，不提交模型。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Middle && _panning) { StopPointerGesture(); Invalidate(); return; }
        if (RegionEditingEnabled && _mode == CanvasEditorMode.查看) { RegionMouseUp(e); return; }
        if (e.Button != MouseButtons.Left || !_dragging)
            return;
        if (TryImagePoint(e.Location, true, out Point point))
            UpdateDrag(point);
        _dragging = false;
        Capture = false;
    }

    /// <summary>丢失鼠标捕获后停止笔画，避免重新进入窗口时误涂。</summary>
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            if (_regionPointerDown) { CancelRegionGesture(); RegionSelectionChanged?.Invoke(this, EventArgs.Empty); }
            _dragging = false; _panning = false;
            Cursor = _mode == CanvasEditorMode.查看 ? Cursors.Default : Cursors.Cross;
        }
    }

    /// <summary>鼠标离开后隐藏画笔圆圈。</summary>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pointer = null;
        Invalidate();
    }

    /// <summary>画布尺寸变化立即重绘；ROI 和模型特征始终保存在原图坐标，不累积缩放误差。</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        StopPointerGesture();
        _pointer = null;
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Esc 撤销当前框选或尚未提交的涂抹。</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (ProcessRegionKey(keyData)) return true;
        if (keyData == Keys.Escape && _mode != CanvasEditorMode.查看)
        {
            EndEdit();
            EditCancelled?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>更新一段拖动；ROI 使用包含终点像素的坐标。</summary>
    private void UpdateDrag(Point point)
    {
        if (_mode == CanvasEditorMode.创建模板)
        {
            _draftRoi = Rectangle.FromLTRB(
                Math.Min(_origin.X, point.X), Math.Min(_origin.Y, point.Y),
                Math.Max(_origin.X, point.X) + 1, Math.Max(_origin.Y, point.Y) + 1);
        }
        else if (_mode == CanvasEditorMode.涂抹)
        {
            PaintLine(_lastBrushPoint, point);
            _lastBrushPoint = point;
        }
        EditChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>插值圆形笔画，标记删除区；原图像素始终不变。</summary>
    private void PaintLine(Point first, Point second)
    {
        if (_eraseMask == null)
            return;
        double dx = second.X - first.X;
        double dy = second.Y - first.Y;
        int steps = Math.Max(1, (int)Math.Ceiling(
            Math.Sqrt(dx * dx + dy * dy) / Math.Max(1D, BrushSize / 4D)));
        double radius = Math.Max(0.5D, BrushSize / 2D);
        for (int step = 0; step <= steps; step++)
        {
            double cx = first.X + dx * step / steps - _modelRoi.X;
            double cy = first.Y + dy * step / steps - _modelRoi.Y;
            int left = Math.Max(0, (int)Math.Floor(cx - radius));
            int right = Math.Min(_modelRoi.Width - 1, (int)Math.Ceiling(cx + radius));
            int top = Math.Max(0, (int)Math.Floor(cy - radius));
            int bottom = Math.Min(_modelRoi.Height - 1, (int)Math.Ceiling(cy + radius));
            for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    _eraseMask[y * _modelRoi.Width + x] = 255;
        }
        for (int i = 0; i < _features.Count; i++)
        {
            PointF point = _features[i];
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);
            if (!_pendingFeatures[i] && x >= 0 && x < _modelRoi.Width
                && y >= 0 && y < _modelRoi.Height && _eraseMask[y * _modelRoi.Width + x] != 0)
            {
                _pendingFeatures[i] = true;
                PendingFeatureCount++;
            }
        }
        EditChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>绘制图像、ROI、实际特征以及匹配结果。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        if (_frame == null)
        {
            TextRenderer.DrawText(g, "加载搜索图像后，点击“创建模板”", Font, ClientRectangle,
                ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        RectangleF bounds = ImageBounds();
        if (bounds.IsEmpty)
            return;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_frame.DisplayBitmap, bounds);
        if (!RegionEditingEnabled && !Roi.IsEmpty && _matches.Count == 0)
        {
            using (Pen roiPen = new Pen(Color.Gold, 1.5F) { DashStyle = DashStyle.Dash })
            g.DrawRectangle(roiPen, Rectangle.Round(RectangleF.FromLTRB(
                ClientPoint(Roi.Left, Roi.Top).X, ClientPoint(Roi.Left, Roi.Top).Y,
                ClientPoint(Roi.Right, Roi.Bottom).X, ClientPoint(Roi.Right, Roi.Bottom).Y)));
        }
        if (_mode != CanvasEditorMode.创建模板 && !_modelRoi.IsEmpty && _matches.Count == 0)
        {
            DrawContourPaths(g, _modelPaths, _modelRoi.Location);
            for (int i = 0; i < _features.Count; i++)
            {
                PointF p = ClientPoint(_modelRoi.X + _features[i].X, _modelRoi.Y + _features[i].Y);
                bool pending = _mode == CanvasEditorMode.涂抹 && _pendingFeatures[i];
                if (_modelPaths.Length > 0 && !pending)
                    continue;
                g.FillEllipse(pending ? Brushes.OrangeRed : Brushes.LimeGreen,
                    p.X - (pending ? 1F : 2F), p.Y - (pending ? 1F : 2F), pending ? 2F : 4F, pending ? 2F : 4F);
            }
        }
        DrawMatches(g);
        DrawTemplateRegions(g);
        if (_mode == CanvasEditorMode.涂抹 && !_panning && _pointer is Point pointer)
        {
            float diameter = BrushSize * ImageBounds().Width / _frame.Width;
            using (Pen brushPen = new Pen(Color.OrangeRed))
            g.DrawEllipse(brushPen, pointer.X - diameter / 2, pointer.Y - diameter / 2, diameter, diameter);
        }
    }

    /// <summary>绘制每个匹配目标的实际模板轮廓、旋转框、中心和分数。</summary>
    private void DrawMatches(Graphics g)
    {
        if (_frame == null)
            return;
        RectangleF imageBounds = ImageBounds();
        float scaleX = imageBounds.Width / _frame.Width;
        float scaleY = imageBounds.Height / _frame.Height;
        for (int matchIndex = 0; matchIndex < _matches.Count; matchIndex++)
        {
            ShapeMatchResult match = _matches[matchIndex];
            double angle = match.AngleDegrees * Math.PI / 180D;
            double cosine = Math.Cos(angle);
            double sine = Math.Sin(angle);
            // 产品角度在图像坐标系中顺时针为正，与原生返回约定一致。
            PointF[] corners = new PointF[4];
            double[] xs = new double[] {-match.Width / 2, match.Width / 2, match.Width / 2, -match.Width / 2};
            double[] ys = new double[] {-match.Height / 2, -match.Height / 2, match.Height / 2, match.Height / 2};
            for (int i = 0; i < corners.Length; i++)
                corners[i] = ClientPoint(match.CenterX + cosine * xs[i] - sine * ys[i],
                    match.CenterY + sine * xs[i] + cosine * ys[i]);
            using (Pen pen = new Pen(Color.LimeGreen, 2))
            {
            g.DrawPolygon(pen, corners);
            DrawContourPaths(g, _matchPaths[matchIndex], PointF.Empty);
            // 仅旧的特征点调用使用离散回退；真实Demo传入完整有序路径，以细线展示文字轮廓。
            foreach (PointF feature in _matchPaths[matchIndex].Length == 0 ? _matchContours[matchIndex] : Array.Empty<PointF>())
            {
                float x = imageBounds.X + feature.X * scaleX;
                float y = imageBounds.Y + feature.Y * scaleY;
                g.FillEllipse(Brushes.Lime, x - 2F, y - 2F, 4F, 4F);
            }
            PointF center = ClientPoint(match.CenterX, match.CenterY);
            g.DrawLine(pen, center.X - 6, center.Y, center.X + 6, center.Y);
            g.DrawLine(pen, center.X, center.Y - 6, center.X, center.Y + 6);
            g.DrawString($"{match.Score:F3} / {match.AngleDegrees:F2}°", Font,
                Brushes.Lime, center.X + 8, center.Y + 8);
            }
        }
    }

    /// <summary>按独立有序路径绘制1屏幕像素细轮廓，避免大圆点遮挡细节及跨轮廓假连线。</summary>
    private void DrawContourPaths(Graphics graphics, IReadOnlyList<PointF[]> paths, PointF offset)
    {
        if (_frame == null || paths.Count == 0)
            return;
        RectangleF bounds = ImageBounds();
        if (bounds.IsEmpty)
            return;
        float scale = bounds.Width / _frame.Width;
        GraphicsState saved = graphics.Save();
        try
        {
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(offset.X, offset.Y);
            using (Pen pen = new Pen(Color.LimeGreen, 1F / scale))
            foreach (PointF[] path in paths)
            {
                if (path.Length >= 2)
                    graphics.DrawLines(pen, path);
                else if (path.Length == 1)
                    graphics.FillRectangle(Brushes.LimeGreen, path[0].X, path[0].Y, 1F / scale, 1F / scale);
            }
        }
        finally
        {
            graphics.Restore(saved);
        }
    }

    /// <summary>计算等比例图像显示范围。</summary>
    private RectangleF ImageBounds()
    {
        if (_frame == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return RectangleF.Empty;
        float scale = (float)(Math.Min(ClientSize.Width / (double)_frame.Width,
            ClientSize.Height / (double)_frame.Height) * _zoom);
        float width = _frame.Width * scale;
        float height = _frame.Height * scale;
        return new RectangleF((ClientSize.Width - width) / 2 + _pan.X, (ClientSize.Height - height) / 2 + _pan.Y, width, height);
    }

    /// <summary>由原图坐标转换为画布坐标。</summary>
    private PointF ClientPoint(double x, double y)
    {
        if (_frame == null)
            return PointF.Empty;
        RectangleF bounds = ImageBounds();
        return new PointF(bounds.X + (float)(x * bounds.Width / _frame.Width),
            bounds.Y + (float)(y * bounds.Height / _frame.Height));
    }

    /// <summary>转换鼠标坐标；仅在拖动期间允许裁剪越界点。</summary>
    private bool TryImagePoint(Point point, bool clamp, out Point pixel)
    {
        pixel = Point.Empty;
        RectangleF bounds = ImageBounds();
        if (_frame == null || bounds.IsEmpty || (!clamp && !bounds.Contains(point)))
            return false;
        pixel = new Point(ContourCompatibility.Clamp((int)((point.X - bounds.X) * _frame.Width / bounds.Width), 0, _frame.Width - 1),
            ContourCompatibility.Clamp((int)((point.Y - bounds.Y) * _frame.Height / bounds.Height), 0, _frame.Height - 1));
        return true;
    }
}

}
