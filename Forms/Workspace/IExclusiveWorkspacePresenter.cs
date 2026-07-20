using System.Windows.Forms;

namespace TDJS_Vision.Forms.Workspace
{
    /// <summary>
    /// 定义独占工作区窗口的显示方式，用于统一隐藏和恢复主窗体。
    /// </summary>
    internal interface IExclusiveWorkspacePresenter
    {
        /// <summary>
        /// 隐藏主窗体并模态显示工作区窗口，工作区结束后恢复主窗体。
        /// </summary>
        /// <param name="mainForm">需要暂时隐藏的主窗体。</param>
        /// <param name="workspaceForm">需要独占显示的工作区窗体。</param>
        /// <returns>工作区窗体的关闭结果。</returns>
        DialogResult ShowDialog(Form mainForm, Form workspaceForm);
    }
}
