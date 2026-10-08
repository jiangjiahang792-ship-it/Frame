using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>只消费独立日志订阅的虚拟列表，不调用生产流程或全局日志清空。</summary>
    public partial class RuntimeLogWidget : UserControl
    {
        /// <summary>单次刷新最多提取的记录数，限制界面线程工作量。</summary>
        private const int BatchSize = 200;
        /// <summary>默认缓存容量。</summary>
        private const int DefaultCapacity = 2000;
        /// <summary>由本控件拥有并在换绑或关闭时释放的订阅。</summary>
        private IRuntimeLogSubscription source;
        /// <summary>按订阅返回顺序保存记录的环形缓存。</summary>
        private RuntimeLogEntry[] entries = new RuntimeLogEntry[DefaultCapacity];
        /// <summary>环形缓存中最早记录的位置。</summary>
        private int head;
        /// <summary>当前缓存条数。</summary>
        private int entryCount;
        /// <summary>缓存内异常和致命记录总数，不受等级过滤影响。</summary>
        private int errorCount;
        /// <summary>满足最低等级的记录引用，不复制日志正文。</summary>
        private readonly List<RuntimeLogEntry> visibleEntries = new List<RuntimeLogEntry>();
        /// <summary>最低显示等级，过滤不会删除原始缓存。</summary>
        private RuntimeLogLevel minimumLevel = RuntimeLogLevel.Info;
        /// <summary>暂停仅停止本控件提取，不影响生产或其它订阅。</summary>
        private bool paused;
        /// <summary>是否在新批次到达时滚动至末行。</summary>
        private bool autoScroll = true;
        /// <summary>避免初始化或换绑引发重复过滤。</summary>
        private bool updatingControls;
        /// <summary>宿主可每50毫秒调用，实际提取间隔至少100毫秒。</summary>
        private readonly Stopwatch refreshClock = new Stopwatch();
        /// <summary>控件拥有的工具栏位图。</summary>
        private readonly List<Bitmap> ownedIcons = new List<Bitmap>();
        /// <summary>暂停图标。</summary>
        private Bitmap pauseIcon;
        /// <summary>恢复图标。</summary>
        private Bitmap resumeIcon;

        /// <summary>初始化设计器中的固定控件和无生产副作用的交互。</summary>
        public RuntimeLogWidget()
        {
            InitializeComponent();
            clearButton.Image = CreateIcon(0xE74D);
            pauseIcon = CreateIcon(0xE769);
            resumeIcon = CreateIcon(0xE768);
            scrollButton.Image = CreateIcon(0xE74B);
            overflowButton.Image = CreateIcon(0xE712);
            clearButton.Click += ClearClicked;
            pauseButton.Click += PauseClicked;
            scrollButton.Click += ScrollClicked;
            levelFilter.SelectedIndexChanged += FilterChanged;
            logList.RetrieveVirtualItem += RetrieveItem;
            overflowButton.Click += OpenCompactMenu;
            clearMenuItem.Click += ClearClicked;
            pauseMenuItem.Click += PauseClicked;
            scrollMenuItem.Click += ScrollClicked;
            foreach (ToolStripMenuItem item in levelMenuItem.DropDownItems) item.Click += MenuLevelClicked;
            SizeChanged += LayoutToolbar;
            foreach (Control control in new Control[] { clearButton, pauseButton, scrollButton, levelFilter, countLabel, overflowButton })
            {
                control.MouseEnter += EnableToolTips;
                control.MouseLeave += DismissToolTips;
                control.MouseDown += DismissToolTips;
                control.KeyDown += DismissToolTips;
                control.LostFocus += DismissToolTips;
            }
            updatingControls = true;
            levelFilter.SelectedIndex = (int)minimumLevel;
            updatingControls = false;
            UpdateToolbar();
            UpdateCount();
            LayoutToolbar(this, EventArgs.Empty);
        }

        /// <summary>接管订阅；换绑释放旧订阅，同一实例重复绑定不释放且保留缓存。</summary>
        public void Bind(IRuntimeLogSubscription source, int capacity, RuntimeLogLevel minimumLevel, bool autoScroll)
        {
            if (IsDisposed || Disposing) throw new ObjectDisposedException(nameof(RuntimeLogWidget));
            int size = capacity <= 0 ? DefaultCapacity : Math.Max(100, Math.Min(10000, capacity));
            if (!ReferenceEquals(this.source, source))
            {
                var previous = this.source;
                this.source = source;
                previous?.Dispose();
                entries = new RuntimeLogEntry[size];
                head = entryCount = errorCount = 0;
            }
            else if (entries.Length != size)
            {
                var retained = new RuntimeLogEntry[Math.Min(entryCount, size)];
                for (int i = 0; i < retained.Length; i++)
                    retained[i] = entries[(head + entryCount - retained.Length + i) % entries.Length];
                entries = new RuntimeLogEntry[size];
                head = entryCount = errorCount = 0;
                foreach (var entry in retained) Append(entry);
            }
            this.minimumLevel = Enum.IsDefined(typeof(RuntimeLogLevel), minimumLevel) ? minimumLevel : RuntimeLogLevel.Info;
            this.autoScroll = autoScroll;
            paused = false;
            refreshClock.Reset();
            updatingControls = true;
            levelFilter.SelectedIndex = (int)this.minimumLevel;
            updatingControls = false;
            UpdateToolbar();
            RebuildVisible();
        }

        /// <summary>由宿主界面线程调用，按有界批次更新；空批次不重绘也不修改提示。</summary>
        public void RefreshLogs()
        {
            if (IsDisposed || Disposing || paused || source == null) return;
            if (refreshClock.IsRunning && refreshClock.ElapsedMilliseconds < 100) return;
            refreshClock.Restart();
            try
            {
                var batch = source.Drain(BatchSize);
                bool changed = false;
                if (batch != null)
                    for (int i = 0; i < Math.Min(batch.Count, BatchSize); i++)
                    {
                        if (batch[i] == null) continue;
                        Append(batch[i]);
                        changed = true;
                    }
                if (changed) RebuildVisible();
                else UpdateCount();
            }
            catch (Exception ex) { ShowReadError(ex); }
        }

        /// <summary>沿用布局配色；工具栏保持紧凑深色，错误等级保留红色强调。</summary>
        public void ApplyAppearance(Color background, Color foreground)
        {
            if (BackColor != background) BackColor = background;
            if (logList.BackColor != background) logList.BackColor = background;
            if (logList.ForeColor != foreground) logList.ForeColor = foreground;
            if (ForeColor != foreground) ForeColor = foreground;
        }

        /// <summary>追加引用并仅淘汰最旧条目，更新异常计数不遍历全部正文。</summary>
        private void Append(RuntimeLogEntry entry)
        {
            if (entryCount == entries.Length)
            {
                if (entries[head].Level >= RuntimeLogLevel.Exception) errorCount--;
                entries[head] = entry;
                head = (head + 1) % entries.Length;
            }
            else entries[(head + entryCount++) % entries.Length] = entry;
            if (entry.Level >= RuntimeLogLevel.Exception) errorCount++;
        }

        /// <summary>仅在批次或过滤变化时重建可见引用，非跟随模式尽量锚定当前阅读记录。</summary>
        private void RebuildVisible()
        {
            RuntimeLogEntry anchor = null;
            if (!autoScroll && logList.IsHandleCreated && logList.VirtualListSize > 0)
            {
                int index = logList.TopItem?.Index ?? -1;
                if (index >= 0 && index < visibleEntries.Count) anchor = visibleEntries[index];
            }
            logList.BeginUpdate();
            try
            {
                visibleEntries.Clear();
                for (int i = 0; i < entryCount; i++)
                {
                    var entry = entries[(head + i) % entries.Length];
                    if (entry.Level >= minimumLevel) visibleEntries.Add(entry);
                }
                logList.VirtualListSize = visibleEntries.Count;
                if (logList.IsHandleCreated && visibleEntries.Count > 0)
                {
                    if (autoScroll) logList.EnsureVisible(visibleEntries.Count - 1);
                    else if (anchor != null)
                    {
                        int index = Math.Max(0, visibleEntries.IndexOf(anchor));
                        if (logList.TopItem?.Index != index) logList.TopItem = logList.Items[index];
                    }
                }
            }
            finally { logList.EndUpdate(); }
            logList.Invalidate();
            UpdateCount();
        }

        /// <summary>只为列表请求的可见行生成显示项，单行文本保留横向滚动。</summary>
        private void RetrieveItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= visibleEntries.Count)
            { e.Item = new ListViewItem(new[] { "", "", "", "" }); return; }
            var entry = visibleEntries[e.ItemIndex];
            e.Item = new ListViewItem(new[] { entry.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                LevelName(entry.Level), SingleLine(entry.Source), SingleLine(entry.Message) });
            e.Item.ForeColor = entry.Level >= RuntimeLogLevel.Exception ? Color.FromArgb(255, 105, 105) :
                entry.Level == RuntimeLogLevel.Warn ? Color.FromArgb(225, 190, 104) : logList.ForeColor;
            e.Item.BackColor = logList.BackColor;
        }

        /// <summary>返回稳定中文等级别名。</summary>
        private static string LevelName(RuntimeLogLevel level)
        {
            switch (level)
            {
                case RuntimeLogLevel.Debug: return "调试";
                case RuntimeLogLevel.Info: return "信息";
                case RuntimeLogLevel.Warn: return "警告";
                case RuntimeLogLevel.Exception: return "异常";
                case RuntimeLogLevel.Fatal: return "致命";
                default: return "未知";
            }
        }

        /// <summary>仅显示时压平换行，不修改缓存中的原始日志。</summary>
        private static string SingleLine(string text) => (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

        /// <summary>清空本地与自己的订阅队列，不触碰生产日志。</summary>
        private void ClearClicked(object sender, EventArgs e)
        {
            DismissToolTips(sender, e);
            Array.Clear(entries, 0, entries.Length);
            head = entryCount = errorCount = 0;
            RebuildVisible();
            try { source?.Clear(); }
            catch (Exception ex) { ShowReadError(ex); }
        }

        /// <summary>切换显示暂停，不调用执行控制器。</summary>
        private void PauseClicked(object sender, EventArgs e)
        {
            DismissToolTips(sender, e);
            paused = !paused;
            refreshClock.Reset();
            UpdateToolbar();
        }

        /// <summary>切换自动滚动，关闭时不改变阅读位置。</summary>
        private void ScrollClicked(object sender, EventArgs e)
        {
            DismissToolTips(sender, e);
            autoScroll = !autoScroll;
            UpdateToolbar();
            if (autoScroll && logList.IsHandleCreated && visibleEntries.Count > 0)
                logList.EnsureVisible(visibleEntries.Count - 1);
        }

        /// <summary>过滤只影响可见引用，不删除环形缓存。</summary>
        private void FilterChanged(object sender, EventArgs e)
        {
            if (updatingControls || levelFilter.SelectedIndex < 0) return;
            DismissToolTips(sender, e);
            minimumLevel = (RuntimeLogLevel)levelFilter.SelectedIndex;
            UpdateToolbar();
            RebuildVisible();
        }

        /// <summary>窄尺寸使用单个溢出入口，避免最小布局中的固定列重叠或越界。</summary>
        private void LayoutToolbar(object sender, EventArgs e)
        {
            bool compact = ClientSize.Width < 320;
            toolbar.SuspendLayout();
            try
            {
                clearButton.Visible = pauseButton.Visible = scrollButton.Visible = levelFilter.Visible = countLabel.Visible = !compact;
                overflowButton.Visible = compact;
                for (int i = 0; i < 4; i++) toolbar.ColumnStyles[i].Width = compact ? 0 : i == 3 ? 78 : 28;
                toolbar.ColumnStyles[5].Width = compact ? 28 : 0;
                rootLayout.RowStyles[0].Height = Math.Min(ClientSize.Height, compact ? 26 : 30);
                logList.HeaderStyle = ClientSize.Height < 80 ? ColumnHeaderStyle.None : ColumnHeaderStyle.Nonclickable;
            }
            finally { toolbar.ResumeLayout(true); }
        }

        /// <summary>展开固定菜单，不生成临时控件或修改日志缓存。</summary>
        private void OpenCompactMenu(object sender, EventArgs e)
        {
            DismissToolTips(sender, e);
            compactMenu.Show(overflowButton, new Point(0, overflowButton.Height));
        }

        /// <summary>菜单等级与普通下拉框共用同一个过滤入口。</summary>
        private void MenuLevelClicked(object sender, EventArgs e)
        {
            levelFilter.SelectedIndex = (int)((ToolStripMenuItem)sender).Tag;
        }

        /// <summary>工具状态变化时才修改图标、强调色与中文提示。</summary>
        private void UpdateToolbar()
        {
            var icon = paused ? resumeIcon : pauseIcon;
            if (!ReferenceEquals(pauseButton.Image, icon)) pauseButton.Image = icon;
            string action = paused ? "恢复刷新" : "暂停刷新";
            if (pauseButton.AccessibleName != action) pauseButton.AccessibleName = action;
            SetToolTip(pauseButton, action);
            SetToolTip(scrollButton, autoScroll ? "关闭自动滚动" : "开启自动滚动");
            scrollButton.AccessibleDescription = autoScroll ? "已开启" : "已关闭";
            Color selected = Color.FromArgb(91, 64, 40);
            Color pauseColor = paused ? selected : toolbar.BackColor;
            Color scrollColor = autoScroll ? selected : toolbar.BackColor;
            if (pauseButton.BackColor != pauseColor) pauseButton.BackColor = pauseColor;
            if (scrollButton.BackColor != scrollColor) scrollButton.BackColor = scrollColor;
            if (pauseMenuItem.Text != action) pauseMenuItem.Text = action;
            if (pauseMenuItem.Checked != paused) pauseMenuItem.Checked = paused;
            if (scrollMenuItem.Checked != autoScroll) scrollMenuItem.Checked = autoScroll;
            foreach (ToolStripMenuItem item in levelMenuItem.DropDownItems)
            {
                bool selectedLevel = (int)item.Tag == (int)minimumLevel;
                if (item.Checked != selectedLevel) item.Checked = selectedLevel;
            }
        }

        /// <summary>计数文案与颜色幂等更新，错误总数包含当前缓存的异常和致命记录。</summary>
        private void UpdateCount()
        {
            string text = visibleEntries.Count + " 条  异常 " + errorCount;
            if (countLabel.Text != text) countLabel.Text = text;
            Color color = errorCount > 0 ? Color.FromArgb(255, 105, 105) : Color.FromArgb(194, 198, 203);
            if (countLabel.ForeColor != color) countLabel.ForeColor = color;
            SetToolTip(countLabel, string.Empty);
        }

        /// <summary>读取失败保留已显示记录，并呈现可检查的错误信息。</summary>
        private void ShowReadError(Exception error)
        {
            if (countLabel.Text != "日志读取失败") countLabel.Text = "日志读取失败";
            Color color = Color.FromArgb(255, 105, 105);
            if (countLabel.ForeColor != color) countLabel.ForeColor = color;
            SetToolTip(countLabel, error.Message);
        }

        /// <summary>相同提示不重复进入原生ToolTip更新路径。</summary>
        private void SetToolTip(Control control, string text)
        {
            if (commandToolTip.GetToolTip(control) == text) return;
            commandToolTip.Hide(control);
            commandToolTip.SetToolTip(control, text);
        }

        /// <summary>仅有效重新进入控件时启用提示，状态刷新不重新激活。</summary>
        private void EnableToolTips(object sender, EventArgs e)
        {
            if (!IsDisposed && !Disposing && MouseButtons == MouseButtons.None) commandToolTip.Active = true;
        }

        /// <summary>离开、点击及失焦立即停用所有工具提示。</summary>
        private void DismissToolTips(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !commandToolTip.Active) return;
            commandToolTip.Active = false;
            commandToolTip.Hide(sender as Control ?? toolbar);
        }

        /// <summary>使用统一Segoe MDL2图标并登记释放责任。</summary>
        private Bitmap CreateIcon(int glyph)
        {
            var bitmap = RuntimeDesignerIcons.Create(glyph, Color.WhiteSmoke, 16);
            ownedIcons.Add(bitmap);
            return bitmap;
        }
    }
}
