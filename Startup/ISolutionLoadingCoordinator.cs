using System.Windows.Forms;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 定义带独立动画和真实进度的方案加载操作。
    /// </summary>
    internal interface ISolutionLoadingCoordinator
    {
        /// <summary>
        /// 隐藏主窗体、显示加载动画，并在当前主 UI 线程完成方案恢复。
        /// </summary>
        /// <param name="mainForm">加载期间需要隐藏的主窗体。</param>
        /// <param name="solutionPath">需要打开的方案文件。</param>
        /// <param name="showInfo">是否输出详细设备和流程加载日志。</param>
        void Load(Form mainForm, string solutionPath, bool showInfo);
    }
}
