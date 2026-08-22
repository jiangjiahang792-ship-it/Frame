namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    partial class NodeParamFormColorDiscern
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
                ReleaseImageResources();
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormColorDiscern));
            this.button3 = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label3 = new System.Windows.Forms.Label();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.textBoxModelPath = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.showImageControl1 = new TDJS_Vision.Forms.DispShowImage.ShowImageControl();
            this.label8 = new System.Windows.Forms.Label();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.comboBoxDevice = new System.Windows.Forms.ComboBox();
            this.ListeningAddressTextbox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.groupBoxPositionCorrection = new System.Windows.Forms.GroupBox();
            this.checkBoxUsePositionCorrection = new System.Windows.Forms.CheckBox();
            this.nodeSubscriptionPositionCorrection = new TDJS_Vision.Node.NodeSubscription();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBoxPositionCorrection.SuspendLayout();
            this.SuspendLayout();
            // 
            // button3
            // 
            this.button3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button3.Location = new System.Drawing.Point(621, 10);
            this.button3.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(117, 46);
            this.button3.TabIndex = 4;
            this.button3.Text = "刷新图像";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRun.Location = new System.Drawing.Point(925, 320);
            this.buttonRun.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(182, 50);
            this.buttonRun.TabIndex = 1;
            this.buttonRun.Text = "执行匹配";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSave.Location = new System.Drawing.Point(796, 9);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(102, 49);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 6;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 19.06631F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.46636F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.46636F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.78058F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.21296F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.00742F));
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.nodeSubscription1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.textBoxModelPath, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.button2, 4, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 5, 1);
            this.tableLayoutPanel1.Controls.Add(this.showImageControl1, 0, 6);
            this.tableLayoutPanel1.Controls.Add(this.button3, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonSave, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.label8, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonRun, 5, 5);
            this.tableLayoutPanel1.Controls.Add(this.radioButton1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.radioButton2, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxDevice, 1, 5);
            this.tableLayoutPanel1.Controls.Add(this.ListeningAddressTextbox, 2, 5);
            this.tableLayoutPanel1.Controls.Add(this.label9, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label7, 2, 4);
            this.tableLayoutPanel1.Controls.Add(this.checkBox1, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.comboBox1, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.groupBoxPositionCorrection, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 7;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 67F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 82F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1118, 752);
            this.tableLayoutPanel1.TabIndex = 3;
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(59, 23);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 21);
            this.label3.TabIndex = 7;
            this.label3.Text = "订阅图像";
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tableLayoutPanel1.SetColumnSpan(this.nodeSubscription1, 2);
            this.nodeSubscription1.Location = new System.Drawing.Point(215, 4);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(2);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(260, 59);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(353, 59);
            this.nodeSubscription1.TabIndex = 5;
            // 
            // textBoxModelPath
            // 
            this.textBoxModelPath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel1.SetColumnSpan(this.textBoxModelPath, 3);
            this.textBoxModelPath.Location = new System.Drawing.Point(264, 85);
            this.textBoxModelPath.Margin = new System.Windows.Forms.Padding(51, 2, 3, 2);
            this.textBoxModelPath.Name = "textBoxModelPath";
            this.textBoxModelPath.Size = new System.Drawing.Size(512, 31);
            this.textBoxModelPath.TabIndex = 4;
            // 
            // button2
            // 
            this.button2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button2.Location = new System.Drawing.Point(792, 78);
            this.button2.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(110, 44);
            this.button2.TabIndex = 4;
            this.button2.Text = "选择模版";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.label1.Location = new System.Drawing.Point(922, 82);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(188, 36);
            this.label1.TabIndex = 6;
            this.label1.Text = "还没有彩色模版？点击这里创建";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // showImageControl1
            // 
            this.showImageControl1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.showImageControl1.BackgroundColorCustom = System.Drawing.Color.Black;
            this.showImageControl1.BackgroundImageCustom = null;
            this.showImageControl1.BackgroundLayoutMode = TDJS_Vision.Forms.DispShowImage.ShowImageControl.BackgroundImageLayoutMode.Fill;
            this.showImageControl1.BackgroundTransparency = 1F;
            this.tableLayoutPanel1.SetColumnSpan(this.showImageControl1, 6);
            this.showImageControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.showImageControl1.Location = new System.Drawing.Point(3, 400);
            this.showImageControl1.Name = "showImageControl1";
            this.showImageControl1.RoiColor = System.Drawing.Color.Lime;
            this.showImageControl1.ShowCheckerBackground = false;
            this.showImageControl1.Size = new System.Drawing.Size(1112, 349);
            this.showImageControl1.StaticShapeColor = System.Drawing.Color.Red;
            this.showImageControl1.TabIndex = 9;
            // 
            // label8
            // 
            this.label8.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(59, 90);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(94, 21);
            this.label8.TabIndex = 7;
            this.label8.Text = "模板路径";
            // 
            // radioButton1
            // 
            this.radioButton1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.radioButton1.Checked = true;
            this.radioButton1.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioButton1.Location = new System.Drawing.Point(45, 154);
            this.radioButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(123, 26);
            this.radioButton1.TabIndex = 18;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "固定模板";
            this.radioButton1.UseVisualStyleBackColor = true;
            this.radioButton1.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // radioButton2
            // 
            this.radioButton2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.radioButton2.AutoSize = true;
            this.radioButton2.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioButton2.Location = new System.Drawing.Point(23, 332);
            this.radioButton2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(167, 26);
            this.radioButton2.TabIndex = 19;
            this.radioButton2.Text = "通信获取模板";
            this.radioButton2.UseVisualStyleBackColor = true;
            this.radioButton2.CheckedChanged += new System.EventHandler(this.radioButton2_CheckedChanged);
            // 
            // comboBoxDevice
            // 
            this.comboBoxDevice.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxDevice.Enabled = false;
            this.comboBoxDevice.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxDevice.FormattingEnabled = true;
            this.comboBoxDevice.Location = new System.Drawing.Point(216, 330);
            this.comboBoxDevice.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxDevice.Name = "comboBoxDevice";
            this.comboBoxDevice.Size = new System.Drawing.Size(178, 30);
            this.comboBoxDevice.TabIndex = 21;
            this.comboBoxDevice.SelectedIndexChanged += new System.EventHandler(this.comboBoxDevice_SelectedIndexChanged);
            // 
            // ListeningAddressTextbox
            // 
            this.ListeningAddressTextbox.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.ListeningAddressTextbox.Location = new System.Drawing.Point(425, 329);
            this.ListeningAddressTextbox.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.ListeningAddressTextbox.Name = "ListeningAddressTextbox";
            this.ListeningAddressTextbox.Size = new System.Drawing.Size(127, 31);
            this.ListeningAddressTextbox.TabIndex = 22;
            this.ListeningAddressTextbox.Text = "1";
            this.ListeningAddressTextbox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.ListeningAddressTextbox.TextChanged += new System.EventHandler(this.ListeningAddressTextbox_TextChanged);
            // 
            // label9
            // 
            this.label9.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(258, 277);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(94, 21);
            this.label9.TabIndex = 7;
            this.label9.Text = "通信设备";
            // 
            // label7
            // 
            this.label7.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(442, 277);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(94, 21);
            this.label7.TabIndex = 7;
            this.label7.Text = "通信地址";
            // 
            // checkBox1
            // 
            this.checkBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(245, 155);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(120, 25);
            this.checkBox1.TabIndex = 23;
            this.checkBox1.Text = "是否排序";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // comboBox1
            // 
            this.comboBox1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBox1.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "X排序",
            "Y排序"});
            this.comboBox1.Location = new System.Drawing.Point(400, 152);
            this.comboBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(178, 30);
            this.comboBox1.TabIndex = 21;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // groupBoxPositionCorrection
            // 
            this.groupBoxPositionCorrection.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.tableLayoutPanel1.SetColumnSpan(this.groupBoxPositionCorrection, 4);
            this.groupBoxPositionCorrection.Controls.Add(this.checkBoxUsePositionCorrection);
            this.groupBoxPositionCorrection.Controls.Add(this.nodeSubscriptionPositionCorrection);
            this.groupBoxPositionCorrection.Location = new System.Drawing.Point(3, 204);
            this.groupBoxPositionCorrection.Name = "groupBoxPositionCorrection";
            this.groupBoxPositionCorrection.Size = new System.Drawing.Size(770, 76);
            this.groupBoxPositionCorrection.TabIndex = 24;
            this.groupBoxPositionCorrection.TabStop = false;
            this.groupBoxPositionCorrection.Text = "位置修正";
            // 
            // checkBoxUsePositionCorrection
            // 
            this.checkBoxUsePositionCorrection.AutoSize = true;
            this.checkBoxUsePositionCorrection.Location = new System.Drawing.Point(18, 31);
            this.checkBoxUsePositionCorrection.Name = "checkBoxUsePositionCorrection";
            this.checkBoxUsePositionCorrection.Size = new System.Drawing.Size(120, 25);
            this.checkBoxUsePositionCorrection.TabIndex = 0;
            this.checkBoxUsePositionCorrection.Text = "启用修正";
            this.checkBoxUsePositionCorrection.UseVisualStyleBackColor = true;
            this.checkBoxUsePositionCorrection.CheckedChanged += new System.EventHandler(this.checkBoxUsePositionCorrection_CheckedChanged);
            // 
            // nodeSubscriptionPositionCorrection
            // 
            this.nodeSubscriptionPositionCorrection.Enabled = false;
            this.nodeSubscriptionPositionCorrection.Location = new System.Drawing.Point(154, 13);
            this.nodeSubscriptionPositionCorrection.Margin = new System.Windows.Forms.Padding(2);
            this.nodeSubscriptionPositionCorrection.MinimumSize = new System.Drawing.Size(260, 59);
            this.nodeSubscriptionPositionCorrection.Name = "nodeSubscriptionPositionCorrection";
            this.nodeSubscriptionPositionCorrection.Size = new System.Drawing.Size(406, 59);
            this.nodeSubscriptionPositionCorrection.TabIndex = 1;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // toolTip1
            // 
            this.toolTip1.AutomaticDelay = 200;
            this.toolTip1.AutoPopDelay = 3000;
            this.toolTip1.InitialDelay = 200;
            this.toolTip1.ReshowDelay = 40;
            // 
            // NodeParamFormColorDiscern
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1122, 792);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormColorDiscern";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "颜色识别";
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.groupBoxPositionCorrection.ResumeLayout(false);
            this.groupBoxPositionCorrection.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TextBox textBoxModelPath;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private NodeSubscription nodeSubscription1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label label3;
        private Forms.DispShowImage.ShowImageControl showImageControl1;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox comboBoxDevice;
        private System.Windows.Forms.TextBox ListeningAddressTextbox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.GroupBox groupBoxPositionCorrection;
        private System.Windows.Forms.CheckBox checkBoxUsePositionCorrection;
        private NodeSubscription nodeSubscriptionPositionCorrection;
    }
}
