namespace TDJS_Vision.Node._4_Measurement.CaliperEllipse
{
    partial class NodeParamFormCaliperEllipse
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

        private void InitializeComponent()
        {
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxGeometry = new System.Windows.Forms.GroupBox();
            this.labelCenterX = new System.Windows.Forms.Label();
            this.textBoxCenterX = new System.Windows.Forms.TextBox();
            this.labelCenterY = new System.Windows.Forms.Label();
            this.textBoxCenterY = new System.Windows.Forms.TextBox();
            this.labelWidth = new System.Windows.Forms.Label();
            this.textBoxWidth = new System.Windows.Forms.TextBox();
            this.labelHeight = new System.Windows.Forms.Label();
            this.textBoxHeight = new System.Windows.Forms.TextBox();
            this.labelAngle = new System.Windows.Forms.Label();
            this.textBoxAngle = new System.Windows.Forms.TextBox();
            this.groupBoxCaliper = new System.Windows.Forms.GroupBox();
            this.labelCaliperWidth = new System.Windows.Forms.Label();
            this.textBoxCaliperWidth = new System.Windows.Forms.TextBox();
            this.labelCaliperHeight = new System.Windows.Forms.Label();
            this.textBoxCaliperHeight = new System.Windows.Forms.TextBox();
            this.labelCount = new System.Windows.Forms.Label();
            this.textBoxCount = new System.Windows.Forms.TextBox();
            this.labelEdgeStrength = new System.Windows.Forms.Label();
            this.textBoxEdgeStrength = new System.Windows.Forms.TextBox();
            this.labelBlurSize = new System.Windows.Forms.Label();
            this.textBoxBlurSize = new System.Windows.Forms.TextBox();
            this.labelPolarity = new System.Windows.Forms.Label();
            this.comboBoxPolarity = new System.Windows.Forms.ComboBox();
            this.labelFindMode = new System.Windows.Forms.Label();
            this.comboBoxFindMode = new System.Windows.Forms.ComboBox();
            this.labelDirection = new System.Windows.Forms.Label();
            this.comboBoxDirection = new System.Windows.Forms.ComboBox();
            this.checkBoxEnableFitValidPointCount = new System.Windows.Forms.CheckBox();
            this.labelFitValidPointCount = new System.Windows.Forms.Label();
            this.textBoxFitValidPointCount = new System.Windows.Forms.TextBox();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.groupBoxGeometry.SuspendLayout();
            this.groupBoxCaliper.SuspendLayout();
            this.SuspendLayout();
            //
            // nodeSubscription1
            //
            this.nodeSubscription1.Location = new System.Drawing.Point(16, 26);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(356, 32);
            this.nodeSubscription1.TabIndex = 0;
            //
            // groupBoxInput
            //
            this.groupBoxInput.Controls.Add(this.nodeSubscription1);
            this.groupBoxInput.Location = new System.Drawing.Point(18, 44);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(390, 74);
            this.groupBoxInput.TabIndex = 1;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入图像";
            //
            // groupBoxPositionCorrection
            //
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(18, 124);
            this.groupBoxPositionCorrection.Name = "groupBoxPositionCorrection";
            this.groupBoxPositionCorrection.Size = new System.Drawing.Size(390, 88);
            this.groupBoxPositionCorrection.TabIndex = 2;
            this.groupBoxPositionCorrection.TabStop = false;
            this.groupBoxPositionCorrection.Text = "位置修正";
            //
            // checkBoxUsePositionCorrection
            //
            this.checkBoxUsePositionCorrection.AutoSize = true;
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(16, 24);
            this.checkBoxUsePositionCorrection.Name = "checkBoxUsePositionCorrection";
            this.checkBoxUsePositionCorrection.Size = new System.Drawing.Size(89, 19);
            this.checkBoxUsePositionCorrection.TabIndex = 0;
            this.checkBoxUsePositionCorrection.Text = "启用修正";
            this.checkBoxUsePositionCorrection.UseVisualStyleBackColor = true;
            this.checkBoxUsePositionCorrection.CheckedChanged += new System.EventHandler(this.checkBoxUsePositionCorrection_CheckedChanged);
            //
            // nodeSubscriptionPositionCorrection
            //
            this.nodeSubscriptionPositionCorrection.Enabled = false;
            this.nodeSubscriptionPositionCorrection.Location = new System.Drawing.Point(16, 48);
            this.nodeSubscriptionPositionCorrection.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionPositionCorrection.Name = "nodeSubscriptionPositionCorrection";
            this.nodeSubscriptionPositionCorrection.Size = new System.Drawing.Size(356, 32);
            this.nodeSubscriptionPositionCorrection.TabIndex = 1;
            // 
            // groupBoxGeometry
            // 
            this.groupBoxGeometry.Controls.Add(this.labelCenterX);
            this.groupBoxGeometry.Controls.Add(this.textBoxCenterX);
            this.groupBoxGeometry.Controls.Add(this.labelCenterY);
            this.groupBoxGeometry.Controls.Add(this.textBoxCenterY);
            this.groupBoxGeometry.Controls.Add(this.labelWidth);
            this.groupBoxGeometry.Controls.Add(this.textBoxWidth);
            this.groupBoxGeometry.Controls.Add(this.labelHeight);
            this.groupBoxGeometry.Controls.Add(this.textBoxHeight);
            this.groupBoxGeometry.Controls.Add(this.labelAngle);
            this.groupBoxGeometry.Controls.Add(this.textBoxAngle);
            this.groupBoxGeometry.Location = new System.Drawing.Point(18, 218);
            this.groupBoxGeometry.Name = "groupBoxGeometry";
            this.groupBoxGeometry.Size = new System.Drawing.Size(390, 154);
            this.groupBoxGeometry.TabIndex = 3;
            this.groupBoxGeometry.TabStop = false;
            this.groupBoxGeometry.Text = "基准椭圆";
            // 
            // labelCenterX
            // 
            this.labelCenterX.AutoSize = true;
            this.labelCenterX.Location = new System.Drawing.Point(16, 30);
            this.labelCenterX.Name = "labelCenterX";
            this.labelCenterX.Size = new System.Drawing.Size(42, 15);
            this.labelCenterX.TabIndex = 0;
            this.labelCenterX.Text = "中心X";
            // 
            // textBoxCenterX
            // 
            this.textBoxCenterX.Location = new System.Drawing.Point(72, 26);
            this.textBoxCenterX.Name = "textBoxCenterX";
            this.textBoxCenterX.Size = new System.Drawing.Size(100, 25);
            this.textBoxCenterX.TabIndex = 1;
            this.textBoxCenterX.Text = "250";
            // 
            // labelCenterY
            // 
            this.labelCenterY.AutoSize = true;
            this.labelCenterY.Location = new System.Drawing.Point(206, 30);
            this.labelCenterY.Name = "labelCenterY";
            this.labelCenterY.Size = new System.Drawing.Size(42, 15);
            this.labelCenterY.TabIndex = 2;
            this.labelCenterY.Text = "中心Y";
            // 
            // textBoxCenterY
            // 
            this.textBoxCenterY.Location = new System.Drawing.Point(262, 26);
            this.textBoxCenterY.Name = "textBoxCenterY";
            this.textBoxCenterY.Size = new System.Drawing.Size(100, 25);
            this.textBoxCenterY.TabIndex = 3;
            this.textBoxCenterY.Text = "250";
            // 
            // labelWidth
            // 
            this.labelWidth.AutoSize = true;
            this.labelWidth.Location = new System.Drawing.Point(16, 72);
            this.labelWidth.Name = "labelWidth";
            this.labelWidth.Size = new System.Drawing.Size(37, 15);
            this.labelWidth.TabIndex = 4;
            this.labelWidth.Text = "宽度";
            // 
            // textBoxWidth
            // 
            this.textBoxWidth.Location = new System.Drawing.Point(72, 68);
            this.textBoxWidth.Name = "textBoxWidth";
            this.textBoxWidth.Size = new System.Drawing.Size(100, 25);
            this.textBoxWidth.TabIndex = 5;
            this.textBoxWidth.Text = "220";
            // 
            // labelHeight
            // 
            this.labelHeight.AutoSize = true;
            this.labelHeight.Location = new System.Drawing.Point(206, 72);
            this.labelHeight.Name = "labelHeight";
            this.labelHeight.Size = new System.Drawing.Size(37, 15);
            this.labelHeight.TabIndex = 6;
            this.labelHeight.Text = "高度";
            // 
            // textBoxHeight
            // 
            this.textBoxHeight.Location = new System.Drawing.Point(262, 68);
            this.textBoxHeight.Name = "textBoxHeight";
            this.textBoxHeight.Size = new System.Drawing.Size(100, 25);
            this.textBoxHeight.TabIndex = 7;
            this.textBoxHeight.Text = "120";
            // 
            // labelAngle
            // 
            this.labelAngle.AutoSize = true;
            this.labelAngle.Location = new System.Drawing.Point(16, 114);
            this.labelAngle.Name = "labelAngle";
            this.labelAngle.Size = new System.Drawing.Size(37, 15);
            this.labelAngle.TabIndex = 8;
            this.labelAngle.Text = "角度";
            // 
            // textBoxAngle
            // 
            this.textBoxAngle.Location = new System.Drawing.Point(72, 110);
            this.textBoxAngle.Name = "textBoxAngle";
            this.textBoxAngle.Size = new System.Drawing.Size(100, 25);
            this.textBoxAngle.TabIndex = 9;
            this.textBoxAngle.Text = "0";
            // 
            // groupBoxCaliper
            // 
            this.groupBoxCaliper.Controls.Add(this.labelCaliperWidth);
            this.groupBoxCaliper.Controls.Add(this.textBoxCaliperWidth);
            this.groupBoxCaliper.Controls.Add(this.labelCaliperHeight);
            this.groupBoxCaliper.Controls.Add(this.textBoxCaliperHeight);
            this.groupBoxCaliper.Controls.Add(this.labelCount);
            this.groupBoxCaliper.Controls.Add(this.textBoxCount);
            this.groupBoxCaliper.Controls.Add(this.labelEdgeStrength);
            this.groupBoxCaliper.Controls.Add(this.textBoxEdgeStrength);
            this.groupBoxCaliper.Controls.Add(this.labelBlurSize);
            this.groupBoxCaliper.Controls.Add(this.textBoxBlurSize);
            this.groupBoxCaliper.Controls.Add(this.labelPolarity);
            this.groupBoxCaliper.Controls.Add(this.comboBoxPolarity);
            this.groupBoxCaliper.Controls.Add(this.labelFindMode);
            this.groupBoxCaliper.Controls.Add(this.comboBoxFindMode);
            this.groupBoxCaliper.Controls.Add(this.labelDirection);
            this.groupBoxCaliper.Controls.Add(this.comboBoxDirection);
            this.groupBoxCaliper.Controls.Add(this.checkBoxEnableFitValidPointCount);
            this.groupBoxCaliper.Controls.Add(this.labelFitValidPointCount);
            this.groupBoxCaliper.Controls.Add(this.textBoxFitValidPointCount);
            this.groupBoxCaliper.Location = new System.Drawing.Point(18, 378);
            this.groupBoxCaliper.Name = "groupBoxCaliper";
            this.groupBoxCaliper.Size = new System.Drawing.Size(390, 246);
            this.groupBoxCaliper.TabIndex = 4;
            this.groupBoxCaliper.TabStop = false;
            this.groupBoxCaliper.Text = "卡尺参数";
            // 
            // labelCaliperWidth
            // 
            this.labelCaliperWidth.AutoSize = true;
            this.labelCaliperWidth.Location = new System.Drawing.Point(16, 32);
            this.labelCaliperWidth.Name = "labelCaliperWidth";
            this.labelCaliperWidth.Size = new System.Drawing.Size(37, 15);
            this.labelCaliperWidth.TabIndex = 0;
            this.labelCaliperWidth.Text = "宽度";
            // 
            // textBoxCaliperWidth
            // 
            this.textBoxCaliperWidth.Location = new System.Drawing.Point(72, 28);
            this.textBoxCaliperWidth.Name = "textBoxCaliperWidth";
            this.textBoxCaliperWidth.Size = new System.Drawing.Size(100, 25);
            this.textBoxCaliperWidth.TabIndex = 1;
            this.textBoxCaliperWidth.Text = "10";
            // 
            // labelCaliperHeight
            // 
            this.labelCaliperHeight.AutoSize = true;
            this.labelCaliperHeight.Location = new System.Drawing.Point(206, 32);
            this.labelCaliperHeight.Name = "labelCaliperHeight";
            this.labelCaliperHeight.Size = new System.Drawing.Size(37, 15);
            this.labelCaliperHeight.TabIndex = 2;
            this.labelCaliperHeight.Text = "高度";
            // 
            // textBoxCaliperHeight
            // 
            this.textBoxCaliperHeight.Location = new System.Drawing.Point(262, 28);
            this.textBoxCaliperHeight.Name = "textBoxCaliperHeight";
            this.textBoxCaliperHeight.Size = new System.Drawing.Size(100, 25);
            this.textBoxCaliperHeight.TabIndex = 3;
            this.textBoxCaliperHeight.Text = "60";
            // 
            // labelCount
            // 
            this.labelCount.AutoSize = true;
            this.labelCount.Location = new System.Drawing.Point(16, 74);
            this.labelCount.Name = "labelCount";
            this.labelCount.Size = new System.Drawing.Size(37, 15);
            this.labelCount.TabIndex = 4;
            this.labelCount.Text = "数量";
            // 
            // textBoxCount
            // 
            this.textBoxCount.Location = new System.Drawing.Point(72, 70);
            this.textBoxCount.Name = "textBoxCount";
            this.textBoxCount.Size = new System.Drawing.Size(100, 25);
            this.textBoxCount.TabIndex = 5;
            this.textBoxCount.Text = "30";
            // 
            // labelEdgeStrength
            // 
            this.labelEdgeStrength.AutoSize = true;
            this.labelEdgeStrength.Location = new System.Drawing.Point(206, 74);
            this.labelEdgeStrength.Name = "labelEdgeStrength";
            this.labelEdgeStrength.Size = new System.Drawing.Size(37, 15);
            this.labelEdgeStrength.TabIndex = 6;
            this.labelEdgeStrength.Text = "阈值";
            // 
            // textBoxEdgeStrength
            // 
            this.textBoxEdgeStrength.Location = new System.Drawing.Point(262, 70);
            this.textBoxEdgeStrength.Name = "textBoxEdgeStrength";
            this.textBoxEdgeStrength.Size = new System.Drawing.Size(100, 25);
            this.textBoxEdgeStrength.TabIndex = 7;
            this.textBoxEdgeStrength.Text = "20";
            // 
            // labelBlurSize
            // 
            this.labelBlurSize.AutoSize = true;
            this.labelBlurSize.Location = new System.Drawing.Point(16, 116);
            this.labelBlurSize.Name = "labelBlurSize";
            this.labelBlurSize.Size = new System.Drawing.Size(37, 15);
            this.labelBlurSize.TabIndex = 8;
            this.labelBlurSize.Text = "平滑";
            // 
            // textBoxBlurSize
            // 
            this.textBoxBlurSize.Location = new System.Drawing.Point(72, 112);
            this.textBoxBlurSize.Name = "textBoxBlurSize";
            this.textBoxBlurSize.Size = new System.Drawing.Size(100, 25);
            this.textBoxBlurSize.TabIndex = 9;
            this.textBoxBlurSize.Text = "3";
            // 
            // labelPolarity
            // 
            this.labelPolarity.AutoSize = true;
            this.labelPolarity.Location = new System.Drawing.Point(206, 116);
            this.labelPolarity.Name = "labelPolarity";
            this.labelPolarity.Size = new System.Drawing.Size(37, 15);
            this.labelPolarity.TabIndex = 10;
            this.labelPolarity.Text = "极性";
            // 
            // comboBoxPolarity
            // 
            this.comboBoxPolarity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPolarity.FormattingEnabled = true;
            this.comboBoxPolarity.Location = new System.Drawing.Point(262, 112);
            this.comboBoxPolarity.Name = "comboBoxPolarity";
            this.comboBoxPolarity.Size = new System.Drawing.Size(100, 23);
            this.comboBoxPolarity.TabIndex = 11;
            // 
            // labelFindMode
            // 
            this.labelFindMode.AutoSize = true;
            this.labelFindMode.Location = new System.Drawing.Point(16, 158);
            this.labelFindMode.Name = "labelFindMode";
            this.labelFindMode.Size = new System.Drawing.Size(37, 15);
            this.labelFindMode.TabIndex = 12;
            this.labelFindMode.Text = "模式";
            // 
            // comboBoxFindMode
            // 
            this.comboBoxFindMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFindMode.FormattingEnabled = true;
            this.comboBoxFindMode.Location = new System.Drawing.Point(72, 154);
            this.comboBoxFindMode.Name = "comboBoxFindMode";
            this.comboBoxFindMode.Size = new System.Drawing.Size(100, 23);
            this.comboBoxFindMode.TabIndex = 13;
            // 
            // labelDirection
            // 
            this.labelDirection.AutoSize = true;
            this.labelDirection.Location = new System.Drawing.Point(206, 158);
            this.labelDirection.Name = "labelDirection";
            this.labelDirection.Size = new System.Drawing.Size(37, 15);
            this.labelDirection.TabIndex = 14;
            this.labelDirection.Text = "方向";
            // 
            // comboBoxDirection
            // 
            this.comboBoxDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDirection.FormattingEnabled = true;
            this.comboBoxDirection.Location = new System.Drawing.Point(262, 154);
            this.comboBoxDirection.Name = "comboBoxDirection";
            this.comboBoxDirection.Size = new System.Drawing.Size(100, 23);
            this.comboBoxDirection.TabIndex = 15;
            //
            // checkBoxEnableFitValidPointCount
            //
            this.checkBoxEnableFitValidPointCount.AutoSize = true;
            this.checkBoxEnableFitValidPointCount.Location = new System.Drawing.Point(16, 198);
            this.checkBoxEnableFitValidPointCount.Name = "checkBoxEnableFitValidPointCount";
            this.checkBoxEnableFitValidPointCount.Size = new System.Drawing.Size(149, 19);
            this.checkBoxEnableFitValidPointCount.TabIndex = 16;
            this.checkBoxEnableFitValidPointCount.Text = "启用拟合有效点数";
            this.checkBoxEnableFitValidPointCount.UseVisualStyleBackColor = true;
            this.checkBoxEnableFitValidPointCount.CheckedChanged += new System.EventHandler(this.checkBoxEnableFitValidPointCount_CheckedChanged);
            //
            // labelFitValidPointCount
            //
            this.labelFitValidPointCount.AutoSize = true;
            this.labelFitValidPointCount.Enabled = false;
            this.labelFitValidPointCount.Location = new System.Drawing.Point(206, 200);
            this.labelFitValidPointCount.Name = "labelFitValidPointCount";
            this.labelFitValidPointCount.Size = new System.Drawing.Size(97, 15);
            this.labelFitValidPointCount.TabIndex = 17;
            this.labelFitValidPointCount.Text = "拟合有效点数";
            //
            // textBoxFitValidPointCount
            //
            this.textBoxFitValidPointCount.Enabled = false;
            this.textBoxFitValidPointCount.Location = new System.Drawing.Point(306, 196);
            this.textBoxFitValidPointCount.Name = "textBoxFitValidPointCount";
            this.textBoxFitValidPointCount.Size = new System.Drawing.Size(56, 25);
            this.textBoxFitValidPointCount.TabIndex = 18;
            this.textBoxFitValidPointCount.Text = "10";
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
            this.showImageControl1.TabIndex = 5;
            //
            // buttonDrawRoi
            //
            this.buttonDrawRoi.Location = new System.Drawing.Point(424, 594);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonDrawRoi.TabIndex = 6;
            this.buttonDrawRoi.Text = "绘制ROI";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            //
            // buttonConfirmRoi
            //
            this.buttonConfirmRoi.Location = new System.Drawing.Point(522, 594);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonConfirmRoi.TabIndex = 7;
            this.buttonConfirmRoi.Text = "确认ROI";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            //
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(620, 594);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(98, 34);
            this.buttonRefresh.TabIndex = 8;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(724, 594);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(100, 34);
            this.buttonRun.TabIndex = 9;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(844, 594);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(100, 34);
            this.buttonSave.TabIndex = 10;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCaliperEllipse
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
            this.Controls.Add(this.groupBoxCaliper);
            this.Controls.Add(this.groupBoxGeometry);
            this.Controls.Add(this.groupBoxPositionCorrection);
            this.Controls.Add(this.groupBoxInput);
            this.Name = "NodeParamFormCaliperEllipse";
            this.Text = "卡尺找椭圆";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxGeometry.ResumeLayout(false);
            this.groupBoxGeometry.PerformLayout();
            this.groupBoxCaliper.ResumeLayout(false);
            this.groupBoxCaliper.PerformLayout();
            this.ResumeLayout(false);
        }

