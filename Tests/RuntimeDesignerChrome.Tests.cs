using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>窗口外观独立回归测试，窗口仅在屏幕外显示，不启动生产入口或设备。</summary>
internal static class RuntimeDesignerChromeTests
{
    /// <summary>已经执行的断言数量。</summary>
    private static int assertions;
    /// <summary>记录真实窗口最终传给默认窗口过程的非客户区绘制消息。</summary>
    private static readonly List<Message> defaultFrameMessages = new List<Message>();

    /// <summary>按单线程单元模型验证真实设计器窗体。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            VerifyPhysicalMonitorMaximize();
            using (var editor = new OffscreenEditorForm()) VerifyShownActivation(editor);
            using (var preview = new OffscreenPreviewForm()) VerifyShownActivation(preview);
            using (var editor = new RuntimeInterfaceEditorForm()) VerifyWindow(editor);
            using (var preview = new RuntimeInterfacePreviewForm()) VerifyWindow(preview);
            using (var editor = new RuntimeInterfaceEditorForm()) VerifyEditor(editor);
            using (var editor = new OffscreenEditorForm()) VerifyShownRestore(editor);
            using (var preview = new OffscreenPreviewForm()) VerifyShownRestore(preview);
            Console.WriteLine("通过：窗口外观独立测试，" + assertions + " 项断言。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("失败：" + ex.GetBaseException().Message);
            return 1;
        }
    }

    /// <summary>在当前每块真实显示器上验证最大化结果，透明测试窗体不操作现场程序。</summary>
    private static void VerifyPhysicalMonitorMaximize()
    {
        using (var form = new TransparentPreviewForm())
        {
            form.WindowState = FormWindowState.Normal;
            form.StartPosition = FormStartPosition.Manual;
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            foreach (var screen in Screen.AllScreens)
            {
                form.WindowState = FormWindowState.Normal;
                form.Bounds = new Rectangle(screen.WorkingArea.X + 40, screen.WorkingArea.Y + 40, 700, 500);
                if (!form.Visible) form.Show();
                Application.DoEvents();
                Rectangle original = form.Bounds;
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    form.WindowState = FormWindowState.Maximized;
                    Application.DoEvents();
                    Rectangle area = Screen.FromHandle(form.Handle).WorkingArea;
                    Check(form.Bounds == area, "跨屏最大化必须严格填充当前工作区：" + screen.DeviceName +
                        "，期望=" + area + "，实际=" + form.Bounds);
                    var close = Field<Button>(form, "closeButton");
                    Check(area.Contains(close.RectangleToScreen(close.ClientRectangle)), "关闭按钮必须完整位于当前显示器内");
                    Check(form.ClientSize == Field<Control>(form, "rootLayout").Size, "根布局应填充最大化客户区");
                    form.WindowState = FormWindowState.Minimized;
                    Application.DoEvents();
                    form.WindowState = FormWindowState.Maximized;
                    Application.DoEvents();
                    Check(form.Bounds == area, "从任务栏恢复最大化状态不能放大到显示器外");
                    form.WindowState = FormWindowState.Normal;
                    Application.DoEvents();
                    Check(form.Bounds == original, "跨屏最大化和最小化不能改变普通还原边界");
                }
            }
            form.Close();
        }
    }

    /// <summary>用于真实多屏几何测试，不抢焦点、不显示任务栏。</summary>
    private sealed class TransparentPreviewForm : RuntimeInterfacePreviewForm
    {
        /// <summary>显示时保持用户当前应用的焦点。</summary>
        protected override bool ShowWithoutActivation => true;
    }

    /// <summary>向屏幕外真实窗口发送激活消息，检查系统绘制入口而不切换桌面焦点。</summary>
    private static void VerifyShownActivation(Form form)
    {
        form.WindowState = FormWindowState.Normal;
        form.StartPosition = FormStartPosition.Manual;
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        form.Bounds = new Rectangle(OffscreenLocation, new Size(1200, 800));
        try
        {
            form.Show();
            Application.DoEvents();
            Check(form.Visible && form.IsHandleCreated, "激活回归必须覆盖真实显示窗口和原生句柄");
            Rectangle original = form.Bounds;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                VerifyActivationMessages(form);
                Check(form.Bounds == original, "激活切换不能改变普通窗口边界");
                Click(Field<Button>(form, "maximizeButton"));
                Application.DoEvents();
                VerifyActivationMessages(form);
                Click(Field<Button>(form, "maximizeButton"));
                Application.DoEvents();
                Check(form.Bounds == original, "激活切换与最大化还原交替后不能发生尺寸漂移");
            }
            VerifyHitTests(form);
            Click(Field<Button>(form, "minimizeButton"));
            Application.DoEvents();
            defaultFrameMessages.Clear();
            NativeMessage(form.Handle, 0x86, IntPtr.Zero, IntPtr.Zero);
            Check(defaultFrameMessages.Exists(message => message.Msg == 0x86 && message.LParam == IntPtr.Zero), "最小化时应保留系统非客户区激活处理");
            form.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            Check(form.Bounds == original, "最小化激活消息不能污染还原尺寸");
        }
        finally { form.Close(); }
        Check(form.IsDisposed, "激活回归的屏幕外窗口必须销毁");
    }

    /// <summary>检查激活切换仍交给系统，但不允许原生灰色边框覆盖客户端边缘。</summary>
    private static void VerifyActivationMessages(Form form)
    {
        FormWindowState state = form.WindowState;
        Rectangle bounds = form.Bounds;
        defaultFrameMessages.Clear();
        IntPtr deactivate = NativeMessage(form.Handle, 0x86, IntPtr.Zero, IntPtr.Zero);
        NativeMessage(form.Handle, 0x85, new IntPtr(1), IntPtr.Zero);
        NativeMessage(form.Handle, 0x86, new IntPtr(1), IntPtr.Zero);
        NativeMessage(form.Handle, 0x85, new IntPtr(1), IntPtr.Zero);
        Application.DoEvents();
        Check(deactivate != IntPtr.Zero, "失活消息必须允许系统完成激活切换");
        Check(!defaultFrameMessages.Exists(message => message.Msg == 0x86 && message.LParam != new IntPtr(-1)), "激活切换不能进入默认非客户区边框重画路径");
        Check(defaultFrameMessages.Exists(message => message.Msg == 0x86 && message.WParam == IntPtr.Zero) &&
            defaultFrameMessages.Exists(message => message.Msg == 0x86 && message.WParam == new IntPtr(1)), "激活与失活状态仍应传递到系统默认窗口过程");
        Check(!defaultFrameMessages.Exists(message => message.Msg == 0x85), "自定义边框不能落入 WM_NCPAINT 系统绘制");
        Check(form.Bounds == bounds && form.WindowState == state, "非客户区激活绘制不能改变窗口几何或状态");
        Check(!SystemInformation.VirtualScreen.IntersectsWith(form.Bounds), "激活回归窗口必须始终保持在屏幕外");
    }

    /// <summary>显示真实窗口后连续切换状态，防止还原时重复叠加系统边框尺寸。</summary>
    private static void VerifyShownRestore(Form form)
    {
        form.WindowState = FormWindowState.Normal;
        form.StartPosition = FormStartPosition.Manual;
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        form.Bounds = new Rectangle(OffscreenLocation, new Size(2160, 1290));
        try
        {
            form.Show();
            Application.DoEvents();
            Check(form.Visible && form.IsHandleCreated, "尺寸回归必须覆盖真实显示窗口和原生句柄");
            Rectangle original = form.Bounds;
            Check(!SystemInformation.VirtualScreen.IntersectsWith(original), "测试窗口必须保持在屏幕外");
            for (int mode = 0; mode < 2; mode++)
            {
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    if (mode == 0) Click(Field<Button>(form, "maximizeButton"));
                    else SendWindowMessage(form, 0xA3, new IntPtr(2), IntPtr.Zero);
                    Application.DoEvents();
                    Check(form.WindowState == FormWindowState.Maximized, "真实窗口应进入最大化状态");
                    Check(form.Size == Screen.FromHandle(form.Handle).WorkingArea.Size, "校正还原缓存不能改变实际最大化工作区尺寸");
                    Check(!SystemInformation.VirtualScreen.IntersectsWith(form.Bounds), "最大化测试窗口不能进入桌面");
                    if (mode == 0) Click(Field<Button>(form, "maximizeButton"));
                    else SendWindowMessage(form, 0xA3, new IntPtr(2), IntPtr.Zero);
                    Application.DoEvents();
                    Check(form.WindowState == FormWindowState.Normal, "真实窗口应进入还原状态");
                    Check(form.Bounds == original, form.GetType().Name + " 第 " + cycle + " 次" + (mode == 0 ? "按钮" : "双击") +
                        "还原发生漂移，原始=" + original + "，实际=" + form.Bounds);
                }
            }
            form.Size = new Size(original.Width - 80, original.Height - 50);
            Application.DoEvents();
            Rectangle resized = form.Bounds;
            Check(resized.Size != original.Size, "回归测试应覆盖用户重新调整后的窗口尺寸");
            Click(Field<Button>(form, "maximizeButton"));
            Application.DoEvents();
            Click(Field<Button>(form, "maximizeButton"));
            Application.DoEvents();
            Check(form.Bounds == resized, "还原应使用最新普通窗口边界，不能回到首次显示尺寸");
            VerifyHitTests(form);
        }
        finally { form.Close(); }
        Check(form.IsDisposed, "屏幕外测试窗口必须销毁");
    }

    /// <summary>将测试窗口放在所有显示器之外，保留可编码的原生鼠标坐标。</summary>
    private static Point OffscreenLocation => new Point(SystemInformation.VirtualScreen.Left - 12000, SystemInformation.VirtualScreen.Top - 12000);

    /// <summary>只移动测试最大化位置到屏幕外，保留生产尺寸计算和还原流程。</summary>
    private static void KeepMaximizedOffscreen(Form form, ref Message message)
    {
        if (message.Msg == 0x46 && IsZoomed(form.Handle))
        {
            Marshal.WriteInt32(message.LParam, IntPtr.Size * 2, OffscreenLocation.X);
            Marshal.WriteInt32(message.LParam, IntPtr.Size * 2 + 4, OffscreenLocation.Y);
            return;
        }
        if (message.Msg != 0x24) return;
        Rectangle monitor = Screen.FromHandle(form.Handle).Bounds;
        Marshal.WriteInt32(message.LParam, 16, OffscreenLocation.X - monitor.X);
        Marshal.WriteInt32(message.LParam, 20, OffscreenLocation.Y - monitor.Y);
    }

    /// <summary>真实编辑器的屏幕外测试宿主，不激活或显示任务栏按钮。</summary>
    private sealed class OffscreenEditorForm : RuntimeInterfaceEditorForm
    {
        /// <summary>显示测试窗口时不抢占当前应用焦点。</summary>
        protected override bool ShowWithoutActivation => true;
        /// <summary>保留生产消息处理，只调整测试窗口最大化坐标。</summary>
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            KeepMaximizedOffscreen(this, ref message);
        }
        /// <summary>观察真实默认处理入口，记录后继续执行系统逻辑。</summary>
        protected override void DefWndProc(ref Message message)
        {
            if (message.Msg == 0x85 || message.Msg == 0x86) defaultFrameMessages.Add(message);
            base.DefWndProc(ref message);
        }
    }

    /// <summary>真实预览窗口的屏幕外测试宿主。</summary>
    private sealed class OffscreenPreviewForm : RuntimeInterfacePreviewForm
    {
        /// <summary>显示测试窗口时不抢占当前应用焦点。</summary>
        protected override bool ShowWithoutActivation => true;
        /// <summary>保留生产消息处理，只调整测试窗口最大化坐标。</summary>
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            KeepMaximizedOffscreen(this, ref message);
        }
        /// <summary>观察真实默认处理入口，记录后继续执行系统逻辑。</summary>
        protected override void DefWndProc(ref Message message)
        {
            if (message.Msg == 0x85 || message.Msg == 0x86) defaultFrameMessages.Add(message);
            base.DefWndProc(ref message);
        }
    }

    /// <summary>验证窗口按钮、边缘缩放、最大化约束和取消关闭行为。</summary>
    private static void VerifyWindow(Form form)
    {
        if (form is RuntimeInterfacePreviewForm)
            Check(form.WindowState == FormWindowState.Maximized, "预览窗口应默认最大化");
        form.WindowState = FormWindowState.Normal;
        Check(form.FormBorderStyle == FormBorderStyle.None, form.GetType().Name + " 应使用无边框窗口");
        IntPtr handle = form.Handle;
        var title = Field<Control>(form, "titleLabel");
        var minimize = Field<Button>(form, "minimizeButton");
        var maximize = Field<Button>(form, "maximizeButton");
        var close = Field<Button>(form, "closeButton");
        var tooltip = Field<ToolTip>(form, "windowToolTip");
        foreach (var size in new[] { form.Size, form.MinimumSize, new Size(1600, 1000) })
        {
            form.Size = size;
            form.PerformLayout();
            Check(!title.Bounds.IntersectsWith(minimize.Bounds), "标题不能覆盖窗口按钮");
            Check(!minimize.Bounds.IntersectsWith(maximize.Bounds) && !maximize.Bounds.IntersectsWith(close.Bounds), "窗口按钮不能重叠");
            Check(close.Right <= close.Parent.ClientSize.Width && close.Height >= 30, "关闭按钮应完整显示");
            Check(minimize.Width >= 40 && maximize.Width >= 40, "窗口按钮应保持可点击尺寸");
            Check(title.Parent.Height * 96f / form.DeviceDpi <= 32.5f, "紧凑标题栏逻辑高度不能超过 32 像素");
        }
        Check(minimize.Image != null && maximize.Image != null && close.Image != null, "窗口按钮应显示标准图标");
        Check(tooltip.GetToolTip(minimize) == "最小化" && tooltip.GetToolTip(maximize) == "最大化" && tooltip.GetToolTip(close) == "关闭", "窗口按钮应显示中文提示");
        VerifyHitTests(form);
        VerifyWorkingArea(form);
        Click(maximize);
        Check(form.WindowState == FormWindowState.Maximized && tooltip.GetToolTip(maximize) == "还原", "最大化后应切换为还原操作");
        Check(HitTest(form, new Point(1, 1)) != 13, "最大化时应禁用边缘缩放");
        Click(maximize);
        Check(form.WindowState == FormWindowState.Normal && tooltip.GetToolTip(maximize) == "最大化", "还原后应恢复窗口操作");
        SendWindowMessage(form, 0xA3, new IntPtr(2), IntPtr.Zero);
        Check(form.WindowState == FormWindowState.Maximized, "标题栏双击应最大化");
        SendWindowMessage(form, 0xA3, new IntPtr(2), IntPtr.Zero);
        Check(form.WindowState == FormWindowState.Normal, "标题栏再次双击应还原");
        Click(minimize);
        Check(form.WindowState == FormWindowState.Minimized, "最小化按钮应改变窗口状态");
        form.WindowState = FormWindowState.Normal;
        int closing = 0;
        FormClosingEventHandler cancelClose = (sender, args) => { closing++; args.Cancel = true; };
        form.FormClosing += cancelClose;
        Click(close);
        Check(closing == 1 && !form.IsDisposed, "关闭按钮应保留可取消的标准关闭流程");
        form.FormClosing -= cancelClose;
        Click(close);
        Check(form.IsDisposed, "允许关闭时应释放窗口");
    }

    /// <summary>验证八个边缘方向，覆盖嵌套控件之外的实际窗口缩放区域。</summary>
    private static void VerifyHitTests(Form form)
    {
        int width = form.ClientSize.Width;
        int height = form.ClientSize.Height;
        Point[] points = { new Point(1, 1), new Point(width / 2, 1), new Point(width - 2, 1),
            new Point(1, height / 2), new Point(width - 2, height / 2),
            new Point(1, height - 2), new Point(width / 2, height - 2), new Point(width - 2, height - 2) };
        int[] expected = { 13, 12, 14, 10, 11, 16, 15, 17 };
        for (int index = 0; index < points.Length; index++)
            Check(HitTest(form, points[index]) == expected[index], "边缘命中方向错误：" + expected[index]);
        Check(form.Padding.Left >= 5 && form.Padding.Bottom >= 5, "应保留不被子控件遮挡的缩放边缘");
    }

    /// <summary>核对原生最大化边界和最小拖动尺寸。</summary>
    private static void VerifyWorkingArea(Form form)
    {
        IntPtr buffer = Marshal.AllocHGlobal(40);
        try
        {
            for (int offset = 0; offset < 40; offset += 4) Marshal.WriteInt32(buffer, offset, 0);
            SendWindowMessage(form, 0x24, IntPtr.Zero, buffer);
            var screen = Screen.FromHandle(form.Handle);
            Check(Marshal.ReadInt32(buffer, 8) == screen.WorkingArea.Width && Marshal.ReadInt32(buffer, 12) == screen.WorkingArea.Height, "最大化尺寸应排除任务栏");
            Check(Marshal.ReadInt32(buffer, 16) == screen.WorkingArea.X - screen.Bounds.X && Marshal.ReadInt32(buffer, 20) == screen.WorkingArea.Y - screen.Bounds.Y, "最大化位置应使用当前屏幕工作区");
            Check(Marshal.ReadInt32(buffer, 24) >= form.MinimumSize.Width && Marshal.ReadInt32(buffer, 28) >= form.MinimumSize.Height, "拖动缩放应遵守最小窗口尺寸");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    /// <summary>验证组件选择仍能同步到属性栏，图层命令仍然有效。</summary>
    private static void VerifyEditor(RuntimeInterfaceEditorForm editor)
    {
        Check(editor.GetType().GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic) == null, "编辑器应移除组件列表");
        var session = Field<RuntimeLayoutSession>(editor, "session");
        var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
        var first = session.Add(RuntimeWidgetKind.Image, new Point(40, 40));
        session.Add(RuntimeWidgetKind.Image, new Point(80, 80));
        canvas.SelectElement(first.Id);
        Check(Field<PropertyGrid>(editor, "propertyGrid").SelectedObject is RuntimeElementProperties, "画布选择应同步组件属性");
        Field<ToolStripButton>(editor, "forwardButton").PerformClick();
        Check(session.Document.Elements[1].Id == first.Id, "上移图层命令应继续生效");
        Field<ToolStripButton>(editor, "backwardButton").PerformClick();
        Check(session.Document.Elements[0].Id == first.Id, "下移图层命令应继续生效");
        Check(!Field<Label>(editor, "titleLabel").Text.Contains("*") && !editor.Text.Contains("*"), "未保存状态不能在标题中显示星号");
        Check(Field<ToolStripStatusLabel>(editor, "stateLabel").Text.Contains("未保存"), "未保存状态应保留在底部状态栏");
    }

    /// <summary>将客户端坐标编码为原生命中测试消息。</summary>
    private static int HitTest(Form form, Point client)
    {
        Point screen = form.PointToScreen(client);
        return SendWindowMessage(form, 0x84, IntPtr.Zero, new IntPtr(unchecked((screen.Y << 16) | (screen.X & 0xffff)))).ToInt32();
    }

    /// <summary>直接调用隐藏窗体消息入口，不发送桌面输入。</summary>
    private static IntPtr SendWindowMessage(Form form, int id, IntPtr parameter, IntPtr data)
    {
        object[] args = { Message.Create(form.Handle, id, parameter, data) };
        form.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args);
        return ((Message)args[0]).Result;
    }

    /// <summary>直接触发按钮事件，无需显示窗口。</summary>
    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty });

    /// <summary>读取设计器控件或会话以验证真实交互结果。</summary>
    private static T Field<T>(object owner, string name)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return (T)field.GetValue(owner);
        }
        throw new InvalidOperationException("未找到测试字段：" + name);
    }

    /// <summary>在首次行为偏差时报告明确的失败原因。</summary>
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>仅向测试窗口发送原生消息，不移动鼠标、不切换其它应用焦点。</summary>
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr NativeMessage(IntPtr window, int message, IntPtr parameter, IntPtr data);

    /// <summary>测试宿主在托管Resize前读取原生最大化状态以保持透明窗口在屏幕外。</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);
}
