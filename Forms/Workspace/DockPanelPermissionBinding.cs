using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TDJS_Vision.Forms.Login;
using WeifenLuo.WinFormsUI.Docking;

namespace TDJS_Vision.Forms.Workspace
{
    /// <summary>随停靠面板存活的权限绑定，覆盖现有和后续加载的窗格，并在面板释放时解除订阅。</summary>
    internal sealed class DockPanelPermissionBinding
    {
        /// <summary>当前受保护的停靠面板。</summary>
        private readonly DockPanel panel;

        /// <summary>记录窗格自身的原始能力，高级权限也不能打开原本就禁止的操作。</summary>
        private readonly Dictionary<IDockContent, ContentPermissions> contents = new Dictionary<IDockContent, ContentPermissions>();

        /// <summary>窗格原始操作能力。</summary>
        private sealed class ContentPermissions
        {
            /// <summary>窗格原本是否允许关闭。</summary>
            internal bool CanClose;
            /// <summary>窗格原本是否允许拖放。</summary>
            internal bool CanDrag;
        }

        /// <summary>绑定权限事件，面板自身负责触发释放解绑。</summary>
        internal DockPanelPermissionBinding(DockPanel panel)
        {
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
            // 原库标题栏双击独立于拖动权限，必须在窗格创建前装饰主题工厂。
            if (!(panel.Theme.Extender.DockPaneCaptionFactory is WorkspaceCaptionFactory))
                panel.Theme.Extender.DockPaneCaptionFactory = new WorkspaceCaptionFactory(panel.Theme.Extender.DockPaneCaptionFactory);
            panel.ContentAdded += ContentAdded;
            panel.ContentRemoved += ContentRemoved;
            panel.Disposed += PanelDisposed;
            UserPermissionContext.RoleChanged += RoleChanged;
            foreach (IDockContent content in panel.Contents) Register(content);
            ApplyPermissions();
        }

        /// <summary>为新加入或从文件恢复的窗格立即设置当前权限。</summary>
        private void ContentAdded(object sender, DockContentEventArgs e) { Register(e.Content); ApplyPermissions(); }

        /// <summary>记录能力并绑定用户关闭事件，避免快捷键绕过关闭按钮。</summary>
        private void Register(IDockContent content)
        {
            if (contents.ContainsKey(content)) return;
            contents.Add(content, new ContentPermissions { CanClose = content.DockHandler.CloseButton,
                CanDrag = content.DockHandler.AllowEndUserDocking });
            content.DockHandler.Form.FormClosing += ContentClosing;
        }

        /// <summary>窗格离开面板时解除事件并恢复原始能力，避免跨面板迁移遗留低权限状态。</summary>
        private void ContentRemoved(object sender, DockContentEventArgs e)
        {
            if (!contents.TryGetValue(e.Content, out ContentPermissions original)) return;
            contents.Remove(e.Content);
            e.Content.DockHandler.Form.FormClosing -= ContentClosing;
            if (e.Content.DockHandler.Form.IsDisposed || e.Content.DockHandler.Form.Disposing) return;
            e.Content.DockHandler.CloseButton = original.CanClose;
            e.Content.DockHandler.AllowEndUserDocking = original.CanDrag;
        }

        /// <summary>角色变化时立即更新所有已打开窗格。</summary>
        private void RoleChanged(object sender, UserRole role) { ApplyPermissions(); }

        /// <summary>只在角色或窗格集合变化时刷新权限，不增加图像和日志刷新负担。</summary>
        private void ApplyPermissions()
        {
            if (panel.IsDisposed || panel.Disposing) return;
            bool allowed = UserPermissionContext.CanEditWorkspaceLayout;
            panel.AllowEndUserDocking = allowed;
            foreach (KeyValuePair<IDockContent, ContentPermissions> entry in contents)
            {
                entry.Key.DockHandler.CloseButton = allowed && entry.Value.CanClose;
                entry.Key.DockHandler.AllowEndUserDocking = allowed && entry.Value.CanDrag;
            }
        }

        /// <summary>只阻止用户关闭工作区内容，不妨碍应用退出和资源释放。</summary>
        private void ContentClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !UserPermissionContext.CanEditWorkspaceLayout)
                e.Cancel = true;
        }

        /// <summary>面板释放时断开静态事件，防止已关闭窗口被长期持有。</summary>
        private void PanelDisposed(object sender, EventArgs e)
        {
            UserPermissionContext.RoleChanged -= RoleChanged;
            panel.ContentAdded -= ContentAdded;
            panel.ContentRemoved -= ContentRemoved;
            panel.Disposed -= PanelDisposed;
            foreach (IDockContent content in contents.Keys)
                content.DockHandler.Form.FormClosing -= ContentClosing;
            contents.Clear();
        }
    }
}
