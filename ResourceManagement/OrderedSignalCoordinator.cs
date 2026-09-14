using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>按物理设备键提供进程内独占调用租约。</summary>
    public sealed class SignalEndpointSerializer : ISignalEndpointSerializer, IDisposable
    {
        /// <summary>保护端点状态字典。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>每个规范化设备键对应一个独立信号量。</summary>
        private readonly Dictionary<string, EndpointState> _endpointStates =
            new Dictionary<string, EndpointState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>1表示串行器已经停止接收新租约。</summary>
        private int _disposed;

        /// <summary>异步取得指定物理端点的唯一执行租约。</summary>
        public async Task<ISignalEndpointLease> AcquireAsync(
            string endpointKey,
            CancellationToken cancellationToken)
        {
            string normalizedEndpointKey = WorkpieceExecutionContext.NormalizeKey(
                endpointKey,
                nameof(endpointKey));
            ThrowIfDisposed();

            EndpointState state;
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (!_endpointStates.TryGetValue(normalizedEndpointKey, out state))
                {
                    state = new EndpointState();
                    _endpointStates.Add(normalizedEndpointKey, state);
                }
            }

            await state.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0)
            {
                state.Semaphore.Release();
                throw new ObjectDisposedException(nameof(SignalEndpointSerializer));
            }
            return new EndpointLease(normalizedEndpointKey, state);
        }

        /// <summary>停止接收新端点租约；活动租约仍可正常幂等归还。</summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
        }

        /// <summary>串行器释放后拒绝新操作。</summary>
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(SignalEndpointSerializer));
        }

        /// <summary>单个物理端点的独占状态。</summary>
        private sealed class EndpointState
        {
            /// <summary>端点唯一许可。</summary>
            public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
        }

        /// <summary>绑定一次端点许可的幂等租约。</summary>
        private sealed class EndpointLease : ISignalEndpointLease
        {
            /// <summary>尚未归还时持有端点状态。</summary>
            private EndpointState _state;

            /// <summary>创建已经取得许可的端点租约。</summary>
            public EndpointLease(string endpointKey, EndpointState state)
            {
                EndpointKey = endpointKey;
                _state = state ?? throw new ArgumentNullException(nameof(state));
            }

            /// <summary>获取规范化物理设备键。</summary>
            public string EndpointKey { get; }

            /// <summary>获取许可是否已经归还。</summary>
            public bool IsReleased => Volatile.Read(ref _state) == null;

            /// <summary>幂等归还唯一端点许可。</summary>
            public void Dispose()
            {
                EndpointState state = Interlocked.Exchange(ref _state, null);
                state?.Semaphore.Release();
            }
        }
    }

    /// <summary>按生产顺序域、工件号和工件内信号号严格执行外部副作用。</summary>
    public sealed class OrderedSignalCoordinator : IOrderedSignalCoordinator, IDisposable
    {
        /// <summary>默认前序工件或信号缺口报警时间。</summary>
        public const int DefaultSignalOrderGapTimeoutMs = 30000;

        /// <summary>允许配置的最小缺口报警时间。</summary>
        public const int MinimumSignalOrderGapTimeoutMs = 1000;

        /// <summary>允许配置的最大缺口报警时间。</summary>
        public const int MaximumSignalOrderGapTimeoutMs = 600000;

        /// <summary>保护全部顺序域、意图和关闭状态。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>跨顺序域共享的物理设备串行器。</summary>
        private readonly ISignalEndpointSerializer _endpointSerializer;

        /// <summary>是否由协调器负责释放端点串行器。</summary>
        private readonly bool _ownsEndpointSerializer;

        /// <summary>后续信号已排队但前序缺失时允许等待的最长时间。</summary>
        private readonly int _signalOrderGapTimeoutMs;

        /// <summary>按规范化生产顺序域保存当前会话状态。</summary>
        private readonly Dictionary<string, OrderDomainState> _domainStates =
            new Dictionary<string, OrderDomainState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>按意图ID保存首次任务，重复提交不能再次发送。</summary>
        private readonly Dictionary<Guid, SignalIntentRecord> _intentRecords =
            new Dictionary<Guid, SignalIntentRecord>();

        /// <summary>发送结果未知时保留的端点租约，直至协调器停止。</summary>
        private readonly List<ISignalEndpointLease> _frozenEndpointLeases =
            new List<ISignalEndpointLease>();

        /// <summary>取消全部尚未越过外部副作用提交点的调度记录。</summary>
        private readonly CancellationTokenSource _shutdownCancellation =
            new CancellationTokenSource();

        /// <summary>1表示协调器已经停止。</summary>
        private int _disposed;

        /// <summary>创建拥有默认端点串行器的协调器。</summary>
        public OrderedSignalCoordinator()
            : this(
                new SignalEndpointSerializer(),
                true,
                DefaultSignalOrderGapTimeoutMs)
        {
        }

        /// <summary>创建拥有默认端点串行器和指定缺口超时的协调器。</summary>
        public OrderedSignalCoordinator(int signalOrderGapTimeoutMs)
            : this(new SignalEndpointSerializer(), true, signalOrderGapTimeoutMs)
        {
        }

        /// <summary>创建使用可替换端点串行器的协调器。</summary>
        public OrderedSignalCoordinator(ISignalEndpointSerializer endpointSerializer)
            : this(
                endpointSerializer,
                false,
                DefaultSignalOrderGapTimeoutMs)
        {
        }

        /// <summary>创建使用可替换端点串行器和指定缺口超时的协调器。</summary>
        public OrderedSignalCoordinator(
            ISignalEndpointSerializer endpointSerializer,
            int signalOrderGapTimeoutMs)
            : this(endpointSerializer, false, signalOrderGapTimeoutMs)
        {
        }

        /// <summary>创建协调器并声明端点串行器所有权。</summary>
        private OrderedSignalCoordinator(
            ISignalEndpointSerializer endpointSerializer,
            bool ownsEndpointSerializer,
            int signalOrderGapTimeoutMs)
        {
            _endpointSerializer = endpointSerializer ?? throw new ArgumentNullException(nameof(endpointSerializer));
            _ownsEndpointSerializer = ownsEndpointSerializer;
            _signalOrderGapTimeoutMs = Math.Max(
                MinimumSignalOrderGapTimeoutMs,
                Math.Min(MaximumSignalOrderGapTimeoutMs, signalOrderGapTimeoutMs));
        }

        /// <summary>获取当前协调器使用的前序缺口报警时间。</summary>
        public int SignalOrderGapTimeoutMs => _signalOrderGapTimeoutMs;

        /// <summary>顺序缺口持续超过阈值并已阻断当前域时触发。</summary>
        public event EventHandler<OrderedSignalGapEventArgs> OrderGapDetected;

        /// <summary>按身份序号登记已准入工件，禁止缺号、重复对象和跨会话混用。</summary>
        public void RegisterWorkpiece(WorkpieceExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            lock (_syncRoot)
            {
                ThrowIfDisposedUnderLock();
                if (!_domainStates.TryGetValue(context.OrderDomain, out OrderDomainState state))
                {
                    state = new OrderDomainState(context.OrderDomain, context.Identity.SessionEpoch);
                    _domainStates.Add(context.OrderDomain, state);
                }
                if (state.SessionEpoch != context.Identity.SessionEpoch)
                    throw new InvalidOperationException($"顺序域{context.OrderDomain}不能混用不同生产会话。 ");
                if (state.WorkpiecesBySequence.TryGetValue(
                    context.Identity.Sequence,
                    out WorkpieceSignalState existing))
                {
                    if (ReferenceEquals(existing.Context, context))
                        return;
                    throw new InvalidOperationException($"工件身份{context.Identity}已经由其他上下文登记。 ");
                }
                if (context.Identity.Sequence != state.NextRegistrationSequence)
                {
                    throw new InvalidOperationException(
                        $"顺序域{context.OrderDomain}登记工件出现缺口：期望={state.NextRegistrationSequence}，实际={context.Identity.Sequence}。 ");
                }

                WorkpieceSignalState workpiece = new WorkpieceSignalState(context);
                state.Workpieces.AddLast(workpiece);
                state.WorkpiecesBySequence.Add(context.Identity.Sequence, workpiece);
                state.NextRegistrationSequence++;
            }
        }

        /// <summary>提交不可变意图；只有当前顺序域队首工件的下一信号可以开始。</summary>
        public Task<OrderedSignalSendResult> SubmitAsync(
            OrderedSignalIntent intent,
            CancellationToken cancellationToken)
        {
            if (intent == null)
                throw new ArgumentNullException(nameof(intent));

            SignalIntentRecord record;
            SignalIntentRecord scheduledRecord;
            bool completeImmediately = false;
            lock (_syncRoot)
            {
                ThrowIfDisposedUnderLock();
                if (_intentRecords.TryGetValue(intent.IntentId, out record))
                {
                    if (!ReferenceEquals(record.Intent, intent))
                        throw new InvalidOperationException($"意图ID {intent.IntentId}已经绑定其他不可变意图。 ");
                    return record.Completion.Task;
                }

                OrderDomainState state = GetRegisteredDomainUnderLock(intent.Context);
                WorkpieceSignalState workpiece = state.WorkpiecesBySequence[intent.Context.Identity.Sequence];
                if (!ReferenceEquals(workpiece.Context, intent.Context))
                    throw new InvalidOperationException("信号意图上下文与已登记工件对象不一致。 ");
                if (workpiece.Signals.ContainsKey(intent.SignalSequence))
                    throw new InvalidOperationException($"工件{intent.Context.Identity}的信号序号{intent.SignalSequence}已经登记。 ");

                record = new SignalIntentRecord(state, workpiece, intent, cancellationToken);
                workpiece.Signals.Add(intent.SignalSequence, record);
                _intentRecords.Add(intent.IntentId, record);
                if (state.Blocked)
                {
                    CompleteQueuedRecordUnderLock(
                        record,
                        new OrderedSignalSendResult(OrderedSignalSendStatus.Rejected, state.BlockReason));
                    scheduledRecord = null;
                    completeImmediately = true;
                }
                else
                {
                    scheduledRecord = TryScheduleNextUnderLock(state);
                }
            }

            if (scheduledRecord != null)
                StartRecord(scheduledRecord);
            if (completeImmediately)
                record.Completion.TrySetResult(record.Result);
            return AwaitRecordWithCancellationAsync(record, cancellationToken);
        }

        /// <summary>请求工件封口，并在其全部意图返回后推进到下一工件。</summary>
        public async Task CompleteWorkpieceAsync(
            WorkpieceExecutionContext context,
            WorkpieceTerminalState terminalState)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (terminalState == WorkpieceTerminalState.Active ||
                !Enum.IsDefined(typeof(WorkpieceTerminalState), terminalState))
            {
                throw new ArgumentOutOfRangeException(nameof(terminalState));
            }

            lock (_syncRoot)
            {
                ThrowIfDisposedUnderLock();
                GetRegisteredDomainUnderLock(context);
            }
            context.TryComplete(terminalState);
            WorkpieceTerminalState finalState = await context.Completion.ConfigureAwait(false);

            SignalIntentRecord scheduledRecord = null;
            List<SignalIntentRecord> rejectedRecords = null;
            lock (_syncRoot)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;
                OrderDomainState state = GetRegisteredDomainUnderLock(context);
                WorkpieceSignalState workpiece = state.WorkpiecesBySequence[context.Identity.Sequence];
                if (!workpiece.CompletionProcessed)
                {
                    workpiece.CompletionProcessed = true;
                    workpiece.TerminalState = finalState;
                    if (!IsNormalTerminalState(finalState))
                    {
                        rejectedRecords = BlockDomainUnderLock(
                            state,
                            $"前序工件{context.Identity}以{finalState}结束，后续信号已停止。 ");
                    }
                    else
                    {
                        AdvanceCompletedWorkpiecesUnderLock(state);
                        scheduledRecord = TryScheduleNextUnderLock(state);
                    }
                }
            }

            CompleteRejectedRecords(rejectedRecords);
            if (scheduledRecord != null)
                StartRecord(scheduledRecord);
        }

        /// <summary>停止新提交，明确拒绝全部尚未开始的意图并释放冻结端点。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _shutdownCancellation.Cancel();

            List<SignalIntentRecord> rejectedRecords = new List<SignalIntentRecord>();
            List<ISignalEndpointLease> frozenLeases;
            lock (_syncRoot)
            {
                foreach (OrderDomainState state in _domainStates.Values)
                {
                    List<SignalIntentRecord> domainRejected = BlockDomainUnderLock(
                        state,
                        "有序信号协调器已经停止。 ");
                    if (domainRejected != null)
                        rejectedRecords.AddRange(domainRejected);
                }
                _intentRecords.Clear();
                frozenLeases = new List<ISignalEndpointLease>(_frozenEndpointLeases);
                _frozenEndpointLeases.Clear();
            }

            CompleteRejectedRecords(rejectedRecords);
            foreach (ISignalEndpointLease lease in frozenLeases)
            {
                try { lease.Dispose(); } catch { }
            }
            if (_ownsEndpointSerializer && _endpointSerializer is IDisposable disposable)
                disposable.Dispose();
        }

        /// <summary>等待首次结果；取消只尝试永久移除尚未开始的意图。</summary>
        private async Task<OrderedSignalSendResult> AwaitRecordWithCancellationAsync(
            SignalIntentRecord record,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return await record.Completion.Task.ConfigureAwait(false);

            using (CancellationTokenSource waitCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task cancellationTask = Task.Delay(Timeout.Infinite, waitCancellation.Token);
                Task completed = await Task.WhenAny(record.Completion.Task, cancellationTask).ConfigureAwait(false);
                if (ReferenceEquals(completed, record.Completion.Task))
                {
                    waitCancellation.Cancel();
                    return await record.Completion.Task.ConfigureAwait(false);
                }
            }

            CancelBeforeStart(record);
            return await record.Completion.Task.ConfigureAwait(false);
        }

        /// <summary>取消尚未开始的意图并阻断当前顺序域，禁止形成信号序号缺口。</summary>
        private void CancelBeforeStart(SignalIntentRecord record)
        {
            List<SignalIntentRecord> rejectedRecords = null;
            bool cancelled = false;
            lock (_syncRoot)
            {
                if (record.State == SignalRecordState.Queued)
                {
                    record.State = SignalRecordState.Completed;
                    record.Result = new OrderedSignalSendResult(
                        OrderedSignalSendStatus.CancelledBeforeStart,
                        $"工件{record.Intent.Context.Identity}的信号在发送开始前取消。 ");
                    cancelled = true;
                    rejectedRecords = BlockDomainUnderLock(
                        record.Domain,
                        $"工件{record.Intent.Context.Identity}出现发送前取消，顺序域已停止。 ");
                }
            }

            if (cancelled)
                record.Completion.TrySetResult(record.Result);
            CompleteRejectedRecords(rejectedRecords);
        }

        /// <summary>把已准入记录投递到线程池，避免在状态锁或调用节点线程内同步执行设备动作。</summary>
        private void StartRecord(SignalIntentRecord record)
        {
            Task.Run(() => ExecuteRecordAsync(record));
        }

        /// <summary>取得物理端点后执行唯一发送动作，副作用开始后不再响应等待取消。</summary>
        private async Task ExecuteRecordAsync(SignalIntentRecord record)
        {
            ISignalEndpointLease endpointLease = null;
            OrderedSignalSendResult result;
            bool sideEffectStarted = false;
            try
            {
                using (CancellationTokenSource acquireCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        record.WaitingCancellationToken,
                        _shutdownCancellation.Token))
                {
                    endpointLease = await _endpointSerializer.AcquireAsync(
                        record.Intent.EndpointKey,
                        acquireCancellation.Token).ConfigureAwait(false);
                    acquireCancellation.Token.ThrowIfCancellationRequested();
                    lock (_syncRoot)
                    {
                        if (Volatile.Read(ref _disposed) != 0)
                            throw new OperationCanceledException(_shutdownCancellation.Token);
                        sideEffectStarted = true;
                    }
                    result = await record.Intent.ExecuteAsync().ConfigureAwait(false);
                }
                if (result == null)
                {
                    result = new OrderedSignalSendResult(
                        OrderedSignalSendStatus.Unknown,
                        "设备发送动作返回空结果，无法判断外部副作用状态。 ");
                }
            }
            catch (OperationCanceledException) when (!sideEffectStarted)
            {
                result = new OrderedSignalSendResult(
                    OrderedSignalSendStatus.CancelledBeforeStart,
                    "取得物理设备发送权之前已取消。 ");
            }
            catch (DeviceSendNotStartedException exception)
            {
                // 适配器在真实写入前明确拒绝，不冻结整台设备；本工件仍按失败保护。
                result = new OrderedSignalSendResult(OrderedSignalSendStatus.Rejected, exception.Message);
            }
            catch (Exception exception)
            {
                result = new OrderedSignalSendResult(
                    sideEffectStarted
                        ? OrderedSignalSendStatus.Unknown
                        : OrderedSignalSendStatus.Rejected,
                    sideEffectStarted
                        ? $"设备发送动作异常，副作用状态未知：{exception.Message}"
                        : $"取得设备发送权失败：{exception.Message}");
            }

            bool retainEndpointLease = result.Status == OrderedSignalSendStatus.Unknown &&
                endpointLease != null;
            if (!retainEndpointLease)
            {
                try { endpointLease?.Dispose(); } catch { }
                endpointLease = null;
            }
            CompleteExecutedRecord(record, result, endpointLease);
        }

        /// <summary>原子提交执行结果、推进信号序号或阻断顺序域。</summary>
        private void CompleteExecutedRecord(
            SignalIntentRecord record,
            OrderedSignalSendResult result,
            ISignalEndpointLease endpointLeaseToFreeze)
        {
            SignalIntentRecord scheduledRecord = null;
            List<SignalIntentRecord> rejectedRecords = null;
            bool retainEndpointLease = false;
            lock (_syncRoot)
            {
                if (record.State == SignalRecordState.Completed)
                {
                    retainEndpointLease = false;
                }
                else
                {
                    record.State = SignalRecordState.Completed;
                    record.Result = result;
                    record.Domain.DispatchActive = false;
                    if (endpointLeaseToFreeze != null && Volatile.Read(ref _disposed) == 0)
                    {
                        _frozenEndpointLeases.Add(endpointLeaseToFreeze);
                        retainEndpointLease = true;
                    }

                    if (IsSuccessfulSendResult(result.Status))
                    {
                        record.Workpiece.NextSignalSequence++;
                        scheduledRecord = TryScheduleNextUnderLock(record.Domain);
                    }
                    else
                    {
                        rejectedRecords = BlockDomainUnderLock(
                            record.Domain,
                            $"工件{record.Intent.Context.Identity}的信号{record.Intent.SignalSequence}返回{result.Status}，顺序域已停止。 ");
                    }
                }
            }

            if (!retainEndpointLease)
            {
                try { endpointLeaseToFreeze?.Dispose(); } catch { }
            }
            record.Completion.TrySetResult(result);
            CompleteRejectedRecords(rejectedRecords);
            if (scheduledRecord != null)
                StartRecord(scheduledRecord);
        }

        /// <summary>只调度当前队首工件的下一连续信号。</summary>
        private SignalIntentRecord TryScheduleNextUnderLock(OrderDomainState state)
        {
            if (state.Blocked || state.DispatchActive || state.Workpieces.First == null)
                return null;
            WorkpieceSignalState workpiece = state.Workpieces.First.Value;
            if (!workpiece.Signals.TryGetValue(workpiece.NextSignalSequence, out SignalIntentRecord record) ||
                record.State != SignalRecordState.Queued)
            {
                UpdateGapWatchUnderLock(state);
                return null;
            }
            CancelGapWatchUnderLock(state);
            record.State = SignalRecordState.Started;
            state.DispatchActive = true;
            return record;
        }

        /// <summary>根据当前队首和后方排队压力启动、保持或取消唯一缺口计时。</summary>
        private void UpdateGapWatchUnderLock(OrderDomainState state)
        {
            if (!TryCreateGapObservationUnderLock(state, out SignalGapObservation observation))
            {
                CancelGapWatchUnderLock(state);
                return;
            }

            if (state.GapWatchCancellation != null &&
                state.GapExpectedWorkpieceSequence == observation.ExpectedWorkpieceSequence &&
                state.GapExpectedSignalSequence == observation.ExpectedSignalSequence)
            {
                return;
            }

            CancelGapWatchUnderLock(state);
            var cancellation = new CancellationTokenSource();
            state.GapWatchCancellation = cancellation;
            state.GapExpectedWorkpieceSequence = observation.ExpectedWorkpieceSequence;
            state.GapExpectedSignalSequence = observation.ExpectedSignalSequence;
            long generation = ++state.GapWatchGeneration;
            _ = MonitorGapAsync(state, generation, cancellation.Token);
        }

        /// <summary>异步等待缺口阈值，计时期间不占用线程。</summary>
        private async Task MonitorGapAsync(
            OrderDomainState state,
            long generation,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_signalOrderGapTimeoutMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            HandleGapTimeout(state, generation);
        }

        /// <summary>重新确认缺口仍存在后阻断顺序域，并在锁外发布唯一报警。</summary>
        private void HandleGapTimeout(OrderDomainState state, long generation)
        {
            List<SignalIntentRecord> rejectedRecords;
            List<WorkpieceExecutionContext> affectedContexts;
            OrderedSignalGapEventArgs eventArgs;
            lock (_syncRoot)
            {
                if (Volatile.Read(ref _disposed) != 0 ||
                    state.Blocked ||
                    state.GapWatchGeneration != generation ||
                    !TryCreateGapObservationUnderLock(state, out SignalGapObservation observation) ||
                    observation.ExpectedWorkpieceSequence != state.GapExpectedWorkpieceSequence ||
                    observation.ExpectedSignalSequence != state.GapExpectedSignalSequence)
                {
                    return;
                }

                string message =
                    $"有序信号顺序缺口超时，已停止方案：顺序域={state.OrderDomain}；会话={state.SessionEpoch:N}；" +
                    $"期望工件={observation.ExpectedWorkpieceSequence}；期望信号={observation.ExpectedSignalSequence}；" +
                    $"首个等待=工件{observation.FirstWaitingWorkpieceSequence}/信号{observation.FirstWaitingSignalSequence}/节点{observation.FirstWaitingNodePath}；" +
                    $"等待意图={observation.WaitingIntentCount}；超时={_signalOrderGapTimeoutMs}ms。禁止越过缺口发送。";
                rejectedRecords = BlockDomainUnderLock(state, message);
                affectedContexts = new List<WorkpieceExecutionContext>(state.Workpieces.Count);
                foreach (WorkpieceSignalState workpiece in state.Workpieces)
                    affectedContexts.Add(workpiece.Context);
                eventArgs = new OrderedSignalGapEventArgs(
                    state.OrderDomain,
                    state.SessionEpoch,
                    observation.ExpectedWorkpieceSequence,
                    observation.ExpectedSignalSequence,
                    observation.FirstWaitingWorkpieceSequence,
                    observation.FirstWaitingSignalSequence,
                    observation.FirstWaitingNodePath,
                    observation.WaitingIntentCount,
                    _signalOrderGapTimeoutMs,
                    DateTime.UtcNow,
                    message);
            }

            foreach (WorkpieceExecutionContext context in affectedContexts)
                context.TryEscalateTerminalState(WorkpieceTerminalState.Stopped);
            CompleteRejectedRecords(rejectedRecords);
            RaiseOrderGapDetected(eventArgs);
        }

        /// <summary>判断是否有后续意图因当前队首的前序工件或信号缺失而等待。</summary>
        private static bool TryCreateGapObservationUnderLock(
            OrderDomainState state,
            out SignalGapObservation observation)
        {
            observation = null;
            if (state.Blocked || state.DispatchActive || state.Workpieces.First == null)
                return false;

            WorkpieceSignalState head = state.Workpieces.First.Value;
            if (head.Signals.TryGetValue(head.NextSignalSequence, out SignalIntentRecord expected) &&
                expected.State == SignalRecordState.Queued)
            {
                return false;
            }

            SignalIntentRecord firstWaiting = null;
            int waitingCount = 0;
            foreach (WorkpieceSignalState workpiece in state.Workpieces)
            {
                foreach (SignalIntentRecord record in workpiece.Signals.Values)
                {
                    if (record.State != SignalRecordState.Queued)
                        continue;
                    if (ReferenceEquals(workpiece, head) &&
                        record.Intent.SignalSequence <= head.NextSignalSequence)
                    {
                        continue;
                    }
                    waitingCount++;
                    if (firstWaiting == null)
                        firstWaiting = record;
                }
            }
            if (firstWaiting == null)
                return false;

            observation = new SignalGapObservation(
                head.Context.Identity.Sequence,
                head.NextSignalSequence,
                firstWaiting.Intent.Context.Identity.Sequence,
                firstWaiting.Intent.SignalSequence,
                firstWaiting.Intent.NodePath,
                waitingCount);
            return true;
        }

        /// <summary>取消当前域的缺口监视；重复调用安全。</summary>
        private static void CancelGapWatchUnderLock(OrderDomainState state)
        {
            CancellationTokenSource cancellation = state.GapWatchCancellation;
            state.GapWatchCancellation = null;
            state.GapExpectedWorkpieceSequence = 0L;
            state.GapExpectedSignalSequence = 0L;
            state.GapWatchGeneration++;
            if (cancellation == null)
                return;
            try { cancellation.Cancel(); } catch { }
            cancellation.Dispose();
        }

        /// <summary>逐个隔离报警订阅者异常，避免诊断回调破坏协调器状态。</summary>
        private void RaiseOrderGapDetected(OrderedSignalGapEventArgs eventArgs)
        {
            EventHandler<OrderedSignalGapEventArgs> handlers = OrderGapDetected;
            if (handlers == null)
                return;
            foreach (EventHandler<OrderedSignalGapEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, eventArgs); } catch { }
            }
        }

        /// <summary>阻断顺序域并取出全部尚未开始的意图。</summary>
        private List<SignalIntentRecord> BlockDomainUnderLock(
            OrderDomainState state,
            string reason)
        {
            CancelGapWatchUnderLock(state);
            if (!state.Blocked)
            {
                state.Blocked = true;
                state.BlockReason = reason ?? "有序信号顺序域已经停止。 ";
            }
            List<SignalIntentRecord> rejected = new List<SignalIntentRecord>();
            foreach (WorkpieceSignalState workpiece in state.Workpieces)
            {
                foreach (SignalIntentRecord record in workpiece.Signals.Values)
                {
                    if (record.State != SignalRecordState.Queued)
                        continue;
                    CompleteQueuedRecordUnderLock(
                        record,
                        new OrderedSignalSendResult(
                            OrderedSignalSendStatus.Rejected,
                            state.BlockReason));
                    rejected.Add(record);
                }
            }
            return rejected;
        }

        /// <summary>标记一个尚未执行的记录为明确结果，任务在锁外完成。</summary>
        private static void CompleteQueuedRecordUnderLock(
            SignalIntentRecord record,
            OrderedSignalSendResult result)
        {
            record.State = SignalRecordState.Completed;
            record.Result = result;
        }

        /// <summary>在锁外完成被顺序域阻断的任务。</summary>
        private static void CompleteRejectedRecords(IEnumerable<SignalIntentRecord> records)
        {
            if (records == null)
                return;
            foreach (SignalIntentRecord record in records)
                record.Completion.TrySetResult(record.Result);
        }

        /// <summary>移除已经正常封口的连续队首工件。</summary>
        private void AdvanceCompletedWorkpiecesUnderLock(OrderDomainState state)
        {
            while (state.Workpieces.First != null &&
                state.Workpieces.First.Value.CompletionProcessed &&
                IsNormalTerminalState(state.Workpieces.First.Value.TerminalState))
            {
                WorkpieceSignalState completed = state.Workpieces.First.Value;
                state.Workpieces.RemoveFirst();
                state.WorkpiecesBySequence.Remove(completed.Context.Identity.Sequence);
                foreach (SignalIntentRecord record in completed.Signals.Values)
                    _intentRecords.Remove(record.Intent.IntentId);
            }
        }

        /// <summary>获取上下文对应的已登记顺序域并验证会话。</summary>
        private OrderDomainState GetRegisteredDomainUnderLock(WorkpieceExecutionContext context)
        {
            if (!_domainStates.TryGetValue(context.OrderDomain, out OrderDomainState state) ||
                state.SessionEpoch != context.Identity.SessionEpoch ||
                !state.WorkpiecesBySequence.ContainsKey(context.Identity.Sequence))
            {
                throw new InvalidOperationException($"工件{context.Identity}尚未登记到有序信号协调器。 ");
            }
            return state;
        }

        /// <summary>协调器关闭后拒绝新操作。</summary>
        private void ThrowIfDisposedUnderLock()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(OrderedSignalCoordinator));
        }

        /// <summary>判断设备结果是否允许同顺序域继续发送。</summary>
        private static bool IsSuccessfulSendResult(OrderedSignalSendStatus status)
        {
            return status == OrderedSignalSendStatus.DeviceAcknowledged ||
                status == OrderedSignalSendStatus.LocalCallCompleted;
        }

        /// <summary>判断工件终态是否属于可正常推进的业务完成。</summary>
        private static bool IsNormalTerminalState(WorkpieceTerminalState terminalState)
        {
            return terminalState == WorkpieceTerminalState.Succeeded ||
                terminalState == WorkpieceTerminalState.BusinessRejected;
        }

        /// <summary>单个生产顺序域的工件FIFO和发送状态。</summary>
        private sealed class OrderDomainState
        {
            /// <summary>创建当前生产会话的顺序域。</summary>
            public OrderDomainState(string orderDomain, Guid sessionEpoch)
            {
                OrderDomain = orderDomain;
                SessionEpoch = sessionEpoch;
            }

            /// <summary>规范化顺序域。</summary>
            public string OrderDomain { get; }

            /// <summary>当前生产会话纪元。</summary>
            public Guid SessionEpoch { get; }

            /// <summary>下一允许登记的连续工件序号。</summary>
            public long NextRegistrationSequence { get; set; } = 1L;

            /// <summary>按登记顺序等待的工件。</summary>
            public LinkedList<WorkpieceSignalState> Workpieces { get; } =
                new LinkedList<WorkpieceSignalState>();

            /// <summary>按工件序号快速定位状态。</summary>
            public Dictionary<long, WorkpieceSignalState> WorkpiecesBySequence { get; } =
                new Dictionary<long, WorkpieceSignalState>();

            /// <summary>是否已有设备动作正在本顺序域执行。</summary>
            public bool DispatchActive { get; set; }

            /// <summary>是否因失败、未知、取消或关闭停止后续发送。</summary>
            public bool Blocked { get; set; }

            /// <summary>阻断后续发送的首个原因。</summary>
            public string BlockReason { get; set; }

            /// <summary>当前缺口监视的取消源；为空表示没有后续排队压力。</summary>
            public CancellationTokenSource GapWatchCancellation { get; set; }

            /// <summary>用于淘汰迟到计时回调的代数。</summary>
            public long GapWatchGeneration { get; set; }

            /// <summary>当前缺口计时等待的工件序号。</summary>
            public long GapExpectedWorkpieceSequence { get; set; }

            /// <summary>当前缺口计时等待的信号序号。</summary>
            public long GapExpectedSignalSequence { get; set; }
        }

        /// <summary>一次锁内顺序缺口判断的不可变快照。</summary>
        private sealed class SignalGapObservation
        {
            /// <summary>创建缺口观察快照。</summary>
            public SignalGapObservation(
                long expectedWorkpieceSequence,
                long expectedSignalSequence,
                long firstWaitingWorkpieceSequence,
                long firstWaitingSignalSequence,
                string firstWaitingNodePath,
                int waitingIntentCount)
            {
                ExpectedWorkpieceSequence = expectedWorkpieceSequence;
                ExpectedSignalSequence = expectedSignalSequence;
                FirstWaitingWorkpieceSequence = firstWaitingWorkpieceSequence;
                FirstWaitingSignalSequence = firstWaitingSignalSequence;
                FirstWaitingNodePath = firstWaitingNodePath;
                WaitingIntentCount = waitingIntentCount;
            }

            /// <summary>获取当前缺失的工件序号。</summary>
            public long ExpectedWorkpieceSequence { get; }

            /// <summary>获取当前缺失的工件内信号序号。</summary>
            public long ExpectedSignalSequence { get; }

            /// <summary>获取首个后续等待工件序号。</summary>
            public long FirstWaitingWorkpieceSequence { get; }

            /// <summary>获取首个后续等待信号序号。</summary>
            public long FirstWaitingSignalSequence { get; }

            /// <summary>获取首个后续等待节点路径。</summary>
            public string FirstWaitingNodePath { get; }

            /// <summary>获取后续累计等待意图数量。</summary>
            public int WaitingIntentCount { get; }
        }

        /// <summary>单个已登记工件的动态信号序列。</summary>
        private sealed class WorkpieceSignalState
        {
            /// <summary>创建绑定上下文的工件信号状态。</summary>
            public WorkpieceSignalState(WorkpieceExecutionContext context)
            {
                Context = context;
            }

            /// <summary>工件执行上下文。</summary>
            public WorkpieceExecutionContext Context { get; }

            /// <summary>按工件内信号序号保存意图。</summary>
            public SortedDictionary<long, SignalIntentRecord> Signals { get; } =
                new SortedDictionary<long, SignalIntentRecord>();

            /// <summary>下一允许执行的工件内信号序号。</summary>
            public long NextSignalSequence { get; set; } = 1L;

            /// <summary>是否已经观察并处理唯一终态。</summary>
            public bool CompletionProcessed { get; set; }

            /// <summary>工件最终终态。</summary>
            public WorkpieceTerminalState TerminalState { get; set; }
        }

        /// <summary>单个不可变意图的首次提交和唯一结果。</summary>
        private sealed class SignalIntentRecord
        {
            /// <summary>创建尚未开始的意图记录。</summary>
            public SignalIntentRecord(
                OrderDomainState domain,
                WorkpieceSignalState workpiece,
                OrderedSignalIntent intent,
                CancellationToken waitingCancellationToken)
            {
                Domain = domain;
                Workpiece = workpiece;
                Intent = intent;
                WaitingCancellationToken = waitingCancellationToken;
                Completion = new TaskCompletionSource<OrderedSignalSendResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            /// <summary>所属顺序域。</summary>
            public OrderDomainState Domain { get; }

            /// <summary>所属工件。</summary>
            public WorkpieceSignalState Workpiece { get; }

            /// <summary>不可变发送意图。</summary>
            public OrderedSignalIntent Intent { get; }

            /// <summary>只用于副作用开始前等待的取消令牌。</summary>
            public CancellationToken WaitingCancellationToken { get; }

            /// <summary>首次提交的唯一结果任务。</summary>
            public TaskCompletionSource<OrderedSignalSendResult> Completion { get; }

            /// <summary>当前执行状态。</summary>
            public SignalRecordState State { get; set; }

            /// <summary>完成时的明确结果。</summary>
            public OrderedSignalSendResult Result { get; set; }
        }

        /// <summary>意图从排队到唯一终结的内部状态。</summary>
        private enum SignalRecordState
        {
            /// <summary>尚未取得顺序权。</summary>
            Queued = 0,

            /// <summary>已经取得顺序权并等待或执行物理端点。</summary>
            Started = 1,

            /// <summary>已经产生唯一最终结果。</summary>
            Completed = 2
        }
    }
}
