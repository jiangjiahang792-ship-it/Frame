namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    partial class NodeParamFormFindPoint
    {
        /// <summary>
        /// Designer component container.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Releases managed designer components.
        /// </summary>
        /// <param name="disposing">True when managed resources should be disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Creates designer-visible controls for the FindPoint parameter form.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormFindPoint));
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageBasic = new System.Windows.Forms.TabPage();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.labelStatus = new System.Windows.Forms.Label();
            this.labelRoiCount = new System.Windows.Forms.Label();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonClearRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonDrawRoi = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.showImageControlPreview = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.tabPageRun = new System.Windows.Forms.TabPage();
            this.groupBoxRun = new System.Windows.Forms.GroupBox();
            this.labelMaxPointCount = new System.Windows.Forms.Label();
            this.textBoxMaxPointCount = new System.Windows.Forms.TextBox();
            this.labelSampleStep = new System.Windows.Forms.Label();
            this.textBoxSampleStep = new System.Windows.Forms.TextBox();
            this.labelMinContourPoints = new System.Windows.Forms.Label();
            this.textBoxMinContourPoints = new System.Windows.Forms.TextBox();
            this.labelBlurSize = new System.Windows.Forms.Label();
            this.textBoxBlurSize = new System.Windows.Forms.TextBox();
            this.labelHighThreshold = new System.Windows.Forms.Label();
            this.textBoxHighThreshold = new System.Windows.Forms.TextBox();
            this.labelLowThreshold = new System.Windows.Forms.Label();
            this.textBoxLowThreshold = new System.Windows.Forms.TextBox();
            this.tabControlMain.SuspendLayout();
            this.tabPageBasic.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.groupBoxInput.SuspendLayout();
            this.tabPageRun.SuspendLayout();
            this.groupBoxRun.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageBasic);
            this.tabControlMain.Controls.Add(this.tabPageRun);
            this.tabControlMain.Location = new System.Drawing.Point(18, 53);
            this.tabControlMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1213, 845);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageBasic
            // 
            this.tabPageBasic.AutoScroll = true;
            this.tabPageBasic.Controls.Add(this.groupBoxPositionCorrection);
            this.tabPageBasic.Controls.Add(this.labelStatus);
            this.tabPageBasic.Controls.Add(this.labelRoiCount);
            this.tabPageBasic.Controls.Add(this.buttonSave);
            this.tabPageBasic.Controls.Add(this.buttonRun);
            this.tabPageBasic.Controls.Add(this.buttonClearRoi);
            this.tabPageBasic.Controls.Add(this.buttonConfirmRoi);
            this.tabPageBasic.Controls.Add(this.buttonDrawRoi);
            this.tabPageBasic.Controls.Add(this.buttonRefresh);
            this.tabPageBasic.Controls.Add(this.showImageControlPreview);
            this.tabPageBasic.Controls.Add(this.groupBoxInput);
            this.tabPageBasic.Location = new System.Drawing.Point(4, 28);
            this.tabPageBasic.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageBasic.Name = "tabPageBasic";
            this.tabPageBasic.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageBasic.Size = new System.Drawing.Size(1205, 813);
            this.tabPageBasic.TabIndex = 0;
            this.tabPageBasic.Text = "基本参数";
            this.tabPageBasic.UseVisualStyleBackColor = true;
            // 
            // groupBoxPositionCorrection
            // 
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(22, 138);
            this.groupBoxPositionCorrection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxPositionCorrection.Name = "groupBoxPositionCorrection";
            this.groupBoxPositionCorrection.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxPositionCorrection.Size = new System.Drawing.Size(467, 132);
            this.groupBoxPositionCorrection.TabIndex = 10;
            this.groupBoxPositionCorrection.TabStop = false;
            this.groupBoxPositionCorrection.Text = "位置修正";
            // 
            // checkBoxUsePositionCorrection
            // 
            this.checkBoxUsePositionCorrection.AutoSize = true;
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(18, 28);
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
            this.nodeSubscriptionPositionCorrection.Location = new System.Drawing.Point(18, 53);
            this.nodeSubscriptionPositionCorrection.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionPositionCorrection.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionPositionCorrection.Name = "nodeSubscriptionPositionCorrection";
            this.nodeSubscriptionPositionCorrection.Size = new System.Drawing.Size(428, 72);
            this.nodeSubscriptionPositionCorrection.TabIndex = 1;
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(25, 746);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(44, 18);
            this.labelStatus.TabIndex = 9;
            this.labelStatus.Text = "就绪";
            // 
            // labelRoiCount
            // 
            this.labelRoiCount.AutoSize = true;
            this.labelRoiCount.Location = new System.Drawing.Point(25, 706);
            this.labelRoiCount.Name = "labelRoiCount";
            this.labelRoiCount.Size = new System.Drawing.Size(107, 18);
            this.labelRoiCount.TabIndex = 8;
            this.labelRoiCount.Text = "区域数量：0";
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(1050, 736);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(124, 38);
            this.buttonSave.TabIndex = 7;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Location = new System.Drawing.Point(906, 736);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(124, 38);
            this.buttonRun.TabIndex = 6;
            this.buttonRun.Text = "运行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonClearRoi
            // 
            this.buttonClearRoi.Location = new System.Drawing.Point(285, 278);
            this.buttonClearRoi.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonClearRoi.Name = "buttonClearRoi";
            this.buttonClearRoi.Size = new System.Drawing.Size(124, 38);
            this.buttonClearRoi.TabIndex = 4;
            this.buttonClearRoi.Text = "清空区域";
            this.buttonClearRoi.UseVisualStyleBackColor = true;
            this.buttonClearRoi.Click += new System.EventHandler(this.buttonClearRoi_Click);
            // 
            // buttonConfirmRoi
            // 
            this.buttonConfirmRoi.Location = new System.Drawing.Point(154, 278);
            this.buttonConfirmRoi.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(124, 38);
            this.buttonConfirmRoi.TabIndex = 3;
            this.buttonConfirmRoi.Text = "确认区域";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            // 
            // buttonDrawRoi
            // 
            this.buttonDrawRoi.Location = new System.Drawing.Point(24, 278);
            this.buttonDrawRoi.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonDrawRoi.Name = "buttonDrawRoi";
            this.buttonDrawRoi.Size = new System.Drawing.Size(124, 38);
            this.buttonDrawRoi.TabIndex = 2;
            this.buttonDrawRoi.Text = "绘制区域";
            this.buttonDrawRoi.UseVisualStyleBackColor = true;
            this.buttonDrawRoi.Click += new System.EventHandler(this.buttonDrawRoi_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(538, 736);
            this.buttonRefresh.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(158, 38);
            this.buttonRefresh.TabIndex = 1;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // showImageControlPreview
            // 
            this.showImageControlPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.showImageControlPreview.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControlPreview.BackgroundImageCustom = null;
            this.showImageControlPreview.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControlPreview.BackgroundTransparency = 1F;
            this.showImageControlPreview.Location = new System.Drawing.Point(538, 26);
            this.showImageControlPreview.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.showImageControlPreview.Name = "showImageControlPreview";
            this.showImageControlPreview.RoiColor = System.Drawing.Color.Lime;
            this.showImageControlPreview.ShowCheckerBackground = false;
            this.showImageControlPreview.ShowPixelInfo = false;
            this.showImageControlPreview.Size = new System.Drawing.Size(636, 679);
            this.showImageControlPreview.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControlPreview.TabIndex = 5;
            // 
            // groupBoxInput
            // 
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxInput.Location = new System.Drawing.Point(22, 26);
            this.groupBoxInput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInput.Size = new System.Drawing.Size(467, 104);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入图像";
            // 
            // nodeSubscriptionImage
            // 
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(18, 31);
            this.nodeSubscriptionImage.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.nodeSubscriptionImage.MinimumSize = new System.Drawing.Size(292, 72);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(428, 72);
            this.nodeSubscriptionImage.TabIndex = 0;
            // 
            // tabPageRun
            // 
            this.tabPageRun.Controls.Add(this.groupBoxRun);
            this.tabPageRun.Location = new System.Drawing.Point(4, 28);
            this.tabPageRun.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageRun.Name = "tabPageRun";
            this.tabPageRun.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabPageRun.Size = new System.Drawing.Size(1205, 813);
            this.tabPageRun.TabIndex = 1;
            this.tabPageRun.Text = "运行参数";
            this.tabPageRun.UseVisualStyleBackColor = true;
            // 
            // groupBoxRun
            // 
            this.groupBoxRun.Controls.Add(this.labelMaxPointCount);
            this.groupBoxRun.Controls.Add(this.textBoxMaxPointCount);
            this.groupBoxRun.Controls.Add(this.labelSampleStep);
            this.groupBoxRun.Controls.Add(this.textBoxSampleStep);
            this.groupBoxRun.Controls.Add(this.labelMinContourPoints);
            this.groupBoxRun.Controls.Add(this.textBoxMinContourPoints);
            this.groupBoxRun.Controls.Add(this.labelBlurSize);
            this.groupBoxRun.Controls.Add(this.textBoxBlurSize);
            this.groupBoxRun.Controls.Add(this.labelHighThreshold);
            this.groupBoxRun.Controls.Add(this.textBoxHighThreshold);
            this.groupBoxRun.Controls.Add(this.labelLowThreshold);
            this.groupBoxRun.Controls.Add(this.textBoxLowThreshold);
            this.groupBoxRun.Location = new System.Drawing.Point(27, 29);
            this.groupBoxRun.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxRun.Name = "groupBoxRun";
            this.groupBoxRun.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxRun.Size = new System.Drawing.Size(484, 350);
            this.groupBoxRun.TabIndex = 0;
            this.groupBoxRun.TabStop = false;
            this.groupBoxRun.Text = "边缘提取参数";
            // 
            // labelMaxPointCount
            // 
            this.labelMaxPointCount.AutoSize = true;
            this.labelMaxPointCount.Location = new System.Drawing.Point(25, 283);
            this.labelMaxPointCount.Name = "labelMaxPointCount";
            this.labelMaxPointCount.Size = new System.Drawing.Size(80, 18);
            this.labelMaxPointCount.TabIndex = 10;
            this.labelMaxPointCount.Text = "最大点数";
            // 
            // textBoxMaxPointCount
            // 
            this.textBoxMaxPointCount.Location = new System.Drawing.Point(200, 278);
            this.textBoxMaxPointCount.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxMaxPointCount.Name = "textBoxMaxPointCount";
            this.textBoxMaxPointCount.Size = new System.Drawing.Size(202, 28);
            this.textBoxMaxPointCount.TabIndex = 11;
            // 
            // labelSampleStep
            // 
            this.labelSampleStep.AutoSize = true;
            this.labelSampleStep.Location = new System.Drawing.Point(25, 235);
            this.labelSampleStep.Name = "labelSampleStep";
            this.labelSampleStep.Size = new System.Drawing.Size(80, 18);
            this.labelSampleStep.TabIndex = 8;
            this.labelSampleStep.Text = "采样步长";
            // 
            // textBoxSampleStep
            // 
            this.textBoxSampleStep.Location = new System.Drawing.Point(200, 230);
            this.textBoxSampleStep.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxSampleStep.Name = "textBoxSampleStep";
            this.textBoxSampleStep.Size = new System.Drawing.Size(202, 28);
            this.textBoxSampleStep.TabIndex = 9;
            // 
            // labelMinContourPoints
            // 
            this.labelMinContourPoints.AutoSize = true;
            this.labelMinContourPoints.Location = new System.Drawing.Point(25, 187);
            this.labelMinContourPoints.Name = "labelMinContourPoints";
            this.labelMinContourPoints.Size = new System.Drawing.Size(116, 18);
            this.labelMinContourPoints.TabIndex = 6;
            this.labelMinContourPoints.Text = "最小轮廓点数";
            // 
            // textBoxMinContourPoints
            // 
            this.textBoxMinContourPoints.Location = new System.Drawing.Point(200, 182);
            this.textBoxMinContourPoints.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxMinContourPoints.Name = "textBoxMinContourPoints";
            this.textBoxMinContourPoints.Size = new System.Drawing.Size(202, 28);
            this.textBoxMinContourPoints.TabIndex = 7;
            // 
            // labelBlurSize
            // 
            this.labelBlurSize.AutoSize = true;
            this.labelBlurSize.Location = new System.Drawing.Point(25, 139);
            this.labelBlurSize.Name = "labelBlurSize";
            this.labelBlurSize.Size = new System.Drawing.Size(62, 18);
            this.labelBlurSize.TabIndex = 4;
            this.labelBlurSize.Text = "平滑核";
            // 
            // textBoxBlurSize
            // 
            this.textBoxBlurSize.Location = new System.Drawing.Point(200, 134);
            this.textBoxBlurSize.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxBlurSize.Name = "textBoxBlurSize";
            this.textBoxBlurSize.Size = new System.Drawing.Size(202, 28);
            this.textBoxBlurSize.TabIndex = 5;
            // 
            // labelHighThreshold
            // 
            this.labelHighThreshold.AutoSize = true;
            this.labelHighThreshold.Location = new System.Drawing.Point(25, 91);
            this.labelHighThreshold.Name = "labelHighThreshold";
            this.labelHighThreshold.Size = new System.Drawing.Size(62, 18);
            this.labelHighThreshold.TabIndex = 2;
            this.labelHighThreshold.Text = "高阈值";
            // 
            // textBoxHighThreshold
            // 
            this.textBoxHighThreshold.Location = new System.Drawing.Point(200, 86);
            this.textBoxHighThreshold.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxHighThreshold.Name = "textBoxHighThreshold";
            this.textBoxHighThreshold.Size = new System.Drawing.Size(202, 28);
            this.textBoxHighThreshold.TabIndex = 3;
            // 
            // labelLowThreshold
            // 
            this.labelLowThreshold.AutoSize = true;
            this.labelLowThreshold.Location = new System.Drawing.Point(25, 43);
            this.labelLowThreshold.Name = "labelLowThreshold";
            this.labelLowThreshold.Size = new System.Drawing.Size(62, 18);
            this.labelLowThreshold.TabIndex = 0;
            this.labelLowThreshold.Text = "低阈值";
            // 
            // textBoxLowThreshold
            // 
            this.textBoxLowThreshold.Location = new System.Drawing.Point(200, 38);
            this.textBoxLowThreshold.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.textBoxLowThreshold.Name = "textBoxLowThreshold";
            this.textBoxLowThreshold.Size = new System.Drawing.Size(202, 28);
            this.textBoxLowThreshold.TabIndex = 1;
            // 
            // NodeParamFormFindPoint
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(1249, 917);
            this.Controls.Add(this.tabControlMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormFindPoint";
            this.Text = "找点";
            this.Controls.SetChildIndex(this.tabControlMain, 0);
            this.tabControlMain.ResumeLayout(false);
            this.tabPageBasic.ResumeLayout(false);
            this.tabPageBasic.PerformLayout();
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.groupBoxInput.ResumeLayout(false);
            this.tabPageRun.ResumeLayout(false);
            this.groupBoxRun.ResumeLayout(false);
            this.groupBoxRun.PerformLayout();
            this.ResumeLayout(false);

        }

        /// <summary>Top-level tab control.</summary>
        private System.Windows.Forms.TabControl tabControlMain;
        /// <summary>Basic parameter tab.</summary>
        private System.Windows.Forms.TabPage tabPageBasic;
        /// <summary>Run parameter tab.</summary>
        private System.Windows.Forms.TabPage tabPageRun;
        /// <summary>Input subscription group.</summary>
        private System.Windows.Forms.GroupBox groupBoxInput;
        /// <summary>Input image subscription selector.</summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionImage;
        /// <summary>Position correction group.</summary>
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        /// <summary>Position correction enable checkbox.</summary>
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        /// <summary>Position correction subscription selector.</summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionPositionCorrection;
        /// <summary>Image and overlay preview control.</summary>
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControlPreview;
        /// <summary>Refresh image button.</summary>
        private System.Windows.Forms.Button buttonRefresh;
        /// <summary>Draw ROI button.</summary>
        private System.Windows.Forms.Button buttonDrawRoi;
        /// <summary>Confirm ROI button.</summary>
        private System.Windows.Forms.Button buttonConfirmRoi;
        /// <summary>Clear ROI button.</summary>
        private System.Windows.Forms.Button buttonClearRoi;
        /// <summary>Run preview button.</summary>
        private System.Windows.Forms.Button buttonRun;
        /// <summary>Save parameter button.</summary>
        private System.Windows.Forms.Button buttonSave;
        /// <summary>Confirmed ROI count label.</summary>
        private System.Windows.Forms.Label labelRoiCount;
        /// <summary>Preview runtime status label.</summary>
        private System.Windows.Forms.Label labelStatus;
        /// <summary>Run parameter group.</summary>
        private System.Windows.Forms.GroupBox groupBoxRun;
        /// <summary>Low threshold label.</summary>
        private System.Windows.Forms.Label labelLowThreshold;
        /// <summary>Low threshold textbox.</summary>
        private System.Windows.Forms.TextBox textBoxLowThreshold;
        /// <summary>High threshold label.</summary>
        private System.Windows.Forms.Label labelHighThreshold;
        /// <summary>High threshold textbox.</summary>
        private System.Windows.Forms.TextBox textBoxHighThreshold;
        /// <summary>Blur kernel size label.</summary>
        private System.Windows.Forms.Label labelBlurSize;
        /// <summary>Blur kernel size textbox.</summary>
        private System.Windows.Forms.TextBox textBoxBlurSize;
        /// <summary>Minimum contour point count label.</summary>
        private System.Windows.Forms.Label labelMinContourPoints;
        /// <summary>Minimum contour point count textbox.</summary>
        private System.Windows.Forms.TextBox textBoxMinContourPoints;
        /// <summary>Contour sampling step label.</summary>
        private System.Windows.Forms.Label labelSampleStep;
        /// <summary>Contour sampling step textbox.</summary>
        private System.Windows.Forms.TextBox textBoxSampleStep;
        /// <summary>Maximum flattened point count label.</summary>
        private System.Windows.Forms.Label labelMaxPointCount;
        /// <summary>Maximum flattened point count textbox.</summary>
        private System.Windows.Forms.TextBox textBoxMaxPointCount;
    }
}
