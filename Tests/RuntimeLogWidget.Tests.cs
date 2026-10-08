using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>纯内存日志控件回归，不创建生产对象、不移动鼠标。</summary>
internal static class RuntimeLogWidgetTests
{
    /// <summary>断言计数。</summary>
    private static int checks;
    /// <summary>实际产品控件类型，缺少实现时明确失败。</summary>
    private static Type widgetType;

    /// <summary>创建屏幕外真实句柄，执行控件行为测试。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            widgetType = typeof(RuntimeLogWidgetTests).Assembly.GetType("TDJS_Vision.Forms.RuntimeDesigner.RuntimeLogWidget");
            Check(widgetType != null, "尚未实现 RuntimeLogWidget");
            Application.EnableVisualStyles();
            using (var host = new OffscreenForm())
            using (var widget = (Control)Activator.CreateInstance(widgetType))
            {
                widget.Size = new Size(320, 160);
                host.Controls.Add(widget);
                host.Show();
                Application.DoEvents();
                Verify(widget);
                host.Close();
            }
            VerifyDefaultCapacity();
            Console.WriteLine("通过：日志控件专项测试，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>验证容量、工具栏及订阅生命周期。</summary>
    private static void Verify(Control widget)
    {
        var first = new Subscription();
        Bind(widget, first, 100, RuntimeLogLevel.Debug, false);
        var list = Field<ListView>(widget, "logList");
        var filter = Field<ComboBox>(widget, "levelFilter");
        var pause = Field<Button>(widget, "pauseButton");
        var clear = Field<Button>(widget, "clearButton");
        var scroll = Field<Button>(widget, "scrollButton");
        var count = Field<Label>(widget, "countLabel");
        Check(list.VirtualMode && list.View == View.Details, "日志必须使用虚拟明细列表");
        Check(string.Join(",", list.Columns.Cast<ColumnHeader>().Select(c => c.Text)) == "时间,等级,来源,内容", "固定四列为中文");
        Check(filter.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(new[] { "调试", "信息", "警告", "异常", "致命" }), "等级别名必须完整使用中文");
        var toolbar = Field<TableLayoutPanel>(widget, "toolbar");
        var controls = toolbar.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
        Check(controls.All(c => c.Right <= toolbar.ClientSize.Width && c.Bottom <= toolbar.ClientSize.Height), "320像素工具栏不能越界");
        Check(controls.SelectMany((c, i) => controls.Skip(i + 1).Select(other => c.Bounds.IntersectsWith(other.Bounds))).All(overlap => !overlap), "工具栏不能重叠");
        Check(list.Columns.Cast<ColumnHeader>().Sum(c => c.Width) > list.ClientSize.Width, "长来源与内容应使用横向滚动而不是换行撑高");
        widget.Size = new Size(48, 32);
        Check(toolbar.Controls.Cast<Control>().Where(c => c.Visible).All(c => c.Left >= 0 && c.Right <= toolbar.ClientSize.Width && c.Bottom <= toolbar.ClientSize.Height), "48x32时工具栏必须收起越界控件");
        Check(Field<Button>(widget, "overflowButton").Visible, "窄尺寸必须保留溢出菜单入口");
        Field<ToolStripMenuItem>(widget, "exceptionMenuItem").PerformClick();
        Check(filter.SelectedIndex == 3, "窄尺寸菜单也能选择最低等级");
        widget.Size = new Size(600, 260);
        Check(!Field<Button>(widget, "overflowButton").Visible && clear.Visible && filter.Visible, "恢复常规尺寸后恢复工具栏");
        widget.Size = new Size(320, 160);
        filter.SelectedIndex = 0;
        for (int i = 0; i < 250; i++) first.Add(i, (RuntimeLogLevel)(i % 5));
        Refresh(widget);
        Check(first.LastLimit == 200 && first.Drains == 1 && first.Count == 50, "每批最多请求200条");
        Check(list.VirtualListSize == 100 && Row(list, 0).SubItems[3].Text == "记录100", "容量淘汰最旧记录，保留时间顺序");
        Check(Enumerable.Range(0, 5).Select(i => Row(list, i).SubItems[1].Text).SequenceEqual(new[] { "调试", "信息", "警告", "异常", "致命" }), "虚拟行的五种等级必须显示中文别名");
        Check(Row(list, 0).Text == "00:00:00.100" && Row(list, 0).SubItems[2].Text == "流程/组合/节点", "显示毫秒时间和完整来源路径");
        Refresh(widget);
        Check(first.Drains == 1, "50毫秒宿主轮询不能每次都提取日志");
        NextRefresh(widget);
        Check(list.VirtualListSize == 100 && Row(list, 0).SubItems[3].Text == "记录150", "后续批次保留最新有界记录");
        Check(count.Text.Contains("40") && count.ForeColor.R > count.ForeColor.G, "异常及致命计数为40且突出红色");
        filter.SelectedIndex = 3;
        Check(list.VirtualListSize == 40 && Row(list, 0).SubItems[1].Text == "异常", "最低等级过滤包含异常和致命");
        Check(Row(list, 1).ForeColor.R > Row(list, 1).ForeColor.G, "致命记录显示红色");
        filter.SelectedIndex = 0;
        Check(list.VirtualListSize == 100, "恢复等级过滤不能丢失缓存记录");
        pause.PerformClick();
        int drains = first.Drains;
        first.Add(250, RuntimeLogLevel.Info);
        NextRefresh(widget);
        Check(first.Drains == drains && first.Count == 1, "暂停只停止控件提取，保留订阅队列");
        clear.PerformClick();
        Check(first.Clears == 1 && first.Count == 0 && list.VirtualListSize == 0, "暂停期间清空同时清理本地和订阅队列");
        pause.PerformClick();
        for (int i = 0; i < 80; i++) first.Add(i, RuntimeLogLevel.Info);
        NextRefresh(widget);
        list.TopItem = list.Items[10];
        int top = list.TopItem.Index;
        first.Add(80, RuntimeLogLevel.Warn);
        NextRefresh(widget);
        Check(list.TopItem.Index == top, "自动滚动关闭时保留阅读位置");
        scroll.PerformClick();
        first.Add(81, RuntimeLogLevel.Info);
        NextRefresh(widget);
        Check(list.TopItem.Index > top, "启用自动滚动后新批次跟随最新行");
        VerifyIdle(widget, list);
        Call(widget, "ApplyAppearance", Color.FromArgb(32, 34, 36), Color.WhiteSmoke);
        Check(list.BackColor == Color.FromArgb(32, 34, 36) && list.ForeColor == Color.WhiteSmoke, "外观应用到列表");
        first.ThrowOnDrain = true;
        NextRefresh(widget);
        Check(count.Text.Contains("失败") && list.VirtualListSize == 82, "提取异常可见且保留旧记录");
        first.ThrowOnDrain = false;
        var second = new Subscription();
        Bind(widget, second, 2000, RuntimeLogLevel.Info, true);
        Check(first.Disposals == 1 && list.VirtualListSize == 0, "换绑释放旧订阅并清理旧来源记录");
        Bind(widget, second, 2000, RuntimeLogLevel.Info, true);
        Check(second.Disposals == 0, "相同订阅重复绑定不能提前释放");
        widget.Dispose();
        widget.Dispose();
        Refresh(widget);
        Check(second.Disposals == 1, "控件关闭恰好释放一次订阅且后续刷新无效");
    }

    /// <summary>空批次不重绘列表，不重设提示；离开及点击及时关闭提示。</summary>
    private static void VerifyIdle(Control widget, ListView list)
    {
        int invalidations = 0;
        InvalidateEventHandler handler = (s, e) => invalidations++;
        list.Invalidated += handler;
        for (int i = 0; i < 3; i++) NextRefresh(widget);
        list.Invalidated -= handler;
        Check(invalidations == 0, "空批次不得重复重绘列表");
        var tips = Field<ToolTip>(widget, "commandToolTip");
        var pause = Field<Button>(widget, "pauseButton");
        var tools = (IDictionary)typeof(ToolTip).GetField("tools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tips);
        var original = tools[pause];
        for (int i = 0; i < 3; i++) NextRefresh(widget);
        Check(ReferenceEquals(original, tools[pause]), "稳态提取不能重复替换原生提示对象");
        Raise(pause, "OnMouseEnter");
        Check(tips.Active, "进入工具按钮允许悬停提示");
        Raise(pause, "OnMouseLeave");
        Check(!tips.Active, "离开工具按钮立即隐藏提示");
        NextRefresh(widget);
        Check(!tips.Active, "刷新不能重新激活离开的提示");
        Raise(pause, "OnMouseEnter");
        pause.PerformClick();
        Check(!tips.Active, "点击工具按钮隐藏提示");
        pause.PerformClick();
    }

    /// <summary>默认2000条容量及多个控件的清空、释放相互独立。</summary>
    private static void VerifyDefaultCapacity()
    {
        var source = new Subscription();
        var otherSource = new Subscription();
        using (var widget = (Control)Activator.CreateInstance(widgetType))
        using (var other = (Control)Activator.CreateInstance(widgetType))
        {
            Bind(widget, source, 0, RuntimeLogLevel.Debug, false);
            Bind(other, otherSource, 100, RuntimeLogLevel.Debug, false);
            for (int i = 0; i < 2200; i++) source.Add(i, RuntimeLogLevel.Info);
            for (int i = 0; i < 11; i++) NextRefresh(widget);
            var list = Field<ListView>(widget, "logList");
            Check(list.VirtualListSize == 2000 && Row(list, 0).SubItems[3].Text == "记录200", "默认容量应为2000且最旧记录正确淘汰");
            otherSource.Add(7, RuntimeLogLevel.Exception);
            Refresh(other);
            // 未显示控件的PerformClick不派发；直接调用真实Click事件处理链。
            typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Field<Button>(widget, "clearButton"), new object[] { EventArgs.Empty });
            Check(source.Clears == 1 && otherSource.Clears == 0 && Field<ListView>(other, "logList").VirtualListSize == 1, "清空一个控件不影响另一个订阅及显示");
            widget.Dispose();
            Check(otherSource.Disposals == 0, "关闭一个控件不释放另一个订阅");
        }
        Check(source.Disposals == 1 && otherSource.Disposals == 1, "各控件关闭后分别释放自己的订阅");
    }

    /// <summary>读取虚拟行，走实际RetrieveVirtualItem处理程序。</summary>
    private static ListViewItem Row(ListView list, int index) => list.Items[index];
    /// <summary>通过公共接口绑定，避免测试源码缺少控件时编译失败掩盖RED。</summary>
    private static void Bind(Control widget, IRuntimeLogSubscription source, int capacity, RuntimeLogLevel level, bool scroll) => Call(widget, "Bind", source, capacity, level, scroll);
    /// <summary>调用一次宿主刷新。</summary>
    private static void Refresh(Control widget) => Call(widget, "RefreshLogs");
    /// <summary>经过100毫秒节流周期再刷新，不控制桌面。</summary>
    private static void NextRefresh(Control widget) { Thread.Sleep(115); Refresh(widget); }
    /// <summary>调用公共产品方法。</summary>
    private static void Call(Control widget, string method, params object[] args) => widgetType.GetMethod(method).Invoke(widget, args);
    /// <summary>读取设计器固定控件。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>直接派发控件事件，不移动光标。</summary>
    private static void Raise(Control control, string name) => typeof(Control).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { EventArgs.Empty });
    /// <summary>记录行为断言。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }

