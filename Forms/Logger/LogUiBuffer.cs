using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Logger
{
    /// <summary>
    /// 表示一条等待显示到日志界面的轻量日志。
    /// </summary>
    public sealed class LogUiEntry
    {
        /// <summary>
        /// 日志等级。
        /// </summary>
        private readonly MsgLevel _level;

        /// <summary>
        /// 已经格式化的日志显示内容。
        /// </summary>
        private readonly string _info;

        /// <summary>
        /// 创建一条日志界面显示记录。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="info">已经格式化的日志显示内容。</param>
        public LogUiEntry(MsgLevel level, string info)
        {
            _level = level;
            _info = info ?? string.Empty;
        }

        /// <summary>
        /// 获取日志等级。
        /// </summary>
        public MsgLevel Level
        {
            get { return _level; }
        }

        /// <summary>
        /// 获取已经格式化的日志显示内容。
        /// </summary>
        public string Info
        {
            get { return _info; }
        }
    }

    /// <summary>
    /// 定义线程安全的日志界面缓冲区，便于替换不同的限流策略。
    /// </summary>
    public interface ILogUiBuffer
    {
        /// <summary>
        /// 获取当前等待显示的日志数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 写入一条等待显示的日志。
        /// </summary>
        /// <param name="entry">待显示日志。</param>
        void Enqueue(LogUiEntry entry);

        /// <summary>
        /// 按先进先出顺序取出指定上限的一批日志。
        /// </summary>
        /// <param name="maxCount">本批最多取出的日志数量。</param>
        /// <returns>本批取出的日志。</returns>
        IReadOnlyList<LogUiEntry> DequeueBatch(int maxCount);

        /// <summary>
        /// 清空所有尚未显示的日志。
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 使用并发队列实现的有界日志界面缓冲区，超限时优先淘汰最旧日志。
    /// </summary>
    public sealed class BoundedLogUiBuffer : ILogUiBuffer
    {
        /// <summary>
        /// 保存等待显示日志的线程安全队列。
        /// </summary>
        private readonly ConcurrentQueue<LogUiEntry> _entries = new ConcurrentQueue<LogUiEntry>();

        /// <summary>
        /// 缓冲区允许保留的最大日志数量。
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        /// 当前队列数量，避免高频路径反复读取并发队列的 Count。
        /// </summary>
        private int _count;

        /// <summary>
        /// 创建指定容量的有界日志界面缓冲区。
        /// </summary>
        /// <param name="capacity">最多保留的日志数量。</param>
        public BoundedLogUiBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException("capacity", "日志界面缓冲容量必须大于零。");

            _capacity = capacity;
        }

        /// <summary>
        /// 获取当前等待显示的日志数量。
        /// </summary>
        public int Count
        {
            get { return Math.Max(0, Volatile.Read(ref _count)); }
        }

        /// <summary>
        /// 写入一条日志；容量不足时淘汰队首的旧日志。
        /// </summary>
        /// <param name="entry">待显示日志。</param>
        public void Enqueue(LogUiEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException("entry");

            _entries.Enqueue(entry);
            int currentCount = Interlocked.Increment(ref _count);
            LogUiEntry ignoredEntry;
            while (currentCount > _capacity && _entries.TryDequeue(out ignoredEntry))
            {
                currentCount = Interlocked.Decrement(ref _count);
            }
        }

        /// <summary>
        /// 按先进先出顺序取出指定上限的一批日志。
        /// </summary>
        /// <param name="maxCount">本批最多取出的日志数量。</param>
        /// <returns>本批取出的日志。</returns>
        public IReadOnlyList<LogUiEntry> DequeueBatch(int maxCount)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "日志界面刷新批量必须大于零。");

            List<LogUiEntry> batch = new List<LogUiEntry>(Math.Min(maxCount, Count));
            LogUiEntry entry;
            while (batch.Count < maxCount && _entries.TryDequeue(out entry))
            {
                Interlocked.Decrement(ref _count);
                batch.Add(entry);
            }

            return batch;
        }

        /// <summary>
        /// 清空所有尚未显示的日志。
        /// </summary>
        public void Clear()
        {
            LogUiEntry ignoredEntry;
            while (_entries.TryDequeue(out ignoredEntry))
            {
                Interlocked.Decrement(ref _count);
            }
        }
    }
}
