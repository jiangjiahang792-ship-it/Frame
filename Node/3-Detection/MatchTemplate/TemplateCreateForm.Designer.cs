namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    partial class TemplateCreateForm
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
            if (disposing)
            {
                DisposeImages();
                if (components != null)
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
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.tableSource = new System.Windows.Forms.TableLayoutPanel();
            this.flowSourceToolbar = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonResetRoi = new System.Windows.Forms.Button();
            this.buttonConfirmRoi = new System.Windows.Forms.Button();
            this.buttonGenerateTemplate = new System.Windows.Forms.Button();
            this.labelSourceStatus = new System.Windows.Forms.Label();
            this.showImageControlSource = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.groupBoxTemplate = new System.Windows.Forms.GroupBox();
            this.tableTemplate = new System.Windows.Forms.TableLayoutPanel();
            this.labelTemplateStatus = new System.Windows.Forms.Label();
            this.pictureBoxTemplate = new System.Windows.Forms.PictureBox();
            this.panelTemplateTools = new System.Windows.Forms.Panel();
            this.buttonRestoreTemplate = new System.Windows.Forms.Button();
            this.numericBrushSize = new System.Windows.Forms.NumericUpDown();
            this.labelBrushSize = new System.Windows.Forms.Label();
            this.checkBoxEraseTemplate = new System.Windows.Forms.CheckBox();
            this.panelFooter = new System.Windows.Forms.Panel();
            this.buttonOk = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.groupBoxSource.SuspendLayout();
            this.tableSource.SuspendLayout();
            this.flowSourceToolbar.SuspendLayout();
            this.groupBoxTemplate.SuspendLayout();
            this.tableTemplate.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTemplate)).BeginInit();
            this.panelTemplateTools.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericBrushSize)).BeginInit();
            this.panelFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.splitContainerMain, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelFooter, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1060, 700);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(12, 12);
            this.splitContainerMain.Margin = new System.Windows.Forms.Padding(12);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.groupBoxSource);
            this.splitContainerMain.Panel1MinSize = 460;
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxTemplate);
            this.splitContainerMain.Panel2MinSize = 360;
            this.splitContainerMain.Size = new System.Drawing.Size(1036, 622);
            this.splitContainerMain.SplitterDistance = 600;
            this.splitContainerMain.TabIndex = 0;
            // 
            // groupBoxSource
            // 
            this.groupBoxSource.Controls.Add(this.tableSource);
            this.groupBoxSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSource.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Padding = new System.Windows.Forms.Padding(8);
            this.groupBoxSource.Size = new System.Drawing.Size(600, 622);
            this.groupBoxSource.TabIndex = 0;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "模板区域";
            // 
            // tableSource
            // 
            this.tableSource.ColumnCount = 1;
            this.tableSource.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableSource.Controls.Add(this.flowSourceToolbar, 0, 0);
            this.tableSource.Controls.Add(this.labelSourceStatus, 0, 1);
            this.tableSource.Controls.Add(this.showImageControlSource, 0, 2);
            this.tableSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableSource.Location = new System.Drawing.Point(8, 32);
            this.tableSource.Name = "tableSource";
            this.tableSource.RowCount = 3;
            this.tableSource.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tableSource.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableSource.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableSource.Size = new System.Drawing.Size(584, 582);
            this.tableSource.TabIndex = 0;
            // 
            // flowSourceToolbar
            // 
            this.flowSourceToolbar.Controls.Add(this.buttonResetRoi);
            this.flowSourceToolbar.Controls.Add(this.buttonConfirmRoi);
            this.flowSourceToolbar.Controls.Add(this.buttonGenerateTemplate);
            this.flowSourceToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowSourceToolbar.Location = new System.Drawing.Point(0, 0);
            this.flowSourceToolbar.Margin = new System.Windows.Forms.Padding(0);
            this.flowSourceToolbar.Name = "flowSourceToolbar";
            this.flowSourceToolbar.Padding = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.flowSourceToolbar.Size = new System.Drawing.Size(584, 44);
            this.flowSourceToolbar.TabIndex = 0;
            // 
            // buttonResetRoi
            // 
            this.buttonResetRoi.Location = new System.Drawing.Point(3, 6);
            this.buttonResetRoi.Name = "buttonResetRoi";
            this.buttonResetRoi.Size = new System.Drawing.Size(132, 32);
            this.buttonResetRoi.TabIndex = 0;
            this.buttonResetRoi.Text = "绘制ROI";
            this.buttonResetRoi.UseVisualStyleBackColor = true;
            this.buttonResetRoi.Click += new System.EventHandler(this.buttonResetRoi_Click);
            // 
            // buttonConfirmRoi
            // 
            this.buttonConfirmRoi.Location = new System.Drawing.Point(141, 6);
            this.buttonConfirmRoi.Name = "buttonConfirmRoi";
            this.buttonConfirmRoi.Size = new System.Drawing.Size(112, 32);
            this.buttonConfirmRoi.TabIndex = 1;
            this.buttonConfirmRoi.Text = "确定ROI";
            this.buttonConfirmRoi.UseVisualStyleBackColor = true;
            this.buttonConfirmRoi.Click += new System.EventHandler(this.buttonConfirmRoi_Click);
            // 
            // buttonGenerateTemplate
            // 
            this.buttonGenerateTemplate.Location = new System.Drawing.Point(259, 6);
            this.buttonGenerateTemplate.Name = "buttonGenerateTemplate";
            this.buttonGenerateTemplate.Size = new System.Drawing.Size(132, 32);
            this.buttonGenerateTemplate.TabIndex = 2;
            this.buttonGenerateTemplate.Text = "创建模板";
            this.buttonGenerateTemplate.UseVisualStyleBackColor = true;
            this.buttonGenerateTemplate.Click += new System.EventHandler(this.buttonGenerateTemplate_Click);
            // 
            // labelSourceStatus
            // 
            this.labelSourceStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelSourceStatus.AutoSize = true;
            this.labelSourceStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelSourceStatus.Location = new System.Drawing.Point(3, 49);
            this.labelSourceStatus.Name = "labelSourceStatus";
            this.labelSourceStatus.Size = new System.Drawing.Size(280, 24);
            this.labelSourceStatus.TabIndex = 1;
            this.labelSourceStatus.Text = "点击“绘制ROI”后调整模板区域。";
            // 
            // showImageControlSource
            // 
            this.showImageControlSource.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControlSource.BackgroundImageCustom = null;
            this.showImageControlSource.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControlSource.BackgroundTransparency = 1F;
            this.showImageControlSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControlSource.Location = new System.Drawing.Point(3, 81);
            this.showImageControlSource.Name = "showImageControlSource";
            this.showImageControlSource.RoiColor = System.Drawing.Color.Lime;
            this.showImageControlSource.ShowCheckerBackground = false;
            this.showImageControlSource.ShowPixelInfo = false;
            this.showImageControlSource.Size = new System.Drawing.Size(578, 498);
            this.showImageControlSource.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControlSource.TabIndex = 2;
            // 
            // groupBoxTemplate
            // 
            this.groupBoxTemplate.Controls.Add(this.tableTemplate);
            this.groupBoxTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxTemplate.Location = new System.Drawing.Point(0, 0);
            this.groupBoxTemplate.Name = "groupBoxTemplate";
            this.groupBoxTemplate.Padding = new System.Windows.Forms.Padding(8);
            this.groupBoxTemplate.Size = new System.Drawing.Size(432, 622);
            this.groupBoxTemplate.TabIndex = 0;
            this.groupBoxTemplate.TabStop = false;
            this.groupBoxTemplate.Text = "轮廓编辑";
            // 
            // tableTemplate
            // 
            this.tableTemplate.ColumnCount = 1;
            this.tableTemplate.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplate.Controls.Add(this.labelTemplateStatus, 0, 0);
            this.tableTemplate.Controls.Add(this.pictureBoxTemplate, 0, 1);
            this.tableTemplate.Controls.Add(this.panelTemplateTools, 0, 2);
            this.tableTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableTemplate.Location = new System.Drawing.Point(8, 32);
            this.tableTemplate.Name = "tableTemplate";
            this.tableTemplate.RowCount = 3;
            this.tableTemplate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableTemplate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableTemplate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            this.tableTemplate.Size = new System.Drawing.Size(416, 582);
            this.tableTemplate.TabIndex = 0;
            // 
            // labelTemplateStatus
            // 
            this.labelTemplateStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelTemplateStatus.AutoSize = true;
            this.labelTemplateStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(68)))), ((int)(((byte)(76)))), ((int)(((byte)(89)))));
            this.labelTemplateStatus.Location = new System.Drawing.Point(3, 5);
            this.labelTemplateStatus.Name = "labelTemplateStatus";
            this.labelTemplateStatus.Size = new System.Drawing.Size(100, 24);
            this.labelTemplateStatus.TabIndex = 0;
            this.labelTemplateStatus.Text = "未生成模板";
            // 
            // pictureBoxTemplate
            // 
            this.pictureBoxTemplate.BackColor = System.Drawing.Color.Gainsboro;
            this.pictureBoxTemplate.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxTemplate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxTemplate.Location = new System.Drawing.Point(3, 37);
            this.pictureBoxTemplate.Name = "pictureBoxTemplate";
            this.pictureBoxTemplate.Size = new System.Drawing.Size(410, 430);
            this.pictureBoxTemplate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxTemplate.TabIndex = 1;
            this.pictureBoxTemplate.TabStop = false;
            this.pictureBoxTemplate.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pictureBoxTemplate_MouseDown);
            this.pictureBoxTemplate.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBoxTemplate_MouseMove);
            this.pictureBoxTemplate.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pictureBoxTemplate_MouseUp);
            // 
            // panelTemplateTools
            // 
            this.panelTemplateTools.Controls.Add(this.buttonRestoreTemplate);
            this.panelTemplateTools.Controls.Add(this.numericBrushSize);
            this.panelTemplateTools.Controls.Add(this.labelBrushSize);
            this.panelTemplateTools.Controls.Add(this.checkBoxEraseTemplate);
            this.panelTemplateTools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTemplateTools.Location = new System.Drawing.Point(3, 473);
            this.panelTemplateTools.Name = "panelTemplateTools";
            this.panelTemplateTools.Size = new System.Drawing.Size(410, 106);
            this.panelTemplateTools.TabIndex = 2;
            // 
            // buttonRestoreTemplate
            // 
            this.buttonRestoreTemplate.Enabled = false;
            this.buttonRestoreTemplate.Location = new System.Drawing.Point(3, 56);
            this.buttonRestoreTemplate.Name = "buttonRestoreTemplate";
            this.buttonRestoreTemplate.Size = new System.Drawing.Size(110, 32);
            this.buttonRestoreTemplate.TabIndex = 3;
            this.buttonRestoreTemplate.Text = "还原";
            this.buttonRestoreTemplate.UseVisualStyleBackColor = true;
            this.buttonRestoreTemplate.Click += new System.EventHandler(this.buttonRestoreTemplate_Click);
            // 
            // numericBrushSize
            // 
            this.numericBrushSize.Location = new System.Drawing.Point(230, 13);
            this.numericBrushSize.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericBrushSize.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericBrushSize.Name = "numericBrushSize";
            this.numericBrushSize.Size = new System.Drawing.Size(72, 31);
            this.numericBrushSize.TabIndex = 2;
            this.numericBrushSize.Value = new decimal(new int[] {
            12,
            0,
            0,
            0});
            // 
            // labelBrushSize
            // 
            this.labelBrushSize.AutoSize = true;
            this.labelBrushSize.Location = new System.Drawing.Point(146, 16);
            this.labelBrushSize.Name = "labelBrushSize";
            this.labelBrushSize.Size = new System.Drawing.Size(64, 24);
            this.labelBrushSize.TabIndex = 1;
            this.labelBrushSize.Text = "笔刷";
            // 
            // checkBoxEraseTemplate
            // 
            this.checkBoxEraseTemplate.AutoSize = true;
            this.checkBoxEraseTemplate.Location = new System.Drawing.Point(3, 15);
            this.checkBoxEraseTemplate.Name = "checkBoxEraseTemplate";
            this.checkBoxEraseTemplate.Size = new System.Drawing.Size(126, 28);
            this.checkBoxEraseTemplate.TabIndex = 0;
            this.checkBoxEraseTemplate.Text = "涂抹消除";
            this.checkBoxEraseTemplate.UseVisualStyleBackColor = true;
            this.checkBoxEraseTemplate.CheckedChanged += new System.EventHandler(this.checkBoxEraseTemplate_CheckedChanged);
            // 
            // panelFooter
            // 
            this.panelFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(232)))), ((int)(((byte)(236)))));
            this.panelFooter.Controls.Add(this.buttonOk);
            this.panelFooter.Controls.Add(this.buttonCancel);
            this.panelFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFooter.Location = new System.Drawing.Point(0, 646);
            this.panelFooter.Margin = new System.Windows.Forms.Padding(0);
            this.panelFooter.Name = "panelFooter";
            this.panelFooter.Size = new System.Drawing.Size(1060, 54);
            this.panelFooter.TabIndex = 1;
            // 
            // buttonOk
            // 
            this.buttonOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOk.Enabled = false;
            this.buttonOk.Location = new System.Drawing.Point(838, 9);
            this.buttonOk.Name = "buttonOk";
            this.buttonOk.Size = new System.Drawing.Size(96, 36);
            this.buttonOk.TabIndex = 0;
            this.buttonOk.Text = "确定";
            this.buttonOk.UseVisualStyleBackColor = true;
            this.buttonOk.Click += new System.EventHandler(this.buttonOk_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(946, 9);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(96, 36);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // TemplateCreateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1060, 700);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F);
            this.MinimizeBox = false;
            this.Name = "TemplateCreateForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "创建/编辑模板";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.groupBoxSource.ResumeLayout(false);
            this.tableSource.ResumeLayout(false);
            this.tableSource.PerformLayout();
            this.flowSourceToolbar.ResumeLayout(false);
            this.groupBoxTemplate.ResumeLayout(false);
            this.tableTemplate.ResumeLayout(false);
            this.tableTemplate.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxTemplate)).EndInit();
            this.panelTemplateTools.ResumeLayout(false);
            this.panelTemplateTools.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericBrushSize)).EndInit();
            this.panelFooter.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.GroupBox groupBoxSource;
        private System.Windows.Forms.TableLayoutPanel tableSource;
        private System.Windows.Forms.FlowLayoutPanel flowSourceToolbar;
        private System.Windows.Forms.Button buttonResetRoi;
        private System.Windows.Forms.Button buttonConfirmRoi;
        private System.Windows.Forms.Button buttonGenerateTemplate;
        private System.Windows.Forms.Label labelSourceStatus;
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControlSource;
        private System.Windows.Forms.GroupBox groupBoxTemplate;
        private System.Windows.Forms.TableLayoutPanel tableTemplate;
        private System.Windows.Forms.Label labelTemplateStatus;
        private System.Windows.Forms.PictureBox pictureBoxTemplate;
        private System.Windows.Forms.Panel panelTemplateTools;
        private System.Windows.Forms.CheckBox checkBoxEraseTemplate;
        private System.Windows.Forms.Label labelBrushSize;
        private System.Windows.Forms.NumericUpDown numericBrushSize;
        private System.Windows.Forms.Button buttonRestoreTemplate;
        private System.Windows.Forms.Panel panelFooter;
        private System.Windows.Forms.Button buttonOk;
        private System.Windows.Forms.Button buttonCancel;
    }
}
