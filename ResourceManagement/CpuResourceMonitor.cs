using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 使用Windows系统时间计数器采样全机CPU使用率，不创建性能计数器或WMI查询。
    /// </summary>
    public sealed class WindowsSystemCpuUsageSampler : ISystemCpuUsageSampler
    {
        /// <summary>保护连续采样基线。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>上次系统空闲时间。</summary>
        private ulong _previousIdleTime;

        /// <summary>上次系统内核时间。</summary>
        private ulong _previousKernelTime;

        /// <summary>上次系统用户时间。</summary>
        private ulong _previousUserTime;

        /// <summary>是否已经取得首个采样基线。</summary>
        private bool _hasBaseline;

        /// <summary>
        /// 尝试读取0～100范围的全机CPU使用率。
        /// </summary>
        public bool TrySample(out double cpuPercent)
        {
            cpuPercent = 0D;
            NativeFileTime idle;
            NativeFileTime kernel;
            NativeFileTime user;
            if (!GetSystemTimes(out idle, out kernel, out user))
                return false;

            ulong idleTime = idle.ToUInt64();
            ulong kernelTime = kernel.ToUInt64();
            ulong userTime = user.ToUInt64();
            lock (_syncRoot)
            {
                if (!_hasBaseline)
                {
                    SetBaseline(idleTime, kernelTime, userTime);
                    return false;
                }

                if (idleTime < _previousIdleTime ||
                    kernelTime < _previousKernelTime ||
                    userTime < _previousUserTime)
                {
                    SetBaseline(idleTime, kernelTime, userTime);
                    return false;
                }

                ulong idleDelta = idleTime - _previousIdleTime;
                ulong totalDelta = (kernelTime - _previousKernelTime) + (userTime - _previousUserTime);
                SetBaseline(idleTime, kernelTime, userTime);
                if (totalDelta == 0UL || idleDelta > totalDelta)
                    return false;

                cpuPercent = Math.Max(0D, Math.Min(100D, (totalDelta - idleDelta) * 100D / totalDelta));
                return true;
            }
        }

        /// <summary>保存下一次差值采样使用的系统时间。</summary>
        private void SetBaseline(ulong idleTime, ulong kernelTime, ulong userTime)
        {
            _previousIdleTime = idleTime;
            _previousKernelTime = kernelTime;
            _previousUserTime = userTime;
            _hasBaseline = true;
        }

        /// <summary>Windows FILETIME的托管布局。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime
        {
            /// <summary>低32位。</summary>
            public uint LowDateTime;

            /// <summary>高32位。</summary>
            public uint HighDateTime;

            /// <summary>合并为无符号64位计数。</summary>
            public ulong ToUInt64()
            {
                return ((ulong)HighDateTime << 32) | LowDateTime;
            }
        }

        /// <summary>读取系统空闲、内核和用户累计时间。</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(
            out NativeFileTime idleTime,
            out NativeFileTime kernelTime,
            out NativeFileTime userTime);
    }

    /// <summary>
    /// 低频采样全机CPU并驱动CPU调度器动态额度，不占用UI消息循环。
    /// </summary>
    public sealed class CpuResourceMonitor : IDisposable
    {
        /// <summary>接收CPU采样的调度器。</summary>
        private readonly ICpuWorkScheduler _scheduler;

        /// <summary>可替换的系统CPU采样器。</summary>
        private readonly ISystemCpuUsageSampler _sampler;

        /// <summary>用于动态策略的单调时钟。</summary>
        private readonly Stopwatch _monotonicClock = Stopwatch.StartNew();

        /// <summary>后台低频采样定时器。</summary>
        private readonly Timer _timer;

        /// <summary>防止定时器回调重入。</summary>
        private int _sampling;

        /// <summary>监控器已经停止。</summary>
        private int _disposed;

        /// <summary>
        /// 创建并启动CPU资源监控器。
        /// </summary>
        /// <param name="scheduler">接收采样的CPU调度器。</param>
        /// <param name="sampler">系统CPU采样器。</param>
        /// <param name="sampleIntervalMilliseconds">采样周期毫秒数。</param>
        public CpuResourceMonitor(
            ICpuWorkScheduler scheduler,
            ISystemCpuUsageSampler sampler,
            int sampleIntervalMilliseconds)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            int interval = NormalizeInterval(sampleIntervalMilliseconds);
            _timer = new Timer(SampleTimerCallback, null, interval, interval);
        }

        /// <summary>
        /// 更新后台CPU采样周期。
        /// </summary>
        /// <param name="sampleIntervalMilliseconds">新的采样周期毫秒数。</param>
        public void Reconfigure(int sampleIntervalMilliseconds)
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;
            int interval = NormalizeInterval(sampleIntervalMilliseconds);
            _timer.Change(interval, interval);
        }

        /// <summary>
        /// 立即执行一次采样，供后台定时器和专项测试复用。
        /// </summary>
        /// <returns>取得有效CPU样本返回true。</returns>
        public bool SampleNow()
        {
            if (Volatile.Read(ref _disposed) == 1 || Interlocked.Exchange(ref _sampling, 1) == 1)
                return false;
            try
            {
                if (!_sampler.TrySample(out double cpuPercent))
                    return false;
                _scheduler.ObserveCpuSample(cpuPercent, _monotonicClock.ElapsedMilliseconds);
                return true;
            }
            finally
            {
                Volatile.Write(ref _sampling, 0);
            }
        }

        /// <summary>停止后台CPU采样。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            _timer.Dispose();
        }

        /// <summary>响应后台定时器，异常由下次采样自动恢复。</summary>
        private void SampleTimerCallback(object state)
        {
            try
            {
                SampleNow();
            }
            catch
            {
            }
        }

        /// <summary>把采样周期限制在低开销范围。</summary>
        private static int NormalizeInterval(int sampleIntervalMilliseconds)
        {
            return Math.Max(1000, Math.Min(60000, sampleIntervalMilliseconds));
        }
    }
}
