namespace TDJS_Vision.Node._5_EquipmentCommunication.AIResultSendByModbus
{
    partial class ParamFormSignalSendByModbus
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParamFormSignalSendByModbus));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxSignalView = new System.Windows.Forms.GroupBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkedListBox1 = new System.Windows.Forms.CheckedListBox();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.label4 = new System.Windows.Forms.Label();
            this.comboBoxModBusList = new System.Windows.Forms.ComboBox();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.checkBoxAutoSelect = new System.Windows.Forms.CheckBox();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonAuto2 = new System.Windows.Forms.Button();
            this.buttonAuto1 = new System.Windows.Forms.Button();
            this.textBoxHoldTime = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBoxReSet = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBoxSignalView.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 6;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13.9986F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13.9986F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15.9984F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.66813F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.66813F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 18.66813F));
            this.tableLayoutPanel1.Controls.Add(this.groupBoxSignalView, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.nodeSubscription1, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label4, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxModBusList, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonSave, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1139, 474);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // groupBoxSignalView
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.groupBoxSignalView, 4);
            this.groupBoxSignalView.Controls.Add(this.dataGridView1);
            this.groupBoxSignalView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSignalView.Font = new System.Drawing.Font("宋体", 10F, System.Drawing.FontStyle.Bold);
            this.groupBoxSignalView.Location = new System.Drawing.Point(321, 123);
            this.groupBoxSignalView.Margin = new System.Windows.Forms.Padding(3, 3, 30, 3);
            this.groupBoxSignalView.Name = "groupBoxSignalView";
            this.groupBoxSignalView.Padding = new System.Windows.Forms.Padding(3, 3, 3, 30);
            this.groupBoxSignalView.Size = new System.Drawing.Size(788, 348);
            this.groupBoxSignalView.TabIndex = 0;
            this.groupBoxSignalView.TabStop = false;
            this.groupBoxSignalView.Text = "信号列表";
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridView1.Location = new System.Drawing.Point(3, 23);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.RowHeadersWidth = 51;
            this.dataGridView1.RowTemplate.Height = 27;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView1.Size = new System.Drawing.Size(782, 295);
            this.dataGridView1.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.groupBox1, 2);
            this.groupBox1.Controls.Add(this.checkedListBox1);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBox1.Location = new System.Drawing.Point(30, 123);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(30, 3, 3, 30);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(285, 321);
            this.groupBox1.TabIndex = 12;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "所有检测项";
            // 
            // checkedListBox1
            // 
            this.checkedListBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.checkedListBox1.FormattingEnabled = true;
            this.checkedListBox1.Location = new System.Drawing.Point(3, 21);
            this.checkedListBox1.Margin = new System.Windows.Forms.Padding(3, 20, 3, 3);
            this.checkedListBox1.Name = "checkedListBox1";
            this.checkedListBox1.Size = new System.Drawing.Size(279, 297);
            this.checkedListBox1.TabIndex = 11;
            // 
            // nodeSubscription1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.nodeSubscription1, 2);
            this.nodeSubscription1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nodeSubscription1.Location = new System.Drawing.Point(162, 2);
            this.nodeSubscription1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(231, 50);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(335, 56);
            this.nodeSubscription1.TabIndex = 10;
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(548, 21);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(116, 18);
            this.label4.TabIndex = 2;
            this.label4.Text = "选择Modelbus";
            // 
            // comboBoxModBusList
            // 
            this.comboBoxModBusList.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxModBusList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxModBusList.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxModBusList.FormattingEnabled = true;
            this.comboBoxModBusList.Location = new System.Drawing.Point(715, 17);
            this.comboBoxModBusList.Name = "comboBoxModBusList";
            this.comboBoxModBusList.Size = new System.Drawing.Size(206, 25);
            this.comboBoxModBusList.TabIndex = 3;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSave.BackColor = System.Drawing.Color.MediumTurquoise;
            this.buttonSave.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonSave.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.buttonSave.Location = new System.Drawing.Point(957, 3);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(148, 54);
            this.buttonSave.TabIndex = 1;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = false;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 7;
            this.tableLayoutPanel1.SetColumnSpan(this.tableLayoutPanel2, 6);
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel2.Controls.Add(this.checkBoxAutoSelect, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonAdd, 6, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonAuto2, 5, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonAuto1, 4, 0);
            this.tableLayoutPanel2.Controls.Add(this.textBoxHoldTime, 3, 0);
            this.tableLayoutPanel2.Controls.Add(this.label1, 2, 0);
            this.tableLayoutPanel2.Controls.Add(this.checkBoxReSet, 1, 0);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 63);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 1;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(1133, 54);
            this.tableLayoutPanel2.TabIndex = 15;
            // 
            // checkBoxAutoSelect
            // 
            this.checkBoxAutoSelect.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxAutoSelect.AutoSize = true;
            this.checkBoxAutoSelect.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.checkBoxAutoSelect.Location = new System.Drawing.Point(22, 16);
            this.checkBoxAutoSelect.Name = "checkBoxAutoSelect";
            this.checkBoxAutoSelect.Size = new System.Drawing.Size(102, 22);
            this.checkBoxAutoSelect.TabIndex = 14;
            this.checkBoxAutoSelect.Text = "一键全选";
            this.checkBoxAutoSelect.UseVisualStyleBackColor = true;
            this.checkBoxAutoSelect.CheckedChanged += new System.EventHandler(this.checkBoxAutoSelect_CheckedChanged);
            // 
            // buttonAdd
            // 
            this.buttonAdd.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonAdd.BackColor = System.Drawing.Color.LightSteelBlue;
            this.buttonAdd.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonAdd.Location = new System.Drawing.Point(983, 7);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(125, 40);
            this.buttonAdd.TabIndex = 1;
            this.buttonAdd.Text = "增量配置 ?";
            this.buttonAdd.UseVisualStyleBackColor = false;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // buttonAuto2
            // 
            this.buttonAuto2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonAuto2.BackColor = System.Drawing.Color.LightSteelBlue;
            this.buttonAuto2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonAuto2.Location = new System.Drawing.Point(811, 7);
            this.buttonAuto2.Name = "buttonAuto2";
            this.buttonAuto2.Size = new System.Drawing.Size(125, 40);
            this.buttonAuto2.TabIndex = 1;
            this.buttonAuto2.Text = "自动配置2 ?";
            this.buttonAuto2.UseVisualStyleBackColor = false;
            this.buttonAuto2.Click += new System.EventHandler(this.button1_Click);
            // 
            // buttonAuto1
            // 
            this.buttonAuto1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonAuto1.BackColor = System.Drawing.Color.LightSteelBlue;
            this.buttonAuto1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.buttonAuto1.Location = new System.Drawing.Point(641, 6);
            this.buttonAuto1.Name = "buttonAuto1";
            this.buttonAuto1.Size = new System.Drawing.Size(126, 41);
            this.buttonAuto1.TabIndex = 1;
            this.buttonAuto1.Text = "自动配置1 ?";
            this.buttonAuto1.UseVisualStyleBackColor = false;
            this.buttonAuto1.Click += new System.EventHandler(this.button2_Click);
            // 
            // textBoxHoldTime
            // 
            this.textBoxHoldTime.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxHoldTime.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBoxHoldTime.Location = new System.Drawing.Point(477, 13);
            this.textBoxHoldTime.Name = "textBoxHoldTime";
            this.textBoxHoldTime.Size = new System.Drawing.Size(116, 28);
            this.textBoxHoldTime.TabIndex = 13;
            this.textBoxHoldTime.Text = "30";
            this.textBoxHoldTime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(325, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 18);
            this.label1.TabIndex = 4;
            this.label1.Text = "信号保持时间";
            // 
            // checkBoxReSet
            // 
            this.checkBoxReSet.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxReSet.AutoSize = true;
            this.checkBoxReSet.Checked = true;
            this.checkBoxReSet.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxReSet.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.checkBoxReSet.Location = new System.Drawing.Point(171, 16);
            this.checkBoxReSet.Name = "checkBoxReSet";
            this.checkBoxReSet.Size = new System.Drawing.Size(120, 22);
            this.checkBoxReSet.TabIndex = 14;
            this.checkBoxReSet.Text = "信号重置 ?";
            this.checkBoxReSet.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.Location = new System.Drawing.Point(30, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(98, 18);
            this.label2.TabIndex = 2;
            this.label2.Text = "订阅AI结果";
            // 
            // toolTip1
            // 
            this.toolTip1.AutomaticDelay = 200;
            this.toolTip1.AutoPopDelay = 4000;
            this.toolTip1.InitialDelay = 200;
            this.toolTip1.ReshowDelay = 40;
            this.toolTip1.ToolTipTitle = "效果";
            // 
            // ParamFormSignalSendByModbus
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1143, 508);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormSignalSendByModbus";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "配置Modbus信号列表";
            this.Shown += new System.EventHandler(this.Form1_Shown);
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.groupBoxSignalView.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBoxSignalView;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboBoxModBusList;
        private NodeSubscription nodeSubscription1;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.CheckedListBox checkedListBox1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button buttonAuto2;
        private System.Windows.Forms.Button buttonAuto1;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TextBox textBoxHoldTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxAutoSelect;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.CheckBox checkBoxReSet;
        private System.Windows.Forms.Label label2;
    }
}