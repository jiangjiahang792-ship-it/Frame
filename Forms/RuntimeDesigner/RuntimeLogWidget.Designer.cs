using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>日志控件固定布局，所有可视控件均可在设计器中查看。</summary>
    partial class RuntimeLogWidget
    {
        /// <summary>拥有提示组件的容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>固定工具栏与可伸缩列表布局。</summary>
        private TableLayoutPanel rootLayout;
        /// <summary>紧凑单行工具栏。</summary>
        private TableLayoutPanel toolbar;
        /// <summary>仅清空本地订阅的按钮。</summary>
        private Button clearButton;
        /// <summary>暂停或恢复显示刷新。</summary>
        private Button pauseButton;
        /// <summary>切换自动滚动。</summary>
        private Button scrollButton;
        /// <summary>窄尺寸的工具溢出入口。</summary>
        private Button overflowButton;
        /// <summary>最小尺寸共用的固定工具菜单。</summary>
        private ContextMenuStrip compactMenu;
        /// <summary>菜单清空命令。</summary>
        private ToolStripMenuItem clearMenuItem;
        /// <summary>菜单暂停命令。</summary>
        private ToolStripMenuItem pauseMenuItem;
        /// <summary>菜单自动滚动开关。</summary>
        private ToolStripMenuItem scrollMenuItem;
        /// <summary>菜单最低等级分组。</summary>
        private ToolStripMenuItem levelMenuItem;
        /// <summary>调试等级选项。</summary>
        private ToolStripMenuItem debugMenuItem;
        /// <summary>信息等级选项。</summary>
        private ToolStripMenuItem infoMenuItem;
        /// <summary>警告等级选项。</summary>
        private ToolStripMenuItem warnMenuItem;
        /// <summary>异常等级选项。</summary>
        private ToolStripMenuItem exceptionMenuItem;
        /// <summary>致命等级选项。</summary>
        private ToolStripMenuItem fatalMenuItem;
        /// <summary>最低等级选择器。</summary>
        private ComboBox levelFilter;
        /// <summary>可见条数与异常条数。</summary>
        private Label countLabel;
        /// <summary>固定高度行、支持横向滚动的虚拟列表。</summary>
        private ListView logList;
        /// <summary>时间列。</summary>
        private ColumnHeader timeColumn;
        /// <summary>中文等级列。</summary>
        private ColumnHeader levelColumn;
        /// <summary>完整来源路径列。</summary>
        private ColumnHeader sourceColumn;
        /// <summary>单行日志内容列。</summary>
        private ColumnHeader messageColumn;
        /// <summary>工具按钮的中文提示。</summary>
        private ToolTip commandToolTip;
        /// <summary>本控件拥有的界面字体。</summary>
        private Font uiFont;

        /// <summary>释放拥有的订阅、图标与组件，不操作生产日志源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var owned = source;
                source = null;
                try { owned?.Dispose(); }
                finally
                {
                    clearButton.Image = pauseButton.Image = scrollButton.Image = overflowButton.Image = null;
                    foreach (var icon in ownedIcons) icon.Dispose();
                    ownedIcons.Clear();
                    ArrayClearEntries();
                    components?.Dispose();
                    uiFont?.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>释放记录引用，防止关闭后的控件引用继续保留大量日志正文。</summary>
        private void ArrayClearEntries()
        {
            System.Array.Clear(entries, 0, entries.Length);
            visibleEntries.Clear();
            entryCount = errorCount = head = 0;
        }

        /// <summary>创建固定四列列表和320像素宽可容纳的图标工具栏。</summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            commandToolTip = new ToolTip(components) { Active = false, ShowAlways = false, InitialDelay = 500, ReshowDelay = 100, AutoPopDelay = 2500 };
            rootLayout = new TableLayoutPanel();
            toolbar = new TableLayoutPanel();
            clearButton = new Button();
            pauseButton = new Button();
            scrollButton = new Button();
            overflowButton = new Button();
            compactMenu = new ContextMenuStrip(components);
            clearMenuItem = new ToolStripMenuItem("清空日志");
            pauseMenuItem = new ToolStripMenuItem("暂停刷新");
            scrollMenuItem = new ToolStripMenuItem("自动滚动");
            levelMenuItem = new ToolStripMenuItem("最低日志等级");
            debugMenuItem = new ToolStripMenuItem("调试") { Tag = 0 };
            infoMenuItem = new ToolStripMenuItem("信息") { Tag = 1 };
            warnMenuItem = new ToolStripMenuItem("警告") { Tag = 2 };
            exceptionMenuItem = new ToolStripMenuItem("异常") { Tag = 3 };
            fatalMenuItem = new ToolStripMenuItem("致命") { Tag = 4 };
            levelMenuItem.DropDownItems.AddRange(new ToolStripItem[] { debugMenuItem, infoMenuItem, warnMenuItem, exceptionMenuItem, fatalMenuItem });
            compactMenu.Items.AddRange(new ToolStripItem[] { clearMenuItem, pauseMenuItem, scrollMenuItem, levelMenuItem });
            levelFilter = new ComboBox();
            countLabel = new Label();
            logList = new ListView();
            timeColumn = new ColumnHeader();
            levelColumn = new ColumnHeader();
            sourceColumn = new ColumnHeader();
            messageColumn = new ColumnHeader();
            uiFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            SuspendLayout();
            rootLayout.SuspendLayout();
            toolbar.SuspendLayout();
            Font = uiFont;
            BackColor = Color.FromArgb(32, 34, 36);
            ForeColor = Color.WhiteSmoke;
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Margin = Padding.Empty;
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.RowCount = 2;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            toolbar.Dock = DockStyle.Fill;
            toolbar.Margin = Padding.Empty;
            toolbar.Padding = new Padding(2, 1, 2, 1);
            toolbar.BackColor = Color.FromArgb(40, 42, 45);
            toolbar.ColumnCount = 6;
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            toolbar.RowCount = 1;
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (var button in new[] { clearButton, pauseButton, scrollButton, overflowButton })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(1);
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 66, 69);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 64, 40);
                button.BackColor = toolbar.BackColor;
                button.ForeColor = Color.WhiteSmoke;
                button.UseVisualStyleBackColor = false;
            }
            clearButton.AccessibleName = "清空日志";
            pauseButton.AccessibleName = "暂停刷新";
            scrollButton.AccessibleName = "自动滚动";
            overflowButton.AccessibleName = "日志工具";
            commandToolTip.SetToolTip(overflowButton, "日志工具");
            commandToolTip.SetToolTip(clearButton, "清空当前日志");
            levelFilter.Dock = DockStyle.Fill;
            levelFilter.Margin = new Padding(3, 1, 3, 1);
            levelFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            levelFilter.FlatStyle = FlatStyle.Flat;
            levelFilter.BackColor = toolbar.BackColor;
            levelFilter.ForeColor = Color.WhiteSmoke;
            levelFilter.AccessibleName = "最低日志等级";
            levelFilter.Items.AddRange(new object[] { "调试", "信息", "警告", "异常", "致命" });
            commandToolTip.SetToolTip(levelFilter, "最低日志等级");
            countLabel.Dock = DockStyle.Fill;
            countLabel.Margin = new Padding(4, 0, 2, 0);
            countLabel.TextAlign = ContentAlignment.MiddleRight;
            countLabel.AutoEllipsis = true;
            countLabel.ForeColor = Color.FromArgb(194, 198, 203);
            countLabel.Text = "0 条  异常 0";
            toolbar.Controls.Add(clearButton, 0, 0);
            toolbar.Controls.Add(pauseButton, 1, 0);
            toolbar.Controls.Add(scrollButton, 2, 0);
            toolbar.Controls.Add(levelFilter, 3, 0);
            toolbar.Controls.Add(countLabel, 4, 0);
            toolbar.Controls.Add(overflowButton, 5, 0);
            logList.Dock = DockStyle.Fill;
            logList.Margin = Padding.Empty;
            logList.BorderStyle = BorderStyle.None;
            logList.BackColor = BackColor;
            logList.ForeColor = ForeColor;
            logList.View = View.Details;
            logList.VirtualMode = true;
            logList.VirtualListSize = 0;
            logList.FullRowSelect = true;
            logList.HideSelection = false;
            logList.MultiSelect = false;
            logList.ShowGroups = false;
            logList.ShowItemToolTips = false;
            logList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            logList.AccessibleName = "运行日志";
            timeColumn.Text = "时间";
            timeColumn.Width = 104;
            levelColumn.Text = "等级";
            levelColumn.Width = 56;
            sourceColumn.Text = "来源";
            sourceColumn.Width = 220;
            messageColumn.Text = "内容";
            messageColumn.Width = 700;
            logList.Columns.AddRange(new[] { timeColumn, levelColumn, sourceColumn, messageColumn });
            rootLayout.Controls.Add(toolbar, 0, 0);
            rootLayout.Controls.Add(logList, 0, 1);
            Controls.Add(rootLayout);
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(640, 260);
            toolbar.ResumeLayout(false);
            rootLayout.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}
