using System;
using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 单方案运行准入器，防止固定节拍触发重入同一批可编辑流程对象。
    /// </summary>
    public sealed class SolutionRunAdmissionController
    {
        /// <summary>保护准入状态、计数和耗时样本的一致性锁。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>保存最近完成轮次耗时的固定样本上限。</summary>
        private readonly int _durationSampleCapacity;

        /// <summary>最近完成轮次的耗时样本。</summary>
        private readonly Queue<long> _durationSamples = new Queue<long>();

        /// <summary>累计收到的触发请求数。</summary>
        private long _requestedCount;

        /// <summary>累计准入的触发请求数。</summary>
        private long _acceptedCount;

        /// <summary>方案忙碌时明确拒绝的触发请求数。</summary>
        private long _busyRejectedCount;

        /// <summary>正常返回的准入轮次数。</summary>
        private long _completedCount;

        /// <summary>出现未处理异常的准入轮次数。</summary>
        private long _failedCount;

        /// <summary>当前是否有一轮方案持有准入权。</summary>
        private bool _isActive;

        /// <summary>
        /// 使用固定耗时样本容量初始化准入器。
        /// </summary>
        /// <param name="durationSampleCapacity">用于分位数诊断的最近完成轮次数。</param>
        public SolutionRunAdmissionController(int durationSampleCapacity = 2048)
        {
            _durationSampleCapacity = Math.Max(1, durationSampleCapacity);
        }

        /// <summary>
        /// 尝试取得单方案运行权；方案忙碌时立即拒绝，不阻塞触发线程也不排无界队列。
        /// </summary>
        /// <param name="lease">准入成功时返回必须完成的运行租约。</param>
        /// <returns>取得运行权返回true；当前已有一轮运行返回false。</returns>
        public bool TryAcquire(out SolutionRunAdmissionLease lease)
        {
            lock (_syncRoot)
            {
                _requestedCount++;
                if (_isActive)
                {
                    _busyRejectedCount++;
                    lease = null;
                    return false;
                }

                _isActive = true;
                _acceptedCount++;
                lease = new SolutionRunAdmissionLease(this);
                return true;
            }
        }

        /// <summary>
        /// 在没有活动轮次时清空诊断计数，供正式压力前排除预热数据。
        /// </summary>
        /// <returns>成功清空返回true；仍有活动轮次返回false。</returns>
        public bool TryReset()
        {
            lock (_syncRoot)
            {
                if (_isActive)
                    return false;

                _requestedCount = 0;
                _acceptedCount = 0;
                _busyRejectedCount = 0;
                _completedCount = 0;
                _failedCount = 0;
                _durationSamples.Clear();
                return true;
            }
        }

        /// <summary>
        /// 获取当前触发准入计数和已完成轮次耗时分位数。
        /// </summary>
        /// <returns>线程安全的不可变快照。</returns>
        public SolutionRunAdmissionSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                long[] sortedDurations = _durationSamples.OrderBy(value => value).ToArray();
                return new SolutionRunAdmissionSnapshot
                {
                    RequestedCount = _requestedCount,
                    AcceptedCount = _acceptedCount,
                    BusyRejectedCount = _busyRejectedCount,
                    CompletedCount = _completedCount,
                    FailedCount = _failedCount,
                    IsActive = _isActive,
                    DurationSampleCount = sortedDurations.Length,
                    P50Milliseconds = GetPercentile(sortedDurations, 0.50D),
                    P95Milliseconds = GetPercentile(sortedDurations, 0.95D),
                    P99Milliseconds = GetPercentile(sortedDurations, 0.99D),
                    MaximumMilliseconds = sortedDurations.Length == 0
                        ? 0L
                        : sortedDurations[sortedDurations.Length - 1]
                };
            }
        }

        /// <summary>
        /// 完成当前运行租约并记录结果与墙钟耗时。
        /// </summary>
        /// <param name="succeeded">本轮是否正常返回。</param>
        /// <param name="elapsedMilliseconds">本轮墙钟耗时。</param>
        internal void Complete(bool succeeded, long elapsedMilliseconds)
        {
            lock (_syncRoot)
            {
                if (!_isActive)
                    return;

                _isActive = false;
                if (succeeded)
                    _completedCount++;
                else
                    _failedCount++;

                _durationSamples.Enqueue(Math.Max(0L, elapsedMilliseconds));
                while (_durationSamples.Count > _durationSampleCapacity)
                    _durationSamples.Dequeue();
            }
        }

        /// <summary>
        /// 从已排序耗时样本中取得最近秩分位值。
        /// </summary>
        /// <param name="sortedValues">升序耗时样本。</param>
        /// <param name="percentile">0到1之间的目标分位。</param>
        /// <returns>没有样本时返回0。</returns>
        private static long GetPercentile(long[] sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0L;

            int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
            return sortedValues[index];
        }
    }

    /// <summary>
    /// 单次准入运行的完成租约，确保异常路径也会释放下一轮运行权。
    /// </summary>
    public sealed class SolutionRunAdmissionLease : IDisposable
    {
        /// <summary>持有本租约的准入器；完成后置空防止重复提交。</summary>
        private SolutionRunAdmissionController _owner;

        /// <summary>租约取得时间。</summary>
        private readonly long _startedTimestamp;

        /// <summary>
        /// 创建一份活动运行租约。
        /// </summary>
        /// <param name="owner">持有运行权的准入器。</param>
        internal SolutionRunAdmissionLease(SolutionRunAdmissionController owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _startedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// 按实际执行结果完成租约并释放下一轮运行权。
        /// </summary>
        /// <param name="succeeded">本轮是否正常返回。</param>
        public void Complete(bool succeeded)
        {
            SolutionRunAdmissionController owner = System.Threading.Interlocked.Exchange(ref _owner, null);
            if (owner == null)
                return;

            long elapsedTicks = Math.Max(0L, System.Diagnostics.Stopwatch.GetTimestamp() - _startedTimestamp);
            long elapsedMilliseconds = (long)Math.Ceiling(
                elapsedTicks * 1000D / System.Diagnostics.Stopwatch.Frequency);
            owner.Complete(succeeded, elapsedMilliseconds);
        }

        /// <summary>
        /// 未显式提交结果时按失败完成，避免异常路径永久占用运行权。
        /// </summary>
        public void Dispose()
        {
            Complete(false);
        }
    }

    /// <summary>
    /// 固定节拍触发准入与完整方案墙钟耗时快照。
    /// </summary>
    public sealed class SolutionRunAdmissionSnapshot
    {
        /// <summary>累计触发请求数。</summary>
        public long RequestedCount { get; set; }

        /// <summary>累计准入数。</summary>
        public long AcceptedCount { get; set; }

        /// <summary>方案忙碌拒绝数。</summary>
        public long BusyRejectedCount { get; set; }

        /// <summary>正常完成数。</summary>
        public long CompletedCount { get; set; }

        /// <summary>异常完成数。</summary>
        public long FailedCount { get; set; }

        /// <summary>当前是否仍有活动轮次。</summary>
        public bool IsActive { get; set; }

        /// <summary>参与耗时分位统计的完成样本数。</summary>
        public int DurationSampleCount { get; set; }

        /// <summary>完整方案墙钟耗时P50。</summary>
        public long P50Milliseconds { get; set; }

        /// <summary>完整方案墙钟耗时P95。</summary>
        public long P95Milliseconds { get; set; }

        /// <summary>完整方案墙钟耗时P99。</summary>
        public long P99Milliseconds { get; set; }

        /// <summary>完整方案墙钟最大耗时。</summary>
        public long MaximumMilliseconds { get; set; }

        /// <summary>
        /// 生成固定触发测试和运行日志共用的简体中文摘要。
        /// </summary>
        /// <returns>准入、完成和耗时数据。</returns>
        public string ToLogText()
        {
            return $"请求={RequestedCount}；准入={AcceptedCount}；忙碌拒绝={BusyRejectedCount}；完成={CompletedCount}；异常={FailedCount}；活动={IsActive}；耗时样本={DurationSampleCount}；P50={P50Milliseconds}ms；P95={P95Milliseconds}ms；P99={P99Milliseconds}ms；最大={MaximumMilliseconds}ms";
        }
    }
}
