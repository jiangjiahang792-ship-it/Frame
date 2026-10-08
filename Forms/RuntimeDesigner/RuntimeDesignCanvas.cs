using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>负责组件拖入、选择、移动和缩放的双缓冲画布。</summary>
    public sealed partial class RuntimeDesignCanvas : Control
    {
        /// <summary>工具箱拖放格式。</summary>
        public const string DragFormat = "TDJS.RuntimeWidget";
        /// <summary>页面周围留白。</summary>
        private const int MarginSize = 44;
        /// <summary>独立运行页面的编辑态标题栏高度，不计入文档坐标。</summary>
        private const int PageHeaderHeight = 28;
        /// <summary>页面、选中控件和交互浮标共用的橙色。</summary>
        private static readonly Color SelectionColor = Color.FromArgb(255, 128, 0);
        /// <summary>有界绘图表面缓存，防止拖动重复缩放全部图片。</summary>
        private readonly RuntimeWidgetSurfaceCache surfaces = new RuntimeWidgetSurfaceCache();
        /// <summary>当前可用工作区，决定页面居中与滚动范围。</summary>
        private Size viewportSize;
        /// <summary>最近一次智能对齐的临时反馈。</summary>
        private RuntimeAlignmentResult alignment;
        /// <summary>从工具箱拖入时尚未提交的控件。</summary>
        private RuntimeLayoutElement dropElement;
        /// <summary>越过拖动阈值后生成的副本预览，释放前不写入文档。</summary>
        private List<RuntimeLayoutElement> copyElements;
        /// <summary>当前选择，顺序最后一项为主选控件。</summary>
        private readonly List<string> selectedIds = new List<string>();
        /// <summary>按下时的选择，取消交互时恢复。</summary>
        private string[] selectionBefore;
        /// <summary>本次移动的独立快照，不在鼠标移动期间创建文档事务。</summary>
        private List<RuntimeLayoutElement> pointerElements;
        /// <summary>按控件编号保存原始位置，避免绘制期间反复扫描组成员。</summary>
        private Dictionary<string, Rectangle> pointerBounds;
        /// <summary>是否从页面空白处框选。</summary>
        private bool marquee;
        /// <summary>当前框选的文档坐标矩形。</summary>
        private Rectangle marqueeBounds;
        /// <summary>按下时锁定Ctrl意图：单击切换选择，真正拖动才复制。</summary>
        private bool controlGesture;
        /// <summary>本次交互是否已越过系统拖动阈值。</summary>
        private bool pointerMoved;
        /// <summary>按下命中的控件，供单击完成时切换选择。</summary>
        private string pointerHitId;
        /// <summary>可替换的组件剪贴板，测试不读写用户系统数据。</summary>
        private readonly IRuntimeWidgetClipboard clipboard;
        /// <summary>最近一次复制或粘贴的内容，用于重复粘贴偏移。</summary>
        private string lastPastePayload;
        /// <summary>相同快照已连续粘贴的次数。</summary>
        private int pasteCount;
        /// <summary>按类型注册的绘制组件。</summary>
        private readonly Dictionary<RuntimeWidgetKind, IRuntimeWidgetRenderer> renderers = new Dictionary<RuntimeWidgetKind, IRuntimeWidgetRenderer>();
        /// <summary>当前编辑会话。</summary>
        private RuntimeLayoutSession session;
        /// <summary>缩放比。</summary>
        private float zoom = 1;
        /// <summary>选中的组件标识。</summary>
        private string selectedId;
        /// <summary>鼠标拖动起点的逻辑位置。</summary>
        private Point startPoint;
        /// <summary>鼠标按下的屏幕坐标，用于区分单击和真正拖动。</summary>
        private Point startMousePoint;
        /// <summary>鼠标按下时的组件边界。</summary>
        private Rectangle originalBounds;
        /// <summary>拖动期间的临时边界。</summary>
        private Rectangle dragBounds;
        /// <summary>当前缩放手柄；-1 表示移动。</summary>
        private int handleIndex = -1;
        /// <summary>是否正在进行交互。</summary>
        private bool dragging;
        /// <summary>禁用交互的预览模式。</summary>
        public bool PreviewMode { get; set; }
        /// <summary>显示运行页面外部的工作区网格，页面内部始终使用纯色背景。</summary>
        public bool ShowGrid { get; set; } = true;
        /// <summary>拖动时对齐到可见网格和其他控件。</summary>
        public bool SnapToGrid { get; set; } = true;
        /// <summary>组件选择变化。</summary>
        public event EventHandler SelectionChanged;
        /// <summary>向宿主报告无损的交互错误。</summary>
        public event Action<string> EditError;
        /// <summary>当前选择标识。</summary>
        public string SelectedId => selectedId;
        /// <summary>选中控件编号的独立快照，外部不能修改画布选择集合。</summary>
        public IReadOnlyList<string> SelectedIds => selectedIds.ToArray();
        /// <summary>页面内容原点，标题栏和外部网格不占用方案布局坐标。</summary>
        private PointF PageOrigin => session == null ? PointF.Empty : new PointF(
            Math.Max(MarginSize, (ClientSize.Width - session.Document.Width * zoom) / 2),
            Math.Max(MarginSize + (PreviewMode ? 0 : PageHeaderHeight),
                (ClientSize.Height - session.Document.Height * zoom + (PreviewMode ? 0 : PageHeaderHeight)) / 2));

        /// <summary>初始化内置渲染器和画布样式。</summary>
        public RuntimeDesignCanvas() : this(new RuntimeSystemWidgetClipboard()) { }

        /// <summary>注入剪贴板，画布交互仍使用同一条编辑和校验路径。</summary>
        public RuntimeDesignCanvas(IRuntimeWidgetClipboard clipboard)
        {
            this.clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(38, 40, 43);
            AllowDrop = true;
            TabStop = true;
            Register(new RuntimeImageRenderer(GetImageViewport));
            Register(new RuntimeElapsedRenderer());
            Register(new RuntimeTextRenderer());
            Register(new RuntimeSignalRenderer());
            Register(new RuntimeRunControlRenderer());
            Register(new RuntimeLogRenderer());
            Register(new RuntimeTuningRenderer(RuntimeWidgetKind.ManualParameters));
            Register(new RuntimeTuningRenderer(RuntimeWidgetKind.OneClickLearning));
        }

        /// <summary>注册或替换某类组件的绘制器。</summary>
        public void Register(IRuntimeWidgetRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            IRuntimeWidgetRenderer old;
            if (renderers.TryGetValue(renderer.Kind, out old)) old.Dispose();
            renderers[renderer.Kind] = renderer;
            surfaces.Dispose();
            Invalidate();
        }

        /// <summary>连接工作文档并同步画布尺寸。</summary>
        public void Bind(RuntimeLayoutSession value)
        {
            imageMenu.Close();
            contextImageId = null;
            StopImagePan();
            CancelPointerInteraction();
            imageViews.Clear();
            imageSources.Clear();
            surfaces.Dispose();
            dropElement = null;
            pasteCount = 0;
            lastPastePayload = null;
            if (session != null) session.Changed -= DocumentChanged;
            session = value;
            if (session != null) session.Changed += DocumentChanged;
            SelectElements(new string[0]);
            DocumentChanged(this, EventArgs.Empty);
        }

        /// <summary>设置缩放，所有几何编辑仍以逻辑坐标计算。</summary>
        public void SetZoom(float value)
        {
            // 预览尺寸由 Dock.Fill 决定，不套用编辑器的边距和滚动范围。
            if (PreviewMode) { Invalidate(); return; }
            zoom = Math.Max(0.05f, Math.Min(2f, value));
            if (session != null)
            {
                int contentWidth = (int)Math.Ceiling(session.Document.Width * zoom) + MarginSize * 2;
                int contentHeight = (int)Math.Ceiling(session.Document.Height * zoom) + MarginSize * 2 + (PreviewMode ? 0 : PageHeaderHeight);
                int width = viewportSize.Width, height = viewportSize.Height;
                bool horizontal = contentWidth > width, vertical = contentHeight > height;
                if (vertical) width -= SystemInformation.VerticalScrollBarWidth;
                if (horizontal) height -= SystemInformation.HorizontalScrollBarHeight;
                if (!horizontal && contentWidth > width) height -= SystemInformation.HorizontalScrollBarHeight;
                if (!vertical && contentHeight > height) width -= SystemInformation.VerticalScrollBarWidth;
                Size = new Size(Math.Max(width - 1, contentWidth), Math.Max(height - 1, contentHeight));
            }
            Invalidate();
        }

        /// <summary>更新工作区尺寸，让固定缩放和自适应缩放均能居中显示。</summary>
        public void SetViewport(Size value) { viewportSize = value; }

        /// <summary>计算当前视口下的完整页面缩放。</summary>
        public float FitZoom(Size viewport)
        {
            if (session == null) return 1;
            return Math.Max(0.05f, Math.Min(1, Math.Min((viewport.Width - MarginSize * 2f - 2) / session.Document.Width,
                (viewport.Height - MarginSize * 2f - 2 - (PreviewMode ? 0 : PageHeaderHeight)) / session.Document.Height)));
        }

        /// <summary>选择组件，同时通知属性和组件列表。</summary>
        public void SelectElement(string id)
        {
            SelectElements(id == null ? new string[0] : new[] { id });
        }

        /// <summary>更新一组选中控件，过滤无效编号且仅在实际变化时通知宿主。</summary>
        public void SelectElements(IEnumerable<string> ids)
        {
            var valid = new HashSet<string>(session?.Document.Elements.Select(item => item.Id) ?? Enumerable.Empty<string>());
            var next = (ids ?? Enumerable.Empty<string>()).Where(valid.Contains).Distinct().ToArray();
            if (selectedIds.SequenceEqual(next)) return;
            selectedIds.Clear(); selectedIds.AddRange(next);
            selectedId = selectedIds.LastOrDefault();
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>按照原有图层次序取得选择，复制后不改变组内遮挡关系。</summary>
        private List<RuntimeLayoutElement> SelectedElements()
        {
            var ids = new HashSet<string>(selectedIds);
            return session?.Document.Elements.Where(item => ids.Contains(item.Id)).ToList() ?? new List<RuntimeLayoutElement>();
        }

        /// <summary>工具栏与快捷键共用的批量复制入口。</summary>
        public void DuplicateSelection()
        {
            if (session == null || dragging || selectedIds.Count == 0) return;
            SelectElements(session.InsertCopies(SelectedElements(), new Point(24, 24)).Select(item => item.Id));
        }

        /// <summary>工具栏与快捷键共用的批量删除入口。</summary>
        public void DeleteSelection()
        {
            if (session == null || dragging || selectedIds.Count == 0) return;
            session.Remove(selectedIds.ToArray());
        }

        /// <summary>文档变化后清理图片缓存和失效选择。</summary>
        private void DocumentChanged(object sender, EventArgs e)
        {
            if (dragging) CancelPointerInteraction();
            if (session == null) return;
            SynchronizeImageViews();
            foreach (var renderer in renderers.Values) renderer.Synchronize(session.Document);
            surfaces.Synchronize(session.Document);
            SelectElements(selectedIds);
            SetZoom(zoom);
            Invalidate();
        }

        /// <summary>绘制设计页面、组件和选择手柄。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (session == null) return;
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (PreviewMode) { DrawFilledPreview(graphics, e.ClipRectangle); return; }
            PointF origin = PageOrigin;
            DrawWorkspace(graphics, origin);
            var state = graphics.Save();
            graphics.TranslateTransform(origin.X, origin.Y);
            graphics.ScaleTransform(zoom, zoom);
            var page = new Rectangle(0, 0, session.Document.Width, session.Document.Height);
            using (var brush = new SolidBrush(RuntimeLayoutSerializer.ParseColor(session.Document.Background))) graphics.FillRectangle(brush, page);
            graphics.SetClip(page);
            surfaces.BeginFrame();
            foreach (var element in session.Document.Elements)
            {
                Rectangle bounds = GetDesignBounds(element);
                if (!ToScreen(bounds).IntersectsWith(e.ClipRectangle)) continue;
                var elementState = graphics.Save();
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.SetClip(new Rectangle(0, 0, bounds.Width, bounds.Height));
                try
                {
                    surfaces.Draw(graphics, renderers[element.Kind], element, bounds.Size, zoom,
                        dragging && !marquee && element.Id == selectedId && handleIndex >= 0);
                }
                finally { graphics.Restore(elementState); }
            }
            var ghosts = copyElements ?? (dropElement != null && alignment != null ? new List<RuntimeLayoutElement> { dropElement } : null);
            if (ghosts != null)
            {
                foreach (var ghost in ghosts)
                {
                    Rectangle bounds = copyElements != null ? OffsetBounds(ghost.Bounds, DragOffset) : alignment.Bounds;
                    var ghostState = graphics.Save();
                    try
                    {
                        graphics.TranslateTransform(bounds.X, bounds.Y);
                        graphics.SetClip(new Rectangle(Point.Empty, bounds.Size));
                        surfaces.Draw(graphics, renderers[ghost.Kind], ghost, bounds.Size, zoom, false);
                    }
                    finally { graphics.Restore(ghostState); }
                }
            }
            graphics.Restore(state);
            using (var pen = new Pen(PreviewMode ? Color.FromArgb(90, 95, 101) : SelectionColor, PreviewMode ? 1 : 2))
                graphics.DrawRectangle(pen, origin.X - 1, origin.Y - 1 - (PreviewMode ? 0 : PageHeaderHeight),
                    page.Width * zoom + 1, page.Height * zoom + 1 + (PreviewMode ? 0 : PageHeaderHeight));
            if (ghosts != null)
            {
                foreach (var ghost in ghosts)
                    DrawSelection(graphics, copyElements != null ? OffsetBounds(ghost.Bounds, DragOffset) : alignment.Bounds, false);
            }
            else foreach (var selected in SelectedElements()) DrawSelection(graphics, GetDesignBounds(selected), selectedIds.Count == 1 && !marquee);
            if (marquee && pointerMoved)
            {
                var rectangle = ToScreen(marqueeBounds);
                using (var fill = new SolidBrush(Color.FromArgb(24, SelectionColor))) graphics.FillRectangle(fill, rectangle);
                using (var pen = new Pen(SelectionColor) { DashStyle = DashStyle.Dash })
                    graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            }
            if (!PreviewMode && alignment != null && (dragging || dropElement != null)) DrawFeedback(graphics);
        }

        /// <summary>绘制每个选中控件；仅单选时提供缩放手柄，避免误改多选组尺寸。</summary>
        private void DrawSelection(Graphics graphics, Rectangle bounds, bool showHandles)
        {
            RectangleF rect = ToScreen(bounds);
            using (var pen = new Pen(SelectionColor, 2)) graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            if (!showHandles) return;
            foreach (PointF point in Handles(rect))
            {
                var handle = new RectangleF(point.X - 4, point.Y - 4, 8, 8);
                graphics.FillRectangle(Brushes.White, handle);
                using (var pen = new Pen(SelectionColor)) graphics.DrawRectangle(pen, handle.X, handle.Y, handle.Width, handle.Height);
            }
        }

        /// <summary>整个选择组共用的移动量，保持控件之间的相对位置。</summary>
        private Point DragOffset => new Point(dragBounds.X - originalBounds.X, dragBounds.Y - originalBounds.Y);

        /// <summary>获取当前编辑预览几何；复制预览不移动原件。</summary>
        private Rectangle GetDesignBounds(RuntimeLayoutElement element)
        {
            Rectangle original;
            if (!dragging || marquee || copyElements != null || pointerBounds == null || !pointerBounds.TryGetValue(element.Id, out original)) return element.Bounds;
            return handleIndex >= 0 ? dragBounds : OffsetBounds(original, DragOffset);
        }

        /// <summary>不修改来源矩形的平移计算。</summary>
        private static Rectangle OffsetBounds(Rectangle bounds, Point offset) { bounds.Offset(offset); return bounds; }

        /// <summary>计算一组选中控件的共同边界。</summary>
        private static Rectangle GroupBounds(IEnumerable<RuntimeLayoutElement> elements) => elements.Select(item => item.Bounds).Aggregate(Rectangle.Union);

        /// <summary>将运行布局铺满预览客户区；只缩放临时几何，图像绘制器继续按比例显示原图。</summary>
        private void DrawFilledPreview(Graphics graphics, Rectangle clip)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            graphics.Clear(RuntimeLayoutSerializer.ParseColor(session.Document.Background));
            float scaleX = (float)ClientSize.Width / session.Document.Width;
            float scaleY = (float)ClientSize.Height / session.Document.Height;
            surfaces.BeginFrame();
            foreach (var element in session.Document.Elements)
            {
                // 左右、上下使用同一比例换算并四舍五入，避免相邻组件出现一像素缝隙。
                var bounds = Rectangle.FromLTRB((int)Math.Round(element.X * scaleX), (int)Math.Round(element.Y * scaleY),
                    (int)Math.Round((element.X + element.Width) * scaleX), (int)Math.Round((element.Y + element.Height) * scaleY));
                if (bounds.Width <= 0 || bounds.Height <= 0 || !bounds.IntersectsWith(clip)) continue;
                var draft = element.Copy();
                draft.FontSize = element.FontSize * Math.Min(scaleX, scaleY);
                var state = graphics.Save();
                try
                {
                    graphics.TranslateTransform(bounds.X, bounds.Y);
                    graphics.SetClip(new Rectangle(Point.Empty, bounds.Size), CombineMode.Intersect);
                    surfaces.Draw(graphics, renderers[element.Kind], draft, bounds.Size, 1, false);
                }
                finally { graphics.Restore(state); }
            }
        }

        /// <summary>按预览的同一比例计算原生交互控件位置，避免缩放后绘图和点击区域错位。</summary>
        internal Rectangle GetPreviewBounds(RuntimeLayoutElement element)
        {
            if (session == null) return Rectangle.Empty;
            float x = (float)ClientSize.Width / session.Document.Width;
            float y = (float)ClientSize.Height / session.Document.Height;
            return Rectangle.FromLTRB((int)Math.Round(element.X * x), (int)Math.Round(element.Y * y),
                (int)Math.Round((element.X + element.Width) * x), (int)Math.Round((element.Y + element.Height) * y));
        }

        /// <summary>运行结果只重绘对应控件；同一批图像与ROI更新合并，保留区域内原有图层顺序。</summary>
        internal void InvalidateElements(IEnumerable<string> elementIds)
        {
            if (session == null || IsDisposed) return;
            var ids = new HashSet<string>(elementIds, StringComparer.Ordinal);
            if (ids.Count == 0) return;
            if (!PreviewMode) { Invalidate(); return; }
            foreach (var element in session.Document.Elements)
            {
                if (!ids.Contains(element.Id)) continue;
                Rectangle bounds = Rectangle.Intersect(ClientRectangle, GetPreviewBounds(element));
                if (!bounds.IsEmpty) Invalidate(bounds, false);
            }
        }

        /// <summary>绘制外部网格及独立页面标题，网格原点始终与页面逻辑坐标一致。</summary>
        private void DrawWorkspace(Graphics graphics, PointF origin)
        {
            if (PreviewMode) return;
            graphics.Clear(Color.FromArgb(61, 63, 67));
            if (ShowGrid && zoom >= 0.25f)
            {
                var gridState = graphics.Save();
                // 轴向细网格无需抗锯齿，并跳过随后被页面覆盖的区域。
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.ExcludeClip(new Rectangle((int)origin.X, (int)origin.Y - PageHeaderHeight,
                    (int)(session.Document.Width * zoom), (int)(session.Document.Height * zoom) + PageHeaderHeight));
                float spacing = RuntimeLayoutAlignment.GridSpacing * zoom;
                using (var minor = new Pen(Color.FromArgb(42, 31, 33, 36)))
                using (var major = new Pen(Color.FromArgb(65, 27, 29, 32)))
                {
                    int firstX = (int)Math.Floor(-origin.X / spacing), firstY = (int)Math.Floor(-origin.Y / spacing);
                    for (int i = firstX; origin.X + i * spacing < Width; i++)
                        graphics.DrawLine(i % 5 == 0 ? major : minor, origin.X + i * spacing, 0, origin.X + i * spacing, Height);
                    for (int i = firstY; origin.Y + i * spacing < Height; i++)
                        graphics.DrawLine(i % 5 == 0 ? major : minor, 0, origin.Y + i * spacing, Width, origin.Y + i * spacing);
                }
                graphics.Restore(gridState);
            }
            var header = new RectangleF(origin.X, origin.Y - PageHeaderHeight, session.Document.Width * zoom, PageHeaderHeight);
            using (var brush = new SolidBrush(Color.FromArgb(37, 39, 42))) graphics.FillRectangle(brush, header);
            using (var font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(233, 235, 237)))
            using (var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                graphics.DrawString(session.Document.Name, font, brush, new RectangleF(header.X + 10, header.Y, Math.Max(1, header.Width - 20), header.Height), format);
        }

        /// <summary>绘制横纵虚线和宽高位置浮标，绘制范围与字号使用屏幕坐标。</summary>
        private void DrawFeedback(Graphics graphics)
        {
            PointF origin = PageOrigin;
            var state = graphics.Save();
            graphics.SetClip(new RectangleF(origin.X, origin.Y, session.Document.Width * zoom, session.Document.Height * zoom));
            using (var pen = new Pen(Color.FromArgb(85, 202, 237)) { DashStyle = DashStyle.Dash })
                foreach (var guide in alignment.Guides)
                {
                    if (guide.Vertical) graphics.DrawLine(pen, origin.X + guide.Position * zoom, origin.Y + guide.Start * zoom - 10,
                        origin.X + guide.Position * zoom, origin.Y + guide.End * zoom + 10);
                    else graphics.DrawLine(pen, origin.X + guide.Start * zoom - 10, origin.Y + guide.Position * zoom,
                        origin.X + guide.End * zoom + 10, origin.Y + guide.Position * zoom);
                }
            graphics.Restore(state);
            Rectangle bounds = alignment.Bounds;
            string text = $"宽 {bounds.Width}  高 {bounds.Height}   |   X {bounds.X}  Y {bounds.Y}";
            using (var font = new Font("Microsoft YaHei UI", 9))
            {
                Size size = TextRenderer.MeasureText(text, font);
                RectangleF rect = ToScreen(bounds);
                Rectangle visible = Parent == null ? ClientRectangle : Rectangle.Intersect(ClientRectangle,
                    RectangleToClient(Parent.RectangleToScreen(Parent.ClientRectangle)));
                int width = size.Width + 18, height = size.Height + 12;
                int x = Math.Max(visible.Left + 4, Math.Min((int)rect.Right - width, visible.Right - width - 4));
                int y = (int)rect.Bottom + 12;
                if (y + height > visible.Bottom - 4) y = (int)rect.Top - height - 12;
                y = Math.Max(visible.Top + 4, Math.Min(y, visible.Bottom - height - 4));
                var label = new Rectangle(x, y, width, height);
                using (var brush = new SolidBrush(Color.FromArgb(28, 30, 33))) graphics.FillRectangle(brush, label);
                using (var pen = new Pen(SelectionColor)) graphics.DrawRectangle(pen, label);
                TextRenderer.DrawText(graphics, text, font, label, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>将逻辑矩形转换为当前屏幕坐标。</summary>
        private RectangleF ToScreen(Rectangle rect) => new RectangleF(PageOrigin.X + rect.X * zoom, PageOrigin.Y + rect.Y * zoom, rect.Width * zoom, rect.Height * zoom);
        /// <summary>将鼠标位置换算到设计坐标。</summary>
        private Point ToDocument(Point point) => new Point((int)Math.Round((point.X - PageOrigin.X) / zoom), (int)Math.Round((point.Y - PageOrigin.Y) / zoom));
        /// <summary>获得八个缩放手柄，顺序为顺时针。</summary>
        private static PointF[] Handles(RectangleF r) => new[]
        {
            new PointF(r.Left, r.Top), new PointF(r.Left + r.Width / 2, r.Top), new PointF(r.Right, r.Top),
            new PointF(r.Right, r.Top + r.Height / 2), new PointF(r.Right, r.Bottom),
            new PointF(r.Left + r.Width / 2, r.Bottom), new PointF(r.Left, r.Bottom), new PointF(r.Left, r.Top + r.Height / 2)
        };

        /// <summary>判断鼠标命中的缩放手柄。</summary>
        private int HitHandle(Point location)
        {
            if (selectedIds.Count != 1) return -1;
            var selected = session?.Find(selectedId);
            if (selected == null) return -1;
            var handles = Handles(ToScreen(selected.Bounds));
            for (int i = 0; i < handles.Length; i++)
                if (Math.Abs(location.X - handles[i].X) <= 7 && Math.Abs(location.Y - handles[i].Y) <= 7) return i;
            return -1;
        }

        /// <summary>按图层从顶到底命中组件。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (TryBeginImagePan(e)) return;
            BeginPointerInteraction(e, ModifierKeys);
        }

        /// <summary>记录选择与修饰键；空白处框选，Ctrl单击多选，Ctrl拖动复制。</summary>
        internal void BeginPointerInteraction(MouseEventArgs e, Keys modifiers)
        {
            if (PreviewMode || session == null || e.Button != MouseButtons.Left) return;
            CancelPointerInteraction();
            Focus();
            selectionBefore = selectedIds.ToArray();
            controlGesture = (modifiers & Keys.Control) != 0;
            startMousePoint = e.Location;
            startPoint = ToDocument(e.Location);
            handleIndex = controlGesture ? -1 : HitHandle(e.Location);
            var hit = handleIndex >= 0 ? session.Find(selectedId) : session.Document.Elements.LastOrDefault(item => item.Bounds.Contains(startPoint));
            pointerHitId = hit?.Id;
            if (hit == null)
            {
                if (!controlGesture) SelectElements(new string[0]);
                if (!new Rectangle(0, 0, session.Document.Width, session.Document.Height).Contains(startPoint))
                { selectionBefore = null; return; }
                marquee = true;
                marqueeBounds = new Rectangle(startPoint, Size.Empty);
            }
            else
            {
                if (!selectedIds.Contains(hit.Id)) SelectElements(controlGesture ? selectedIds.Concat(new[] { hit.Id }) : new[] { hit.Id });
                pointerElements = SelectedElements().Select(item => item.Copy()).ToList();
                pointerBounds = pointerElements.ToDictionary(item => item.Id, item => item.Bounds);
                originalBounds = GroupBounds(pointerElements);
                dragBounds = originalBounds;
                if (handleIndex < 0) Cursor = Cursors.SizeAll;
            }
            alignment = null;
            dragging = true;
            Capture = true;
            Invalidate();
        }

        /// <summary>仅更新交互预览边界。</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (TryMoveImage(e)) return;
            if (PreviewMode || session == null) return;
            if (!dragging)
            {
                int handle = HitHandle(e.Location);
                Cursor = handle < 0 ? (session.Document.Elements.Any(item => item.Bounds.Contains(ToDocument(e.Location))) ? Cursors.SizeAll : Cursors.Default) : handle == 1 || handle == 5 ? Cursors.SizeNS :
                    handle == 3 || handle == 7 ? Cursors.SizeWE : handle == 0 || handle == 4 ? Cursors.SizeNWSE : Cursors.SizeNESW;
                return;
            }
            UpdatePointerPreview(e.Location);
        }

        /// <summary>移动和松开共用最终坐标计算，不依赖最后一条MouseMove是否已送达。</summary>
        private void UpdatePointerPreview(Point location)
        {
            if (!pointerMoved &&
                Math.Abs(location.X - startMousePoint.X) < Math.Max(1, SystemInformation.DragSize.Width / 2) &&
                Math.Abs(location.Y - startMousePoint.Y) < Math.Max(1, SystemInformation.DragSize.Height / 2)) return;
            pointerMoved = true;
            Point point = ToDocument(location);
            if (marquee)
            {
                point.X = Math.Max(0, Math.Min(session.Document.Width, point.X));
                point.Y = Math.Max(0, Math.Min(session.Document.Height, point.Y));
                marqueeBounds = Rectangle.FromLTRB(Math.Min(startPoint.X, point.X), Math.Min(startPoint.Y, point.Y),
                    Math.Max(startPoint.X, point.X), Math.Max(startPoint.Y, point.Y));
                var contained = session.Document.Elements.Where(item => marqueeBounds.Contains(item.Bounds)).Select(item => item.Id);
                SelectElements(controlGesture ? selectionBefore.Concat(contained) : contained);
                Invalidate();
                return;
            }
            if (controlGesture && copyElements == null)
            {
                copyElements = pointerElements.Select(item => item.Copy()).ToList();
                foreach (var copy in copyElements) copy.Id = Guid.NewGuid().ToString("N");
            }
            int dx = point.X - startPoint.X, dy = point.Y - startPoint.Y;
            Rectangle bounds = originalBounds;
            if (handleIndex < 0) bounds.Offset(dx, dy);
            else
            {
                int left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom;
                if (handleIndex == 0 || handleIndex == 6 || handleIndex == 7) left = Math.Max(0, Math.Min(right - 48, left + dx));
                if (handleIndex == 2 || handleIndex == 3 || handleIndex == 4) right = Math.Min(session.Document.Width, Math.Max(left + 48, right + dx));
                if (handleIndex == 0 || handleIndex == 1 || handleIndex == 2) top = Math.Max(0, Math.Min(bottom - 32, top + dy));
                if (handleIndex == 4 || handleIndex == 5 || handleIndex == 6) bottom = Math.Min(session.Document.Height, Math.Max(top + 32, bottom + dy));
                bounds = Rectangle.FromLTRB(left, top, right, bottom);
            }
            alignment = RuntimeLayoutAlignment.ResolveGroup(session.Document, copyElements == null ? pointerBounds.Keys : Enumerable.Empty<string>(),
                bounds, originalBounds, handleIndex, zoom, SnapToGrid);
            dragBounds = alignment.Bounds;
            Invalidate();
        }

        /// <summary>松开左键提交一次移动、缩放或复制；页面外释放副本则取消。</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (TryEndImagePointer(e)) return;
            if (!dragging || e.Button != MouseButtons.Left) return;
            if (controlGesture && !marquee && !new Rectangle(0, 0, session.Document.Width, session.Document.Height).Contains(ToDocument(e.Location)))
            { CancelPointerInteraction(); return; }
            UpdatePointerPreview(e.Location);
            bool isMarquee = marquee, moved = pointerMoved, control = controlGesture;
            var copies = copyElements;
            var hitId = pointerHitId;
            var before = selectionBefore;
            var offset = DragOffset;
            var bounds = pointerElements?.ToDictionary(item => item.Id, item => GetDesignBounds(item));
            CancelPointerInteraction(false);
            try
            {
                if (isMarquee) return;
                if (!moved)
                {
                    if (control) SelectElements(before.Contains(hitId) ? before.Where(id => id != hitId) : before.Concat(new[] { hitId }));
                    else SelectElement(hitId);
                }
                else if (copies != null) SelectElements(session.InsertCopies(copies, offset).Select(item => item.Id));
                else if (bounds != null) session.SetBounds(bounds);
            }
            catch (Exception ex)
            {
                SelectElements(before);
                EditError?.Invoke(ex.Message);
            }
            Invalidate();
        }

        /// <summary>捕获意外丢失时放弃临时移动。</summary>
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) StopImagePan();
            if (dragging && !Capture) CancelPointerInteraction();
        }

        /// <summary>放弃临时交互及其绘图缓存，不修改文档或撤销栈。</summary>
        private void CancelPointerInteraction(bool restoreSelection = true)
        {
            bool hadCopy = copyElements != null;
            var before = selectionBefore;
            dragging = false;
            copyElements = null;
            pointerElements = null;
            pointerBounds = null;
            pointerHitId = null;
            selectionBefore = null;
            marquee = false;
            pointerMoved = false;
            controlGesture = false;
            alignment = null;
            Capture = false;
            Cursor = Cursors.Default;
            if (hadCopy && session != null) { surfaces.Synchronize(session.Document); SynchronizeImageViews(); }
            if (restoreSelection && before != null) SelectElements(before);
            Invalidate();
        }

        /// <summary>接受来自工具箱的组件类型。</summary>
        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            base.OnDragEnter(drgevent);
            UpdateDropPreview(drgevent);
        }

        /// <summary>拖入期间持续更新控件预览、参考线及尺寸浮标。</summary>
        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);
            UpdateDropPreview(drgevent);
        }

        /// <summary>离开内容页后清除临时控件，不修改撤销历史。</summary>
        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            ClearDropPreview();
        }

        /// <summary>校验拖入区域，控件只能落在独立运行页面内。</summary>
        private void UpdateDropPreview(DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            RuntimeWidgetKind kind;
            Point point = ToDocument(PointToClient(new Point(e.X, e.Y)));
            if (PreviewMode || session == null || e.Data == null || !e.Data.GetDataPresent(DragFormat) ||
                !Enum.TryParse(e.Data.GetData(DragFormat) as string, out kind) || !Enum.IsDefined(typeof(RuntimeWidgetKind), kind) ||
                !new Rectangle(0, 0, session.Document.Width, session.Document.Height).Contains(point))
            { ClearDropPreview(); return; }
            if (dropElement == null || dropElement.Kind != kind) dropElement = RuntimeLayoutSession.CreateElement(kind);
            var proposed = new Rectangle(point, new Size(dropElement.Width, dropElement.Height));
            alignment = RuntimeLayoutAlignment.Resolve(session.Document, null, proposed, proposed, -1, zoom, SnapToGrid);
            e.Effect = DragDropEffects.Copy;
            Invalidate();
        }

        /// <summary>释放拖入反馈，不触及正式布局。</summary>
        private void ClearDropPreview()
        {
            dropElement = null; alignment = null;
            if (session != null) surfaces.Synchronize(session.Document);
            Invalidate();
        }

        /// <summary>按拖入位置创建新组件。</summary>
        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            UpdateDropPreview(drgevent);
            if (dropElement == null || alignment == null) return;
            RuntimeWidgetKind kind = dropElement.Kind;
            Rectangle bounds = alignment.Bounds;
            ClearDropPreview();
            try { SelectElement(session.Add(kind, bounds.Location).Id); Focus(); }
            catch (Exception ex) { EditError?.Invoke(ex.Message); }
        }

        /// <summary>画布快捷键不拦截属性栏的文本编辑。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && panningImageId != null) { StopImagePan(); return true; }
            if (PreviewMode || session == null) return base.ProcessCmdKey(ref msg, keyData);
            try { return ProcessEditKey(ref msg, keyData); }
            catch (Exception ex) { EditError?.Invoke(ex.Message); return true; }
        }

        /// <summary>执行编辑快捷键，由入口统一向宿主报告失败。</summary>
        private bool ProcessEditKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && dragging) { CancelPointerInteraction(); return true; }
            if (dragging) return true;
            if (keyData == (Keys.Control | Keys.Z)) { session.Undo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { session.Redo(); return true; }
            if (keyData == Keys.Delete) { DeleteSelection(); return true; }
            if (keyData == (Keys.Control | Keys.D)) { DuplicateSelection(); return true; }
            if (keyData == (Keys.Control | Keys.A)) { SelectElements(session.Document.Elements.Select(item => item.Id)); return true; }
            if (keyData == (Keys.Control | Keys.C))
            {
                var elements = SelectedElements();
                if (elements.Count > 0)
                {
                    string payload = RuntimeWidgetClipboardCodec.Encode(session.Document, elements);
                    clipboard.Write(payload);
                    lastPastePayload = payload; pasteCount = 0;
                }
                return true;
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                string payload = clipboard.Read();
                if (string.IsNullOrWhiteSpace(payload)) return true;
                var elements = RuntimeWidgetClipboardCodec.DecodeMany(payload);
                int next = payload == lastPastePayload ? Math.Min(pasteCount + 1, 1000) : 1;
                SelectElements(session.InsertCopies(elements, new Point(next * 24, next * 24)).Select(item => item.Id));
                lastPastePayload = payload; pasteCount = next;
                return true;
            }
            var selected = SelectedElements();
            if (selected.Count > 0)
            {
                int distance = (keyData & Keys.Shift) != 0 ? RuntimeLayoutAlignment.GridSpacing : 1;
                Keys key = keyData & Keys.KeyCode;
                var original = GroupBounds(selected);
                var bounds = original;
                if (key == Keys.Left) bounds.Offset(-distance, 0);
                else if (key == Keys.Right) bounds.Offset(distance, 0);
                else if (key == Keys.Up) bounds.Offset(0, -distance);
                else if (key == Keys.Down) bounds.Offset(0, distance);
                else return base.ProcessCmdKey(ref msg, keyData);
                bounds = session.Constrain(bounds);
                var offset = new Point(bounds.X - original.X, bounds.Y - original.Y);
                session.SetBounds(selected.ToDictionary(item => item.Id, item => OffsetBounds(item.Bounds, offset)));
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>解除事件并释放组件的 GDI 缓存。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopImagePan();
                components?.Dispose();
                imageViews.Clear();
                imageSources.Clear();
                if (session != null) session.Changed -= DocumentChanged;
                foreach (var renderer in renderers.Values) renderer.Dispose();
                renderers.Clear();
                surfaces.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
