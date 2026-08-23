using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 使用异步执行上下文传播当前工件，子任务、条件分支和组合模块会自然继承。
    /// </summary>
    public sealed class WorkpieceContextAccessor : IWorkpieceContextAccessor
    {
        /// <summary>当前异步执行链中的不可变上下文栈顶。</summary>
        private readonly AsyncLocal<ContextFrame> _currentFrame = new AsyncLocal<ContextFrame>();

        /// <summary>获取当前异步执行链关联的工件上下文。</summary>
        public WorkpieceExecutionContext Current => _currentFrame.Value?.Context;

        /// <summary>
        /// 把工件上下文压入当前执行链，并返回负责恢复上一层的幂等作用域。
        /// </summary>
        /// <param name="context">待传播的非空工件上下文。</param>
        /// <returns>释放后恢复上一层上下文的作用域。</returns>
        public IDisposable Push(WorkpieceExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ContextFrame previous = _currentFrame.Value;
            ContextFrame current = new ContextFrame(context, previous);
            _currentFrame.Value = current;
            return new ContextScope(this, current);
        }

        /// <summary>
        /// 严格按后进先出顺序恢复上一层；乱序释放直接失败，禁止恢复错误工件。
        /// </summary>
        /// <param name="frame">待弹出的上下文帧。</param>
        private void Pop(ContextFrame frame)
        {
            if (!ReferenceEquals(_currentFrame.Value, frame))
                throw new InvalidOperationException("工件上下文作用域必须按后进先出顺序释放。 ");

            _currentFrame.Value = frame.Previous;
        }

        /// <summary>不可变上下文栈帧。</summary>
        private sealed class ContextFrame
        {
            /// <summary>
            /// 创建上下文栈帧。
            /// </summary>
            /// <param name="context">当前工件上下文。</param>
            /// <param name="previous">上一层栈帧。</param>
            public ContextFrame(WorkpieceExecutionContext context, ContextFrame previous)
            {
                Context = context;
                Previous = previous;
            }

            /// <summary>获取当前工件上下文。</summary>
            public WorkpieceExecutionContext Context { get; }

            /// <summary>获取上一层栈帧。</summary>
            public ContextFrame Previous { get; }

        }

        /// <summary>负责幂等恢复上一层上下文的作用域。</summary>
        private sealed class ContextScope : IDisposable
        {
            /// <summary>拥有当前栈帧的访问器；释放后原子清空。</summary>
            private WorkpieceContextAccessor _owner;

            /// <summary>本作用域压入的栈帧。</summary>
            private readonly ContextFrame _frame;

            /// <summary>作用域释放状态，1表示已经成功恢复上一层。</summary>
            private int _disposed;

            /// <summary>
            /// 创建上下文恢复作用域。
            /// </summary>
            /// <param name="owner">上下文访问器。</param>
            /// <param name="frame">本次压入的栈帧。</param>
            public ContextScope(WorkpieceContextAccessor owner, ContextFrame frame)
            {
                _owner = owner;
                _frame = frame;
            }

            /// <summary>幂等恢复上一层上下文。</summary>
            public void Dispose()
            {
                if (Volatile.Read(ref _disposed) == 1)
                    return;

                WorkpieceContextAccessor owner = _owner;
                if (owner == null)
                    return;

                owner.Pop(_frame);
                Volatile.Write(ref _disposed, 1);
                Interlocked.Exchange(ref _owner, null);
            }
        }
    }

    /// <summary>
    /// 为每个规范化生产顺序域生成会话内严格递增的工件身份。
    /// </summary>
    public sealed class WorkpieceIdentityGenerator
    {
        /// <summary>保护会话纪元和各顺序域计数器的一致性锁。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>当前生产会话纪元。</summary>
        private Guid _sessionEpoch = Guid.NewGuid();

        /// <summary>各规范化顺序域已经分配的最大序号。</summary>
        private readonly Dictionary<string, long> _domainSequences =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        /// <summary>获取当前生产会话纪元。</summary>
        public Guid SessionEpoch
        {
            get
            {
                lock (_syncRoot)
                    return _sessionEpoch;
            }
        }

        /// <summary>
        /// 为指定生产顺序域分配下一个工件身份。
        /// </summary>
        /// <param name="orderDomain">生产线或工位顺序域。</param>
        /// <returns>当前会话内严格递增的工件身份。</returns>
        public WorkpieceIdentity Next(string orderDomain)
        {
            string normalizedDomain = NormalizeDomain(orderDomain);
            lock (_syncRoot)
            {
                _domainSequences.TryGetValue(normalizedDomain, out long current);
                if (current == long.MaxValue)
                    throw new OverflowException($"生产顺序域{normalizedDomain}的工件序号已经耗尽。请停止生产并建立新会话。");

                long next = current + 1L;
                _domainSequences[normalizedDomain] = next;
                return new WorkpieceIdentity(_sessionEpoch, normalizedDomain, next);
            }
        }

        /// <summary>
        /// 在确认旧会话完全排空后建立新生产会话，所有顺序域从1重新开始。
        /// </summary>
        /// <returns>新生产会话纪元。</returns>
        internal Guid ResetSession()
        {
            lock (_syncRoot)
            {
                _sessionEpoch = Guid.NewGuid();
                _domainSequences.Clear();
                return _sessionEpoch;
            }
        }

        /// <summary>
        /// 规范生产顺序域，防止大小写或首尾空白拆分计数器。
        /// </summary>
        /// <param name="orderDomain">原始顺序域。</param>
        /// <returns>规范化顺序域。</returns>
        private static string NormalizeDomain(string orderDomain)
        {
            if (string.IsNullOrWhiteSpace(orderDomain))
                throw new ArgumentException("生产顺序域不能为空。", nameof(orderDomain));

            return orderDomain.Trim().ToUpperInvariant();
        }
    }

    /// <summary>单流程工件入口满载时抛出的明确准入异常。</summary>
    public sealed class ProcessWorkpieceQueueFullException : InvalidOperationException
    {
        /// <summary>
        /// 创建包含流程身份和容量的满载异常。
        /// </summary>
        /// <param name="processKey">流程稳定身份。</param>
        /// <param name="capacity">队列总容量，包含活动工件。</param>
        public ProcessWorkpieceQueueFullException(string processKey, int capacity)
            : base($"流程{processKey}的工件队列已满，容量={capacity}。必须暂停触发并报警，禁止先采图后丢弃。")
        {
            ProcessKey = processKey;
            Capacity = capacity;
        }

        /// <summary>获取发生满载的流程稳定身份。</summary>
        public string ProcessKey { get; }

        /// <summary>获取队列总容量。</summary>
        public int Capacity { get; }
    }

    /// <summary>单流程工件队列的只读运行快照。</summary>
    public sealed class ProcessWorkpieceGateSnapshot
    {
        /// <summary>获取队列总容量，包含活动工件。</summary>
        public int Capacity { get; internal set; }

        /// <summary>获取当前是否有活动工件。</summary>
        public bool HasActiveWorkpiece { get; internal set; }

        /// <summary>获取当前排队等待工件数。</summary>
        public int WaitingCount { get; internal set; }

        /// <summary>获取历史最大占用数量。</summary>
        public int PeakOccupancy { get; internal set; }

        /// <summary>获取满载拒绝次数。</summary>
        public long RejectedFullCount { get; internal set; }
    }

    /// <summary>单流程工件独占执行租约。</summary>
    public interface IProcessWorkpieceLease : IDisposable
    {
        /// <summary>获取流程稳定身份。</summary>
        string ProcessKey { get; }

        /// <summary>获取租约是否已幂等释放。</summary>
        bool IsReleased { get; }
    }

    /// <summary>
    /// 每个流程一个严格FIFO、数量有界、并发固定为1的工件执行入口。
    /// </summary>
    public sealed class ProcessWorkpieceGate : IDisposable
    {
        /// <summary>默认总容量，包含一个活动工件和等待工件。</summary>
        public const int DefaultCapacity = 8;

        /// <summary>保护活动状态、等待链表和统计的一致性锁。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>严格按到达顺序排列的等待工件。</summary>
        private readonly LinkedList<GateWaiter> _waiters = new LinkedList<GateWaiter>();

        /// <summary>流程稳定身份。</summary>
        private readonly string _processKey;

        /// <summary>总容量，包含活动工件。</summary>
        private readonly int _capacity;

        /// <summary>当前是否已有活动工件。</summary>
        private bool _hasActiveWorkpiece;

        /// <summary>历史最大占用数量。</summary>
        private int _peakOccupancy;

        /// <summary>满载拒绝次数。</summary>
        private long _rejectedFullCount;

        /// <summary>是否已经停止并释放。</summary>
        private bool _disposed;

        /// <summary>
        /// 创建单流程工件执行入口。
        /// </summary>
        /// <param name="processKey">流程稳定身份。</param>
        /// <param name="capacity">总容量，包含活动工件。</param>
        public ProcessWorkpieceGate(string processKey, int capacity = DefaultCapacity)
        {
            if (string.IsNullOrWhiteSpace(processKey))
                throw new ArgumentException("流程稳定身份不能为空。", nameof(processKey));
            if (capacity < 2 || capacity > 1024)
                throw new ArgumentOutOfRangeException(nameof(capacity), "工件队列容量必须在2到1024之间。 ");

            _processKey = processKey.Trim();
            _capacity = capacity;
        }

        /// <summary>
        /// 严格按到达顺序取得单流程执行权；满载时在任何流程副作用开始前失败。
        /// </summary>
        /// <param name="cancellationToken">只取消排队等待，不中断已经取得的执行权。</param>
        /// <returns>必须幂等释放的执行租约。</returns>
        public Task<IProcessWorkpieceLease> EnterAsync(CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_hasActiveWorkpiece)
                {
                    _hasActiveWorkpiece = true;
                    UpdatePeakOccupancyUnderLock();
                    return Task.FromResult<IProcessWorkpieceLease>(new GateLease(this, _processKey));
                }

                if (1 + _waiters.Count >= _capacity)
                {
                    _rejectedFullCount++;
                    throw new ProcessWorkpieceQueueFullException(_processKey, _capacity);
                }

                GateWaiter waiter = new GateWaiter(this, cancellationToken);
                waiter.Node = _waiters.AddLast(waiter);
                UpdatePeakOccupancyUnderLock();
                waiter.RegisterCancellation();
                return waiter.Completion.Task;
            }
        }

        /// <summary>获取当前队列状态和累计满载统计。</summary>
        /// <returns>不暴露内部可变集合的只读值快照。</returns>
        public ProcessWorkpieceGateSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return new ProcessWorkpieceGateSnapshot
                {
                    Capacity = _capacity,
                    HasActiveWorkpiece = _hasActiveWorkpiece,
                    WaitingCount = _waiters.Count,
                    PeakOccupancy = _peakOccupancy,
                    RejectedFullCount = _rejectedFullCount
                };
            }
        }

        /// <summary>停止入口并取消全部尚未取得执行权的等待工件。</summary>
        public void Dispose()
        {
            List<GateWaiter> cancelledWaiters = new List<GateWaiter>();
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                while (_waiters.Count > 0)
                {
                    GateWaiter waiter = _waiters.First.Value;
                    _waiters.RemoveFirst();
                    waiter.Node = null;
                    cancelledWaiters.Add(waiter);
                }
            }

            foreach (GateWaiter waiter in cancelledWaiters)
                waiter.CancelBecauseGateStopped();
        }

        /// <summary>
        /// 释放当前活动工件，并只唤醒严格FIFO队首的有效等待者。
        /// </summary>
        private void Release()
        {
            GateWaiter grantedWaiter = null;
            lock (_syncRoot)
            {
                if (!_hasActiveWorkpiece)
                    return;

                while (_waiters.Count > 0)
                {
                    GateWaiter candidate = _waiters.First.Value;
                    _waiters.RemoveFirst();
                    candidate.Node = null;
                    if (candidate.TryGrant())
                    {
                        grantedWaiter = candidate;
                        break;
                    }
                }

                if (grantedWaiter == null)
                    _hasActiveWorkpiece = false;
            }

            grantedWaiter?.CompleteGrant(new GateLease(this, _processKey));
        }

        /// <summary>
        /// 从等待链表原子移除被取消的等待者。
        /// </summary>
        /// <param name="waiter">待取消等待者。</param>
        private void CancelWaiter(GateWaiter waiter)
        {
            bool removed = false;
            lock (_syncRoot)
            {
                if (waiter.Node != null && waiter.Node.List == _waiters)
                {
                    _waiters.Remove(waiter.Node);
                    waiter.Node = null;
                    removed = waiter.TryCancel();
                }
            }

            if (removed)
                waiter.CompleteCancellation();
        }

        /// <summary>在持锁时更新历史最大占用。</summary>
        private void UpdatePeakOccupancyUnderLock()
        {
            int occupancy = (_hasActiveWorkpiece ? 1 : 0) + _waiters.Count;
            if (occupancy > _peakOccupancy)
                _peakOccupancy = occupancy;
        }

        /// <summary>入口已停止时抛出对象释放异常。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ProcessWorkpieceGate));
        }

        /// <summary>等待取得流程执行权的单个队列项。</summary>
        private sealed class GateWaiter
        {
            /// <summary>等待状态。</summary>
            private int _state;

            /// <summary>所属执行入口。</summary>
            private readonly ProcessWorkpieceGate _owner;

            /// <summary>调用方等待取消令牌。</summary>
            private readonly CancellationToken _cancellationToken;

            /// <summary>取消注册。</summary>
            private CancellationTokenRegistration _cancellationRegistration;

            /// <summary>
            /// 创建等待队列项。
            /// </summary>
            /// <param name="owner">所属执行入口。</param>
            /// <param name="cancellationToken">等待取消令牌。</param>
            public GateWaiter(ProcessWorkpieceGate owner, CancellationToken cancellationToken)
            {
                _owner = owner;
                _cancellationToken = cancellationToken;
                Completion = new TaskCompletionSource<IProcessWorkpieceLease>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>获取异步完成源。</summary>
            public TaskCompletionSource<IProcessWorkpieceLease> Completion { get; }

            /// <summary>获取或设置当前链表节点。</summary>
            public LinkedListNode<GateWaiter> Node { get; set; }

            /// <summary>在离开入口锁后登记取消回调。</summary>
            public void RegisterCancellation()
            {
                if (_cancellationToken.CanBeCanceled)
                {
                    CancellationTokenRegistration registration = _cancellationToken.Register(
                        state => ((GateWaiter)state)._owner.CancelWaiter((GateWaiter)state),
                        this);
                    _cancellationRegistration = registration;
                    if (Volatile.Read(ref _state) != 0)
                        _cancellationRegistration.Dispose();
                }
            }

            /// <summary>尝试把等待状态切换为已授予。</summary>
            public bool TryGrant()
            {
                return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            }

            /// <summary>尝试把等待状态切换为已取消。</summary>
            public bool TryCancel()
            {
                return Interlocked.CompareExchange(ref _state, 2, 0) == 0;
            }

            /// <summary>完成执行权授予并释放取消注册。</summary>
            /// <param name="lease">已授予租约。</param>
            public void CompleteGrant(IProcessWorkpieceLease lease)
            {
                _cancellationRegistration.Dispose();
                Completion.TrySetResult(lease);
            }

            /// <summary>完成调用方主动取消。</summary>
            public void CompleteCancellation()
            {
                _cancellationRegistration.Dispose();
                Completion.TrySetCanceled(_cancellationToken);
            }

            /// <summary>入口停止时取消尚未授予的等待。</summary>
            public void CancelBecauseGateStopped()
            {
                if (!TryCancel())
                    return;

                _cancellationRegistration.Dispose();
                Completion.TrySetException(new ObjectDisposedException(nameof(ProcessWorkpieceGate)));
            }
        }

        /// <summary>幂等释放单流程执行权的租约。</summary>
        private sealed class GateLease : IProcessWorkpieceLease
        {
            /// <summary>所属执行入口；释放后原子清空。</summary>
            private ProcessWorkpieceGate _owner;

            /// <summary>
            /// 创建执行租约。
            /// </summary>
            /// <param name="owner">所属执行入口。</param>
            /// <param name="processKey">流程稳定身份。</param>
            public GateLease(ProcessWorkpieceGate owner, string processKey)
            {
                _owner = owner;
                ProcessKey = processKey;
            }

            /// <summary>获取流程稳定身份。</summary>
            public string ProcessKey { get; }

            /// <summary>获取租约是否已幂等释放。</summary>
            public bool IsReleased => Volatile.Read(ref _owner) == null;

            /// <summary>幂等释放执行权并推进严格FIFO队首。</summary>
            public void Dispose()
            {
                ProcessWorkpieceGate owner = Interlocked.Exchange(ref _owner, null);
                owner?.Release();
            }
        }
    }
}
