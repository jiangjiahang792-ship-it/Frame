using TDJS_Vision.Forms.DispShowImage;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    partial class NodeParamFormImageCrop
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
                TDJS_Vision.Forms.SolRunParam.SolRunParamControl.RefreshParamView -= SolRunParamControl_RefreshParamView;
                if (showImageControl1 != null)
                    showImageControl1.DynamicRoiChanged -= showImageControl1_DynamicRoiChanged;
                components?.Dispose();
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormImageCrop));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tableLayoutPanel5 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this.button3 = new System.Windows.Forms.Button();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.button2 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.uiSwitch1 = new Sunny.UI.UISwitch();
            this.buttonClearRois = new System.Windows.Forms.Button();
            this.labelRoiHelp = new System.Windows.Forms.Label();
            this.groupBoxRoiSize = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelRoiSize = new System.Windows.Forms.TableLayoutPanel();
            this.labelRoiWidth = new System.Windows.Forms.Label();
            this.labelRoiHeight = new System.Windows.Forms.Label();
            this.numericRoiWidth = new System.Windows.Forms.NumericUpDown();
            this.numericRoiHeight = new System.Windows.Forms.NumericUpDown();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tableLayoutPanel5.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.groupBoxRoiSize.SuspendLayout();
            this.tableLayoutPanelRoiSize.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiHeight)).BeginInit();
            this.SuspendLayout();
            //
            // tableLayoutPanel1
            //
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanel1.Controls.Add(this.panel1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.showImageControl1, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1022, 446);
            this.tableLayoutPanel1.TabIndex = 0;
            //
            // panel1
            //
            this.panel1.Controls.Add(this.tableLayoutPanel5);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(667, 2);
            this.panel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(352, 442);
            this.panel1.TabIndex = 1;
            //
            // tableLayoutPanel5
            //
            this.tableLayoutPanel5.ColumnCount = 2;
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel5.Controls.Add(this.groupBox2, 0, 0);
            this.tableLayoutPanel5.Controls.Add(this.button2, 0, 5);
            this.tableLayoutPanel5.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel5.Controls.Add(this.uiSwitch1, 1, 1);
            this.tableLayoutPanel5.Controls.Add(this.groupBoxRoiSize, 0, 2);
            this.tableLayoutPanel5.SetColumnSpan(this.groupBoxRoiSize, 2);
            this.tableLayoutPanel5.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel5.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel5.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel5.Name = "tableLayoutPanel5";
            this.tableLayoutPanel5.RowCount = 6;
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 55F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 45F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel5.Size = new System.Drawing.Size(352, 478);
            this.tableLayoutPanel5.TabIndex = 3;
            //
            // groupBox2
            //
            this.tableLayoutPanel5.SetColumnSpan(this.groupBox2, 2);
            this.groupBox2.Controls.Add(this.tableLayoutPanel4);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBox2.Location = new System.Drawing.Point(3, 2);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox2.Size = new System.Drawing.Size(346, 154);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "订阅图像";
            //
            // tableLayoutPanel4
            //
            this.tableLayoutPanel4.ColumnCount = 1;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel4.Controls.Add(this.button3, 0, 1);
            this.tableLayoutPanel4.Controls.Add(this.nodeSubscription1, 0, 0);
            this.tableLayoutPanel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel4.Location = new System.Drawing.Point(3, 22);
            this.tableLayoutPanel4.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 2;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(340, 130);
            this.tableLayoutPanel4.TabIndex = 0;
            //
            // button3
            //
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button3.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button3.Location = new System.Drawing.Point(118, 81);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(104, 33);
            this.button3.TabIndex = 0;
            this.button3.Text = "刷新图像";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.nodeSubscription1.Location = new System.Drawing.Point(3, 6);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(231, 50);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(334, 52);
            this.nodeSubscription1.TabIndex = 0;
            // 
            // button2
            // 
            this.button2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel5.SetColumnSpan(this.button2, 2);
            this.button2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button2.Location = new System.Drawing.Point(137, 425);
            this.button2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(78, 32);
            this.button2.TabIndex = 0;
            this.button2.Text = "保存";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(34, 198);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 18);
            this.label1.TabIndex = 3;
            this.label1.Text = "ROI是否启用";
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.showImageControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControl1.Location = new System.Drawing.Point(3, 2);
            this.showImageControl1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.Size = new System.Drawing.Size(658, 442);
            this.showImageControl1.TabIndex = 2;
            this.showImageControl1.EnableRectangleRoiDrawing = true;
            // 
            // uiSwitch1
            // 
            this.uiSwitch1.ActiveText = "启用";
            this.uiSwitch1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.uiSwitch1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSwitch1.InActiveText = "禁用";
            this.uiSwitch1.Location = new System.Drawing.Point(218, 193);
            this.uiSwitch1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSwitch1.Name = "uiSwitch1";
            this.uiSwitch1.Size = new System.Drawing.Size(92, 29);
            this.uiSwitch1.TabIndex = 4;
            this.uiSwitch1.Text = "启用裁剪";
            //
            // groupBoxRoiSize
            //
            this.groupBoxRoiSize.Controls.Add(this.tableLayoutPanelRoiSize);
            this.groupBoxRoiSize.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxRoiSize.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxRoiSize.Location = new System.Drawing.Point(3, 207);
            this.groupBoxRoiSize.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBoxRoiSize.Name = "groupBoxRoiSize";
            this.groupBoxRoiSize.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBoxRoiSize.Size = new System.Drawing.Size(346, 82);
            this.groupBoxRoiSize.TabIndex = 5;
            this.groupBoxRoiSize.TabStop = false;
            this.groupBoxRoiSize.Text = "ROI宽高";
            //
            // tableLayoutPanelRoiSize
            //
            this.tableLayoutPanelRoiSize.ColumnCount = 4;
            this.tableLayoutPanelRoiSize.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutPanelRoiSize.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelRoiSize.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this.tableLayoutPanelRoiSize.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelRoiSize.Controls.Add(this.labelRoiWidth, 0, 0);
            this.tableLayoutPanelRoiSize.Controls.Add(this.numericRoiWidth, 1, 0);
            this.tableLayoutPanelRoiSize.Controls.Add(this.labelRoiHeight, 2, 0);
            this.tableLayoutPanelRoiSize.Controls.Add(this.numericRoiHeight, 3, 0);
            this.tableLayoutPanelRoiSize.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoiSize.Location = new System.Drawing.Point(3, 22);
            this.tableLayoutPanelRoiSize.Name = "tableLayoutPanelRoiSize";
            this.tableLayoutPanelRoiSize.RowCount = 1;
            this.tableLayoutPanelRoiSize.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoiSize.Size = new System.Drawing.Size(340, 58);
            this.tableLayoutPanelRoiSize.TabIndex = 0;
            //
            // labelRoiWidth
            //
            this.labelRoiWidth.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiWidth.Location = new System.Drawing.Point(3, 0);
            this.labelRoiWidth.Name = "labelRoiWidth";
            this.labelRoiWidth.Size = new System.Drawing.Size(66, 58);
            this.labelRoiWidth.TabIndex = 0;
            this.labelRoiWidth.Text = "宽度";
            this.labelRoiWidth.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // labelRoiHeight
            //
            this.labelRoiHeight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiHeight.Location = new System.Drawing.Point(173, 0);
            this.labelRoiHeight.Name = "labelRoiHeight";
            this.labelRoiHeight.Size = new System.Drawing.Size(66, 58);
            this.labelRoiHeight.TabIndex = 2;
            this.labelRoiHeight.Text = "高度";
            this.labelRoiHeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // numericRoiWidth
            //
            this.numericRoiWidth.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.numericRoiWidth.DecimalPlaces = 1;
            this.numericRoiWidth.Location = new System.Drawing.Point(75, 15);
            this.numericRoiWidth.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericRoiWidth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericRoiWidth.Name = "numericRoiWidth";
            this.numericRoiWidth.Size = new System.Drawing.Size(92, 27);
            this.numericRoiWidth.TabIndex = 1;
            this.numericRoiWidth.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericRoiWidth.ValueChanged += new System.EventHandler(this.RoiSizeNumeric_ValueChanged);
            //
            // numericRoiHeight
            //
            this.numericRoiHeight.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.numericRoiHeight.DecimalPlaces = 1;
            this.numericRoiHeight.Location = new System.Drawing.Point(245, 15);
            this.numericRoiHeight.Maximum = new decimal(new int[] {
            1000000,
            0,
            0,
            0});
            this.numericRoiHeight.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericRoiHeight.Name = "numericRoiHeight";
            this.numericRoiHeight.Size = new System.Drawing.Size(92, 27);
            this.numericRoiHeight.TabIndex = 3;
            this.numericRoiHeight.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericRoiHeight.ValueChanged += new System.EventHandler(this.RoiSizeNumeric_ValueChanged);
            //
            // 操作说明及清空按钮均保留在设计器中，便于直接查看和调整布局。
            this.tableLayoutPanel5.Controls.Add(this.labelRoiHelp, 0, 3);
            this.tableLayoutPanel5.SetColumnSpan(this.labelRoiHelp, 2);
            this.labelRoiHelp.Name = "labelRoiHelp";
            this.labelRoiHelp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelRoiHelp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.labelRoiHelp.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.labelRoiHelp.Text = "左键拖动绘制一个ROI，已存在时不能再绘制第二个。\r\n框内拖动；角点缩放；右上弧形手柄旋转。\r\n拖动ROI会实时同步宽高，也可手动设置宽高。\r\n自动摆正，越界部分按边缘像素补齐。";
            this.tableLayoutPanel5.Controls.Add(this.buttonClearRois, 0, 4);
            this.tableLayoutPanel5.SetColumnSpan(this.buttonClearRois, 2);
            this.buttonClearRois.Name = "buttonClearRois";
            this.buttonClearRois.Text = "清空ROI";
            this.buttonClearRois.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonClearRois.Size = new System.Drawing.Size(100, 30);
            this.buttonClearRois.UseVisualStyleBackColor = true;
            this.buttonClearRois.Click += new System.EventHandler(this.buttonClearRois_Click);
            // NodeParamFormImageCrop
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1026, 560);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormImageCrop";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "图像裁剪";
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.tableLayoutPanel5.ResumeLayout(false);
            this.tableLayoutPanel5.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.tableLayoutPanel4.ResumeLayout(false);
            this.groupBoxRoiSize.ResumeLayout(false);
            this.tableLayoutPanelRoiSize.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericRoiHeight)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button2;
        /// <summary>常用图像显示控件，提供矩形绘制和旋转编辑。</summary>
        private ShowImageControl showImageControl1;
        /// <summary>清空全部裁剪区域按钮。</summary>
        private System.Windows.Forms.Button buttonClearRois;
        /// <summary>矩形编辑及裁剪规则提示。</summary>
        private System.Windows.Forms.Label labelRoiHelp;
        /// <summary>ROI 宽高参数分组。</summary>
        private System.Windows.Forms.GroupBox groupBoxRoiSize;
        /// <summary>ROI 宽高参数布局。</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoiSize;
        /// <summary>ROI 宽度标签。</summary>
        private System.Windows.Forms.Label labelRoiWidth;
        /// <summary>ROI 高度标签。</summary>
        private System.Windows.Forms.Label labelRoiHeight;
        /// <summary>ROI 宽度参数控件。</summary>
        private System.Windows.Forms.NumericUpDown numericRoiWidth;
        /// <summary>ROI 高度参数控件。</summary>
        private System.Windows.Forms.NumericUpDown numericRoiHeight;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private NodeSubscription nodeSubscription1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel5;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Label label1;
        private Sunny.UI.UISwitch uiSwitch1;
    }
}
