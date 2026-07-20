namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    partial class NodeParamFormImagePreprocess
    {
        /// <summary>
        /// 设计器组件容器。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 释放窗体资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化窗体控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
            this.showImageControlPreview = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.panelRight = new System.Windows.Forms.Panel();
            this.groupBoxInput = new System.Windows.Forms.GroupBox();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.nodeSubscriptionImage = new TDJS_Vision.Node.NodeSubscription();
            this.groupBoxMode = new System.Windows.Forms.GroupBox();
            this.comboBoxMode = new System.Windows.Forms.ComboBox();
            this.labelMode = new System.Windows.Forms.Label();
            this.groupBoxEmphasize = new System.Windows.Forms.GroupBox();
            this.numericFactor = new System.Windows.Forms.NumericUpDown();
            this.labelFactor = new System.Windows.Forms.Label();
            this.numericMaskHeight = new System.Windows.Forms.NumericUpDown();
            this.labelMaskHeight = new System.Windows.Forms.Label();
            this.numericMaskWidth = new System.Windows.Forms.NumericUpDown();
            this.labelMaskWidth = new System.Windows.Forms.Label();
            this.groupBoxLaws = new System.Windows.Forms.GroupBox();
            this.numericEnergySize = new System.Windows.Forms.NumericUpDown();
            this.labelEnergySize = new System.Windows.Forms.Label();
            this.comboBoxLawsKernel = new System.Windows.Forms.ComboBox();
            this.labelLawsKernel = new System.Windows.Forms.Label();
            this.groupBoxMedian = new System.Windows.Forms.GroupBox();
            this.numericMedianKernel = new System.Windows.Forms.NumericUpDown();
            this.labelMedianKernel = new System.Windows.Forms.Label();
            this.buttonPreview = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.groupBoxInput.SuspendLayout();
            this.groupBoxMode.SuspendLayout();
            this.groupBoxEmphasize.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFactor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaskHeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaskWidth)).BeginInit();
            this.groupBoxLaws.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericEnergySize)).BeginInit();
            this.groupBoxMedian.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMedianKernel)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanelRoot
            // 
            this.tableLayoutPanelRoot.ColumnCount = 2;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 64F));
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36F));
            this.tableLayoutPanelRoot.Controls.Add(this.showImageControlPreview, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanelRoot.Name = "tableLayoutPanelRoot";
            this.tableLayoutPanelRoot.RowCount = 1;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Size = new System.Drawing.Size(1000, 566);
            this.tableLayoutPanelRoot.TabIndex = 0;
            // 
            // showImageControlPreview
            // 
            this.showImageControlPreview.BackColor = System.Drawing.Color.Black;
            this.showImageControlPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControlPreview.Location = new System.Drawing.Point(3, 3);
            this.showImageControlPreview.Name = "showImageControlPreview";
            this.showImageControlPreview.Size = new System.Drawing.Size(634, 560);
            this.showImageControlPreview.TabIndex = 0;
            // 
            // panelRight
            // 
            this.panelRight.Controls.Add(this.groupBoxInput);
            this.panelRight.Controls.Add(this.groupBoxMode);
            this.panelRight.Controls.Add(this.groupBoxEmphasize);
            this.panelRight.Controls.Add(this.groupBoxLaws);
            this.panelRight.Controls.Add(this.groupBoxMedian);
            this.panelRight.Controls.Add(this.buttonPreview);
            this.panelRight.Controls.Add(this.buttonSave);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(643, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(354, 560);
            this.panelRight.TabIndex = 1;
            // 
            // groupBoxInput
            // 
            this.groupBoxInput.Controls.Add(this.buttonRefresh);
            this.groupBoxInput.Controls.Add(this.nodeSubscriptionImage);
            this.groupBoxInput.Location = new System.Drawing.Point(12, 8);
            this.groupBoxInput.Name = "groupBoxInput";
            this.groupBoxInput.Size = new System.Drawing.Size(336, 112);
            this.groupBoxInput.TabIndex = 0;
            this.groupBoxInput.TabStop = false;
            this.groupBoxInput.Text = "输入图像";
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(118, 70);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(98, 30);
            this.buttonRefresh.TabIndex = 1;
            this.buttonRefresh.Text = "刷新图像";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // nodeSubscriptionImage
            // 
            this.nodeSubscriptionImage.Location = new System.Drawing.Point(10, 25);
            this.nodeSubscriptionImage.Margin = new System.Windows.Forms.Padding(4);
            this.nodeSubscriptionImage.Name = "nodeSubscriptionImage";
            this.nodeSubscriptionImage.Size = new System.Drawing.Size(318, 36);
            this.nodeSubscriptionImage.TabIndex = 0;
            // 
            // groupBoxMode
            // 
            this.groupBoxMode.Controls.Add(this.comboBoxMode);
            this.groupBoxMode.Controls.Add(this.labelMode);
            this.groupBoxMode.Location = new System.Drawing.Point(12, 128);
            this.groupBoxMode.Name = "groupBoxMode";
            this.groupBoxMode.Size = new System.Drawing.Size(336, 72);
            this.groupBoxMode.TabIndex = 1;
            this.groupBoxMode.TabStop = false;
            this.groupBoxMode.Text = "处理方式";
            // 
            // comboBoxMode
            // 
            this.comboBoxMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMode.FormattingEnabled = true;
            this.comboBoxMode.Location = new System.Drawing.Point(104, 28);
            this.comboBoxMode.Name = "comboBoxMode";
            this.comboBoxMode.Size = new System.Drawing.Size(204, 23);
            this.comboBoxMode.TabIndex = 1;
            // 
            // labelMode
            // 
            this.labelMode.AutoSize = true;
            this.labelMode.Location = new System.Drawing.Point(20, 32);
            this.labelMode.Name = "labelMode";
            this.labelMode.Size = new System.Drawing.Size(67, 15);
            this.labelMode.TabIndex = 0;
            this.labelMode.Text = "算法类型";
            // 
            // groupBoxEmphasize
            // 
            this.groupBoxEmphasize.Controls.Add(this.numericFactor);
            this.groupBoxEmphasize.Controls.Add(this.labelFactor);
            this.groupBoxEmphasize.Controls.Add(this.numericMaskHeight);
            this.groupBoxEmphasize.Controls.Add(this.labelMaskHeight);
            this.groupBoxEmphasize.Controls.Add(this.numericMaskWidth);
            this.groupBoxEmphasize.Controls.Add(this.labelMaskWidth);
            this.groupBoxEmphasize.Location = new System.Drawing.Point(12, 208);
            this.groupBoxEmphasize.Name = "groupBoxEmphasize";
            this.groupBoxEmphasize.Size = new System.Drawing.Size(336, 116);
            this.groupBoxEmphasize.TabIndex = 2;
            this.groupBoxEmphasize.TabStop = false;
            this.groupBoxEmphasize.Text = "边缘增强参数";
            // 
            // numericFactor
            // 
            this.numericFactor.DecimalPlaces = 2;
            this.numericFactor.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numericFactor.Location = new System.Drawing.Point(104, 78);
            this.numericFactor.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericFactor.Name = "numericFactor";
            this.numericFactor.Size = new System.Drawing.Size(92, 25);
            this.numericFactor.TabIndex = 5;
            this.numericFactor.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericFactor.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // labelFactor
            // 
            this.labelFactor.AutoSize = true;
            this.labelFactor.Location = new System.Drawing.Point(20, 82);
            this.labelFactor.Name = "labelFactor";
            this.labelFactor.Size = new System.Drawing.Size(67, 15);
            this.labelFactor.TabIndex = 4;
            this.labelFactor.Text = "增强强度";
            // 
            // numericMaskHeight
            // 
            this.numericMaskHeight.Location = new System.Drawing.Point(246, 32);
            this.numericMaskHeight.Maximum = new decimal(new int[] {
            101,
            0,
            0,
            0});
            this.numericMaskHeight.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericMaskHeight.Name = "numericMaskHeight";
            this.numericMaskHeight.Size = new System.Drawing.Size(66, 25);
            this.numericMaskHeight.TabIndex = 3;
            this.numericMaskHeight.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericMaskHeight.Value = new decimal(new int[] {
            7,
            0,
            0,
            0});
            // 
            // labelMaskHeight
            // 
            this.labelMaskHeight.AutoSize = true;
            this.labelMaskHeight.Location = new System.Drawing.Point(174, 36);
            this.labelMaskHeight.Name = "labelMaskHeight";
            this.labelMaskHeight.Size = new System.Drawing.Size(67, 15);
            this.labelMaskHeight.TabIndex = 2;
            this.labelMaskHeight.Text = "窗口高度";
            // 
            // numericMaskWidth
            // 
            this.numericMaskWidth.Location = new System.Drawing.Point(104, 32);
            this.numericMaskWidth.Maximum = new decimal(new int[] {
            101,
            0,
            0,
            0});
            this.numericMaskWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericMaskWidth.Name = "numericMaskWidth";
            this.numericMaskWidth.Size = new System.Drawing.Size(58, 25);
            this.numericMaskWidth.TabIndex = 1;
            this.numericMaskWidth.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericMaskWidth.Value = new decimal(new int[] {
            7,
            0,
            0,
            0});
            // 
            // labelMaskWidth
            // 
            this.labelMaskWidth.AutoSize = true;
            this.labelMaskWidth.Location = new System.Drawing.Point(20, 36);
            this.labelMaskWidth.Name = "labelMaskWidth";
            this.labelMaskWidth.Size = new System.Drawing.Size(67, 15);
            this.labelMaskWidth.TabIndex = 0;
            this.labelMaskWidth.Text = "窗口宽度";
            // 
            // groupBoxLaws
            // 
            this.groupBoxLaws.Controls.Add(this.numericEnergySize);
            this.groupBoxLaws.Controls.Add(this.labelEnergySize);
            this.groupBoxLaws.Controls.Add(this.comboBoxLawsKernel);
            this.groupBoxLaws.Controls.Add(this.labelLawsKernel);
            this.groupBoxLaws.Location = new System.Drawing.Point(12, 332);
            this.groupBoxLaws.Name = "groupBoxLaws";
            this.groupBoxLaws.Size = new System.Drawing.Size(336, 92);
            this.groupBoxLaws.TabIndex = 3;
            this.groupBoxLaws.TabStop = false;
            this.groupBoxLaws.Text = "纹理滤波参数";
            // 
            // numericEnergySize
            // 
            this.numericEnergySize.Location = new System.Drawing.Point(104, 58);
            this.numericEnergySize.Maximum = new decimal(new int[] {
            101,
            0,
            0,
            0});
            this.numericEnergySize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericEnergySize.Name = "numericEnergySize";
            this.numericEnergySize.Size = new System.Drawing.Size(92, 25);
            this.numericEnergySize.TabIndex = 3;
            this.numericEnergySize.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericEnergySize.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
            // 
            // labelEnergySize
            // 
            this.labelEnergySize.AutoSize = true;
            this.labelEnergySize.Location = new System.Drawing.Point(20, 62);
            this.labelEnergySize.Name = "labelEnergySize";
            this.labelEnergySize.Size = new System.Drawing.Size(67, 15);
            this.labelEnergySize.TabIndex = 2;
            this.labelEnergySize.Text = "能量窗口";
            // 
            // comboBoxLawsKernel
            // 
            this.comboBoxLawsKernel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxLawsKernel.FormattingEnabled = true;
            this.comboBoxLawsKernel.Location = new System.Drawing.Point(104, 26);
            this.comboBoxLawsKernel.Name = "comboBoxLawsKernel";
            this.comboBoxLawsKernel.Size = new System.Drawing.Size(204, 23);
            this.comboBoxLawsKernel.TabIndex = 1;
            // 
            // labelLawsKernel
            // 
            this.labelLawsKernel.AutoSize = true;
            this.labelLawsKernel.Location = new System.Drawing.Point(20, 30);
            this.labelLawsKernel.Name = "labelLawsKernel";
            this.labelLawsKernel.Size = new System.Drawing.Size(67, 15);
            this.labelLawsKernel.TabIndex = 0;
            this.labelLawsKernel.Text = "纹理类型";
            // 
            // groupBoxMedian
            // 
            this.groupBoxMedian.Controls.Add(this.numericMedianKernel);
            this.groupBoxMedian.Controls.Add(this.labelMedianKernel);
            this.groupBoxMedian.Location = new System.Drawing.Point(12, 432);
            this.groupBoxMedian.Name = "groupBoxMedian";
            this.groupBoxMedian.Size = new System.Drawing.Size(336, 62);
            this.groupBoxMedian.TabIndex = 4;
            this.groupBoxMedian.TabStop = false;
            this.groupBoxMedian.Text = "中值滤波参数";
            // 
            // numericMedianKernel
            // 
            this.numericMedianKernel.Location = new System.Drawing.Point(104, 26);
            this.numericMedianKernel.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.numericMedianKernel.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.numericMedianKernel.Name = "numericMedianKernel";
            this.numericMedianKernel.Size = new System.Drawing.Size(92, 25);
            this.numericMedianKernel.TabIndex = 1;
            this.numericMedianKernel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numericMedianKernel.Value = new decimal(new int[] {
            3,
            0,
            0,
            0});
            // 
            // labelMedianKernel
            // 
            this.labelMedianKernel.AutoSize = true;
            this.labelMedianKernel.Location = new System.Drawing.Point(20, 30);
            this.labelMedianKernel.Name = "labelMedianKernel";
            this.labelMedianKernel.Size = new System.Drawing.Size(67, 15);
            this.labelMedianKernel.TabIndex = 0;
            this.labelMedianKernel.Text = "窗口大小";
            // 
            // buttonPreview
            // 
            this.buttonPreview.Location = new System.Drawing.Point(76, 512);
            this.buttonPreview.Name = "buttonPreview";
            this.buttonPreview.Size = new System.Drawing.Size(94, 32);
            this.buttonPreview.TabIndex = 5;
            this.buttonPreview.Text = "预览";
            this.buttonPreview.UseVisualStyleBackColor = true;
            this.buttonPreview.Click += new System.EventHandler(this.buttonPreview_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(198, 512);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(94, 32);
            this.buttonSave.TabIndex = 6;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormImagePreprocess
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1004, 600);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormImagePreprocess";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "图像预处理";
            this.Controls.SetChildIndex(this.tableLayoutPanelRoot, 0);
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.groupBoxInput.ResumeLayout(false);
            this.groupBoxMode.ResumeLayout(false);
            this.groupBoxMode.PerformLayout();
            this.groupBoxEmphasize.ResumeLayout(false);
            this.groupBoxEmphasize.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericFactor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaskHeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaskWidth)).EndInit();
            this.groupBoxLaws.ResumeLayout(false);
            this.groupBoxLaws.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericEnergySize)).EndInit();
            this.groupBoxMedian.ResumeLayout(false);
            this.groupBoxMedian.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericMedianKernel)).EndInit();
            this.ResumeLayout(false);

        }

        /// <summary>
        /// 根布局。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;

        /// <summary>
        /// 预览图控件。
        /// </summary>
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControlPreview;

        /// <summary>
        /// 右侧参数面板。
        /// </summary>
        private System.Windows.Forms.Panel panelRight;

        /// <summary>
        /// 输入图像参数组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxInput;

        /// <summary>
        /// 刷新图像按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonRefresh;

        /// <summary>
        /// 输入图像订阅控件。
        /// </summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscriptionImage;

        /// <summary>
        /// 处理方式参数组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxMode;

        /// <summary>
        /// 预处理方式下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxMode;

        /// <summary>
        /// 预处理方式标签。
        /// </summary>
        private System.Windows.Forms.Label labelMode;

        /// <summary>
        /// 边缘增强参数组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxEmphasize;

        /// <summary>
        /// 边缘增强强度数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericFactor;

        /// <summary>
        /// 边缘增强强度标签。
        /// </summary>
        private System.Windows.Forms.Label labelFactor;

        /// <summary>
        /// 边缘增强窗口高度数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMaskHeight;

        /// <summary>
        /// 边缘增强窗口高度标签。
        /// </summary>
        private System.Windows.Forms.Label labelMaskHeight;

        /// <summary>
        /// 边缘增强窗口宽度数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMaskWidth;

        /// <summary>
        /// 边缘增强窗口宽度标签。
        /// </summary>
        private System.Windows.Forms.Label labelMaskWidth;

        /// <summary>
        /// 纹理滤波参数组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxLaws;

        /// <summary>
        /// 纹理能量窗口数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericEnergySize;

        /// <summary>
        /// 纹理能量窗口标签。
        /// </summary>
        private System.Windows.Forms.Label labelEnergySize;

        /// <summary>
        /// Laws 纹理核下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxLawsKernel;

        /// <summary>
        /// Laws 纹理核标签。
        /// </summary>
        private System.Windows.Forms.Label labelLawsKernel;

        /// <summary>
        /// 中值滤波参数组。
        /// </summary>
        private System.Windows.Forms.GroupBox groupBoxMedian;

        /// <summary>
        /// 中值滤波窗口数值框。
        /// </summary>
        private System.Windows.Forms.NumericUpDown numericMedianKernel;

        /// <summary>
        /// 中值滤波窗口标签。
        /// </summary>
        private System.Windows.Forms.Label labelMedianKernel;

        /// <summary>
        /// 预览按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonPreview;

        /// <summary>
        /// 保存按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSave;
    }
}
