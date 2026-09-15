namespace TDJS_Vision.Forms.SystemSetting
{
    partial class FrmSystemSetting
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
                ReleaseGlobalEventSubscriptions();
            }
            if (disposing && (components != null))
            {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmSystemSetting));
            this.tabControlSettings = new System.Windows.Forms.TabControl();
            this.tabPageGeneral = new System.Windows.Forms.TabPage();
            this.tabPagePerformance = new System.Windows.Forms.TabPage();
            this.tableLayoutPanelPerformance = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxPerformanceHardware = new System.Windows.Forms.GroupBox();
            this.labelProfileSummary = new System.Windows.Forms.Label();
            this.labelWorkloadSummary = new System.Windows.Forms.Label();
            this.labelHardwareSummary = new System.Windows.Forms.Label();
            this.flowLayoutPanelPerformanceActions = new System.Windows.Forms.FlowLayoutPanel();
            this.checkBoxAdaptiveCpu = new System.Windows.Forms.CheckBox();
            this.buttonRefreshHardware = new System.Windows.Forms.Button();
            this.buttonRestoreAutomatic = new System.Windows.Forms.Button();
            this.buttonSavePerformanceSettings = new System.Windows.Forms.Button();
            this.dataGridViewPerformance = new System.Windows.Forms.DataGridView();
            this.columnCategory = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnParameterName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnAutomaticValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnManualEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.columnManualValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnEffectiveValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnRecommendedRange = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnApplyState = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelPerformanceStatus = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.checkBox7 = new System.Windows.Forms.CheckBox();
            this.checkBox8 = new System.Windows.Forms.CheckBox();
            this.tabControlSettings.SuspendLayout();
            this.tabPageGeneral.SuspendLayout();
            this.tabPagePerformance.SuspendLayout();
            this.tableLayoutPanelPerformance.SuspendLayout();
            this.groupBoxPerformanceHardware.SuspendLayout();
            this.flowLayoutPanelPerformanceActions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPerformance)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            //
            // tabControlSettings
            //
            this.tabControlSettings.Controls.Add(this.tabPageGeneral);
            this.tabControlSettings.Controls.Add(this.tabPagePerformance);
            this.tabControlSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlSettings.Location = new System.Drawing.Point(2, 38);
            this.tabControlSettings.Name = "tabControlSettings";
            this.tabControlSettings.SelectedIndex = 0;
            this.tabControlSettings.Size = new System.Drawing.Size(1276, 720);
            this.tabControlSettings.TabIndex = 0;
            //
            // tabPageGeneral
            //
            this.tabPageGeneral.Controls.Add(this.tableLayoutPanel1);
            this.tabPageGeneral.Location = new System.Drawing.Point(4, 28);
            this.tabPageGeneral.Name = "tabPageGeneral";
            this.tabPageGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageGeneral.Size = new System.Drawing.Size(1268, 688);
            this.tabPageGeneral.TabIndex = 0;
            this.tabPageGeneral.Text = "常规设置";
            this.tabPageGeneral.UseVisualStyleBackColor = true;
            //
            // tabPagePerformance
            //
            this.tabPagePerformance.Controls.Add(this.tableLayoutPanelPerformance);
            this.tabPagePerformance.Location = new System.Drawing.Point(4, 28);
            this.tabPagePerformance.Name = "tabPagePerformance";
            this.tabPagePerformance.Padding = new System.Windows.Forms.Padding(3);
            this.tabPagePerformance.Size = new System.Drawing.Size(1268, 688);
            this.tabPagePerformance.TabIndex = 1;
            this.tabPagePerformance.Text = "性能与资源";
            this.tabPagePerformance.UseVisualStyleBackColor = true;
            //
            // tableLayoutPanelPerformance
            //
            this.tableLayoutPanelPerformance.ColumnCount = 1;
            this.tableLayoutPanelPerformance.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPerformance.Controls.Add(this.groupBoxPerformanceHardware, 0, 0);
            this.tableLayoutPanelPerformance.Controls.Add(this.flowLayoutPanelPerformanceActions, 0, 1);
            this.tableLayoutPanelPerformance.Controls.Add(this.dataGridViewPerformance, 0, 2);
            this.tableLayoutPanelPerformance.Controls.Add(this.labelPerformanceStatus, 0, 3);
            this.tableLayoutPanelPerformance.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelPerformance.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelPerformance.Name = "tableLayoutPanelPerformance";
            this.tableLayoutPanelPerformance.RowCount = 4;
            this.tableLayoutPanelPerformance.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 128F));
            this.tableLayoutPanelPerformance.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            this.tableLayoutPanelPerformance.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPerformance.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelPerformance.Size = new System.Drawing.Size(1262, 682);
            this.tableLayoutPanelPerformance.TabIndex = 0;
            //
            // groupBoxPerformanceHardware
            //
            this.groupBoxPerformanceHardware.Controls.Add(this.labelProfileSummary);
            this.groupBoxPerformanceHardware.Controls.Add(this.labelWorkloadSummary);
            this.groupBoxPerformanceHardware.Controls.Add(this.labelHardwareSummary);
            this.groupBoxPerformanceHardware.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxPerformanceHardware.Location = new System.Drawing.Point(3, 3);
            this.groupBoxPerformanceHardware.Name = "groupBoxPerformanceHardware";
            this.groupBoxPerformanceHardware.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this.groupBoxPerformanceHardware.Size = new System.Drawing.Size(1256, 122);
            this.groupBoxPerformanceHardware.TabIndex = 0;
            this.groupBoxPerformanceHardware.TabStop = false;
            this.groupBoxPerformanceHardware.Text = "本机资源与当前方案";
            //
            // labelProfileSummary
            //
            this.labelProfileSummary.AutoEllipsis = true;
            this.labelProfileSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelProfileSummary.Location = new System.Drawing.Point(10, 88);
            this.labelProfileSummary.Name = "labelProfileSummary";
            this.labelProfileSummary.Padding = new System.Windows.Forms.Padding(2, 5, 2, 0);
            this.labelProfileSummary.Size = new System.Drawing.Size(1236, 28);
            this.labelProfileSummary.TabIndex = 2;
            this.labelProfileSummary.Text = "本次预览：正在计算。";
            //
            // labelWorkloadSummary
            //
            this.labelWorkloadSummary.AutoEllipsis = true;
            this.labelWorkloadSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelWorkloadSummary.Location = new System.Drawing.Point(10, 58);
            this.labelWorkloadSummary.Name = "labelWorkloadSummary";
            this.labelWorkloadSummary.Padding = new System.Windows.Forms.Padding(2, 5, 2, 0);
            this.labelWorkloadSummary.Size = new System.Drawing.Size(1236, 30);
            this.labelWorkloadSummary.TabIndex = 1;
            this.labelWorkloadSummary.Text = "当前方案：正在读取。";
            //
            // labelHardwareSummary
            //
            this.labelHardwareSummary.AutoEllipsis = true;
            this.labelHardwareSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelHardwareSummary.Location = new System.Drawing.Point(10, 27);
            this.labelHardwareSummary.Name = "labelHardwareSummary";
            this.labelHardwareSummary.Padding = new System.Windows.Forms.Padding(2, 5, 2, 0);
            this.labelHardwareSummary.Size = new System.Drawing.Size(1236, 31);
            this.labelHardwareSummary.TabIndex = 0;
            this.labelHardwareSummary.Text = "硬件信息：正在读取。";
            //
            // flowLayoutPanelPerformanceActions
            //
            this.flowLayoutPanelPerformanceActions.Controls.Add(this.checkBoxAdaptiveCpu);
            this.flowLayoutPanelPerformanceActions.Controls.Add(this.buttonRefreshHardware);
            this.flowLayoutPanelPerformanceActions.Controls.Add(this.buttonRestoreAutomatic);
            this.flowLayoutPanelPerformanceActions.Controls.Add(this.buttonSavePerformanceSettings);
            this.flowLayoutPanelPerformanceActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelPerformanceActions.Location = new System.Drawing.Point(3, 131);
            this.flowLayoutPanelPerformanceActions.Name = "flowLayoutPanelPerformanceActions";
            this.flowLayoutPanelPerformanceActions.Padding = new System.Windows.Forms.Padding(6, 6, 6, 4);
            this.flowLayoutPanelPerformanceActions.Size = new System.Drawing.Size(1256, 48);
            this.flowLayoutPanelPerformanceActions.TabIndex = 1;
            this.flowLayoutPanelPerformanceActions.WrapContents = false;
            //
            // checkBoxAdaptiveCpu
            //
            this.checkBoxAdaptiveCpu.AutoSize = true;
            this.checkBoxAdaptiveCpu.Checked = true;
            this.checkBoxAdaptiveCpu.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxAdaptiveCpu.Location = new System.Drawing.Point(9, 14);
            this.checkBoxAdaptiveCpu.Margin = new System.Windows.Forms.Padding(3, 8, 18, 3);
            this.checkBoxAdaptiveCpu.Name = "checkBoxAdaptiveCpu";
            this.checkBoxAdaptiveCpu.Size = new System.Drawing.Size(223, 22);
            this.checkBoxAdaptiveCpu.TabIndex = 0;
            this.checkBoxAdaptiveCpu.Text = "启用CPU动态并发调节";
            this.checkBoxAdaptiveCpu.UseVisualStyleBackColor = true;
            this.checkBoxAdaptiveCpu.CheckedChanged += new System.EventHandler(this.checkBoxAdaptiveCpu_CheckedChanged);
            //
            // buttonRefreshHardware
            //
            this.buttonRefreshHardware.Location = new System.Drawing.Point(253, 9);
            this.buttonRefreshHardware.Name = "buttonRefreshHardware";
            this.buttonRefreshHardware.Size = new System.Drawing.Size(132, 34);
            this.buttonRefreshHardware.TabIndex = 1;
            this.buttonRefreshHardware.Text = "重新读取硬件";
            this.buttonRefreshHardware.UseVisualStyleBackColor = true;
            this.buttonRefreshHardware.Click += new System.EventHandler(this.buttonRefreshHardware_Click);
            //
            // buttonRestoreAutomatic
            //
            this.buttonRestoreAutomatic.Location = new System.Drawing.Point(391, 9);
            this.buttonRestoreAutomatic.Name = "buttonRestoreAutomatic";
            this.buttonRestoreAutomatic.Size = new System.Drawing.Size(132, 34);
            this.buttonRestoreAutomatic.TabIndex = 2;
            this.buttonRestoreAutomatic.Text = "恢复全部自动";
            this.buttonRestoreAutomatic.UseVisualStyleBackColor = true;
            this.buttonRestoreAutomatic.Click += new System.EventHandler(this.buttonRestoreAutomatic_Click);
            //
            // buttonSavePerformanceSettings
            //
            this.buttonSavePerformanceSettings.Location = new System.Drawing.Point(529, 9);
            this.buttonSavePerformanceSettings.Name = "buttonSavePerformanceSettings";
            this.buttonSavePerformanceSettings.Size = new System.Drawing.Size(132, 34);
            this.buttonSavePerformanceSettings.TabIndex = 3;
            this.buttonSavePerformanceSettings.Text = "保存并应用";
            this.buttonSavePerformanceSettings.UseVisualStyleBackColor = true;
            this.buttonSavePerformanceSettings.Click += new System.EventHandler(this.buttonSavePerformanceSettings_Click);
            //
            // dataGridViewPerformance
            //
            this.dataGridViewPerformance.AllowUserToAddRows = false;
            this.dataGridViewPerformance.AllowUserToDeleteRows = false;
            this.dataGridViewPerformance.AllowUserToResizeRows = false;
            this.dataGridViewPerformance.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridViewPerformance.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridViewPerformance.ColumnHeadersHeight = 34;
            this.dataGridViewPerformance.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridViewPerformance.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnCategory,
            this.columnParameterName,
            this.columnAutomaticValue,
            this.columnManualEnabled,
            this.columnManualValue,
            this.columnEffectiveValue,
            this.columnRecommendedRange,
            this.columnApplyState});
            this.dataGridViewPerformance.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewPerformance.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridViewPerformance.Location = new System.Drawing.Point(3, 185);
            this.dataGridViewPerformance.MultiSelect = false;
            this.dataGridViewPerformance.Name = "dataGridViewPerformance";
            this.dataGridViewPerformance.RowHeadersVisible = false;
            this.dataGridViewPerformance.RowTemplate.Height = 30;
            this.dataGridViewPerformance.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewPerformance.Size = new System.Drawing.Size(1256, 448);
            this.dataGridViewPerformance.TabIndex = 2;
            this.dataGridViewPerformance.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewPerformance_CellValueChanged);
            this.dataGridViewPerformance.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewPerformance_CurrentCellDirtyStateChanged);
            this.dataGridViewPerformance.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewPerformance_DataError);
            //
            // columnCategory
            //
            this.columnCategory.HeaderText = "类别";
            this.columnCategory.MinimumWidth = 90;
            this.columnCategory.Name = "columnCategory";
            this.columnCategory.ReadOnly = true;
            this.columnCategory.Width = 110;
            //
            // columnParameterName
            //
            this.columnParameterName.HeaderText = "参数";
            this.columnParameterName.MinimumWidth = 160;
            this.columnParameterName.Name = "columnParameterName";
            this.columnParameterName.ReadOnly = true;
            this.columnParameterName.Width = 205;
            //
            // columnAutomaticValue
            //
            this.columnAutomaticValue.HeaderText = "自动值";
            this.columnAutomaticValue.MinimumWidth = 80;
            this.columnAutomaticValue.Name = "columnAutomaticValue";
            this.columnAutomaticValue.ReadOnly = true;
            this.columnAutomaticValue.Width = 95;
            //
            // columnManualEnabled
            //
            this.columnManualEnabled.HeaderText = "使用手动";
            this.columnManualEnabled.MinimumWidth = 82;
            this.columnManualEnabled.Name = "columnManualEnabled";
            this.columnManualEnabled.Width = 82;
            //
            // columnManualValue
            //
            this.columnManualValue.HeaderText = "手动值";
            this.columnManualValue.MinimumWidth = 85;
            this.columnManualValue.Name = "columnManualValue";
            this.columnManualValue.Width = 95;
            //
            // columnEffectiveValue
            //
            this.columnEffectiveValue.HeaderText = "最终值";
            this.columnEffectiveValue.MinimumWidth = 80;
            this.columnEffectiveValue.Name = "columnEffectiveValue";
            this.columnEffectiveValue.ReadOnly = true;
            this.columnEffectiveValue.Width = 95;
            //
            // columnRecommendedRange
            //
            this.columnRecommendedRange.HeaderText = "推荐范围";
            this.columnRecommendedRange.MinimumWidth = 145;
            this.columnRecommendedRange.Name = "columnRecommendedRange";
            this.columnRecommendedRange.ReadOnly = true;
            this.columnRecommendedRange.Width = 170;
            //
            // columnApplyState
            //
            this.columnApplyState.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.columnApplyState.HeaderText = "生效状态";
            this.columnApplyState.MinimumWidth = 220;
            this.columnApplyState.Name = "columnApplyState";
            this.columnApplyState.ReadOnly = true;
            //
            // labelPerformanceStatus
            //
            this.labelPerformanceStatus.AutoEllipsis = true;
            this.labelPerformanceStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelPerformanceStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelPerformanceStatus.Location = new System.Drawing.Point(3, 639);
            this.labelPerformanceStatus.Margin = new System.Windows.Forms.Padding(3);
            this.labelPerformanceStatus.Name = "labelPerformanceStatus";
            this.labelPerformanceStatus.Padding = new System.Windows.Forms.Padding(8, 10, 8, 0);
            this.labelPerformanceStatus.Size = new System.Drawing.Size(1256, 40);
            this.labelPerformanceStatus.TabIndex = 3;
            this.labelPerformanceStatus.Text = "正在读取机器性能设置。";
            //
            // tableLayoutPanel1
            //
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox5, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.groupBox3, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.groupBox4, 0, 4);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 18.18182F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 18.18182F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 9.090909F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 22.72727F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 17.9289F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 14.37403F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1262, 682);
            this.tableLayoutPanel1.TabIndex = 0;
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.checkBox1);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(3, 2);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Size = new System.Drawing.Size(959, 113);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "自启动设置";
            //
            // checkBox1
            //
            this.checkBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(67, 51);
            this.checkBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(160, 22);
            this.checkBox1.TabIndex = 0;
            this.checkBox1.Text = "开机自启动软件";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            //
            // groupBox2
            //
            this.groupBox2.Controls.Add(this.checkBox2);
            this.groupBox2.Controls.Add(this.button1);
            this.groupBox2.Controls.Add(this.textBox1);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(3, 119);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.SetRowSpan(this.groupBox2, 2);
            this.groupBox2.Size = new System.Drawing.Size(959, 171);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "默认打开方案";
            //
            // checkBox2
            //
            this.checkBox2.AutoSize = true;
            this.checkBox2.Location = new System.Drawing.Point(66, 53);
            this.checkBox2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(142, 22);
            this.checkBox2.TabIndex = 3;
            this.checkBox2.Text = "打开以下方案";
            this.checkBox2.UseVisualStyleBackColor = true;
            this.checkBox2.CheckedChanged += new System.EventHandler(this.checkBox2_CheckedChanged);
            //
            // button1
            //
            this.button1.BackColor = System.Drawing.SystemColors.Control;
            this.button1.Enabled = false;
            this.button1.Location = new System.Drawing.Point(737, 91);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(80, 36);
            this.button1.TabIndex = 2;
            this.button1.Text = "选择";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            //
            // textBox1
            //
            this.textBox1.Enabled = false;
            this.textBox1.Location = new System.Drawing.Point(64, 97);
            this.textBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(638, 28);
            this.textBox1.TabIndex = 1;
            //
            // groupBox3
            //
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.textBox2);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(3, 294);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox3.Size = new System.Drawing.Size(959, 142);
            this.groupBox3.TabIndex = 3;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "流程循环执行间隔";
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(170, 56);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(26, 18);
            this.label1.TabIndex = 1;
            this.label1.Text = "ms";
            //
            // textBox2
            //
            this.textBox2.Location = new System.Drawing.Point(64, 46);
            this.textBox2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(100, 28);
            this.textBox2.TabIndex = 0;
            this.textBox2.Text = "0";
            this.textBox2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBox2.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            //
            // openFileDialog1
            //
            this.openFileDialog1.FileName = "openFileDialog1";
            //
            // checkBox3
            //
            this.checkBox3.AutoSize = true;
            this.checkBox3.Location = new System.Drawing.Point(66, 46);
            this.checkBox3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(214, 22);
            this.checkBox3.TabIndex = 0;
            this.checkBox3.Text = "方案加载完成自动运行";
            this.checkBox3.UseVisualStyleBackColor = true;
            this.checkBox3.CheckedChanged += new System.EventHandler(this.checkBox3_CheckedChanged);
            //
            // groupBox4
            //
            this.groupBox4.Controls.Add(this.checkBox3);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox4.Location = new System.Drawing.Point(3, 442);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox4.Size = new System.Drawing.Size(959, 107);
            this.groupBox4.TabIndex = 4;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "自动运行";
            //
            // groupBox5
            //
            this.groupBox5.Controls.Add(this.tableLayoutPanel2);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox5.Location = new System.Drawing.Point(3, 557);
            this.groupBox5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox5.Size = new System.Drawing.Size(959, 86);
            this.groupBox5.TabIndex = 5;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "日志记录";
            //
            // tableLayoutPanel2
            //
            this.tableLayoutPanel2.ColumnCount = 6;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.checkBox8, 4, 0);
            this.tableLayoutPanel2.Controls.Add(this.checkBox7, 3, 0);
            this.tableLayoutPanel2.Controls.Add(this.checkBox6, 2, 0);
            this.tableLayoutPanel2.Controls.Add(this.checkBox5, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.checkBox4, 0, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 25);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(953, 57);
            this.tableLayoutPanel2.TabIndex = 0;
            //
            // checkBox4
            //
            this.checkBox4.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox4.AutoSize = true;
            this.checkBox4.Location = new System.Drawing.Point(3, 17);
            this.checkBox4.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(124, 22);
            this.checkBox4.TabIndex = 1;
            this.checkBox4.Text = "调试(Debug)";
            this.checkBox4.UseVisualStyleBackColor = true;
            this.checkBox4.CheckedChanged += new System.EventHandler(this.checkBox4_CheckedChanged);
            //
            // checkBox5
            //
            this.checkBox5.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox5.AutoSize = true;
            this.checkBox5.Checked = true;
            this.checkBox5.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox5.Location = new System.Drawing.Point(133, 17);
            this.checkBox5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(115, 22);
            this.checkBox5.TabIndex = 2;
            this.checkBox5.Text = "消息(Info)";
            this.checkBox5.UseVisualStyleBackColor = true;
            this.checkBox5.CheckedChanged += new System.EventHandler(this.checkBox5_CheckedChanged);
            //
            // checkBox6
            //
            this.checkBox6.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox6.AutoSize = true;
            this.checkBox6.Checked = true;
            this.checkBox6.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox6.Location = new System.Drawing.Point(254, 17);
            this.checkBox6.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(115, 22);
            this.checkBox6.TabIndex = 3;
            this.checkBox6.Text = "警告(Warn)";
            this.checkBox6.UseVisualStyleBackColor = true;
            this.checkBox6.CheckedChanged += new System.EventHandler(this.checkBox6_CheckedChanged);
            //
            // checkBox7
            //
            this.checkBox7.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox7.AutoSize = true;
            this.checkBox7.Checked = true;
            this.checkBox7.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox7.Location = new System.Drawing.Point(375, 17);
            this.checkBox7.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox7.Name = "checkBox7";
            this.checkBox7.Size = new System.Drawing.Size(160, 22);
            this.checkBox7.TabIndex = 4;
            this.checkBox7.Text = "异常(Exception)";
            this.checkBox7.UseVisualStyleBackColor = true;
            this.checkBox7.CheckedChanged += new System.EventHandler(this.checkBox7_CheckedChanged);
            //
            // checkBox8
            //
            this.checkBox8.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox8.AutoSize = true;
            this.checkBox8.Checked = true;
            this.checkBox8.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox8.Location = new System.Drawing.Point(541, 17);
            this.checkBox8.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBox8.Name = "checkBox8";
            this.checkBox8.Size = new System.Drawing.Size(124, 22);
            this.checkBox8.TabIndex = 5;
            this.checkBox8.Text = "致命(Fatal)";
            this.checkBox8.UseVisualStyleBackColor = true;
            this.checkBox8.CheckedChanged += new System.EventHandler(this.checkBox8_CheckedChanged);
            //
            // FrmSystemSetting
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 760);
            this.Controls.Add(this.tabControlSettings);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(1050, 680);
            this.Name = "FrmSystemSetting";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "系统设置";
            this.Controls.SetChildIndex(this.tabControlSettings, 0);
            this.tabControlSettings.ResumeLayout(false);
            this.tabPageGeneral.ResumeLayout(false);
            this.tabPagePerformance.ResumeLayout(false);
            this.tableLayoutPanelPerformance.ResumeLayout(false);
            this.groupBoxPerformanceHardware.ResumeLayout(false);
            this.flowLayoutPanelPerformanceActions.ResumeLayout(false);
            this.flowLayoutPanelPerformanceActions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPerformance)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControlSettings;
        private System.Windows.Forms.TabPage tabPageGeneral;
        private System.Windows.Forms.TabPage tabPagePerformance;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPerformance;
        private System.Windows.Forms.GroupBox groupBoxPerformanceHardware;
        private System.Windows.Forms.Label labelHardwareSummary;
        private System.Windows.Forms.Label labelWorkloadSummary;
        private System.Windows.Forms.Label labelProfileSummary;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelPerformanceActions;
        private System.Windows.Forms.CheckBox checkBoxAdaptiveCpu;
        private System.Windows.Forms.Button buttonRefreshHardware;
        private System.Windows.Forms.Button buttonRestoreAutomatic;
        private System.Windows.Forms.Button buttonSavePerformanceSettings;
        private System.Windows.Forms.DataGridView dataGridViewPerformance;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnCategory;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnParameterName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnAutomaticValue;
        private System.Windows.Forms.DataGridViewCheckBoxColumn columnManualEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnManualValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnEffectiveValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRecommendedRange;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnApplyState;
        private System.Windows.Forms.Label labelPerformanceStatus;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.CheckBox checkBox2;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.CheckBox checkBox3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.CheckBox checkBox5;
        private System.Windows.Forms.CheckBox checkBox4;
        private System.Windows.Forms.CheckBox checkBox6;
        private System.Windows.Forms.CheckBox checkBox8;
        private System.Windows.Forms.CheckBox checkBox7;
    }
}
