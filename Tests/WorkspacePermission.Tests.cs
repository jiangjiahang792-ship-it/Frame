using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.Login;
using WeifenLuo.WinFormsUI.Docking;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

/// <summary>使用真实主窗体控件验证权限，不执行主窗体Load、方案加载或设备初始化。</summary>
internal static class WorkspacePermissionTests
{
    /// <summary>通过的断言数量。</summary>
    private static int checks;

    /// <summary>验证角色切换、保存入口、训练拦截以及双屏浮窗的关闭和拖动保护。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            VerifyManufacturerLogin();
            using (var main = new FormMain())
            using (var host = new Form { Text = "工作区权限回归", StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(80, 80, 1000, 700) })
            {
                var panel = Read<DockPanel>(main, "dockPanel1");
                // 不显示主窗体：将其真实工作区移到无业务加载事件的测试宿主。
                host.Controls.Add(panel);
                var viewer = ReadStatic<FrmImageViewer>("FrmImgeDlg");
                viewer.SetWindowNum(4);
                viewer.Show(panel, DockState.Document);
                foreach (string name in new[] { "FrmLoggerDlg", "FrmResultDlg", "FrmLogoDlg" })
                    ReadStatic<DockContent>(name).Show(panel, DockState.Document);
                viewer.Activate();
                host.Show();
                Application.DoEvents();
                var inner = Read<DockPanel>(viewer, "dockPanel1");
                FrmSingleImage first = FrmImageViewer.FrmSingleImages[0];

                foreach (UserRole role in new[] { UserRole.Low, UserRole.Manufacturer, UserRole.Hight, UserRole.Medium, UserRole.Manufacturer, UserRole.Unknown, UserRole.Low })
                {
                    Invoke(main, "SetLockStatus", role);
                    bool layout = role == UserRole.Manufacturer;
                    bool high = layout || role == UserRole.Hight;
                    bool save = high || role == UserRole.Low;
                    Check(Read<ToolStripItem>(main, "视图ToolStripMenuItem").Enabled == layout, "只有厂家可操作视图布局菜单");
                    Check(Read<ToolStripItem>(main, "运行界面设计ToolStripMenuItem").Enabled == layout, "只有厂家可编辑运行界面布局");
                    Check(Read<ToolStripItem>(main, "画布设置ToolStripMenuItem").Enabled == layout, "只有厂家可设置画布布局");
                    Check(UserPermissionContext.CanUseManualTuning == high, "厂家继承高级手动调参权限");
                    Check(Read<ToolStripItem>(main, "toolStripMenuItem1").Enabled == high, "AI训练主菜单权限");
                    Check(Read<ToolStripItem>(main, "tsbt_SolSave").Enabled == save, "保存按钮权限");
                    Check(Read<ToolStripItem>(main, "保存方案ToolStripMenuItem").Enabled == save, "保存菜单权限");
                    Check(Read<ToolStripItem>(main, "新建方案ToolStripMenuItem").Enabled == high, "不放开新建菜单");
                    Check(Read<ToolStripItem>(main, "另存方案ToolStripMenuItem").Enabled == high, "不放开另存菜单");
                    Check(panel.AllowEndUserDocking == layout && inner.AllowEndUserDocking == layout, "两层面板拖动权限");
                    foreach (IDockContent content in panel.Contents) CheckContent(content, layout);
                    foreach (IDockContent content in inner.Contents) CheckContent(content, layout);
                    Invoke(main, "UpdateUI", true);
                    Check(!Read<ToolStripItem>(main, "tsbt_SolSave").Enabled, "运行中保存保护");
                    Check(!Read<ToolStripItem>(main, "toolStripMenuItem1").Enabled, "运行中训练保护");
                    Invoke(main, "UpdateUI", false);
                    Check(Read<ToolStripItem>(main, "tsbt_SolSave").Enabled == save, "停止后恢复角色保存权限");
                    Check(Read<ToolStripItem>(main, "toolStripMenuItem1").Enabled == high, "停止不越权开放训练");
                    if (!layout)
                    {
                        foreach (string name in new[] { "FrmImgeDlg", "FrmLoggerDlg", "FrmResultDlg", "FrmLogoDlg" })
                        {
                            DockContent content = ReadStatic<DockContent>(name);
                            content.Close();
                            Check(!content.IsHidden && !content.IsDisposed, "低权限不能关闭或隐藏：" + name);
                        }
                        foreach (string menu in new[] { "默认视图", "检测图像视图", "检测结果视图", "运行日志视图", "公司Logo视图" })
                            Invoke(main, menu + "ToolStripMenuItem_Click", null, EventArgs.Empty);
                        Check(!viewer.IsHidden, "直接调用视图菜单不能绕过厂家权限");
                    }
                    if (!high)
                    {
                        // 直接调用入口也必须返回，不能弹出训练窗口或加载训练依赖。
                        Invoke(main, "OpenUnsupervisedTrainForm");
                        Invoke(main, "OpenLargeModelTrainForm");
                        Check(Read<Form>(main, "unsupervisedTrainForm") == null && Read<Form>(main, "largeModelTrainForm") == null,
                            "训练入口不能绕过权限");
                    }
                }

                foreach (Screen screen in Screen.AllScreens)
                {
                    Invoke(main, "SetLockStatus", UserRole.Manufacturer);
                    first.Show(inner, new Rectangle(screen.WorkingArea.Location + new Size(120, 100), new Size(600, 450)));
                    Application.DoEvents();
                    FloatWindow floating = first.Pane.FloatWindow;
                    Invoke(main, "SetLockStatus", UserRole.Low);
                    Rectangle bounds = floating.Bounds;
                    SendCaptionDoubleClick(first);
                    Check(!floating.IsDisposed && floating.Bounds == bounds && first.IsFloat,
                        "初级权限双击内部标题栏不能返回主屏：" + screen.DeviceName);
                    foreach (int message in new[] { 0xA1, 0xA3, 0x112, 0x10 })
                    {
                        SendWindowMessage(floating, message, message == 0x112 ? 0xF010 : 2);
                        Check(!floating.IsDisposed && floating.Bounds == bounds && first.IsFloat, "低权限不能拖动/双击/关闭浮窗");
                    }
                    SendWindowMessage(floating, 0x112, 0xF060);
                    Check(!floating.IsDisposed && !first.IsHidden, "系统关闭命令不能绕过权限");
                    int countBeforeClose = FrmImageViewer.CurWindowsNum;
                    first.Close();
                    floating.Close();
                    Application.DoEvents();
                    Check(!first.IsDisposed && !floating.IsDisposed && !first.IsHidden, "关闭入口不能绕过权限");
                    Check(FrmImageViewer.CurWindowsNum == countBeforeClose, "拒绝关闭不能误减图像窗口数量");
                    using (var late = new DockContent { Text = "新加入窗格" })
                    {
                        late.Show(panel, DockState.Document);
                        CheckContent(late, false);
                        Invoke(main, "SetLockStatus", UserRole.Manufacturer);
                        CheckContent(late, true);
                        late.Close();
                        Check(late.IsDisposed, "厂家权限可以关闭窗格");
                    }
                    SendWindowMessage(floating, 0x10, 0);
                    Check(first.IsHidden, "厂家权限可以通过浮窗关闭按钮隐藏图像");
                    first.Show(inner, DockState.Document);
                    var logger = ReadStatic<DockContent>("FrmLoggerDlg");
                    logger.Show(panel, new Rectangle(screen.WorkingArea.Location + new Size(120, 100), new Size(600, 450)));
                    FloatWindow logFloat = logger.Pane.FloatWindow;
                    Invoke(main, "SetLockStatus", UserRole.Low);
                    SendWindowMessage(logFloat, 0x10, 0);
                    Check(!logger.IsHidden && !logFloat.IsDisposed, "主面板的日志浮窗同样禁止关闭");
                    Invoke(main, "SetLockStatus", UserRole.Manufacturer);
                    SendWindowMessage(logFloat, 0x10, 0);
                    Check(logger.IsHidden, "厂家权限恢复日志浮窗关闭");
                    logger.Show(panel, DockState.Document);
                    first.DockState = DockState.Document;
                    CheckCaptionPermissions(main, first, inner, screen);
                    CheckCaptionPermissions(main, logger, panel, screen);
                }
                Invoke(main, "SetLockStatus", UserRole.Low);
                host.Close();
                Check(host.IsDisposed, "低权限不能阻止宿主退出");
            }
            UserPermissionContext.UpdateRole(UserRole.Manufacturer);
            UserPermissionContext.UpdateRole(UserRole.Low);
            Check(true, "释放后角色切换不访问已释放窗格");
            Console.WriteLine("通过：工作区权限，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>检查停靠内容的按钮及拖放能力。</summary>
    private static void CheckContent(IDockContent content, bool allowed)
    {
        Check(content.DockHandler.CloseButton == allowed, "关闭按钮权限：" + content.DockHandler.Form.Text);
        Check(content.DockHandler.AllowEndUserDocking == allowed, "窗格拖动权限：" + content.DockHandler.Form.Text);
    }

    /// <summary>通过真实登录控件验证厂家密码、默认初级选择及退出后的布局锁定。</summary>
    private static void VerifyManufacturerLogin()
    {
        using (var login = new FormLogin())
        {
            var roles = Read<ComboBox>(login, "comboBox1");
            Check(roles.SelectedItem.ToString() == "初级权限", "新增厂家不能改变默认初级选择");
            roles.SelectedIndex = 0;
            Check(roles.SelectedItem.ToString() == "厂家", "厂家选项应显示在最高位置");
            Read<TextBox>(login, "textBoxPassword").Text = "tdjs";
            Invoke(login, "buttonLogin_Click", null, EventArgs.Empty);
            Check(UserPermissionContext.CurrentRole == UserRole.Manufacturer && UserPermissionContext.CanEditWorkspaceLayout,
                "厂家正确密码应开放布局权限");
            Invoke(login, "buttonQuit_Click", null, EventArgs.Empty);
            Check(!UserPermissionContext.CanEditWorkspaceLayout, "退出厂家后应立即锁定布局");
        }
    }

    /// <summary>对独立测试窗口调用实际消息入口，不发送鼠标操作或生产命令。</summary>
    private static void SendWindowMessage(FloatWindow window, int message, int value)
    {
        Message native = Message.Create(window.Handle, message, new IntPtr(value), IntPtr.Zero);
        window.GetType().GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(window, new object[] { native });
    }

    /// <summary>验证两层面板的双击权限、角色恢复、低权限新建窗格及句柄重建。</summary>
    private static void CheckCaptionPermissions(FormMain main, DockContent content, DockPanel panel, Screen screen)
    {
        foreach (UserRole role in new[] { UserRole.Low, UserRole.Medium, UserRole.Hight, UserRole.Unknown })
        {
            Invoke(main, "SetLockStatus", role);
            // 布局恢复通过程序接口创建浮窗，在低权限时也要自动带上双击保护。
            content.Show(panel, new Rectangle(screen.WorkingArea.Location + new Size(120, 100), new Size(600, 450)));
            Application.DoEvents();
            FloatWindow floating = content.Pane.FloatWindow;
            Rectangle bounds = floating.Bounds;
            Check(!FindCaption(content).Enabled, "非厂家不能使用标题栏的自动隐藏及关闭按钮");
            SendCaptionDoubleClick(content);
            Check(content.IsFloat && !floating.IsDisposed && floating.Bounds == bounds,
                "低权限内部标题栏不回主屏：" + screen.DeviceName + "/" + role + "/" + content.Text);
            Invoke(FindCaption(content), "RecreateHandle");
            SendCaptionDoubleClick(content);
            Check(content.IsFloat && !floating.IsDisposed && floating.Bounds == bounds, "句柄重建后双击仍被拦截");
            Invoke(main, "SetLockStatus", UserRole.Manufacturer);
            Check(FindCaption(content).Enabled, "厂家应恢复标题栏布局按钮");
            SendCaptionDoubleClick(content);
            Check(!content.IsFloat, "切回厂家权限可双击恢复停靠");
            Invoke(main, "SetLockStatus", role);
            DockPane pane = content.Pane;
            DockState state = content.DockState;
            SendCaptionDoubleClick(content);
            Check(!content.IsFloat && content.Pane == pane && content.DockState == state, "低权限停靠窗格双击也不能浮动");
            Invoke(main, "SetLockStatus", UserRole.Manufacturer);
            SendCaptionDoubleClick(content);
            Check(content.IsFloat, "厂家权限可双击恢复浮动");
            content.Show(panel, DockState.Document);
        }
    }

    /// <summary>查找主题实际创建的标题栏控件，不依赖私有字段名或颜色。</summary>
    private static DockPaneCaptionBase FindCaption(DockContent content)
    {
        foreach (Control control in content.Pane.Controls)
        {
            if (control is DockPaneCaptionBase caption) return caption;
        }
        throw new Exception("未找到真实窗格标题栏。");
    }

    /// <summary>向真实标题栏句柄发送双击，覆盖控件库未检查拖放权限的客户区消息路径。</summary>
    private static void SendCaptionDoubleClick(DockContent content)
    {
        SendMessage(FindCaption(content).Handle, 0x0203, new IntPtr(1), new IntPtr((8 << 16) | 20));
        Application.DoEvents();
    }

    /// <summary>仅向测试宿主拥有的控件派发原生窗口消息。</summary>
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    /// <summary>读取实际主窗体或图像管理器中的控件。</summary>
    private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    /// <summary>读取主窗体已经创建的工作区内容，避免额外创建静态图像窗口集合。</summary>
    private static T ReadStatic<T>(string name) => (T)typeof(FormMain).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    /// <summary>调用实际入口而不是复制权限逻辑。</summary>
    private static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

    /// <summary>累计通过数并报告错误。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
