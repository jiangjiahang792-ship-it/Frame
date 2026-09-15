using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 由CPU调度器统一管理的工作类型；AI推理不属于本枚举并继续跟随可编辑流程。
    /// </summary>
    public enum CpuWorkloadKind
    {
        /// <summary>OpenCV、测量和其他传统CPU视觉算法。</summary>
        TraditionalVisionAlgorithm = 0,

        /// <summary>Mat、Bitmap和相机像素格式之间的图像转换。</summary>
        ImageConversion = 1,

        /// <summary>JPEG等图片编码操作。</summary>
        ImageEncoding = 2
    }

    /// <summary>
    /// CPU重任务调度参数；后续系统设置界面只需生成本机覆盖值。
    /// </summary>
    public sealed class CpuWorkSchedulerOptions
    {
        /// <summary>是否允许根据全机CPU采样动态下降和恢复并发。</summary>
        public bool DynamicAdjustmentEnabled { get; set; } = true;

        /// <summary>CPU重任务自动最大并发。</summary>
        public int MaximumConcurrency { get; set; } = 1;

        /// <summary>动态调节允许下降到的最小并发。</summary>
        public int MinimumConcurrency { get; set; } = 1;

        /// <summary>图像转换在全局CPU并发中的分类上限。</summary>
        public int ImageConversionMaximumConcurrency { get; set; } = 1;

        /// <summary>触发并发下降的CPU高水位百分比。</summary>
        public double HighCpuPercent { get; set; } = 85D;

        /// <summary>允许并发恢复的CPU低水位百分比。</summary>
        public double RecoveryCpuPercent { get; set; } = 65D;

        /// <summary>高水位必须持续的毫秒数。</summary>
        public int HighCpuSustainMilliseconds { get; set; } = 5000;

        /// <summary>低水位必须持续的毫秒数。</summary>
        public int RecoveryCpuSustainMilliseconds { get; set; } = 10000;

        /// <summary>CPU采样和资源策略评估周期，单位毫秒。</summary>
        public int AdjustmentIntervalMilliseconds { get; set; } = 5000;

        /// <summary>两次调整之间的冷却时间，单位毫秒。</summary>
        public int AdjustmentCooldownMilliseconds { get; set; } = 30000;

        /// <summary>每次调整的并发许可数量。</summary>
        public int AdjustmentStep { get; set; } = 1;

        /// <summary>排队耗时滚动样本容量。</summary>
        public int WaitSampleCapacity { get; set; } = 2048;

        /// <summary>
        /// 从自动硬件资源档案创建CPU调度参数。
        /// </summary>
        /// <param name="profile">当前自动资源档案。</param>
        /// <returns>带保守降级值的CPU调度参数。</returns>
        public static CpuWorkSchedulerOptions FromProfile(AutomaticResourceProfile profile)
        {
            if (profile == null)
                return new CpuWorkSchedulerOptions();

            return new CpuWorkSchedulerOptions
            {
                DynamicAdjustmentEnabled = profile.AdaptiveResourceManagementEnabled,
                MaximumConcurrency = Math.Max(1, profile.CpuHeavyMaxConcurrency),
                MinimumConcurrency = 1,
                ImageConversionMaximumConcurrency = Math.Max(1, profile.ImageConvertMaxConcurrency),
                HighCpuPercent = profile.CpuHighWatermarkPercent,
                RecoveryCpuPercent = profile.CpuRecoveryWatermarkPercent,
                HighCpuSustainMilliseconds = profile.CpuHighDurationMs,
                RecoveryCpuSustainMilliseconds = profile.CpuRecoveryDurationMs,
                AdjustmentIntervalMilliseconds = profile.ResourceAdjustmentIntervalMs,
                AdjustmentCooldownMilliseconds = profile.ResourceAdjustmentCooldownMs,
                AdjustmentStep = profile.ConcurrencyAdjustmentStep
            };
        }

        /// <summary>
        /// 创建边界安全的参数副本，避免调用方后续修改正在运行的调度器。
        /// </summary>
        /// <returns>规范化参数副本。</returns>
        internal CpuWorkSchedulerOptions Normalize()
        {
            int maximumConcurrency = Clamp(MaximumConcurrency, 1, 32);
            int minimumConcurrency = Clamp(MinimumConcurrency, 1, maximumConcurrency);
            double highCpuPercent = Clamp(HighCpuPercent, 50D, 100D);
            double recoveryCpuPercent = Clamp(RecoveryCpuPercent, 20D, Math.Max(20D, highCpuPercent - 5D));
            return new CpuWorkSchedulerOptions
            {
                DynamicAdjustmentEnabled = DynamicAdjustmentEnabled,
                MaximumConcurrency = maximumConcurrency,
                MinimumConcurrency = minimumConcurrency,
                ImageConversionMaximumConcurrency = Clamp(ImageConversionMaximumConcurrency, 1, maximumConcurrency),
                HighCpuPercent = highCpuPercent,
                RecoveryCpuPercent = recoveryCpuPercent,
                HighCpuSustainMilliseconds = Clamp(HighCpuSustainMilliseconds, 1000, 60000),
                RecoveryCpuSustainMilliseconds = Clamp(RecoveryCpuSustainMilliseconds, 1000, 300000),
                AdjustmentIntervalMilliseconds = Clamp(AdjustmentIntervalMilliseconds, 1000, 60000),
                AdjustmentCooldownMilliseconds = Clamp(AdjustmentCooldownMilliseconds, 5000, 600000),
                AdjustmentStep = Clamp(AdjustmentStep, 1, 4),
                WaitSampleCapacity = Clamp(WaitSampleCapacity, 32, 20000)
            };
        }

        /// <summary>把整数限制在指定闭区间。</summary>
        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        /// <summary>把浮点数限制在指定闭区间。</summary>
        private static double Clamp(double value, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    /// <summary>
    /// CPU高低水位动态并发策略；只计算目标额度，不直接管理线程或节点。
    /// </summary>
    public sealed class CpuConcurrencyAdjustmentPolicy
    {
        /// <summary>规范化策略参数。</summary>
        private readonly CpuWorkSchedulerOptions _options;

        /// <summary>CPU高水位开始时间；未处于高水位时为-1。</summary>
        private long _highCpuStartedAt = -1L;

        /// <summary>CPU恢复水位开始时间；未处于恢复水位时为-1。</summary>
        private long _recoveryCpuStartedAt = -1L;

        /// <summary>最近一次并发调整时间；尚未调整时为-1。</summary>
        private long _lastAdjustmentAt = -1L;

        /// <summary>最近一次采样时间，用于抵御倒退时间戳。</summary>
        private long _lastObservedAt;

        /// <summary>当前动态并发额度。</summary>
        public int CurrentConcurrency { get; private set; }

        /// <summary>最近一次归一化CPU采样。</summary>
        public double LastCpuPercent { get; private set; }

        /// <summary>
        /// 创建动态并发策略。
        /// </summary>
        /// <param name="options">CPU调度参数。</param>
        /// <param name="initialConcurrency">初始动态并发。</param>
        public CpuConcurrencyAdjustmentPolicy(CpuWorkSchedulerOptions options, int initialConcurrency)
        {
            _options = (options ?? new CpuWorkSchedulerOptions()).Normalize();
            CurrentConcurrency = Math.Max(
                _options.MinimumConcurrency,
                Math.Min(_options.MaximumConcurrency, initialConcurrency));
        }

        /// <summary>
        /// 输入一次CPU采样并计算当前目标并发。
        /// </summary>
        /// <param name="cpuPercent">按全机逻辑线程归一化的CPU使用率。</param>
        /// <param name="timestampMilliseconds">单调递增时间戳，单位毫秒。</param>
        /// <returns>本次采样后的目标并发。</returns>
        public int Observe(double cpuPercent, long timestampMilliseconds)
        {
            long observedAt = Math.Max(_lastObservedAt, Math.Max(0L, timestampMilliseconds));
            _lastObservedAt = observedAt;
            LastCpuPercent = Math.Max(0D, Math.Min(100D, double.IsNaN(cpuPercent) ? 0D : cpuPercent));

            if (!_options.DynamicAdjustmentEnabled)
            {
                _highCpuStartedAt = -1L;
                _recoveryCpuStartedAt = -1L;
                return CurrentConcurrency;
            }

            if (LastCpuPercent >= _options.HighCpuPercent)
            {
                _recoveryCpuStartedAt = -1L;
                if (_highCpuStartedAt < 0L)
                    _highCpuStartedAt = observedAt;

                if (HasSustained(_highCpuStartedAt, observedAt, _options.HighCpuSustainMilliseconds) &&
                    IsCooldownComplete(observedAt) &&
                    CurrentConcurrency > _options.MinimumConcurrency)
                {
                    CurrentConcurrency = Math.Max(
                        _options.MinimumConcurrency,
                        CurrentConcurrency - _options.AdjustmentStep);
                    _lastAdjustmentAt = observedAt;
                    _highCpuStartedAt = observedAt;
                }
                return CurrentConcurrency;
            }

            if (LastCpuPercent <= _options.RecoveryCpuPercent)
            {
                _highCpuStartedAt = -1L;
                if (_recoveryCpuStartedAt < 0L)
                    _recoveryCpuStartedAt = observedAt;

                if (HasSustained(_recoveryCpuStartedAt, observedAt, _options.RecoveryCpuSustainMilliseconds) &&
                    IsCooldownComplete(observedAt) &&
                    CurrentConcurrency < _options.MaximumConcurrency)
                {
                    CurrentConcurrency = Math.Min(
                        _options.MaximumConcurrency,
                        CurrentConcurrency + _options.AdjustmentStep);
                    _lastAdjustmentAt = observedAt;
                    _recoveryCpuStartedAt = observedAt;
                }
                return CurrentConcurrency;
            }

            _highCpuStartedAt = -1L;
            _recoveryCpuStartedAt = -1L;
            return CurrentConcurrency;
        }

        /// <summary>判断指定水位是否达到持续时间。</summary>
        private static bool HasSustained(long startedAt, long observedAt, int durationMilliseconds)
        {
            return startedAt >= 0L && observedAt - startedAt >= durationMilliseconds;
        }

        /// <summary>判断本次动态调整是否已经经过冷却时间。</summary>
        private bool IsCooldownComplete(long observedAt)
        {
            return _lastAdjustmentAt < 0L ||
                observedAt - _lastAdjustmentAt >= _options.AdjustmentCooldownMilliseconds;
        }
    }

    /// <summary>
    /// CPU调度器只读诊断快照。
    /// </summary>
    public sealed class CpuWorkSchedulerSnapshot
    {
        /// <summary>硬件档案建议最大并发。</summary>
        public int MaximumConcurrency { get; set; }

        /// <summary>动态策略当前并发。</summary>
        public int CurrentConcurrency { get; set; }

        /// <summary>图像转换分类并发上限。</summary>
        public int ImageConversionMaximumConcurrency { get; set; }

        /// <summary>当前全部CPU重任务数量。</summary>
        public int ActiveCount { get; set; }

        /// <summary>当前图像转换任务数量。</summary>
        public int ActiveImageConversionCount { get; set; }

        /// <summary>当前排队任务数量。</summary>
        public int WaitingCount { get; set; }

        /// <summary>历史全部CPU重任务峰值。</summary>
        public int PeakActiveCount { get; set; }

        /// <summary>历史图像转换任务峰值。</summary>
        public int PeakImageConversionCount { get; set; }

        /// <summary>历史排队任务峰值。</summary>
        public int PeakWaitingCount { get; set; }

        /// <summary>累计取得许可次数。</summary>
        public long GrantedCount { get; set; }

        /// <summary>累计正常归还许可次数。</summary>
        public long CompletedCount { get; set; }

        /// <summary>累计取消等待次数。</summary>
        public long CanceledWaitCount { get; set; }

        /// <summary>动态并发累计下降次数。</summary>
        public long ConcurrencyDecreaseCount { get; set; }

        /// <summary>动态并发累计恢复次数。</summary>
        public long ConcurrencyIncreaseCount { get; set; }

        /// <summary>最近一次归一化CPU采样。</summary>
        public double LastCpuPercent { get; set; }

        /// <summary>许可等待P50，单位毫秒。</summary>
        public double WaitP50Milliseconds { get; set; }

        /// <summary>许可等待P95，单位毫秒。</summary>
        public double WaitP95Milliseconds { get; set; }

        /// <summary>许可等待P99，单位毫秒。</summary>
        public double WaitP99Milliseconds { get; set; }

        /// <summary>许可等待最大值，单位毫秒。</summary>
        public double WaitMaximumMilliseconds { get; set; }

        /// <summary>调度器是否已经停止接收新任务。</summary>
        public bool IsDisposed { get; set; }

        /// <summary>
        /// 生成现场Debug日志摘要。
        /// </summary>
        /// <returns>CPU调度诊断摘要。</returns>
        public string ToLogText()
        {
            return $"并发={CurrentConcurrency}/{MaximumConcurrency}；图像转换={ActiveImageConversionCount}/{ImageConversionMaximumConcurrency}；活动={ActiveCount}；等待={WaitingCount}；峰值活动/转换/等待={PeakActiveCount}/{PeakImageConversionCount}/{PeakWaitingCount}；许可/完成/取消={GrantedCount}/{CompletedCount}/{CanceledWaitCount}；降级/恢复={ConcurrencyDecreaseCount}/{ConcurrencyIncreaseCount}；CPU={LastCpuPercent:F1}%；等待P50/P95/P99/最大={WaitP50Milliseconds:F3}/{WaitP95Milliseconds:F3}/{WaitP99Milliseconds:F3}/{WaitMaximumMilliseconds:F3}ms；已停止={IsDisposed}";
        }
    }

    /// <summary>
    /// 全方案共享的CPU重任务调度器；采用一个全局额度和图像转换分类额度，避免额度相加失控。
    /// </summary>
    public sealed class CpuWorkScheduler : ICpuWorkScheduler
    {
        /// <summary>保护活动数、等待队列、策略和诊断计数。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>等待许可的任务链表，允许取消时常数时间移除。</summary>
        private readonly LinkedList<CpuWorkWaiter> _waiters = new LinkedList<CpuWorkWaiter>();

        /// <summary>许可等待耗时滚动样本。</summary>
        private readonly Queue<double> _waitSamples = new Queue<double>();

        /// <summary>当前规范化参数。</summary>
        private CpuWorkSchedulerOptions _options;

        /// <summary>当前CPU高低水位动态策略。</summary>
        private CpuConcurrencyAdjustmentPolicy _adjustmentPolicy;

        /// <summary>当前全部CPU重任务数量。</summary>
        private int _activeCount;

        /// <summary>当前图像转换任务数量。</summary>
        private int _activeImageConversionCount;

        /// <summary>历史全部CPU重任务峰值。</summary>
        private int _peakActiveCount;

        /// <summary>历史图像转换任务峰值。</summary>
        private int _peakImageConversionCount;

        /// <summary>历史等待任务峰值。</summary>
        private int _peakWaitingCount;

        /// <summary>累计取得许可次数。</summary>
        private long _grantedCount;

        /// <summary>累计归还许可次数。</summary>
        private long _completedCount;

        /// <summary>累计取消等待次数。</summary>
        private long _canceledWaitCount;

        /// <summary>动态并发累计下降次数。</summary>
        private long _concurrencyDecreaseCount;

        /// <summary>动态并发累计恢复次数。</summary>
        private long _concurrencyIncreaseCount;

        /// <summary>调度器已经停止接收新任务。</summary>
        private bool _disposed;

        /// <summary>
        /// 创建CPU重任务调度器。
        /// </summary>
        /// <param name="options">CPU调度参数。</param>
        public CpuWorkScheduler(CpuWorkSchedulerOptions options)
        {
            _options = (options ?? new CpuWorkSchedulerOptions()).Normalize();
            _adjustmentPolicy = new CpuConcurrencyAdjustmentPolicy(_options, _options.MaximumConcurrency);
        }

        /// <summary>
        /// 异步取得一项CPU重任务许可。
        /// </summary>
        public Task<ICpuWorkLease> AcquireAsync(CpuWorkloadKind workloadKind, CancellationToken cancellationToken)
        {
            ValidateWorkloadKind(workloadKind);
            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_waiters.Count == 0 && CanGrant(workloadKind))
                {
                    CpuWorkLease immediateLease = GrantLease(workloadKind, 0D);
                    return Task.FromResult<ICpuWorkLease>(immediateLease);
                }

                CpuWorkWaiter waiter = new CpuWorkWaiter(this, workloadKind, Stopwatch.GetTimestamp());
                waiter.Node = _waiters.AddLast(waiter);
                _peakWaitingCount = Math.Max(_peakWaitingCount, _waiters.Count);
                waiter.CancellationRegistration = cancellationToken.Register(waiter.Cancel);
                if (Volatile.Read(ref waiter.State) != CpuWorkWaiter.WaitingState)
                    waiter.CancellationRegistration.Dispose();
                return waiter.Completion.Task;
            }
        }

        /// <summary>
        /// 输入CPU采样并应用动态并发变化。
        /// </summary>
        public void ObserveCpuSample(double cpuPercent, long timestampMilliseconds)
        {
            List<CpuWorkWaiter> grantedWaiters = null;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                int previousConcurrency = _adjustmentPolicy.CurrentConcurrency;
                int currentConcurrency = _adjustmentPolicy.Observe(cpuPercent, timestampMilliseconds);
                if (currentConcurrency < previousConcurrency)
                    _concurrencyDecreaseCount++;
                else if (currentConcurrency > previousConcurrency)
                    _concurrencyIncreaseCount++;

                if (currentConcurrency > previousConcurrency)
                    grantedWaiters = GrantPendingWaiters();
            }
            CompleteGrantedWaiters(grantedWaiters);
        }

        /// <summary>
        /// 重新配置调度器；活动任务超过新额度时允许自然结束，不强制取消。
        /// </summary>
        public void Reconfigure(CpuWorkSchedulerOptions options)
        {
            CpuWorkSchedulerOptions normalizedOptions = (options ?? new CpuWorkSchedulerOptions()).Normalize();
            List<CpuWorkWaiter> grantedWaiters;
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (OptionsEqual(_options, normalizedOptions))
                    return;
                _options = normalizedOptions;
                _adjustmentPolicy = new CpuConcurrencyAdjustmentPolicy(_options, _options.MaximumConcurrency);
                while (_waitSamples.Count > _options.WaitSampleCapacity)
                    _waitSamples.Dequeue();
                grantedWaiters = GrantPendingWaiters();
            }
            CompleteGrantedWaiters(grantedWaiters);
        }

        /// <summary>
        /// 判断两份规范化参数是否完全一致，避免每轮流程刷新硬件档案时重置动态额度。
        /// </summary>
        private static bool OptionsEqual(CpuWorkSchedulerOptions left, CpuWorkSchedulerOptions right)
        {
            return left.DynamicAdjustmentEnabled == right.DynamicAdjustmentEnabled &&
                left.MaximumConcurrency == right.MaximumConcurrency &&
                left.MinimumConcurrency == right.MinimumConcurrency &&
                left.ImageConversionMaximumConcurrency == right.ImageConversionMaximumConcurrency &&
                left.HighCpuPercent.Equals(right.HighCpuPercent) &&
                left.RecoveryCpuPercent.Equals(right.RecoveryCpuPercent) &&
                left.HighCpuSustainMilliseconds == right.HighCpuSustainMilliseconds &&
                left.RecoveryCpuSustainMilliseconds == right.RecoveryCpuSustainMilliseconds &&
                left.AdjustmentIntervalMilliseconds == right.AdjustmentIntervalMilliseconds &&
                left.AdjustmentCooldownMilliseconds == right.AdjustmentCooldownMilliseconds &&
                left.AdjustmentStep == right.AdjustmentStep &&
                left.WaitSampleCapacity == right.WaitSampleCapacity;
        }

        /// <summary>
        /// 读取调度器只读诊断快照。
        /// </summary>
        public CpuWorkSchedulerSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                double[] waitSamples = _waitSamples.OrderBy(value => value).ToArray();
                return new CpuWorkSchedulerSnapshot
                {
                    MaximumConcurrency = _options.MaximumConcurrency,
                    CurrentConcurrency = _adjustmentPolicy.CurrentConcurrency,
                    ImageConversionMaximumConcurrency = _options.ImageConversionMaximumConcurrency,
                    ActiveCount = _activeCount,
                    ActiveImageConversionCount = _activeImageConversionCount,
                    WaitingCount = _waiters.Count,
                    PeakActiveCount = _peakActiveCount,
                    PeakImageConversionCount = _peakImageConversionCount,
                    PeakWaitingCount = _peakWaitingCount,
                    GrantedCount = _grantedCount,
                    CompletedCount = _completedCount,
                    CanceledWaitCount = _canceledWaitCount,
                    ConcurrencyDecreaseCount = _concurrencyDecreaseCount,
                    ConcurrencyIncreaseCount = _concurrencyIncreaseCount,
                    LastCpuPercent = _adjustmentPolicy.LastCpuPercent,
                    WaitP50Milliseconds = GetPercentile(waitSamples, 0.50D),
                    WaitP95Milliseconds = GetPercentile(waitSamples, 0.95D),
                    WaitP99Milliseconds = GetPercentile(waitSamples, 0.99D),
                    WaitMaximumMilliseconds = waitSamples.Length == 0 ? 0D : waitSamples[waitSamples.Length - 1],
                    IsDisposed = _disposed
                };
            }
        }

        /// <summary>
        /// 停止接收新任务并取消全部等待；已经取得许可的任务继续到正常释放。
        /// </summary>
        public void Dispose()
        {
            List<CpuWorkWaiter> canceledWaiters = new List<CpuWorkWaiter>();
            lock (_syncRoot)
            {
                if (_disposed)
                    return;
                _disposed = true;
                while (_waiters.First != null)
                {
                    CpuWorkWaiter waiter = _waiters.First.Value;
                    _waiters.RemoveFirst();
                    waiter.Node = null;
                    if (Interlocked.CompareExchange(
                        ref waiter.State,
                        CpuWorkWaiter.CanceledState,
                        CpuWorkWaiter.WaitingState) == CpuWorkWaiter.WaitingState)
                    {
                        _canceledWaitCount++;
                        canceledWaiters.Add(waiter);
                    }
                }
            }

            foreach (CpuWorkWaiter waiter in canceledWaiters)
            {
                waiter.CancellationRegistration.Dispose();
                waiter.Completion.TrySetCanceled();
            }
        }

        /// <summary>取消一个仍在等待队列中的任务。</summary>
        private void CancelWaiter(CpuWorkWaiter waiter)
        {
            bool canceled = false;
            lock (_syncRoot)
            {
                if (Interlocked.CompareExchange(
                    ref waiter.State,
                    CpuWorkWaiter.CanceledState,
                    CpuWorkWaiter.WaitingState) != CpuWorkWaiter.WaitingState)
                {
                    return;
                }

                if (waiter.Node != null && waiter.Node.List != null)
                    _waiters.Remove(waiter.Node);
                waiter.Node = null;
                _canceledWaitCount++;
                canceled = true;
            }

            if (canceled)
                waiter.Completion.TrySetCanceled();
        }

        /// <summary>归还一个CPU重任务许可并唤醒符合双层边界的等待任务。</summary>
        private void Release(CpuWorkloadKind workloadKind)
        {
            List<CpuWorkWaiter> grantedWaiters;
            lock (_syncRoot)
            {
                _activeCount = Math.Max(0, _activeCount - 1);
                if (workloadKind == CpuWorkloadKind.ImageConversion)
                    _activeImageConversionCount = Math.Max(0, _activeImageConversionCount - 1);
                _completedCount++;
                grantedWaiters = _disposed ? null : GrantPendingWaiters();
            }
            CompleteGrantedWaiters(grantedWaiters);
        }

        /// <summary>按全局和图像转换分类边界判断任务是否可开始。</summary>
        private bool CanGrant(CpuWorkloadKind workloadKind)
        {
            if (_activeCount >= _adjustmentPolicy.CurrentConcurrency)
                return false;
            return workloadKind != CpuWorkloadKind.ImageConversion ||
                _activeImageConversionCount < _options.ImageConversionMaximumConcurrency;
        }

        /// <summary>登记一个已经取得许可的任务。</summary>
        private CpuWorkLease GrantLease(CpuWorkloadKind workloadKind, double waitElapsedMilliseconds)
        {
            _activeCount++;
            if (workloadKind == CpuWorkloadKind.ImageConversion)
                _activeImageConversionCount++;
            _peakActiveCount = Math.Max(_peakActiveCount, _activeCount);
            _peakImageConversionCount = Math.Max(_peakImageConversionCount, _activeImageConversionCount);
            _grantedCount++;
            AddWaitSample(waitElapsedMilliseconds);
            return new CpuWorkLease(this, workloadKind, waitElapsedMilliseconds);
        }

        /// <summary>扫描等待队列并跳过暂时受分类额度限制的队首任务。</summary>
        private List<CpuWorkWaiter> GrantPendingWaiters()
        {
            List<CpuWorkWaiter> grantedWaiters = new List<CpuWorkWaiter>();
            LinkedListNode<CpuWorkWaiter> node = _waiters.First;
            while (node != null && _activeCount < _adjustmentPolicy.CurrentConcurrency)
            {
                LinkedListNode<CpuWorkWaiter> next = node.Next;
                CpuWorkWaiter waiter = node.Value;
                if (Volatile.Read(ref waiter.State) != CpuWorkWaiter.WaitingState)
                {
                    _waiters.Remove(node);
                    waiter.Node = null;
                }
                else if (CanGrant(waiter.WorkloadKind) &&
                    Interlocked.CompareExchange(
                        ref waiter.State,
                        CpuWorkWaiter.GrantedState,
                        CpuWorkWaiter.WaitingState) == CpuWorkWaiter.WaitingState)
                {
                    _waiters.Remove(node);
                    waiter.Node = null;
                    double waitElapsedMilliseconds = GetElapsedMilliseconds(waiter.EnqueuedTimestamp);
                    waiter.Lease = GrantLease(waiter.WorkloadKind, waitElapsedMilliseconds);
                    grantedWaiters.Add(waiter);
                }
                node = next;
            }
            return grantedWaiters;
        }

        /// <summary>在锁外完成许可任务，避免用户延续代码进入调度器锁。</summary>
        private static void CompleteGrantedWaiters(List<CpuWorkWaiter> grantedWaiters)
        {
            if (grantedWaiters == null)
                return;
            foreach (CpuWorkWaiter waiter in grantedWaiters)
            {
                waiter.CancellationRegistration.Dispose();
                waiter.Completion.TrySetResult(waiter.Lease);
            }
        }

        /// <summary>写入固定容量的许可等待耗时样本。</summary>
        private void AddWaitSample(double waitElapsedMilliseconds)
        {
            while (_waitSamples.Count >= _options.WaitSampleCapacity)
                _waitSamples.Dequeue();
            _waitSamples.Enqueue(Math.Max(0D, waitElapsedMilliseconds));
        }

        /// <summary>校验调用方没有把通信或AI伪装成CPU调度类型。</summary>
        private static void ValidateWorkloadKind(CpuWorkloadKind workloadKind)
        {
            if (!Enum.IsDefined(typeof(CpuWorkloadKind), workloadKind))
                throw new ArgumentOutOfRangeException(nameof(workloadKind), "未知CPU工作类型。");
        }

        /// <summary>调度器停止后拒绝新的许可请求。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CpuWorkScheduler));
        }

        /// <summary>计算从Stopwatch时间戳到现在的毫秒数。</summary>
        private static double GetElapsedMilliseconds(long startedTimestamp)
        {
            long elapsedTicks = Math.Max(0L, Stopwatch.GetTimestamp() - startedTimestamp);
            return elapsedTicks * 1000D / Stopwatch.Frequency;
        }

        /// <summary>读取有序耗时样本的指定分位。</summary>
        private static double GetPercentile(double[] sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0)
                return 0D;
            int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
            return sortedValues[index];
        }

        /// <summary>排队中的单个CPU许可请求。</summary>
        private sealed class CpuWorkWaiter
        {
            /// <summary>等待状态。</summary>
            internal const int WaitingState = 0;

            /// <summary>已经取得许可的状态。</summary>
            internal const int GrantedState = 1;

            /// <summary>已经取消的状态。</summary>
            internal const int CanceledState = 2;

            /// <summary>所属调度器。</summary>
            private readonly CpuWorkScheduler _owner;

            /// <summary>异步许可结果。</summary>
            internal readonly TaskCompletionSource<ICpuWorkLease> Completion;

            /// <summary>CPU工作类型。</summary>
            internal readonly CpuWorkloadKind WorkloadKind;

            /// <summary>进入等待队列时的Stopwatch时间戳。</summary>
            internal readonly long EnqueuedTimestamp;

            /// <summary>等待状态，使用Interlocked避免取消和授予竞态。</summary>
            internal int State;

            /// <summary>等待队列节点。</summary>
            internal LinkedListNode<CpuWorkWaiter> Node;

            /// <summary>取消回调注册。</summary>
            internal CancellationTokenRegistration CancellationRegistration;

            /// <summary>授予后准备交给调用方的许可。</summary>
            internal ICpuWorkLease Lease;

            /// <summary>创建CPU许可等待项。</summary>
            internal CpuWorkWaiter(CpuWorkScheduler owner, CpuWorkloadKind workloadKind, long enqueuedTimestamp)
            {
                _owner = owner;
                WorkloadKind = workloadKind;
                EnqueuedTimestamp = enqueuedTimestamp;
                Completion = new TaskCompletionSource<ICpuWorkLease>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>响应调用方取消令牌。</summary>
            internal void Cancel()
            {
                _owner.CancelWaiter(this);
            }
        }

        /// <summary>一次CPU重任务许可，支持重复释放。</summary>
        private sealed class CpuWorkLease : ICpuWorkLease
        {
            /// <summary>许可所属调度器；释放后置空。</summary>
            private CpuWorkScheduler _owner;

            /// <summary>当前许可对应的CPU工作类型。</summary>
            public CpuWorkloadKind WorkloadKind { get; private set; }

            /// <summary>取得许可前的排队耗时，单位毫秒。</summary>
            public double WaitElapsedMilliseconds { get; private set; }

            /// <summary>创建CPU工作许可。</summary>
            internal CpuWorkLease(CpuWorkScheduler owner, CpuWorkloadKind workloadKind, double waitElapsedMilliseconds)
            {
                _owner = owner;
                WorkloadKind = workloadKind;
                WaitElapsedMilliseconds = waitElapsedMilliseconds;
            }

            /// <summary>幂等归还CPU工作许可。</summary>
            public void Dispose()
            {
                CpuWorkScheduler owner = Interlocked.Exchange(ref _owner, null);
                owner?.Release(WorkloadKind);
            }
        }
    }
}
