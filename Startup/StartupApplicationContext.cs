using Logger;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 协调独立启动页线程、主窗体关键初始化和最终界面切换。
    /// </summary>
    internal sealed class StartupApplicationContext : ApplicationContext
    {
        /// <summary>
        /// 启动页要求的最短展示时间。
        /// </summary>
        private static readonly TimeSpan MinimumSplashDuration = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 启动时需要优先加载的命令行方案路径。
        /// </summary>
        private readonly string solutionPath;

        /// <summary>
        /// 独立线程启动页控制器。
        /// </summary>
        private readonly StartupSplashController splashController;

        /// <summary>
        /// 记录启动页实际展示时长。
        /// </summary>
        private readonly Stopwatch startupStopwatch;

        /// <summary>
        /// 尚未显示的主窗体实例。
        /// </summary>
        private FormMain mainForm;

        /// <summary>
        /// 标记启动流程是否已经开始，避免 Idle 事件重复进入。
        /// </summary>
        private bool initializationStarted;

        /// <summary>
        /// 标记启动上下文是否已经释放。
        /// </summary>
        private bool disposed;

        /// <summary>
        /// 初始化启动应用上下文并立即显示独立启动页。
        /// </summary>
        /// <param name="solutionPath">命令行指定的方案路径；没有指定时为空。</param>
        public StartupApplicationContext(string solutionPath)
        {
            this.solutionPath = solutionPath ?? string.Empty;
            splashController = new StartupSplashController();
            splashController.Start();
            startupStopwatch = Stopwatch.StartNew();
            splashController.Report(new StartupProgressInfo("正在启动软件……", "正在准备运行环境", 0, 0, 2));
            Application.Idle += Application_Idle;
        }

        /// <summary>
        /// 在主 UI 消息循环启动后执行可等待的初始化流程。
        /// </summary>
        /// <param name="sender">应用程序对象。</param>
        /// <param name="e">事件参数。</param>
        private void Application_Idle(object sender, EventArgs e)
        {
            if (initializationStarted)
                return;

            initializationStarted = true;
            Application.Idle -= Application_Idle;
            InitializeAsync();
        }

        /// <summary>
        /// 执行关键初始化，失败时停留在启动页等待重试或退出。
        /// </summary>
        private async void InitializeAsync()
        {
            while (!disposed)
            {
                try
                {
                    if (mainForm == null || mainForm.IsDisposed)
                    {
                        splashController.Report(new StartupProgressInfo("正在初始化主界面", "创建主窗口和工具窗口", 0, 0, 8));
                        mainForm = new FormMain(solutionPath);
                    }

                    using (StartupProgressContext.Begin(splashController))
                    {
                        await mainForm.InitializeForStartupAsync(splashController);
                        StartupProgressContext.ThrowIfFailures();
                    }

                    splashController.Report(new StartupProgressInfo("启动准备完成", "即将进入主页面", 0, 0, 100));
                    TimeSpan remaining = MinimumSplashDuration - startupStopwatch.Elapsed;
                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(remaining);

                    ShowMainForm();
                    return;
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"软件启动初始化失败：{ex}", true);
                    StartupFailureAction action = await splashController.ShowFailureAsync(CreateUserErrorMessage(ex));
                    if (action == StartupFailureAction.Exit)
                    {
                        ExitStartup();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 公开并显示已经完成关键初始化的主窗体，再关闭启动页并恢复前台焦点。
        /// </summary>
        private void ShowMainForm()
        {
            Program.MainForm = mainForm;
            MainForm = mainForm;
            mainForm.FormClosed += MainForm_FormClosed;
            mainForm.Show();
            splashController.Close();
            mainForm.Activate();
            mainForm.BringToFront();
        }

        /// <summary>
        /// 主窗体关闭后退出主 UI 消息循环。
        /// </summary>
        /// <param name="sender">主窗体。</param>
        /// <param name="e">事件参数。</param>
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            ExitThread();
        }

        /// <summary>
        /// 用户在失败界面选择退出时释放隐藏窗体并结束消息循环。
        /// </summary>
        private void ExitStartup()
        {
            splashController.Close();
            if (mainForm != null && !mainForm.IsDisposed)
                mainForm.Dispose();

            ExitThread();
        }

        /// <summary>
        /// 将异常转换成不暴露完整方案路径的用户错误信息。
        /// </summary>
        /// <param name="exception">原始启动异常。</param>
        /// <returns>适合启动页显示的简体中文错误。</returns>
        private string CreateUserErrorMessage(Exception exception)
        {
            string message = exception?.Message ?? "启动初始化失败。";
            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                string fileName = Path.GetFileName(solutionPath);
                message = message.Replace(solutionPath, fileName);
            }

            return $"软件启动初始化失败：{message}";
        }

        /// <summary>
        /// 释放启动页控制器和尚未公开的主窗体。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                base.Dispose(disposing);
                return;
            }

            disposed = true;
            Application.Idle -= Application_Idle;
            if (disposing)
            {
                splashController.Dispose();
                if (MainForm == null && mainForm != null && !mainForm.IsDisposed)
                    mainForm.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
