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
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelRoot = new System.Windows.Forms.TableLayoutPanel();
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
            this.flowLayoutPanelButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelRoot.SuspendLayout();
            this.flowLayoutPanelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelRoot
            // 
            this.tableLayoutPanelRoot.ColumnCount = 3;
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this.tableLayoutPanelRoot.Controls.Add(this.labelSourceProcess, 0, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.comboBoxSourceProcess, 1, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.buttonRefresh, 2, 0);
            this.tableLayoutPanelRoot.Controls.Add(this.labelModuleName, 0, 1);
            this.tableLayoutPanelRoot.Controls.Add(this.textBoxModuleName, 1, 1);
            this.tableLayoutPanelRoot.Controls.Add(this.labelNodeCount, 0, 2);
            this.tableLayoutPanelRoot.Controls.Add(this.textBoxNodeCount, 1, 2);
            this.tableLayoutPanelRoot.Controls.Add(this.buttonImport, 2, 2);
            this.tableLayoutPanelRoot.Controls.Add(this.labelConnectionCount, 0, 3);
            this.tableLayoutPanelRoot.Controls.Add(this.textBoxConnectionCount, 1, 3);
            this.tableLayoutPanelRoot.Controls.Add(this.flowLayoutPanelButtons, 0, 4);
            this.tableLayoutPanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRoot.Location = new System.Drawing.Point(14, 14);
            this.tableLayoutPanelRoot.Name = "tableLayoutPanelRoot";
            this.tableLayoutPanelRoot.RowCount = 5;
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            this.tableLayoutPanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanelRoot.Size = new System.Drawing.Size(486, 190);
            this.tableLayoutPanelRoot.TabIndex = 0;
            // 
            // labelSourceProcess
            // 
            this.labelSourceProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSourceProcess.Location = new System.Drawing.Point(3, 0);
            this.labelSourceProcess.Name = "labelSourceProcess";
            this.labelSourceProcess.Size = new System.Drawing.Size(84, 36);
            this.labelSourceProcess.TabIndex = 0;
            this.labelSourceProcess.Text = "来源流程";
            this.labelSourceProcess.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // comboBoxSourceProcess
            // 
            this.comboBoxSourceProcess.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBoxSourceProcess.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxSourceProcess.FormattingEnabled = true;
            this.comboBoxSourceProcess.Location = new System.Drawing.Point(93, 6);
            this.comboBoxSourceProcess.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.comboBoxSourceProcess.Name = "comboBoxSourceProcess";
            this.comboBoxSourceProcess.Size = new System.Drawing.Size(294, 23);
            this.comboBoxSourceProcess.TabIndex = 1;
            this.comboBoxSourceProcess.SelectedIndexChanged += new System.EventHandler(this.comboBoxSourceProcess_SelectedIndexChanged);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRefresh.Location = new System.Drawing.Point(393, 4);
            this.buttonRefresh.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(90, 28);
            this.buttonRefresh.TabIndex = 2;
            this.buttonRefresh.Text = "刷新";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // labelModuleName
            // 
            this.labelModuleName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelModuleName.Location = new System.Drawing.Point(3, 36);
            this.labelModuleName.Name = "labelModuleName";
            this.labelModuleName.Size = new System.Drawing.Size(84, 36);
            this.labelModuleName.TabIndex = 3;
            this.labelModuleName.Text = "模块名称";
            this.labelModuleName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxModuleName
            // 
            this.tableLayoutPanelRoot.SetColumnSpan(this.textBoxModuleName, 2);
            this.textBoxModuleName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxModuleName.Location = new System.Drawing.Point(93, 42);
            this.textBoxModuleName.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.textBoxModuleName.Name = "textBoxModuleName";
            this.textBoxModuleName.Size = new System.Drawing.Size(390, 25);
            this.textBoxModuleName.TabIndex = 4;
            // 
            // labelNodeCount
            // 
            this.labelNodeCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelNodeCount.Location = new System.Drawing.Point(3, 72);
            this.labelNodeCount.Name = "labelNodeCount";
            this.labelNodeCount.Size = new System.Drawing.Size(84, 36);
            this.labelNodeCount.TabIndex = 5;
            this.labelNodeCount.Text = "内部节点";
            this.labelNodeCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxNodeCount
            // 
            this.textBoxNodeCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxNodeCount.Location = new System.Drawing.Point(93, 78);
            this.textBoxNodeCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.textBoxNodeCount.Name = "textBoxNodeCount";
            this.textBoxNodeCount.ReadOnly = true;
            this.textBoxNodeCount.Size = new System.Drawing.Size(294, 25);
            this.textBoxNodeCount.TabIndex = 6;
            // 
            // buttonImport
            // 
            this.buttonImport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonImport.Location = new System.Drawing.Point(393, 76);
            this.buttonImport.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonImport.Name = "buttonImport";
            this.buttonImport.Size = new System.Drawing.Size(90, 28);
            this.buttonImport.TabIndex = 7;
            this.buttonImport.Text = "导入快照";
            this.buttonImport.UseVisualStyleBackColor = true;
            this.buttonImport.Click += new System.EventHandler(this.buttonImport_Click);
            // 
            // labelConnectionCount
            // 
            this.labelConnectionCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelConnectionCount.Location = new System.Drawing.Point(3, 108);
            this.labelConnectionCount.Name = "labelConnectionCount";
            this.labelConnectionCount.Size = new System.Drawing.Size(84, 36);
            this.labelConnectionCount.TabIndex = 8;
            this.labelConnectionCount.Text = "内部连线";
            this.labelConnectionCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textBoxConnectionCount
            // 
            this.tableLayoutPanelRoot.SetColumnSpan(this.textBoxConnectionCount, 2);
            this.textBoxConnectionCount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxConnectionCount.Location = new System.Drawing.Point(93, 114);
            this.textBoxConnectionCount.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.textBoxConnectionCount.Name = "textBoxConnectionCount";
            this.textBoxConnectionCount.ReadOnly = true;
            this.textBoxConnectionCount.Size = new System.Drawing.Size(390, 25);
            this.textBoxConnectionCount.TabIndex = 9;
            // 
            // flowLayoutPanelButtons
            // 
            this.tableLayoutPanelRoot.SetColumnSpan(this.flowLayoutPanelButtons, 3);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonCancel);
            this.flowLayoutPanelButtons.Controls.Add(this.buttonSave);
            this.flowLayoutPanelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanelButtons.Location = new System.Drawing.Point(3, 147);
            this.flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            this.flowLayoutPanelButtons.Padding = new System.Windows.Forms.Padding(0, 7, 0, 0);
            this.flowLayoutPanelButtons.Size = new System.Drawing.Size(480, 40);
            this.flowLayoutPanelButtons.TabIndex = 10;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Location = new System.Drawing.Point(397, 10);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(80, 28);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "取消";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(311, 10);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(80, 28);
            this.buttonSave.TabIndex = 0;
            this.buttonSave.Text = "保存";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // NodeParamFormCompositeModule
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(514, 218);
            this.Controls.Add(this.tableLayoutPanelRoot);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NodeParamFormCompositeModule";
            this.Padding = new System.Windows.Forms.Padding(14);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "组合模块";
            this.Shown += new System.EventHandler(this.NodeParamFormCompositeModule_Shown);
            this.tableLayoutPanelRoot.ResumeLayout(false);
            this.tableLayoutPanelRoot.PerformLayout();
            this.flowLayoutPanelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRoot;
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
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;
    }
}
