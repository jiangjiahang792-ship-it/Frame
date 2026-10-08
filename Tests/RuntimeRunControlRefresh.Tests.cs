using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>运行按钮稳态刷新与提示生命周期回归，只显示屏幕外无激活测试窗口。</summary>
internal static class RuntimeRunControlRefreshTests
{
    /// <summary>已经执行的行为断言数量。</summary>
    private static int checks;
    /// <summary>在独立界面线程验证原生消息和实际控件事件。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Console.WriteLine("被测程序集：" + typeof(RuntimeRunControlWidget).Assembly.Location);
            Application.EnableVisualStyles();
            using (var host = new OffscreenForm())
            using (var widget = new RuntimeRunControlWidget())
            {
                host.Controls.Add(widget);
                host.Show();
                Application.DoEvents();
                var controller = new TestController();
                widget.Bind(controller, 42);
                var once = Field<Button>(widget, "onceButton");
                var loop = Field<Button>(widget, "loopButton");
                var tooltip = Field<ToolTip>(widget, "commandToolTip");
                VerifySteady(widget, once, loop, tooltip, "空闲");
                controller.IsRunning = true;
                widget.RefreshState();
                Check(!once.Enabled && loop.Enabled && loop.AccessibleName == "停止运行", "外部运行应立即同步为停止动作");
                Check(ReferenceEquals(loop.Image, Field<Bitmap>(widget, "stopIcon")), "外部运行必须显示停止图标而非循环箭头");
                VerifySteady(widget, once, loop, tooltip, "运行");
                controller.IsStopping = true;
                widget.RefreshState();
                Check(!loop.Enabled && loop.AccessibleName == "正在停止", "外部停止中应锁定按钮");
                VerifySteady(widget, once, loop, tooltip, "停止中");
                controller.IsRunning = controller.IsStopping = false;
                controller.TargetAvailable = false;
                widget.RefreshState();
                Check(!once.Enabled && !loop.Enabled && tooltip.GetToolTip(loop) == "指定流程已失效", "目录失效不能回退全流程");
                VerifySteady(widget, once, loop, tooltip, "失效目标");
                controller.TargetAvailable = true;
                widget.RefreshState();
                Check(once.Enabled && loop.Enabled, "目录恢复后按钮应恢复");
                Check(ReferenceEquals(loop.Image, Field<Bitmap>(widget, "loopIcon")), "恢复空闲必须还原循环箭头");
                VerifyTips(widget, once, loop, tooltip, controller);
                VerifyPressedStop(widget, loop, controller);
                var previous = once.Image;
                widget.Size = new Size(48, 32);
                Check(once.Image != null && !ReferenceEquals(previous, once.Image), "尺寸变化必须重建图标而不能被状态缓存跳过");
                previous = once.Image;
                widget.ApplyAppearance(Color.Black, Color.Cyan);
                Check(!ReferenceEquals(previous, once.Image), "前景色变化也必须更新图标");
                VerifySteady(widget, once, loop, tooltip, "调整外观后");
                host.Close();
            }
            Console.WriteLine("通过：运行按钮刷新专项测试，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>重复轮询不能替换提示对象、发送提示更新消息或触发按钮重绘。</summary>
    private static void VerifySteady(RuntimeRunControlWidget widget, Button once, Button loop, ToolTip tooltip, string state)
    {
        var tools = (IDictionary)typeof(ToolTip).GetField("tools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tooltip);
        object onceTip = tools[once], loopTip = tools[loop];
        int invalidations = 0;
        InvalidateEventHandler invalidated = (sender, args) => invalidations++;
        once.Invalidated += invalidated; loop.Invalidated += invalidated;
        var handle = (IntPtr)typeof(ToolTip).GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tooltip, null);
        using (var messages = new TooltipMessages(handle))
        {
            for (int index = 0; index < 1000; index++) widget.RefreshState();
            Console.WriteLine(state + "：1000 次刷新，提示更新消息=" + messages.Updates + "，按钮重绘=" + invalidations);
            Check(ReferenceEquals(onceTip, tools[once]) && ReferenceEquals(loopTip, tools[loop]), state + "稳态刷新不应反复调用 SetToolTip");
            Check(messages.Updates == 0 && invalidations == 0, state + "稳态刷新应为零提示更新、零重绘");
        }
        once.Invalidated -= invalidated; loop.Invalidated -= invalidated;
    }

    /// <summary>提示仅随有效悬停启用，离开、点击及失焦后不能被刷新重新激活。</summary>
    private static void VerifyTips(RuntimeRunControlWidget widget, Button once, Button loop, ToolTip tooltip, TestController controller)
    {
        Raise(loop, "OnMouseEnter", EventArgs.Empty);
        Check(tooltip.Active, "进入按钮后允许正常悬停提示");
        Raise(loop, "OnMouseLeave", EventArgs.Empty);
        Check(!tooltip.Active, "离开按钮必须立即停用提示");
        for (int index = 0; index < 100; index++) widget.RefreshState();
        Check(!tooltip.Active, "轮询不能重新激活已离开的提示");
        Raise(loop, "OnMouseEnter", EventArgs.Empty);
        loop.PerformClick();
        Check(controller.Starts == 1 && controller.IsRunning && !tooltip.Active, "点击循环后执行一次且立即隐藏提示");
        loop.PerformClick();
        Check(controller.Stops == 1 && !loop.Enabled, "停止请求等待期间仍正确锁定按钮");
        controller.Finish();
        Pump(() => once.Enabled);
        Raise(once, "OnMouseEnter", EventArgs.Empty);
        Check(tooltip.Active, "重新进入另一按钮应恢复提示");
        Raise(once, "OnLostFocus", EventArgs.Empty);
        Check(!tooltip.Active, "失焦应隐藏残留提示");
        Raise(once, "OnMouseEnter", EventArgs.Empty);
        once.PerformClick();
        Check(controller.Starts == 2 && !tooltip.Active, "单次点击也必须隐藏提示");
        controller.Finish();
        Pump(() => once.Enabled);
    }

    /// <summary>离开按钮只隐藏提示，不得清除按下时锁定的停止动作。</summary>
    private static void VerifyPressedStop(RuntimeRunControlWidget widget, Button loop, TestController controller)
    {
        controller.IsRunning = true;
        widget.RefreshState();
        int starts = controller.Starts;
        Raise(loop, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
        Raise(loop, "OnMouseLeave", EventArgs.Empty);
        controller.IsRunning = false;
        widget.RefreshState();
        loop.PerformClick();
        Check(controller.Starts == starts, "按下停止后离开及刷新不能反向启动");
        controller.IsRunning = true;
        widget.RefreshState();
        Raise(loop, "OnKeyDown", new KeyEventArgs(Keys.Space));
        controller.IsRunning = false;
        widget.RefreshState();
        loop.PerformClick();
        Check(controller.Starts == starts, "键盘按下停止后刷新也不能反向启动");
    }

    /// <summary>只观察本测试提示窗口的原生消息，不控制鼠标或桌面。</summary>
    private sealed class TooltipMessages : NativeWindow, IDisposable
    {
        /// <summary>原生提示内容更新次数。</summary>
        public int Updates { get; private set; }
        /// <summary>附加到测试组件自己的提示句柄。</summary>
        public TooltipMessages(IntPtr handle) { AssignHandle(handle); }
        /// <summary>统计 ANSI 与 Unicode 的提示内容更新消息。</summary>
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x409 || message.Msg == 0x436 || message.Msg == 0x40c || message.Msg == 0x439) Updates++;
            base.WndProc(ref message);
        }
        /// <summary>移除观察钩子，不销毁组件拥有的句柄。</summary>
        public void Dispose() { ReleaseHandle(); }
    }

