using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.ResourceManagement
{
    /// <summary>
    /// 有序外发节点类型与框架节点类型之间的唯一映射目录。
    /// </summary>
    public static class OrderedSignalNodeCatalog
    {
        /// <summary>
        /// 尝试把框架节点类型映射为当前业务确认的有序外发类别。
        /// </summary>
        /// <param name="nodeType">框架节点类型。</param>
        /// <param name="signalKind">映射成功时返回有序外发类别。</param>
        /// <returns>当前所有外部发送节点均直接执行，返回false；保留入口兼容已有编译计划结构。</returns>
        public static bool TryGetSignalKind(NodeType nodeType, out OrderedSignalNodeKind signalKind)
        {
            signalKind = default(OrderedSignalNodeKind);
            return false;
        }
    }

    /// <summary>
    /// 流程编译诊断的严重程度。
    /// </summary>
    public enum FlowPlanDiagnosticSeverity
    {
        /// <summary>不阻止生产启动的提示。</summary>
        Warning = 1,

        /// <summary>必须修正后才能启动生产的错误。</summary>
        Error = 2
    }

    /// <summary>
    /// 流程编译阶段可稳定判断的诊断类别。
    /// </summary>
    public enum FlowPlanDiagnosticCode
    {
        /// <summary>同一层流程图出现重复节点ID。</summary>
        DuplicateNodeId = 1,

        /// <summary>连线引用了不存在的节点。</summary>
        MissingConnectionNode = 2,

        /// <summary>可能同时执行的外发节点之间没有明确先后路径。</summary>
        UnorderedParallelSignals = 3,

        /// <summary>组合模块嵌套深度超过安全上限。</summary>
        CompositeDepthExceeded = 4,

        /// <summary>组合模块参数缺失，无法检查内部流程。</summary>
        CompositeSnapshotMissing = 5,

        /// <summary>分支路径数量超过静态分析上限。</summary>
        BranchAnalysisLimitExceeded = 6
    }

    /// <summary>
    /// 一条不可变流程编译诊断。
    /// </summary>
    public sealed class FlowPlanDiagnostic
    {
        /// <summary>
        /// 创建流程编译诊断。
        /// </summary>
        /// <param name="severity">诊断严重程度。</param>
        /// <param name="code">稳定诊断类别。</param>
        /// <param name="nodePath">关联节点或流程图路径。</param>
        /// <param name="message">简体中文诊断说明。</param>
        public FlowPlanDiagnostic(
            FlowPlanDiagnosticSeverity severity,
            FlowPlanDiagnosticCode code,
            string nodePath,
            string message)
        {
            Severity = severity;
            Code = code;
            NodePath = nodePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>获取诊断严重程度。</summary>
        public FlowPlanDiagnosticSeverity Severity { get; }

        /// <summary>获取稳定诊断类别。</summary>
        public FlowPlanDiagnosticCode Code { get; }

        /// <summary>获取关联节点或流程图路径。</summary>
        public string NodePath { get; }

        /// <summary>获取简体中文诊断说明。</summary>
        public string Message { get; }
    }

    /// <summary>
    /// 编译后不可变的流程节点元数据。
    /// </summary>
    public sealed class CompiledFlowNode
    {
        /// <summary>
        /// 创建节点元数据快照。
        /// </summary>
        /// <param name="graphPath">节点所属流程图路径。</param>
        /// <param name="nodePath">包含节点ID和名称的完整路径。</param>
        /// <param name="nodeId">节点在所属流程图内的ID。</param>
        /// <param name="nodeName">节点编译时名称。</param>
        /// <param name="nodeType">节点编译时类型。</param>
        /// <param name="active">节点编译时是否启用。</param>
        public CompiledFlowNode(
            string graphPath,
            string nodePath,
            int nodeId,
            string nodeName,
            NodeType nodeType,
            bool active)
        {
            GraphPath = graphPath;
            NodePath = nodePath;
            NodeId = nodeId;
            NodeName = nodeName;
            NodeType = nodeType;
            Active = active;
        }

        /// <summary>获取节点所属流程图路径。</summary>
        public string GraphPath { get; }

        /// <summary>获取节点完整路径。</summary>
        public string NodePath { get; }

        /// <summary>获取节点在所属流程图内的ID。</summary>
        public int NodeId { get; }

        /// <summary>获取节点编译时名称。</summary>
        public string NodeName { get; }

        /// <summary>获取节点编译时类型。</summary>
        public NodeType NodeType { get; }

        /// <summary>获取节点编译时是否启用。</summary>
        public bool Active { get; }
    }

    /// <summary>
    /// 编译后不可变的流程连线元数据。
    /// </summary>
    public sealed class CompiledFlowConnection
    {
        /// <summary>
        /// 创建连线元数据快照。
        /// </summary>
        /// <param name="graphPath">连线所属流程图路径。</param>
        /// <param name="connectionId">连线ID。</param>
        /// <param name="fromNodeId">源节点ID。</param>
        /// <param name="toNodeId">目标节点ID。</param>
        /// <param name="branch">分支类别。</param>
        public CompiledFlowConnection(
            string graphPath,
            string connectionId,
            int fromNodeId,
            int toNodeId,
            ProcessConnectionBranch branch)
        {
            GraphPath = graphPath;
            ConnectionId = connectionId ?? string.Empty;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Branch = branch;
        }

        /// <summary>获取连线所属流程图路径。</summary>
        public string GraphPath { get; }

        /// <summary>获取连线ID。</summary>
        public string ConnectionId { get; }

        /// <summary>获取源节点ID。</summary>
        public int FromNodeId { get; }

        /// <summary>获取目标节点ID。</summary>
        public int ToNodeId { get; }

        /// <summary>获取条件分支类别。</summary>
        public ProcessConnectionBranch Branch { get; }
    }

    /// <summary>
    /// 编译期发现的一处可能外发节点。
    /// </summary>
    public sealed class CompiledPotentialSignal
    {
        /// <summary>
        /// 创建可能外发节点快照。
        /// </summary>
        /// <param name="graphPath">节点所属流程图路径。</param>
        /// <param name="nodePath">节点完整路径。</param>
        /// <param name="nodeId">节点在所属流程图内的ID。</param>
        /// <param name="nodeType">框架节点类型。</param>
        /// <param name="signalKind">有序外发类别。</param>
        public CompiledPotentialSignal(
            string graphPath,
            string nodePath,
            int nodeId,
            NodeType nodeType,
            OrderedSignalNodeKind signalKind)
        {
            GraphPath = graphPath;
            NodePath = nodePath;
            NodeId = nodeId;
            NodeType = nodeType;
            SignalKind = signalKind;
        }

        /// <summary>获取节点所属流程图路径。</summary>
        public string GraphPath { get; }

        /// <summary>获取节点完整路径。</summary>
        public string NodePath { get; }

        /// <summary>获取节点在所属流程图内的ID。</summary>
        public int NodeId { get; }

        /// <summary>获取框架节点类型。</summary>
        public NodeType NodeType { get; }

        /// <summary>获取有序外发类别。</summary>
        public OrderedSignalNodeKind SignalKind { get; }
    }

    /// <summary>
    /// 从可编辑流程复制得到的不可变运行元数据计划。
    /// </summary>
    public sealed class CompiledFlowExecutionPlan
    {
        /// <summary>
        /// 创建完整流程编译结果。
        /// </summary>
        /// <param name="flowRevision">流程快照版本。</param>
        /// <param name="processName">顶层流程名称。</param>
        /// <param name="orderDomain">生产顺序域。</param>
        /// <param name="nodes">全部顶层和组合模块节点。</param>
        /// <param name="connections">全部顶层和组合模块连线。</param>
        /// <param name="potentialSignals">全部可能执行的白名单外发节点。</param>
        /// <param name="diagnostics">编译诊断。</param>
        internal CompiledFlowExecutionPlan(
            long flowRevision,
            string processName,
            string orderDomain,
            IList<CompiledFlowNode> nodes,
            IList<CompiledFlowConnection> connections,
            IList<CompiledPotentialSignal> potentialSignals,
            IList<FlowPlanDiagnostic> diagnostics)
        {
            FlowRevision = flowRevision;
            ProcessName = processName;
            OrderDomain = orderDomain;
            Nodes = new ReadOnlyCollection<CompiledFlowNode>(nodes.ToArray());
            Connections = new ReadOnlyCollection<CompiledFlowConnection>(connections.ToArray());
            PotentialSignals = new ReadOnlyCollection<CompiledPotentialSignal>(potentialSignals.ToArray());
            Diagnostics = new ReadOnlyCollection<FlowPlanDiagnostic>(diagnostics.ToArray());
        }

        /// <summary>获取流程快照版本。</summary>
        public long FlowRevision { get; }

        /// <summary>获取顶层流程名称。</summary>
        public string ProcessName { get; }

        /// <summary>获取生产顺序域。</summary>
        public string OrderDomain { get; }

        /// <summary>获取不可变节点快照。</summary>
        public ReadOnlyCollection<CompiledFlowNode> Nodes { get; }

        /// <summary>获取不可变连线快照。</summary>
        public ReadOnlyCollection<CompiledFlowConnection> Connections { get; }

        /// <summary>获取不可变可能外发节点清单。</summary>
        public ReadOnlyCollection<CompiledPotentialSignal> PotentialSignals { get; }

        /// <summary>获取不可变编译诊断。</summary>
        public ReadOnlyCollection<FlowPlanDiagnostic> Diagnostics { get; }

        /// <summary>获取当前计划是否通过生产启动静态检查。</summary>
        public bool IsProductionReady =>
            !Diagnostics.Any(item => item.Severity == FlowPlanDiagnosticSeverity.Error);
    }

    /// <summary>
    /// 把可编辑流程递归编译为不可变运行元数据和有序外发清单。
    /// </summary>
    public sealed class FlowExecutionPlanCompiler
    {
        /// <summary>允许的最大组合模块嵌套层数。</summary>
        public const int MaximumCompositeDepth = 16;

        /// <summary>单个目标节点允许枚举的最大条件路径数。</summary>
        public const int MaximumBranchRouteCount = 1024;

        /// <summary>
        /// 从当前内存流程复制并编译运行元数据。
        /// </summary>
        /// <param name="process">待编译的可编辑流程。</param>
        /// <param name="flowRevision">大于0的流程快照版本。</param>
        /// <param name="orderDomain">生产顺序域。</param>
        /// <returns>与后续编辑隔离的不可变编译结果。</returns>
        /// <remarks>调用方必须持有统一流程编辑门或确认编辑已停止；本方法只负责复制值，不能替代跨节点与连线集合的原子编辑事务。</remarks>
        public CompiledFlowExecutionPlan Compile(Process process, long flowRevision, string orderDomain)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            FlowSourceGraph source = CaptureProcess(process);
            return CompileCore(source, flowRevision, orderDomain);
        }

        /// <summary>
        /// 从已保存流程配置复制并编译运行元数据。
        /// </summary>
        /// <param name="processConfig">待编译的流程配置。</param>
        /// <param name="flowRevision">大于0的流程快照版本。</param>
        /// <param name="orderDomain">生产顺序域。</param>
        /// <returns>与源配置后续修改隔离的不可变编译结果。</returns>
        public CompiledFlowExecutionPlan Compile(ProcessConfig processConfig, long flowRevision, string orderDomain)
        {
            if (processConfig == null)
                throw new ArgumentNullException(nameof(processConfig));

            FlowSourceGraph source = CaptureProcessConfig(processConfig);
            return CompileCore(source, flowRevision, orderDomain);
        }

        /// <summary>
        /// 校验公共参数并执行递归扫描。
        /// </summary>
        /// <param name="source">已经脱离编辑集合的顶层流程源。</param>
        /// <param name="flowRevision">流程快照版本。</param>
        /// <param name="orderDomain">生产顺序域。</param>
        /// <returns>不可变编译结果。</returns>
        private static CompiledFlowExecutionPlan CompileCore(
            FlowSourceGraph source,
            long flowRevision,
            string orderDomain)
        {
            if (flowRevision <= 0L)
                throw new ArgumentOutOfRangeException(nameof(flowRevision), "流程快照版本必须大于0。");
            if (string.IsNullOrWhiteSpace(orderDomain))
                throw new ArgumentException("生产顺序域不能为空。", nameof(orderDomain));

            List<CompiledFlowNode> nodes = new List<CompiledFlowNode>();
            List<CompiledFlowConnection> connections = new List<CompiledFlowConnection>();
            List<CompiledPotentialSignal> signals = new List<CompiledPotentialSignal>();
            List<FlowPlanDiagnostic> diagnostics = new List<FlowPlanDiagnostic>();
            ScanGraph(
                source,
                NormalizePathSegment(source.Name, "未命名流程"),
                0,
                nodes,
                connections,
                signals,
                diagnostics);

            return new CompiledFlowExecutionPlan(
                flowRevision,
                source.Name,
                orderDomain.Trim(),
                nodes,
                connections,
                signals,
                diagnostics);
        }

        /// <summary>
        /// 递归复制一层流程图，并检查该层外发节点是否存在并行乱序可能。
        /// </summary>
        /// <param name="graph">当前流程图源。</param>
        /// <param name="graphPath">当前流程图完整路径。</param>
        /// <param name="depth">当前组合模块嵌套深度。</param>
        /// <param name="compiledNodes">累计节点快照。</param>
        /// <param name="compiledConnections">累计连线快照。</param>
        /// <param name="compiledSignals">累计外发节点快照。</param>
        /// <param name="diagnostics">累计诊断。</param>
        /// <returns>本层每个节点位置可能产生的外发节点。</returns>
        private static List<SignalOccurrence> ScanGraph(
            FlowSourceGraph graph,
            string graphPath,
            int depth,
            List<CompiledFlowNode> compiledNodes,
            List<CompiledFlowConnection> compiledConnections,
            List<CompiledPotentialSignal> compiledSignals,
            List<FlowPlanDiagnostic> diagnostics)
        {
            List<SignalOccurrence> occurrences = new List<SignalOccurrence>();
            if (depth > MaximumCompositeDepth)
            {
                diagnostics.Add(new FlowPlanDiagnostic(
                    FlowPlanDiagnosticSeverity.Error,
                    FlowPlanDiagnosticCode.CompositeDepthExceeded,
                    graphPath,
                    $"组合模块嵌套超过{MaximumCompositeDepth}层，已停止继续扫描。"));
                return occurrences;
            }

            Dictionary<int, FlowSourceNode> nodesById = new Dictionary<int, FlowSourceNode>();
            foreach (FlowSourceNode node in graph.Nodes)
            {
                string nodePath = BuildNodePath(graphPath, node);
                compiledNodes.Add(new CompiledFlowNode(
                    graphPath,
                    nodePath,
                    node.Id,
                    node.Name,
                    node.NodeType,
                    node.Active));

                if (nodesById.ContainsKey(node.Id))
                {
                    diagnostics.Add(new FlowPlanDiagnostic(
                        FlowPlanDiagnosticSeverity.Error,
                        FlowPlanDiagnosticCode.DuplicateNodeId,
                        nodePath,
                        $"流程图内存在重复节点ID：{node.Id}。"));
                }
                else
                {
                    nodesById.Add(node.Id, node);
                }
            }

            List<FlowSourceConnection> validConnections = new List<FlowSourceConnection>();
            foreach (FlowSourceConnection connection in graph.Connections)
            {
                compiledConnections.Add(new CompiledFlowConnection(
                    graphPath,
                    connection.Id,
                    connection.FromNodeId,
                    connection.ToNodeId,
                    connection.Branch));

                if (!nodesById.ContainsKey(connection.FromNodeId) ||
                    !nodesById.ContainsKey(connection.ToNodeId))
                {
                    diagnostics.Add(new FlowPlanDiagnostic(
                        FlowPlanDiagnosticSeverity.Error,
                        FlowPlanDiagnosticCode.MissingConnectionNode,
                        graphPath,
                        $"连线{connection.Id}引用不存在的节点：{connection.FromNodeId} -> {connection.ToNodeId}。"));
                    continue;
                }

                validConnections.Add(connection);
            }

            if (ShouldUseLegacySequence(graph, validConnections))
            {
                for (int index = 0; index + 1 < graph.Nodes.Count; index++)
                {
                    FlowSourceConnection legacyConnection = new FlowSourceConnection
                    {
                        Id = $"legacy-{graph.Nodes[index].Id}-{graph.Nodes[index + 1].Id}",
                        FromNodeId = graph.Nodes[index].Id,
                        ToNodeId = graph.Nodes[index + 1].Id,
                        Branch = ProcessConnectionBranch.Default
                    };
                    validConnections.Add(legacyConnection);
                    compiledConnections.Add(new CompiledFlowConnection(
                        graphPath,
                        legacyConnection.Id,
                        legacyConnection.FromNodeId,
                        legacyConnection.ToNodeId,
                        legacyConnection.Branch));
                }
            }

            foreach (FlowSourceNode node in graph.Nodes)
            {
                if (!node.Active)
                    continue;

                string nodePath = BuildNodePath(graphPath, node);
                if (OrderedSignalNodeCatalog.TryGetSignalKind(node.NodeType, out OrderedSignalNodeKind signalKind))
                {
                    CompiledPotentialSignal signal = new CompiledPotentialSignal(
                        graphPath,
                        nodePath,
                        node.Id,
                        node.NodeType,
                        signalKind);
                    compiledSignals.Add(signal);
                    occurrences.Add(new SignalOccurrence(node.Id, signal));
                }

                if (node.NodeType != NodeType.CompositeModule)
                    continue;

                NodeParamCompositeModule composite = node.Parameter as NodeParamCompositeModule;
                if (composite == null)
                {
                    diagnostics.Add(new FlowPlanDiagnostic(
                        FlowPlanDiagnosticSeverity.Error,
                        FlowPlanDiagnosticCode.CompositeSnapshotMissing,
                        nodePath,
                        "组合模块缺少内部流程快照，无法检查其中的外发节点。"));
                    continue;
                }

                FlowSourceGraph nestedGraph = CaptureComposite(composite, node.Name);
                if (nestedGraph.Nodes.Count == 0)
                {
                    diagnostics.Add(new FlowPlanDiagnostic(
                        FlowPlanDiagnosticSeverity.Error,
                        FlowPlanDiagnosticCode.CompositeSnapshotMissing,
                        nodePath,
                        "组合模块未包含任何内部节点，无法作为可运行快照。"));
                    continue;
                }

                List<SignalOccurrence> nestedOccurrences = ScanGraph(
                    nestedGraph,
                    nodePath,
                    depth + 1,
                    compiledNodes,
                    compiledConnections,
                    compiledSignals,
                    diagnostics);
                foreach (SignalOccurrence nestedOccurrence in nestedOccurrences)
                {
                    occurrences.Add(new SignalOccurrence(node.Id, nestedOccurrence.Signal));
                }
            }

            ValidateSignalOrdering(
                graph,
                graphPath,
                validConnections,
                occurrences,
                diagnostics);
            return occurrences;
        }

        /// <summary>
        /// 检查可能在同一轮共同执行的外发节点是否由图路径明确排序。
        /// </summary>
        /// <param name="graph">当前流程图。</param>
        /// <param name="graphPath">当前流程图路径。</param>
        /// <param name="connections">有效连线。</param>
        /// <param name="occurrences">当前层节点位置可能产生的外发节点。</param>
        /// <param name="diagnostics">累计诊断。</param>
        private static void ValidateSignalOrdering(
            FlowSourceGraph graph,
            string graphPath,
            List<FlowSourceConnection> connections,
            List<SignalOccurrence> occurrences,
            List<FlowPlanDiagnostic> diagnostics)
        {
            Dictionary<int, List<RouteConstraint>> routeCache = new Dictionary<int, List<RouteConstraint>>();
            HashSet<string> reportedPairs = new HashSet<string>(StringComparer.Ordinal);
            for (int leftIndex = 0; leftIndex < occurrences.Count; leftIndex++)
            {
                SignalOccurrence left = occurrences[leftIndex];
                for (int rightIndex = leftIndex + 1; rightIndex < occurrences.Count; rightIndex++)
                {
                    SignalOccurrence right = occurrences[rightIndex];
                    if (left.AnchorNodeId == right.AnchorNodeId ||
                        IsReachable(left.AnchorNodeId, right.AnchorNodeId, connections) ||
                        IsReachable(right.AnchorNodeId, left.AnchorNodeId, connections))
                    {
                        continue;
                    }

                    List<RouteConstraint> leftRoutes = GetRoutes(
                        graph,
                        graphPath,
                        left.AnchorNodeId,
                        connections,
                        routeCache,
                        diagnostics);
                    List<RouteConstraint> rightRoutes = GetRoutes(
                        graph,
                        graphPath,
                        right.AnchorNodeId,
                        connections,
                        routeCache,
                        diagnostics);
                    if (!HaveCompatibleRoutes(leftRoutes, rightRoutes))
                        continue;

                    string pairKey = string.CompareOrdinal(left.Signal.NodePath, right.Signal.NodePath) <= 0
                        ? left.Signal.NodePath + "|" + right.Signal.NodePath
                        : right.Signal.NodePath + "|" + left.Signal.NodePath;
                    if (!reportedPairs.Add(pairKey))
                        continue;

                    diagnostics.Add(new FlowPlanDiagnostic(
                        FlowPlanDiagnosticSeverity.Error,
                        FlowPlanDiagnosticCode.UnorderedParallelSignals,
                        graphPath,
                        $"外发节点“{left.Signal.NodePath}”与“{right.Signal.NodePath}”可能在同一轮并行执行，但流程图没有明确先后连线。请增加依赖关系后再启动生产。"));
                }
            }
        }

        /// <summary>
        /// 获取目标节点从所有图起点到达时可能携带的条件分支约束。
        /// </summary>
        /// <param name="graph">当前流程图。</param>
        /// <param name="graphPath">当前流程图路径。</param>
        /// <param name="targetNodeId">目标节点ID。</param>
        /// <param name="connections">有效连线。</param>
        /// <param name="routeCache">同层路径分析缓存。</param>
        /// <param name="diagnostics">累计诊断。</param>
        /// <returns>到达目标节点的条件约束集合。</returns>
        private static List<RouteConstraint> GetRoutes(
            FlowSourceGraph graph,
            string graphPath,
            int targetNodeId,
            List<FlowSourceConnection> connections,
            Dictionary<int, List<RouteConstraint>> routeCache,
            List<FlowPlanDiagnostic> diagnostics)
        {
            if (routeCache.TryGetValue(targetNodeId, out List<RouteConstraint> cached))
                return cached;

            List<int> startNodeIds = GetGraphStartNodeIds(graph, connections);
            HashSet<int> activeNodeIds = new HashSet<int>(
                graph.Nodes.Where(item => item.Active).Select(item => item.Id));

            Dictionary<int, List<FlowSourceConnection>> outgoing = connections
                .GroupBy(item => item.FromNodeId)
                .ToDictionary(group => group.Key, group => group.ToList());
            List<RouteConstraint> routes = new List<RouteConstraint>();
            foreach (int startNodeId in startNodeIds)
            {
                CollectRoutes(
                    startNodeId,
                    targetNodeId,
                    outgoing,
                    activeNodeIds,
                    new HashSet<int>(),
                    new Dictionary<int, bool>(),
                    routes);
                if (routes.Count >= MaximumBranchRouteCount)
                    break;
            }

            if (routes.Count >= MaximumBranchRouteCount)
            {
                diagnostics.Add(new FlowPlanDiagnostic(
                    FlowPlanDiagnosticSeverity.Error,
                    FlowPlanDiagnosticCode.BranchAnalysisLimitExceeded,
                    graphPath,
                    $"节点{targetNodeId}的条件路径超过{MaximumBranchRouteCount}条，无法可靠证明外发顺序。"));
            }

            if (routes.Count == 0)
                routes.Add(new RouteConstraint(new Dictionary<int, bool>()));

            routeCache[targetNodeId] = routes;
            return routes;
        }

        /// <summary>
        /// 深度优先收集一条目标路径上的True/False约束，并阻止环路无限递归。
        /// </summary>
        /// <param name="currentNodeId">当前节点ID。</param>
        /// <param name="targetNodeId">目标节点ID。</param>
        /// <param name="outgoing">按源节点分组的连线。</param>
        /// <param name="pathNodeIds">当前递归路径已经访问的节点。</param>
        /// <param name="branchValues">当前路径携带的条件取值。</param>
        /// <param name="routes">累计目标路径。</param>
        private static void CollectRoutes(
            int currentNodeId,
            int targetNodeId,
            Dictionary<int, List<FlowSourceConnection>> outgoing,
            HashSet<int> activeNodeIds,
            HashSet<int> pathNodeIds,
            Dictionary<int, bool> branchValues,
            List<RouteConstraint> routes)
        {
            if (routes.Count >= MaximumBranchRouteCount || !pathNodeIds.Add(currentNodeId))
                return;

            if (currentNodeId == targetNodeId)
            {
                routes.Add(new RouteConstraint(new Dictionary<int, bool>(branchValues)));
                pathNodeIds.Remove(currentNodeId);
                return;
            }

            if (outgoing.TryGetValue(currentNodeId, out List<FlowSourceConnection> nextConnections))
            {
                foreach (FlowSourceConnection connection in nextConnections)
                {
                    Dictionary<int, bool> nextBranchValues = new Dictionary<int, bool>(branchValues);
                    // 运行器跳过禁用节点时会放行全部分支，不能继续把True/False当成互斥条件。
                    if (activeNodeIds.Contains(currentNodeId))
                    {
                        if (connection.Branch == ProcessConnectionBranch.True)
                            nextBranchValues[currentNodeId] = true;
                        else if (connection.Branch == ProcessConnectionBranch.False)
                            nextBranchValues[currentNodeId] = false;
                    }

                    CollectRoutes(
                        connection.ToNodeId,
                        targetNodeId,
                        outgoing,
                        activeNodeIds,
                        pathNodeIds,
                        nextBranchValues,
                        routes);
                    if (routes.Count >= MaximumBranchRouteCount)
                        break;
                }
            }

            pathNodeIds.Remove(currentNodeId);
        }

        /// <summary>
        /// 按运行器规则确定当前图的全部起点，包含显式起点之外的独立连通分量。
        /// </summary>
        /// <param name="graph">当前流程图。</param>
        /// <param name="connections">当前有效连线。</param>
        /// <returns>按节点列表顺序排列且不重复的起点ID。</returns>
        private static List<int> GetGraphStartNodeIds(
            FlowSourceGraph graph,
            List<FlowSourceConnection> connections)
        {
            List<int> startNodeIds = new List<int>();
            HashSet<int> coveredNodeIds = new HashSet<int>();
            HashSet<int> incomingNodeIds = new HashSet<int>(connections.Select(item => item.ToNodeId));
            HashSet<int> existingNodeIds = new HashSet<int>(graph.Nodes.Select(item => item.Id));

            foreach (FlowSourceNode node in graph.Nodes)
            {
                if (!node.IsStartNode || startNodeIds.Contains(node.Id))
                    continue;

                startNodeIds.Add(node.Id);
                MarkConnectedComponent(node.Id, connections, existingNodeIds, coveredNodeIds);
            }

            foreach (FlowSourceNode node in graph.Nodes)
            {
                if (coveredNodeIds.Contains(node.Id) ||
                    incomingNodeIds.Contains(node.Id) ||
                    startNodeIds.Contains(node.Id))
                {
                    continue;
                }

                startNodeIds.Add(node.Id);
                MarkConnectedComponent(node.Id, connections, existingNodeIds, coveredNodeIds);
            }

            if (startNodeIds.Count == 0 && graph.Nodes.Count > 0)
                startNodeIds.Add(graph.Nodes[0].Id);

            return startNodeIds;
        }

        /// <summary>
        /// 标记一个起点所属的无向连通分量，与运行器的显式起点覆盖规则保持一致。
        /// </summary>
        /// <param name="startNodeId">连通分量起点ID。</param>
        /// <param name="connections">当前有效连线。</param>
        /// <param name="existingNodeIds">当前图实际存在的节点ID。</param>
        /// <param name="componentNodeIds">累计已覆盖节点ID。</param>
        private static void MarkConnectedComponent(
            int startNodeId,
            List<FlowSourceConnection> connections,
            HashSet<int> existingNodeIds,
            HashSet<int> componentNodeIds)
        {
            if (!componentNodeIds.Add(startNodeId))
                return;

            Queue<int> pendingNodeIds = new Queue<int>();
            pendingNodeIds.Enqueue(startNodeId);
            while (pendingNodeIds.Count > 0)
            {
                int nodeId = pendingNodeIds.Dequeue();
                foreach (FlowSourceConnection connection in connections)
                {
                    int connectedNodeId = 0;
                    if (connection.FromNodeId == nodeId)
                        connectedNodeId = connection.ToNodeId;
                    else if (connection.ToNodeId == nodeId)
                        connectedNodeId = connection.FromNodeId;

                    if (existingNodeIds.Contains(connectedNodeId) &&
                        componentNodeIds.Add(connectedNodeId))
                    {
                        pendingNodeIds.Enqueue(connectedNodeId);
                    }
                }
            }
        }

        /// <summary>
        /// 判断当前图是否会像运行器一样退回节点列表顺序执行。
        /// </summary>
        /// <param name="graph">当前流程图。</param>
        /// <param name="connections">当前有效连线。</param>
        /// <returns>没有有效连线且没有显式起点时返回true。</returns>
        private static bool ShouldUseLegacySequence(
            FlowSourceGraph graph,
            List<FlowSourceConnection> connections)
        {
            return connections.Count == 0 && !graph.Nodes.Any(item => item.IsStartNode);
        }

        /// <summary>
        /// 判断两组节点路径中是否至少存在一对可以在同一轮成立的条件组合。
        /// </summary>
        /// <param name="leftRoutes">左侧节点路径约束。</param>
        /// <param name="rightRoutes">右侧节点路径约束。</param>
        /// <returns>存在条件不冲突的路径组合返回true。</returns>
        private static bool HaveCompatibleRoutes(
            List<RouteConstraint> leftRoutes,
            List<RouteConstraint> rightRoutes)
        {
            foreach (RouteConstraint left in leftRoutes)
            {
                foreach (RouteConstraint right in rightRoutes)
                {
                    if (left.IsCompatibleWith(right))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断当前流程图是否存在从源节点到目标节点的有向路径。
        /// </summary>
        /// <param name="fromNodeId">源节点ID。</param>
        /// <param name="toNodeId">目标节点ID。</param>
        /// <param name="connections">有效连线。</param>
        /// <returns>存在有向路径返回true。</returns>
        private static bool IsReachable(
            int fromNodeId,
            int toNodeId,
            List<FlowSourceConnection> connections)
        {
            Queue<int> pending = new Queue<int>();
            HashSet<int> visited = new HashSet<int>();
            pending.Enqueue(fromNodeId);
            visited.Add(fromNodeId);
            while (pending.Count > 0)
            {
                int currentNodeId = pending.Dequeue();
                foreach (FlowSourceConnection connection in connections)
                {
                    if (connection.FromNodeId != currentNodeId)
                        continue;
                    if (connection.ToNodeId == toNodeId)
                        return true;
                    if (visited.Add(connection.ToNodeId))
                        pending.Enqueue(connection.ToNodeId);
                }
            }

            return false;
        }

        /// <summary>
        /// 从当前内存流程复制一份脱离编辑集合的扫描源。
        /// </summary>
        /// <param name="process">当前内存流程。</param>
        /// <returns>仅供本次编译使用的图源。</returns>
        private static FlowSourceGraph CaptureProcess(Process process)
        {
            NodeBase[] nodes = CaptureList(process.Nodes, "流程节点");
            ProcessConnection[] connections = CaptureList(process.Connections, "流程连线");
            return new FlowSourceGraph
            {
                Name = NormalizePathSegment(process.ProcessName, $"流程{process.ID}"),
                HasCanvasGraph = process.HasCanvasGraph,
                Nodes = nodes.Select(item => new FlowSourceNode
                {
                    Id = item.ID,
                    Name = NormalizePathSegment(item.NodeName, $"节点{item.ID}"),
                    NodeType = item.NodeType,
                    Active = item.Active,
                    IsStartNode = item.IsStartNode,
                    Parameter = item.ParamForm == null ? null : item.ParamForm.Params
                }).ToList(),
                Connections = connections.Select(item => new FlowSourceConnection
                {
                    Id = item.ID,
                    FromNodeId = item.FromNodeId,
                    ToNodeId = item.ToNodeId,
                    Branch = item.Branch
                }).ToList()
            };
        }

        /// <summary>
        /// 从保存配置复制一份脱离源集合的扫描图。
        /// </summary>
        /// <param name="config">保存流程配置。</param>
        /// <returns>仅供本次编译使用的图源。</returns>
        private static FlowSourceGraph CaptureProcessConfig(ProcessConfig config)
        {
            List<NodeConfig> sourceNodes = config.NodeInfos == null
                ? new List<NodeConfig>()
                : config.NodeInfos.ToList();
            List<ProcessConnectionConfig> sourceConnections = config.ConnectionInfos == null
                ? new List<ProcessConnectionConfig>()
                : config.ConnectionInfos.ToList();
            return new FlowSourceGraph
            {
                Name = NormalizePathSegment(config.ProcessName, $"流程{config.ID}"),
                HasCanvasGraph = config.HasCanvasGraph,
                Nodes = sourceNodes.Where(item => item != null).Select(item => new FlowSourceNode
                {
                    Id = item.ID,
                    Name = NormalizePathSegment(item.NodeName, $"节点{item.ID}"),
                    NodeType = item.NodeType,
                    Active = item.Active,
                    IsStartNode = item.IsStartNode,
                    Parameter = item.NodeParam
                }).ToList(),
                Connections = sourceConnections.Where(item => item != null).Select(item => new FlowSourceConnection
                {
                    Id = item.ID,
                    FromNodeId = item.FromNodeId,
                    ToNodeId = item.ToNodeId,
                    Branch = item.Branch
                }).ToList()
            };
        }

        /// <summary>
        /// 从组合模块参数复制内部流程图源。
        /// </summary>
        /// <param name="composite">组合模块快照参数。</param>
        /// <param name="fallbackName">内部流程名称缺失时使用的名称。</param>
        /// <returns>仅供本次递归扫描使用的图源。</returns>
        private static FlowSourceGraph CaptureComposite(
            NodeParamCompositeModule composite,
            string fallbackName)
        {
            List<NodeConfig> sourceNodes = composite.NodeInfos == null
                ? new List<NodeConfig>()
                : composite.NodeInfos.ToList();
            List<ProcessConnectionConfig> sourceConnections = composite.ConnectionInfos == null
                ? new List<ProcessConnectionConfig>()
                : composite.ConnectionInfos.ToList();
            return new FlowSourceGraph
            {
                Name = NormalizePathSegment(composite.ModuleName, fallbackName),
                HasCanvasGraph = composite.HasCanvasGraph,
                Nodes = sourceNodes.Where(item => item != null).Select(item => new FlowSourceNode
                {
                    Id = item.ID,
                    Name = NormalizePathSegment(item.NodeName, $"节点{item.ID}"),
                    NodeType = item.NodeType,
                    Active = item.Active,
                    IsStartNode = item.IsStartNode,
                    Parameter = item.NodeParam
                }).ToList(),
                Connections = sourceConnections.Where(item => item != null).Select(item => new FlowSourceConnection
                {
                    Id = item.ID,
                    FromNodeId = item.FromNodeId,
                    ToNodeId = item.ToNodeId,
                    Branch = item.Branch
                }).ToList()
            };
        }

        /// <summary>
        /// 在调用方已隔离编辑的前提下重试单个集合复制，避免偶发枚举异常。
        /// </summary>
        /// <typeparam name="T">集合元素类型。</typeparam>
        /// <param name="source">可编辑源集合。</param>
        /// <param name="displayName">异常中使用的集合名称。</param>
        /// <returns>单个集合的稳定数组副本；跨集合一致性仍由调用方编辑门保证。</returns>
        private static T[] CaptureList<T>(IList<T> source, string displayName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    int countBefore = source.Count;
                    T[] values = source.ToArray();
                    if (countBefore == source.Count && values.Length == countBefore)
                        return values;
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
            }

            throw new InvalidOperationException($"{displayName}正在被修改，无法生成一致的流程快照，请停止编辑后重试。");
        }

        /// <summary>
        /// 生成包含所属图、节点ID和节点名称的稳定路径。
        /// </summary>
        /// <param name="graphPath">所属流程图路径。</param>
        /// <param name="node">节点源。</param>
        /// <returns>节点完整路径。</returns>
        private static string BuildNodePath(string graphPath, FlowSourceNode node)
        {
            return $"{graphPath}/{node.Id}.{NormalizePathSegment(node.Name, $"节点{node.Id}")}";
        }

        /// <summary>
        /// 规范路径片段并在空值时使用可读默认值。
        /// </summary>
        /// <param name="value">原始名称。</param>
        /// <param name="fallback">空名称时的默认值。</param>
        /// <returns>不包含路径分隔符的稳定名称。</returns>
        private static string NormalizePathSegment(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return normalized.Replace('/', '／').Replace('\\', '＼');
        }

        /// <summary>
        /// 单层流程图的脱离编辑集合扫描源。
        /// </summary>
        private sealed class FlowSourceGraph
        {
            /// <summary>流程图名称。</summary>
            public string Name;

            /// <summary>是否按画布连接图解释。</summary>
            public bool HasCanvasGraph;

            /// <summary>节点值副本。</summary>
            public List<FlowSourceNode> Nodes;

            /// <summary>连线值副本。</summary>
            public List<FlowSourceConnection> Connections;
        }

        /// <summary>
        /// 编译期间使用的节点值副本。
        /// </summary>
        private sealed class FlowSourceNode
        {
            /// <summary>节点ID。</summary>
            public int Id;

            /// <summary>节点名称。</summary>
            public string Name;

            /// <summary>节点类型。</summary>
            public NodeType NodeType;

            /// <summary>节点是否启用。</summary>
            public bool Active;

            /// <summary>节点是否为画布起点。</summary>
            public bool IsStartNode;

            /// <summary>仅用于递归读取组合模块快照的参数引用。</summary>
            public INodeParam Parameter;
        }

        /// <summary>
        /// 编译期间使用的连线值副本。
        /// </summary>
        private sealed class FlowSourceConnection
        {
            /// <summary>连线ID。</summary>
            public string Id;

            /// <summary>源节点ID。</summary>
            public int FromNodeId;

            /// <summary>目标节点ID。</summary>
            public int ToNodeId;

            /// <summary>条件分支类别。</summary>
            public ProcessConnectionBranch Branch;
        }

        /// <summary>
        /// 当前层一个节点位置可能产生的外发节点。
        /// </summary>
        private sealed class SignalOccurrence
        {
            /// <summary>
            /// 创建外发节点位置关系。
            /// </summary>
            /// <param name="anchorNodeId">当前层代表该动作位置的节点ID。</param>
            /// <param name="signal">实际外发节点快照。</param>
            public SignalOccurrence(int anchorNodeId, CompiledPotentialSignal signal)
            {
                AnchorNodeId = anchorNodeId;
                Signal = signal;
            }

            /// <summary>获取当前层代表该动作位置的节点ID。</summary>
            public int AnchorNodeId { get; }

            /// <summary>获取实际外发节点快照。</summary>
            public CompiledPotentialSignal Signal { get; }
        }

        /// <summary>
        /// 一条到达目标节点的条件分支约束。
        /// </summary>
        private sealed class RouteConstraint
        {
            /// <summary>条件节点ID及其要求的True或False取值。</summary>
            private readonly Dictionary<int, bool> _branchValues;

            /// <summary>
            /// 创建不可变使用的分支约束副本。
            /// </summary>
            /// <param name="branchValues">条件节点取值。</param>
            public RouteConstraint(Dictionary<int, bool> branchValues)
            {
                _branchValues = branchValues;
            }

            /// <summary>
            /// 判断两条路径的条件约束是否可以同时成立。
            /// </summary>
            /// <param name="other">另一条路径约束。</param>
            /// <returns>不存在同一条件节点取值冲突时返回true。</returns>
            public bool IsCompatibleWith(RouteConstraint other)
            {
                foreach (KeyValuePair<int, bool> branchValue in _branchValues)
                {
                    if (other._branchValues.TryGetValue(branchValue.Key, out bool otherValue) &&
                        otherValue != branchValue.Value)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
