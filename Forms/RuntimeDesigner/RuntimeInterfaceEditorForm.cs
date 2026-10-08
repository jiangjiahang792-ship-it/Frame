using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行界面编辑器，支持布局设计、示例预览和绑定结果预览。</summary>
    public partial class RuntimeInterfaceEditorForm : Form
    {
        /// <summary>当前编辑工作副本。</summary>
        private RuntimeLayoutSession session;
        /// <summary>多页草稿；为空引用时保持旧的单文档构造行为。</summary>
        private List<PageDraft> pageDrafts;
        /// <summary>当前页面及其独立选择状态。</summary>
        private PageDraft activePage;
        /// <summary>宿主提供的全页保存入口，只接收深复制集合。</summary>
        private Func<IReadOnlyList<RuntimeInterfacePage>, bool> savePages;
        /// <summary>最近一次成功保存的页面顺序、编号及文档内容。</summary>
        private KeyValuePair<string, string>[] savedPagesSnapshot;
        /// <summary>宿主提供的方案保存操作，取消时返回 false。</summary>
        private readonly Func<RuntimeLayoutDocument, bool> saveDocument;
        /// <summary>宿主提供的只读数据源目录，缺省时保持原有示例编辑功能。</summary>
        private readonly IRuntimeBindingCatalog bindingCatalog;
        /// <summary>按当前编辑文档创建只读运行数据源，缺省时仍使用示例预览。</summary>
        private readonly Func<RuntimeLayoutDocument, IRuntimeBindingDataSource> createDataSource;
        /// <summary>只用于读取流程目录的宿主控制器，绝不交给编辑预览执行。</summary>
        private readonly IRuntimeRunController runController;
        /// <summary>由当前窗体创建的工具图标。</summary>
        private readonly List<Image> ownedIcons = new List<Image>();
        /// <summary>最近一次成功保存的文档快照。</summary>
        private string savedSnapshot;
        /// <summary>复用窗口拖动、缩放和标题按钮行为。</summary>
        private readonly RuntimeDesignerWindowChrome windowChrome;
        /// <summary>防止滚动条变化引起缩放布局递归。</summary>
        private bool applyingZoom;
        /// <summary>工具栏或滚轮选择的手动倍率，不受工作区尺寸变化影响。</summary>
        private float manualZoom = 1f;
        /// <summary>倍率下拉框最多保留一个滚轮生成的自定义项。</summary>
        private string customZoomItem;
        /// <summary>属性面板当前的选中组件。</summary>
        private string propertyElementId;
        /// <summary>防止刷新下拉目录时误提交流程选择。</summary>
        private bool refreshingRunProcesses;

        /// <summary>供 Visual Studio 设计器实例化使用。</summary>
        public RuntimeInterfaceEditorForm() : this(new RuntimeLayoutDocument(), "未命名方案", null) { }

        /// <summary>使用宿主提供的方案布局创建编辑窗口，不初始化设备。</summary>
        public RuntimeInterfaceEditorForm(RuntimeLayoutDocument document, string solutionName, Func<RuntimeLayoutDocument, bool> save,
            IRuntimeBindingCatalog bindingCatalog = null, Func<RuntimeLayoutDocument, IRuntimeBindingDataSource> createDataSource = null,
            IRuntimeRunController runController = null)
        {
            InitializeComponent();
            windowChrome = new RuntimeDesignerWindowChrome(this, titleLabel, minimizeButton, maximizeButton, closeButton, windowToolTip);
            session = new RuntimeLayoutSession(document);
            saveDocument = save;
            this.bindingCatalog = bindingCatalog;
            this.createDataSource = createDataSource;
            this.runController = runController;
            savedSnapshot = RuntimeLayoutSerializer.Serialize(session.Document);
            solutionLabel.Text = solutionName;
            InitializeToolbar();
            InitializePageActions();
            InitializeBindingActions();
            InitializeToolbox();
            canvas.Bind(session);
            canvas.SelectionChanged += CanvasSelectionChanged;
            canvas.ZoomRequested += CanvasZoomRequested;
            canvas.EditError += ShowError;
            session.Changed += SessionChanged;
            propertyGrid.PropertyValueChanged += PropertyChanged;
            loadImageButton.Click += LoadImage;
            clearImageButton.Click += ClearImage;
            runProcessCombo.SelectedIndexChanged += RunProcessSelectedIndexChanged;
            runProcessCombo.DropDown += RunProcessDropDown;
            workspace.Resize += WorkspaceResized;
            Shown += EditorShown;
            FormClosing += EditorClosing;
            RefreshDocument();
        }

        /// <summary>创建内联多页编辑器，输入集合和绑定均深复制，旧六参数构造不受影响。</summary>
        public static RuntimeInterfaceEditorForm CreateForPages(IEnumerable<RuntimeInterfacePage> pages, string solutionName,
            Func<IReadOnlyList<RuntimeInterfacePage>, bool> save, IRuntimeBindingCatalog bindingCatalog = null,
            Func<RuntimeLayoutDocument, IRuntimeBindingDataSource> createDataSource = null, IRuntimeRunController runController = null)
        {
            var copies = RuntimeInterfacePage.ClonePages(pages);
            var editor = new RuntimeInterfaceEditorForm(copies.FirstOrDefault()?.Layout, solutionName, null,
                bindingCatalog, createDataSource, runController);
            try
            {
                editor.session.Changed -= editor.SessionChanged;
                editor.session = null;
                editor.savePages = save;
                editor.pageDrafts = copies.Select(page => new PageDraft(page.Id, new RuntimeLayoutSession(page.Layout))).ToList();
                foreach (var page in editor.pageDrafts) page.Session.Changed += editor.SessionChanged;
                editor.savedPagesSnapshot = editor.CapturePagesState();
                editor.pageTabs.Visible = true;
                editor.centerLayout.RowStyles[0].Height = (float)Math.Round(32f * editor.DeviceDpi / 96f);
                if (editor.pageDrafts.Count == 0) editor.AddPage();
                else editor.SelectPage(editor.pageDrafts[0].Id);
                return editor;
            }
            catch { editor.Dispose(); throw; }
        }

        /// <summary>每页独立拥有撤销会话和选中标识，缓存序列化内容避免每次编辑重复复制全部页面。</summary>
        private sealed class PageDraft
        {
            /// <summary>与宿主页面对应的稳定标识。</summary>
            public readonly string Id;
            /// <summary>只属于本页的撤销与重做工作会话。</summary>
            public readonly RuntimeLayoutSession Session;
            /// <summary>离开页面时保存的完整组件选择，最后一项为主选项。</summary>
            public string[] SelectedIds = new string[0];
            /// <summary>最近一次文档修改后的序列化缓存。</summary>
            public string Snapshot;
            /// <summary>随页面动态生成的标签按钮，不属于固定设计器控件。</summary>
            public ToolStripButton Tab;
            /// <summary>初始化独立会话及内容缓存。</summary>
            public PageDraft(string id, RuntimeLayoutSession session)
            { Id = id; Session = session; Snapshot = RuntimeLayoutSerializer.Serialize(session.Document); }
        }

        /// <summary>配置固定加号和右键菜单，不通过额外参数窗体新增页面。</summary>
        private void InitializePageActions()
        {
            var icon = RuntimeDesignerIcons.Create(0xE710, Color.WhiteSmoke);
            ownedIcons.Add(icon);
            addPageButton.Image = icon;
            pageTabs.Renderer = new PageTabRenderer();
            addPageButton.Click += (sender, args) => RunEdit(AddPage);
            deletePageMenuItem.Click += DeletePageClicked;
        }

        /// <summary>唯一中文默认名称从图像、图像1顺序生成，新增布局不继承其它页的绑定。</summary>
        private void AddPage()
        {
            if (pageDrafts == null || !Validate()) return;
            int suffix = 0;
            string name;
            do { name = "图像" + (suffix == 0 ? string.Empty : suffix.ToString()); suffix++; }
            while (pageDrafts.Any(page => page.Session.Document.Name == name));
            var draft = new PageDraft(Guid.NewGuid().ToString("N"), new RuntimeLayoutSession(new RuntimeLayoutDocument { Name = name }));
            draft.Session.Changed += SessionChanged;
            pageDrafts.Add(draft);
            SelectPage(draft.Id);
        }

        /// <summary>切换前提交当前属性编辑，随后恢复目标页面独立会话和组件选择。</summary>
        private void SelectPage(string id)
        {
            if (pageDrafts == null) return;
            var next = pageDrafts.FirstOrDefault(page => page.Id == id);
            if (ReferenceEquals(next, activePage) || !Validate()) return;
            if (activePage != null) activePage.SelectedIds = canvas.SelectedIds.ToArray();
            activePage = next;
            session = next?.Session;
            canvas.Bind(session);
            canvas.SelectElements(next?.SelectedIds);
            RefreshDocument();
            ApplyZoom();
        }

        /// <summary>动态标签只改变当前页，不新建窗口或覆盖其它页草稿。</summary>
        private void PageTabClicked(object sender, EventArgs e)
        {
            var page = (sender as ToolStripItem)?.Tag as PageDraft;
            if (page != null) SelectPage(page.Id);
        }

        /// <summary>右键菜单绑定实际点击的页签，删除非当前页也不会误删当前页。</summary>
        private void PageTabMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var button = sender as ToolStripItem;
            var page = button?.Tag as PageDraft;
            if (page == null || !pageDrafts.Contains(page)) return;
            pageTabMenu.Tag = page.Id;
            pageTabMenu.Show(Cursor.Position);
        }

        /// <summary>删除需要明确确认；取消不影响会话、撤销栈及选择。</summary>
        private void DeletePageClicked(object sender, EventArgs e)
        {
            string id = pageTabMenu.Tag as string;
            var page = pageDrafts?.FirstOrDefault(item => item.Id == id);
            if (page == null) return;
            if (MessageBox.Show(this, "删除运行界面“" + page.Session.Document.Name + "”？", "删除页面",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RemovePage(id);
        }

        /// <summary>移除已确认的页面；最后一页删除后保持空集合而不自动补页。</summary>
        private void RemovePage(string id)
        {
            if (pageDrafts == null || !Validate()) return;
            int index = pageDrafts.FindIndex(page => page.Id == id);
            if (index < 0) return;
            var removed = pageDrafts[index];
            removed.Session.Changed -= SessionChanged;
            pageDrafts.RemoveAt(index);
            if (removed.Tab != null) { pageTabs.Items.Remove(removed.Tab); removed.Tab.Dispose(); }
            if (ReferenceEquals(removed, activePage))
                SelectPage(pageDrafts.Count == 0 ? null : pageDrafts[Math.Min(index, pageDrafts.Count - 1)].Id);
            else RefreshDocument();
        }

        /// <summary>同步标签名称和橙色选中态，保留原生溢出布局及紧随标签的加号。</summary>
        private void RefreshPageTabs()
        {
            if (pageDrafts == null) return;
            pageTabs.SuspendLayout();
            try
            {
                foreach (var page in pageDrafts)
                {
                    if (page.Tab == null)
                    {
                        page.Tab = new ToolStripButton { Tag = page, AutoSize = false, DisplayStyle = ToolStripItemDisplayStyle.Text,
                            Margin = Padding.Empty, Overflow = ToolStripItemOverflow.AsNeeded, AutoToolTip = false };
                        page.Tab.Click += PageTabClicked;
                        page.Tab.MouseUp += PageTabMouseUp;
                        pageTabs.Items.Insert(pageTabs.Items.IndexOf(addPageButton), page.Tab);
                    }
                    string name = page.Session.Document.Name;
                    float scale = DeviceDpi / 96f;
                    int height = (int)Math.Round(30 * scale);
                    if (page.Tab.Text != name || page.Tab.Height != height)
                    {
                        page.Tab.Text = name;
                        page.Tab.ToolTipText = name;
                        page.Tab.AccessibleName = name;
                        int width = Math.Max((int)(64 * scale), Math.Min((int)(180 * scale), TextRenderer.MeasureText(name, pageTabs.Font).Width + (int)(24 * scale)));
                        page.Tab.Size = new Size(width, height);
                    }
                    page.Tab.Checked = ReferenceEquals(page, activePage);
                    page.Tab.ForeColor = page.Tab.Checked ? Color.FromArgb(255, 151, 55) : Color.WhiteSmoke;
                }
            }
            finally { pageTabs.ResumeLayout(true); }
        }

        /// <summary>页面内容使用修改时缓存，仅组合稳定编号、顺序和内容判断全页脏状态。</summary>
        private KeyValuePair<string, string>[] CapturePagesState() => pageDrafts.Select(
            page => new KeyValuePair<string, string>(page.Id, page.Snapshot)).ToArray();

        /// <summary>旧入口检查单文档，多页入口包含新增、删除及非当前页面的所有修改。</summary>
        private bool HasUnsavedChanges()
        {
            if (pageDrafts == null) return RuntimeLayoutSerializer.Serialize(session.Document) != savedSnapshot;
            if (savedPagesSnapshot.Length != pageDrafts.Count) return true;
            for (int index = 0; index < pageDrafts.Count; index++)
                if (pageDrafts[index].Id != savedPagesSnapshot[index].Key || pageDrafts[index].Snapshot != savedPagesSnapshot[index].Value) return true;
            return false;
        }

        /// <summary>统一关闭决策，保存失败或保存期间出现新修改都不能丢弃草稿。</summary>
        private bool CanCloseWithChoice(DialogResult choice) => !HasUnsavedChanges() || choice == DialogResult.No ||
            choice == DialogResult.Yes && SaveDocument() && !HasUnsavedChanges();

        /// <summary>暗色页签及橙色选中边线，溢出菜单仍由原生工具条负责布局。</summary>
        private sealed class PageTabRenderer : ToolStripProfessionalRenderer
        {
            /// <summary>工具条使用固定深灰背景，不绘制默认浅色渐变。</summary>
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            { using (var brush = new SolidBrush(Color.FromArgb(33, 35, 38))) e.Graphics.FillRectangle(brush, e.AffectedBounds); }
            /// <summary>省略默认外围边线，保持页签与画布连续。</summary>
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
            /// <summary>选中页面用底部橙线识别，悬停仅改变深灰背景。</summary>
            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                var button = e.Item as ToolStripButton;
                bool selected = button?.Checked == true;
                using (var brush = new SolidBrush(selected ? Color.FromArgb(64, 48, 37) : e.Item.Selected ? Color.FromArgb(58, 60, 64) : Color.FromArgb(33, 35, 38)))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                if (selected)
                    using (var brush = new SolidBrush(Color.FromArgb(255, 128, 0)))
                        e.Graphics.FillRectangle(brush, 0, Math.Max(0, e.Item.Height - 2), e.Item.Width, 2);
            }
            /// <summary>长名称省略显示，完整名称由悬停提示提供。</summary>
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            { e.TextFormat |= TextFormatFlags.EndEllipsis; base.OnRenderItemText(e); }
            /// <summary>深色背景上的菜单箭头保持可见。</summary>
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            { e.ArrowColor = Color.WhiteSmoke; base.OnRenderArrow(e); }
        }

        /// <summary>设置工具栏图标和命令。</summary>
        private void InitializeToolbar()
        {
            SetIcon(saveButton, 0xE74E);
            SetIcon(importButton, 0xE8B5);
            SetIcon(exportButton, 0xE898);
            SetIcon(undoButton, 0xE7A7);
            SetIcon(redoButton, 0xE7A6);
            SetIcon(duplicateButton, 0xE8C8);
            SetIcon(deleteButton, 0xE74D);
            SetIcon(forwardButton, 0xE74A);
            SetIcon(backwardButton, 0xE74B);
            SetIcon(arrangeHorizontalButton, 0xE8E3);
            arrangeHorizontalButton.Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
            SetIcon(arrangeVerticalButton, 0xE8E3);
            SetIcon(fitButton, 0xE9A6);
            SetIcon(gridButton, 0xE80A);
            SetIcon(snapButton, 0xE840);
            SetIcon(previewButton, 0xE768);
            previewButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            saveButton.Click += SaveClicked;
            importButton.Click += ImportClicked;
            exportButton.Click += ExportClicked;
            undoButton.Click += (s, e) => session?.Undo();
            redoButton.Click += (s, e) => session?.Redo();
            duplicateButton.Click += DuplicateClicked;
            deleteButton.Click += (s, e) => RunEdit(() => canvas.DeleteSelection());
            forwardButton.Click += (s, e) => ChangeLayer(1);
            backwardButton.Click += (s, e) => ChangeLayer(-1);
            arrangeHorizontalButton.Click += (s, e) => RunEdit(() => session?.Arrange(canvas.SelectedIds, true));
            arrangeVerticalButton.Click += (s, e) => RunEdit(() => session?.Arrange(canvas.SelectedIds, false));
            gridButton.Click += (s, e) => { canvas.ShowGrid = gridButton.Checked; canvas.Invalidate(); };
            snapButton.Click += (s, e) => canvas.SnapToGrid = snapButton.Checked;
            fitButton.Click += (s, e) => { zoomCombo.SelectedIndex = 0; ApplyZoom(); };
            zoomCombo.SelectedIndexChanged += ZoomSelectionChanged;
            previewButton.Click += PreviewClicked;
            saveButton.Enabled = saveDocument != null;
        }

        /// <summary>为工具按钮配置标准图标和悬停说明。</summary>
        private void SetIcon(ToolStripButton button, int glyph)
        {
            var icon = RuntimeDesignerIcons.Create(glyph, Color.FromArgb(88, 96, 105));
            ownedIcons.Add(icon);
            button.Image = icon;
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ToolTipText = button.Text;
            button.AutoSize = false;
            button.Size = new Size(34, 30);
            if (button == previewButton) button.Size = new Size(82, 30);
        }

        /// <summary>建立包括运行控制在内的可拖拽组件工具箱。</summary>
        private void InitializeToolbox()
        {
            var glyphs = new Dictionary<RuntimeWidgetKind, int>
            {
                { RuntimeWidgetKind.Image, 0xEB9F }, { RuntimeWidgetKind.Elapsed, 0xE823 },
                { RuntimeWidgetKind.Text, 0xE8D2 }, { RuntimeWidgetKind.Signal, 0xEA3A },
                { RuntimeWidgetKind.RunControl, 0xE768 }, { RuntimeWidgetKind.Log, 0xE8A5 },
                { RuntimeWidgetKind.ManualParameters, 0xE713 }, { RuntimeWidgetKind.OneClickLearning, 0xE945 }
            };
            foreach (RuntimeWidgetKind kind in Enum.GetValues(typeof(RuntimeWidgetKind)))
            {
                // ImageList 延迟到创建句柄时复制原图，必须将原图保留到窗体释放。
                int glyph;
                if (!glyphs.TryGetValue(kind, out glyph)) glyph = 0xE713;
                var icon = RuntimeDesignerIcons.Create(glyph, Color.FromArgb(93, 111, 127), 36);
                ownedIcons.Add(icon);
                toolboxImages.Images.Add(kind.ToString(), icon);
                toolbox.Items.Add(new ListViewItem(RuntimeLayoutSession.GetName(kind), kind.ToString()) { Tag = kind });
            }
            toolbox.ItemDrag += ToolboxItemDrag;
            toolbox.DoubleClick += ToolboxDoubleClick;
        }

        /// <summary>从工具箱开始系统拖放。</summary>
        private void ToolboxItemDrag(object sender, ItemDragEventArgs e)
        {
            if (session == null) return;
            var item = e.Item as ListViewItem;
            if (item == null) return;
            var data = new DataObject();
            data.SetData(RuntimeDesignCanvas.DragFormat, ((RuntimeWidgetKind)item.Tag).ToString());
            toolbox.DoDragDrop(data, DragDropEffects.Copy);
        }

        /// <summary>双击也可在页面左上区域添加组件。</summary>
        private void ToolboxDoubleClick(object sender, EventArgs e)
        {
            if (session == null || toolbox.SelectedItems.Count == 0) return;
            RunEdit(() => canvas.SelectElement(session.Add((RuntimeWidgetKind)toolbox.SelectedItems[0].Tag,
                new Point(40 + session.Document.Elements.Count * 16, 40 + session.Document.Elements.Count * 16)).Id));
        }

        /// <summary>首次显示时完整适应画布。</summary>
        private void EditorShown(object sender, EventArgs e) { zoomCombo.SelectedIndex = 0; ApplyZoom(); }
        /// <summary>窗口尺寸变化时同步固定缩放和自适应模式的视口。</summary>
        private void WorkspaceResized(object sender, EventArgs e) => ApplyZoom();

        /// <summary>工具栏选择手动倍率或自适应，不把显示文字作为持续刷新时的状态来源。</summary>
        private void ZoomSelectionChanged(object sender, EventArgs e)
        {
            if (applyingZoom) return;
            if (zoomCombo.SelectedIndex > 0)
                manualZoom = float.Parse(zoomCombo.SelectedItem.ToString().TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture) / 100f;
            ApplyZoom();
        }

        /// <summary>画布负责区分图像和页面滚轮，宿主只处理页面倍率并保持鼠标下的文档锚点。</summary>
        private void CanvasZoomRequested(object sender, RuntimeCanvasZoomEventArgs e)
        {
            if (session == null || applyingZoom || e.Delta == 0) return;
            PointF documentPoint = canvas.DocumentPoint(e.Location);
            var anchor = new Point(canvas.Left + e.Location.X, canvas.Top + e.Location.Y);
            float value = (float)Math.Max(0.05, Math.Min(2, canvas.Zoom * Math.Pow(1.1, e.Delta / 120.0)));
            manualZoom = value;
            applyingZoom = true;
            try
            {
                if (customZoomItem != null) zoomCombo.Items.Remove(customZoomItem);
                customZoomItem = null;
                string text = (value * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
                if (!zoomCombo.Items.Contains(text))
                {
                    customZoomItem = text;
                    zoomCombo.Items.Add(text);
                }
                zoomCombo.SelectedItem = text;
            }
            finally { applyingZoom = false; }
            ApplyZoom();
            PointF transformed = canvas.CanvasPoint(documentPoint);
            workspace.AutoScrollPosition = new Point(Math.Max(0, (int)Math.Round(transformed.X - anchor.X)),
                Math.Max(0, (int)Math.Round(transformed.Y - anchor.Y)));
        }

        /// <summary>自适应才清除滚动位置；手动倍率刷新保留偏移，并补回原生滚动条占用的视口尺寸。</summary>
        private void ApplyZoom()
        {
            if (session == null || applyingZoom) return;
            applyingZoom = true;
            try
            {
                bool fit = zoomCombo.SelectedIndex <= 0;
                Point scroll = workspace.AutoScrollPosition;
                if (fit) workspace.AutoScrollPosition = Point.Empty;
                workspace.AutoScroll = !fit;
                Size viewport = workspace.ClientSize;
                if (workspace.VerticalScroll.Visible) viewport.Width += SystemInformation.VerticalScrollBarWidth;
                if (workspace.HorizontalScroll.Visible) viewport.Height += SystemInformation.HorizontalScrollBarHeight;
                canvas.SetViewport(viewport);
                canvas.SetZoom(fit ? canvas.FitZoom(viewport) : manualZoom);
                workspace.PerformLayout();
                if (!fit) workspace.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            }
            finally { applyingZoom = false; }
        }

        /// <summary>编辑完成后刷新可视状态。</summary>
        private void SessionChanged(object sender, EventArgs e)
        {
            var page = pageDrafts?.FirstOrDefault(item => ReferenceEquals(item.Session, sender));
            if (page != null) page.Snapshot = RuntimeLayoutSerializer.Serialize(page.Session.Document);
            RefreshDocument();
        }

        /// <summary>同步属性栏、状态栏和命令可用状态。</summary>
        private void RefreshDocument()
        {
            undoButton.Enabled = session?.CanUndo == true;
            redoButton.Enabled = session?.CanRedo == true;
            bool dirty = HasUnsavedChanges();
            bool hasPage = session != null;
            saveButton.Enabled = pageDrafts == null ? saveDocument != null : savePages != null;
            importButton.Enabled = exportButton.Enabled = previewButton.Enabled = toolbox.Enabled = hasPage;
            canvas.Visible = canvas.Enabled = hasPage;
            emptyPagesLabel.Visible = !hasPage;
            propertyGrid.Enabled = hasPage;
            if (!hasPage)
            {
                workspace.AutoScroll = false;
                workspace.AutoScrollPosition = Point.Empty;
                emptyPagesLabel.BringToFront();
            }
            Text = "运行界面设计";
            stateLabel.Text = (hasPage ? $"  {session.Document.Name}   {session.Document.Width} × {session.Document.Height}   |   {session.Document.Elements.Count} 个组件   |   " : "  暂无运行界面   |   ") +
                (pageDrafts == null ? string.Empty : pageDrafts.Count + " 个页面   |   ") + (dirty ? "未保存" : "已保存");
            RefreshPageTabs();
            RefreshProperties();
            if (zoomCombo.SelectedIndex == 0) ApplyZoom();
        }

        /// <summary>画布选择变化时同步属性栏。</summary>
        private void CanvasSelectionChanged(object sender, EventArgs e)
        {
            RefreshProperties();
        }

        /// <summary>仅单选显示组件属性副本，多选只显示数量，防止误改主选项。</summary>
        private void RefreshProperties()
        {
            int count = session == null ? 0 : canvas.SelectedIds.Count;
            var selected = count == 1 ? session.Find(canvas.SelectedId) : null;
            propertyElementId = selected?.Id;
            propertyGrid.Enabled = session != null && count < 2;
            if (count > 1)
            {
                propertiesLabel.Text = "选中 " + count + " 个控件";
                propertyGrid.SelectedObject = null;
            }
            else if (selected == null)
            {
                propertiesLabel.Text = "页面属性";
                propertyGrid.SelectedObject = session == null ? null : new RuntimePageProperties(session.Document);
            }
            else
            {
                var copy = selected.Copy();
                propertiesLabel.Text = RuntimeLayoutSession.GetName(selected.Kind) + " · 属性";
                propertyGrid.SelectedObject = new RuntimeElementProperties(copy);
            }
            imageActions.Visible = selected?.Kind == RuntimeWidgetKind.Image;
            duplicateButton.Enabled = deleteButton.Enabled = count > 0;
            forwardButton.Enabled = backwardButton.Enabled = selected != null;
            arrangeHorizontalButton.Enabled = arrangeVerticalButton.Enabled = count >= 2;
            RefreshBindingActions(selected);
            RefreshRoiBindingActions(selected);
            RefreshRunProcessActions(selected);
        }

        /// <summary>运行、日志与调参共用流程选择栏，调参必须明确绑定且不提供全流程选项。</summary>
        private void RefreshRunProcessActions(RuntimeLayoutElement element)
        {
            bool log = element?.Kind == RuntimeWidgetKind.Log;
            bool tuning = element != null && RuntimeTuningController.Supports(element.Kind);
            bool supported = log || tuning || element?.Kind == RuntimeWidgetKind.RunControl;
            runProcessActions.Visible = supported;
            float height = supported ? (float)Math.Round(68f * DeviceDpi / 96f) : 0;
            if (rightLayout.RowStyles[2].Height != height) rightLayout.RowStyles[2].Height = height;
            if (!supported) return;
            runProcessLabel.Text = log ? "订阅流程（包含组合模块）" : tuning ? "绑定流程" : "运行流程";
            int? targetId = log ? element.LogProcessId : tuning ? element.ActionProcessId : element.RunProcessId;

            IReadOnlyList<RuntimeProcessChoice> processes = new RuntimeProcessChoice[0];
            string error = null;
            try { processes = (log || tuning) && bindingCatalog is IRuntimeLogProcessCatalog logCatalog
                    ? logCatalog.GetLogProcesses() : runController?.GetProcesses() ?? processes; }
            catch (Exception ex) { error = "流程目录不可用：" + ex.Message; }

            refreshingRunProcesses = true;
            runProcessCombo.BeginUpdate();
            try
            {
                runProcessCombo.Items.Clear();
                runProcessCombo.Items.Add(new RunProcessItem(null, tuning ? "未绑定" : "全流程"));
                int selectedIndex = targetId == null ? 0 : -1;
                var seen = new HashSet<int>();
                foreach (var process in processes)
                {
                    if (process == null || !seen.Add(process.Id)) continue;
                    int index = runProcessCombo.Items.Add(new RunProcessItem(process.Id,
                        string.IsNullOrWhiteSpace(process.Name) ? "流程 " + process.Id : process.Name));
                    if (targetId == process.Id) selectedIndex = index;
                }
                if (selectedIndex < 0)
                    selectedIndex = runProcessCombo.Items.Add(new RunProcessItem(targetId, "已失效：流程 " + targetId));
                runProcessCombo.SelectedIndex = selectedIndex;
                windowToolTip.SetToolTip(runProcessCombo, error ?? ((RunProcessItem)runProcessCombo.SelectedItem).Name);
            }
            finally
            {
                runProcessCombo.EndUpdate();
                refreshingRunProcesses = false;
            }
        }

        /// <summary>展开时重新读取目录，以便显示编辑期间发生的流程改名和删除。</summary>
        private void RunProcessDropDown(object sender, EventArgs e) => RefreshRunProcessActions(session?.Find(propertyElementId));

        /// <summary>用户更改流程时提交可撤销事务；程序刷新和重复选择不创建记录。</summary>
        private void RunProcessSelectedIndexChanged(object sender, EventArgs e)
        {
            if (refreshingRunProcesses) return;
            var choice = runProcessCombo.SelectedItem as RunProcessItem;
            string id = propertyElementId;
            var element = session?.Find(id);
            if (choice == null || element == null) return;
            if (element.Kind == RuntimeWidgetKind.Log)
            {
                if (element.LogProcessId != choice.ProcessId)
                    RunEdit(() => session.Edit(document => session.Find(id).LogProcessId = choice.ProcessId));
            }
            else if (element.Kind == RuntimeWidgetKind.RunControl && element.RunProcessId != choice.ProcessId)
                RunEdit(() => session.Edit(document => session.Find(id).RunProcessId = choice.ProcessId));
            else if (RuntimeTuningController.Supports(element.Kind) && element.ActionProcessId != choice.ProcessId)
                RunEdit(() => session.Edit(document => session.Find(id).ActionProcessId = choice.ProcessId));
        }

        /// <summary>用可空编号区分全流程和指定流程，显示名称不参与保存或解析。</summary>
        private sealed class RunProcessItem
        {
            /// <summary>空值表示全流程，其余值保留宿主流程编号。</summary>
            public int? ProcessId { get; }
            /// <summary>目录当前名称或失效说明。</summary>
            public string Name { get; }
            /// <summary>复制下拉框需要的轻量目录数据。</summary>
            public RunProcessItem(int? processId, string name) { ProcessId = processId; Name = name; }
        }

        /// <summary>为设计器内的数据源按钮设置标准图标和事件。</summary>
        private void InitializeBindingActions()
        {
            var chooseIcon = RuntimeDesignerIcons.Create(0xE712, Color.FromArgb(88, 96, 105));
            var clearIcon = RuntimeDesignerIcons.Create(0xE8BB, Color.FromArgb(88, 96, 105));
            ownedIcons.Add(chooseIcon);
            ownedIcons.Add(clearIcon);
            chooseBindingButton.Image = chooseIcon;
            clearBindingButton.Image = clearIcon;
            chooseBindingButton.Click += ChooseBindingClicked;
            clearBindingButton.Click += ClearBindingClicked;
            chooseRoiBindingButton.Image = chooseIcon;
            clearRoiBindingButton.Image = clearIcon;
            chooseRoiBindingButton.Click += ChooseRoiBindingClicked;
            clearRoiBindingButton.Click += ClearRoiBindingClicked;
        }

        /// <summary>本轮仅为图像和运行耗时开放真实数据源配置。</summary>
        private static bool SupportsBinding(RuntimeLayoutElement element) => element != null &&
            (element.Kind == RuntimeWidgetKind.Image || element.Kind == RuntimeWidgetKind.Elapsed);

        /// <summary>按选中类型显示数据源条，目录描述只用于显示，不写回文档。</summary>
        private void RefreshBindingActions(RuntimeLayoutElement element)
        {
            bool supported = SupportsBinding(element);
            bindingActions.Visible = supported;
            bindingLabel.Text = element?.Kind == RuntimeWidgetKind.Image ? "图像源" : "数据源";
            float height = supported ? (float)Math.Round(68f * DeviceDpi / 96f) : 0;
            if (rightLayout.RowStyles[1].Height != height) rightLayout.RowStyles[1].Height = height;
            chooseBindingButton.Enabled = supported && bindingCatalog != null;
            clearBindingButton.Enabled = supported && bindingCatalog != null && element.Binding != null;
            bindingPath.Text = supported ? DescribeBinding(element.Binding) : string.Empty;
            windowToolTip.SetToolTip(bindingPath, bindingPath.Text);
        }

        /// <summary>读取当前名称或失效说明，缺省目录时仍保留原保存路径。</summary>
        private string DescribeBinding(RuntimeDataBinding binding)
        {
            if (binding == null) return "未绑定";
            if (bindingCatalog == null || binding.Kind == RuntimeBindingKind.Roi && !(bindingCatalog is IRuntimeRoiBindingCatalog))
                return string.IsNullOrEmpty(binding.DisplayPath) ? "数据源目录不可用" : binding.DisplayPath;
            try { return bindingCatalog.Describe(binding.Copy()) ?? "数据源不可用"; }
            catch (Exception ex) { return "数据源不可用：" + ex.Message; }
        }

        /// <summary>以当前组件类型获取目录，只有明确确认后才提交绑定。</summary>
        private void ChooseBindingClicked(object sender, EventArgs e)
        {
            string id = propertyElementId;
            var element = session?.Find(id);
            if (bindingCatalog == null || !SupportsBinding(element)) return;
            RunEdit(() =>
            {
                using (var picker = new RuntimeBindingPickerForm(bindingCatalog.GetSources(element.Kind), element.Binding))
                    if (picker.ShowDialog(this) == DialogResult.OK) ApplyBinding(id, picker.SelectedBinding);
            });
        }

        /// <summary>清除数据源进入同一撤销事务，不影响组件的示例图像和外观。</summary>
        private void ClearBindingClicked(object sender, EventArgs e)
        {
            if (bindingCatalog == null) return;
            string id = propertyElementId;
            RunEdit(() => ApplyBinding(id, null));
        }

        /// <summary>按组件稳定标识提交独立绑定副本，同时拒绝不匹配的数据类型。</summary>
        private void ApplyBinding(string elementId, RuntimeDataBinding binding)
        {
            var element = session?.Find(elementId);
            if (!SupportsBinding(element)) return;
            if (binding != null && (element.Kind == RuntimeWidgetKind.Image ? binding.Kind != RuntimeBindingKind.Image :
                binding.Kind != RuntimeBindingKind.ProcessElapsed && binding.Kind != RuntimeBindingKind.NodeElapsed))
                throw new InvalidOperationException("所选数据源与组件类型不匹配。");
            session.Edit(document => session.Find(elementId).Binding = binding?.Copy());
        }

        /// <summary>仅图像显示独立区域来源，切换选择不修改现有绑定。</summary>
        private void RefreshRoiBindingActions(RuntimeLayoutElement element)
        {
            bool supported = element?.Kind == RuntimeWidgetKind.Image;
            roiBindingActions.Visible = supported;
            float height = supported ? (float)Math.Round(68f * DeviceDpi / 96f) : 0;
            if (rightLayout.RowStyles[3].Height != height) rightLayout.RowStyles[3].Height = height;
            chooseRoiBindingButton.Enabled = supported && bindingCatalog != null;
            clearRoiBindingButton.Enabled = supported && element.RoiBinding != null;
            roiBindingPath.Text = supported ? DescribeBinding(element.RoiBinding) : string.Empty;
            windowToolTip.SetToolTip(roiBindingPath, roiBindingPath.Text);
        }

        /// <summary>兼容旧目录的空区域树，并按当前区域绑定预选，不混用图像目录。</summary>
        private RuntimeBindingPickerForm CreateRoiBindingPicker(RuntimeLayoutElement element)
        {
            var sources = (bindingCatalog as IRuntimeRoiBindingCatalog)?.GetRoiSources() ?? new RuntimeBindingChoice[0];
            return new RuntimeBindingPickerForm(sources, element.RoiBinding);
        }

        /// <summary>确认区域叶子后才提交独立绑定，取消不改变文档。</summary>
        private void ChooseRoiBindingClicked(object sender, EventArgs e)
        {
            string id = propertyElementId;
            var element = session?.Find(id);
            if (bindingCatalog == null || element?.Kind != RuntimeWidgetKind.Image) return;
            RunEdit(() =>
            {
                using (var picker = CreateRoiBindingPicker(element))
                    if (picker.ShowDialog(this) == DialogResult.OK) ApplyRoiBinding(id, picker.SelectedBinding);
            });
        }

        /// <summary>清除区域绑定允许撤销，不依赖目录可用性且不影响图像源。</summary>
        private void ClearRoiBindingClicked(object sender, EventArgs e)
        {
            string id = propertyElementId;
            RunEdit(() => ApplyRoiBinding(id, null));
        }

        /// <summary>只接受图像组件的区域来源，提交深副本并保留原图像绑定。</summary>
        private void ApplyRoiBinding(string elementId, RuntimeDataBinding binding)
        {
            var element = session?.Find(elementId);
            if (element?.Kind != RuntimeWidgetKind.Image) return;
            if (binding != null && binding.Kind != RuntimeBindingKind.Roi)
                throw new InvalidOperationException("所选数据源不是ROI来源。");
            session.Edit(document => session.Find(elementId).RoiBinding = binding?.Copy());
        }

        /// <summary>用户修改属性后验证并提交；非法尺寸不会污染画布。</summary>
        private void PropertyChanged(object sender, PropertyValueChangedEventArgs e)
        {
            if (session == null || canvas.SelectedIds.Count > 1) return;
            var elementProperties = propertyGrid.SelectedObject as RuntimeElementProperties;
            var pageProperties = propertyGrid.SelectedObject as RuntimePageProperties;
            RunEdit(() =>
            {
                if (elementProperties != null) session.UpdateElement(elementProperties.Draft);
                else if (pageProperties != null)
                    session.Edit(doc =>
                    {
                        doc.Name = pageProperties.Name; doc.Width = pageProperties.Width; doc.Height = pageProperties.Height;
                        doc.Background = RuntimeLayoutSerializer.FormatColor(pageProperties.Background);
                    });
            });
        }

        /// <summary>载入可随模板移植的图片资源。</summary>
        private void LoadImage(object sender, EventArgs e)
        {
            string id = propertyElementId;
            if (session?.Find(id)?.Kind != RuntimeWidgetKind.Image) return;
            using (var dialog = new OpenFileDialog { Title = "载入预览图片", Filter = "图像文件|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RunEdit(() =>
                {
                    string data = RuntimeLayoutSerializer.ReadImage(dialog.FileName);
                    session.Edit(doc => session.Find(id).ImageData = data);
                });
            }
        }

        /// <summary>只清除选中组件的内嵌图片。</summary>
        private void ClearImage(object sender, EventArgs e)
        {
            if (session?.Find(propertyElementId) != null) session.Edit(doc => session.Find(propertyElementId).ImageData = null);
        }

        /// <summary>批量复制选中组件，由画布统一选中副本并保持一次撤销。</summary>
        private void DuplicateClicked(object sender, EventArgs e) => RunEdit(() => canvas.DuplicateSelection());

        /// <summary>按一层调整组件绘制顺序。</summary>
        private void ChangeLayer(int direction)
        {
            if (session == null || canvas.SelectedIds.Count != 1) return;
            string id = canvas.SelectedId;
            session.Edit(doc =>
            {
                int index = doc.Elements.FindIndex(element => element.Id == id);
                int target = index + direction;
                if (index < 0 || target < 0 || target >= doc.Elements.Count) return;
                var item = doc.Elements[index];
                doc.Elements.RemoveAt(index); doc.Elements.Insert(target, item);
            });
        }

        /// <summary>导入只替换编辑副本，可通过撤销恢复。</summary>
        private void ImportClicked(object sender, EventArgs e)
        {
            if (session == null) return;
            using (var dialog = new OpenFileDialog { Title = "导入运行界面模板", Filter = "运行界面模板|*.runtime-ui.json;*.json", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RunEdit(() => { session.Replace(RuntimeLayoutSerializer.Load(dialog.FileName)); canvas.SelectElement(null); ApplyZoom(); });
            }
        }

        /// <summary>导出当前布局，不改变方案的已保存状态。</summary>
        private void ExportClicked(object sender, EventArgs e)
        {
            if (session == null) return;
            using (var dialog = new SaveFileDialog { Title = "导出运行界面模板", Filter = "运行界面模板|*.runtime-ui.json", FileName = "运行界面.runtime-ui.json", AddExtension = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RunEdit(() => { RuntimeLayoutSerializer.Save(dialog.FileName, session.Document); stateLabel.Text = "  模板已导出：" + Path.GetFileName(dialog.FileName); });
            }
        }

        /// <summary>提交经过校验的独立快照到宿主方案。</summary>
        private bool SaveDocument()
        {
            if (!Validate()) return false;
            if (pageDrafts == null ? saveDocument == null : savePages == null) return false;
            try
            {
                if (pageDrafts != null)
                {
                    var submitted = RuntimeInterfacePage.ClonePages(pageDrafts.Select(page => new RuntimeInterfacePage { Id = page.Id, Layout = page.Session.Document }));
                    var baseline = CapturePagesState();
                    if (!savePages(submitted)) return false;
                    savedPagesSnapshot = baseline;
                }
                else
                {
                    var submitted = RuntimeLayoutSerializer.Clone(session.Document);
                    string baseline = RuntimeLayoutSerializer.Serialize(session.Document);
                    if (!saveDocument(submitted)) return false;
                    savedSnapshot = baseline;
                }
                RefreshDocument();
                return true;
            }
            catch (Exception ex) { ShowError(ex.Message); return false; }
        }

        /// <summary>保存工具栏入口。</summary>
        private void SaveClicked(object sender, EventArgs e) => SaveDocument();

        /// <summary>按当前绑定打开实时或示例预览，关闭后释放窗口持有的资源。</summary>
        private void PreviewClicked(object sender, EventArgs e)
        {
            if (session == null || !Validate()) return;
            RunEdit(() =>
            {
                using (var preview = CreatePreview()) preview.ShowDialog(this);
            });
        }

        /// <summary>只有存在绑定且宿主提供工厂时创建实时数据源，成功构造后交给预览持有。</summary>
        private RuntimeInterfacePreviewForm CreatePreview()
        {
            if (session == null) throw new InvalidOperationException("请先新增运行界面。");
            RuntimeInterfacePreviewForm preview;
            if (createDataSource == null || !session.Document.Elements.Any(element => element.Binding != null || element.RoiBinding != null))
                preview = new RuntimeInterfacePreviewForm(session.Document);
            else
            {
                var source = createDataSource(session.Document);
                try { preview = new RuntimeInterfacePreviewForm(session.Document, source, null); }
                catch
                {
                    // 构造未完成，调用方拿不到窗体，必须在移交所有权之前释放数据源。
                    source?.Dispose();
                    throw;
                }
            }

            try
            {
                // 编辑预览永远使用新模拟实例；正式控制器仅提供流程目录。
                preview.AttachRunController(new RuntimePreviewRunController(runController?.GetProcesses() ?? new RuntimeProcessChoice[0]));
                preview.AttachLogSource(new RuntimePreviewLogSource());
                return preview;
            }
            catch
            {
                preview.Dispose();
                throw;
            }
        }

        /// <summary>关闭时保护尚未保存的编辑。</summary>
        private void EditorClosing(object sender, FormClosingEventArgs e)
        {
            if (!Validate()) { e.Cancel = true; return; }
            if (!HasUnsavedChanges()) return;
            DialogResult result = MessageBox.Show(this, pageDrafts == null ? "是否保存当前运行界面的修改？" : "是否保存全部运行界面的修改？",
                "未保存的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            e.Cancel = !CanCloseWithChoice(result);
        }

        /// <summary>把预期的文件或编辑失败展示给用户。</summary>
        private void RunEdit(Action action)
        {
            try { action(); }
            catch (Exception ex) { RefreshProperties(); ShowError(ex.Message); }
        }

        /// <summary>显示编辑器操作失败原因。</summary>
        private void ShowError(string message) => MessageBox.Show(this, message, "运行界面设计", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        /// <summary>保存快捷键在编辑器内可用，其余交互交给焦点控件。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S)) { Validate(); SaveDocument(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>保留系统移动和缩放能力，由设计器控件绘制窗口外观。</summary>
        protected override CreateParams CreateParams => RuntimeDesignerWindowChrome.ConfigureStyles(base.CreateParams);

        /// <summary>处理窗口边缘命中和当前显示器工作区限制。</summary>
        protected override void WndProc(ref Message message)
        {
            if (!RuntimeDesignerWindowChrome.ProcessMessage(this, ref message)) base.WndProc(ref message);
        }
    }
}
