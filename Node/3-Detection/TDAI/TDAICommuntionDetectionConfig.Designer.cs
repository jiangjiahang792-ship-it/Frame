namespace TDJS_Vision.Node._3_Detection.TDAI
{
    partial class TDAICommuntionDetectionConfig
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TDAICommuntionDetectionConfig));
            this.dgvAIParam = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button4 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.openFileDialog2 = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.labelReadType = new System.Windows.Forms.Label();
            this.comboBoxReadType = new System.Windows.Forms.ComboBox();
            this.labelRegisterCount = new System.Windows.Forms.Label();
            this.numericRegisterCount = new System.Windows.Forms.NumericUpDown();
            this.labelEncoding = new System.Windows.Forms.Label();
            this.comboBoxEncoding = new System.Windows.Forms.ComboBox();
            this.checkBoxLowByteFirst = new System.Windows.Forms.CheckBox();
            this.labelReadHint = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numericRegisterCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAIParam)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvAIParam
            // 
            this.dgvAIParam.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvAIParam.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dgvAIParam.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAIParam.Location = new System.Drawing.Point(8, 240);
            this.dgvAIParam.Name = "dgvAIParam";
            this.dgvAIParam.RowHeadersWidth = 62;
            this.dgvAIParam.RowTemplate.Height = 30;
            this.dgvAIParam.Size = new System.Drawing.Size(674, 353);
            this.dgvAIParam.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(10, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(189, 21);
            this.label1.TabIndex = 3;
            this.label1.Text = "当前通信设备名称:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.Location = new System.Drawing.Point(200, 53);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(31, 21);
            this.label2.TabIndex = 3;
            this.label2.Text = "无";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(639, 93);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(43, 32);
            this.button1.TabIndex = 4;
            this.button1.Text = "+";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.Red;
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button2.Location = new System.Drawing.Point(580, 93);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(43, 32);
            this.button2.TabIndex = 4;
            this.button2.Text = "-";
            this.button2.UseVisualStyleBackColor = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(10, 93);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(105, 21);
            this.label3.TabIndex = 3;
            this.label3.Text = "通信地址:";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(120, 90);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(111, 28);
            this.textBox1.TabIndex = 5;
            // 
            // button4
            // 
            this.button4.BackColor = System.Drawing.Color.Red;
            this.button4.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button4.Location = new System.Drawing.Point(475, 93);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(43, 32);
            this.button4.TabIndex = 4;
            this.button4.Text = "导入";
            this.button4.UseVisualStyleBackColor = false;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.Red;
            this.button3.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button3.Location = new System.Drawing.Point(529, 93);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(43, 32);
            this.button3.TabIndex = 4;
            this.button3.Text = "导出";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // openFileDialog2
            // 
            this.openFileDialog2.FileName = "openFileDialog1";
            // 
            // saveFileDialog1
            // 
            this.saveFileDialog1.DefaultExt = "aicmo";
            this.saveFileDialog1.Filter = "AI通信配置检测项文件(*.aicmo)|";
            this.saveFileDialog1.Title = "AI通信配置检测项文件保存位置";
            // 
            // TDAICommuntionDetectionConfig
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(694, 605);
            // 读取类型：
            this.labelReadType.Name = "labelReadType";
            this.labelReadType.Location = new System.Drawing.Point(10, 135);
            this.labelReadType.Size = new System.Drawing.Size(100, 25);
            this.labelReadType.Text = "读取类型：";
            // comboBoxReadType
            this.comboBoxReadType.Name = "comboBoxReadType";
            this.comboBoxReadType.Location = new System.Drawing.Point(120, 132);
            this.comboBoxReadType.Size = new System.Drawing.Size(190, 28);
            // 寄存器数量：
            this.labelRegisterCount.Name = "labelRegisterCount";
            this.labelRegisterCount.Location = new System.Drawing.Point(330, 135);
            this.labelRegisterCount.Size = new System.Drawing.Size(115, 25);
            this.labelRegisterCount.Text = "寄存器数量：";
            // numericRegisterCount
            this.numericRegisterCount.Name = "numericRegisterCount";
            this.numericRegisterCount.Location = new System.Drawing.Point(450, 132);
            this.numericRegisterCount.Size = new System.Drawing.Size(90, 28);
            // 字符串编码：
            this.labelEncoding.Name = "labelEncoding";
            this.labelEncoding.Location = new System.Drawing.Point(10, 175);
            this.labelEncoding.Size = new System.Drawing.Size(110, 25);
            this.labelEncoding.Text = "字符串编码：";
            // comboBoxEncoding
            this.comboBoxEncoding.Name = "comboBoxEncoding";
            this.comboBoxEncoding.Location = new System.Drawing.Point(120, 172);
            this.comboBoxEncoding.Size = new System.Drawing.Size(190, 28);
            // 低字节在前
            this.checkBoxLowByteFirst.Name = "checkBoxLowByteFirst";
            this.checkBoxLowByteFirst.Location = new System.Drawing.Point(330, 175);
            this.checkBoxLowByteFirst.Size = new System.Drawing.Size(180, 25);
            this.checkBoxLowByteFirst.Text = "低字节在前";
            // 布尔触发值：true/false 或 1/0；字符串每个寄存器占两个字节。
            this.labelReadHint.Name = "labelReadHint";
            this.labelReadHint.Location = new System.Drawing.Point(10, 210);
            this.labelReadHint.Size = new System.Drawing.Size(674, 25);
            this.labelReadHint.Text = "布尔触发值：true/false 或 1/0；字符串每个寄存器占两个字节。";
            this.comboBoxReadType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxReadType.Items.AddRange(new object[] { "无符号整数（ushort）", "字符串", "布尔值（bool）" });
            this.comboBoxReadType.SelectedIndexChanged += new System.EventHandler(this.ComboBoxReadType_SelectedIndexChanged);
            this.comboBoxEncoding.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxEncoding.Items.AddRange(new object[] { "英文（ASCII）", "通用（UTF-8）", "中文（GB18030）", "双字节（UTF-16小端）" });
            this.numericRegisterCount.Minimum = 1;
            this.numericRegisterCount.Maximum = 125;
            this.numericRegisterCount.Value = 10;
            this.Controls.Add(this.labelReadType);
            this.Controls.Add(this.comboBoxReadType);
            this.Controls.Add(this.labelRegisterCount);
            this.Controls.Add(this.numericRegisterCount);
            this.Controls.Add(this.labelEncoding);
            this.Controls.Add(this.comboBoxEncoding);
            this.Controls.Add(this.checkBoxLowByteFirst);
            this.Controls.Add(this.labelReadHint);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dgvAIParam);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TDAICommuntionDetectionConfig";
            this.Text = "AI通信检测项配置";
            this.Load += new System.EventHandler(this.TDAICommuntionDetectionConfig_Load);
            this.Controls.SetChildIndex(this.dgvAIParam, 0);
            this.Controls.SetChildIndex(this.label1, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Controls.SetChildIndex(this.label2, 0);
            this.Controls.SetChildIndex(this.button1, 0);
            this.Controls.SetChildIndex(this.button2, 0);
            this.Controls.SetChildIndex(this.button3, 0);
            this.Controls.SetChildIndex(this.button4, 0);
            this.Controls.SetChildIndex(this.textBox1, 0);
            ((System.ComponentModel.ISupportInitialize)(this.dgvAIParam)).EndInit();
            this.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericRegisterCount)).EndInit();
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvAIParam;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.OpenFileDialog openFileDialog2;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        /// <summary>读取类型：。</summary>
        private System.Windows.Forms.Label labelReadType;
        /// <summary>通信读取类型选项。</summary>
        private System.Windows.Forms.ComboBox comboBoxReadType;
        /// <summary>寄存器数量：。</summary>
        private System.Windows.Forms.Label labelRegisterCount;
        /// <summary>字符串寄存器数量。</summary>
        private System.Windows.Forms.NumericUpDown numericRegisterCount;
        /// <summary>字符串编码：。</summary>
        private System.Windows.Forms.Label labelEncoding;
        /// <summary>字符串编码选项。</summary>
        private System.Windows.Forms.ComboBox comboBoxEncoding;
        /// <summary>低字节在前。</summary>
        private System.Windows.Forms.CheckBox checkBoxLowByteFirst;
        /// <summary>布尔触发值：true/false 或 1/0；字符串每个寄存器占两个字节。。</summary>
        private System.Windows.Forms.Label labelReadHint;
    }
}
