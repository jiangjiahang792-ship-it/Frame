namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    partial class NodeParamFormLineLineAngle
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormLineLineAngle));
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxSourceMode = new System.Windows.Forms.GroupBox();
            this.radioButtonDraw = new System.Windows.Forms.RadioButton();
            this.radioButtonSubscribe = new System.Windows.Forms.RadioButton();
            this.groupBoxSubscribed = new System.Windows.Forms.GroupBox();
            this.labelLine1Source = new System.Windows.Forms.Label();
            this.nodeSubscriptionLine1 = new TDJS_Vision.Node.NodeSubscription();
            this.labelLine2Source = new System.Windows.Forms.Label();
            this.nodeSubscriptionLine2 = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxGeometry = new System.Windows.Forms.GroupBox();
            this.labelL1StartX = new System.Windows.Forms.Label();
            this.textBoxL1StartX = new System.Windows.Forms.TextBox();
            this.labelL1StartY = new System.Windows.Forms.Label();
            this.textBoxL1StartY = new System.Windows.Forms.TextBox();
            this.labelL1EndX = new System.Windows.Forms.Label();
            this.textBoxL1EndX = new System.Windows.Forms.TextBox();
            this.labelL1EndY = new System.Windows.Forms.Label();
            this.textBoxL1EndY = new System.Windows.Forms.TextBox();
            this.labelL2StartX = new System.Windows.Forms.Label();
            this.textBoxL2StartX = new System.Windows.Forms.TextBox();
            this.labelL2StartY = new System.Windows.Forms.Label();
            this.textBoxL2StartY = new System.Windows.Forms.TextBox();
            this.labelL2EndX = new System.Windows.Forms.Label();
            this.textBoxL2EndX = new System.Windows.Forms.TextBox();
            this.labelL2EndY = new System.Windows.Forms.Label();
            this.textBoxL2EndY = new System.Windows.Forms.TextBox();
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
            this.groupBoxInput.Text = "图像输入";
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
            this.groupBoxSubscribed.Controls.Add(this.labelLine1Source);
            this.groupBoxSubscribed.Controls.Add(this.nodeSubscriptionLine1);
            this.groupBoxSubscribed.Controls.Add(this.labelLine2Source);
            this.groupBoxSubscribed.Controls.Add(this.nodeSubscriptionLine2);
            this.groupBoxSubscribed.Location = new System.Drawing.Point(11, 227);
            this.groupBoxSubscribed.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSubscribed.Name = "groupBoxSubscribed";
            this.groupBoxSubscribed.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSubscribed.Size = new System.Drawing.Size(416, 170);
            this.groupBoxSubscribed.TabIndex = 2;
            this.groupBoxSubscribed.TabStop = false;
            this.groupBoxSubscribed.Text = "订阅直线";
            // 
            // labelLine1Source
            // 
            this.labelLine1Source.AutoSize = true;
            this.labelLine1Source.Location = new System.Drawing.Point(14, 59);
            this.labelLine1Source.Name = "labelLine1Source";
            this.labelLine1Source.Size = new System.Drawing.Size(53, 18);
            this.labelLine1Source.TabIndex = 0;
            this.labelLine1Source.Text = "直线1";
            // 
            // nodeSubscriptionLine1
            // 
            this.nodeSubscriptionLine1.Location = new System.Drawing.Point(74, 26);
            this.nodeSubscriptionLine1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionLine1.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionLine1.Name = "nodeSubscriptionLine1";
            this.nodeSubscriptionLine1.Size = new System.Drawing.Size(324, 72);
            this.nodeSubscriptionLine1.TabIndex = 1;
            // 
            // labelLine2Source
            // 
            this.labelLine2Source.AutoSize = true;
            this.labelLine2Source.Location = new System.Drawing.Point(10, 135);
            this.labelLine2Source.Name = "labelLine2Source";
            this.labelLine2Source.Size = new System.Drawing.Size(53, 18);
            this.labelLine2Source.TabIndex = 2;
            this.labelLine2Source.Text = "直线2";
            // 
            // nodeSubscriptionLine2
            // 
            this.nodeSubscriptionLine2.Location = new System.Drawing.Point(74, 99);
            this.nodeSubscriptionLine2.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionLine2.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionLine2.Name = "nodeSubscriptionLine2";
            this.nodeSubscriptionLine2.Size = new System.Drawing.Size(324, 72);
            this.nodeSubscriptionLine2.TabIndex = 3;
            // 
            // groupBoxPositionCorrection
            // 
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(11, 408);
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
            this.groupBoxGeometry.Controls.Add(this.labelL1StartX);
            this.groupBoxGeometry.Controls.Add(this.textBoxL1StartX);
            this.groupBoxGeometry.Controls.Add(this.labelL1StartY);
            this.groupBoxGeometry.Controls.Add(this.textBoxL1StartY);
            this.groupBoxGeometry.Controls.Add(this.labelL1EndX);
            this.groupBoxGeometry.Controls.Add(this.textBoxL1EndX);
            this.groupBoxGeometry.Controls.Add(this.labelL1EndY);
            this.groupBoxGeometry.Controls.Add(this.textBoxL1EndY);
            this.groupBoxGeometry.Controls.Add(this.labelL2StartX);
            this.groupBoxGeometry.Controls.Add(this.textBoxL2StartX);
            this.groupBoxGeometry.Controls.Add(this.labelL2StartY);
            this.groupBoxGeometry.Controls.Add(this.textBoxL2StartY);
            this.groupBoxGeometry.Controls.Add(this.labelL2EndX);
            this.groupBoxGeometry.Controls.Add(this.textBoxL2EndX);
            this.groupBoxGeometry.Controls.Add(this.labelL2EndY);
            this.groupBoxGeometry.Controls.Add(this.textBoxL2EndY);
            this.groupBoxGeometry.Location = new System.Drawing.Point(11, 562);
            this.groupBoxGeometry.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxGeometry.Name = "groupBoxGeometry";
            this.groupBoxGeometry.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxGeometry.Size = new System.Drawing.Size(416, 209);
            this.groupBoxGeometry.TabIndex = 4;
            this.groupBoxGeometry.TabStop = false;
            this.groupBoxGeometry.Text = "绘制直线卡尺";
            // 
            // labelL1StartX
            // 
            this.labelL1StartX.AutoSize = true;
            this.labelL1StartX.Location = new System.Drawing.Point(14, 36);
            this.labelL1StartX.Name = "labelL1StartX";
            this.labelL1StartX.Size = new System.Drawing.Size(62, 18);
            this.labelL1StartX.TabIndex = 0;
            this.labelL1StartX.Text = "线1起X";
            // 
            // textBoxL1StartX
            // 
            this.textBoxL1StartX.Location = new System.Drawing.Point(86, 31);
            this.textBoxL1StartX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL1StartX.Name = "textBoxL1StartX";
            this.textBoxL1StartX.Size = new System.Drawing.Size(83, 28);
            this.textBoxL1StartX.TabIndex = 1;
            this.textBoxL1StartX.Text = "120";
            // 
            // labelL1StartY
            // 
            this.labelL1StartY.AutoSize = true;
            this.labelL1StartY.Location = new System.Drawing.Point(214, 36);
            this.labelL1StartY.Name = "labelL1StartY";
            this.labelL1StartY.Size = new System.Drawing.Size(62, 18);
            this.labelL1StartY.TabIndex = 2;
            this.labelL1StartY.Text = "线1起Y";
            // 
            // textBoxL1StartY
            // 
            this.textBoxL1StartY.Location = new System.Drawing.Point(286, 31);
            this.textBoxL1StartY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL1StartY.Name = "textBoxL1StartY";
            this.textBoxL1StartY.Size = new System.Drawing.Size(83, 28);
            this.textBoxL1StartY.TabIndex = 3;
            this.textBoxL1StartY.Text = "160";
            // 
            // labelL1EndX
            // 
            this.labelL1EndX.AutoSize = true;
            this.labelL1EndX.Location = new System.Drawing.Point(14, 79);
            this.labelL1EndX.Name = "labelL1EndX";
            this.labelL1EndX.Size = new System.Drawing.Size(62, 18);
            this.labelL1EndX.TabIndex = 4;
            this.labelL1EndX.Text = "线1终X";
            // 
            // textBoxL1EndX
            // 
            this.textBoxL1EndX.Location = new System.Drawing.Point(86, 74);
            this.textBoxL1EndX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL1EndX.Name = "textBoxL1EndX";
            this.textBoxL1EndX.Size = new System.Drawing.Size(83, 28);
            this.textBoxL1EndX.TabIndex = 5;
            this.textBoxL1EndX.Text = "420";
            // 
            // labelL1EndY
            // 
            this.labelL1EndY.AutoSize = true;
            this.labelL1EndY.Location = new System.Drawing.Point(214, 79);
            this.labelL1EndY.Name = "labelL1EndY";
            this.labelL1EndY.Size = new System.Drawing.Size(62, 18);
            this.labelL1EndY.TabIndex = 6;
            this.labelL1EndY.Text = "线1终Y";
            // 
            // textBoxL1EndY
            // 
            this.textBoxL1EndY.Location = new System.Drawing.Point(286, 74);
            this.textBoxL1EndY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL1EndY.Name = "textBoxL1EndY";
            this.textBoxL1EndY.Size = new System.Drawing.Size(83, 28);
            this.textBoxL1EndY.TabIndex = 7;
            this.textBoxL1EndY.Text = "160";
            // 
            // labelL2StartX
            // 
            this.labelL2StartX.AutoSize = true;
            this.labelL2StartX.Location = new System.Drawing.Point(14, 122);
            this.labelL2StartX.Name = "labelL2StartX";
            this.labelL2StartX.Size = new System.Drawing.Size(62, 18);
            this.labelL2StartX.TabIndex = 8;
            this.labelL2StartX.Text = "线2起X";
            // 
            // textBoxL2StartX
            // 
            this.textBoxL2StartX.Location = new System.Drawing.Point(86, 118);
            this.textBoxL2StartX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL2StartX.Name = "textBoxL2StartX";
            this.textBoxL2StartX.Size = new System.Drawing.Size(83, 28);
            this.textBoxL2StartX.TabIndex = 9;
            this.textBoxL2StartX.Text = "160";
            // 
            // labelL2StartY
            // 
            this.labelL2StartY.AutoSize = true;
            this.labelL2StartY.Location = new System.Drawing.Point(214, 122);
            this.labelL2StartY.Name = "labelL2StartY";
            this.labelL2StartY.Size = new System.Drawing.Size(62, 18);
            this.labelL2StartY.TabIndex = 10;
            this.labelL2StartY.Text = "线2起Y";
            // 
            // textBoxL2StartY
            // 
            this.textBoxL2StartY.Location = new System.Drawing.Point(286, 118);
            this.textBoxL2StartY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL2StartY.Name = "textBoxL2StartY";
            this.textBoxL2StartY.Size = new System.Drawing.Size(83, 28);
            this.textBoxL2StartY.TabIndex = 11;
            this.textBoxL2StartY.Text = "360";
            // 
            // labelL2EndX
            // 
            this.labelL2EndX.AutoSize = true;
            this.labelL2EndX.Location = new System.Drawing.Point(14, 166);
            this.labelL2EndX.Name = "labelL2EndX";
            this.labelL2EndX.Size = new System.Drawing.Size(62, 18);
            this.labelL2EndX.TabIndex = 12;
            this.labelL2EndX.Text = "线2终X";
            // 
            // textBoxL2EndX
            // 
            this.textBoxL2EndX.Location = new System.Drawing.Point(86, 161);
            this.textBoxL2EndX.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL2EndX.Name = "textBoxL2EndX";
            this.textBoxL2EndX.Size = new System.Drawing.Size(83, 28);
            this.textBoxL2EndX.TabIndex = 13;
            this.textBoxL2EndX.Text = "420";
            // 
            // labelL2EndY
            // 
            this.labelL2EndY.AutoSize = true;
            this.labelL2EndY.Location = new System.Drawing.Point(214, 166);
            this.labelL2EndY.Name = "labelL2EndY";
            this.labelL2EndY.Size = new System.Drawing.Size(62, 18);
            this.labelL2EndY.TabIndex = 14;
            this.labelL2EndY.Text = "线2终Y";
            // 
            // textBoxL2EndY
            // 
            this.textBoxL2EndY.Location = new System.Drawing.Point(286, 161);
            this.textBoxL2EndY.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxL2EndY.Name = "textBoxL2EndY";
            this.textBoxL2EndY.Size = new System.Drawing.Size(83, 28);
            this.textBoxL2EndY.TabIndex = 15;
            this.textBoxL2EndY.Text = "240";
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
            this.groupBoxRunParams.Size = new System.Drawing.Size(416, 307);
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
            this.textBoxCaliperWidth.Text = "20";
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
            this.textBoxCaliperHeight.Text = "120";
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
            this.textBoxCount.Text = "15";
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
            this.labelRunRoiTarget.Size = new System.Drawing.Size(35, 18);
            this.labelRunRoiTarget.TabIndex = 18;
            this.labelRunRoiTarget.Text = "ROI";
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
            this.buttonDrawRoi.Text = "绘制ROI";
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
            this.buttonConfirmRoi.Text = "确认ROI";
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
            this.buttonRun.Text = "执行";
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
            // NodeParamFormLineLineAngle
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(1151, 886);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonRun);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonConfirmRoi);
            this.Controls.Add(this.buttonDrawRoi);
            this.Controls.Add(this.showImageControl1);
            this.Controls.Add(this.tabControlMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.Name = "NodeParamFormLineLineAngle";
            this.Text = "线到线夹角";
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
        private System.Windows.Forms.Label labelLine1Source;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionLine1;
        private System.Windows.Forms.Label labelLine2Source;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionLine2;
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;
        private System.Windows.Forms.GroupBox groupBoxGeometry;
        private System.Windows.Forms.Label labelL1StartX;
        private System.Windows.Forms.TextBox textBoxL1StartX;
        private System.Windows.Forms.Label labelL1StartY;
        private System.Windows.Forms.TextBox textBoxL1StartY;
        private System.Windows.Forms.Label labelL1EndX;
        private System.Windows.Forms.TextBox textBoxL1EndX;
        private System.Windows.Forms.Label labelL1EndY;
        private System.Windows.Forms.TextBox textBoxL1EndY;
        private System.Windows.Forms.Label labelL2StartX;
        private System.Windows.Forms.TextBox textBoxL2StartX;
        private System.Windows.Forms.Label labelL2StartY;
        private System.Windows.Forms.TextBox textBoxL2StartY;
        private System.Windows.Forms.Label labelL2EndX;
        private System.Windows.Forms.TextBox textBoxL2EndX;
        private System.Windows.Forms.Label labelL2EndY;
        private System.Windows.Forms.TextBox textBoxL2EndY;
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
