using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.ImageViewer;
using WeifenLuo.WinFormsUI.Docking;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

/// <summary>使用真实图像窗体和嵌套 DockPanel 验证双屏浮动、停靠与布局重载。</summary>
internal static class ImageDockPanelFloatingLayoutTests
{
    /// <summary>测试输出目录。</summary>
    private static readonly string Output = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>查询窗口在桌面上的实际矩形。</summary>
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, int[] value, int size);

    /// <summary>在独立进程内运行，不加载方案或连接设备。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--capture-secondary") >= 0)
        {
            Rectangle screen = (Array.Find(Screen.AllScreens, item => !item.Primary) ?? Screen.PrimaryScreen).Bounds;
            using (var bitmap = new Bitmap(screen.Width, screen.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
                bitmap.Save(Path.Combine(Output, "secondary-desktop.png"));
            }
            return 0;
        }
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Error));
        try { return Run(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>从指定构建目录加载实际软件依赖。</summary>
    private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
    {
        string directory = Environment.GetEnvironmentVariable("TDJS_FLOAT_TEST_BUILD");
        string name = new AssemblyName(args.Name).Name;
        foreach (string extension in new[] { ".dll", ".exe" })
        {
            string path = Path.Combine(directory, name + extension);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }

    /// <summary>保持宿主层级与主程序一致，输出真实可见状态。</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 浮动、合并和拖出属于高级权限功能，布局回归应在明确授权的测试角色下执行。
        TDJS_Vision.Forms.Login.UserPermissionContext.UpdateRole(TDJS_Vision.Forms.Login.UserRole.Manufacturer);
        Console.WriteLine("Assembly: " + typeof(FrmImageViewer).Assembly.Location);
        if (Array.IndexOf(args, "--drag-size") >= 0) return DiagnoseDragSize(args);
        if (Array.IndexOf(args, "--merge-regression") >= 0) return CheckFloatMerge();
        bool unopened = Array.IndexOf(args, "--unopened") >= 0;
        string layoutPath = Path.Combine(Output, "DockPanelImageWindows.config");
        byte[] originalLayout = unopened ? File.ReadAllBytes(layoutPath) : null;
        using (var host = new FormBase { Text = "图像浮窗双屏验证", Bounds = new Rectangle(80, 80, 1050, 700), StartPosition = FormStartPosition.Manual })
        using (var outer = new DockPanel { Dock = DockStyle.Fill, Theme = new GreenTheme() })
        using (var viewer = new FrmImageViewer())
        {
            if (unopened)
            {
                CanvasSet.SaveDockPanelEvent?.Invoke(host, EventArgs.Empty);
                if (Convert.ToBase64String(File.ReadAllBytes(layoutPath)) != Convert.ToBase64String(originalLayout))
                    throw new Exception("An unopened image panel overwrote its saved layout.");
                Console.WriteLine("Unopened image panel preserves its saved layout.");
                return 0;
            }
            host.Controls.Add(outer);
            viewer.Show(outer, DockState.Document);
            host.Show();
            Application.DoEvents();
            bool lostFloat = Array.IndexOf(args, "--lost-float") >= 0;
            bool restore = Array.IndexOf(args, "--restore") >= 0 || lostFloat;
            if (!restore) viewer.SetWindowNum(4);
            Application.DoEvents();
            var inner = (DockPanel)typeof(FrmImageViewer).GetField("dockPanel1", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(viewer);
            FrmSingleImage first = FrmImageViewer.FrmSingleImages[0];
            if (lostFloat)
            {
                first.Activate();
                Application.DoEvents();
                Console.WriteLine("Recovered dock: " + first.DockState + ", Visible=" + first.Visible + ", Count=" + FrmImageViewer.CurWindowsNum + ", Bounds=" + first.Bounds);
                if (first.IsFloat || !first.Visible || FrmImageViewer.CurWindowsNum != 4 || first.Width < 200)
                    throw new Exception("Missing float pane must recover all four images to a laid-out docked position.");
                Console.WriteLine("Deleted float pane and stale IsFloat recovery passed.");
                return 0;
            }
            Screen screen = Array.Find(Screen.AllScreens, item => !item.Primary) ?? Screen.PrimaryScreen;
            Rectangle bounds = new Rectangle(screen.WorkingArea.X + 120, screen.WorkingArea.Y + 100, 640, 480);
            if (!restore) first.Show(inner, bounds);
            Application.DoEvents();
            FloatWindow floating = first.Pane.FloatWindow;
            if (first.DockState != DockState.Float || floating == null)
                throw new Exception("The saved floating image was not restored.");
            if (floating.Bounds != bounds)
                throw new Exception("Restored bounds changed: " + floating.Bounds + ", expected " + bounds);
            Console.WriteLine("Screens: " + string.Join("; ", Array.ConvertAll(Screen.AllScreens, item => item.Bounds.ToString())));
            Console.WriteLine("Float: " + floating.Bounds + ", Visible=" + floating.Visible + ", ContentVisible=" + first.Visible + ", Owner=" + floating.Owner?.GetType().Name + ", TopLevel=" + floating.TopLevel);
            var nativeBounds = new int[4];
            DwmGetWindowAttribute(floating.Handle, 9, nativeBounds, 16);
            Console.WriteLine("Physical float bounds: " + string.Join(",", nativeBounds));
            if (Math.Abs(nativeBounds[0] - bounds.X) > 20 || Math.Abs(nativeBounds[1] - bounds.Y) > 20)
                throw new Exception("DPI virtualization moved the window away from its requested location.");
            if (!screen.WorkingArea.Contains(Rectangle.FromLTRB(nativeBounds[0], nativeBounds[1], nativeBounds[2], nativeBounds[3])))
                throw new Exception("The floating window is not fully on the target screen.");
            if (FrmImageViewer.CurWindowsNum != 4)
                throw new Exception("Restored image count must be four, including the floating image.");
            using (var bitmap = new Bitmap(600, 400))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(nativeBounds[0], nativeBounds[1], 0, 0, bitmap.Size);
                bitmap.Save(Path.Combine(Output, restore ? "restored-float-screen.png" : "float-screen.png"));
            }
            CanvasSet.SaveDockPanelEvent?.Invoke(host, EventArgs.Empty);
            if (Array.IndexOf(args, "--interactive") >= 0)
            {
                host.Bounds = SystemInformation.VirtualScreen;
                first.DockState = DockState.DockLeft;
                first.Text = "副屏拖动验证";
                using (var timer = new Timer { Interval = 1000 })
                {
                    string previous = null;
                    timer.Tick += (sender, e) =>
                    {
                        string current = first.DockState + " " + first.Pane?.FloatWindow?.Bounds;
                        if (current == previous) return;
                        previous = current;
                        Console.WriteLine("Drag state: " + current);
                        CanvasSet.SaveDockPanelEvent?.Invoke(host, EventArgs.Empty);
                    };
                    timer.Start();
                    Application.Run(host);
                }
                return 0;
            }
            if (!floating.Visible || !first.Visible) throw new Exception("Secondary float is not visible.");
            first.DockState = DockState.Document;
            Application.DoEvents();
            first.IsFloat = true;
            Application.DoEvents();
            if (!first.Visible || !first.Pane.FloatWindow.Visible) throw new Exception("Refloating failed.");
            CanvasSet.SaveDockPanelEvent?.Invoke(host, EventArgs.Empty);
            Console.WriteLine(restore ? "Restore, physical bounds, visibility, count and refloat passed." : "Float, physical bounds, visibility, count and save passed.");
        }
        return 0;
    }

    /// <summary>调用实际拖动入口，在主副屏记录尺寸变化；只操作独立测试窗体，不保存布局。</summary>
    private static int DiagnoseDragSize(string[] args)
    {
        Screen primary = Screen.PrimaryScreen;
        Screen secondary = Array.Find(Screen.AllScreens, screen => !screen.Primary) ?? primary;
        Screen source = Array.IndexOf(args, "--secondary") >= 0 ? secondary : primary;
        Screen target = source == primary ? secondary : primary;
        using (var host = new FormBase { Text = "图像拖动尺寸诊断", StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(primary.WorkingArea.Location + new Size(80, 80), new Size(1100, 800)) })
        using (var outer = new DockPanel { Dock = DockStyle.Fill, Theme = new GreenTheme() })
        using (var viewer = new FrmImageViewer())
        {
            host.Controls.Add(outer);
            viewer.SetWindowNum(4);
            viewer.Show(outer, DockState.Document);
            host.Show();
            PumpMessages();
            host.Location = source.WorkingArea.Location + new Size(80, 80);
            PumpMessages();
            FrmSingleImage first = FrmImageViewer.FrmSingleImages[0];
            Console.WriteLine("Source=" + source.DeviceName + ", HostDpi=" + host.DeviceDpi +
                ", DockedPane=" + first.Pane.Size + ", Content=" + first.Size);
            Type dragType = typeof(DockPanel).Assembly.GetType("WeifenLuo.WinFormsUI.Docking.IDockDragSource", true);
            MethodInfo beginDrag = dragType.GetMethod("BeginDrag");
            MethodInfo endDrag = dragType.GetMethod("EndDrag");
            Rectangle preview = (Rectangle)beginDrag.Invoke(first.DockHandler,
                new object[] { first.Pane.PointToScreen(new Point(30, 10)) });
            endDrag.Invoke(first.DockHandler, null);
            Console.WriteLine("Docked drag preview=" + preview);
            first.DockHandler.FloatAt(preview);
            PumpMessages();
            FloatWindow floating = first.Pane.FloatWindow;
            PrintFloat("After undock", floating);
            // 先设置可比的用户尺寸，然后只改变拖动目标坐标，保留拖动开始时的矩形大小。
            floating.Bounds = new Rectangle(source.WorkingArea.Location + new Size(160, 160), new Size(640, 480));
            PumpMessages();
            foreach (Screen destination in new[] { source, target, source, target, source })
            {
                Size before = floating.Size;
                Rectangle proposed = (Rectangle)beginDrag.Invoke(floating,
                    new object[] { floating.Location + new Size(30, 10) });
                proposed.Location = destination.WorkingArea.Location + new Size(200, 200);
                floating.FloatAt(proposed);
                endDrag.Invoke(floating, null);
                PumpMessages();
                Console.WriteLine("Move to=" + destination.DeviceName + ", Before=" + before + ", Requested=" + proposed.Size);
                PrintFloat("After move", floating);
            }
            return 0;
        }
    }

    /// <summary>处理测试窗口布局与 DPI 消息。</summary>
    private static void PumpMessages()
    {
        for (int index = 0; index < 5; index++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
    }

    /// <summary>输出托管尺寸和 DWM 实际边界，避免把截图缩放误判为窗口缩放。</summary>
    private static void PrintFloat(string phase, FloatWindow floating)
    {
        var physical = new int[4];
        DwmGetWindowAttribute(floating.Handle, 9, physical, 16);
        Console.WriteLine(phase + ": Dpi=" + floating.DeviceDpi + ", Bounds=" + floating.Bounds +
            ", Dwm=" + string.Join(",", physical));
    }

    /// <summary>验证两屏浮窗合并、重新拆出和已释放窗口的迟到激活回调。</summary>
    private static int CheckFloatMerge()
    {
        int checks = 0;
        using (var host = new FormBase { Text = "浮窗合并回归", StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(80, 80, 1000, 700) })
        using (var outer = new DockPanel { Dock = DockStyle.Fill, Theme = new GreenTheme() })
        using (var viewer = new FrmImageViewer())
        {
            host.Controls.Add(outer);
            viewer.SetWindowNum(4);
            viewer.Show(outer, DockState.Document);
            host.Show();
            PumpMessages();
            var inner = (DockPanel)typeof(FrmImageViewer).GetField("dockPanel1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(viewer);
            FrmSingleImage first = FrmImageViewer.FrmSingleImages[0];
            FrmSingleImage second = FrmImageViewer.FrmSingleImages[1];
            FrmSingleImage third = FrmImageViewer.FrmSingleImages[2];
            foreach (Screen screen in Screen.AllScreens)
            foreach (DockStyle style in new[] { DockStyle.Fill, DockStyle.Left, DockStyle.Right, DockStyle.Top, DockStyle.Bottom })
            foreach (bool tabGroup in new[] { false, true })
            for (int repeat = 0; repeat < 3; repeat++)
            {
                // 清除上一轮保留的浮动停靠位置，确保本轮来源与目标确实是两个独立浮窗。
                first.DockPanel = null;
                second.DockPanel = null;
                third.DockPanel = null;
                PumpMessages();
                first.Show(inner, new Rectangle(screen.WorkingArea.Location + new Size(80, 100), new Size(500, 400)));
                second.Show(inner, new Rectangle(screen.WorkingArea.Location + new Size(650, 100), new Size(500, 400)));
                FrmSingleImage destination = second;
                if (tabGroup)
                {
                    second.Pane.FloatWindow.DockTo(first.Pane, DockStyle.Fill, -1);
                    third.Show(inner, new Rectangle(screen.WorkingArea.Location + new Size(650, 100), new Size(500, 400)));
                    destination = third;
                }
                PumpMessages();
                FloatWindow oldFloat = first.Pane.FloatWindow;
                FloatWindow target = destination.Pane.FloatWindow;
                if (oldFloat == target) throw new Exception("Merge requires two independent floating windows.");
                int activated = 0;
                EventHandler activationHandler = (sender, e) => activated++;
                oldFloat.Activated += activationHandler;
                oldFloat.GetType().GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(oldFloat, new object[] { EventArgs.Empty });
                oldFloat.Activated -= activationHandler;
                if (activated != 1) throw new Exception("Live float must still propagate its Activated event.");
                checks++;
                oldFloat.Activate();
                oldFloat.DockTo(destination.Pane, style, -1);
                PumpMessages();
                if (first.Pane.FloatWindow != target || second.Pane.FloatWindow != target ||
                    destination.Pane.FloatWindow != target || first.IsDisposed || second.IsDisposed ||
                    destination.IsDisposed || !target.Visible)
                    throw new Exception("Floating merge lost image content: " + screen.DeviceName + "/" + style);
                checks++;
                if (!oldFloat.IsDisposed || oldFloat.DockPanel != null)
                    throw new Exception("Merged source float was not disposed.");
                checks++;
                // 模拟合并释放完成后迟到的激活通知，重现现场堆栈中的同一生命周期边界。
                oldFloat.GetType().GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(oldFloat, new object[] { EventArgs.Empty });
                checks++;
                first.DockState = DockState.Document;
                second.DockState = DockState.Document;
                if (tabGroup) third.DockState = DockState.Document;
                PumpMessages();
                Console.WriteLine("Merge passed: " + screen.DeviceName + "/" + style + "/tabs=" + tabGroup + "/" + repeat);
            }
        }
        Console.WriteLine("Float merge lifecycle passed: " + checks + " checks.");
        return 0;
    }
}
