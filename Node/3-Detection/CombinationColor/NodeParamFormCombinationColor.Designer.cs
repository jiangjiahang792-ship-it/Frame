namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    partial class NodeParamFormCombinationColor
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>选择识别颜色空间。</summary>
        private System.Windows.Forms.ComboBox comboColorSpace;
        /// <summary>从当前旋转框学习主色。</summary>
        private System.Windows.Forms.Button buttonLearn;
        /// <summary>独立绘制颜色采样框。</summary>
        private System.Windows.Forms.Button buttonDrawColorRoi;
        /// <summary>按需展开或收起矩形数值参数。</summary>
        private System.Windows.Forms.Button buttonRectangleSettings;



        /// <summary>显示本次组合颜色识别的实际前景像素面积。</summary>
        private System.Windows.Forms.Label labelMeasuredArea;

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
            this.buttonRectangleSettings = new System.Windows.Forms.Button();
            this.buttonRectangleSettings.Name = "buttonRectangleSettings";
            this.buttonRectangleSettings.Text = "矩形参数设置";
            this.buttonRectangleSettings.Location = new System.Drawing.Point(18, 242);
            this.buttonRectangleSettings.Size = new System.Drawing.Size(390, 30);
            this.buttonRectangleSettings.Click += new System.EventHandler(this.buttonRectangleSettings_Click);
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
            this.groupBoxColor = new System.Windows.Forms.GroupBox();
            this.labelMinimumArea = new System.Windows.Forms.Label();
            this.numericMinimumArea = new System.Windows.Forms.NumericUpDown();
            this.labelSensitivity = new System.Windows.Forms.Label();
            this.numericSensitivity = new System.Windows.Forms.NumericUpDown();
            this.labelOutputRegionMode = new System.Windows.Forms.Label();
            this.comboBoxOutputRegionMode = new System.Windows.Forms.ComboBox();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.labelMeasuredArea = new System.Windows.Forms.Label();
            this.labelMeasuredArea.Location = new System.Drawing.Point(18, 438);
            this.labelMeasuredArea.Size = new System.Drawing.Size(390, 28);
            this.labelMeasuredArea.Name = "labelMeasuredArea";
            this.labelMeasuredArea.Text = "识别区域面积：未执行";
            this.labelMeasuredArea.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelMeasuredArea.ForeColor = System.Drawing.Color.DarkGreen;
            this.groupBoxInput.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.groupBoxRegion.SuspendLayout();
            this.groupBoxColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMinimumArea)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericSensitivity)).BeginInit();
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
            this.groupBoxRegion.Location = new System.Drawing.Point(18, 278);
            this.groupBoxRegion.Name = "groupBoxRegion";
            this.groupBoxRegion.Size = new System.Drawing.Size(390, 160);
            this.groupBoxRegion.Visible = false;
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
            // groupBoxColor
            // 
            this.groupBoxColor.Controls.Add(this.comboBoxOutputRegionMode);
            this.groupBoxColor.Controls.Add(this.labelOutputRegionMode);
            this.groupBoxColor.Controls.Add(this.labelMinimumArea);
            this.groupBoxColor.Controls.Add(this.numericMinimumArea);
            this.groupBoxColor.Controls.Add(this.labelSensitivity);
            this.groupBoxColor.Controls.Add(this.numericSensitivity);
            this.groupBoxColor.Location = new System.Drawing.Point(18, 278);
            this.groupBoxColor.Name = "groupBoxColor";
            this.groupBoxColor.Size = new System.Drawing.Size(390, 154);
            this.groupBoxColor.TabIndex = 3;
            this.groupBoxColor.TabStop = false;
            this.groupBoxColor.Text = "颜色与区域筛选";
            // 
            // labelMinimumArea
            // 
            this.labelMinimumArea.AutoSize = true;
            this.labelMinimumArea.Location = new System.Drawing.Point(16, 32);
            this.labelMinimumArea.Name = "labelMinimumArea";
            this.labelMinimumArea.Size = new System.Drawing.Size(67, 15);
            this.labelMinimumArea.TabIndex = 0;
            this.labelMinimumArea.Text = "最小面积";
            // 
            // numericMinimumArea
            // 
            this.numericMinimumArea.Location = new System.Drawing.Point(302, 28);
            this.numericMinimumArea.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericMinimumArea.Name = "numericMinimumArea";
            this.numericMinimumArea.Size = new System.Drawing.Size(60, 25);
            this.numericMinimumArea.TabIndex = 1;
            // 
            // 
            // 
            // labelSensitivity
            // 
            this.labelSensitivity.AutoSize = true;
            this.labelSensitivity.Location = new System.Drawing.Point(16, 70);
            this.labelSensitivity.Name = "labelSensitivity";
            this.labelSensitivity.Size = new System.Drawing.Size(67, 15);
            this.labelSensitivity.TabIndex = 2;
            this.labelSensitivity.Text = "灵敏度";
            // 
            // numericSensitivity
            // 
            this.numericSensitivity.Location = new System.Drawing.Point(302, 66);
            this.numericSensitivity.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericSensitivity.Name = "numericSensitivity";
            this.numericSensitivity.Size = new System.Drawing.Size(60, 25);
            this.numericSensitivity.TabIndex = 3;
            this.numericSensitivity.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // 
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
            this.buttonDrawRoi.Text = "绘制搜索ROI";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            // 
            // buttonConfirmRoi
            // 
            this.buttonConfirmRoi.Location = new System.Drawing.Point(522, 594);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonConfirmRoi.TabIndex = 6;
            this.buttonConfirmRoi.Text = "确认搜索ROI";
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
            // NodeParamFormCombinationColor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(962, 646);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.labelMeasuredArea);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonConfirmRoi);
            this.Controls.Add(this.buttonDrawRoi);
            this.Controls.Add(this.showImageControl1);
            this.Controls.Add(this.groupBoxColor);
            this.Controls.Add(this.groupBoxRegion);
            this.Controls.Add(this.buttonRectangleSettings);
            this.Controls.Add(this.groupBoxPositionCorrection);
            this.Controls.Add(this.groupBoxInput);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormCombinationColor";
            this.Text = "组合颜色识别";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxRegion.ResumeLayout(false);
            this.groupBoxRegion.PerformLayout();
            this.groupBoxColor.ResumeLayout(false);
            this.groupBoxColor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMinimumArea)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericSensitivity)).EndInit();

            this.groupBoxColor.Text = "颜色与区域筛选";
            this.labelMinimumArea.Text = "最小面积";
            this.labelSensitivity.Text = "灵敏度";
            this.numericMinimumArea.Maximum = 2147483647;
            this.numericSensitivity.Maximum = 100;
            this.numericSensitivity.Value = 50;
            this.comboColorSpace = new System.Windows.Forms.ComboBox();
            this.comboColorSpace.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboColorSpace.Items.AddRange(new object[] { "HSV", "HSI", "LAB", "RGB" });
            this.comboColorSpace.SelectedIndex = 0;
            this.comboColorSpace.Location = new System.Drawing.Point(104, 28);
            this.comboColorSpace.Size = new System.Drawing.Size(180, 28);
            this.groupBoxColor.Controls.Add(this.comboColorSpace);
            this.buttonLearn = new System.Windows.Forms.Button();
            this.buttonLearn.Text = "学习颜色";
            this.buttonLearn.Name = "buttonLearn";
            this.buttonLearn.Location = new System.Drawing.Point(104, 66);
            this.buttonLearn.Size = new System.Drawing.Size(180, 30);
            this.buttonLearn.Click += this.LearnColor;
            this.groupBoxColor.Controls.Add(this.buttonLearn);

            this.labelMinimumArea.Location = new System.Drawing.Point(16, 70);
            this.numericMinimumArea.Location = new System.Drawing.Point(100, 66);
            this.numericMinimumArea.Size = new System.Drawing.Size(100, 26);
            this.labelSensitivity.Location = new System.Drawing.Point(210, 70);
            this.labelSensitivity.Size = new System.Drawing.Size(85, 24);
            this.comboColorSpace.Size = new System.Drawing.Size(70, 28);
            this.comboColorSpace.Location = new System.Drawing.Point(16, 28);
            this.buttonLearn.Location = new System.Drawing.Point(242, 25);
            this.buttonLearn.Size = new System.Drawing.Size(132, 32);
            this.buttonDrawColorRoi = new System.Windows.Forms.Button();
            this.buttonDrawColorRoi.Name = "buttonDrawColorRoi";
            this.buttonDrawColorRoi.Text = "绘制颜色ROI";
            this.buttonDrawColorRoi.Location = new System.Drawing.Point(94, 25);
            this.buttonDrawColorRoi.Size = new System.Drawing.Size(140, 32);
            this.buttonDrawColorRoi.Click += this.DrawColorRoi;
            this.groupBoxColor.Controls.Add(this.buttonDrawColorRoi);
            this.buttonDrawRoi.Size = new System.Drawing.Size(114, 34);
            this.buttonConfirmRoi.Location = new System.Drawing.Point(544, 594);
            this.buttonConfirmRoi.Size = new System.Drawing.Size(110, 34);
            this.buttonRefresh.Location = new System.Drawing.Point(660, 594);
            this.buttonRefresh.Size = new System.Drawing.Size(88, 34);
            this.buttonRun.Location = new System.Drawing.Point(754, 594);
            this.buttonRun.Size = new System.Drawing.Size(90, 34);
            this.buttonSave.Location = new System.Drawing.Point(850, 594);
            this.buttonSave.Size = new System.Drawing.Size(94, 34);
            this.checkBoxEnableDetectionRegion.Text = "搜索区域（否则全图）";
            this.checkBoxEnableDetectionRegion.AutoSize = false;
            this.checkBoxEnableDetectionRegion.Size = new System.Drawing.Size(210, 26);
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(230, 24);
            this.checkBoxUsePositionCorrection.AutoSize = false;
            this.checkBoxUsePositionCorrection.Size = new System.Drawing.Size(135, 26);
            this.checkBoxUsePositionCorrection.Text = "位置跟随";
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
        /// 颜色和筛选分组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxColor;

        /// <summary>
        /// 最小面积标签。
        /// </summary>
        private System.Windows.Forms.Label labelMinimumArea;

        /// <summary>
        /// 最小面积输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMinimumArea;


        /// <summary>
        /// 灵敏度标签。
        /// </summary>
        private System.Windows.Forms.Label labelSensitivity;

        /// <summary>
        /// 灵敏度输入框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericSensitivity;


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
