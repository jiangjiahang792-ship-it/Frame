using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>数据源选择窗口的全部固定控件和布局。</summary>
    partial class RuntimeBindingPickerForm
    {
        /// <summary>持有窗口按钮提示的设计器组件容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>标题、目录、路径与操作栏的主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>唯一标题栏及窗口操作的固定四列布局。</summary>
        private TableLayoutPanel titleBar;
        /// <summary>深色区域的中文标题。</summary>
        private Label titleLabel;
        /// <summary>选择器最小化按钮。</summary>
        private Button minimizeButton;
        /// <summary>选择器最大化和还原按钮。</summary>
        private Button maximizeButton;
        /// <summary>取消并关闭选择器的标题按钮。</summary>
        private Button closeButton;
        /// <summary>窗口按钮的中文悬停提示。</summary>
        private ToolTip windowToolTip;
        /// <summary>目录及空状态的显示区域。</summary>
        private Panel treePanel;
        /// <summary>按业务层级显示可绑定输出的树。</summary>
        private TreeView sourceTree;
        /// <summary>无目录项时的空状态。</summary>
        private Label emptyLabel;
        /// <summary>候选输出的完整中文路径。</summary>
        private Label selectionLabel;
        /// <summary>确认和取消按钮的固定操作栏。</summary>
        private TableLayoutPanel buttonLayout;
        /// <summary>只对有效叶子启用的确认按钮。</summary>
        private Button confirmButton;
        /// <summary>取消当前选择的按钮。</summary>
        private Button cancelButton;

        /// <summary>释放共享窗口交互拥有的图标及设计器组件。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                windowChrome?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>固定控件全部在设计器文件中创建，支持可视布局编辑。</summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new TableLayoutPanel();
            titleBar = new TableLayoutPanel();
            titleLabel = new Label();
            minimizeButton = new Button();
            maximizeButton = new Button();
            closeButton = new Button();
            windowToolTip = new ToolTip(components);
            treePanel = new Panel();
            sourceTree = new TreeView();
            emptyLabel = new Label();
            selectionLabel = new Label();
            buttonLayout = new TableLayoutPanel();
            confirmButton = new Button();
            cancelButton = new Button();
            SuspendLayout();
            rootLayout.SuspendLayout();
            titleBar.SuspendLayout();
            treePanel.SuspendLayout();
            buttonLayout.SuspendLayout();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Margin = Padding.Empty;
            rootLayout.BackColor = Color.FromArgb(244, 245, 247);
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowCount = 4;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            rootLayout.Controls.Add(titleBar, 0, 0);
            rootLayout.Controls.Add(treePanel, 0, 1);
            rootLayout.Controls.Add(selectionLabel, 0, 2);
            rootLayout.Controls.Add(buttonLayout, 0, 3);
            titleBar.Dock = DockStyle.Fill;
            titleBar.Margin = Padding.Empty;
            titleBar.BackColor = Color.FromArgb(33, 35, 38);
            titleBar.ColumnCount = 4;
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            titleBar.RowCount = 1;
            titleBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            titleBar.Controls.Add(titleLabel, 0, 0);
            titleBar.Controls.Add(minimizeButton, 1, 0);
            titleBar.Controls.Add(maximizeButton, 2, 0);
            titleBar.Controls.Add(closeButton, 3, 0);
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Margin = Padding.Empty;
            titleLabel.Padding = new Padding(12, 0, 0, 0);
            titleLabel.Text = "选择数据源";
            titleLabel.AutoEllipsis = true;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.BackColor = Color.FromArgb(33, 35, 38);
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            minimizeButton.Dock = DockStyle.Fill;
            minimizeButton.Margin = Padding.Empty;
            minimizeButton.FlatStyle = FlatStyle.Flat;
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 66, 71);
            minimizeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 83, 88);
            minimizeButton.BackColor = titleBar.BackColor;
            minimizeButton.ForeColor = Color.White;
            minimizeButton.UseVisualStyleBackColor = false;
            minimizeButton.TabStop = false;
            minimizeButton.AccessibleName = "最小化";
            windowToolTip.SetToolTip(minimizeButton, "最小化");
            maximizeButton.Dock = DockStyle.Fill;
            maximizeButton.Margin = Padding.Empty;
            maximizeButton.FlatStyle = FlatStyle.Flat;
            maximizeButton.FlatAppearance.BorderSize = 0;
            maximizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(63, 66, 71);
            maximizeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(79, 83, 88);
            maximizeButton.BackColor = titleBar.BackColor;
            maximizeButton.ForeColor = Color.White;
            maximizeButton.UseVisualStyleBackColor = false;
            maximizeButton.TabStop = false;
            maximizeButton.AccessibleName = "最大化";
            windowToolTip.SetToolTip(maximizeButton, "最大化");
            closeButton.Dock = DockStyle.Fill;
            closeButton.Margin = Padding.Empty;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(196, 43, 28);
            closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 32, 24);
            closeButton.BackColor = titleBar.BackColor;
            closeButton.ForeColor = Color.White;
            closeButton.UseVisualStyleBackColor = false;
            closeButton.TabStop = false;
            closeButton.AccessibleName = "关闭";
            windowToolTip.SetToolTip(closeButton, "关闭");
            treePanel.Dock = DockStyle.Fill;
            treePanel.Margin = new Padding(8, 8, 8, 0);
            treePanel.Controls.Add(sourceTree);
            treePanel.Controls.Add(emptyLabel);
            sourceTree.Dock = DockStyle.Fill;
            sourceTree.BorderStyle = BorderStyle.FixedSingle;
            sourceTree.BackColor = Color.FromArgb(250, 250, 251);
            sourceTree.ForeColor = Color.FromArgb(56, 62, 68);
            sourceTree.HideSelection = false;
            sourceTree.FullRowSelect = true;
            sourceTree.ShowNodeToolTips = true;
            sourceTree.ItemHeight = 26;
            sourceTree.Indent = 20;
            sourceTree.PathSeparator = " / ";
            sourceTree.AccessibleName = "可用数据源";
            sourceTree.TabIndex = 0;
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.Text = "暂无可用数据源";
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.ForeColor = Color.FromArgb(100, 106, 114);
            emptyLabel.BackColor = sourceTree.BackColor;
            emptyLabel.Visible = false;
            selectionLabel.Dock = DockStyle.Fill;
            selectionLabel.Margin = Padding.Empty;
            selectionLabel.Padding = new Padding(12, 0, 12, 0);
            selectionLabel.AutoEllipsis = true;
            selectionLabel.TextAlign = ContentAlignment.MiddleLeft;
            selectionLabel.Text = "未选择数据源";
            buttonLayout.Dock = DockStyle.Fill;
            buttonLayout.Margin = Padding.Empty;
            buttonLayout.Padding = new Padding(8, 4, 8, 8);
            buttonLayout.ColumnCount = 3;
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            buttonLayout.RowCount = 1;
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            buttonLayout.Controls.Add(confirmButton, 1, 0);
            buttonLayout.Controls.Add(cancelButton, 2, 0);
            confirmButton.Dock = DockStyle.Fill;
            confirmButton.Margin = new Padding(4, 0, 4, 0);
            confirmButton.Text = "确定";
            confirmButton.FlatStyle = FlatStyle.Flat;
            confirmButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            confirmButton.Enabled = false;
            confirmButton.TabIndex = 1;
            cancelButton.Dock = DockStyle.Fill;
            cancelButton.Margin = new Padding(4, 0, 0, 0);
            cancelButton.Text = "取消";
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(199, 203, 208);
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.TabIndex = 2;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9);
            BackColor = Color.FromArgb(33, 35, 38);
            ClientSize = new Size(560, 520);
            MinimumSize = new Size(440, 340);
            Text = "选择数据源";
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(6);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AcceptButton = confirmButton;
            CancelButton = cancelButton;
            Controls.Add(rootLayout);
            buttonLayout.ResumeLayout(false);
            treePanel.ResumeLayout(false);
            titleBar.ResumeLayout(false);
            rootLayout.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
