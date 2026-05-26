using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Node;

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
        /// 流程是否正在运行
        /// </summary>
        public bool IsRuning { get; set; } = false;

        /// <summary>
        /// 当前流程运行批次，用于判断节点结果是否来自本次运行。
        /// </summary>
        public int CurrentRunId { get; private set; }

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
        private async Task<bool> RunConnectedNodes(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null)
        {
            if (nodeToSkip != null ||
                stopBeforeNode != null ||
                !CanUseParallelConnectedRuntime())
            {
                return await RunConnectedNodesSequential(nodeToSkip, stopBeforeNode);
            }

            return await RunConnectedNodesParallel(nodeToSkip, stopBeforeNode);
        }

        private async Task<bool> RunConnectedNodesSequential(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null)
        {
            HashSet<int> visitedNodeIds = new HashSet<int>();
            HashSet<int> failedNodeIds = new HashSet<int>();
            Exception firstNodeException = null;
            foreach (NodeBase startNode in GetConnectionStartNodes())
            {
                if (startNode == null || visitedNodeIds.Contains(startNode.ID))
                    continue;

                LinkedList<NodeBase> pendingNodes = new LinkedList<NodeBase>();
                pendingNodes.AddLast(startNode);

                while (pendingNodes.Count > 0)
                {
                    NodeBase node = pendingNodes.First.Value;
                    pendingNodes.RemoveFirst();

                    if (node == null || !visitedNodeIds.Add(node.ID))
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
                        result = await node.Run(Solution.Instance.CancellationToken, ShowLog);
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

        private async Task<bool> RunConnectedNodesParallel(NodeBase nodeToSkip = null, NodeBase stopBeforeNode = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long logicalRunTime = 0;
            Exception firstNodeException = null;
            try
            {
                HashSet<int> completedNodeIds = new HashSet<int>();
                Dictionary<int, long> completedNodeFinishTimes = new Dictionary<int, long>();
                foreach (NodeBase startNode in GetConnectionStartNodes())
                {
                    if (startNode == null || completedNodeIds.Contains(startNode.ID))
                        continue;

                    long componentStartTime = logicalRunTime;
                    HashSet<int> componentNodeIds = GetConnectedComponentNodeIds(startNode);
                    HashSet<int> inactiveNodeIds = new HashSet<int>();
                    HashSet<string> skippedEdges = new HashSet<string>();

                    while (true)
                    {
                        List<NodeBase> readyNodes = GetReadyComponentNodes(componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges, startNode);
                        if (readyNodes.Count == 0)
                            break;

                        if (stopBeforeNode != null && ContainsNode(readyNodes, stopBeforeNode))
                            return false;

                        List<NodeBase> batch = BuildReadyExecutionBatch(readyNodes);
                        List<ConnectedNodeRunResult> results = await RunReadyNodeBatch(batch, nodeToSkip);
                        foreach (ConnectedNodeRunResult runResult in results)
                        {
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
                                continue;
                            }

                            if (!runResult.SkippedExecution &&
                                runResult.Return != null &&
                                runResult.Return.Flag == NodeRunFlag.StopRun)
                            {
                                return true;
                            }

                            if (!runResult.SkippedExecution)
                                ApplyConditionalBranchSkips(node, runResult.Return, componentNodeIds, completedNodeIds, inactiveNodeIds, skippedEdges);
                        }
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

        private List<ConnectedNodeRunResult> GetOrderedResults(List<NodeBase> batch, Dictionary<int, ConnectedNodeRunResult> resultMap)
        {
            List<ConnectedNodeRunResult> orderedResults = new List<ConnectedNodeRunResult>();
            foreach (NodeBase node in batch)
            {
                if (node != null && resultMap.ContainsKey(node.ID))
                    orderedResults.Add(resultMap[node.ID]);
            }

            return orderedResults;
        }

        private async Task<List<ConnectedNodeRunResult>> RunReadyNodeBatch(List<NodeBase> batch, NodeBase nodeToSkip)
        {
            if (batch.Count <= 1)
            {
                List<ConnectedNodeRunResult> singleResult = new List<ConnectedNodeRunResult>();
                singleResult.Add(await RunConnectedNode(batch[0], nodeToSkip));
                return singleResult;
            }

            List<Task<ConnectedNodeRunResult>> tasks = new List<Task<ConnectedNodeRunResult>>();
            Dictionary<Task<ConnectedNodeRunResult>, NodeBase> taskNodes = new Dictionary<Task<ConnectedNodeRunResult>, NodeBase>();
            foreach (NodeBase node in batch)
            {
                NodeBase nodeToRun = node;
                Task<ConnectedNodeRunResult> task = Task.Run(async () => await RunConnectedNode(nodeToRun, nodeToSkip));
                tasks.Add(task);
                taskNodes[task] = nodeToRun;
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
            }

            Exception firstException = null;
            Dictionary<int, ConnectedNodeRunResult> resultMap = new Dictionary<int, ConnectedNodeRunResult>();
            foreach (Task<ConnectedNodeRunResult> task in tasks)
            {
                if (task.IsFaulted)
                {
                    if (firstException == null)
                        firstException = task.Exception == null ? new Exception("Parallel branch node failed.") : task.Exception.GetBaseException();

                    NodeBase faultedNode;
                    if (taskNodes.TryGetValue(task, out faultedNode) && faultedNode != null)
                    {
                        MarkNodeFailed(faultedNode);
                        resultMap[faultedNode.ID] = new ConnectedNodeRunResult
                        {
                            Node = faultedNode,
                            Exception = firstException,
                            RunTime = faultedNode.Result == null ? 0 : faultedNode.Result.RunTime
                        };
                    }

                    continue;
                }

                if (task.IsCanceled)
                {
                    if (firstException == null)
                        firstException = new OperationCanceledException();
                    continue;
                }

                ConnectedNodeRunResult result = task.Result;
                resultMap[result.Node.ID] = result;
            }

            if (firstException != null && resultMap.Count == 0)
                throw firstException;

            return GetOrderedResults(batch, resultMap);
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
                NodeReturn result = await node.Run(Solution.Instance.CancellationToken, ShowLog);
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

        private List<NodeBase> BuildReadyExecutionBatch(List<NodeBase> readyNodes)
        {
            List<NodeBase> batch = new List<NodeBase>();
            if (readyNodes.Count == 0)
                return batch;

            foreach (NodeBase node in readyNodes)
            {
                if (node == null)
                    continue;

                batch.Add(node);
            }

            return batch;
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
                    ConnectionMatchesBranch(connection, result.NextBranch))
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

        private sealed class ConnectedNodeRunResult
        {
            public NodeBase Node;
            public NodeReturn Return;
            public Exception Exception;
            public int RunTime;
            public bool SkippedExecution;
            public bool FailedStatus;
        }

        private List<NodeBase> GetConnectionStartNodes()
        {
            List<NodeBase> startNodes = new List<NodeBase>();
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
                    (!includeAllBranches && !ConnectionMatchesBranch(connection, branch)))
                    continue;

                NodeBase nextNode = GetNodeById(connection.ToNodeId);
                if (nextNode != null)
                    pendingNodes.AddLast(nextNode);
            }
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
                node.SetStatus(NodeStatus.Running, "*");
        }

        private static void MarkNodeFailed(NodeBase node)
        {
            if (node == null || node.RuntimeStatus == NodeStatus.Failed)
                return;

            string runTime = node.Result == null || node.Result.RunTime <= 0
                ? "0"
                : node.Result.RunTime.ToString();
            node.SetStatus(NodeStatus.Failed, runTime);
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

        private void BeginProcessRun()
        {
            CurrentRunId = CurrentRunId == int.MaxValue ? 1 : CurrentRunId + 1;
            foreach (var node in Nodes)
                node.BeginProcessRun(CurrentRunId);
        }


        #region 实现被触发执行的逻辑

        /// <summary>
        /// 是否是在流程编辑界面中手动点击运行
        /// </summary>
        public bool IsHandRun { get; set; }

        /// <summary>
        /// 是否为被动触发流程
        /// </summary>
        public bool IsPassiveTriggered { get; set; }


        /// <summary>
        /// 可选：添加触发方法
        /// </summary>
        /// <param name="passiveProcesses"></param>
        /// <param name="ct"></param>
        public void TriggerPassiveProcesses(IEnumerable<Process> passiveProcesses, CancellationToken ct)
        {
            foreach (var p in passiveProcesses)
            {
                if (p == null)
                    continue;
                // 可在这里加条件判断是否可触发等
                p.RunInternal(isCyclical: false, isTriggered: true, ct: ct);
            }
        }

        private Action<Process> _triggerAction; // 由外部注入

        public void SetTriggerAction(Action<Process> action)
        {
            _triggerAction = action;
        }

        // 在流程的某个节点需要触发其他流程时调用
        public async Task TriggerProcess(string processName)
        {
            foreach(var pro in Solution.Instance.AllProcesses)
            {
                if (pro.ProcessName == processName)
                {
                    // 触发某个流程
                    _triggerAction?.Invoke(pro);
                    break;
                }
            }
        }

        // 包装 Run 方法，支持标记“是否被触发”
        public async Task RunInternal(bool isCyclical, bool isTriggered, CancellationToken ct)
        {
            using (ct.Register(() => { /* 可选：清理 */ }))
            {
                try
                {

                    //Stopwatch stopwatch = new Stopwatch();
                    //stopwatch.Start();

                    //FrmLogger.AddLog($"开始: 耗时: {stopwatch.ElapsedMilliseconds}  当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}", MsgLevel.Debug);

                    await Run(isCyclical); // 实际执行

                    //stopwatch.Stop();
                    //FrmLogger.AddLog(
                    //     $"结束: 耗时: {stopwatch.ElapsedMilliseconds}ms  当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
                    //     MsgLevel.Debug
                    // );
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"被动流程执行取消！");
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"被动流程执行异常: {ex.Message}");
                }
            }
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

                process.BeginProcessRun();

                process.RunTime = 0;
                UpdateRunStatus?.Invoke(process, new ProcessRunResult(true, false, process.ProcessName));

                if (process.ShowLog)
                    LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{process.ProcessName}】（开始）  -----------------------------------------------------", true);
                process.IsRuning = true;
                process.Success = false;

                if (process.ShouldRunByConnections())
                {
                    try
                    {
                        bool stoppedEarly = await process.RunConnectedNodes();
                        if (stoppedEarly)
                        {
                            process.Success = true;
                            process.IsRuning = false;

                            if (process.ShowLog)
                                LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, true, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Completed);
                            return;
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        process.Success = false;
                        process.IsRuning = false;
                        if (process.ShowLog)
                            LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                        ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        process.Success = false;
                        process.IsRuning = false;
                        if (process.ShowLog)
                            LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（失败）  ---------------------------------", true);
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
                            NodeReturn result = await node.Run(Solution.Instance.CancellationToken, process.ShowLog);
                            FinalizeNodeRun(node);
                            process.RunTime += node.Result.RunTime;

                            if (IsNodeFailedStatus(node))
                                break;

                            // 检查还要不要继续运行
                            if (result.Flag == NodeRunFlag.StopRun)
                            {
                                // 当前节点要求停止后续节点执行
                                process.Success = true; // 可以设置为成功或其他状态
                                process.IsRuning = false;

                                if (process.ShowLog)
                                    LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
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
                            if (process.ShowLog)
                                LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                            throw ex;
                        }
                        catch (Exception ex)
                        {
                            process.Success = false;
                            process.IsRuning = false;
                            if (process.ShowLog)
                                LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（失败）  ---------------------------------", true);
                            UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                            ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Failed, ex);
                            throw ex;
                        }
                    }
                }

                // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
                bool processSucceeded = !process.HasFailedRuntimeNode();
                process.Success = processSucceeded;
                process.IsRuning = false;
                if (process.ShowLog)
                    LogHelper.AddLog(processSucceeded ? MsgLevel.Info : MsgLevel.Exception, $"---------------------------------  【{process.ProcessName}】（结束） 【耗时】（{process.RunTime}ms） 【状态】（{(processSucceeded ? "成功" : "失败")}）  ---------------------------------", true);
                UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, processSucceeded, process.ProcessName));
                ProcessEvents.OnProcessEnded(process.ID, processSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
            }
        }

        /// <summary>
        /// 流程开始运行
        /// </summary>
        public async Task Run(bool isCyclical)
        {
            if (Nodes.Count == 0 || !Enable)
                return;

            BeginProcessRun();

            RunTime = 0;
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(true, false, ProcessName));

            if (ShowLog)
                LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{ProcessName}】（开始）  -----------------------------------------------------", true);
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

                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
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
                        NodeReturn result = await node.Run(Solution.Instance.CancellationToken, ShowLog);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun)
                        {
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            if (ShowLog)
                                LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                            ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
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
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        IsRuning = false;
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                        throw ex;
                    }
                }
            }

            // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
            bool runSucceeded = !HasFailedRuntimeNode();
            Success = runSucceeded;
            IsRuning = false;
            if (ShowLog)
                LogHelper.AddLog(runSucceeded ? MsgLevel.Info : MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（{(runSucceeded ? "成功" : "失败")}）  ---------------------------------", true);
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, runSucceeded, ProcessName));
            ProcessEvents.OnProcessEnded(ID, runSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
        }


        /// <summary>
        /// 流程开始运行
        /// </summary>
        public async Task Run(NodeBase nodeImage,bool isCyclical)
        {
            if (Nodes.Count == 0 || !Enable)
                return;

            BeginProcessRun();

            RunTime = 0;
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(true, false, ProcessName));

            if (ShowLog)
                LogHelper.AddLog(MsgLevel.Info, $"-----------------------------------------------------  【{ProcessName}】（开始）  -----------------------------------------------------", true);
            IsRuning = true;
            Success = false;

            if (ShouldRunByConnections())
            {
                try
                {
                    bool stoppedEarly = await RunConnectedNodes(nodeImage);
                    if (stoppedEarly)
                    {
                        Success = true;
                        IsRuning = false;

                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, true, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        return;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
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
                        if (node==nodeImage)
                            continue;
                        if (!node.Active)
                            continue;

                        MarkNodeRunning(node);
                        NodeReturn result = await node.Run(Solution.Instance.CancellationToken, ShowLog);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun)
                        {
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            if (ShowLog)
                                LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
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
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Warn, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（运行中断）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Cancelled);
                        throw ex;
                    }
                    catch (Exception ex)
                    {
                        Success = false;
                        IsRuning = false;
                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        ProcessEvents.OnProcessEnded(ID, ProcessEndStatus.Failed, ex);
                        throw ex;
                    }
                }
            }

            // 所有可运行节点都执行完毕，若任一节点最终为失败状态，则流程按失败结束。
            bool runSucceeded = !HasFailedRuntimeNode();
            Success = runSucceeded;
            IsRuning = false;
            if (ShowLog)
                LogHelper.AddLog(runSucceeded ? MsgLevel.Info : MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（{(runSucceeded ? "成功" : "失败")}）  ---------------------------------", true);
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, runSucceeded, ProcessName));
            ProcessEvents.OnProcessEnded(ID, runSucceeded ? ProcessEndStatus.Completed : ProcessEndStatus.Failed);
        }


        /// <summary>
        /// 专用于需要订阅刷新前一个节点图像的节点
        /// 要在参数节点处停止运行
        /// </summary>
        public async Task RunForUpdateImages(NodeBase node2Stop)
        {
            // 重置运行取消令牌
            Solution.Instance.ResetTokenSource();

            // 节点数为0或流程不启用则不运行
            if (Nodes.Count == 0 || !Enable)
                return;

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
                        Success = true;
                        IsRuning = false;

                        if (ShowLog)
                            LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
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
                    UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                    throw ex;
                }
                catch (Exception ex)
                {
                    Success = false;
                    IsRuning = false;
                    if (ShowLog)
                        LogHelper.AddLog(MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（失败）  ---------------------------------", true);
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
                        NodeReturn result = await node.Run(Solution.Instance.CancellationToken, ShowLog);
                        FinalizeNodeRun(node);
                        RunTime += node.Result.RunTime;

                        if (IsNodeFailedStatus(node))
                            break;

                        // 检查还要不要继续运行
                        if (result.Flag == NodeRunFlag.StopRun)
                        {
                            // 当前节点要求停止后续节点执行
                            Success = true; // 可以设置为成功或其他状态
                            IsRuning = false;

                            if (ShowLog)
                                LogHelper.AddLog(MsgLevel.Info, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（提前完成）  ---------------------------------", true);
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
                        UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, false, ProcessName));
                        throw ex;
                    }
                }
            }

            bool updateSucceeded = !HasFailedRuntimeNode();
            Success = updateSucceeded;
            IsRuning = false;
            if (ShowLog)
                LogHelper.AddLog(updateSucceeded ? MsgLevel.Info : MsgLevel.Exception, $"---------------------------------  【{ProcessName}】（结束） 【耗时】（{RunTime}ms） 【状态】（{(updateSucceeded ? "成功" : "失败")}）  ---------------------------------", true);
            UpdateRunStatus?.Invoke(this, new ProcessRunResult(false, updateSucceeded, ProcessName));
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

    public enum ProcessGroup
    {
        Group1,
        Group2,
        Group3,
        Group4,
        Group5,
        Group6,
        Group7,
        //Group8,
        //Group9,
        //Group10,
        //Group11,
        //Group12,
        //Group13,
        //Group14,
        //Group15,
        //Group16
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
