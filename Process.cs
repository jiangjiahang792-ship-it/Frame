using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision
{
    public struct ProcessRunResult 
    {
        public bool IsRunning;
        public bool IsSuccess;
        public string ProcessName;
        public ProcessRunResult(bool  isRunning, bool isSuccess, string name)
        {
            IsRunning = isRunning;
            IsSuccess = isSuccess;
            ProcessName = name;
        }
    }

    /// <summary>
    /// 单次流程调用不可变结果，避免后续排队运行覆盖Process.Success后污染当前工件判断。
    /// </summary>
    public sealed class ProcessInvocationResult
    {
        /// <summary>
        /// 创建单次流程调用结果。
        /// </summary>
        /// <param name="processName">流程名称。</param>
        /// <param name="succeeded">本次调用是否成功。</param>
        /// <param name="cancelled">本次调用是否取消。</param>
        /// <param name="exception">本次调用捕获的异常。</param>
        internal ProcessInvocationResult(
            string processName,
            bool succeeded,
            bool cancelled,
            Exception exception)
        {
            ProcessName = processName ?? string.Empty;
            Succeeded = succeeded;
            Cancelled = cancelled;
            Exception = exception;
        }

        /// <summary>获取流程名称。</summary>
        public string ProcessName { get; }

        /// <summary>获取本次流程调用是否成功。</summary>
        public bool Succeeded { get; }

        /// <summary>获取本次流程调用是否由取消结束。</summary>
        public bool Cancelled { get; }

        /// <summary>获取本次流程调用捕获的原始异常。</summary>
        public Exception Exception { get; }
    }

    /// <summary>
    /// 检测流程类
    /// 一个方案可以拥有多个流程
    /// 每个流程也可以单独执行
    /// </summary>
    public class Process
    {
        /// <summary>
        /// 流程包含的节点
        /// </summary>
        private List<NodeBase> _nodes = new List<NodeBase>();
        /// <summary>
        /// 流程图编辑器中节点之间的连接关系。
        /// </summary>
        private List<ProcessConnection> _connections = new List<ProcessConnection>();
        /// <summary>
        /// 流程ID
        /// </summary>
        public int ID { get; set; }
        /// <summary>
        /// 流程名称
        /// </summary>
        public string ProcessName { get; set; }
        /// <summary>
        /// 流程运行时是否输出日志
        /// </summary>
        public bool ShowLog { get; set; } = true;
        /// <summary>
        /// 流程包含的节点
        /// </summary>
        public List<NodeBase> Nodes { get => _nodes;}

        /// <summary>
        /// 流程图连接。有图数据时运行入口从起始节点和连接关系推进。
        /// </summary>
        public List<ProcessConnection> Connections { get => _connections; }

        /// <summary>
        /// 当前流程是否按自由画布图模式解释连接语义。
        /// 旧顺序流程自动生成的兼容连接不按 If 锚点推导 True/False 分支。
        /// </summary>
        public bool HasCanvasGraph { get; set; } = true;

        /// <summary>
        /// 画布连线增删后通知订阅控件重新计算上游节点。
        /// </summary>
        public event EventHandler ConnectionsChanged;

        /// <summary>
        /// OK数量
        /// </summary>
        public int OKNumber;
        /// <summary>
        /// NG数量
        /// </summary>
        public int NGNumber;
        /// <summary>
        /// 本轮流程开始前的OK数量，用于判断循环空跑时是否发生业务输出变化。
        /// </summary>
        private int _runStartOKNumber;
        /// <summary>
        /// 本轮流程开始前的NG数量，用于判断循环空跑时是否发生业务输出变化。
        /// </summary>
        private int _runStartNGNumber;

        /// <summary>跨线程可见的流程运行状态，1表示正在运行。</summary>
        private int _isRunning;

        /// <summary>单流程容量8、严格FIFO、并发固定为1的工件执行入口。</summary>
        private ProcessWorkpieceGate _workpieceRunGate;

        /// <summary>当前异步分支已经进入的流程路径，用于在等待FIFO前拒绝流程触发环。</summary>
        private static readonly AsyncLocal<ProcessRunPath> CurrentProcessRunPath =
            new AsyncLocal<ProcessRunPath>();

        /// <summary>当前流程定义版本；后续统一编辑门切换快照时递增。</summary>
        private long _flowRevision = 1L;

        /// <summary>
        /// 获取或设置流程是否正在运行；重置线程可立即观察到后台线程的状态变化。
        /// </summary>
        public bool IsRuning
        {
            get { return Volatile.Read(ref _isRunning) == 1; }
            set { Volatile.Write(ref _isRunning, value ? 1 : 0); }
        }

        /// <summary>
        /// 当前流程运行批次，用于判断节点结果是否来自本次运行。
        /// </summary>
        public int CurrentRunId { get; private set; }

        /// <summary>获取当前流程定义版本。</summary>
        public long FlowRevision => Volatile.Read(ref _flowRevision);

        /// <summary>获取或设置生产顺序域；空值时按流程组生成稳定默认域。</summary>
        public string ProductionOrderDomain { get; set; }

        /// <summary>
        /// 当前运行批次的Debug完整耗时追踪；Debug关闭时始终为空。
        /// </summary>
        private ProcessPerformanceTrace _performanceTrace;

        /// <summary>
        /// 获取当前运行批次的跨线程诊断关联号，Debug关闭时返回空字符串。
        /// </summary>
        public string CurrentPerformanceTraceId => _performanceTrace == null ? string.Empty : _performanceTrace.TraceId;

        /// <summary>
        /// 流程运行优先级
        /// </summary>
        public ProcessLvEnum RunLv { get; set; } = ProcessLvEnum.Lv5;

        /// <summary>
        /// 流程组别
        /// </summary>
        public ProcessGroup Group { get; set; } = ProcessGroup.Group1;
        /// <summary>
        /// 流程运行时间
        /// </summary>
        public long RunTime { get; private set; } = 0;

        /// <summary>
        /// 流程运行是否成功
        /// </summary>
        public bool Success { get; private set; }
        /// <summary>
        /// 是否跳过Else逻辑
        /// </summary>
        public bool SkipNextElseBlock { get; set; }
        /// <summary>
        /// 流程是否启用
        /// </summary>
        public bool Enable { get; set; } = true;
        /// <summary>
        /// 流程运行完更新流程界面和主界面的运行按钮Enable
        /// </summary>
        public static EventHandler<ProcessRunResult> UpdateRunStatus;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="solution"></param>
        public Process(string processName)
        {
            ProcessName = processName;
            ID = Solution.Instance.ProcessCount;
        }

        /// <summary>
        /// 判断指定节点本次运行是否允许输出日志，流程级日志关闭时节点不能单独打开日志。
        /// </summary>
        private static bool ShouldShowNodeLog(Process process, NodeBase node)
        {
            return process != null && process.ShowLog && node != null && node.OutputLog;
        }

        /// <summary>
        /// 写入流程开始日志；循环运行时先延迟写入，等结束时确认本轮不是无效空跑再补写。
        /// </summary>
        private static bool WriteProcessStartLog(Process process, bool isCyclical)
        {
            if (process == null || !process.ShowLog || isCyclical)
                return false;

            LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{process.ProcessName}】（开始）  -----------------------------------------------------", true);
            return true;
        }

        /// <summary>
        /// 确保流程开始日志已写入；失败、异常、取消等关键结果不能只写结束。
        /// </summary>
        private static void EnsureProcessStartLog(Process process, ref bool startLogWritten)
        {
            if (process == null || !process.ShowLog || startLogWritten)
                return;

            LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{process.ProcessName}】（开始）  -----------------------------------------------------", true);
            startLogWritten = true;
        }

        /// <summary>
        /// 写入流程结束日志，必要时先补写延迟的开始日志。
        /// </summary>
        private static void WriteProcessEndLog(Process process, ref bool startLogWritten, MsgLevel level, string statusText)
        {
            if (process == null)
                return;

            process.CompletePerformanceTrace(statusText);
            if (!process.ShowLog)
                return;

            EnsureProcessStartLog(process, ref startLogWritten);
            LogHelper.AddLog(level, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（{statusText}）  ---------------------------------", true);
        }

        /// <summary>
        /// 写入最终结束日志；循环运行成功但未跑完整个启用节点集合时跳过，避免刷爆日志文件。
        /// </summary>
        private static void WriteFinalProcessEndLog(Process process, bool isCyclical, bool succeeded, ref bool startLogWritten)
        {
            if (process == null)
                return;

            process.CompletePerformanceTrace(succeeded ? "成功" : "失败");
            if (!process.ShowLog)
                return;

            if (!ShouldWriteFinalProcessLog(process, isCyclical, succeeded))
                return;

            WriteProcessEndLog(process, ref startLogWritten, succeeded ? MsgLevel.Info : MsgLevel.Exception, succeeded ? "成功" : "失败");
        }

        /// <summary>
        /// 判断最终流程是否需要写日志；失败结果始终保留，只有循环成功的非完整运行才允许过滤。
        /// </summary>
        private static bool ShouldWriteFinalProcessLog(Process process, bool isCyclical, bool succeeded)
        {
            if (process == null || !process.ShowLog)
                return false;
            if (!isCyclical || !succeeded)
                return true;
            if (!process.HasExecutedAllEnabledNodesInCurrentRun())
                return false;
            if (process.RunTime != 0)
                return true;
            if (process.HasOutputLogNodeExecutedInCurrentRun())
                return true;
            if (process.HasKeyBusinessOutputChangedInCurrentRun())
                return true;

            return false;
        }

        public void AddNode(NodeBase node)
        {
            _nodes.Add(node);
        }

        public void RemoveNodeConnections(int nodeId)
        {
            int removedCount = _connections.RemoveAll(connection =>
                connection.FromNodeId == nodeId || connection.ToNodeId == nodeId);

            if (removedCount > 0)
                NotifyConnectionsChanged();
        }

        public void AddConnection(ProcessConnection connection)
        {
            if (connection == null)
                return;

            HasCanvasGraph = true;
            NormalizeConnectionBranch(connection);
            _connections.Add(connection);
            NotifyConnectionsChanged();
        }

        public bool RemoveConnection(ProcessConnection connection)
        {
            bool removed = connection != null && _connections.Remove(connection);
            if (removed)
                NotifyConnectionsChanged();

            return removed;
        }

        public void NotifyConnectionsChanged()
        {
            ConnectionsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取可到达指定节点的所有图上游节点，保留流程节点列表的显示顺序。
        /// </summary>
        public List<NodeBase> GetUpstreamNodes(NodeBase node)
        {
            List<NodeBase> upstreamNodes = new List<NodeBase>();
            if (node == null)
                return upstreamNodes;

            HashSet<int> upstreamNodeIds = new HashSet<int>();
            Queue<int> pendingNodeIds = new Queue<int>();
            pendingNodeIds.Enqueue(node.ID);

            while (pendingNodeIds.Count > 0)
            {
                int currentNodeId = pendingNodeIds.Dequeue();
                foreach (ProcessConnection connection in _connections)
                {
                    if (connection.ToNodeId != currentNodeId ||
                        connection.FromNodeId == node.ID ||
                        !upstreamNodeIds.Add(connection.FromNodeId))
                        continue;

                    pendingNodeIds.Enqueue(connection.FromNodeId);
                }
            }

            foreach (NodeBase processNode in _nodes)
            {
                if (upstreamNodeIds.Contains(processNode.ID))
                    upstreamNodes.Add(processNode);
            }

            return upstreamNodes;
        }

        public bool HasConditionalBranchConnections(int nodeId)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId == nodeId &&
                    GetEffectiveConnectionBranch(connection) != ProcessConnectionBranch.Default)
                    return true;
            }

            return false;
        }

        public void NormalizeConnectionBranch(ProcessConnection connection)
        {
            if (connection == null || connection.Branch != ProcessConnectionBranch.Default)
                return;

            connection.Branch = GetEffectiveConnectionBranch(connection);
        }

        private bool ShouldRunByConnections()
        {
            if (_connections.Count > 0)
                return true;

            foreach (NodeBase node in _nodes)
            {
                if (node.IsStartNode)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Executes the transitional graph runtime. Legacy logic nodes may still jump
        /// by Process.Nodes index until graph-native branch rules are introduced.
        /// </summary>
        private async Task<bool> RunConnectedNodes(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null, NodeBase startNodeOverride = null)
        {
            if (!CanUseParallelConnectedRuntime())
            {
                return await RunConnectedNodesSequential(nodeToSkip, stopBeforeNode, startNodeOverride);
            }

            return await RunConnectedNodesParallel(nodeToSkip, stopBeforeNode, startNodeOverride);
        }

        /// <summary>
        /// 相机回调高频入口的保守快速路径：图像源结果准备完成后，直接运行单一下游图像旋转节点，再从其下游恢复通用图运行时。
        /// </summary>
        /// <param name="nodeImage">本次相机回调对应的图像源节点。</param>
        /// <returns>快速路径处理结果；未命中时由普通图运行时继续处理。</returns>
        private async Task<CameraCallbackFastPathResult> TryRunCameraCallbackFastPath(NodeBase nodeImage)
        {
            NodeBase fastStartNode;
            if (!CanUseCameraCallbackFastPath(nodeImage, out fastStartNode))
                return CameraCallbackFastPathResult.NotHandled;

            Stopwatch fastPathWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            ConnectedNodeRunResult fastResult = await RunConnectedNode(fastStartNode, null);
            long afterFirstNode = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(fastPathWatch);
            if (fastResult == null)
                return CameraCallbackFastPathResult.HandledContinue;

            if (fastResult.Exception != null)
            {
                OperationCanceledException canceledException = fastResult.Exception as OperationCanceledException;
                if (canceledException != null)
                    throw canceledException;

                throw fastResult.Exception;
            }

            long prefixRunTime = RunTime + GetRunResultLogicalTime(fastResult);
            if (fastResult.FailedStatus)
            {
                RunTime = prefixRunTime;
                return CameraCallbackFastPathResult.HandledContinue;
            }

            NodeReturn firstReturn = fastResult.Return ?? new NodeReturn();
            if (firstReturn.Flag == NodeRunFlag.StopRun)
            {
                RunTime = prefixRunTime;
                return CameraCallbackFastPathResult.HandledStopRun;
            }

            if (firstReturn.Flag == NodeRunFlag.StopBranch)
            {
                RunTime = prefixRunTime;
                return CameraCallbackFastPathResult.HandledContinue;
            }

            NodeBase resumeNode = firstReturn.NextIndex >= 0
                ? GetNodeByLegacyIndex(firstReturn.NextIndex)
                : GetSingleNextNode(fastStartNode, firstReturn.NextBranch);
            if (resumeNode == null)
            {
                RunTime = prefixRunTime;
                return CameraCallbackFastPathResult.HandledContinue;
            }

            bool stoppedEarly = await RunConnectedNodes(null, null, resumeNode);
            long afterRemainder = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(fastPathWatch);
            RunTime += prefixRunTime;
            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => $"【性能诊断-相机回调快速路径】流程={ProcessName}；跳过节点=({nodeImage?.ID}.{nodeImage?.NodeName})；首节点=({fastStartNode.ID}.{fastStartNode.NodeName})；恢复节点=({resumeNode.ID}.{resumeNode.NodeName})；首节点含调度={afterFirstNode}ms；后续={afterRemainder - afterFirstNode}ms；总耗时={afterRemainder}ms",
                true);

            return stoppedEarly
                ? CameraCallbackFastPathResult.HandledStopRun
                : CameraCallbackFastPathResult.HandledContinue;
        }

        /// <summary>
        /// 判断当前相机回调是否可以使用首节点直跑优化；第一阶段只支持图像源到单一图像旋转的稳定直链。
        /// </summary>
        /// <param name="nodeImage">本次相机回调对应的图像源节点。</param>
        /// <param name="fastStartNode">命中快速路径时输出要直接执行的首个下游节点。</param>
        /// <returns>可以安全使用快速路径时返回 true。</returns>
        private bool CanUseCameraCallbackFastPath(NodeBase nodeImage, out NodeBase fastStartNode)
        {
            fastStartNode = null;
            if (nodeImage == null ||
                nodeImage.NodeType != NodeType.ImageSource ||
                !HasCanvasGraph ||
                !CanUseParallelConnectedRuntime() ||
                !TryGetSingleDefaultNextNode(nodeImage, out fastStartNode) ||
                fastStartNode == null ||
                !fastStartNode.Active ||
                fastStartNode.NodeType != NodeType.ImageRotate)
            {
                return false;
            }

            NodeBase ignoredResumeNode;
            if (CountDefaultNextNodes(fastStartNode, out ignoredResumeNode) > 1)
                return false;

            return true;
        }

        private async Task<bool> RunConnectedNodesSequential(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null, NodeBase startNodeOverride = null)
        {
            HashSet<int> visitedNodeIds = new HashSet<int>();
            HashSet<int> failedNodeIds = new HashSet<int>();
            Exception firstNodeException = null;
            foreach (NodeBase startNode in GetConnectionStartNodes(startNodeOverride))
            {
                if (startNode == null || visitedNodeIds.Contains(startNode.ID))
                    continue;

                LinkedList<NodeBase> pendingNodes = new LinkedList<NodeBase>();
                pendingNodes.AddLast(startNode);

                while (pendingNodes.Count > 0)
                {
                    NodeBase node = pendingNodes.First.Value;
                    pendingNodes.RemoveFirst();

                    if (node == null)
                        continue;

                    if (HasPendingSubscriptionDependency(node, visitedNodeIds, pendingNodes))
                    {
                        pendingNodes.AddLast(node);
                        continue;
                    }

                    if (HasPendingIncomingConnection(node, visitedNodeIds, failedNodeIds, pendingNodes))
                    {
                        pendingNodes.AddLast(node);
                        continue;
                    }

                    if (!visitedNodeIds.Add(node.ID))
                        continue;

                    if (node == stopBeforeNode)
                        return false;

                    if (HasFailedIncomingConnection(node.ID, failedNodeIds))
                    {
                        failedNodeIds.Add(node.ID);
                        AddConnectedNextNodes(node, pendingNodes, ProcessConnectionBranch.Default, true);
                        continue;
                    }

                    if (node == nodeToSkip || !node.Active)
                    {
                        AddConnectedNextNodes(node, pendingNodes, ProcessConnectionBranch.Default, true);
                        continue;
                    }

                    NodeReturn result;
                    try
                    {
                        MarkNodeRunning(node);
                        result = await RunNodeWithCpuResourcePolicy(this, node);
                        FinalizeNodeRun(node);
                        if (node.Result != null)
                            RunTime += node.Result.RunTime;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        MarkNodeFailed(node);
                        if (firstNodeException == null)
                            firstNodeException = ex;

                        failedNodeIds.Add(node.ID);
                        AddConnectedNextNodes(node, pendingNodes, ProcessConnectionBranch.Default, true);
                        continue;
                    }

                    if (IsNodeFailedStatus(node))
                    {
                        failedNodeIds.Add(node.ID);
                        AddConnectedNextNodes(node, pendingNodes, ProcessConnectionBranch.Default, true);
                        continue;
                    }

                    if (result.Flag == NodeRunFlag.StopRun)
                        return true;

                    if (result.Flag == NodeRunFlag.StopBranch)
                        continue;

                    if (result.NextIndex >= 0)
                    {
                        NodeBase nextByLegacyIndex = GetNodeByLegacyIndex(result.NextIndex);
                        if (nextByLegacyIndex != null)
                            pendingNodes.AddFirst(nextByLegacyIndex);
                        continue;
                    }

                    AddConnectedNextNodes(node, pendingNodes, result.NextBranch);
                }
            }

            if (firstNodeException != null)
                throw firstNodeException;

            return false;
        }

        private async Task<bool> RunConnectedNodesParallel(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null, NodeBase startNodeOverride = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long logicalRunTime = 0;
            Exception firstNodeException = null;
            try
            {
                HashSet<int> completedNodeIds = new HashSet<int>();
                Dictionary<int, long> completedNodeFinishTimes = new Dictionary<int, long>();
                foreach (NodeBase startNode in GetConnectionStartNodes(startNodeOverride))
                {
                    if (startNode == null || completedNodeIds.Contains(startNode.ID))
                        continue;

                    long componentStartTime = logicalRunTime;
                    HashSet<int> componentNodeIds = startNodeOverride == null
                        ? GetConnectedComponentNodeIds(startNode)
                        : GetDownstreamComponentNodeIds(startNode);
                    HashSet<int> inactiveNodeIds = new HashSet<int>();
                    HashSet<string> skippedEdges = new HashSet<string>();

                    HashSet<int> runningNodeIds = new HashSet<int>();
                    List<Task<ConnectedNodeRunResult>> runningTasks = new List<Task<ConnectedNodeRunResult>>();
                    Dictionary<Task<ConnectedNodeRunResult>, NodeBase> runningTaskNodes =
                        new Dictionary<Task<ConnectedNodeRunResult>, NodeBase>();
                    Dictionary<Task<ConnectedNodeRunResult>, ParallelNodeTaskGroup> runningTaskGroups =
                        new Dictionary<Task<ConnectedNodeRunResult>, ParallelNodeTaskGroup>();
                    bool stopRunRequested = false;
                    bool stopBeforeReached = false;

                    while (true)
                    {
                        if (!stopRunRequested && !stopBeforeReached)
                        {
                            List<NodeBase> readyNodes = GetReadyComponentNodes(componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges, startNode);
                            RemoveRunningNodes(readyNodes, runningNodeIds);

                            if (stopBeforeNode != null && ContainsNode(readyNodes, stopBeforeNode))
                            {
                                stopBeforeReached = true;
                            }
                            else
                            {
                                List<NodeBase> batch = BuildReadyExecutionBatch(readyNodes);
                                if (batch.Count == 1 && runningTasks.Count == 0)
                                {
                                    ConnectedNodeRunResult directResult = await RunConnectedNode(batch[0], nodeToSkip);
                                    stopRunRequested = ProcessCompletedConnectedNodeResult(
                                        directResult,
                                        componentNodeIds,
                                        completedNodeIds,
                                        inactiveNodeIds,
                                        skippedEdges,
                                        completedNodeFinishTimes,
                                        componentStartTime,
                                        ref logicalRunTime,
                                        ref firstNodeException);
                                    continue;
                                }

                                StartReadyNodeBatch(
                                    batch,
                                    nodeToSkip,
                                    runningNodeIds,
                                    runningTasks,
                                    runningTaskNodes,
                                    runningTaskGroups);
                            }
                        }

                        if (runningTasks.Count == 0)
                        {
                            if (stopBeforeReached)
                                return false;

                            if (stopRunRequested)
                                return true;

                            break;
                        }

                        Task<ConnectedNodeRunResult> finishedTask = await Task.WhenAny(runningTasks);
                        runningTasks.Remove(finishedTask);

                        NodeBase finishedNode;
                        runningTaskNodes.TryGetValue(finishedTask, out finishedNode);
                        runningTaskNodes.Remove(finishedTask);
                        if (finishedNode != null)
                            runningNodeIds.Remove(finishedNode.ID);

                        ReleaseCompletedTaskGroup(finishedTask, runningTaskGroups);

                        ConnectedNodeRunResult finishedResult = GetCompletedConnectedNodeResult(finishedTask, finishedNode);
                        bool nodeRequestedStopRun = ProcessCompletedConnectedNodeResult(
                            finishedResult,
                            componentNodeIds,
                            completedNodeIds,
                            inactiveNodeIds,
                            skippedEdges,
                            completedNodeFinishTimes,
                            componentStartTime,
                            ref logicalRunTime,
                            ref firstNodeException);
                        if (nodeRequestedStopRun)
                            stopRunRequested = true;
                    }
                }

                if (firstNodeException != null)
                    throw firstNodeException;

                return false;
            }
            finally
            {
                stopwatch.Stop();
                RunTime = logicalRunTime > 0 ? logicalRunTime : stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// 从候选节点中移除已经在执行中的节点，避免完成驱动调度重复启动同一节点。
        /// </summary>
        private static void RemoveRunningNodes(List<NodeBase> nodes, HashSet<int> runningNodeIds)
        {
            if (nodes == null || runningNodeIds == null || runningNodeIds.Count == 0)
                return;

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                NodeBase node = nodes[i];
                if (node == null || runningNodeIds.Contains(node.ID))
                    nodes.RemoveAt(i);
            }
        }

        /// <summary>
        /// 启动当前已经满足依赖的节点批次，后续由 Task.WhenAny 按完成顺序继续放行下游节点。
        /// </summary>
        private void StartReadyNodeBatch(
            List<NodeBase> batch,
            NodeBase nodeToSkip,
            HashSet<int> runningNodeIds,
            List<Task<ConnectedNodeRunResult>> runningTasks,
            Dictionary<Task<ConnectedNodeRunResult>, NodeBase> runningTaskNodes,
            Dictionary<Task<ConnectedNodeRunResult>, ParallelNodeTaskGroup> runningTaskGroups)
        {
            if (batch == null || batch.Count == 0)
                return;

            List<IParallelSignalWaitNode> preparedSignalWaitNodes = PrepareParallelSignalWaitNodes(batch);
            ParallelNodeTaskGroup taskGroup = new ParallelNodeTaskGroup(preparedSignalWaitNodes);

            foreach (NodeBase node in batch)
            {
                if (node == null || runningNodeIds.Contains(node.ID))
                    continue;

                NodeBase nodeToRun = node;
                Task<ConnectedNodeRunResult> task = Task.Run(async () => await RunConnectedNode(nodeToRun, nodeToSkip));
                runningNodeIds.Add(nodeToRun.ID);
                runningTasks.Add(task);
                runningTaskNodes[task] = nodeToRun;
                runningTaskGroups[task] = taskGroup;
                taskGroup.Tasks.Add(task);
            }

            if (taskGroup.Tasks.Count == 0)
                ReleaseParallelSignalWaitNodes(preparedSignalWaitNodes);
        }

        /// <summary>
        /// 释放已完成任务所属批次的并行信号等待预登记。
        /// </summary>
        private static void ReleaseCompletedTaskGroup(
            Task<ConnectedNodeRunResult> task,
            Dictionary<Task<ConnectedNodeRunResult>, ParallelNodeTaskGroup> runningTaskGroups)
        {
            if (task == null || runningTaskGroups == null)
                return;

            ParallelNodeTaskGroup taskGroup;
            if (!runningTaskGroups.TryGetValue(task, out taskGroup))
                return;

            runningTaskGroups.Remove(task);
            if (taskGroup == null)
                return;

            taskGroup.Tasks.Remove(task);
            if (taskGroup.Tasks.Count == 0)
                ReleaseParallelSignalWaitNodes(taskGroup.PreparedSignalWaitNodes);
        }

        /// <summary>
        /// 将已结束任务转换为统一的节点运行结果，保证异常分支也能走下游禁用逻辑。
        /// </summary>
        private ConnectedNodeRunResult GetCompletedConnectedNodeResult(Task<ConnectedNodeRunResult> task, NodeBase node)
        {
            if (task == null)
                return null;

            if (task.IsFaulted)
            {
                Exception exception = task.Exception == null
                    ? new Exception("并行分支节点运行失败。")
                    : task.Exception.GetBaseException();
                if (node != null)
                    MarkNodeFailed(node);

                return new ConnectedNodeRunResult
                {
                    Node = node,
                    Exception = exception,
                    RunTime = node == null || node.Result == null ? 0 : node.Result.RunTime,
                    FailedStatus = true
                };
            }

            if (task.IsCanceled)
            {
                return new ConnectedNodeRunResult
                {
                    Node = node,
                    Exception = new OperationCanceledException(),
                    RunTime = node == null || node.Result == null ? 0 : node.Result.RunTime,
                    FailedStatus = true
                };
            }

            return task.Result;
        }

        /// <summary>
        /// 处理单个完成节点的结果，并立即更新完成集合、逻辑耗时和分支跳过状态。
        /// </summary>
        private bool ProcessCompletedConnectedNodeResult(
            ConnectedNodeRunResult runResult,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges,
            Dictionary<int, long> completedNodeFinishTimes,
            long componentStartTime,
            ref long logicalRunTime,
            ref Exception firstNodeException)
        {
            if (runResult == null || runResult.Node == null)
                return false;

            NodeBase node = runResult.Node;
            completedNodeIds.Add(node.ID);

            long nodeStartTime = GetNodeDependencyFinishTime(
                node,
                componentNodeIds,
                inactiveNodeIds,
                skippedEdges,
                completedNodeFinishTimes,
                componentStartTime);
            long nodeFinishTime = nodeStartTime + GetRunResultLogicalTime(runResult);
            completedNodeFinishTimes[node.ID] = nodeFinishTime;
            if (nodeFinishTime > logicalRunTime)
                logicalRunTime = nodeFinishTime;

            if (runResult.Exception != null || runResult.FailedStatus)
            {
                if (runResult.Exception != null && firstNodeException == null)
                    firstNodeException = runResult.Exception;

                MarkFailedDownstreamInactive(node.ID, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
                return false;
            }

            if (!runResult.SkippedExecution &&
                runResult.Return != null &&
                runResult.Return.Flag == NodeRunFlag.StopRun)
            {
                return true;
            }

            if (!runResult.SkippedExecution)
            {
                if (runResult.Return != null &&
                    runResult.Return.Flag == NodeRunFlag.StopBranch)
                {
                    MarkStoppedBranchDownstreamInactive(node.ID, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
                    return false;
                }

                ApplyConditionalBranchSkips(node, runResult.Return, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
            }

            return false;
        }

        private async Task<ConnectedNodeRunResult> RunConnectedNode(NodeBase node, NodeBase nodeToSkip)
        {
            if (node == nodeToSkip || !node.Active)
            {
                return new ConnectedNodeRunResult
                {
                    Node = node,
                    Return = new NodeReturn(NodeRunFlag.ContinueRun),
                    SkippedExecution = true
                };
            }

            MarkNodeRunning(node);
            try
            {
                NodeReturn result = await RunNodeWithCpuResourcePolicy(this, node);
                FinalizeNodeRun(node);
                return new ConnectedNodeRunResult
                {
                    Node = node,
                    Return = result,
                    RunTime = node.Result == null ? 0 : node.Result.RunTime,
                    FailedStatus = IsNodeFailedStatus(node)
                };
            }
            catch (OperationCanceledException)
            {
                TraceNodeFaulted(node, "节点运行取消");
                throw;
            }
            catch (Exception ex)
            {
                MarkNodeFailed(node);
                return new ConnectedNodeRunResult
                {
                    Node = node,
                    Exception = ex,
                    RunTime = node.Result == null ? 0 : node.Result.RunTime,
                    FailedStatus = true
                };
            }
        }

        private long GetNodeDependencyFinishTime(
            NodeBase node,
            HashSet<int> componentNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges,
            Dictionary<int, long> completedNodeFinishTimes,
            long defaultStartTime)
        {
            if (node == null)
                return defaultStartTime;

            long dependencyFinishTime = defaultStartTime;
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.ToNodeId != node.ID ||
                    !componentNodeIds.Contains(connection.FromNodeId) ||
                    skippedEdges.Contains(GetConnectionEdgeKey(connection)) ||
                    inactiveNodeIds.Contains(connection.FromNodeId))
                {
                    continue;
                }

                long upstreamFinishTime;
                if (completedNodeFinishTimes.TryGetValue(connection.FromNodeId, out upstreamFinishTime) &&
                    upstreamFinishTime > dependencyFinishTime)
                {
                    dependencyFinishTime = upstreamFinishTime;
                }
            }

            foreach (int dependencyNodeId in GetSubscriptionDependencyNodeIds(node))
            {
                if (!IsActiveSubscriptionDependency(node.ID, dependencyNodeId, componentNodeIds, inactiveNodeIds))
                    continue;

                long upstreamFinishTime;
                if (completedNodeFinishTimes.TryGetValue(dependencyNodeId, out upstreamFinishTime) &&
                    upstreamFinishTime > dependencyFinishTime)
                {
                    dependencyFinishTime = upstreamFinishTime;
                }
            }

            return dependencyFinishTime;
        }

        private static long GetRunResultLogicalTime(ConnectedNodeRunResult runResult)
        {
            if (runResult == null || runResult.SkippedExecution || runResult.RunTime <= 0)
                return 0;

            return runResult.RunTime;
        }

        private void MarkFailedDownstreamInactive(
            int nodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != nodeId ||
                    !componentNodeIds.Contains(connection.ToNodeId))
                {
                    continue;
                }

                skippedEdges.Add(GetConnectionEdgeKey(connection));
                MarkFailedNodeInactive(connection.ToNodeId, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
            }
        }

        /// <summary>
        /// 将 StopBranch 节点的下游连线标记为跳过，只结束当前分支而不影响同批其它分支。
        /// </summary>
        private void MarkStoppedBranchDownstreamInactive(
            int nodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != nodeId ||
                    !componentNodeIds.Contains(connection.ToNodeId))
                {
                    continue;
                }

                skippedEdges.Add(GetConnectionEdgeKey(connection));
                TryMarkInactiveNode(connection.ToNodeId, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
            }
        }

        private void MarkFailedNodeInactive(
            int nodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            if (!componentNodeIds.Contains(nodeId) ||
                completedNodeIds.Contains(nodeId) ||
                inactiveNodeIds.Contains(nodeId))
            {
                return;
            }

            if (IsFailureTolerantNode(nodeId))
                return;

            inactiveNodeIds.Add(nodeId);
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId == nodeId &&
                    componentNodeIds.Contains(connection.ToNodeId))
                {
                    skippedEdges.Add(GetConnectionEdgeKey(connection));
                    MarkFailedNodeInactive(connection.ToNodeId, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
                }
            }
        }

        /// <summary>
        /// 根据当前可运行节点构建本轮执行批次。
        /// 分支控制节点只隔离自己的结构下游，并列分支仍保留在同一批次并发执行。
        /// </summary>
        private List<NodeBase> BuildReadyExecutionBatch(List<NodeBase> readyNodes)
        {
            List<NodeBase> batch = new List<NodeBase>();
            if (readyNodes.Count == 0)
                return batch;

            List<NodeBase> conditionalBranchNodes = GetReadyConditionalBranchNodes(readyNodes);
            if (conditionalBranchNodes.Count > 0)
            {
                foreach (NodeBase node in readyNodes)
                {
                    if (node == null)
                        continue;

                    if (IsConditionalBranchNode(node) ||
                        !IsDownstreamOfAnyConditionalBranchNode(node.ID, conditionalBranchNodes))
                    {
                        batch.Add(node);
                    }
                }

                return batch;
            }

            foreach (NodeBase node in readyNodes)
            {
                if (node == null)
                    continue;

                batch.Add(node);
            }

            return batch;
        }

        /// <summary>
        /// 获取本轮全部可运行的分支控制节点。
        /// </summary>
        private static List<NodeBase> GetReadyConditionalBranchNodes(List<NodeBase> readyNodes)
        {
            List<NodeBase> conditionalBranchNodes = new List<NodeBase>();
            foreach (NodeBase node in readyNodes)
            {
                if (IsConditionalBranchNode(node))
                    conditionalBranchNodes.Add(node);
            }

            return conditionalBranchNodes;
        }

        /// <summary>
        /// 判断候选节点是否属于任一当前待运行分支控制节点的结构下游。
        /// </summary>
        private bool IsDownstreamOfAnyConditionalBranchNode(int candidateNodeId, List<NodeBase> conditionalBranchNodes)
        {
            foreach (NodeBase conditionalBranchNode in conditionalBranchNodes)
            {
                if (conditionalBranchNode != null &&
                    IsReachableThroughConnections(conditionalBranchNode.ID, candidateNodeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 沿流程图连线判断目标节点是否可从起点节点到达。
        /// </summary>
        private bool IsReachableThroughConnections(int startNodeId, int targetNodeId)
        {
            if (startNodeId <= 0 || targetNodeId <= 0 || startNodeId == targetNodeId)
                return false;

            HashSet<int> visitedNodeIds = new HashSet<int>();
            Queue<int> pendingNodeIds = new Queue<int>();
            visitedNodeIds.Add(startNodeId);
            pendingNodeIds.Enqueue(startNodeId);

            while (pendingNodeIds.Count > 0)
            {
                int nodeId = pendingNodeIds.Dequeue();
                foreach (ProcessConnection connection in _connections)
                {
                    if (connection.FromNodeId != nodeId)
                        continue;

                    if (connection.ToNodeId == targetNodeId)
                        return true;

                    if (visitedNodeIds.Add(connection.ToNodeId))
                        pendingNodeIds.Enqueue(connection.ToNodeId);
                }
            }

            return false;
        }

        /// <summary>
        /// 并行批次启动前，预登记同一监听信号上的多个等待节点，避免首个节点复位后其它节点继续卡住。
        /// </summary>
        private static List<IParallelSignalWaitNode> PrepareParallelSignalWaitNodes(List<NodeBase> batch)
        {
            List<IParallelSignalWaitNode> preparedNodes = new List<IParallelSignalWaitNode>();
            if (batch == null || batch.Count <= 1)
                return preparedNodes;

            Dictionary<string, List<IParallelSignalWaitNode>> waitNodeGroups =
                new Dictionary<string, List<IParallelSignalWaitNode>>();

            foreach (NodeBase node in batch)
            {
                IParallelSignalWaitNode waitNode = node as IParallelSignalWaitNode;
                if (waitNode == null ||
                    !waitNode.CanPrepareParallelSignalWait ||
                    string.IsNullOrEmpty(waitNode.ParallelSignalWaitKey))
                {
                    continue;
                }

                List<IParallelSignalWaitNode> group;
                if (!waitNodeGroups.TryGetValue(waitNode.ParallelSignalWaitKey, out group))
                {
                    group = new List<IParallelSignalWaitNode>();
                    waitNodeGroups[waitNode.ParallelSignalWaitKey] = group;
                }

                group.Add(waitNode);
            }

            foreach (KeyValuePair<string, List<IParallelSignalWaitNode>> group in waitNodeGroups)
            {
                if (group.Value.Count <= 1)
                    continue;

                foreach (IParallelSignalWaitNode waitNode in group.Value)
                {
                    waitNode.BeginParallelSignalWait();
                    preparedNodes.Add(waitNode);
                }
            }

            return preparedNodes;
        }

        /// <summary>
        /// 释放并行批次中未被实际运行消费的信号等待预登记。
        /// </summary>
        private static void ReleaseParallelSignalWaitNodes(List<IParallelSignalWaitNode> preparedNodes)
        {
            if (preparedNodes == null)
                return;

            for (int i = preparedNodes.Count - 1; i >= 0; i--)
            {
                if (preparedNodes[i] != null)
                    preparedNodes[i].EndParallelSignalWait();
            }
        }

        private List<NodeBase> GetReadyComponentNodes(
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges,
            NodeBase startNode)
        {
            List<NodeBase> readyNodes = new List<NodeBase>();
            foreach (NodeBase node in _nodes)
            {
                if (node == null ||
                    !componentNodeIds.Contains(node.ID) ||
                    completedNodeIds.Contains(node.ID) ||
                    inactiveNodeIds.Contains(node.ID))
                {
                    continue;
                }

                if (IsNodeReady(node, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges, startNode))
                    readyNodes.Add(node);
            }

            return readyNodes;
        }

        private bool IsNodeReady(
            NodeBase node,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges,
            NodeBase startNode)
        {
            if (!AreSubscriptionDependenciesReady(node, componentNodeIds, completedNodeIds, inactiveNodeIds))
                return false;

            if (node == startNode)
                return true;

            foreach (ProcessConnection connection in _connections)
            {
                if (connection.ToNodeId != node.ID ||
                    !componentNodeIds.Contains(connection.FromNodeId) ||
                    skippedEdges.Contains(GetConnectionEdgeKey(connection)) ||
                    inactiveNodeIds.Contains(connection.FromNodeId))
                {
                    continue;
                }

                if (!completedNodeIds.Contains(connection.FromNodeId))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断节点参数中引用的订阅源是否已经完成，避免订阅节点先于被订阅节点运行。
        /// </summary>
        private bool AreSubscriptionDependenciesReady(
            NodeBase node,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds)
        {
            foreach (int dependencyNodeId in GetSubscriptionDependencyNodeIds(node))
            {
                if (!IsActiveSubscriptionDependency(node.ID, dependencyNodeId, componentNodeIds, inactiveNodeIds))
                    continue;

                if (!completedNodeIds.Contains(dependencyNodeId))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 判断顺序运行队列中是否仍有订阅源等待运行。
        /// </summary>
        private bool HasPendingSubscriptionDependency(
            NodeBase node,
            HashSet<int> visitedNodeIds,
            LinkedList<NodeBase> pendingNodes)
        {
            if (node == null || visitedNodeIds == null || pendingNodes == null || pendingNodes.Count == 0)
                return false;

            foreach (int dependencyNodeId in GetSubscriptionDependencyNodeIds(node))
            {
                if (dependencyNodeId <= 0 ||
                    dependencyNodeId == node.ID ||
                    visitedNodeIds.Contains(dependencyNodeId))
                {
                    continue;
                }

                if (ContainsPendingNode(pendingNodes, dependencyNodeId))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 按节点资源分类取得CPU许可后执行节点；未分类节点保持原执行路径。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="node">待执行节点。</param>
        /// <returns>节点运行控制结果。</returns>
        private static async Task<NodeReturn> RunNodeWithCpuResourcePolicy(Process process, NodeBase node)
        {
            Solution solution = Solution.Instance;
            WorkpieceExecutionContext workpieceContext = solution.WorkpieceContextAccessor.Current;
            CancellationToken cancellationToken = workpieceContext == null
                ? solution.CancellationToken
                : workpieceContext.CancellationToken;
            bool showLog = ShouldShowNodeLog(process, node);
            ICpuWorkloadClassifier classifier = solution.CpuWorkloadClassifier;
            if (classifier == null || !classifier.TryClassify(node, out CpuWorkloadKind workloadKind))
                return await node.Run(cancellationToken, showLog);

            using (ICpuWorkLease lease = await solution.AcquireCpuWorkAsync(workloadKind, cancellationToken))
                return await node.Run(cancellationToken, showLog);
        }

        /// <summary>
        /// 判断顺序运行队列中是否还有未完成的入线来源，避免多入线节点提前执行。
        /// </summary>
        private bool HasPendingIncomingConnection(
            NodeBase node,
            HashSet<int> visitedNodeIds,
            HashSet<int> failedNodeIds,
            LinkedList<NodeBase> pendingNodes)
        {
            if (node == null || visitedNodeIds == null || pendingNodes == null || pendingNodes.Count == 0)
                return false;

            foreach (ProcessConnection connection in _connections)
            {
                if (connection == null || connection.ToNodeId != node.ID)
                    continue;

                int upstreamNodeId = connection.FromNodeId;
                if (upstreamNodeId <= 0 ||
                    upstreamNodeId == node.ID ||
                    visitedNodeIds.Contains(upstreamNodeId) ||
                    IsReachableThroughConnections(node.ID, upstreamNodeId) ||
                    (failedNodeIds != null && failedNodeIds.Contains(upstreamNodeId)))
                {
                    continue;
                }

                if (IsPendingOrReachableFromPendingNodes(pendingNodes, upstreamNodeId))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断目标节点是否已经在待执行队列中，或能从队列内节点继续沿连线到达。
        /// </summary>
        private bool IsPendingOrReachableFromPendingNodes(LinkedList<NodeBase> pendingNodes, int targetNodeId)
        {
            if (pendingNodes == null || targetNodeId <= 0)
                return false;

            foreach (NodeBase pendingNode in pendingNodes)
            {
                if (pendingNode == null)
                    continue;

                if (pendingNode.ID == targetNodeId ||
                    IsReachableThroughConnections(pendingNode.ID, targetNodeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断订阅源是否是当前图组件内仍然有效的等待依赖。
        /// </summary>
        private bool IsActiveSubscriptionDependency(
            int nodeId,
            int dependencyNodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> inactiveNodeIds)
        {
            return dependencyNodeId > 0 &&
                dependencyNodeId != nodeId &&
                componentNodeIds != null &&
                componentNodeIds.Contains(dependencyNodeId) &&
                (inactiveNodeIds == null || !inactiveNodeIds.Contains(dependencyNodeId)) &&
                !IsDownstreamNodeId(nodeId, dependencyNodeId);
        }

        /// <summary>
        /// 获取节点声明的订阅源节点 ID。
        /// </summary>
        private static IEnumerable<int> GetSubscriptionDependencyNodeIds(NodeBase node)
        {
            INodeSubscriptionDependencyProvider provider = node as INodeSubscriptionDependencyProvider;
            if (provider == null)
                return new List<int>();

            IEnumerable<int> dependencyNodeIds = provider.GetSubscriptionDependencyNodeIds();
            return dependencyNodeIds ?? new List<int>();
        }

        /// <summary>
        /// 判断指定节点是否在当前节点的结构下游，用于避免隐藏订阅依赖形成反向等待。
        /// </summary>
        private bool IsDownstreamNodeId(int nodeId, int downstreamNodeId)
        {
            NodeBase downstreamNode = GetNodeById(downstreamNodeId);
            if (downstreamNode == null || nodeId <= 0)
                return false;

            foreach (NodeBase upstreamNode in GetUpstreamNodes(downstreamNode))
            {
                if (upstreamNode != null && upstreamNode.ID == nodeId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断顺序运行待执行队列中是否包含指定节点。
        /// </summary>
        private static bool ContainsPendingNode(LinkedList<NodeBase> pendingNodes, int nodeId)
        {
            foreach (NodeBase pendingNode in pendingNodes)
            {
                if (pendingNode != null && pendingNode.ID == nodeId)
                    return true;
            }

            return false;
        }

        private void ApplyConditionalBranchSkips(
            NodeBase node,
            NodeReturn result,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            if (!IsConditionalBranchNode(node) ||
                result == null ||
                result.NextBranch == ProcessConnectionBranch.Default)
            {
                return;
            }

            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != node.ID ||
                    !componentNodeIds.Contains(connection.ToNodeId) ||
                    ConnectionMatchesNodeOutput(node, connection, result.NextBranch))
                {
                    continue;
                }

                skippedEdges.Add(GetConnectionEdgeKey(connection));
                TryMarkInactiveNode(connection.ToNodeId, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
            }
        }

        private void TryMarkInactiveNode(
            int nodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> completedNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            if (!componentNodeIds.Contains(nodeId) ||
                completedNodeIds.Contains(nodeId) ||
                inactiveNodeIds.Contains(nodeId) ||
                HasActiveIncomingConnection(nodeId, componentNodeIds, inactiveNodeIds, skippedEdges))
            {
                return;
            }

            inactiveNodeIds.Add(nodeId);
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId == nodeId &&
                    componentNodeIds.Contains(connection.ToNodeId))
                {
                    TryMarkInactiveNode(connection.ToNodeId, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
                }
            }
        }

        private bool HasActiveIncomingConnection(
            int nodeId,
            HashSet<int> componentNodeIds,
            HashSet<int> inactiveNodeIds,
            HashSet<string> skippedEdges)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.ToNodeId == nodeId &&
                    componentNodeIds.Contains(connection.FromNodeId) &&
                    !inactiveNodeIds.Contains(connection.FromNodeId) &&
                    !skippedEdges.Contains(GetConnectionEdgeKey(connection)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasFailedIncomingConnection(int nodeId, HashSet<int> failedNodeIds)
        {
            if (failedNodeIds == null || failedNodeIds.Count == 0)
                return false;

            if (IsFailureTolerantNode(nodeId))
                return false;

            foreach (ProcessConnection connection in _connections)
            {
                if (connection.ToNodeId == nodeId && failedNodeIds.Contains(connection.FromNodeId))
                    return true;
            }

            return false;
        }

        private HashSet<int> GetConnectedComponentNodeIds(NodeBase startNode)
        {
            HashSet<int> componentNodeIds = new HashSet<int>();
            Queue<int> pendingNodeIds = new Queue<int>();
            if (startNode == null || !componentNodeIds.Add(startNode.ID))
                return componentNodeIds;

            pendingNodeIds.Enqueue(startNode.ID);
            while (pendingNodeIds.Count > 0)
            {
                int nodeId = pendingNodeIds.Dequeue();
                foreach (ProcessConnection connection in _connections)
                {
                    int connectedNodeId = 0;
                    if (connection.FromNodeId == nodeId)
                        connectedNodeId = connection.ToNodeId;
                    else if (connection.ToNodeId == nodeId)
                        connectedNodeId = connection.FromNodeId;

                    if (connectedNodeId > 0 &&
                        ContainsNode(connectedNodeId) &&
                        componentNodeIds.Add(connectedNodeId))
                    {
                        pendingNodeIds.Enqueue(connectedNodeId);
                    }
                }
            }

            return componentNodeIds;
        }

        /// <summary>
        /// 获取从指定节点沿输出连线可到达的下游节点集合，用于相机回调从图像源节点向后执行。
        /// </summary>
        private HashSet<int> GetDownstreamComponentNodeIds(NodeBase startNode)
        {
            HashSet<int> componentNodeIds = new HashSet<int>();
            Queue<int> pendingNodeIds = new Queue<int>();
            if (startNode == null || !componentNodeIds.Add(startNode.ID))
                return componentNodeIds;

            pendingNodeIds.Enqueue(startNode.ID);
            while (pendingNodeIds.Count > 0)
            {
                int nodeId = pendingNodeIds.Dequeue();
                foreach (ProcessConnection connection in _connections)
                {
                    if (connection.FromNodeId != nodeId ||
                        !ContainsNode(connection.ToNodeId) ||
                        !componentNodeIds.Add(connection.ToNodeId))
                    {
                        continue;
                    }

                    pendingNodeIds.Enqueue(connection.ToNodeId);
                }
            }

            return componentNodeIds;
        }

        private bool CanUseParallelConnectedRuntime()
        {
            if (!HasCanvasGraph)
                return false;

            foreach (NodeBase node in _nodes)
            {
                if (node == null)
                    continue;

                if (node.NodeType == NodeType.Else || node.NodeType == NodeType.EndIf)
                    return false;

                if (IsConditionalBranchNode(node) && !HasConditionalBranchConnections(node.ID))
                    return false;
            }

            return true;
        }

        private static bool ContainsNode(List<NodeBase> nodes, NodeBase target)
        {
            foreach (NodeBase node in nodes)
            {
                if (node == target)
                    return true;
            }

            return false;
        }

        private string GetConnectionEdgeKey(ProcessConnection connection)
        {
            if (connection == null)
                return string.Empty;

            ProcessConnectionBranch branch = GetEffectiveConnectionBranch(connection);
            return connection.FromNodeId.ToString() + ">" + connection.ToNodeId.ToString() + ">" + branch.ToString();
        }

        /// <summary>
        /// 流程图节点执行后的统一结果，用于调度器继续推进下游节点。
        /// </summary>
        private sealed class CameraCallbackFastPathResult
        {
            /// <summary>
            /// 未命中快速路径，调用方继续走普通图运行时。
            /// </summary>
            public static readonly CameraCallbackFastPathResult NotHandled = new CameraCallbackFastPathResult(false, false);

            /// <summary>
            /// 快速路径已处理，流程继续按普通成功出口收尾。
            /// </summary>
            public static readonly CameraCallbackFastPathResult HandledContinue = new CameraCallbackFastPathResult(true, false);

            /// <summary>
            /// 快速路径已处理，且节点要求提前结束整个流程。
            /// </summary>
            public static readonly CameraCallbackFastPathResult HandledStopRun = new CameraCallbackFastPathResult(true, true);

            /// <summary>
            /// 是否已经由快速路径处理。
            /// </summary>
            public readonly bool Handled;

            /// <summary>
            /// 是否需要按提前完成收尾。
            /// </summary>
            public readonly bool StoppedEarly;

            /// <summary>
            /// 创建相机回调快速路径结果。
            /// </summary>
            /// <param name="handled">是否已经处理。</param>
            /// <param name="stoppedEarly">是否提前结束流程。</param>
            private CameraCallbackFastPathResult(bool handled, bool stoppedEarly)
            {
                Handled = handled;
                StoppedEarly = stoppedEarly;
            }
        }

        /// <summary>
        /// 流程图节点执行后的统一结果，用于调度器继续推进下游节点。
        /// </summary>
        private sealed class ConnectedNodeRunResult
        {
            /// <summary>
            /// 本次执行对应的节点。
            /// </summary>
            public NodeBase Node;

            /// <summary>
            /// 节点返回的流程控制标记。
            /// </summary>
            public NodeReturn Return;

            /// <summary>
            /// 节点执行过程中捕获到的异常。
            /// </summary>
            public Exception Exception;

            /// <summary>
            /// 节点结果中记录的运行耗时。
            /// </summary>
            public int RunTime;

            /// <summary>
            /// 是否因为跳过节点或节点禁用而未实际执行。
            /// </summary>
            public bool SkippedExecution;

            /// <summary>
            /// 节点是否已经进入失败状态。
            /// </summary>
            public bool FailedStatus;
        }

        /// <summary>
        /// 同一轮启动的并行节点任务组，用于最后一个节点完成时释放等待信号预登记。
        /// </summary>
        private sealed class ParallelNodeTaskGroup
        {
            /// <summary>
            /// 本组仍未完成的节点任务。
            /// </summary>
            public readonly HashSet<Task<ConnectedNodeRunResult>> Tasks = new HashSet<Task<ConnectedNodeRunResult>>();

            /// <summary>
            /// 本组启动前预登记的信号等待节点。
            /// </summary>
            public readonly List<IParallelSignalWaitNode> PreparedSignalWaitNodes;

            /// <summary>
            /// 创建并行任务组。
            /// </summary>
            public ParallelNodeTaskGroup(List<IParallelSignalWaitNode> preparedSignalWaitNodes)
            {
                PreparedSignalWaitNodes = preparedSignalWaitNodes;
            }
        }

        private List<NodeBase> GetConnectionStartNodes(NodeBase startNodeOverride = null)
        {
            List<NodeBase> startNodes = new List<NodeBase>();
            if (startNodeOverride != null && _nodes.Contains(startNodeOverride))
            {
                startNodes.Add(startNodeOverride);
                return startNodes;
            }

            HashSet<int> explicitComponents = new HashSet<int>();

            foreach (NodeBase node in _nodes)
            {
                if (!node.IsStartNode)
                    continue;

                startNodes.Add(node);
                MarkConnectedComponent(node, explicitComponents);
            }

            foreach (NodeBase node in _nodes)
            {
                if (explicitComponents.Contains(node.ID) || HasIncomingConnection(node.ID))
                    continue;

                startNodes.Add(node);
                MarkConnectedComponent(node, explicitComponents);
            }

            if (startNodes.Count == 0 && _nodes.Count > 0)
                startNodes.Add(_nodes[0]);

            return startNodes;
        }

        private void MarkConnectedComponent(NodeBase startNode, HashSet<int> componentNodeIds)
        {
            Queue<int> pendingNodeIds = new Queue<int>();
            if (!componentNodeIds.Add(startNode.ID))
                return;

            pendingNodeIds.Enqueue(startNode.ID);
            while (pendingNodeIds.Count > 0)
            {
                int nodeId = pendingNodeIds.Dequeue();
                foreach (ProcessConnection connection in _connections)
                {
                    int connectedNodeId = 0;
                    if (connection.FromNodeId == nodeId)
                        connectedNodeId = connection.ToNodeId;
                    else if (connection.ToNodeId == nodeId)
                        connectedNodeId = connection.FromNodeId;

                    if (connectedNodeId > 0 &&
                        ContainsNode(connectedNodeId) &&
                        componentNodeIds.Add(connectedNodeId))
                    {
                        pendingNodeIds.Enqueue(connectedNodeId);
                    }
                }
            }
        }

        private void AddConnectedNextNodes(
            NodeBase node,
            LinkedList<NodeBase> pendingNodes,
            ProcessConnectionBranch branch = ProcessConnectionBranch.Default,
            bool includeAllBranches = false)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != node.ID ||
                    (!includeAllBranches && !ConnectionMatchesNodeOutput(node, connection, branch)))
                    continue;

                NodeBase nextNode = GetNodeById(connection.ToNodeId);
                if (nextNode != null)
                    pendingNodes.AddLast(nextNode);
            }
        }

        /// <summary>
        /// 获取节点唯一的默认下游节点；没有下游或存在多个默认下游时返回 false。
        /// </summary>
        /// <param name="node">待检查的上游节点。</param>
        /// <param name="nextNode">唯一默认下游节点。</param>
        /// <returns>仅存在一个默认下游节点时返回 true。</returns>
        private bool TryGetSingleDefaultNextNode(NodeBase node, out NodeBase nextNode)
        {
            return CountDefaultNextNodes(node, out nextNode) == 1;
        }

        /// <summary>
        /// 统计节点默认下游节点数量，并在数量为一时输出该节点。
        /// </summary>
        /// <param name="node">待检查的上游节点。</param>
        /// <param name="singleNode">默认下游数量为一时输出该节点，否则为空。</param>
        /// <returns>默认下游节点数量。</returns>
        private int CountDefaultNextNodes(NodeBase node, out NodeBase singleNode)
        {
            singleNode = null;
            int count = 0;
            if (node == null)
                return count;

            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != node.ID ||
                    GetEffectiveConnectionBranch(connection) != ProcessConnectionBranch.Default)
                {
                    continue;
                }

                NodeBase candidate = GetNodeById(connection.ToNodeId);
                if (candidate == null)
                    continue;

                count++;
                singleNode = count == 1 ? candidate : null;
            }

            return count;
        }

        /// <summary>
        /// 按节点本次输出分支获取唯一后续节点；没有后续或多个后续时返回空并交由通用逻辑兜底。
        /// </summary>
        /// <param name="node">刚执行完成的节点。</param>
        /// <param name="branch">节点本次输出分支。</param>
        /// <returns>唯一后续节点；不存在唯一节点时返回 null。</returns>
        private NodeBase GetSingleNextNode(NodeBase node, ProcessConnectionBranch branch)
        {
            NodeBase singleNode = null;
            int count = 0;
            if (node == null)
                return null;

            foreach (ProcessConnection connection in _connections)
            {
                if (connection.FromNodeId != node.ID ||
                    !ConnectionMatchesNodeOutput(node, connection, branch))
                {
                    continue;
                }

                NodeBase candidate = GetNodeById(connection.ToNodeId);
                if (candidate == null)
                    continue;

                count++;
                singleNode = count == 1 ? candidate : null;
            }

            return count == 1 ? singleNode : null;
        }

        /// <summary>
        /// 判断连线是否应该跟随节点本次输出分支；多条件的默认分支作为公共路径始终放行。
        /// </summary>
        private bool ConnectionMatchesNodeOutput(NodeBase node, ProcessConnection connection, ProcessConnectionBranch branch)
        {
            if (ConnectionMatchesBranch(connection, branch))
                return true;

            return IsMultiConditionDefaultOutput(node, connection, branch);
        }

        /// <summary>
        /// 多条件节点支持默认执行路径，True/False 判定完成后默认分支也继续执行。
        /// </summary>
        private bool IsMultiConditionDefaultOutput(NodeBase node, ProcessConnection connection, ProcessConnectionBranch branch)
        {
            if (node == null ||
                node.NodeType != NodeType.MultiCondition ||
                branch == ProcessConnectionBranch.Default)
            {
                return false;
            }

            return GetEffectiveConnectionBranch(connection) == ProcessConnectionBranch.Default;
        }

        private bool ConnectionMatchesBranch(ProcessConnection connection, ProcessConnectionBranch branch)
        {
            ProcessConnectionBranch connectionBranch = GetEffectiveConnectionBranch(connection);

            if (branch == ProcessConnectionBranch.Default)
                return connectionBranch == ProcessConnectionBranch.Default;

            return connectionBranch == branch;
        }

        private ProcessConnectionBranch GetEffectiveConnectionBranch(ProcessConnection connection)
        {
            if (connection == null || connection.Branch != ProcessConnectionBranch.Default)
                return connection == null ? ProcessConnectionBranch.Default : connection.Branch;

            if (!HasCanvasGraph)
                return ProcessConnectionBranch.Default;

            NodeBase fromNode = GetNodeById(connection.FromNodeId);
            if (!IsConditionalBranchNode(fromNode))
                return ProcessConnectionBranch.Default;

            if (string.Equals(connection.FromAnchor, "Right", StringComparison.OrdinalIgnoreCase))
                return ProcessConnectionBranch.True;

            if (string.Equals(connection.FromAnchor, "Bottom", StringComparison.OrdinalIgnoreCase))
                return ProcessConnectionBranch.False;

            return ProcessConnectionBranch.Default;
        }

        private static bool IsConditionalBranchNode(NodeBase node)
        {
            return node != null &&
                (node.NodeType == NodeType.If || node.NodeType == NodeType.MultiCondition);
        }

        private bool IsFailureTolerantNode(int nodeId)
        {
            NodeBase node = GetNodeById(nodeId);
            return node != null &&
                (node.NodeType == NodeType.MultiCondition ||
                 node.NodeType == NodeType.ResultOverlayDraw);
        }

        private bool HasIncomingConnection(int nodeId)
        {
            foreach (ProcessConnection connection in _connections)
            {
                if (connection.ToNodeId == nodeId)
                    return true;
            }

            return false;
        }

        private bool ContainsNode(int nodeId)
        {
            return GetNodeById(nodeId) != null;
        }

        private NodeBase GetNodeById(int nodeId)
        {
            foreach (NodeBase node in _nodes)
            {
                if (node.ID == nodeId)
                    return node;
            }

            return null;
        }

        private NodeBase GetNodeByLegacyIndex(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodes.Count)
                return null;

            return _nodes[nodeIndex];
        }

        private static void MarkNodeRunning(NodeBase node)
        {
            if (node != null)
            {
                node.SetStatus(NodeStatus.Running, "*");
                node.Process?.TraceNodeStarted(node);
            }
        }

        private static void MarkNodeFailed(NodeBase node)
        {
            if (node == null || node.RuntimeStatus == NodeStatus.Failed)
                return;

            string runTime = node.Result == null || node.Result.RunTime <= 0
                ? "0"
                : node.Result.RunTime.ToString();
            node.SetStatus(NodeStatus.Failed, runTime);
            node.Process?.TraceNodeFaulted(node, "节点执行器捕获到未处理异常");
        }

        private static void FinalizeNodeRun(NodeBase node)
        {
            if (node != null)
                node.ApplyResultOutcomeStatus();
        }

        private static bool IsNodeFailedStatus(NodeBase node)
        {
            return node != null && node.RuntimeStatus == NodeStatus.Failed;
        }

        private bool HasFailedRuntimeNode()
        {
            foreach (NodeBase node in _nodes)
            {
                if (IsNodeFailedStatus(node))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断本轮是否已成功跑完当前流程的全部启用节点；禁用节点不参与数量比较。
        /// </summary>
        private bool HasExecutedAllEnabledNodesInCurrentRun()
        {
            int enabledNodeCount = 0;
            int executedNodeCount = 0;

            foreach (NodeBase node in _nodes)
            {
                if (node == null || !node.Active)
                    continue;

                enabledNodeCount++;
                if (node.HasSuccessfulResultForRun(CurrentRunId))
                    executedNodeCount++;
            }

            return enabledNodeCount > 0 && executedNodeCount >= enabledNodeCount;
        }

        /// <summary>
        /// 判断当前运行批次中是否有允许输出日志的节点成功执行过。
        /// </summary>
        private bool HasOutputLogNodeExecutedInCurrentRun()
        {
            foreach (NodeBase node in _nodes)
            {
                if (node != null &&
                    node.OutputLog &&
                    node.HasSuccessfulResultForRun(CurrentRunId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断当前运行批次中是否有关键业务输出变化；计数变化和业务NG都不能按空跑过滤。
        /// </summary>
        private bool HasKeyBusinessOutputChangedInCurrentRun()
        {
            return OKNumber != _runStartOKNumber ||
                NGNumber != _runStartNGNumber ||
                HasBusinessNgNodeInCurrentRun();
        }

        /// <summary>
        /// 判断当前运行批次中是否有业务结果 NG 的节点，NG 属于关键业务状态，不能按空跑过滤。
        /// </summary>
        private bool HasBusinessNgNodeInCurrentRun()
        {
            foreach (NodeBase node in _nodes)
            {
                if (node != null &&
                    node.RuntimeResultNg &&
                    node.HasSuccessfulResultForRun(CurrentRunId))
                {
                    return true;
                }
            }

            return false;
        }

        private void BeginProcessRun()
        {
            _runStartOKNumber = OKNumber;
            _runStartNGNumber = NGNumber;
            CurrentRunId = CurrentRunId == int.MaxValue ? 1 : CurrentRunId + 1;
            foreach (var node in Nodes)
                node.BeginProcessRun(CurrentRunId);

            _performanceTrace = ProcessPerformanceTrace.CreateIfEnabled(
                ID,
                ProcessName,
                CurrentRunId,
                Nodes.Count,
                ShouldRunByConnections());
        }

        /// <summary>
        /// 在任何节点或相机软触发执行前取得单流程FIFO执行权，并建立或继承工件上下文。
        /// </summary>
        /// <param name="triggerSource">本轮运行入口说明。</param>
        /// <returns>负责封口、恢复上下文和释放执行权的运行作用域。</returns>
        private async Task<ManagedProcessRunScope> EnterManagedProcessRunAsync(
            string triggerSource,
            CancellationToken cancellationToken,
            ProcessRunPathScope processPathScope)
        {
            if (processPathScope == null)
                throw new ArgumentNullException(nameof(processPathScope));

            ProcessWorkpieceGate gate = LazyInitializer.EnsureInitialized(
                ref _workpieceRunGate,
                () => new ProcessWorkpieceGate($"{ID}.{ProcessName}"));
            IProcessWorkpieceLease lease;
            try
            {
                lease = await gate.EnterAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessWorkpieceQueueFullException ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, ex.Message, true);
                throw;
            }

            try
            {
                IWorkpieceContextAccessor accessor = Solution.Instance.WorkpieceContextAccessor;
                WorkpieceExecutionContext inheritedContext = accessor.Current;
                IWorkpieceExecutionLease executionLease;
                if (Solution.Instance.TryAcquireInheritedWorkpieceContext(
                    inheritedContext,
                    out executionLease))
                {
                    return new ManagedProcessRunScope(
                        lease,
                        processPathScope,
                        inheritedContext,
                        false,
                        executionLease);
                }

                string orderDomain = string.IsNullOrWhiteSpace(ProductionOrderDomain)
                    ? $"GROUP:{Group}"
                    : ProductionOrderDomain;
                WorkpieceExecutionContext ownedContext = Solution.Instance.CreateWorkpieceExecutionContext(
                    orderDomain,
                    FlowRevision,
                    triggerSource,
                    cancellationToken);
                return new ManagedProcessRunScope(
                    lease,
                    processPathScope,
                    ownedContext,
                    true,
                    null);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 在当前异步分支压入本流程；同一流程再次出现表示触发图存在执行环。
        /// </summary>
        /// <returns>按后进先出恢复调用路径的作用域。</returns>
        private ProcessRunPathScope PushProcessRunPath()
        {
            ProcessRunPath previousPath = CurrentProcessRunPath.Value;
            WorkpieceExecutionContext currentContext = Solution.Instance.WorkpieceContextAccessor.Current;
            bool hasCurrentContext = Solution.Instance.CanInheritWorkpieceContext(currentContext);
            if (!hasCurrentContext)
                previousPath = null;

            for (ProcessRunPath cursor = previousPath; cursor != null; cursor = cursor.Parent)
            {
                if (cursor.Identity.HasValue &&
                    cursor.Identity.Value.Equals(currentContext.Identity) &&
                    ReferenceEquals(cursor.Process, this))
                {
                    throw new InvalidOperationException(
                        $"检测到流程触发环：流程【{ProcessName}】在当前工件调用链中被重复触发。");
                }
            }

            ProcessRunPath currentPath = new ProcessRunPath(this, previousPath);
            CurrentProcessRunPath.Value = currentPath;
            return new ProcessRunPathScope(currentPath, previousPath);
        }

        /// <summary>
        /// 根据流程结果封口本入口创建的上下文，并始终释放作用域和执行租约。
        /// </summary>
        /// <param name="scope">当前运行作用域。</param>
        /// <param name="succeeded">流程业务执行是否成功。</param>
        private async Task ExitManagedProcessRunAsync(ManagedProcessRunScope scope, bool succeeded)
        {
            if (scope == null)
                return;

            try
            {
                WorkpieceTerminalState terminalState = scope.Context.CancellationToken.IsCancellationRequested
                    ? WorkpieceTerminalState.Cancelled
                    : succeeded ? WorkpieceTerminalState.Succeeded : WorkpieceTerminalState.Faulted;
                scope.CompleteExecutionBranch(terminalState);
                await scope.CompleteOwnedContextAsync(terminalState).ConfigureAwait(false);
            }
            finally
            {
                scope.Dispose();
            }
        }

        /// <summary>获取单流程工件入口的只读运行快照。</summary>
        /// <returns>尚未首次运行时返回默认空快照。</returns>
        public ProcessWorkpieceGateSnapshot GetWorkpieceGateSnapshot()
        {
            ProcessWorkpieceGate gate = Volatile.Read(ref _workpieceRunGate);
            return gate == null
                ? new ProcessWorkpieceGateSnapshot
                {
                    Capacity = ProcessWorkpieceGate.DefaultCapacity,
                    HasActiveWorkpiece = false,
                    WaitingCount = 0,
                    PeakOccupancy = 0,
                    RejectedFullCount = 0L
                }
                : gate.GetSnapshot();
        }

        /// <summary>单次流程运行的工件上下文和FIFO租约所有权作用域。</summary>
        private sealed class ManagedProcessRunScope : IDisposable
        {
            /// <summary>单流程执行租约。</summary>
            private IProcessWorkpieceLease _lease;

            /// <summary>本入口创建上下文时对应的AsyncLocal恢复作用域。</summary>
            private IDisposable _contextScope;

            /// <summary>本入口对应的流程触发路径恢复作用域。</summary>
            private ProcessRunPathScope _processPathScope;

            /// <summary>继承父工件时持有的在途流程分支租约。</summary>
            private IWorkpieceExecutionLease _executionLease;

            /// <summary>本入口创建或继承的工件上下文。</summary>
            private readonly WorkpieceExecutionContext _context;

            /// <summary>本入口是否拥有工件终态封口权。</summary>
            private readonly bool _ownsContext;

            /// <summary>
            /// 创建流程运行所有权作用域。
            /// </summary>
            /// <param name="lease">单流程执行租约。</param>
            /// <param name="processPathScope">流程触发路径恢复作用域。</param>
            /// <param name="context">当前工件上下文。</param>
            /// <param name="ownsContext">是否由本入口创建上下文。</param>
            public ManagedProcessRunScope(
                IProcessWorkpieceLease lease,
                ProcessRunPathScope processPathScope,
                WorkpieceExecutionContext context,
                bool ownsContext,
                IWorkpieceExecutionLease executionLease)
            {
                _lease = lease ?? throw new ArgumentNullException(nameof(lease));
                _processPathScope = processPathScope ?? throw new ArgumentNullException(nameof(processPathScope));
                _context = context ?? throw new ArgumentNullException(nameof(context));
                _ownsContext = ownsContext;
                _executionLease = executionLease;
            }

            /// <summary>
            /// 在取得FIFO执行权后由流程调用者同步激活自有工件上下文，确保AsyncLocal对节点链可见。
            /// </summary>
            /// <param name="accessor">全方案工件上下文访问器。</param>
            public void ActivateOwnedContext(IWorkpieceContextAccessor accessor)
            {
                _processPathScope.BindIdentity(_context.Identity);
                if (!_ownsContext)
                    return;
                if (accessor == null)
                    throw new ArgumentNullException(nameof(accessor));
                if (_contextScope != null)
                    throw new InvalidOperationException("工件上下文不能重复激活。");

                _contextScope = accessor.Push(_context);
            }

            /// <summary>
            /// 仅由上下文创建者请求终态，并等待全部已预约信号终结。
            /// </summary>
            /// <param name="terminalState">流程请求的终态。</param>
            /// <returns>继承上下文时立即完成；自有上下文在信号排空后完成。</returns>
            public Task CompleteOwnedContextAsync(WorkpieceTerminalState terminalState)
            {
                if (!_ownsContext)
                    return Task.CompletedTask;

                return Solution.Instance.CompleteWorkpieceExecutionAsync(
                    _context,
                    terminalState);
            }

            /// <summary>
            /// 把继承流程的实际终态合并到父工件；自有上下文不持有分支租约。
            /// </summary>
            /// <param name="terminalState">本次流程运行终态。</param>
            public void CompleteExecutionBranch(WorkpieceTerminalState terminalState)
            {
                IWorkpieceExecutionLease executionLease = Interlocked.Exchange(ref _executionLease, null);
                executionLease?.Complete(terminalState);
            }

            /// <summary>获取本次流程运行继承或创建的工件上下文。</summary>
            public WorkpieceExecutionContext Context => _context;

            /// <summary>幂等恢复上下文并释放单流程执行权。</summary>
            public void Dispose()
            {
                IDisposable contextScope = Interlocked.Exchange(ref _contextScope, null);
                IProcessWorkpieceLease lease = Interlocked.Exchange(ref _lease, null);
                IWorkpieceExecutionLease executionLease = Interlocked.Exchange(ref _executionLease, null);
                ProcessRunPathScope processPathScope = Interlocked.Exchange(ref _processPathScope, null);
                try
                {
                    contextScope?.Dispose();
                }
                finally
                {
                    try
                    {
                        executionLease?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            lease?.Dispose();
                        }
                        finally
                        {
                            processPathScope?.Dispose();
                        }
                    }
                }
            }
        }

        /// <summary>当前异步分支的不可变流程调用路径节点。</summary>
        private sealed class ProcessRunPath
        {
            /// <summary>
            /// 创建流程调用路径节点。
            /// </summary>
            /// <param name="process">当前流程。</param>
            /// <param name="parent">上一层调用路径。</param>
            public ProcessRunPath(Process process, ProcessRunPath parent)
            {
                Process = process ?? throw new ArgumentNullException(nameof(process));
                Parent = parent;
            }

            /// <summary>获取当前路径节点对应的流程。</summary>
            public Process Process { get; }

            /// <summary>获取上一层流程调用路径。</summary>
            public ProcessRunPath Parent { get; }

            /// <summary>获取本路径节点绑定的工件身份；尚未取得FIFO执行权时为空。</summary>
            public WorkpieceIdentity? Identity { get; private set; }

            /// <summary>
            /// 在流程取得执行权后一次性绑定实际工件身份。
            /// </summary>
            /// <param name="identity">本次流程运行实际使用的工件身份。</param>
            public void BindIdentity(WorkpieceIdentity identity)
            {
                if (Identity.HasValue && !Identity.Value.Equals(identity))
                    throw new InvalidOperationException("流程调用路径不能改绑到其他工件身份。");
                Identity = identity;
            }
        }

        /// <summary>严格按后进先出规则恢复流程调用路径。</summary>
        private sealed class ProcessRunPathScope : IDisposable
        {
            /// <summary>本作用域压入的路径节点。</summary>
            private readonly ProcessRunPath _expectedPath;

            /// <summary>释放时需要恢复的上一层路径。</summary>
            private readonly ProcessRunPath _previousPath;

            /// <summary>作用域释放状态，1表示已经释放。</summary>
            private int _isDisposed;

            /// <summary>
            /// 创建流程调用路径恢复作用域。
            /// </summary>
            /// <param name="expectedPath">本作用域压入的路径。</param>
            /// <param name="previousPath">上一层路径。</param>
            public ProcessRunPathScope(ProcessRunPath expectedPath, ProcessRunPath previousPath)
            {
                _expectedPath = expectedPath;
                _previousPath = previousPath;
            }

            /// <summary>
            /// 把已经获得执行权的流程路径绑定到实际工件身份。
            /// </summary>
            /// <param name="identity">当前工件身份。</param>
            public void BindIdentity(WorkpieceIdentity identity)
            {
                _expectedPath.BindIdentity(identity);
            }

            /// <summary>恢复上一层流程路径；乱序释放时明确失败。</summary>
            public void Dispose()
            {
                if (Volatile.Read(ref _isDisposed) == 1)
                    return;
                if (!ReferenceEquals(CurrentProcessRunPath.Value, _expectedPath))
                    throw new InvalidOperationException("流程调用路径作用域必须按后进先出顺序释放。");

                CurrentProcessRunPath.Value = _previousPath;
                Volatile.Write(ref _isDisposed, 1);
            }
        }

        /// <summary>
        /// 记录节点进入执行状态。
        /// </summary>
        /// <param name="node">当前节点。</param>
        internal void TraceNodeStarted(NodeBase node)
        {
            _performanceTrace?.NodeStarted(node);
        }

        /// <summary>
        /// 记录节点通过统一结果入口完成。
        /// </summary>
        /// <param name="node">当前节点。</param>
        /// <param name="status">节点状态。</param>
        /// <param name="elapsedMilliseconds">节点报告耗时。</param>
        internal void TraceNodeCompleted(NodeBase node, NodeStatus status, int elapsedMilliseconds)
        {
            _performanceTrace?.NodeCompleted(node, status, elapsedMilliseconds);
        }

        /// <summary>
        /// 记录节点在统一结果入口之外发生的异常或中断。
        /// </summary>
        /// <param name="node">当前节点。</param>
        /// <param name="reason">异常或中断原因。</param>
        internal void TraceNodeFaulted(NodeBase node, string reason)
        {
            _performanceTrace?.NodeFaulted(node, reason);
        }

        /// <summary>
        /// 结束本轮Debug完整耗时追踪；重复调用不会重复写日志。
        /// </summary>
        /// <param name="statusText">流程结束状态。</param>
        private void CompletePerformanceTrace(string statusText)
        {
            _performanceTrace?.Complete(statusText, RunTime);
        }

        /// <summary>
        /// 为被跳过执行的节点准备本次运行结果，避免下游订阅读到旧批次数据。
        /// </summary>
        private bool TryPrepareSkippedNodeRunResult(NodeBase nodeToSkip)
        {
            Stopwatch prepareWatch = PerformanceSpikeDiagnostics.StartStopwatchIfEnabled(MsgLevel.Debug);
            ISkippedNodeRunResultProvider provider = nodeToSkip as ISkippedNodeRunResultProvider;
            if (provider == null)
            {
                PerformanceSpikeDiagnostics.LogIfEnabled(MsgLevel.Debug,
                    () => $"【链路诊断-跳过节点准备跳过】流程={ProcessName}；RunId={CurrentRunId}；节点={GetSkippedNodeText(nodeToSkip)}；Provider=无；主程序={PerformanceSpikeDiagnostics.GetCurrentProcessPathText()}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                    true);
                return true;
            }

            PerformanceSpikeDiagnostics.LogIfEnabled(MsgLevel.Debug,
                () => $"【链路诊断-跳过节点准备开始】流程={ProcessName}；RunId={CurrentRunId}；节点={GetSkippedNodeText(nodeToSkip)}；Provider={PerformanceSpikeDiagnostics.GetAssemblyText(provider)}；结果前={GetNodeResultText(nodeToSkip.Result)}；主程序={PerformanceSpikeDiagnostics.GetCurrentProcessPathText()}",
                true);

            string message;
            bool prepared;
            try
            {
                prepared = provider.TryPrepareSkippedRunResult(DateTime.Now, out message);
            }
            catch (Exception ex)
            {
                prepareWatch?.Stop();
                long prepareElapsedMs = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(prepareWatch);
                PerformanceSpikeDiagnostics.LogIfEnabled(MsgLevel.Exception,
                    () => $"【链路诊断-跳过节点准备异常】流程={ProcessName}；RunId={CurrentRunId}；节点={GetSkippedNodeText(nodeToSkip)}；Provider耗时={prepareElapsedMs}ms；异常={ex.GetType().Name}:{ex.Message}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                    true);
                throw;
            }

            prepareWatch?.Stop();
            long prepareElapsed = PerformanceSpikeDiagnostics.GetElapsedMilliseconds(prepareWatch);
            int appendedRunTime = nodeToSkip.Result == null ? 0 : nodeToSkip.Result.RunTime;
            PerformanceSpikeDiagnostics.LogIfEnabled(MsgLevel.Debug,
                () => $"【链路诊断-跳过节点准备结束】流程={ProcessName}；RunId={CurrentRunId}；节点={GetSkippedNodeText(nodeToSkip)}；成功={prepared}；Provider耗时={prepareElapsed}ms；消息={FormatDiagnosticMessage(message)}；结果后={GetNodeResultText(nodeToSkip.Result)}；RunTime追加={appendedRunTime}ms",
                true);
            PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                MsgLevel.Debug,
                PerformanceSpikeDiagnostics.CommonSlowMs,
                () => $"【慢诊断-跳过节点准备】流程={ProcessName}；RunId={CurrentRunId}；节点={GetSkippedNodeText(nodeToSkip)}；Provider耗时={prepareElapsed}ms；结果后={GetNodeResultText(nodeToSkip.Result)}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                true,
                prepareElapsed);

            if (prepared)
            {
                if (nodeToSkip.Result != null)
                    RunTime += nodeToSkip.Result.RunTime;
                return true;
            }

            if (ShouldShowNodeLog(this, nodeToSkip) && !string.IsNullOrWhiteSpace(message))
                LogHelper.AddLog(MsgLevel.Warn, $"节点({nodeToSkip.ID}.{nodeToSkip.NodeName})准备跳过运行结果失败：{message}", true);

            return false;
        }

        /// <summary>
        /// 生成被跳过节点的关键身份信息，便于从日志中确认调度目标。
        /// </summary>
        /// <param name="node">被跳过执行的节点。</param>
        /// <returns>节点编号、名称、类型和程序集文本。</returns>
        private static string GetSkippedNodeText(NodeBase node)
        {
            if (node == null)
                return "空";

            return $"({node.ID}.{node.NodeName})；节点类型={PerformanceSpikeDiagnostics.GetAssemblyText(node)}";
        }

        /// <summary>
        /// 生成节点结果摘要，图像源节点会额外输出图像对象结构，便于判断结果是否真的写入。
        /// </summary>
        /// <param name="result">节点运行结果。</param>
        /// <returns>节点结果摘要文本。</returns>
        private static string GetNodeResultText(INodeResult result)
        {
            if (result == null)
                return "空";

            NodeResultImageSource imageSourceResult = result as NodeResultImageSource;
            if (imageSourceResult != null)
            {
                return $"{result.GetType().FullName}；RunTime={result.RunTime}ms；OutputImage={PerformanceSpikeDiagnostics.GetOutputImageText(imageSourceResult.OutputImage)}";
            }

            return $"{result.GetType().FullName}；RunTime={result.RunTime}ms";
        }

        /// <summary>
        /// 将诊断消息统一压缩成单行，避免日志表格换行后难以检索。
        /// </summary>
        /// <param name="message">原始诊断消息。</param>
        /// <returns>单行诊断消息。</returns>
        private static string FormatDiagnosticMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "无";

            return message.Replace("\r", " ").Replace("\n", " ").Trim();
        }


        #region 实现被触发执行的逻辑

        /// <summary>
        /// 是否是在流程编辑界面中手动点击运行。该标记只用于硬触发和流程触发上下文，不再关闭图并行运行。
        /// </summary>
        public bool IsHandRun { get; set; }

        /// <summary>
        /// 是否为被动触发流程
        /// </summary>
        public bool IsPassiveTriggered { get; set; }

        /// <summary>
        /// 是否由相机回调帧触发运行。启用后从相机图像源节点向下游执行。
        /// </summary>
        public bool IsCameraCallbackTriggered { get; set; }


        /// <summary>
        /// 可选：添加触发方法
        /// </summary>
        /// <param name="passiveProcesses"></param>
        /// <param name="ct"></param>
        public async Task TriggerPassiveProcesses(IEnumerable<Process> passiveProcesses, CancellationToken ct)
        {
            List<Task<ProcessInvocationResult>> tasks = new List<Task<ProcessInvocationResult>>();
            foreach (var p in passiveProcesses)
            {
                if (p == null)
                    continue;
                // 可在这里加条件判断是否可触发等
                tasks.Add(p.RunInternalWithResultAsync(isCyclical: false, isTriggered: true, ct: ct));
            }

            ProcessInvocationResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (ProcessInvocationResult result in results)
                ThrowIfInvocationFailed(result, ct);
        }

        /// <summary>
        /// 在当前流程组内触发名称唯一的被动流程，并等待其本次执行完成。
        /// </summary>
        /// <param name="processName">目标流程名称。</param>
        /// <param name="cancellationToken">当前工件调用链取消令牌。</param>
        /// <returns>目标流程执行任务。</returns>
        public async Task TriggerProcess(string processName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(processName))
                throw new ArgumentException("目标流程名称不能为空。", nameof(processName));

            Process targetProcess = null;
            foreach(var pro in Solution.Instance.AllProcesses)
            {
                if (pro.Group != Group ||
                    !string.Equals(pro.ProcessName, processName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (targetProcess != null)
                    throw new InvalidOperationException($"流程组{Group}内存在重名流程【{processName}】，无法确定触发目标。");
                targetProcess = pro;
            }

            if (targetProcess == null)
                throw new InvalidOperationException($"流程组{Group}内不存在目标流程【{processName}】。");
            if (!targetProcess.IsPassiveTriggered)
                throw new InvalidOperationException($"目标流程【{processName}】没有配置为被动触发流程。");

            ProcessInvocationResult result = await targetProcess.RunInternalWithResultAsync(
                isCyclical: false,
                isTriggered: true,
                ct: cancellationToken).ConfigureAwait(false);
            ThrowIfInvocationFailed(result, cancellationToken);
        }

        /// <summary>
        /// 使用当前工件或方案停止令牌触发指定被动流程，保留旧节点插件调用签名。
        /// </summary>
        /// <param name="processName">目标流程名称。</param>
        /// <returns>目标流程执行任务。</returns>
        public Task TriggerProcess(string processName)
        {
            WorkpieceExecutionContext context = Solution.Instance.WorkpieceContextAccessor.Current;
            CancellationToken cancellationToken = context == null
                ? Solution.Instance.CancellationToken
                : context.CancellationToken;
            return TriggerProcess(processName, cancellationToken);
        }

        /// <summary>
        /// 执行一次流程并返回不受后续排队运行覆盖的调用结果。
        /// </summary>
        /// <param name="isCyclical">是否按循环运行语义记录本次流程。</param>
        /// <param name="isTriggered">是否由流程触发节点进入。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>本次调用独立结果。</returns>
        public async Task<ProcessInvocationResult> RunInternalWithResultAsync(
            bool isCyclical,
            bool isTriggered,
            CancellationToken ct)
        {
            CancellationToken effectiveCancellationToken = ct.CanBeCanceled
                ? ct
                : Solution.Instance.CancellationToken;
            try
            {
                effectiveCancellationToken.ThrowIfCancellationRequested();
                bool succeeded = await RunWithResultAsync(
                    isCyclical,
                    effectiveCancellationToken).ConfigureAwait(false);
                return new ProcessInvocationResult(ProcessName, succeeded, false, null);
            }
            catch (OperationCanceledException ex) when (effectiveCancellationToken.IsCancellationRequested)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"流程【{ProcessName}】执行取消！");
                return new ProcessInvocationResult(ProcessName, false, true, ex);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"流程【{ProcessName}】执行异常: {ex.Message}");
                return new ProcessInvocationResult(ProcessName, false, false, ex);
            }
        }

        /// <summary>
        /// 执行一次流程并保留旧节点插件使用的Task返回签名。
        /// </summary>
        /// <param name="isCyclical">是否按循环运行语义记录。</param>
        /// <param name="isTriggered">是否由流程触发节点进入。</param>
        /// <param name="ct">调用方取消令牌。</param>
        public async Task RunInternal(bool isCyclical, bool isTriggered, CancellationToken ct)
        {
            await RunInternalWithResultAsync(isCyclical, isTriggered, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 把被动流程的明确失败结果恢复为当前触发节点可观察的异常。
        /// </summary>
        /// <param name="result">目标流程本次调用结果。</param>
        /// <param name="cancellationToken">当前调用链取消令牌。</param>
        private static void ThrowIfInvocationFailed(
            ProcessInvocationResult result,
            CancellationToken cancellationToken)
        {
            if (result == null)
                throw new InvalidOperationException("被动流程没有返回执行结果。");
            if (result.Succeeded)
                return;
            if (result.Cancelled)
                throw new OperationCanceledException(
                    $"被动流程【{result.ProcessName}】执行取消。",
                    result.Exception,
                    cancellationToken);

            throw new InvalidOperationException(
                $"被动流程【{result.ProcessName}】执行失败。",
                result.Exception);
        }

        #endregion

        /// <summary>
        /// 运行指定ID的流程
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="isCyclical"></param>
        /// <returns></returns>
        public static async Task Run(int processId, bool isCyclical = false)
        {
            var process = Solution.Instance.AllProcesses.Find(p => p.ID == processId);
            if (process != null)
            {
                if (process.Nodes.Count == 0 || !process.Enable)
                    return;

                if (!Solution.Instance.TryBeginExternalRunSession())
                    return;

                ManagedProcessRunScope managedRunScope = null;
                ProcessRunPathScope pendingProcessPathScope = null;
                bool invocationSucceeded = false;
                try
                {
                pendingProcessPathScope = process.PushProcessRunPath();
                managedRunScope = await process.EnterManagedProcessRunAsync(
                    "按流程ID运行",
                    Solution.Instance.CancellationToken,
                    pendingProcessPathScope).ConfigureAwait(false);
                pendingProcessPathScope = null;
                managedRunScope.ActivateOwnedContext(Solution.Instance.WorkpieceContextAccessor);

                process.BeginProcessRun();

                process.RunTime = 0;
                UpdateRunStatus?.Invoke(process, new ProcessRunResult(true, false, process.ProcessName));

                bool processStartLogWritten = WriteProcessStartLog(process, isCyclical);
                process.IsRuning = true;
                process.Success = false;

                if (process.ShouldRunByConnections())
                {
                    try
                    {
                        bool stoppedEarly = await process.RunConnectedNodes();
                        if (stoppedEarly)
                        {
                            invocationSucceeded = true;
                            process.Success = true;
                            process.IsRuning = false;

                            WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, true, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Completed);
                            return;
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        process.Success = false;
                        process.IsRuning = false;
                        WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                        UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                        ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        process.Success = false;
                        process.IsRuning = false;
                        WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Exception, "失败");
                        UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                        ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Failed, ex);
                        throw ex;
                    }
                }
                else
                {
                    for (int i = 0; i < process._nodes.Count; i++)
                    {
                        NodeBase node = process._nodes[i];
                        try
                        {
                            if (!node.Active)
                                continue;

                            MarkNodeRunning(node);
                            NodeReturn result = await RunNodeWithCpuResourcePolicy(process, node);
                            FinalizeNodeRun(node);
                            process.RunTime += node.Result.RunTime;

                            if (IsNodeFailedStatus(node))
                                break;

                            // 检查还要不要继续运行
                            if (result.Flag == NodeRunFlag.StopRun || result.Flag == NodeRunFlag.StopBranch)
                            {
                                invocationSucceeded = true;
                                // 当前节点要求停止后续节点执行
                                process.Success = true; // 可以设置为成功或其他状态
                                process.IsRuning = false;

                                WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                                UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, true, process.ProcessName));
                                ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Completed);
                                return; // 提前退出整个流程
                            }

                            #region 实现IF^Else逻辑

                            // ✅ 让节点返回“下一个要执行的索引”
                            //    -1 表示顺序执行下一个（i+1）
                            //    其他表示跳转到指定索引
                            if (result.NextIndex == -1)
                            {
                                // 顺序执行下一个
                                continue;
                            }
                            else
                            {
                                // 跳转
                                i = result.NextIndex - 1; // for 循环会 +1，所以先 -1
                            }
                            #endregion
                        }
                        catch (OperationCanceledException ex)
                        {
                            process.Success = false;
                            process.IsRuning = false;
                            WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            process.Success = false;
                            process.IsRuning = false;
                            WriteProcessEndLog(process, ref processStartLogWritten, MsgLevel.Exception, "失败");
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Failed, ex);
                            throw ex;
                        }
                    }
                }

                // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
                bool processSucceeded = !process.HasFailedRuntimeNode();
                invocationSucceeded = processSucceeded;
                process.Success = processSucceeded;
                process.IsRuning = false;
                WriteFinalProcessEndLog(process, isCyclical, processSucceeded, ref processStartLogWritten);
                UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, processSucceeded, process.ProcessName));
                ProcessEvents.OnProcessEnded(process.ID, processSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
                }
                finally
                {
                    try
                    {
                        await process.ExitManagedProcessRunAsync(managedRunScope, invocationSucceeded).ConfigureAwait(false);
                    }
                    finally
                    {
                        pendingProcessPathScope?.Dispose();
                        Solution.Instance.EndExternalRunSession();
                    }
                }
            }
        }

        /// <summary>
        /// 流程开始运行
        /// </summary>
        internal async Task<bool> RunWithResultAsync(
            bool isCyclical,
            CancellationToken cancellationToken)
        {
            if (Nodes.Count == 0 || !Enable)
                return false;

            if (!Solution.Instance.TryBeginExternalRunSession())
                return false;

            CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled
                ? cancellationToken
                : Solution.Instance.CancellationToken;

            ManagedProcessRunScope managedRunScope = null;
            ProcessRunPathScope pendingProcessPathScope = null;
            bool invocationSucceeded = false;
            try
            {
            pendingProcessPathScope = PushProcessRunPath();
            managedRunScope = await EnterManagedProcessRunAsync(
                "流程运行",
                effectiveCancellationToken,
                pendingProcessPathScope).ConfigureAwait(false);
            pendingProcessPathScope = null;
            managedRunScope.ActivateOwnedContext(Solution.Instance.WorkpieceContextAccessor);

            BeginProcessRun();

            RunTime = 0;
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(true, false, ProcessName));

            bool processStartLogWritten = WriteProcessStartLog(this, isCyclical);
            IsRuning = true;
            Success = false;

            if (ShouldRunByConnections())
            {
                try
                {
                    bool stoppedEarly = await RunConnectedNodes();
                    if (stoppedEarly)
                    {
                        Success = true;
                        IsRuning = false;

                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        invocationSucceeded = true;
                        return true;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Success = false;
                    IsRuning = false;
                    WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Exception, "失败");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                    throw ex;
                }
            }
            else
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    NodeBase node = _nodes[i];
                    try
                    {
                        if (!node.Active)
                            continue;

                        MarkNodeRunning(node);
                        NodeReturn result = await RunNodeWithCpuResourcePolicy(this, node);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun || result.Flag == NodeRunFlag.StopBranch)
                        {
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                            ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                            invocationSucceeded = true;
                            return true; // 提前退出整个流程
                        }

                        #region 实现IF^Else逻辑

                        // ✅ 让节点返回“下一个要执行的索引”
                        //    -1 表示顺序执行下一个（i+1）
                        //    其他表示跳转到指定索引
                        if (result.NextIndex == -1)
                        {
                            // 顺序执行下一个
                            continue;
                        }
                        else
                        {
                            // 跳转
                            i = result.NextIndex - 1; // for 循环会 +1，所以先 -1
                        }
                        #endregion
                    }
                    catch (OperationCanceledException ex)
                    {
                        Success = false;
                        IsRuning = false;
                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        IsRuning = false;
                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Exception, "失败");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                        throw ex;
                    }
                }
            }

            // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
            bool runSucceeded = !HasFailedRuntimeNode();
            invocationSucceeded = runSucceeded;
            Success = runSucceeded;
            IsRuning = false;
            WriteFinalProcessEndLog(this, isCyclical, runSucceeded, ref processStartLogWritten);
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, runSucceeded, ProcessName));
            ProcessEvents.OnProcessEnded(ID, runSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
            return runSucceeded;
            }
            finally
            {
                try
                {
                    await ExitManagedProcessRunAsync(managedRunScope, invocationSucceeded).ConfigureAwait(false);
                }
                finally
                {
                    pendingProcessPathScope?.Dispose();
                    Solution.Instance.EndExternalRunSession();
                }
            }
        }

        /// <summary>
        /// 流程开始运行，保留原有公开Task签名。
        /// </summary>
        /// <param name="isCyclical">是否按循环运行语义记录。</param>
        public async Task Run(bool isCyclical)
        {
            await RunWithResultAsync(isCyclical, Solution.Instance.CancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// 流程开始运行
        /// </summary>
        public async Task Run(NodeBase nodeImage,bool isCyclical)
        {
            if (Nodes.Count == 0 || !Enable)
                return;

            if (!Solution.Instance.TryBeginExternalRunSession())
                return;

            ManagedProcessRunScope managedRunScope = null;
            ProcessRunPathScope pendingProcessPathScope = null;
            bool invocationSucceeded = false;
            try
            {
            pendingProcessPathScope = PushProcessRunPath();
            managedRunScope = await EnterManagedProcessRunAsync(
                "相机帧续跑",
                Solution.Instance.CancellationToken,
                pendingProcessPathScope).ConfigureAwait(false);
            pendingProcessPathScope = null;
            managedRunScope.ActivateOwnedContext(Solution.Instance.WorkpieceContextAccessor);

            BeginProcessRun();

            RunTime = 0;
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(true, false, ProcessName));

            bool processStartLogWritten = WriteProcessStartLog(this, isCyclical);
            IsRuning = true;
            Success = false;

            if (!TryPrepareSkippedNodeRunResult(nodeImage))
            {
                Success = false;
                IsRuning = false;
                WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Exception, "失败");
                UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed);
                return;
            }

            if (ShouldRunByConnections())
            {
                try
                {
                    CameraCallbackFastPathResult fastPathResult = await TryRunCameraCallbackFastPath(nodeImage);
                    bool stoppedEarly = fastPathResult.Handled
                        ? fastPathResult.StoppedEarly
                        : await RunConnectedNodes(nodeImage, null, nodeImage);
                    if (stoppedEarly)
                    {
                        invocationSucceeded = true;
                        Success = true;
                        IsRuning = false;

                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Success = false;
                    IsRuning = false;
                    WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Exception, "失败");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                    throw ex;
                }
            }
            else
            {
                int startIndex = 0;
                if (nodeImage != null)
                {
                    int imageIndex = _nodes.IndexOf(nodeImage);
                    if (imageIndex >= 0)
                        startIndex = imageIndex + 1;
                }

                for (int i = startIndex; i < _nodes.Count; i++)
                {
                    NodeBase node = _nodes[i];
                    try
                    {
                        if (node == nodeImage)
                            continue;
                        if (!node.Active)
                            continue;

                        MarkNodeRunning(node);
                        NodeReturn result = await RunNodeWithCpuResourcePolicy(this, node);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun || result.Flag == NodeRunFlag.StopBranch)
                        {
                            invocationSucceeded = true;
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Info, "提前完成");
                            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                            ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                            return; // 提前退出整个流程
                        }

                        #region 实现IF^Else逻辑

                        // ✅ 让节点返回“下一个要执行的索引”
                        //    -1 表示顺序执行下一个（i+1）
                        //    其他表示跳转到指定索引
                        // 跳转
                        if (result.NextIndex == -1)
                        {
                            // 顺序执行下一个
                            continue;
                        }
                        else
                        {
                            // 跳转
                            i = result.NextIndex - 1; // for 循环会 +1，所以先 -1
                        }
                        #endregion
                    }
                    catch (OperationCanceledException ex)
                    {
                        Success = false;
                        IsRuning = false;
                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Warn, "运行中断");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        IsRuning = false;
                        WriteProcessEndLog(this, ref processStartLogWritten, MsgLevel.Exception, "失败");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                        throw ex;
                    }
                }
            }

            // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
            bool runSucceeded = !HasFailedRuntimeNode();
            invocationSucceeded = runSucceeded;
            Success = runSucceeded;
            IsRuning = false;
            WriteFinalProcessEndLog(this, isCyclical, runSucceeded, ref processStartLogWritten);
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, runSucceeded, ProcessName));
            ProcessEvents.OnProcessEnded(ID, runSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
            }
            finally
            {
                try
                {
                    await ExitManagedProcessRunAsync(managedRunScope, invocationSucceeded).ConfigureAwait(false);
                }
                finally
                {
                    pendingProcessPathScope?.Dispose();
                    Solution.Instance.EndExternalRunSession();
                }
            }
        }


        /// <summary>
        /// 专用于需要订阅刷新前一个节点图像的节点
        /// 要在参数节点处停止运行
        /// </summary>
        public async Task RunForUpdateImages(NodeBase node2Stop)
        {
            if (Nodes.Count == 0 || !Enable)
                return;

            if (!Solution.Instance.TryBeginExternalRunSession())
                return;

            ManagedProcessRunScope managedRunScope = null;
            ProcessRunPathScope pendingProcessPathScope = null;
            bool invocationSucceeded = false;
            try
            {
            pendingProcessPathScope = PushProcessRunPath();
            managedRunScope = await EnterManagedProcessRunAsync(
                "参数图像刷新",
                Solution.Instance.CancellationToken,
                pendingProcessPathScope).ConfigureAwait(false);
            pendingProcessPathScope = null;
            managedRunScope.ActivateOwnedContext(Solution.Instance.WorkpieceContextAccessor);

            BeginProcessRun();
            RunTime = 0;
            // 更新运行状态
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(true, false, ProcessName));
            if(ShowLog)
                LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{ProcessName}】（开始）  -----------------------------------------------------", true);
            IsRuning = true;
            Success = false;

            if (ShouldRunByConnections())
            {
                try
                {
                    bool stoppedEarly = await RunConnectedNodes(null, node2Stop);
                    if (stoppedEarly)
                    {
                        invocationSucceeded = true;
                        Success = true;
                        IsRuning = false;

                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                        CompletePerformanceTrace("提前完成");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                    CompletePerformanceTrace("运行中断");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
                    CompletePerformanceTrace("失败");
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    throw ex;
                }
            }
            else
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    NodeBase node = _nodes[i];
                    try
                    {
                        if (node2Stop == node)
                        {
                            break;
                        }

                        MarkNodeRunning(node);
                        NodeReturn result = await RunNodeWithCpuResourcePolicy(this, node);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun || result.Flag == NodeRunFlag.StopBranch)
                        {
                            invocationSucceeded = true;
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            if (ShowLog)
                                LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                            CompletePerformanceTrace("提前完成");
                            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                            return; // 提前退出整个流程
                        }

                        #region 实现IF^Else逻辑

                        // ✅ 让节点返回“下一个要执行的索引”
                        //    -1 表示顺序执行下一个（i+1）
                        //    其他表示跳转到指定索引
                        if (result.NextIndex == -1)
                        {
                            // 顺序执行下一个
                            continue;
                        }
                        else
                        {
                            // 跳转
                            i = result.NextIndex - 1; // for 循环会 +1，所以先 -1
                        }
                        #endregion
                    }
                    catch (OperationCanceledException ex)
                    {
                        Success = false;
                        IsRuning = false;
                        RunTime += node.Result.RunTime;
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                        CompletePerformanceTrace("运行中断");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        IsRuning = false;
                        RunTime += node.Result.RunTime;
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
                        CompletePerformanceTrace("失败");
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        throw ex;
                    }
                }
            }

            bool updateSucceeded = !HasFailedRuntimeNode();
            invocationSucceeded = updateSucceeded;
            Success = updateSucceeded;
            IsRuning = false;
            if (ShowLog)
                LogHelper.AddLog(updateSucceeded ? MsgLevel.Info : MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（{(updateSucceeded ? "成功" : "失败")}）  ---------------------------------", true);
            CompletePerformanceTrace(updateSucceeded ? "成功" : "失败");
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, updateSucceeded, ProcessName));
            }
            finally
            {
                try
                {
                    await ExitManagedProcessRunAsync(managedRunScope, invocationSucceeded).ConfigureAwait(false);
                }
                finally
                {
                    pendingProcessPathScope?.Dispose();
                    Solution.Instance.EndExternalRunSession();
                }
            }
        }
    }

    /// <summary>
    /// 流程运行优先级别，同级并行运行
    /// </summary>
    public enum ProcessLvEnum 
    {
        /// <summary>
        /// 1级最先运行
        /// </summary>
        Lv1,
        Lv2,
        Lv3,
        Lv4,
        /// <summary>
        /// 5级最后运行
        /// </summary>
        Lv5,
    }

    /// <summary>方案流程组；保留既有编号，并扩展为40个独立调度组。</summary>
    public enum ProcessGroup
    {
        /// <summary>第1组。</summary>
        Group1 = 0,
        /// <summary>第2组。</summary>
        Group2 = 1,
        /// <summary>第3组。</summary>
        Group3 = 2,
        /// <summary>第4组。</summary>
        Group4 = 3,
        /// <summary>第5组。</summary>
        Group5 = 4,
        /// <summary>第6组。</summary>
        Group6 = 5,
        /// <summary>第7组。</summary>
        Group7 = 6,
        /// <summary>第8组。</summary>
        Group8 = 7,
        /// <summary>第9组。</summary>
        Group9 = 8,
        /// <summary>第10组。</summary>
        Group10 = 9,
        /// <summary>第11组。</summary>
        Group11 = 10,
        /// <summary>第12组。</summary>
        Group12 = 11,
        /// <summary>第13组。</summary>
        Group13 = 12,
        /// <summary>第14组。</summary>
        Group14 = 13,
        /// <summary>第15组。</summary>
        Group15 = 14,
        /// <summary>第16组。</summary>
        Group16 = 15,
        /// <summary>第17组。</summary>
        Group17 = 16,
        /// <summary>第18组。</summary>
        Group18 = 17,
        /// <summary>第19组。</summary>
        Group19 = 18,
        /// <summary>第20组。</summary>
        Group20 = 19,
        /// <summary>第21组。</summary>
        Group21 = 20,
        /// <summary>第22组。</summary>
        Group22 = 21,
        /// <summary>第23组。</summary>
        Group23 = 22,
        /// <summary>第24组。</summary>
        Group24 = 23,
        /// <summary>第25组。</summary>
        Group25 = 24,
        /// <summary>第26组。</summary>
        Group26 = 25,
        /// <summary>第27组。</summary>
        Group27 = 26,
        /// <summary>第28组。</summary>
        Group28 = 27,
        /// <summary>第29组。</summary>
        Group29 = 28,
        /// <summary>第30组。</summary>
        Group30 = 29,
        /// <summary>第31组。</summary>
        Group31 = 30,
        /// <summary>第32组。</summary>
        Group32 = 31,
        /// <summary>第33组。</summary>
        Group33 = 32,
        /// <summary>第34组。</summary>
        Group34 = 33,
        /// <summary>第35组。</summary>
        Group35 = 34,
        /// <summary>第36组。</summary>
        Group36 = 35,
        /// <summary>第37组。</summary>
        Group37 = 36,
        /// <summary>第38组。</summary>
        Group38 = 37,
        /// <summary>第39组。</summary>
        Group39 = 38,
        /// <summary>第40组。</summary>
        Group40 = 39,
    }

    /// <summary>
    /// 流程图中一条从源节点锚点到目标节点锚点的连接。
    /// </summary>
    public class ProcessConnection
    {
        public string ID { get; set; } = Guid.NewGuid().ToString("N");
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }
        public string FromAnchor { get; set; } = "Right";
        public string ToAnchor { get; set; } = "Left";
        public ProcessConnectionBranch Branch { get; set; } = ProcessConnectionBranch.Default;
    }

    public enum ProcessConnectionBranch
    {
        Default,
        True,
        False
    }
}
