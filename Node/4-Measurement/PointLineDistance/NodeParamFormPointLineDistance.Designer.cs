namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    partial class NodeParamFormPointLineDistance
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
            this.components = new System.ComponentModel.Container();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxSourceMode = new System.Windows.Forms.GroupBox();
            this.radioButtonDraw = new System.Windows.Forms.RadioButton();
            this.radioButtonSubscribe = new System.Windows.Forms.RadioButton();
            this.groupBoxSubscribed = new System.Windows.Forms.GroupBox();
            this.labelPointSource = new System.Windows.Forms.Label();
            this.nodeSubscriptionPoint = new TDJS_Vision.Node.NodeSubscription();
            this.comboBoxPointRole = new System.Windows.Forms.ComboBox();
            this.labelLineSource = new System.Windows.Forms.Label();
            this.nodeSubscriptionLine = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxGeometry = new System.Windows.Forms.GroupBox();
            this.labelPointCenterX = new System.Windows.Forms.Label();
            this.textBoxPointCenterX = new System.Windows.Forms.TextBox();
            this.labelPointCenterY = new System.Windows.Forms.Label();
            this.textBoxPointCenterY = new System.Windows.Forms.TextBox();
            this.labelPointRadius = new System.Windows.Forms.Label();
            this.textBoxPointRadius = new System.Windows.Forms.TextBox();
            this.labelLineStartX = new System.Windows.Forms.Label();
            this.textBoxLineStartX = new System.Windows.Forms.TextBox();
            this.labelLineStartY = new System.Windows.Forms.Label();
            this.textBoxLineStartY = new System.Windows.Forms.TextBox();
            this.labelLineEndX = new System.Windows.Forms.Label();
            this.textBoxLineEndX = new System.Windows.Forms.TextBox();
            this.labelLineEndY = new System.Windows.Forms.Label();
            this.textBoxLineEndY = new System.Windows.Forms.TextBox();
            this.tabPageRun = new System.Windows.Forms.TabPage();
            this.groupBoxRunParams = new System.Windows.Forms.GroupBox();
            this.labelMeasureMode = new System.Windows.Forms.Label();
            this.comboBoxMeasureMode = new System.Windows.Forms.ComboBox();
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
            this.labelRunRoiTarget = new System.Windows.Forms.Label();
            this.comboBoxRunRoiTarget = new System.Windows.Forms.ComboBox();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tabControlMain.SuspendLayout();
            this.tabPageBasic.SuspendLayout();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxSourceMode.SuspendLayout();
            this.groupBoxSubscribed.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.groupBoxGeometry.SuspendLayout();
            this.tabPageRun.SuspendLayout();
            this.groupBoxRunParams.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageBasic);
            this.tabControlMain.Controls.Add(this.tabPageRun);
            this.tabControlMain.Location = new System.Drawing.Point(8, 46);
            this.tabControlMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(480, 827);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageBasic
            // 
            this.tabPageBasic.Controls.Add(this.groupBoxInput);
            this.tabPageBasic.Controls.Add(this.groupBoxSourceMode);
            this.tabPageBasic.Controls.Add(this.groupBoxSubscribed);
            this.tabPageBasic.Controls.Add(this.groupBoxPositionCorrection);
            this.tabPageBasic.Controls.Add(this.groupBoxGeometry);
            this.tabPageBasic.Location = new System.Drawing.Point(4, 28);
            this.tabPageBasic.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageBasic.Name = "tabPageBasic";
            this.tabPageBasic.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageBasic.Size = new System.Drawing.Size(472, 795);
            this.tabPageBasic.TabIndex = 0;
            this.tabPageBasic.Text = "基本参数";
            this.tabPageBasic.UseVisualStyleBackColor = true;
            // 
            // groupBoxInput
            // 
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxInput.Location = new System.Drawing.Point(11, 12);
            this.groupBoxInput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInput.Size = new System.Drawing.Size(416, 126);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入图像";
            // 
            // nodeSubscriptionImage
            // 
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(13, 30);
            this.nodeSubscriptionImage.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionImage.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(385, 77);
            this.nodeSubscriptionImage.TabIndex = 0;
            // 
            // groupBoxSourceMode
            // 
            this.groupBoxSourceMode.Controls.Add(this.radioButtonDraw);
            this.groupBoxSourceMode.Controls.Add(this.radioButtonSubscribe);
            this.groupBoxSourceMode.Location = new System.Drawing.Point(11, 149);
            this.groupBoxSourceMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSourceMode.Name = "groupBoxSourceMode";
            this.groupBoxSourceMode.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSourceMode.Size = new System.Drawing.Size(416, 70);
            this.groupBoxSourceMode.TabIndex = 1;
            this.groupBoxSourceMode.TabStop = false;
            this.groupBoxSourceMode.Text = "数据来源";
            // 
            // radioButtonDraw
            // 
            this.radioButtonDraw.AutoSize = true;
            this.radioButtonDraw.Checked = true;
            this.radioButtonDraw.Location = new System.Drawing.Point(142, 31);
            this.radioButtonDraw.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButtonDraw.Name = "radioButtonDraw";
            this.radioButtonDraw.Size = new System.Drawing.Size(69, 22);
            this.radioButtonDraw.TabIndex = 1;
            this.radioButtonDraw.TabStop = true;
            this.radioButtonDraw.Text = "绘制";
            this.radioButtonDraw.UseVisualStyleBackColor = true;
            // 
            // radioButtonSubscribe
            // 
            this.radioButtonSubscribe.AutoSize = true;
            this.radioButtonSubscribe.Location = new System.Drawing.Point(27, 31);
            this.radioButtonSubscribe.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButtonSubscribe.Name = "radioButtonSubscribe";
            this.radioButtonSubscribe.Size = new System.Drawing.Size(69, 22);
            this.radioButtonSubscribe.TabIndex = 0;
            this.radioButtonSubscribe.Text = "订阅";
            this.radioButtonSubscribe.UseVisualStyleBackColor = true;
            // 
            // groupBoxSubscribed
            // 
            this.groupBoxSubscribed.Controls.Add(this.labelPointSource);
            this.groupBoxSubscribed.Controls.Add(this.nodeSubscriptionPoint);
            this.groupBoxSubscribed.Controls.Add(this.comboBoxPointRole);
            this.groupBoxSubscribed.Controls.Add(this.labelLineSource);
            this.groupBoxSubscribed.Controls.Add(this.nodeSubscriptionLine);
            this.groupBoxSubscribed.Location = new System.Drawing.Point(11, 227);
            this.groupBoxSubscribed.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSubscribed.Name = "groupBoxSubscribed";
            this.groupBoxSubscribed.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSubscribed.Size = new System.Drawing.Size(416, 220);
            this.groupBoxSubscribed.TabIndex = 2;
            this.groupBoxSubscribed.TabStop = false;
            this.groupBoxSubscribed.Text = "订阅结果";
            // 
            // labelPointSource
            // 
            this.labelPointSource.AutoSize = true;
            this.labelPointSource.Location = new System.Drawing.Point(14, 59);
            this.labelPointSource.Name = "labelPointSource";
            this.labelPointSource.Size = new System.Drawing.Size(62, 18);
            this.labelPointSource.TabIndex = 0;
            this.labelPointSource.Text = "目标点";
            // 
            // nodeSubscriptionPoint
            // 
            this.nodeSubscriptionPoint.Location = new System.Drawing.Point(82, 26);
            this.nodeSubscriptionPoint.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionPoint.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionPoint.Name = "nodeSubscriptionPoint";
            this.nodeSubscriptionPoint.Size = new System.Drawing.Size(316, 72);
            this.nodeSubscriptionPoint.TabIndex = 1;
            // 
            // comboBoxPointRole
            // 
            this.comboBoxPointRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPointRole.FormattingEnabled = true;
            this.comboBoxPointRole.Location = new System.Drawing.Point(82, 100);
            this.comboBoxPointRole.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxPointRole.Name = "comboBoxPointRole";
            this.comboBoxPointRole.Size = new System.Drawing.Size(136, 26);
            this.comboBoxPointRole.TabIndex = 2;
            // 
            // labelLineSource
            // 
            this.labelLineSource.AutoSize = true;
            this.labelLineSource.Location = new System.Drawing.Point(14, 166);
            this.labelLineSource.Name = "labelLineSource";
            this.labelLineSource.Size = new System.Drawing.Size(44, 18);
            this.labelLineSource.TabIndex = 3;
            this.labelLineSource.Text = "直线";
            // 
            // nodeSubscriptionLine
            // 
            this.nodeSubscriptionLine.Location = new System.Drawing.Point(82, 133);
            this.nodeSubscriptionLine.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionLine.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionLine.Name = "nodeSubscriptionLine";
            this.nodeSubscriptionLine.Size = new System.Drawing.Size(316, 72);
            this.nodeSubscriptionLine.TabIndex = 4;
            // 
            // groupBoxPositionCorrection
            // 
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(11, 459);
            this.groupBoxPositionCorrection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxPositionCorrection.Name = "groupBoxPositionCorrection";
            this.groupBoxPositionCorrection.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxPositionCorrection.Size = new System.Drawing.Size(416, 134);
            this.groupBoxPositionCorrection.TabIndex = 3;
            this.groupBoxPositionCorrection.TabStop = false;
            this.groupBoxPositionCorrection.Text = "位置修正";
            // 
            // checkBoxUsePositionCorrection
            // 
            this.checkBoxUsePositionCorrection.AutoSize = true;
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(18, 29);
            this.checkBoxUsePositionCorrection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBoxUsePositionCorrection.Name = "checkBoxUsePositionCorrection";
            this.checkBoxUsePositionCorrection.Size = new System.Drawing.Size(106, 22);
            this.checkBoxUsePositionCorrection.TabIndex = 0;
            this.checkBoxUsePositionCorrection.Text = "启用修正";
            this.checkBoxUsePositionCorrection.UseVisualStyleBackColor = true;
            this.checkBoxUsePositionCorrection.CheckedChanged += new System.EventHandler(this.checkBoxUsePositionCorrection_CheckedChanged);
            // 
            // nodeSubscriptionPositionCorrection
            // 
            this.nodeSubscriptionPositionCorrection.Enabled = false;
            this.nodeSubscriptionPositionCorrection.Location = new System.Drawing.Point(13, 53);
            this.nodeSubscriptionPositionCorrection.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionPositionCorrection.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionPositionCorrection.Name = "nodeSubscriptionPositionCorrection";
            this.nodeSubscriptionPositionCorrection.Size = new System.Drawing.Size(380, 72);
            this.nodeSubscriptionPositionCorrection.TabIndex = 1;
            // 
            // groupBoxGeometry
            // 
            this.groupBoxGeometry.Controls.Add(this.labelPointCenterX);
            this.groupBoxGeometry.Controls.Add(this.textBoxPointCenterX);
            this.groupBoxGeometry.Controls.Add(this.labelPointCenterY);
            this.groupBoxGeometry.Controls.Add(this.textBoxPointCenterY);
            this.groupBoxGeometry.Controls.Add(this.labelPointRadius);
            this.groupBoxGeometry.Controls.Add(this.textBoxPointRadius);
            this.groupBoxGeometry.Controls.Add(this.labelLineStartX);
            this.groupBoxGeometry.Controls.Add(this.textBoxLineStartX);
            this.groupBoxGeometry.Controls.Add(this.labelLineStartY);
            this.groupBoxGeometry.Controls.Add(this.textBoxLineStartY);
            this.groupBoxGeometry.Controls.Add(this.labelLineEndX);
            this.groupBoxGeometry.Controls.Add(this.textBoxLineEndX);
            this.groupBoxGeometry.Controls.Add(this.labelLineEndY);
            this.groupBoxGeometry.Controls.Add(this.textBoxLineEndY);
            this.groupBoxGeometry.Location = new System.Drawing.Point(11, 605);
            this.groupBoxGeometry.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxGeometry.Name = "groupBoxGeometry";
            this.groupBoxGeometry.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxGeometry.Size = new System.Drawing.Size(416, 166);
            this.groupBoxGeometry.TabIndex = 4;
            this.groupBoxGeometry.TabStop = false;
            this.groupBoxGeometry.Text = "绘制区域";
            // 
            // labelPointCenterX
            // 
            this.labelPointCenterX.AutoSize = true;
            this.labelPointCenterX.Location = new System.Drawing.Point(14, 36);
            this.labelPointCenterX.Name = "labelPointCenterX";
            this.labelPointCenterX.Size = new System.Drawing.Size(44, 18);
            this.labelPointCenterX.TabIndex = 0;
            this.labelPointCenterX.Text = "点 X";
            // 
            // textBoxPointCenterX
            // 
            this.textBoxPointCenterX.Location = new System.Drawing.Point(64, 31);
            this.textBoxPointCenterX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxPointCenterX.Name = "textBoxPointCenterX";
            this.textBoxPointCenterX.Size = new System.Drawing.Size(64, 28);
            this.textBoxPointCenterX.TabIndex = 1;
            this.textBoxPointCenterX.Text = "240";
            // 
            // labelPointCenterY
            // 
            this.labelPointCenterY.AutoSize = true;
            this.labelPointCenterY.Location = new System.Drawing.Point(137, 36);
            this.labelPointCenterY.Name = "labelPointCenterY";
            this.labelPointCenterY.Size = new System.Drawing.Size(44, 18);
            this.labelPointCenterY.TabIndex = 2;
            this.labelPointCenterY.Text = "点 Y";
            // 
            // textBoxPointCenterY
            // 
            this.textBoxPointCenterY.Location = new System.Drawing.Point(187, 31);
            this.textBoxPointCenterY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxPointCenterY.Name = "textBoxPointCenterY";
            this.textBoxPointCenterY.Size = new System.Drawing.Size(64, 28);
            this.textBoxPointCenterY.TabIndex = 3;
            this.textBoxPointCenterY.Text = "220";
            // 
            // labelPointRadius
            // 
            this.labelPointRadius.AutoSize = true;
            this.labelPointRadius.Location = new System.Drawing.Point(260, 36);
            this.labelPointRadius.Name = "labelPointRadius";
            this.labelPointRadius.Size = new System.Drawing.Size(44, 18);
            this.labelPointRadius.TabIndex = 4;
            this.labelPointRadius.Text = "半径";
            // 
            // textBoxPointRadius
            // 
            this.textBoxPointRadius.Location = new System.Drawing.Point(310, 31);
            this.textBoxPointRadius.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxPointRadius.Name = "textBoxPointRadius";
            this.textBoxPointRadius.Size = new System.Drawing.Size(64, 28);
            this.textBoxPointRadius.TabIndex = 5;
            this.textBoxPointRadius.Text = "50";
            // 
            // labelLineStartX
            // 
            this.labelLineStartX.AutoSize = true;
            this.labelLineStartX.Location = new System.Drawing.Point(14, 79);
            this.labelLineStartX.Name = "labelLineStartX";
            this.labelLineStartX.Size = new System.Drawing.Size(62, 18);
            this.labelLineStartX.TabIndex = 6;
            this.labelLineStartX.Text = "线起 X";
            // 
            // textBoxLineStartX
            // 
            this.textBoxLineStartX.Location = new System.Drawing.Point(86, 74);
            this.textBoxLineStartX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxLineStartX.Name = "textBoxLineStartX";
            this.textBoxLineStartX.Size = new System.Drawing.Size(83, 28);
            this.textBoxLineStartX.TabIndex = 7;
            this.textBoxLineStartX.Text = "120";
            // 
            // labelLineStartY
            // 
            this.labelLineStartY.AutoSize = true;
            this.labelLineStartY.Location = new System.Drawing.Point(214, 79);
            this.labelLineStartY.Name = "labelLineStartY";
            this.labelLineStartY.Size = new System.Drawing.Size(62, 18);
            this.labelLineStartY.TabIndex = 8;
            this.labelLineStartY.Text = "线起 Y";
            // 
            // textBoxLineStartY
            // 
            this.textBoxLineStartY.Location = new System.Drawing.Point(286, 74);
            this.textBoxLineStartY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxLineStartY.Name = "textBoxLineStartY";
            this.textBoxLineStartY.Size = new System.Drawing.Size(83, 28);
            this.textBoxLineStartY.TabIndex = 9;
            this.textBoxLineStartY.Text = "360";
            // 
            // labelLineEndX
            // 
            this.labelLineEndX.AutoSize = true;
            this.labelLineEndX.Location = new System.Drawing.Point(14, 122);
            this.labelLineEndX.Name = "labelLineEndX";
            this.labelLineEndX.Size = new System.Drawing.Size(62, 18);
            this.labelLineEndX.TabIndex = 10;
            this.labelLineEndX.Text = "线终 X";
            // 
            // textBoxLineEndX
            // 
            this.textBoxLineEndX.Location = new System.Drawing.Point(86, 117);
            this.textBoxLineEndX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxLineEndX.Name = "textBoxLineEndX";
            this.textBoxLineEndX.Size = new System.Drawing.Size(83, 28);
            this.textBoxLineEndX.TabIndex = 11;
            this.textBoxLineEndX.Text = "460";
            // 
            // labelLineEndY
            // 
            this.labelLineEndY.AutoSize = true;
            this.labelLineEndY.Location = new System.Drawing.Point(214, 122);
            this.labelLineEndY.Name = "labelLineEndY";
            this.labelLineEndY.Size = new System.Drawing.Size(62, 18);
            this.labelLineEndY.TabIndex = 12;
            this.labelLineEndY.Text = "线终 Y";
            // 
            // textBoxLineEndY
            // 
            this.textBoxLineEndY.Location = new System.Drawing.Point(286, 117);
            this.textBoxLineEndY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxLineEndY.Name = "textBoxLineEndY";
            this.textBoxLineEndY.Size = new System.Drawing.Size(83, 28);
            this.textBoxLineEndY.TabIndex = 13;
            this.textBoxLineEndY.Text = "360";
            // 
            // tabPageRun
            // 
            this.tabPageRun.Controls.Add(this.groupBoxRunParams);
            this.tabPageRun.Location = new System.Drawing.Point(4, 28);
            this.tabPageRun.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageRun.Name = "tabPageRun";
            this.tabPageRun.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageRun.Size = new System.Drawing.Size(472, 795);
            this.tabPageRun.TabIndex = 1;
            this.tabPageRun.Text = "运行参数";
            this.tabPageRun.UseVisualStyleBackColor = true;
            // 
            // groupBoxRunParams
            // 
            this.groupBoxRunParams.Controls.Add(this.labelMeasureMode);
            this.groupBoxRunParams.Controls.Add(this.comboBoxMeasureMode);
            this.groupBoxRunParams.Controls.Add(this.labelCaliperWidth);
            this.groupBoxRunParams.Controls.Add(this.textBoxCaliperWidth);
            this.groupBoxRunParams.Controls.Add(this.labelCaliperHeight);
            this.groupBoxRunParams.Controls.Add(this.textBoxCaliperHeight);
            this.groupBoxRunParams.Controls.Add(this.labelCount);
            this.groupBoxRunParams.Controls.Add(this.textBoxCount);
            this.groupBoxRunParams.Controls.Add(this.labelEdgeStrength);
            this.groupBoxRunParams.Controls.Add(this.textBoxEdgeStrength);
            this.groupBoxRunParams.Controls.Add(this.labelBlurSize);
            this.groupBoxRunParams.Controls.Add(this.textBoxBlurSize);
            this.groupBoxRunParams.Controls.Add(this.labelPolarity);
            this.groupBoxRunParams.Controls.Add(this.comboBoxPolarity);
            this.groupBoxRunParams.Controls.Add(this.labelFindMode);
            this.groupBoxRunParams.Controls.Add(this.comboBoxFindMode);
            this.groupBoxRunParams.Controls.Add(this.labelDirection);
            this.groupBoxRunParams.Controls.Add(this.comboBoxDirection);
            this.groupBoxRunParams.Controls.Add(this.labelRunRoiTarget);
            this.groupBoxRunParams.Controls.Add(this.comboBoxRunRoiTarget);
            this.groupBoxRunParams.Location = new System.Drawing.Point(11, 12);
            this.groupBoxRunParams.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxRunParams.Name = "groupBoxRunParams";
            this.groupBoxRunParams.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxRunParams.Size = new System.Drawing.Size(416, 286);
            this.groupBoxRunParams.TabIndex = 0;
            this.groupBoxRunParams.TabStop = false;
            this.groupBoxRunParams.Text = "算法参数";
            // 
            // labelMeasureMode
            // 
            this.labelMeasureMode.AutoSize = true;
            this.labelMeasureMode.Location = new System.Drawing.Point(18, 38);
            this.labelMeasureMode.Name = "labelMeasureMode";
            this.labelMeasureMode.Size = new System.Drawing.Size(80, 18);
            this.labelMeasureMode.TabIndex = 0;
            this.labelMeasureMode.Text = "测量精度";
            // 
            // comboBoxMeasureMode
            // 
            this.comboBoxMeasureMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMeasureMode.FormattingEnabled = true;
            this.comboBoxMeasureMode.Location = new System.Drawing.Point(104, 34);
            this.comboBoxMeasureMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxMeasureMode.Name = "comboBoxMeasureMode";
            this.comboBoxMeasureMode.Size = new System.Drawing.Size(108, 26);
            this.comboBoxMeasureMode.TabIndex = 1;
            // 
            // labelCaliperWidth
            // 
            this.labelCaliperWidth.AutoSize = true;
            this.labelCaliperWidth.Location = new System.Drawing.Point(227, 38);
            this.labelCaliperWidth.Name = "labelCaliperWidth";
            this.labelCaliperWidth.Size = new System.Drawing.Size(80, 18);
            this.labelCaliperWidth.TabIndex = 2;
            this.labelCaliperWidth.Text = "卡尺宽度";
            // 
            // textBoxCaliperWidth
            // 
            this.textBoxCaliperWidth.Location = new System.Drawing.Point(310, 34);
            this.textBoxCaliperWidth.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxCaliperWidth.Name = "textBoxCaliperWidth";
            this.textBoxCaliperWidth.Size = new System.Drawing.Size(78, 28);
            this.textBoxCaliperWidth.TabIndex = 3;
            this.textBoxCaliperWidth.Text = "10";
            // 
            // labelCaliperHeight
            // 
            this.labelCaliperHeight.AutoSize = true;
            this.labelCaliperHeight.Location = new System.Drawing.Point(18, 89);
            this.labelCaliperHeight.Name = "labelCaliperHeight";
            this.labelCaliperHeight.Size = new System.Drawing.Size(80, 18);
            this.labelCaliperHeight.TabIndex = 4;
            this.labelCaliperHeight.Text = "卡尺高度";
            // 
            // textBoxCaliperHeight
            // 
            this.textBoxCaliperHeight.Location = new System.Drawing.Point(104, 84);
            this.textBoxCaliperHeight.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxCaliperHeight.Name = "textBoxCaliperHeight";
            this.textBoxCaliperHeight.Size = new System.Drawing.Size(108, 28);
            this.textBoxCaliperHeight.TabIndex = 5;
            this.textBoxCaliperHeight.Text = "60";
            // 
            // labelCount
            // 
            this.labelCount.AutoSize = true;
            this.labelCount.Location = new System.Drawing.Point(227, 89);
            this.labelCount.Name = "labelCount";
            this.labelCount.Size = new System.Drawing.Size(80, 18);
            this.labelCount.TabIndex = 6;
            this.labelCount.Text = "卡尺数量";
            // 
            // textBoxCount
            // 
            this.textBoxCount.Location = new System.Drawing.Point(310, 84);
            this.textBoxCount.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxCount.Name = "textBoxCount";
            this.textBoxCount.Size = new System.Drawing.Size(78, 28);
            this.textBoxCount.TabIndex = 7;
            this.textBoxCount.Text = "30";
            // 
            // labelEdgeStrength
            // 
            this.labelEdgeStrength.AutoSize = true;
            this.labelEdgeStrength.Location = new System.Drawing.Point(18, 139);
            this.labelEdgeStrength.Name = "labelEdgeStrength";
            this.labelEdgeStrength.Size = new System.Drawing.Size(80, 18);
            this.labelEdgeStrength.TabIndex = 8;
            this.labelEdgeStrength.Text = "边缘阈值";
            // 
            // textBoxEdgeStrength
            // 
            this.textBoxEdgeStrength.Location = new System.Drawing.Point(104, 134);
            this.textBoxEdgeStrength.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxEdgeStrength.Name = "textBoxEdgeStrength";
            this.textBoxEdgeStrength.Size = new System.Drawing.Size(108, 28);
            this.textBoxEdgeStrength.TabIndex = 9;
            this.textBoxEdgeStrength.Text = "20";
            // 
            // labelBlurSize
            // 
            this.labelBlurSize.AutoSize = true;
            this.labelBlurSize.Location = new System.Drawing.Point(227, 139);
            this.labelBlurSize.Name = "labelBlurSize";
            this.labelBlurSize.Size = new System.Drawing.Size(62, 18);
            this.labelBlurSize.TabIndex = 10;
            this.labelBlurSize.Text = "平滑核";
            // 
            // textBoxBlurSize
            // 
            this.textBoxBlurSize.Location = new System.Drawing.Point(310, 134);
            this.textBoxBlurSize.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxBlurSize.Name = "textBoxBlurSize";
            this.textBoxBlurSize.Size = new System.Drawing.Size(78, 28);
            this.textBoxBlurSize.TabIndex = 11;
            this.textBoxBlurSize.Text = "3";
            // 
            // labelPolarity
            // 
            this.labelPolarity.AutoSize = true;
            this.labelPolarity.Location = new System.Drawing.Point(18, 190);
            this.labelPolarity.Name = "labelPolarity";
            this.labelPolarity.Size = new System.Drawing.Size(80, 18);
            this.labelPolarity.TabIndex = 12;
            this.labelPolarity.Text = "边缘极性";
            // 
            // comboBoxPolarity
            // 
            this.comboBoxPolarity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxPolarity.FormattingEnabled = true;
            this.comboBoxPolarity.Location = new System.Drawing.Point(104, 185);
            this.comboBoxPolarity.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxPolarity.Name = "comboBoxPolarity";
            this.comboBoxPolarity.Size = new System.Drawing.Size(108, 26);
            this.comboBoxPolarity.TabIndex = 13;
            // 
            // labelFindMode
            // 
            this.labelFindMode.AutoSize = true;
            this.labelFindMode.Location = new System.Drawing.Point(227, 190);
            this.labelFindMode.Name = "labelFindMode";
            this.labelFindMode.Size = new System.Drawing.Size(80, 18);
            this.labelFindMode.TabIndex = 14;
            this.labelFindMode.Text = "取边方式";
            // 
            // comboBoxFindMode
            // 
            this.comboBoxFindMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxFindMode.FormattingEnabled = true;
            this.comboBoxFindMode.Location = new System.Drawing.Point(310, 185);
            this.comboBoxFindMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxFindMode.Name = "comboBoxFindMode";
            this.comboBoxFindMode.Size = new System.Drawing.Size(78, 26);
            this.comboBoxFindMode.TabIndex = 15;
            // 
            // labelDirection
            // 
            this.labelDirection.AutoSize = true;
            this.labelDirection.Location = new System.Drawing.Point(18, 240);
            this.labelDirection.Name = "labelDirection";
            this.labelDirection.Size = new System.Drawing.Size(80, 18);
            this.labelDirection.TabIndex = 16;
            this.labelDirection.Text = "搜索方向";
            // 
            // comboBoxDirection
            // 
            this.comboBoxDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDirection.FormattingEnabled = true;
            this.comboBoxDirection.Location = new System.Drawing.Point(104, 235);
            this.comboBoxDirection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxDirection.Name = "comboBoxDirection";
            this.comboBoxDirection.Size = new System.Drawing.Size(108, 26);
            this.comboBoxDirection.TabIndex = 17;
            // 
            // labelRunRoiTarget
            // 
            this.labelRunRoiTarget.AutoSize = true;
            this.labelRunRoiTarget.Location = new System.Drawing.Point(227, 240);
            this.labelRunRoiTarget.Name = "labelRunRoiTarget";
            this.labelRunRoiTarget.Size = new System.Drawing.Size(80, 18);
            this.labelRunRoiTarget.TabIndex = 18;
            this.labelRunRoiTarget.Text = "参数对象";
            // 
            // comboBoxRunRoiTarget
            // 
            this.comboBoxRunRoiTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRunRoiTarget.FormattingEnabled = true;
            this.comboBoxRunRoiTarget.Location = new System.Drawing.Point(310, 235);
            this.comboBoxRunRoiTarget.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxRunRoiTarget.Name = "comboBoxRunRoiTarget";
            this.comboBoxRunRoiTarget.Size = new System.Drawing.Size(78, 26);
            this.comboBoxRunRoiTarget.TabIndex = 19;
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackColor = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundImageCustom = null;
            this.showImageControl1.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControl1.BackgroundTransparency = 1F;
            this.showImageControl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.showImageControl1.Location = new System.Drawing.Point(494, 46);
            this.showImageControl1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.RoiColor = System.Drawing.Color.Lime;
            this.showImageControl1.ShowCheckerBackground = false;
            this.showImageControl1.ShowPixelInfo = false;
            this.showImageControl1.Size = new System.Drawing.Size(645, 778);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 1;
            // 
            // buttonDrawRoi
            // 
            this.buttonDrawRoi.Location = new System.Drawing.Point(532, 832);
            this.buttonDrawRoi.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(108, 41);
            this.buttonDrawRoi.TabIndex = 2;
            this.buttonDrawRoi.Text = "绘制区域";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            // 
            // buttonConfirmRoi
            // 
            this.buttonConfirmRoi.Location = new System.Drawing.Point(646, 832);
            this.buttonConfirmRoi.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(108, 41);
            this.buttonConfirmRoi.TabIndex = 3;
            this.buttonConfirmRoi.Text = "确认区域";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(761, 832);
            this.buttonRefresh.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(117, 41);
            this.buttonRefresh.TabIndex = 4;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(885, 832);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(117, 41);
            this.buttonRun.TabIndex = 5;
            this.buttonRun.Text = "运行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(1022, 832);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(117, 41);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormPointLineDistance
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1151, 886);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonConfirmRoi);
            this.Controls.Add(this.buttonDrawRoi);
            this.Controls.Add(this.showImageControl1);
            this.Controls.Add(this.tabControlMain);
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.Name = "NodeParamFormPointLineDistance";
            this.Text = "点到线距离";
            this.Controls.SetChildIndex(this.tabControlMain, 0);
            this.Controls.SetChildIndex(this.showImageControl1, 0);
            this.Controls.SetChildIndex(this.buttonDrawRoi, 0);
            this.Controls.SetChildIndex(this.buttonConfirmRoi, 0);
            this.Controls.SetChildIndex(this.buttonRefresh, 0);
            this.Controls.SetChildIndex(this.buttonRun, 0);
            this.Controls.SetChildIndex(this.buttonSave, 0);
            this.tabControlMain.ResumeLayout(false);
            this.tabPageBasic.ResumeLayout(false);
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxSourceMode.ResumeLayout(false);
            this.groupBoxSourceMode.PerformLayout();
            this.groupBoxSubscribed.ResumeLayout(false);
            this.groupBoxSubscribed.PerformLayout();
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxGeometry.ResumeLayout(false);
            this.groupBoxGeometry.PerformLayout();
            this.tabPageRun.ResumeLayout(false);
            this.groupBoxRunParams.ResumeLayout(false);
            this.groupBoxRunParams.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageBasic;
        private System.Windows.Forms.TabPage tabPageRun;
        private System.Windows.Forms.GroupBox groupBoxInput;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionImage;
        private System.Windows.Forms.GroupBox groupBoxSourceMode;
        private System.Windows.Forms.RadioButton radioButtonDraw;
        private System.Windows.Forms.RadioButton radioButtonSubscribe;
        private System.Windows.Forms.GroupBox groupBoxSubscribed;
        private System.Windows.Forms.Label labelPointSource;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPoint;
        private System.Windows.Forms.ComboBox comboBoxPointRole;
        private System.Windows.Forms.Label labelLineSource;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionLine;
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;
        private System.Windows.Forms.GroupBox groupBoxGeometry;
        private System.Windows.Forms.Label labelPointCenterX;
        private System.Windows.Forms.TextBox textBoxPointCenterX;
        private System.Windows.Forms.Label labelPointCenterY;
        private System.Windows.Forms.TextBox textBoxPointCenterY;
        private System.Windows.Forms.Label labelPointRadius;
        private System.Windows.Forms.TextBox textBoxPointRadius;
        private System.Windows.Forms.Label labelLineStartX;
        private System.Windows.Forms.TextBox textBoxLineStartX;
        private System.Windows.Forms.Label labelLineStartY;
        private System.Windows.Forms.TextBox textBoxLineStartY;
        private System.Windows.Forms.Label labelLineEndX;
        private System.Windows.Forms.TextBox textBoxLineEndX;
        private System.Windows.Forms.Label labelLineEndY;
        private System.Windows.Forms.TextBox textBoxLineEndY;
        private System.Windows.Forms.GroupBox groupBoxRunParams;
        private System.Windows.Forms.Label labelMeasureMode;
        private System.Windows.Forms.ComboBox comboBoxMeasureMode;
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
        private System.Windows.Forms.Label labelRunRoiTarget;
        private System.Windows.Forms.ComboBox comboBoxRunRoiTarget;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
        private System.Windows.Forms.Button buttonDrawRoi;
        private System.Windows.Forms.Button buttonConfirmRoi;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonSave;
    }
}
