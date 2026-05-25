namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    partial class NodeParamFormResultOverlayDraw
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelLeft = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxImage = new System.Windows.Forms.GroupBox();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxItems = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelItems = new System.Windows.Forms.TableLayoutPanel();
            this.flowLayoutPanelAdd = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonAddText = new System.Windows.Forms.Button();
            this.buttonAddLine = new System.Windows.Forms.Button();
            this.buttonAddRectangle = new System.Windows.Forms.Button();
            this.buttonAddRegion = new System.Windows.Forms.Button();
            this.buttonDelete = new System.Windows.Forms.Button();
            this.dataGridViewItems = new System.Windows.Forms.DataGridView();
            this.ColumnEnabled = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnColor = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBoxItemSetting = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelSetting = new System.Windows.Forms.TableLayoutPanel();
            this.labelEnabled = new System.Windows.Forms.Label();
            this.checkBoxEnabled = new System.Windows.Forms.CheckBox();
            this.labelItemType = new System.Windows.Forms.Label();
            this.comboBoxItemType = new System.Windows.Forms.ComboBox();
            this.labelName = new System.Windows.Forms.Label();
            this.textBoxName = new System.Windows.Forms.TextBox();
            this.labelSource = new System.Windows.Forms.Label();
            this.nodeSubscriptionSource = new TDJS_Vision.Node.NodeSubscription();
            this.labelManualText = new System.Windows.Forms.Label();
            this.tableLayoutPanelManual = new System.Windows.Forms.TableLayoutPanel();
            this.checkBoxManualText = new System.Windows.Forms.CheckBox();
            this.textBoxManualText = new System.Windows.Forms.TextBox();
            this.labelColor = new System.Windows.Forms.Label();
            this.tableLayoutPanelColor = new System.Windows.Forms.TableLayoutPanel();
            this.panelColor = new System.Windows.Forms.Panel();
            this.buttonChooseColor = new System.Windows.Forms.Button();
            this.labelFontSize = new System.Windows.Forms.Label();
            this.numericFontSize = new System.Windows.Forms.NumericUpDown();
            this.labelLineWidth = new System.Windows.Forms.Label();
            this.numericLineWidth = new System.Windows.Forms.NumericUpDown();
            this.labelTextPosition = new System.Windows.Forms.Label();
            this.comboBoxTextPosition = new System.Windows.Forms.ComboBox();
            this.labelMargin = new System.Windows.Forms.Label();
            this.numericMargin = new System.Windows.Forms.NumericUpDown();
            this.flowLayoutPanelBottom = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonPreview = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxPreview = new System.Windows.Forms.GroupBox();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.tableLayoutPanelLeft.SuspendLayout();
            this.groupBoxImage.SuspendLayout();
            this.groupBoxItems.SuspendLayout();
            this.tableLayoutPanelItems.SuspendLayout();
            this.flowLayoutPanelAdd.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewItems)).BeginInit();
            this.groupBoxItemSetting.SuspendLayout();
            this.tableLayoutPanelSetting.SuspendLayout();
            this.tableLayoutPanelManual.SuspendLayout();
            this.tableLayoutPanelColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFontSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericLineWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMargin)).BeginInit();
            this.flowLayoutPanelBottom.SuspendLayout();
            this.groupBoxPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelRoot
            // 
            this.tableLayoutPanelRoot.ColumnCount = 2;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 465F));
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Controls.Add(this.tableLayoutPanelLeft, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.groupBoxPreview, 1, 0);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelRoot.Name = "tableLayoutPanelRoot";
            this.tableLayoutPanelRoot.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutPanelRoot.RowCount = 1;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Size = new System.Drawing.Size(1196, 710);
            this.tableLayoutPanelRoot.TabIndex = 0;
            // 
            // tableLayoutPanelLeft
            // 
            this.tableLayoutPanelLeft.ColumnCount = 1;
            this.tableLayoutPanelLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxImage, 0, 0);
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxItems, 0, 1);
            this.tableLayoutPanelLeft.Controls.Add(this.groupBoxItemSetting, 0, 2);
            this.tableLayoutPanelLeft.Controls.Add(this.flowLayoutPanelBottom, 0, 3);
            this.tableLayoutPanelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelLeft.Location = new System.Drawing.Point(13, 13);
            this.tableLayoutPanelLeft.Name = "tableLayoutPanelLeft";
            this.tableLayoutPanelLeft.RowCount = 4;
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 92F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 210F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelLeft.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52F));
            this.tableLayoutPanelLeft.Size = new System.Drawing.Size(459, 684);
            this.tableLayoutPanelLeft.TabIndex = 0;
            // 
            // groupBoxImage
            // 
            this.groupBoxImage.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxImage.Location = new System.Drawing.Point(3, 3);
            this.groupBoxImage.Name = "groupBoxImage";
            this.groupBoxImage.Size = new System.Drawing.Size(453, 86);
            this.groupBoxImage.TabIndex = 0;
            this.groupBoxImage.TabStop = false;
            this.groupBoxImage.Text = "输入图像";
            // 
            // nodeSubscriptionImage
            // 
            this.nodeSubscriptionImage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(14, 23);
            this.nodeSubscriptionImage.MinimumSize = new System.Drawing.Size(260, 50);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(423, 56);
            this.nodeSubscriptionImage.TabIndex = 0;
            // 
            // groupBoxItems
            // 
            this.groupBoxItems.Controls.Add(this.tableLayoutPanelItems);
            this.groupBoxItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxItems.Location = new System.Drawing.Point(3, 95);
            this.groupBoxItems.Name = "groupBoxItems";
            this.groupBoxItems.Size = new System.Drawing.Size(453, 204);
            this.groupBoxItems.TabIndex = 1;
            this.groupBoxItems.TabStop = false;
            this.groupBoxItems.Text = "绘制项";
            // 
            // tableLayoutPanelItems
            // 
            this.tableLayoutPanelItems.ColumnCount = 1;
            this.tableLayoutPanelItems.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelItems.Controls.Add(this.flowLayoutPanelAdd, 0, 0);
            this.tableLayoutPanelItems.Controls.Add(this.dataGridViewItems, 0, 1);
            this.tableLayoutPanelItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelItems.Location = new System.Drawing.Point(3, 21);
            this.tableLayoutPanelItems.Name = "tableLayoutPanelItems";
            this.tableLayoutPanelItems.RowCount = 2;
            this.tableLayoutPanelItems.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelItems.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelItems.Size = new System.Drawing.Size(447, 180);
            this.tableLayoutPanelItems.TabIndex = 0;
            // 
            // flowLayoutPanelAdd
            // 
            this.flowLayoutPanelAdd.Controls.Add(this.buttonAddText);
            this.flowLayoutPanelAdd.Controls.Add(this.buttonAddLine);
            this.flowLayoutPanelAdd.Controls.Add(this.buttonAddRectangle);
            this.flowLayoutPanelAdd.Controls.Add(this.buttonAddRegion);
            this.flowLayoutPanelAdd.Controls.Add(this.buttonDelete);
            this.flowLayoutPanelAdd.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelAdd.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanelAdd.Name = "flowLayoutPanelAdd";
            this.flowLayoutPanelAdd.Size = new System.Drawing.Size(441, 34);
            this.flowLayoutPanelAdd.TabIndex = 0;
            // 
            // buttonAddText
            // 
            this.buttonAddText.Location = new System.Drawing.Point(3, 3);
            this.buttonAddText.Name = "buttonAddText";
            this.buttonAddText.Size = new System.Drawing.Size(76, 28);
            this.buttonAddText.TabIndex = 0;
            this.buttonAddText.Text = "加文本";
            this.buttonAddText.UseVisualStyleBackColor = true;
            this.buttonAddText.Click += new System.EventHandler(this.buttonAddText_Click);
            // 
            // buttonAddLine
            // 
            this.buttonAddLine.Location = new System.Drawing.Point(85, 3);
            this.buttonAddLine.Name = "buttonAddLine";
            this.buttonAddLine.Size = new System.Drawing.Size(70, 28);
            this.buttonAddLine.TabIndex = 1;
            this.buttonAddLine.Text = "加线";
            this.buttonAddLine.UseVisualStyleBackColor = true;
            this.buttonAddLine.Click += new System.EventHandler(this.buttonAddLine_Click);
            // 
            // buttonAddRectangle
            // 
            this.buttonAddRectangle.Location = new System.Drawing.Point(161, 3);
            this.buttonAddRectangle.Name = "buttonAddRectangle";
            this.buttonAddRectangle.Size = new System.Drawing.Size(76, 28);
            this.buttonAddRectangle.TabIndex = 2;
            this.buttonAddRectangle.Text = "加矩形";
            this.buttonAddRectangle.UseVisualStyleBackColor = true;
            this.buttonAddRectangle.Click += new System.EventHandler(this.buttonAddRectangle_Click);
            // 
            // buttonAddRegion
            // 
            this.buttonAddRegion.Location = new System.Drawing.Point(243, 3);
            this.buttonAddRegion.Name = "buttonAddRegion";
            this.buttonAddRegion.Size = new System.Drawing.Size(86, 28);
            this.buttonAddRegion.TabIndex = 3;
            this.buttonAddRegion.Text = "加区域";
            this.buttonAddRegion.UseVisualStyleBackColor = true;
            this.buttonAddRegion.Click += new System.EventHandler(this.buttonAddRegion_Click);
            // 
            // buttonDelete
            // 
            this.buttonDelete.Location = new System.Drawing.Point(335, 3);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(70, 28);
            this.buttonDelete.TabIndex = 4;
            this.buttonDelete.Text = "删除";
            this.buttonDelete.UseVisualStyleBackColor = true;
            this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
            // 
            // dataGridViewItems
            // 
            this.dataGridViewItems.AllowUserToAddRows = false;
            this.dataGridViewItems.AllowUserToDeleteRows = false;
            this.dataGridViewItems.AllowUserToResizeRows = false;
            this.dataGridViewItems.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewItems.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewItems.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnEnabled,
            this.ColumnType,
            this.ColumnName,
            this.ColumnSource,
            this.ColumnColor});
            this.dataGridViewItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewItems.Location = new System.Drawing.Point(3, 43);
            this.dataGridViewItems.MultiSelect = false;
            this.dataGridViewItems.Name = "dataGridViewItems";
            this.dataGridViewItems.ReadOnly = true;
            this.dataGridViewItems.RowHeadersVisible = false;
            this.dataGridViewItems.RowHeadersWidth = 51;
            this.dataGridViewItems.RowTemplate.Height = 27;
            this.dataGridViewItems.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewItems.Size = new System.Drawing.Size(441, 134);
            this.dataGridViewItems.TabIndex = 1;
            this.dataGridViewItems.SelectionChanged += new System.EventHandler(this.dataGridViewItems_SelectionChanged);
            // 
            // ColumnEnabled
            // 
            this.ColumnEnabled.HeaderText = "启用";
            this.ColumnEnabled.MinimumWidth = 6;
            this.ColumnEnabled.Name = "ColumnEnabled";
            this.ColumnEnabled.ReadOnly = true;
            this.ColumnEnabled.Width = 52;
            // 
            // ColumnType
            // 
            this.ColumnType.HeaderText = "类型";
            this.ColumnType.MinimumWidth = 6;
            this.ColumnType.Name = "ColumnType";
            this.ColumnType.ReadOnly = true;
            this.ColumnType.Width = 72;
            // 
            // ColumnName
            // 
            this.ColumnName.HeaderText = "名称";
            this.ColumnName.MinimumWidth = 6;
            this.ColumnName.Name = "ColumnName";
            this.ColumnName.ReadOnly = true;
            this.ColumnName.Width = 92;
            // 
            // ColumnSource
            // 
            this.ColumnSource.HeaderText = "订阅";
            this.ColumnSource.MinimumWidth = 6;
            this.ColumnSource.Name = "ColumnSource";
            this.ColumnSource.ReadOnly = true;
            this.ColumnSource.Width = 150;
            // 
            // ColumnColor
            // 
            this.ColumnColor.HeaderText = "颜色";
            this.ColumnColor.MinimumWidth = 6;
            this.ColumnColor.Name = "ColumnColor";
            this.ColumnColor.ReadOnly = true;
            this.ColumnColor.Width = 70;
            // 
            // groupBoxItemSetting
            // 
            this.groupBoxItemSetting.Controls.Add(this.tableLayoutPanelSetting);
            this.groupBoxItemSetting.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxItemSetting.Location = new System.Drawing.Point(3, 305);
            this.groupBoxItemSetting.Name = "groupBoxItemSetting";
            this.groupBoxItemSetting.Size = new System.Drawing.Size(453, 324);
            this.groupBoxItemSetting.TabIndex = 2;
            this.groupBoxItemSetting.TabStop = false;
            this.groupBoxItemSetting.Text = "当前项";
            // 
            // tableLayoutPanelSetting
            // 
            this.tableLayoutPanelSetting.ColumnCount = 2;
            this.tableLayoutPanelSetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 98F));
            this.tableLayoutPanelSetting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelSetting.Controls.Add(this.labelEnabled, 0, 0);
            this.tableLayoutPanelSetting.Controls.Add(this.checkBoxEnabled, 1, 0);
            this.tableLayoutPanelSetting.Controls.Add(this.labelItemType, 0, 1);
            this.tableLayoutPanelSetting.Controls.Add(this.comboBoxItemType, 1, 1);
            this.tableLayoutPanelSetting.Controls.Add(this.labelName, 0, 2);
            this.tableLayoutPanelSetting.Controls.Add(this.textBoxName, 1, 2);
            this.tableLayoutPanelSetting.Controls.Add(this.labelSource, 0, 3);
            this.tableLayoutPanelSetting.Controls.Add(this.nodeSubscriptionSource, 1, 3);
            this.tableLayoutPanelSetting.Controls.Add(this.labelManualText, 0, 4);
            this.tableLayoutPanelSetting.Controls.Add(this.tableLayoutPanelManual, 1, 4);
            this.tableLayoutPanelSetting.Controls.Add(this.labelColor, 0, 5);
            this.tableLayoutPanelSetting.Controls.Add(this.tableLayoutPanelColor, 1, 5);
            this.tableLayoutPanelSetting.Controls.Add(this.labelFontSize, 0, 6);
            this.tableLayoutPanelSetting.Controls.Add(this.numericFontSize, 1, 6);
            this.tableLayoutPanelSetting.Controls.Add(this.labelLineWidth, 0, 7);
            this.tableLayoutPanelSetting.Controls.Add(this.numericLineWidth, 1, 7);
            this.tableLayoutPanelSetting.Controls.Add(this.labelTextPosition, 0, 8);
            this.tableLayoutPanelSetting.Controls.Add(this.comboBoxTextPosition, 1, 8);
            this.tableLayoutPanelSetting.Controls.Add(this.labelMargin, 0, 9);
            this.tableLayoutPanelSetting.Controls.Add(this.numericMargin, 1, 9);
            this.tableLayoutPanelSetting.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelSetting.Location = new System.Drawing.Point(3, 21);
            this.tableLayoutPanelSetting.Name = "tableLayoutPanelSetting";
            this.tableLayoutPanelSetting.RowCount = 10;
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanelSetting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tableLayoutPanelSetting.Size = new System.Drawing.Size(447, 300);
            this.tableLayoutPanelSetting.TabIndex = 0;
            // 
            // labelEnabled
            // 
            this.labelEnabled.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelEnabled.AutoSize = true;
            this.labelEnabled.Location = new System.Drawing.Point(59, 6);
            this.labelEnabled.Name = "labelEnabled";
            this.labelEnabled.Size = new System.Drawing.Size(36, 15);
            this.labelEnabled.TabIndex = 0;
            this.labelEnabled.Text = "启用";
            // 
            // checkBoxEnabled
            // 
            this.checkBoxEnabled.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxEnabled.AutoSize = true;
            this.checkBoxEnabled.Checked = true;
            this.checkBoxEnabled.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxEnabled.Location = new System.Drawing.Point(101, 4);
            this.checkBoxEnabled.Name = "checkBoxEnabled";
            this.checkBoxEnabled.Size = new System.Drawing.Size(18, 17);
            this.checkBoxEnabled.TabIndex = 1;
            this.checkBoxEnabled.UseVisualStyleBackColor = true;
            this.checkBoxEnabled.CheckedChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelItemType
            // 
            this.labelItemType.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelItemType.AutoSize = true;
            this.labelItemType.Location = new System.Drawing.Point(59, 36);
            this.labelItemType.Name = "labelItemType";
            this.labelItemType.Size = new System.Drawing.Size(36, 15);
            this.labelItemType.TabIndex = 2;
            this.labelItemType.Text = "类型";
            // 
            // comboBoxItemType
            // 
            this.comboBoxItemType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxItemType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxItemType.FormattingEnabled = true;
            this.comboBoxItemType.Location = new System.Drawing.Point(101, 32);
            this.comboBoxItemType.Name = "comboBoxItemType";
            this.comboBoxItemType.Size = new System.Drawing.Size(343, 23);
            this.comboBoxItemType.TabIndex = 3;
            this.comboBoxItemType.SelectedIndexChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelName
            // 
            this.labelName.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelName.AutoSize = true;
            this.labelName.Location = new System.Drawing.Point(59, 68);
            this.labelName.Name = "labelName";
            this.labelName.Size = new System.Drawing.Size(36, 15);
            this.labelName.TabIndex = 4;
            this.labelName.Text = "名称";
            // 
            // textBoxName
            // 
            this.textBoxName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxName.Location = new System.Drawing.Point(101, 63);
            this.textBoxName.Name = "textBoxName";
            this.textBoxName.Size = new System.Drawing.Size(343, 25);
            this.textBoxName.TabIndex = 5;
            this.textBoxName.TextChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelSource
            // 
            this.labelSource.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelSource.AutoSize = true;
            this.labelSource.Location = new System.Drawing.Point(59, 115);
            this.labelSource.Name = "labelSource";
            this.labelSource.Size = new System.Drawing.Size(36, 15);
            this.labelSource.TabIndex = 6;
            this.labelSource.Text = "订阅";
            // 
            // nodeSubscriptionSource
            // 
            this.nodeSubscriptionSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.nodeSubscriptionSource.Location = new System.Drawing.Point(101, 96);
            this.nodeSubscriptionSource.MinimumSize = new System.Drawing.Size(260, 50);
            this.nodeSubscriptionSource.Name = "nodeSubscriptionSource";
            this.nodeSubscriptionSource.Size = new System.Drawing.Size(343, 54);
            this.nodeSubscriptionSource.TabIndex = 7;
            // 
            // labelManualText
            // 
            this.labelManualText.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelManualText.AutoSize = true;
            this.labelManualText.Location = new System.Drawing.Point(59, 166);
            this.labelManualText.Name = "labelManualText";
            this.labelManualText.Size = new System.Drawing.Size(36, 15);
            this.labelManualText.TabIndex = 8;
            this.labelManualText.Text = "文本";
            // 
            // tableLayoutPanelManual
            // 
            this.tableLayoutPanelManual.ColumnCount = 2;
            this.tableLayoutPanelManual.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            this.tableLayoutPanelManual.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelManual.Controls.Add(this.checkBoxManualText, 0, 0);
            this.tableLayoutPanelManual.Controls.Add(this.textBoxManualText, 1, 0);
            this.tableLayoutPanelManual.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelManual.Location = new System.Drawing.Point(98, 154);
            this.tableLayoutPanelManual.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanelManual.Name = "tableLayoutPanelManual";
            this.tableLayoutPanelManual.RowCount = 1;
            this.tableLayoutPanelManual.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelManual.Size = new System.Drawing.Size(349, 40);
            this.tableLayoutPanelManual.TabIndex = 9;
            // 
            // checkBoxManualText
            // 
            this.checkBoxManualText.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxManualText.AutoSize = true;
            this.checkBoxManualText.Checked = true;
            this.checkBoxManualText.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxManualText.Location = new System.Drawing.Point(3, 10);
            this.checkBoxManualText.Name = "checkBoxManualText";
            this.checkBoxManualText.Size = new System.Drawing.Size(59, 19);
            this.checkBoxManualText.TabIndex = 0;
            this.checkBoxManualText.Text = "手填";
            this.checkBoxManualText.UseVisualStyleBackColor = true;
            this.checkBoxManualText.CheckedChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // textBoxManualText
            // 
            this.textBoxManualText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxManualText.Location = new System.Drawing.Point(79, 7);
            this.textBoxManualText.Name = "textBoxManualText";
            this.textBoxManualText.Size = new System.Drawing.Size(267, 25);
            this.textBoxManualText.TabIndex = 1;
            this.textBoxManualText.TextChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelColor
            // 
            this.labelColor.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelColor.AutoSize = true;
            this.labelColor.Location = new System.Drawing.Point(59, 203);
            this.labelColor.Name = "labelColor";
            this.labelColor.Size = new System.Drawing.Size(36, 15);
            this.labelColor.TabIndex = 10;
            this.labelColor.Text = "颜色";
            // 
            // tableLayoutPanelColor
            // 
            this.tableLayoutPanelColor.ColumnCount = 2;
            this.tableLayoutPanelColor.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanelColor.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutPanelColor.Controls.Add(this.panelColor, 0, 0);
            this.tableLayoutPanelColor.Controls.Add(this.buttonChooseColor, 1, 0);
            this.tableLayoutPanelColor.Location = new System.Drawing.Point(98, 194);
            this.tableLayoutPanelColor.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanelColor.Name = "tableLayoutPanelColor";
            this.tableLayoutPanelColor.RowCount = 1;
            this.tableLayoutPanelColor.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelColor.Size = new System.Drawing.Size(160, 34);
            this.tableLayoutPanelColor.TabIndex = 11;
            // 
            // panelColor
            // 
            this.panelColor.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.panelColor.BackColor = System.Drawing.Color.Lime;
            this.panelColor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelColor.Location = new System.Drawing.Point(3, 6);
            this.panelColor.Name = "panelColor";
            this.panelColor.Size = new System.Drawing.Size(44, 22);
            this.panelColor.TabIndex = 0;
            // 
            // buttonChooseColor
            // 
            this.buttonChooseColor.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.buttonChooseColor.Location = new System.Drawing.Point(59, 3);
            this.buttonChooseColor.Name = "buttonChooseColor";
            this.buttonChooseColor.Size = new System.Drawing.Size(76, 28);
            this.buttonChooseColor.TabIndex = 1;
            this.buttonChooseColor.Text = "选择";
            this.buttonChooseColor.UseVisualStyleBackColor = true;
            this.buttonChooseColor.Click += new System.EventHandler(this.buttonChooseColor_Click);
            // 
            // labelFontSize
            // 
            this.labelFontSize.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelFontSize.AutoSize = true;
            this.labelFontSize.Location = new System.Drawing.Point(59, 236);
            this.labelFontSize.Name = "labelFontSize";
            this.labelFontSize.Size = new System.Drawing.Size(36, 15);
            this.labelFontSize.TabIndex = 12;
            this.labelFontSize.Text = "字号";
            // 
            // numericFontSize
            // 
            this.numericFontSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericFontSize.Location = new System.Drawing.Point(101, 231);
            this.numericFontSize.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericFontSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericFontSize.Name = "numericFontSize";
            this.numericFontSize.Size = new System.Drawing.Size(90, 25);
            this.numericFontSize.TabIndex = 13;
            this.numericFontSize.Value = new decimal(new int[] {
            18,
            0,
            0,
            0});
            this.numericFontSize.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelLineWidth
            // 
            this.labelLineWidth.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelLineWidth.AutoSize = true;
            this.labelLineWidth.Location = new System.Drawing.Point(59, 268);
            this.labelLineWidth.Name = "labelLineWidth";
            this.labelLineWidth.Size = new System.Drawing.Size(36, 15);
            this.labelLineWidth.TabIndex = 14;
            this.labelLineWidth.Text = "线宽";
            // 
            // numericLineWidth
            // 
            this.numericLineWidth.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericLineWidth.Location = new System.Drawing.Point(101, 263);
            this.numericLineWidth.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numericLineWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericLineWidth.Name = "numericLineWidth";
            this.numericLineWidth.Size = new System.Drawing.Size(90, 25);
            this.numericLineWidth.TabIndex = 15;
            this.numericLineWidth.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numericLineWidth.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelTextPosition
            // 
            this.labelTextPosition.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelTextPosition.AutoSize = true;
            this.labelTextPosition.Location = new System.Drawing.Point(59, 300);
            this.labelTextPosition.Name = "labelTextPosition";
            this.labelTextPosition.Size = new System.Drawing.Size(36, 15);
            this.labelTextPosition.TabIndex = 16;
            this.labelTextPosition.Text = "位置";
            // 
            // comboBoxTextPosition
            // 
            this.comboBoxTextPosition.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxTextPosition.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxTextPosition.FormattingEnabled = true;
            this.comboBoxTextPosition.Location = new System.Drawing.Point(101, 296);
            this.comboBoxTextPosition.Name = "comboBoxTextPosition";
            this.comboBoxTextPosition.Size = new System.Drawing.Size(343, 23);
            this.comboBoxTextPosition.TabIndex = 17;
            this.comboBoxTextPosition.SelectedIndexChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // labelMargin
            // 
            this.labelMargin.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelMargin.AutoSize = true;
            this.labelMargin.Location = new System.Drawing.Point(59, 332);
            this.labelMargin.Name = "labelMargin";
            this.labelMargin.Size = new System.Drawing.Size(36, 15);
            this.labelMargin.TabIndex = 18;
            this.labelMargin.Text = "边距";
            // 
            // numericMargin
            // 
            this.numericMargin.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericMargin.Location = new System.Drawing.Point(101, 327);
            this.numericMargin.Maximum = new decimal(new int[] {
            500,
            0,
            0,
            0});
            this.numericMargin.Name = "numericMargin";
            this.numericMargin.Size = new System.Drawing.Size(90, 25);
            this.numericMargin.TabIndex = 19;
            this.numericMargin.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericMargin.ValueChanged += new System.EventHandler(this.ItemControl_ValueChanged);
            // 
            // flowLayoutPanelBottom
            // 
            this.flowLayoutPanelBottom.Controls.Add(this.buttonPreview);
            this.flowLayoutPanelBottom.Controls.Add(this.buttonSave);
            this.flowLayoutPanelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelBottom.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelBottom.Location = new System.Drawing.Point(3, 634);
            this.flowLayoutPanelBottom.Name = "flowLayoutPanelBottom";
            this.flowLayoutPanelBottom.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.flowLayoutPanelBottom.Size = new System.Drawing.Size(453, 47);
            this.flowLayoutPanelBottom.TabIndex = 3;
            // 
            // buttonPreview
            // 
            this.buttonPreview.Location = new System.Drawing.Point(365, 11);
            this.buttonPreview.Name = "buttonPreview";
            this.buttonPreview.Size = new System.Drawing.Size(85, 30);
            this.buttonPreview.TabIndex = 0;
            this.buttonPreview.Text = "预览";
            this.buttonPreview.UseVisualStyleBackColor = true;
            this.buttonPreview.Click += new System.EventHandler(this.buttonPreview_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(274, 11);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(85, 30);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // groupBoxPreview
            // 
            this.groupBoxPreview.Controls.Add(this.showImageControl1);
            this.groupBoxPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxPreview.Location = new System.Drawing.Point(478, 13);
            this.groupBoxPreview.Name = "groupBoxPreview";
            this.groupBoxPreview.Size = new System.Drawing.Size(705, 684);
            this.groupBoxPreview.TabIndex = 1;
            this.groupBoxPreview.TabStop = false;
            this.groupBoxPreview.Text = "预览";
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackColor = System.Drawing.Color.Black;
            this.showImageControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControl1.Location = new System.Drawing.Point(3, 21);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.Size = new System.Drawing.Size(699, 660);
            this.showImageControl1.TabIndex = 0;
            // 
            // NodeParamFormResultOverlayDraw
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 750);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormResultOverlayDraw";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ROI结果绘制";
            this.Controls.SetChildIndex(this.tableLayoutPanelRoot, 0);
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.tableLayoutPanelLeft.ResumeLayout(false);
            this.groupBoxImage.ResumeLayout(false);
            this.groupBoxItems.ResumeLayout(false);
            this.tableLayoutPanelItems.ResumeLayout(false);
            this.flowLayoutPanelAdd.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewItems)).EndInit();
            this.groupBoxItemSetting.ResumeLayout(false);
            this.tableLayoutPanelSetting.ResumeLayout(false);
            this.tableLayoutPanelSetting.PerformLayout();
            this.tableLayoutPanelManual.ResumeLayout(false);
            this.tableLayoutPanelManual.PerformLayout();
            this.tableLayoutPanelColor.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericFontSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericLineWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMargin)).EndInit();
            this.flowLayoutPanelBottom.ResumeLayout(false);
            this.groupBoxPreview.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelLeft;
        private System.Windows.Forms.GroupBox groupBoxImage;
        private NodeSubscription nodeSubscriptionImage;
        private System.Windows.Forms.GroupBox groupBoxItems;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelItems;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelAdd;
        private System.Windows.Forms.Button buttonAddText;
        private System.Windows.Forms.Button buttonAddLine;
        private System.Windows.Forms.Button buttonAddRectangle;
        private System.Windows.Forms.Button buttonAddRegion;
        private System.Windows.Forms.Button buttonDelete;
        private System.Windows.Forms.DataGridView dataGridViewItems;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnType;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnColor;
        private System.Windows.Forms.GroupBox groupBoxItemSetting;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelSetting;
        private System.Windows.Forms.Label labelEnabled;
        private System.Windows.Forms.CheckBox checkBoxEnabled;
        private System.Windows.Forms.Label labelItemType;
        private System.Windows.Forms.ComboBox comboBoxItemType;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.TextBox textBoxName;
        private System.Windows.Forms.Label labelSource;
        private NodeSubscription nodeSubscriptionSource;
        private System.Windows.Forms.Label labelManualText;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelManual;
        private System.Windows.Forms.CheckBox checkBoxManualText;
        private System.Windows.Forms.TextBox textBoxManualText;
        private System.Windows.Forms.Label labelColor;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelColor;
        private System.Windows.Forms.Panel panelColor;
        private System.Windows.Forms.Button buttonChooseColor;
        private System.Windows.Forms.Label labelFontSize;
        private System.Windows.Forms.NumericUpDown numericFontSize;
        private System.Windows.Forms.Label labelLineWidth;
        private System.Windows.Forms.NumericUpDown numericLineWidth;
        private System.Windows.Forms.Label labelTextPosition;
        private System.Windows.Forms.ComboBox comboBoxTextPosition;
        private System.Windows.Forms.Label labelMargin;
        private System.Windows.Forms.NumericUpDown numericMargin;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelBottom;
        private System.Windows.Forms.Button buttonPreview;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.GroupBox groupBoxPreview;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
    }
}
