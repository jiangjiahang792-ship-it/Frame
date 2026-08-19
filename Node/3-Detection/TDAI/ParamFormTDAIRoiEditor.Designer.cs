namespace TDJS_Vision.Node._3_Detection.TDAI
{
    partial class ParamFormTDAIRoiEditor
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
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelPrompt = new System.Windows.Forms.Label();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonConfirm = new System.Windows.Forms.Button();
            this.buttonGetCurrentImage = new System.Windows.Forms.Button();
            this.buttonClear = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.labelPrompt, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.showImageControl1, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.flowLayoutPanelButtons, 0, 2);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1096, 660);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelPrompt
            // 
            this.labelPrompt.AutoEllipsis = true;
            this.labelPrompt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelPrompt.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelPrompt.Location = new System.Drawing.Point(3, 0);
            this.labelPrompt.Name = "labelPrompt";
            this.labelPrompt.Size = new System.Drawing.Size(1090, 42);
            this.labelPrompt.TabIndex = 0;
            this.labelPrompt.Text = "在图像空白处按住鼠标左键拖拽绘制检测区域；可绘制多个 ROI；点击目标框选中后按 Del 删除对应 ROI。";
            this.labelPrompt.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundImageCustom = null;
            this.showImageControl1.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControl1.BackgroundTransparency = 1F;
            this.showImageControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControl1.EnableRectangleRoiDrawing = true;
            this.showImageControl1.Location = new System.Drawing.Point(3, 45);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.RoiColor = System.Drawing.Color.Lime;
            this.showImageControl1.ShowCheckerBackground = false;
            this.showImageControl1.ShowPixelInfo = false;
            this.showImageControl1.Size = new System.Drawing.Size(1090, 554);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 1;
            // 
            // flowLayoutPanelButtons
            // 
            this.flowLayoutPanelButtons.Controls.Add(this.buttonConfirm);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonGetCurrentImage);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonClear);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(3, 605);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(0, 8, 12, 0);
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(1090, 52);
            this.flowLayoutPanelButtons.TabIndex = 2;
            // 
            // buttonConfirm
            // 
            this.buttonConfirm.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonConfirm.Location = new System.Drawing.Point(963, 11);
            this.buttonConfirm.Name = "buttonConfirm";
            this.buttonConfirm.Size = new System.Drawing.Size(112, 36);
            this.buttonConfirm.TabIndex = 0;
            this.buttonConfirm.Text = "确定";
            this.buttonConfirm.UseVisualStyleBackColor = true;
            this.buttonConfirm.Click += new System.EventHandler(this.buttonConfirm_Click);
            // 
            // buttonGetCurrentImage
            // 
            this.buttonGetCurrentImage.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonGetCurrentImage.Location = new System.Drawing.Point(815, 11);
            this.buttonGetCurrentImage.Name = "buttonGetCurrentImage";
            this.buttonGetCurrentImage.Size = new System.Drawing.Size(142, 36);
            this.buttonGetCurrentImage.TabIndex = 1;
            this.buttonGetCurrentImage.Text = "获取当前图像";
            this.buttonGetCurrentImage.UseVisualStyleBackColor = true;
            this.buttonGetCurrentImage.Click += new System.EventHandler(this.buttonGetCurrentImage_Click);
            // 
            // buttonClear
            // 
            this.buttonClear.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonClear.Location = new System.Drawing.Point(697, 11);
            this.buttonClear.Name = "buttonClear";
            this.buttonClear.Size = new System.Drawing.Size(112, 36);
            this.buttonClear.TabIndex = 2;
            this.buttonClear.Text = "清空ROI";
            this.buttonClear.UseVisualStyleBackColor = true;
            this.buttonClear.Click += new System.EventHandler(this.buttonClear_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Font = new System.Drawing.Font("宋体", 10.5F);
            this.buttonCancel.Location = new System.Drawing.Point(579, 11);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(112, 36);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // ParamFormTDAIRoiEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 700);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimizeBox = false;
            this.Name = "ParamFormTDAIRoiEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "AI检测ROI绘制";
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        /// <summary>主布局面板。</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>绘制操作提示标签。</summary>
        private System.Windows.Forms.Label labelPrompt;
        /// <summary>ROI 绘制图像控件。</summary>
        private TDJS_Vision.Forms.DispShowImage.ShowImageControl showImageControl1;
        /// <summary>底部按钮布局面板。</summary>
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        /// <summary>确定按钮。</summary>
        private System.Windows.Forms.Button buttonConfirm;
        /// <summary>获取当前图像按钮。</summary>
        private System.Windows.Forms.Button buttonGetCurrentImage;
        /// <summary>清空 ROI 按钮。</summary>
        private System.Windows.Forms.Button buttonClear;
        /// <summary>取消按钮。</summary>
        private System.Windows.Forms.Button buttonCancel;
    }
}
