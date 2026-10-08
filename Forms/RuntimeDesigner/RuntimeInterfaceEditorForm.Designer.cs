using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行界面编辑器的固定控件和布局。</summary>
    partial class RuntimeInterfaceEditorForm
    {
        /// <summary>窗体设计器组件容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>标题文字和窗口操作的固定四列布局。</summary>
        private TableLayoutPanel titleBar;
        /// <summary>设计器标题。</summary>
        private Label titleLabel;
        /// <summary>窗口最小化按钮。</summary>
        private Button minimizeButton;
        /// <summary>窗口最大化和还原按钮。</summary>
        private Button maximizeButton;
        /// <summary>沿用标准关闭流程的按钮。</summary>
        private Button closeButton;
        /// <summary>窗口按钮中文悬停提示。</summary>
        private ToolTip windowToolTip;
        /// <summary>编辑工具栏。</summary>
        private ToolStrip toolbar;
        /// <summary>保存关联方案。</summary>
        private ToolStripButton saveButton;
        /// <summary>模板导入。</summary>
        private ToolStripButton importButton;
        /// <summary>模板导出。</summary>
        private ToolStripButton exportButton;
        /// <summary>撤销操作。</summary>
        private ToolStripButton undoButton;
        /// <summary>重做操作。</summary>
        private ToolStripButton redoButton;
        /// <summary>复制组件。</summary>
        private ToolStripButton duplicateButton;
        /// <summary>删除组件。</summary>
        private ToolStripButton deleteButton;
        /// <summary>上移图层。</summary>
        private ToolStripButton forwardButton;
        /// <summary>下移图层。</summary>
        private ToolStripButton backwardButton;
        /// <summary>选中组件横向等间距排列。</summary>
        private ToolStripButton arrangeHorizontalButton;
        /// <summary>选中组件纵向等间距排列。</summary>
        private ToolStripButton arrangeVerticalButton;
        /// <summary>页面自适应缩放。</summary>
        private ToolStripButton fitButton;
        /// <summary>缩放比例。</summary>
        private ToolStripComboBox zoomCombo;
        /// <summary>网格显示开关。</summary>
        private ToolStripButton gridButton;
        /// <summary>网格吸附开关。</summary>
        private ToolStripButton snapButton;
        /// <summary>独立预览入口。</summary>
        private ToolStripButton previewButton;
        /// <summary>工具箱、画布和属性栏三列布局。</summary>
        private TableLayoutPanel contentLayout;
        /// <summary>左侧工具布局。</summary>
        private TableLayoutPanel leftLayout;
        /// <summary>工具箱标题。</summary>
        private Label toolboxLabel;
        /// <summary>基础显示组件及运行控制的工具箱。</summary>
        private ListView toolbox;
        /// <summary>工具箱图标。</summary>
        private ImageList toolboxImages;
        /// <summary>中间页签和画布的固定两行布局。</summary>
        private TableLayoutPanel centerLayout;
        /// <summary>暗色水平页签条，页面过多时使用原生溢出下拉。</summary>
        private ToolStrip pageTabs;
        /// <summary>紧跟可见标签的直接新增页面按钮。</summary>
        private ToolStripButton addPageButton;
        /// <summary>页面标签的右键操作菜单。</summary>
        private ContextMenuStrip pageTabMenu;
        /// <summary>确认后删除对应页面的菜单命令。</summary>
        private ToolStripMenuItem deletePageMenuItem;
        /// <summary>全部页面删除后的空态标签。</summary>
        private Label emptyPagesLabel;
        /// <summary>画布滚动容器。</summary>
        private RuntimeDesignWorkspace workspace;
        /// <summary>实际设计画布。</summary>
        private RuntimeDesignCanvas canvas;
        /// <summary>右侧属性栏布局。</summary>
        private TableLayoutPanel rightLayout;
        /// <summary>属性栏标题。</summary>
        private Label propertiesLabel;
        /// <summary>图像和耗时的数据源路径及操作布局。</summary>
        private TableLayoutPanel bindingActions;
        /// <summary>数据源条的中文标题。</summary>
        private Label bindingLabel;
        /// <summary>只能通过选择器设置的完整数据源路径。</summary>
        private TextBox bindingPath;
        /// <summary>打开流程输出树的省略号图标按钮。</summary>
        private Button chooseBindingButton;
        /// <summary>清除当前绑定的叉号图标按钮。</summary>
        private Button clearBindingButton;
        /// <summary>图像组件独立的区域来源配置布局。</summary>
        private TableLayoutPanel roiBindingActions;
        /// <summary>区域来源的中文标题。</summary>
        private Label roiBindingLabel;
        /// <summary>仅通过选择器修改的区域来源路径。</summary>
        private TextBox roiBindingPath;
        /// <summary>打开区域输出目录的省略号图标按钮。</summary>
        private Button chooseRoiBindingButton;
        /// <summary>仅清除区域绑定的叉号图标按钮。</summary>
        private Button clearRoiBindingButton;
        /// <summary>运行控制的流程目标配置区域。</summary>
        private TableLayoutPanel runProcessActions;
        /// <summary>流程目标的中文标签。</summary>
        private Label runProcessLabel;
        /// <summary>全流程、指定流程和保留失效编号的只选下拉框。</summary>
        private ComboBox runProcessCombo;
        /// <summary>按类型筛选的属性编辑器。</summary>
        private PropertyGrid propertyGrid;
        /// <summary>图像操作条。</summary>
        private FlowLayoutPanel imageActions;
        /// <summary>加载图片按钮。</summary>
        private Button loadImageButton;
        /// <summary>清除图片按钮。</summary>
        private Button clearImageButton;
        /// <summary>底部状态栏。</summary>
        private StatusStrip statusStrip;
        /// <summary>当前页面状态。</summary>
        private ToolStripStatusLabel stateLabel;
        /// <summary>方案关联名称。</summary>
        private ToolStripStatusLabel solutionLabel;

        /// <summary>释放设计器图标、组件和会话事件。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                windowChrome?.Dispose();
                if (session != null) session.Changed -= SessionChanged;
                if (pageDrafts != null)
                    foreach (var page in pageDrafts) page.Session.Changed -= SessionChanged;
                components?.Dispose();
                foreach (var icon in ownedIcons) icon.Dispose();
                ownedIcons.Clear();
            }
            base.Dispose(disposing);
        }

        /// <summary>所有固定控件都在设计器文件中创建，便于 Visual Studio 调整布局。</summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new TableLayoutPanel();
            titleBar = new TableLayoutPanel();
            titleLabel = new Label();
            minimizeButton = new Button();
            maximizeButton = new Button();
            closeButton = new Button();
            windowToolTip = new ToolTip(components);
            toolbar = new ToolStrip();
            saveButton = new ToolStripButton();
            importButton = new ToolStripButton();
            exportButton = new ToolStripButton();
            undoButton = new ToolStripButton();
            redoButton = new ToolStripButton();
            duplicateButton = new ToolStripButton();
            deleteButton = new ToolStripButton();
            forwardButton = new ToolStripButton();
            backwardButton = new ToolStripButton();
            arrangeHorizontalButton = new ToolStripButton();
            arrangeVerticalButton = new ToolStripButton();
            fitButton = new ToolStripButton();
            zoomCombo = new ToolStripComboBox();
            gridButton = new ToolStripButton();
            snapButton = new ToolStripButton();
            previewButton = new ToolStripButton();
            contentLayout = new TableLayoutPanel();
            leftLayout = new TableLayoutPanel();
            toolboxLabel = new Label();
            toolbox = new ListView();
            toolboxImages = new ImageList(components);
            centerLayout = new TableLayoutPanel();
            pageTabs = new ToolStrip();
            addPageButton = new ToolStripButton();
            pageTabMenu = new ContextMenuStrip(components);
            deletePageMenuItem = new ToolStripMenuItem();
            emptyPagesLabel = new Label();
            workspace = new RuntimeDesignWorkspace();
            canvas = new RuntimeDesignCanvas();
            rightLayout = new TableLayoutPanel();
            propertiesLabel = new Label();
            bindingActions = new TableLayoutPanel();
            bindingLabel = new Label();
            bindingPath = new TextBox();
            chooseBindingButton = new Button();
            clearBindingButton = new Button();
            roiBindingActions = new TableLayoutPanel();
            roiBindingLabel = new Label();
            roiBindingPath = new TextBox();
            chooseRoiBindingButton = new Button();
            clearRoiBindingButton = new Button();
            runProcessActions = new TableLayoutPanel();
            runProcessLabel = new Label();
            runProcessCombo = new ComboBox();
            propertyGrid = new PropertyGrid();
            imageActions = new FlowLayoutPanel();
            loadImageButton = new Button();
            clearImageButton = new Button();
            statusStrip = new StatusStrip();
            stateLabel = new ToolStripStatusLabel();
            solutionLabel = new ToolStripStatusLabel();
            SuspendLayout();
            rootLayout.SuspendLayout();
            titleBar.SuspendLayout();
            contentLayout.SuspendLayout();
            leftLayout.SuspendLayout();
            centerLayout.SuspendLayout();
            pageTabs.SuspendLayout();
            rightLayout.SuspendLayout();
            bindingActions.SuspendLayout();
            roiBindingActions.SuspendLayout();
            runProcessActions.SuspendLayout();
            toolbar.SuspendLayout();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowCount = 4;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            rootLayout.Controls.Add(titleBar, 0, 0);
            rootLayout.Controls.Add(toolbar, 0, 1);
            rootLayout.Controls.Add(contentLayout, 0, 2);
            rootLayout.Controls.Add(statusStrip, 0, 3);
            titleBar.Dock = DockStyle.Fill;
            titleBar.Margin = Padding.Empty;
            titleBar.BackColor = Color.FromArgb(33, 35, 38);
            titleBar.ColumnCount = 4;
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.RowCount = 1;
            titleBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            titleBar.Controls.Add(titleLabel, 0, 0);
            titleBar.Controls.Add(minimizeButton, 1, 0);
            titleBar.Controls.Add(maximizeButton, 2, 0);
            titleBar.Controls.Add(closeButton, 3, 0);
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Margin = Padding.Empty;
            titleLabel.Padding = new Padding(18, 0, 0, 0);
            titleLabel.BackColor = Color.FromArgb(33, 35, 38);
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Text = "运行界面设计";
            titleLabel.AutoEllipsis = true;
            minimizeButton.Dock = DockStyle.Fill;
            minimizeButton.Margin = Padding.Empty;
            minimizeButton.FlatStyle = FlatStyle.Flat;
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 66, 71);
            minimizeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 83, 88);
            minimizeButton.BackColor = titleBar.BackColor;
            minimizeButton.ForeColor = Color.White;
            minimizeButton.UseVisualStyleBackColor = false;
            minimizeButton.TabStop = false;
            minimizeButton.AccessibleName = "最小化";
            windowToolTip.SetToolTip(minimizeButton, "最小化");
            maximizeButton.Dock = DockStyle.Fill;
            maximizeButton.Margin = Padding.Empty;
            maximizeButton.FlatStyle = FlatStyle.Flat;
            maximizeButton.FlatAppearance.BorderSize = 0;
            maximizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 66, 71);
            maximizeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 83, 88);
            maximizeButton.BackColor = titleBar.BackColor;
            maximizeButton.ForeColor = Color.White;
            maximizeButton.UseVisualStyleBackColor = false;
            maximizeButton.TabStop = false;
            maximizeButton.AccessibleName = "最大化";
            windowToolTip.SetToolTip(maximizeButton, "最大化");
            closeButton.Dock = DockStyle.Fill;
            closeButton.Margin = Padding.Empty;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
            closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 32, 24);
            closeButton.BackColor = titleBar.BackColor;
            closeButton.ForeColor = Color.White;
            closeButton.UseVisualStyleBackColor = false;
            closeButton.TabStop = false;
            closeButton.AccessibleName = "关闭";
            windowToolTip.SetToolTip(closeButton, "关闭");
            toolbar.Dock = DockStyle.Fill;
            toolbar.Margin = Padding.Empty;
            toolbar.Padding = new Padding(10, 6, 10, 5);
            toolbar.GripStyle = ToolStripGripStyle.Hidden;
            toolbar.BackColor = Color.FromArgb(248, 249, 250);
            toolbar.ImageScalingSize = new Size(20, 20);
            toolbar.Items.AddRange(new ToolStripItem[] { saveButton, importButton, exportButton, new ToolStripSeparator(),
                undoButton, redoButton, new ToolStripSeparator(), duplicateButton, deleteButton, forwardButton, backwardButton,
                arrangeHorizontalButton, arrangeVerticalButton,
                new ToolStripSeparator(), gridButton, snapButton, new ToolStripSeparator(), fitButton, zoomCombo, previewButton });
            saveButton.Text = "保存到方案";
            importButton.Text = "导入模板";
            exportButton.Text = "导出模板";
            undoButton.Text = "撤销";
            redoButton.Text = "重做";
            duplicateButton.Text = "复制组件";
            deleteButton.Text = "删除组件";
            forwardButton.Text = "上移一层";
            backwardButton.Text = "下移一层";
            arrangeHorizontalButton.Text = "横向等间距排列";
            arrangeHorizontalButton.Enabled = false;
            arrangeVerticalButton.Text = "纵向等间距排列";
            arrangeVerticalButton.Enabled = false;
            fitButton.Text = "适应画布";
            gridButton.Text = "显示网格";
            gridButton.CheckOnClick = true;
            gridButton.Checked = true;
            snapButton.Text = "网格吸附";
            snapButton.CheckOnClick = true;
            snapButton.Checked = true;
            zoomCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            zoomCombo.AutoSize = false;
            zoomCombo.Width = 82;
            zoomCombo.Items.AddRange(new object[] { "适应画布", "5%", "10%", "25%", "50%", "75%", "100%", "125%", "150%", "200%" });
            previewButton.Text = "预览";
            previewButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            previewButton.Alignment = ToolStripItemAlignment.Right;
            contentLayout.Dock = DockStyle.Fill;
            contentLayout.Margin = Padding.Empty;
            contentLayout.ColumnCount = 3;
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 182));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 276));
            contentLayout.RowCount = 1;
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contentLayout.Controls.Add(leftLayout, 0, 0);
            contentLayout.Controls.Add(centerLayout, 1, 0);
            contentLayout.Controls.Add(rightLayout, 2, 0);
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.Margin = Padding.Empty;
            leftLayout.BackColor = Color.FromArgb(244, 245, 247);
            leftLayout.ColumnCount = 1;
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            leftLayout.RowCount = 2;
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftLayout.Controls.Add(toolboxLabel, 0, 0);
            leftLayout.Controls.Add(toolbox, 0, 1);
            toolboxLabel.Dock = DockStyle.Fill;
            toolboxLabel.Text = "基础控件";
            toolboxLabel.TextAlign = ContentAlignment.MiddleLeft;
            toolboxLabel.Padding = new Padding(12, 0, 0, 0);
            toolboxLabel.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
            toolbox.Dock = DockStyle.Fill;
            toolbox.Margin = new Padding(6, 0, 6, 0);
            toolbox.BackColor = leftLayout.BackColor;
            toolbox.BorderStyle = BorderStyle.None;
            toolbox.MultiSelect = false;
            toolbox.HideSelection = false;
            toolbox.LargeImageList = toolboxImages;
            toolbox.UseCompatibleStateImageBehavior = false;
            toolbox.View = View.LargeIcon;
            toolboxImages.ColorDepth = ColorDepth.Depth32Bit;
            toolboxImages.ImageSize = new Size(36, 36);
            centerLayout.Dock = DockStyle.Fill;
            centerLayout.Margin = Padding.Empty;
            centerLayout.ColumnCount = 1;
            centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            centerLayout.RowCount = 2;
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            centerLayout.Controls.Add(pageTabs, 0, 0);
            centerLayout.Controls.Add(workspace, 0, 1);
            pageTabs.Dock = DockStyle.Fill;
            pageTabs.Margin = Padding.Empty;
            pageTabs.Padding = Padding.Empty;
            pageTabs.AutoSize = false;
            pageTabs.GripStyle = ToolStripGripStyle.Hidden;
            pageTabs.CanOverflow = true;
            pageTabs.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            pageTabs.BackColor = Color.FromArgb(33, 35, 38);
            pageTabs.ForeColor = Color.WhiteSmoke;
            pageTabs.Visible = false;
            pageTabs.Items.Add(addPageButton);
            addPageButton.Text = "新增页面";
            addPageButton.ToolTipText = "新增页面";
            addPageButton.AccessibleName = "新增页面";
            addPageButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            addPageButton.AutoSize = false;
            addPageButton.Size = new Size(32, 30);
            addPageButton.Margin = Padding.Empty;
            addPageButton.Overflow = ToolStripItemOverflow.Never;
            pageTabMenu.Items.Add(deletePageMenuItem);
            deletePageMenuItem.Text = "删除页面";
            emptyPagesLabel.Dock = DockStyle.Fill;
            emptyPagesLabel.Margin = Padding.Empty;
            emptyPagesLabel.Text = "暂无运行界面";
            emptyPagesLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyPagesLabel.ForeColor = Color.FromArgb(183, 187, 193);
            emptyPagesLabel.BackColor = Color.FromArgb(38, 40, 43);
            emptyPagesLabel.Visible = false;
            workspace.Dock = DockStyle.Fill;
            workspace.Margin = Padding.Empty;
            workspace.BackColor = Color.FromArgb(38, 40, 43);
            workspace.AutoScroll = true;
            workspace.Controls.Add(canvas);
            workspace.Controls.Add(emptyPagesLabel);
            canvas.Location = Point.Empty;
            canvas.Size = new Size(800, 500);
            rightLayout.Dock = DockStyle.Fill;
            rightLayout.Margin = Padding.Empty;
            rightLayout.BackColor = Color.FromArgb(244, 245, 247);
            rightLayout.ColumnCount = 1;
            rightLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rightLayout.RowCount = 6;
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            rightLayout.Controls.Add(propertiesLabel, 0, 0);
            rightLayout.Controls.Add(bindingActions, 0, 1);
            rightLayout.Controls.Add(runProcessActions, 0, 2);
            rightLayout.Controls.Add(roiBindingActions, 0, 3);
            rightLayout.Controls.Add(propertyGrid, 0, 4);
            rightLayout.Controls.Add(imageActions, 0, 5);
            propertiesLabel.Dock = DockStyle.Fill;
            propertiesLabel.Text = "页面属性";
            propertiesLabel.TextAlign = ContentAlignment.MiddleLeft;
            propertiesLabel.Padding = new Padding(12, 0, 0, 0);
            propertiesLabel.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
            propertiesLabel.ForeColor = Color.FromArgb(177, 94, 22);
            bindingActions.Dock = DockStyle.Fill;
            bindingActions.Margin = Padding.Empty;
            bindingActions.Padding = new Padding(8, 0, 8, 8);
            bindingActions.ColumnCount = 3;
            bindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            bindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            bindingActions.RowCount = 2;
            bindingActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            bindingActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bindingActions.Controls.Add(bindingLabel, 0, 0);
            bindingActions.SetColumnSpan(bindingLabel, 3);
            bindingActions.Controls.Add(bindingPath, 0, 1);
            bindingActions.Controls.Add(chooseBindingButton, 1, 1);
            bindingActions.Controls.Add(clearBindingButton, 2, 1);
            bindingLabel.Dock = DockStyle.Fill;
            bindingLabel.Margin = Padding.Empty;
            bindingLabel.Text = "数据源";
            bindingLabel.TextAlign = ContentAlignment.MiddleLeft;
            bindingLabel.ForeColor = Color.FromArgb(61, 65, 71);
            bindingPath.Dock = DockStyle.Fill;
            bindingPath.Margin = new Padding(0, 3, 4, 3);
            bindingPath.ReadOnly = true;
            bindingPath.BackColor = Color.FromArgb(250, 250, 251);
            bindingPath.ForeColor = Color.FromArgb(56, 62, 68);
            bindingPath.BorderStyle = BorderStyle.FixedSingle;
            bindingPath.Text = "未绑定";
            bindingPath.AccessibleName = "数据源路径";
            chooseBindingButton.Dock = DockStyle.Fill;
            chooseBindingButton.Margin = new Padding(0, 2, 2, 2);
            chooseBindingButton.FlatStyle = FlatStyle.Flat;
            chooseBindingButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            chooseBindingButton.AccessibleName = "选择数据源";
            chooseBindingButton.Enabled = false;
            windowToolTip.SetToolTip(chooseBindingButton, "选择数据源");
            clearBindingButton.Dock = DockStyle.Fill;
            clearBindingButton.Margin = new Padding(2, 2, 0, 2);
            clearBindingButton.FlatStyle = FlatStyle.Flat;
            clearBindingButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            clearBindingButton.AccessibleName = "清除数据源";
            clearBindingButton.Enabled = false;
            windowToolTip.SetToolTip(clearBindingButton, "清除数据源");
            roiBindingActions.Dock = DockStyle.Fill;
            roiBindingActions.Margin = Padding.Empty;
            roiBindingActions.Padding = new Padding(8, 0, 8, 8);
            roiBindingActions.ColumnCount = 3;
            roiBindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            roiBindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            roiBindingActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            roiBindingActions.RowCount = 2;
            roiBindingActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            roiBindingActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            roiBindingActions.Controls.Add(roiBindingLabel, 0, 0);
            roiBindingActions.SetColumnSpan(roiBindingLabel, 3);
            roiBindingActions.Controls.Add(roiBindingPath, 0, 1);
            roiBindingActions.Controls.Add(chooseRoiBindingButton, 1, 1);
            roiBindingActions.Controls.Add(clearRoiBindingButton, 2, 1);
            roiBindingLabel.Dock = DockStyle.Fill;
            roiBindingLabel.Margin = Padding.Empty;
            roiBindingLabel.Text = "ROI源";
            roiBindingLabel.TextAlign = ContentAlignment.MiddleLeft;
            roiBindingLabel.ForeColor = Color.FromArgb(61, 65, 71);
            roiBindingPath.Dock = DockStyle.Fill;
            roiBindingPath.Margin = new Padding(0, 3, 4, 3);
            roiBindingPath.ReadOnly = true;
            roiBindingPath.BackColor = Color.FromArgb(250, 250, 251);
            roiBindingPath.ForeColor = Color.FromArgb(56, 62, 68);
            roiBindingPath.BorderStyle = BorderStyle.FixedSingle;
            roiBindingPath.Text = "未绑定";
            roiBindingPath.AccessibleName = "ROI源路径";
            chooseRoiBindingButton.Dock = DockStyle.Fill;
            chooseRoiBindingButton.Margin = new Padding(0, 2, 2, 2);
            chooseRoiBindingButton.FlatStyle = FlatStyle.Flat;
            chooseRoiBindingButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            chooseRoiBindingButton.AccessibleName = "选择ROI源";
            chooseRoiBindingButton.Enabled = false;
            windowToolTip.SetToolTip(chooseRoiBindingButton, "选择ROI源");
            clearRoiBindingButton.Dock = DockStyle.Fill;
            clearRoiBindingButton.Margin = new Padding(2, 2, 0, 2);
            clearRoiBindingButton.FlatStyle = FlatStyle.Flat;
            clearRoiBindingButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            clearRoiBindingButton.AccessibleName = "清除ROI源";
            clearRoiBindingButton.Enabled = false;
            windowToolTip.SetToolTip(clearRoiBindingButton, "清除ROI源");
            runProcessActions.Dock = DockStyle.Fill;
            runProcessActions.Margin = Padding.Empty;
            runProcessActions.Padding = new Padding(8, 0, 8, 8);
            runProcessActions.ColumnCount = 1;
            runProcessActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            runProcessActions.RowCount = 2;
            runProcessActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            runProcessActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            runProcessActions.Controls.Add(runProcessLabel, 0, 0);
            runProcessActions.Controls.Add(runProcessCombo, 0, 1);
            runProcessLabel.Dock = DockStyle.Fill;
            runProcessLabel.Margin = Padding.Empty;
            runProcessLabel.Text = "运行流程";
            runProcessLabel.TextAlign = ContentAlignment.MiddleLeft;
            runProcessLabel.ForeColor = Color.FromArgb(61, 65, 71);
            runProcessCombo.Dock = DockStyle.Fill;
            runProcessCombo.Margin = new Padding(0, 3, 0, 3);
            runProcessCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            runProcessCombo.DisplayMember = "Name";
            runProcessCombo.ValueMember = "ProcessId";
            runProcessCombo.BackColor = Color.FromArgb(250, 250, 251);
            runProcessCombo.ForeColor = Color.FromArgb(56, 62, 68);
            runProcessCombo.AccessibleName = "运行流程";
            runProcessCombo.IntegralHeight = false;
            runProcessCombo.DropDownHeight = 240;
            windowToolTip.SetToolTip(runProcessCombo, "运行流程");
            propertyGrid.Dock = DockStyle.Fill;
            propertyGrid.Margin = new Padding(4, 0, 4, 0);
            propertyGrid.ToolbarVisible = false;
            propertyGrid.HelpVisible = false;
            propertyGrid.PropertySort = PropertySort.Categorized;
            propertyGrid.ViewBackColor = Color.FromArgb(250, 250, 251);
            propertyGrid.LineColor = Color.FromArgb(232, 234, 237);
            propertyGrid.CategoryForeColor = Color.FromArgb(61, 65, 71);
            imageActions.Dock = DockStyle.Fill;
            imageActions.Padding = new Padding(8, 4, 0, 0);
            imageActions.Controls.Add(loadImageButton);
            imageActions.Controls.Add(clearImageButton);
            loadImageButton.Text = "载入图片";
            loadImageButton.Size = new Size(110, 30);
            loadImageButton.FlatStyle = FlatStyle.Flat;
            loadImageButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            clearImageButton.Text = "清除图片";
            clearImageButton.Size = new Size(110, 30);
            clearImageButton.FlatStyle = FlatStyle.Flat;
            clearImageButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            statusStrip.Dock = DockStyle.Fill;
            statusStrip.BackColor = Color.FromArgb(238, 240, 243);
            statusStrip.SizingGrip = false;
            statusStrip.Items.AddRange(new ToolStripItem[] { stateLabel, solutionLabel });
            stateLabel.Spring = true;
            stateLabel.TextAlign = ContentAlignment.MiddleLeft;
            solutionLabel.Text = "未关联方案";
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1440, 860);
            MinimumSize = new Size(1000, 640);
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(6);
            BackColor = Color.FromArgb(33, 35, 38);
            Font = new Font("Microsoft YaHei UI", 9);
            Controls.Add(rootLayout);
            Text = "运行界面设计";
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            toolbar.ResumeLayout(false);
            toolbar.PerformLayout();
            leftLayout.ResumeLayout(false);
            bindingActions.ResumeLayout(false);
            bindingActions.PerformLayout();
            roiBindingActions.ResumeLayout(false);
            roiBindingActions.PerformLayout();
            pageTabs.ResumeLayout(false);
            centerLayout.ResumeLayout(false);
            runProcessActions.ResumeLayout(false);
            rightLayout.ResumeLayout(false);
            contentLayout.ResumeLayout(false);
            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            titleBar.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
