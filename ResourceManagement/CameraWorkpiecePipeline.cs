using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Diagnostics;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>为生产票据、相机转换线程和图像源节点生成同一个稳定相机键。</summary>
    public static class CameraProductionIdentity
    {
        /// <summary>为没有SDK序列号的相机对象分配进程内不可变且互不重复的身份。</summary>
        private static readonly ConditionalWeakTable<ICamera, CameraIdentity> Identities =
            new ConditionalWeakTable<ICamera, CameraIdentity>();

        /// <summary>
        /// 优先使用品牌和序列号标识物理相机；没有序列号时再使用用户名称、设备名称和类型降级。
        /// </summary>
        /// <param name="camera">需要生成生产键的相机。</param>
        /// <returns>不依赖对象引用和列表顺序的规范化相机键。</returns>
        public static string GetStableKey(ICamera camera)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));

            return Identities.GetValue(camera, CreateIdentity).Key;
        }

        /// <summary>移除键组成部分的首尾空白，空值统一为空字符串。</summary>
        private static string NormalizePart(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>在首次看到相机对象时冻结键，后续SDK补齐属性或界面改名均不改变身份。</summary>
        private static CameraIdentity CreateIdentity(ICamera camera)
        {
            string brand = camera.Brand.ToString().Trim();
            string serialNumber = NormalizePart(camera.SN);
            string key = string.IsNullOrEmpty(serialNumber)
                ? string.Concat(brand, "|INSTANCE|", Guid.NewGuid().ToString("N"))
                : string.Concat(brand, "|SN|", serialNumber);
            return new CameraIdentity(key);
        }

        /// <summary>相机对象首次使用时冻结的不可变生产身份。</summary>
        private sealed class CameraIdentity
        {
            /// <summary>创建不可变相机身份。</summary>
            public CameraIdentity(string key)
            {
                Key = key;
            }

            /// <summary>获取冻结后的生产键。</summary>
            public string Key { get; }
        }
    }

    /// <summary>相机生产帧的触发来源。</summary>
    public enum CameraTriggerKind
    {
        /// <summary>软件命令触发。</summary>
        Software = 0,

        /// <summary>外部线路硬触发。</summary>
        Hardware = 1,

        /// <summary>连续采集，只接管当前节点等待期间的新帧。</summary>
        Continuous = 2
    }

    /// <summary>工件图像内存预算不足时抛出的生产准入异常。</summary>
    public sealed class WorkpieceFrameMemoryBudgetExceededException : InvalidOperationException
    {
        /// <summary>创建包含本次申请量和全局预算的异常。</summary>
        public WorkpieceFrameMemoryBudgetExceededException(long requestedBytes, long reservedBytes, long budgetBytes)
            : base($"工件图像内存预算不足：申请={requestedBytes}字节，已预留={reservedBytes}字节，预算={budgetBytes}字节。必须暂停触发并报警。")
        {
            RequestedBytes = requestedBytes;
            ReservedBytes = reservedBytes;
            BudgetBytes = budgetBytes;
        }

        /// <summary>获取本次申请字节数。</summary>
        public long RequestedBytes { get; }

        /// <summary>获取申请前已预留字节数。</summary>
        public long ReservedBytes { get; }

        /// <summary>获取当前全局预算字节数。</summary>
        public long BudgetBytes { get; }
    }

    /// <summary>工件图像内存预算租约。</summary>
    public interface IWorkpieceFrameMemoryLease : IDisposable
    {
        /// <summary>获取租约所属工件。</summary>
        WorkpieceIdentity Identity { get; }

        /// <summary>获取当前记账字节数。</summary>
        long ReservedBytes { get; }

        /// <summary>获取租约是否已经释放。</summary>
        bool IsReleased { get; }

        /// <summary>把触发前估算值校正为Mat实际行跨度字节数。</summary>
        bool TryCommitActualBytes(long actualBytes);
    }

    /// <summary>工件图像内存预算的只读快照。</summary>
    public sealed class WorkpieceFrameMemoryBudgetSnapshot
    {
        /// <summary>获取当前预算字节数。</summary>
        public long BudgetBytes { get; internal set; }

        /// <summary>获取当前预留字节数。</summary>
        public long ReservedBytes { get; internal set; }

        /// <summary>获取历史峰值字节数。</summary>
        public long PeakReservedBytes { get; internal set; }

        /// <summary>获取当前活动租约数。</summary>
        public int ActiveLeaseCount { get; internal set; }

        /// <summary>获取累计拒绝次数。</summary>
        public long RejectedCount { get; internal set; }
    }

    /// <summary>按实际字节为全部在途生产帧提供统一硬预算。</summary>
    public sealed class WorkpieceFrameMemoryBudgetManager
    {
        /// <summary>保护预算、租约和峰值统计。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>当前活动租约共同遵守的预算。</summary>
        private long _budgetBytes;

        /// <summary>当前全部租约记账字节数。</summary>
        private long _reservedBytes;

        /// <summary>历史峰值记账字节数。</summary>
        private long _peakReservedBytes;

        /// <summary>当前活动租约数。</summary>
        private int _activeLeaseCount;

        /// <summary>累计预算拒绝次数。</summary>
        private long _rejectedCount;

        /// <summary>在实际触发前预留一帧估算内存。</summary>
        public IWorkpieceFrameMemoryLease Reserve(
            WorkpieceIdentity identity,
            long estimatedBytes,
            long budgetBytes)
        {
            if (identity.SessionEpoch == Guid.Empty || identity.Sequence <= 0L)
                throw new ArgumentException("必须提供有效工件身份。", nameof(identity));
            if (estimatedBytes <= 0L)
                throw new ArgumentOutOfRangeException(nameof(estimatedBytes));
            if (budgetBytes <= 0L)
                throw new ArgumentOutOfRangeException(nameof(budgetBytes));

            lock (_syncRoot)
            {
                if (_activeLeaseCount == 0)
                    _budgetBytes = budgetBytes;
                else if (budgetBytes < _budgetBytes)
                {
                    if (_reservedBytes > budgetBytes)
                    {
                        _rejectedCount++;
                        throw new WorkpieceFrameMemoryBudgetExceededException(
                            estimatedBytes,
                            _reservedBytes,
                            budgetBytes);
                    }
                    _budgetBytes = budgetBytes;
                }

                if (estimatedBytes > _budgetBytes - _reservedBytes)
                {
                    _rejectedCount++;
                    throw new WorkpieceFrameMemoryBudgetExceededException(
                        estimatedBytes,
                        _reservedBytes,
                        _budgetBytes);
                }

                _reservedBytes = checked(_reservedBytes + estimatedBytes);
                _activeLeaseCount++;
                if (_reservedBytes > _peakReservedBytes)
                    _peakReservedBytes = _reservedBytes;
                return new WorkpieceFrameMemoryLease(this, identity, estimatedBytes);
            }
        }

        /// <summary>获取当前预算运行快照。</summary>
        public WorkpieceFrameMemoryBudgetSnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return new WorkpieceFrameMemoryBudgetSnapshot
                {
                    BudgetBytes = _budgetBytes,
                    ReservedBytes = _reservedBytes,
                    PeakReservedBytes = _peakReservedBytes,
                    ActiveLeaseCount = _activeLeaseCount,
                    RejectedCount = _rejectedCount
                };
            }
        }

        /// <summary>把租约估算值原子调整为实际值。</summary>
        private bool TryAdjust(long previousBytes, long actualBytes)
        {
            lock (_syncRoot)
            {
                long delta = actualBytes - previousBytes;
                if (delta > 0L && delta > _budgetBytes - _reservedBytes)
                {
                    _rejectedCount++;
                    return false;
                }

                _reservedBytes = checked(_reservedBytes + delta);
                if (_reservedBytes > _peakReservedBytes)
                    _peakReservedBytes = _reservedBytes;
                return true;
            }
        }

        /// <summary>幂等归还图像内存预算。</summary>
        private void Release(long reservedBytes)
        {
            lock (_syncRoot)
            {
                _reservedBytes = Math.Max(0L, _reservedBytes - Math.Max(0L, reservedBytes));
                _activeLeaseCount = Math.Max(0, _activeLeaseCount - 1);
                if (_activeLeaseCount == 0)
                {
                    _reservedBytes = 0L;
                    _budgetBytes = 0L;
                }
            }
        }

        /// <summary>默认工件帧内存租约。</summary>
        private sealed class WorkpieceFrameMemoryLease : IWorkpieceFrameMemoryLease
        {
            /// <summary>串行化实际字节校正与租约释放。</summary>
            private readonly object _syncRoot = new object();

            /// <summary>拥有当前记账的预算管理器。</summary>
            private WorkpieceFrameMemoryBudgetManager _owner;

            /// <summary>当前实际记账字节数。</summary>
            private long _reservedBytes;

            /// <summary>是否已经提交过实际字节数。</summary>
            private int _actualBytesCommitted;

            /// <summary>创建触发前内存预留。</summary>
            public WorkpieceFrameMemoryLease(
                WorkpieceFrameMemoryBudgetManager owner,
                WorkpieceIdentity identity,
                long reservedBytes)
            {
                _owner = owner;
                Identity = identity;
                _reservedBytes = reservedBytes;
            }

            /// <summary>获取租约所属工件。</summary>
            public WorkpieceIdentity Identity { get; }

            /// <summary>获取当前记账字节数。</summary>
            public long ReservedBytes => Interlocked.Read(ref _reservedBytes);

            /// <summary>获取租约是否已经释放。</summary>
            public bool IsReleased => Volatile.Read(ref _owner) == null;

            /// <summary>最多一次把估算值校正为实际值。</summary>
            public bool TryCommitActualBytes(long actualBytes)
            {
                if (actualBytes <= 0L)
                    throw new ArgumentOutOfRangeException(nameof(actualBytes));
                lock (_syncRoot)
                {
                    WorkpieceFrameMemoryBudgetManager owner = _owner;
                    if (owner == null)
                        return false;
                    if (_actualBytesCommitted != 0)
                        return actualBytes == _reservedBytes;
                    _actualBytesCommitted = 1;

                    long previousBytes = _reservedBytes;
                    if (!owner.TryAdjust(previousBytes, actualBytes))
                    {
                        _actualBytesCommitted = 0;
                        return false;
                    }

                    _reservedBytes = actualBytes;
                    return true;
                }
            }

            /// <summary>幂等释放当前记账。</summary>
            public void Dispose()
            {
                WorkpieceFrameMemoryBudgetManager owner;
                long reservedBytes;
                lock (_syncRoot)
                {
                    owner = _owner;
                    if (owner == null)
                        return;
                    _owner = null;
                    reservedBytes = _reservedBytes;
                }
                owner.Release(reservedBytes);
            }
        }
    }

    /// <summary>相机生产帧入口发生的安全故障。</summary>
    public sealed class CameraProductionFault
    {
        /// <summary>创建可用于日志和停机决策的故障快照。</summary>
        public CameraProductionFault(
            string cameraKey,
            WorkpieceIdentity? identity,
            string reason,
            Exception exception,
            bool requiresSolutionStop = true)
        {
            CameraKey = cameraKey ?? string.Empty;
            Identity = identity;
            Reason = reason ?? string.Empty;
            Exception = exception;
            RequiresSolutionStop = requiresSolutionStop;
            OccurredAtUtc = DateTime.UtcNow;
        }

        /// <summary>获取相机稳定键。</summary>
        public string CameraKey { get; }

        /// <summary>获取可关联的工件身份。</summary>
        public WorkpieceIdentity? Identity { get; }

        /// <summary>获取故障原因。</summary>
        public string Reason { get; }

        /// <summary>获取原始异常。</summary>
        public Exception Exception { get; }

        /// <summary>获取该事件是否必须停止整套方案。</summary>
        public bool RequiresSolutionStop { get; }

        /// <summary>获取故障UTC时间。</summary>
        public DateTime OccurredAtUtc { get; }
    }

    /// <summary>已经与工件和相机票据绑定的独立Mat。</summary>
    public sealed class CameraFrameEnvelope : IDisposable
    {
        /// <summary>保护图像和预算租约必须成对转移。</summary>
        private readonly object _ownershipSyncRoot = new object();

        /// <summary>当前拥有的独立Mat。</summary>
        private Mat _image;

        /// <summary>跟随Mat生命周期的内存预算租约。</summary>
        private IWorkpieceFrameMemoryLease _memoryLease;

        /// <summary>创建完成匹配的相机生产帧。</summary>
        internal CameraFrameEnvelope(
            WorkpieceIdentity identity,
            string cameraKey,
            Guid ticketId,
            long frameId,
            uint sdkFrameNumber,
            DateTime triggerIssuedAtUtc,
            DateTime receivedAtUtc,
            Mat image,
            IWorkpieceFrameMemoryLease memoryLease,
            CameraFrameTraceInfo traceInfo)
        {
            Identity = identity;
            CameraKey = cameraKey;
            TicketId = ticketId;
            FrameId = frameId;
            SdkFrameNumber = sdkFrameNumber;
            TriggerIssuedAtUtc = triggerIssuedAtUtc;
            ReceivedAtUtc = receivedAtUtc;
            _image = image ?? throw new ArgumentNullException(nameof(image));
            _memoryLease = memoryLease ?? throw new ArgumentNullException(nameof(memoryLease));
            TraceInfo = traceInfo;
        }

        /// <summary>获取工件身份。</summary>
        public WorkpieceIdentity Identity { get; }

        /// <summary>获取相机稳定键。</summary>
        public string CameraKey { get; }

        /// <summary>获取触发票据ID。</summary>
        public Guid TicketId { get; }

        /// <summary>获取相机实例内部帧号。</summary>
        public long FrameId { get; }

        /// <summary>获取SDK帧号。</summary>
        public uint SdkFrameNumber { get; }

        /// <summary>获取触发命令或硬触发等待开始UTC时间。</summary>
        public DateTime TriggerIssuedAtUtc { get; }

        /// <summary>获取完成Mat转换并绑定票据的UTC时间。</summary>
        public DateTime ReceivedAtUtc { get; }

        /// <summary>获取相机阶段诊断信息。</summary>
        public CameraFrameTraceInfo TraceInfo { get; }

        /// <summary>获取当前帧实际记账字节数。</summary>
        public long RetainedBytes => _memoryLease?.ReservedBytes ?? 0L;

        /// <summary>把Mat和预算租约成对转交给流程图像拥有者。</summary>
        public void TransferOwnership(out Mat image, out IWorkpieceFrameMemoryLease memoryLease)
        {
            lock (_ownershipSyncRoot)
            {
                if (_image == null || _memoryLease == null)
                    throw new InvalidOperationException("相机帧所有权已经转移或释放。 ");

                image = _image;
                memoryLease = _memoryLease;
                _image = null;
                _memoryLease = null;
            }
        }

        /// <summary>幂等释放尚未转交的Mat和预算租约。</summary>
        public void Dispose()
        {
            Mat image;
            IWorkpieceFrameMemoryLease memoryLease;
            lock (_ownershipSyncRoot)
            {
                image = _image;
                memoryLease = _memoryLease;
                _image = null;
                _memoryLease = null;
            }

            Exception disposalFailure = null;
            try { image?.Dispose(); }
            catch (Exception exception) { disposalFailure = exception; }
            try { memoryLease?.Dispose(); }
            catch (Exception exception)
            {
                if (disposalFailure == null)
                    disposalFailure = exception;
            }
            if (disposalFailure != null)
                throw disposalFailure;
        }
    }

    /// <summary>已经在真实相机触发前登记的生产票据。</summary>
    public sealed class CameraTriggerTicket : IDisposable
    {
        /// <summary>拥有票据队列的注册表。</summary>
        private CameraTriggerTicketRegistry _owner;

        /// <summary>触发前取得的图像内存预算。</summary>
        private IWorkpieceFrameMemoryLease _memoryLease;

        /// <summary>阻止工件在相机票据结束前提前封口的流程分支租约。</summary>
        private IWorkpieceExecutionLease _executionLease;

        /// <summary>帧到达或明确失败时完成的异步结果。</summary>
        private readonly TaskCompletionSource<CameraFrameEnvelope> _completionSource =
            new TaskCompletionSource<CameraFrameEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>0表示排队，1表示已经结束。</summary>
        private int _completed;

        /// <summary>真实触发命令发出或硬触发等待开始的UTC刻度。</summary>
        private long _triggerIssuedAtUtcTicks;

        /// <summary>真实触发提交点的单调时钟刻度，用于拒绝触发前已进入原始队列的旧帧。</summary>
        private long _triggerIssuedTimestamp;

        /// <summary>创建已经完成内存预留的触发票据。</summary>
        internal CameraTriggerTicket(
            CameraTriggerTicketRegistry owner,
            string cameraKey,
            WorkpieceExecutionContext context,
            CameraTriggerKind triggerKind,
            DateTime deadlineUtc,
            IWorkpieceFrameMemoryLease memoryLease,
            IWorkpieceExecutionLease executionLease)
        {
            _owner = owner;
            _memoryLease = memoryLease;
            _executionLease = executionLease;
            TicketId = Guid.NewGuid();
            CameraKey = cameraKey;
            Context = context;
            TriggerKind = triggerKind;
            CreatedAtUtc = DateTime.UtcNow;
            DeadlineUtc = deadlineUtc;
        }

        /// <summary>获取票据ID。</summary>
        public Guid TicketId { get; }

        /// <summary>获取相机稳定键。</summary>
        public string CameraKey { get; }

        /// <summary>获取票据所属工件上下文。</summary>
        public WorkpieceExecutionContext Context { get; }

        /// <summary>获取触发类型。</summary>
        public CameraTriggerKind TriggerKind { get; }

        /// <summary>获取票据创建UTC时间。</summary>
        public DateTime CreatedAtUtc { get; }

        /// <summary>获取票据最迟有效UTC时间。</summary>
        public DateTime DeadlineUtc { get; }

        /// <summary>获取真实触发开始UTC时间，尚未标记时为空。</summary>
        public DateTime? TriggerIssuedAtUtc
        {
            get
            {
                long ticks = Interlocked.Read(ref _triggerIssuedAtUtcTicks);
                return ticks <= 0L ? (DateTime?)null : new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        /// <summary>获取真实触发提交点的单调时钟刻度。</summary>
        internal long TriggerIssuedTimestamp => Interlocked.Read(ref _triggerIssuedTimestamp);

        /// <summary>登记时冻结的相机重连代数，只在注册表状态锁内读写。</summary>
        internal long ReconnectGeneration { get; set; }

        /// <summary>获取帧信封异步结果。</summary>
        public Task<CameraFrameEnvelope> Completion => _completionSource.Task;

        /// <summary>获取票据是否已经结束。</summary>
        public bool IsCompleted => Volatile.Read(ref _completed) == 1;

        /// <summary>让软件触发命令和票据武装在同一票据锁内完成。</summary>
        public void IssueSoftwareTrigger(Action triggerAction)
        {
            if (triggerAction == null)
                throw new ArgumentNullException(nameof(triggerAction));
            if (TriggerKind != CameraTriggerKind.Software)
                throw new InvalidOperationException("只有软件触发票据可以执行软件触发命令。 ");

            CameraTriggerTicketRegistry owner = Volatile.Read(ref _owner);
            if (owner == null || IsCompleted)
                throw new InvalidOperationException("相机票据已经取消或结束，禁止继续发送软件触发。 ");
            owner.IssueSoftwareTrigger(this, triggerAction);
        }

        /// <summary>硬触发票据明确进入等待状态；软件触发必须使用IssueSoftwareTrigger。</summary>
        public void MarkTriggerIssued(DateTime issuedAtUtc)
        {
            if (issuedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("触发时间必须使用UTC。", nameof(issuedAtUtc));
            if (TriggerKind != CameraTriggerKind.Hardware)
                throw new InvalidOperationException("软件触发必须通过IssueSoftwareTrigger原子执行相机命令。 ");

            CameraTriggerTicketRegistry owner = Volatile.Read(ref _owner);
            if (owner == null || IsCompleted)
                throw new InvalidOperationException("相机票据已经取消或结束，禁止进入硬触发等待。 ");
            owner.MarkReadyForFrame(this, issuedAtUtc);
        }

        /// <summary>仅允许注册表在确认票据仍位于当前相机FIFO时写入首次武装时间。</summary>
        internal bool TryMarkReadyForFrame(DateTime issuedAtUtc, long issuedTimestamp)
        {
            if (issuedTimestamp <= 0L)
                throw new ArgumentOutOfRangeException(nameof(issuedTimestamp));
            if (Interlocked.CompareExchange(ref _triggerIssuedAtUtcTicks, issuedAtUtc.Ticks, 0L) != 0L)
                return false;
            Interlocked.Exchange(ref _triggerIssuedTimestamp, issuedTimestamp);
            return true;
        }

        /// <summary>把触发前预算租约转给完成匹配的帧信封。</summary>
        internal IWorkpieceFrameMemoryLease DetachMemoryLease()
        {
            return Interlocked.Exchange(ref _memoryLease, null);
        }

        /// <summary>尝试以成功帧结束票据。</summary>
        internal bool TryComplete(CameraFrameEnvelope envelope)
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                return false;
            Interlocked.Exchange(ref _owner, null);
            Interlocked.Exchange(ref _executionLease, null)?.Complete(WorkpieceTerminalState.Succeeded);
            return _completionSource.TrySetResult(envelope);
        }

        /// <summary>尝试以明确异常结束票据并归还预算。</summary>
        internal bool TryFail(Exception exception, WorkpieceTerminalState terminalState)
        {
            if (terminalState == WorkpieceTerminalState.Active ||
                terminalState == WorkpieceTerminalState.Succeeded)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalState), "失败票据必须提交安全终态。 ");
            }
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                return false;
            Interlocked.Exchange(ref _owner, null);
            Interlocked.Exchange(ref _memoryLease, null)?.Dispose();
            Interlocked.Exchange(ref _executionLease, null)?.Complete(terminalState);
            return _completionSource.TrySetException(exception ?? new InvalidOperationException("相机生产票据失败。"));
        }

        /// <summary>尝试以等待取消结束票据并归还预算。</summary>
        internal bool TryCancel()
        {
            if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                return false;
            Interlocked.Exchange(ref _owner, null);
            Interlocked.Exchange(ref _memoryLease, null)?.Dispose();
            Interlocked.Exchange(ref _executionLease, null)?.Complete(WorkpieceTerminalState.Cancelled);
            return _completionSource.TrySetCanceled();
        }

        /// <summary>取消仍在排队的票据；已经完成时无操作。</summary>
        public void Dispose()
        {
            CameraTriggerTicketRegistry owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
                owner.Cancel(this);
        }
    }

    /// <summary>相机触发票据队列只读快照。</summary>
    public sealed class CameraTriggerTicketRegistrySnapshot
    {
        /// <summary>获取当前相机状态数量。</summary>
        public int CameraCount { get; internal set; }

        /// <summary>获取全部相机待匹配票据数。</summary>
        public int PendingTicketCount { get; internal set; }

        /// <summary>获取历史最大待匹配票据数。</summary>
        public int PeakPendingTicketCount { get; internal set; }

        /// <summary>获取累计匹配帧数。</summary>
        public long MatchedFrameCount { get; internal set; }

        /// <summary>获取累计帧号故障数。</summary>
        public long FrameSequenceFaultCount { get; internal set; }

        /// <summary>获取累计超时票据数。</summary>
        public long TimedOutTicketCount { get; internal set; }

        /// <summary>获取当前仍未从SDK触发调用返回的数量。</summary>
        public int ActiveTriggerDispatchCount { get; internal set; }

        /// <summary>获取必须完成停流排空后才能恢复的相机数量。</summary>
        public int CameraCountRequiringStreamReset { get; internal set; }

        /// <summary>获取尚未由独立派发线程处理完的生产故障数。</summary>
        public int PendingProductionFaultCount { get; internal set; }

        /// <summary>获取累计不会停止方案的可恢复相机告警数。</summary>
        public long RecoverableProductionFaultCount { get; internal set; }

        /// <summary>获取累计要求停止方案的相机故障数。</summary>
        public long StoppingProductionFaultCount { get; internal set; }

        /// <summary>获取当前等待后续工件执行的硬触发帧数。</summary>
        public int BufferedHardwareFrameCount { get; internal set; }

        /// <summary>获取历史最大待执行硬触发帧数。</summary>
        public int PeakBufferedHardwareFrameCount { get; internal set; }

        /// <summary>获取累计进入待执行队列的硬触发帧数。</summary>
        public long TotalBufferedHardwareFrameCount { get; internal set; }

        /// <summary>获取当前待执行硬触发Mat实际字节数。</summary>
        public long BufferedHardwareFrameBytes { get; internal set; }

        /// <summary>获取待执行硬触发Mat历史峰值字节数。</summary>
        public long PeakBufferedHardwareFrameBytes { get; internal set; }

        /// <summary>获取全部相机待执行硬触发Mat的参考报警字节预算。</summary>
        public long BufferedHardwareFrameBudgetBytes { get; internal set; }

        /// <summary>获取累计待执行硬触发帧积压报警数。</summary>
        public long BufferedHardwareFramePressureWarningCount { get; internal set; }
    }

    /// <summary>相机软件触发命令租约，用于区分未发命令、成功发出和结果未知三种退出路径。</summary>
    public interface ISoftwareTriggerCommandLease : IDisposable
    {
        /// <summary>标记调用方即将进入真实相机SDK命令。</summary>
        void MarkCommandStarted();

        /// <summary>标记真实相机SDK已经明确返回成功。</summary>
        void MarkCommandSucceeded();
    }

    /// <summary>按相机严格FIFO登记触发票据并把转换后Mat绑定到唯一工件。</summary>
    public sealed class CameraTriggerTicketRegistry : IDisposable
    {
        /// <summary>当前线程正在执行的唯一生产软件触发票据，供相机命令入口区分生产与预览。</summary>
        [ThreadStatic]
        private static CameraTriggerTicket _authorizedSoftwareTriggerTicket;

        /// <summary>单相机最大待匹配票据数。</summary>
        public const int MaximumPendingTicketsPerCamera = 64;

        /// <summary>单相机待执行硬触发帧每增长到该张数倍数时输出一次积压报警。</summary>
        public const int BufferedHardwareFrameWarningIntervalPerCamera = 8;

        /// <summary>没有资源档案时使用的全局待执行硬触发Mat预算。</summary>
        public const long DefaultBufferedHardwareFrameBudgetBytes = 256L * 1024L * 1024L;

        /// <summary>关闭时等待故障订阅者返回的最长时间，超时后由后台线程自行收尾。</summary>
        public const int FaultDispatcherShutdownTimeoutMs = 2000;

        /// <summary>关闭时等待真实SDK触发调用返回的最长时间。</summary>
        public const int TriggerDispatchShutdownTimeoutMs = 2000;

        /// <summary>保护全部相机票据链和帧号状态。</summary>
        private readonly object _syncRoot = new object();

        /// <summary>全部相机的独立FIFO状态。</summary>
        private readonly Dictionary<string, CameraTicketQueueState> _cameraStates =
            new Dictionary<string, CameraTicketQueueState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>保护有序故障待发队列，事件只在相机状态锁释放后派发。</summary>
        private readonly object _faultQueueSyncRoot = new object();

        /// <summary>按产生顺序等待控制层处理的生产故障。</summary>
        private readonly Queue<CameraProductionFault> _pendingProductionFaults =
            new Queue<CameraProductionFault>();

        /// <summary>唤醒独立故障派发线程。</summary>
        private readonly AutoResetEvent _faultDispatchSignal = new AutoResetEvent(false);

        /// <summary>不占用SDK回调和通用线程池的单消费者故障线程。</summary>
        private readonly Thread _faultDispatcherThread;

        /// <summary>注册表是否已经进入释放阶段。</summary>
        private int _disposed;

        /// <summary>当前生产运行会话是否仍允许原始队列中的帧进入票据或硬触发FIFO。</summary>
        private int _acceptingProductionFrames = 1;

        /// <summary>注册表是否已经完成票据清场和关闭屏障。</summary>
        private int _disposeCompleted;

        /// <summary>串行化首次关闭和超时后的重试关闭。</summary>
        private readonly object _disposeSyncRoot = new object();

        /// <summary>生产帧实际字节预算管理器。</summary>
        private readonly WorkpieceFrameMemoryBudgetManager _memoryBudgetManager;

        /// <summary>历史最大待匹配票据数。</summary>
        private int _peakPendingTicketCount;

        /// <summary>累计成功匹配帧数。</summary>
        private long _matchedFrameCount;

        /// <summary>累计帧号故障数。</summary>
        private long _frameSequenceFaultCount;

        /// <summary>累计超时票据数。</summary>
        private long _timedOutTicketCount;

        /// <summary>累计不会停止方案的相机生产告警数。</summary>
        private long _recoverableProductionFaultCount;

        /// <summary>累计要求停止方案的相机生产故障数。</summary>
        private long _stoppingProductionFaultCount;

        /// <summary>历史最大待执行硬触发帧数。</summary>
        private int _peakBufferedHardwareFrameCount;

        /// <summary>累计成功暂存的硬触发帧数。</summary>
        private long _bufferedHardwareFrameCount;

        /// <summary>累计已发出的待执行硬触发帧积压报警数。</summary>
        private long _bufferedHardwareFramePressureWarningCount;

        /// <summary>当前全部相机待执行硬触发Mat实际字节数。</summary>
        private long _bufferedHardwareFrameBytes;

        /// <summary>已从相机FIFO脱离、等待在全局状态锁外释放的硬触发帧。</summary>
        private List<BufferedHardwareFrame> _retiredBufferedHardwareFrames;

        /// <summary>历史最大待执行硬触发Mat实际字节数。</summary>
        private long _peakBufferedHardwareFrameBytes;

        /// <summary>全部相机待执行硬触发Mat的参考报警字节预算。</summary>
        private long _bufferedHardwareFrameBudgetBytes;

        /// <summary>创建使用默认硬触发缓存预算的票据注册表。</summary>
        public CameraTriggerTicketRegistry(WorkpieceFrameMemoryBudgetManager memoryBudgetManager)
            : this(memoryBudgetManager, DefaultBufferedHardwareFrameBudgetBytes)
        {
        }

        /// <summary>创建使用指定流程图像预算和硬触发缓存预算的票据注册表。</summary>
        public CameraTriggerTicketRegistry(
            WorkpieceFrameMemoryBudgetManager memoryBudgetManager,
            long bufferedHardwareFrameBudgetBytes)
        {
            _memoryBudgetManager = memoryBudgetManager ?? throw new ArgumentNullException(nameof(memoryBudgetManager));
            if (bufferedHardwareFrameBudgetBytes <= 0L)
                throw new ArgumentOutOfRangeException(nameof(bufferedHardwareFrameBudgetBytes));
            _bufferedHardwareFrameBudgetBytes = bufferedHardwareFrameBudgetBytes;
            _faultDispatcherThread = new Thread(DispatchProductionFaultLoop)
            {
                IsBackground = true,
                Name = "TDJS相机生产故障派发"
            };
            _faultDispatcherThread.Start();
        }

        /// <summary>生产帧无法安全交接时通知方案控制层停机。</summary>
        public event Action<CameraProductionFault> ProductionFaulted;

        /// <summary>获取当前注册表是否仍接收生产帧。</summary>
        internal bool IsAcceptingProductionFrames =>
            Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _acceptingProductionFrames) != 0;

        /// <summary>关闭当前运行会话的生产帧入口，并释放所有尚未绑定工件的硬触发Mat。</summary>
        internal void StopAcceptingProductionFrames()
        {
            if (Interlocked.Exchange(ref _acceptingProductionFrames, 0) == 0)
                return;

            List<string> cameraKeys;
            lock (_syncRoot)
                cameraKeys = new List<string>(_cameraStates.Keys);
            foreach (string cameraKey in cameraKeys)
            {
                ExecuteCameraSynchronized(cameraKey, () =>
                {
                    lock (_syncRoot)
                    {
                        if (!_cameraStates.TryGetValue(cameraKey, out CameraTicketQueueState state))
                            return true;
                        state.HardwareFrameBufferingEnabled = false;
                        DrainBufferedHardwareFramesUnderLock(state);
                    }
                    return true;
                }, true);
                DisposeRetiredBufferedHardwareFrames();
            }
        }

        /// <summary>仅在没有待执行Mat时应用新的全局硬触发缓存参考报警预算。</summary>
        public void UpdateBufferedHardwareFrameBudget(long budgetBytes)
        {
            if (budgetBytes <= 0L)
                throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_bufferedHardwareFrameBytes != 0L)
                    throw new InvalidOperationException("待执行硬触发帧尚未排空，禁止切换缓存字节预算。 ");
                _bufferedHardwareFrameBudgetBytes = budgetBytes;
            }
        }

        /// <summary>在触发/取消共用的票据锁内确认票据仍排队并原子武装。</summary>
        internal void MarkReadyForFrame(CameraTriggerTicket ticket, DateTime issuedAtUtc)
        {
            if (ticket == null)
                throw new ArgumentNullException(nameof(ticket));

            ExecuteCameraSynchronized(ticket.CameraKey, () =>
            {
                MarkReadyForFrameCore(ticket, issuedAtUtc, false);
                return true;
            });
        }

        /// <summary>原子提交软件触发后在锁外调用SDK，避免SDK同步等待回调时形成锁反转。</summary>
        internal void IssueSoftwareTrigger(CameraTriggerTicket ticket, Action triggerAction)
        {
            ExecuteCameraSynchronized(ticket.CameraKey, () =>
            {
                MarkReadyForFrameCore(ticket, DateTime.UtcNow, true);
                return true;
            });

            // 票据武装是触发提交点。取消若在此后发生，会锁定相机并清场迟到帧，
            // 但不能通过持锁阻塞SDK，因为部分SDK会等待另一线程的图像回调返回。
            try
            {
                CameraTriggerTicket previousAuthorizedTicket = _authorizedSoftwareTriggerTicket;
                _authorizedSoftwareTriggerTicket = ticket;
                try
                {
                    triggerAction();
                }
                finally
                {
                    _authorizedSoftwareTriggerTicket = previousAuthorizedTicket;
                }
            }
            catch (Exception exception)
            {
                FailTriggeredTicket(ticket, exception);
                throw;
            }
            finally
            {
                ExecuteCameraSynchronized(ticket.CameraKey, () =>
                {
                    lock (_syncRoot)
                    {
                        CameraTicketQueueState state = GetOrCreateStateUnderLock(ticket.CameraKey);
                        state.ActiveTriggerDispatchCount = Math.Max(0, state.ActiveTriggerDispatchCount - 1);
                    }
                    return true;
                }, true);
            }
        }

        /// <summary>调用方持有单相机锁时复核票据仍排队并写入首次武装时间。</summary>
        private void MarkReadyForFrameCore(
            CameraTriggerTicket ticket,
            DateTime issuedAtUtc,
            bool trackSoftwareDispatch)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (!_cameraStates.TryGetValue(ticket.CameraKey, out CameraTicketQueueState state) ||
                    state.Faulted ||
                    !ContainsPendingTicketUnderLock(state, ticket) ||
                    ticket.IsCompleted)
                {
                    throw new InvalidOperationException("相机票据已经取消、故障或离开等待队列，禁止继续触发。 ");
                }
                if (state.ActiveUncoordinatedTriggerDispatchCount != 0)
                    throw new InvalidOperationException($"相机{ticket.CameraKey}正在执行预览软件触发，生产票据本轮禁止同时触发。 ");
                if (state.ActiveTriggerDispatchCount != 0)
                    throw new InvalidOperationException($"相机{ticket.CameraKey}已有尚未返回的软件触发命令，本轮禁止并发进入SDK。 ");
                if (state.PendingTickets.First == null ||
                    !ReferenceEquals(state.PendingTickets.First.Value, ticket))
                {
                    throw new InvalidOperationException(
                        $"相机{ticket.CameraKey}的前序工件尚未取得生产帧，后序工件禁止提前触发。 ");
                }
                if (!ticket.TryMarkReadyForFrame(issuedAtUtc, Stopwatch.GetTimestamp()))
                    throw new InvalidOperationException("相机票据已经发送过触发，禁止重复触发。 ");
                if (trackSoftwareDispatch)
                    state.ActiveTriggerDispatchCount++;
            }
        }

        /// <summary>
        /// 相机软件触发命令进入前取得短生命周期门禁；生产授权调用可通过，预览调用与生产票据严格互斥。
        /// </summary>
        public ISoftwareTriggerCommandLease EnterSoftwareTriggerCommand(string cameraKey)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            bool countedAsUncoordinated = false;
            ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                lock (_syncRoot)
                {
                    ThrowIfDisposed();
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    if (state.Faulted || state.RequiresStreamRestart)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}处于生产帧故障状态，停流排空并重启确认前禁止软件触发。 ");
                    }
                    if (state.HardwareFrameBufferingEnabled || state.BufferedHardwareFrames.Count != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}已经进入生产硬触发持续接管，停流排空并重启前禁止执行预览或软件触发。 ");
                    }
                    CameraTriggerTicket authorizedTicket = _authorizedSoftwareTriggerTicket;
                    bool isAuthorizedProductionTrigger = authorizedTicket != null &&
                        string.Equals(authorizedTicket.CameraKey, normalizedCameraKey, StringComparison.OrdinalIgnoreCase) &&
                        ContainsPendingTicketUnderLock(state, authorizedTicket) &&
                        authorizedTicket.TriggerIssuedAtUtc.HasValue;
                    if (isAuthorizedProductionTrigger)
                    {
                        return true;
                    }

                    bool hasProductionTicket = state.InFlightTicket != null ||
                        state.PendingTickets.Count != 0;
                    if (hasProductionTicket || state.ActiveTriggerDispatchCount != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}正在等待生产工件帧，预览或手动软件触发已拒绝。 ");
                    }
                    if (state.ActiveUncoordinatedTriggerDispatchCount != 0)
                        throw new InvalidOperationException($"相机{normalizedCameraKey}已有预览软件触发命令正在执行。 ");
                    if (state.PendingUncoordinatedFrameCount >= MaximumPendingTicketsPerCamera)
                        throw new InvalidOperationException($"相机{normalizedCameraKey}待返回预览帧过多，禁止继续发送软件触发。 ");

                    state.ActiveUncoordinatedTriggerDispatchCount++;
                    countedAsUncoordinated = true;
                }
                return true;
            });
            return new SoftwareTriggerCommandLease(
                this,
                normalizedCameraKey,
                countedAsUncoordinated);
        }

        /// <summary>预览SDK命令开始前先登记待返回帧，允许同步回调在命令返回前正确消费。</summary>
        private void MarkSoftwareTriggerCommandStarted(string cameraKey, bool countedAsUncoordinated)
        {
            if (!countedAsUncoordinated)
                return;
            ExecuteCameraSynchronized(cameraKey, () =>
            {
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(cameraKey);
                    if (state.Faulted || state.RequiresStreamRestart ||
                        state.ActiveUncoordinatedTriggerDispatchCount == 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{cameraKey}软件触发门禁已经失效，禁止进入SDK命令。 ");
                    }
                    state.PendingUncoordinatedFrameCount = checked(
                        state.PendingUncoordinatedFrameCount + 1);
                }
                return true;
            });
        }

        /// <summary>归还一次预览或手动软件触发门禁。</summary>
        private void ReleaseSoftwareTriggerCommand(
            string cameraKey,
            bool countedAsUncoordinated,
            bool commandStarted,
            bool commandSucceeded)
        {
            if (!countedAsUncoordinated)
                return;
            ExecuteCameraSynchronized(cameraKey, () =>
            {
                lock (_syncRoot)
                {
                    if (_cameraStates.TryGetValue(cameraKey, out CameraTicketQueueState state))
                    {
                        state.ActiveUncoordinatedTriggerDispatchCount = Math.Max(
                            0,
                            state.ActiveUncoordinatedTriggerDispatchCount - 1);
                        if (commandStarted && !commandSucceeded)
                        {
                            if (state.PendingUncoordinatedFrameCount != 0)
                                state.PendingUncoordinatedFrameCount--;
                            state.Faulted = true;
                            RequireStreamRestartUnderLock(state);
                        }
                    }
                }
                return true;
            }, true);
        }

        /// <summary>真实软件触发调用抛出异常时锁定相机并结束全部相关票据。</summary>
        internal void FailTriggeredTicket(CameraTriggerTicket ticket, Exception exception)
        {
            if (ticket == null)
                return;

            ExecuteCameraSynchronized(ticket.CameraKey, () =>
            {
                FailTriggeredTicketCore(ticket, exception);
                return true;
            }, true);
        }

        /// <summary>调用方持有单相机锁时锁定相机并结束全部排队票据。</summary>
        private void FailTriggeredTicketCore(CameraTriggerTicket ticket, Exception exception)
        {
            List<CameraTriggerTicket> failedTickets = null;
            lock (_syncRoot)
            {
                if (_cameraStates.TryGetValue(ticket.CameraKey, out CameraTicketQueueState state))
                {
                    // 重连已经隔离旧票据；旧SDK调用随后返回异常不应升级为全方案停机。
                    if (ticket.ReconnectGeneration != state.ReconnectGeneration)
                        return;
                    state.Faulted = true;
                    // 票据可能已收到帧，但SDK命令仍可在随后失败或卡死，流身份仍不可信。
                    RequireStreamRestartUnderLock(state);
                    state.InFlightFailure = exception;
                    failedTickets = DrainPendingTicketsUnderLock(state);
                }
            }

            Exception failure = exception ?? new InvalidOperationException("软件触发命令执行失败。 ");
            FailTickets(failedTickets, ticket.CameraKey, failure, WorkpieceTerminalState.Stopped);
            RaiseProductionFault(new CameraProductionFault(
                ticket.CameraKey,
                ticket.Context.Identity,
                failure.Message,
                failure));
        }

        /// <summary>在真实相机触发前登记工件票据并预留估算图像内存。</summary>
        public CameraTriggerTicket Register(
            string cameraKey,
            WorkpieceExecutionContext context,
            CameraTriggerKind triggerKind,
            DateTime deadlineUtc,
            long estimatedFrameBytes,
            long memoryBudgetBytes)
        {
            ThrowIfDisposed();
            if (Volatile.Read(ref _acceptingProductionFrames) == 0)
                throw new InvalidOperationException("当前生产运行会话已经停止，禁止登记新的相机票据。 ");
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.TerminalState != WorkpieceTerminalState.Active || context.IsCompletionRequested)
                throw new InvalidOperationException("工件已经封口，禁止登记相机触发票据。 ");
            if (deadlineUtc.Kind != DateTimeKind.Utc || deadlineUtc <= DateTime.UtcNow)
                throw new ArgumentOutOfRangeException(nameof(deadlineUtc), "票据截止时间必须是未来UTC时间。 ");

            if (!context.TryAcquireExecutionLease(out IWorkpieceExecutionLease executionLease))
                throw new InvalidOperationException("工件已封口或取消，无法取得相机票据流程分支租约。 ");

            IWorkpieceFrameMemoryLease memoryLease = null;
            CameraTriggerTicket ticket = null;
            try
            {
                memoryLease = _memoryBudgetManager.Reserve(
                    context.Identity,
                    estimatedFrameBytes,
                    memoryBudgetBytes);
                ticket = new CameraTriggerTicket(
                    this,
                    normalizedCameraKey,
                    context,
                    triggerKind,
                    deadlineUtc,
                    memoryLease,
                    executionLease);
                memoryLease = null;
                executionLease = null;

                lock (_syncRoot)
                {
                    ThrowIfDisposed();
                    if (Volatile.Read(ref _acceptingProductionFrames) == 0)
                        throw new InvalidOperationException("当前生产运行会话已经停止，禁止登记新的相机票据。 ");
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    if (state.Faulted || state.RequiresStreamRestart)
                        throw new InvalidOperationException($"相机{normalizedCameraKey}处于生产帧故障状态，确认并重置前禁止继续触发。 ");
                    if (state.ActiveUncoordinatedTriggerDispatchCount != 0 ||
                        state.PendingUncoordinatedFrameCount != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}仍有预览软件触发或待返回预览帧，禁止登记生产工件。 ");
                    }
                    if (triggerKind != CameraTriggerKind.Hardware &&
                        (state.HardwareFrameBufferingEnabled || state.BufferedHardwareFrames.Count != 0))
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}已经进入生产硬触发持续接管，停流排空并重启前禁止切换采集模式。 ");
                    }
                    int occupancy = state.PendingTickets.Count + (state.InFlightTicket == null ? 0 : 1);
                    if (occupancy >= MaximumPendingTicketsPerCamera)
                        throw new InvalidOperationException($"相机{normalizedCameraKey}待匹配票据已达到{MaximumPendingTicketsPerCamera}，必须暂停触发并报警。 ");
                    if (state.InFlightTicket != null && state.InFlightTicket.Context.Identity == context.Identity)
                        throw new InvalidOperationException($"工件{context.Identity}已经在相机{normalizedCameraKey}执行帧匹配，禁止重复登记。 ");
                    foreach (CameraTriggerTicket pending in state.PendingTickets)
                    {
                        if (pending.Context.Identity == context.Identity)
                            throw new InvalidOperationException($"工件{context.Identity}已经登记相机{normalizedCameraKey}，禁止重复登记。 ");
                    }

                    ticket.ReconnectGeneration = state.ReconnectGeneration;
                    state.PendingTickets.AddLast(ticket);
                    UpdatePeakPendingUnderLock();
                }

                return ticket;
            }
            catch (Exception exception)
            {
                if (ticket != null)
                    ticket.TryFail(exception, WorkpieceTerminalState.Faulted);
                else
                {
                    memoryLease?.Dispose();
                    executionLease?.Complete(WorkpieceTerminalState.Faulted);
                }
                throw;
            }
        }

        /// <summary>原子登记连续采集等待；空闲帧不缓存，预算租约仍随输出图像释放。</summary>
        /// <param name="cameraKey">稳定相机键。</param>
        /// <param name="context">当前工件上下文。</param>
        /// <param name="deadlineUtc">本轮采图截止时间。</param>
        /// <param name="estimatedFrameBytes">预留的估算图像字节数。</param>
        /// <param name="memoryBudgetBytes">图像总预算。</param>
        /// <returns>已就绪的连续采集票据。</returns>
        public CameraTriggerTicket RegisterContinuousWait(
            string cameraKey,
            WorkpieceExecutionContext context,
            DateTime deadlineUtc,
            long estimatedFrameBytes,
            long memoryBudgetBytes)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            return ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                CameraTriggerTicket ticket = Register(normalizedCameraKey, context,
                    CameraTriggerKind.Continuous, deadlineUtc, estimatedFrameBytes, memoryBudgetBytes);
                try
                {
                    MarkReadyForFrameCore(ticket, DateTime.UtcNow, false);
                    return ticket;
                }
                catch
                {
                    ticket.Dispose();
                    throw;
                }
            });
        }

        /// <summary>原子登记并武装线路硬触发票据，确保物理帧不能穿过登记与就绪之间的空窗。</summary>
        /// <param name="cameraKey">稳定相机键。</param>
        /// <param name="context">当前工件上下文。</param>
        /// <param name="deadlineUtc">硬触发帧最迟到达时间。</param>
        /// <param name="estimatedFrameBytes">触发前预留的估算图像字节数。</param>
        /// <param name="memoryBudgetBytes">本次运行使用的图像总预算。</param>
        /// <returns>已经进入硬触发等待状态的唯一票据。</returns>
        public CameraTriggerTicket RegisterHardwareWait(
            string cameraKey,
            WorkpieceExecutionContext context,
            DateTime deadlineUtc,
            long estimatedFrameBytes,
            long memoryBudgetBytes)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            return ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                CameraTriggerTicket ticket = null;
                BufferedHardwareFrame bufferedFrame = null;
                try
                {
                    ticket = Register(
                        normalizedCameraKey,
                        context,
                        CameraTriggerKind.Hardware,
                        deadlineUtc,
                        estimatedFrameBytes,
                        memoryBudgetBytes);
                    MarkReadyForFrameCore(ticket, DateTime.UtcNow, false);
                    lock (_syncRoot)
                    {
                        CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                        state.HardwareFrameBufferingEnabled = true;
                        if (state.BufferedHardwareFrames.Count != 0)
                            bufferedFrame = DequeueBufferedHardwareFrameUnderLock(state);
                    }
                    if (bufferedFrame != null)
                    {
                        Mat bufferedImage = bufferedFrame.DetachImage();
                        bool bufferedImageAccepted = false;
                        try
                        {
                            bufferedImageAccepted = TryPublishFrameCore(
                                normalizedCameraKey,
                                bufferedFrame.FrameId,
                                bufferedFrame.SdkFrameNumber,
                                bufferedFrame.CallbackStartedTimestamp,
                                bufferedImage,
                                bufferedFrame.TraceInfo,
                                true);
                            if (bufferedImageAccepted)
                                bufferedImage = null;
                            else
                                throw new InvalidOperationException(
                                    $"相机{normalizedCameraKey}的待执行硬触发帧未被生产票据接管。 ");
                        }
                        finally
                        {
                            SafeDispose(bufferedImage);
                            bufferedFrame.Dispose();
                        }
                    }
                    return ticket;
                }
                catch
                {
                    try { ticket?.Dispose(); } catch { }
                    throw;
                }
            });
        }

        /// <summary>把转换线程产出的独立Mat匹配给该相机FIFO队首票据。</summary>
        public bool TryPublishFrame(
            string cameraKey,
            long frameId,
            uint sdkFrameNumber,
            long callbackStartedTimestamp,
            Mat image,
            CameraFrameTraceInfo traceInfo)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            if (callbackStartedTimestamp <= 0L)
                throw new ArgumentOutOfRangeException(nameof(callbackStartedTimestamp));
            if (image == null || image.Empty())
                return false;

            return ExecuteCameraSynchronized(normalizedCameraKey, () => TryPublishFrameCore(
                normalizedCameraKey,
                frameId,
                sdkFrameNumber,
                callbackStartedTimestamp,
                image,
                traceInfo,
                false));
        }

        /// <summary>相机转换线程使用的无异常帧路由入口；旧注册表关闭或释放后统一返回false。</summary>
        internal bool TryRouteFrame(
            string cameraKey,
            long frameId,
            uint sdkFrameNumber,
            long callbackStartedTimestamp,
            Mat image,
            CameraFrameTraceInfo traceInfo)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return false;
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            if (callbackStartedTimestamp <= 0L)
                throw new ArgumentOutOfRangeException(nameof(callbackStartedTimestamp));
            if (image == null || image.Empty())
                return false;

            try
            {
                return ExecuteCameraSynchronized(normalizedCameraKey, () => TryPublishFrameCore(
                    normalizedCameraKey,
                    frameId,
                    sdkFrameNumber,
                    callbackStartedTimestamp,
                    image,
                    traceInfo,
                    false));
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>预览帧在原始入口或Mat转换阶段失败时，无异常归还一次待返回预览计数。</summary>
        internal bool TryAcknowledgePreviewFrameFailure(string cameraKey)
        {
            if (Volatile.Read(ref _disposed) != 0 || string.IsNullOrWhiteSpace(cameraKey))
                return false;
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            try
            {
                return ExecuteCameraSynchronized(normalizedCameraKey, () =>
                {
                    lock (_syncRoot)
                    {
                        if (!_cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state) ||
                            state.PendingUncoordinatedFrameCount == 0)
                        {
                            return false;
                        }
                        state.PendingUncoordinatedFrameCount--;
                        return true;
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>判断没有节点临时订阅者时是否仍需持续接管该相机的硬触发帧。</summary>
        public bool ShouldCaptureBufferedHardwareFrame(string cameraKey)
        {
            if (Volatile.Read(ref _disposed) != 0 || string.IsNullOrWhiteSpace(cameraKey))
                return false;
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            lock (_syncRoot)
            {
                return _cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state) &&
                    state.HardwareFrameBufferingEnabled &&
                    !state.Faulted &&
                    !state.RequiresStreamRestart;
            }
        }

        /// <summary>调用方持有候选票据锁时复核队首并完成实际帧发布。</summary>
        private bool TryPublishFrameCore(
            string normalizedCameraKey,
            long frameId,
            uint sdkFrameNumber,
            long callbackStartedTimestamp,
            Mat image,
            CameraFrameTraceInfo traceInfo,
            bool isBufferedHardwareFrame)
        {

            CameraTriggerTicket ticket = null;
            Exception failure = null;
            CameraProductionFault productionFault = null;
            List<CameraTriggerTicket> failedTickets = null;
            bool bufferedFrameAccepted = false;
            lock (_syncRoot)
            {
                if (!_cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state))
                    return false;

                // 停止、故障或等待物理重启期间禁止把迟到帧重新积压到下一轮。
                if (state.Faulted || state.RequiresStreamRestart)
                    return false;

                if (state.PendingUncoordinatedFrameCount != 0)
                {
                    state.PendingUncoordinatedFrameCount--;
                    state.LastFrameId = frameId;
                    state.LastSdkFrameNumber = sdkFrameNumber;
                    state.HasAcceptedFrame = true;
                    return false;
                }

                if (Volatile.Read(ref _acceptingProductionFrames) == 0)
                    return false;

                if (state.PendingTickets.Count == 0 && state.InFlightTicket == null)
                {
                    if (!state.HardwareFrameBufferingEnabled)
                        return false;
                    long bufferedFrameBytes = GetMatBytes(image);
                    long updatedBufferedBytes = checked(
                        _bufferedHardwareFrameBytes + bufferedFrameBytes);
                    state.BufferedHardwareFrames.Enqueue(new BufferedHardwareFrame(
                        frameId,
                        sdkFrameNumber,
                        callbackStartedTimestamp,
                        image,
                        traceInfo,
                        bufferedFrameBytes));
                    _bufferedHardwareFrameBytes = updatedBufferedBytes;
                    if (_bufferedHardwareFrameBytes > _peakBufferedHardwareFrameBytes)
                        _peakBufferedHardwareFrameBytes = _bufferedHardwareFrameBytes;
                    Interlocked.Increment(ref _bufferedHardwareFrameCount);
                    UpdatePeakBufferedHardwareFramesUnderLock();
                    bufferedFrameAccepted = true;

                    int bufferedCount = state.BufferedHardwareFrames.Count;
                    if (bufferedCount >= state.NextBufferedHardwareFrameWarningCount)
                    {
                        productionFault = new CameraProductionFault(
                            normalizedCameraKey,
                            null,
                            $"相机{normalizedCameraKey}待执行硬触发帧积压报警：" +
                            $"当前={bufferedCount}张/{_bufferedHardwareFrameBytes}字节，" +
                            $"报警间隔={BufferedHardwareFrameWarningIntervalPerCamera}张，" +
                            $"参考内存预算={_bufferedHardwareFrameBudgetBytes}字节。" +
                            "队列不设张数和字节拒绝上限，方案继续运行，请检查触发频率与流程耗时。",
                            null,
                            false);
                        Interlocked.Increment(ref _bufferedHardwareFramePressureWarningCount);
                        state.NextBufferedHardwareFrameWarningCount =
                            bufferedCount > int.MaxValue - BufferedHardwareFrameWarningIntervalPerCamera
                                ? int.MaxValue
                                : bufferedCount + BufferedHardwareFrameWarningIntervalPerCamera;
                    }
                }
                else if (state.InFlightTicket != null)
                {
                    state.Faulted = true;
                    ticket = state.InFlightTicket;
                    failure = new InvalidOperationException($"相机{normalizedCameraKey}出现并发帧发布，单相机生产帧必须顺序交接。 ");
                    state.InFlightFailure = failure;
                    failedTickets = DrainPendingTicketsUnderLock(state);
                    productionFault = new CameraProductionFault(
                        normalizedCameraKey,
                        ticket.Context.Identity,
                        failure.Message,
                        failure);
                }
                else
                {
                    ticket = state.PendingTickets.First.Value;
                    // 连续流允许跳过空闲帧，但不把本轮等待之前已进入回调的帧交给当前节点。
                    if (ticket.TriggerKind == CameraTriggerKind.Continuous &&
                        (callbackStartedTimestamp < ticket.TriggerIssuedTimestamp ||
                            (state.HasAcceptedFrame && frameId <= state.LastFrameId)))
                        return false;
                    if (!ticket.TriggerIssuedAtUtc.HasValue)
                    {
                        state.Faulted = true;
                        failure = new InvalidOperationException($"相机{normalizedCameraKey}在票据明确进入触发等待前收到生产帧，禁止把旧帧绑定给工件。 ");
                    }
                    // 软件触发必须保持命令与返回帧的因果关系；线路硬触发由外部独立产生，按连续帧号FIFO归属。
                    else if (ticket.TriggerKind == CameraTriggerKind.Software &&
                        !isBufferedHardwareFrame &&
                        callbackStartedTimestamp < ticket.TriggerIssuedTimestamp)
                    {
                        state.Faulted = true;
                        failure = new InvalidOperationException($"相机{normalizedCameraKey}收到触发前已进入SDK回调的旧帧，禁止绑定给工件{ticket.Context.Identity}。 ");
                    }
                    else if (ticket.DeadlineUtc <= DateTime.UtcNow)
                    {
                        state.Faulted = true;
                        _timedOutTicketCount++;
                        failure = new TimeoutException($"相机{normalizedCameraKey}等待工件{ticket.Context.Identity}帧超时。 ");
                    }
                    else if (ticket.TriggerKind != CameraTriggerKind.Continuous &&
                        !IsNextFrameNumber(state, frameId, sdkFrameNumber, out string sequenceReason))
                    {
                        state.Faulted = true;
                        _frameSequenceFaultCount++;
                        failure = new InvalidOperationException(sequenceReason);
                    }

                    if (failure != null)
                    {
                        // 任一无法可信绑定的入站帧都证明流中可能仍有旧缓存，普通重置不安全。
                        RequireStreamRestartUnderLock(state);
                        failedTickets = DrainPendingTicketsUnderLock(state);
                        productionFault = new CameraProductionFault(
                            normalizedCameraKey,
                            ticket.Context.Identity,
                            failure.Message,
                            failure,
                            !(failure is TimeoutException));
                    }
                    else
                    {
                        state.PendingTickets.RemoveFirst();
                        state.InFlightTicket = ticket;
                    }
                }
            }

            if (bufferedFrameAccepted)
            {
                RaiseProductionFault(productionFault);
                return true;
            }

            if (failure != null)
            {
                SafeDispose(image);
                if (ticket != null)
                {
                    FailPrimaryAndBlockedTickets(
                        failedTickets,
                        ticket,
                        normalizedCameraKey,
                        failure,
                        failure is TimeoutException
                            ? WorkpieceTerminalState.TimedOut
                            : WorkpieceTerminalState.Stopped);
                }
                else
                {
                    FailTickets(failedTickets, normalizedCameraKey, failure, WorkpieceTerminalState.Stopped);
                }
                RaiseProductionFault(productionFault);
                return true;
            }

            IWorkpieceFrameMemoryLease memoryLease = null;
            CameraFrameEnvelope envelope = null;
            try
            {
                memoryLease = ticket.DetachMemoryLease();
                long actualBytes = GetProductionOutputBytes(image);
                if (memoryLease == null || !memoryLease.TryCommitActualBytes(actualBytes))
                {
                    WorkpieceFrameMemoryBudgetSnapshot budgetSnapshot = _memoryBudgetManager.GetSnapshot();
                    SafeDispose(image);
                    SafeDispose(memoryLease);
                    memoryLease = null;
                    failure = new WorkpieceFrameMemoryBudgetExceededException(
                        actualBytes,
                        budgetSnapshot.ReservedBytes,
                        budgetSnapshot.BudgetBytes);
                    lock (_syncRoot)
                    {
                        if (_cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state))
                        {
                            state.Faulted = true;
                            failedTickets = DrainPendingTicketsUnderLock(state);
                            if (ReferenceEquals(state.InFlightTicket, ticket))
                            {
                                state.InFlightTicket = null;
                                state.InFlightFailure = null;
                                failedTickets.Insert(0, ticket);
                            }
                        }
                    }
                    FailTickets(failedTickets, normalizedCameraKey, failure, WorkpieceTerminalState.Stopped);
                    RaiseProductionFault(new CameraProductionFault(
                        normalizedCameraKey,
                        ticket.Context.Identity,
                        "转换后Mat实际字节超过工件图像预算。",
                        failure));
                    return true;
                }

                DateTime triggerIssuedAtUtc = ticket.TriggerIssuedAtUtc.Value;
                envelope = new CameraFrameEnvelope(
                    ticket.Context.Identity,
                    normalizedCameraKey,
                    ticket.TicketId,
                    frameId,
                    sdkFrameNumber,
                    triggerIssuedAtUtc,
                    DateTime.UtcNow,
                    image,
                    memoryLease,
                    traceInfo);
                memoryLease = null;
            }
            catch (Exception exception)
            {
                SafeDispose(envelope);
                if (envelope == null)
                {
                    SafeDispose(image);
                    SafeDispose(memoryLease);
                }

                failure = new InvalidOperationException(
                    $"相机{normalizedCameraKey}转换帧构造生产信封失败。",
                    exception);
                lock (_syncRoot)
                {
                    if (_cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state))
                    {
                        state.Faulted = true;
                        failedTickets = DrainPendingTicketsUnderLock(state);
                        if (ReferenceEquals(state.InFlightTicket, ticket))
                        {
                            state.InFlightTicket = null;
                            state.InFlightFailure = null;
                            failedTickets.Insert(0, ticket);
                        }
                    }
                }
                FailTickets(failedTickets, normalizedCameraKey, failure, WorkpieceTerminalState.Stopped);
                RaiseProductionFault(new CameraProductionFault(
                    normalizedCameraKey,
                    ticket.Context.Identity,
                    failure.Message,
                    failure));
                return true;
            }

            bool mayComplete;
            Exception publicationFailure = null;
            lock (_syncRoot)
            {
                mayComplete = _cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state) &&
                    ReferenceEquals(state.InFlightTicket, ticket) &&
                    !state.Faulted;
                if (_cameraStates.TryGetValue(normalizedCameraKey, out state) &&
                    ReferenceEquals(state.InFlightTicket, ticket))
                {
                    state.InFlightTicket = null;
                    publicationFailure = state.InFlightFailure;
                    state.InFlightFailure = null;
                    if (mayComplete)
                    {
                        state.LastFrameId = frameId;
                        state.LastSdkFrameNumber = sdkFrameNumber;
                        state.HasAcceptedFrame = true;
                    }
                }
            }
            if (!mayComplete)
            {
                SafeDispose(envelope);
                ticket.TryFail(
                    publicationFailure ?? new InvalidOperationException($"相机{normalizedCameraKey}在帧发布期间已进入故障，当前帧不得继续绑定工件。 "),
                    publicationFailure is TimeoutException
                        ? WorkpieceTerminalState.TimedOut
                        : WorkpieceTerminalState.Stopped);
                return true;
            }

            if (!ticket.TryComplete(envelope))
            {
                SafeDispose(envelope);
                return true;
            }

            Interlocked.Increment(ref _matchedFrameCount);
            return true;
        }

        /// <summary>让SDK回调入口在原始缓冲耗尽等情况下立即失败队首生产票据。</summary>
        public bool TryFailNext(string cameraKey, Exception exception)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            return ExecuteCameraSynchronized(
                normalizedCameraKey,
                () => TryFailNextCore(normalizedCameraKey, exception));
        }

        /// <summary>调用方持有候选票据锁时复核并失败该相机全部生产票据。</summary>
        private bool TryFailNextCore(string normalizedCameraKey, Exception exception)
        {
            CameraTriggerTicket ticket = null;
            List<CameraTriggerTicket> failedTickets;
            lock (_syncRoot)
            {
                if (Volatile.Read(ref _acceptingProductionFrames) == 0)
                    return false;
                if (!_cameraStates.TryGetValue(normalizedCameraKey, out CameraTicketQueueState state))
                    return false;
                if (state.Faulted || state.RequiresStreamRestart)
                    return false;

                ticket = state.InFlightTicket ?? state.PendingTickets.First?.Value;
                if (ticket == null && !state.HardwareFrameBufferingEnabled)
                    return false;

                state.Faulted = true;
                RequireStreamRestartUnderLock(state);
                state.InFlightFailure = exception;
                failedTickets = DrainPendingTicketsUnderLock(state);
            }

            Exception effectiveException = exception ?? new InvalidOperationException("相机生产帧入口失败。 ");
            FailTickets(failedTickets, normalizedCameraKey, effectiveException, WorkpieceTerminalState.Stopped);
            RaiseProductionFault(new CameraProductionFault(
                normalizedCameraKey,
                ticket?.Context.Identity,
                effectiveException.Message,
                effectiveException));
            return true;
        }

        /// <summary>
        /// 按发生故障的SDK回调时刻归属生产票据，禁止把延迟报告的旧故障错误归给后一工件。
        /// </summary>
        public bool TryFailFrame(
            string cameraKey,
            long frameId,
            long callbackStartedTimestamp,
            Exception exception)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            if (frameId <= 0L)
                throw new ArgumentOutOfRangeException(nameof(frameId));
            if (callbackStartedTimestamp <= 0L)
                throw new ArgumentOutOfRangeException(nameof(callbackStartedTimestamp));

            return ExecuteCameraSynchronized(normalizedCameraKey, () => TryFailFrameCore(
                normalizedCameraKey,
                frameId,
                callbackStartedTimestamp,
                exception));
        }

        /// <summary>调用方持有单相机锁时记录流故障，并只失败触发提交早于该回调的票据。</summary>
        private bool TryFailFrameCore(
            string normalizedCameraKey,
            long frameId,
            long callbackStartedTimestamp,
            Exception exception)
        {
            CameraTriggerTicket affectedTicket = null;
            List<CameraTriggerTicket> failedTickets;
            bool failurePredatesAffectedTicket = false;
            lock (_syncRoot)
            {
                if (Volatile.Read(ref _acceptingProductionFrames) == 0)
                    return false;
                CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                if (state.Faulted || state.RequiresStreamRestart)
                    return false;
                CameraTriggerTicket candidate = state.InFlightTicket ?? state.PendingTickets.First?.Value;
                if (candidate == null && !state.HardwareFrameBufferingEnabled)
                    return false;

                RequireStreamRestartUnderLock(state);
                affectedTicket = candidate;
                failurePredatesAffectedTicket = candidate != null &&
                    (!candidate.TriggerIssuedAtUtc.HasValue ||
                    candidate.TriggerIssuedTimestamp > callbackStartedTimestamp);
                state.Faulted = true;
                state.InFlightFailure = exception;
                failedTickets = DrainPendingTicketsUnderLock(state);
            }

            Exception effectiveException = affectedTicket == null
                ? (Exception)new InvalidOperationException(
                    $"相机{normalizedCameraKey}持续接管硬触发帧期间，FrameId={frameId}转换或交接失败。 ",
                    exception)
                : failurePredatesAffectedTicket
                ? (Exception)new InvalidOperationException(
                    $"相机{normalizedCameraKey}在当前工件触发前已有FrameId={frameId}入口故障，当前及后续工件必须停止，禁止把旧故障归为当前工件采集失败。 ",
                    exception)
                : exception ?? new InvalidOperationException(
                    $"相机{normalizedCameraKey}的FrameId={frameId}未完成生产帧交接。 ");
            FailTickets(failedTickets, normalizedCameraKey, effectiveException, WorkpieceTerminalState.Stopped);
            RaiseProductionFault(new CameraProductionFault(
                normalizedCameraKey,
                affectedTicket?.Context.Identity,
                effectiveException.Message,
                effectiveException));
            return true;
        }

        /// <summary>按节点本次等待结果只清场目标相机，禁止用未来扫描时间误伤其他相机票据。</summary>
        internal int FailTimedOutTicket(
            CameraTriggerTicket ticket,
            TimeoutException exception)
        {
            if (ticket == null)
                return 0;

            return ExecuteCameraSynchronized(ticket.CameraKey, () =>
            {
                List<CameraTriggerTicket> failedTickets;
                bool ticketFound;
                lock (_syncRoot)
                {
                    if (!_cameraStates.TryGetValue(ticket.CameraKey, out CameraTicketQueueState state))
                        return 0;

                    bool wasInFlight = ReferenceEquals(state.InFlightTicket, ticket);
                    ticketFound = wasInFlight || ContainsPendingTicketUnderLock(state, ticket);
                    if (!ticketFound)
                        return 0;

                    state.Faulted = true;
                    RequireStreamRestartUnderLock(state);
                    state.InFlightFailure = exception;
                    failedTickets = DrainPendingTicketsUnderLock(state);
                    if (wasInFlight)
                    {
                        state.InFlightTicket = null;
                        state.InFlightFailure = null;
                        failedTickets.Insert(0, ticket);
                    }
                    _timedOutTicketCount++;
                }

                TimeoutException failure = exception ?? new TimeoutException(
                    $"相机{ticket.CameraKey}等待工件{ticket.Context.Identity}帧超时。 ");
                FailPrimaryAndBlockedTickets(
                    failedTickets,
                    ticket,
                    ticket.CameraKey,
                    failure,
                    WorkpieceTerminalState.TimedOut);
                RaiseProductionFault(new CameraProductionFault(
                    ticket.CameraKey,
                    ticket.Context.Identity,
                    failure.Message,
                    failure,
                    false));
                return failedTickets.Count;
            }, true);
        }

        /// <summary>清理所有自然到期票据并要求对应相机清流恢复，但普通取帧超时不停止整套方案。</summary>
        public int FailExpired(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("扫描时间必须使用UTC。", nameof(utcNow));

            List<string> cameraKeys;
            lock (_syncRoot)
                cameraKeys = new List<string>(_cameraStates.Keys);

            int failedCount = 0;
            foreach (string cameraKey in cameraKeys)
            {
                if (TryExecuteCameraSynchronized(
                    cameraKey,
                    () => FailExpiredCameraCore(cameraKey, utcNow),
                    out int cameraFailedCount))
                {
                    failedCount += cameraFailedCount;
                }
            }
            return failedCount;
        }

        /// <summary>调用方持有单相机锁时清理该相机全部到期和被阻断票据。</summary>
        private int FailExpiredCameraCore(string cameraKey, DateTime utcNow)
        {
            List<Tuple<CameraTriggerTicket, Exception>> failedTickets =
                new List<Tuple<CameraTriggerTicket, Exception>>();
            List<CameraProductionFault> inFlightFaults = new List<CameraProductionFault>();
            lock (_syncRoot)
            {
                if (!_cameraStates.TryGetValue(cameraKey, out CameraTicketQueueState state))
                    return 0;

                bool hasExpiredTicket = state.InFlightTicket != null &&
                    state.InFlightTicket.DeadlineUtc <= utcNow;
                foreach (CameraTriggerTicket pendingTicket in state.PendingTickets)
                {
                    if (pendingTicket.DeadlineUtc <= utcNow)
                    {
                        hasExpiredTicket = true;
                        break;
                    }
                }
                if (!hasExpiredTicket)
                    return 0;

                state.Faulted = true;
                if (state.InFlightTicket != null)
                {
                    bool inFlightExpired = state.InFlightTicket.DeadlineUtc <= utcNow;
                    state.InFlightFailure = inFlightExpired
                        ? (Exception)new TimeoutException($"相机{cameraKey}在途生产帧发布超时。 ")
                        : new InvalidOperationException($"相机{cameraKey}前序生产票据超时，在途帧必须停止发布。 ");
                    if (inFlightExpired)
                        _timedOutTicketCount++;
                    inFlightFaults.Add(new CameraProductionFault(
                        cameraKey,
                        state.InFlightTicket.Context.Identity,
                        state.InFlightFailure.Message,
                        state.InFlightFailure,
                        !(state.InFlightFailure is TimeoutException)));
                }
                foreach (CameraTriggerTicket failedTicket in DrainPendingTicketsUnderLock(state))
                {
                    Exception failure = failedTicket.DeadlineUtc <= utcNow
                        ? (Exception)new TimeoutException($"相机{failedTicket.CameraKey}等待工件{failedTicket.Context.Identity}帧超时。 ")
                        : new InvalidOperationException($"相机{cameraKey}前序生产票据超时，后续工件禁止继续等待。 ");
                    if (failure is TimeoutException)
                        _timedOutTicketCount++;
                    failedTickets.Add(Tuple.Create(failedTicket, failure));
                }
            }

            foreach (Tuple<CameraTriggerTicket, Exception> failedTicket in failedTickets)
            {
                CameraTriggerTicket ticket = failedTicket.Item1;
                Exception failure = failedTicket.Item2;
                ticket.TryFail(
                    failure,
                    failure is TimeoutException
                        ? WorkpieceTerminalState.TimedOut
                        : WorkpieceTerminalState.Stopped);
                RaiseProductionFault(new CameraProductionFault(
                    ticket.CameraKey,
                    ticket.Context.Identity,
                    failure.Message,
                    failure,
                    false));
            }
            foreach (CameraProductionFault inFlightFault in inFlightFaults)
                RaiseProductionFault(inFlightFault);

            return failedTickets.Count + inFlightFaults.Count;
        }

        /// <summary>取消票据；真实触发已经开始时必须故障清场，禁止迟到帧错配下一工件。</summary>
        internal void Cancel(CameraTriggerTicket ticket)
        {
            if (ticket == null)
                return;

            ExecuteCameraSynchronized(ticket.CameraKey, () =>
            {
                CancelCore(ticket);
                return true;
            }, true);
        }

        /// <summary>调用方持有单相机锁时执行取消或已触发故障清场。</summary>
        private void CancelCore(CameraTriggerTicket ticket)
        {
            bool removed = false;
            bool mustFaultCamera = false;
            List<CameraTriggerTicket> failedTickets = null;
            lock (_syncRoot)
            {
                if (_cameraStates.TryGetValue(ticket.CameraKey, out CameraTicketQueueState state))
                {
                    if (ReferenceEquals(state.InFlightTicket, ticket))
                    {
                        state.Faulted = true;
                        mustFaultCamera = true;
                        state.InFlightFailure = new InvalidOperationException(
                            $"相机{ticket.CameraKey}的在途票据收到取消请求。 ");
                        failedTickets = DrainPendingTicketsUnderLock(state);
                    }

                    LinkedListNode<CameraTriggerTicket> node = state.PendingTickets.First;
                    while (!mustFaultCamera && node != null)
                    {
                        LinkedListNode<CameraTriggerTicket> next = node.Next;
                        if (ReferenceEquals(node.Value, ticket))
                        {
                            if (ticket.TriggerIssuedAtUtc.HasValue)
                            {
                                state.Faulted = true;
                                mustFaultCamera = true;
                                failedTickets = DrainPendingTicketsUnderLock(state);
                            }
                            else
                            {
                                state.PendingTickets.Remove(node);
                                removed = true;
                            }
                            break;
                        }
                        node = next;
                    }
                }
            }

            if (removed)
                ticket.TryCancel();
            if (mustFaultCamera)
            {
                InvalidOperationException failure = new InvalidOperationException(
                    $"相机{ticket.CameraKey}的工件{ticket.Context.Identity}在真实触发后取消，迟到帧身份不再可信。 ");
                FailTickets(failedTickets, ticket.CameraKey, failure, WorkpieceTerminalState.Stopped);
                RaiseProductionFault(new CameraProductionFault(
                    ticket.CameraKey,
                    ticket.Context.Identity,
                    failure.Message,
                    failure));
            }
        }

        /// <summary>隔离单相机旧票据和缓存帧，保留物理重启门禁，不发布全方案停机故障。</summary>
        /// <param name="cameraKey">断线相机的稳定标识。</param>
        internal void SuspendCameraForReconnect(string cameraKey)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                List<CameraTriggerTicket> failedTickets;
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    state.ReconnectGeneration = checked(state.ReconnectGeneration + 1L);
                    state.Faulted = true;
                    RequireStreamRestartUnderLock(state);
                    failedTickets = DrainPendingTicketsUnderLock(state);
                    state.HardwareFrameBufferingEnabled = false;
                }
                FailTickets(failedTickets, normalizedCameraKey,
                    new InvalidOperationException($"相机{normalizedCameraKey}掉线，本轮取图失败，等待自动重连；方案继续运行。"),
                    WorkpieceTerminalState.Faulted);
                return true;
            });
            DisposeRetiredBufferedHardwareFrames();
        }

        /// <summary>在人工确认且当前没有待匹配票据时清除相机故障状态。</summary>
        public void ResetCamera(string cameraKey)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    if (state.PendingTickets.Count != 0 ||
                        state.InFlightTicket != null ||
                        state.BufferedHardwareFrames.Count != 0 ||
                        state.ActiveTriggerDispatchCount != 0 ||
                        state.ActiveUncoordinatedTriggerDispatchCount != 0 ||
                        state.PendingUncoordinatedFrameCount != 0 ||
                        state.PendingBlockingProductionFaultCount != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}仍有待匹配票据、在途帧或未返回的SDK触发调用，禁止重置故障。 ");
                    }
                    if (state.RequiresStreamRestart)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}清场过已武装票据，必须先完成停流、回调排空和重新取流确认。 ");
                    }
                    state.Faulted = false;
                    state.InFlightFailure = null;
                    state.HasAcceptedFrame = false;
                    state.LastFrameId = 0L;
                    state.LastSdkFrameNumber = 0U;
                }
                return true;
            });
        }

        /// <summary>相机已经停流并排空回调后，记录本次重新取流前的故障代数。</summary>
        internal long PrepareCameraStreamReset(string cameraKey)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            long preparedFaultGeneration = ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    if (state.PendingTickets.Count != 0 ||
                        state.InFlightTicket != null ||
                        state.ActiveTriggerDispatchCount != 0 ||
                        state.ActiveUncoordinatedTriggerDispatchCount != 0 ||
                        state.PendingBlockingProductionFaultCount != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}尚未排空票据、帧或SDK触发调用，禁止准备流重置。 ");
                    }
                    DrainBufferedHardwareFramesUnderLock(state);
                    state.HardwareFrameBufferingEnabled = false;
                    state.PendingUncoordinatedFrameCount = 0;
                    return state.StreamFaultGeneration;
                }
            });
            DisposeRetiredBufferedHardwareFrames();
            return preparedFaultGeneration;
        }

        /// <summary>仅供相机生命周期层在重新取流成功且期间没有新故障时解除迟到帧门禁。</summary>
        internal void ConfirmCameraStreamReset(string cameraKey, long preparedFaultGeneration)
        {
            string normalizedCameraKey = NormalizeCameraKey(cameraKey);
            ExecuteCameraSynchronized(normalizedCameraKey, () =>
            {
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(normalizedCameraKey);
                    if (state.PendingTickets.Count != 0 ||
                        state.InFlightTicket != null ||
                        state.BufferedHardwareFrames.Count != 0 ||
                        state.ActiveTriggerDispatchCount != 0 ||
                        state.ActiveUncoordinatedTriggerDispatchCount != 0 ||
                        state.PendingUncoordinatedFrameCount != 0 ||
                        state.PendingBlockingProductionFaultCount != 0)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}重新取流期间出现待处理票据、帧或故障，禁止确认流重置。 ");
                    }
                    if (state.StreamFaultGeneration != preparedFaultGeneration)
                    {
                        throw new InvalidOperationException(
                            $"相机{normalizedCameraKey}重新取流期间又发生生产帧故障，禁止清除门禁。 ");
                    }
                    state.Faulted = false;
                    state.RequiresStreamRestart = false;
                    state.InFlightFailure = null;
                    state.HasAcceptedFrame = false;
                    state.LastFrameId = 0L;
                    state.LastSdkFrameNumber = 0U;
                }
                return true;
            });
        }

        /// <summary>获取全部相机票据和帧序诊断快照。</summary>
        public CameraTriggerTicketRegistrySnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                int pendingCount = 0;
                int activeTriggerDispatchCount = 0;
                int cameraCountRequiringStreamReset = 0;
                int pendingProductionFaultCount = 0;
                int bufferedHardwareFrameCount = 0;
                foreach (CameraTicketQueueState state in _cameraStates.Values)
                {
                    pendingCount += state.PendingTickets.Count + (state.InFlightTicket == null ? 0 : 1);
                    activeTriggerDispatchCount += state.ActiveTriggerDispatchCount +
                        state.ActiveUncoordinatedTriggerDispatchCount;
                    if (state.RequiresStreamRestart)
                        cameraCountRequiringStreamReset++;
                    pendingProductionFaultCount += state.PendingProductionFaultCount;
                    bufferedHardwareFrameCount += state.BufferedHardwareFrames.Count;
                }
                return new CameraTriggerTicketRegistrySnapshot
                {
                    CameraCount = _cameraStates.Count,
                    PendingTicketCount = pendingCount,
                    PeakPendingTicketCount = _peakPendingTicketCount,
                    MatchedFrameCount = Interlocked.Read(ref _matchedFrameCount),
                    FrameSequenceFaultCount = _frameSequenceFaultCount,
                    TimedOutTicketCount = _timedOutTicketCount,
                    ActiveTriggerDispatchCount = activeTriggerDispatchCount,
                    CameraCountRequiringStreamReset = cameraCountRequiringStreamReset,
                    PendingProductionFaultCount = pendingProductionFaultCount,
                    RecoverableProductionFaultCount = Interlocked.Read(
                        ref _recoverableProductionFaultCount),
                    StoppingProductionFaultCount = Interlocked.Read(
                        ref _stoppingProductionFaultCount),
                    BufferedHardwareFrameCount = bufferedHardwareFrameCount,
                    PeakBufferedHardwareFrameCount = _peakBufferedHardwareFrameCount,
                    TotalBufferedHardwareFrameCount = Interlocked.Read(ref _bufferedHardwareFrameCount),
                    BufferedHardwareFrameBytes = _bufferedHardwareFrameBytes,
                    PeakBufferedHardwareFrameBytes = _peakBufferedHardwareFrameBytes,
                    BufferedHardwareFrameBudgetBytes = _bufferedHardwareFrameBudgetBytes,
                    BufferedHardwareFramePressureWarningCount =
                        Interlocked.Read(ref _bufferedHardwareFramePressureWarningCount)
                };
            }
        }

        /// <summary>在锁内取得或创建单相机FIFO状态。</summary>
        private CameraTicketQueueState GetOrCreateStateUnderLock(string cameraKey)
        {
            if (!_cameraStates.TryGetValue(cameraKey, out CameraTicketQueueState state))
            {
                state = new CameraTicketQueueState();
                _cameraStates.Add(cameraKey, state);
            }
            return state;
        }

        /// <summary>按相机独立串行化触发、取消、帧发布和故障切换，不阻塞其他相机。</summary>
        private T ExecuteCameraSynchronized<T>(
            string cameraKey,
            Func<T> action,
            bool allowDuringShutdown = false)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (!allowDuringShutdown)
                ThrowIfDisposed();

            CameraTicketQueueState state;
            lock (_syncRoot)
                state = GetOrCreateStateUnderLock(cameraKey);
            lock (state.TriggerSyncRoot)
                return action();
        }

        /// <summary>不等待忙相机执行状态操作，避免周期扫描被单台相机拖住。</summary>
        private bool TryExecuteCameraSynchronized<T>(string cameraKey, Func<T> action, out T result)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();

            CameraTicketQueueState state;
            lock (_syncRoot)
            {
                if (!_cameraStates.TryGetValue(cameraKey, out state))
                {
                    result = default(T);
                    return false;
                }
            }

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(state.TriggerSyncRoot, ref lockTaken);
                if (!lockTaken)
                {
                    result = default(T);
                    return false;
                }
                result = action();
                return true;
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(state.TriggerSyncRoot);
            }
        }

        /// <summary>取出相机全部排队票据；在途票据由发布线程完成资源清理后自行终结。</summary>
        private List<CameraTriggerTicket> DrainPendingTicketsUnderLock(CameraTicketQueueState state)
        {
            List<CameraTriggerTicket> tickets = new List<CameraTriggerTicket>(state.PendingTickets.Count);
            while (state.PendingTickets.Count > 0)
            {
                CameraTriggerTicket ticket = state.PendingTickets.First.Value;
                if (ticket.TriggerIssuedAtUtc.HasValue)
                    RequireStreamRestartUnderLock(state);
                tickets.Add(ticket);
                state.PendingTickets.RemoveFirst();
            }
            DrainBufferedHardwareFramesUnderLock(state);
            return tickets;
        }

        /// <summary>清空单相机尚未交给工件的硬触发Mat，并转交给锁外释放队列。</summary>
        private void DrainBufferedHardwareFramesUnderLock(CameraTicketQueueState state)
        {
            if (state.BufferedHardwareFrames.Count == 0)
                return;
            if (_retiredBufferedHardwareFrames == null)
            {
                _retiredBufferedHardwareFrames =
                    new List<BufferedHardwareFrame>(state.BufferedHardwareFrames.Count);
            }
            while (state.BufferedHardwareFrames.Count != 0)
                _retiredBufferedHardwareFrames.Add(DequeueBufferedHardwareFrameUnderLock(state));
        }

        /// <summary>原子取出已脱离FIFO的帧，并在全局状态锁外逐张释放Mat。</summary>
        private void DisposeRetiredBufferedHardwareFrames()
        {
            List<BufferedHardwareFrame> retiredFrames;
            lock (_syncRoot)
            {
                retiredFrames = _retiredBufferedHardwareFrames;
                _retiredBufferedHardwareFrames = null;
            }
            if (retiredFrames == null)
                return;
            foreach (BufferedHardwareFrame retiredFrame in retiredFrames)
                SafeDispose(retiredFrame);
        }

        /// <summary>从单相机FIFO取出一帧并同步归还全局等待字节记账。</summary>
        private BufferedHardwareFrame DequeueBufferedHardwareFrameUnderLock(CameraTicketQueueState state)
        {
            BufferedHardwareFrame frame = state.BufferedHardwareFrames.Dequeue();
            _bufferedHardwareFrameBytes = Math.Max(
                0L,
                _bufferedHardwareFrameBytes - frame.RetainedBytes);
            if (state.BufferedHardwareFrames.Count < BufferedHardwareFrameWarningIntervalPerCamera)
            {
                state.NextBufferedHardwareFrameWarningCount =
                    BufferedHardwareFrameWarningIntervalPerCamera;
            }
            return frame;
        }

        /// <summary>更新全部相机待执行硬触发帧的历史峰值。</summary>
        private void UpdatePeakBufferedHardwareFramesUnderLock()
        {
            int bufferedCount = 0;
            foreach (CameraTicketQueueState state in _cameraStates.Values)
                bufferedCount += state.BufferedHardwareFrames.Count;
            if (bufferedCount > _peakBufferedHardwareFrameCount)
                _peakBufferedHardwareFrameCount = bufferedCount;
        }

        /// <summary>记录一次新的流身份故障，并保持物理停流重启门禁。</summary>
        private static void RequireStreamRestartUnderLock(CameraTicketQueueState state)
        {
            state.RequiresStreamRestart = true;
            state.StreamFaultGeneration = checked(state.StreamFaultGeneration + 1L);
        }

        /// <summary>检查指定票据是否仍在当前相机的等待链表内。</summary>
        private static bool ContainsPendingTicketUnderLock(
            CameraTicketQueueState state,
            CameraTriggerTicket ticket)
        {
            foreach (CameraTriggerTicket pendingTicket in state.PendingTickets)
            {
                if (ReferenceEquals(pendingTicket, ticket))
                    return true;
            }
            return false;
        }

        /// <summary>当前票据保留明确终态，后续票据统一按停机结束。</summary>
        private static void FailPrimaryAndBlockedTickets(
            IEnumerable<CameraTriggerTicket> tickets,
            CameraTriggerTicket primaryTicket,
            string cameraKey,
            Exception leadingFailure,
            WorkpieceTerminalState primaryTerminalState)
        {
            if (tickets == null)
                return;
            foreach (CameraTriggerTicket ticket in tickets)
            {
                bool isPrimary = ReferenceEquals(ticket, primaryTicket);
                Exception ticketFailure = isPrimary
                    ? leadingFailure
                    : new InvalidOperationException(
                        $"相机{cameraKey}前序生产帧故障，后续工件{ticket.Context.Identity}已停止等待。",
                        leadingFailure);
                ticket.TryFail(
                    ticketFailure,
                    isPrimary ? primaryTerminalState : WorkpieceTerminalState.Stopped);
            }
        }

        /// <summary>相机故障后明确失败全部后续票据，避免流程无限等待。</summary>
        private static void FailTickets(
            IEnumerable<CameraTriggerTicket> tickets,
            string cameraKey,
            Exception leadingFailure,
            WorkpieceTerminalState terminalState)
        {
            if (tickets == null)
                return;
            foreach (CameraTriggerTicket ticket in tickets)
            {
                Exception ticketFailure = new InvalidOperationException(
                    $"相机{cameraKey}生产帧故障，工件{ticket.Context.Identity}已停止等待。",
                    leadingFailure);
                ticket.TryFail(ticketFailure, terminalState);
            }
        }

        /// <summary>检查内部帧号和SDK帧号均严格前进。</summary>
        private static bool IsNextFrameNumber(
            CameraTicketQueueState state,
            long frameId,
            uint sdkFrameNumber,
            out string reason)
        {
            if (frameId <= 0L)
            {
                reason = "相机内部FrameId无效。";
                return false;
            }
            if (!state.HasAcceptedFrame)
            {
                reason = null;
                return true;
            }
            if (frameId != state.LastFrameId + 1L)
            {
                reason = $"相机内部FrameId断层：上一帧={state.LastFrameId}，当前帧={frameId}。";
                return false;
            }

            uint expectedSdkFrameNumber = unchecked(state.LastSdkFrameNumber + 1U);
            if (sdkFrameNumber != expectedSdkFrameNumber)
            {
                reason = $"相机SDK帧号断层：上一帧={state.LastSdkFrameNumber}，当前帧={sdkFrameNumber}。";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>统计全部相机当前待匹配票据峰值。</summary>
        private void UpdatePeakPendingUnderLock()
        {
            int pendingCount = 0;
            foreach (CameraTicketQueueState state in _cameraStates.Values)
                pendingCount += state.PendingTickets.Count + (state.InFlightTicket == null ? 0 : 1);
            if (pendingCount > _peakPendingTicketCount)
                _peakPendingTicketCount = pendingCount;
        }

        /// <summary>把故障加入独立单消费者队列，并在相机状态中保留恢复门禁。</summary>
        private void RaiseProductionFault(CameraProductionFault fault)
        {
            DisposeRetiredBufferedHardwareFrames();
            if (fault == null)
                return;

            if (fault.RequiresSolutionStop)
                Interlocked.Increment(ref _stoppingProductionFaultCount);
            else
                Interlocked.Increment(ref _recoverableProductionFaultCount);

            lock (_faultQueueSyncRoot)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;
                _pendingProductionFaults.Enqueue(fault);
                lock (_syncRoot)
                {
                    CameraTicketQueueState state = GetOrCreateStateUnderLock(fault.CameraKey);
                    state.PendingProductionFaultCount++;
                    if (fault.RequiresSolutionStop)
                        state.PendingBlockingProductionFaultCount++;
                }
                _faultDispatchSignal.Set();
            }
        }

        /// <summary>独立线程按故障产生顺序调用订阅者，不持有相机状态锁。</summary>
        private void DispatchProductionFaultLoop()
        {
            try
            {
                while (true)
                {
                    CameraProductionFault fault = null;
                    lock (_faultQueueSyncRoot)
                    {
                        if (_pendingProductionFaults.Count > 0)
                            fault = _pendingProductionFaults.Dequeue();
                        else if (Volatile.Read(ref _disposed) != 0)
                            return;
                    }

                    if (fault != null)
                    {
                        Action<CameraProductionFault> handlers = ProductionFaulted;
                        if (handlers != null)
                        {
                            foreach (Delegate handler in handlers.GetInvocationList())
                            {
                                try { ((Action<CameraProductionFault>)handler)(fault); }
                                catch { }
                            }
                        }
                        lock (_syncRoot)
                        {
                            if (_cameraStates.TryGetValue(fault.CameraKey, out CameraTicketQueueState state))
                            {
                                state.PendingProductionFaultCount =
                                    Math.Max(0, state.PendingProductionFaultCount - 1);
                                if (fault.RequiresSolutionStop)
                                {
                                    state.PendingBlockingProductionFaultCount = Math.Max(
                                        0,
                                        state.PendingBlockingProductionFaultCount - 1);
                                }
                            }
                        }
                        continue;
                    }

                    _faultDispatchSignal.WaitOne();
                }
            }
            finally
            {
                _faultDispatchSignal.Dispose();
            }
        }

        /// <summary>停止故障派发线程并完成已经入队的故障，重复释放无害。</summary>
        public void Dispose()
        {
            lock (_disposeSyncRoot)
            {
                if (Volatile.Read(ref _disposeCompleted) != 0)
                    return;

                lock (_faultQueueSyncRoot)
                {
                    if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                    {
                        _faultDispatchSignal.Set();
                    }
                }

                DateTime triggerDeadlineUtc =
                    DateTime.UtcNow.AddMilliseconds(TriggerDispatchShutdownTimeoutMs);
                while (GetActiveTriggerDispatchCount() != 0)
                {
                    if (DateTime.UtcNow >= triggerDeadlineUtc)
                    {
                        throw new TimeoutException(
                            $"相机生产票据注册表关闭超时：真实SDK触发调用在{TriggerDispatchShutdownTimeoutMs}ms内未返回。" +
                            "必须先停止相机取流并解除SDK阻塞，再重试关闭。 ");
                    }
                    Thread.Sleep(5);
                }

                InvalidOperationException shutdownFailure =
                    new InvalidOperationException("相机生产票据注册表已经停止。 ");
                List<CameraTriggerTicket> shutdownTickets = new List<CameraTriggerTicket>();
                List<string> shutdownCameraKeys;
                lock (_syncRoot)
                    shutdownCameraKeys = new List<string>(_cameraStates.Keys);
                foreach (string cameraKey in shutdownCameraKeys)
                {
                    ExecuteCameraSynchronized(cameraKey, () =>
                    {
                        lock (_syncRoot)
                        {
                            CameraTicketQueueState state = GetOrCreateStateUnderLock(cameraKey);
                            state.Faulted = true;
                            state.InFlightFailure = shutdownFailure;
                            shutdownTickets.AddRange(DrainPendingTicketsUnderLock(state));
                        }
                        return true;
                    }, true);
                }
                DisposeRetiredBufferedHardwareFrames();
                foreach (CameraTriggerTicket shutdownTicket in shutdownTickets)
                    shutdownTicket.TryFail(shutdownFailure, WorkpieceTerminalState.Stopped);

                Volatile.Write(ref _disposeCompleted, 1);
                if (!ReferenceEquals(Thread.CurrentThread, _faultDispatcherThread))
                    _faultDispatcherThread.Join(FaultDispatcherShutdownTimeoutMs);
            }
        }

        /// <summary>读取全部相机尚未返回的真实SDK触发调用数。</summary>
        private int GetActiveTriggerDispatchCount()
        {
            lock (_syncRoot)
            {
                int activeCount = 0;
                foreach (CameraTicketQueueState state in _cameraStates.Values)
                    activeCount += state.ActiveTriggerDispatchCount +
                        state.ActiveUncoordinatedTriggerDispatchCount;
                return activeCount;
            }
        }

        /// <summary>禁止释放后的注册表继续接收生产操作。</summary>
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(CameraTriggerTicketRegistry));
        }

        /// <summary>故障清场不允许资源释放异常打断票据、预算和执行租约终结。</summary>
        private static void SafeDispose(IDisposable resource)
        {
            if (resource == null)
                return;
            try { resource.Dispose(); }
            catch { }
        }

        /// <summary>规范相机键，防止大小写和首尾空白拆分队列。</summary>
        private static string NormalizeCameraKey(string cameraKey)
        {
            if (string.IsNullOrWhiteSpace(cameraKey))
                throw new ArgumentException("相机稳定键不能为空。", nameof(cameraKey));
            return cameraKey.Trim().ToUpperInvariant();
        }

        /// <summary>按Mat实际行跨度计算生产帧字节数。</summary>
        private static long GetMatBytes(Mat image)
        {
            return Math.Max(1L, checked((long)image.Rows * (long)image.Step()));
        }

        /// <summary>计算图像源标准输出将长期持有的源Mat和按需灰度Mat实际字节。</summary>
        private static long GetProductionOutputBytes(Mat image)
        {
            long sourceBytes = GetMatBytes(image);
            if (image.Channels() <= 1)
                return sourceBytes;
            long grayBytes = Math.Max(1L, checked((long)image.Rows * (long)image.Cols));
            return checked(sourceBytes + grayBytes);
        }

        /// <summary>相机软件触发命令结束时归还预览触发在途计数。</summary>
        private sealed class SoftwareTriggerCommandLease : ISoftwareTriggerCommandLease
        {
            /// <summary>拥有当前门禁的注册表。</summary>
            private CameraTriggerTicketRegistry _owner;

            /// <summary>规范化相机键。</summary>
            private readonly string _cameraKey;

            /// <summary>是否需要归还非生产触发计数。</summary>
            private readonly bool _countedAsUncoordinated;

            /// <summary>1表示真实SDK命令已经开始。</summary>
            private int _commandStarted;

            /// <summary>1表示真实SDK命令明确返回成功。</summary>
            private int _commandSucceeded;

            /// <summary>创建软件触发命令门禁租约。</summary>
            public SoftwareTriggerCommandLease(
                CameraTriggerTicketRegistry owner,
                string cameraKey,
                bool countedAsUncoordinated)
            {
                _owner = owner;
                _cameraKey = cameraKey;
                _countedAsUncoordinated = countedAsUncoordinated;
            }

            /// <summary>标记真实SDK命令已经开始。</summary>
            public void MarkCommandStarted()
            {
                if (Interlocked.CompareExchange(ref _commandStarted, 1, 0) != 0)
                    throw new InvalidOperationException("软件触发命令开始状态禁止重复标记。 ");
                CameraTriggerTicketRegistry owner = Volatile.Read(ref _owner);
                if (owner == null)
                {
                    Interlocked.Exchange(ref _commandStarted, 0);
                    throw new ObjectDisposedException(nameof(SoftwareTriggerCommandLease));
                }
                try
                {
                    owner.MarkSoftwareTriggerCommandStarted(_cameraKey, _countedAsUncoordinated);
                }
                catch
                {
                    Interlocked.Exchange(ref _commandStarted, 0);
                    throw;
                }
            }

            /// <summary>标记真实SDK命令明确返回成功。</summary>
            public void MarkCommandSucceeded()
            {
                if (Volatile.Read(ref _commandStarted) == 0)
                    throw new InvalidOperationException("必须先标记软件触发命令开始，再标记成功。 ");
                Interlocked.Exchange(ref _commandSucceeded, 1);
            }

            /// <summary>幂等归还软件触发命令门禁。</summary>
            public void Dispose()
            {
                CameraTriggerTicketRegistry owner = Interlocked.Exchange(ref _owner, null);
                owner?.ReleaseSoftwareTriggerCommand(
                    _cameraKey,
                    _countedAsUncoordinated,
                    Volatile.Read(ref _commandStarted) != 0,
                    Volatile.Read(ref _commandSucceeded) != 0);
            }
        }

        /// <summary>单台相机的票据FIFO和连续帧状态。</summary>
        private sealed class CameraTicketQueueState
        {
            /// <summary>单相机触发、取消、发布和故障状态机锁。</summary>
            public object TriggerSyncRoot { get; } = new object();

            /// <summary>已经出队并正在校正预算或构造信封的唯一票据。</summary>
            public CameraTriggerTicket InFlightTicket { get; set; }

            /// <summary>在途发布期间由其他线程登记的停止原因，必须由发布线程清理资源后提交。</summary>
            public Exception InFlightFailure { get; set; }

            /// <summary>待匹配生产票据。</summary>
            public LinkedList<CameraTriggerTicket> PendingTickets { get; } =
                new LinkedList<CameraTriggerTicket>();

            /// <summary>完整流程忙碌期间已经到达、等待下一工件消费的硬触发帧。</summary>
            public Queue<BufferedHardwareFrame> BufferedHardwareFrames { get; } =
                new Queue<BufferedHardwareFrame>();

            /// <summary>下一次需要输出积压报警的待执行帧数。</summary>
            public int NextBufferedHardwareFrameWarningCount { get; set; } =
                BufferedHardwareFrameWarningIntervalPerCamera;

            /// <summary>本运行会话是否已经由生产硬触发节点启用持续帧接管。</summary>
            public bool HardwareFrameBufferingEnabled { get; set; }

            /// <summary>上一帧内部编号。</summary>
            public long LastFrameId { get; set; }

            /// <summary>上一帧SDK编号。</summary>
            public uint LastSdkFrameNumber { get; set; }

            /// <summary>是否已经接收过生产帧。</summary>
            public bool HasAcceptedFrame { get; set; }

            /// <summary>是否需要人工确认后才能恢复。</summary>
            public bool Faulted { get; set; }

            /// <summary>单相机断线隔离代数，防止旧连接异步异常误伤已经恢复的新连接。</summary>
            public long ReconnectGeneration { get; set; }

            /// <summary>已经提交但真实SDK调用尚未返回的触发数量。</summary>
            public int ActiveTriggerDispatchCount { get; set; }

            /// <summary>正在执行SDK命令但不属于生产票据的预览或手动软件触发数量。</summary>
            public int ActiveUncoordinatedTriggerDispatchCount { get; set; }

            /// <summary>已成功发送但尚未由转换线程消费的预览或手动软触发帧数量。</summary>
            public int PendingUncoordinatedFrameCount { get; set; }

            /// <summary>是否清场过已武装票据，必须完成物理停流排空后才能解除。</summary>
            public bool RequiresStreamRestart { get; set; }

            /// <summary>每次出现新的不可信帧流事实时递增，用于两阶段重启确认。</summary>
            public long StreamFaultGeneration { get; set; }

            /// <summary>尚未由独立派发线程处理完的该相机生产故障数。</summary>
            public int PendingProductionFaultCount { get; set; }

            /// <summary>尚未派发完成且会停止方案的故障数；可恢复告警不阻塞单相机重启确认。</summary>
            public int PendingBlockingProductionFaultCount { get; set; }
        }

        /// <summary>暂存完整流程忙碌期间到达的独立Mat及其帧身份。</summary>
        private sealed class BufferedHardwareFrame : IDisposable
        {
            /// <summary>当前尚未转移给生产票据的独立Mat。</summary>
            private Mat _image;

            /// <summary>创建一个等待后续硬触发工件消费的帧。</summary>
            public BufferedHardwareFrame(
                long frameId,
                uint sdkFrameNumber,
                long callbackStartedTimestamp,
                Mat image,
                CameraFrameTraceInfo traceInfo,
                long retainedBytes)
            {
                FrameId = frameId;
                SdkFrameNumber = sdkFrameNumber;
                CallbackStartedTimestamp = callbackStartedTimestamp;
                _image = image ?? throw new ArgumentNullException(nameof(image));
                TraceInfo = traceInfo;
                RetainedBytes = retainedBytes > 0L
                    ? retainedBytes
                    : throw new ArgumentOutOfRangeException(nameof(retainedBytes));
            }

            /// <summary>获取进程内连续帧号。</summary>
            public long FrameId { get; }

            /// <summary>获取相机SDK帧号。</summary>
            public uint SdkFrameNumber { get; }

            /// <summary>获取帧进入SDK回调的单调时钟。</summary>
            public long CallbackStartedTimestamp { get; }

            /// <summary>获取帧耗时诊断信息。</summary>
            public CameraFrameTraceInfo TraceInfo { get; }

            /// <summary>获取该Mat在全局待执行队列中记账的实际字节数。</summary>
            public long RetainedBytes { get; }

            /// <summary>把Mat唯一所有权转交给生产票据。</summary>
            public Mat DetachImage()
            {
                Mat image = Interlocked.Exchange(ref _image, null);
                if (image == null)
                    throw new InvalidOperationException("待执行硬触发帧已经转交或释放。 ");
                return image;
            }

            /// <summary>释放尚未被工件接管的Mat。</summary>
            public void Dispose()
            {
                SafeDispose(Interlocked.Exchange(ref _image, null));
            }
        }
    }
}
