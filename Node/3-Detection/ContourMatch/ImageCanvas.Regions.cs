using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>模板区域交互扩展；按需启用，不改变搜索框与原有轮廓擦除行为。</summary>
    public sealed partial class ImageCanvas
    {
        /// <summary>当前区域集合，只在UI线程编辑。</summary>
        private readonly List<TemplateRegion> _regions = new List<TemplateRegion>();
        /// <summary>最多保存四十次区域操作。</summary>
        private readonly List<List<TemplateRegion>> _regionUndo = new List<List<TemplateRegion>>();
        /// <summary>撤销后可重做的区域操作。</summary>
        private readonly List<List<TemplateRegion>> _regionRedo = new List<List<TemplateRegion>>();
        /// <summary>当前选择的区域索引。</summary>
        private int _selectedRegion = -1;
        /// <summary>当前绘制工具，完成一次绘制后返回选择。</summary>
        private TemplateRegionKind _regionTool;
        /// <summary>本次拖动的起始原图位置。</summary>
        private PointF _regionOrigin;
        /// <summary>正在拖动绘制或调整控制柄。</summary>
        private bool _regionPointerDown;
        /// <summary>当前控制柄：0至7缩放，8旋转，9移动。</summary>
        private int _regionHandle = -1;
        /// <summary>变换开始前的区域，用于无累积误差的拖动和取消。</summary>
        private TemplateRegion _regionBefore;
        /// <summary>当前待完成的几何区域。</summary>
        private TemplateRegion _regionDraft;
        /// <summary>多边形顶点或画笔采样点，始终保存原图坐标。</summary>
        private readonly List<PointF> _regionPoints = new List<PointF>();
        /// <summary>多边形当前悬停位置。</summary>
        private PointF _regionHover;
        /// <summary>缩放柄在局部外接框上的归一化位置。</summary>
        private static readonly PointF[] RegionHandles = { new PointF(-.5F,-.5F), new PointF(0,-.5F),
            new PointF(.5F,-.5F), new PointF(.5F,0), new PointF(.5F,.5F), new PointF(0,.5F), new PointF(-.5F,.5F), new PointF(-.5F,0) };

        /// <summary>模板编辑器启用独立区域交互，搜索画布保持原有操作。</summary>
        public bool RegionEditingEnabled { get; set; }
        /// <summary>当前区域绘制工具。</summary>
        public TemplateRegionKind RegionTool => _regionTool;
        /// <summary>选择索引，空白处单击可取消选择。</summary>
        public int SelectedRegionIndex => _selectedRegion;
        /// <summary>区域数量。</summary>
        public int RegionCount => _regions.Count;
        /// <summary>当前选区的独立副本。</summary>
        public TemplateRegion SelectedRegion => _selectedRegion >= 0 ? _regions[_selectedRegion].Copy() : null;
        /// <summary>是否有尚未完成的绘制或变换。</summary>
        public bool HasRegionGesture => _regionPointerDown || _regionPoints.Count > 0;
        /// <summary>是否可以撤销。</summary>
        public bool CanUndoRegion => _regionUndo.Count > 0;
        /// <summary>是否可以重做。</summary>
        public bool CanRedoRegion => _regionRedo.Count > 0;
        /// <summary>区域增删、变换或启停提交后触发；不在每次鼠标移动时重建模板。</summary>
        public event EventHandler RegionsChanged;
        /// <summary>选择、工具或控制柄预览改变。</summary>
        public event EventHandler RegionSelectionChanged;

        /// <summary>取得用于后台建模与持久化的深复制快照。</summary>
        public List<TemplateRegion> CopyRegions() => _regions.Select(region => region.Copy()).ToList();

        /// <summary>载入区域并清空历史，旧模板可由调用方转换为矩形。</summary>
        public void SetRegions(IEnumerable<TemplateRegion> regions)
        {
            var snapshot = regions?.Select(region => region.Copy()).ToList();
            if (snapshot != null) foreach (var region in snapshot) region.Validate();
            CancelRegionGesture();
            _regions.Clear();
            if (snapshot != null) _regions.AddRange(snapshot);
            _regionUndo.Clear(); _regionRedo.Clear(); _regionTool = TemplateRegionKind.选择;
            SelectRegion(_regions.Count > 0 ? 0 : -1);
        }

        /// <summary>显式选择下一次绘制工具，保留所有已经完成的区域。</summary>
        public void SetRegionTool(TemplateRegionKind tool)
        {
            if (!RegionEditingEnabled) return;
            CancelRegionGesture();
            EndEdit(); _regionTool = tool;
            Cursor = tool == TemplateRegionKind.选择 ? Cursors.Default : Cursors.Cross;
            RegionSelectionChanged?.Invoke(this, EventArgs.Empty);
            Focus(); Invalidate();
        }

        /// <summary>列表与画布共用选择入口。</summary>
        public void SelectRegion(int index)
        {
            _selectedRegion = index >= 0 && index < _regions.Count ? index : -1;
            RegionSelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }

        /// <summary>更新选中区域参数，记录一次可撤销操作。</summary>
        public void UpdateSelectedRegion(TemplateRegion region)
        {
            if (_selectedRegion < 0 || region == null) return;
            region.Validate(); RememberRegions(); _regions[_selectedRegion] = region.Copy(); PublishRegions();
        }

        /// <summary>删除当前选区，不影响其余区域。</summary>
        public void DeleteSelectedRegion()
        {
            if (_selectedRegion < 0) return;
            CancelRegionGesture(); RememberRegions(); _regions.RemoveAt(_selectedRegion);
            _selectedRegion = Math.Min(_selectedRegion, _regions.Count - 1); PublishRegions();
        }

        /// <summary>清空区域，不隐式转换为全图模板。</summary>
        public void ClearRegions()
        {
            CancelRegionGesture(); if (_regions.Count == 0) return;
            RememberRegions(); _regions.Clear(); _selectedRegion = -1; PublishRegions();
        }

        /// <summary>撤销或重做区域操作，不撤销已经提交的轮廓擦除。</summary>
        public void UndoRegion(bool redo)
        {
            CancelRegionGesture();
            var source = redo ? _regionRedo : _regionUndo;
            var target = redo ? _regionUndo : _regionRedo;
            if (source.Count == 0) return;
            target.Add(CopyRegions());
            var snapshot = source[source.Count - 1]; source.RemoveAt(source.Count - 1);
            _regions.Clear(); _regions.AddRange(snapshot); _selectedRegion = _regions.Count - 1;
            PublishRegions();
        }

        /// <summary>保存操作前的集合；仅在提交时复制，拖动过程中不复制整个集合。</summary>
        private void RememberRegions()
        {
            _regionUndo.Add(CopyRegions()); if (_regionUndo.Count > 40) _regionUndo.RemoveAt(0);
            _regionRedo.Clear();
        }

        /// <summary>清除过期模型显示并通知编辑器，避免把旧轮廓当作新区域结果。</summary>
        private void PublishRegions()
        {
            ClearModelPreview();
            RegionsChanged?.Invoke(this, EventArgs.Empty);
            RegionSelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }

        /// <summary>清空预览而保留区域与视口。</summary>
        public void ClearModelPreview()
        {
            _modelRoi = Rectangle.Empty; _features = Array.Empty<PointF>(); _modelPaths = Array.Empty<PointF[]>(); Invalidate();
        }

        /// <summary>撤销尚未松开的变换或尚未闭合的绘制。</summary>
        private void CancelRegionGesture()
        {
            if (_regionBefore != null && _selectedRegion >= 0) _regions[_selectedRegion] = _regionBefore;
            _regionPointerDown = false; _regionBefore = null; _regionDraft = null; _regionPoints.Clear();
            _regionHandle = -1;
            Invalidate();
        }

        /// <summary>获取区域控制柄的屏幕位置，旋转柄与边界保持固定像素间距。</summary>
        private PointF RegionHandlePoint(TemplateRegion region, int handle)
        {
            PointF p = handle == 8 ? region.Transform(0, -region.Height / 2 - (float)(24 / ImageScale))
                : region.Transform(RegionHandles[handle].X * region.Width, RegionHandles[handle].Y * region.Height);
            return ClientPoint(p.X, p.Y);
        }

        /// <summary>优先命中控制柄，再按绘制层次选择最上方区域。</summary>
        private int HitRegionHandle(Point point)
        {
            if (_selectedRegion < 0) return -1;
            for (int i = 8; i >= 0; i--)
            {
                PointF handle = RegionHandlePoint(_regions[_selectedRegion], i);
                if (Math.Abs(handle.X - point.X) <= 7 && Math.Abs(handle.Y - point.Y) <= 7) return i;
            }
            return -1;
        }

        /// <summary>区域操作鼠标按下，右键仅闭合多边形，不隐式执行模型创建。</summary>
        private void RegionMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (_regionTool == TemplateRegionKind.多边形) FinishPolygon();
                else SetRegionTool(TemplateRegionKind.选择);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            int handle = _regionTool == TemplateRegionKind.选择 ? HitRegionHandle(e.Location) : -1;
            if (!TryImagePoint(e.Location, handle >= 0, out Point pixel)) return;
            PointF point = pixel;
            _regionOrigin = point; _regionHover = point;
            if (_regionTool == TemplateRegionKind.多边形)
            {
                if (_regionPoints.Count == 0 || Distance(_regionPoints.Last(), point) > 1) _regionPoints.Add(point);
                if (e.Clicks > 1) FinishPolygon();
                RegionSelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate(); return;
            }
            if (_regionTool == TemplateRegionKind.选择)
            {
                if (handle < 0)
                {
                    int hit = -1;
                    for (int i = _regions.Count - 1; i >= 0; i--)
                    using (var path = _regions[i].CreatePath())
                    using (var pen = new Pen(Color.White, (float)(8 / ImageScale)))
                        if (path.IsVisible(point) || path.IsOutlineVisible(point, pen)) { hit = i; break; }
                    SelectRegion(hit); handle = hit < 0 ? -1 : 9;
                }
                if (handle < 0) return;
                _regionHandle = handle; _regionBefore = _regions[_selectedRegion].Copy();
            }
            else if (_regionTool == TemplateRegionKind.画笔) _regionPoints.Add(point);
            _regionPointerDown = true; Capture = true;
            RegionSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>两点距离，用于限制画笔采样量。</summary>
        private static double Distance(PointF a, PointF b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));

        /// <summary>更新绘制或变换预览；松开前不触发算法。</summary>
        private void RegionMouseMove(MouseEventArgs e)
        {
            if (!TryImagePoint(e.Location, true, out Point pixel)) return;
            PointF point = pixel; _regionHover = point;
            if (!_regionPointerDown)
            {
                if (_regionTool == TemplateRegionKind.选择)
                {
                    int handle = HitRegionHandle(e.Location);
                    Cursor = handle == 8 ? Cursors.Hand : handle >= 0 ? Cursors.SizeAll : Cursors.Default;
                }
                if (_regionPoints.Count > 0) Invalidate();
                return;
            }
            if (_regionBefore != null)
            {
                var region = _regionBefore.Copy();
                if (_regionHandle == 9)
                {
                    region.CenterX += point.X - _regionOrigin.X; region.CenterY += point.Y - _regionOrigin.Y;
                }
                else if (_regionHandle == 8)
                {
                    double first = Math.Atan2(_regionOrigin.Y - region.CenterY, _regionOrigin.X - region.CenterX);
                    double next = Math.Atan2(point.Y - region.CenterY, point.X - region.CenterX);
                    region.Angle = NormalizeAngle(region.Angle + (float)((next - first) * 180 / Math.PI));
                }
                else
                {
                    // 对角点固定在原位，在旋转前的局部坐标中缩放，再变回原图。
                    PointF axis = RegionHandles[_regionHandle]; PointF local = region.Inverse(point);
                    float anchorX = -axis.X * region.Width, anchorY = -axis.Y * region.Height;
                    float centerX = 0, centerY = 0;
                    if (axis.X != 0)
                    {
                        float moving = axis.X < 0 ? Math.Min(local.X, anchorX - 2) : Math.Max(local.X, anchorX + 2);
                        region.Width = Math.Abs(moving - anchorX); centerX = (moving + anchorX) / 2;
                    }
                    if (axis.Y != 0)
                    {
                        float moving = axis.Y < 0 ? Math.Min(local.Y, anchorY - 2) : Math.Max(local.Y, anchorY + 2);
                        region.Height = Math.Abs(moving - anchorY); centerY = (moving + anchorY) / 2;
                    }
                    PointF center = _regionBefore.Transform(centerX, centerY); region.CenterX = center.X; region.CenterY = center.Y;
                }
                _regions[_selectedRegion] = region;
                RegionSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_regionTool == TemplateRegionKind.画笔)
            {
                if (_regionPoints.Count < 20000 && Distance(_regionPoints.Last(), point) >= Math.Max(1, BrushSize / 5F)) _regionPoints.Add(point);
                _regionDraft = RegionFromPoints(TemplateRegionKind.画笔, _regionPoints);
            }
            else
            {
                _regionDraft = new TemplateRegion { Kind = _regionTool, CenterX = (_regionOrigin.X + point.X) / 2,
                    CenterY = (_regionOrigin.Y + point.Y) / 2, Width = Math.Max(1, Math.Abs(point.X - _regionOrigin.X)),
                    Height = Math.Max(1, Math.Abs(point.Y - _regionOrigin.Y)) };
            }
            Invalidate();
        }

        /// <summary>归一化角度，防止多次旋转后输入控件越界。</summary>
        private static float NormalizeAngle(float angle) => (angle % 360 + 540) % 360 - 180;

        /// <summary>松开立即保存一个区域或变换，自动回到选择工具。</summary>
        private void RegionMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_regionPointerDown) return;
            RegionMouseMove(e);
            _regionPointerDown = false;
            if (_regionBefore != null)
            {
                var after = _regions[_selectedRegion];
                bool changed = after.CenterX != _regionBefore.CenterX || after.CenterY != _regionBefore.CenterY ||
                    after.Width != _regionBefore.Width || after.Height != _regionBefore.Height || after.Angle != _regionBefore.Angle;
                if (changed)
                {
                    _regions[_selectedRegion] = _regionBefore; RememberRegions(); _regions[_selectedRegion] = after;
                }
                _regionBefore = null; _regionHandle = -1; Capture = false;
                if (changed) PublishRegions();
                else RegionSelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_regionDraft != null && (_regionTool == TemplateRegionKind.画笔 || (_regionDraft.Width >= 2 && _regionDraft.Height >= 2)))
                AddFinishedRegion(_regionDraft);
            else { CancelRegionGesture(); RegionSelectionChanged?.Invoke(this, EventArgs.Empty); }
            Capture = false;
        }

        /// <summary>把自由顶点归一化到区域外接框，画笔宽度保存在形状数据中。</summary>
        private TemplateRegion RegionFromPoints(TemplateRegionKind kind, IReadOnlyList<PointF> points)
        {
            float padding = kind == TemplateRegionKind.画笔 ? BrushSize / 2F : 0;
            float left = points.Min(p => p.X) - padding, top = points.Min(p => p.Y) - padding;
            float width = Math.Max(2, points.Max(p => p.X) + padding - left);
            float height = Math.Max(2, points.Max(p => p.Y) + padding - top);
            return new TemplateRegion { Kind = kind, CenterX = left + width / 2, CenterY = top + height / 2, Width = width, Height = height,
                StrokeRatio = kind == TemplateRegionKind.画笔 ? BrushSize / Math.Min(width, height) : 0,
                Points = points.Select(p => new PointF((p.X - left) / width - .5F, (p.Y - top) / height - .5F)).ToList() };
        }

        /// <summary>右键、双击或回车闭合至少三个顶点的多边形。</summary>
        private void FinishPolygon()
        {
            if (_regionPoints.Count < 3) return;
            var region = RegionFromPoints(TemplateRegionKind.多边形, _regionPoints);
            double area = 0;
            for (int i = 0; i < _regionPoints.Count; i++)
            {
                var a = _regionPoints[i]; var b = _regionPoints[(i + 1) % _regionPoints.Count]; area += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(area) < 4) return;
            AddFinishedRegion(region);
        }

        /// <summary>追加区域并切回选择，不覆盖已有区域。</summary>
        private void AddFinishedRegion(TemplateRegion region)
        {
            RememberRegions(); region.Name = region.Kind + " " + (_regions.Count + 1);
            _regions.Add(region); _selectedRegion = _regions.Count - 1;
            _regionDraft = null; _regionPoints.Clear(); _regionTool = TemplateRegionKind.选择;
            Cursor = Cursors.Default; PublishRegions();
        }

        /// <summary>区域键盘命令，Esc仅取消当前手势，Delete删除选区。</summary>
        private bool ProcessRegionKey(Keys keyData)
        {
            if (!RegionEditingEnabled || _mode != CanvasEditorMode.查看) return false;
            if (keyData == Keys.Escape) { SetRegionTool(TemplateRegionKind.选择); return true; }
            if (keyData == Keys.Enter && _regionTool == TemplateRegionKind.多边形) { FinishPolygon(); return true; }
            if (keyData == Keys.Delete) { DeleteSelectedRegion(); return true; }
            if (keyData == (Keys.Control | Keys.Z)) { UndoRegion(false); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { UndoRegion(true); return true; }
            return false;
        }

        /// <summary>绘制半透明区域、固定屏幕尺寸控制柄与旋转手柄。</summary>
        private void DrawTemplateRegions(Graphics graphics)
        {
            if (!RegionEditingEnabled || _frame == null || _mode == CanvasEditorMode.涂抹) return;
            for (int i = 0; i < _regions.Count; i++) DrawRegionPath(graphics, _regions[i], i == _selectedRegion);
            if (_regionDraft != null) DrawRegionPath(graphics, _regionDraft, false);
            if (_regionTool == TemplateRegionKind.多边形 && _regionPoints.Count > 0)
            {
                var points = _regionPoints.Concat(new[] { _regionHover }).Select(p => ClientPoint(p.X, p.Y)).ToArray();
                using (var pen = new Pen(Color.Orange, 1.5F)) graphics.DrawLines(pen, points);
            }
            if (_selectedRegion < 0) return;
            var selected = _regions[_selectedRegion];
            var corners = new[] { 0, 2, 4, 6 }.Select(i => RegionHandlePoint(selected, i)).ToArray();
            using (var pen = new Pen(Color.Orange, 1) { DashStyle = DashStyle.Dash })
            {
                graphics.DrawPolygon(pen, corners);
                graphics.DrawLine(pen, RegionHandlePoint(selected, 1), RegionHandlePoint(selected, 8));
            }
            for (int i = 0; i < 9; i++)
            {
                PointF p = RegionHandlePoint(selected, i);
                if (i == 8) graphics.FillEllipse(Brushes.Orange, p.X - 5, p.Y - 5, 10, 10);
                else graphics.FillRectangle(Brushes.Orange, p.X - 3, p.Y - 3, 6, 6);
            }
            PointF center = ClientPoint(selected.CenterX, selected.CenterY);
            using (var pen = new Pen(Color.LimeGreen, 1.5F))
            { graphics.DrawLine(pen, center.X - 6, center.Y, center.X + 6, center.Y); graphics.DrawLine(pen, center.X, center.Y - 6, center.X, center.Y + 6); }
        }

        /// <summary>绘制单个区域，轮廓可见时减小遮罩透明度。</summary>
        private void DrawRegionPath(Graphics graphics, TemplateRegion region, bool selected)
        {
            using (var path = region.CreatePath())
            using (var transform = new Matrix((float)ImageScale, 0, 0, (float)ImageScale, ImageBounds().X, ImageBounds().Y))
            {
                path.Transform(transform);
                Color color = region.Enabled ? Color.DodgerBlue : Color.Gray;
                using (var fill = new SolidBrush(Color.FromArgb(_features.Count > 0 ? 12 : selected ? 48 : 24, color))) graphics.FillPath(fill, path);
                using (var pen = new Pen(selected ? Color.Orange : color, selected ? 1.8F : 1.2F)) graphics.DrawPath(pen, path);
            }
        }
    }
}
