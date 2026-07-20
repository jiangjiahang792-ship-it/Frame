namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    partial class NodeParamFormCompositeInput
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelTip = new System.Windows.Forms.Label();
            this.dataGridViewPorts = new System.Windows.Forms.DataGridView();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnValueType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnDefaultValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonMoveUp = new System.Windows.Forms.Button();
            this.buttonMoveDown = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPorts)).BeginInit();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.labelTip, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.dataGridViewPorts, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.panelButtons, 0, 2);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(12, 12);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(760, 426);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelTip
            // 
            this.labelTip.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTip.ForeColor = System.Drawing.Color.DimGray;
            this.labelTip.Location = new System.Drawing.Point(3, 0);
            this.labelTip.Name = "labelTip";
            this.labelTip.Size = new System.Drawing.Size(754, 42);
            this.labelTip.TabIndex = 0;
            this.labelTip.Text = "声明组合模块内部可订阅的输入变量，内部节点可从本节点选择“变量.端口名”。";
            this.labelTip.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // dataGridViewPorts
            // 
            this.dataGridViewPorts.AllowUserToAddRows = false;
            this.dataGridViewPorts.AllowUserToDeleteRows = false;
            this.dataGridViewPorts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewPorts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnName,
            this.ColumnValueType,
            this.ColumnDefaultValue,
            this.ColumnNote});
            this.dataGridViewPorts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewPorts.Location = new System.Drawing.Point(3, 45);
            this.dataGridViewPorts.MultiSelect = false;
            this.dataGridViewPorts.Name = "dataGridViewPorts";
            this.dataGridViewPorts.RowHeadersWidth = 28;
            this.dataGridViewPorts.RowTemplate.Height = 30;
            this.dataGridViewPorts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewPorts.Size = new System.Drawing.Size(754, 330);
            this.dataGridViewPorts.TabIndex = 1;
            this.dataGridViewPorts.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewPorts_DataError);
            // 
            // ColumnName
            // 
            this.ColumnName.FillWeight = 110F;
            this.ColumnName.HeaderText = "端口名";
            this.ColumnName.MinimumWidth = 110;
            this.ColumnName.Name = "ColumnName";
            // 
            // ColumnValueType
            // 
            this.ColumnValueType.FillWeight = 85F;
            this.ColumnValueType.HeaderText = "类型";
            this.ColumnValueType.MinimumWidth = 90;
            this.ColumnValueType.Name = "ColumnValueType";
            // 
            // ColumnDefaultValue
            // 
            this.ColumnDefaultValue.FillWeight = 120F;
            this.ColumnDefaultValue.HeaderText = "默认值";
            this.ColumnDefaultValue.MinimumWidth = 120;
            this.ColumnDefaultValue.Name = "ColumnDefaultValue";
            // 
            // ColumnNote
            // 
            this.ColumnNote.FillWeight = 160F;
            this.ColumnNote.HeaderText = "备注";
            this.ColumnNote.MinimumWidth = 140;
            this.ColumnNote.Name = "ColumnNote";
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.buttonAdd);
            this.panelButtons.Controls.Add(this.buttonRemove);
            this.panelButtons.Controls.Add(this.buttonMoveUp);
            this.panelButtons.Controls.Add(this.buttonMoveDown);
            this.panelButtons.Controls.Add(this.buttonCancel);
            this.panelButtons.Controls.Add(this.buttonSave);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelButtons.Location = new System.Drawing.Point(3, 381);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(754, 42);
            this.panelButtons.TabIndex = 2;
            // 
            // buttonAdd
            // 
            this.buttonAdd.Location = new System.Drawing.Point(0, 7);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(80, 28);
            this.buttonAdd.TabIndex = 0;
            this.buttonAdd.Text = "新增";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Location = new System.Drawing.Point(86, 7);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(80, 28);
            this.buttonRemove.TabIndex = 1;
            this.buttonRemove.Text = "删除";
            this.buttonRemove.UseVisualStyleBackColor = true;
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // buttonMoveUp
            // 
            this.buttonMoveUp.Location = new System.Drawing.Point(172, 7);
            this.buttonMoveUp.Name = "buttonMoveUp";
            this.buttonMoveUp.Size = new System.Drawing.Size(80, 28);
            this.buttonMoveUp.TabIndex = 2;
            this.buttonMoveUp.Text = "上移";
            this.buttonMoveUp.UseVisualStyleBackColor = true;
            this.buttonMoveUp.Click += new System.EventHandler(this.buttonMoveUp_Click);
            // 
            // buttonMoveDown
            // 
            this.buttonMoveDown.Location = new System.Drawing.Point(258, 7);
            this.buttonMoveDown.Name = "buttonMoveDown";
            this.buttonMoveDown.Size = new System.Drawing.Size(80, 28);
            this.buttonMoveDown.TabIndex = 3;
            this.buttonMoveDown.Text = "下移";
            this.buttonMoveDown.UseVisualStyleBackColor = true;
            this.buttonMoveDown.Click += new System.EventHandler(this.buttonMoveDown_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(587, 7);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(80, 28);
            this.buttonCancel.TabIndex = 5;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(674, 7);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(80, 28);
            this.buttonSave.TabIndex = 4;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCompositeInput
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 450);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormCompositeInput";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "组合输入";
            this.tableLayoutPanelMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPorts)).EndInit();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.Label labelTip;
        private System.Windows.Forms.DataGridView dataGridViewPorts;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnValueType;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnDefaultValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnNote;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonMoveUp;
        private System.Windows.Forms.Button buttonMoveDown;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
    }
}
