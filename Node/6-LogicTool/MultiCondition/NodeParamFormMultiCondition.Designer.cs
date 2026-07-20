namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    partial class NodeParamFormMultiCondition
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormMultiCondition));
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.labelMatchMode = new System.Windows.Forms.Label();
            this.comboBoxMatchMode = new System.Windows.Forms.ComboBox();
            this.labelMatchModeTip = new System.Windows.Forms.Label();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.dataGridViewConditions = new System.Windows.Forms.DataGridView();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnSourceNode = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnProperty = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnOperator = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnValue1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnValue2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnRunParam = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBoxSource = new System.Windows.Forms.GroupBox();
            this.treeViewSources = new System.Windows.Forms.TreeView();
            this.panelSourceBottom = new System.Windows.Forms.Panel();
            this.buttonSelectProperty = new System.Windows.Forms.Button();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewConditions)).BeginInit();
            this.groupBoxSource.SuspendLayout();
            this.panelSourceBottom.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.panelTop, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.splitContainerMain, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.panelBottom, 0, 2);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1211, 704);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.labelMatchMode);
            this.panelTop.Controls.Add(this.comboBoxMatchMode);
            this.panelTop.Controls.Add(this.labelMatchModeTip);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(3, 4);
            this.panelTop.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1205, 62);
            this.panelTop.TabIndex = 0;
            // 
            // labelMatchMode
            // 
            this.labelMatchMode.AutoSize = true;
            this.labelMatchMode.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelMatchMode.Location = new System.Drawing.Point(20, 20);
            this.labelMatchMode.Name = "labelMatchMode";
            this.labelMatchMode.Size = new System.Drawing.Size(94, 21);
            this.labelMatchMode.TabIndex = 0;
            this.labelMatchMode.Text = "判断方式";
            // 
            // comboBoxMatchMode
            // 
            this.comboBoxMatchMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxMatchMode.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.comboBoxMatchMode.FormattingEnabled = true;
            this.comboBoxMatchMode.Items.AddRange(new object[] {
            "全部满足",
            "任一满足"});
            this.comboBoxMatchMode.Location = new System.Drawing.Point(125, 16);
            this.comboBoxMatchMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxMatchMode.Name = "comboBoxMatchMode";
            this.comboBoxMatchMode.Size = new System.Drawing.Size(202, 29);
            this.comboBoxMatchMode.TabIndex = 1;
            // 
            // labelMatchModeTip
            // 
            this.labelMatchModeTip.AutoSize = true;
            this.labelMatchModeTip.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelMatchModeTip.ForeColor = System.Drawing.Color.DimGray;
            this.labelMatchModeTip.Location = new System.Drawing.Point(344, 20);
            this.labelMatchModeTip.Name = "labelMatchModeTip";
            this.labelMatchModeTip.Size = new System.Drawing.Size(566, 21);
            this.labelMatchModeTip.TabIndex = 2;
            this.labelMatchModeTip.Text = "True/False 分支按判定执行，默认分支始终执行";
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(3, 74);
            this.splitContainerMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.dataGridViewConditions);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.groupBoxSource);
            this.splitContainerMain.Size = new System.Drawing.Size(1205, 556);
            this.splitContainerMain.SplitterDistance = 873;
            this.splitContainerMain.TabIndex = 1;
            // 
            // dataGridViewConditions
            // 
            this.dataGridViewConditions.AllowUserToAddRows = false;
            this.dataGridViewConditions.AllowUserToDeleteRows = false;
            this.dataGridViewConditions.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewConditions.AutoGenerateColumns = false;
            this.dataGridViewConditions.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridViewConditions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewConditions.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnName,
            this.ColumnSourceNode,
            this.ColumnProperty,
            this.ColumnOperator,
            this.ColumnValue1,
            this.ColumnValue2,
            this.ColumnRunParam,
            this.ColumnNote});
            this.dataGridViewConditions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewConditions.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewConditions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.dataGridViewConditions.MultiSelect = false;
            this.dataGridViewConditions.Name = "dataGridViewConditions";
            this.dataGridViewConditions.RowHeadersWidth = 30;
            this.dataGridViewConditions.RowTemplate.Height = 32;
            this.dataGridViewConditions.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewConditions.Size = new System.Drawing.Size(873, 556);
            this.dataGridViewConditions.TabIndex = 0;
            // 
            // ColumnName
            // 
            this.ColumnName.DataPropertyName = "Name";
            this.ColumnName.FillWeight = 90F;
            this.ColumnName.HeaderText = "名称";
            this.ColumnName.MinimumWidth = 80;
            this.ColumnName.Name = "ColumnName";
            // 
            // ColumnSourceNode
            // 
            this.ColumnSourceNode.DataPropertyName = "SourceNodeText";
            this.ColumnSourceNode.FillWeight = 165F;
            this.ColumnSourceNode.HeaderText = "节点";
            this.ColumnSourceNode.MinimumWidth = 150;
            this.ColumnSourceNode.Name = "ColumnSourceNode";
            this.ColumnSourceNode.ReadOnly = true;
            // 
            // ColumnProperty
            // 
            this.ColumnProperty.DataPropertyName = "PropertyDisplayName";
            this.ColumnProperty.FillWeight = 230F;
            this.ColumnProperty.HeaderText = "属性";
            this.ColumnProperty.MinimumWidth = 190;
            this.ColumnProperty.Name = "ColumnProperty";
            this.ColumnProperty.ReadOnly = true;
            // 
            // ColumnOperator
            // 
            this.ColumnOperator.DataPropertyName = "Operator";
            this.ColumnOperator.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.ColumnOperator.FillWeight = 155F;
            this.ColumnOperator.HeaderText = "操作";
            this.ColumnOperator.MinimumWidth = 145;
            this.ColumnOperator.Name = "ColumnOperator";
            this.ColumnOperator.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnOperator.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // ColumnValue1
            // 
            this.ColumnValue1.DataPropertyName = "Value1";
            this.ColumnValue1.FillWeight = 95F;
            this.ColumnValue1.HeaderText = "值1";
            this.ColumnValue1.MinimumWidth = 80;
            this.ColumnValue1.Name = "ColumnValue1";
            // 
            // ColumnValue2
            // 
            this.ColumnValue2.DataPropertyName = "Value2";
            this.ColumnValue2.FillWeight = 95F;
            this.ColumnValue2.HeaderText = "值2";
            this.ColumnValue2.MinimumWidth = 80;
            this.ColumnValue2.Name = "ColumnValue2";
            // 
            // ColumnRunParam
            // 
            this.ColumnRunParam.DataPropertyName = "EnableRunParamAdjust";
            this.ColumnRunParam.FillWeight = 85F;
            this.ColumnRunParam.HeaderText = "运行参数";
            this.ColumnRunParam.MinimumWidth = 75;
            this.ColumnRunParam.Name = "ColumnRunParam";
            // 
            // ColumnNote
            // 
            this.ColumnNote.DataPropertyName = "Note";
            this.ColumnNote.FillWeight = 150F;
            this.ColumnNote.HeaderText = "注释";
            this.ColumnNote.MinimumWidth = 130;
            this.ColumnNote.Name = "ColumnNote";
            // 
            // groupBoxSource
            // 
            this.groupBoxSource.Controls.Add(this.treeViewSources);
            this.groupBoxSource.Controls.Add(this.panelSourceBottom);
            this.groupBoxSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSource.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxSource.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSource.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSource.Name = "groupBoxSource";
            this.groupBoxSource.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSource.Size = new System.Drawing.Size(328, 556);
            this.groupBoxSource.TabIndex = 0;
            this.groupBoxSource.TabStop = false;
            this.groupBoxSource.Text = "上游节点结果";
            // 
            // treeViewSources
            // 
            this.treeViewSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewSources.HideSelection = false;
            this.treeViewSources.Location = new System.Drawing.Point(3, 28);
            this.treeViewSources.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.treeViewSources.Name = "treeViewSources";
            this.treeViewSources.Size = new System.Drawing.Size(322, 469);
            this.treeViewSources.TabIndex = 0;
            this.treeViewSources.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewSources_NodeMouseDoubleClick);
            // 
            // panelSourceBottom
            // 
            this.panelSourceBottom.Controls.Add(this.buttonSelectProperty);
            this.panelSourceBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSourceBottom.Location = new System.Drawing.Point(3, 497);
            this.panelSourceBottom.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelSourceBottom.Name = "panelSourceBottom";
            this.panelSourceBottom.Size = new System.Drawing.Size(322, 55);
            this.panelSourceBottom.TabIndex = 1;
            // 
            // buttonSelectProperty
            // 
            this.buttonSelectProperty.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelectProperty.Location = new System.Drawing.Point(178, 10);
            this.buttonSelectProperty.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSelectProperty.Name = "buttonSelectProperty";
            this.buttonSelectProperty.Size = new System.Drawing.Size(133, 37);
            this.buttonSelectProperty.TabIndex = 0;
            this.buttonSelectProperty.Text = "选择属性";
            this.buttonSelectProperty.UseVisualStyleBackColor = true;
            this.buttonSelectProperty.Click += new System.EventHandler(this.buttonSelectProperty_Click);
            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.buttonAdd);
            this.panelBottom.Controls.Add(this.buttonRemove);
            this.panelBottom.Controls.Add(this.buttonCancel);
            this.panelBottom.Controls.Add(this.buttonSave);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBottom.Location = new System.Drawing.Point(3, 638);
            this.panelBottom.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1205, 62);
            this.panelBottom.TabIndex = 2;
            // 
            // buttonAdd
            // 
            this.buttonAdd.Location = new System.Drawing.Point(3, 12);
            this.buttonAdd.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(106, 38);
            this.buttonAdd.TabIndex = 0;
            this.buttonAdd.Text = "添加条件";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Location = new System.Drawing.Point(122, 12);
            this.buttonRemove.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(106, 38);
            this.buttonRemove.TabIndex = 1;
            this.buttonRemove.Text = "删除条件";
            this.buttonRemove.UseVisualStyleBackColor = true;
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(1084, 12);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(106, 38);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(966, 12);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(106, 38);
            this.buttonSave.TabIndex = 2;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormMultiCondition
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1215, 744);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimizeBox = false;
            this.Name = "NodeParamFormMultiCondition";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "多条件判断";
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewConditions)).EndInit();
            this.groupBoxSource.ResumeLayout(false);
            this.panelSourceBottom.ResumeLayout(false);
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label labelMatchMode;
        private System.Windows.Forms.ComboBox comboBoxMatchMode;
        private System.Windows.Forms.Label labelMatchModeTip;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.DataGridView dataGridViewConditions;
        private System.Windows.Forms.GroupBox groupBoxSource;
        private System.Windows.Forms.TreeView treeViewSources;
        private System.Windows.Forms.Panel panelSourceBottom;
        private System.Windows.Forms.Button buttonSelectProperty;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnSourceNode;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnProperty;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnOperator;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnValue1;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnValue2;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnRunParam;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnNote;
    }
}
