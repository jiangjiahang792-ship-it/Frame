using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>验证正式窗口位置接入，预览不操作本机配置。</summary>
internal static class RuntimeWindowPlacementLifecycleTests
{
    /// <summary>已验证的行为数量。</summary>
    private static int checks;
    /// <summary>独立入口，不显示生产主程序。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            if (typeof(RuntimeInterfacePreviewForm).GetMethod("AttachLocalPlacement") == null)
                throw new InvalidOperationException("正式运行窗口缺少本机位置配置入口。");
            VerifyNormalAndMinimized();
            VerifyMaximizedMonitorMove();
            VerifyFailureAndPreviewIsolation();
            VerifyNativeCloseLifecycle();
            Console.WriteLine("通过：运行窗口位置生命周期，" + checks + " 项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>真实屏幕外Show和Close验证原生事件顺序，而非仅手工触发生命周期。</summary>
    private static void VerifyNativeCloseLifecycle()
    {
        var display = new RuntimeDisplayInfo { DeviceName = "屏幕外测试屏", WorkingArea = new Rectangle(-30000, -30000, 1600, 900) };
        foreach (bool maximize in new[] { false, true })
        {
            var store = new MemoryStore { Placement = new RuntimeWindowPlacement { DeviceName = display.DeviceName,
                WorkingArea = display.WorkingArea, NormalBounds = new Rectangle(-29800, -29850, 900, 600), Maximized = false } };
            using (var window = new OffscreenForm(display.WorkingArea) { StartPosition = FormStartPosition.Manual,
                Location = new Point(-30000, -30000), ShowInTaskbar = false })
            using (var binding = new RuntimeWindowPlacementBinding(window, store, @"C:\实际关闭测试.Sol", "页面", null,
                () => new[] { display }, form => display.DeviceName))
            {
                window.Show();
                Application.DoEvents();
                var normal = new Rectangle(-29750, -29820, 950, 620);
                window.Bounds = normal;
                if (maximize) window.WindowState = FormWindowState.Maximized;
                Application.DoEvents();
                Check(window.Left < -10000 && window.Top < -10000, "实际窗口始终位于屏幕外");
                FormClosingEventHandler cancel = (sender, args) => args.Cancel = true;
                window.FormClosing += cancel;
                window.Close();
                Check(!window.IsDisposed && store.Saves == 0, "原生取消关闭不写入位置");
                window.FormClosing -= cancel;
                window.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                window.Close();
                Application.DoEvents();
                Check(window.IsDisposed && store.Saves == 1, "原生关闭在Dispose前保存且只写一次");
                Check(store.Placement.Maximized == maximize, "原生最小化关闭保留前一个窗口状态");
                Check(store.Placement.NormalBounds == normal, "原生最大化与最小化不损坏普通还原边界");
            }
        }
    }

    /// <summary>无激活、无任务栏的原生窗口，不影响用户正在操作的应用。</summary>
    private sealed class OffscreenForm : Form
    {
        /// <summary>原生最大化也限定在屏幕外测试区域。</summary>
        public OffscreenForm(Rectangle area) { MaximizedBounds = area; }
        /// <summary>禁止测试窗口抢占前台。</summary>
        protected override bool ShowWithoutActivation => true;
    }

    /// <summary>普通状态、取消关闭及最小化关闭都保留正确位置且移动期间不落盘。</summary>
    private static void VerifyNormalAndMinimized()
    {
        var displays = Displays();
        var saved = new RuntimeWindowPlacement { DeviceName = "副屏", WorkingArea = displays[1].WorkingArea,
            NormalBounds = new Rectangle(-1500, 80, 900, 650), Maximized = false };
        var store = new MemoryStore { Placement = saved };
        string screen = "主屏";
        using (var window = new Form { MinimumSize = new Size(480, 320) })
        using (var binding = new RuntimeWindowPlacementBinding(window, store, @"C:\方案A.Sol", "界面一", null,
            () => displays, form => screen))
        {
            Raise(window, "OnLoad", EventArgs.Empty);
            Check(window.Bounds == saved.NormalBounds && window.StartPosition == FormStartPosition.Manual, "恢复负坐标副屏的原窗口边界");
            Check(window.WindowState == FormWindowState.Normal, "保留普通窗口状态");
            screen = "副屏";
            Raise(window, "OnShown", EventArgs.Empty);
            var moved = new Rectangle(-1420, 130, 1000, 700);
            window.Bounds = moved;
            for (int i = 0; i < 20; i++) Raise(window, "OnLocationChanged", EventArgs.Empty);
            Check(store.Saves == 0 && store.Loads == 1, "移动刷新只更新内存，不重复读写本机配置");
            Raise(window, "OnFormClosing", new FormClosingEventArgs(CloseReason.UserClosing, true));
            Check(store.Saves == 0, "取消关闭不保存");
            window.WindowState = FormWindowState.Minimized;
            Raise(window, "OnFormClosing", new FormClosingEventArgs(CloseReason.UserClosing, false));
            Raise(window, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
            Check(store.Saves == 1 && store.Placement.NormalBounds == moved && !store.Placement.Maximized, "最小化关闭保留上一次普通状态及位置");
            Check(store.SolutionPath == @"C:\方案A.Sol" && store.PageId == "界面一", "使用打开时捕获的方案和页面键");
            Raise(window, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
            Check(store.Saves == 1, "关闭事件重复不重复保存");
        }
    }

    /// <summary>最大化跨屏记住新屏幕，最小化关闭不覆盖为最小化状态。</summary>
    private static void VerifyMaximizedMonitorMove()
    {
        var displays = Displays();
        var store = new MemoryStore { Placement = new RuntimeWindowPlacement { DeviceName = "副屏", WorkingArea = displays[1].WorkingArea,
            NormalBounds = new Rectangle(-1440, 90, 960, 630), Maximized = true } };
        string screen = "副屏";
        using (var window = new Form { MinimumSize = new Size(480, 320) })
        using (var binding = new RuntimeWindowPlacementBinding(window, store, @"C:\方案A.Sol", "界面二", null,
            () => displays, form => screen))
        {
            Raise(window, "OnLoad", EventArgs.Empty);
            Check(window.WindowState == FormWindowState.Maximized, "恢复保存的最大化状态");
            Raise(window, "OnShown", EventArgs.Empty);
            screen = "主屏";
            Raise(window, "OnLocationChanged", EventArgs.Empty);
            Check(store.Saves == 0, "最大化跨屏期间不落盘");
            window.WindowState = FormWindowState.Minimized;
            Raise(window, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
            Check(store.Placement.DeviceName == "主屏" && store.Placement.Maximized, "最小化关闭记住最新最大化屏幕");
            Check(displays[0].WorkingArea.Contains(store.Placement.NormalBounds), "跨屏后还原边界位于新屏工作区内");
        }
    }

    /// <summary>文件失败只报告告警，预览与未显示即释放不保存默认位置。</summary>
    private static void VerifyFailureAndPreviewIsolation()
    {
        var displays = Displays(); int warnings = 0;
        var store = new MemoryStore { FailLoad = true, FailSave = true };
        using (var window = new Form())
        using (var binding = new RuntimeWindowPlacementBinding(window, store, @"C:\方案B.Sol", "界面一", text => warnings++,
            () => displays, form => "主屏"))
        {
            Raise(window, "OnLoad", EventArgs.Empty);
            Check(warnings == 1 && window.WindowState == FormWindowState.Maximized, "读取失败安全回退首次最大化");
            Raise(window, "OnShown", EventArgs.Empty);
            Raise(window, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
            Check(warnings == 2, "保存失败报告而不阻止关闭");
        }
        var notShown = new MemoryStore();
        using (var window = new Form())
        using (var binding = new RuntimeWindowPlacementBinding(window, notShown, @"C:\方案B.Sol", "界面一", null,
            () => displays, form => "主屏"))
        {
            Raise(window, "OnLoad", EventArgs.Empty);
            Raise(window, "OnFormClosed", new FormClosedEventArgs(CloseReason.UserClosing));
            Check(notShown.Saves == 0, "未显示窗口不覆盖已保存位置");
        }
        using (var preview = new RuntimeInterfacePreviewForm())
        {
            var field = typeof(RuntimeInterfacePreviewForm).GetField("placementBinding", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(field.GetValue(preview) == null, "设计预览未连接本机位置存储");
            preview.AttachLocalPlacement(null, "界面一", notShown);
            Check(field.GetValue(preview) == null && notShown.Saves == 0, "未保存方案不写本机位置配置");
        }
    }

    /// <summary>不同大小、含负坐标的双屏，不依赖测试机器实际显示器。</summary>
    private static RuntimeDisplayInfo[] Displays() => new[]
    {
        new RuntimeDisplayInfo { DeviceName = "主屏", WorkingArea = new Rectangle(0, 0, 1920, 1040) },
        new RuntimeDisplayInfo { DeviceName = "副屏", WorkingArea = new Rectangle(-1600, 0, 1600, 900) }
    };

    /// <summary>内存替身记录读写次数，不访问真实文件。</summary>
    private sealed class MemoryStore : IRuntimeWindowPlacementStore
    {
        /// <summary>当前保存的窗口状态。</summary>
        public RuntimeWindowPlacement Placement;
        /// <summary>读取次数。</summary>
        public int Loads;
        /// <summary>保存次数。</summary>
        public int Saves;
        /// <summary>模拟读取失败。</summary>
        public bool FailLoad;
        /// <summary>模拟保存失败。</summary>
        public bool FailSave;
        /// <summary>最近写入使用的方案键。</summary>
        public string SolutionPath;
        /// <summary>最近写入使用的页面键。</summary>
        public string PageId;
        /// <summary>返回独立的内存快照。</summary>
        public RuntimeWindowPlacement Load(string solutionPath, string pageId)
        { Loads++; if (FailLoad) throw new InvalidOperationException("测试读取失败"); return Placement?.Copy(); }
        /// <summary>记录保存参数。</summary>
        public void Save(string solutionPath, string pageId, RuntimeWindowPlacement placement)
        { Saves++; if (FailSave) throw new InvalidOperationException("测试保存失败"); Placement = placement.Copy(); SolutionPath = solutionPath; PageId = pageId; }
    }

    /// <summary>直接触发窗体生命周期事件，不显示窗口或操作桌面。</summary>
    private static void Raise(Form form, string name, EventArgs args) => typeof(Form)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { args });
    /// <summary>验证精确预期。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
}