    /// <summary>不抢焦点、不出现在任务栏的屏幕外测试宿主。</summary>
    private sealed class OffscreenForm : Form
    {
        /// <summary>将真实窗口限制在屏幕外，不影响当前桌面操作。</summary>
        public OffscreenForm() { StartPosition = FormStartPosition.Manual; Location = new Point(-30000, -30000); ShowInTaskbar = false; }
        /// <summary>显示仅用于创建真实子控件，不激活窗口。</summary>
        protected override bool ShowWithoutActivation => true;
    }

    /// <summary>可控完成点模拟运行，不连接方案或设备。</summary>
    private sealed class TestController : IRuntimeRunController
    {
        /// <summary>外部运行状态。</summary>
        public bool IsRunning { get; set; }
        /// <summary>外部停止状态。</summary>
        public bool IsStopping { get; set; }
        /// <summary>模拟流程目录是否仍有目标。</summary>
        public bool TargetAvailable = true;
        /// <summary>启动调用数量。</summary>
        public int Starts;
        /// <summary>停止调用数量。</summary>
        public int Stops;
        /// <summary>当前异步运行完成点。</summary>
        private TaskCompletionSource<bool> completion;
        /// <summary>返回轻量模拟目录。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => TargetAvailable ? new[] { new RuntimeProcessChoice { Id = 42, Name = "模拟流程" } } : new RuntimeProcessChoice[0];
        /// <summary>占位运行并保持任务等待。</summary>
        public Task RunAsync(int? processId, bool continuous)
        { Starts++; IsRunning = true; completion = new TaskCompletionSource<bool>(); return completion.Task; }
        /// <summary>模拟等待中的停止请求。</summary>
        public Task StopAsync() { Stops++; IsStopping = true; return completion.Task; }
        /// <summary>让运行及停止任务同时退出。</summary>
        public void Finish() { IsRunning = IsStopping = false; completion.SetResult(true); }
    }

    /// <summary>读取设计器定义的控件。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>直接分发控件事件，不移动实际鼠标。</summary>
    private static void Raise(Control control, string method, EventArgs args) => typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { args });
    /// <summary>有限等待界面异步续体完成。</summary>
    private static void Pump(Func<bool> ready)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!ready() && clock.ElapsedMilliseconds < 3000) { Application.DoEvents(); System.Threading.Thread.Sleep(1); }
        Check(ready(), "运行状态应在超时前恢复");
    }
    /// <summary>记录行为断言，失败时返回具体上下文。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
}
