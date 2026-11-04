namespace TDJS_Vision.Forms.SolRunParam
{
    partial class MyDataGridViewForm
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

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.RowHeadersWidth = 51;
            this.dataGridView1.RowTemplate.Height = 27;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView1.Size = new System.Drawing.Size(540, 326);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.ValidateCompensationCell);
            this.enableColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.enableColumn.Name = "Enable";
            this.enableColumn.HeaderText = "是否启用";
            this.enableColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.enableColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.nameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.nameColumn.Name = "Name";
            this.nameColumn.HeaderText = "检测项";
            this.nameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.nameColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.minColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.minColumn.Name = "MinValue";
            this.minColumn.HeaderText = "下限值";
            this.minColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.minColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.valueColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valueColumn.Name = "Value";
            this.valueColumn.HeaderText = "当前值";
            this.valueColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.valueColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.maxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.maxColumn.Name = "MaxValue";
            this.maxColumn.HeaderText = "上限值";
            this.maxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.maxColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.compensationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.compensationColumn.Name = "Compensation";
            this.compensationColumn.HeaderText = "补偿值";
            this.compensationColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.compensationColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.countColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.hiddenColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.countColumn.Name = "IsCountItem";
            this.countColumn.HeaderText = "数量型";
            this.countColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.countColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.hiddenColumn.Name = "IsManualTuningHidden";
            this.hiddenColumn.HeaderText = "是否隐藏";
            this.hiddenColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.hiddenColumn.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.nameColumn.ReadOnly = true;
            this.valueColumn.ReadOnly = true;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.enableColumn, this.nameColumn, this.minColumn, this.valueColumn, this.maxColumn, this.compensationColumn, this.countColumn, this.hiddenColumn });
            this.dataGridView1.ColumnHeadersHeight = 64;
            this.dataGridView1.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dataGridView1.RowTemplate.Height = 36;
            // 
            // MyDataGridViewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.dataGridView1);
            this.Name = "MyDataGridViewForm";
            this.Size = new System.Drawing.Size(540, 326);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        /// <summary>是否启用列。</summary>
        private System.Windows.Forms.DataGridViewCheckBoxColumn enableColumn;
        /// <summary>检测项列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn nameColumn;
        /// <summary>下限值列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn minColumn;
        /// <summary>当前值列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn valueColumn;
        /// <summary>上限值列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn maxColumn;
        /// <summary>补偿值列。</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn compensationColumn;
        /// <summary>数量型列。</summary>
        private System.Windows.Forms.DataGridViewCheckBoxColumn countColumn;
        /// <summary>非厂家权限隐藏列，仅厂家权限可见和编辑。</summary>
        private System.Windows.Forms.DataGridViewCheckBoxColumn hiddenColumn;
    }
}
