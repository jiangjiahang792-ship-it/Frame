using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 使用单调时钟为每个图像窗口分别提供显示帧率许可。
    /// </summary>
    public sealed class PerWindowUiFrameRateLimiter : IUiFrameRateLimiter
    {
        /// <summary>没有运行档案时使用的默认最大显示帧率。</summary>
        public const int DefaultMaximumFramesPerSecond = 30;

        /// <summary>允许配置的最小显示帧率。</summary>
        public const int MinimumFramesPerSecond = 1;

        /// <summary>允许配置的最大显示帧率。</summary>
        public const int MaximumFramesPerSecond = 60;

        /// <summary>各窗口互不共享的限速状态。</summary>
        private readonly ConcurrentDictionary<string, WindowRateState> _windowStates =
            new ConcurrentDictionary<string, WindowRateState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>可替换的单调时间戳读取器。</summary>
        private readonly Func<long> _timestampProvider;

        /// <summary>单调时钟每秒包含的时间戳数量。</summary>
        private readonly long _timestampFrequency;

        /// <summary>
        /// 使用系统高精度单调时钟创建帧率门控器。
        /// </summary>
        public PerWindowUiFrameRateLimiter()
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
        {
        }

        /// <summary>
        /// 使用指定单调时钟创建帧率门控器，供测试和特殊运行环境替换。
        /// </summary>
        /// <param name="timestampProvider">单调时间戳读取器。</param>
        /// <param name="timestampFrequency">每秒时间戳数量。</param>
        public PerWindowUiFrameRateLimiter(Func<long> timestampProvider, long timestampFrequency)
        {
            _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
            if (timestampFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(timestampFrequency), "时间戳频率必须大于0。");

            _timestampFrequency = timestampFrequency;
        }

        /// <summary>
        /// 原子判断当前窗口是否已到下一次允许显示的时间。
        /// </summary>
        /// <param name="windowName">图像窗口名称。</param>
        /// <param name="maximumFramesPerSecond">每秒最大显示帧数。</param>
        /// <returns>允许本次显示返回 true。</returns>
        public bool TryAcquire(string windowName, int maximumFramesPerSecond)
        {
            string key = string.IsNullOrWhiteSpace(windowName) ? string.Empty : windowName.Trim();
            int safeFramesPerSecond = Clamp(
                maximumFramesPerSecond,
                MinimumFramesPerSecond,
                MaximumFramesPerSecond);
            long minimumInterval = Math.Max(
                1L,
                (long)Math.Ceiling(_timestampFrequency / (double)safeFramesPerSecond));
            long now = _timestampProvider();
            WindowRateState state = _windowStates.GetOrAdd(key, ignored => new WindowRateState());

            lock (state.SyncRoot)
            {
                if (state.HasPublishedFrame && now < state.NextAllowedTimestamp)
                    return false;

                state.HasPublishedFrame = true;
                state.NextAllowedTimestamp = now > long.MaxValue - minimumInterval
                    ? long.MaxValue
                    : now + minimumInterval;
                return true;
            }
        }

        /// <summary>
        /// 删除指定窗口的限速状态。
        /// </summary>
        /// <param name="windowName">图像窗口名称。</param>
        public void Reset(string windowName)
        {
            string key = string.IsNullOrWhiteSpace(windowName) ? string.Empty : windowName.Trim();
            _windowStates.TryRemove(key, out WindowRateState ignored);
        }

        /// <summary>
        /// 删除全部窗口的限速状态。
        /// </summary>
        public void ResetAll()
        {
            _windowStates.Clear();
        }

        /// <summary>
        /// 将整数限制到指定闭区间。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <param name="minimum">最小值。</param>
        /// <param name="maximum">最大值。</param>
        /// <returns>限制后的值。</returns>
        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        /// <summary>
        /// 单个窗口的下一次许可时间及同步对象。
        /// </summary>
        private sealed class WindowRateState
        {
            /// <summary>保护本窗口时间状态的独立同步锁。</summary>
            public readonly object SyncRoot = new object();

            /// <summary>当前窗口是否已经发布过第一帧。</summary>
            public bool HasPublishedFrame;

            /// <summary>下一次允许显示的单调时间戳。</summary>
            public long NextAllowedTimestamp;
        }
    }
}
