namespace TDJS_Vision.Node._1_Acquisition.CameraExposureGain
{
    partial class ParamFormCameraExposureGain
    {
        /// <summary>
        /// 设计器组件容器。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 释放窗体占用的资源。
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

        #region Windows Form Designer generated code

        /// <summary>
        /// 设计器支持所需的方法。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelCamera = new System.Windows.Forms.Label();
            this.comboBoxCamera = new System.Windows.Forms.ComboBox();
            this.labelExposure = new System.Windows.Forms.Label();
            this.numericUpDownExposure = new System.Windows.Forms.NumericUpDown();
            this.labelGain = new System.Windows.Forms.Label();
            this.numericUpDownGain = new System.Windows.Forms.NumericUpDown();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownExposure)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGain)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56F));
            this.tableLayoutPanelMain.Controls.Add(this.labelCamera, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxCamera, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(this.labelExposure, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.numericUpDownExposure, 1, 1);
            this.tableLayoutPanelMain.Controls.Add(this.labelGain, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.numericUpDownGain, 1, 2);
            this.tableLayoutPanelMain.Controls.Add(this.buttonSave, 0, 3);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 4;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(560, 238);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelCamera
            // 
            this.labelCamera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.labelCamera.AutoSize = true;
            this.labelCamera.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelCamera.Location = new System.Drawing.Point(3, 20);
            this.labelCamera.Name = "labelCamera";
            this.labelCamera.Size = new System.Drawing.Size(240, 18);
            this.labelCamera.TabIndex = 0;
            this.labelCamera.Text = "选择相机";
            this.labelCamera.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboBoxCamera
            // 
            this.comboBoxCamera.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCamera.Font = new System.Drawing.Font("宋体", 10.5F);
            this.comboBoxCamera.FormattingEnabled = true;
            this.comboBoxCamera.Items.AddRange(new object[] {
            "[未设置]"});
            this.comboBoxCamera.Location = new System.Drawing.Point(297, 16);
            this.comboBoxCamera.Name = "comboBoxCamera";
            this.comboBoxCamera.Size = new System.Drawing.Size(211, 25);
            this.comboBoxCamera.TabIndex = 1;
            this.comboBoxCamera.SelectedIndexChanged += new System.EventHandler(this.comboBoxCamera_SelectedIndexChanged);
            // 
            // labelExposure
            // 
            this.labelExposure.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.labelExposure.AutoSize = true;
            this.labelExposure.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelExposure.Location = new System.Drawing.Point(3, 79);
            this.labelExposure.Name = "labelExposure";
            this.labelExposure.Size = new System.Drawing.Size(240, 18);
            this.labelExposure.TabIndex = 2;
            this.labelExposure.Text = "曝光(us)";
            this.labelExposure.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDownExposure
            // 
            this.numericUpDownExposure.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownExposure.DecimalPlaces = 2;
            this.numericUpDownExposure.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownExposure.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.numericUpDownExposure.Location = new System.Drawing.Point(297, 75);
            this.numericUpDownExposure.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.numericUpDownExposure.Name = "numericUpDownExposure";
            this.numericUpDownExposure.Size = new System.Drawing.Size(211, 27);
            this.numericUpDownExposure.TabIndex = 3;
            this.numericUpDownExposure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericUpDownExposure.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // labelGain
            // 
            this.labelGain.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.labelGain.AutoSize = true;
            this.labelGain.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelGain.Location = new System.Drawing.Point(3, 138);
            this.labelGain.Name = "labelGain";
            this.labelGain.Size = new System.Drawing.Size(240, 18);
            this.labelGain.TabIndex = 4;
            this.labelGain.Text = "增益";
            this.labelGain.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // numericUpDownGain
            // 
            this.numericUpDownGain.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.numericUpDownGain.DecimalPlaces = 2;
            this.numericUpDownGain.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownGain.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericUpDownGain.Location = new System.Drawing.Point(297, 134);
            this.numericUpDownGain.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numericUpDownGain.Name = "numericUpDownGain";
            this.numericUpDownGain.Size = new System.Drawing.Size(211, 27);
            this.numericUpDownGain.TabIndex = 5;
            this.numericUpDownGain.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanelMain.SetColumnSpan(this.buttonSave, 2);
            this.buttonSave.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonSave.Location = new System.Drawing.Point(235, 188);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(90, 38);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // ParamFormCameraExposureGain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(564, 272);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormCameraExposureGain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "相机曝光增益";
            this.Shown += new System.EventHandler(this.ParamFormCameraExposureGain_Shown);
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownExposure)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownGain)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        /// <summary>
        /// 主布局面板。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>
        /// 选择相机文本标签。
        /// </summary>
        private System.Windows.Forms.Label labelCamera;

        /// <summary>
        /// 相机选择下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxCamera;

        /// <summary>
        /// 曝光文本标签。
        /// </summary>
        private System.Windows.Forms.Label labelExposure;

        /// <summary>
        /// 曝光数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericUpDownExposure;

        /// <summary>
        /// 增益文本标签。
        /// </summary>
        private System.Windows.Forms.Label labelGain;

        /// <summary>
        /// 增益数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericUpDownGain;

        /// <summary>
        /// 保存按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSave;
    }
}
