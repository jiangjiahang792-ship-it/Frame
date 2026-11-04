namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>线扫相机列表控件设计器。</summary>
    partial class SingleLineScanCamera
    {
        /// <summary>设计器组件。</summary>
        private System.ComponentModel.IContainer components = null;
        /// <summary>主布局。</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>相机名称。</summary>
        private System.Windows.Forms.Label labelName;
        /// <summary>连接开关。</summary>
        private Sunny.UI.UISwitch uiSwitchConnect;
        /// <summary>右键菜单。</summary>
        private System.Windows.Forms.ContextMenuStrip contextMenu;
        /// <summary>移除菜单。</summary>
        private System.Windows.Forms.ToolStripMenuItem menuRemove;

        /// <summary>释放设计器资源。</summary>
        protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }

        /// <summary>初始化设计器控件。</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.labelName = new System.Windows.Forms.Label();
            this.uiSwitchConnect = new Sunny.UI.UISwitch();
            this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuRemove = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.menuRemove });
            this.menuRemove.Text = "移除";
            this.menuRemove.Click += new System.EventHandler(this.menuRemove_Click);
            this.tableLayoutPanelMain.BackColor = System.Drawing.Color.CadetBlue;
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 68F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.tableLayoutPanelMain.ContextMenuStrip = this.contextMenu;
            this.tableLayoutPanelMain.Controls.Add(this.labelName, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.uiSwitchConnect, 1, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleLineScanCamera_MouseClick);
            this.labelName.Text = "海康线扫相机";
            this.labelName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.labelName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelName.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleLineScanCamera_MouseClick);
            this.uiSwitchConnect.ActiveColor = System.Drawing.Color.Teal;
            this.uiSwitchConnect.InActiveColor = System.Drawing.Color.Silver;
            this.uiSwitchConnect.Size = new System.Drawing.Size(43, 28);
            this.uiSwitchConnect.ValueChanged += new Sunny.UI.UISwitch.OnValueChanged(this.uiSwitchConnect_ValueChanged);
            this.uiSwitchConnect.MouseClick += new System.Windows.Forms.MouseEventHandler(this.SingleLineScanCamera_MouseClick);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.MaximumSize = new System.Drawing.Size(190, 35);
            this.Size = new System.Drawing.Size(190, 35);
            this.Name = "SingleLineScanCamera";
        }
    }
}
