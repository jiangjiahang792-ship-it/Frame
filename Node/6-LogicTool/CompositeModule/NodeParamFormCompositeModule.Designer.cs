namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    partial class NodeParamFormCompositeModule
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NodeParamFormCompositeModule));
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxSnapshot = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelSnapshot = new System.Windows.Forms.TableLayoutPanel();
            this.labelSourceProcess = new System.Windows.Forms.Label();
            this.comboBoxSourceProcess = new System.Windows.Forms.ComboBox();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.labelModuleName = new System.Windows.Forms.Label();
            this.textBoxModuleName = new System.Windows.Forms.TextBox();
            this.labelNodeCount = new System.Windows.Forms.Label();
            this.textBoxNodeCount = new System.Windows.Forms.TextBox();
            this.buttonImport = new System.Windows.Forms.Button();
            this.labelConnectionCount = new System.Windows.Forms.Label();
            this.textBoxConnectionCount = new System.Windows.Forms.TextBox();
            this.labelInputCount = new System.Windows.Forms.Label();
            this.textBoxInputCount = new System.Windows.Forms.TextBox();
            this.labelOutputCount = new System.Windows.Forms.Label();
            this.textBoxOutputCount = new System.Windows.Forms.TextBox();
            this.splitContainerPorts = new System.Windows.Forms.SplitContainer();
            this.groupBoxInputBindings = new System.Windows.Forms.GroupBox();
            this.dataGridViewInputBindings = new System.Windows.Forms.DataGridView();
            this.ColumnInputName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnInputType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnInputMode = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.ColumnInputConstant = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnInputSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnInputNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBoxSources = new System.Windows.Forms.GroupBox();
            this.treeViewSources = new System.Windows.Forms.TreeView();
            this.panelSourceBottom = new System.Windows.Forms.Panel();
            this.buttonSelectInputSource = new System.Windows.Forms.Button();
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.groupBoxSnapshot.SuspendLayout();
            this.tableLayoutPanelSnapshot.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerPorts)).BeginInit();
            this.splitContainerPorts.Panel1.SuspendLayout();
            this.splitContainerPorts.Panel2.SuspendLayout();
            this.splitContainerPorts.SuspendLayout();
            this.groupBoxInputBindings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewInputBindings)).BeginInit();
            this.groupBoxSources.SuspendLayout();
            this.panelSourceBottom.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelRoot
            // 
            this.tableLayoutPanelRoot.ColumnCount = 1;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.Controls.Add(this.groupBoxSnapshot, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.splitContainerPorts, 0, 1);
            this.tableLayoutPanelRoot.Controls.Add(this.flowLayoutPanelButtons, 0, 2);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.Location = new System.Drawing.Point(16, 52);
            this.tableLayoutPanelRoot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelRoot.Name = "tableLayoutPanelRoot";
            this.tableLayoutPanelRoot.RowCount = 3;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 204F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 58F));
            this.tableLayoutPanelRoot.Size = new System.Drawing.Size(1232, 700);
            this.tableLayoutPanelRoot.TabIndex = 0;
            // 
            // groupBoxSnapshot
            // 
            this.groupBoxSnapshot.Controls.Add(this.tableLayoutPanelSnapshot);
            this.groupBoxSnapshot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSnapshot.Location = new System.Drawing.Point(3, 4);
            this.groupBoxSnapshot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSnapshot.Name = "groupBoxSnapshot";
            this.groupBoxSnapshot.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSnapshot.Size = new System.Drawing.Size(1226, 196);
            this.groupBoxSnapshot.TabIndex = 0;
            this.groupBoxSnapshot.TabStop = false;
            this.groupBoxSnapshot.Text = "模块快照";
            // 
            // tableLayoutPanelSnapshot
            // 
            this.tableLayoutPanelSnapshot.ColumnCount = 6;
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 101F));
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 38F));
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 101F));
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31F));
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 101F));
            this.tableLayoutPanelSnapshot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31F));
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelSourceProcess, 0, 0);
            this.tableLayoutPanelSnapshot.Controls.Add(this.comboBoxSourceProcess, 1, 0);
            this.tableLayoutPanelSnapshot.Controls.Add(this.buttonRefresh, 5, 0);
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelModuleName, 0, 1);
            this.tableLayoutPanelSnapshot.Controls.Add(this.textBoxModuleName, 1, 1);
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelNodeCount, 0, 2);
            this.tableLayoutPanelSnapshot.Controls.Add(this.textBoxNodeCount, 1, 2);
            this.tableLayoutPanelSnapshot.Controls.Add(this.buttonImport, 5, 2);
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelConnectionCount, 2, 2);
            this.tableLayoutPanelSnapshot.Controls.Add(this.textBoxConnectionCount, 3, 2);
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelInputCount, 0, 3);
            this.tableLayoutPanelSnapshot.Controls.Add(this.textBoxInputCount, 1, 3);
            this.tableLayoutPanelSnapshot.Controls.Add(this.labelOutputCount, 2, 3);
            this.tableLayoutPanelSnapshot.Controls.Add(this.textBoxOutputCount, 3, 3);
            this.tableLayoutPanelSnapshot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelSnapshot.Location = new System.Drawing.Point(3, 25);
            this.tableLayoutPanelSnapshot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tableLayoutPanelSnapshot.Name = "tableLayoutPanelSnapshot";
            this.tableLayoutPanelSnapshot.Padding = new System.Windows.Forms.Padding(7);
            this.tableLayoutPanelSnapshot.RowCount = 4;
            this.tableLayoutPanelSnapshot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 41F));
            this.tableLayoutPanelSnapshot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 41F));
            this.tableLayoutPanelSnapshot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 41F));
            this.tableLayoutPanelSnapshot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 41F));
            this.tableLayoutPanelSnapshot.Size = new System.Drawing.Size(1220, 167);
            this.tableLayoutPanelSnapshot.TabIndex = 0;
            // 
            // labelSourceProcess
            // 
            this.labelSourceProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSourceProcess.Location = new System.Drawing.Point(10, 7);
            this.labelSourceProcess.Name = "labelSourceProcess";
            this.labelSourceProcess.Size = new System.Drawing.Size(95, 41);
            this.labelSourceProcess.TabIndex = 0;
            this.labelSourceProcess.Text = "来源流程";
            this.labelSourceProcess.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxSourceProcess
            // 
            this.tableLayoutPanelSnapshot.SetColumnSpan(this.comboBoxSourceProcess, 4);
            this.comboBoxSourceProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBoxSourceProcess.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSourceProcess.FormattingEnabled = true;
            this.comboBoxSourceProcess.Location = new System.Drawing.Point(111, 13);
            this.comboBoxSourceProcess.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.comboBoxSourceProcess.Name = "comboBoxSourceProcess";
            this.comboBoxSourceProcess.Size = new System.Drawing.Size(818, 26);
            this.comboBoxSourceProcess.TabIndex = 1;
            this.comboBoxSourceProcess.SelectedIndexChanged += new System.EventHandler(this.comboBoxSourceProcess_SelectedIndexChanged);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRefresh.Location = new System.Drawing.Point(935, 11);
            this.buttonRefresh.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(275, 33);
            this.buttonRefresh.TabIndex = 2;
            this.buttonRefresh.Text = "刷新";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // labelModuleName
            // 
            this.labelModuleName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelModuleName.Location = new System.Drawing.Point(10, 48);
            this.labelModuleName.Name = "labelModuleName";
            this.labelModuleName.Size = new System.Drawing.Size(95, 41);
            this.labelModuleName.TabIndex = 3;
            this.labelModuleName.Text = "模块名称";
            this.labelModuleName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxModuleName
            // 
            this.tableLayoutPanelSnapshot.SetColumnSpan(this.textBoxModuleName, 5);
            this.textBoxModuleName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxModuleName.Location = new System.Drawing.Point(111, 54);
            this.textBoxModuleName.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.textBoxModuleName.Name = "textBoxModuleName";
            this.textBoxModuleName.Size = new System.Drawing.Size(1099, 28);
            this.textBoxModuleName.TabIndex = 4;
            // 
            // labelNodeCount
            // 
            this.labelNodeCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelNodeCount.Location = new System.Drawing.Point(10, 89);
            this.labelNodeCount.Name = "labelNodeCount";
            this.labelNodeCount.Size = new System.Drawing.Size(95, 41);
            this.labelNodeCount.TabIndex = 5;
            this.labelNodeCount.Text = "内部节点";
            this.labelNodeCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxNodeCount
            // 
            this.textBoxNodeCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxNodeCount.Location = new System.Drawing.Point(111, 95);
            this.textBoxNodeCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.textBoxNodeCount.Name = "textBoxNodeCount";
            this.textBoxNodeCount.ReadOnly = true;
            this.textBoxNodeCount.Size = new System.Drawing.Size(337, 28);
            this.textBoxNodeCount.TabIndex = 6;
            // 
            // buttonImport
            // 
            this.buttonImport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonImport.Location = new System.Drawing.Point(935, 93);
            this.buttonImport.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonImport.Name = "buttonImport";
            this.buttonImport.Size = new System.Drawing.Size(275, 33);
            this.buttonImport.TabIndex = 7;
            this.buttonImport.Text = "导入快照";
            this.buttonImport.UseVisualStyleBackColor = true;
            this.buttonImport.Click += new System.EventHandler(this.buttonImport_Click);
            // 
            // labelConnectionCount
            // 
            this.labelConnectionCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelConnectionCount.Location = new System.Drawing.Point(454, 89);
            this.labelConnectionCount.Name = "labelConnectionCount";
            this.labelConnectionCount.Size = new System.Drawing.Size(95, 41);
            this.labelConnectionCount.TabIndex = 8;
            this.labelConnectionCount.Text = "内部连线";
            this.labelConnectionCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxConnectionCount
            // 
            this.textBoxConnectionCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxConnectionCount.Location = new System.Drawing.Point(555, 95);
            this.textBoxConnectionCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.textBoxConnectionCount.Name = "textBoxConnectionCount";
            this.textBoxConnectionCount.ReadOnly = true;
            this.textBoxConnectionCount.Size = new System.Drawing.Size(273, 28);
            this.textBoxConnectionCount.TabIndex = 9;
            // 
            // labelInputCount
            // 
            this.labelInputCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelInputCount.Location = new System.Drawing.Point(10, 130);
            this.labelInputCount.Name = "labelInputCount";
            this.labelInputCount.Size = new System.Drawing.Size(95, 41);
            this.labelInputCount.TabIndex = 10;
            this.labelInputCount.Text = "输入端口";
            this.labelInputCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxInputCount
            // 
            this.textBoxInputCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxInputCount.Location = new System.Drawing.Point(111, 136);
            this.textBoxInputCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.textBoxInputCount.Name = "textBoxInputCount";
            this.textBoxInputCount.ReadOnly = true;
            this.textBoxInputCount.Size = new System.Drawing.Size(337, 28);
            this.textBoxInputCount.TabIndex = 11;
            // 
            // labelOutputCount
            // 
            this.labelOutputCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelOutputCount.Location = new System.Drawing.Point(454, 130);
            this.labelOutputCount.Name = "labelOutputCount";
            this.labelOutputCount.Size = new System.Drawing.Size(95, 41);
            this.labelOutputCount.TabIndex = 12;
            this.labelOutputCount.Text = "输出端口";
            this.labelOutputCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxOutputCount
            // 
            this.textBoxOutputCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxOutputCount.Location = new System.Drawing.Point(555, 136);
            this.textBoxOutputCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 4);
            this.textBoxOutputCount.Name = "textBoxOutputCount";
            this.textBoxOutputCount.ReadOnly = true;
            this.textBoxOutputCount.Size = new System.Drawing.Size(273, 28);
            this.textBoxOutputCount.TabIndex = 13;
            // 
            // splitContainerPorts
            // 
            this.splitContainerPorts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerPorts.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerPorts.Location = new System.Drawing.Point(3, 208);
            this.splitContainerPorts.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.splitContainerPorts.Name = "splitContainerPorts";
            // 
            // splitContainerPorts.Panel1
            // 
            this.splitContainerPorts.Panel1.Controls.Add(this.groupBoxInputBindings);
            // 
            // splitContainerPorts.Panel2
            // 
            this.splitContainerPorts.Panel2.Controls.Add(this.groupBoxSources);
            this.splitContainerPorts.Size = new System.Drawing.Size(1226, 430);
            this.splitContainerPorts.SplitterDistance = 870;
            this.splitContainerPorts.TabIndex = 1;
            // 
            // groupBoxInputBindings
            // 
            this.groupBoxInputBindings.Controls.Add(this.dataGridViewInputBindings);
            this.groupBoxInputBindings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxInputBindings.Location = new System.Drawing.Point(0, 0);
            this.groupBoxInputBindings.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInputBindings.Name = "groupBoxInputBindings";
            this.groupBoxInputBindings.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxInputBindings.Size = new System.Drawing.Size(870, 430);
            this.groupBoxInputBindings.TabIndex = 0;
            this.groupBoxInputBindings.TabStop = false;
            this.groupBoxInputBindings.Text = "输入绑定";
            // 
            // dataGridViewInputBindings
            // 
            this.dataGridViewInputBindings.AllowUserToAddRows = false;
            this.dataGridViewInputBindings.AllowUserToDeleteRows = false;
            this.dataGridViewInputBindings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewInputBindings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnInputName,
            this.ColumnInputType,
            this.ColumnInputMode,
            this.ColumnInputConstant,
            this.ColumnInputSource,
            this.ColumnInputNote});
            this.dataGridViewInputBindings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewInputBindings.Location = new System.Drawing.Point(3, 25);
            this.dataGridViewInputBindings.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.dataGridViewInputBindings.MultiSelect = false;
            this.dataGridViewInputBindings.Name = "dataGridViewInputBindings";
            this.dataGridViewInputBindings.RowHeadersWidth = 28;
            this.dataGridViewInputBindings.RowTemplate.Height = 30;
            this.dataGridViewInputBindings.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewInputBindings.Size = new System.Drawing.Size(864, 401);
            this.dataGridViewInputBindings.TabIndex = 0;
            this.dataGridViewInputBindings.DataError += new System.Windows.Forms.DataGridViewDataErrorEventHandler(this.dataGridViewInputBindings_DataError);
            // 
            // ColumnInputName
            // 
            this.ColumnInputName.FillWeight = 88F;
            this.ColumnInputName.HeaderText = "端口名";
            this.ColumnInputName.MinimumWidth = 90;
            this.ColumnInputName.Name = "ColumnInputName";
            this.ColumnInputName.ReadOnly = true;
            this.ColumnInputName.Width = 150;
            // 
            // ColumnInputType
            // 
            this.ColumnInputType.FillWeight = 62F;
            this.ColumnInputType.HeaderText = "类型";
            this.ColumnInputType.MinimumWidth = 70;
            this.ColumnInputType.Name = "ColumnInputType";
            this.ColumnInputType.ReadOnly = true;
            this.ColumnInputType.Width = 150;
            // 
            // ColumnInputMode
            // 
            this.ColumnInputMode.FillWeight = 82F;
            this.ColumnInputMode.HeaderText = "来源";
            this.ColumnInputMode.MinimumWidth = 82;
            this.ColumnInputMode.Name = "ColumnInputMode";
            this.ColumnInputMode.Width = 150;
            // 
            // ColumnInputConstant
            // 
            this.ColumnInputConstant.FillWeight = 110F;
            this.ColumnInputConstant.HeaderText = "常量值";
            this.ColumnInputConstant.MinimumWidth = 100;
            this.ColumnInputConstant.Name = "ColumnInputConstant";
            this.ColumnInputConstant.Width = 150;
            // 
            // ColumnInputSource
            // 
            this.ColumnInputSource.FillWeight = 190F;
            this.ColumnInputSource.HeaderText = "订阅来源";
            this.ColumnInputSource.MinimumWidth = 150;
            this.ColumnInputSource.Name = "ColumnInputSource";
            this.ColumnInputSource.ReadOnly = true;
            this.ColumnInputSource.Width = 150;
            // 
            // ColumnInputNote
            // 
            this.ColumnInputNote.FillWeight = 120F;
            this.ColumnInputNote.HeaderText = "备注";
            this.ColumnInputNote.MinimumWidth = 100;
            this.ColumnInputNote.Name = "ColumnInputNote";
            this.ColumnInputNote.Width = 150;
            // 
            // groupBoxSources
            // 
            this.groupBoxSources.Controls.Add(this.treeViewSources);
            this.groupBoxSources.Controls.Add(this.panelSourceBottom);
            this.groupBoxSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSources.Location = new System.Drawing.Point(0, 0);
            this.groupBoxSources.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSources.Name = "groupBoxSources";
            this.groupBoxSources.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBoxSources.Size = new System.Drawing.Size(352, 430);
            this.groupBoxSources.TabIndex = 0;
            this.groupBoxSources.TabStop = false;
            this.groupBoxSources.Text = "外部上游结果";
            // 
            // treeViewSources
            // 
            this.treeViewSources.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewSources.HideSelection = false;
            this.treeViewSources.Location = new System.Drawing.Point(3, 25);
            this.treeViewSources.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.treeViewSources.Name = "treeViewSources";
            this.treeViewSources.Size = new System.Drawing.Size(346, 351);
            this.treeViewSources.TabIndex = 0;
            this.treeViewSources.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewSources_NodeMouseDoubleClick);
            // 
            // panelSourceBottom
            // 
            this.panelSourceBottom.Controls.Add(this.buttonSelectInputSource);
            this.panelSourceBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSourceBottom.Location = new System.Drawing.Point(3, 376);
            this.panelSourceBottom.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelSourceBottom.Name = "panelSourceBottom";
            this.panelSourceBottom.Size = new System.Drawing.Size(346, 50);
            this.panelSourceBottom.TabIndex = 1;
            // 
            // buttonSelectInputSource
            // 
            this.buttonSelectInputSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSelectInputSource.Location = new System.Drawing.Point(218, 8);
            this.buttonSelectInputSource.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSelectInputSource.Name = "buttonSelectInputSource";
            this.buttonSelectInputSource.Size = new System.Drawing.Size(122, 34);
            this.buttonSelectInputSource.TabIndex = 0;
            this.buttonSelectInputSource.Text = "设为输入";
            this.buttonSelectInputSource.UseVisualStyleBackColor = true;
            this.buttonSelectInputSource.Click += new System.EventHandler(this.buttonSelectInputSource_Click);
            // 
            // flowLayoutPanelButtons
            // 
            this.flowLayoutPanelButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonSave);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(3, 646);
            this.flowLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(1226, 50);
            this.flowLayoutPanelButtons.TabIndex = 2;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Location = new System.Drawing.Point(1133, 12);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(90, 34);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(1037, 12);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(90, 34);
            this.buttonSave.TabIndex = 0;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCompositeModule
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 768);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 5, 3, 5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormCompositeModule";
            this.Padding = new System.Windows.Forms.Padding(14);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "组合模块";
            this.Shown += new System.EventHandler(this.NodeParamFormCompositeModule_Shown);
            this.Controls.SetChildIndex(this.tableLayoutPanelRoot, 0);
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.groupBoxSnapshot.ResumeLayout(false);
            this.tableLayoutPanelSnapshot.ResumeLayout(false);
            this.tableLayoutPanelSnapshot.PerformLayout();
            this.splitContainerPorts.Panel1.ResumeLayout(false);
            this.splitContainerPorts.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerPorts)).EndInit();
            this.splitContainerPorts.ResumeLayout(false);
            this.groupBoxInputBindings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewInputBindings)).EndInit();
            this.groupBoxSources.ResumeLayout(false);
            this.panelSourceBottom.ResumeLayout(false);
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;
        private System.Windows.Forms.GroupBox groupBoxSnapshot;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelSnapshot;
        private System.Windows.Forms.Label labelSourceProcess;
        private System.Windows.Forms.ComboBox comboBoxSourceProcess;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Label labelModuleName;
        private System.Windows.Forms.TextBox textBoxModuleName;
        private System.Windows.Forms.Label labelNodeCount;
        private System.Windows.Forms.TextBox textBoxNodeCount;
        private System.Windows.Forms.Button buttonImport;
        private System.Windows.Forms.Label labelConnectionCount;
        private System.Windows.Forms.TextBox textBoxConnectionCount;
        private System.Windows.Forms.Label labelInputCount;
        private System.Windows.Forms.TextBox textBoxInputCount;
        private System.Windows.Forms.Label labelOutputCount;
        private System.Windows.Forms.TextBox textBoxOutputCount;
        private System.Windows.Forms.SplitContainer splitContainerPorts;
        private System.Windows.Forms.GroupBox groupBoxInputBindings;
        private System.Windows.Forms.DataGridView dataGridViewInputBindings;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInputName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInputType;
        private System.Windows.Forms.DataGridViewComboBoxColumn ColumnInputMode;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInputConstant;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInputSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnInputNote;
        private System.Windows.Forms.GroupBox groupBoxSources;
        private System.Windows.Forms.TreeView treeViewSources;
        private System.Windows.Forms.Panel panelSourceBottom;
        private System.Windows.Forms.Button buttonSelectInputSource;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
    }
}
