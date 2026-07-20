namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    partial class NodeParamFormCompositeOutput
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
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.dataGridViewPorts = new System.Windows.Forms.DataGridView();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnValueType = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBoxSources = new System.Windows.Forms.GroupBox();
            this.treeViewSources = new System.Windows.Forms.TreeView();
            this.panelSourceBottom = new System.Windows.Forms.Panel();
            this.buttonSelectProperty = new System.Windows.Forms.Button();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonMoveUp = new System.Windows.Forms.Button();
            this.buttonMoveDown = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPorts)).BeginInit();
            this.groupBoxSources.SuspendLayout();
            this.panelSourceBottom.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.labelTip, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.splitContainerMain, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.panelButtons, 0, 2);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(12, 12);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1000, 536);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelTip
            // 
            this.labelTip.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelTip.ForeColor = System.Drawing.Color.DimGray;
            this.labelTip.Location = new System.Drawing.Point(3, 0);
            this.labelTip.Name = "labelTip";
            this.labelTip.Size = new System.Drawing.Size(994, 42);
            this.labelTip.TabIndex = 0;
            this.labelTip.Text = "把内部上游节点结果映射成组合模块的外部输出变量，外部节点可订阅“变量.端口名”。";
            this.labelTip.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(3, 45);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.dataGridViewPorts);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxSources);
            this.splitContainerMain.Size = new System.Drawing.Size(994, 440);
            this.splitContainerMain.SplitterDistance = 634;
            this.splitContainerMain.TabIndex = 1;
            // 
            // dataGridViewPorts
            // 
            this.dataGridViewPorts.AllowUserToAddRows = false;
            this.dataGridViewPorts.AllowUserToDeleteRows = false;
            this.dataGridViewPorts.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewPorts.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnName,
            this.ColumnValueType,
            this.ColumnSource,
            this.ColumnNote});
            this.dataGridViewPorts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewPorts.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewPorts.MultiSelect = false;
            this.dataGridViewPorts.Name = "dataGridViewPorts";
            this.dataGridViewPorts.RowHeadersWidth = 28;
            this.dataGridViewPorts.RowTemplate.Height = 30;
            this.dataGridViewPorts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewPorts.Size = new System.Drawing.Size(634, 440);
            this.dataGridViewPorts.TabIndex = 0;
            this.dataGridViewPorts.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewPorts_DataError);
            // 
            // ColumnName
            // 
            this.ColumnName.FillWeight = 95F;
            this.ColumnName.HeaderText = "端口名";
            this.ColumnName.MinimumWidth = 95;
            this.ColumnName.Name = "ColumnName";
            // 
            // ColumnValueType
            // 
            this.ColumnValueType.FillWeight = 76F;
            this.ColumnValueType.HeaderText = "类型";
            this.ColumnValueType.MinimumWidth = 80;
            this.ColumnValueType.Name = "ColumnValueType";
            // 
            // ColumnSource
            // 
            this.ColumnSource.FillWeight = 190F;
            this.ColumnSource.HeaderText = "内部来源";
            this.ColumnSource.MinimumWidth = 160;
            this.ColumnSource.Name = "ColumnSource";
            this.ColumnSource.ReadOnly = true;
            // 
            // ColumnNote
            // 
            this.ColumnNote.FillWeight = 120F;
            this.ColumnNote.HeaderText = "备注";
            this.ColumnNote.MinimumWidth = 100;
            this.ColumnNote.Name = "ColumnNote";
            // 
            // groupBoxSources
            // 
            this.groupBoxSources.Controls.Add(this.treeViewSources);
            this.groupBoxSources.Controls.Add(this.panelSourceBottom);
            this.groupBoxSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSources.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSources.Name = "groupBoxSources";
            this.groupBoxSources.Size = new System.Drawing.Size(356, 440);
            this.groupBoxSources.TabIndex = 0;
            this.groupBoxSources.TabStop = false;
            this.groupBoxSources.Text = "内部结果";
            // 
            // treeViewSources
            // 
            this.treeViewSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewSources.HideSelection = false;
            this.treeViewSources.Location = new System.Drawing.Point(3, 21);
            this.treeViewSources.Name = "treeViewSources";
            this.treeViewSources.Size = new System.Drawing.Size(350, 374);
            this.treeViewSources.TabIndex = 0;
            this.treeViewSources.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewSources_NodeMouseDoubleClick);
            // 
            // panelSourceBottom
            // 
            this.panelSourceBottom.Controls.Add(this.buttonSelectProperty);
            this.panelSourceBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSourceBottom.Location = new System.Drawing.Point(3, 395);
            this.panelSourceBottom.Name = "panelSourceBottom";
            this.panelSourceBottom.Size = new System.Drawing.Size(350, 42);
            this.panelSourceBottom.TabIndex = 1;
            // 
            // buttonSelectProperty
            // 
            this.buttonSelectProperty.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelectProperty.Location = new System.Drawing.Point(236, 7);
            this.buttonSelectProperty.Name = "buttonSelectProperty";
            this.buttonSelectProperty.Size = new System.Drawing.Size(108, 28);
            this.buttonSelectProperty.TabIndex = 0;
            this.buttonSelectProperty.Text = "设为来源";
            this.buttonSelectProperty.UseVisualStyleBackColor = true;
            this.buttonSelectProperty.Click += new System.EventHandler(this.buttonSelectProperty_Click);
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
            this.panelButtons.Location = new System.Drawing.Point(3, 491);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(994, 42);
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
            this.buttonCancel.Location = new System.Drawing.Point(827, 7);
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
            this.buttonSave.Location = new System.Drawing.Point(914, 7);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(80, 28);
            this.buttonSave.TabIndex = 4;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCompositeOutput
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 560);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormCompositeOutput";
            this.Padding = new System.Windows.Forms.Padding(12);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "组合输出";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPorts)).EndInit();
            this.groupBoxSources.ResumeLayout(false);
            this.panelSourceBottom.ResumeLayout(false);
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.Label labelTip;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.DataGridView dataGridViewPorts;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnValueType;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnNote;
        private System.Windows.Forms.GroupBox groupBoxSources;
        private System.Windows.Forms.TreeView treeViewSources;
        private System.Windows.Forms.Panel panelSourceBottom;
        private System.Windows.Forms.Button buttonSelectProperty;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonMoveUp;
        private System.Windows.Forms.Button buttonMoveDown;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
    }
}
