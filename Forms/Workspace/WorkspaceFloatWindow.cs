using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.Login;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision.Forms.Workspace
{
    /// <summary>工作区浮窗工厂，统一保护主面板和图像面板的浮窗生命周期及用户操作权限。</summary>
    internal sealed class WorkspaceFloatWindowFactory : DockPanelExtender.IFloatWindowFactory
    {
        /// <summary>按停靠面板的默认尺寸创建图像浮窗。</summary>
        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane)
        {
            return new WorkspaceFloatWindow(dockPanel, pane);
        }

        /// <summary>按拖放或布局恢复提供的矩形创建图像浮窗。</summary>
        public FloatWindow CreateFloatWindow(DockPanel dockPanel, DockPane pane, Rectangle bounds)
        {
            return new WorkspaceFloatWindow(dockPanel, pane, bounds);
        }
    }

    /// <summary>防止图像浮窗合并释放后，迟到的激活消息访问已清空的DockPanel。</summary>
    internal sealed class WorkspaceFloatWindow : FloatWindow
    {
        /// <summary>在基类清空DockPanel之前标记释放，覆盖原生窗口销毁期间的重入消息。</summary>
        private bool disposalStarted;

        /// <summary>创建默认位置和尺寸的图像浮窗。</summary>
        internal WorkspaceFloatWindow(DockPanel dockPanel, DockPane pane) : base(dockPanel, pane)
        {
        }

        /// <summary>创建指定桌面矩形的图像浮窗。</summary>
        internal WorkspaceFloatWindow(DockPanel dockPanel, DockPane pane, Rectangle bounds)
            : base(dockPanel, pane, bounds)
        {
        }

        /// <summary>仅对仍与有效停靠面板关联的浮窗执行原生激活与内容事件传播。</summary>
        protected override void OnActivated(EventArgs e)
        {
            // 原库先清空DockPanel再销毁句柄，合并浮窗时可能重入WM_ACTIVATE。
            if (disposalStarted || IsDisposed || Disposing || DockPanel == null ||
                DockPanel.IsDisposed || DockPanel.Disposing)
                return;

            base.OnActivated(e);
        }

        /// <summary>先关闭激活入口，再由原库移除浮窗和释放资源。</summary>
        protected override void Dispose(bool disposing)
        {
            disposalStarted = true;
            base.Dispose(disposing);
        }

        /// <summary>阻止低权限通过原生标题栏、系统菜单或Alt+F4绕过停靠控件的操作限制。</summary>
        protected override void WndProc(ref Message m)
        {
            const int wmClose = 0x0010;
            const int wmNonClientLeftDown = 0x00A1;
            const int wmNonClientDoubleClick = 0x00A3;
            const int wmSystemCommand = 0x0112;
            const int hitCaption = 2;
            const long systemMove = 0xF010;
            const long systemClose = 0xF060;
            if (!UserPermissionContext.CanEditWorkspaceLayout)
            {
                long command = m.WParam.ToInt64() & 0xFFF0;
                if (m.Msg == wmClose ||
                    ((m.Msg == wmNonClientLeftDown || m.Msg == wmNonClientDoubleClick) && m.WParam.ToInt64() == hitCaption) ||
                    (m.Msg == wmSystemCommand && (command == systemMove || command == systemClose)))
                    return;
            }
            base.WndProc(ref m);
        }

        /// <summary>阻止用户关闭低权限浮窗，但允许主程序退出时统一释放所属窗口。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && !UserPermissionContext.CanEditWorkspaceLayout)
                e.Cancel = true;
        }
    }
}
