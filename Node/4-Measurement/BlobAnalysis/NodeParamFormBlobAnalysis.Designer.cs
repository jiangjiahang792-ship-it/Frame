namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    partial class NodeParamFormBlobAnalysis
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法。
        /// </summary>
        private void InitializeComponent()
        {
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxEnableDetectionRegion = new System.Windows.Forms.CheckBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxRegion = new System.Windows.Forms.GroupBox();
            this.labelRegionColumn = new System.Windows.Forms.Label();
            this.textBoxRegionColumn = new System.Windows.Forms.TextBox();
            this.labelRegionRow = new System.Windows.Forms.Label();
            this.textBoxRegionRow = new System.Windows.Forms.TextBox();
            this.labelRegionLength1 = new System.Windows.Forms.Label();
            this.textBoxRegionLength1 = new System.Windows.Forms.TextBox();
            this.labelRegionLength2 = new System.Windows.Forms.Label();
            this.textBoxRegionLength2 = new System.Windows.Forms.TextBox();
            this.labelRegionPhi = new System.Windows.Forms.Label();
            this.textBoxRegionPhi = new System.Windows.Forms.TextBox();
            this.groupBoxGray = new System.Windows.Forms.GroupBox();
            this.labelMinGray = new System.Windows.Forms.Label();
            this.numericMinGray = new System.Windows.Forms.NumericUpDown();
            this.trackBarMinGray = new System.Windows.Forms.TrackBar();
            this.labelMaxGray = new System.Windows.Forms.Label();
            this.numericMaxGray = new System.Windows.Forms.NumericUpDown();
            this.trackBarMaxGray = new System.Windows.Forms.TrackBar();
            this.labelOutputRegionMode = new System.Windows.Forms.Label();
            this.comboBoxOutputRegionMode = new System.Windows.Forms.ComboBox();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.groupBoxRegion.SuspendLayout();
            this.groupBoxGray.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMinGray)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarMinGray)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxGray)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarMaxGray)).BeginInit();
            this.SuspendLayout();
            // 
            // nodeSubscriptionImage
            // 
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(16, 26);
            this.nodeSubscriptionImage.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(356, 32);
            this.nodeSubscriptionImage.TabIndex = 0;
            // 
            // groupBoxInput
            // 
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxInput.Location = new System.Drawing.Point(18, 44);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(390, 74);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入图像";
            // 
            // groupBoxPositionCorrection
            // 
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxEnableDetectionRegion);
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(18, 124);
            this.groupBoxPositionCorrection.Name = "groupBoxPositionCorrection";
            this.groupBoxPositionCorrection.Size = new System.Drawing.Size(390, 112);
            this.groupBoxPositionCorrection.TabIndex = 1;
            this.groupBoxPositionCorrection.TabStop = false;
            this.groupBoxPositionCorrection.Text = "检测区域";
            // 
            // checkBoxEnableDetectionRegion
            // 
            this.checkBoxEnableDetectionRegion.AutoSize = true;
            this.checkBoxEnableDetectionRegion.Location = new System.Drawing.Point(16, 24);
            this.checkBoxEnableDetectionRegion.Name = "checkBoxEnableDetectionRegion";
            this.checkBoxEnableDetectionRegion.Size = new System.Drawing.Size(89, 19);
            this.checkBoxEnableDetectionRegion.TabIndex = 0;
            this.checkBoxEnableDetectionRegion.Text = "启用区域";
            this.checkBoxEnableDetectionRegion.UseVisualStyleBackColor = true;
            this.checkBoxEnableDetectionRegion.CheckedChanged += new System.EventHandler(this.checkBoxEnableDetectionRegion_CheckedChanged);
            // 
            // checkBoxUsePositionCorrection
            // 
            this.checkBoxUsePositionCorrection.AutoSize = true;
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(126, 24);
            this.checkBoxUsePositionCorrection.Name = "checkBoxUsePositionCorrection";
            this.checkBoxUsePositionCorrection.Size = new System.Drawing.Size(89, 19);
            this.checkBoxUsePositionCorrection.TabIndex = 1;
            this.checkBoxUsePositionCorrection.Text = "启用修正";
            this.checkBoxUsePositionCorrection.UseVisualStyleBackColor = true;
            this.checkBoxUsePositionCorrection.CheckedChanged += new System.EventHandler(this.checkBoxUsePositionCorrection_CheckedChanged);
            // 
            // nodeSubscriptionPositionCorrection
            // 
            this.nodeSubscriptionPositionCorrection.Enabled = false;
            this.nodeSubscriptionPositionCorrection.Location = new System.Drawing.Point(16, 56);
            this.nodeSubscriptionPositionCorrection.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionPositionCorrection.Name = "nodeSubscriptionPositionCorrection";
            this.nodeSubscriptionPositionCorrection.Size = new System.Drawing.Size(356, 32);
            this.nodeSubscriptionPositionCorrection.TabIndex = 2;
            // 
            // groupBoxRegion
            // 
            this.groupBoxRegion.Controls.Add(this.labelRegionColumn);
            this.groupBoxRegion.Controls.Add(this.textBoxRegionColumn);
            this.groupBoxRegion.Controls.Add(this.labelRegionRow);
            this.groupBoxRegion.Controls.Add(this.textBoxRegionRow);
            this.groupBoxRegion.Controls.Add(this.labelRegionLength1);
            this.groupBoxRegion.Controls.Add(this.textBoxRegionLength1);
            this.groupBoxRegion.Controls.Add(this.labelRegionLength2);
            this.groupBoxRegion.Controls.Add(this.textBoxRegionLength2);
            this.groupBoxRegion.Controls.Add(this.labelRegionPhi);
            this.groupBoxRegion.Controls.Add(this.textBoxRegionPhi);
            this.groupBoxRegion.Location = new System.Drawing.Point(18, 242);
            this.groupBoxRegion.Name = "groupBoxRegion";
            this.groupBoxRegion.Size = new System.Drawing.Size(390, 156);
            this.groupBoxRegion.TabIndex = 2;
            this.groupBoxRegion.TabStop = false;
            this.groupBoxRegion.Text = "区域参数";
            // 
            // labelRegionColumn
            // 
            this.labelRegionColumn.AutoSize = true;
            this.labelRegionColumn.Location = new System.Drawing.Point(16, 32);
            this.labelRegionColumn.Name = "labelRegionColumn";
            this.labelRegionColumn.Size = new System.Drawing.Size(52, 15);
            this.labelRegionColumn.TabIndex = 0;
            this.labelRegionColumn.Text = "中心X";
            // 
            // textBoxRegionColumn
            // 
            this.textBoxRegionColumn.Location = new System.Drawing.Point(72, 28);
            this.textBoxRegionColumn.Name = "textBoxRegionColumn";
            this.textBoxRegionColumn.Size = new System.Drawing.Size(100, 25);
            this.textBoxRegionColumn.TabIndex = 1;
            this.textBoxRegionColumn.Text = "200";
            // 
            // labelRegionRow
            // 
            this.labelRegionRow.AutoSize = true;
            this.labelRegionRow.Location = new System.Drawing.Point(206, 32);
            this.labelRegionRow.Name = "labelRegionRow";
            this.labelRegionRow.Size = new System.Drawing.Size(52, 15);
            this.labelRegionRow.TabIndex = 2;
            this.labelRegionRow.Text = "中心Y";
            // 
            // textBoxRegionRow
            // 
            this.textBoxRegionRow.Location = new System.Drawing.Point(262, 28);
            this.textBoxRegionRow.Name = "textBoxRegionRow";
            this.textBoxRegionRow.Size = new System.Drawing.Size(100, 25);
            this.textBoxRegionRow.TabIndex = 3;
            this.textBoxRegionRow.Text = "200";
            // 
            // labelRegionLength1
            // 
            this.labelRegionLength1.AutoSize = true;
            this.labelRegionLength1.Location = new System.Drawing.Point(16, 74);
            this.labelRegionLength1.Name = "labelRegionLength1";
            this.labelRegionLength1.Size = new System.Drawing.Size(37, 15);
            this.labelRegionLength1.TabIndex = 4;
            this.labelRegionLength1.Text = "半宽";
            // 
            // textBoxRegionLength1
            // 
            this.textBoxRegionLength1.Location = new System.Drawing.Point(72, 70);
            this.textBoxRegionLength1.Name = "textBoxRegionLength1";
            this.textBoxRegionLength1.Size = new System.Drawing.Size(100, 25);
            this.textBoxRegionLength1.TabIndex = 5;
            this.textBoxRegionLength1.Text = "100";
            // 
            // labelRegionLength2
            // 
            this.labelRegionLength2.AutoSize = true;
            this.labelRegionLength2.Location = new System.Drawing.Point(206, 74);
            this.labelRegionLength2.Name = "labelRegionLength2";
            this.labelRegionLength2.Size = new System.Drawing.Size(37, 15);
            this.labelRegionLength2.TabIndex = 6;
            this.labelRegionLength2.Text = "半高";
            // 
            // textBoxRegionLength2
            // 
            this.textBoxRegionLength2.Location = new System.Drawing.Point(262, 70);
            this.textBoxRegionLength2.Name = "textBoxRegionLength2";
            this.textBoxRegionLength2.Size = new System.Drawing.Size(100, 25);
            this.textBoxRegionLength2.TabIndex = 7;
            this.textBoxRegionLength2.Text = "100";
            // 
            // labelRegionPhi
            // 
            this.labelRegionPhi.AutoSize = true;
            this.labelRegionPhi.Location = new System.Drawing.Point(16, 116);
            this.labelRegionPhi.Name = "labelRegionPhi";
            this.labelRegionPhi.Size = new System.Drawing.Size(37, 15);
            this.labelRegionPhi.TabIndex = 8;
            this.labelRegionPhi.Text = "角度";
            // 
            // textBoxRegionPhi
            // 
            this.textBoxRegionPhi.Location = new System.Drawing.Point(72, 112);
            this.textBoxRegionPhi.Name = "textBoxRegionPhi";
            this.textBoxRegionPhi.Size = new System.Drawing.Size(100, 25);
            this.textBoxRegionPhi.TabIndex = 9;
            this.textBoxRegionPhi.Text = "0";
            // 
            // groupBoxGray
            // 
            this.groupBoxGray.Controls.Add(this.comboBoxOutputRegionMode);
            this.groupBoxGray.Controls.Add(this.labelOutputRegionMode);
            this.groupBoxGray.Controls.Add(this.trackBarMaxGray);
            this.groupBoxGray.Controls.Add(this.trackBarMinGray);
            this.groupBoxGray.Controls.Add(this.labelMinGray);
            this.groupBoxGray.Controls.Add(this.numericMinGray);
            this.groupBoxGray.Controls.Add(this.labelMaxGray);
            this.groupBoxGray.Controls.Add(this.numericMaxGray);
            this.groupBoxGray.Location = new System.Drawing.Point(18, 404);
            this.groupBoxGray.Name = "groupBoxGray";
            this.groupBoxGray.Size = new System.Drawing.Size(390, 154);
            this.groupBoxGray.TabIndex = 3;
            this.groupBoxGray.TabStop = false;
            this.groupBoxGray.Text = "灰度范围";
            // 
            // labelMinGray
            // 
            this.labelMinGray.AutoSize = true;
            this.labelMinGray.Location = new System.Drawing.Point(16, 32);
            this.labelMinGray.Name = "labelMinGray";
            this.labelMinGray.Size = new System.Drawing.Size(67, 15);
            this.labelMinGray.TabIndex = 0;
            this.labelMinGray.Text = "最小灰度";
            // 
            // numericMinGray
            // 
            this.numericMinGray.Location = new System.Drawing.Point(302, 28);
            this.numericMinGray.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericMinGray.Name = "numericMinGray";
            this.numericMinGray.Size = new System.Drawing.Size(60, 25);
            this.numericMinGray.TabIndex = 1;
            // 
            // trackBarMinGray
            // 
            this.trackBarMinGray.AutoSize = false;
            this.trackBarMinGray.Location = new System.Drawing.Point(102, 24);
            this.trackBarMinGray.Maximum = 255;
            this.trackBarMinGray.Name = "trackBarMinGray";
            this.trackBarMinGray.Size = new System.Drawing.Size(190, 30);
            this.trackBarMinGray.TabIndex = 2;
            this.trackBarMinGray.TickFrequency = 32;
            // 
            // labelMaxGray
            // 
            this.labelMaxGray.AutoSize = true;
            this.labelMaxGray.Location = new System.Drawing.Point(16, 70);
            this.labelMaxGray.Name = "labelMaxGray";
            this.labelMaxGray.Size = new System.Drawing.Size(67, 15);
            this.labelMaxGray.TabIndex = 2;
            this.labelMaxGray.Text = "最大灰度";
            // 
            // numericMaxGray
            // 
            this.numericMaxGray.Location = new System.Drawing.Point(302, 66);
            this.numericMaxGray.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericMaxGray.Name = "numericMaxGray";
            this.numericMaxGray.Size = new System.Drawing.Size(60, 25);
            this.numericMaxGray.TabIndex = 3;
            this.numericMaxGray.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // trackBarMaxGray
            // 
            this.trackBarMaxGray.AutoSize = false;
            this.trackBarMaxGray.Location = new System.Drawing.Point(102, 62);
            this.trackBarMaxGray.Maximum = 255;
            this.trackBarMaxGray.Name = "trackBarMaxGray";
            this.trackBarMaxGray.Size = new System.Drawing.Size(190, 30);
            this.trackBarMaxGray.TabIndex = 4;
            this.trackBarMaxGray.TickFrequency = 32;
            this.trackBarMaxGray.Value = 255;
            // 
            // labelOutputRegionMode
            // 
            this.labelOutputRegionMode.AutoSize = true;
            this.labelOutputRegionMode.Location = new System.Drawing.Point(16, 116);
            this.labelOutputRegionMode.Name = "labelOutputRegionMode";
            this.labelOutputRegionMode.Size = new System.Drawing.Size(67, 15);
            this.labelOutputRegionMode.TabIndex = 5;
            this.labelOutputRegionMode.Text = "输出区域";
            // 
            // comboBoxOutputRegionMode
            // 
            this.comboBoxOutputRegionMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxOutputRegionMode.FormattingEnabled = true;
            this.comboBoxOutputRegionMode.Location = new System.Drawing.Point(102, 112);
            this.comboBoxOutputRegionMode.Name = "comboBoxOutputRegionMode";
            this.comboBoxOutputRegionMode.Size = new System.Drawing.Size(130, 23);
            this.comboBoxOutputRegionMode.TabIndex = 6;
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackColor = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundImageCustom = null;
            this.showImageControl1.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControl1.BackgroundTransparency = 1F;
            this.showImageControl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.showImageControl1.Location = new System.Drawing.Point(424, 44);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.RoiColor = System.Drawing.Color.Lime;
            this.showImageControl1.ShowCheckerBackground = false;
            this.showImageControl1.ShowPixelInfo = false;
            this.showImageControl1.Size = new System.Drawing.Size(520, 532);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 4;
            // 
            // buttonDrawRoi
            // 
            this.buttonDrawRoi.Location = new System.Drawing.Point(424, 594);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonDrawRoi.TabIndex = 5;
            this.buttonDrawRoi.Text = "绘制ROI";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            // 
            // buttonConfirmRoi
            // 
            this.buttonConfirmRoi.Location = new System.Drawing.Point(522, 594);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonConfirmRoi.TabIndex = 6;
            this.buttonConfirmRoi.Text = "确认ROI";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(620, 594);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(98, 34);
            this.buttonRefresh.TabIndex = 7;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(724, 594);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(100, 34);
            this.buttonRun.TabIndex = 8;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(844, 594);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(100, 34);
            this.buttonSave.TabIndex = 9;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormBlobAnalysis
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(962, 646);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonConfirmRoi);
            this.Controls.Add(this.buttonDrawRoi);
            this.Controls.Add(this.showImageControl1);
            this.Controls.Add(this.groupBoxGray);
            this.Controls.Add(this.groupBoxRegion);
            this.Controls.Add(this.groupBoxPositionCorrection);
            this.Controls.Add(this.groupBoxInput);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormBlobAnalysis";
            this.Text = "Blob分析工具";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxRegion.ResumeLayout(false);
            this.groupBoxRegion.PerformLayout();
            this.groupBoxGray.ResumeLayout(false);
            this.groupBoxGray.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMinGray)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarMinGray)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxGray)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarMaxGray)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        /// <summary>
        /// 输入图像订阅控件。
        /// </summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionImage;

        /// <summary>
        /// 输入图像分组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxInput;

        /// <summary>
        /// 检测区域与位置修正分组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;

        /// <summary>
        /// 检测区域启用开关。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxEnableDetectionRegion;

        /// <summary>
        /// 位置修正启用开关。
        /// </summary>
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;

        /// <summary>
        /// 位置修正订阅控件。
        /// </summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;

        /// <summary>
        /// 区域参数分组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxRegion;

        /// <summary>
        /// 区域中心X标签。
        /// </summary>
        private System.Windows.Forms.Label labelRegionColumn;

        /// <summary>
        /// 区域中心X输入框。
        /// </summary>
        private System.Windows.Forms.TextBox textBoxRegionColumn;

        /// <summary>
        /// 区域中心Y标签。
        /// </summary>
        private System.Windows.Forms.Label labelRegionRow;

        /// <summary>
        /// 区域中心Y输入框。
        /// </summary>
        private System.Windows.Forms.TextBox textBoxRegionRow;

        /// <summary>
        /// 区域半宽标签。
        /// </summary>
        private System.Windows.Forms.Label labelRegionLength1;

        /// <summary>
        /// 区域半宽输入框。
        /// </summary>
        private System.Windows.Forms.TextBox textBoxRegionLength1;

        /// <summary>
        /// 区域半高标签。
        /// </summary>
        private System.Windows.Forms.Label labelRegionLength2;

        /// <summary>
        /// 区域半高输入框。
        /// </summary>
        private System.Windows.Forms.TextBox textBoxRegionLength2;

        /// <summary>
        /// 区域角度标签。
        /// </summary>
        private System.Windows.Forms.Label labelRegionPhi;

        /// <summary>
        /// 区域角度输入框，单位为度。
        /// </summary>
        private System.Windows.Forms.TextBox textBoxRegionPhi;

        /// <summary>
        /// 灰度范围分组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxGray;

        /// <summary>
        /// 最小灰度标签。
        /// </summary>
        private System.Windows.Forms.Label labelMinGray;

        /// <summary>
        /// 最小灰度输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMinGray;

        /// <summary>
        /// 最小灰度滑块。
        /// </summary>
        private System.Windows.Forms.TrackBar trackBarMinGray;

        /// <summary>
        /// 最大灰度标签。
        /// </summary>
        private System.Windows.Forms.Label labelMaxGray;

        /// <summary>
        /// 最大灰度输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMaxGray;

        /// <summary>
        /// 最大灰度滑块。
        /// </summary>
        private System.Windows.Forms.TrackBar trackBarMaxGray;

        /// <summary>
        /// 输出区域模式标签。
        /// </summary>
        private System.Windows.Forms.Label labelOutputRegionMode;

        /// <summary>
        /// 输出区域模式下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxOutputRegionMode;

        /// <summary>
        /// 图像预览控件。
        /// </summary>
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;

        /// <summary>
        /// 绘制ROI按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonDrawRoi;

        /// <summary>
        /// 确认ROI按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonConfirmRoi;

        /// <summary>
        /// 刷新图像按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRefresh;

        /// <summary>
        /// 执行预览按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRun;

        /// <summary>
        /// 保存参数按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSave;
    }
}