    /// <summary>无设备的可控订阅队列。</summary>
    private sealed class Subscription : IRuntimeLogSubscription
    {
        /// <summary>待提取记录。</summary>
        private readonly Queue<RuntimeLogEntry> queue = new Queue<RuntimeLogEntry>();
        /// <summary>队列长度。</summary>
        public int Count => queue.Count;
        /// <summary>提取次数。</summary>
        public int Drains;
        /// <summary>最后批次上限。</summary>
        public int LastLimit;
        /// <summary>清空次数。</summary>
        public int Clears;
        /// <summary>释放次数。</summary>
        public int Disposals;
        /// <summary>模拟提取失败。</summary>
        public bool ThrowOnDrain;
        /// <summary>加入固定时间序列的模拟记录。</summary>
        public void Add(int index, RuntimeLogLevel level) => queue.Enqueue(new RuntimeLogEntry(new DateTime(2026, 9, 26).AddMilliseconds(index), level, "流程/组合/节点", "记录" + index));
        /// <summary>只返回约定的有限批次。</summary>
        public IReadOnlyList<RuntimeLogEntry> Drain(int maxCount)
        {
            Drains++; LastLimit = maxCount;
            if (ThrowOnDrain) throw new InvalidOperationException("模拟读取错误");
            var result = new List<RuntimeLogEntry>();
            while (result.Count < maxCount && queue.Count > 0) result.Add(queue.Dequeue());
            return result;
        }
        /// <summary>仅清空自己的订阅。</summary>
        public void Clear() { Clears++; queue.Clear(); }
        /// <summary>记录所有权释放。</summary>
        public void Dispose() { Disposals++; queue.Clear(); }
    }

    /// <summary>只在屏幕外显示且不激活的测试宿主。</summary>
    private sealed class OffscreenForm : Form
    {
        /// <summary>测试窗口不进入任务栏。</summary>
        public OffscreenForm() { StartPosition = FormStartPosition.Manual; Location = new Point(-30000, -30000); ShowInTaskbar = false; ClientSize = new Size(400, 240); }
        /// <summary>禁止显示时抢占焦点。</summary>
        protected override bool ShowWithoutActivation => true;
    }
}
