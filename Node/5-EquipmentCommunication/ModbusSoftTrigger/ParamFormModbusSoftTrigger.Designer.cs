namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusSoftTrigger
{
    partial class ParamFormModbusSoftTrigger
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParamFormModbusSoftTrigger));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelModBusName = new System.Windows.Forms.Label();
            this.comboBoxModBusList = new System.Windows.Forms.ComboBox();
            this.labelAddress = new System.Windows.Forms.Label();
            this.textBoxAddress = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.checkBoxReset = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.comboBoxDataType = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36.56174F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 63.43826F));
            this.tableLayoutPanel1.Controls.Add(this.labelModBusName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxModBusList, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelAddress, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBoxAddress, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.button1, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.checkBoxReset, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.comboBoxDataType, 1, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 5;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(409, 245);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // labelModBusName
            // 
            this.labelModBusName.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelModBusName.AutoSize = true;
            this.labelModBusName.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelModBusName.Location = new System.Drawing.Point(25, 15);
            this.labelModBusName.Name = "labelModBusName";
            this.labelModBusName.Size = new System.Drawing.Size(98, 18);
            this.labelModBusName.TabIndex = 0;
            this.labelModBusName.Text = "选择Modbus";
            // 
            // comboBoxModBusList
            // 
            this.comboBoxModBusList.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxModBusList.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxModBusList.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxModBusList.FormattingEnabled = true;
            this.comboBoxModBusList.Location = new System.Drawing.Point(203, 11);
            this.comboBoxModBusList.Name = "comboBoxModBusList";
            this.comboBoxModBusList.Size = new System.Drawing.Size(152, 25);
            this.comboBoxModBusList.TabIndex = 1;
            // 
            // labelAddress
            // 
            this.labelAddress.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelAddress.AutoSize = true;
            this.labelAddress.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelAddress.Location = new System.Drawing.Point(34, 64);
            this.labelAddress.Name = "labelAddress";
            this.labelAddress.Size = new System.Drawing.Size(80, 18);
            this.labelAddress.TabIndex = 0;
            this.labelAddress.Text = "触发地址";
            // 
            // textBoxAddress
            // 
            this.textBoxAddress.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxAddress.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBoxAddress.Location = new System.Drawing.Point(203, 60);
            this.textBoxAddress.Name = "textBoxAddress";
            this.textBoxAddress.Size = new System.Drawing.Size(152, 27);
            this.textBoxAddress.TabIndex = 2;
            // 
            // button1
            // 
            this.button1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel1.SetColumnSpan(this.button1, 2);
            this.button1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button1.Location = new System.Drawing.Point(158, 202);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(93, 36);
            this.button1.TabIndex = 3;
            this.button1.Text = "保存";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // checkBoxReset
            // 
            this.checkBoxReset.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxReset.AutoSize = true;
            this.checkBoxReset.Checked = true;
            this.checkBoxReset.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxReset.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.checkBoxReset.Location = new System.Drawing.Point(270, 163);
            this.checkBoxReset.Name = "checkBoxReset";
            this.checkBoxReset.Size = new System.Drawing.Size(18, 17);
            this.checkBoxReset.TabIndex = 5;
            this.checkBoxReset.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(16, 162);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(116, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "监听到后重置";
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.Location = new System.Drawing.Point(34, 113);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 18);
            this.label2.TabIndex = 0;
            this.label2.Text = "数据类型";
            // 
            // comboBoxDataType
            // 
            this.comboBoxDataType.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxDataType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDataType.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxDataType.FormattingEnabled = true;
            this.comboBoxDataType.Items.AddRange(new object[] {
            "Bool",
            "Short",
            "UShort",
            "Int",
            "UInt",
            "Float",
            "Double",
            "Long",
            "ULong",
            "线圈",
            "离散输入"});
            this.comboBoxDataType.Location = new System.Drawing.Point(203, 109);
            this.comboBoxDataType.Name = "comboBoxDataType";
            this.comboBoxDataType.Size = new System.Drawing.Size(152, 25);
            this.comboBoxDataType.TabIndex = 1;
            // 
            // ParamFormModbusSoftTrigger
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(413, 279);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormModbusSoftTrigger";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Modbus软触发信号";
            this.Load += new System.EventHandler(this.ParamFormModbusSoftTrigger_Load);
            this.Controls.SetChildIndex(this.tableLayoutPanel1, 0);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelModBusName;
        private System.Windows.Forms.ComboBox comboBoxModBusList;
        private System.Windows.Forms.Label labelAddress;
        private System.Windows.Forms.TextBox textBoxAddress;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBoxReset;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboBoxDataType;
    }
}