        private TDJS_Vision.Node.NodeSubscription nodeSubscription1;
        private System.Windows.Forms.GroupBox groupBoxInput;
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;
        private System.Windows.Forms.GroupBox groupBoxGeometry;
        private System.Windows.Forms.Label labelCenterX;
        private System.Windows.Forms.TextBox textBoxCenterX;
        private System.Windows.Forms.Label labelCenterY;
        private System.Windows.Forms.TextBox textBoxCenterY;
        private System.Windows.Forms.Label labelWidth;
        private System.Windows.Forms.TextBox textBoxWidth;
        private System.Windows.Forms.Label labelHeight;
        private System.Windows.Forms.TextBox textBoxHeight;
        private System.Windows.Forms.Label labelAngle;
        private System.Windows.Forms.TextBox textBoxAngle;
        private System.Windows.Forms.GroupBox groupBoxCaliper;
        private System.Windows.Forms.Label labelCaliperWidth;
        private System.Windows.Forms.TextBox textBoxCaliperWidth;
        private System.Windows.Forms.Label labelCaliperHeight;
        private System.Windows.Forms.TextBox textBoxCaliperHeight;
        private System.Windows.Forms.Label labelCount;
        private System.Windows.Forms.TextBox textBoxCount;
        private System.Windows.Forms.Label labelEdgeStrength;
        private System.Windows.Forms.TextBox textBoxEdgeStrength;
        private System.Windows.Forms.Label labelBlurSize;
        private System.Windows.Forms.TextBox textBoxBlurSize;
        private System.Windows.Forms.Label labelPolarity;
        private System.Windows.Forms.ComboBox comboBoxPolarity;
        private System.Windows.Forms.Label labelFindMode;
        private System.Windows.Forms.ComboBox comboBoxFindMode;
        private System.Windows.Forms.Label labelDirection;
        private System.Windows.Forms.ComboBox comboBoxDirection;
        private System.Windows.Forms.CheckBox checkBoxEnableFitValidPointCount;
        private System.Windows.Forms.Label labelFitValidPointCount;
        private System.Windows.Forms.TextBox textBoxFitValidPointCount;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
        private System.Windows.Forms.Button buttonDrawRoi;
        private System.Windows.Forms.Button buttonConfirmRoi;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
    }
}
