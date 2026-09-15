namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    partial class NodeParamFormMatchTemplate
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeImages();
                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormMatchTemplate));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tabControlParams = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.splitContainerBasic = new System.Windows.Forms.SplitContainer();
            this.tableBasic = new System.Windows.Forms.TableLayoutPanel();
            this.labelBasicTitle = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.button3 = new System.Windows.Forms.Button();
            this.panelBasicLine = new System.Windows.Forms.Panel();
            this.labelRoiTitle = new System.Windows.Forms.Label();
            this.checkBoxAllSearch = new System.Windows.Forms.CheckBox();
            this.flowSearchActions = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonResetRoi = new System.Windows.Forms.Button();
            this.buttonConfirmSearchRoi = new System.Windows.Forms.Button();
            this.labelSearchStatus = new System.Windows.Forms.Label();
            this.panelDisplayLine = new System.Windows.Forms.Panel();
            this.labelDisplayTitle = new System.Windows.Forms.Label();
            this.checkBoxShowMatchBox = new System.Windows.Forms.CheckBox();
            this.checkBoxShowOutline = new System.Windows.Forms.CheckBox();
            this.labelResultSummary = new System.Windows.Forms.Label();
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.showImageControlSource = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.tabPageTemplate = new System.Windows.Forms.TabPage();
            this.splitContainerTemplate = new System.Windows.Forms.SplitContainer();
            this.tableTemplateList = new System.Windows.Forms.TableLayoutPanel();
            this.flowTemplateToolbar = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonCreateTemplate = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.textBoxModelPath = new System.Windows.Forms.TextBox();
            this.listBoxTemplate = new System.Windows.Forms.ListBox();
            this.tableTemplatePreview = new System.Windows.Forms.TableLayoutPanel();
            this.labelTemplatePreview = new System.Windows.Forms.Label();
            this.pictureBoxTemplate = new System.Windows.Forms.PictureBox();
            this.flowTemplateActions = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonEditTemplate = new System.Windows.Forms.Button();
            this.buttonRestoreTemplate = new System.Windows.Forms.Button();
            this.buttonDeleteTemplate = new System.Windows.Forms.Button();
            this.tabPageRun = new System.Windows.Forms.TabPage();
            this.tableRun = new System.Windows.Forms.TableLayoutPanel();
            this.labelRunTitle = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxMinScore = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxResultNum = new System.Windows.Forms.TextBox();
            this.labelSortMode = new System.Windows.Forms.Label();
            this.comboBoxSortMode = new System.Windows.Forms.ComboBox();
            this.labelToleranceAngle = new System.Windows.Forms.Label();
            this.textBoxToleranceAngle = new System.Windows.Forms.TextBox();
            this.labelAngleStep = new System.Windows.Forms.Label();
            this.textBoxAngleStep = new System.Windows.Forms.TextBox();
            this.labelMaxOverlap = new System.Windows.Forms.Label();
            this.textBoxMaxOverlap = new System.Windows.Forms.TextBox();
            this.checkBoxCoarseMatch = new System.Windows.Forms.CheckBox();
            this.panelFooter = new System.Windows.Forms.Panel();
            this.buttonContinuousRun = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.tabControlParams.SuspendLayout();
            this.tabPageBasic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerBasic)).BeginInit();
            this.splitContainerBasic.Panel1.SuspendLayout();
            this.splitContainerBasic.Panel2.SuspendLayout();
            this.splitContainerBasic.SuspendLayout();
            this.tableBasic.SuspendLayout();
            this.flowSearchActions.SuspendLayout();
            this.groupBoxSource.SuspendLayout();
            this.tabPageTemplate.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerTemplate)).BeginInit();
            this.splitContainerTemplate.Panel1.SuspendLayout();
            this.splitContainerTemplate.Panel2.SuspendLayout();
            this.splitContainerTemplate.SuspendLayout();
            this.tableTemplateList.SuspendLayout();
            this.flowTemplateToolbar.SuspendLayout();
            this.tableTemplatePreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTemplate)).BeginInit();
            this.flowTemplateActions.SuspendLayout();
            this.tabPageRun.SuspendLayout();
            this.tableRun.SuspendLayout();
            this.panelFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(244)))), ((int)(((byte)(246)))));
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tabControlParams, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelFooter, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(996, 680);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // tabControlParams
            // 
            this.tabControlParams.Controls.Add(this.tabPageBasic);
            this.tabControlParams.Controls.Add(this.tabPageTemplate);
            this.tabControlParams.Controls.Add(this.tabPageRun);
            this.tabControlParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlParams.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabControlParams.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F);
            this.tabControlParams.ItemSize = new System.Drawing.Size(126, 48);
            this.tabControlParams.Location = new System.Drawing.Point(0, 0);
            this.tabControlParams.Margin = new System.Windows.Forms.Padding(0);
            this.tabControlParams.Name = "tabControlParams";
            this.tabControlParams.SelectedIndex = 0;
            this.tabControlParams.Size = new System.Drawing.Size(996, 628);
            this.tabControlParams.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControlParams.TabIndex = 0;
            this.tabControlParams.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.tabControlParams_DrawItem);
            // 
            // tabPageBasic
            // 
            this.tabPageBasic.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(244)))), ((int)(((byte)(246)))));
            this.tabPageBasic.Controls.Add(this.splitContainerBasic);
            this.tabPageBasic.Location = new System.Drawing.Point(4, 52);
            this.tabPageBasic.Name = "tabPageBasic";
            this.tabPageBasic.Padding = new System.Windows.Forms.Padding(14);
            this.tabPageBasic.Size = new System.Drawing.Size(988, 572);
            this.tabPageBasic.TabIndex = 0;
            this.tabPageBasic.Text = "基本参数";
            // 
            // splitContainerBasic
            // 
            this.splitContainerBasic.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerBasic.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerBasic.Location = new System.Drawing.Point(14, 14);
            this.splitContainerBasic.Name = "splitContainerBasic";
            // 
            // splitContainerBasic.Panel1
            // 
            this.splitContainerBasic.Panel1.Controls.Add(this.tableBasic);
            this.splitContainerBasic.Panel1MinSize = 330;
            // 
            // splitContainerBasic.Panel2
            // 
            this.splitContainerBasic.Panel2.Controls.Add(this.groupBoxSource);
            this.splitContainerBasic.Panel2MinSize = 420;
            this.splitContainerBasic.Size = new System.Drawing.Size(960, 544);
            this.splitContainerBasic.SplitterDistance = 340;
            this.splitContainerBasic.TabIndex = 0;
            // 
            // tableBasic
            // 
            this.tableBasic.ColumnCount = 3;
            this.tableBasic.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 92F));
            this.tableBasic.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableBasic.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            this.tableBasic.Controls.Add(this.labelBasicTitle, 0, 0);
            this.tableBasic.Controls.Add(this.label3, 0, 1);
            this.tableBasic.Controls.Add(this.nodeSubscription1, 1, 1);
            this.tableBasic.Controls.Add(this.button3, 2, 1);
            this.tableBasic.Controls.Add(this.panelBasicLine, 0, 2);
            this.tableBasic.Controls.Add(this.labelRoiTitle, 0, 3);
            this.tableBasic.Controls.Add(this.checkBoxAllSearch, 0, 4);
            this.tableBasic.Controls.Add(this.flowSearchActions, 0, 5);
            this.tableBasic.Controls.Add(this.labelSearchStatus, 0, 6);
            this.tableBasic.Controls.Add(this.panelDisplayLine, 0, 7);
            this.tableBasic.Controls.Add(this.labelDisplayTitle, 0, 8);
            this.tableBasic.Controls.Add(this.checkBoxShowMatchBox, 0, 9);
            this.tableBasic.Controls.Add(this.checkBoxShowOutline, 0, 10);
            this.tableBasic.Controls.Add(this.labelResultSummary, 0, 11);
            this.tableBasic.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableBasic.Location = new System.Drawing.Point(0, 0);
            this.tableBasic.Name = "tableBasic";
            this.tableBasic.RowCount = 13;
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 18F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableBasic.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableBasic.Size = new System.Drawing.Size(340, 544);
            this.tableBasic.TabIndex = 0;
            // 
            // labelBasicTitle
            // 
            this.labelBasicTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelBasicTitle.AutoSize = true;
            this.tableBasic.SetColumnSpan(this.labelBasicTitle, 3);
            this.labelBasicTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelBasicTitle.Location = new System.Drawing.Point(3, 9);
            this.labelBasicTitle.Name = "labelBasicTitle";
            this.labelBasicTitle.Size = new System.Drawing.Size(92, 27);
            this.labelBasicTitle.TabIndex = 0;
            this.labelBasicTitle.Text = "图像输入";
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label3.AutoSize = true;
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.label3.Location = new System.Drawing.Point(3, 63);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(64, 24);
            this.label3.TabIndex = 1;
            this.label3.Text = "输入源";
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.nodeSubscription1.Location = new System.Drawing.Point(94, 50);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(2);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(160, 49);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(168, 49);
            this.nodeSubscription1.TabIndex = 2;
            // 
            // button3
            // 
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.button3.Location = new System.Drawing.Point(267, 59);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(66, 32);
            this.button3.TabIndex = 3;
            this.button3.Text = "刷新";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // panelBasicLine
            // 
            this.panelBasicLine.BackColor = System.Drawing.Color.Gainsboro;
            this.tableBasic.SetColumnSpan(this.panelBasicLine, 3);
            this.panelBasicLine.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBasicLine.Location = new System.Drawing.Point(3, 119);
            this.panelBasicLine.Name = "panelBasicLine";
            this.panelBasicLine.Size = new System.Drawing.Size(334, 1);
            this.panelBasicLine.TabIndex = 4;
            // 
            // labelRoiTitle
            // 
            this.labelRoiTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelRoiTitle.AutoSize = true;
            this.tableBasic.SetColumnSpan(this.labelRoiTitle, 3);
            this.labelRoiTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelRoiTitle.Location = new System.Drawing.Point(3, 130);
            this.labelRoiTitle.Name = "labelRoiTitle";
            this.labelRoiTitle.Size = new System.Drawing.Size(92, 27);
            this.labelRoiTitle.TabIndex = 5;
            this.labelRoiTitle.Text = "搜索区域";
            // 
            // checkBoxAllSearch
            // 
            this.checkBoxAllSearch.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxAllSearch.AutoSize = true;
            this.checkBoxAllSearch.Checked = true;
            this.checkBoxAllSearch.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tableBasic.SetColumnSpan(this.checkBoxAllSearch, 3);
            this.checkBoxAllSearch.Location = new System.Drawing.Point(3, 172);
            this.checkBoxAllSearch.Name = "checkBoxAllSearch";
            this.checkBoxAllSearch.Size = new System.Drawing.Size(126, 28);
            this.checkBoxAllSearch.TabIndex = 6;
            this.checkBoxAllSearch.Text = "整幅图搜索";
            this.checkBoxAllSearch.UseVisualStyleBackColor = true;
            this.checkBoxAllSearch.CheckedChanged += new System.EventHandler(this.checkBoxAllSearch_CheckedChanged);
            // 
            // flowSearchActions
            // 
            this.tableBasic.SetColumnSpan(this.flowSearchActions, 3);
            this.flowSearchActions.Controls.Add(this.buttonResetRoi);
            this.flowSearchActions.Controls.Add(this.buttonConfirmSearchRoi);
            this.flowSearchActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowSearchActions.Location = new System.Drawing.Point(0, 206);
            this.flowSearchActions.Margin = new System.Windows.Forms.Padding(0);
            this.flowSearchActions.Name = "flowSearchActions";
            this.flowSearchActions.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.flowSearchActions.Size = new System.Drawing.Size(340, 42);
            this.flowSearchActions.TabIndex = 7;
            // 
            // buttonResetRoi
            // 
            this.buttonResetRoi.Location = new System.Drawing.Point(3, 7);
            this.buttonResetRoi.Name = "buttonResetRoi";
            this.buttonResetRoi.Size = new System.Drawing.Size(156, 30);
            this.buttonResetRoi.TabIndex = 7;
            this.buttonResetRoi.Text = "绘制搜索区";
            this.buttonResetRoi.UseVisualStyleBackColor = true;
            this.buttonResetRoi.Click += new System.EventHandler(this.buttonResetRoi_Click);
            // 
            // buttonConfirmSearchRoi
            // 
            this.buttonConfirmSearchRoi.Location = new System.Drawing.Point(165, 7);
            this.buttonConfirmSearchRoi.Name = "buttonConfirmSearchRoi";
            this.buttonConfirmSearchRoi.Size = new System.Drawing.Size(96, 30);
            this.buttonConfirmSearchRoi.TabIndex = 8;
            this.buttonConfirmSearchRoi.Text = "确定ROI";
            this.buttonConfirmSearchRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmSearchRoi.Click += new System.EventHandler(this.buttonConfirmSearchRoi_Click);
            // 
            // labelSearchStatus
            // 
            this.labelSearchStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSearchStatus.AutoSize = true;
            this.tableBasic.SetColumnSpan(this.labelSearchStatus, 3);
            this.labelSearchStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelSearchStatus.Location = new System.Drawing.Point(3, 263);
            this.labelSearchStatus.Name = "labelSearchStatus";
            this.labelSearchStatus.Size = new System.Drawing.Size(118, 24);
            this.labelSearchStatus.TabIndex = 8;
            this.labelSearchStatus.Text = "当前：整图搜索";
            // 
            // panelDisplayLine
            // 
            this.panelDisplayLine.BackColor = System.Drawing.Color.Gainsboro;
            this.tableBasic.SetColumnSpan(this.panelDisplayLine, 3);
            this.panelDisplayLine.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelDisplayLine.Location = new System.Drawing.Point(3, 317);
            this.panelDisplayLine.Name = "panelDisplayLine";
            this.panelDisplayLine.Size = new System.Drawing.Size(334, 1);
            this.panelDisplayLine.TabIndex = 9;
            // 
            // labelDisplayTitle
            // 
            this.labelDisplayTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelDisplayTitle.AutoSize = true;
            this.tableBasic.SetColumnSpan(this.labelDisplayTitle, 3);
            this.labelDisplayTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelDisplayTitle.Location = new System.Drawing.Point(3, 328);
            this.labelDisplayTitle.Name = "labelDisplayTitle";
            this.labelDisplayTitle.Size = new System.Drawing.Size(92, 27);
            this.labelDisplayTitle.TabIndex = 10;
            this.labelDisplayTitle.Text = "运行预览";
            // 
            // checkBoxShowMatchBox
            // 
            this.checkBoxShowMatchBox.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxShowMatchBox.AutoSize = true;
            this.checkBoxShowMatchBox.Checked = true;
            this.checkBoxShowMatchBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tableBasic.SetColumnSpan(this.checkBoxShowMatchBox, 3);
            this.checkBoxShowMatchBox.Location = new System.Drawing.Point(3, 369);
            this.checkBoxShowMatchBox.Name = "checkBoxShowMatchBox";
            this.checkBoxShowMatchBox.Size = new System.Drawing.Size(108, 28);
            this.checkBoxShowMatchBox.TabIndex = 11;
            this.checkBoxShowMatchBox.Text = "显示外框";
            this.checkBoxShowMatchBox.UseVisualStyleBackColor = true;
            // 
            // checkBoxShowOutline
            // 
            this.checkBoxShowOutline.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxShowOutline.AutoSize = true;
            this.checkBoxShowOutline.Checked = true;
            this.checkBoxShowOutline.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tableBasic.SetColumnSpan(this.checkBoxShowOutline, 3);
            this.checkBoxShowOutline.Location = new System.Drawing.Point(3, 407);
            this.checkBoxShowOutline.Name = "checkBoxShowOutline";
            this.checkBoxShowOutline.Size = new System.Drawing.Size(108, 28);
            this.checkBoxShowOutline.TabIndex = 12;
            this.checkBoxShowOutline.Text = "显示轮廓";
            this.checkBoxShowOutline.UseVisualStyleBackColor = true;
            // 
            // labelResultSummary
            // 
            this.labelResultSummary.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelResultSummary.AutoSize = true;
            this.tableBasic.SetColumnSpan(this.labelResultSummary, 3);
            this.labelResultSummary.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelResultSummary.Location = new System.Drawing.Point(3, 452);
            this.labelResultSummary.Name = "labelResultSummary";
            this.labelResultSummary.Size = new System.Drawing.Size(118, 24);
            this.labelResultSummary.TabIndex = 13;
            this.labelResultSummary.Text = "暂无运行结果";
            // 
            // groupBoxSource
            // 
            this.groupBoxSource.Controls.Add(this.showImageControlSource);
            this.groupBoxSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSource.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.groupBoxSource.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Padding = new System.Windows.Forms.Padding(8);
            this.groupBoxSource.Size = new System.Drawing.Size(616, 544);
            this.groupBoxSource.TabIndex = 0;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "搜索区域 / 运行效果";
            // 
            // showImageControlSource
            // 
            this.showImageControlSource.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControlSource.BackgroundImageCustom = null;
            this.showImageControlSource.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControlSource.BackgroundTransparency = 1F;
            this.showImageControlSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControlSource.Location = new System.Drawing.Point(8, 30);
            this.showImageControlSource.Margin = new System.Windows.Forms.Padding(0);
            this.showImageControlSource.Name = "showImageControlSource";
            this.showImageControlSource.RoiColor = System.Drawing.Color.Lime;
            this.showImageControlSource.ShowCheckerBackground = false;
            this.showImageControlSource.ShowPixelInfo = false;
            this.showImageControlSource.Size = new System.Drawing.Size(600, 506);
            this.showImageControlSource.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControlSource.TabIndex = 0;
            // 
            // tabPageTemplate
            // 
            this.tabPageTemplate.BackColor = System.Drawing.Color.WhiteSmoke;
            this.tabPageTemplate.Controls.Add(this.splitContainerTemplate);
            this.tabPageTemplate.Location = new System.Drawing.Point(4, 52);
            this.tabPageTemplate.Name = "tabPageTemplate";
            this.tabPageTemplate.Padding = new System.Windows.Forms.Padding(14);
            this.tabPageTemplate.Size = new System.Drawing.Size(988, 572);
            this.tabPageTemplate.TabIndex = 1;
            this.tabPageTemplate.Text = "特征模板";
            // 
            // splitContainerTemplate
            // 
            this.splitContainerTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerTemplate.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerTemplate.Location = new System.Drawing.Point(14, 14);
            this.splitContainerTemplate.Name = "splitContainerTemplate";
            // 
            // splitContainerTemplate.Panel1
            // 
            this.splitContainerTemplate.Panel1.Controls.Add(this.tableTemplateList);
            this.splitContainerTemplate.Panel1MinSize = 430;
            // 
            // splitContainerTemplate.Panel2
            // 
            this.splitContainerTemplate.Panel2.Controls.Add(this.tableTemplatePreview);
            this.splitContainerTemplate.Panel2MinSize = 300;
            this.splitContainerTemplate.Size = new System.Drawing.Size(960, 544);
            this.splitContainerTemplate.SplitterDistance = 610;
            this.splitContainerTemplate.TabIndex = 0;
            // 
            // tableTemplateList
            // 
            this.tableTemplateList.ColumnCount = 1;
            this.tableTemplateList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplateList.Controls.Add(this.flowTemplateToolbar, 0, 0);
            this.tableTemplateList.Controls.Add(this.textBoxModelPath, 0, 1);
            this.tableTemplateList.Controls.Add(this.listBoxTemplate, 0, 2);
            this.tableTemplateList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableTemplateList.Location = new System.Drawing.Point(0, 0);
            this.tableTemplateList.Name = "tableTemplateList";
            this.tableTemplateList.RowCount = 3;
            this.tableTemplateList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableTemplateList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableTemplateList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplateList.Size = new System.Drawing.Size(610, 544);
            this.tableTemplateList.TabIndex = 0;
            // 
            // flowTemplateToolbar
            // 
            this.flowTemplateToolbar.Controls.Add(this.buttonCreateTemplate);
            this.flowTemplateToolbar.Controls.Add(this.button2);
            this.flowTemplateToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowTemplateToolbar.Location = new System.Drawing.Point(0, 0);
            this.flowTemplateToolbar.Margin = new System.Windows.Forms.Padding(0);
            this.flowTemplateToolbar.Name = "flowTemplateToolbar";
            this.flowTemplateToolbar.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.flowTemplateToolbar.Size = new System.Drawing.Size(610, 46);
            this.flowTemplateToolbar.TabIndex = 0;
            // 
            // buttonCreateTemplate
            // 
            this.buttonCreateTemplate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonCreateTemplate.Location = new System.Drawing.Point(3, 6);
            this.buttonCreateTemplate.Name = "buttonCreateTemplate";
            this.buttonCreateTemplate.Size = new System.Drawing.Size(112, 34);
            this.buttonCreateTemplate.TabIndex = 0;
            this.buttonCreateTemplate.Text = "⊞ 创建";
            this.buttonCreateTemplate.UseVisualStyleBackColor = true;
            this.buttonCreateTemplate.Click += new System.EventHandler(this.buttonCreateTemplate_Click);
            // 
            // button2
            // 
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button2.Location = new System.Drawing.Point(121, 6);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(112, 34);
            this.button2.TabIndex = 1;
            this.button2.Text = "⇩ 载入";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // textBoxModelPath
            // 
            this.textBoxModelPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxModelPath.Location = new System.Drawing.Point(3, 49);
            this.textBoxModelPath.Name = "textBoxModelPath";
            this.textBoxModelPath.ReadOnly = true;
            this.textBoxModelPath.Size = new System.Drawing.Size(604, 31);
            this.textBoxModelPath.TabIndex = 1;
            this.textBoxModelPath.Text = "未创建模板";
            // 
            // listBoxTemplate
            // 
            this.listBoxTemplate.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listBoxTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxTemplate.FormattingEnabled = true;
            this.listBoxTemplate.ItemHeight = 24;
            this.listBoxTemplate.Location = new System.Drawing.Point(3, 87);
            this.listBoxTemplate.Name = "listBoxTemplate";
            this.listBoxTemplate.Size = new System.Drawing.Size(604, 454);
            this.listBoxTemplate.TabIndex = 2;
            // 
            // tableTemplatePreview
            // 
            this.tableTemplatePreview.ColumnCount = 1;
            this.tableTemplatePreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplatePreview.Controls.Add(this.labelTemplatePreview, 0, 0);
            this.tableTemplatePreview.Controls.Add(this.pictureBoxTemplate, 0, 1);
            this.tableTemplatePreview.Controls.Add(this.flowTemplateActions, 0, 2);
            this.tableTemplatePreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableTemplatePreview.Location = new System.Drawing.Point(0, 0);
            this.tableTemplatePreview.Name = "tableTemplatePreview";
            this.tableTemplatePreview.RowCount = 3;
            this.tableTemplatePreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableTemplatePreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplatePreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 128F));
            this.tableTemplatePreview.Size = new System.Drawing.Size(346, 544);
            this.tableTemplatePreview.TabIndex = 0;
            // 
            // labelTemplatePreview
            // 
            this.labelTemplatePreview.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelTemplatePreview.AutoSize = true;
            this.labelTemplatePreview.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this.labelTemplatePreview.Location = new System.Drawing.Point(3, 6);
            this.labelTemplatePreview.Name = "labelTemplatePreview";
            this.labelTemplatePreview.Size = new System.Drawing.Size(118, 24);
            this.labelTemplatePreview.TabIndex = 0;
            this.labelTemplatePreview.Text = "模板轮廓预览";
            // 
            // pictureBoxTemplate
            // 
            this.pictureBoxTemplate.BackColor = System.Drawing.Color.Gainsboro;
            this.pictureBoxTemplate.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxTemplate.Location = new System.Drawing.Point(3, 39);
            this.pictureBoxTemplate.Name = "pictureBoxTemplate";
            this.pictureBoxTemplate.Size = new System.Drawing.Size(340, 374);
            this.pictureBoxTemplate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxTemplate.TabIndex = 1;
            this.pictureBoxTemplate.TabStop = false;
            // 
            // flowTemplateActions
            // 
            this.flowTemplateActions.Controls.Add(this.buttonEditTemplate);
            this.flowTemplateActions.Controls.Add(this.buttonRestoreTemplate);
            this.flowTemplateActions.Controls.Add(this.buttonDeleteTemplate);
            this.flowTemplateActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowTemplateActions.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowTemplateActions.Location = new System.Drawing.Point(3, 419);
            this.flowTemplateActions.Name = "flowTemplateActions";
            this.flowTemplateActions.Size = new System.Drawing.Size(340, 122);
            this.flowTemplateActions.TabIndex = 2;
            // 
            // buttonEditTemplate
            // 
            this.buttonEditTemplate.Enabled = false;
            this.buttonEditTemplate.Location = new System.Drawing.Point(3, 3);
            this.buttonEditTemplate.Name = "buttonEditTemplate";
            this.buttonEditTemplate.Size = new System.Drawing.Size(180, 34);
            this.buttonEditTemplate.TabIndex = 0;
            this.buttonEditTemplate.Text = "编辑模板";
            this.buttonEditTemplate.UseVisualStyleBackColor = true;
            this.buttonEditTemplate.Click += new System.EventHandler(this.buttonEditTemplate_Click);
            // 
            // buttonRestoreTemplate
            // 
            this.buttonRestoreTemplate.Enabled = false;
            this.buttonRestoreTemplate.Location = new System.Drawing.Point(3, 43);
            this.buttonRestoreTemplate.Name = "buttonRestoreTemplate";
            this.buttonRestoreTemplate.Size = new System.Drawing.Size(180, 34);
            this.buttonRestoreTemplate.TabIndex = 1;
            this.buttonRestoreTemplate.Text = "还原模板";
            this.buttonRestoreTemplate.UseVisualStyleBackColor = true;
            this.buttonRestoreTemplate.Click += new System.EventHandler(this.buttonRestoreTemplate_Click);
            // 
            // buttonDeleteTemplate
            // 
            this.buttonDeleteTemplate.Enabled = false;
            this.buttonDeleteTemplate.Location = new System.Drawing.Point(3, 83);
            this.buttonDeleteTemplate.Name = "buttonDeleteTemplate";
            this.buttonDeleteTemplate.Size = new System.Drawing.Size(180, 34);
            this.buttonDeleteTemplate.TabIndex = 2;
            this.buttonDeleteTemplate.Text = "删除模板";
            this.buttonDeleteTemplate.UseVisualStyleBackColor = true;
            this.buttonDeleteTemplate.Click += new System.EventHandler(this.buttonDeleteTemplate_Click);
            // 
            // tabPageRun
            // 
            this.tabPageRun.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(244)))), ((int)(((byte)(246)))));
            this.tabPageRun.Controls.Add(this.tableRun);
            this.tabPageRun.Location = new System.Drawing.Point(4, 52);
            this.tabPageRun.Name = "tabPageRun";
            this.tabPageRun.Padding = new System.Windows.Forms.Padding(46, 28, 46, 28);
            this.tabPageRun.Size = new System.Drawing.Size(988, 572);
            this.tabPageRun.TabIndex = 2;
            this.tabPageRun.Text = "运行参数";
            // 
            // tableRun
            // 
            this.tableRun.ColumnCount = 3;
            this.tableRun.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableRun.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 240F));
            this.tableRun.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableRun.Controls.Add(this.labelRunTitle, 0, 0);
            this.tableRun.Controls.Add(this.label4, 0, 1);
            this.tableRun.Controls.Add(this.textBoxMinScore, 1, 1);
            this.tableRun.Controls.Add(this.label5, 0, 2);
            this.tableRun.Controls.Add(this.textBoxResultNum, 1, 2);
            this.tableRun.Controls.Add(this.labelSortMode, 0, 3);
            this.tableRun.Controls.Add(this.comboBoxSortMode, 1, 3);
            this.tableRun.Controls.Add(this.labelToleranceAngle, 0, 4);
            this.tableRun.Controls.Add(this.textBoxToleranceAngle, 1, 4);
            this.tableRun.Controls.Add(this.labelAngleStep, 0, 5);
            this.tableRun.Controls.Add(this.textBoxAngleStep, 1, 5);
            this.tableRun.Controls.Add(this.labelMaxOverlap, 0, 6);
            this.tableRun.Controls.Add(this.textBoxMaxOverlap, 1, 6);
            this.tableRun.Controls.Add(this.checkBoxCoarseMatch, 1, 7);
            this.tableRun.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableRun.Location = new System.Drawing.Point(46, 28);
            this.tableRun.Name = "tableRun";
            this.tableRun.RowCount = 9;
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableRun.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableRun.Size = new System.Drawing.Size(896, 516);
            this.tableRun.TabIndex = 0;
            // 
            // labelRunTitle
            // 
            this.labelRunTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelRunTitle.AutoSize = true;
            this.tableRun.SetColumnSpan(this.labelRunTitle, 3);
            this.labelRunTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.labelRunTitle.Location = new System.Drawing.Point(3, 14);
            this.labelRunTitle.Name = "labelRunTitle";
            this.labelRunTitle.Size = new System.Drawing.Size(92, 27);
            this.labelRunTitle.TabIndex = 0;
            this.labelRunTitle.Text = "运行参数";
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label4.AutoSize = true;
            this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.label4.Location = new System.Drawing.Point(3, 70);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(118, 24);
            this.label4.TabIndex = 1;
            this.label4.Text = "最小匹配分数";
            // 
            // textBoxMinScore
            // 
            this.textBoxMinScore.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxMinScore.Location = new System.Drawing.Point(153, 66);
            this.textBoxMinScore.Name = "textBoxMinScore";
            this.textBoxMinScore.Size = new System.Drawing.Size(210, 31);
            this.textBoxMinScore.TabIndex = 2;
            this.textBoxMinScore.Text = "0.50";
            this.textBoxMinScore.TextChanged += new System.EventHandler(this.textBox3_TextChanged);
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label5.AutoSize = true;
            this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.label5.Location = new System.Drawing.Point(3, 122);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(118, 24);
            this.label5.TabIndex = 3;
            this.label5.Text = "最大匹配个数";
            // 
            // textBoxResultNum
            // 
            this.textBoxResultNum.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxResultNum.Location = new System.Drawing.Point(153, 118);
            this.textBoxResultNum.Name = "textBoxResultNum";
            this.textBoxResultNum.Size = new System.Drawing.Size(210, 31);
            this.textBoxResultNum.TabIndex = 4;
            this.textBoxResultNum.Text = "1";
            // 
            // labelSortMode
            // 
            this.labelSortMode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSortMode.AutoSize = true;
            this.labelSortMode.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelSortMode.Location = new System.Drawing.Point(3, 174);
            this.labelSortMode.Name = "labelSortMode";
            this.labelSortMode.Size = new System.Drawing.Size(136, 24);
            this.labelSortMode.TabIndex = 5;
            this.labelSortMode.Text = "多目标排序方式";
            // 
            // comboBoxSortMode
            // 
            this.comboBoxSortMode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.comboBoxSortMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSortMode.FormattingEnabled = true;
            this.comboBoxSortMode.Items.AddRange(new object[] {
            "列排序输出",
            "行排序输出"});
            this.comboBoxSortMode.Location = new System.Drawing.Point(153, 170);
            this.comboBoxSortMode.Name = "comboBoxSortMode";
            this.comboBoxSortMode.Size = new System.Drawing.Size(210, 32);
            this.comboBoxSortMode.TabIndex = 6;
            // 
            // labelToleranceAngle
            // 
            this.labelToleranceAngle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelToleranceAngle.AutoSize = true;
            this.labelToleranceAngle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelToleranceAngle.Location = new System.Drawing.Point(3, 226);
            this.labelToleranceAngle.Name = "labelToleranceAngle";
            this.labelToleranceAngle.Size = new System.Drawing.Size(82, 24);
            this.labelToleranceAngle.TabIndex = 7;
            this.labelToleranceAngle.Text = "角度范围";
            // 
            // textBoxToleranceAngle
            // 
            this.textBoxToleranceAngle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxToleranceAngle.Location = new System.Drawing.Point(153, 222);
            this.textBoxToleranceAngle.Name = "textBoxToleranceAngle";
            this.textBoxToleranceAngle.Size = new System.Drawing.Size(210, 31);
            this.textBoxToleranceAngle.TabIndex = 8;
            this.textBoxToleranceAngle.Text = "80";
            this.textBoxToleranceAngle.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // labelAngleStep
            // 
            this.labelAngleStep.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelAngleStep.AutoSize = true;
            this.labelAngleStep.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelAngleStep.Location = new System.Drawing.Point(3, 278);
            this.labelAngleStep.Name = "labelAngleStep";
            this.labelAngleStep.Size = new System.Drawing.Size(82, 24);
            this.labelAngleStep.TabIndex = 9;
            this.labelAngleStep.Text = "角度步长";
            // 
            // textBoxAngleStep
            // 
            this.textBoxAngleStep.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxAngleStep.Location = new System.Drawing.Point(153, 274);
            this.textBoxAngleStep.Name = "textBoxAngleStep";
            this.textBoxAngleStep.Size = new System.Drawing.Size(210, 31);
            this.textBoxAngleStep.TabIndex = 10;
            this.textBoxAngleStep.Text = "0";
            // 
            // labelMaxOverlap
            // 
            this.labelMaxOverlap.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelMaxOverlap.AutoSize = true;
            this.labelMaxOverlap.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelMaxOverlap.Location = new System.Drawing.Point(3, 330);
            this.labelMaxOverlap.Name = "labelMaxOverlap";
            this.labelMaxOverlap.Size = new System.Drawing.Size(100, 24);
            this.labelMaxOverlap.TabIndex = 11;
            this.labelMaxOverlap.Text = "最大重叠率";
            // 
            // textBoxMaxOverlap
            // 
            this.textBoxMaxOverlap.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxMaxOverlap.Location = new System.Drawing.Point(153, 326);
            this.textBoxMaxOverlap.Name = "textBoxMaxOverlap";
            this.textBoxMaxOverlap.Size = new System.Drawing.Size(210, 31);
            this.textBoxMaxOverlap.TabIndex = 12;
            this.textBoxMaxOverlap.Text = "40";
            // 
            // checkBoxCoarseMatch
            // 
            this.checkBoxCoarseMatch.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxCoarseMatch.AutoSize = true;
            this.checkBoxCoarseMatch.Location = new System.Drawing.Point(153, 381);
            this.checkBoxCoarseMatch.Name = "checkBoxCoarseMatch";
            this.checkBoxCoarseMatch.Size = new System.Drawing.Size(126, 28);
            this.checkBoxCoarseMatch.TabIndex = 13;
            this.checkBoxCoarseMatch.Text = "极速粗匹配";
            this.checkBoxCoarseMatch.UseVisualStyleBackColor = true;
            // 
            // panelFooter
            // 
            this.panelFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(232)))), ((int)(((byte)(236)))));
            this.panelFooter.Controls.Add(this.buttonContinuousRun);
            this.panelFooter.Controls.Add(this.buttonRun);
            this.panelFooter.Controls.Add(this.buttonSave);
            this.panelFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFooter.Location = new System.Drawing.Point(0, 628);
            this.panelFooter.Margin = new System.Windows.Forms.Padding(0);
            this.panelFooter.Name = "panelFooter";
            this.panelFooter.Size = new System.Drawing.Size(996, 52);
            this.panelFooter.TabIndex = 1;
            // 
            // buttonContinuousRun
            // 
            this.buttonContinuousRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonContinuousRun.Location = new System.Drawing.Point(638, 8);
            this.buttonContinuousRun.Name = "buttonContinuousRun";
            this.buttonContinuousRun.Size = new System.Drawing.Size(112, 36);
            this.buttonContinuousRun.TabIndex = 0;
            this.buttonContinuousRun.Text = "连续执行";
            this.buttonContinuousRun.UseVisualStyleBackColor = true;
            this.buttonContinuousRun.Click += new System.EventHandler(this.buttonContinuousRun_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRun.Location = new System.Drawing.Point(762, 8);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(100, 36);
            this.buttonRun.TabIndex = 1;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(874, 8);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(100, 36);
            this.buttonSave.TabIndex = 2;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Filter = "图片文件 (*.BMP, *.JPG, *.JPEG, *.PNG)|*.BMP;*.JPG;*.JPEG;*.PNG|所有文件 (*.*)|*.*";
            // 
            // toolTip1
            // 
            this.toolTip1.AutomaticDelay = 200;
            this.toolTip1.AutoPopDelay = 3000;
            this.toolTip1.InitialDelay = 200;
            this.toolTip1.ReshowDelay = 40;
            // 
            // NodeParamFormMatchTemplate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 720);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormMatchTemplate";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "轮廓匹配";
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tabControlParams.ResumeLayout(false);
            this.tabPageBasic.ResumeLayout(false);
            this.splitContainerBasic.Panel1.ResumeLayout(false);
            this.splitContainerBasic.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerBasic)).EndInit();
            this.splitContainerBasic.ResumeLayout(false);
            this.tableBasic.ResumeLayout(false);
            this.tableBasic.PerformLayout();
            this.flowSearchActions.ResumeLayout(false);
            this.groupBoxSource.ResumeLayout(false);
            this.tabPageTemplate.ResumeLayout(false);
            this.splitContainerTemplate.Panel1.ResumeLayout(false);
            this.splitContainerTemplate.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerTemplate)).EndInit();
            this.splitContainerTemplate.ResumeLayout(false);
            this.tableTemplateList.ResumeLayout(false);
            this.tableTemplateList.PerformLayout();
            this.flowTemplateToolbar.ResumeLayout(false);
            this.tableTemplatePreview.ResumeLayout(false);
            this.tableTemplatePreview.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTemplate)).EndInit();
            this.flowTemplateActions.ResumeLayout(false);
            this.tabPageRun.ResumeLayout(false);
            this.tableRun.ResumeLayout(false);
            this.tableRun.PerformLayout();
            this.panelFooter.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TabControl tabControlParams;
        private System.Windows.Forms.TabPage tabPageBasic;
        private System.Windows.Forms.TabPage tabPageTemplate;
        private System.Windows.Forms.TabPage tabPageRun;
        private System.Windows.Forms.SplitContainer splitContainerBasic;
        private System.Windows.Forms.TableLayoutPanel tableBasic;
        private System.Windows.Forms.Label labelBasicTitle;
        private System.Windows.Forms.Label label3;
        private TDJS_Vision.Node.NodeSubscription nodeSubscription1;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Panel panelBasicLine;
        private System.Windows.Forms.Label labelRoiTitle;
        private System.Windows.Forms.CheckBox checkBoxAllSearch;
        private System.Windows.Forms.FlowLayoutPanel flowSearchActions;
        private System.Windows.Forms.Button buttonResetRoi;
        private System.Windows.Forms.Button buttonConfirmSearchRoi;
        private System.Windows.Forms.Label labelSearchStatus;
        private System.Windows.Forms.Panel panelDisplayLine;
        private System.Windows.Forms.Label labelDisplayTitle;
        private System.Windows.Forms.CheckBox checkBoxShowMatchBox;
        private System.Windows.Forms.CheckBox checkBoxShowOutline;
        private System.Windows.Forms.Label labelResultSummary;
        private System.Windows.Forms.GroupBox groupBoxSource;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControlSource;
        private System.Windows.Forms.SplitContainer splitContainerTemplate;
        private System.Windows.Forms.TableLayoutPanel tableTemplateList;
        private System.Windows.Forms.FlowLayoutPanel flowTemplateToolbar;
        private System.Windows.Forms.Button buttonCreateTemplate;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TextBox textBoxModelPath;
        private System.Windows.Forms.ListBox listBoxTemplate;
        private System.Windows.Forms.TableLayoutPanel tableTemplatePreview;
        private System.Windows.Forms.Label labelTemplatePreview;
        private System.Windows.Forms.PictureBox pictureBoxTemplate;
        private System.Windows.Forms.FlowLayoutPanel flowTemplateActions;
        private System.Windows.Forms.Button buttonEditTemplate;
        private System.Windows.Forms.Button buttonRestoreTemplate;
        private System.Windows.Forms.Button buttonDeleteTemplate;
        private System.Windows.Forms.TableLayoutPanel tableRun;
        private System.Windows.Forms.Label labelRunTitle;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxMinScore;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBoxResultNum;
        private System.Windows.Forms.Label labelSortMode;
        private System.Windows.Forms.ComboBox comboBoxSortMode;
        private System.Windows.Forms.Label labelToleranceAngle;
        private System.Windows.Forms.TextBox textBoxToleranceAngle;
        private System.Windows.Forms.Label labelAngleStep;
        private System.Windows.Forms.TextBox textBoxAngleStep;
        private System.Windows.Forms.Label labelMaxOverlap;
        private System.Windows.Forms.TextBox textBoxMaxOverlap;
        private System.Windows.Forms.CheckBox checkBoxCoarseMatch;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Button buttonContinuousRun;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
