using System;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.Workspace
{
    /// <summary>
    /// 通过隐藏主窗体、显示单一工作区和最终恢复主窗体，避免任务栏出现两个业务窗口。
    /// </summary>
    internal sealed class ExclusiveWorkspacePresenter : IExclusiveWorkspacePresenter
    {
        /// <summary>
        /// 隐藏主窗体并模态显示工作区窗口，工作区结束后恢复主窗体。
        /// </summary>
        /// <param name="mainForm">需要暂时隐藏的主窗体。</param>
        /// <param name="workspaceForm">需要独占显示的工作区窗体。</param>
        /// <returns>工作区窗体的关闭结果。</returns>
        public DialogResult ShowDialog(Form mainForm, Form workspaceForm)
        {
            if (mainForm == null)
                throw new ArgumentNullException(nameof(mainForm));
            if (workspaceForm == null)
                throw new ArgumentNullException(nameof(workspaceForm));
            if (mainForm.IsDisposed || workspaceForm.IsDisposed)
                return DialogResult.Cancel;

            PrepareWorkspaceBounds(mainForm, workspaceForm);
            bool restoreMainForm = mainForm.Visible;

            try
            {
                mainForm.Hide();
                return workspaceForm.ShowDialog(mainForm);
            }
            finally
            {
                if (restoreMainForm && !mainForm.IsDisposed)
                {
                    mainForm.Show();
                    mainForm.Activate();
                    mainForm.BringToFront();
                }
            }
        }

        /// <summary>
        /// 将最大化工作区预定位到主窗体所在屏幕，避免恢复最大化时短暂露出主界面边缘。
        /// </summary>
        /// <param name="mainForm">主窗体。</param>
        /// <param name="workspaceForm">工作区窗体。</param>
        private static void PrepareWorkspaceBounds(Form mainForm, Form workspaceForm)
        {
            workspaceForm.ShowInTaskbar = true;
            if (workspaceForm.WindowState != FormWindowState.Maximized)
                return;

            workspaceForm.StartPosition = FormStartPosition.Manual;
            workspaceForm.WindowState = FormWindowState.Normal;
            workspaceForm.Bounds = mainForm.Bounds;
            workspaceForm.WindowState = FormWindowState.Maximized;
        }
    }
}
