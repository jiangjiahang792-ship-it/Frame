namespace TDJS_Vision.Forms.CameraAdd
{
    partial class SingleCamera3D
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
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.uiSwitchConnect = new Sunny.UI.UISwitch();
            this.labelCameraName = new System.Windows.Forms.Label();
            this.contextMenuStripMain = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.移除ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanelMain.SuspendLayout();
            this.contextMenuStripMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.BackColor = System.Drawing.Color.CadetBlue;
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 68F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.tableLayoutPanelMain.ContextMenuStrip = this.contextMenuStripMain;
            this.tableLayoutPanelMain.Controls.Add(this.uiSwitchConnect, 1, 0);
            this.tableLayoutPanelMain.Controls.Add(this.labelCameraName, 0, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 1;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(153, 35);
            this.tableLayoutPanelMain.TabIndex = 0;
            this.tableLayoutPanelMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleCamera3D_MouseClick);
            // 
            // uiSwitchConnect
            // 
            this.uiSwitchConnect.ActiveColor = System.Drawing.Color.Teal;
            this.uiSwitchConnect.Font = new System.Drawing.Font("宋体", 12F);
            this.uiSwitchConnect.InActiveColor = System.Drawing.Color.Silver;
            this.uiSwitchConnect.Location = new System.Drawing.Point(107, 3);
            this.uiSwitchConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSwitchConnect.Name = "uiSwitchConnect";
            this.uiSwitchConnect.Size = new System.Drawing.Size(43, 28);
            this.uiSwitchConnect.TabIndex = 0;
            this.uiSwitchConnect.Text = "连接";
            this.uiSwitchConnect.ValueChanged += new Sunny.UI.UISwitch.OnValueChanged(this.uiSwitchConnect_ValueChanged);
            this.uiSwitchConnect.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleCamera3D_MouseClick);
            // 
            // labelCameraName
            // 
            this.labelCameraName.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.labelCameraName.AutoSize = true;
            this.labelCameraName.ContextMenuStrip = this.contextMenuStripMain;
            this.labelCameraName.Font = new System.Drawing.Font("宋体", 10.5F);
            this.labelCameraName.Location = new System.Drawing.Point(18, 8);
            this.labelCameraName.Name = "labelCameraName";
            this.labelCameraName.Size = new System.Drawing.Size(68, 18);
            this.labelCameraName.TabIndex = 1;
            this.labelCameraName.Text = "3D相机";
            this.labelCameraName.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleCamera3D_MouseClick);
            // 
            // contextMenuStripMain
            // 
            this.contextMenuStripMain.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.移除ToolStripMenuItem});
            this.contextMenuStripMain.Name = "contextMenuStripMain";
            this.contextMenuStripMain.Size = new System.Drawing.Size(109, 28);
            // 
            // 移除ToolStripMenuItem
            // 
            this.移除ToolStripMenuItem.Name = "移除ToolStripMenuItem";
            this.移除ToolStripMenuItem.Size = new System.Drawing.Size(108, 24);
            this.移除ToolStripMenuItem.Text = "移除";
            this.移除ToolStripMenuItem.Click += new System.EventHandler(this.移除ToolStripMenuItem_Click);
            // 
            // SingleCamera3D
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.Controls.Add(this.tableLayoutPanelMain);
            this.MaximumSize = new System.Drawing.Size(153, 35);
            this.Name = "SingleCamera3D";
            this.Size = new System.Drawing.Size(153, 35);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            this.contextMenuStripMain.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        /// <summary>
        /// 主布局面板。
        /// </summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>
        /// 3D 相机名称标签。
        /// </summary>
        private System.Windows.Forms.Label labelCameraName;

        /// <summary>
        /// 连接开关。
        /// </summary>
        public Sunny.UI.UISwitch uiSwitchConnect;

        /// <summary>
        /// 右键菜单。
        /// </summary>
        private System.Windows.Forms.ContextMenuStrip contextMenuStripMain;

        /// <summary>
        /// 移除菜单项。
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem 移除ToolStripMenuItem;
    }
}
