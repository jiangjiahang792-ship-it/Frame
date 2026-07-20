namespace TDJS_Vision.Forms.CameraAdd
{
    partial class CameraLiveDebugControl
    {
        /// <summary>
        /// 设计器组件容器。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 释放控件持有的托管资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveQualityRoiSnapshot();
                StopCollectingSilently();
                DisposePreviewImage();
                DisposeTreeGroupFont();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化设计器控件布局。
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("基本属性");
            System.Windows.Forms.TreeNode treeNode2 = new System.Windows.Forms.TreeNode("IO 输入");
            System.Windows.Forms.TreeNode treeNode3 = new System.Windows.Forms.TreeNode("IO 输出");
            System.Windows.Forms.TreeNode treeNode4 = new System.Windows.Forms.TreeNode("ROI");
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelPreviewRoot = new System.Windows.Forms.Panel();
            this.tableLayoutPanelPreview = new System.Windows.Forms.TableLayoutPanel();
            this.panelPreviewTitle = new System.Windows.Forms.Panel();
            this.labelPreviewTitle = new System.Windows.Forms.Label();
            this.panelPreviewToolbar = new System.Windows.Forms.Panel();
            this.buttonHoldImage = new System.Windows.Forms.Button();
            this.buttonStartCollect = new System.Windows.Forms.Button();
            this.panelPreviewStage = new System.Windows.Forms.Panel();
            this.labelEmptyPreview = new System.Windows.Forms.Label();
            this.pictureBoxPreview = new System.Windows.Forms.PictureBox();
            this.panelPropertyRoot = new System.Windows.Forms.Panel();
            this.tableLayoutPanelProperty = new System.Windows.Forms.TableLayoutPanel();
            this.treeViewProperty = new System.Windows.Forms.TreeView();
            this.panelPropertyPages = new System.Windows.Forms.Panel();
            this.panelRoiPage = new System.Windows.Forms.Panel();
            this.tableLayoutPanelRoi = new System.Windows.Forms.TableLayoutPanel();
            this.labelRoiTitle = new System.Windows.Forms.Label();
            this.labelRoiX = new System.Windows.Forms.Label();
            this.numericRoiX = new System.Windows.Forms.NumericUpDown();
            this.labelRoiY = new System.Windows.Forms.Label();
            this.numericRoiY = new System.Windows.Forms.NumericUpDown();
            this.labelRoiWidth = new System.Windows.Forms.Label();
            this.numericRoiWidth = new System.Windows.Forms.NumericUpDown();
            this.labelRoiHeight = new System.Windows.Forms.Label();
            this.numericRoiHeight = new System.Windows.Forms.NumericUpDown();
            this.buttonEditRoi = new System.Windows.Forms.Button();
            this.buttonCompleteRoi = new System.Windows.Forms.Button();
            this.buttonRestoreRoi = new System.Windows.Forms.Button();
            this.panelIoOutputPage = new System.Windows.Forms.Panel();
            this.tableLayoutPanelIoOutput = new System.Windows.Forms.TableLayoutPanel();
            this.labelIoOutputTitle = new System.Windows.Forms.Label();
            this.labelLineSelector = new System.Windows.Forms.Label();
            this.comboLineSelector = new System.Windows.Forms.ComboBox();
            this.labelLineMode = new System.Windows.Forms.Label();
            this.comboLineMode = new System.Windows.Forms.ComboBox();
            this.checkStrobeEnable = new System.Windows.Forms.CheckBox();
            this.checkLineInverter = new System.Windows.Forms.CheckBox();
            this.panelTriggerPage = new System.Windows.Forms.Panel();
            this.tableLayoutPanelTrigger = new System.Windows.Forms.TableLayoutPanel();
            this.labelTriggerTitle = new System.Windows.Forms.Label();
            this.labelTriggerMode = new System.Windows.Forms.Label();
            this.comboTriggerMode = new System.Windows.Forms.ComboBox();
            this.labelTriggerSource = new System.Windows.Forms.Label();
            this.comboTriggerSource = new System.Windows.Forms.ComboBox();
            this.labelTriggerEdge = new System.Windows.Forms.Label();
            this.comboTriggerEdge = new System.Windows.Forms.ComboBox();
            this.labelTriggerDelay = new System.Windows.Forms.Label();
            this.numericTriggerDelay = new System.Windows.Forms.NumericUpDown();
            this.labelSoftTrigger = new System.Windows.Forms.Label();
            this.buttonSoftTrigger = new System.Windows.Forms.Button();
            this.panelBasicPage = new System.Windows.Forms.Panel();
            this.tableLayoutPanelBasic = new System.Windows.Forms.TableLayoutPanel();
            this.labelBasicTitle = new System.Windows.Forms.Label();
            this.labelCurrentCamera = new System.Windows.Forms.Label();
            this.labelCurrentCameraValue = new System.Windows.Forms.Label();
            this.labelDeviceModel = new System.Windows.Forms.Label();
            this.labelDeviceModelValue = new System.Windows.Forms.Label();
            this.labelSnIp = new System.Windows.Forms.Label();
            this.labelSnIpValue = new System.Windows.Forms.Label();
            this.labelExposure = new System.Windows.Forms.Label();
            this.numericExposure = new System.Windows.Forms.NumericUpDown();
            this.labelGain = new System.Windows.Forms.Label();
            this.numericGain = new System.Windows.Forms.NumericUpDown();
            this.buttonSyncBrightnessToProcess = new System.Windows.Forms.Button();
            this.labelImageSize = new System.Windows.Forms.Label();
            this.labelImageSizeValue = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelStatusValue = new System.Windows.Forms.Label();
            this.labelQualityTitle = new System.Windows.Forms.Label();
            this.labelBrightnessState = new System.Windows.Forms.Label();
            this.labelBrightnessStateValue = new System.Windows.Forms.Label();
            this.labelSharpnessState = new System.Windows.Forms.Label();
            this.labelSharpnessStateValue = new System.Windows.Forms.Label();
            this.labelQualitySuggestion = new System.Windows.Forms.Label();
            this.labelQualitySuggestionValue = new System.Windows.Forms.Label();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelPreviewRoot.SuspendLayout();
            this.tableLayoutPanelPreview.SuspendLayout();
            this.panelPreviewTitle.SuspendLayout();
            this.panelPreviewToolbar.SuspendLayout();
            this.panelPreviewStage.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).BeginInit();
            this.panelPropertyRoot.SuspendLayout();
            this.tableLayoutPanelProperty.SuspendLayout();
            this.panelPropertyPages.SuspendLayout();
            this.panelRoiPage.SuspendLayout();
            this.tableLayoutPanelRoi.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiHeight)).BeginInit();
            this.panelIoOutputPage.SuspendLayout();
            this.tableLayoutPanelIoOutput.SuspendLayout();
            this.panelTriggerPage.SuspendLayout();
            this.tableLayoutPanelTrigger.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericTriggerDelay)).BeginInit();
            this.panelBasicPage.SuspendLayout();
            this.tableLayoutPanelBasic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericExposure)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericGain)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 526F));
            this.tableLayoutPanelMain.Controls.Add(this.panelPreviewRoot, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.panelPropertyRoot, 1, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 1;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1400, 760);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // panelPreviewRoot
            // 
            this.panelPreviewRoot.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(38)))), ((int)(((byte)(39)))), ((int)(((byte)(43)))));
            this.panelPreviewRoot.Controls.Add(this.tableLayoutPanelPreview);
            this.panelPreviewRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreviewRoot.Location = new System.Drawing.Point(0, 0);
            this.panelPreviewRoot.Margin = new System.Windows.Forms.Padding(0);
            this.panelPreviewRoot.Name = "panelPreviewRoot";
            this.panelPreviewRoot.Size = new System.Drawing.Size(874, 760);
            this.panelPreviewRoot.TabIndex = 0;
            // 
            // tableLayoutPanelPreview
            // 
            this.tableLayoutPanelPreview.ColumnCount = 1;
            this.tableLayoutPanelPreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Controls.Add(this.panelPreviewTitle, 0, 0);
            this.tableLayoutPanelPreview.Controls.Add(this.panelPreviewToolbar, 0, 1);
            this.tableLayoutPanelPreview.Controls.Add(this.panelPreviewStage, 0, 2);
            this.tableLayoutPanelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelPreview.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelPreview.Name = "tableLayoutPanelPreview";
            this.tableLayoutPanelPreview.RowCount = 3;
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Size = new System.Drawing.Size(874, 760);
            this.tableLayoutPanelPreview.TabIndex = 0;
            // 
            // panelPreviewTitle
            // 
            this.panelPreviewTitle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(53)))), ((int)(((byte)(55)))));
            this.panelPreviewTitle.Controls.Add(this.labelPreviewTitle);
            this.panelPreviewTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreviewTitle.Location = new System.Drawing.Point(0, 0);
            this.panelPreviewTitle.Margin = new System.Windows.Forms.Padding(0);
            this.panelPreviewTitle.Name = "panelPreviewTitle";
            this.panelPreviewTitle.Size = new System.Drawing.Size(874, 28);
            this.panelPreviewTitle.TabIndex = 0;
            // 
            // labelPreviewTitle
            // 
            this.labelPreviewTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelPreviewTitle.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.labelPreviewTitle.ForeColor = System.Drawing.Color.White;
            this.labelPreviewTitle.Location = new System.Drawing.Point(0, 0);
            this.labelPreviewTitle.Name = "labelPreviewTitle";
            this.labelPreviewTitle.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this.labelPreviewTitle.Size = new System.Drawing.Size(874, 28);
            this.labelPreviewTitle.TabIndex = 0;
            this.labelPreviewTitle.Text = "未选择相机";
            this.labelPreviewTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelPreviewToolbar
            // 
            this.panelPreviewToolbar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(21)))), ((int)(((byte)(22)))), ((int)(((byte)(23)))));
            this.panelPreviewToolbar.Controls.Add(this.buttonHoldImage);
            this.panelPreviewToolbar.Controls.Add(this.buttonStartCollect);
            this.panelPreviewToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreviewToolbar.Location = new System.Drawing.Point(0, 28);
            this.panelPreviewToolbar.Margin = new System.Windows.Forms.Padding(0);
            this.panelPreviewToolbar.Name = "panelPreviewToolbar";
            this.panelPreviewToolbar.Size = new System.Drawing.Size(874, 50);
            this.panelPreviewToolbar.TabIndex = 1;
            // 
            // buttonHoldImage
            // 
            this.buttonHoldImage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(48)))), ((int)(((byte)(54)))));
            this.buttonHoldImage.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(63)))), ((int)(((byte)(68)))), ((int)(((byte)(77)))));
            this.buttonHoldImage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonHoldImage.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.buttonHoldImage.ForeColor = System.Drawing.Color.White;
            this.buttonHoldImage.Location = new System.Drawing.Point(116, 10);
            this.buttonHoldImage.Name = "buttonHoldImage";
            this.buttonHoldImage.Size = new System.Drawing.Size(94, 30);
            this.buttonHoldImage.TabIndex = 1;
            this.buttonHoldImage.Text = "保持图像";
            this.buttonHoldImage.UseVisualStyleBackColor = false;
            // 
            // buttonStartCollect
            // 
            this.buttonStartCollect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(111)))), ((int)(((byte)(211)))));
            this.buttonStartCollect.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(116)))), ((int)(((byte)(214)))));
            this.buttonStartCollect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonStartCollect.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.buttonStartCollect.ForeColor = System.Drawing.Color.White;
            this.buttonStartCollect.Location = new System.Drawing.Point(14, 10);
            this.buttonStartCollect.Name = "buttonStartCollect";
            this.buttonStartCollect.Size = new System.Drawing.Size(94, 30);
            this.buttonStartCollect.TabIndex = 0;
            this.buttonStartCollect.Text = "开始采集";
            this.buttonStartCollect.UseVisualStyleBackColor = false;
            // 
            // panelPreviewStage
            // 
            this.panelPreviewStage.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
            this.panelPreviewStage.Controls.Add(this.labelEmptyPreview);
            this.panelPreviewStage.Controls.Add(this.pictureBoxPreview);
            this.panelPreviewStage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreviewStage.Location = new System.Drawing.Point(0, 78);
            this.panelPreviewStage.Margin = new System.Windows.Forms.Padding(0);
            this.panelPreviewStage.Name = "panelPreviewStage";
            this.panelPreviewStage.Size = new System.Drawing.Size(874, 682);
            this.panelPreviewStage.TabIndex = 2;
            // 
            // labelEmptyPreview
            // 
            this.labelEmptyPreview.BackColor = System.Drawing.Color.Transparent;
            this.labelEmptyPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelEmptyPreview.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Regular);
            this.labelEmptyPreview.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(88)))), ((int)(((byte)(94)))));
            this.labelEmptyPreview.Location = new System.Drawing.Point(0, 0);
            this.labelEmptyPreview.Name = "labelEmptyPreview";
            this.labelEmptyPreview.Size = new System.Drawing.Size(874, 682);
            this.labelEmptyPreview.TabIndex = 1;
            this.labelEmptyPreview.Text = "无图像";
            this.labelEmptyPreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pictureBoxPreview
            // 
            this.pictureBoxPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
            this.pictureBoxPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxPreview.Location = new System.Drawing.Point(0, 0);
            this.pictureBoxPreview.Name = "pictureBoxPreview";
            this.pictureBoxPreview.Size = new System.Drawing.Size(874, 682);
            this.pictureBoxPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxPreview.TabIndex = 0;
            this.pictureBoxPreview.TabStop = false;
            // 
            // panelPropertyRoot
            // 
            this.panelPropertyRoot.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(65)))), ((int)(((byte)(67)))));
            this.panelPropertyRoot.Controls.Add(this.tableLayoutPanelProperty);
            this.panelPropertyRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPropertyRoot.Location = new System.Drawing.Point(874, 0);
            this.panelPropertyRoot.Margin = new System.Windows.Forms.Padding(0);
            this.panelPropertyRoot.Name = "panelPropertyRoot";
            this.panelPropertyRoot.Size = new System.Drawing.Size(526, 760);
            this.panelPropertyRoot.TabIndex = 1;
            // 
            // tableLayoutPanelProperty
            // 
            this.tableLayoutPanelProperty.ColumnCount = 2;
            this.tableLayoutPanelProperty.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 205F));
            this.tableLayoutPanelProperty.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelProperty.Controls.Add(this.treeViewProperty, 0, 0);
            this.tableLayoutPanelProperty.Controls.Add(this.panelPropertyPages, 1, 0);
            this.tableLayoutPanelProperty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelProperty.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelProperty.Name = "tableLayoutPanelProperty";
            this.tableLayoutPanelProperty.RowCount = 1;
            this.tableLayoutPanelProperty.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelProperty.Size = new System.Drawing.Size(526, 760);
            this.tableLayoutPanelProperty.TabIndex = 0;
            // 
            // treeViewProperty
            // 
            this.treeViewProperty.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(65)))), ((int)(((byte)(67)))));
            this.treeViewProperty.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeViewProperty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewProperty.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Regular);
            this.treeViewProperty.ForeColor = System.Drawing.Color.White;
            this.treeViewProperty.HideSelection = false;
            this.treeViewProperty.Location = new System.Drawing.Point(0, 0);
            this.treeViewProperty.Margin = new System.Windows.Forms.Padding(0);
            this.treeViewProperty.Name = "treeViewProperty";
            treeNode1.Name = "nodeBasic";
            treeNode1.Tag = "Basic";
            treeNode1.Text = "基本属性";
            treeNode2.Name = "nodeTrigger";
            treeNode2.Tag = "Trigger";
            treeNode2.Text = "IO 输入";
            treeNode3.Name = "nodeIoOutput";
            treeNode3.Tag = "IoOutput";
            treeNode3.Text = "IO 输出";
            treeNode4.Name = "nodeRoi";
            treeNode4.Tag = "Roi";
            treeNode4.Text = "ROI";
            this.treeViewProperty.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1,
            treeNode2,
            treeNode3,
            treeNode4});
            this.treeViewProperty.ShowLines = false;
            this.treeViewProperty.ShowPlusMinus = false;
            this.treeViewProperty.ShowRootLines = false;
            this.treeViewProperty.Size = new System.Drawing.Size(205, 760);
            this.treeViewProperty.TabIndex = 0;
            // 
            // panelPropertyPages
            // 
            this.panelPropertyPages.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(65)))), ((int)(((byte)(67)))));
            this.panelPropertyPages.Controls.Add(this.panelRoiPage);
            this.panelPropertyPages.Controls.Add(this.panelIoOutputPage);
            this.panelPropertyPages.Controls.Add(this.panelTriggerPage);
            this.panelPropertyPages.Controls.Add(this.panelBasicPage);
            this.panelPropertyPages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPropertyPages.Location = new System.Drawing.Point(205, 0);
            this.panelPropertyPages.Margin = new System.Windows.Forms.Padding(0);
            this.panelPropertyPages.Name = "panelPropertyPages";
            this.panelPropertyPages.Size = new System.Drawing.Size(321, 760);
            this.panelPropertyPages.TabIndex = 1;
            // 
            // panelRoiPage
            // 
            this.panelRoiPage.AutoScroll = true;
            this.panelRoiPage.Controls.Add(this.tableLayoutPanelRoi);
            this.panelRoiPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRoiPage.Location = new System.Drawing.Point(0, 0);
            this.panelRoiPage.Name = "panelRoiPage";
            this.panelRoiPage.Size = new System.Drawing.Size(321, 760);
            this.panelRoiPage.TabIndex = 3;
            // 
            // tableLayoutPanelRoi
            // 
            this.tableLayoutPanelRoi.ColumnCount = 2;
            this.tableLayoutPanelRoi.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelRoi.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoi.Controls.Add(this.labelRoiTitle, 0, 0);
            this.tableLayoutPanelRoi.Controls.Add(this.labelRoiX, 0, 1);
            this.tableLayoutPanelRoi.Controls.Add(this.numericRoiX, 1, 1);
            this.tableLayoutPanelRoi.Controls.Add(this.labelRoiY, 0, 2);
            this.tableLayoutPanelRoi.Controls.Add(this.numericRoiY, 1, 2);
            this.tableLayoutPanelRoi.Controls.Add(this.labelRoiWidth, 0, 3);
            this.tableLayoutPanelRoi.Controls.Add(this.numericRoiWidth, 1, 3);
            this.tableLayoutPanelRoi.Controls.Add(this.labelRoiHeight, 0, 4);
            this.tableLayoutPanelRoi.Controls.Add(this.numericRoiHeight, 1, 4);
            this.tableLayoutPanelRoi.Controls.Add(this.buttonEditRoi, 0, 5);
            this.tableLayoutPanelRoi.Controls.Add(this.buttonCompleteRoi, 1, 5);
            this.tableLayoutPanelRoi.Controls.Add(this.buttonRestoreRoi, 1, 6);
            this.tableLayoutPanelRoi.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelRoi.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelRoi.Name = "tableLayoutPanelRoi";
            this.tableLayoutPanelRoi.Padding = new System.Windows.Forms.Padding(14, 16, 10, 0);
            this.tableLayoutPanelRoi.RowCount = 8;
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelRoi.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoi.Size = new System.Drawing.Size(321, 340);
            this.tableLayoutPanelRoi.TabIndex = 0;
            // 
            // labelRoiTitle
            // 
            this.labelRoiTitle.AutoSize = true;
            this.tableLayoutPanelRoi.SetColumnSpan(this.labelRoiTitle, 2);
            this.labelRoiTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.labelRoiTitle.ForeColor = System.Drawing.Color.White;
            this.labelRoiTitle.Location = new System.Drawing.Point(17, 16);
            this.labelRoiTitle.Name = "labelRoiTitle";
            this.labelRoiTitle.Size = new System.Drawing.Size(67, 14);
            this.labelRoiTitle.TabIndex = 0;
            this.labelRoiTitle.Text = "高级属性（ROI）";
            // 
            // labelRoiX
            // 
            this.labelRoiX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiX.ForeColor = System.Drawing.Color.White;
            this.labelRoiX.Location = new System.Drawing.Point(17, 50);
            this.labelRoiX.Name = "labelRoiX";
            this.labelRoiX.Size = new System.Drawing.Size(76, 46);
            this.labelRoiX.TabIndex = 1;
            this.labelRoiX.Text = "水平偏移";
            this.labelRoiX.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericRoiX
            // 
            this.numericRoiX.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericRoiX.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericRoiX.ForeColor = System.Drawing.Color.White;
            this.numericRoiX.Location = new System.Drawing.Point(99, 61);
            this.numericRoiX.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericRoiX.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numericRoiX.Name = "numericRoiX";
            this.numericRoiX.Size = new System.Drawing.Size(71, 23);
            this.numericRoiX.TabIndex = 2;
            // 
            // labelRoiY
            // 
            this.labelRoiY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiY.ForeColor = System.Drawing.Color.White;
            this.labelRoiY.Location = new System.Drawing.Point(17, 96);
            this.labelRoiY.Name = "labelRoiY";
            this.labelRoiY.Size = new System.Drawing.Size(76, 46);
            this.labelRoiY.TabIndex = 3;
            this.labelRoiY.Text = "垂直偏移";
            this.labelRoiY.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericRoiY
            // 
            this.numericRoiY.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericRoiY.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericRoiY.ForeColor = System.Drawing.Color.White;
            this.numericRoiY.Location = new System.Drawing.Point(99, 107);
            this.numericRoiY.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericRoiY.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numericRoiY.Name = "numericRoiY";
            this.numericRoiY.Size = new System.Drawing.Size(71, 23);
            this.numericRoiY.TabIndex = 4;
            // 
            // labelRoiWidth
            // 
            this.labelRoiWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiWidth.ForeColor = System.Drawing.Color.White;
            this.labelRoiWidth.Location = new System.Drawing.Point(17, 142);
            this.labelRoiWidth.Name = "labelRoiWidth";
            this.labelRoiWidth.Size = new System.Drawing.Size(76, 46);
            this.labelRoiWidth.TabIndex = 5;
            this.labelRoiWidth.Text = "宽度";
            this.labelRoiWidth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericRoiWidth
            // 
            this.numericRoiWidth.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericRoiWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericRoiWidth.ForeColor = System.Drawing.Color.White;
            this.numericRoiWidth.Location = new System.Drawing.Point(99, 153);
            this.numericRoiWidth.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericRoiWidth.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numericRoiWidth.Name = "numericRoiWidth";
            this.numericRoiWidth.Size = new System.Drawing.Size(71, 23);
            this.numericRoiWidth.TabIndex = 6;
            // 
            // labelRoiHeight
            // 
            this.labelRoiHeight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiHeight.ForeColor = System.Drawing.Color.White;
            this.labelRoiHeight.Location = new System.Drawing.Point(17, 188);
            this.labelRoiHeight.Name = "labelRoiHeight";
            this.labelRoiHeight.Size = new System.Drawing.Size(76, 46);
            this.labelRoiHeight.TabIndex = 7;
            this.labelRoiHeight.Text = "高度";
            this.labelRoiHeight.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericRoiHeight
            // 
            this.numericRoiHeight.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericRoiHeight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericRoiHeight.ForeColor = System.Drawing.Color.White;
            this.numericRoiHeight.Location = new System.Drawing.Point(99, 199);
            this.numericRoiHeight.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericRoiHeight.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.numericRoiHeight.Name = "numericRoiHeight";
            this.numericRoiHeight.Size = new System.Drawing.Size(71, 23);
            this.numericRoiHeight.TabIndex = 8;
            // 
            // buttonEditRoi
            // 
            this.buttonEditRoi.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(100)))), ((int)(((byte)(210)))));
            this.buttonEditRoi.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonEditRoi.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonEditRoi.ForeColor = System.Drawing.Color.White;
            this.buttonEditRoi.Location = new System.Drawing.Point(17, 237);
            this.buttonEditRoi.Name = "buttonEditRoi";
            this.buttonEditRoi.Size = new System.Drawing.Size(76, 36);
            this.buttonEditRoi.TabIndex = 9;
            this.buttonEditRoi.Text = "编辑ROI";
            this.buttonEditRoi.UseVisualStyleBackColor = false;
            // 
            // buttonCompleteRoi
            // 
            this.buttonCompleteRoi.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(88)))), ((int)(((byte)(92)))));
            this.buttonCompleteRoi.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonCompleteRoi.Enabled = false;
            this.buttonCompleteRoi.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonCompleteRoi.ForeColor = System.Drawing.Color.White;
            this.buttonCompleteRoi.Location = new System.Drawing.Point(99, 237);
            this.buttonCompleteRoi.Name = "buttonCompleteRoi";
            this.buttonCompleteRoi.Size = new System.Drawing.Size(71, 36);
            this.buttonCompleteRoi.TabIndex = 10;
            this.buttonCompleteRoi.Text = "完成";
            this.buttonCompleteRoi.UseVisualStyleBackColor = false;
            // 
            // buttonRestoreRoi
            // 
            this.buttonRestoreRoi.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(88)))), ((int)(((byte)(92)))));
            this.buttonRestoreRoi.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRestoreRoi.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonRestoreRoi.ForeColor = System.Drawing.Color.White;
            this.buttonRestoreRoi.Location = new System.Drawing.Point(99, 279);
            this.buttonRestoreRoi.Name = "buttonRestoreRoi";
            this.buttonRestoreRoi.Size = new System.Drawing.Size(71, 36);
            this.buttonRestoreRoi.TabIndex = 11;
            this.buttonRestoreRoi.Text = "默认ROI";
            this.buttonRestoreRoi.UseVisualStyleBackColor = false;
            // 
            // panelIoOutputPage
            // 
            this.panelIoOutputPage.AutoScroll = true;
            this.panelIoOutputPage.Controls.Add(this.tableLayoutPanelIoOutput);
            this.panelIoOutputPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelIoOutputPage.Location = new System.Drawing.Point(0, 0);
            this.panelIoOutputPage.Name = "panelIoOutputPage";
            this.panelIoOutputPage.Size = new System.Drawing.Size(321, 760);
            this.panelIoOutputPage.TabIndex = 2;
            // 
            // tableLayoutPanelIoOutput
            // 
            this.tableLayoutPanelIoOutput.ColumnCount = 2;
            this.tableLayoutPanelIoOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelIoOutput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelIoOutput.Controls.Add(this.labelIoOutputTitle, 0, 0);
            this.tableLayoutPanelIoOutput.Controls.Add(this.labelLineSelector, 0, 1);
            this.tableLayoutPanelIoOutput.Controls.Add(this.comboLineSelector, 1, 1);
            this.tableLayoutPanelIoOutput.Controls.Add(this.labelLineMode, 0, 2);
            this.tableLayoutPanelIoOutput.Controls.Add(this.comboLineMode, 1, 2);
            this.tableLayoutPanelIoOutput.Controls.Add(this.checkStrobeEnable, 1, 3);
            this.tableLayoutPanelIoOutput.Controls.Add(this.checkLineInverter, 1, 4);
            this.tableLayoutPanelIoOutput.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelIoOutput.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelIoOutput.Name = "tableLayoutPanelIoOutput";
            this.tableLayoutPanelIoOutput.Padding = new System.Windows.Forms.Padding(14, 16, 10, 0);
            this.tableLayoutPanelIoOutput.RowCount = 6;
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelIoOutput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelIoOutput.Size = new System.Drawing.Size(321, 246);
            this.tableLayoutPanelIoOutput.TabIndex = 0;
            // 
            // labelIoOutputTitle
            // 
            this.labelIoOutputTitle.AutoSize = true;
            this.tableLayoutPanelIoOutput.SetColumnSpan(this.labelIoOutputTitle, 2);
            this.labelIoOutputTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.labelIoOutputTitle.ForeColor = System.Drawing.Color.White;
            this.labelIoOutputTitle.Location = new System.Drawing.Point(17, 16);
            this.labelIoOutputTitle.Name = "labelIoOutputTitle";
            this.labelIoOutputTitle.Size = new System.Drawing.Size(67, 14);
            this.labelIoOutputTitle.TabIndex = 0;
            this.labelIoOutputTitle.Text = "触发输出";
            // 
            // labelLineSelector
            // 
            this.labelLineSelector.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelLineSelector.ForeColor = System.Drawing.Color.White;
            this.labelLineSelector.Location = new System.Drawing.Point(17, 50);
            this.labelLineSelector.Name = "labelLineSelector";
            this.labelLineSelector.Size = new System.Drawing.Size(76, 46);
            this.labelLineSelector.TabIndex = 1;
            this.labelLineSelector.Text = "线路选择";
            this.labelLineSelector.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboLineSelector
            // 
            this.comboLineSelector.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.comboLineSelector.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboLineSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLineSelector.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboLineSelector.ForeColor = System.Drawing.Color.White;
            this.comboLineSelector.FormattingEnabled = true;
            this.comboLineSelector.Location = new System.Drawing.Point(99, 61);
            this.comboLineSelector.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.comboLineSelector.Name = "comboLineSelector";
            this.comboLineSelector.Size = new System.Drawing.Size(71, 21);
            this.comboLineSelector.TabIndex = 2;
            // 
            // labelLineMode
            // 
            this.labelLineMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelLineMode.ForeColor = System.Drawing.Color.White;
            this.labelLineMode.Location = new System.Drawing.Point(17, 96);
            this.labelLineMode.Name = "labelLineMode";
            this.labelLineMode.Size = new System.Drawing.Size(76, 46);
            this.labelLineMode.TabIndex = 3;
            this.labelLineMode.Text = "线路模式";
            this.labelLineMode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboLineMode
            // 
            this.comboLineMode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.comboLineMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboLineMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLineMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboLineMode.ForeColor = System.Drawing.Color.White;
            this.comboLineMode.FormattingEnabled = true;
            this.comboLineMode.Location = new System.Drawing.Point(99, 107);
            this.comboLineMode.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.comboLineMode.Name = "comboLineMode";
            this.comboLineMode.Size = new System.Drawing.Size(71, 21);
            this.comboLineMode.TabIndex = 4;
            // 
            // checkStrobeEnable
            // 
            this.checkStrobeEnable.AutoSize = true;
            this.checkStrobeEnable.ForeColor = System.Drawing.Color.White;
            this.checkStrobeEnable.Location = new System.Drawing.Point(99, 153);
            this.checkStrobeEnable.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.checkStrobeEnable.Name = "checkStrobeEnable";
            this.checkStrobeEnable.Size = new System.Drawing.Size(74, 18);
            this.checkStrobeEnable.TabIndex = 5;
            this.checkStrobeEnable.Text = "频闪使能";
            this.checkStrobeEnable.UseVisualStyleBackColor = true;
            // 
            // checkLineInverter
            // 
            this.checkLineInverter.AutoSize = true;
            this.checkLineInverter.ForeColor = System.Drawing.Color.White;
            this.checkLineInverter.Location = new System.Drawing.Point(99, 191);
            this.checkLineInverter.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.checkLineInverter.Name = "checkLineInverter";
            this.checkLineInverter.Size = new System.Drawing.Size(74, 18);
            this.checkLineInverter.TabIndex = 6;
            this.checkLineInverter.Text = "线路反转";
            this.checkLineInverter.UseVisualStyleBackColor = true;
            // 
            // panelTriggerPage
            // 
            this.panelTriggerPage.AutoScroll = true;
            this.panelTriggerPage.Controls.Add(this.tableLayoutPanelTrigger);
            this.panelTriggerPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTriggerPage.Location = new System.Drawing.Point(0, 0);
            this.panelTriggerPage.Name = "panelTriggerPage";
            this.panelTriggerPage.Size = new System.Drawing.Size(321, 760);
            this.panelTriggerPage.TabIndex = 1;
            // 
            // tableLayoutPanelTrigger
            // 
            this.tableLayoutPanelTrigger.ColumnCount = 2;
            this.tableLayoutPanelTrigger.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelTrigger.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrigger.Controls.Add(this.labelTriggerTitle, 0, 0);
            this.tableLayoutPanelTrigger.Controls.Add(this.labelTriggerMode, 0, 1);
            this.tableLayoutPanelTrigger.Controls.Add(this.comboTriggerMode, 1, 1);
            this.tableLayoutPanelTrigger.Controls.Add(this.labelTriggerSource, 0, 2);
            this.tableLayoutPanelTrigger.Controls.Add(this.comboTriggerSource, 1, 2);
            this.tableLayoutPanelTrigger.Controls.Add(this.labelTriggerEdge, 0, 3);
            this.tableLayoutPanelTrigger.Controls.Add(this.comboTriggerEdge, 1, 3);
            this.tableLayoutPanelTrigger.Controls.Add(this.labelTriggerDelay, 0, 4);
            this.tableLayoutPanelTrigger.Controls.Add(this.numericTriggerDelay, 1, 4);
            this.tableLayoutPanelTrigger.Controls.Add(this.labelSoftTrigger, 0, 5);
            this.tableLayoutPanelTrigger.Controls.Add(this.buttonSoftTrigger, 1, 5);
            this.tableLayoutPanelTrigger.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelTrigger.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelTrigger.Name = "tableLayoutPanelTrigger";
            this.tableLayoutPanelTrigger.Padding = new System.Windows.Forms.Padding(14, 16, 10, 0);
            this.tableLayoutPanelTrigger.RowCount = 7;
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelTrigger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrigger.Size = new System.Drawing.Size(321, 292);
            this.tableLayoutPanelTrigger.TabIndex = 0;
            // 
            // labelTriggerTitle
            // 
            this.labelTriggerTitle.AutoSize = true;
            this.tableLayoutPanelTrigger.SetColumnSpan(this.labelTriggerTitle, 2);
            this.labelTriggerTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.labelTriggerTitle.ForeColor = System.Drawing.Color.White;
            this.labelTriggerTitle.Location = new System.Drawing.Point(17, 16);
            this.labelTriggerTitle.Name = "labelTriggerTitle";
            this.labelTriggerTitle.Size = new System.Drawing.Size(67, 14);
            this.labelTriggerTitle.TabIndex = 0;
            this.labelTriggerTitle.Text = "触发输入";
            // 
            // labelTriggerMode
            // 
            this.labelTriggerMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTriggerMode.ForeColor = System.Drawing.Color.White;
            this.labelTriggerMode.Location = new System.Drawing.Point(17, 50);
            this.labelTriggerMode.Name = "labelTriggerMode";
            this.labelTriggerMode.Size = new System.Drawing.Size(76, 46);
            this.labelTriggerMode.TabIndex = 1;
            this.labelTriggerMode.Text = "触发模式";
            this.labelTriggerMode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboTriggerMode
            // 
            this.comboTriggerMode.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.comboTriggerMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboTriggerMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTriggerMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboTriggerMode.ForeColor = System.Drawing.Color.White;
            this.comboTriggerMode.FormattingEnabled = true;
            this.comboTriggerMode.Location = new System.Drawing.Point(99, 61);
            this.comboTriggerMode.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.comboTriggerMode.Name = "comboTriggerMode";
            this.comboTriggerMode.Size = new System.Drawing.Size(71, 21);
            this.comboTriggerMode.TabIndex = 2;
            // 
            // labelTriggerSource
            // 
            this.labelTriggerSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTriggerSource.ForeColor = System.Drawing.Color.White;
            this.labelTriggerSource.Location = new System.Drawing.Point(17, 96);
            this.labelTriggerSource.Name = "labelTriggerSource";
            this.labelTriggerSource.Size = new System.Drawing.Size(76, 46);
            this.labelTriggerSource.TabIndex = 3;
            this.labelTriggerSource.Text = "触发源";
            this.labelTriggerSource.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboTriggerSource
            // 
            this.comboTriggerSource.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.comboTriggerSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboTriggerSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTriggerSource.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboTriggerSource.ForeColor = System.Drawing.Color.White;
            this.comboTriggerSource.FormattingEnabled = true;
            this.comboTriggerSource.Location = new System.Drawing.Point(99, 107);
            this.comboTriggerSource.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.comboTriggerSource.Name = "comboTriggerSource";
            this.comboTriggerSource.Size = new System.Drawing.Size(71, 21);
            this.comboTriggerSource.TabIndex = 4;
            // 
            // labelTriggerEdge
            // 
            this.labelTriggerEdge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTriggerEdge.ForeColor = System.Drawing.Color.White;
            this.labelTriggerEdge.Location = new System.Drawing.Point(17, 142);
            this.labelTriggerEdge.Name = "labelTriggerEdge";
            this.labelTriggerEdge.Size = new System.Drawing.Size(76, 46);
            this.labelTriggerEdge.TabIndex = 5;
            this.labelTriggerEdge.Text = "触发极性";
            this.labelTriggerEdge.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboTriggerEdge
            // 
            this.comboTriggerEdge.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.comboTriggerEdge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboTriggerEdge.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboTriggerEdge.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboTriggerEdge.ForeColor = System.Drawing.Color.White;
            this.comboTriggerEdge.FormattingEnabled = true;
            this.comboTriggerEdge.Location = new System.Drawing.Point(99, 153);
            this.comboTriggerEdge.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.comboTriggerEdge.Name = "comboTriggerEdge";
            this.comboTriggerEdge.Size = new System.Drawing.Size(71, 21);
            this.comboTriggerEdge.TabIndex = 6;
            // 
            // labelTriggerDelay
            // 
            this.labelTriggerDelay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTriggerDelay.ForeColor = System.Drawing.Color.White;
            this.labelTriggerDelay.Location = new System.Drawing.Point(17, 188);
            this.labelTriggerDelay.Name = "labelTriggerDelay";
            this.labelTriggerDelay.Size = new System.Drawing.Size(76, 46);
            this.labelTriggerDelay.TabIndex = 7;
            this.labelTriggerDelay.Text = "触发延迟";
            this.labelTriggerDelay.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericTriggerDelay
            // 
            this.numericTriggerDelay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericTriggerDelay.DecimalPlaces = 4;
            this.numericTriggerDelay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericTriggerDelay.ForeColor = System.Drawing.Color.White;
            this.numericTriggerDelay.Location = new System.Drawing.Point(99, 199);
            this.numericTriggerDelay.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericTriggerDelay.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericTriggerDelay.Name = "numericTriggerDelay";
            this.numericTriggerDelay.Size = new System.Drawing.Size(71, 23);
            this.numericTriggerDelay.TabIndex = 8;
            // 
            // labelSoftTrigger
            // 
            this.labelSoftTrigger.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSoftTrigger.ForeColor = System.Drawing.Color.White;
            this.labelSoftTrigger.Location = new System.Drawing.Point(17, 234);
            this.labelSoftTrigger.Name = "labelSoftTrigger";
            this.labelSoftTrigger.Size = new System.Drawing.Size(76, 46);
            this.labelSoftTrigger.TabIndex = 9;
            this.labelSoftTrigger.Text = "软触发";
            this.labelSoftTrigger.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // buttonSoftTrigger
            // 
            this.buttonSoftTrigger.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(111)))), ((int)(((byte)(211)))));
            this.buttonSoftTrigger.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(57)))), ((int)(((byte)(68)))), ((int)(((byte)(82)))));
            this.buttonSoftTrigger.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSoftTrigger.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.buttonSoftTrigger.ForeColor = System.Drawing.Color.White;
            this.buttonSoftTrigger.Location = new System.Drawing.Point(99, 241);
            this.buttonSoftTrigger.Margin = new System.Windows.Forms.Padding(3, 7, 3, 3);
            this.buttonSoftTrigger.Name = "buttonSoftTrigger";
            this.buttonSoftTrigger.Size = new System.Drawing.Size(112, 32);
            this.buttonSoftTrigger.TabIndex = 10;
            this.buttonSoftTrigger.Text = "软触发一次";
            this.buttonSoftTrigger.UseVisualStyleBackColor = false;
            // 
            // panelBasicPage
            // 
            this.panelBasicPage.AutoScroll = true;
            this.panelBasicPage.Controls.Add(this.tableLayoutPanelBasic);
            this.panelBasicPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBasicPage.Location = new System.Drawing.Point(0, 0);
            this.panelBasicPage.Name = "panelBasicPage";
            this.panelBasicPage.Size = new System.Drawing.Size(321, 760);
            this.panelBasicPage.TabIndex = 0;
            // 
            // tableLayoutPanelBasic
            // 
            this.tableLayoutPanelBasic.ColumnCount = 2;
            this.tableLayoutPanelBasic.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelBasic.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelBasic.Controls.Add(this.labelBasicTitle, 0, 0);
            this.tableLayoutPanelBasic.Controls.Add(this.labelCurrentCamera, 0, 1);
            this.tableLayoutPanelBasic.Controls.Add(this.labelCurrentCameraValue, 1, 1);
            this.tableLayoutPanelBasic.Controls.Add(this.labelDeviceModel, 0, 2);
            this.tableLayoutPanelBasic.Controls.Add(this.labelDeviceModelValue, 1, 2);
            this.tableLayoutPanelBasic.Controls.Add(this.labelSnIp, 0, 3);
            this.tableLayoutPanelBasic.Controls.Add(this.labelSnIpValue, 1, 3);
            this.tableLayoutPanelBasic.Controls.Add(this.labelExposure, 0, 4);
            this.tableLayoutPanelBasic.Controls.Add(this.numericExposure, 1, 4);
            this.tableLayoutPanelBasic.Controls.Add(this.labelGain, 0, 5);
            this.tableLayoutPanelBasic.Controls.Add(this.numericGain, 1, 5);
            this.tableLayoutPanelBasic.Controls.Add(this.buttonSyncBrightnessToProcess, 1, 6);
            this.tableLayoutPanelBasic.Controls.Add(this.labelImageSize, 0, 7);
            this.tableLayoutPanelBasic.Controls.Add(this.labelImageSizeValue, 1, 7);
            this.tableLayoutPanelBasic.Controls.Add(this.labelStatus, 0, 8);
            this.tableLayoutPanelBasic.Controls.Add(this.labelStatusValue, 1, 8);
            this.tableLayoutPanelBasic.Controls.Add(this.labelQualityTitle, 0, 9);
            this.tableLayoutPanelBasic.Controls.Add(this.labelBrightnessState, 0, 10);
            this.tableLayoutPanelBasic.Controls.Add(this.labelBrightnessStateValue, 1, 10);
            this.tableLayoutPanelBasic.Controls.Add(this.labelSharpnessState, 0, 11);
            this.tableLayoutPanelBasic.Controls.Add(this.labelSharpnessStateValue, 1, 11);
            this.tableLayoutPanelBasic.Controls.Add(this.labelQualitySuggestion, 0, 12);
            this.tableLayoutPanelBasic.Controls.Add(this.labelQualitySuggestionValue, 1, 12);
            this.tableLayoutPanelBasic.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelBasic.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelBasic.Name = "tableLayoutPanelBasic";
            this.tableLayoutPanelBasic.Padding = new System.Windows.Forms.Padding(14, 16, 10, 0);
            this.tableLayoutPanelBasic.RowCount = 14;
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutPanelBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelBasic.Size = new System.Drawing.Size(321, 620);
            this.tableLayoutPanelBasic.TabIndex = 0;
            // 
            // labelBasicTitle
            // 
            this.labelBasicTitle.AutoSize = true;
            this.tableLayoutPanelBasic.SetColumnSpan(this.labelBasicTitle, 2);
            this.labelBasicTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.labelBasicTitle.ForeColor = System.Drawing.Color.White;
            this.labelBasicTitle.Location = new System.Drawing.Point(17, 16);
            this.labelBasicTitle.Name = "labelBasicTitle";
            this.labelBasicTitle.Size = new System.Drawing.Size(67, 14);
            this.labelBasicTitle.TabIndex = 0;
            this.labelBasicTitle.Text = "基本属性";
            // 
            // labelCurrentCamera
            // 
            this.labelCurrentCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelCurrentCamera.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelCurrentCamera.Location = new System.Drawing.Point(17, 50);
            this.labelCurrentCamera.Name = "labelCurrentCamera";
            this.labelCurrentCamera.Size = new System.Drawing.Size(76, 42);
            this.labelCurrentCamera.TabIndex = 1;
            this.labelCurrentCamera.Text = "当前相机";
            this.labelCurrentCamera.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelCurrentCameraValue
            // 
            this.labelCurrentCameraValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelCurrentCameraValue.ForeColor = System.Drawing.Color.White;
            this.labelCurrentCameraValue.Location = new System.Drawing.Point(99, 50);
            this.labelCurrentCameraValue.Name = "labelCurrentCameraValue";
            this.labelCurrentCameraValue.Size = new System.Drawing.Size(71, 42);
            this.labelCurrentCameraValue.TabIndex = 2;
            this.labelCurrentCameraValue.Text = "-";
            this.labelCurrentCameraValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelDeviceModel
            // 
            this.labelDeviceModel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelDeviceModel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelDeviceModel.Location = new System.Drawing.Point(17, 92);
            this.labelDeviceModel.Name = "labelDeviceModel";
            this.labelDeviceModel.Size = new System.Drawing.Size(76, 42);
            this.labelDeviceModel.TabIndex = 3;
            this.labelDeviceModel.Text = "设备型号";
            this.labelDeviceModel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelDeviceModelValue
            // 
            this.labelDeviceModelValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelDeviceModelValue.ForeColor = System.Drawing.Color.White;
            this.labelDeviceModelValue.Location = new System.Drawing.Point(99, 92);
            this.labelDeviceModelValue.Name = "labelDeviceModelValue";
            this.labelDeviceModelValue.Size = new System.Drawing.Size(71, 42);
            this.labelDeviceModelValue.TabIndex = 4;
            this.labelDeviceModelValue.Text = "-";
            this.labelDeviceModelValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelSnIp
            // 
            this.labelSnIp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSnIp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelSnIp.Location = new System.Drawing.Point(17, 134);
            this.labelSnIp.Name = "labelSnIp";
            this.labelSnIp.Size = new System.Drawing.Size(76, 42);
            this.labelSnIp.TabIndex = 5;
            this.labelSnIp.Text = "SN / IP";
            this.labelSnIp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelSnIpValue
            // 
            this.labelSnIpValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSnIpValue.ForeColor = System.Drawing.Color.White;
            this.labelSnIpValue.Location = new System.Drawing.Point(99, 134);
            this.labelSnIpValue.Name = "labelSnIpValue";
            this.labelSnIpValue.Size = new System.Drawing.Size(71, 42);
            this.labelSnIpValue.TabIndex = 6;
            this.labelSnIpValue.Text = "-";
            this.labelSnIpValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelExposure
            // 
            this.labelExposure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelExposure.ForeColor = System.Drawing.Color.White;
            this.labelExposure.Location = new System.Drawing.Point(17, 176);
            this.labelExposure.Name = "labelExposure";
            this.labelExposure.Size = new System.Drawing.Size(76, 46);
            this.labelExposure.TabIndex = 7;
            this.labelExposure.Text = "曝光时间";
            this.labelExposure.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericExposure
            // 
            this.numericExposure.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericExposure.DecimalPlaces = 4;
            this.numericExposure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericExposure.ForeColor = System.Drawing.Color.White;
            this.numericExposure.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.numericExposure.Location = new System.Drawing.Point(99, 187);
            this.numericExposure.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericExposure.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericExposure.Name = "numericExposure";
            this.numericExposure.Size = new System.Drawing.Size(71, 23);
            this.numericExposure.TabIndex = 8;
            this.numericExposure.Value = new decimal(new int[] {
            513,
            0,
            0,
            0});
            // 
            // labelGain
            // 
            this.labelGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelGain.ForeColor = System.Drawing.Color.White;
            this.labelGain.Location = new System.Drawing.Point(17, 222);
            this.labelGain.Name = "labelGain";
            this.labelGain.Size = new System.Drawing.Size(76, 46);
            this.labelGain.TabIndex = 9;
            this.labelGain.Text = "增益";
            this.labelGain.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericGain
            // 
            this.numericGain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(43)))), ((int)(((byte)(48)))));
            this.numericGain.DecimalPlaces = 4;
            this.numericGain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numericGain.ForeColor = System.Drawing.Color.White;
            this.numericGain.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericGain.Location = new System.Drawing.Point(99, 233);
            this.numericGain.Margin = new System.Windows.Forms.Padding(3, 11, 3, 3);
            this.numericGain.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numericGain.Name = "numericGain";
            this.numericGain.Size = new System.Drawing.Size(71, 23);
            this.numericGain.TabIndex = 10;
            this.numericGain.Value = new decimal(new int[] {
            1497,
            0,
            0,
            131072});
            // 
            // buttonSyncBrightnessToProcess
            // 
            this.buttonSyncBrightnessToProcess.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(47)))), ((int)(((byte)(111)))), ((int)(((byte)(215)))));
            this.buttonSyncBrightnessToProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonSyncBrightnessToProcess.FlatAppearance.BorderSize = 0;
            this.buttonSyncBrightnessToProcess.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSyncBrightnessToProcess.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.buttonSyncBrightnessToProcess.ForeColor = System.Drawing.Color.White;
            this.buttonSyncBrightnessToProcess.Location = new System.Drawing.Point(99, 276);
            this.buttonSyncBrightnessToProcess.Margin = new System.Windows.Forms.Padding(3, 8, 3, 8);
            this.buttonSyncBrightnessToProcess.Name = "buttonSyncBrightnessToProcess";
            this.buttonSyncBrightnessToProcess.Size = new System.Drawing.Size(71, 32);
            this.buttonSyncBrightnessToProcess.TabIndex = 22;
            this.buttonSyncBrightnessToProcess.Text = "同步亮度到流程";
            this.buttonSyncBrightnessToProcess.UseVisualStyleBackColor = false;
            // 
            // labelImageSize
            // 
            this.labelImageSize.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelImageSize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelImageSize.Location = new System.Drawing.Point(17, 268);
            this.labelImageSize.Name = "labelImageSize";
            this.labelImageSize.Size = new System.Drawing.Size(76, 42);
            this.labelImageSize.TabIndex = 11;
            this.labelImageSize.Text = "画幅";
            this.labelImageSize.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelImageSizeValue
            // 
            this.labelImageSizeValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelImageSizeValue.ForeColor = System.Drawing.Color.White;
            this.labelImageSizeValue.Location = new System.Drawing.Point(99, 268);
            this.labelImageSizeValue.Name = "labelImageSizeValue";
            this.labelImageSizeValue.Size = new System.Drawing.Size(71, 42);
            this.labelImageSizeValue.TabIndex = 12;
            this.labelImageSizeValue.Text = "-";
            this.labelImageSizeValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelStatus
            // 
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelStatus.Location = new System.Drawing.Point(17, 310);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(76, 42);
            this.labelStatus.TabIndex = 13;
            this.labelStatus.Text = "状态";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelStatusValue
            // 
            this.labelStatusValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelStatusValue.ForeColor = System.Drawing.Color.White;
            this.labelStatusValue.Location = new System.Drawing.Point(99, 310);
            this.labelStatusValue.Name = "labelStatusValue";
            this.labelStatusValue.Size = new System.Drawing.Size(71, 42);
            this.labelStatusValue.TabIndex = 14;
            this.labelStatusValue.Text = "未开始采集";
            this.labelStatusValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelQualityTitle
            // 
            this.labelQualityTitle.AutoSize = true;
            this.tableLayoutPanelBasic.SetColumnSpan(this.labelQualityTitle, 2);
            this.labelQualityTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.labelQualityTitle.ForeColor = System.Drawing.Color.White;
            this.labelQualityTitle.Location = new System.Drawing.Point(17, 352);
            this.labelQualityTitle.Name = "labelQualityTitle";
            this.labelQualityTitle.Size = new System.Drawing.Size(67, 20);
            this.labelQualityTitle.TabIndex = 15;
            this.labelQualityTitle.Text = "成像判定";
            // 
            // labelBrightnessState
            // 
            this.labelBrightnessState.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelBrightnessState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelBrightnessState.Location = new System.Drawing.Point(17, 388);
            this.labelBrightnessState.Name = "labelBrightnessState";
            this.labelBrightnessState.Size = new System.Drawing.Size(76, 42);
            this.labelBrightnessState.TabIndex = 16;
            this.labelBrightnessState.Text = "亮度判定";
            this.labelBrightnessState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelBrightnessStateValue
            // 
            this.labelBrightnessStateValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelBrightnessStateValue.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.labelBrightnessStateValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelBrightnessStateValue.Location = new System.Drawing.Point(99, 388);
            this.labelBrightnessStateValue.Name = "labelBrightnessStateValue";
            this.labelBrightnessStateValue.Size = new System.Drawing.Size(71, 42);
            this.labelBrightnessStateValue.TabIndex = 17;
            this.labelBrightnessStateValue.Text = "等待图像";
            this.labelBrightnessStateValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelSharpnessState
            // 
            this.labelSharpnessState.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSharpnessState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelSharpnessState.Location = new System.Drawing.Point(17, 430);
            this.labelSharpnessState.Name = "labelSharpnessState";
            this.labelSharpnessState.Size = new System.Drawing.Size(76, 42);
            this.labelSharpnessState.TabIndex = 18;
            this.labelSharpnessState.Text = "清晰度判定";
            this.labelSharpnessState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelSharpnessStateValue
            // 
            this.labelSharpnessStateValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSharpnessStateValue.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Bold);
            this.labelSharpnessStateValue.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelSharpnessStateValue.Location = new System.Drawing.Point(99, 430);
            this.labelSharpnessStateValue.Name = "labelSharpnessStateValue";
            this.labelSharpnessStateValue.Size = new System.Drawing.Size(71, 42);
            this.labelSharpnessStateValue.TabIndex = 19;
            this.labelSharpnessStateValue.Text = "等待图像";
            this.labelSharpnessStateValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelQualitySuggestion
            // 
            this.labelQualitySuggestion.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelQualitySuggestion.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(205)))), ((int)(((byte)(212)))));
            this.labelQualitySuggestion.Location = new System.Drawing.Point(17, 472);
            this.labelQualitySuggestion.Name = "labelQualitySuggestion";
            this.labelQualitySuggestion.Size = new System.Drawing.Size(76, 72);
            this.labelQualitySuggestion.TabIndex = 20;
            this.labelQualitySuggestion.Text = "调试提示";
            this.labelQualitySuggestion.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // labelQualitySuggestionValue
            // 
            this.labelQualitySuggestionValue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelQualitySuggestionValue.ForeColor = System.Drawing.Color.White;
            this.labelQualitySuggestionValue.Location = new System.Drawing.Point(99, 472);
            this.labelQualitySuggestionValue.Name = "labelQualitySuggestionValue";
            this.labelQualitySuggestionValue.Size = new System.Drawing.Size(71, 72);
            this.labelQualitySuggestionValue.TabIndex = 21;
            this.labelQualitySuggestionValue.Text = "请先确认亮度，再调整焦距。";
            this.labelQualitySuggestionValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CameraLiveDebugControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Font = new System.Drawing.Font("微软雅黑", 9.5F);
            this.Name = "CameraLiveDebugControl";
            this.Size = new System.Drawing.Size(1400, 760);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.panelPreviewRoot.ResumeLayout(false);
            this.tableLayoutPanelPreview.ResumeLayout(false);
            this.panelPreviewTitle.ResumeLayout(false);
            this.panelPreviewToolbar.ResumeLayout(false);
            this.panelPreviewStage.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).EndInit();
            this.panelPropertyRoot.ResumeLayout(false);
            this.tableLayoutPanelProperty.ResumeLayout(false);
            this.panelPropertyPages.ResumeLayout(false);
            this.panelRoiPage.ResumeLayout(false);
            this.tableLayoutPanelRoi.ResumeLayout(false);
            this.tableLayoutPanelRoi.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiHeight)).EndInit();
            this.panelIoOutputPage.ResumeLayout(false);
            this.tableLayoutPanelIoOutput.ResumeLayout(false);
            this.tableLayoutPanelIoOutput.PerformLayout();
            this.panelTriggerPage.ResumeLayout(false);
            this.tableLayoutPanelTrigger.ResumeLayout(false);
            this.tableLayoutPanelTrigger.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericTriggerDelay)).EndInit();
            this.panelBasicPage.ResumeLayout(false);
            this.tableLayoutPanelBasic.ResumeLayout(false);
            this.tableLayoutPanelBasic.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericExposure)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericGain)).EndInit();
            this.ResumeLayout(false);

        }

        /// <summary>
        /// 主布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>
        /// 实时预览根面板。
        /// </summary>
        private System.Windows.Forms.Panel panelPreviewRoot;
        /// <summary>
        /// 实时预览布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPreview;
        /// <summary>
        /// 预览标题栏。
        /// </summary>
        private System.Windows.Forms.Panel panelPreviewTitle;
        /// <summary>
        /// 预览标题文本。
        /// </summary>
        private System.Windows.Forms.Label labelPreviewTitle;
        /// <summary>
        /// 预览操作栏。
        /// </summary>
        private System.Windows.Forms.Panel panelPreviewToolbar;
        /// <summary>
        /// 开始或停止采集按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonStartCollect;
        /// <summary>
        /// 保持当前图像按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonHoldImage;
        /// <summary>
        /// 图像显示区域。
        /// </summary>
        private System.Windows.Forms.Panel panelPreviewStage;
        /// <summary>
        /// 图像显示控件。
        /// </summary>
        private System.Windows.Forms.PictureBox pictureBoxPreview;
        /// <summary>
        /// 无图像提示文本。
        /// </summary>
        private System.Windows.Forms.Label labelEmptyPreview;
        /// <summary>
        /// 参数调试根面板。
        /// </summary>
        private System.Windows.Forms.Panel panelPropertyRoot;
        /// <summary>
        /// 参数调试布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelProperty;
        /// <summary>
        /// 参数分类树。
        /// </summary>
        private System.Windows.Forms.TreeView treeViewProperty;
        /// <summary>
        /// 参数页面容器。
        /// </summary>
        private System.Windows.Forms.Panel panelPropertyPages;
        /// <summary>
        /// 基本属性页面。
        /// </summary>
        private System.Windows.Forms.Panel panelBasicPage;
        /// <summary>
        /// 基本属性布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelBasic;
        /// <summary>
        /// 基本属性标题。
        /// </summary>
        private System.Windows.Forms.Label labelBasicTitle;
        /// <summary>
        /// 当前相机标签。
        /// </summary>
        private System.Windows.Forms.Label labelCurrentCamera;
        /// <summary>
        /// 当前相机值。
        /// </summary>
        private System.Windows.Forms.Label labelCurrentCameraValue;
        /// <summary>
        /// 设备型号标签。
        /// </summary>
        private System.Windows.Forms.Label labelDeviceModel;
        /// <summary>
        /// 设备型号值。
        /// </summary>
        private System.Windows.Forms.Label labelDeviceModelValue;
        /// <summary>
        /// 序列号和 IP 标签。
        /// </summary>
        private System.Windows.Forms.Label labelSnIp;
        /// <summary>
        /// 序列号和 IP 值。
        /// </summary>
        private System.Windows.Forms.Label labelSnIpValue;
        /// <summary>
        /// 曝光时间标签。
        /// </summary>
        private System.Windows.Forms.Label labelExposure;
        /// <summary>
        /// 曝光时间输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericExposure;
        /// <summary>
        /// 增益标签。
        /// </summary>
        private System.Windows.Forms.Label labelGain;
        /// <summary>
        /// 增益输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericGain;
        /// <summary>
        /// 将当前曝光和增益同步到流程相机节点的按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSyncBrightnessToProcess;
        /// <summary>
        /// 图像尺寸标签。
        /// </summary>
        private System.Windows.Forms.Label labelImageSize;
        /// <summary>
        /// 图像尺寸值。
        /// </summary>
        private System.Windows.Forms.Label labelImageSizeValue;
        /// <summary>
        /// 当前状态标签。
        /// </summary>
        private System.Windows.Forms.Label labelStatus;
        /// <summary>
        /// 当前状态值。
        /// </summary>
        private System.Windows.Forms.Label labelStatusValue;
        /// <summary>
        /// 成像判定标题。
        /// </summary>
        private System.Windows.Forms.Label labelQualityTitle;
        /// <summary>
        /// 亮度判定标签。
        /// </summary>
        private System.Windows.Forms.Label labelBrightnessState;
        /// <summary>
        /// 亮度判定值。
        /// </summary>
        private System.Windows.Forms.Label labelBrightnessStateValue;
        /// <summary>
        /// 清晰度判定标签。
        /// </summary>
        private System.Windows.Forms.Label labelSharpnessState;
        /// <summary>
        /// 清晰度判定值。
        /// </summary>
        private System.Windows.Forms.Label labelSharpnessStateValue;
        /// <summary>
        /// 成像调试提示标签。
        /// </summary>
        private System.Windows.Forms.Label labelQualitySuggestion;
        /// <summary>
        /// 成像调试提示值。
        /// </summary>
        private System.Windows.Forms.Label labelQualitySuggestionValue;
        /// <summary>
        /// 触发输入页面。
        /// </summary>
        private System.Windows.Forms.Panel panelTriggerPage;
        /// <summary>
        /// 触发输入布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTrigger;
        /// <summary>
        /// 触发输入标题。
        /// </summary>
        private System.Windows.Forms.Label labelTriggerTitle;
        /// <summary>
        /// 触发模式标签。
        /// </summary>
        private System.Windows.Forms.Label labelTriggerMode;
        /// <summary>
        /// 触发模式下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboTriggerMode;
        /// <summary>
        /// 触发源标签。
        /// </summary>
        private System.Windows.Forms.Label labelTriggerSource;
        /// <summary>
        /// 触发源下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboTriggerSource;
        /// <summary>
        /// 触发极性标签。
        /// </summary>
        private System.Windows.Forms.Label labelTriggerEdge;
        /// <summary>
        /// 触发极性下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboTriggerEdge;
        /// <summary>
        /// 触发延迟标签。
        /// </summary>
        private System.Windows.Forms.Label labelTriggerDelay;
        /// <summary>
        /// 触发延迟输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericTriggerDelay;
        /// <summary>
        /// 软触发标签。
        /// </summary>
        private System.Windows.Forms.Label labelSoftTrigger;
        /// <summary>
        /// 软触发一次按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSoftTrigger;
        /// <summary>
        /// IO 输出页面。
        /// </summary>
        private System.Windows.Forms.Panel panelIoOutputPage;
        /// <summary>
        /// IO 输出布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelIoOutput;
        /// <summary>
        /// IO 输出标题。
        /// </summary>
        private System.Windows.Forms.Label labelIoOutputTitle;
        /// <summary>
        /// 线路选择标签。
        /// </summary>
        private System.Windows.Forms.Label labelLineSelector;
        /// <summary>
        /// 线路选择下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboLineSelector;
        /// <summary>
        /// 线路模式标签。
        /// </summary>
        private System.Windows.Forms.Label labelLineMode;
        /// <summary>
        /// 线路模式下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboLineMode;
        /// <summary>
        /// 频闪使能开关。
        /// </summary>
        private System.Windows.Forms.CheckBox checkStrobeEnable;
        /// <summary>
        /// 线路反转开关。
        /// </summary>
        private System.Windows.Forms.CheckBox checkLineInverter;
        /// <summary>
        /// ROI 页面。
        /// </summary>
        private System.Windows.Forms.Panel panelRoiPage;
        /// <summary>
        /// ROI 布局容器。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoi;
        /// <summary>
        /// ROI 标题。
        /// </summary>
        private System.Windows.Forms.Label labelRoiTitle;
        /// <summary>
        /// ROI 水平偏移标签。
        /// </summary>
        private System.Windows.Forms.Label labelRoiX;
        /// <summary>
        /// ROI 水平偏移输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericRoiX;
        /// <summary>
        /// ROI 垂直偏移标签。
        /// </summary>
        private System.Windows.Forms.Label labelRoiY;
        /// <summary>
        /// ROI 垂直偏移输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericRoiY;
        /// <summary>
        /// ROI 宽度标签。
        /// </summary>
        private System.Windows.Forms.Label labelRoiWidth;
        /// <summary>
        /// ROI 宽度输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericRoiWidth;
        /// <summary>
        /// ROI 高度标签。
        /// </summary>
        private System.Windows.Forms.Label labelRoiHeight;
        /// <summary>
        /// ROI 高度输入。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericRoiHeight;
        /// <summary>
        /// 编辑 ROI 按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonEditRoi;
        /// <summary>
        /// 完成 ROI 编辑按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonCompleteRoi;
        /// <summary>
        /// 恢复默认 ROI 按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRestoreRoi;
    }
}
