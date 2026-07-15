using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.Startup;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 在独立 STA UI 线程中管理启动窗体，并负责跨线程进度和错误交互。
    /// </summary>
    internal sealed class StartupSplashController : IStartupProgressReporter, IDisposable
    {
        /// <summary>
        /// 启动窗体线程完成创建时发出信号。
        /// </summary>
        private readonly ManualResetEventSlim startupReady = new ManualResetEventSlim(false);

        /// <summary>
        /// 保护失败操作等待源的同步锁。
        /// </summary>
        private readonly object failureActionSync = new object();

        /// <summary>
        /// 承载启动窗体消息循环的线程。
        /// </summary>
        private Thread startupThread;

        /// <summary>
        /// 独立线程中创建的启动窗体。
        /// </summary>
        private FormStartup startupForm;

        /// <summary>
        /// 保存启动页线程在首次显示前发生的异常，以便回传主启动线程。
        /// </summary>
        private Exception startupThreadFailure;

        /// <summary>
        /// 标记启动页是否已经通过程序或系统操作关闭。
        /// </summary>
        private volatile bool startupClosed;

        /// <summary>
        /// 标记主线程已经请求动画线程退出，用于处理窗体尚未完成创建时的关闭竞争。
        /// </summary>
        private volatile bool closeRequested;

        /// <summary>
        /// 当前等待用户选择的失败操作结果源。
        /// </summary>
        private TaskCompletionSource<StartupFailureAction> failureActionSource;

        /// <summary>
        /// 标记控制器是否已经释放。
        /// </summary>
        private bool disposed;

        /// <summary>
        /// 创建并启动独立的启动页 UI 线程。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (startupThread != null)
                return;

            startupThread = new Thread(RunStartupThread)
            {
                IsBackground = true,
                Name = "TDJS-Vision 启动动画线程"
            };
            startupThread.SetApartmentState(ApartmentState.STA);
            startupThread.Start();

            if (!startupReady.Wait(TimeSpan.FromSeconds(10)))
            {
                Close();
                throw new TimeoutException("启动动画窗体创建超时。");
            }

            if (startupThreadFailure != null)
            {
                Close();
                throw new InvalidOperationException("启动动画窗体创建失败。", startupThreadFailure);
            }
        }

        /// <summary>
        /// 将最新启动进度安全投递到启动页线程。
        /// </summary>
        /// <param name="progress">当前启动进度。</param>
        public void Report(StartupProgressInfo progress)
        {
            PostToStartupForm(form => form.UpdateProgress(progress));
        }

        /// <summary>
        /// 显示关键失败并等待用户选择重试或退出软件。
        /// </summary>
        /// <param name="message">适合用户查看的错误信息。</param>
        /// <returns>用户选择的后续操作。</returns>
        public Task<StartupFailureAction> ShowFailureAsync(string message)
        {
            ThrowIfDisposed();
            var source = new TaskCompletionSource<StartupFailureAction>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (failureActionSync)
            {
                if (startupClosed)
                    return Task.FromResult(StartupFailureAction.Exit);

                failureActionSource?.TrySetResult(StartupFailureAction.Exit);
                failureActionSource = source;
            }

            PostToStartupForm(form => form.ShowFailure(message));
            return source.Task;
        }

        /// <summary>
        /// 关闭启动窗体并等待独立消息循环退出。
        /// </summary>
        public void Close()
        {
            closeRequested = true;
            if (startupForm != null && !startupForm.IsDisposed && startupForm.IsHandleCreated)
            {
                try
                {
                    startupForm.BeginInvoke(new Action(() => startupForm.Close()));
                }
                catch (InvalidOperationException)
                {
                    // 窗体句柄已经销毁时，无需重复关闭。
                }
            }

            if (startupThread != null && startupThread.IsAlive && Thread.CurrentThread != startupThread)
                startupThread.Join(TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// 释放线程同步对象和启动窗体。
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            Close();
            disposed = true;
            // 线程仍存活时保留同步对象，避免它稍后在 finally 中 Set 已释放的事件。
            if (startupThread == null || !startupThread.IsAlive)
                startupReady.Dispose();
        }

        /// <summary>
        /// 在独立线程中创建启动窗体并运行消息循环。
        /// </summary>
        private void RunStartupThread()
        {
            try
            {
                // 视觉样式与默认文本呈现方式已在 Program.Main 创建任何窗体前统一设置。
                // 此控制器还会用于主窗体创建后的手动方案加载，在线程内重复设置会触发 WinForms 异常。
                startupForm = new FormStartup();
                startupForm.Shown += StartupForm_Shown;
                startupForm.FormClosed += StartupForm_FormClosed;
                startupForm.RetryRequested += StartupForm_RetryRequested;
                startupForm.ExitRequested += StartupForm_ExitRequested;
                Application.Run(startupForm);
            }
            catch (Exception ex)
            {
                startupThreadFailure = ex;
            }
            finally
            {
                startupReady.Set();
            }
        }

        /// <summary>
        /// 在启动窗体完成首次显示并创建句柄后，允许主线程投递进度更新。
        /// </summary>
        /// <param name="sender">启动窗体。</param>
        /// <param name="e">事件参数。</param>
        private void StartupForm_Shown(object sender, EventArgs e)
        {
            startupReady.Set();
            if (closeRequested && startupForm != null && !startupForm.IsDisposed)
                startupForm.Close();
        }

        /// <summary>
        /// 启动页被程序、系统或快捷键关闭时，结束可能正在等待的失败选择。
        /// </summary>
        /// <param name="sender">启动窗体。</param>
        /// <param name="e">窗体关闭事件参数。</param>
        private void StartupForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            TaskCompletionSource<StartupFailureAction> source;
            lock (failureActionSync)
            {
                startupClosed = true;
                source = failureActionSource;
                failureActionSource = null;
            }

            source?.TrySetResult(StartupFailureAction.Exit);
        }

        /// <summary>
        /// 将指定操作投递到启动窗体线程。
        /// </summary>
        /// <param name="action">需要在启动窗体线程执行的操作。</param>
        private void PostToStartupForm(Action<FormStartup> action)
        {
            if (disposed || action == null || startupForm == null || startupForm.IsDisposed)
                return;

            try
            {
                startupForm.BeginInvoke(new Action(() => action(startupForm)));
            }
            catch (InvalidOperationException)
            {
                // 软件正在退出时忽略已经无法投递的界面更新。
            }
        }

        /// <summary>
        /// 完成“重试”选择并恢复启动动画。
        /// </summary>
        /// <param name="sender">启动窗体。</param>
        /// <param name="e">事件参数。</param>
        private void StartupForm_RetryRequested(object sender, EventArgs e)
        {
            CompleteFailureAction(StartupFailureAction.Retry);
            startupForm.ResetForRetry();
        }

        /// <summary>
        /// 完成“退出软件”选择。
        /// </summary>
        /// <param name="sender">启动窗体。</param>
        /// <param name="e">事件参数。</param>
        private void StartupForm_ExitRequested(object sender, EventArgs e)
        {
            CompleteFailureAction(StartupFailureAction.Exit);
        }

        /// <summary>
        /// 以指定结果完成当前失败操作等待源。
        /// </summary>
        /// <param name="action">用户选择的操作。</param>
        private void CompleteFailureAction(StartupFailureAction action)
        {
            TaskCompletionSource<StartupFailureAction> source;
            lock (failureActionSync)
            {
                source = failureActionSource;
                failureActionSource = null;
            }

            source?.TrySetResult(action);
        }

        /// <summary>
        /// 控制器释放后禁止继续使用。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StartupSplashController));
        }
    }
}
