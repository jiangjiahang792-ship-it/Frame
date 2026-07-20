namespace TDJS_Vision.Node._1_Acquisition.ImageSource3D
{
    /// <summary>
    /// 3D 图像源参数窗体设计器。
    /// </summary>
    partial class ParamFormImageSource3D
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 主布局。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>
        /// 相机标签。
        /// </summary>
        private System.Windows.Forms.Label labelCamera;

        /// <summary>
        /// 3D 相机下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxCamera;

        /// <summary>
        /// 刷新相机按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRefresh;

        /// <summary>
        /// 图像模式标签。
        /// </summary>
        private System.Windows.Forms.Label labelImageMode;

        /// <summary>
        /// 图像模式下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxImageMode;

        /// <summary>
        /// 超时时间标签。
        /// </summary>
        private System.Windows.Forms.Label labelTimeOut;

        /// <summary>
        /// 超时时间输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericUpDownTimeOut;

        /// <summary>
        /// 最大点数标签。
        /// </summary>
        private System.Windows.Forms.Label labelMaxPointCount;

        /// <summary>
        /// 最大点数输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericUpDownMaxPointCount;

        /// <summary>
        /// 自动打开相机复选框。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxAutoOpen;

        /// <summary>
        /// 自动启动采集复选框。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxAutoStartGrabbing;

        /// <summary>
        /// 原生点云缓存复选框。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxNativeCache;

        /// <summary>
        /// 托管点云复选框。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxManagedPointCloud;

        /// <summary>
        /// 保存按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSave;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，则为 true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化窗体控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelCamera = new System.Windows.Forms.Label();
            this.comboBoxCamera = new System.Windows.Forms.ComboBox();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.labelImageMode = new System.Windows.Forms.Label();
            this.comboBoxImageMode = new System.Windows.Forms.ComboBox();
            this.labelTimeOut = new System.Windows.Forms.Label();
            this.numericUpDownTimeOut = new System.Windows.Forms.NumericUpDown();
            this.labelMaxPointCount = new System.Windows.Forms.Label();
            this.numericUpDownMaxPointCount = new System.Windows.Forms.NumericUpDown();
            this.checkBoxAutoOpen = new System.Windows.Forms.CheckBox();
            this.checkBoxAutoStartGrabbing = new System.Windows.Forms.CheckBox();
            this.checkBoxNativeCache = new System.Windows.Forms.CheckBox();
            this.checkBoxManagedPointCloud = new System.Windows.Forms.CheckBox();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeOut)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMaxPointCount)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 3;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 82F));
            this.tableLayoutPanelMain.Controls.Add(this.labelCamera, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxCamera, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(this.buttonRefresh, 2, 0);
            this.tableLayoutPanelMain.Controls.Add(this.labelImageMode, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxImageMode, 1, 1);
            this.tableLayoutPanelMain.Controls.Add(this.labelTimeOut, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.numericUpDownTimeOut, 1, 2);
            this.tableLayoutPanelMain.Controls.Add(this.labelMaxPointCount, 0, 3);
            this.tableLayoutPanelMain.Controls.Add(this.numericUpDownMaxPointCount, 1, 3);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxAutoOpen, 1, 4);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxAutoStartGrabbing, 1, 5);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxNativeCache, 1, 6);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxManagedPointCloud, 1, 7);
            this.tableLayoutPanelMain.Controls.Add(this.buttonSave, 1, 8);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.Padding = new System.Windows.Forms.Padding(12, 10, 12, 12);
            this.tableLayoutPanelMain.RowCount = 9;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(516, 380);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelCamera
            // 
            this.labelCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelCamera.Location = new System.Drawing.Point(15, 10);
            this.labelCamera.Name = "labelCamera";
            this.labelCamera.Size = new System.Drawing.Size(114, 38);
            this.labelCamera.TabIndex = 0;
            this.labelCamera.Text = "选择3D相机";
            this.labelCamera.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxCamera
            // 
            this.comboBoxCamera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCamera.FormattingEnabled = true;
            this.comboBoxCamera.Location = new System.Drawing.Point(123, 7);
            this.comboBoxCamera.Name = "comboBoxCamera";
            this.comboBoxCamera.Size = new System.Drawing.Size(252, 23);
            this.comboBoxCamera.TabIndex = 1;
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRefresh.Location = new System.Drawing.Point(383, 6);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(72, 26);
            this.buttonRefresh.TabIndex = 2;
            this.buttonRefresh.Text = "刷新";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // labelImageMode
            // 
            this.labelImageMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelImageMode.Location = new System.Drawing.Point(3, 38);
            this.labelImageMode.Name = "labelImageMode";
            this.labelImageMode.Size = new System.Drawing.Size(114, 38);
            this.labelImageMode.TabIndex = 3;
            this.labelImageMode.Text = "图像模式";
            this.labelImageMode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxImageMode
            // 
            this.comboBoxImageMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxImageMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxImageMode.FormattingEnabled = true;
            this.comboBoxImageMode.Items.AddRange(new object[] {
            "深度图",
            "点云图",
            "原始图",
            "亮度图"});
            this.comboBoxImageMode.Location = new System.Drawing.Point(123, 45);
            this.comboBoxImageMode.Name = "comboBoxImageMode";
            this.comboBoxImageMode.Size = new System.Drawing.Size(252, 23);
            this.comboBoxImageMode.TabIndex = 4;
            // 
            // labelTimeOut
            // 
            this.labelTimeOut.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTimeOut.Location = new System.Drawing.Point(3, 76);
            this.labelTimeOut.Name = "labelTimeOut";
            this.labelTimeOut.Size = new System.Drawing.Size(114, 38);
            this.labelTimeOut.TabIndex = 5;
            this.labelTimeOut.Text = "超时(ms)";
            this.labelTimeOut.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericUpDownTimeOut
            // 
            this.numericUpDownTimeOut.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDownTimeOut.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.numericUpDownTimeOut.Location = new System.Drawing.Point(123, 83);
            this.numericUpDownTimeOut.Maximum = new decimal(new int[] {
            60000,
            0,
            0,
            0});
            this.numericUpDownTimeOut.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numericUpDownTimeOut.Name = "numericUpDownTimeOut";
            this.numericUpDownTimeOut.Size = new System.Drawing.Size(252, 25);
            this.numericUpDownTimeOut.TabIndex = 6;
            this.numericUpDownTimeOut.Value = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            // 
            // labelMaxPointCount
            // 
            this.labelMaxPointCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelMaxPointCount.Location = new System.Drawing.Point(3, 114);
            this.labelMaxPointCount.Name = "labelMaxPointCount";
            this.labelMaxPointCount.Size = new System.Drawing.Size(114, 38);
            this.labelMaxPointCount.TabIndex = 7;
            this.labelMaxPointCount.Text = "最大点数";
            this.labelMaxPointCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // numericUpDownMaxPointCount
            // 
            this.numericUpDownMaxPointCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.numericUpDownMaxPointCount.Increment = new decimal(new int[] {
            10000,
            0,
            0,
            0});
            this.numericUpDownMaxPointCount.Location = new System.Drawing.Point(123, 121);
            this.numericUpDownMaxPointCount.Maximum = new decimal(new int[] {
            5000000,
            0,
            0,
            0});
            this.numericUpDownMaxPointCount.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numericUpDownMaxPointCount.Name = "numericUpDownMaxPointCount";
            this.numericUpDownMaxPointCount.Size = new System.Drawing.Size(252, 25);
            this.numericUpDownMaxPointCount.TabIndex = 8;
            this.numericUpDownMaxPointCount.Value = new decimal(new int[] {
            350000,
            0,
            0,
            0});
            // 
            // checkBoxAutoOpen
            // 
            this.checkBoxAutoOpen.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxAutoOpen.AutoSize = true;
            this.tableLayoutPanelMain.SetColumnSpan(this.checkBoxAutoOpen, 2);
            this.checkBoxAutoOpen.Location = new System.Drawing.Point(123, 159);
            this.checkBoxAutoOpen.Name = "checkBoxAutoOpen";
            this.checkBoxAutoOpen.Size = new System.Drawing.Size(119, 19);
            this.checkBoxAutoOpen.TabIndex = 9;
            this.checkBoxAutoOpen.Text = "未连接时自动打开";
            this.checkBoxAutoOpen.UseVisualStyleBackColor = true;
            // 
            // checkBoxAutoStartGrabbing
            // 
            this.checkBoxAutoStartGrabbing.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxAutoStartGrabbing.AutoSize = true;
            this.tableLayoutPanelMain.SetColumnSpan(this.checkBoxAutoStartGrabbing, 2);
            this.checkBoxAutoStartGrabbing.Location = new System.Drawing.Point(123, 191);
            this.checkBoxAutoStartGrabbing.Name = "checkBoxAutoStartGrabbing";
            this.checkBoxAutoStartGrabbing.Size = new System.Drawing.Size(119, 19);
            this.checkBoxAutoStartGrabbing.TabIndex = 10;
            this.checkBoxAutoStartGrabbing.Text = "未采集时自动取流";
            this.checkBoxAutoStartGrabbing.UseVisualStyleBackColor = true;
            // 
            // checkBoxNativeCache
            // 
            this.checkBoxNativeCache.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxNativeCache.AutoSize = true;
            this.checkBoxNativeCache.Checked = true;
            this.checkBoxNativeCache.CheckState = System.Windows.Forms.CheckState.Checked;
            this.tableLayoutPanelMain.SetColumnSpan(this.checkBoxNativeCache, 2);
            this.checkBoxNativeCache.Location = new System.Drawing.Point(123, 223);
            this.checkBoxNativeCache.Name = "checkBoxNativeCache";
            this.checkBoxNativeCache.Size = new System.Drawing.Size(119, 19);
            this.checkBoxNativeCache.TabIndex = 11;
            this.checkBoxNativeCache.Text = "缓存原生点云帧";
            this.checkBoxNativeCache.UseVisualStyleBackColor = true;
            // 
            // checkBoxManagedPointCloud
            // 
            this.checkBoxManagedPointCloud.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxManagedPointCloud.AutoSize = true;
            this.tableLayoutPanelMain.SetColumnSpan(this.checkBoxManagedPointCloud, 2);
            this.checkBoxManagedPointCloud.Location = new System.Drawing.Point(123, 255);
            this.checkBoxManagedPointCloud.Name = "checkBoxManagedPointCloud";
            this.checkBoxManagedPointCloud.Size = new System.Drawing.Size(134, 19);
            this.checkBoxManagedPointCloud.TabIndex = 12;
            this.checkBoxManagedPointCloud.Text = "生成托管点云抽样";
            this.checkBoxManagedPointCloud.UseVisualStyleBackColor = true;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.buttonSave.Location = new System.Drawing.Point(211, 287);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 7, 3, 3);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 28);
            this.buttonSave.TabIndex = 13;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // ParamFormImageSource3D
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(520, 420);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "ParamFormImageSource3D";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "3D图像源";
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimeOut)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMaxPointCount)).EndInit();
            this.ResumeLayout(false);

        }
    }
}
