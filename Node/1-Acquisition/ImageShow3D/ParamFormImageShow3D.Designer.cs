namespace TDJS_Vision.Node._1_Acquisition.ImageShow3D
{
    partial class ParamFormImageShow3D
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，则为 true。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 初始化窗体控件。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelSubscription = new System.Windows.Forms.Label();
            this.nodeSubscription1 = new TDJS_Vision.Node.NodeSubscription();
            this.labelWindowName = new System.Windows.Forms.Label();
            this.comboBoxWindowName = new System.Windows.Forms.ComboBox();
            this.buttonSave = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanelMain.Controls.Add(this.labelSubscription, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.nodeSubscription1, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(this.labelWindowName, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.comboBoxWindowName, 1, 1);
            this.tableLayoutPanelMain.Controls.Add(this.buttonSave, 1, 2);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.Padding = new System.Windows.Forms.Padding(10);
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 26F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(436, 210);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // labelSubscription
            // 
            this.labelSubscription.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSubscription.Location = new System.Drawing.Point(13, 10);
            this.labelSubscription.Name = "labelSubscription";
            this.labelSubscription.Size = new System.Drawing.Size(139, 79);
            this.labelSubscription.TabIndex = 0;
            this.labelSubscription.Text = "订阅深度图";
            this.labelSubscription.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // nodeSubscription1
            // 
            this.nodeSubscription1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nodeSubscription1.Location = new System.Drawing.Point(158, 13);
            this.nodeSubscription1.MinimumSize = new System.Drawing.Size(213, 49);
            this.nodeSubscription1.Name = "nodeSubscription1";
            this.nodeSubscription1.Size = new System.Drawing.Size(265, 73);
            this.nodeSubscription1.TabIndex = 1;
            // 
            // labelWindowName
            // 
            this.labelWindowName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelWindowName.Location = new System.Drawing.Point(13, 89);
            this.labelWindowName.Name = "labelWindowName";
            this.labelWindowName.Size = new System.Drawing.Size(139, 60);
            this.labelWindowName.TabIndex = 2;
            this.labelWindowName.Text = "图像窗口";
            this.labelWindowName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // comboBoxWindowName
            // 
            this.comboBoxWindowName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxWindowName.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxWindowName.FormattingEnabled = true;
            this.comboBoxWindowName.Location = new System.Drawing.Point(158, 107);
            this.comboBoxWindowName.Name = "comboBoxWindowName";
            this.comboBoxWindowName.Size = new System.Drawing.Size(265, 23);
            this.comboBoxWindowName.TabIndex = 3;
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonSave.Location = new System.Drawing.Point(253, 166);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 28);
            this.buttonSave.TabIndex = 4;
            this.buttonSave.Text = "确定";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // ParamFormImageShow3D
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(440, 250);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "ParamFormImageShow3D";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "3D图像显示";
            this.Controls.SetChildIndex(this.tableLayoutPanelMain, 0);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        /// <summary>
        /// 主布局。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>
        /// 订阅标签。
        /// </summary>
        private System.Windows.Forms.Label labelSubscription;

        /// <summary>
        /// 订阅控件。
        /// </summary>
        private TDJS_Vision.Node.NodeSubscription nodeSubscription1;

        /// <summary>
        /// 窗口标签。
        /// </summary>
        private System.Windows.Forms.Label labelWindowName;

        /// <summary>
        /// 窗口下拉框。
        /// </summary>
        private System.Windows.Forms.ComboBox comboBoxWindowName;

        /// <summary>
        /// 保存按钮。
        /// </summary>
        private System.Windows.Forms.Button buttonSave;
    }
}
