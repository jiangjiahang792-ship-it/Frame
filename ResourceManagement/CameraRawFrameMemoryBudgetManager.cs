using System;
using System.Threading;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>表示一台相机已经取得的原始帧非托管内存预算。</summary>
    public interface ICameraRawFrameMemoryLease : IDisposable
    {
        /// <summary>获取预算拥有者名称。</summary>
        string OwnerName { get; }

        /// <summary>获取允许分配的原始帧缓冲数量。</summary>
        int BufferCount { get; }

        /// <summary>获取本租约实际预留的字节数。</summary>
        long ReservedBytes { get; }
    }

    /// <summary>定义全方案相机原始帧非托管内存预算管理器。</summary>
    public interface ICameraRawFrameMemoryBudgetManager
    {
        /// <summary>获取当前活动租约采用的全局预算字节数。</summary>
        long CurrentBudgetBytes { get; }

        /// <summary>获取当前全部相机已经预留的字节数。</summary>
        long CurrentReservedBytes { get; }

        /// <summary>获取当前活动相机原始帧内存租约数量。</summary>
        int ActiveReservationCount { get; }

        /// <summary>为一台相机原子申请原始帧内存预算。</summary>
        /// <param name="ownerName">相机名称。</param>
        /// <param name="totalBudgetBytes">全方案允许使用的总字节数。</param>
        /// <param name="bytesPerBuffer">单块原始帧缓冲字节数。</param>
        /// <param name="desiredBufferCount">按配置相机数量计算的期望缓冲数。</param>
        /// <param name="minimumBufferCount">允许启动相机的最少缓冲数。</param>
        /// <returns>必须在相机停止时释放的内存预算租约。</returns>
        ICameraRawFrameMemoryLease Reserve(
            string ownerName,
            long totalBudgetBytes,
            int bytesPerBuffer,
            int desiredBufferCount,
            int minimumBufferCount);
    }

    /// <summary>使用锁内原子记账保证多台相机原始帧缓冲总量不突破活动预算。</summary>
    public sealed class CameraRawFrameMemoryBudgetManager : ICameraRawFrameMemoryBudgetManager
    {
        /// <summary>保护预算、已预留字节和活动租约数量。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>当前活动租约共同遵守的全局预算。</summary>
        private long _currentBudgetBytes;

        /// <summary>当前全部活动租约已经预留的字节数。</summary>
        private long _currentReservedBytes;

        /// <summary>当前尚未释放的租约数量。</summary>
        private int _activeReservationCount;

        /// <summary>获取当前活动租约采用的全局预算字节数。</summary>
        public long CurrentBudgetBytes
        {
            get
            {
                lock (_syncRoot)
                    return _currentBudgetBytes;
            }
        }

        /// <summary>获取当前全部相机已经预留的字节数。</summary>
        public long CurrentReservedBytes
        {
            get
            {
                lock (_syncRoot)
                    return _currentReservedBytes;
            }
        }

        /// <summary>获取当前活动相机原始帧内存租约数量。</summary>
        public int ActiveReservationCount
        {
            get
            {
                lock (_syncRoot)
                    return _activeReservationCount;
            }
        }

        /// <summary>为一台相机原子申请原始帧内存预算。</summary>
        public ICameraRawFrameMemoryLease Reserve(
            string ownerName,
            long totalBudgetBytes,
            int bytesPerBuffer,
            int desiredBufferCount,
            int minimumBufferCount)
        {
            if (totalBudgetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalBudgetBytes));
            if (bytesPerBuffer <= 0)
                throw new ArgumentOutOfRangeException(nameof(bytesPerBuffer));
            if (minimumBufferCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumBufferCount));
            if (desiredBufferCount < minimumBufferCount)
                throw new ArgumentOutOfRangeException(nameof(desiredBufferCount));

            lock (_syncRoot)
            {
                if (_activeReservationCount == 0)
                {
                    _currentBudgetBytes = totalBudgetBytes;
                }
                else if (totalBudgetBytes < _currentBudgetBytes)
                {
                    if (_currentReservedBytes > totalBudgetBytes)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "相机原始帧总预算由{0:F1}MB降低到{1:F1}MB，但现有相机已预留{2:F1}MB。请停止现有相机后重新启动。",
                                _currentBudgetBytes / 1024D / 1024D,
                                totalBudgetBytes / 1024D / 1024D,
                                _currentReservedBytes / 1024D / 1024D));
                    }

                    _currentBudgetBytes = totalBudgetBytes;
                }

                long availableBytes = _currentBudgetBytes - _currentReservedBytes;
                long availableBufferCount = availableBytes / bytesPerBuffer;
                int grantedBufferCount = (int)Math.Min(desiredBufferCount, Math.Min(int.MaxValue, availableBufferCount));
                if (grantedBufferCount < minimumBufferCount)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "相机{0}原始帧总预算不足：全局预算={1:F1}MB，已预留={2:F1}MB，单帧={3:F1}MB，至少还需{4}块。",
                            string.IsNullOrWhiteSpace(ownerName) ? "未命名" : ownerName,
                            _currentBudgetBytes / 1024D / 1024D,
                            _currentReservedBytes / 1024D / 1024D,
                            bytesPerBuffer / 1024D / 1024D,
                            minimumBufferCount));
                }

                long reservedBytes = checked((long)bytesPerBuffer * grantedBufferCount);
                _currentReservedBytes = checked(_currentReservedBytes + reservedBytes);
                _activeReservationCount++;
                return new CameraRawFrameMemoryLease(
                    this,
                    string.IsNullOrWhiteSpace(ownerName) ? "未命名" : ownerName,
                    grantedBufferCount,
                    reservedBytes);
            }
        }

        /// <summary>归还一个已经释放的相机原始帧内存租约。</summary>
        /// <param name="reservedBytes">归还字节数。</param>
        private void Release(long reservedBytes)
        {
            lock (_syncRoot)
            {
                _currentReservedBytes = Math.Max(0L, _currentReservedBytes - reservedBytes);
                _activeReservationCount = Math.Max(0, _activeReservationCount - 1);
                if (_activeReservationCount == 0)
                {
                    _currentReservedBytes = 0L;
                    _currentBudgetBytes = 0L;
                }
            }
        }

        /// <summary>默认的相机原始帧内存预算租约。</summary>
        private sealed class CameraRawFrameMemoryLease : ICameraRawFrameMemoryLease
        {
            /// <summary>创建租约的预算管理器。</summary>
            private CameraRawFrameMemoryBudgetManager _owner;

            /// <summary>创建一个原始帧内存预算租约。</summary>
            public CameraRawFrameMemoryLease(
                CameraRawFrameMemoryBudgetManager owner,
                string ownerName,
                int bufferCount,
                long reservedBytes)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                OwnerName = ownerName;
                BufferCount = bufferCount;
                ReservedBytes = reservedBytes;
            }

            /// <summary>获取预算拥有者名称。</summary>
            public string OwnerName { get; }

            /// <summary>获取允许分配的原始帧缓冲数量。</summary>
            public int BufferCount { get; }

            /// <summary>获取本租约实际预留的字节数。</summary>
            public long ReservedBytes { get; }

            /// <summary>幂等归还当前租约预留的全局内存预算。</summary>
            public void Dispose()
            {
                CameraRawFrameMemoryBudgetManager owner = Interlocked.Exchange(ref _owner, null);
                owner?.Release(ReservedBytes);
            }
        }
    }
}
