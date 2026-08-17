namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    partial class NodeParamFormCaliperLine
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
            this.labelStartX = new System.Windows.Forms.Label();
            this.textBoxStartX = new System.Windows.Forms.TextBox();
            this.labelStartY = new System.Windows.Forms.Label();
            this.textBoxStartY = new System.Windows.Forms.TextBox();
            this.labelEndX = new System.Windows.Forms.Label();
            this.textBoxEndX = new System.Windows.Forms.TextBox();
            this.labelEndY = new System.Windows.Forms.Label();
            this.textBoxEndY = new System.Windows.Forms.TextBox();
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
            this.labelSamplingMode = new System.Windows.Forms.Label();
            this.comboBoxSamplingMode = new System.Windows.Forms.ComboBox();
            this.labelMeasureMode = new System.Windows.Forms.Label();
            this.comboBoxMeasureMode = new System.Windows.Forms.ComboBox();
            this.groupBoxQuality = new System.Windows.Forms.GroupBox();
            this.checkBoxEnableQualityValidation = new System.Windows.Forms.CheckBox();
            this.labelMinimumValidPointRatio = new System.Windows.Forms.Label();
            this.textBoxMinimumValidPointRatio = new System.Windows.Forms.TextBox();
            this.labelMaximumAverageResidual = new System.Windows.Forms.Label();
            this.textBoxMaximumAverageResidual = new System.Windows.Forms.TextBox();
            this.labelMaximumResidual = new System.Windows.Forms.Label();
            this.textBoxMaximumResidual = new System.Windows.Forms.TextBox();
            this.labelMinimumCoverageRatio = new System.Windows.Forms.Label();
            this.textBoxMinimumCoverageRatio = new System.Windows.Forms.TextBox();
            this.labelMaximumAngleDeviation = new System.Windows.Forms.Label();
            this.textBoxMaximumAngleDeviation = new System.Windows.Forms.TextBox();
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
            this.groupBoxQuality.SuspendLayout();
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
            this.groupBoxGeometry.Controls.Add(this.labelStartX);
            this.groupBoxGeometry.Controls.Add(this.textBoxStartX);
            this.groupBoxGeometry.Controls.Add(this.labelStartY);
            this.groupBoxGeometry.Controls.Add(this.textBoxStartY);
            this.groupBoxGeometry.Controls.Add(this.labelEndX);
            this.groupBoxGeometry.Controls.Add(this.textBoxEndX);
            this.groupBoxGeometry.Controls.Add(this.labelEndY);
            this.groupBoxGeometry.Controls.Add(this.textBoxEndY);
            this.groupBoxGeometry.Location = new System.Drawing.Point(18, 218);
            this.groupBoxGeometry.Name = "groupBoxGeometry";
            this.groupBoxGeometry.Size = new System.Drawing.Size(390, 112);
            this.groupBoxGeometry.TabIndex = 3;
            this.groupBoxGeometry.TabStop = false;
            this.groupBoxGeometry.Text = "基准线";
            // 
            // labelStartX
            // 
            this.labelStartX.AutoSize = true;
            this.labelStartX.Location = new System.Drawing.Point(16, 30);
            this.labelStartX.Name = "labelStartX";
            this.labelStartX.Size = new System.Drawing.Size(42, 15);
            this.labelStartX.TabIndex = 0;
            this.labelStartX.Text = "起点X";
            // 
            // textBoxStartX
            // 
            this.textBoxStartX.Location = new System.Drawing.Point(72, 26);
            this.textBoxStartX.Name = "textBoxStartX";
            this.textBoxStartX.Size = new System.Drawing.Size(100, 25);
            this.textBoxStartX.TabIndex = 1;
            this.textBoxStartX.Text = "100";
            // 
            // labelStartY
            // 
            this.labelStartY.AutoSize = true;
            this.labelStartY.Location = new System.Drawing.Point(206, 30);
            this.labelStartY.Name = "labelStartY";
            this.labelStartY.Size = new System.Drawing.Size(42, 15);
            this.labelStartY.TabIndex = 2;
            this.labelStartY.Text = "起点Y";
            // 
            // textBoxStartY
            // 
            this.textBoxStartY.Location = new System.Drawing.Point(262, 26);
            this.textBoxStartY.Name = "textBoxStartY";
            this.textBoxStartY.Size = new System.Drawing.Size(100, 25);
            this.textBoxStartY.TabIndex = 3;
            this.textBoxStartY.Text = "100";
            // 
            // labelEndX
            // 
            this.labelEndX.AutoSize = true;
            this.labelEndX.Location = new System.Drawing.Point(16, 72);
            this.labelEndX.Name = "labelEndX";
            this.labelEndX.Size = new System.Drawing.Size(42, 15);
            this.labelEndX.TabIndex = 4;
            this.labelEndX.Text = "终点X";
            // 
            // textBoxEndX
            // 
            this.textBoxEndX.Location = new System.Drawing.Point(72, 68);
            this.textBoxEndX.Name = "textBoxEndX";
            this.textBoxEndX.Size = new System.Drawing.Size(100, 25);
            this.textBoxEndX.TabIndex = 5;
            this.textBoxEndX.Text = "400";
            // 
            // labelEndY
            // 
            this.labelEndY.AutoSize = true;
            this.labelEndY.Location = new System.Drawing.Point(206, 72);
            this.labelEndY.Name = "labelEndY";
            this.labelEndY.Size = new System.Drawing.Size(42, 15);
            this.labelEndY.TabIndex = 6;
            this.labelEndY.Text = "终点Y";
            // 
            // textBoxEndY
            // 
            this.textBoxEndY.Location = new System.Drawing.Point(262, 68);
            this.textBoxEndY.Name = "textBoxEndY";
            this.textBoxEndY.Size = new System.Drawing.Size(100, 25);
            this.textBoxEndY.TabIndex = 7;
            this.textBoxEndY.Text = "100";
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
            this.groupBoxCaliper.Controls.Add(this.labelSamplingMode);
            this.groupBoxCaliper.Controls.Add(this.comboBoxSamplingMode);
            this.groupBoxCaliper.Controls.Add(this.labelMeasureMode);
            this.groupBoxCaliper.Controls.Add(this.comboBoxMeasureMode);
            this.groupBoxCaliper.Location = new System.Drawing.Point(18, 336);
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
            this.textBoxCaliperWidth.Text = "20";
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
            this.textBoxCaliperHeight.Text = "120";
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
            this.textBoxCount.Text = "15";
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
            // labelSamplingMode
            //
            this.labelSamplingMode.AutoSize = true;
            this.labelSamplingMode.Location = new System.Drawing.Point(16, 200);
            this.labelSamplingMode.Name = "labelSamplingMode";
            this.labelSamplingMode.Size = new System.Drawing.Size(67, 15);
            this.labelSamplingMode.TabIndex = 16;
            this.labelSamplingMode.Text = "采样模式";
            //
            // comboBoxSamplingMode
            //
            this.comboBoxSamplingMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSamplingMode.FormattingEnabled = true;
            this.comboBoxSamplingMode.Location = new System.Drawing.Point(88, 196);
            this.comboBoxSamplingMode.Name = "comboBoxSamplingMode";
            this.comboBoxSamplingMode.Size = new System.Drawing.Size(120, 23);
            this.comboBoxSamplingMode.TabIndex = 17;
            //
            // labelMeasureMode
            //
            this.labelMeasureMode.AutoSize = true;
            this.labelMeasureMode.Location = new System.Drawing.Point(220, 200);
            this.labelMeasureMode.Name = "labelMeasureMode";
            this.labelMeasureMode.Size = new System.Drawing.Size(67, 15);
            this.labelMeasureMode.TabIndex = 18;
            this.labelMeasureMode.Text = "测量精度";
            //
            // comboBoxMeasureMode
            //
            this.comboBoxMeasureMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMeasureMode.FormattingEnabled = true;
            this.comboBoxMeasureMode.Location = new System.Drawing.Point(292, 196);
            this.comboBoxMeasureMode.Name = "comboBoxMeasureMode";
            this.comboBoxMeasureMode.Size = new System.Drawing.Size(70, 23);
            this.comboBoxMeasureMode.TabIndex = 19;
            //
            // groupBoxQuality
            //
            this.groupBoxQuality.Controls.Add(this.checkBoxEnableQualityValidation);
            this.groupBoxQuality.Controls.Add(this.labelMinimumValidPointRatio);
            this.groupBoxQuality.Controls.Add(this.textBoxMinimumValidPointRatio);
            this.groupBoxQuality.Controls.Add(this.labelMaximumAverageResidual);
            this.groupBoxQuality.Controls.Add(this.textBoxMaximumAverageResidual);
            this.groupBoxQuality.Controls.Add(this.labelMaximumResidual);
            this.groupBoxQuality.Controls.Add(this.textBoxMaximumResidual);
            this.groupBoxQuality.Controls.Add(this.labelMinimumCoverageRatio);
            this.groupBoxQuality.Controls.Add(this.textBoxMinimumCoverageRatio);
            this.groupBoxQuality.Controls.Add(this.labelMaximumAngleDeviation);
            this.groupBoxQuality.Controls.Add(this.textBoxMaximumAngleDeviation);
            this.groupBoxQuality.Location = new System.Drawing.Point(18, 588);
            this.groupBoxQuality.Name = "groupBoxQuality";
            this.groupBoxQuality.Size = new System.Drawing.Size(390, 204);
            this.groupBoxQuality.TabIndex = 5;
            this.groupBoxQuality.TabStop = false;
            this.groupBoxQuality.Text = "拟合质量判定";
            //
            // checkBoxEnableQualityValidation
            //
            this.checkBoxEnableQualityValidation.AutoSize = true;
            this.checkBoxEnableQualityValidation.Checked = true;
            this.checkBoxEnableQualityValidation.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxEnableQualityValidation.Location = new System.Drawing.Point(16, 26);
            this.checkBoxEnableQualityValidation.Name = "checkBoxEnableQualityValidation";
            this.checkBoxEnableQualityValidation.Size = new System.Drawing.Size(119, 19);
            this.checkBoxEnableQualityValidation.TabIndex = 0;
            this.checkBoxEnableQualityValidation.Text = "启用质量判定";
            this.checkBoxEnableQualityValidation.UseVisualStyleBackColor = true;
            //
            // labelMinimumValidPointRatio
            //
            this.labelMinimumValidPointRatio.AutoSize = true;
            this.labelMinimumValidPointRatio.Location = new System.Drawing.Point(16, 68);
            this.labelMinimumValidPointRatio.Name = "labelMinimumValidPointRatio";
            this.labelMinimumValidPointRatio.Size = new System.Drawing.Size(82, 15);
            this.labelMinimumValidPointRatio.TabIndex = 1;
            this.labelMinimumValidPointRatio.Text = "有效点比例";
            //
            // textBoxMinimumValidPointRatio
            //
            this.textBoxMinimumValidPointRatio.Location = new System.Drawing.Point(112, 64);
            this.textBoxMinimumValidPointRatio.Name = "textBoxMinimumValidPointRatio";
            this.textBoxMinimumValidPointRatio.Size = new System.Drawing.Size(60, 25);
            this.textBoxMinimumValidPointRatio.TabIndex = 2;
            this.textBoxMinimumValidPointRatio.Text = "0.8";
            //
            // labelMinimumCoverageRatio
            //
            this.labelMinimumCoverageRatio.AutoSize = true;
            this.labelMinimumCoverageRatio.Location = new System.Drawing.Point(206, 68);
            this.labelMinimumCoverageRatio.Name = "labelMinimumCoverageRatio";
            this.labelMinimumCoverageRatio.Size = new System.Drawing.Size(67, 15);
            this.labelMinimumCoverageRatio.TabIndex = 3;
            this.labelMinimumCoverageRatio.Text = "最低覆盖率";
            //
            // textBoxMinimumCoverageRatio
            //
            this.textBoxMinimumCoverageRatio.Location = new System.Drawing.Point(302, 64);
            this.textBoxMinimumCoverageRatio.Name = "textBoxMinimumCoverageRatio";
            this.textBoxMinimumCoverageRatio.Size = new System.Drawing.Size(60, 25);
            this.textBoxMinimumCoverageRatio.TabIndex = 4;
            this.textBoxMinimumCoverageRatio.Text = "0.75";
            //
            // labelMaximumAverageResidual
            //
            this.labelMaximumAverageResidual.AutoSize = true;
            this.labelMaximumAverageResidual.Location = new System.Drawing.Point(16, 110);
            this.labelMaximumAverageResidual.Name = "labelMaximumAverageResidual";
            this.labelMaximumAverageResidual.Size = new System.Drawing.Size(82, 15);
            this.labelMaximumAverageResidual.TabIndex = 5;
            this.labelMaximumAverageResidual.Text = "平均残差上限";
            //
            // textBoxMaximumAverageResidual
            //
            this.textBoxMaximumAverageResidual.Location = new System.Drawing.Point(112, 106);
            this.textBoxMaximumAverageResidual.Name = "textBoxMaximumAverageResidual";
            this.textBoxMaximumAverageResidual.Size = new System.Drawing.Size(60, 25);
            this.textBoxMaximumAverageResidual.TabIndex = 6;
            this.textBoxMaximumAverageResidual.Text = "1.5";
            //
            // labelMaximumResidual
            //
            this.labelMaximumResidual.AutoSize = true;
            this.labelMaximumResidual.Location = new System.Drawing.Point(206, 110);
            this.labelMaximumResidual.Name = "labelMaximumResidual";
            this.labelMaximumResidual.Size = new System.Drawing.Size(82, 15);
            this.labelMaximumResidual.TabIndex = 7;
            this.labelMaximumResidual.Text = "最大残差上限";
            //
            // textBoxMaximumResidual
            //
            this.textBoxMaximumResidual.Location = new System.Drawing.Point(302, 106);
            this.textBoxMaximumResidual.Name = "textBoxMaximumResidual";
            this.textBoxMaximumResidual.Size = new System.Drawing.Size(60, 25);
            this.textBoxMaximumResidual.TabIndex = 8;
            this.textBoxMaximumResidual.Text = "3.5";
            //
            // labelMaximumAngleDeviation
            //
            this.labelMaximumAngleDeviation.AutoSize = true;
            this.labelMaximumAngleDeviation.Location = new System.Drawing.Point(16, 152);
            this.labelMaximumAngleDeviation.Name = "labelMaximumAngleDeviation";
            this.labelMaximumAngleDeviation.Size = new System.Drawing.Size(82, 15);
            this.labelMaximumAngleDeviation.TabIndex = 9;
            this.labelMaximumAngleDeviation.Text = "方向偏差上限";
            //
            // textBoxMaximumAngleDeviation
            //
            this.textBoxMaximumAngleDeviation.Location = new System.Drawing.Point(112, 148);
            this.textBoxMaximumAngleDeviation.Name = "textBoxMaximumAngleDeviation";
            this.textBoxMaximumAngleDeviation.Size = new System.Drawing.Size(60, 25);
            this.textBoxMaximumAngleDeviation.TabIndex = 10;
            this.textBoxMaximumAngleDeviation.Text = "5";
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
            this.showImageControl1.Size = new System.Drawing.Size(520, 742);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 6;
            //
            // buttonDrawRoi
            //
            this.buttonDrawRoi.Location = new System.Drawing.Point(424, 804);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonDrawRoi.TabIndex = 7;
            this.buttonDrawRoi.Text = "绘制ROI";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            //
            // buttonConfirmRoi
            //
            this.buttonConfirmRoi.Location = new System.Drawing.Point(522, 804);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(92, 34);
            this.buttonConfirmRoi.TabIndex = 8;
            this.buttonConfirmRoi.Text = "确认ROI";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            //
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(620, 804);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(98, 34);
            this.buttonRefresh.TabIndex = 9;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(724, 804);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(100, 34);
            this.buttonRun.TabIndex = 10;
            this.buttonRun.Text = "执行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(844, 804);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(100, 34);
            this.buttonSave.TabIndex = 11;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCaliperLine
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(962, 856);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonConfirmRoi);
            this.Controls.Add(this.buttonDrawRoi);
            this.Controls.Add(this.showImageControl1);
            this.Controls.Add(this.groupBoxQuality);
            this.Controls.Add(this.groupBoxCaliper);
            this.Controls.Add(this.groupBoxGeometry);
            this.Controls.Add(this.groupBoxPositionCorrection);
            this.Controls.Add(this.groupBoxInput);
            this.Name = "NodeParamFormCaliperLine";
            this.Text = "卡尺找线";
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxGeometry.ResumeLayout(false);
            this.groupBoxGeometry.PerformLayout();
            this.groupBoxCaliper.ResumeLayout(false);
            this.groupBoxCaliper.PerformLayout();
            this.groupBoxQuality.ResumeLayout(false);
            this.groupBoxQuality.PerformLayout();
            this.ResumeLayout(false);
        }

        private TDJS_Vision.Node.NodeSubscription nodeSubscription1;
        private System.Windows.Forms.GroupBox groupBoxInput;
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;
        private System.Windows.Forms.GroupBox groupBoxGeometry;
        private System.Windows.Forms.Label labelStartX;
        private System.Windows.Forms.TextBox textBoxStartX;
        private System.Windows.Forms.Label labelStartY;
        private System.Windows.Forms.TextBox textBoxStartY;
        private System.Windows.Forms.Label labelEndX;
        private System.Windows.Forms.TextBox textBoxEndX;
        private System.Windows.Forms.Label labelEndY;
        private System.Windows.Forms.TextBox textBoxEndY;
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
        private System.Windows.Forms.Label labelSamplingMode;
        private System.Windows.Forms.ComboBox comboBoxSamplingMode;
        private System.Windows.Forms.Label labelMeasureMode;
        private System.Windows.Forms.ComboBox comboBoxMeasureMode;
        private System.Windows.Forms.GroupBox groupBoxQuality;
        private System.Windows.Forms.CheckBox checkBoxEnableQualityValidation;
        private System.Windows.Forms.Label labelMinimumValidPointRatio;
        private System.Windows.Forms.TextBox textBoxMinimumValidPointRatio;
        private System.Windows.Forms.Label labelMaximumAverageResidual;
        private System.Windows.Forms.TextBox textBoxMaximumAverageResidual;
        private System.Windows.Forms.Label labelMaximumResidual;
        private System.Windows.Forms.TextBox textBoxMaximumResidual;
        private System.Windows.Forms.Label labelMinimumCoverageRatio;
        private System.Windows.Forms.TextBox textBoxMinimumCoverageRatio;
        private System.Windows.Forms.Label labelMaximumAngleDeviation;
        private System.Windows.Forms.TextBox textBoxMaximumAngleDeviation;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
        private System.Windows.Forms.Button buttonDrawRoi;
        private System.Windows.Forms.Button buttonConfirmRoi;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
    }
}
