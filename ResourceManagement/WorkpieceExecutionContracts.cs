using System;
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

        /// <summary>当前会话内从1开始递增的工件序号。</summary>
        private readonly long _sequence;

        /// <summary>
        /// 创建不可变工件身份。
        /// </summary>
        /// <param name="sessionEpoch">生产会话唯一纪元。</param>
        /// <param name="sequence">会话内大于0的递增序号。</param>
        public WorkpieceIdentity(Guid sessionEpoch, long sequence)
        {
            if (sessionEpoch == Guid.Empty)
                throw new ArgumentException("生产会话纪元不能为空。", nameof(sessionEpoch));
            if (sequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sequence), "工件序号必须大于0。");

            _sessionEpoch = sessionEpoch;
            _sequence = sequence;
        }

        /// <summary>获取生产会话唯一纪元。</summary>
        public Guid SessionEpoch => _sessionEpoch;

        /// <summary>获取会话内工件序号。</summary>
        public long Sequence => _sequence;

        /// <summary>
        /// 比较同一生产会话内两个工件的先后顺序。
        /// </summary>
        /// <param name="other">待比较的工件身份。</param>
        /// <returns>小于0表示当前工件更早，0表示相同，大于0表示当前工件更晚。</returns>
        public int CompareTo(WorkpieceIdentity other)
        {
            if (_sessionEpoch != other._sessionEpoch)
                throw new InvalidOperationException("不同生产会话的工件不能比较先后顺序。");

            return _sequence.CompareTo(other._sequence);
        }

        /// <summary>
        /// 判断两个工件身份是否完全相同。
        /// </summary>
        /// <param name="other">待比较的工件身份。</param>
        /// <returns>会话纪元和序号都相同时返回true。</returns>
        public bool Equals(WorkpieceIdentity other)
        {
            return _sessionEpoch == other._sessionEpoch && _sequence == other._sequence;
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
                return (_sessionEpoch.GetHashCode() * 397) ^ _sequence.GetHashCode();
            }
        }

        /// <summary>
        /// 获取便于日志关联的工件身份文本。
        /// </summary>
        /// <returns>会话纪元和工件序号文本。</returns>
        public override string ToString()
        {
            return $"{_sessionEpoch:N}/{_sequence}";
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
    /// 单个工件运行完整可编辑流程时携带的隔离上下文。
    /// </summary>
    public sealed class WorkpieceExecutionContext
    {
        /// <summary>保护信号序号分配和唯一终态切换不可交错的实例锁。</summary>
        private readonly object _stateSyncRoot = new object();

        /// <summary>已分配的实际外发信号序号。</summary>
        private long _signalSequence;

        /// <summary>当前工件唯一终态的整数值。</summary>
        private int _terminalState;

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
            if (identity.SessionEpoch == Guid.Empty || identity.Sequence <= 0L)
                throw new ArgumentException("必须提供有效工件身份。", nameof(identity));
            if (flowRevision <= 0L)
                throw new ArgumentOutOfRangeException(nameof(flowRevision), "流程快照版本必须大于0。");
            if (string.IsNullOrWhiteSpace(orderDomain))
                throw new ArgumentException("生产顺序域不能为空。", nameof(orderDomain));
            if (string.IsNullOrWhiteSpace(triggerSource))
                throw new ArgumentException("触发来源不能为空。", nameof(triggerSource));
            if (createdAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("工件创建时间必须使用UTC。", nameof(createdAtUtc));

            Identity = identity;
            FlowRevision = flowRevision;
            OrderDomain = orderDomain.Trim();
            TriggerSource = triggerSource.Trim();
            CreatedAtUtc = createdAtUtc;
            CancellationToken = cancellationToken;
            _terminalState = (int)WorkpieceTerminalState.Active;
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

        /// <summary>
        /// 为本轮实际到达的外发节点分配严格递增信号序号。
        /// </summary>
        /// <returns>从1开始且仅属于当前工件的信号序号。</returns>
        public long AllocateSignalSequence()
        {
            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active)
                    throw new InvalidOperationException("工件已经封口，不能继续分配外发信号序号。");

                return ++_signalSequence;
            }
        }

        /// <summary>
        /// 尝试把当前工件从活动状态原子切换到唯一终态。
        /// </summary>
        /// <param name="terminalState">不允许为Active的目标终态。</param>
        /// <returns>本次调用完成唯一状态切换返回true；已经封口返回false。</returns>
        public bool TryComplete(WorkpieceTerminalState terminalState)
        {
            if (terminalState == WorkpieceTerminalState.Active)
                throw new ArgumentException("工件终态不能设置为Active。", nameof(terminalState));
            if (!Enum.IsDefined(typeof(WorkpieceTerminalState), terminalState))
                throw new ArgumentOutOfRangeException(nameof(terminalState), "未知的工件终态。");

            lock (_stateSyncRoot)
            {
                if (_terminalState != (int)WorkpieceTerminalState.Active)
                    return false;

                Volatile.Write(ref _terminalState, (int)terminalState);
                return true;
            }
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
        ModbusWrite = 4
    }

    /// <summary>
    /// 设备发送动作可以被确认的结果状态。
    /// </summary>
    public enum OrderedSignalSendStatus
    {
        /// <summary>设备已确认发送成功。</summary>
        Confirmed = 1,

        /// <summary>设备明确拒绝或确认发送失败。</summary>
        Rejected = 2,

        /// <summary>无法确认外部副作用是否已经发生。</summary>
        Unknown = 3
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
        public OrderedSignalSendResult(OrderedSignalSendStatus status, string message)
        {
            if (!Enum.IsDefined(typeof(OrderedSignalSendStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status), "未知的外发确认状态。");

            Status = status;
            Message = message ?? string.Empty;
        }

        /// <summary>获取设备确认状态。</summary>
        public OrderedSignalSendStatus Status { get; }

        /// <summary>获取诊断说明。</summary>
        public string Message { get; }
    }

    /// <summary>
    /// 已冻结节点元数据和发送动作的单次有序信号意图。
    /// </summary>
    public sealed class OrderedSignalIntent
    {
        /// <summary>
        /// 创建不可变有序信号意图。
        /// </summary>
        /// <param name="context">当前工件执行上下文。</param>
        /// <param name="signalSequence">上下文分配的实际信号序号。</param>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <param name="nodeKind">白名单外发节点类别。</param>
        /// <param name="endpointKey">物理设备串行化键。</param>
        /// <param name="sendAsync">已经冻结动态发送参数的设备动作。</param>
        public OrderedSignalIntent(
            WorkpieceExecutionContext context,
            long signalSequence,
            string nodePath,
            OrderedSignalNodeKind nodeKind,
            string endpointKey,
            Func<CancellationToken, Task<OrderedSignalSendResult>> sendAsync)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (signalSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(signalSequence), "信号序号必须大于0。");
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("节点路径不能为空。", nameof(nodePath));
            if (!Enum.IsDefined(typeof(OrderedSignalNodeKind), nodeKind))
                throw new ArgumentOutOfRangeException(nameof(nodeKind), "节点不属于有序外发白名单。");
            if (string.IsNullOrWhiteSpace(endpointKey))
                throw new ArgumentException("物理设备键不能为空。", nameof(endpointKey));

            Context = context;
            SignalSequence = signalSequence;
            NodePath = nodePath.Trim();
            NodeKind = nodeKind;
            EndpointKey = endpointKey.Trim();
            SendAsync = sendAsync ?? throw new ArgumentNullException(nameof(sendAsync));
        }

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

        /// <summary>获取已经冻结动态发送参数的设备动作。</summary>
        public Func<CancellationToken, Task<OrderedSignalSendResult>> SendAsync { get; }
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
        /// 在节点真正执行到达时冻结动态参数并创建有序信号意图。
        /// </summary>
        /// <param name="context">当前工件执行上下文。</param>
        /// <param name="nodePath">运行快照中的节点路径。</param>
        /// <returns>可提交到有序协调器的信号意图。</returns>
        OrderedSignalIntent CreateOrderedSignalIntent(WorkpieceExecutionContext context, string nodePath);
    }

    /// <summary>
    /// 负责跨工件顺序和工件封口的信号协调器接口。
    /// </summary>
    public interface IOrderedSignalCoordinator
    {
        /// <summary>
        /// 登记工件身份，使后到达发送节点的工件不能越过尚未到达的前序工件。
        /// </summary>
        /// <param name="context">已准入的工件执行上下文。</param>
        void RegisterWorkpiece(WorkpieceExecutionContext context);

        /// <summary>
        /// 按生产顺序域、工件序号和信号序号提交设备动作。
        /// </summary>
        /// <param name="intent">已冻结的外发信号意图。</param>
        /// <param name="cancellationToken">等待顺序和设备发送的取消令牌。</param>
        /// <returns>设备确认结果。</returns>
        Task<OrderedSignalSendResult> SubmitAsync(
            OrderedSignalIntent intent,
            CancellationToken cancellationToken);

        /// <summary>
        /// 封口工件并声明本轮不会再产生新的外发信号。
        /// </summary>
        /// <param name="context">已进入唯一终态的工件上下文。</param>
        void CompleteWorkpiece(WorkpieceExecutionContext context);
    }

    /// <summary>
    /// 对同一物理设备连接实施互斥调用的可插拔串行器接口。
    /// </summary>
    public interface ISignalEndpointSerializer
    {
        /// <summary>
        /// 在指定物理设备键上串行执行已经冻结的发送动作。
        /// </summary>
        /// <param name="intent">待执行的信号意图。</param>
        /// <param name="cancellationToken">等待和发送取消令牌。</param>
        /// <returns>设备确认结果。</returns>
        Task<OrderedSignalSendResult> ExecuteAsync(
            OrderedSignalIntent intent,
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
