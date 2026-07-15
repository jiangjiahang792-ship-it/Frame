using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    /// <summary>
    /// 定义图像源等待单帧相机回调的可替换协作接口。
    /// </summary>
    public interface ICameraFrameAwaiter : IDisposable
    {
        /// <summary>
        /// 建立一次可取消的相机帧等待。
        /// </summary>
        /// <param name="cancellationToken">流程停止时使用的取消令牌。</param>
        /// <returns>收到当前批次回调帧后完成的任务。</returns>
        Task<Mat> BeginWaitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 尝试把回调帧交给当前等待者。
        /// </summary>
        /// <param name="frame">由当前订阅者独立接管生命周期的相机帧。</param>
        /// <returns>当前存在有效等待者并成功接收帧时返回 true。</returns>
        bool TrySupplyFrame(Mat frame);

        /// <summary>
        /// 取消当前尚未完成的相机帧等待。
        /// </summary>
        void CancelPendingWait();
    }

    /// <summary>
    /// 使用一次性任务完成源实现线程安全的单帧相机回调等待。
    /// </summary>
    public sealed class CameraFrameAwaiter : ICameraFrameAwaiter
    {
        /// <summary>
        /// 保护等待任务和取消注册状态的同步锁。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 当前唯一有效的帧等待任务完成源。
        /// </summary>
        private TaskCompletionSource<Mat> _pendingSource;

        /// <summary>
        /// 当前流程取消令牌的回调注册。
        /// </summary>
        private CancellationTokenRegistration _cancellationRegistration;

        /// <summary>
        /// 标记等待器是否已经释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 建立一次可取消的相机帧等待。
        /// </summary>
        /// <param name="cancellationToken">流程停止时使用的取消令牌。</param>
        /// <returns>收到当前批次回调帧后完成的任务。</returns>
        public Task<Mat> BeginWaitAsync(CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_pendingSource != null)
                    throw new InvalidOperationException("当前图像源已经在等待相机回调帧。");

                TaskCompletionSource<Mat> source =
                    new TaskCompletionSource<Mat>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingSource = source;

                CancellationTokenRegistration registration = cancellationToken.Register(CancelPendingWait);
                if (ReferenceEquals(_pendingSource, source))
                    _cancellationRegistration = registration;
                else
                    registration.Dispose();

                return source.Task;
            }
        }

        /// <summary>
        /// 尝试把回调帧交给当前等待者。
        /// </summary>
        /// <param name="frame">由当前订阅者独立接管生命周期的相机帧。</param>
        /// <returns>当前存在有效等待者并成功接收帧时返回 true。</returns>
        public bool TrySupplyFrame(Mat frame)
        {
            if (frame == null)
                return false;

            TaskCompletionSource<Mat> source;
            CancellationTokenRegistration registration;
            lock (_syncRoot)
            {
                if (_disposed || _pendingSource == null)
                    return false;

                source = _pendingSource;
                _pendingSource = null;
                registration = _cancellationRegistration;
                _cancellationRegistration = default(CancellationTokenRegistration);
            }

            registration.Dispose();
            return source.TrySetResult(frame);
        }

        /// <summary>
        /// 取消当前尚未完成的相机帧等待。
        /// </summary>
        public void CancelPendingWait()
        {
            TaskCompletionSource<Mat> source;
            CancellationTokenRegistration registration;
            lock (_syncRoot)
            {
                source = _pendingSource;
                _pendingSource = null;
                registration = _cancellationRegistration;
                _cancellationRegistration = default(CancellationTokenRegistration);
            }

            if (source != null)
                source.TrySetCanceled();
            registration.Dispose();
        }

        /// <summary>
        /// 取消尚未完成的等待并禁止后续复用。
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            CancelPendingWait();
        }

        /// <summary>
        /// 等待器释放后继续使用时抛出明确异常。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("CameraFrameAwaiter");
        }
    }
}
