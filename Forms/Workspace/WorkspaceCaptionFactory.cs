using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.Login;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision.Forms.Workspace
{
    /// <summary>装饰现有主题标题栏工厂，保留外观并为每个新建窗格补齐双击权限。</summary>
    internal sealed class WorkspaceCaptionFactory : DockPanelExtender.IDockPaneCaptionFactory
    {
        /// <summary>主题原有的标题栏工厂。</summary>
        private readonly DockPanelExtender.IDockPaneCaptionFactory inner;

        /// <summary>保留主题提供的绘制和按钮行为。</summary>
        internal WorkspaceCaptionFactory(DockPanelExtender.IDockPaneCaptionFactory inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>为新建、恢复布局及拆分产生的标题栏安装局部消息保护。</summary>
        public DockPaneCaptionBase CreateDockPaneCaption(DockPane pane)
        {
            DockPaneCaptionBase caption = inner.CreateDockPaneCaption(pane);
            new CaptionPermissionWindow(caption);
            return caption;
        }

        /// <summary>随标题栏句柄存活的消息保护，不替换主题控件或拦截图像区域双击。</summary>
        private sealed class CaptionPermissionWindow : NativeWindow
        {
            /// <summary>持有并订阅句柄生命周期的原主题标题栏。</summary>
            private readonly DockPaneCaptionBase caption;

            /// <summary>兼容尚未创建句柄及后续DPI等原因造成的句柄重建。</summary>
            internal CaptionPermissionWindow(DockPaneCaptionBase caption)
            {
                this.caption = caption;
                caption.HandleCreated += HandleCreated;
                caption.HandleDestroyed += HandleDestroyed;
                caption.Disposed += CaptionDisposed;
                if (caption.IsHandleCreated) AssignHandle(caption.Handle);
            }

            /// <summary>在每次句柄创建后重新安装保护。</summary>
            private void HandleCreated(object sender, EventArgs e) { AssignHandle(caption.Handle); }

            /// <summary>解除已销毁句柄，避免原生句柄复用时影响其它窗口。</summary>
            private void HandleDestroyed(object sender, EventArgs e)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }

            /// <summary>释放标题栏时解除所有事件订阅。</summary>
            private void CaptionDisposed(object sender, EventArgs e)
            {
                caption.HandleCreated -= HandleCreated;
                caption.HandleDestroyed -= HandleDestroyed;
                caption.Disposed -= CaptionDisposed;
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }

            /// <summary>原库客户区双击不检查拖放权限，必须在它切换浮动状态之前拦截。</summary>
            protected override void WndProc(ref Message m)
            {
                const int wmLeftButtonDoubleClick = 0x0203;
                if (m.Msg == wmLeftButtonDoubleClick && !UserPermissionContext.CanEditWorkspaceLayout)
                    return;

                base.WndProc(ref m);
            }
        }
    }
}
