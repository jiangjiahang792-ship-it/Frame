using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行界面预览窗口的固定控件和布局。</summary>
    partial class RuntimeInterfacePreviewForm
    {
        /// <summary>设计器组件容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>标题区域和内容区域的主布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>预览标题和窗口按钮的固定四列布局。</summary>
        private TableLayoutPanel titleBar;
        /// <summary>支持拖动的预览标题。</summary>
        private Label titleLabel;
        /// <summary>窗口最小化按钮。</summary>
        private Button minimizeButton;
        /// <summary>窗口最大化和还原按钮。</summary>
        private Button maximizeButton;
        /// <summary>预览关闭按钮。</summary>
        private Button closeButton;
        /// <summary>窗口按钮中文提示。</summary>
        private ToolTip windowToolTip;
        /// <summary>预览填充区域。</summary>
        private Panel viewport;
        /// <summary>禁用编辑交互的同源画布。</summary>
        private RuntimeDesignCanvas canvas;
        /// <summary>显示侧刷新定时器，最多每秒 20 次，不参与生产流程调度。</summary>
        private Timer refreshTimer;

        /// <summary>释放窗口交互、图标和设计器组件。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer?.Stop();
                placementBinding?.Dispose();
                previewRunController?.Dispose();
                liveController?.Dispose();
                foreach (var widget in logWidgets.Values) widget.Dispose();
                logWidgets.Clear();
                foreach (var widget in tuningWidgets.Values) widget.Dispose();
                tuningWidgets.Clear();
                windowChrome?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>预览布局保持在 WinForms 设计器文件内。</summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            refreshTimer = new Timer(components) { Interval = 50 };
            rootLayout = new TableLayoutPanel();
            titleBar = new TableLayoutPanel();
            titleLabel = new Label();
            minimizeButton = new Button();
            maximizeButton = new Button();
            closeButton = new Button();
            windowToolTip = new ToolTip(components);
            viewport = new Panel();
            canvas = new RuntimeDesignCanvas();
            SuspendLayout();
            rootLayout.SuspendLayout();
            titleBar.SuspendLayout();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Margin = Padding.Empty;
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowCount = 2;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.Controls.Add(titleBar, 0, 0);
            rootLayout.Controls.Add(viewport, 0, 1);
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
            titleLabel.Padding = new Padding(18, 0, 0, 0);
            titleLabel.BackColor = titleBar.BackColor;
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.AutoEllipsis = true;
            titleLabel.Text = "运行界面预览（示例数据）";
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
            viewport.Dock = DockStyle.Fill;
            viewport.Margin = Padding.Empty;
            viewport.BackColor = Color.FromArgb(38, 40, 43);
            viewport.AutoScroll = false;
            viewport.Controls.Add(canvas);
            canvas.Location = Point.Empty;
            canvas.Dock = DockStyle.Fill;
            canvas.PreviewMode = true;
            canvas.ShowGrid = false;
            Controls.Add(rootLayout);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1200, 760);
            MinimumSize = new Size(480, 320);
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(6);
            BackColor = Color.FromArgb(33, 35, 38);
            Font = new Font("Microsoft YaHei UI", 9);
            Text = "运行界面预览（示例数据）";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            titleBar.ResumeLayout(false);
            rootLayout.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
