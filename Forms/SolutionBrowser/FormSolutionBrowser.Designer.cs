namespace TDJS_Vision.Forms.SolutionBrowser
{
    /// <summary>方案选择窗体的可视化布局。</summary>
    partial class FormSolutionBrowser
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>搜索提示。</summary>
        private System.Windows.Forms.Label searchLabel;
        /// <summary>名称及路径搜索框。</summary>
        private System.Windows.Forms.TextBox searchBox;
        /// <summary>方案列表。</summary>
        private System.Windows.Forms.DataGridView solutionGrid;
        /// <summary>方案名称列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn nameColumn;
        /// <summary>修改时间列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn modifiedColumn;
        /// <summary>路径列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn pathColumn;
        /// <summary>列表状态提示。</summary>
        private System.Windows.Forms.Label statusLabel;
        /// <summary>刷新按钮。</summary>
        private System.Windows.Forms.Button refreshButton;
        /// <summary>浏览其他方案按钮。</summary>
        private System.Windows.Forms.Button browseButton;
        /// <summary>打开所选方案按钮。</summary>
        private System.Windows.Forms.Button openButton;
        /// <summary>取消按钮。</summary>
        private System.Windows.Forms.Button cancelButton;
        /// <summary>备用文件选择器。</summary>
        private System.Windows.Forms.OpenFileDialog browseDialog;

        /// <summary>释放窗体及文件选择器资源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>初始化全部控件，支持在 WinForms 设计器中调整布局。</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.searchLabel = new System.Windows.Forms.Label();
            this.searchBox = new System.Windows.Forms.TextBox();
            this.solutionGrid = new System.Windows.Forms.DataGridView();
            this.nameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.modifiedColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pathColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.statusLabel = new System.Windows.Forms.Label();
            this.refreshButton = new System.Windows.Forms.Button();
            this.browseButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.browseDialog = new System.Windows.Forms.OpenFileDialog();
            this.components.Add(this.browseDialog);
            ((System.ComponentModel.ISupportInitialize)(this.solutionGrid)).BeginInit();
            this.SuspendLayout();
            this.searchLabel.Location = new System.Drawing.Point(20, 23);
            this.searchLabel.Size = new System.Drawing.Size(90, 24);
            this.searchLabel.Text = "搜索方案：";
            this.searchBox.Location = new System.Drawing.Point(112, 20);
            this.searchBox.Size = new System.Drawing.Size(610, 27);
            this.searchBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.searchBox.TextChanged += new System.EventHandler(this.SearchBox_TextChanged);
            this.refreshButton.Location = new System.Drawing.Point(738, 18);
            this.refreshButton.Size = new System.Drawing.Size(120, 32);
            this.refreshButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.refreshButton.Text = "刷新列表";
            this.refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            this.solutionGrid.Location = new System.Drawing.Point(20, 65);
            this.solutionGrid.Size = new System.Drawing.Size(838, 372);
            this.solutionGrid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.solutionGrid.AutoGenerateColumns = false;
            this.solutionGrid.AllowUserToAddRows = false;
            this.solutionGrid.AllowUserToDeleteRows = false;
            this.solutionGrid.ReadOnly = true;
            this.solutionGrid.MultiSelect = false;
            this.solutionGrid.RowHeadersVisible = false;
            this.solutionGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.solutionGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.solutionGrid.BackgroundColor = System.Drawing.Color.White;
            this.solutionGrid.RowTemplate.Height = 32;
            this.solutionGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.nameColumn, this.modifiedColumn, this.pathColumn });
            this.solutionGrid.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.SolutionGrid_CellDoubleClick);
            this.nameColumn.DataPropertyName = "Name";
            this.nameColumn.HeaderText = "方案名称";
            this.nameColumn.FillWeight = 26F;
            this.modifiedColumn.DataPropertyName = "ModifiedAt";
            this.modifiedColumn.HeaderText = "修改时间";
            this.modifiedColumn.FillWeight = 24F;
            this.modifiedColumn.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
            this.pathColumn.DataPropertyName = "FullPath";
            this.pathColumn.HeaderText = "方案路径";
            this.pathColumn.FillWeight = 50F;
            this.statusLabel.Location = new System.Drawing.Point(20, 449);
            this.statusLabel.Size = new System.Drawing.Size(838, 26);
            this.statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.statusLabel.Text = "正在读取方案列表…";
            this.browseButton.Location = new System.Drawing.Point(20, 484);
            this.browseButton.Size = new System.Drawing.Size(166, 36);
            this.browseButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.browseButton.Text = "浏览其他方案";
            this.browseButton.Click += new System.EventHandler(this.BrowseButton_Click);
            this.openButton.Location = new System.Drawing.Point(590, 484);
            this.openButton.Size = new System.Drawing.Size(130, 36);
            this.openButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.openButton.Text = "打开方案";
            this.openButton.Enabled = false;
            this.openButton.Click += new System.EventHandler(this.OpenButton_Click);
            this.cancelButton.Location = new System.Drawing.Point(738, 484);
            this.cancelButton.Size = new System.Drawing.Size(120, 36);
            this.cancelButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.cancelButton.Text = "取消";
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.browseDialog.Filter = "视觉方案|*.Sol";
            this.browseDialog.Title = "浏览其他方案";
            this.browseDialog.CheckFileExists = true;
            this.AcceptButton = this.openButton;
            this.CancelButton = this.cancelButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(880, 540);
            this.MinimumSize = new System.Drawing.Size(780, 480);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.MinimizeBox = false;
            this.Text = "打开方案";
            this.Controls.Add(this.searchLabel);
            this.Controls.Add(this.searchBox);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.solutionGrid);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.browseButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.cancelButton);
            this.Shown += new System.EventHandler(this.FormSolutionBrowser_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.solutionGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
