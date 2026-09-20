namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>按卡尺风格只保留输入、位置修正和算法设置；绘制与结果均在图像上完成。</summary>
    partial class NodeParamFormTerminalAngle
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>整体左右分栏布局。</summary>
        private System.Windows.Forms.TableLayoutPanel root;
        /// <summary>左侧输入与算法分组。</summary>
        private System.Windows.Forms.TableLayoutPanel sidebar;
        /// <summary>输入图像分组。</summary>
        private System.Windows.Forms.GroupBox inputGroup;
        /// <summary>与卡尺一致的位置修正分组。</summary>
        private System.Windows.Forms.GroupBox correctionGroup;
        /// <summary>分割参数分组。</summary>
        private System.Windows.Forms.GroupBox segmentationGroup;
        /// <summary>质量检查分组。</summary>
        private System.Windows.Forms.GroupBox qualityGroup;
        /// <summary>输入图像订阅。</summary>
        private NodeSubscription imageSubscription;
        /// <summary>位置修正信息列表订阅。</summary>
        private NodeSubscription correctionSubscription;
        /// <summary>启用位置修正。</summary>
        private System.Windows.Forms.CheckBox correctionCheckBox;
        /// <summary>修正开关及订阅纵向布局。</summary>
        private System.Windows.Forms.TableLayoutPanel correctionLayout;
        /// <summary>分割参数双列布局。</summary>
        private System.Windows.Forms.TableLayoutPanel segmentationLayout;
        /// <summary>质量参数双列布局。</summary>
        private System.Windows.Forms.TableLayoutPanel qualityLayout;
        /// <summary>图像下方操作栏。</summary>
        private System.Windows.Forms.FlowLayoutPanel toolbar;
        /// <summary>框架图像和区域编辑控件。</summary>
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl viewer;
        /// <summary>蓝色通道分割开关。</summary>
        private System.Windows.Forms.CheckBox blueChannelCheckBox;
        /// <summary>绘制端子按钮。</summary>
        private System.Windows.Forms.Button terminalButton;
        /// <summary>绘制基座按钮。</summary>
        private System.Windows.Forms.Button baseButton;
        /// <summary>刷新图像按钮。</summary>
        private System.Windows.Forms.Button refreshButton;
        /// <summary>执行按钮。</summary>
        private System.Windows.Forms.Button testButton;
        /// <summary>确定按钮。</summary>
        private System.Windows.Forms.Button saveButton;
        /// <summary>阈值说明。</summary>
        private System.Windows.Forms.Label segmentationThresholdLabel;
        /// <summary>阈值输入框。</summary>
        private System.Windows.Forms.TextBox segmentationThresholdTextBox;
        /// <summary>高斯尺寸说明。</summary>
        private System.Windows.Forms.Label segmentationGaussianLabel;
        /// <summary>高斯尺寸输入框。</summary>
        private System.Windows.Forms.TextBox segmentationGaussianTextBox;
        /// <summary>最小宽度说明。</summary>
        private System.Windows.Forms.Label qualityMinimumWidthLabel;
        /// <summary>最小宽度输入框。</summary>
        private System.Windows.Forms.TextBox qualityMinimumWidthTextBox;
        /// <summary>最大宽度说明。</summary>
        private System.Windows.Forms.Label qualityMaximumWidthLabel;
        /// <summary>最大宽度输入框。</summary>
        private System.Windows.Forms.TextBox qualityMaximumWidthTextBox;
        /// <summary>高度覆盖说明。</summary>
        private System.Windows.Forms.Label qualityCoverageLabel;
        /// <summary>高度覆盖输入框。</summary>
        private System.Windows.Forms.TextBox qualityCoverageTextBox;
        /// <summary>取消预览并释放窗体组件。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleasePreview(); if (components != null) components.Dispose(); }
            base.Dispose(disposing);
        }
        /// <summary>所有控件、布局与标准事件绑定均在设计器文件中定义。</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormTerminalAngle));
            this.root = new System.Windows.Forms.TableLayoutPanel();
            this.sidebar = new System.Windows.Forms.TableLayoutPanel();
            this.inputGroup = new System.Windows.Forms.GroupBox();
            this.imageSubscription = new TDJS_Vision.Node.NodeSubscription();
            this.correctionGroup = new System.Windows.Forms.GroupBox();
            this.correctionLayout = new System.Windows.Forms.TableLayoutPanel();
            this.correctionCheckBox = new System.Windows.Forms.CheckBox();
            this.correctionSubscription = new TDJS_Vision.Node.NodeSubscription();
            this.segmentationGroup = new System.Windows.Forms.GroupBox();
            this.segmentationLayout = new System.Windows.Forms.TableLayoutPanel();
            this.segmentationThresholdLabel = new System.Windows.Forms.Label();
            this.segmentationThresholdTextBox = new System.Windows.Forms.TextBox();
            this.segmentationGaussianLabel = new System.Windows.Forms.Label();
            this.segmentationGaussianTextBox = new System.Windows.Forms.TextBox();
            this.blueChannelCheckBox = new System.Windows.Forms.CheckBox();
            this.qualityGroup = new System.Windows.Forms.GroupBox();
            this.qualityLayout = new System.Windows.Forms.TableLayoutPanel();
            this.qualityMinimumWidthLabel = new System.Windows.Forms.Label();
            this.qualityMinimumWidthTextBox = new System.Windows.Forms.TextBox();
            this.qualityMaximumWidthLabel = new System.Windows.Forms.Label();
            this.qualityMaximumWidthTextBox = new System.Windows.Forms.TextBox();
            this.qualityCoverageLabel = new System.Windows.Forms.Label();
            this.qualityCoverageTextBox = new System.Windows.Forms.TextBox();
            this.viewer = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.toolbar = new System.Windows.Forms.FlowLayoutPanel();
            this.terminalButton = new System.Windows.Forms.Button();
            this.baseButton = new System.Windows.Forms.Button();
            this.refreshButton = new System.Windows.Forms.Button();
            this.testButton = new System.Windows.Forms.Button();
            this.saveButton = new System.Windows.Forms.Button();
            this.root.SuspendLayout();
            this.sidebar.SuspendLayout();
            this.inputGroup.SuspendLayout();
            this.correctionGroup.SuspendLayout();
            this.correctionLayout.SuspendLayout();
            this.segmentationGroup.SuspendLayout();
            this.segmentationLayout.SuspendLayout();
            this.qualityGroup.SuspendLayout();
            this.qualityLayout.SuspendLayout();
            this.toolbar.SuspendLayout();
            this.SuspendLayout();
            // 
            // root
            // 
            this.root.ColumnCount = 2;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 326F));
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.sidebar, 0, 0);
            this.root.Controls.Add(this.viewer, 1, 0);
            this.root.Controls.Add(this.toolbar, 1, 1);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.Location = new System.Drawing.Point(2, 38);
            this.root.Name = "root";
            this.root.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this.root.RowCount = 2;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.root.Size = new System.Drawing.Size(896, 520);
            this.root.TabIndex = 2;
            // 
            // sidebar
            // 
            this.sidebar.ColumnCount = 1;
            this.sidebar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.sidebar.Controls.Add(this.inputGroup, 0, 0);
            this.sidebar.Controls.Add(this.correctionGroup, 0, 1);
            this.sidebar.Controls.Add(this.segmentationGroup, 0, 2);
            this.sidebar.Controls.Add(this.qualityGroup, 0, 3);
            this.sidebar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidebar.Location = new System.Drawing.Point(12, 10);
            this.sidebar.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.sidebar.Name = "sidebar";
            this.sidebar.RowCount = 5;
            this.root.SetRowSpan(this.sidebar, 2);
            this.sidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 104F));
            this.sidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 128F));
            this.sidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.sidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.sidebar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.sidebar.Size = new System.Drawing.Size(314, 500);
            this.sidebar.TabIndex = 0;
            // 
            // inputGroup
            // 
            this.inputGroup.Controls.Add(this.imageSubscription);
            this.inputGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.inputGroup.Location = new System.Drawing.Point(0, 0);
            this.inputGroup.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.inputGroup.Name = "inputGroup";
            this.inputGroup.Padding = new System.Windows.Forms.Padding(8, 4, 8, 6);
            this.inputGroup.Size = new System.Drawing.Size(314, 96);
            this.inputGroup.TabIndex = 0;
            this.inputGroup.TabStop = false;
            this.inputGroup.Text = "输入图像";
            // 
            // imageSubscription
            // 
            this.imageSubscription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.imageSubscription.Location = new System.Drawing.Point(8, 28);
            this.imageSubscription.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.imageSubscription.MinimumSize = new System.Drawing.Size(260, 60);
            this.imageSubscription.Name = "imageSubscription";
            this.imageSubscription.Size = new System.Drawing.Size(298, 62);
            this.imageSubscription.TabIndex = 0;
            this.imageSubscription.SelectionChanged += new System.EventHandler(this.ImageSubscriptionChanged);
            // 
            // correctionGroup
            // 
            this.correctionGroup.Controls.Add(this.correctionLayout);
            this.correctionGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.correctionGroup.Location = new System.Drawing.Point(0, 104);
            this.correctionGroup.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.correctionGroup.Name = "correctionGroup";
            this.correctionGroup.Padding = new System.Windows.Forms.Padding(8, 4, 8, 6);
            this.correctionGroup.Size = new System.Drawing.Size(314, 120);
            this.correctionGroup.TabIndex = 1;
            this.correctionGroup.TabStop = false;
            this.correctionGroup.Text = "位置修正";
            // 
            // correctionLayout
            // 
            this.correctionLayout.ColumnCount = 1;
            this.correctionLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.correctionLayout.Controls.Add(this.correctionCheckBox, 0, 0);
            this.correctionLayout.Controls.Add(this.correctionSubscription, 0, 1);
            this.correctionLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.correctionLayout.Location = new System.Drawing.Point(8, 28);
            this.correctionLayout.Name = "correctionLayout";
            this.correctionLayout.RowCount = 2;
            this.correctionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.correctionLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.correctionLayout.Size = new System.Drawing.Size(298, 86);
            this.correctionLayout.TabIndex = 0;
            // 
            // correctionCheckBox
            // 
            this.correctionCheckBox.AutoSize = true;
            this.correctionCheckBox.Location = new System.Drawing.Point(3, 3);
            this.correctionCheckBox.Name = "correctionCheckBox";
            this.correctionCheckBox.Size = new System.Drawing.Size(120, 18);
            this.correctionCheckBox.TabIndex = 0;
            this.correctionCheckBox.Text = "启用修正";
            this.correctionCheckBox.CheckedChanged += new System.EventHandler(this.CorrectionSelectionChanged);
            // 
            // correctionSubscription
            // 
            this.correctionSubscription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.correctionSubscription.Enabled = false;
            this.correctionSubscription.Location = new System.Drawing.Point(0, 24);
            this.correctionSubscription.Margin = new System.Windows.Forms.Padding(0);
            this.correctionSubscription.MinimumSize = new System.Drawing.Size(260, 60);
            this.correctionSubscription.Name = "correctionSubscription";
            this.correctionSubscription.Size = new System.Drawing.Size(298, 62);
            this.correctionSubscription.TabIndex = 1;
            this.correctionSubscription.SelectionChanged += new System.EventHandler(this.CorrectionSelectionChanged);
            // 
            // segmentationGroup
            // 
            this.segmentationGroup.Controls.Add(this.segmentationLayout);
            this.segmentationGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segmentationGroup.Location = new System.Drawing.Point(0, 232);
            this.segmentationGroup.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.segmentationGroup.Name = "segmentationGroup";
            this.segmentationGroup.Padding = new System.Windows.Forms.Padding(8, 4, 8, 6);
            this.segmentationGroup.Size = new System.Drawing.Size(314, 92);
            this.segmentationGroup.TabIndex = 2;
            this.segmentationGroup.TabStop = false;
            this.segmentationGroup.Text = "分割参数";
            // 
            // segmentationLayout
            // 
            this.segmentationLayout.ColumnCount = 4;
            this.segmentationLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.segmentationLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.segmentationLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.segmentationLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.segmentationLayout.Controls.Add(this.segmentationThresholdLabel, 0, 0);
            this.segmentationLayout.Controls.Add(this.segmentationThresholdTextBox, 1, 0);
            this.segmentationLayout.Controls.Add(this.segmentationGaussianLabel, 2, 0);
            this.segmentationLayout.Controls.Add(this.segmentationGaussianTextBox, 3, 0);
            this.segmentationLayout.Controls.Add(this.blueChannelCheckBox, 0, 1);
            this.segmentationLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segmentationLayout.Location = new System.Drawing.Point(8, 28);
            this.segmentationLayout.Name = "segmentationLayout";
            this.segmentationLayout.RowCount = 2;
            this.segmentationLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.segmentationLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.segmentationLayout.Size = new System.Drawing.Size(298, 58);
            this.segmentationLayout.TabIndex = 0;
            // 
            // segmentationThresholdLabel
            // 
            this.segmentationThresholdLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segmentationThresholdLabel.Location = new System.Drawing.Point(0, 0);
            this.segmentationThresholdLabel.Margin = new System.Windows.Forms.Padding(0);
            this.segmentationThresholdLabel.Name = "segmentationThresholdLabel";
            this.segmentationThresholdLabel.Size = new System.Drawing.Size(71, 29);
            this.segmentationThresholdLabel.TabIndex = 0;
            this.segmentationThresholdLabel.Text = "阈值";
            this.segmentationThresholdLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // segmentationThresholdTextBox
            // 
            this.segmentationThresholdTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.segmentationThresholdTextBox.Location = new System.Drawing.Point(73, 2);
            this.segmentationThresholdTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 6, 2);
            this.segmentationThresholdTextBox.Name = "segmentationThresholdTextBox";
            this.segmentationThresholdTextBox.Size = new System.Drawing.Size(69, 31);
            this.segmentationThresholdTextBox.TabIndex = 1;
            this.segmentationThresholdTextBox.TextChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            this.segmentationThresholdTextBox.Validated += new System.EventHandler(this.ParameterEditor_Validated);
            // 
            // segmentationGaussianLabel
            // 
            this.segmentationGaussianLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.segmentationGaussianLabel.Location = new System.Drawing.Point(148, 0);
            this.segmentationGaussianLabel.Margin = new System.Windows.Forms.Padding(0);
            this.segmentationGaussianLabel.Name = "segmentationGaussianLabel";
            this.segmentationGaussianLabel.Size = new System.Drawing.Size(71, 29);
            this.segmentationGaussianLabel.TabIndex = 2;
            this.segmentationGaussianLabel.Text = "高斯尺寸";
            this.segmentationGaussianLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // segmentationGaussianTextBox
            // 
            this.segmentationGaussianTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.segmentationGaussianTextBox.Location = new System.Drawing.Point(221, 2);
            this.segmentationGaussianTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 6, 2);
            this.segmentationGaussianTextBox.Name = "segmentationGaussianTextBox";
            this.segmentationGaussianTextBox.Size = new System.Drawing.Size(71, 31);
            this.segmentationGaussianTextBox.TabIndex = 3;
            this.segmentationGaussianTextBox.TextChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            this.segmentationGaussianTextBox.Validated += new System.EventHandler(this.ParameterEditor_Validated);
            // 
            // blueChannelCheckBox
            // 
            this.blueChannelCheckBox.AutoSize = true;
            this.segmentationLayout.SetColumnSpan(this.blueChannelCheckBox, 4);
            this.blueChannelCheckBox.Location = new System.Drawing.Point(3, 32);
            this.blueChannelCheckBox.Name = "blueChannelCheckBox";
            this.blueChannelCheckBox.Size = new System.Drawing.Size(162, 23);
            this.blueChannelCheckBox.TabIndex = 4;
            this.blueChannelCheckBox.Text = "使用蓝色通道";
            this.blueChannelCheckBox.CheckedChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            // 
            // qualityGroup
            // 
            this.qualityGroup.Controls.Add(this.qualityLayout);
            this.qualityGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qualityGroup.Location = new System.Drawing.Point(0, 332);
            this.qualityGroup.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.qualityGroup.Name = "qualityGroup";
            this.qualityGroup.Padding = new System.Windows.Forms.Padding(8, 4, 8, 6);
            this.qualityGroup.Size = new System.Drawing.Size(314, 92);
            this.qualityGroup.TabIndex = 3;
            this.qualityGroup.TabStop = false;
            this.qualityGroup.Text = "质量检查";
            // 
            // qualityLayout
            // 
            this.qualityLayout.ColumnCount = 4;
            this.qualityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.qualityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.qualityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24F));
            this.qualityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.qualityLayout.Controls.Add(this.qualityMinimumWidthLabel, 0, 0);
            this.qualityLayout.Controls.Add(this.qualityMinimumWidthTextBox, 1, 0);
            this.qualityLayout.Controls.Add(this.qualityMaximumWidthLabel, 2, 0);
            this.qualityLayout.Controls.Add(this.qualityMaximumWidthTextBox, 3, 0);
            this.qualityLayout.Controls.Add(this.qualityCoverageLabel, 0, 1);
            this.qualityLayout.Controls.Add(this.qualityCoverageTextBox, 1, 1);
            this.qualityLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qualityLayout.Location = new System.Drawing.Point(8, 28);
            this.qualityLayout.Name = "qualityLayout";
            this.qualityLayout.RowCount = 2;
            this.qualityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.qualityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.qualityLayout.Size = new System.Drawing.Size(298, 58);
            this.qualityLayout.TabIndex = 0;
            // 
            // qualityMinimumWidthLabel
            // 
            this.qualityMinimumWidthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qualityMinimumWidthLabel.Location = new System.Drawing.Point(0, 0);
            this.qualityMinimumWidthLabel.Margin = new System.Windows.Forms.Padding(0);
            this.qualityMinimumWidthLabel.Name = "qualityMinimumWidthLabel";
            this.qualityMinimumWidthLabel.Size = new System.Drawing.Size(71, 29);
            this.qualityMinimumWidthLabel.TabIndex = 0;
            this.qualityMinimumWidthLabel.Text = "最小宽度";
            this.qualityMinimumWidthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // qualityMinimumWidthTextBox
            // 
            this.qualityMinimumWidthTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityMinimumWidthTextBox.Location = new System.Drawing.Point(73, 2);
            this.qualityMinimumWidthTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 6, 2);
            this.qualityMinimumWidthTextBox.Name = "qualityMinimumWidthTextBox";
            this.qualityMinimumWidthTextBox.Size = new System.Drawing.Size(69, 31);
            this.qualityMinimumWidthTextBox.TabIndex = 1;
            this.qualityMinimumWidthTextBox.TextChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            this.qualityMinimumWidthTextBox.Validated += new System.EventHandler(this.ParameterEditor_Validated);
            // 
            // qualityMaximumWidthLabel
            // 
            this.qualityMaximumWidthLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qualityMaximumWidthLabel.Location = new System.Drawing.Point(148, 0);
            this.qualityMaximumWidthLabel.Margin = new System.Windows.Forms.Padding(0);
            this.qualityMaximumWidthLabel.Name = "qualityMaximumWidthLabel";
            this.qualityMaximumWidthLabel.Size = new System.Drawing.Size(71, 29);
            this.qualityMaximumWidthLabel.TabIndex = 2;
            this.qualityMaximumWidthLabel.Text = "最大宽度";
            this.qualityMaximumWidthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // qualityMaximumWidthTextBox
            // 
            this.qualityMaximumWidthTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityMaximumWidthTextBox.Location = new System.Drawing.Point(221, 2);
            this.qualityMaximumWidthTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 6, 2);
            this.qualityMaximumWidthTextBox.Name = "qualityMaximumWidthTextBox";
            this.qualityMaximumWidthTextBox.Size = new System.Drawing.Size(71, 31);
            this.qualityMaximumWidthTextBox.TabIndex = 3;
            this.qualityMaximumWidthTextBox.TextChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            this.qualityMaximumWidthTextBox.Validated += new System.EventHandler(this.ParameterEditor_Validated);
            // 
            // qualityCoverageLabel
            // 
            this.qualityCoverageLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.qualityCoverageLabel.Location = new System.Drawing.Point(0, 29);
            this.qualityCoverageLabel.Margin = new System.Windows.Forms.Padding(0);
            this.qualityCoverageLabel.Name = "qualityCoverageLabel";
            this.qualityCoverageLabel.Size = new System.Drawing.Size(71, 29);
            this.qualityCoverageLabel.TabIndex = 4;
            this.qualityCoverageLabel.Text = "高度覆盖";
            this.qualityCoverageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // qualityCoverageTextBox
            // 
            this.qualityCoverageTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.qualityCoverageTextBox.Location = new System.Drawing.Point(73, 31);
            this.qualityCoverageTextBox.Margin = new System.Windows.Forms.Padding(2, 2, 6, 2);
            this.qualityCoverageTextBox.Name = "qualityCoverageTextBox";
            this.qualityCoverageTextBox.Size = new System.Drawing.Size(69, 31);
            this.qualityCoverageTextBox.TabIndex = 5;
            this.qualityCoverageTextBox.TextChanged += new System.EventHandler(this.ParameterEditor_TextChanged);
            this.qualityCoverageTextBox.Validated += new System.EventHandler(this.ParameterEditor_Validated);
            // 
            // viewer
            // 
            this.viewer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.viewer.BackgroundColorCustom = System.Drawing.Color.Black;
            this.viewer.BackgroundImageCustom = null;
            this.viewer.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.viewer.BackgroundTransparency = 1F;
            this.viewer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.viewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.viewer.EnableRectangleRoiDrawing = false;
            this.viewer.Location = new System.Drawing.Point(338, 10);
            this.viewer.Margin = new System.Windows.Forms.Padding(0);
            this.viewer.Name = "viewer";
            this.viewer.RoiColor = System.Drawing.Color.Lime;
            this.viewer.ShowCheckerBackground = false;
            this.viewer.ShowPixelInfo = false;
            this.viewer.Size = new System.Drawing.Size(546, 458);
            this.viewer.StaticShapeColor = System.Drawing.Color.Red;
            this.viewer.TabIndex = 1;
            this.viewer.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Viewer_MouseUp);
            // 
            // toolbar
            // 
            this.toolbar.Controls.Add(this.terminalButton);
            this.toolbar.Controls.Add(this.baseButton);
            this.toolbar.Controls.Add(this.refreshButton);
            this.toolbar.Controls.Add(this.testButton);
            this.toolbar.Controls.Add(this.saveButton);
            this.toolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolbar.Location = new System.Drawing.Point(338, 468);
            this.toolbar.Margin = new System.Windows.Forms.Padding(0);
            this.toolbar.Name = "toolbar";
            this.toolbar.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.toolbar.Size = new System.Drawing.Size(546, 42);
            this.toolbar.TabIndex = 2;
            this.toolbar.WrapContents = false;
            // 
            // terminalButton
            // 
            this.terminalButton.Location = new System.Drawing.Point(0, 10);
            this.terminalButton.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.terminalButton.Name = "terminalButton";
            this.terminalButton.Size = new System.Drawing.Size(76, 30);
            this.terminalButton.TabIndex = 0;
            this.terminalButton.Text = "绘制端子";
            this.terminalButton.UseVisualStyleBackColor = true;
            this.terminalButton.Click += new System.EventHandler(this.TerminalButton_Click);
            // 
            // baseButton
            // 
            this.baseButton.Location = new System.Drawing.Point(82, 10);
            this.baseButton.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.baseButton.Name = "baseButton";
            this.baseButton.Size = new System.Drawing.Size(76, 30);
            this.baseButton.TabIndex = 1;
            this.baseButton.Text = "绘制基座";
            this.baseButton.UseVisualStyleBackColor = true;
            this.baseButton.Click += new System.EventHandler(this.BaseButton_Click);
            // 
            // refreshButton
            // 
            this.refreshButton.Location = new System.Drawing.Point(164, 10);
            this.refreshButton.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(76, 30);
            this.refreshButton.TabIndex = 2;
            this.refreshButton.Text = "刷新图像";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            // 
            // testButton
            // 
            this.testButton.Location = new System.Drawing.Point(246, 10);
            this.testButton.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.testButton.Name = "testButton";
            this.testButton.Size = new System.Drawing.Size(66, 30);
            this.testButton.TabIndex = 3;
            this.testButton.Text = "执行";
            this.testButton.UseVisualStyleBackColor = true;
            this.testButton.Click += new System.EventHandler(this.TestButton_Click);
            // 
            // saveButton
            // 
            this.saveButton.CausesValidation = false;
            this.saveButton.Location = new System.Drawing.Point(318, 10);
            this.saveButton.Margin = new System.Windows.Forms.Padding(0);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(66, 30);
            this.saveButton.TabIndex = 4;
            this.saveButton.Text = "确定";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.SaveButton_Click);
            // 
            // NodeParamFormTerminalAngle
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(900, 560);
            this.Controls.Add(this.root);
            this.Font = new System.Drawing.Font("宋体", 10.5F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            this.MinimumSize = new System.Drawing.Size(860, 520);
            this.Name = "NodeParamFormTerminalAngle";
            this.Text = "端子角度";
            this.Controls.SetChildIndex(this.root, 0);
            this.root.ResumeLayout(false);
            this.sidebar.ResumeLayout(false);
            this.inputGroup.ResumeLayout(false);
            this.correctionGroup.ResumeLayout(false);
            this.correctionLayout.ResumeLayout(false);
            this.correctionLayout.PerformLayout();
            this.segmentationGroup.ResumeLayout(false);
            this.segmentationLayout.ResumeLayout(false);
            this.segmentationLayout.PerformLayout();
            this.qualityGroup.ResumeLayout(false);
            this.qualityLayout.ResumeLayout(false);
            this.qualityLayout.PerformLayout();
            this.toolbar.ResumeLayout(false);
            this.ResumeLayout(false);

        }
    }
}
