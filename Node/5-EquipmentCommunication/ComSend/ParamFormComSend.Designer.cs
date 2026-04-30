namespace TDJS_Vision.Node._5_EquipmentCommunication.ComSend
{
    partial class ParamFormComSend
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParamFormComSend));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.瞳达光源控制命令ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.光源全部关闭ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.光源全部打开ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.通道1光源设为255ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.通道2光源设为255ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.通道3光源设为255ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.通道4光源设为255ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxComList = new System.Windows.Forms.ComboBox();
            this.textBoxCMD = new System.Windows.Forms.TextBox();
            this.buttonTest = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxEncoding = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49.05956F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.94044F));
            this.tableLayoutPanel1.ContextMenuStrip = this.contextMenuStrip1;
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxComList, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.textBoxCMD, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.buttonTest, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonSave, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxEncoding, 1, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(723, 288);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.瞳达光源控制命令ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(169, 28);
            // 
            // 瞳达光源控制命令ToolStripMenuItem
            // 
            this.瞳达光源控制命令ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.光源全部关闭ToolStripMenuItem,
            this.光源全部打开ToolStripMenuItem,
            this.通道1光源设为255ToolStripMenuItem,
            this.通道2光源设为255ToolStripMenuItem,
            this.通道3光源设为255ToolStripMenuItem,
            this.通道4光源设为255ToolStripMenuItem});
            this.瞳达光源控制命令ToolStripMenuItem.Name = "瞳达光源控制命令ToolStripMenuItem";
            this.瞳达光源控制命令ToolStripMenuItem.Size = new System.Drawing.Size(168, 24);
            this.瞳达光源控制命令ToolStripMenuItem.Text = "光源控制指令";
            // 
            // 光源全部关闭ToolStripMenuItem
            // 
            this.光源全部关闭ToolStripMenuItem.Name = "光源全部关闭ToolStripMenuItem";
            this.光源全部关闭ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.光源全部关闭ToolStripMenuItem.Text = "光源全部关闭";
            this.光源全部关闭ToolStripMenuItem.Click += new System.EventHandler(this.光源全部关闭ToolStripMenuItem_Click);
            // 
            // 光源全部打开ToolStripMenuItem
            // 
            this.光源全部打开ToolStripMenuItem.Name = "光源全部打开ToolStripMenuItem";
            this.光源全部打开ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.光源全部打开ToolStripMenuItem.Text = "光源全部打开";
            this.光源全部打开ToolStripMenuItem.Click += new System.EventHandler(this.光源全部打开ToolStripMenuItem_Click);
            // 
            // 通道1光源设为255ToolStripMenuItem
            // 
            this.通道1光源设为255ToolStripMenuItem.Name = "通道1光源设为255ToolStripMenuItem";
            this.通道1光源设为255ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.通道1光源设为255ToolStripMenuItem.Text = "通道1光源设为255";
            this.通道1光源设为255ToolStripMenuItem.Click += new System.EventHandler(this.通道1光源设为255ToolStripMenuItem_Click);
            // 
            // 通道2光源设为255ToolStripMenuItem
            // 
            this.通道2光源设为255ToolStripMenuItem.Name = "通道2光源设为255ToolStripMenuItem";
            this.通道2光源设为255ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.通道2光源设为255ToolStripMenuItem.Text = "通道2光源设为0";
            this.通道2光源设为255ToolStripMenuItem.Click += new System.EventHandler(this.通道2光源设为0ToolStripMenuItem_Click);
            // 
            // 通道3光源设为255ToolStripMenuItem
            // 
            this.通道3光源设为255ToolStripMenuItem.Name = "通道3光源设为255ToolStripMenuItem";
            this.通道3光源设为255ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.通道3光源设为255ToolStripMenuItem.Text = "通道3光源设为255";
            this.通道3光源设为255ToolStripMenuItem.Click += new System.EventHandler(this.通道3光源设为255ToolStripMenuItem_Click);
            // 
            // 通道4光源设为255ToolStripMenuItem
            // 
            this.通道4光源设为255ToolStripMenuItem.Name = "通道4光源设为255ToolStripMenuItem";
            this.通道4光源设为255ToolStripMenuItem.Size = new System.Drawing.Size(218, 26);
            this.通道4光源设为255ToolStripMenuItem.Text = "通道4光源设为0";
            this.通道4光源设为255ToolStripMenuItem.Click += new System.EventHandler(this.通道4光源设为0ToolStripMenuItem_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(137, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "选择串口";
            // 
            // comboBoxComList
            // 
            this.comboBoxComList.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxComList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxComList.FormattingEnabled = true;
            this.comboBoxComList.Location = new System.Drawing.Point(439, 15);
            this.comboBoxComList.Name = "comboBoxComList";
            this.comboBoxComList.Size = new System.Drawing.Size(198, 25);
            this.comboBoxComList.TabIndex = 1;
            // 
            // textBoxCMD
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.textBoxCMD, 2);
            this.textBoxCMD.ContextMenuStrip = this.contextMenuStrip1;
            this.textBoxCMD.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxCMD.Location = new System.Drawing.Point(30, 115);
            this.textBoxCMD.Margin = new System.Windows.Forms.Padding(30, 3, 30, 3);
            this.textBoxCMD.Multiline = true;
            this.textBoxCMD.Name = "textBoxCMD";
            this.textBoxCMD.Size = new System.Drawing.Size(663, 106);
            this.textBoxCMD.TabIndex = 5;
            // 
            // buttonTest
            // 
            this.buttonTest.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonTest.Location = new System.Drawing.Point(133, 237);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(88, 38);
            this.buttonTest.TabIndex = 4;
            this.buttonTest.Text = "测试";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Click += new System.EventHandler(this.buttonTest_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSave.Location = new System.Drawing.Point(494, 237);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(88, 38);
            this.buttonSave.TabIndex = 4;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSvae_Click);
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(137, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 18);
            this.label2.TabIndex = 0;
            this.label2.Text = "选择编码";
            // 
            // comboBoxEncoding
            // 
            this.comboBoxEncoding.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxEncoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxEncoding.FormattingEnabled = true;
            this.comboBoxEncoding.Items.AddRange(new object[] {
            "ASCII",
            "UTF8",
            "Unicode",
            "BigEndianUnicode"});
            this.comboBoxEncoding.Location = new System.Drawing.Point(439, 72);
            this.comboBoxEncoding.Name = "comboBoxEncoding";
            this.comboBoxEncoding.Size = new System.Drawing.Size(198, 25);
            this.comboBoxEncoding.TabIndex = 1;
            // 
            // ParamFormComSend
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(727, 322);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormComSend";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "串口发送";
            this.Shown += new System.EventHandler(this.ParamFormPlcWrite_Shown);
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxComList;
        private System.Windows.Forms.Button buttonTest;
        private System.Windows.Forms.TextBox textBoxCMD;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 瞳达光源控制命令ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 通道1光源设为255ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 通道2光源设为255ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 通道3光源设为255ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 通道4光源设为255ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 光源全部关闭ToolStripMenuItem;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBoxEncoding;
        private System.Windows.Forms.ToolStripMenuItem 光源全部打开ToolStripMenuItem;
    }
}