using System.Drawing;
using System.Windows.Forms;
namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>仅包含创建参数、轮廓编辑和保存的独立模板设计器。</summary>
    partial class ContourTemplateEditor
    {
        /// <summary>模板编辑主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>创建参数容器。</summary>
        private TableLayoutPanel parameterLayout;
        /// <summary>创建参数网格。</summary>
        private TableLayoutPanel modelParametersFlow;
        /// <summary>编辑操作工具栏。</summary>
        private FlowLayoutPanel subscriptionPanel;
        /// <summary>模板保存与取消操作。</summary>
        private FlowLayoutPanel footerActions;
        /// <summary>自动对比度布局。</summary>
        private FlowLayoutPanel contrastModePanel;
        /// <summary>创建参数分组。</summary>
        private GroupBox modelParametersGroup;
        /// <summary>加载模板图像按钮。</summary>
        private Button loadImageButton;
        /// <summary>创建模板按钮。</summary>
        private Button createModelButton;
        /// <summary>保存模板按钮。</summary>
        private Button saveButton;
        /// <summary>取消编辑按钮。</summary>
        private Button closeButton;
        /// <summary>画笔直径标签。</summary>
        private Label brushSizeLabel;
        /// <summary>编辑状态提示。</summary>
        private Label statusLabel;
        /// <summary>建模耗时显示。</summary>
        private Label modelTimeLabel;
        /// <summary>金字塔层数标签。</summary>
        private Label modelLevelsLabel;
        /// <summary>创建起始角标签。</summary>
        private Label modelAngleStartLabel;
        /// <summary>创建结束角标签。</summary>
        private Label modelAngleEndLabel;
        /// <summary>角度步长标签。</summary>
        private Label angleStepLabel;
        /// <summary>边缘阈值标签。</summary>
        private Label contrastLabel;
        /// <summary>搜索最小对比度标签。</summary>
        private Label minimumContrastLabel;
        /// <summary>特征数标签。</summary>
        private Label featureCountLabel;
        /// <summary>极性标签。</summary>
        private Label metricLabel;
        /// <summary>画笔直径输入。</summary>
        private NumericUpDown brushSizeNumeric;
        /// <summary>创建金字塔层数输入。</summary>
        private NumericUpDown modelLevelsNumeric;
        /// <summary>创建起始角输入。</summary>
        private NumericUpDown modelAngleStartNumeric;
        /// <summary>创建结束角输入。</summary>
        private NumericUpDown modelAngleEndNumeric;
        /// <summary>创建角度步长输入。</summary>
        private NumericUpDown angleStepNumeric;
        /// <summary>边缘阈值输入。</summary>
        private NumericUpDown contrastNumeric;
        /// <summary>搜索最小对比度输入。</summary>
        private NumericUpDown minimumContrastNumeric;
        /// <summary>特征数输入。</summary>
        private NumericUpDown featureCountNumeric;
        /// <summary>自动对比度开关。</summary>
        private CheckBox autoContrastCheckBox;
        /// <summary>极性选择。</summary>
        private ComboBox metricComboBox;
        /// <summary>模板框选和涂抹画布。</summary>
        private ImageCanvas imageCanvas;
        /// <summary>右侧导航工具栏与单一画布的紧凑布局。</summary>
        private TableLayoutPanel imageLayout;
        /// <summary>缩放操作栏，始终可在涂抹期间使用。</summary>
        private FlowLayoutPanel zoomActions;
        /// <summary>放大当前图像。</summary>
        private Button zoomInButton;
        /// <summary>缩小当前图像。</summary>
        private Button zoomOutButton;
        /// <summary>恢复整图适应画布。</summary>
        private Button fitImageButton;
        /// <summary>实际图像显示比例。</summary>
        private Label zoomLabel;
        /// <summary>鼠标导航操作提示。</summary>
        private Label navigationHint;
        /// <summary>模板图像选择对话框。</summary>
        private OpenFileDialog imageOpenFileDialog;

        /// <summary>区域绘制工具栏。</summary>
        private ToolStrip regionTools;
        /// <summary>选择区域。</summary>
        private ToolStripButton selectRegionButton;
        /// <summary>绘制矩形。</summary>
        private ToolStripButton rectangleRegionButton;
        /// <summary>绘制椭圆。</summary>
        private ToolStripButton ellipseRegionButton;
        /// <summary>绘制扇形。</summary>
        private ToolStripButton sectorRegionButton;
        /// <summary>绘制多边形。</summary>
        private ToolStripButton polygonRegionButton;
        /// <summary>画笔取样。</summary>
        private ToolStripButton brushRegionButton;
        /// <summary>擦除轮廓。</summary>
        private ToolStripButton eraseRegionButton;
        /// <summary>删除选区。</summary>
        private ToolStripButton deleteRegionButton;
        /// <summary>清空区域。</summary>
        private ToolStripButton clearRegionsButton;
        /// <summary>撤销区域编辑。</summary>
        private ToolStripButton undoRegionButton;
        /// <summary>重做区域编辑。</summary>
        private ToolStripButton redoRegionButton;
        /// <summary>区域列表分组。</summary>
        private GroupBox regionListGroup;
        /// <summary>按绘制顺序显示区域。</summary>
        private ListBox regionList;
        /// <summary>区域与创建参数分页。</summary>
        private TabControl editorParameterTabs;
        /// <summary>选区几何参数页。</summary>
        private TabPage regionParameterPage;
        /// <summary>轮廓创建参数页。</summary>
        private TabPage modelParameterPage;
        /// <summary>选区几何参数布局。</summary>
        private TableLayoutPanel regionProperties;
        /// <summary>是否参与组合建模。</summary>
        private CheckBox regionEnabledCheckBox;
        /// <summary>控制柄操作提示。</summary>
        private Label regionHelpLabel;
        /// <summary>中心横坐标标签。</summary>
        private Label regionXLabel;
        /// <summary>中心横坐标输入。</summary>
        private NumericUpDown regionXNumeric;
        /// <summary>中心纵坐标标签。</summary>
        private Label regionYLabel;
        /// <summary>中心纵坐标输入。</summary>
        private NumericUpDown regionYNumeric;
        /// <summary>宽度标签。</summary>
        private Label regionWidthLabel;
        /// <summary>宽度输入。</summary>
        private NumericUpDown regionWidthNumeric;
        /// <summary>高度标签。</summary>
        private Label regionHeightLabel;
        /// <summary>高度输入。</summary>
        private NumericUpDown regionHeightNumeric;
        /// <summary>旋转角度标签。</summary>
        private Label regionAngleLabel;
        /// <summary>旋转角度输入。</summary>
        private NumericUpDown regionAngleNumeric;
        /// <summary>扇形起始角标签。</summary>
        private Label sectorStartLabel;
        /// <summary>扇形起始角输入。</summary>
        private NumericUpDown sectorStartNumeric;
        /// <summary>扇形张角标签。</summary>
        private Label sectorSweepLabel;
        /// <summary>扇形张角输入。</summary>
        private NumericUpDown sectorSweepNumeric;

        /// <summary>释放编辑窗口独占的图像及模型。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _matcher?.Dispose(); _image?.Dispose(); _modelImage?.Dispose(); imageOpenFileDialog?.Dispose(); }
            base.Dispose(disposing);
        }
        /// <summary>创建模板编辑界面，所有控件及布局均保留在设计器文件中。</summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            rootLayout = new TableLayoutPanel();
            parameterLayout = new TableLayoutPanel();
            modelParametersFlow = new TableLayoutPanel();
            subscriptionPanel = new FlowLayoutPanel();
            footerActions = new FlowLayoutPanel();
            contrastModePanel = new FlowLayoutPanel();
            modelParametersGroup = new GroupBox();
            loadImageButton = new Button();
            createModelButton = new Button();
            saveButton = new Button();
            closeButton = new Button();
            brushSizeLabel = new Label();
            statusLabel = new Label();
            modelTimeLabel = new Label();
            modelLevelsLabel = new Label();
            modelAngleStartLabel = new Label();
            modelAngleEndLabel = new Label();
            angleStepLabel = new Label();
            contrastLabel = new Label();
            minimumContrastLabel = new Label();
            featureCountLabel = new Label();
            metricLabel = new Label();
            brushSizeNumeric = new NumericUpDown();
            modelLevelsNumeric = new NumericUpDown();
            modelAngleStartNumeric = new NumericUpDown();
            modelAngleEndNumeric = new NumericUpDown();
            angleStepNumeric = new NumericUpDown();
            contrastNumeric = new NumericUpDown();
            minimumContrastNumeric = new NumericUpDown();
            featureCountNumeric = new NumericUpDown();
            autoContrastCheckBox = new CheckBox();
            metricComboBox = new ComboBox();
            imageCanvas = new ImageCanvas();
            imageLayout = new TableLayoutPanel(); zoomActions = new FlowLayoutPanel();
            zoomInButton = new Button(); zoomOutButton = new Button(); fitImageButton = new Button();
            zoomLabel = new Label(); navigationHint = new Label();
            imageOpenFileDialog = new OpenFileDialog();

            regionTools = new ToolStrip();
            selectRegionButton = new ToolStripButton();
            rectangleRegionButton = new ToolStripButton();
            ellipseRegionButton = new ToolStripButton();
            sectorRegionButton = new ToolStripButton();
            polygonRegionButton = new ToolStripButton();
            brushRegionButton = new ToolStripButton();
            eraseRegionButton = new ToolStripButton();
            deleteRegionButton = new ToolStripButton();
            clearRegionsButton = new ToolStripButton();
            undoRegionButton = new ToolStripButton();
            redoRegionButton = new ToolStripButton();
            regionListGroup = new GroupBox();
            regionList = new ListBox();
            editorParameterTabs = new TabControl();
            regionParameterPage = new TabPage();
            modelParameterPage = new TabPage();
            regionProperties = new TableLayoutPanel();
            regionEnabledCheckBox = new CheckBox();
            regionHelpLabel = new Label();
            regionXLabel = new Label();
            regionXNumeric = new NumericUpDown();
            regionYLabel = new Label();
            regionYNumeric = new NumericUpDown();
            regionWidthLabel = new Label();
            regionWidthNumeric = new NumericUpDown();
            regionHeightLabel = new Label();
            regionHeightNumeric = new NumericUpDown();
            regionAngleLabel = new Label();
            regionAngleNumeric = new NumericUpDown();
            sectorStartLabel = new Label();
            sectorStartNumeric = new NumericUpDown();
            sectorSweepLabel = new Label();
            sectorSweepNumeric = new NumericUpDown();
            regionTools.Dock = DockStyle.Fill; regionTools.GripStyle = ToolStripGripStyle.Hidden; regionTools.BackColor = Color.White; regionTools.Padding = new Padding(2);
            selectRegionButton.Text = "选择"; selectRegionButton.Tag = TemplateRegionKind.选择; selectRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; selectRegionButton.ToolTipText = "选择区域，拖动中心移动、方形柄缩放、圆形柄旋转"; selectRegionButton.Click += RegionTool_Click;
            rectangleRegionButton.Text = "矩形"; rectangleRegionButton.Tag = TemplateRegionKind.矩形; rectangleRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; rectangleRegionButton.ToolTipText = "拖动绘制矩形，松开生成"; rectangleRegionButton.Click += RegionTool_Click;
            ellipseRegionButton.Text = "椭圆"; ellipseRegionButton.Tag = TemplateRegionKind.椭圆; ellipseRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; ellipseRegionButton.ToolTipText = "拖动绘制椭圆，松开生成"; ellipseRegionButton.Click += RegionTool_Click;
            sectorRegionButton.Text = "扇形"; sectorRegionButton.Tag = TemplateRegionKind.扇形; sectorRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; sectorRegionButton.ToolTipText = "拖动绘制扇形，在区域参数中调整起始角和张角"; sectorRegionButton.Click += RegionTool_Click;
            polygonRegionButton.Text = "多边形"; polygonRegionButton.Tag = TemplateRegionKind.多边形; polygonRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; polygonRegionButton.ToolTipText = "逐点绘制，双击、右键或回车闭合"; polygonRegionButton.Click += RegionTool_Click;
            brushRegionButton.Text = "画笔"; brushRegionButton.Tag = TemplateRegionKind.画笔; brushRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; brushRegionButton.ToolTipText = "按住左键涂画取样区域，松开生成"; brushRegionButton.Click += RegionTool_Click;
            eraseRegionButton.Text = "擦除"; eraseRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; eraseRegionButton.Click += EraseRegionButton_Click;
            deleteRegionButton.Text = "删除"; deleteRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; deleteRegionButton.Click += DeleteRegionButton_Click;
            clearRegionsButton.Text = "清空"; clearRegionsButton.DisplayStyle = ToolStripItemDisplayStyle.Text; clearRegionsButton.Click += ClearRegionsButton_Click;
            undoRegionButton.Text = "撤销"; undoRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; undoRegionButton.Click += UndoRegionButton_Click;
            redoRegionButton.Text = "重做"; redoRegionButton.DisplayStyle = ToolStripItemDisplayStyle.Text; redoRegionButton.Click += RedoRegionButton_Click;
            regionTools.Items.AddRange(new ToolStripItem[] { selectRegionButton, rectangleRegionButton, ellipseRegionButton, sectorRegionButton, polygonRegionButton, brushRegionButton, new ToolStripSeparator(), eraseRegionButton, deleteRegionButton, clearRegionsButton, new ToolStripSeparator(), undoRegionButton, redoRegionButton });
            regionListGroup.Text = "区域列表 · 合并取样"; regionListGroup.Dock = DockStyle.Fill;
            regionList.Dock = DockStyle.Fill; regionList.BorderStyle = BorderStyle.None; regionList.IntegralHeight = false; regionList.ItemHeight = 24;
            regionList.SelectedIndexChanged += RegionList_SelectedIndexChanged; regionListGroup.Controls.Add(regionList);
            editorParameterTabs.Dock = DockStyle.Fill; regionParameterPage.Text = "区域参数"; modelParameterPage.Text = "创建参数";
            editorParameterTabs.Controls.Add(regionParameterPage); editorParameterTabs.Controls.Add(modelParameterPage);
            regionProperties.Dock = DockStyle.Fill; regionProperties.ColumnCount = 2; regionProperties.RowCount = 9; regionProperties.Padding = new Padding(8);
            regionProperties.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); regionProperties.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            regionEnabledCheckBox.Text = "参与组合建模"; regionEnabledCheckBox.AutoSize = true; regionEnabledCheckBox.Checked = true; regionEnabledCheckBox.CheckedChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionEnabledCheckBox, 0, 0); regionProperties.SetColumnSpan(regionEnabledCheckBox, 2);
            regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            regionXLabel.Text = "中心横坐标"; regionXLabel.AutoSize = true; regionXLabel.Anchor = AnchorStyles.Left;
            regionXNumeric.Minimum = -1000000; regionXNumeric.Maximum = 1000000; regionXNumeric.Value = 0; regionXNumeric.DecimalPlaces = 1; regionXNumeric.Dock = DockStyle.Fill; regionXNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionXLabel, 0, 1); regionProperties.Controls.Add(regionXNumeric, 1, 1); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            regionYLabel.Text = "中心纵坐标"; regionYLabel.AutoSize = true; regionYLabel.Anchor = AnchorStyles.Left;
            regionYNumeric.Minimum = -1000000; regionYNumeric.Maximum = 1000000; regionYNumeric.Value = 0; regionYNumeric.DecimalPlaces = 1; regionYNumeric.Dock = DockStyle.Fill; regionYNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionYLabel, 0, 2); regionProperties.Controls.Add(regionYNumeric, 1, 2); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            regionWidthLabel.Text = "宽度"; regionWidthLabel.AutoSize = true; regionWidthLabel.Anchor = AnchorStyles.Left;
            regionWidthNumeric.Minimum = 1; regionWidthNumeric.Maximum = 1000000; regionWidthNumeric.Value = 10; regionWidthNumeric.DecimalPlaces = 1; regionWidthNumeric.Dock = DockStyle.Fill; regionWidthNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionWidthLabel, 0, 3); regionProperties.Controls.Add(regionWidthNumeric, 1, 3); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            regionHeightLabel.Text = "高度"; regionHeightLabel.AutoSize = true; regionHeightLabel.Anchor = AnchorStyles.Left;
            regionHeightNumeric.Minimum = 1; regionHeightNumeric.Maximum = 1000000; regionHeightNumeric.Value = 10; regionHeightNumeric.DecimalPlaces = 1; regionHeightNumeric.Dock = DockStyle.Fill; regionHeightNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionHeightLabel, 0, 4); regionProperties.Controls.Add(regionHeightNumeric, 1, 4); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            regionAngleLabel.Text = "旋转角度"; regionAngleLabel.AutoSize = true; regionAngleLabel.Anchor = AnchorStyles.Left;
            regionAngleNumeric.Minimum = -180; regionAngleNumeric.Maximum = 180; regionAngleNumeric.Value = 0; regionAngleNumeric.DecimalPlaces = 1; regionAngleNumeric.Dock = DockStyle.Fill; regionAngleNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(regionAngleLabel, 0, 5); regionProperties.Controls.Add(regionAngleNumeric, 1, 5); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            sectorStartLabel.Text = "扇形起始角"; sectorStartLabel.AutoSize = true; sectorStartLabel.Anchor = AnchorStyles.Left;
            sectorStartNumeric.Minimum = -360; sectorStartNumeric.Maximum = 360; sectorStartNumeric.Value = -45; sectorStartNumeric.DecimalPlaces = 1; sectorStartNumeric.Dock = DockStyle.Fill; sectorStartNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(sectorStartLabel, 0, 6); regionProperties.Controls.Add(sectorStartNumeric, 1, 6); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            sectorSweepLabel.Text = "扇形张角"; sectorSweepLabel.AutoSize = true; sectorSweepLabel.Anchor = AnchorStyles.Left;
            sectorSweepNumeric.Minimum = 1; sectorSweepNumeric.Maximum = 360; sectorSweepNumeric.Value = 90; sectorSweepNumeric.DecimalPlaces = 1; sectorSweepNumeric.Dock = DockStyle.Fill; sectorSweepNumeric.ValueChanged += RegionProperty_ValueChanged;
            regionProperties.Controls.Add(sectorSweepLabel, 0, 7); regionProperties.Controls.Add(sectorSweepNumeric, 1, 7); regionProperties.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            regionHelpLabel.Text = "拖动区域移动，方形柄缩放，圆形柄旋转。\r\n多个区域共同组成一个模板。"; regionHelpLabel.Dock = DockStyle.Fill; regionHelpLabel.ForeColor = Color.DimGray;
            regionProperties.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); regionProperties.Controls.Add(regionHelpLabel, 0, 8); regionProperties.SetColumnSpan(regionHelpLabel, 2);
            regionParameterPage.Controls.Add(regionProperties);
            rootLayout.Dock = DockStyle.Fill; rootLayout.ColumnCount = 2; rootLayout.RowCount = 4; rootLayout.Padding = new Padding(8);
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            subscriptionPanel.Dock = DockStyle.Fill; subscriptionPanel.Margin = Padding.Empty; subscriptionPanel.WrapContents = false;
            loadImageButton.Text = "选择其他图像"; loadImageButton.Size = new Size(110, 30); loadImageButton.Click += LoadImageButton_Click;
            createModelButton.Text = "创建模板"; createModelButton.Size = new Size(90, 30); createModelButton.Click += CreateModelButton_Click;
            brushSizeLabel.Text = "画笔直径"; brushSizeLabel.AutoSize = true; brushSizeLabel.Margin = new Padding(16, 8, 3, 0);
            brushSizeNumeric.Minimum = 1; brushSizeNumeric.Maximum = 200; brushSizeNumeric.Value = 16; brushSizeNumeric.Width = 64;
            brushSizeNumeric.Margin = new Padding(3, 5, 3, 3); brushSizeNumeric.ValueChanged += BrushSizeNumeric_ValueChanged;
            modelTimeLabel.AutoSize = true; modelTimeLabel.Margin = new Padding(20, 8, 3, 0); modelTimeLabel.Text = "建模耗时：--";
            subscriptionPanel.Controls.AddRange(new Control[] { loadImageButton, createModelButton, brushSizeLabel, brushSizeNumeric, modelTimeLabel });
            footerActions.Dock = DockStyle.Fill; footerActions.Margin = Padding.Empty; footerActions.FlowDirection = FlowDirection.RightToLeft;
            saveButton.Text = "确定"; saveButton.Size = new Size(88, 30); saveButton.Click += SaveButton_Click; footerActions.Controls.Add(saveButton);
            closeButton.Text = "取消"; closeButton.Size = new Size(88, 30); closeButton.Click += CloseButton_Click; footerActions.Controls.Add(closeButton);
            parameterLayout.Dock = DockStyle.Fill; parameterLayout.Margin = new Padding(10, 0, 0, 0); parameterLayout.ColumnCount = 1; parameterLayout.RowCount = 2;
            parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); parameterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            parameterLayout.Controls.Add(regionListGroup, 0, 0); parameterLayout.Controls.Add(editorParameterTabs, 0, 1);
            modelParameterPage.Controls.Add(modelParametersGroup);
        modelParametersGroup.Controls.Add(modelParametersFlow);
        modelParametersGroup.Dock = DockStyle.Fill;
        modelParametersGroup.Text = "模板参数";
        //
        // modelParametersFlow
        //
        modelParametersFlow.Controls.Add(modelLevelsLabel, 0, 0); modelParametersFlow.Controls.Add(modelLevelsNumeric, 1, 0);
        modelParametersFlow.Controls.Add(modelAngleStartLabel, 0, 1); modelParametersFlow.Controls.Add(modelAngleStartNumeric, 1, 1);
        modelParametersFlow.Controls.Add(modelAngleEndLabel, 0, 2); modelParametersFlow.Controls.Add(modelAngleEndNumeric, 1, 2);
        modelParametersFlow.Controls.Add(angleStepLabel, 0, 3); modelParametersFlow.Controls.Add(angleStepNumeric, 1, 3);
        modelParametersFlow.Controls.Add(contrastModePanel, 0, 4); modelParametersFlow.Controls.Add(contrastNumeric, 1, 4);
        modelParametersFlow.Controls.Add(minimumContrastLabel, 0, 5); modelParametersFlow.Controls.Add(minimumContrastNumeric, 1, 5);
        modelParametersFlow.Controls.Add(featureCountLabel, 0, 6); modelParametersFlow.Controls.Add(featureCountNumeric, 1, 6);
        modelParametersFlow.Controls.Add(metricLabel, 0, 7); modelParametersFlow.Controls.Add(metricComboBox, 1, 7);
        modelParametersFlow.ColumnCount = 2;
        modelParametersFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        modelParametersFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        modelParametersFlow.RowCount = 8;
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelParametersFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));
        modelLevelsLabel.Anchor = AnchorStyles.Left;
        modelLevelsNumeric.Dock = DockStyle.Fill;
        modelAngleStartLabel.Anchor = AnchorStyles.Left;
        modelAngleStartNumeric.Dock = DockStyle.Fill;
        modelAngleEndLabel.Anchor = AnchorStyles.Left;
        modelAngleEndNumeric.Dock = DockStyle.Fill;
        angleStepLabel.Anchor = AnchorStyles.Left;
        angleStepNumeric.Dock = DockStyle.Fill;
        contrastLabel.Anchor = AnchorStyles.Left;
        contrastNumeric.Dock = DockStyle.Fill;
        minimumContrastLabel.Anchor = AnchorStyles.Left;
        minimumContrastNumeric.Dock = DockStyle.Fill;
        featureCountLabel.Anchor = AnchorStyles.Left;
        featureCountNumeric.Dock = DockStyle.Fill;
        metricLabel.Anchor = AnchorStyles.Left;
        metricComboBox.Dock = DockStyle.Fill;
        modelParametersFlow.Dock = DockStyle.Fill;
        modelParametersFlow.Padding = new Padding(6);
        //
        // modelLevelsLabel
        //
        modelLevelsLabel.AutoSize = true;
        modelLevelsLabel.Margin = new Padding(3, 8, 3, 0);
        modelLevelsLabel.Text = "金字塔层数(0自动)";
        //
        // modelLevelsNumeric
        //
        modelLevelsNumeric.Maximum = 6;
        modelLevelsNumeric.Size = new Size(58, 23);
        //
        // modelAngleStartLabel
        //
        modelAngleStartLabel.AutoSize = true;
        modelAngleStartLabel.Margin = new Padding(3, 8, 3, 0);
        modelAngleStartLabel.Text = "起始角(°)";
        //
        // modelAngleStartNumeric
        //
        modelAngleStartNumeric.DecimalPlaces = 1;
        modelAngleStartNumeric.Maximum = 180;
        modelAngleStartNumeric.Minimum = -180;
        modelAngleStartNumeric.Size = new Size(70, 23);
        modelAngleStartNumeric.Value = -30;
        //
        // modelAngleEndLabel
        //
        modelAngleEndLabel.AutoSize = true;
        modelAngleEndLabel.Margin = new Padding(3, 8, 3, 0);
        modelAngleEndLabel.Text = "结束角(°)";
        //
        // modelAngleEndNumeric
        //
        modelAngleEndNumeric.DecimalPlaces = 1;
        modelAngleEndNumeric.Maximum = 180;
        modelAngleEndNumeric.Minimum = -180;
        modelAngleEndNumeric.Size = new Size(70, 23);
        modelAngleEndNumeric.Value = 30;
        //
        // angleStepLabel
        //
        angleStepLabel.AutoSize = true;
        angleStepLabel.Margin = new Padding(3, 8, 3, 0);
        angleStepLabel.Text = "角度步长(°)";
        //
        // angleStepNumeric
        //
        angleStepNumeric.DecimalPlaces = 1;
        angleStepNumeric.Increment = 0.5M;
        angleStepNumeric.Maximum = 180;
        angleStepNumeric.Minimum = 0.1M;
        angleStepNumeric.Size = new Size(70, 23);
        angleStepNumeric.Value = 1;
        //
        // contrastLabel
        //
        contrastLabel.AutoSize = true;
        contrastLabel.Margin = new Padding(0, 6, 0, 0);
        contrastLabel.Text = "阈值";
        contrastModePanel.Name = "contrastModePanel";
        contrastModePanel.Dock = DockStyle.Fill;
        contrastModePanel.Margin = new Padding(3, 0, 0, 0);
        contrastModePanel.WrapContents = false;
        contrastModePanel.Controls.Add(contrastLabel);
        contrastModePanel.Controls.Add(autoContrastCheckBox);
        autoContrastCheckBox.Name = "autoContrastCheckBox";
        autoContrastCheckBox.Text = "自动";
        autoContrastCheckBox.AutoSize = true;
        autoContrastCheckBox.Margin = new Padding(2, 3, 0, 0);
        autoContrastCheckBox.Checked = true;
        autoContrastCheckBox.CheckedChanged += AutoContrastCheckBox_CheckedChanged;
        //
        // contrastNumeric
        //
        contrastNumeric.DecimalPlaces = 1;
        contrastNumeric.Maximum = 1024;
        contrastNumeric.Minimum = 1;
        contrastNumeric.Size = new Size(74, 23);
        contrastNumeric.Value = 20;
        contrastNumeric.Enabled = false;
        //
        // minimumContrastLabel
        //
        minimumContrastLabel.AutoSize = true;
        minimumContrastLabel.Margin = new Padding(3, 8, 3, 0);
        minimumContrastLabel.Text = "搜索最小对比度";
        //
        // minimumContrastNumeric
        //
        minimumContrastNumeric.DecimalPlaces = 1;
        minimumContrastNumeric.Maximum = 1024;
        minimumContrastNumeric.Size = new Size(74, 23);
        minimumContrastNumeric.Value = 10;
        //
        // featureCountLabel
        //
        featureCountLabel.AutoSize = true;
        featureCountLabel.Margin = new Padding(3, 8, 3, 0);
        featureCountLabel.Text = "特征数(0自动)";
        //
        // featureCountNumeric
        //
        featureCountNumeric.Increment = 50;
        featureCountNumeric.Maximum = 10000;
        featureCountNumeric.Size = new Size(78, 23);
        featureCountNumeric.Value = 300;
        //
        // metricLabel
        //
        metricLabel.AutoSize = true;
        metricLabel.Margin = new Padding(3, 8, 3, 0);
        metricLabel.Text = "梯度极性";
        //
        // metricComboBox
        //
        metricComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        metricComboBox.Items.AddRange(new object[] { "使用极性", "忽略极性" });
        metricComboBox.SelectedIndex = 0;
        metricComboBox.Size = new Size(96, 23);

            modelParametersGroup.Text = "轮廓提取";
            imageCanvas.Dock = DockStyle.Fill; imageCanvas.Margin = Padding.Empty;
            imageCanvas.ConfirmRequested += ImageCanvas_ConfirmRequested; imageCanvas.EditCancelled += ImageCanvas_EditCancelled; imageCanvas.EditChanged += ImageCanvas_EditChanged;
            imageCanvas.ViewChanged += ImageCanvas_ViewChanged;
            statusLabel.Dock = DockStyle.Fill; statusLabel.Text = "选择顶部工具绘制区域，松开生成；多个区域组合创建模板"; statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true; statusLabel.ForeColor = Color.DimGray; modelTimeLabel.ForeColor = Color.DimGray;
            imageLayout.Dock = DockStyle.Fill; imageLayout.Margin = Padding.Empty; imageLayout.ColumnCount = 1; imageLayout.RowCount = 3;
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); imageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            zoomActions.Dock = DockStyle.Fill; zoomActions.Margin = Padding.Empty; zoomActions.WrapContents = false;
            zoomInButton.Text = "放大"; zoomInButton.Size = new Size(48, 28); zoomInButton.Click += ZoomInButton_Click;
            zoomOutButton.Text = "缩小"; zoomOutButton.Size = new Size(48, 28); zoomOutButton.Click += ZoomOutButton_Click;
            fitImageButton.Text = "适应"; fitImageButton.Size = new Size(54, 28); fitImageButton.Click += FitImageButton_Click;
            zoomLabel.Text = "--"; zoomLabel.Size = new Size(66, 28); zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            navigationHint.Text = "滚轮缩放 · 中键平移"; navigationHint.AutoSize = true; navigationHint.Margin = new Padding(3, 8, 0, 0); navigationHint.ForeColor = Color.DimGray;
            zoomActions.Controls.AddRange(new Control[] { zoomInButton, zoomOutButton, fitImageButton, zoomLabel, navigationHint });
            imageLayout.Controls.Add(regionTools, 0, 0); imageLayout.Controls.Add(imageCanvas, 0, 1); imageLayout.Controls.Add(zoomActions, 0, 2);
            rootLayout.Controls.Add(subscriptionPanel, 0, 0); rootLayout.SetColumnSpan(subscriptionPanel, 2);
            rootLayout.Controls.Add(imageLayout, 0, 1); rootLayout.Controls.Add(parameterLayout, 1, 1);
            rootLayout.Controls.Add(footerActions, 0, 2); rootLayout.SetColumnSpan(footerActions, 2);
            rootLayout.Controls.Add(statusLabel, 0, 3); rootLayout.SetColumnSpan(statusLabel, 2);
            imageOpenFileDialog.Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*";
            AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Microsoft YaHei UI",9F);
            ClientSize = new Size(1040,680); MinimumSize = new Size(900,600); StartPosition = FormStartPosition.CenterParent;
            Text = "模板配置"; Padding = new Padding(1); Controls.Add(rootLayout); rootLayout.BringToFront(); ResumeLayout(true);
        }

    }
}
