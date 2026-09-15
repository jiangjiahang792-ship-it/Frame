namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
    partial class ParamFormUnsupervisedDetection
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelInputImage = new System.Windows.Forms.Label();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.labelTemplate = new System.Windows.Forms.Label();
            this.textBoxTemplatePath = new System.Windows.Forms.TextBox();
            this.buttonSelectTemplate = new System.Windows.Forms.Button();
            this.labelThreshold = new System.Windows.Forms.Label();
            this.numericUpDownThreshold = new System.Windows.Forms.NumericUpDown();
            this.labelInferenceBatchSize = new System.Windows.Forms.Label();
            this.numericUpDownInferenceBatchSize = new System.Windows.Forms.NumericUpDown();
            this.labelMiniArea = new System.Windows.Forms.Label();
            this.numericUpDownMiniArea = new System.Windows.Forms.NumericUpDown();
            this.labelMaxBoxes = new System.Windows.Forms.Label();
            this.textBoxMaxBoxes = new System.Windows.Forms.TextBox();
            this.labelTemplateInfoTitle = new System.Windows.Forms.Label();
            this.labelTemplateInfo = new System.Windows.Forms.Label();
            this.buttonRefreshTemplate = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.openFileDialogTemplate = new System.Windows.Forms.OpenFileDialog();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInferenceBatchSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMiniArea)).BeginInit();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 4;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 140F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160F));
            this.tableLayoutPanel1.Controls.Add(this.labelInputImage, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.nodeSubscription1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelTemplate, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBoxTemplatePath, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonSelectTemplate, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.labelThreshold, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.numericUpDownThreshold, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelInferenceBatchSize, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.numericUpDownInferenceBatchSize, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelMiniArea, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.numericUpDownMiniArea, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.labelMaxBoxes, 2, 3);
            this.tableLayoutPanel1.Controls.Add(this.textBoxMaxBoxes, 3, 3);
            this.tableLayoutPanel1.Controls.Add(this.labelTemplateInfoTitle, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.labelTemplateInfo, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.buttonRefreshTemplate, 3, 4);
            this.tableLayoutPanel1.Controls.Add(this.buttonSave, 0, 6);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 7;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 76F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 78F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(934, 584);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // labelInputImage
            // 
            this.labelInputImage.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelInputImage.AutoSize = true;
            this.labelInputImage.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelInputImage.Location = new System.Drawing.Point(21, 27);
            this.labelInputImage.Name = "labelInputImage";
            this.labelInputImage.Size = new System.Drawing.Size(98, 22);
            this.labelInputImage.TabIndex = 0;
            this.labelInputImage.Text = "输入图像";
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this.nodeSubscription1, 2);
            this.nodeSubscription1.Location = new System.Drawing.Point(143, 8);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(260, 60);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(628, 60);
            this.nodeSubscription1.TabIndex = 1;
            // 
            // labelTemplate
            // 
            this.labelTemplate.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTemplate.AutoSize = true;
            this.labelTemplate.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelTemplate.Location = new System.Drawing.Point(10, 97);
            this.labelTemplate.Name = "labelTemplate";
            this.labelTemplate.Size = new System.Drawing.Size(120, 22);
            this.labelTemplate.TabIndex = 2;
            this.labelTemplate.Text = "无监督模板";
            // 
            // textBoxTemplatePath
            // 
            this.textBoxTemplatePath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this.textBoxTemplatePath, 2);
            this.textBoxTemplatePath.Font = new System.Drawing.Font("宋体", 10.8F);
            this.textBoxTemplatePath.Location = new System.Drawing.Point(143, 92);
            this.textBoxTemplatePath.Name = "textBoxTemplatePath";
            this.textBoxTemplatePath.ReadOnly = true;
            this.textBoxTemplatePath.Size = new System.Drawing.Size(628, 32);
            this.textBoxTemplatePath.TabIndex = 3;
            // 
            // buttonSelectTemplate
            // 
            this.buttonSelectTemplate.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSelectTemplate.Font = new System.Drawing.Font("宋体", 10.8F);
            this.buttonSelectTemplate.Location = new System.Drawing.Point(801, 86);
            this.buttonSelectTemplate.Name = "buttonSelectTemplate";
            this.buttonSelectTemplate.Size = new System.Drawing.Size(106, 44);
            this.buttonSelectTemplate.TabIndex = 4;
            this.buttonSelectTemplate.Text = "选择";
            this.buttonSelectTemplate.UseVisualStyleBackColor = true;
            this.buttonSelectTemplate.Click += new System.EventHandler(this.buttonSelectTemplate_Click);
            // 
            // labelThreshold
            // 
            this.labelThreshold.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelThreshold.AutoSize = true;
            this.labelThreshold.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelThreshold.Location = new System.Drawing.Point(21, 161);
            this.labelThreshold.Name = "labelThreshold";
            this.labelThreshold.Size = new System.Drawing.Size(155, 22);
            this.labelThreshold.TabIndex = 5;
            this.labelThreshold.Text = "异常阈值(0=模板)";
            // 
            // numericUpDownThreshold
            // 
            this.numericUpDownThreshold.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownThreshold.DecimalPlaces = 2;
            this.numericUpDownThreshold.Font = new System.Drawing.Font("宋体", 10.8F);
            this.numericUpDownThreshold.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.numericUpDownThreshold.Location = new System.Drawing.Point(143, 156);
            this.numericUpDownThreshold.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownThreshold.Name = "numericUpDownThreshold";
            this.numericUpDownThreshold.Size = new System.Drawing.Size(160, 32);
            this.numericUpDownThreshold.TabIndex = 6;
            this.numericUpDownThreshold.Value = new decimal(new int[] {
            30,
            0,
            0,
            131072});
            // 
            // labelInferenceBatchSize
            // 
            this.labelInferenceBatchSize.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelInferenceBatchSize.AutoSize = true;
            this.labelInferenceBatchSize.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelInferenceBatchSize.Location = new System.Drawing.Point(524, 161);
            this.labelInferenceBatchSize.Name = "labelInferenceBatchSize";
            this.labelInferenceBatchSize.Size = new System.Drawing.Size(98, 22);
            this.labelInferenceBatchSize.TabIndex = 7;
            this.labelInferenceBatchSize.Text = "推理批次";
            // 
            // numericUpDownInferenceBatchSize
            // 
            this.numericUpDownInferenceBatchSize.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownInferenceBatchSize.Font = new System.Drawing.Font("宋体", 10.8F);
            this.numericUpDownInferenceBatchSize.Location = new System.Drawing.Point(777, 156);
            this.numericUpDownInferenceBatchSize.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
            this.numericUpDownInferenceBatchSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownInferenceBatchSize.Name = "numericUpDownInferenceBatchSize";
            this.numericUpDownInferenceBatchSize.Size = new System.Drawing.Size(136, 32);
            this.numericUpDownInferenceBatchSize.TabIndex = 8;
            this.numericUpDownInferenceBatchSize.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // labelMiniArea
            // 
            this.labelMiniArea.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelMiniArea.AutoSize = true;
            this.labelMiniArea.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelMiniArea.Location = new System.Drawing.Point(10, 225);
            this.labelMiniArea.Name = "labelMiniArea";
            this.labelMiniArea.Size = new System.Drawing.Size(120, 22);
            this.labelMiniArea.TabIndex = 9;
            this.labelMiniArea.Text = "最小缺陷面积";
            // 
            // numericUpDownMiniArea
            // 
            this.numericUpDownMiniArea.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numericUpDownMiniArea.Font = new System.Drawing.Font("宋体", 10.8F);
            this.numericUpDownMiniArea.Location = new System.Drawing.Point(143, 220);
            this.numericUpDownMiniArea.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericUpDownMiniArea.Name = "numericUpDownMiniArea";
            this.numericUpDownMiniArea.Size = new System.Drawing.Size(160, 32);
            this.numericUpDownMiniArea.TabIndex = 10;
            this.numericUpDownMiniArea.Value = new decimal(new int[] {
            100,
            0,
            0,
            0});
            // 
            // labelMaxBoxes
            // 
            this.labelMaxBoxes.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelMaxBoxes.AutoSize = true;
            this.labelMaxBoxes.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelMaxBoxes.Location = new System.Drawing.Point(513, 225);
            this.labelMaxBoxes.Name = "labelMaxBoxes";
            this.labelMaxBoxes.Size = new System.Drawing.Size(120, 22);
            this.labelMaxBoxes.TabIndex = 11;
            this.labelMaxBoxes.Text = "最大异常框";
            // 
            // textBoxMaxBoxes
            // 
            this.textBoxMaxBoxes.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.textBoxMaxBoxes.Font = new System.Drawing.Font("宋体", 10.8F);
            this.textBoxMaxBoxes.Location = new System.Drawing.Point(777, 220);
            this.textBoxMaxBoxes.Name = "textBoxMaxBoxes";
            this.textBoxMaxBoxes.Size = new System.Drawing.Size(136, 32);
            this.textBoxMaxBoxes.TabIndex = 12;
            this.textBoxMaxBoxes.Text = "128";
            // 
            // labelTemplateInfoTitle
            // 
            this.labelTemplateInfoTitle.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTemplateInfoTitle.AutoSize = true;
            this.labelTemplateInfoTitle.Font = new System.Drawing.Font("宋体", 10.8F);
            this.labelTemplateInfoTitle.Location = new System.Drawing.Point(10, 312);
            this.labelTemplateInfoTitle.Name = "labelTemplateInfoTitle";
            this.labelTemplateInfoTitle.Size = new System.Drawing.Size(120, 22);
            this.labelTemplateInfoTitle.TabIndex = 13;
            this.labelTemplateInfoTitle.Text = "模板信息";
            // 
            // labelTemplateInfo
            // 
            this.labelTemplateInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.labelTemplateInfo.AutoEllipsis = true;
            this.tableLayoutPanel1.SetColumnSpan(this.labelTemplateInfo, 2);
            this.labelTemplateInfo.Font = new System.Drawing.Font("宋体", 10.2F);
            this.labelTemplateInfo.Location = new System.Drawing.Point(143, 279);
            this.labelTemplateInfo.Name = "labelTemplateInfo";
            this.labelTemplateInfo.Size = new System.Drawing.Size(628, 88);
            this.labelTemplateInfo.TabIndex = 14;
            this.labelTemplateInfo.Text = "未选择模板";
            this.labelTemplateInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // buttonRefreshTemplate
            // 
            this.buttonRefreshTemplate.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRefreshTemplate.Font = new System.Drawing.Font("宋体", 10.8F);
            this.buttonRefreshTemplate.Location = new System.Drawing.Point(801, 301);
            this.buttonRefreshTemplate.Name = "buttonRefreshTemplate";
            this.buttonRefreshTemplate.Size = new System.Drawing.Size(106, 44);
            this.buttonRefreshTemplate.TabIndex = 15;
            this.buttonRefreshTemplate.Text = "刷新";
            this.buttonRefreshTemplate.UseVisualStyleBackColor = true;
            this.buttonRefreshTemplate.Click += new System.EventHandler(this.buttonRefreshTemplate_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel1.SetColumnSpan(this.buttonSave, 4);
            this.buttonSave.Font = new System.Drawing.Font("宋体", 10.8F);
            this.buttonSave.Location = new System.Drawing.Point(421, 519);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(92, 48);
            this.buttonSave.TabIndex = 16;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // openFileDialogTemplate
            // 
            this.openFileDialogTemplate.Filter = "无监督模板|*.tdunsup";
            this.openFileDialogTemplate.Title = "选择无监督模板";
            // 
            // ParamFormUnsupervisedDetection
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(938, 624);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "ParamFormUnsupervisedDetection";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "无监督检测";
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreshold)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownInferenceBatchSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownMiniArea)).EndInit();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelInputImage;
        private TDJS_Vision.Node.NodeSubscription nodeSubscription1;
        private System.Windows.Forms.Label labelTemplate;
        private System.Windows.Forms.TextBox textBoxTemplatePath;
        private System.Windows.Forms.Button buttonSelectTemplate;
        private System.Windows.Forms.Label labelThreshold;
        private System.Windows.Forms.NumericUpDown numericUpDownThreshold;
        private System.Windows.Forms.Label labelInferenceBatchSize;
        private System.Windows.Forms.NumericUpDown numericUpDownInferenceBatchSize;
        private System.Windows.Forms.Label labelMiniArea;
        private System.Windows.Forms.NumericUpDown numericUpDownMiniArea;
        private System.Windows.Forms.Label labelMaxBoxes;
        private System.Windows.Forms.TextBox textBoxMaxBoxes;
        private System.Windows.Forms.Label labelTemplateInfoTitle;
        private System.Windows.Forms.Label labelTemplateInfo;
        private System.Windows.Forms.Button buttonRefreshTemplate;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.OpenFileDialog openFileDialogTemplate;
    }
}
