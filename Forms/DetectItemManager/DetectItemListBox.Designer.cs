namespace TDJS_Vision.Forms.DetectItemManager
{
    partial class DetectItemListBox
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

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelName = new System.Windows.Forms.Label();
            this.labelTips = new System.Windows.Forms.Label();
            this.panelUp = new System.Windows.Forms.Panel();
            this.panelDown = new System.Windows.Forms.Panel();
            this.myDataGridViewForm1 = new TDJS_Vision.Forms.SolRunParam.MyDataGridViewForm();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelUp.SuspendLayout();
            this.panelDown.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 142F));
            this.tableLayoutPanel1.Controls.Add(this.labelName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelTips, 2, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(768, 42);
            this.tableLayoutPanel1.TabIndex = 0;
            this.tableLayoutPanel1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.tableLayoutPanel1_MouseClick);
            this.tableLayoutPanel1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.labelName_MouseDoubleClick);
            // 
            // labelName
            // 
            this.labelName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelName.AutoSize = true;
            this.labelName.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelName.Location = new System.Drawing.Point(3, 12);
            this.labelName.Name = "labelName";
            this.labelName.Padding = new System.Windows.Forms.Padding(20, 0, 0, 0);
            this.labelName.Size = new System.Drawing.Size(123, 18);
            this.labelName.TabIndex = 0;
            this.labelName.Text = "配置项名称";
            this.labelName.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.labelName_MouseDoubleClick);
            // 
            // labelTips
            // 
            this.labelTips.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelTips.AutoSize = true;
            this.labelTips.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTips.Location = new System.Drawing.Point(652, 12);
            this.labelTips.Name = "labelTips";
            this.labelTips.Padding = new System.Windows.Forms.Padding(20, 0, 0, 0);
            this.labelTips.Size = new System.Drawing.Size(88, 18);
            this.labelTips.TabIndex = 0;
            this.labelTips.Text = "展开 ▶";
            this.labelTips.Click += new System.EventHandler(this.labelTips_Click);
            // 
            // panelUp
            // 
            this.panelUp.Controls.Add(this.tableLayoutPanel1);
            this.panelUp.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelUp.Location = new System.Drawing.Point(0, 0);
            this.panelUp.Name = "panelUp";
            this.panelUp.Size = new System.Drawing.Size(768, 42);
            this.panelUp.TabIndex = 2;
            // 
            // panelDown
            // 
            this.panelDown.AutoSize = true;
            this.panelDown.Controls.Add(this.myDataGridViewForm1);
            this.panelDown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDown.Location = new System.Drawing.Point(0, 42);
            this.panelDown.Name = "panelDown";
            this.panelDown.Size = new System.Drawing.Size(768, 250);
            this.panelDown.TabIndex = 3;
            this.panelDown.Visible = false;
            // 
            // myDataGridViewForm1
            // 
            this.myDataGridViewForm1.AutoSize = true;
            this.myDataGridViewForm1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.myDataGridViewForm1.Location = new System.Drawing.Point(0, 0);
            this.myDataGridViewForm1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.myDataGridViewForm1.MaximumSize = new System.Drawing.Size(1500, 500);
            this.myDataGridViewForm1.MinimumSize = new System.Drawing.Size(500, 250);
            this.myDataGridViewForm1.Name = "myDataGridViewForm1";
            this.myDataGridViewForm1.Size = new System.Drawing.Size(768, 250);
            this.myDataGridViewForm1.TabIndex = 1;
            // 
            // DetectItemListBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Controls.Add(this.panelDown);
            this.Controls.Add(this.panelUp);
            this.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "DetectItemListBox";
            this.Size = new System.Drawing.Size(768, 292);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.panelUp.ResumeLayout(false);
            this.panelDown.ResumeLayout(false);
            this.panelDown.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.Label labelTips;
        private SolRunParam.MyDataGridViewForm myDataGridViewForm1;
        private System.Windows.Forms.Panel panelUp;
        private System.Windows.Forms.Panel panelDown;
    }
}
