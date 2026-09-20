namespace TDJS_Vision
{
    partial class FormBase
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormBase));
            this.tableLayoutPanelTiltle = new System.Windows.Forms.TableLayoutPanel();
            this.labelMinBox = new System.Windows.Forms.Label();
            this.labelMaxBox = new System.Windows.Forms.Label();
            this.labelClose = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.panelRight = new System.Windows.Forms.Panel();
            this.panelButton = new System.Windows.Forms.Panel();
            this.pictureBoxIcon = new System.Windows.Forms.PictureBox();
            this.tableLayoutPanelTiltle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanelTiltle
            // 
            this.tableLayoutPanelTiltle.BackColor = System.Drawing.Color.MediumTurquoise;
            this.tableLayoutPanelTiltle.ColumnCount = 5;
            this.tableLayoutPanelTiltle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanelTiltle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTiltle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanelTiltle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanelTiltle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanelTiltle.Controls.Add(this.labelMinBox, 2, 0);
            this.tableLayoutPanelTiltle.Controls.Add(this.labelMaxBox, 3, 0);
            this.tableLayoutPanelTiltle.Controls.Add(this.labelClose, 4, 0);
            this.tableLayoutPanelTiltle.Controls.Add(this.pictureBoxIcon, 0, 0);
            this.tableLayoutPanelTiltle.Controls.Add(this.labelTitle, 1, 0);
            this.tableLayoutPanelTiltle.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanelTiltle.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelTiltle.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelTiltle.Name = "tableLayoutPanelTiltle";
            this.tableLayoutPanelTiltle.RowCount = 1;
            this.tableLayoutPanelTiltle.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 38F));
            this.tableLayoutPanelTiltle.Size = new System.Drawing.Size(900, 38);
            this.tableLayoutPanelTiltle.TabIndex = 0;
            this.tableLayoutPanelTiltle.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseDoubleClick);
            this.tableLayoutPanelTiltle.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseDown);
            this.tableLayoutPanelTiltle.MouseMove += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseMove);
            this.tableLayoutPanelTiltle.MouseUp += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseUp);
            // 
            // labelMinBox
            // 
            this.labelMinBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelMinBox.AutoSize = true;
            this.labelMinBox.BackColor = System.Drawing.Color.MediumTurquoise;
            this.labelMinBox.Font = new System.Drawing.Font("宋体", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelMinBox.Location = new System.Drawing.Point(743, 5);
            this.labelMinBox.Name = "labelMinBox";
            this.labelMinBox.Size = new System.Drawing.Size(34, 28);
            this.labelMinBox.TabIndex = 0;
            this.labelMinBox.Text = "🗕";
            this.labelMinBox.Click += new System.EventHandler(this.label2_Click);
            // 
            // labelMaxBox
            // 
            this.labelMaxBox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelMaxBox.AutoSize = true;
            this.labelMaxBox.BackColor = System.Drawing.Color.MediumTurquoise;
            this.labelMaxBox.Font = new System.Drawing.Font("宋体", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelMaxBox.Location = new System.Drawing.Point(799, 5);
            this.labelMaxBox.Name = "labelMaxBox";
            this.labelMaxBox.Size = new System.Drawing.Size(34, 28);
            this.labelMaxBox.TabIndex = 0;
            this.labelMaxBox.Text = "🗖";
            this.labelMaxBox.Click += new System.EventHandler(this.label1_Click);
            // 
            // labelClose
            // 
            this.labelClose.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelClose.AutoSize = true;
            this.labelClose.BackColor = System.Drawing.Color.MediumTurquoise;
            this.labelClose.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelClose.Location = new System.Drawing.Point(855, 8);
            this.labelClose.Name = "labelClose";
            this.labelClose.Size = new System.Drawing.Size(33, 22);
            this.labelClose.TabIndex = 0;
            this.labelClose.Text = "✖";
            this.labelClose.Click += new System.EventHandler(this.label3_Click);
            // 
            // labelTitle
            // 
            this.labelTitle.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("微软雅黑 Light", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTitle.Location = new System.Drawing.Point(59, 4);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(101, 30);
            this.labelTitle.TabIndex = 2;
            this.labelTitle.Text = "窗口标题";
            this.labelTitle.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseDoubleClick);
            // 
            // panelLeft
            // 
            this.panelLeft.BackColor = System.Drawing.Color.Teal;
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 38);
            this.panelLeft.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(2, 502);
            this.panelLeft.TabIndex = 1;
            // 
            // panelRight
            // 
            this.panelRight.BackColor = System.Drawing.Color.Teal;
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelRight.Location = new System.Drawing.Point(898, 38);
            this.panelRight.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(2, 502);
            this.panelRight.TabIndex = 1;
            // 
            // panelButton
            // 
            this.panelButton.BackColor = System.Drawing.Color.Teal;
            this.panelButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButton.Location = new System.Drawing.Point(2, 538);
            this.panelButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelButton.Name = "panelButton";
            this.panelButton.Size = new System.Drawing.Size(896, 2);
            this.panelButton.TabIndex = 1;
            // 
            // pictureBoxIcon
            // 
            this.pictureBoxIcon.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.pictureBoxIcon.Location = new System.Drawing.Point(10, 4);
            this.pictureBoxIcon.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pictureBoxIcon.Name = "pictureBoxIcon";
            this.pictureBoxIcon.Size = new System.Drawing.Size(36, 30);
            this.pictureBoxIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxIcon.TabIndex = 1;
            this.pictureBoxIcon.TabStop = false;
            this.pictureBoxIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseDoubleClick);
            // 
            // FormBase
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(900, 540);
            this.Controls.Add(this.panelButton);
            this.Controls.Add(this.panelRight);
            this.Controls.Add(this.panelLeft);
            this.Controls.Add(this.tableLayoutPanelTiltle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "FormBase";
            this.tableLayoutPanelTiltle.ResumeLayout(false);
            this.tableLayoutPanelTiltle.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxIcon)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        /// <summary>自定义标题栏，允许派生窗体在设计器中调整自适应布局。</summary>
        protected System.Windows.Forms.TableLayoutPanel tableLayoutPanelTiltle;
        private System.Windows.Forms.Label labelMinBox;
        private System.Windows.Forms.Label labelMaxBox;
        private System.Windows.Forms.Label labelClose;
        private System.Windows.Forms.PictureBox pictureBoxIcon;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel panelButton;
    }
}
