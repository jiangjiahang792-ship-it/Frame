using System;
using System.Collections.Generic;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 采集保存任务完整图像租约字节数的可替换接口。
    /// </summary>
    public interface IImageResourceSizeSampler
    {
        /// <summary>获取滚动样本容量。</summary>
        int Capacity { get; }

        /// <summary>获取当前有效样本数量。</summary>
        int SampleCount { get; }

        /// <summary>获取当前滚动窗口的平均资源字节数。</summary>
        long AverageBytes { get; }

        /// <summary>
        /// 记录一次保存任务持有的完整图像资源字节数。
        /// </summary>
        /// <param name="estimatedBytes">完整图像租约的估算字节数。</param>
        void Record(long estimatedBytes);

        /// <summary>清空当前方案的全部图像资源样本。</summary>
        void Reset();
    }

    /// <summary>
    /// 线程安全的固定容量滚动图像资源采样器。
    /// </summary>
    public sealed class RollingImageResourceSizeSampler : IImageResourceSizeSampler
    {
        /// <summary>未设置机器级覆盖值时使用的滚动样本数量。</summary>
        public const int DefaultCapacity = 20;

        /// <summary>保护样本队列和累计值的一致性。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>按采集顺序保存最近的有效样本。</summary>
        private readonly Queue<long> _samples = new Queue<long>();

        /// <summary>滚动窗口允许保存的样本数量。</summary>
        private readonly int _capacity;

        /// <summary>使用decimal避免极端大图样本累计时发生长整型溢出。</summary>
        private decimal _sumBytes;

        /// <summary>获取滚动样本容量。</summary>
        public int Capacity => _capacity;

        /// <summary>获取当前有效样本数量。</summary>
        public int SampleCount
        {
            get
            {
                lock (_syncRoot)
                    return _samples.Count;
            }
        }

        /// <summary>获取当前滚动窗口的平均资源字节数。</summary>
        public long AverageBytes
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_samples.Count == 0)
                        return 0L;

                    decimal average = decimal.Floor(_sumBytes / _samples.Count);
                    return average >= long.MaxValue ? long.MaxValue : (long)Math.Max(1M, average);
                }
            }
        }

        /// <summary>
        /// 使用指定滚动窗口容量创建采样器。
        /// </summary>
        /// <param name="capacity">保留的最近样本数量。</param>
        public RollingImageResourceSizeSampler(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "图像资源采样数量必须大于零。");

            _capacity = capacity;
        }

        /// <summary>
        /// 记录有效样本并在达到容量后移除最早样本。
        /// </summary>
        /// <param name="estimatedBytes">完整图像租约的估算字节数。</param>
        public void Record(long estimatedBytes)
        {
            if (estimatedBytes <= 0)
                return;

            lock (_syncRoot)
            {
                _samples.Enqueue(estimatedBytes);
                _sumBytes += estimatedBytes;
                while (_samples.Count > _capacity)
                    _sumBytes -= _samples.Dequeue();
            }
        }

        /// <summary>清空当前滚动窗口。</summary>
        public void Reset()
        {
            lock (_syncRoot)
            {
                _samples.Clear();
                _sumBytes = 0M;
            }
        }
    }
}
