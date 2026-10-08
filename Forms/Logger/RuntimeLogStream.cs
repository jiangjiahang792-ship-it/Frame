using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Logger
{
    /// <summary>生产日志的只读旁路，固定容量订阅独立消费，不回调界面或等待消费者。</summary>
    internal static class RuntimeLogStream
    {
        /// <summary>仅订阅和退订使用此锁，生产发布不取得此锁。</summary>
        private static readonly object registrations = new object();
        /// <summary>发布方读取不可变订阅数组，退订后旧快照也会被关闭标志拒绝。</summary>
        private static Subscription[] subscribers = new Subscription[0];

        /// <summary>注册捕获的根流程实例集合，容量限定为一到一万条。</summary>
        internal static Subscription Subscribe(object[] roots, int capacity)
        {
            if (capacity < 1 || capacity > 10000) throw new ArgumentOutOfRangeException(nameof(capacity));
            var subscription = new Subscription((object[])roots.Clone(), capacity);
            lock (registrations)
            {
                var next = new Subscription[subscribers.Length + 1];
                Array.Copy(subscribers, next, subscribers.Length);
                next[next.Length - 1] = subscription;
                Volatile.Write(ref subscribers, next);
            }
            return subscription;
        }

        /// <summary>在等级开关通过之后、文件任务入队之前冻结来源；任何旁路异常都不进入生产调用栈。</summary>
        internal static void Publish(MsgLevel level, string message, DateTime timestamp)
        {
            try
            {
                var listeners = Volatile.Read(ref subscribers);
                var context = RuntimeLogContext.Current;
                if (listeners.Length == 0 || context?.Root == null) return;
                var entry = new Entry(timestamp, level, context.Source, RuntimeLogContext.Limit(message, 4096));
                foreach (var subscription in listeners)
                {
                    try { subscription.Publish(context.Root, entry); }
                    catch { /* 单个显示订阅故障不能影响生产日志和其它订阅。 */ }
                }
            }
            catch { /* 日志旁路尽力交付，禁止反向中断节点执行。 */ }
        }

        /// <summary>移除订阅，不等待已经读取旧快照的发布者。</summary>
        private static void Remove(Subscription subscription)
        {
            lock (registrations)
            {
                var next = new List<Subscription>(subscribers.Length);
                foreach (var item in subscribers) if (!ReferenceEquals(item, subscription)) next.Add(item);
                Volatile.Write(ref subscribers, next.ToArray());
            }
        }

        /// <summary>多个订阅共享的不可变小记录，不持有流程引用。</summary>
        internal sealed class Entry
        {
            /// <summary>生产线程记录时间。</summary>
            internal readonly DateTime Timestamp;
            /// <summary>原日志等级。</summary>
            internal readonly MsgLevel Level;
            /// <summary>已冻结的来源路径。</summary>
            internal readonly string Source;
            /// <summary>有界日志正文。</summary>
            internal readonly string Message;
            /// <summary>初始化供多个独立缓冲共享的记录。</summary>
            internal Entry(DateTime timestamp, MsgLevel level, string source, string message)
            { Timestamp = timestamp; Level = level; Source = source; Message = message; }
        }

        /// <summary>沿用已有日志缓冲的并发队列策略，超限丢旧，不使用应用层生产者锁。</summary>
        internal sealed class Subscription : IDisposable
        {
            /// <summary>根实例引用只用于过滤，退订后立即释放。</summary>
            private object[] roots;
            /// <summary>清空时整体分离队列和计数；空引用表示永久关闭。</summary>
            private QueueState state = new QueueState();
            /// <summary>当前订阅的保留上限。</summary>
            private readonly int capacity;
            /// <summary>关闭状态，关闭后的迟到发布不可进入缓冲。</summary>
            private int disposed;

            /// <summary>创建独立有界缓冲。</summary>
            internal Subscription(object[] roots, int capacity) { this.roots = roots; this.capacity = capacity; }

            /// <summary>匹配实例后发布，不按名称、编号或正文筛选；超限淘汰最旧记录。</summary>
            internal void Publish(object root, Entry entry)
            {
                if (Volatile.Read(ref disposed) != 0) return;
                bool matches = false;
                foreach (var item in Volatile.Read(ref roots)) if (ReferenceEquals(item, root)) { matches = true; break; }
                if (!matches) return;
                var pending = Volatile.Read(ref state);
                if (pending == null) return;
                pending.entries.Enqueue(entry);
                int currentCount = Interlocked.Increment(ref pending.count);
                Entry ignored;
                while (currentCount > capacity && pending.entries.TryDequeue(out ignored))
                {
                    currentCount = Interlocked.Decrement(ref pending.count);
                }
                // 清空或关闭后的迟到写入仅落在已分离状态，随本次发布退出即可回收。
            }

            /// <summary>有界批量提取，每条已发布记录最多交付一次。</summary>
            internal IReadOnlyList<Entry> Drain(int maxCount)
            {
                if (maxCount <= 0 || Volatile.Read(ref disposed) != 0) return new Entry[0];
                var pending = Volatile.Read(ref state);
                if (pending == null) return new Entry[0];
                int limit = Math.Min(maxCount, capacity);
                var result = new List<Entry>(limit);
                Entry entry;
                while (result.Count < limit && pending.entries.TryDequeue(out entry))
                {
                    Interlocked.Decrement(ref pending.count);
                    result.Add(entry);
                }
                return Volatile.Read(ref disposed) == 0 && ReferenceEquals(pending, Volatile.Read(ref state))
                    ? (IReadOnlyList<Entry>)result : new Entry[0];
            }

            /// <summary>常量工作量分离当前队列；并发清空成功同样覆盖本次边界，不重试或追赶生产者。</summary>
            internal void Clear()
            {
                var previous = Volatile.Read(ref state);
                // 条件交换避免与退订竞争时重新装入队列；迟到生产者仅持有旧状态。
                if (previous != null) Interlocked.CompareExchange(ref state, new QueueState(), previous);
            }

            /// <summary>幂等退订并释放根实例和整个队列，不等待迟到生产者。</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0) return;
                Volatile.Write(ref roots, new object[0]);
                Interlocked.Exchange(ref state, null);
                Remove(this);
            }

            /// <summary>同一代队列和数量必须一起替换，防止清空后的新队列计数受旧发布影响。</summary>
            private sealed class QueueState
            {
                /// <summary>标准并发队列负责发布和消费的原子性。</summary>
                internal readonly ConcurrentQueue<Entry> entries = new ConcurrentQueue<Entry>();
                /// <summary>当前代数量，避免高频遍历队列。</summary>
                internal int count;
            }
        }
    }
}
