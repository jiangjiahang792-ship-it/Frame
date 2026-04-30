namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    partial class ParamFormERUIIO
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParamFormERUIIO));
            this.labelSelectorCamera = new System.Windows.Forms.Label();
            this.comboBoxModbusDev = new System.Windows.Forms.ComboBox();
            this.comboBoxSelectedOperation = new System.Windows.Forms.ComboBox();
            this.labelSelectorIO = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.comboBoxIONumber = new System.Windows.Forms.ComboBox();
            this.radioButtonIsFixed = new System.Windows.Forms.RadioButton();
            this.radioButtonIsDynamic = new System.Windows.Forms.RadioButton();
            this.ioCheck1 = new TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO.IOCheck();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.getIOValue1 = new TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO.GetIOValue();
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel6 = new System.Windows.Forms.TableLayoutPanel();
            this.checkBoxSpecifiedBit = new System.Windows.Forms.CheckBox();
            this.checkBoxContinue = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanel6.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelSelectorCamera
            // 
            this.labelSelectorCamera.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelSelectorCamera.AutoSize = true;
            this.labelSelectorCamera.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelSelectorCamera.Location = new System.Drawing.Point(63, 17);
            this.labelSelectorCamera.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.labelSelectorCamera.Name = "labelSelectorCamera";
            this.labelSelectorCamera.Size = new System.Drawing.Size(98, 18);
            this.labelSelectorCamera.TabIndex = 0;
            this.labelSelectorCamera.Text = "选择Modbus";
            // 
            // comboBoxModbusDev
            // 
            this.comboBoxModbusDev.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxModbusDev.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxModbusDev.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxModbusDev.FormattingEnabled = true;
            this.comboBoxModbusDev.Location = new System.Drawing.Point(304, 13);
            this.comboBoxModbusDev.Name = "comboBoxModbusDev";
            this.comboBoxModbusDev.Size = new System.Drawing.Size(187, 25);
            this.comboBoxModbusDev.TabIndex = 1;
            // 
            // comboBoxSelectedOperation
            // 
            this.comboBoxSelectedOperation.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxSelectedOperation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSelectedOperation.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxSelectedOperation.FormattingEnabled = true;
            this.comboBoxSelectedOperation.Items.AddRange(new object[] {
            "读取输入信号",
            "读取输出信号",
            "写入输出信号"});
            this.comboBoxSelectedOperation.Location = new System.Drawing.Point(304, 65);
            this.comboBoxSelectedOperation.Name = "comboBoxSelectedOperation";
            this.comboBoxSelectedOperation.Size = new System.Drawing.Size(187, 25);
            this.comboBoxSelectedOperation.TabIndex = 1;
            this.comboBoxSelectedOperation.SelectedIndexChanged += new System.EventHandler(this.comboBoxSelectedOperation_SelectedIndexChanged);
            // 
            // labelSelectorIO
            // 
            this.labelSelectorIO.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelSelectorIO.AutoSize = true;
            this.labelSelectorIO.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelSelectorIO.Location = new System.Drawing.Point(72, 69);
            this.labelSelectorIO.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.labelSelectorIO.Name = "labelSelectorIO";
            this.labelSelectorIO.Size = new System.Drawing.Size(80, 18);
            this.labelSelectorIO.TabIndex = 0;
            this.labelSelectorIO.Text = "选择操作";
            // 
            // button1
            // 
            this.button1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanel3.SetColumnSpan(this.button1, 2);
            this.button1.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button1.Location = new System.Drawing.Point(233, 3);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(96, 47);
            this.button1.TabIndex = 3;
            this.button1.Text = "保存";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // comboBoxIONumber
            // 
            this.comboBoxIONumber.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.comboBoxIONumber.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxIONumber.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxIONumber.FormattingEnabled = true;
            this.comboBoxIONumber.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7"});
            this.comboBoxIONumber.Location = new System.Drawing.Point(304, 117);
            this.comboBoxIONumber.Name = "comboBoxIONumber";
            this.comboBoxIONumber.Size = new System.Drawing.Size(187, 25);
            this.comboBoxIONumber.TabIndex = 1;
            // 
            // radioButtonIsFixed
            // 
            this.radioButtonIsFixed.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.radioButtonIsFixed.AutoSize = true;
            this.radioButtonIsFixed.Checked = true;
            this.radioButtonIsFixed.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioButtonIsFixed.Location = new System.Drawing.Point(72, 12);
            this.radioButtonIsFixed.Name = "radioButtonIsFixed";
            this.radioButtonIsFixed.Size = new System.Drawing.Size(137, 22);
            this.radioButtonIsFixed.TabIndex = 0;
            this.radioButtonIsFixed.TabStop = true;
            this.radioButtonIsFixed.Text = "写入固定信号";
            this.radioButtonIsFixed.UseVisualStyleBackColor = true;
            this.radioButtonIsFixed.CheckedChanged += new System.EventHandler(this.radioButton_CheckedChanged);
            // 
            // radioButtonIsDynamic
            // 
            this.radioButtonIsDynamic.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.radioButtonIsDynamic.AutoSize = true;
            this.radioButtonIsDynamic.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.radioButtonIsDynamic.Location = new System.Drawing.Point(353, 12);
            this.radioButtonIsDynamic.Name = "radioButtonIsDynamic";
            this.radioButtonIsDynamic.Size = new System.Drawing.Size(137, 22);
            this.radioButtonIsDynamic.TabIndex = 0;
            this.radioButtonIsDynamic.Text = "写入动态信号";
            this.radioButtonIsDynamic.UseVisualStyleBackColor = true;
            this.radioButtonIsDynamic.CheckedChanged += new System.EventHandler(this.radioButton_CheckedChanged);
            // 
            // ioCheck1
            // 
            this.ioCheck1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.tableLayoutPanelMain.SetColumnSpan(this.ioCheck1, 2);
            this.ioCheck1.Location = new System.Drawing.Point(27, 274);
            this.ioCheck1.Name = "ioCheck1";
            this.tableLayoutPanelMain.SetRowSpan(this.ioCheck1, 2);
            this.ioCheck1.Size = new System.Drawing.Size(513, 76);
            this.ioCheck1.TabIndex = 2;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 2;
            this.tableLayoutPanelMain.SetColumnSpan(this.tableLayoutPanel3, 2);
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.Controls.Add(this.button1, 0, 0);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 367);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 1;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 53F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(562, 53);
            this.tableLayoutPanel3.TabIndex = 0;
            // 
            // getIOValue1
            // 
            this.getIOValue1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.getIOValue1.Enabled = false;
            this.getIOValue1.Location = new System.Drawing.Point(571, 3);
            this.getIOValue1.Name = "getIOValue1";
            this.tableLayoutPanelMain.SetRowSpan(this.getIOValue1, 8);
            this.getIOValue1.Size = new System.Drawing.Size(563, 417);
            this.getIOValue1.TabIndex = 3;
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 3;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxIONumber, 1, 2);
            this.tableLayoutPanelMain.Controls.Add(this.labelSelectorIO, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxModbusDev, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxSelectedOperation, 1, 1);
            this.tableLayoutPanelMain.Controls.Add(this.labelSelectorCamera, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.getIOValue1, 2, 0);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanel3, 0, 7);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxSpecifiedBit, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.ioCheck1, 0, 5);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanel6, 0, 4);
            this.tableLayoutPanelMain.Controls.Add(this.checkBoxContinue, 1, 3);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 32);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 8;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1137, 423);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // tableLayoutPanel6
            // 
            this.tableLayoutPanel6.ColumnCount = 2;
            this.tableLayoutPanelMain.SetColumnSpan(this.tableLayoutPanel6, 2);
            this.tableLayoutPanel6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.Controls.Add(this.radioButtonIsFixed, 0, 0);
            this.tableLayoutPanel6.Controls.Add(this.radioButtonIsDynamic, 1, 0);
            this.tableLayoutPanel6.Location = new System.Drawing.Point(3, 211);
            this.tableLayoutPanel6.Name = "tableLayoutPanel6";
            this.tableLayoutPanel6.RowCount = 1;
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel6.Size = new System.Drawing.Size(562, 46);
            this.tableLayoutPanel6.TabIndex = 0;
            // 
            // checkBoxSpecifiedBit
            // 
            this.checkBoxSpecifiedBit.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxSpecifiedBit.AutoSize = true;
            this.checkBoxSpecifiedBit.Checked = true;
            this.checkBoxSpecifiedBit.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxSpecifiedBit.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.checkBoxSpecifiedBit.Location = new System.Drawing.Point(35, 119);
            this.checkBoxSpecifiedBit.Name = "checkBoxSpecifiedBit";
            this.checkBoxSpecifiedBit.Size = new System.Drawing.Size(156, 22);
            this.checkBoxSpecifiedBit.TabIndex = 4;
            this.checkBoxSpecifiedBit.Text = "读取指定位信号";
            this.checkBoxSpecifiedBit.UseVisualStyleBackColor = true;
            this.checkBoxSpecifiedBit.CheckedChanged += new System.EventHandler(this.checkBoxSpecifiedBit_CheckedChanged);
            // 
            // checkBoxContinue
            // 
            this.checkBoxContinue.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBoxContinue.AutoSize = true;
            this.checkBoxContinue.Checked = true;
            this.checkBoxContinue.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxContinue.Font = new System.Drawing.Font("宋体", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.checkBoxContinue.Location = new System.Drawing.Point(265, 171);
            this.checkBoxContinue.Name = "checkBoxContinue";
            this.checkBoxContinue.Size = new System.Drawing.Size(264, 22);
            this.checkBoxContinue.TabIndex = 4;
            this.checkBoxContinue.Text = "持续监听直到指定位信号到来";
            this.checkBoxContinue.UseVisualStyleBackColor = true;
            this.checkBoxContinue.CheckedChanged += new System.EventHandler(this.checkBoxSpecifiedBit_CheckedChanged);
            // 
            // ParamFormERUIIO
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1141, 457);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ParamFormERUIIO";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "科锐IO模块";
            this.Shown += new System.EventHandler(this.ParamFormERUIIO_Shown);
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            this.tableLayoutPanel6.ResumeLayout(false);
            this.tableLayoutPanel6.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label labelSelectorCamera;
        private System.Windows.Forms.ComboBox comboBoxModbusDev;
        private System.Windows.Forms.Label labelSelectorIO;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ComboBox comboBoxSelectedOperation;
        private System.Windows.Forms.ComboBox comboBoxIONumber;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private IOCheck ioCheck1;
        private System.Windows.Forms.RadioButton radioButtonIsFixed;
        private System.Windows.Forms.RadioButton radioButtonIsDynamic;
        private GetIOValue getIOValue1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel6;
        private System.Windows.Forms.CheckBox checkBoxSpecifiedBit;
        private System.Windows.Forms.CheckBox checkBoxContinue;
    }
}