namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    partial class NodeParamFormArithmeticOperation
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理正在使用的资源。
        /// </summary>
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
        /// 设计器支持所需的方法。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.numericUpDownDecimalPlaces = new System.Windows.Forms.NumericUpDown();
            this.labelDecimalPlaces = new System.Windows.Forms.Label();
            this.labelTip = new System.Windows.Forms.Label();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.dataGridViewRows = new System.Windows.Forms.DataGridView();
            this.ColumnEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnValue1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnOperator = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnValue2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnOutputVariableName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tableLayoutPanelRight = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxOperand = new System.Windows.Forms.GroupBox();
            this.buttonApplyConstant = new System.Windows.Forms.Button();
            this.textBoxConstant = new System.Windows.Forms.TextBox();
            this.labelConstant = new System.Windows.Forms.Label();
            this.radioButtonValue2 = new System.Windows.Forms.RadioButton();
            this.radioButtonValue1 = new System.Windows.Forms.RadioButton();
            this.groupBoxSources = new System.Windows.Forms.GroupBox();
            this.treeViewSources = new System.Windows.Forms.TreeView();
            this.panelSourceBottom = new System.Windows.Forms.Panel();
            this.buttonSelectProperty = new System.Windows.Forms.Button();
            this.groupBoxVariables = new System.Windows.Forms.GroupBox();
            this.listBoxVariables = new System.Windows.Forms.ListBox();
            this.panelVariableBottom = new System.Windows.Forms.Panel();
            this.buttonUseVariable = new System.Windows.Forms.Button();
            this.groupBoxResult = new System.Windows.Forms.GroupBox();
            this.labelRuntimeStatus = new System.Windows.Forms.Label();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.buttonMoveUp = new System.Windows.Forms.Button();
            this.buttonMoveDown = new System.Windows.Forms.Button();
            this.buttonRun = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDecimalPlaces)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRows)).BeginInit();
            this.tableLayoutPanelRight.SuspendLayout();
            this.groupBoxOperand.SuspendLayout();
            this.groupBoxSources.SuspendLayout();
            this.panelSourceBottom.SuspendLayout();
            this.groupBoxVariables.SuspendLayout();
            this.panelVariableBottom.SuspendLayout();
            this.groupBoxResult.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.panelTop, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.splitContainerMain, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.groupBoxResult, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.panelBottom, 0, 3);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 4;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 62F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1220, 724);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.numericUpDownDecimalPlaces);
            this.panelTop.Controls.Add(this.labelDecimalPlaces);
            this.panelTop.Controls.Add(this.labelTip);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(3, 3);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1214, 56);
            this.panelTop.TabIndex = 0;
            // 
            // numericUpDownDecimalPlaces
            // 
            this.numericUpDownDecimalPlaces.Font = new System.Drawing.Font("宋体", 10.5F);
            this.numericUpDownDecimalPlaces.Location = new System.Drawing.Point(126, 15);
            this.numericUpDownDecimalPlaces.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.numericUpDownDecimalPlaces.Name = "numericUpDownDecimalPlaces";
            this.numericUpDownDecimalPlaces.Size = new System.Drawing.Size(94, 27);
            this.numericUpDownDecimalPlaces.TabIndex = 1;
            // 
            // labelDecimalPlaces
            // 
            this.labelDecimalPlaces.AutoSize = true;
            this.labelDecimalPlaces.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelDecimalPlaces.Location = new System.Drawing.Point(18, 19);
            this.labelDecimalPlaces.Name = "labelDecimalPlaces";
            this.labelDecimalPlaces.Size = new System.Drawing.Size(98, 18);
            this.labelDecimalPlaces.TabIndex = 0;
            this.labelDecimalPlaces.Text = "结果小数位";
            // 
            // labelTip
            // 
            this.labelTip.AutoSize = true;
            this.labelTip.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelTip.ForeColor = System.Drawing.Color.DimGray;
            this.labelTip.Location = new System.Drawing.Point(245, 19);
            this.labelTip.Name = "labelTip";
            this.labelTip.Size = new System.Drawing.Size(692, 18);
            this.labelTip.TabIndex = 2;
            this.labelTip.Text = "每行独立计算：值1 与 值2 做四则运算，并输出一个可订阅变量名";
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(3, 65);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.dataGridViewRows);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.tableLayoutPanelRight);
            this.splitContainerMain.Size = new System.Drawing.Size(1214, 506);
            this.splitContainerMain.SplitterDistance = 838;
            this.splitContainerMain.TabIndex = 1;
            // 
            // dataGridViewRows
            // 
            this.dataGridViewRows.AllowUserToAddRows = false;
            this.dataGridViewRows.AllowUserToDeleteRows = false;
            this.dataGridViewRows.AutoGenerateColumns = false;
            this.dataGridViewRows.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dataGridViewRows.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewRows.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnEnabled,
            this.ColumnValue1,
            this.ColumnOperator,
            this.ColumnValue2,
            this.ColumnOutputVariableName,
            this.ColumnNote});
            this.dataGridViewRows.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewRows.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewRows.MultiSelect = false;
            this.dataGridViewRows.Name = "dataGridViewRows";
            this.dataGridViewRows.RowHeadersWidth = 30;
            this.dataGridViewRows.RowTemplate.Height = 32;
            this.dataGridViewRows.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewRows.Size = new System.Drawing.Size(838, 506);
            this.dataGridViewRows.TabIndex = 0;
            this.dataGridViewRows.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewRows_CellEndEdit);
            this.dataGridViewRows.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewRows_DataError);
            this.dataGridViewRows.SelectionChanged += new System.EventHandler(this.dataGridViewRows_SelectionChanged);
            // 
            // ColumnEnabled
            // 
            this.ColumnEnabled.DataPropertyName = "Enabled";
            this.ColumnEnabled.FillWeight = 45F;
            this.ColumnEnabled.HeaderText = "启用";
            this.ColumnEnabled.MinimumWidth = 45;
            this.ColumnEnabled.Name = "ColumnEnabled";
            // 
            // ColumnValue1
            // 
            this.ColumnValue1.DataPropertyName = "Value1Summary";
            this.ColumnValue1.FillWeight = 155F;
            this.ColumnValue1.HeaderText = "值1";
            this.ColumnValue1.MinimumWidth = 150;
            this.ColumnValue1.Name = "ColumnValue1";
            this.ColumnValue1.ReadOnly = true;
            // 
            // ColumnOperator
            // 
            this.ColumnOperator.DataPropertyName = "Operator";
            this.ColumnOperator.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.ColumnOperator.FillWeight = 58F;
            this.ColumnOperator.HeaderText = "运算";
            this.ColumnOperator.MinimumWidth = 58;
            this.ColumnOperator.Name = "ColumnOperator";
            this.ColumnOperator.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnOperator.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // ColumnValue2
            // 
            this.ColumnValue2.DataPropertyName = "Value2Summary";
            this.ColumnValue2.FillWeight = 155F;
            this.ColumnValue2.HeaderText = "值2";
            this.ColumnValue2.MinimumWidth = 150;
            this.ColumnValue2.Name = "ColumnValue2";
            this.ColumnValue2.ReadOnly = true;
            // 
            // ColumnOutputVariableName
            // 
            this.ColumnOutputVariableName.DataPropertyName = "OutputVariableName";
            this.ColumnOutputVariableName.FillWeight = 100F;
            this.ColumnOutputVariableName.HeaderText = "输出变量名";
            this.ColumnOutputVariableName.MinimumWidth = 95;
            this.ColumnOutputVariableName.Name = "ColumnOutputVariableName";
            // 
            // ColumnNote
            // 
            this.ColumnNote.DataPropertyName = "Note";
            this.ColumnNote.FillWeight = 120F;
            this.ColumnNote.HeaderText = "注释";
            this.ColumnNote.MinimumWidth = 100;
            this.ColumnNote.Name = "ColumnNote";
            // 
            // tableLayoutPanelRight
            // 
            this.tableLayoutPanelRight.ColumnCount = 1;
            this.tableLayoutPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxOperand, 0, 0);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxSources, 0, 1);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxVariables, 0, 2);
            this.tableLayoutPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRight.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelRight.Name = "tableLayoutPanelRight";
            this.tableLayoutPanelRight.RowCount = 3;
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 118F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanelRight.Size = new System.Drawing.Size(372, 506);
            this.tableLayoutPanelRight.TabIndex = 0;
            // 
            // groupBoxOperand
            // 
            this.groupBoxOperand.Controls.Add(this.buttonApplyConstant);
            this.groupBoxOperand.Controls.Add(this.textBoxConstant);
            this.groupBoxOperand.Controls.Add(this.labelConstant);
            this.groupBoxOperand.Controls.Add(this.radioButtonValue2);
            this.groupBoxOperand.Controls.Add(this.radioButtonValue1);
            this.groupBoxOperand.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxOperand.Font = new System.Drawing.Font("宋体", 10.5F);
            this.groupBoxOperand.Location = new System.Drawing.Point(3, 3);
            this.groupBoxOperand.Name = "groupBoxOperand";
            this.groupBoxOperand.Size = new System.Drawing.Size(366, 112);
            this.groupBoxOperand.TabIndex = 0;
            this.groupBoxOperand.TabStop = false;
            this.groupBoxOperand.Text = "当前行值设置";
            // 
            // buttonApplyConstant
            // 
            this.buttonApplyConstant.Location = new System.Drawing.Point(246, 68);
            this.buttonApplyConstant.Name = "buttonApplyConstant";
            this.buttonApplyConstant.Size = new System.Drawing.Size(106, 32);
            this.buttonApplyConstant.TabIndex = 4;
            this.buttonApplyConstant.Text = "应用常量";
            this.buttonApplyConstant.UseVisualStyleBackColor = true;
            this.buttonApplyConstant.Click += new System.EventHandler(this.buttonApplyConstant_Click);
            // 
            // textBoxConstant
            // 
            this.textBoxConstant.Location = new System.Drawing.Point(68, 70);
            this.textBoxConstant.Name = "textBoxConstant";
            this.textBoxConstant.Size = new System.Drawing.Size(166, 27);
            this.textBoxConstant.TabIndex = 3;
            this.textBoxConstant.Text = "0";
            // 
            // labelConstant
            // 
            this.labelConstant.AutoSize = true;
            this.labelConstant.Location = new System.Drawing.Point(15, 74);
            this.labelConstant.Name = "labelConstant";
            this.labelConstant.Size = new System.Drawing.Size(44, 18);
            this.labelConstant.TabIndex = 2;
            this.labelConstant.Text = "常量";
            // 
            // radioButtonValue2
            // 
            this.radioButtonValue2.AutoSize = true;
            this.radioButtonValue2.Location = new System.Drawing.Point(97, 31);
            this.radioButtonValue2.Name = "radioButtonValue2";
            this.radioButtonValue2.Size = new System.Drawing.Size(69, 22);
            this.radioButtonValue2.TabIndex = 1;
            this.radioButtonValue2.Text = "值2";
            this.radioButtonValue2.UseVisualStyleBackColor = true;
            // 
            // radioButtonValue1
            // 
            this.radioButtonValue1.AutoSize = true;
            this.radioButtonValue1.Checked = true;
            this.radioButtonValue1.Location = new System.Drawing.Point(18, 31);
            this.radioButtonValue1.Name = "radioButtonValue1";
            this.radioButtonValue1.Size = new System.Drawing.Size(69, 22);
            this.radioButtonValue1.TabIndex = 0;
            this.radioButtonValue1.TabStop = true;
            this.radioButtonValue1.Text = "值1";
            this.radioButtonValue1.UseVisualStyleBackColor = true;
            // 
            // groupBoxSources
            // 
            this.groupBoxSources.Controls.Add(this.treeViewSources);
            this.groupBoxSources.Controls.Add(this.panelSourceBottom);
            this.groupBoxSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSources.Font = new System.Drawing.Font("宋体", 10.5F);
            this.groupBoxSources.Location = new System.Drawing.Point(3, 121);
            this.groupBoxSources.Name = "groupBoxSources";
            this.groupBoxSources.Size = new System.Drawing.Size(366, 246);
            this.groupBoxSources.TabIndex = 1;
            this.groupBoxSources.TabStop = false;
            this.groupBoxSources.Text = "上游可订阅数值";
            // 
            // treeViewSources
            // 
            this.treeViewSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewSources.HideSelection = false;
            this.treeViewSources.Location = new System.Drawing.Point(3, 23);
            this.treeViewSources.Name = "treeViewSources";
            this.treeViewSources.Size = new System.Drawing.Size(360, 175);
            this.treeViewSources.TabIndex = 0;
            this.treeViewSources.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewSources_NodeMouseDoubleClick);
            // 
            // panelSourceBottom
            // 
            this.panelSourceBottom.Controls.Add(this.buttonSelectProperty);
            this.panelSourceBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSourceBottom.Location = new System.Drawing.Point(3, 198);
            this.panelSourceBottom.Name = "panelSourceBottom";
            this.panelSourceBottom.Size = new System.Drawing.Size(360, 45);
            this.panelSourceBottom.TabIndex = 1;
            // 
            // buttonSelectProperty
            // 
            this.buttonSelectProperty.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelectProperty.Location = new System.Drawing.Point(234, 7);
            this.buttonSelectProperty.Name = "buttonSelectProperty";
            this.buttonSelectProperty.Size = new System.Drawing.Size(118, 32);
            this.buttonSelectProperty.TabIndex = 0;
            this.buttonSelectProperty.Text = "选择属性";
            this.buttonSelectProperty.UseVisualStyleBackColor = true;
            this.buttonSelectProperty.Click += new System.EventHandler(this.buttonSelectProperty_Click);
            // 
            // groupBoxVariables
            // 
            this.groupBoxVariables.Controls.Add(this.listBoxVariables);
            this.groupBoxVariables.Controls.Add(this.panelVariableBottom);
            this.groupBoxVariables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxVariables.Font = new System.Drawing.Font("宋体", 10.5F);
            this.groupBoxVariables.Location = new System.Drawing.Point(3, 373);
            this.groupBoxVariables.Name = "groupBoxVariables";
            this.groupBoxVariables.Size = new System.Drawing.Size(366, 130);
            this.groupBoxVariables.TabIndex = 2;
            this.groupBoxVariables.TabStop = false;
            this.groupBoxVariables.Text = "前序输出变量";
            // 
            // listBoxVariables
            // 
            this.listBoxVariables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxVariables.FormattingEnabled = true;
            this.listBoxVariables.ItemHeight = 17;
            this.listBoxVariables.Location = new System.Drawing.Point(3, 23);
            this.listBoxVariables.Name = "listBoxVariables";
            this.listBoxVariables.Size = new System.Drawing.Size(360, 59);
            this.listBoxVariables.TabIndex = 0;
            this.listBoxVariables.DoubleClick += new System.EventHandler(this.listBoxVariables_DoubleClick);
            // 
            // panelVariableBottom
            // 
            this.panelVariableBottom.Controls.Add(this.buttonUseVariable);
            this.panelVariableBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelVariableBottom.Location = new System.Drawing.Point(3, 82);
            this.panelVariableBottom.Name = "panelVariableBottom";
            this.panelVariableBottom.Size = new System.Drawing.Size(360, 45);
            this.panelVariableBottom.TabIndex = 1;
            // 
            // buttonUseVariable
            // 
            this.buttonUseVariable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonUseVariable.Location = new System.Drawing.Point(234, 7);
            this.buttonUseVariable.Name = "buttonUseVariable";
            this.buttonUseVariable.Size = new System.Drawing.Size(118, 32);
            this.buttonUseVariable.TabIndex = 0;
            this.buttonUseVariable.Text = "使用变量";
            this.buttonUseVariable.UseVisualStyleBackColor = true;
            this.buttonUseVariable.Click += new System.EventHandler(this.buttonUseVariable_Click);
            // 
            // groupBoxResult
            // 
            this.groupBoxResult.Controls.Add(this.labelRuntimeStatus);
            this.groupBoxResult.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxResult.Font = new System.Drawing.Font("宋体", 10.5F);
            this.groupBoxResult.Location = new System.Drawing.Point(3, 577);
            this.groupBoxResult.Name = "groupBoxResult";
            this.groupBoxResult.Size = new System.Drawing.Size(1214, 80);
            this.groupBoxResult.TabIndex = 2;
            this.groupBoxResult.TabStop = false;
            this.groupBoxResult.Text = "运行预览";
            // 
            // labelRuntimeStatus
            // 
            this.labelRuntimeStatus.AutoSize = true;
            this.labelRuntimeStatus.Location = new System.Drawing.Point(17, 32);
            this.labelRuntimeStatus.MaximumSize = new System.Drawing.Size(1160, 0);
            this.labelRuntimeStatus.Name = "labelRuntimeStatus";
            this.labelRuntimeStatus.Size = new System.Drawing.Size(116, 18);
            this.labelRuntimeStatus.TabIndex = 0;
            this.labelRuntimeStatus.Text = "暂无运行结果";
            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.buttonAdd);
            this.panelBottom.Controls.Add(this.buttonRemove);
            this.panelBottom.Controls.Add(this.buttonMoveUp);
            this.panelBottom.Controls.Add(this.buttonMoveDown);
            this.panelBottom.Controls.Add(this.buttonRun);
            this.panelBottom.Controls.Add(this.buttonCancel);
            this.panelBottom.Controls.Add(this.buttonSave);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBottom.Location = new System.Drawing.Point(3, 663);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1214, 58);
            this.panelBottom.TabIndex = 3;
            // 
            // buttonAdd
            // 
            this.buttonAdd.Location = new System.Drawing.Point(9, 12);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(92, 34);
            this.buttonAdd.TabIndex = 0;
            this.buttonAdd.Text = "添加行";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.buttonAdd_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Location = new System.Drawing.Point(111, 12);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(92, 34);
            this.buttonRemove.TabIndex = 1;
            this.buttonRemove.Text = "删除行";
            this.buttonRemove.UseVisualStyleBackColor = true;
            this.buttonRemove.Click += new System.EventHandler(this.buttonRemove_Click);
            // 
            // buttonMoveUp
            // 
            this.buttonMoveUp.Location = new System.Drawing.Point(213, 12);
            this.buttonMoveUp.Name = "buttonMoveUp";
            this.buttonMoveUp.Size = new System.Drawing.Size(92, 34);
            this.buttonMoveUp.TabIndex = 2;
            this.buttonMoveUp.Text = "上移";
            this.buttonMoveUp.UseVisualStyleBackColor = true;
            this.buttonMoveUp.Click += new System.EventHandler(this.buttonMoveUp_Click);
            // 
            // buttonMoveDown
            // 
            this.buttonMoveDown.Location = new System.Drawing.Point(315, 12);
            this.buttonMoveDown.Name = "buttonMoveDown";
            this.buttonMoveDown.Size = new System.Drawing.Size(92, 34);
            this.buttonMoveDown.TabIndex = 3;
            this.buttonMoveDown.Text = "下移";
            this.buttonMoveDown.UseVisualStyleBackColor = true;
            this.buttonMoveDown.Click += new System.EventHandler(this.buttonMoveDown_Click);
            // 
            // buttonRun
            // 
            this.buttonRun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRun.Location = new System.Drawing.Point(894, 12);
            this.buttonRun.Name = "buttonRun";
            this.buttonRun.Size = new System.Drawing.Size(92, 34);
            this.buttonRun.TabIndex = 4;
            this.buttonRun.Text = "运行";
            this.buttonRun.UseVisualStyleBackColor = true;
            this.buttonRun.Click += new System.EventHandler(this.buttonRun_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(1098, 12);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(92, 34);
            this.buttonCancel.TabIndex = 6;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(996, 12);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(92, 34);
            this.buttonSave.TabIndex = 5;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormArithmeticOperation
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1224, 764);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimizeBox = false;
            this.Name = "NodeParamFormArithmeticOperation";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "四则运算";
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownDecimalPlaces)).EndInit();
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewRows)).EndInit();
            this.tableLayoutPanelRight.ResumeLayout(false);
            this.groupBoxOperand.ResumeLayout(false);
            this.groupBoxOperand.PerformLayout();
            this.groupBoxSources.ResumeLayout(false);
            this.panelSourceBottom.ResumeLayout(false);
            this.groupBoxVariables.ResumeLayout(false);
            this.panelVariableBottom.ResumeLayout(false);
            this.groupBoxResult.ResumeLayout(false);
            this.groupBoxResult.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.NumericUpDown numericUpDownDecimalPlaces;
        private System.Windows.Forms.Label labelDecimalPlaces;
        private System.Windows.Forms.Label labelTip;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.DataGridView dataGridViewRows;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRight;
        private System.Windows.Forms.GroupBox groupBoxOperand;
        private System.Windows.Forms.Button buttonApplyConstant;
        private System.Windows.Forms.TextBox textBoxConstant;
        private System.Windows.Forms.Label labelConstant;
        private System.Windows.Forms.RadioButton radioButtonValue2;
        private System.Windows.Forms.RadioButton radioButtonValue1;
        private System.Windows.Forms.GroupBox groupBoxSources;
        private System.Windows.Forms.TreeView treeViewSources;
        private System.Windows.Forms.Panel panelSourceBottom;
        private System.Windows.Forms.Button buttonSelectProperty;
        private System.Windows.Forms.GroupBox groupBoxVariables;
        private System.Windows.Forms.ListBox listBoxVariables;
        private System.Windows.Forms.Panel panelVariableBottom;
        private System.Windows.Forms.Button buttonUseVariable;
        private System.Windows.Forms.GroupBox groupBoxResult;
        private System.Windows.Forms.Label labelRuntimeStatus;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.Button buttonMoveUp;
        private System.Windows.Forms.Button buttonMoveDown;
        private System.Windows.Forms.Button buttonRun;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnValue1;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnOperator;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnValue2;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnOutputVariableName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnNote;
    }
}
