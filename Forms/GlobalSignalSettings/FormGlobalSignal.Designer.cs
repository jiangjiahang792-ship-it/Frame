namespace TDJS_Vision.Forms.GlobalSignalSettings
{
    partial class FormGlobalSignal
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormGlobalSignal));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.dataGridView3 = new System.Windows.Forms.DataGridView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.deviceColumn1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.addressColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataTypeColumn1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.dataValueColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.EnableColumn1 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.deleteColumn1 = new System.Windows.Forms.DataGridViewButtonColumn();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.dataGridView2 = new System.Windows.Forms.DataGridView();
            this.deviceColumn2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.addressColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataTypeColumn2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.dataValueColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.EnableColumn2 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.deleteColumn2 = new System.Windows.Forms.DataGridViewButtonColumn();
            this.dataGridViewComboBoxColumn1 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewComboBoxColumn2 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewCheckBoxColumn1 = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.dataGridViewButtonColumn1 = new System.Windows.Forms.DataGridViewButtonColumn();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox2, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(758, 679);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.dataGridView3);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(30, 472);
            this.groupBox3.Margin = new System.Windows.Forms.Padding(30, 20, 30, 4);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox3.Size = new System.Drawing.Size(698, 203);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "视觉监听信号";
            // 
            // dataGridView3
            // 
            this.dataGridView3.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView3.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewComboBoxColumn1,
            this.dataGridViewTextBoxColumn1,
            this.dataGridViewComboBoxColumn2,
            this.dataGridViewTextBoxColumn2,
            this.dataGridViewCheckBoxColumn1,
            this.dataGridViewButtonColumn1});
            this.dataGridView3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView3.Location = new System.Drawing.Point(3, 29);
            this.dataGridView3.Name = "dataGridView3";
            this.dataGridView3.RowHeadersWidth = 51;
            this.dataGridView3.RowTemplate.Height = 27;
            this.dataGridView3.Size = new System.Drawing.Size(692, 170);
            this.dataGridView3.TabIndex = 1;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tableLayoutPanel2);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(30, 20);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(30, 20, 30, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(698, 202);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "视觉准备信号";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanel2.Controls.Add(this.dataGridView1, 0, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 29);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 173F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(692, 169);
            this.tableLayoutPanel2.TabIndex = 1;
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.deviceColumn1,
            this.addressColumn1,
            this.dataTypeColumn1,
            this.dataValueColumn1,
            this.EnableColumn1,
            this.deleteColumn1});
            this.tableLayoutPanel2.SetColumnSpan(this.dataGridView1, 2);
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(3, 3);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 51;
            this.dataGridView1.RowTemplate.Height = 27;
            this.dataGridView1.Size = new System.Drawing.Size(686, 163);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);
            // 
            // deviceColumn1
            // 
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            this.deviceColumn1.DefaultCellStyle = dataGridViewCellStyle3;
            this.deviceColumn1.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.deviceColumn1.HeaderText = "通信设备";
            this.deviceColumn1.MinimumWidth = 6;
            this.deviceColumn1.Name = "deviceColumn1";
            this.deviceColumn1.Width = 125;
            // 
            // addressColumn1
            // 
            this.addressColumn1.HeaderText = "信号地址";
            this.addressColumn1.MinimumWidth = 6;
            this.addressColumn1.Name = "addressColumn1";
            this.addressColumn1.Width = 125;
            // 
            // dataTypeColumn1
            // 
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            this.dataTypeColumn1.DefaultCellStyle = dataGridViewCellStyle4;
            this.dataTypeColumn1.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.dataTypeColumn1.HeaderText = "数据类型";
            this.dataTypeColumn1.Items.AddRange(new object[] {
            "布尔类型",
            "整数类型",
            "字符串类型"});
            this.dataTypeColumn1.MinimumWidth = 6;
            this.dataTypeColumn1.Name = "dataTypeColumn1";
            this.dataTypeColumn1.Width = 125;
            // 
            // dataValueColumn1
            // 
            this.dataValueColumn1.HeaderText = "数据值";
            this.dataValueColumn1.MinimumWidth = 6;
            this.dataValueColumn1.Name = "dataValueColumn1";
            this.dataValueColumn1.Width = 125;
            // 
            // EnableColumn1
            // 
            this.EnableColumn1.HeaderText = "启用";
            this.EnableColumn1.MinimumWidth = 6;
            this.EnableColumn1.Name = "EnableColumn1";
            this.EnableColumn1.Width = 125;
            // 
            // deleteColumn1
            // 
            this.deleteColumn1.HeaderText = "删除";
            this.deleteColumn1.MinimumWidth = 6;
            this.deleteColumn1.Name = "deleteColumn1";
            this.deleteColumn1.Text = "删除";
            this.deleteColumn1.UseColumnTextForButtonValue = true;
            this.deleteColumn1.Width = 125;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.dataGridView2);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(30, 246);
            this.groupBox2.Margin = new System.Windows.Forms.Padding(30, 20, 30, 4);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox2.Size = new System.Drawing.Size(698, 202);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "视觉离线信号";
            // 
            // dataGridView2
            // 
            this.dataGridView2.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView2.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.deviceColumn2,
            this.addressColumn2,
            this.dataTypeColumn2,
            this.dataValueColumn2,
            this.EnableColumn2,
            this.deleteColumn2});
            this.dataGridView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView2.Location = new System.Drawing.Point(3, 29);
            this.dataGridView2.Name = "dataGridView2";
            this.dataGridView2.RowHeadersWidth = 51;
            this.dataGridView2.RowTemplate.Height = 27;
            this.dataGridView2.Size = new System.Drawing.Size(692, 169);
            this.dataGridView2.TabIndex = 1;
            this.dataGridView2.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);
            // 
            // deviceColumn2
            // 
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            this.deviceColumn2.DefaultCellStyle = dataGridViewCellStyle5;
            this.deviceColumn2.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.deviceColumn2.HeaderText = "通信设备";
            this.deviceColumn2.MinimumWidth = 6;
            this.deviceColumn2.Name = "deviceColumn2";
            this.deviceColumn2.Width = 125;
            // 
            // addressColumn2
            // 
            this.addressColumn2.HeaderText = "信号地址";
            this.addressColumn2.MinimumWidth = 6;
            this.addressColumn2.Name = "addressColumn2";
            this.addressColumn2.Width = 125;
            // 
            // dataTypeColumn2
            // 
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Control;
            this.dataTypeColumn2.DefaultCellStyle = dataGridViewCellStyle6;
            this.dataTypeColumn2.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.dataTypeColumn2.HeaderText = "数据类型";
            this.dataTypeColumn2.Items.AddRange(new object[] {
            "布尔类型",
            "整数类型",
            "字符串类型"});
            this.dataTypeColumn2.MinimumWidth = 6;
            this.dataTypeColumn2.Name = "dataTypeColumn2";
            this.dataTypeColumn2.Width = 125;
            // 
            // dataValueColumn2
            // 
            this.dataValueColumn2.HeaderText = "数据值";
            this.dataValueColumn2.MinimumWidth = 6;
            this.dataValueColumn2.Name = "dataValueColumn2";
            this.dataValueColumn2.Width = 125;
            // 
            // EnableColumn2
            // 
            this.EnableColumn2.HeaderText = "启用";
            this.EnableColumn2.MinimumWidth = 6;
            this.EnableColumn2.Name = "EnableColumn2";
            this.EnableColumn2.Width = 125;
            // 
            // deleteColumn2
            // 
            this.deleteColumn2.HeaderText = "删除";
            this.deleteColumn2.MinimumWidth = 6;
            this.deleteColumn2.Name = "deleteColumn2";
            this.deleteColumn2.Text = "删除";
            this.deleteColumn2.UseColumnTextForButtonValue = true;
            this.deleteColumn2.Width = 125;
            // 
            // dataGridViewComboBoxColumn1
            // 
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            this.dataGridViewComboBoxColumn1.DefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewComboBoxColumn1.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.dataGridViewComboBoxColumn1.HeaderText = "通信设备";
            this.dataGridViewComboBoxColumn1.MinimumWidth = 6;
            this.dataGridViewComboBoxColumn1.Name = "dataGridViewComboBoxColumn1";
            this.dataGridViewComboBoxColumn1.Width = 125;
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.HeaderText = "信号地址";
            this.dataGridViewTextBoxColumn1.MinimumWidth = 6;
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.Width = 125;
            // 
            // dataGridViewComboBoxColumn2
            // 
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            this.dataGridViewComboBoxColumn2.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridViewComboBoxColumn2.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.dataGridViewComboBoxColumn2.HeaderText = "数据类型";
            this.dataGridViewComboBoxColumn2.Items.AddRange(new object[] {
            "布尔类型",
            "整数类型",
            "short类型",
            "ushort类型",
            "字符串类型",
            "离散输入类型"});
            this.dataGridViewComboBoxColumn2.MinimumWidth = 6;
            this.dataGridViewComboBoxColumn2.Name = "dataGridViewComboBoxColumn2";
            this.dataGridViewComboBoxColumn2.Width = 125;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.HeaderText = "数据值";
            this.dataGridViewTextBoxColumn2.MinimumWidth = 6;
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.Width = 125;
            // 
            // dataGridViewCheckBoxColumn1
            // 
            this.dataGridViewCheckBoxColumn1.HeaderText = "启用";
            this.dataGridViewCheckBoxColumn1.MinimumWidth = 6;
            this.dataGridViewCheckBoxColumn1.Name = "dataGridViewCheckBoxColumn1";
            this.dataGridViewCheckBoxColumn1.Width = 125;
            // 
            // dataGridViewButtonColumn1
            // 
            this.dataGridViewButtonColumn1.HeaderText = "删除";
            this.dataGridViewButtonColumn1.MinimumWidth = 6;
            this.dataGridViewButtonColumn1.Name = "dataGridViewButtonColumn1";
            this.dataGridViewButtonColumn1.Text = "删除";
            this.dataGridViewButtonColumn1.UseColumnTextForButtonValue = true;
            this.dataGridViewButtonColumn1.Width = 125;
            // 
            // FormGlobalSignal
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 22F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(758, 679);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "FormGlobalSignal";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "全局信号设置";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormGlobalSignal_FormClosing);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView3)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView2)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.DataGridViewComboBoxColumn deviceColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn addressColumn1;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataTypeColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataValueColumn1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn EnableColumn1;
        private System.Windows.Forms.DataGridViewButtonColumn deleteColumn1;
        private System.Windows.Forms.DataGridViewComboBoxColumn deviceColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn addressColumn2;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataTypeColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataValueColumn2;
        private System.Windows.Forms.DataGridViewCheckBoxColumn EnableColumn2;
        private System.Windows.Forms.DataGridViewButtonColumn deleteColumn2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.DataGridView dataGridView3;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataGridViewComboBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataGridViewComboBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn1;
        private System.Windows.Forms.DataGridViewButtonColumn dataGridViewButtonColumn1;
    }
}