using System;
using System.Collections.Generic;
using System.Threading;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 有界优先队列可接收的资源任务契约。
    /// </summary>
    public interface IPrioritizedResourceWorkItem : IDisposable
    {
        /// <summary>获取任务是否属于必须优先保留的高优先级任务。</summary>
        bool IsHighPriority { get; }

        /// <summary>获取任务继续排队会额外持有的资源字节数。</summary>
        long EstimatedBytes { get; }

        /// <summary>获取任务所有者标识，供节点删除时定向排空。</summary>
        string OwnerKey { get; }
    }

    /// <summary>
    /// 有界优先队列的入队状态。
    /// </summary>
    public enum BoundedQueueAdmissionStatus
    {
        /// <summary>任务直接进入队列。</summary>
        Accepted,

        /// <summary>高优先级任务淘汰较早的普通任务后进入队列。</summary>
        AcceptedAfterEviction,

        /// <summary>队列容量或字节预算不足，当前任务被拒绝。</summary>
        RejectedFull,

        /// <summary>单个任务已经超过整个队列的字节预算。</summary>
        RejectedItemExceedsBudget,

        /// <summary>队列已经停止接收任务。</summary>
        RejectedStopped
    }

    /// <summary>
    /// 有界优先队列的单次入队结果。
    /// </summary>
    public struct BoundedQueueAdmissionResult
    {
        /// <summary>获取入队状态。</summary>
        public BoundedQueueAdmissionStatus Status { get; private set; }

        /// <summary>获取为了接收高优先级任务而释放的普通任务数量。</summary>
        public int EvictedLowPriorityCount { get; private set; }

        /// <summary>获取队列是否已经接管当前任务的所有权。</summary>
        public bool Accepted
        {
            get
            {
                return Status == BoundedQueueAdmissionStatus.Accepted ||
                    Status == BoundedQueueAdmissionStatus.AcceptedAfterEviction;
            }
        }

        /// <summary>
        /// 创建入队结果。
        /// </summary>
        /// <param name="status">入队状态。</param>
        /// <param name="evictedLowPriorityCount">被释放的普通任务数量。</param>
        public BoundedQueueAdmissionResult(
            BoundedQueueAdmissionStatus status,
            int evictedLowPriorityCount)
        {
            Status = status;
            EvictedLowPriorityCount = Math.Max(0, evictedLowPriorityCount);
        }
    }

    /// <summary>
    /// 同时限制任务数量与资源字节数的线程安全优先队列。
    /// </summary>
    /// <typeparam name="T">实现资源任务契约的任务类型。</typeparam>
    public sealed class BoundedPriorityWorkQueue<T> : IDisposable
        where T : class, IPrioritizedResourceWorkItem
    {
        /// <summary>保护队列、累计字节数和接收状态的一致性锁。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>按优先级和入队先后保存等待处理的不可变条目。</summary>
        private readonly LinkedList<QueueEntry> _items = new LinkedList<QueueEntry>();

        /// <summary>单次资源释放异常的可选观察器。</summary>
        private readonly Action<Exception> _disposeExceptionHandler;

        /// <summary>高优先级接纳时被淘汰任务的可选异步处理器。</summary>
        private readonly Action<T, long> _evictedItemHandler;

        /// <summary>允许排队的最大任务数量。</summary>
        private readonly int _capacity;

        /// <summary>允许排队任务累计持有的最大字节数。</summary>
        private readonly long _memoryBudgetBytes;

        /// <summary>当前等待任务累计持有的估算字节数。</summary>
        private long _queuedBytes;

        /// <summary>工作池本次生命周期内观察到的等待任务峰值。</summary>
        private int _peakCount;

        /// <summary>工作池本次生命周期内观察到的等待字节峰值。</summary>
        private long _peakQueuedBytes;

        /// <summary>当前队列是否继续接收入队请求。</summary>
        private bool _accepting = true;

        /// <summary>保证释放操作只执行一次。</summary>
        private int _disposed;

        /// <summary>获取允许排队的最大任务数量。</summary>
        public int Capacity
        {
            get { return _capacity; }
        }

        /// <summary>获取允许排队任务累计持有的最大字节数。</summary>
        public long MemoryBudgetBytes
        {
            get { return _memoryBudgetBytes; }
        }

        /// <summary>获取当前等待处理的任务数量。</summary>
        public int Count
        {
            get
            {
                lock (_syncRoot)
                    return _items.Count;
            }
        }

        /// <summary>获取当前等待任务累计持有的估算字节数。</summary>
        public long QueuedBytes
        {
            get
            {
                lock (_syncRoot)
                    return _queuedBytes;
            }
        }

        /// <summary>获取工作池本次生命周期内的等待任务峰值。</summary>
        public int PeakCount
        {
            get
            {
                lock (_syncRoot)
                    return _peakCount;
            }
        }

        /// <summary>获取工作池本次生命周期内的等待字节峰值。</summary>
        public long PeakQueuedBytes
        {
            get
            {
                lock (_syncRoot)
                    return _peakQueuedBytes;
            }
        }

        /// <summary>获取队列当前是否仍接收新任务。</summary>
        public bool IsAccepting
        {
            get
            {
                lock (_syncRoot)
                    return _accepting;
            }
        }

        /// <summary>
        /// 使用任务数量和字节预算初始化有界队列。
        /// </summary>
        /// <param name="capacity">允许排队的最大任务数量。</param>
        /// <param name="memoryBudgetBytes">允许排队任务累计持有的最大字节数。</param>
        /// <param name="disposeExceptionHandler">资源释放失败时的可选观察器。</param>
        /// <param name="evictedItemHandler">被高优先级任务淘汰后的可选处理器。</param>
        public BoundedPriorityWorkQueue(
            int capacity,
            long memoryBudgetBytes,
            Action<Exception> disposeExceptionHandler = null,
            Action<T, long> evictedItemHandler = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "队列容量必须大于零。");
            if (memoryBudgetBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(memoryBudgetBytes), "队列字节预算必须大于零。");

            _capacity = capacity;
            _memoryBudgetBytes = memoryBudgetBytes;
            _disposeExceptionHandler = disposeExceptionHandler;
            _evictedItemHandler = evictedItemHandler;
        }

        /// <summary>
        /// 队列内部保存的不可变任务快照，避免接管后外部修改破坏资源记账。
        /// </summary>
        private sealed class QueueEntry
        {
            /// <summary>队列已经接管的原始任务。</summary>
            public T Item { get; private set; }

            /// <summary>入队瞬间固定的高优先级标志。</summary>
            public bool IsHighPriority { get; private set; }

            /// <summary>入队瞬间固定的资源估算字节数。</summary>
            public long EstimatedBytes { get; private set; }

            /// <summary>入队瞬间固定的任务所有者标识。</summary>
            public string OwnerKey { get; private set; }

            /// <summary>
            /// 从任务当前状态创建不可变队列条目。
            /// </summary>
            /// <param name="item">队列即将接管的任务。</param>
            /// <param name="estimatedBytes">规范化后的资源估算字节数。</param>
            public QueueEntry(T item, long estimatedBytes)
            {
                Item = item;
                IsHighPriority = item.IsHighPriority;
                EstimatedBytes = estimatedBytes;
                OwnerKey = item.OwnerKey;
            }

            /// <summary>
            /// 复制已冻结的任务元数据，只替换两阶段接纳确认后的实际资源字节。
            /// </summary>
            /// <param name="estimatedBytes">最终确认的资源估算字节。</param>
            /// <returns>保留原优先级和所有者的最终队列条目。</returns>
            public QueueEntry WithEstimatedBytes(long estimatedBytes)
            {
                return new QueueEntry
                {
                    Item = Item,
                    IsHighPriority = IsHighPriority,
                    EstimatedBytes = estimatedBytes,
                    OwnerKey = OwnerKey
                };
            }

            /// <summary>仅供复制冻结元数据时初始化队列条目。</summary>
            private QueueEntry()
            {
            }
        }

        /// <summary>
        /// 立即尝试接管任务；普通任务不淘汰旧任务，高优先级任务只淘汰最早的普通任务。
        /// </summary>
        /// <param name="item">待入队任务；拒绝时仍由调用方负责释放。</param>
        /// <returns>入队状态以及已淘汰的普通任务数量。</returns>
        public BoundedQueueAdmissionResult TryEnqueue(T item)
        {
            return TryEnqueueCore(item, null, false);
        }

        /// <summary>
        /// 立即尝试接管任务，并在确定可接收但尚未发布给消费者时执行接收回调。
        /// </summary>
        /// <param name="item">待入队任务；拒绝时仍由调用方负责释放。</param>
        /// <param name="beforeAccept">确定可接收后、正式入队前执行的轻量回调。</param>
        /// <returns>入队状态以及已淘汰的普通任务数量。</returns>
        public BoundedQueueAdmissionResult TryEnqueue(T item, Action beforeAccept)
        {
            Func<long> admissionCallback = beforeAccept == null
                ? null
                : (Func<long>)(() =>
                {
                    beforeAccept();
                    return 0L;
                });
            return TryEnqueueCore(item, admissionCallback, false);
        }

        /// <summary>
        /// 立即尝试接管任务，并在初步确认可接收后取得受保护资源的实际字节，再按实际值完成最终判定。
        /// </summary>
        /// <param name="item">待入队任务；拒绝时仍由调用方负责释放。</param>
        /// <param name="beforeAcceptAndGetEstimatedBytes">正式发布前取得资源并返回实际估算字节的回调。</param>
        /// <returns>按最终实际字节计算的入队状态。</returns>
        public BoundedQueueAdmissionResult TryEnqueueWithUpdatedEstimate(
            T item,
            Func<long> beforeAcceptAndGetEstimatedBytes)
        {
            if (beforeAcceptAndGetEstimatedBytes == null)
                throw new ArgumentNullException(nameof(beforeAcceptAndGetEstimatedBytes));

            return TryEnqueueCore(item, beforeAcceptAndGetEstimatedBytes, true);
        }

        /// <summary>
        /// 执行两阶段零等待接纳；第一阶段用缓存值避免满队列时取得资源，第二阶段可按实际值重新判定。
        /// </summary>
        /// <param name="item">待入队任务。</param>
        /// <param name="beforeAcceptAndGetEstimatedBytes">初步可接收后执行的资源取得回调。</param>
        /// <param name="useUpdatedEstimate">是否使用回调返回值替换初始字节估算。</param>
        /// <returns>最终入队状态。</returns>
        private BoundedQueueAdmissionResult TryEnqueueCore(
            T item,
            Func<long> beforeAcceptAndGetEstimatedBytes,
            bool useUpdatedEstimate)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            long initialItemBytes = NormalizeEstimatedBytes(item.EstimatedBytes);
            QueueEntry preliminaryEntry = new QueueEntry(item, initialItemBytes);
            List<QueueEntry> evictedItems = null;
            BoundedQueueAdmissionResult result;

            lock (_syncRoot)
            {
                if (!_accepting)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedStopped, 0);

                if (initialItemBytes > _memoryBudgetBytes)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedItemExceedsBudget, 0);

                bool initiallyFits = CanFit(1, initialItemBytes);
                if (!initiallyFits && !preliminaryEntry.IsHighPriority)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedFull, 0);

                if (!initiallyFits && FindLowPriorityEvictions(initialItemBytes) == null)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedFull, 0);

                long admittedItemBytes = initialItemBytes;
                if (beforeAcceptAndGetEstimatedBytes != null)
                {
                    long updatedEstimate = beforeAcceptAndGetEstimatedBytes();
                    if (useUpdatedEstimate)
                        admittedItemBytes = NormalizeEstimatedBytes(updatedEstimate);
                }

                if (admittedItemBytes > _memoryBudgetBytes)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedItemExceedsBudget, 0);

                QueueEntry incomingEntry = preliminaryEntry.WithEstimatedBytes(admittedItemBytes);
                if (CanFit(1, admittedItemBytes))
                {
                    AddEntry(incomingEntry);
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.Accepted, 0);
                }

                if (!preliminaryEntry.IsHighPriority)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedFull, 0);

                List<LinkedListNode<QueueEntry>> evictedNodes = FindLowPriorityEvictions(admittedItemBytes);
                if (evictedNodes == null)
                    return new BoundedQueueAdmissionResult(BoundedQueueAdmissionStatus.RejectedFull, 0);

                evictedItems = new List<QueueEntry>(evictedNodes.Count);
                foreach (LinkedListNode<QueueEntry> evictedNode in evictedNodes)
                {
                    evictedItems.Add(evictedNode.Value);
                    _queuedBytes -= evictedNode.Value.EstimatedBytes;
                    _items.Remove(evictedNode);
                }

                AddEntry(incomingEntry);
                result = new BoundedQueueAdmissionResult(
                    BoundedQueueAdmissionStatus.AcceptedAfterEviction,
                    evictedItems.Count);
            }

            HandleEvictedItemsNoThrow(evictedItems);
            return result;
        }

        /// <summary>
        /// 等待并取得最早的任务；队列停止且已经排空时返回 false。
        /// </summary>
        /// <param name="item">成功取得的任务。</param>
        /// <returns>取得任务返回 true；队列已经停止并排空返回 false。</returns>
        public bool TryTake(out T item)
        {
            return TryTake(out item, null);
        }

        /// <summary>
        /// 等待并取得最早任务，在任务离开队列锁保护前执行认领回调。
        /// </summary>
        /// <param name="item">成功取得的任务。</param>
        /// <param name="beforeTake">任务离开队列前执行的无异常轻量回调。</param>
        /// <returns>取得任务返回 true；队列已经停止并排空返回 false。</returns>
        public bool TryTake(out T item, Action beforeTake)
        {
            lock (_syncRoot)
            {
                while (_items.Count == 0 && _accepting)
                    Monitor.Wait(_syncRoot);

                if (_items.Count == 0)
                {
                    item = null;
                    return false;
                }

                QueueEntry entry = _items.First.Value;
                beforeTake?.Invoke();
                _items.RemoveFirst();
                _queuedBytes -= entry.EstimatedBytes;
                if (_queuedBytes < 0)
                    _queuedBytes = 0;
                item = entry.Item;
                return true;
            }
        }

        /// <summary>
        /// 取消指定所有者仍在等待的任务，并立即释放这些任务。
        /// </summary>
        /// <param name="ownerKey">任务所有者标识。</param>
        /// <returns>取消并释放的任务数量。</returns>
        public int CancelOwner(string ownerKey)
        {
            if (string.IsNullOrWhiteSpace(ownerKey))
                return 0;

            List<T> removedItems = new List<T>();
            lock (_syncRoot)
            {
                LinkedListNode<QueueEntry> node = _items.First;
                while (node != null)
                {
                    LinkedListNode<QueueEntry> next = node.Next;
                    if (string.Equals(node.Value.OwnerKey, ownerKey, StringComparison.Ordinal))
                    {
                        removedItems.Add(node.Value.Item);
                        _queuedBytes -= node.Value.EstimatedBytes;
                        _items.Remove(node);
                    }
                    node = next;
                }

                if (_queuedBytes < 0)
                    _queuedBytes = 0;
            }

            DisposeItemsNoThrow(removedItems);
            return removedItems.Count;
        }

        /// <summary>
        /// 停止接收新任务、唤醒消费者并释放全部待处理任务。
        /// </summary>
        /// <returns>排空并释放的任务数量。</returns>
        public int CompleteAndDisposePending()
        {
            List<T> pendingItems = new List<T>();
            lock (_syncRoot)
            {
                _accepting = false;
                foreach (QueueEntry entry in _items)
                    pendingItems.Add(entry.Item);
                _items.Clear();
                _queuedBytes = 0;
                Monitor.PulseAll(_syncRoot);
            }

            DisposeItemsNoThrow(pendingItems);
            return pendingItems.Count;
        }

        /// <summary>
        /// 停止接收新任务并唤醒消费者，现有等待任务仍允许被正常处理。
        /// </summary>
        public void CompleteAdding()
        {
            lock (_syncRoot)
            {
                _accepting = false;
                Monitor.PulseAll(_syncRoot);
            }
        }

        /// <summary>
        /// 释放队列并清理全部待处理任务。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            CompleteAndDisposePending();
        }

        /// <summary>
        /// 判断增加指定任务数量和字节数后是否仍在双重边界内。
        /// </summary>
        /// <param name="additionalCount">新增任务数量。</param>
        /// <param name="additionalBytes">新增任务字节数。</param>
        /// <returns>任务数量和字节数均不越界返回 true。</returns>
        private bool CanFit(int additionalCount, long additionalBytes)
        {
            return _items.Count + additionalCount <= _capacity &&
                additionalBytes <= _memoryBudgetBytes - _queuedBytes;
        }

        /// <summary>
        /// 选择足以容纳高优先级任务的最早普通任务集合，不足时不改变队列。
        /// </summary>
        /// <param name="incomingBytes">高优先级任务字节数。</param>
        /// <returns>可淘汰集合；普通任务不足以腾出空间时返回 null。</returns>
        private List<LinkedListNode<QueueEntry>> FindLowPriorityEvictions(long incomingBytes)
        {
            int projectedCount = _items.Count + 1;
            long projectedBytes = _queuedBytes + incomingBytes;
            List<LinkedListNode<QueueEntry>> candidates = new List<LinkedListNode<QueueEntry>>();

            for (LinkedListNode<QueueEntry> node = _items.First;
                node != null && (projectedCount > _capacity || projectedBytes > _memoryBudgetBytes);
                node = node.Next)
            {
                if (node.Value.IsHighPriority)
                    continue;

                candidates.Add(node);
                projectedCount--;
                projectedBytes -= node.Value.EstimatedBytes;
            }

            return projectedCount <= _capacity && projectedBytes <= _memoryBudgetBytes
                ? candidates
                : null;
        }

        /// <summary>
        /// 按优先级追加不可变条目并唤醒一个等待消费者。
        /// </summary>
        /// <param name="entry">由队列接管的不可变任务条目。</param>
        private void AddEntry(QueueEntry entry)
        {
            if (entry.IsHighPriority)
            {
                LinkedListNode<QueueEntry> insertAfter = _items.Last;
                while (insertAfter != null && !insertAfter.Value.IsHighPriority)
                    insertAfter = insertAfter.Previous;

                if (insertAfter == null)
                    _items.AddFirst(entry);
                else
                    _items.AddAfter(insertAfter, entry);
            }
            else
            {
                _items.AddLast(entry);
            }

            _queuedBytes += entry.EstimatedBytes;
            if (_items.Count > _peakCount)
                _peakCount = _items.Count;
            if (_queuedBytes > _peakQueuedBytes)
                _peakQueuedBytes = _queuedBytes;
            Monitor.Pulse(_syncRoot);
        }

        /// <summary>
        /// 把无效估算值规范为一个字节，保证累计值可计算。
        /// </summary>
        /// <param name="estimatedBytes">任务报告的估算字节数。</param>
        /// <returns>至少为一个字节的估算值。</returns>
        private static long NormalizeEstimatedBytes(long estimatedBytes)
        {
            return Math.Max(1L, estimatedBytes);
        }

        /// <summary>
        /// 处理高优先级接纳时被淘汰的任务；生产环境可将释放转交给专用回收线程。
        /// </summary>
        /// <param name="items">被淘汰且仍由队列拥有的任务。</param>
        private void HandleEvictedItemsNoThrow(IEnumerable<QueueEntry> items)
        {
            if (items == null)
                return;

            foreach (QueueEntry entry in items)
            {
                T item = entry?.Item;
                try
                {
                    if (_evictedItemHandler == null)
                        item?.Dispose();
                    else
                        _evictedItemHandler(item, entry.EstimatedBytes);
                }
                catch (Exception ex)
                {
                    ReportDisposeExceptionNoThrow(ex);
                    try
                    {
                        item?.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        ReportDisposeExceptionNoThrow(disposeEx);
                    }
                }
            }
        }

        /// <summary>
        /// 逐个释放队列已经拥有的任务，单个释放异常不会阻止后续资源回收。
        /// </summary>
        /// <param name="items">需要释放的任务集合。</param>
        private void DisposeItemsNoThrow(IEnumerable<T> items)
        {
            if (items == null)
                return;

            foreach (T item in items)
            {
                try
                {
                    item?.Dispose();
                }
                catch (Exception ex)
                {
                    ReportDisposeExceptionNoThrow(ex);
                }
            }
        }

        /// <summary>
        /// 向可选观察器报告释放异常，观察器自身异常不会破坏队列状态。
        /// </summary>
        /// <param name="exception">资源释放异常。</param>
        private void ReportDisposeExceptionNoThrow(Exception exception)
        {
            try
            {
                _disposeExceptionHandler?.Invoke(exception);
            }
            catch
            {
            }
        }
    }
}
