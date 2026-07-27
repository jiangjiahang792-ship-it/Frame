namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    partial class NodeParamFormResultOverlayDraw
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// 设计器支持所需的方法，请勿使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormResultOverlayDraw));
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelLeft = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxBasic = new System.Windows.Forms.GroupBox();
            this.labelImage = new System.Windows.Forms.Label();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.flowLayoutPanelColors = new System.Windows.Forms.FlowLayoutPanel();
            this.labelOkColor = new System.Windows.Forms.Label();
            this.panelOkColor = new System.Windows.Forms.Panel();
            this.buttonChooseOkColor = new System.Windows.Forms.Button();
            this.labelNgColor = new System.Windows.Forms.Label();
            this.panelNgColor = new System.Windows.Forms.Panel();
            this.buttonChooseNgColor = new System.Windows.Forms.Button();
            this.groupBoxItems = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelItems = new System.Windows.Forms.TableLayoutPanel();
            this.flowLayoutPanelItemButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonAddText = new System.Windows.Forms.Button();
            this.buttonAddRoi = new System.Windows.Forms.Button();
            this.buttonDeleteItem = new System.Windows.Forms.Button();
            this.dataGridViewItems = new System.Windows.Forms.DataGridView();
            this.ColumnItemEnabled = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnItemType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnItemSummary = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBoxItemSetting = new System.Windows.Forms.GroupBox();
            this.flowLayoutPanelSettings = new System.Windows.Forms.FlowLayoutPanel();
            this.panelCommonSetting = new System.Windows.Forms.Panel();
            this.labelEnabled = new System.Windows.Forms.Label();
            this.checkBoxEnabled = new System.Windows.Forms.CheckBox();
            this.labelCurrentType = new System.Windows.Forms.Label();
            this.labelCurrentTypeValue = new System.Windows.Forms.Label();
            this.panelSourceSetting = new System.Windows.Forms.Panel();
            this.labelSource = new System.Windows.Forms.Label();
            this.nodeSubscriptionSource = new TDJS_Vision.Node.NodeSubscription();
            this.panelManualText = new System.Windows.Forms.Panel();
            this.labelManualText = new System.Windows.Forms.Label();
            this.checkBoxManualText = new System.Windows.Forms.CheckBox();
            this.textBoxManualText = new System.Windows.Forms.TextBox();
            this.panelTextPrefix = new System.Windows.Forms.Panel();
            this.labelTextPrefix = new System.Windows.Forms.Label();
            this.textBoxTextPrefix = new System.Windows.Forms.TextBox();
            this.panelTextStyle = new System.Windows.Forms.Panel();
            this.labelFontSize = new System.Windows.Forms.Label();
            this.numericFontSize = new System.Windows.Forms.NumericUpDown();
            this.labelTextPosition = new System.Windows.Forms.Label();
            this.comboBoxTextPosition = new System.Windows.Forms.ComboBox();
            this.panelRoiStyle = new System.Windows.Forms.Panel();
            this.labelLineWidth = new System.Windows.Forms.Label();
            this.numericLineWidth = new System.Windows.Forms.NumericUpDown();
            this.buttonToggleAdvanced = new System.Windows.Forms.Button();
            this.panelAdvanced = new System.Windows.Forms.Panel();
            this.labelTextCoordinateMode = new System.Windows.Forms.Label();
            this.comboBoxTextCoordinateMode = new System.Windows.Forms.ComboBox();
            this.labelMargin = new System.Windows.Forms.Label();
            this.numericMargin = new System.Windows.Forms.NumericUpDown();
            this.flowLayoutPanelBottom = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonPreview = new System.Windows.Forms.Button();
            this.groupBoxPreview = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelPreview = new System.Windows.Forms.TableLayoutPanel();
            this.labelPreviewState = new System.Windows.Forms.Label();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.tableLayoutPanelLeft.SuspendLayout();
            this.groupBoxBasic.SuspendLayout();
            this.flowLayoutPanelColors.SuspendLayout();
            this.groupBoxItems.SuspendLayout();
            this.tableLayoutPanelItems.SuspendLayout();
            this.flowLayoutPanelItemButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewItems)).BeginInit();
            this.groupBoxItemSetting.SuspendLayout();
            this.flowLayoutPanelSettings.SuspendLayout();
            this.panelCommonSetting.SuspendLayout();
            this.panelSourceSetting.SuspendLayout();
            this.panelManualText.SuspendLayout();
            this.panelTextPrefix.SuspendLayout();
            this.panelTextStyle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFontSize)).BeginInit();
            this.panelRoiStyle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericLineWidth)).BeginInit();
            this.panelAdvanced.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMargin)).BeginInit();
            this.flowLayoutPanelBottom.SuspendLayout();
            this.groupBoxPreview.SuspendLayout();
            this.tableLayoutPanelPreview.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanelRoot
            //
            this.tableLayoutPanelRoot.ColumnCount = 2;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 610F));
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Controls.Add(this.tableLayoutPanelLeft, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.groupBoxPreview, 1, 0);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelRoot.Name = "tableLayoutPanelRoot";
            this.tableLayoutPanelRoot.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutPanelRoot.RowCount = 1;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Size = new System.Drawing.Size(1346, 860);
            this.tableLayoutPanelRoot.TabIndex = 0;
            //
            // tableLayoutPanelLeft
            //
            this.tableLayoutPanelLeft.ColumnCount = 1;
            this.tableLayoutPanelLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxBasic, 0, 0);
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxItems, 0, 1);
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxItemSetting, 0, 2);
            this.tableLayoutPanelLeft.Controls.Add(this.flowLayoutPanelBottom, 0, 3);
            this.tableLayoutPanelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelLeft.Location = new System.Drawing.Point(13, 13);
            this.tableLayoutPanelLeft.Name = "tableLayoutPanelLeft";
            this.tableLayoutPanelLeft.RowCount = 4;
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 255F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableLayoutPanelLeft.Size = new System.Drawing.Size(604, 834);
            this.tableLayoutPanelLeft.TabIndex = 0;
            //
            // groupBoxBasic
            //
            this.groupBoxBasic.Controls.Add(this.labelImage);
            this.groupBoxBasic.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxBasic.Controls.Add(this.flowLayoutPanelColors);
            this.groupBoxBasic.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxBasic.Location = new System.Drawing.Point(3, 3);
            this.groupBoxBasic.Name = "groupBoxBasic";
            this.groupBoxBasic.Size = new System.Drawing.Size(598, 134);
            this.groupBoxBasic.TabIndex = 0;
            this.groupBoxBasic.TabStop = false;
            this.groupBoxBasic.Text = "基础设置";
            //
            // labelImage
            //
            this.labelImage.AutoSize = true;
            this.labelImage.Location = new System.Drawing.Point(19, 43);
            this.labelImage.Name = "labelImage";
            this.labelImage.Size = new System.Drawing.Size(80, 18);
            this.labelImage.TabIndex = 0;
            this.labelImage.Text = "输入图像";
            //
            // nodeSubscriptionImage
            //
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(105, 23);
            this.nodeSubscriptionImage.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nodeSubscriptionImage.MinimumSize = new System.Drawing.Size(260, 60);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(475, 60);
            this.nodeSubscriptionImage.TabIndex = 1;
            //
            // flowLayoutPanelColors
            //
            this.flowLayoutPanelColors.Controls.Add(this.labelOkColor);
            this.flowLayoutPanelColors.Controls.Add(this.panelOkColor);
            this.flowLayoutPanelColors.Controls.Add(this.buttonChooseOkColor);
            this.flowLayoutPanelColors.Controls.Add(this.labelNgColor);
            this.flowLayoutPanelColors.Controls.Add(this.panelNgColor);
            this.flowLayoutPanelColors.Controls.Add(this.buttonChooseNgColor);
            this.flowLayoutPanelColors.Location = new System.Drawing.Point(10, 88);
            this.flowLayoutPanelColors.Name = "flowLayoutPanelColors";
            this.flowLayoutPanelColors.Size = new System.Drawing.Size(570, 36);
            this.flowLayoutPanelColors.TabIndex = 2;
            this.flowLayoutPanelColors.WrapContents = false;
            //
            // labelOkColor
            //
            this.labelOkColor.AutoSize = true;
            this.labelOkColor.Location = new System.Drawing.Point(3, 8);
            this.labelOkColor.Margin = new System.Windows.Forms.Padding(3, 8, 3, 0);
            this.labelOkColor.Name = "labelOkColor";
            this.labelOkColor.Size = new System.Drawing.Size(62, 18);
            this.labelOkColor.TabIndex = 0;
            this.labelOkColor.Text = "OK颜色";
            //
            // panelOkColor
            //
            this.panelOkColor.BackColor = System.Drawing.Color.Lime;
            this.panelOkColor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelOkColor.Location = new System.Drawing.Point(71, 5);
            this.panelOkColor.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.panelOkColor.Name = "panelOkColor";
            this.panelOkColor.Size = new System.Drawing.Size(42, 25);
            this.panelOkColor.TabIndex = 1;
            //
            // buttonChooseOkColor
            //
            this.buttonChooseOkColor.Location = new System.Drawing.Point(119, 3);
            this.buttonChooseOkColor.Name = "buttonChooseOkColor";
            this.buttonChooseOkColor.Size = new System.Drawing.Size(62, 30);
            this.buttonChooseOkColor.TabIndex = 2;
            this.buttonChooseOkColor.Text = "选择";
            this.buttonChooseOkColor.UseVisualStyleBackColor = true;
            this.buttonChooseOkColor.Click += new System.EventHandler(this.buttonChooseOkColor_Click);
            //
            // labelNgColor
            //
            this.labelNgColor.AutoSize = true;
            this.labelNgColor.Location = new System.Drawing.Point(202, 8);
            this.labelNgColor.Margin = new System.Windows.Forms.Padding(18, 8, 3, 0);
            this.labelNgColor.Name = "labelNgColor";
            this.labelNgColor.Size = new System.Drawing.Size(62, 18);
            this.labelNgColor.TabIndex = 3;
            this.labelNgColor.Text = "NG颜色";
            //
            // panelNgColor
            //
            this.panelNgColor.BackColor = System.Drawing.Color.Red;
            this.panelNgColor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelNgColor.Location = new System.Drawing.Point(270, 5);
            this.panelNgColor.Margin = new System.Windows.Forms.Padding(3, 5, 3, 3);
            this.panelNgColor.Name = "panelNgColor";
            this.panelNgColor.Size = new System.Drawing.Size(42, 25);
            this.panelNgColor.TabIndex = 4;
            //
            // buttonChooseNgColor
            //
            this.buttonChooseNgColor.Location = new System.Drawing.Point(318, 3);
            this.buttonChooseNgColor.Name = "buttonChooseNgColor";
            this.buttonChooseNgColor.Size = new System.Drawing.Size(62, 30);
            this.buttonChooseNgColor.TabIndex = 5;
            this.buttonChooseNgColor.Text = "选择";
            this.buttonChooseNgColor.UseVisualStyleBackColor = true;
            this.buttonChooseNgColor.Click += new System.EventHandler(this.buttonChooseNgColor_Click);
            //
            // groupBoxItems
            //
            this.groupBoxItems.Controls.Add(this.tableLayoutPanelItems);
            this.groupBoxItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxItems.Location = new System.Drawing.Point(3, 143);
            this.groupBoxItems.Name = "groupBoxItems";
            this.groupBoxItems.Size = new System.Drawing.Size(598, 249);
            this.groupBoxItems.TabIndex = 1;
            this.groupBoxItems.TabStop = false;
            this.groupBoxItems.Text = "绘制项";
            //
            // tableLayoutPanelItems
            //
            this.tableLayoutPanelItems.ColumnCount = 1;
            this.tableLayoutPanelItems.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelItems.Controls.Add(this.flowLayoutPanelItemButtons, 0, 0);
            this.tableLayoutPanelItems.Controls.Add(this.dataGridViewItems, 0, 1);
            this.tableLayoutPanelItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelItems.Location = new System.Drawing.Point(3, 24);
            this.tableLayoutPanelItems.Name = "tableLayoutPanelItems";
            this.tableLayoutPanelItems.RowCount = 2;
            this.tableLayoutPanelItems.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelItems.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelItems.Size = new System.Drawing.Size(592, 222);
            this.tableLayoutPanelItems.TabIndex = 0;
            //
            // flowLayoutPanelItemButtons
            //
            this.flowLayoutPanelItemButtons.Controls.Add(this.buttonAddText);
            this.flowLayoutPanelItemButtons.Controls.Add(this.buttonAddRoi);
            this.flowLayoutPanelItemButtons.Controls.Add(this.buttonDeleteItem);
            this.flowLayoutPanelItemButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelItemButtons.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanelItemButtons.Name = "flowLayoutPanelItemButtons";
            this.flowLayoutPanelItemButtons.Size = new System.Drawing.Size(586, 36);
            this.flowLayoutPanelItemButtons.TabIndex = 0;
            //
            // buttonAddText
            //
            this.buttonAddText.Location = new System.Drawing.Point(3, 3);
            this.buttonAddText.Name = "buttonAddText";
            this.buttonAddText.Size = new System.Drawing.Size(100, 31);
            this.buttonAddText.TabIndex = 0;
            this.buttonAddText.Text = "添加文本";
            this.buttonAddText.UseVisualStyleBackColor = true;
            this.buttonAddText.Click += new System.EventHandler(this.buttonAddText_Click);
            //
            // buttonAddRoi
            //
            this.buttonAddRoi.Location = new System.Drawing.Point(109, 3);
            this.buttonAddRoi.Name = "buttonAddRoi";
            this.buttonAddRoi.Size = new System.Drawing.Size(100, 31);
            this.buttonAddRoi.TabIndex = 1;
            this.buttonAddRoi.Text = "添加ROI";
            this.buttonAddRoi.UseVisualStyleBackColor = true;
            this.buttonAddRoi.Click += new System.EventHandler(this.buttonAddRoi_Click);
            //
            // buttonDeleteItem
            //
            this.buttonDeleteItem.Location = new System.Drawing.Point(215, 3);
            this.buttonDeleteItem.Name = "buttonDeleteItem";
            this.buttonDeleteItem.Size = new System.Drawing.Size(80, 31);
            this.buttonDeleteItem.TabIndex = 2;
            this.buttonDeleteItem.Text = "删除";
            this.buttonDeleteItem.UseVisualStyleBackColor = true;
            this.buttonDeleteItem.Click += new System.EventHandler(this.buttonDeleteItem_Click);
            //
            // dataGridViewItems
            //
            this.dataGridViewItems.AllowUserToAddRows = false;
            this.dataGridViewItems.AllowUserToDeleteRows = false;
            this.dataGridViewItems.AllowUserToResizeRows = false;
            this.dataGridViewItems.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewItems.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewItems.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewItems.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnItemEnabled,
            this.ColumnItemType,
            this.ColumnItemSummary});
            this.dataGridViewItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewItems.Location = new System.Drawing.Point(3, 45);
            this.dataGridViewItems.MultiSelect = false;
            this.dataGridViewItems.Name = "dataGridViewItems";
            this.dataGridViewItems.ReadOnly = true;
            this.dataGridViewItems.RowHeadersVisible = false;
            this.dataGridViewItems.RowHeadersWidth = 62;
            this.dataGridViewItems.RowTemplate.Height = 27;
            this.dataGridViewItems.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewItems.Size = new System.Drawing.Size(586, 174);
            this.dataGridViewItems.TabIndex = 1;
            this.dataGridViewItems.SelectionChanged += new System.EventHandler(this.dataGridViewItems_SelectionChanged);
            //
            // ColumnItemEnabled
            //
            this.ColumnItemEnabled.FillWeight = 18F;
            this.ColumnItemEnabled.HeaderText = "启用";
            this.ColumnItemEnabled.MinimumWidth = 8;
            this.ColumnItemEnabled.Name = "ColumnItemEnabled";
            this.ColumnItemEnabled.ReadOnly = true;
            //
            // ColumnItemType
            //
            this.ColumnItemType.FillWeight = 30F;
            this.ColumnItemType.HeaderText = "类型";
            this.ColumnItemType.MinimumWidth = 8;
            this.ColumnItemType.Name = "ColumnItemType";
            this.ColumnItemType.ReadOnly = true;
            //
            // ColumnItemSummary
            //
            this.ColumnItemSummary.FillWeight = 70F;
            this.ColumnItemSummary.HeaderText = "内容/来源";
            this.ColumnItemSummary.MinimumWidth = 8;
            this.ColumnItemSummary.Name = "ColumnItemSummary";
            this.ColumnItemSummary.ReadOnly = true;
            //
            // groupBoxItemSetting
            //
            this.groupBoxItemSetting.Controls.Add(this.flowLayoutPanelSettings);
            this.groupBoxItemSetting.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxItemSetting.Location = new System.Drawing.Point(3, 398);
            this.groupBoxItemSetting.Name = "groupBoxItemSetting";
            this.groupBoxItemSetting.Size = new System.Drawing.Size(598, 381);
            this.groupBoxItemSetting.TabIndex = 2;
            this.groupBoxItemSetting.TabStop = false;
            this.groupBoxItemSetting.Text = "当前项设置";
            //
            // flowLayoutPanelSettings
            //
            this.flowLayoutPanelSettings.AutoScroll = true;
            this.flowLayoutPanelSettings.Controls.Add(this.panelCommonSetting);
            this.flowLayoutPanelSettings.Controls.Add(this.panelSourceSetting);
            this.flowLayoutPanelSettings.Controls.Add(this.panelManualText);
            this.flowLayoutPanelSettings.Controls.Add(this.panelTextPrefix);
            this.flowLayoutPanelSettings.Controls.Add(this.panelTextStyle);
            this.flowLayoutPanelSettings.Controls.Add(this.panelRoiStyle);
            this.flowLayoutPanelSettings.Controls.Add(this.buttonToggleAdvanced);
            this.flowLayoutPanelSettings.Controls.Add(this.panelAdvanced);
            this.flowLayoutPanelSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelSettings.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanelSettings.Location = new System.Drawing.Point(3, 24);
            this.flowLayoutPanelSettings.Name = "flowLayoutPanelSettings";
            this.flowLayoutPanelSettings.Padding = new System.Windows.Forms.Padding(4);
            this.flowLayoutPanelSettings.Size = new System.Drawing.Size(592, 354);
            this.flowLayoutPanelSettings.TabIndex = 0;
            this.flowLayoutPanelSettings.WrapContents = false;
            //
            // panelCommonSetting
            //
            this.panelCommonSetting.Controls.Add(this.labelEnabled);
            this.panelCommonSetting.Controls.Add(this.checkBoxEnabled);
            this.panelCommonSetting.Controls.Add(this.labelCurrentType);
            this.panelCommonSetting.Controls.Add(this.labelCurrentTypeValue);
            this.panelCommonSetting.Location = new System.Drawing.Point(7, 7);
            this.panelCommonSetting.Name = "panelCommonSetting";
            this.panelCommonSetting.Size = new System.Drawing.Size(554, 36);
            this.panelCommonSetting.TabIndex = 0;
            //
            // labelEnabled
            //
            this.labelEnabled.AutoSize = true;
            this.labelEnabled.Location = new System.Drawing.Point(4, 9);
            this.labelEnabled.Name = "labelEnabled";
            this.labelEnabled.Size = new System.Drawing.Size(44, 18);
            this.labelEnabled.TabIndex = 0;
            this.labelEnabled.Text = "启用";
            //
            // checkBoxEnabled
            //
            this.checkBoxEnabled.AutoSize = true;
            this.checkBoxEnabled.Location = new System.Drawing.Point(68, 8);
            this.checkBoxEnabled.Name = "checkBoxEnabled";
            this.checkBoxEnabled.Size = new System.Drawing.Size(52, 22);
            this.checkBoxEnabled.TabIndex = 1;
            this.checkBoxEnabled.Text = "是";
            this.checkBoxEnabled.UseVisualStyleBackColor = true;
            this.checkBoxEnabled.CheckedChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // labelCurrentType
            //
            this.labelCurrentType.AutoSize = true;
            this.labelCurrentType.Location = new System.Drawing.Point(185, 9);
            this.labelCurrentType.Name = "labelCurrentType";
            this.labelCurrentType.Size = new System.Drawing.Size(44, 18);
            this.labelCurrentType.TabIndex = 2;
            this.labelCurrentType.Text = "类型";
            //
            // labelCurrentTypeValue
            //
            this.labelCurrentTypeValue.AutoSize = true;
            this.labelCurrentTypeValue.ForeColor = System.Drawing.Color.Teal;
            this.labelCurrentTypeValue.Location = new System.Drawing.Point(248, 9);
            this.labelCurrentTypeValue.Name = "labelCurrentTypeValue";
            this.labelCurrentTypeValue.Size = new System.Drawing.Size(62, 18);
            this.labelCurrentTypeValue.TabIndex = 3;
            this.labelCurrentTypeValue.Text = "未选择";
            //
            // panelSourceSetting
            //
            this.panelSourceSetting.Controls.Add(this.labelSource);
            this.panelSourceSetting.Controls.Add(this.nodeSubscriptionSource);
            this.panelSourceSetting.Location = new System.Drawing.Point(7, 49);
            this.panelSourceSetting.Name = "panelSourceSetting";
            this.panelSourceSetting.Size = new System.Drawing.Size(554, 73);
            this.panelSourceSetting.TabIndex = 1;
            //
            // labelSource
            //
            this.labelSource.AutoSize = true;
            this.labelSource.Location = new System.Drawing.Point(4, 10);
            this.labelSource.Name = "labelSource";
            this.labelSource.Size = new System.Drawing.Size(44, 18);
            this.labelSource.TabIndex = 0;
            this.labelSource.Text = "订阅";
            //
            // nodeSubscriptionSource
            //
            this.nodeSubscriptionSource.Location = new System.Drawing.Point(68, 3);
            this.nodeSubscriptionSource.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nodeSubscriptionSource.MinimumSize = new System.Drawing.Size(260, 60);
            this.nodeSubscriptionSource.Name = "nodeSubscriptionSource";
            this.nodeSubscriptionSource.Size = new System.Drawing.Size(475, 60);
            this.nodeSubscriptionSource.TabIndex = 1;
            //
            // panelManualText
            //
            this.panelManualText.Controls.Add(this.labelManualText);
            this.panelManualText.Controls.Add(this.checkBoxManualText);
            this.panelManualText.Controls.Add(this.textBoxManualText);
            this.panelManualText.Location = new System.Drawing.Point(7, 128);
            this.panelManualText.Name = "panelManualText";
            this.panelManualText.Size = new System.Drawing.Size(554, 38);
            this.panelManualText.TabIndex = 2;
            //
            // labelManualText
            //
            this.labelManualText.AutoSize = true;
            this.labelManualText.Location = new System.Drawing.Point(4, 10);
            this.labelManualText.Name = "labelManualText";
            this.labelManualText.Size = new System.Drawing.Size(44, 18);
            this.labelManualText.TabIndex = 0;
            this.labelManualText.Text = "手动";
            //
            // checkBoxManualText
            //
            this.checkBoxManualText.AutoSize = true;
            this.checkBoxManualText.Location = new System.Drawing.Point(68, 9);
            this.checkBoxManualText.Name = "checkBoxManualText";
            this.checkBoxManualText.Size = new System.Drawing.Size(52, 22);
            this.checkBoxManualText.TabIndex = 1;
            this.checkBoxManualText.Text = "是";
            this.checkBoxManualText.UseVisualStyleBackColor = true;
            this.checkBoxManualText.CheckedChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // textBoxManualText
            //
            this.textBoxManualText.Location = new System.Drawing.Point(135, 5);
            this.textBoxManualText.Name = "textBoxManualText";
            this.textBoxManualText.Size = new System.Drawing.Size(408, 28);
            this.textBoxManualText.TabIndex = 2;
            this.textBoxManualText.TextChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // panelTextPrefix
            //
            this.panelTextPrefix.Controls.Add(this.labelTextPrefix);
            this.panelTextPrefix.Controls.Add(this.textBoxTextPrefix);
            this.panelTextPrefix.Location = new System.Drawing.Point(7, 172);
            this.panelTextPrefix.Name = "panelTextPrefix";
            this.panelTextPrefix.Size = new System.Drawing.Size(554, 38);
            this.panelTextPrefix.TabIndex = 3;
            //
            // labelTextPrefix
            //
            this.labelTextPrefix.AutoSize = true;
            this.labelTextPrefix.Location = new System.Drawing.Point(4, 10);
            this.labelTextPrefix.Name = "labelTextPrefix";
            this.labelTextPrefix.Size = new System.Drawing.Size(44, 18);
            this.labelTextPrefix.TabIndex = 0;
            this.labelTextPrefix.Text = "前缀";
            //
            // textBoxTextPrefix
            //
            this.textBoxTextPrefix.Location = new System.Drawing.Point(68, 5);
            this.textBoxTextPrefix.Name = "textBoxTextPrefix";
            this.textBoxTextPrefix.Size = new System.Drawing.Size(475, 28);
            this.textBoxTextPrefix.TabIndex = 1;
            this.textBoxTextPrefix.TextChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // panelTextStyle
            //
            this.panelTextStyle.Controls.Add(this.labelFontSize);
            this.panelTextStyle.Controls.Add(this.numericFontSize);
            this.panelTextStyle.Controls.Add(this.labelTextPosition);
            this.panelTextStyle.Controls.Add(this.comboBoxTextPosition);
            this.panelTextStyle.Location = new System.Drawing.Point(7, 216);
            this.panelTextStyle.Name = "panelTextStyle";
            this.panelTextStyle.Size = new System.Drawing.Size(554, 38);
            this.panelTextStyle.TabIndex = 4;
            //
            // labelFontSize
            //
            this.labelFontSize.AutoSize = true;
            this.labelFontSize.Location = new System.Drawing.Point(4, 10);
            this.labelFontSize.Name = "labelFontSize";
            this.labelFontSize.Size = new System.Drawing.Size(44, 18);
            this.labelFontSize.TabIndex = 0;
            this.labelFontSize.Text = "字号";
            //
            // numericFontSize
            //
            this.numericFontSize.Location = new System.Drawing.Point(68, 5);
            this.numericFontSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericFontSize.Name = "numericFontSize";
            this.numericFontSize.Size = new System.Drawing.Size(76, 28);
            this.numericFontSize.TabIndex = 1;
            this.numericFontSize.Value = new decimal(new int[] {
            18,
            0,
            0,
            0});
            this.numericFontSize.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // labelTextPosition
            //
            this.labelTextPosition.AutoSize = true;
            this.labelTextPosition.Location = new System.Drawing.Point(180, 10);
            this.labelTextPosition.Name = "labelTextPosition";
            this.labelTextPosition.Size = new System.Drawing.Size(44, 18);
            this.labelTextPosition.TabIndex = 2;
            this.labelTextPosition.Text = "位置";
            //
            // comboBoxTextPosition
            //
            this.comboBoxTextPosition.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTextPosition.FormattingEnabled = true;
            this.comboBoxTextPosition.Location = new System.Drawing.Point(242, 5);
            this.comboBoxTextPosition.Name = "comboBoxTextPosition";
            this.comboBoxTextPosition.Size = new System.Drawing.Size(200, 26);
            this.comboBoxTextPosition.TabIndex = 3;
            this.comboBoxTextPosition.SelectedIndexChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // panelRoiStyle
            //
            this.panelRoiStyle.Controls.Add(this.labelLineWidth);
            this.panelRoiStyle.Controls.Add(this.numericLineWidth);
            this.panelRoiStyle.Location = new System.Drawing.Point(7, 260);
            this.panelRoiStyle.Name = "panelRoiStyle";
            this.panelRoiStyle.Size = new System.Drawing.Size(554, 38);
            this.panelRoiStyle.TabIndex = 5;
            //
            // labelLineWidth
            //
            this.labelLineWidth.AutoSize = true;
            this.labelLineWidth.Location = new System.Drawing.Point(4, 10);
            this.labelLineWidth.Name = "labelLineWidth";
            this.labelLineWidth.Size = new System.Drawing.Size(44, 18);
            this.labelLineWidth.TabIndex = 0;
            this.labelLineWidth.Text = "线宽";
            //
            // numericLineWidth
            //
            this.numericLineWidth.Location = new System.Drawing.Point(68, 5);
            this.numericLineWidth.Maximum = new decimal(new int[] {
            20,
            0,
            0,
            0});
            this.numericLineWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericLineWidth.Name = "numericLineWidth";
            this.numericLineWidth.Size = new System.Drawing.Size(76, 28);
            this.numericLineWidth.TabIndex = 1;
            this.numericLineWidth.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numericLineWidth.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // buttonToggleAdvanced
            //
            this.buttonToggleAdvanced.Location = new System.Drawing.Point(7, 304);
            this.buttonToggleAdvanced.Name = "buttonToggleAdvanced";
            this.buttonToggleAdvanced.Size = new System.Drawing.Size(148, 31);
            this.buttonToggleAdvanced.TabIndex = 6;
            this.buttonToggleAdvanced.Text = "展开高级设置";
            this.buttonToggleAdvanced.UseVisualStyleBackColor = true;
            this.buttonToggleAdvanced.Click += new System.EventHandler(this.buttonToggleAdvanced_Click);
            //
            // panelAdvanced
            //
            this.panelAdvanced.Controls.Add(this.labelTextCoordinateMode);
            this.panelAdvanced.Controls.Add(this.comboBoxTextCoordinateMode);
            this.panelAdvanced.Controls.Add(this.labelMargin);
            this.panelAdvanced.Controls.Add(this.numericMargin);
            this.panelAdvanced.Location = new System.Drawing.Point(7, 341);
            this.panelAdvanced.Name = "panelAdvanced";
            this.panelAdvanced.Size = new System.Drawing.Size(554, 40);
            this.panelAdvanced.TabIndex = 7;
            //
            // labelTextCoordinateMode
            //
            this.labelTextCoordinateMode.AutoSize = true;
            this.labelTextCoordinateMode.Location = new System.Drawing.Point(4, 11);
            this.labelTextCoordinateMode.Name = "labelTextCoordinateMode";
            this.labelTextCoordinateMode.Size = new System.Drawing.Size(62, 18);
            this.labelTextCoordinateMode.TabIndex = 0;
            this.labelTextCoordinateMode.Text = "坐标系";
            //
            // comboBoxTextCoordinateMode
            //
            this.comboBoxTextCoordinateMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTextCoordinateMode.FormattingEnabled = true;
            this.comboBoxTextCoordinateMode.Location = new System.Drawing.Point(77, 6);
            this.comboBoxTextCoordinateMode.Name = "comboBoxTextCoordinateMode";
            this.comboBoxTextCoordinateMode.Size = new System.Drawing.Size(160, 26);
            this.comboBoxTextCoordinateMode.TabIndex = 1;
            this.comboBoxTextCoordinateMode.SelectedIndexChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // labelMargin
            //
            this.labelMargin.AutoSize = true;
            this.labelMargin.Location = new System.Drawing.Point(275, 11);
            this.labelMargin.Name = "labelMargin";
            this.labelMargin.Size = new System.Drawing.Size(44, 18);
            this.labelMargin.TabIndex = 2;
            this.labelMargin.Text = "边距";
            //
            // numericMargin
            //
            this.numericMargin.Location = new System.Drawing.Point(337, 6);
            this.numericMargin.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericMargin.Name = "numericMargin";
            this.numericMargin.Size = new System.Drawing.Size(76, 28);
            this.numericMargin.TabIndex = 3;
            this.numericMargin.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericMargin.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            //
            // flowLayoutPanelBottom
            //
            this.flowLayoutPanelBottom.Controls.Add(this.buttonSave);
            this.flowLayoutPanelBottom.Controls.Add(this.buttonPreview);
            this.flowLayoutPanelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelBottom.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelBottom.Location = new System.Drawing.Point(3, 785);
            this.flowLayoutPanelBottom.Name = "flowLayoutPanelBottom";
            this.flowLayoutPanelBottom.Padding = new System.Windows.Forms.Padding(4);
            this.flowLayoutPanelBottom.Size = new System.Drawing.Size(598, 46);
            this.flowLayoutPanelBottom.TabIndex = 3;
            //
            // buttonSave
            //
            this.buttonSave.Location = new System.Drawing.Point(494, 7);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(93, 34);
            this.buttonSave.TabIndex = 0;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            //
            // buttonPreview
            //
            this.buttonPreview.Location = new System.Drawing.Point(395, 7);
            this.buttonPreview.Name = "buttonPreview";
            this.buttonPreview.Size = new System.Drawing.Size(93, 34);
            this.buttonPreview.TabIndex = 1;
            this.buttonPreview.Text = "预览";
            this.buttonPreview.UseVisualStyleBackColor = true;
            this.buttonPreview.Click += new System.EventHandler(this.buttonPreview_Click);
            //
            // groupBoxPreview
            //
            this.groupBoxPreview.Controls.Add(this.tableLayoutPanelPreview);
            this.groupBoxPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxPreview.Location = new System.Drawing.Point(623, 13);
            this.groupBoxPreview.Name = "groupBoxPreview";
            this.groupBoxPreview.Size = new System.Drawing.Size(710, 834);
            this.groupBoxPreview.TabIndex = 1;
            this.groupBoxPreview.TabStop = false;
            this.groupBoxPreview.Text = "预览";
            //
            // tableLayoutPanelPreview
            //
            this.tableLayoutPanelPreview.ColumnCount = 1;
            this.tableLayoutPanelPreview.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Controls.Add(this.labelPreviewState, 0, 0);
            this.tableLayoutPanelPreview.Controls.Add(this.showImageControl1, 0, 1);
            this.tableLayoutPanelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelPreview.Location = new System.Drawing.Point(3, 24);
            this.tableLayoutPanelPreview.Name = "tableLayoutPanelPreview";
            this.tableLayoutPanelPreview.RowCount = 2;
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelPreview.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPreview.Size = new System.Drawing.Size(704, 807);
            this.tableLayoutPanelPreview.TabIndex = 0;
            //
            // labelPreviewState
            //
            this.labelPreviewState.AutoEllipsis = true;
            this.labelPreviewState.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelPreviewState.Location = new System.Drawing.Point(3, 0);
            this.labelPreviewState.Name = "labelPreviewState";
            this.labelPreviewState.Size = new System.Drawing.Size(698, 40);
            this.labelPreviewState.TabIndex = 0;
            this.labelPreviewState.Text = "点击预览查看绘制效果";
            this.labelPreviewState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // showImageControl1
            //
            this.showImageControl1.BackColor = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundImageCustom = null;
            this.showImageControl1.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControl1.BackgroundTransparency = 1F;
            this.showImageControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControl1.Location = new System.Drawing.Point(3, 43);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.RoiColor = System.Drawing.Color.Lime;
            this.showImageControl1.ShowCheckerBackground = false;
            this.showImageControl1.ShowPixelInfo = false;
            this.showImageControl1.Size = new System.Drawing.Size(698, 761);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 1;
            //
            // NodeParamFormResultOverlayDraw
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1350, 900);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.MinimizeBox = false;
            this.Name = "NodeParamFormResultOverlayDraw";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ROI结果绘制";
            this.Controls.SetChildIndex(this.tableLayoutPanelRoot, 0);
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.tableLayoutPanelLeft.ResumeLayout(false);
            this.groupBoxBasic.ResumeLayout(false);
            this.groupBoxBasic.PerformLayout();
            this.flowLayoutPanelColors.ResumeLayout(false);
            this.flowLayoutPanelColors.PerformLayout();
            this.groupBoxItems.ResumeLayout(false);
            this.tableLayoutPanelItems.ResumeLayout(false);
            this.flowLayoutPanelItemButtons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewItems)).EndInit();
            this.groupBoxItemSetting.ResumeLayout(false);
            this.flowLayoutPanelSettings.ResumeLayout(false);
            this.panelCommonSetting.ResumeLayout(false);
            this.panelCommonSetting.PerformLayout();
            this.panelSourceSetting.ResumeLayout(false);
            this.panelSourceSetting.PerformLayout();
            this.panelManualText.ResumeLayout(false);
            this.panelManualText.PerformLayout();
            this.panelTextPrefix.ResumeLayout(false);
            this.panelTextPrefix.PerformLayout();
            this.panelTextStyle.ResumeLayout(false);
            this.panelTextStyle.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFontSize)).EndInit();
            this.panelRoiStyle.ResumeLayout(false);
            this.panelRoiStyle.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericLineWidth)).EndInit();
            this.panelAdvanced.ResumeLayout(false);
            this.panelAdvanced.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMargin)).EndInit();
            this.flowLayoutPanelBottom.ResumeLayout(false);
            this.groupBoxPreview.ResumeLayout(false);
            this.tableLayoutPanelPreview.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelLeft;
        private System.Windows.Forms.GroupBox groupBoxBasic;
        private System.Windows.Forms.Label labelImage;
        private NodeSubscription nodeSubscriptionImage;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelColors;
        private System.Windows.Forms.Label labelOkColor;
        private System.Windows.Forms.Panel panelOkColor;
        private System.Windows.Forms.Button buttonChooseOkColor;
        private System.Windows.Forms.Label labelNgColor;
        private System.Windows.Forms.Panel panelNgColor;
        private System.Windows.Forms.Button buttonChooseNgColor;
        private System.Windows.Forms.GroupBox groupBoxItems;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelItems;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelItemButtons;
        private System.Windows.Forms.Button buttonAddText;
        private System.Windows.Forms.Button buttonAddRoi;
        private System.Windows.Forms.Button buttonDeleteItem;
        private System.Windows.Forms.DataGridView dataGridViewItems;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnItemEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnItemType;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnItemSummary;
        private System.Windows.Forms.GroupBox groupBoxItemSetting;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelSettings;
        private System.Windows.Forms.Panel panelCommonSetting;
        private System.Windows.Forms.Label labelEnabled;
        private System.Windows.Forms.CheckBox checkBoxEnabled;
        private System.Windows.Forms.Label labelCurrentType;
        private System.Windows.Forms.Label labelCurrentTypeValue;
        private System.Windows.Forms.Panel panelSourceSetting;
        private System.Windows.Forms.Label labelSource;
        private NodeSubscription nodeSubscriptionSource;
        private System.Windows.Forms.Panel panelManualText;
        private System.Windows.Forms.Label labelManualText;
        private System.Windows.Forms.CheckBox checkBoxManualText;
        private System.Windows.Forms.TextBox textBoxManualText;
        private System.Windows.Forms.Panel panelTextPrefix;
        private System.Windows.Forms.Label labelTextPrefix;
        private System.Windows.Forms.TextBox textBoxTextPrefix;
        private System.Windows.Forms.Panel panelTextStyle;
        private System.Windows.Forms.Label labelFontSize;
        private System.Windows.Forms.NumericUpDown numericFontSize;
        private System.Windows.Forms.Label labelTextPosition;
        private System.Windows.Forms.ComboBox comboBoxTextPosition;
        private System.Windows.Forms.Panel panelRoiStyle;
        private System.Windows.Forms.Label labelLineWidth;
        private System.Windows.Forms.NumericUpDown numericLineWidth;
        private System.Windows.Forms.Button buttonToggleAdvanced;
        private System.Windows.Forms.Panel panelAdvanced;
        private System.Windows.Forms.Label labelTextCoordinateMode;
        private System.Windows.Forms.ComboBox comboBoxTextCoordinateMode;
        private System.Windows.Forms.Label labelMargin;
        private System.Windows.Forms.NumericUpDown numericMargin;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelBottom;
        private System.Windows.Forms.Button buttonPreview;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.GroupBox groupBoxPreview;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPreview;
        private System.Windows.Forms.Label labelPreviewState;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
    }
}
