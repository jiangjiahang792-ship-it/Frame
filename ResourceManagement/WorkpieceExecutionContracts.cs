using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 生产会话内的工件身份，由会话纪元和严格递增序号共同确定。
    /// </summary>
    public struct WorkpieceIdentity : IEquatable<WorkpieceIdentity>, IComparable<WorkpieceIdentity>
    {
        /// <summary>当前生产会话的唯一纪元。</summary>
        private readonly Guid _sessionEpoch;

        /// <summary>身份所属的规范化生产顺序域。</summary>
        private readonly string _orderDomain;

        /// <summary>当前会话内从1开始递增的工件序号。</summary>
        private readonly long _sequence;

        /// <summary>
        /// 创建不可变工件身份。
        /// </summary>
        /// <param name="sessionEpoch">生产会话唯一纪元。</param>
        /// <param name="orderDomain">身份所属的生产顺序域。</param>
        /// <param name="sequence">会话和顺序域内大于0的递增序号。</param>
        public WorkpieceIdentity(Guid sessionEpoch, string orderDomain, long sequence)
        {
            if (sessionEpoch == Guid.Empty)
                throw new ArgumentException("生产会话纪元不能为空。", nameof(sessionEpoch));
            if (string.IsNullOrWhiteSpace(orderDomain))
                throw new ArgumentException("生产顺序域不能为空。", nameof(orderDomain));
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "工件序号必须大于0。");

            _sessionEpoch = sessionEpoch;
            _orderDomain = orderDomain.Trim().ToUpperInvariant();
            _sequence = sequence;
        }

        /// <summary>获取生产会话唯一纪元。</summary>
        public Guid SessionEpoch => _sessionEpoch;

        /// <summary>获取身份所属的规范化生产顺序域。</summary>
        public string OrderDomain => _orderDomain;

        /// <summary>获取会话和顺序域内工件序号。</summary>
        public long Sequence => _sequence;

        /// <summary>
        /// 比较同一生产会话内两个工件的先后顺序。
        /// </summary>
        /// <param name="other">待比较的工件身份。</param>
        /// <returns>小于0表示当前工件更早，0表示相同，大于0表示当前工件更晚。</returns>
        public int CompareTo(WorkpieceIdentity other)
        {
            if (_sessionEpoch != other._sessionEpoch ||
                !string.Equals(_orderDomain, other._orderDomain, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("不同生产会话或顺序域的工件不能比较先后顺序。");
            }

            return _sequence.CompareTo(other._sequence);
        }

        /// <summary>
        /// 判断两个工件身份是否完全相同。
        /// </summary>
        /// <param name="other">待比较的工件身份。</param>
        /// <returns>会话纪元和序号都相同时返回true。</returns>
        public bool Equals(WorkpieceIdentity other)
        {
            return _sessionEpoch == other._sessionEpoch &&
                string.Equals(_orderDomain, other._orderDomain, StringComparison.Ordinal) &&
                _sequence == other._sequence;
        }

        /// <summary>
        /// 判断对象是否表示同一工件身份。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>对象类型及身份均相同时返回true。</returns>
        public override bool Equals(object obj)
        {
            return obj is WorkpieceIdentity && Equals((WorkpieceIdentity)obj);
        }

        /// <summary>
        /// 获取工件身份的稳定哈希码。
        /// </summary>
        /// <returns>由会话纪元和工件序号生成的哈希码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = _sessionEpoch.GetHashCode();
                hashCode = (hashCode * 397) ^ (_orderDomain == null ? 0 : _orderDomain.GetHashCode());
                return (hashCode * 397) ^ _sequence.GetHashCode();
            }
        }

        /// <summary>
        /// 获取便于日志关联的工件身份文本。
        /// </summary>
        /// <returns>会话纪元和工件序号文本。</returns>
        public override string ToString()
        {
            return $"{_orderDomain}/{_sessionEpoch:N}/{_sequence}";
        }

        /// <summary>
        /// 判断两个工件身份是否相同。
        /// </summary>
        /// <param name="left">左侧工件身份。</param>
        /// <param name="right">右侧工件身份。</param>
        /// <returns>身份相同返回true。</returns>
        public static bool operator ==(WorkpieceIdentity left, WorkpieceIdentity right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个工件身份是否不同。
        /// </summary>
        /// <param name="left">左侧工件身份。</param>
        /// <param name="right">右侧工件身份。</param>
        /// <returns>身份不同返回true。</returns>
        public static bool operator !=(WorkpieceIdentity left, WorkpieceIdentity right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 工件完整流程的唯一终态。
    /// </summary>
    public enum WorkpieceTerminalState
    {
        /// <summary>流程仍在运行或等待。</summary>
        Active = 0,

        /// <summary>流程正常完成。</summary>
        Succeeded = 1,

        /// <summary>流程按业务判定正常结束，但产品判定不合格。</summary>
        BusinessRejected = 2,

        /// <summary>流程因未处理异常失败。</summary>
        Faulted = 3,

        /// <summary>流程收到取消请求后结束。</summary>
        Cancelled = 4,

        /// <summary>流程因等待或执行超时结束。</summary>
        TimedOut = 5,

        /// <summary>流程因生产停机结束。</summary>
        Stopped = 6
    }

    /// <summary>
    /// 已准入工件上下文的在途流程分支租约，重复释放必须无害。
    /// </summary>
    internal interface IWorkpieceExecutionLease : IDisposable
    {
        /// <summary>获取租约所属工件身份。</summary>
        WorkpieceIdentity Identity { get; }

        /// <summary>获取租约是否已经释放。</summary>
        bool IsReleased { get; }

        /// <summary>
        /// 以明确流程终态结束本分支并幂等释放租约。
        /// </summary>
        /// <param name="terminalState">本分支执行终态。</param>
        void Complete(WorkpieceTerminalState terminalState);
    }

    /// <summary>
    /// 单个工件运行完整可编辑流程时携带的隔离上下文。
    /// </summary>
    public sealed class WorkpieceExecutionContext
    {
        /// <summary>保护信号意图预约、在途释放和唯一终态切换不可交错的实例锁。</summary>
        private readonly object _stateSyncRoot = new object();

        /// <summary>等待全部在途信号结束后发布唯一终态的异步完成源。</summary>
        private readonly TaskCompletionSource<WorkpieceTerminalState> _completionSource =
            new TaskCompletionSource<WorkpieceTerminalState>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>已经预约且尚未从协调器返回的信号意图ID。</summary>
        private readonly HashSet<Guid> _pendingSignalIntentIds = new HashSet<Guid>();

        /// <summary>已经准入且尚未退出的异步流程分支租约ID。</summary>
        private readonly HashSet<Guid> _pendingExecutionLeaseIds = new HashSet<Guid>();

        /// <summary>已分配的实际外发信号序号。</summary>
        private long _signalSequence;

        /// <summary>当前工件唯一终态的整数值。</summary>
        private int _terminalState;

        /// <summary>是否已经请求封口，1表示禁止再预约新信号。</summary>
        private int _completionRequested;

        /// <summary>全部在途信号结束后要发布的目标终态。</summary>
        private WorkpieceTerminalState _requestedTerminalState;

        /// <summary>由已完成外发结果累计得到的最严重工件终态；Active表示尚无信号故障。</summary>
        private WorkpieceTerminalState _signalTerminalOverride;

        /// <summary>由已完成流程分支累计得到的最严重工件终态；Active表示尚无分支故障。</summary>
        private WorkpieceTerminalState _executionTerminalOverride;

        /// <summary>
        /// 创建不可变运行元数据和可并发提交的终态容器。
        /// </summary>
        /// <param name="identity">工件身份。</param>
        /// <param name="flowRevision">工件入队时绑定的流程快照版本。</param>
        /// <param name="orderDomain">必须保持生产顺序的业务域。</param>
        /// <param name="triggerSource">本轮触发来源。</param>
        /// <param name="createdAtUtc">工件创建的UTC时间。</param>
        /// <param name="cancellationToken">本轮取消令牌。</param>
        public WorkpieceExecutionContext(
            WorkpieceIdentity identity,
            long flowRevision,
            string orderDomain,
            string triggerSource,
            DateTime createdAtUtc,
            CancellationToken cancellationToken)
        {
            if (identity.SessionEpoch == Guid.Empty ||
                string.IsNullOrWhiteSpace(identity.OrderDomain) ||
                identity.Sequence <= 0L)
            {
                throw new ArgumentException("必须提供有效工件身份。", nameof(identity));
            }
            if (flowRevision <= 0L)
                throw new ArgumentOutOfRangeException(nameof(flowRevision), "流程快照版本必须大于0。");
            if (string.IsNullOrWhiteSpace(orderDomain))
                throw new ArgumentException("生产顺序域不能为空。", nameof(orderDomain));
            if (string.IsNullOrWhiteSpace(triggerSource))
                throw new ArgumentException("触发来源不能为空。", nameof(triggerSource));
            if (createdAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("工件创建时间必须使用UTC。", nameof(createdAtUtc));

            string normalizedOrderDomain = NormalizeKey(orderDomain, nameof(orderDomain));
            if (!string.Equals(identity.OrderDomain, normalizedOrderDomain, StringComparison.Ordinal))
                throw new ArgumentException("工件身份顺序域必须与执行上下文顺序域一致。", nameof(orderDomain));

            Identity = identity;
            FlowRevision = flowRevision;
            OrderDomain = normalizedOrderDomain;
            TriggerSource = triggerSource.Trim();
            CreatedAtUtc = createdAtUtc;
            CancellationToken = cancellationToken;
            _terminalState = (int)WorkpieceTerminalState.Active;
            _signalTerminalOverride = WorkpieceTerminalState.Active;
            _executionTerminalOverride = WorkpieceTerminalState.Active;
        }

        /// <summary>获取本轮工件身份。</summary>
        public WorkpieceIdentity Identity { get; }

        /// <summary>获取本轮绑定的不可变流程快照版本。</summary>
        public long FlowRevision { get; }

        /// <summary>获取必须保持工件顺序的生产域。</summary>
        public string OrderDomain { get; }

        /// <summary>获取本轮触发来源。</summary>
        public string TriggerSource { get; }

        /// <summary>获取工件创建UTC时间。</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>获取本轮取消令牌。</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>获取当前终态；返回Active表示尚未封口。</summary>
        public WorkpieceTerminalState TerminalState =>
            (WorkpieceTerminalState)Volatile.Read(ref _terminalState);

        /// <summary>获取是否已经请求封口并禁止产生新的信号意图。</summary>
        public bool IsCompletionRequested => Volatile.Read(ref _completionRequested) == 1;

        /// <summary>获取等待全部在途信号结束并发布唯一终态的任务。</summary>
        public Task<WorkpieceTerminalState> Completion => _completionSource.Task;

        /// <summary>
        /// 原子预约信号序号和意图ID，并立即通过唯一协调器提交。
        /// </summary>
        /// <param name="coordinator">当前生产会话的有序信号协调器。</param>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <param name="nodeKind">白名单外发节点类别。</param>
        /// <param name="endpointKey">物理设备串行化键。</param>
        /// <param name="sendAsync">已复制动态值且不再读取节点共享状态的发送动作。</param>
        /// <param name="waitingCancellationToken">只取消发送开始前等待，不得中断已经开始的外部副作用。</param>
        /// <returns>协调器返回的明确发送结果。</returns>
        public Task<OrderedSignalSendResult> SubmitOrderedSignalAsync(
            IOrderedSignalCoordinator coordinator,
            string nodePath,
            OrderedSignalNodeKind nodeKind,
            string endpointKey,
            Func<Task<OrderedSignalSendResult>> sendAsync,
            CancellationToken waitingCancellationToken)
        {
            if (coordinator == null)
                throw new ArgumentNullException(nameof(coordinator));
            if (sendAsync == null)
                throw new ArgumentNullException(nameof(sendAsync));

            OrderedSignalIntent intent = ReserveSignalIntent(
                nodePath,
                nodeKind,
                endpointKey,
                sendAsync);
            return SubmitAndReleaseIntentAsync(coordinator, intent, waitingCancellationToken);
        }

        /// <summary>
        /// 尝试把当前工件从活动状态原子切换到唯一终态。
        /// </summary>
        /// <param name="terminalState">不允许为Active的目标终态。</param>
        /// <returns>本次调用成功建立唯一封口请求返回true；已经请求或已经封口返回false。</returns>
        public bool TryComplete(WorkpieceTerminalState terminalState)
        {
            if (terminalState == WorkpieceTerminalState.Active)
                throw new ArgumentException("工件终态不能设置为Active。", nameof(terminalState));
            if (!Enum.IsDefined(typeof(WorkpieceTerminalState), terminalState))
                throw new ArgumentOutOfRangeException(nameof(terminalState), "未知的工件终态。");

            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active || _completionRequested == 1)
                    return false;

                _requestedTerminalState = terminalState;
                Volatile.Write(ref _completionRequested, 1);
                TryPublishTerminalStateUnderLock();
                return true;
            }
        }

        /// <summary>把尚未发布的封口请求升级为更严重终态，供框架不可恢复故障使用。</summary>
        /// <param name="terminalState">需要合并的明确终态。</param>
        /// <returns>终态尚未发布且升级请求已合并时返回true。</returns>
        internal bool TryEscalateTerminalState(WorkpieceTerminalState terminalState)
        {
            if (terminalState == WorkpieceTerminalState.Active ||
                !Enum.IsDefined(typeof(WorkpieceTerminalState), terminalState))
            {
                throw new ArgumentOutOfRangeException(nameof(terminalState), "升级终态必须是明确终态。");
            }

            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active)
                    return false;
                _requestedTerminalState = GetMoreSevereTerminalState(
                    _completionRequested == 0
                        ? WorkpieceTerminalState.Active
                        : _requestedTerminalState,
                    terminalState);
                Volatile.Write(ref _completionRequested, 1);
                TryPublishTerminalStateUnderLock();
                return true;
            }
        }

        /// <summary>
        /// 在封口锁内原子取得一个流程分支在途租约，堵住继承检查与父工件封口之间的竞态。
        /// </summary>
        /// <param name="lease">成功时返回必须释放的分支租约。</param>
        /// <returns>工件仍活动、未请求封口且未取消时返回true。</returns>
        internal bool TryAcquireExecutionLease(out IWorkpieceExecutionLease lease)
        {
            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active ||
                    _completionRequested == 1 ||
                    CancellationToken.IsCancellationRequested)
                {
                    lease = null;
                    return false;
                }

                Guid leaseId = Guid.NewGuid();
                _pendingExecutionLeaseIds.Add(leaseId);
                lease = new WorkpieceExecutionLease(this, leaseId);
                return true;
            }
        }

        /// <summary>
        /// 幂等释放在途流程分支，并在最后一个分支和信号都退出后尝试发布终态。
        /// </summary>
        /// <param name="leaseId">上下文分配的分支租约ID。</param>
        private void ReleaseExecutionLease(Guid leaseId, WorkpieceTerminalState terminalState)
        {
            if (terminalState == WorkpieceTerminalState.Active ||
                !Enum.IsDefined(typeof(WorkpieceTerminalState), terminalState))
            {
                throw new ArgumentOutOfRangeException(nameof(terminalState), "流程分支必须提交明确终态。");
            }

            lock (_stateSyncRoot)
            {
                if (!_pendingExecutionLeaseIds.Remove(leaseId))
                    return;
                _executionTerminalOverride = GetMoreSevereTerminalState(
                    _executionTerminalOverride,
                    terminalState == WorkpieceTerminalState.Succeeded
                        ? WorkpieceTerminalState.Active
                        : terminalState);
                TryPublishTerminalStateUnderLock();
            }
        }

        /// <summary>
        /// 在同一把实例锁内预约不可伪造的信号序号、意图ID和在途名额。
        /// </summary>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <param name="nodeKind">白名单外发节点类别。</param>
        /// <param name="endpointKey">物理设备串行化键。</param>
        /// <param name="sendAsync">已经冻结动态值的发送动作。</param>
        /// <returns>只能交给协调器的信号意图。</returns>
        internal OrderedSignalIntent ReserveSignalIntent(
            string nodePath,
            OrderedSignalNodeKind nodeKind,
            string endpointKey,
            Func<Task<OrderedSignalSendResult>> sendAsync)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("节点路径不能为空。", nameof(nodePath));
            if (!Enum.IsDefined(typeof(OrderedSignalNodeKind), nodeKind))
                throw new ArgumentOutOfRangeException(nameof(nodeKind), "节点不属于有序外发白名单。");
            if (sendAsync == null)
                throw new ArgumentNullException(nameof(sendAsync));

            string normalizedEndpointKey = NormalizeKey(endpointKey, nameof(endpointKey));
            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active || _completionRequested == 1)
                    throw new InvalidOperationException("工件已经请求封口，不能继续创建外发信号意图。");

                Guid intentId = Guid.NewGuid();
                long signalSequence = ++_signalSequence;
                _pendingSignalIntentIds.Add(intentId);
                return new OrderedSignalIntent(
                    intentId,
                    this,
                    signalSequence,
                    nodePath.Trim(),
                    nodeKind,
                    normalizedEndpointKey,
                    sendAsync);
            }
        }

        /// <summary>
        /// 等待协调器返回，并在成功、拒绝、未知、异常和取消路径原子记录发送结论。
        /// </summary>
        /// <param name="coordinator">有序信号协调器。</param>
        /// <param name="intent">已预约意图。</param>
        /// <param name="waitingCancellationToken">发送开始前等待取消令牌。</param>
        /// <returns>明确发送结果。</returns>
        private async Task<OrderedSignalSendResult> SubmitAndReleaseIntentAsync(
            IOrderedSignalCoordinator coordinator,
            OrderedSignalIntent intent,
            CancellationToken waitingCancellationToken)
        {
            OrderedSignalSendResult result;
            try
            {
                result = await coordinator.SubmitAsync(intent, waitingCancellationToken).ConfigureAwait(false);
                if (result == null)
                {
                    result = new OrderedSignalSendResult(
                        OrderedSignalSendStatus.Unknown,
                        "有序信号协调器返回了空结果，无法判断设备副作用状态。");
                }
            }
            catch (OperationCanceledException)
            {
                // 只有协调器明确返回取消结果，才能证明副作用尚未开始且意图已永久移除。
                // 任意取消异常都可能来自设备适配器，必须按发送状态未知保守停机。
                FinalizeSignalIntent(intent.IntentId, OrderedSignalSendStatus.Unknown);
                throw;
            }
            catch
            {
                FinalizeSignalIntent(intent.IntentId, OrderedSignalSendStatus.Unknown);
                throw;
            }

            FinalizeSignalIntent(intent.IntentId, result.Status);
            return result;
        }

        /// <summary>
        /// 幂等终结已经从协调器返回的在途信号，原子合并发送结论并在最后一个信号结束后发布终态。
        /// </summary>
        /// <param name="intentId">上下文预约的信号意图ID。</param>
        /// <param name="sendStatus">协调器保证不再迟到发送后的最终状态。</param>
        internal void FinalizeSignalIntent(Guid intentId, OrderedSignalSendStatus sendStatus)
        {
            if (!Enum.IsDefined(typeof(OrderedSignalSendStatus), sendStatus))
                throw new ArgumentOutOfRangeException(nameof(sendStatus), "未知的外发确认状态。");

            lock (_stateSyncRoot)
            {
                if (!_pendingSignalIntentIds.Remove(intentId))
                    return;

                WorkpieceTerminalState signalTerminalState = GetSignalTerminalState(sendStatus);
                _signalTerminalOverride = GetMoreSevereTerminalState(
                    _signalTerminalOverride,
                    signalTerminalState);
                TryPublishTerminalStateUnderLock();
            }
        }

        /// <summary>
        /// 在持有状态锁时检查封口请求和在途信号，并发布唯一终态。
        /// </summary>
        private void TryPublishTerminalStateUnderLock()
        {
            if (_completionRequested != 1 ||
                _pendingSignalIntentIds.Count != 0 ||
                _pendingExecutionLeaseIds.Count != 0 ||
                _terminalState != (int)WorkpieceTerminalState.Active)
            {
                return;
            }

            WorkpieceTerminalState finalTerminalState = GetMoreSevereTerminalState(
                _requestedTerminalState,
                _signalTerminalOverride);
            finalTerminalState = GetMoreSevereTerminalState(
                finalTerminalState,
                _executionTerminalOverride);
            Volatile.Write(ref _terminalState, (int)finalTerminalState);
            _completionSource.TrySetResult(finalTerminalState);
        }

        /// <summary>绑定单个在途流程分支的幂等租约。</summary>
        private sealed class WorkpieceExecutionLease : IWorkpieceExecutionLease
        {
            /// <summary>租约所属上下文。</summary>
            private WorkpieceExecutionContext _context;

            /// <summary>上下文分配的唯一租约ID。</summary>
            private readonly Guid _leaseId;

            /// <summary>
            /// 创建在途流程分支租约。
            /// </summary>
            /// <param name="context">租约所属上下文。</param>
            /// <param name="leaseId">唯一租约ID。</param>
            public WorkpieceExecutionLease(WorkpieceExecutionContext context, Guid leaseId)
            {
                _context = context ?? throw new ArgumentNullException(nameof(context));
                _leaseId = leaseId;
                Identity = context.Identity;
            }

            /// <summary>获取租约所属工件身份。</summary>
            public WorkpieceIdentity Identity { get; }

            /// <summary>获取租约是否已经释放。</summary>
            public bool IsReleased => Volatile.Read(ref _context) == null;

            /// <summary>以明确终态幂等结束本流程分支。</summary>
            /// <param name="terminalState">本分支执行终态。</param>
            public void Complete(WorkpieceTerminalState terminalState)
            {
                WorkpieceExecutionContext context = Interlocked.Exchange(ref _context, null);
                context?.ReleaseExecutionLease(_leaseId, terminalState);
            }

            /// <summary>未明确完成便释放时保守按停机终态结束分支。</summary>
            public void Dispose()
            {
                Complete(WorkpieceTerminalState.Stopped);
            }
        }

        /// <summary>
        /// 把明确发送状态转换为工件终态覆盖值。
        /// </summary>
        /// <param name="sendStatus">协调器最终发送状态。</param>
        /// <returns>成功发送返回Active；失败、未知或发送前取消返回对应终态。</returns>
        private static WorkpieceTerminalState GetSignalTerminalState(OrderedSignalSendStatus sendStatus)
        {
            switch (sendStatus)
            {
                case OrderedSignalSendStatus.DeviceAcknowledged:
                case OrderedSignalSendStatus.LocalCallCompleted:
                    return WorkpieceTerminalState.Active;
                case OrderedSignalSendStatus.Rejected:
                    return WorkpieceTerminalState.Faulted;
                case OrderedSignalSendStatus.CancelledBeforeStart:
                    return WorkpieceTerminalState.Cancelled;
                case OrderedSignalSendStatus.Unknown:
                default:
                    return WorkpieceTerminalState.Stopped;
            }
        }

        /// <summary>
        /// 按生产安全优先级合并流程终态与信号终态，防止失败被成功覆盖。
        /// </summary>
        /// <param name="left">左侧终态。</param>
        /// <param name="right">右侧终态。</param>
        /// <returns>更需要停止生产的一侧终态。</returns>
        private static WorkpieceTerminalState GetMoreSevereTerminalState(
            WorkpieceTerminalState left,
            WorkpieceTerminalState right)
        {
            return GetTerminalSeverity(right) > GetTerminalSeverity(left) ? right : left;
        }

        /// <summary>
        /// 获取终态合并时使用的生产安全等级。
        /// </summary>
        /// <param name="terminalState">待评估终态。</param>
        /// <returns>值越大表示越不能被普通流程结果覆盖。</returns>
        private static int GetTerminalSeverity(WorkpieceTerminalState terminalState)
        {
            switch (terminalState)
            {
                case WorkpieceTerminalState.Stopped:
                    return 6;
                case WorkpieceTerminalState.Faulted:
                    return 5;
                case WorkpieceTerminalState.TimedOut:
                    return 4;
                case WorkpieceTerminalState.Cancelled:
                    return 3;
                case WorkpieceTerminalState.BusinessRejected:
                    return 2;
                case WorkpieceTerminalState.Succeeded:
                    return 1;
                case WorkpieceTerminalState.Active:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 把顺序域和物理设备键规范为大小写不敏感的稳定比较值。
        /// </summary>
        /// <param name="value">原始键值。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <returns>去除首尾空白并转为大写的稳定键。</returns>
        internal static string NormalizeKey(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("顺序域或物理设备键不能为空。", parameterName);

            return value.Trim().ToUpperInvariant();
        }
    }

    /// <summary>
    /// 当前允许进入生产有序发送通道的外发节点类别。
    /// </summary>
    public enum OrderedSignalNodeKind
    {
        /// <summary>串口发送节点。</summary>
        ComSend = 1,

        /// <summary>相机IO节点。</summary>
        CameraIO = 2,

        /// <summary>PLC写入节点。</summary>
        PLCWrite = 3,

        /// <summary>Modbus写入节点。</summary>
        ModbusWrite = 4,

        /// <summary>订阅基础结果并发送至PLC或Modbus设备。</summary>
        ResultSend = 5
    }

    /// <summary>
    /// 设备发送动作可以被确认的结果状态。
    /// </summary>
    public enum OrderedSignalSendStatus
    {
        /// <summary>协议或设备以可记录凭据确认外部副作用已经生效。</summary>
        DeviceAcknowledged = 1,

        /// <summary>本地设备API正常返回，但协议不提供远端生效确认。</summary>
        LocalCallCompleted = 2,

        /// <summary>设备明确拒绝或确认发送失败。</summary>
        Rejected = 3,

        /// <summary>无法确认外部副作用是否已经发生。</summary>
        Unknown = 4,

        /// <summary>协调器确认意图在副作用开始前已永久移除，后续绝不会迟到发送。</summary>
        CancelledBeforeStart = 5
    }

    /// <summary>设备适配器确认尚未调用任何真实写入，允许归还端点但不伪装成发送成功。</summary>
    public sealed class DeviceSendNotStartedException : InvalidOperationException
    {
        /// <summary>创建明确无写入副作用的拒绝原因。</summary>
        public DeviceSendNotStartedException(string message) : base(message) { }
    }

    /// <summary>
    /// 单次外发动作返回的不可变确认结果。
    /// </summary>
    public sealed class OrderedSignalSendResult
    {
        /// <summary>
        /// 创建外发确认结果。
        /// </summary>
        /// <param name="status">设备确认状态。</param>
        /// <param name="message">用于诊断的简短说明。</param>
        /// <param name="confirmationEvidence">设备确认时必须保存的工件号、响应码或协议凭据。</param>
        public OrderedSignalSendResult(
            OrderedSignalSendStatus status,
            string message,
            string confirmationEvidence = null)
        {
            if (!Enum.IsDefined(typeof(OrderedSignalSendStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status), "未知的外发确认状态。");
            if (status == OrderedSignalSendStatus.DeviceAcknowledged &&
                string.IsNullOrWhiteSpace(confirmationEvidence))
            {
                throw new ArgumentException("设备确认成功必须提供可记录的确认凭据。", nameof(confirmationEvidence));
            }

            Status = status;
            Message = message ?? string.Empty;
            ConfirmationEvidence = confirmationEvidence == null
                ? string.Empty
                : confirmationEvidence.Trim();
        }

        /// <summary>获取设备确认状态。</summary>
        public OrderedSignalSendStatus Status { get; }

        /// <summary>获取诊断说明。</summary>
        public string Message { get; }

        /// <summary>获取设备确认时返回的工件号、响应码或协议凭据。</summary>
        public string ConfirmationEvidence { get; }
    }

    /// <summary>描述有序信号协调器发现的不可恢复顺序缺口。</summary>
    public sealed class OrderedSignalGapEventArgs : EventArgs
    {
        /// <summary>创建不可变顺序缺口诊断。</summary>
        public OrderedSignalGapEventArgs(
            string orderDomain,
            Guid sessionEpoch,
            long expectedWorkpieceSequence,
            long expectedSignalSequence,
            long firstWaitingWorkpieceSequence,
            long firstWaitingSignalSequence,
            string firstWaitingNodePath,
            int waitingIntentCount,
            int timeoutMilliseconds,
            DateTime detectedAtUtc,
            string message)
        {
            OrderDomain = WorkpieceExecutionContext.NormalizeKey(orderDomain, nameof(orderDomain));
            if (sessionEpoch == Guid.Empty)
                throw new ArgumentException("顺序缺口会话纪元不能为空。", nameof(sessionEpoch));
            if (expectedWorkpieceSequence <= 0L || expectedSignalSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedWorkpieceSequence), "期望工件和信号序号必须大于0。");
            if (firstWaitingWorkpieceSequence <= 0L || firstWaitingSignalSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(firstWaitingWorkpieceSequence), "首个等待工件和信号序号必须大于0。");
            if (string.IsNullOrWhiteSpace(firstWaitingNodePath))
                throw new ArgumentException("首个等待节点路径不能为空。", nameof(firstWaitingNodePath));
            if (waitingIntentCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(waitingIntentCount));
            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            if (detectedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("顺序缺口发现时间必须使用UTC。", nameof(detectedAtUtc));

            SessionEpoch = sessionEpoch;
            ExpectedWorkpieceSequence = expectedWorkpieceSequence;
            ExpectedSignalSequence = expectedSignalSequence;
            FirstWaitingWorkpieceSequence = firstWaitingWorkpieceSequence;
            FirstWaitingSignalSequence = firstWaitingSignalSequence;
            FirstWaitingNodePath = firstWaitingNodePath.Trim();
            WaitingIntentCount = waitingIntentCount;
            TimeoutMilliseconds = timeoutMilliseconds;
            DetectedAtUtc = detectedAtUtc;
            Message = message ?? string.Empty;
        }

        /// <summary>获取发生缺口的规范化生产顺序域。</summary>
        public string OrderDomain { get; }

        /// <summary>获取发生缺口的生产会话纪元。</summary>
        public Guid SessionEpoch { get; }

        /// <summary>获取协调器当时等待的工件序号。</summary>
        public long ExpectedWorkpieceSequence { get; }

        /// <summary>获取协调器当时等待的工件内信号序号。</summary>
        public long ExpectedSignalSequence { get; }

        /// <summary>获取已经排队的首个后续工件序号。</summary>
        public long FirstWaitingWorkpieceSequence { get; }

        /// <summary>获取已经排队的首个后续信号序号。</summary>
        public long FirstWaitingSignalSequence { get; }

        /// <summary>获取已经排队的首个后续节点路径。</summary>
        public string FirstWaitingNodePath { get; }

        /// <summary>获取缺口后方累计等待的意图数量。</summary>
        public int WaitingIntentCount { get; }

        /// <summary>获取本次生效的缺口超时时间。</summary>
        public int TimeoutMilliseconds { get; }

        /// <summary>获取UTC缺口发现时间。</summary>
        public DateTime DetectedAtUtc { get; }

        /// <summary>获取可直接记录的中文诊断消息。</summary>
        public string Message { get; }
    }

    /// <summary>
    /// 已冻结节点元数据和发送动作的单次有序信号意图。
    /// </summary>
    public sealed class OrderedSignalIntent
    {
        /// <summary>只有框架协调器可以调用的已冻结发送动作。</summary>
        private readonly Func<Task<OrderedSignalSendResult>> _sendAsync;

        /// <summary>
        /// 创建不可变有序信号意图。
        /// </summary>
        /// <param name="intentId">上下文原子预约的唯一意图ID。</param>
        /// <param name="context">当前工件执行上下文。</param>
        /// <param name="signalSequence">上下文分配的实际信号序号。</param>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <param name="nodeKind">白名单外发节点类别。</param>
        /// <param name="endpointKey">物理设备串行化键。</param>
        /// <param name="sendAsync">已经复制动态发送参数的设备动作。</param>
        internal OrderedSignalIntent(
            Guid intentId,
            WorkpieceExecutionContext context,
            long signalSequence,
            string nodePath,
            OrderedSignalNodeKind nodeKind,
            string endpointKey,
            Func<Task<OrderedSignalSendResult>> sendAsync)
        {
            if (intentId == Guid.Empty)
                throw new ArgumentException("信号意图ID不能为空。", nameof(intentId));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (signalSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(signalSequence), "信号序号必须大于0。");
            IntentId = intentId;
            Context = context;
            SignalSequence = signalSequence;
            NodePath = nodePath.Trim();
            NodeKind = nodeKind;
            EndpointKey = endpointKey;
            _sendAsync = sendAsync ?? throw new ArgumentNullException(nameof(sendAsync));
        }

        /// <summary>获取上下文原子预约的唯一意图ID。</summary>
        public Guid IntentId { get; }

        /// <summary>获取本意图所属工件上下文。</summary>
        public WorkpieceExecutionContext Context { get; }

        /// <summary>获取本工件内的实际信号序号。</summary>
        public long SignalSequence { get; }

        /// <summary>获取流程快照中的节点路径。</summary>
        public string NodePath { get; }

        /// <summary>获取白名单外发节点类别。</summary>
        public OrderedSignalNodeKind NodeKind { get; }

        /// <summary>获取物理设备串行化键。</summary>
        public string EndpointKey { get; }

        /// <summary>
        /// 由框架协调器在取得工件顺序权和物理端点租约后执行发送动作。
        /// </summary>
        /// <returns>明确发送结果。</returns>
        internal Task<OrderedSignalSendResult> ExecuteAsync()
        {
            return _sendAsync();
        }
    }

    /// <summary>
    /// 四类生产外发节点实现的可插拔能力接口。
    /// </summary>
    public interface IOrderedExternalSignalNode
    {
        /// <summary>获取当前外发节点类别。</summary>
        OrderedSignalNodeKind OrderedSignalKind { get; }

        /// <summary>获取当前配置所使用的物理设备串行化键。</summary>
        string OrderedSignalEndpointKey { get; }

        /// <summary>
        /// 在节点真正执行到达时复制动态参数，并只通过协调器提交一次信号。
        /// </summary>
        /// <param name="context">当前工件执行上下文。</param>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <param name="coordinator">当前生产会话的唯一有序协调器。</param>
        /// <param name="waitingCancellationToken">只作用于发送开始前等待的取消令牌。</param>
        /// <returns>明确发送结果。</returns>
        Task<OrderedSignalSendResult> SubmitOrderedSignalAsync(
            WorkpieceExecutionContext context,
            string nodePath,
            IOrderedSignalCoordinator coordinator,
            CancellationToken waitingCancellationToken);
    }

    /// <summary>
    /// 负责跨工件顺序和工件封口的信号协调器接口。
    /// </summary>
    public interface IOrderedSignalCoordinator
    {
        /// <summary>获取前序工件或信号缺口的主动报警时间。</summary>
        int SignalOrderGapTimeoutMs { get; }

        /// <summary>顺序缺口持续超过阈值并已阻断当前域时触发。</summary>
        event EventHandler<OrderedSignalGapEventArgs> OrderGapDetected;

        /// <summary>
        /// 登记工件身份，使后到达发送节点的工件不能越过尚未到达的前序工件。
        /// </summary>
        /// <param name="context">已准入的工件执行上下文。</param>
        void RegisterWorkpiece(WorkpieceExecutionContext context);

        /// <summary>
        /// 按生产顺序域、工件序号和信号序号提交设备动作。
        /// </summary>
        /// <param name="intent">已冻结的外发信号意图。</param>
        /// <param name="cancellationToken">只取消取得发送权之前的等待；副作用开始后不得提前返回取消。</param>
        /// <returns>设备确认结果。</returns>
        /// <remarks>同一IntentId重复提交必须返回首次结果或明确拒绝，禁止再次执行设备动作。任务以取消结束前必须从内部队列永久移除意图并保证后续绝不执行；副作用一旦开始，必须等待明确结果或端点冻结后返回Unknown，不能再抛取消。</remarks>
        Task<OrderedSignalSendResult> SubmitAsync(
            OrderedSignalIntent intent,
            CancellationToken cancellationToken);

        /// <summary>
        /// 请求封口并等待全部已预约信号返回，随后才允许推进下一工件。
        /// </summary>
        /// <param name="terminalState">本轮唯一目标终态。</param>
        /// <returns>在途信号全部结束且终态发布后完成的任务。</returns>
        Task CompleteWorkpieceAsync(
            WorkpieceExecutionContext context,
            WorkpieceTerminalState terminalState);
    }

    /// <summary>
    /// 同一物理设备端点的独占租约，重复释放必须无害且不得增加额外许可。
    /// </summary>
    public interface ISignalEndpointLease : IDisposable
    {
        /// <summary>获取本租约所属的规范化物理设备键。</summary>
        string EndpointKey { get; }

        /// <summary>获取租约是否已经幂等释放。</summary>
        bool IsReleased { get; }
    }

    /// <summary>
    /// 对同一物理设备连接实施互斥调用的可插拔串行器接口。
    /// </summary>
    public interface ISignalEndpointSerializer
    {
        /// <summary>
        /// 在指定物理设备键上取得独占租约；实际发送仍由框架协调器调用意图内部动作。
        /// </summary>
        /// <param name="endpointKey">已经规范化的物理设备键。</param>
        /// <param name="cancellationToken">只取消取得租约之前的等待。</param>
        /// <returns>非空且支持幂等释放的独占租约，必须在设备动作真正结束或端点冻结后释放。</returns>
        Task<ISignalEndpointLease> AcquireAsync(
            string endpointKey,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// 在统一节点执行入口传播当前工件上下文的访问接口。
    /// </summary>
    public interface IWorkpieceContextAccessor
    {
        /// <summary>获取当前异步执行链关联的工件上下文。</summary>
        WorkpieceExecutionContext Current { get; }

        /// <summary>
        /// 把工件上下文压入当前执行链，并返回负责恢复上一层上下文的作用域。
        /// </summary>
        /// <param name="context">待传播的工件上下文。</param>
        /// <returns>必须释放的上下文作用域。</returns>
        IDisposable Push(WorkpieceExecutionContext context);
    }
}
