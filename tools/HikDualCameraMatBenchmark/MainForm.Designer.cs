namespace HikDualCameraMatBenchmark
{
    partial class MainForm
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>页面根布局。</summary>
        private System.Windows.Forms.TableLayoutPanel rootLayout;

        /// <summary>顶部参数区域。</summary>
        private System.Windows.Forms.Panel parameterPanel;

        /// <summary>相机1选择标签。</summary>
        private System.Windows.Forms.Label lblCamera1;

        /// <summary>相机1下拉框。</summary>
        private System.Windows.Forms.ComboBox cmbCamera1;

        /// <summary>相机2选择标签。</summary>
        private System.Windows.Forms.Label lblCamera2;

        /// <summary>相机2下拉框。</summary>
        private System.Windows.Forms.ComboBox cmbCamera2;

        /// <summary>重新枚举相机按钮。</summary>
        private System.Windows.Forms.Button btnRefreshDevices;

        /// <summary>设备数量标签。</summary>
        private System.Windows.Forms.Label lblDeviceCount;

        /// <summary>触发方式标签。</summary>
        private System.Windows.Forms.Label lblTriggerMode;

        /// <summary>触发方式下拉框。</summary>
        private System.Windows.Forms.ComboBox cmbTriggerMode;

        /// <summary>预热帧标签。</summary>
        private System.Windows.Forms.Label lblWarmupFrames;

        /// <summary>预热帧输入框。</summary>
        private System.Windows.Forms.NumericUpDown numWarmupFrames;

        /// <summary>正式样本标签。</summary>
        private System.Windows.Forms.Label lblSampleFrames;

        /// <summary>正式样本输入框。</summary>
        private System.Windows.Forms.NumericUpDown numSampleFrames;

        /// <summary>实时显示开关。</summary>
        private System.Windows.Forms.CheckBox chkDisplayEnabled;

        /// <summary>显示帧率标签。</summary>
        private System.Windows.Forms.Label lblDisplayFps;

        /// <summary>显示帧率输入框。</summary>
        private System.Windows.Forms.NumericUpDown numDisplayFps;

        /// <summary>开始测试按钮。</summary>
        private System.Windows.Forms.Button btnStart;

        /// <summary>停止测试按钮。</summary>
        private System.Windows.Forms.Button btnStop;

        /// <summary>结果目录标签。</summary>
        private System.Windows.Forms.Label lblResultPath;

        /// <summary>结果目录文本框。</summary>
        private System.Windows.Forms.TextBox txtResultPath;

        /// <summary>双相机显示分隔容器。</summary>
        private System.Windows.Forms.SplitContainer cameraSplit;

        /// <summary>相机1显示组。</summary>
        private System.Windows.Forms.GroupBox groupCamera1;

        /// <summary>相机1组布局。</summary>
        private System.Windows.Forms.TableLayoutPanel camera1Layout;

        /// <summary>相机1运行统计标签。</summary>
        private System.Windows.Forms.Label lblCamera1Stats;

        /// <summary>相机1图像控件。</summary>
        private System.Windows.Forms.PictureBox pictureCamera1;

        /// <summary>相机2显示组。</summary>
        private System.Windows.Forms.GroupBox groupCamera2;

        /// <summary>相机2组布局。</summary>
        private System.Windows.Forms.TableLayoutPanel camera2Layout;

        /// <summary>相机2运行统计标签。</summary>
        private System.Windows.Forms.Label lblCamera2Stats;

        /// <summary>相机2图像控件。</summary>
        private System.Windows.Forms.PictureBox pictureCamera2;

        /// <summary>低频状态文本框。</summary>
        private System.Windows.Forms.TextBox txtStatus;

        /// <summary>UI刷新定时器。</summary>
        private System.Windows.Forms.Timer uiTimer;

        /// <summary>
        /// 释放窗体使用的资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化全部可视控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.rootLayout = new System.Windows.Forms.TableLayoutPanel();
            this.parameterPanel = new System.Windows.Forms.Panel();
            this.lblCamera1 = new System.Windows.Forms.Label();
            this.cmbCamera1 = new System.Windows.Forms.ComboBox();
            this.lblCamera2 = new System.Windows.Forms.Label();
            this.cmbCamera2 = new System.Windows.Forms.ComboBox();
            this.btnRefreshDevices = new System.Windows.Forms.Button();
            this.lblDeviceCount = new System.Windows.Forms.Label();
            this.lblTriggerMode = new System.Windows.Forms.Label();
            this.cmbTriggerMode = new System.Windows.Forms.ComboBox();
            this.lblWarmupFrames = new System.Windows.Forms.Label();
            this.numWarmupFrames = new System.Windows.Forms.NumericUpDown();
            this.lblSampleFrames = new System.Windows.Forms.Label();
            this.numSampleFrames = new System.Windows.Forms.NumericUpDown();
            this.chkDisplayEnabled = new System.Windows.Forms.CheckBox();
            this.lblDisplayFps = new System.Windows.Forms.Label();
            this.numDisplayFps = new System.Windows.Forms.NumericUpDown();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.lblResultPath = new System.Windows.Forms.Label();
            this.txtResultPath = new System.Windows.Forms.TextBox();
            this.cameraSplit = new System.Windows.Forms.SplitContainer();
            this.groupCamera1 = new System.Windows.Forms.GroupBox();
            this.camera1Layout = new System.Windows.Forms.TableLayoutPanel();
            this.lblCamera1Stats = new System.Windows.Forms.Label();
            this.pictureCamera1 = new System.Windows.Forms.PictureBox();
            this.groupCamera2 = new System.Windows.Forms.GroupBox();
            this.camera2Layout = new System.Windows.Forms.TableLayoutPanel();
            this.lblCamera2Stats = new System.Windows.Forms.Label();
            this.pictureCamera2 = new System.Windows.Forms.PictureBox();
            this.txtStatus = new System.Windows.Forms.TextBox();
            this.uiTimer = new System.Windows.Forms.Timer(this.components);
            this.rootLayout.SuspendLayout();
            this.parameterPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numWarmupFrames)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSampleFrames)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDisplayFps)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cameraSplit)).BeginInit();
            this.cameraSplit.Panel1.SuspendLayout();
            this.cameraSplit.Panel2.SuspendLayout();
            this.cameraSplit.SuspendLayout();
            this.groupCamera1.SuspendLayout();
            this.camera1Layout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureCamera1)).BeginInit();
            this.groupCamera2.SuspendLayout();
            this.camera2Layout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureCamera2)).BeginInit();
            this.SuspendLayout();
            //
            // rootLayout
            //
            this.rootLayout.ColumnCount = 1;
            this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.Controls.Add(this.parameterPanel, 0, 0);
            this.rootLayout.Controls.Add(this.cameraSplit, 0, 1);
            this.rootLayout.Controls.Add(this.txtStatus, 0, 2);
            this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootLayout.Location = new System.Drawing.Point(0, 0);
            this.rootLayout.Margin = new System.Windows.Forms.Padding(0);
            this.rootLayout.Name = "rootLayout";
            this.rootLayout.RowCount = 3;
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 126F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 145F));
            this.rootLayout.Size = new System.Drawing.Size(1420, 850);
            this.rootLayout.TabIndex = 0;
            //
            // parameterPanel
            //
            this.parameterPanel.Controls.Add(this.lblCamera1);
            this.parameterPanel.Controls.Add(this.cmbCamera1);
            this.parameterPanel.Controls.Add(this.lblCamera2);
            this.parameterPanel.Controls.Add(this.cmbCamera2);
            this.parameterPanel.Controls.Add(this.btnRefreshDevices);
            this.parameterPanel.Controls.Add(this.lblDeviceCount);
            this.parameterPanel.Controls.Add(this.lblTriggerMode);
            this.parameterPanel.Controls.Add(this.cmbTriggerMode);
            this.parameterPanel.Controls.Add(this.lblWarmupFrames);
            this.parameterPanel.Controls.Add(this.numWarmupFrames);
            this.parameterPanel.Controls.Add(this.lblSampleFrames);
            this.parameterPanel.Controls.Add(this.numSampleFrames);
            this.parameterPanel.Controls.Add(this.chkDisplayEnabled);
            this.parameterPanel.Controls.Add(this.lblDisplayFps);
            this.parameterPanel.Controls.Add(this.numDisplayFps);
            this.parameterPanel.Controls.Add(this.btnStart);
            this.parameterPanel.Controls.Add(this.btnStop);
            this.parameterPanel.Controls.Add(this.lblResultPath);
            this.parameterPanel.Controls.Add(this.txtResultPath);
            this.parameterPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.parameterPanel.Location = new System.Drawing.Point(0, 0);
            this.parameterPanel.Margin = new System.Windows.Forms.Padding(0);
            this.parameterPanel.Name = "parameterPanel";
            this.parameterPanel.Size = new System.Drawing.Size(1420, 126);
            this.parameterPanel.TabIndex = 0;
            //
            // lblCamera1
            //
            this.lblCamera1.AutoSize = true;
            this.lblCamera1.Location = new System.Drawing.Point(16, 17);
            this.lblCamera1.Name = "lblCamera1";
            this.lblCamera1.Size = new System.Drawing.Size(56, 17);
            this.lblCamera1.TabIndex = 0;
            this.lblCamera1.Text = "相机1：";
            //
            // cmbCamera1
            //
            this.cmbCamera1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCamera1.FormattingEnabled = true;
            this.cmbCamera1.Location = new System.Drawing.Point(78, 13);
            this.cmbCamera1.Name = "cmbCamera1";
            this.cmbCamera1.Size = new System.Drawing.Size(490, 25);
            this.cmbCamera1.TabIndex = 1;
            //
            // lblCamera2
            //
            this.lblCamera2.AutoSize = true;
            this.lblCamera2.Location = new System.Drawing.Point(586, 17);
            this.lblCamera2.Name = "lblCamera2";
            this.lblCamera2.Size = new System.Drawing.Size(56, 17);
            this.lblCamera2.TabIndex = 2;
            this.lblCamera2.Text = "相机2：";
            //
            // cmbCamera2
            //
            this.cmbCamera2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbCamera2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCamera2.FormattingEnabled = true;
            this.cmbCamera2.Location = new System.Drawing.Point(648, 13);
            this.cmbCamera2.Name = "cmbCamera2";
            this.cmbCamera2.Size = new System.Drawing.Size(470, 25);
            this.cmbCamera2.TabIndex = 3;
            //
            // btnRefreshDevices
            //
            this.btnRefreshDevices.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefreshDevices.Location = new System.Drawing.Point(1134, 11);
            this.btnRefreshDevices.Name = "btnRefreshDevices";
            this.btnRefreshDevices.Size = new System.Drawing.Size(108, 29);
            this.btnRefreshDevices.TabIndex = 4;
            this.btnRefreshDevices.Text = "重新枚举";
            this.btnRefreshDevices.UseVisualStyleBackColor = true;
            this.btnRefreshDevices.Click += new System.EventHandler(this.btnRefreshDevices_Click);
            //
            // lblDeviceCount
            //
            this.lblDeviceCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDeviceCount.AutoSize = true;
            this.lblDeviceCount.Location = new System.Drawing.Point(1254, 17);
            this.lblDeviceCount.Name = "lblDeviceCount";
            this.lblDeviceCount.Size = new System.Drawing.Size(80, 17);
            this.lblDeviceCount.TabIndex = 5;
            this.lblDeviceCount.Text = "发现设备：0";
            //
            // lblTriggerMode
            //
            this.lblTriggerMode.AutoSize = true;
            this.lblTriggerMode.Location = new System.Drawing.Point(16, 55);
            this.lblTriggerMode.Name = "lblTriggerMode";
            this.lblTriggerMode.Size = new System.Drawing.Size(68, 17);
            this.lblTriggerMode.TabIndex = 6;
            this.lblTriggerMode.Text = "触发方式：";
            //
            // cmbTriggerMode
            //
            this.cmbTriggerMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTriggerMode.FormattingEnabled = true;
            this.cmbTriggerMode.Location = new System.Drawing.Point(90, 51);
            this.cmbTriggerMode.Name = "cmbTriggerMode";
            this.cmbTriggerMode.Size = new System.Drawing.Size(245, 25);
            this.cmbTriggerMode.TabIndex = 7;
            //
            // lblWarmupFrames
            //
            this.lblWarmupFrames.AutoSize = true;
            this.lblWarmupFrames.Location = new System.Drawing.Point(354, 55);
            this.lblWarmupFrames.Name = "lblWarmupFrames";
            this.lblWarmupFrames.Size = new System.Drawing.Size(68, 17);
            this.lblWarmupFrames.TabIndex = 8;
            this.lblWarmupFrames.Text = "预热帧数：";
            //
            // numWarmupFrames
            //
            this.numWarmupFrames.Location = new System.Drawing.Point(428, 52);
            this.numWarmupFrames.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numWarmupFrames.Name = "numWarmupFrames";
            this.numWarmupFrames.Size = new System.Drawing.Size(82, 23);
            this.numWarmupFrames.TabIndex = 9;
            this.numWarmupFrames.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // lblSampleFrames
            //
            this.lblSampleFrames.AutoSize = true;
            this.lblSampleFrames.Location = new System.Drawing.Point(529, 55);
            this.lblSampleFrames.Name = "lblSampleFrames";
            this.lblSampleFrames.Size = new System.Drawing.Size(68, 17);
            this.lblSampleFrames.TabIndex = 10;
            this.lblSampleFrames.Text = "正式样本：";
            //
            // numSampleFrames
            //
            this.numSampleFrames.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numSampleFrames.Location = new System.Drawing.Point(603, 52);
            this.numSampleFrames.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
            this.numSampleFrames.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numSampleFrames.Name = "numSampleFrames";
            this.numSampleFrames.Size = new System.Drawing.Size(102, 23);
            this.numSampleFrames.TabIndex = 11;
            this.numSampleFrames.Value = new decimal(new int[] { 10000, 0, 0, 0 });
            //
            // chkDisplayEnabled
            //
            this.chkDisplayEnabled.AutoSize = true;
            this.chkDisplayEnabled.Checked = true;
            this.chkDisplayEnabled.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkDisplayEnabled.Location = new System.Drawing.Point(729, 53);
            this.chkDisplayEnabled.Name = "chkDisplayEnabled";
            this.chkDisplayEnabled.Size = new System.Drawing.Size(75, 21);
            this.chkDisplayEnabled.TabIndex = 12;
            this.chkDisplayEnabled.Text = "实时显示";
            this.chkDisplayEnabled.UseVisualStyleBackColor = true;
            //
            // lblDisplayFps
            //
            this.lblDisplayFps.AutoSize = true;
            this.lblDisplayFps.Location = new System.Drawing.Point(820, 55);
            this.lblDisplayFps.Name = "lblDisplayFps";
            this.lblDisplayFps.Size = new System.Drawing.Size(68, 17);
            this.lblDisplayFps.TabIndex = 13;
            this.lblDisplayFps.Text = "显示帧率：";
            //
            // numDisplayFps
            //
            this.numDisplayFps.Location = new System.Drawing.Point(894, 52);
            this.numDisplayFps.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
            this.numDisplayFps.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numDisplayFps.Name = "numDisplayFps";
            this.numDisplayFps.Size = new System.Drawing.Size(65, 23);
            this.numDisplayFps.TabIndex = 14;
            this.numDisplayFps.Value = new decimal(new int[] { 20, 0, 0, 0 });
            //
            // btnStart
            //
            this.btnStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStart.Location = new System.Drawing.Point(1134, 47);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(108, 32);
            this.btnStart.TabIndex = 15;
            this.btnStart.Text = "开始测试";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            //
            // btnStop
            //
            this.btnStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(1254, 47);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(108, 32);
            this.btnStop.TabIndex = 16;
            this.btnStop.Text = "停止并导出";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // lblResultPath
            //
            this.lblResultPath.AutoSize = true;
            this.lblResultPath.Location = new System.Drawing.Point(16, 95);
            this.lblResultPath.Name = "lblResultPath";
            this.lblResultPath.Size = new System.Drawing.Size(68, 17);
            this.lblResultPath.TabIndex = 17;
            this.lblResultPath.Text = "结果目录：";
            //
            // txtResultPath
            //
            this.txtResultPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtResultPath.Location = new System.Drawing.Point(90, 92);
            this.txtResultPath.Name = "txtResultPath";
            this.txtResultPath.ReadOnly = true;
            this.txtResultPath.Size = new System.Drawing.Size(1272, 23);
            this.txtResultPath.TabIndex = 18;
            //
            // cameraSplit
            //
            this.cameraSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cameraSplit.Location = new System.Drawing.Point(8, 134);
            this.cameraSplit.Margin = new System.Windows.Forms.Padding(8);
            this.cameraSplit.Name = "cameraSplit";
            //
            // cameraSplit.Panel1
            //
            this.cameraSplit.Panel1.Controls.Add(this.groupCamera1);
            //
            // cameraSplit.Panel2
            //
            this.cameraSplit.Panel2.Controls.Add(this.groupCamera2);
            this.cameraSplit.Size = new System.Drawing.Size(1404, 563);
            this.cameraSplit.SplitterDistance = 696;
            this.cameraSplit.SplitterWidth = 8;
            this.cameraSplit.TabIndex = 1;
            //
            // groupCamera1
            //
            this.groupCamera1.Controls.Add(this.camera1Layout);
            this.groupCamera1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupCamera1.Location = new System.Drawing.Point(0, 0);
            this.groupCamera1.Name = "groupCamera1";
            this.groupCamera1.Padding = new System.Windows.Forms.Padding(8);
            this.groupCamera1.Size = new System.Drawing.Size(696, 563);
            this.groupCamera1.TabIndex = 0;
            this.groupCamera1.TabStop = false;
            this.groupCamera1.Text = "相机1";
            //
            // camera1Layout
            //
            this.camera1Layout.ColumnCount = 1;
            this.camera1Layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.camera1Layout.Controls.Add(this.lblCamera1Stats, 0, 0);
            this.camera1Layout.Controls.Add(this.pictureCamera1, 0, 1);
            this.camera1Layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.camera1Layout.Location = new System.Drawing.Point(8, 24);
            this.camera1Layout.Name = "camera1Layout";
            this.camera1Layout.RowCount = 2;
            this.camera1Layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.camera1Layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.camera1Layout.Size = new System.Drawing.Size(680, 531);
            this.camera1Layout.TabIndex = 0;
            //
            // lblCamera1Stats
            //
            this.lblCamera1Stats.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCamera1Stats.Location = new System.Drawing.Point(3, 0);
            this.lblCamera1Stats.Name = "lblCamera1Stats";
            this.lblCamera1Stats.Size = new System.Drawing.Size(674, 32);
            this.lblCamera1Stats.TabIndex = 0;
            this.lblCamera1Stats.Text = "等待开始";
            this.lblCamera1Stats.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // pictureCamera1
            //
            this.pictureCamera1.BackColor = System.Drawing.Color.Black;
            this.pictureCamera1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureCamera1.Location = new System.Drawing.Point(3, 35);
            this.pictureCamera1.Name = "pictureCamera1";
            this.pictureCamera1.Size = new System.Drawing.Size(674, 493);
            this.pictureCamera1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureCamera1.TabIndex = 1;
            this.pictureCamera1.TabStop = false;
            //
            // groupCamera2
            //
            this.groupCamera2.Controls.Add(this.camera2Layout);
            this.groupCamera2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupCamera2.Location = new System.Drawing.Point(0, 0);
            this.groupCamera2.Name = "groupCamera2";
            this.groupCamera2.Padding = new System.Windows.Forms.Padding(8);
            this.groupCamera2.Size = new System.Drawing.Size(700, 563);
            this.groupCamera2.TabIndex = 0;
            this.groupCamera2.TabStop = false;
            this.groupCamera2.Text = "相机2";
            //
            // camera2Layout
            //
            this.camera2Layout.ColumnCount = 1;
            this.camera2Layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.camera2Layout.Controls.Add(this.lblCamera2Stats, 0, 0);
            this.camera2Layout.Controls.Add(this.pictureCamera2, 0, 1);
            this.camera2Layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.camera2Layout.Location = new System.Drawing.Point(8, 24);
            this.camera2Layout.Name = "camera2Layout";
            this.camera2Layout.RowCount = 2;
            this.camera2Layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.camera2Layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.camera2Layout.Size = new System.Drawing.Size(684, 531);
            this.camera2Layout.TabIndex = 0;
            //
            // lblCamera2Stats
            //
            this.lblCamera2Stats.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCamera2Stats.Location = new System.Drawing.Point(3, 0);
            this.lblCamera2Stats.Name = "lblCamera2Stats";
            this.lblCamera2Stats.Size = new System.Drawing.Size(678, 32);
            this.lblCamera2Stats.TabIndex = 0;
            this.lblCamera2Stats.Text = "等待开始";
            this.lblCamera2Stats.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // pictureCamera2
            //
            this.pictureCamera2.BackColor = System.Drawing.Color.Black;
            this.pictureCamera2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureCamera2.Location = new System.Drawing.Point(3, 35);
            this.pictureCamera2.Name = "pictureCamera2";
            this.pictureCamera2.Size = new System.Drawing.Size(678, 493);
            this.pictureCamera2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureCamera2.TabIndex = 1;
            this.pictureCamera2.TabStop = false;
            //
            // txtStatus
            //
            this.txtStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtStatus.Location = new System.Drawing.Point(8, 713);
            this.txtStatus.Margin = new System.Windows.Forms.Padding(8);
            this.txtStatus.Multiline = true;
            this.txtStatus.Name = "txtStatus";
            this.txtStatus.ReadOnly = true;
            this.txtStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtStatus.Size = new System.Drawing.Size(1404, 129);
            this.txtStatus.TabIndex = 2;
            //
            // uiTimer
            //
            this.uiTimer.Interval = 50;
            this.uiTimer.Tick += new System.EventHandler(this.uiTimer_Tick);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1420, 850);
            this.Controls.Add(this.rootLayout);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.MinimumSize = new System.Drawing.Size(1120, 720);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "海康双相机Mat耗时稳定性测试";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.rootLayout.ResumeLayout(false);
            this.rootLayout.PerformLayout();
            this.parameterPanel.ResumeLayout(false);
            this.parameterPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numWarmupFrames)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSampleFrames)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDisplayFps)).EndInit();
            this.cameraSplit.Panel1.ResumeLayout(false);
            this.cameraSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.cameraSplit)).EndInit();
            this.cameraSplit.ResumeLayout(false);
            this.groupCamera1.ResumeLayout(false);
            this.camera1Layout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureCamera1)).EndInit();
            this.groupCamera2.ResumeLayout(false);
            this.camera2Layout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureCamera2)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
