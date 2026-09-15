using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 使用预分配数组保存回调计时，避免在相机热路径中创建集合或写磁盘。
    /// </summary>
    internal sealed class TimingCollector
    {
        /// <summary>完整记录数组，包含预热帧。</summary>
        private readonly FrameTimingRecord[] _records;

        /// <summary>正式统计前忽略的预热帧数。</summary>
        private readonly int _warmupFrames;

        /// <summary>正式样本目标数。</summary>
        private readonly int _sampleFrames;

        /// <summary>下一个写入位置。</summary>
        private int _nextIndex;

        /// <summary>超过数组容量后未保存明细的帧数。</summary>
        private long _overflowCount;

        /// <summary>
        /// 初始化预分配计时器。
        /// </summary>
        /// <param name="warmupFrames">预热帧数。</param>
        /// <param name="sampleFrames">正式样本数。</param>
        public TimingCollector(int warmupFrames, int sampleFrames)
        {
            if (warmupFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(warmupFrames));
            if (sampleFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleFrames));

            _warmupFrames = warmupFrames;
            _sampleFrames = sampleFrames;
            _records = new FrameTimingRecord[checked(warmupFrames + sampleFrames)];
        }

        /// <summary>
        /// 在当前相机的SDK回调线程写入一条记录。
        /// </summary>
        /// <param name="record">完整阶段计时。</param>
        public void Add(FrameTimingRecord record)
        {
            int index = Interlocked.Increment(ref _nextIndex) - 1;
            if (index >= 0 && index < _records.Length)
            {
                _records[index] = record;
                return;
            }

            Interlocked.Increment(ref _overflowCount);
        }

        /// <summary>
        /// 获取已收到的总帧数，包括超出目标的帧。
        /// </summary>
        public long TotalReceived
        {
            get { return Math.Max(0, Volatile.Read(ref _nextIndex)); }
        }

        /// <summary>
        /// 获取已保存的正式样本数量。
        /// </summary>
        public int FormalSampleCount
        {
            get
            {
                int captured = Math.Min(_records.Length, Math.Max(0, Volatile.Read(ref _nextIndex)));
                return Math.Min(_sampleFrames, Math.Max(0, captured - _warmupFrames));
            }
        }

        /// <summary>
        /// 获取是否达到正式样本目标。
        /// </summary>
        public bool IsComplete
        {
            get { return FormalSampleCount >= _sampleFrames; }
        }

        /// <summary>
        /// 获取超出明细容量的帧数。
        /// </summary>
        public long OverflowCount
        {
            get { return Interlocked.Read(ref _overflowCount); }
        }

        /// <summary>
        /// 复制当前已经完整写入的计时记录。
        /// </summary>
        /// <returns>按照到达顺序排列的记录。</returns>
        public FrameTimingRecord[] Snapshot()
        {
            int count = Math.Min(_records.Length, Math.Max(0, Volatile.Read(ref _nextIndex)));
            FrameTimingRecord[] snapshot = new FrameTimingRecord[count];
            Array.Copy(_records, snapshot, count);
            return snapshot;
        }

        /// <summary>
        /// 获取排除预热帧后的正式记录。
        /// </summary>
        /// <returns>正式计时样本。</returns>
        public FrameTimingRecord[] FormalSnapshot()
        {
            FrameTimingRecord[] all = Snapshot();
            int count = Math.Min(_sampleFrames, Math.Max(0, all.Length - _warmupFrames));
            FrameTimingRecord[] formal = new FrameTimingRecord[count];
            if (count > 0)
            {
                Array.Copy(all, _warmupFrames, formal, 0, count);
            }

            return formal;
        }

        /// <summary>
        /// 按最近秩法计算指定计时字段的分位统计。
        /// </summary>
        /// <param name="selector">从记录中选取计时器刻度。</param>
        /// <returns>毫秒口径统计结果。</returns>
        public TimingStatistics Calculate(Func<FrameTimingRecord, long> selector)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            double[] values = FormalSnapshot()
                .Where(item => item.ResultCode == MyCameraSuccessCode)
                .Select(item => TicksToMilliseconds(selector(item)))
                .OrderBy(value => value)
                .ToArray();

            return CalculateSorted(values);
        }

        /// <summary>海康SDK成功返回码，避免统计失败帧。</summary>
        private const int MyCameraSuccessCode = 0;

        /// <summary>
        /// 将Stopwatch刻度转换成毫秒。
        /// </summary>
        /// <param name="ticks">高精度计时器刻度。</param>
        /// <returns>毫秒。</returns>
        public static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>
        /// 计算已经升序排列的耗时数组统计结果。
        /// </summary>
        /// <param name="sortedValues">升序毫秒数组。</param>
        /// <returns>分位统计。</returns>
        public static TimingStatistics CalculateSorted(double[] sortedValues)
        {
            if (sortedValues == null || sortedValues.Length == 0)
            {
                return new TimingStatistics();
            }

            return new TimingStatistics
            {
                Count = sortedValues.Length,
                AverageMs = sortedValues.Average(),
                P50Ms = Percentile(sortedValues, 0.50),
                P95Ms = Percentile(sortedValues, 0.95),
                P99Ms = Percentile(sortedValues, 0.99),
                P999Ms = Percentile(sortedValues, 0.999),
                MaximumMs = sortedValues[sortedValues.Length - 1],
                Over20Ms = sortedValues.Count(value => value > 20.0),
                Over30Ms = sortedValues.Count(value => value > 30.0),
                Over50Ms = sortedValues.Count(value => value > 50.0)
            };
        }

        /// <summary>
        /// 使用最近秩法读取分位数。
        /// </summary>
        /// <param name="sortedValues">升序数组。</param>
        /// <param name="percentile">0到1之间的分位。</param>
        /// <returns>对应样本值。</returns>
        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            int index = Math.Max(0, (int)Math.Ceiling(percentile * sortedValues.Count) - 1);
            return sortedValues[index];
        }
    }

    /// <summary>
    /// 保存UI显示阶段的耗时，不参与相机回调总耗时。
    /// </summary>
    internal sealed class UiTimingCollector
    {
        /// <summary>UI计时记录。</summary>
        private readonly List<UiTimingRecord> _records = new List<UiTimingRecord>();

        /// <summary>集合访问锁。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 添加一条UI显示耗时。
        /// </summary>
        /// <param name="record">UI阶段记录。</param>
        public void Add(UiTimingRecord record)
        {
            lock (_syncRoot)
            {
                _records.Add(record);
            }
        }

        /// <summary>
        /// 计算Mat转Bitmap耗时统计。
        /// </summary>
        /// <returns>分位统计。</returns>
        public TimingStatistics CalculateToBitmap()
        {
            lock (_syncRoot)
            {
                double[] values = _records
                    .Select(item => TimingCollector.TicksToMilliseconds(item.ToBitmapTicks))
                    .OrderBy(value => value)
                    .ToArray();
                return TimingCollector.CalculateSorted(values);
            }
        }

        /// <summary>
        /// 计算PictureBox换图耗时统计。
        /// </summary>
        /// <returns>分位统计。</returns>
        public TimingStatistics CalculateSetImage()
        {
            lock (_syncRoot)
            {
                double[] values = _records
                    .Select(item => TimingCollector.TicksToMilliseconds(item.SetImageTicks))
                    .OrderBy(value => value)
                    .ToArray();
                return TimingCollector.CalculateSorted(values);
            }
        }
    }
}
