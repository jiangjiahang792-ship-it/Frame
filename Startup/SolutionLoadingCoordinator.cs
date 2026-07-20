using Logger;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 在独立启动页线程显示动画，同时在调用方主 UI 线程同步恢复方案数据和控件。
    /// </summary>
    internal sealed class SolutionLoadingCoordinator : ISolutionLoadingCoordinator
    {
        /// <summary>
        /// 手动打开方案时动画最短展示时间。
        /// </summary>
        private static readonly TimeSpan MinimumSplashDuration = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 隐藏主窗体、显示加载动画，并在当前主 UI 线程完成方案恢复。
        /// </summary>
        /// <param name="mainForm">加载期间需要隐藏的主窗体。</param>
        /// <param name="solutionPath">需要打开的方案文件。</param>
        /// <param name="showInfo">是否输出详细设备和流程加载日志。</param>
        public void Load(Form mainForm, string solutionPath, bool showInfo)
        {
            if (mainForm == null)
                throw new ArgumentNullException(nameof(mainForm));
            if (string.IsNullOrWhiteSpace(solutionPath))
                throw new ArgumentException("方案文件路径不能为空。", nameof(solutionPath));
            if (!File.Exists(solutionPath))
                throw new FileNotFoundException("选择的方案文件不存在。", solutionPath);

            using (var splashController = new StartupSplashController())
            {
                bool restoreMainForm = mainForm.Visible;
                splashController.Start();
                Stopwatch stopwatch = Stopwatch.StartNew();

                try
                {
                    splashController.Report(new StartupProgressInfo(
                        "正在打开方案",
                        Path.GetFileName(solutionPath),
                        0,
                        0,
                        5));
                    mainForm.Hide();

                    using (StartupProgressContext.Begin(splashController))
                    {
                        ConfigHelper.SolLoad(solutionPath, showInfo);
                        StartupProgressContext.ThrowIfFailures();
                    }

                    splashController.Report(new StartupProgressInfo(
                        "方案加载完成",
                        "正在返回主页面",
                        0,
                        0,
                        100));
                }
                finally
                {
                    WaitForMinimumSplashDuration(stopwatch);
                    RestoreMainForm(mainForm, splashController, restoreMainForm);
                }
            }
        }

        /// <summary>
        /// 成功或失败时都补足动画最短展示时间，避免快速加载或快速失败造成界面闪烁。
        /// </summary>
        /// <param name="stopwatch">从动画真正显示后开始计时的计时器。</param>
        private static void WaitForMinimumSplashDuration(Stopwatch stopwatch)
        {
            TimeSpan remaining = MinimumSplashDuration - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return;

            try
            {
                Thread.Sleep(remaining);
            }
            catch (ThreadInterruptedException ex)
            {
                LogCleanupFailure("等待动画最短展示时间被中断", ex);
            }
        }

        /// <summary>
        /// 先重新显示主窗体，再关闭置顶动画并恢复主窗体前台焦点。
        /// </summary>
        /// <param name="mainForm">需要恢复的主窗体。</param>
        /// <param name="splashController">需要关闭的动画控制器。</param>
        /// <param name="restoreMainForm">加载前主窗体是否可见。</param>
        private static void RestoreMainForm(
            Form mainForm,
            StartupSplashController splashController,
            bool restoreMainForm)
        {
            try
            {
                if (restoreMainForm && !mainForm.IsDisposed)
                    mainForm.Show();
            }
            catch (Exception ex)
            {
                LogCleanupFailure("主窗体显示失败", ex);
            }

            try
            {
                splashController.Close();
            }
            catch (Exception ex)
            {
                LogCleanupFailure("动画关闭失败", ex);
            }
            finally
            {
                try
                {
                    if (restoreMainForm && !mainForm.IsDisposed)
                    {
                        mainForm.Activate();
                        mainForm.BringToFront();
                    }
                }
                catch (Exception ex)
                {
                    LogCleanupFailure("主窗体激活失败", ex);
                }
            }
        }

        /// <summary>
        /// 安全记录方案动画收尾异常，日志组件异常时也不覆盖原始方案加载异常。
        /// </summary>
        /// <param name="stageName">发生异常的收尾阶段。</param>
        /// <param name="exception">需要记录的原始异常。</param>
        private static void LogCleanupFailure(string stageName, Exception exception)
        {
            try
            {
                LogHelper.AddLog(MsgLevel.Exception, $"手动打开方案收尾失败（{stageName}）：{exception}", true);
            }
            catch
            {
                // 收尾日志失败时保持原始异常不变，避免再次阻断主窗体恢复流程。
            }
        }
    }
}
