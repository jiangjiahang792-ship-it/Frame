using Logger;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using TDJS_Vision.Node;

namespace TDJS_Vision.Diagnostics
{
    /// <summary>
    /// 记录一次可编辑流程运行的完整耗时关联信息，仅在系统Debug日志开启时创建。
    /// </summary>
    internal sealed class ProcessPerformanceTrace
    {
        /// <summary>所有流程共享的诊断序号，用于避免流程重载后RunId重复。</summary>
        private static long _globalTraceSequence;

        /// <summary>保存各节点进入运行态时的流程经过时间和线程信息。</summary>
        private readonly ConcurrentDictionary<int, NodeTraceStartInfo> _nodeStartInfo =
            new ConcurrentDictionary<int, NodeTraceStartInfo>();

        /// <summary>本轮流程使用的高精度计时器。</summary>
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        /// <summary>防止同一流程的多个结束分支重复输出结束诊断。</summary>
        private int _completed;

        /// <summary>流程编号。</summary>
        private readonly int _processId;

        /// <summary>流程名称。</summary>
        private readonly string _processName;

        /// <summary>流程内部运行批次。</summary>
        private readonly int _runId;

        /// <summary>
        /// 节点进入运行态时的只读诊断现场。
        /// </summary>
        private struct NodeTraceStartInfo
        {
            /// <summary>节点开始时相对流程开始的毫秒数。</summary>
            public long FlowElapsedMilliseconds { get; }

            /// <summary>节点开始时的托管线程编号。</summary>
            public int ManagedThreadId { get; }

            /// <summary>
            /// 创建节点开始现场。
            /// </summary>
            /// <param name="flowElapsedMilliseconds">相对流程开始的毫秒数。</param>
            /// <param name="managedThreadId">当前托管线程编号。</param>
            public NodeTraceStartInfo(long flowElapsedMilliseconds, int managedThreadId)
            {
                FlowElapsedMilliseconds = flowElapsedMilliseconds;
                ManagedThreadId = managedThreadId;
            }
        }

        /// <summary>本轮跨线程关联编号。</summary>
        public string TraceId { get; }

        /// <summary>
        /// 创建一次流程性能追踪。
        /// </summary>
        private ProcessPerformanceTrace(int processId, string processName, int runId)
        {
            _processId = processId;
            _processName = string.IsNullOrWhiteSpace(processName) ? "未命名流程" : processName;
            _runId = runId;
            long sequence = Interlocked.Increment(ref _globalTraceSequence);
            TraceId = $"P{processId}-R{runId}-T{sequence}";
        }

        /// <summary>
        /// Debug开启时创建流程追踪，关闭时直接返回null，避免生产运行分配诊断对象。
        /// </summary>
        /// <param name="processId">流程编号。</param>
        /// <param name="processName">流程名称。</param>
        /// <param name="runId">流程运行批次。</param>
        /// <param name="nodeCount">流程节点数量。</param>
        /// <param name="runByConnections">是否按照画布连接关系运行。</param>
        /// <returns>Debug开启时返回追踪对象。</returns>
        public static ProcessPerformanceTrace CreateIfEnabled(int processId, string processName, int runId, int nodeCount, bool runByConnections)
        {
            if (!PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug))
                return null;

            ProcessPerformanceTrace trace = new ProcessPerformanceTrace(processId, processName, runId);
            trace.LogStage(
                "流程开始",
                () => $"节点数量={nodeCount}；运行模式={(runByConnections ? "画布连接" : "顺序兼容")}；{PerformanceSpikeDiagnostics.GetRuntimeText()}");
            return trace;
        }

        /// <summary>
        /// 记录节点真正进入执行状态的时间。
        /// </summary>
        /// <param name="node">当前节点。</param>
        public void NodeStarted(NodeBase node)
        {
            if (node == null || Volatile.Read(ref _completed) != 0)
                return;

            long flowElapsed = _stopwatch.ElapsedMilliseconds;
            _nodeStartInfo[node.ID] = new NodeTraceStartInfo(flowElapsed, Thread.CurrentThread.ManagedThreadId);
            bool communicationNode = IsCommunicationNodeType(node.NodeType);
            string stage = communicationNode ? "通信节点开始" : "节点开始";
            LogStage(
                stage,
                () => communicationNode
                    ? $"节点=({node.ID}.{node.NodeName})；节点类型={node.NodeType}；通信类型={GetCommunicationTypeText(node.NodeType)}"
                    : $"节点=({node.ID}.{node.NodeName})；节点类型={node.NodeType}");
        }

        /// <summary>
        /// 记录节点通过统一结果入口完成的时间和状态。
        /// </summary>
        /// <param name="node">当前节点。</param>
        /// <param name="status">节点状态。</param>
        /// <param name="reportedElapsedMilliseconds">节点自身报告耗时。</param>
        public void NodeCompleted(NodeBase node, NodeStatus status, int reportedElapsedMilliseconds)
        {
            if (node == null || Volatile.Read(ref _completed) != 0)
                return;

            NodeTraceStartInfo startInfo;
            if (!_nodeStartInfo.TryRemove(node.ID, out startInfo))
                return;

            long actualElapsed = Math.Max(0, _stopwatch.ElapsedMilliseconds - startInfo.FlowElapsedMilliseconds);
            int completionThreadId = Thread.CurrentThread.ManagedThreadId;
            bool communicationNode = IsCommunicationNodeType(node.NodeType);
            if (communicationNode)
            {
                LogStage(
                    "通信节点结束",
                    () => $"节点=({node.ID}.{node.NodeName})；节点类型={node.NodeType}；通信类型={GetCommunicationTypeText(node.NodeType)}；状态={GetNodeStatusText(status)}；通信实际耗时={FormatElapsed(actualElapsed)}；节点报告={reportedElapsedMilliseconds}ms；开始线程={startInfo.ManagedThreadId}；结束线程={completionThreadId}；异步换线程={(startInfo.ManagedThreadId == completionThreadId ? "否" : "是")}；慢耗时={(actualElapsed >= PerformanceSpikeDiagnostics.CommonSlowMs ? "是" : "否")}；{GetCommunicationRuntimeText(actualElapsed)}");
                return;
            }

            LogStage(
                "节点结束",
                () => $"节点=({node.ID}.{node.NodeName})；状态={GetNodeStatusText(status)}；实际经过={FormatElapsed(actualElapsed)}；节点报告={reportedElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 记录节点在统一结果入口之外抛出的异常。
        /// </summary>
        /// <param name="node">异常节点。</param>
        /// <param name="reason">异常或中断原因。</param>
        public void NodeFaulted(NodeBase node, string reason)
        {
            if (node == null || Volatile.Read(ref _completed) != 0)
                return;

            NodeTraceStartInfo startInfo;
            if (!_nodeStartInfo.TryRemove(node.ID, out startInfo))
                return;

            long actualElapsed = Math.Max(0, _stopwatch.ElapsedMilliseconds - startInfo.FlowElapsedMilliseconds);
            int completionThreadId = Thread.CurrentThread.ManagedThreadId;
            bool communicationNode = IsCommunicationNodeType(node.NodeType);
            string stage = communicationNode ? "通信节点异常" : "节点异常";
            LogStage(
                stage,
                () => communicationNode
                    ? $"节点=({node.ID}.{node.NodeName})；节点类型={node.NodeType}；通信类型={GetCommunicationTypeText(node.NodeType)}；通信实际耗时={FormatElapsed(actualElapsed)}；开始线程={startInfo.ManagedThreadId}；结束线程={completionThreadId}；异步换线程={(startInfo.ManagedThreadId == completionThreadId ? "否" : "是")}；原因={FormatText(reason)}；{PerformanceSpikeDiagnostics.GetRuntimeText()}"
                    : $"节点=({node.ID}.{node.NodeName})；实际经过={FormatElapsed(actualElapsed)}；原因={FormatText(reason)}；{PerformanceSpikeDiagnostics.GetRuntimeText()}");
        }

        /// <summary>
        /// 记录流程内部的一个关键阶段。
        /// </summary>
        /// <param name="stage">阶段名称。</param>
        /// <param name="detailFactory">仅在Debug开启时生成的阶段明细。</param>
        public void LogStage(string stage, Func<string> detailFactory)
        {
            if (Volatile.Read(ref _completed) != 0 && !string.Equals(stage, "流程结束", StringComparison.Ordinal))
                return;

            long elapsed = _stopwatch.ElapsedMilliseconds;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => $"【完整耗时】TraceId={TraceId}；流程=({_processId}.{_processName})；RunId={_runId}；阶段={stage}；流程经过={elapsed}ms；线程={threadId}；{(detailFactory == null ? string.Empty : detailFactory())}",
                false);
        }

        /// <summary>
        /// 完成本轮流程追踪并记录最终运行现场。
        /// </summary>
        /// <param name="statusText">流程结束状态。</param>
        /// <param name="logicalRunTime">流程累计的逻辑节点耗时。</param>
        public void Complete(string statusText, long logicalRunTime)
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
                return;

            _stopwatch.Stop();
            long wallElapsed = _stopwatch.ElapsedMilliseconds;
            int unfinishedNodeCount = _nodeStartInfo.Count;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => $"【完整耗时】TraceId={TraceId}；流程=({_processId}.{_processName})；RunId={_runId}；阶段=流程结束；流程经过={wallElapsed}ms；线程={threadId}；状态={FormatText(statusText)}；逻辑节点耗时={logicalRunTime}ms；未闭合节点={unfinishedNodeCount}；待写日志={LogHelper.PendingLogCount}；已丢诊断={LogHelper.DroppedDiagnosticLogCount}；{PerformanceSpikeDiagnostics.GetRuntimeText()}",
                false);
        }

        /// <summary>
        /// 将节点状态转换为简体中文诊断文本。
        /// </summary>
        private static string GetNodeStatusText(NodeStatus status)
        {
            switch (status)
            {
                case NodeStatus.Successful:
                    return "成功";
                case NodeStatus.Failed:
                    return "失败";
                case NodeStatus.Running:
                    return "运行中";
                case NodeStatus.Unexecuted:
                    return "未执行";
                default:
                    return status.ToString();
            }
        }

        /// <summary>
        /// 格式化可能不存在的节点实际耗时。
        /// </summary>
        private static string FormatElapsed(long elapsedMilliseconds)
        {
            return elapsedMilliseconds < 0 ? "未知" : elapsedMilliseconds + "ms";
        }

        /// <summary>
        /// 将空文本转换为稳定的日志占位符。
        /// </summary>
        private static string FormatText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "无" : value.Replace("\r", " ").Replace("\n", " ");
        }

        /// <summary>
        /// 判断节点类型是否属于设备通信、协议通信或外部信号等待。
        /// </summary>
        /// <param name="nodeType">待判断的节点类型。</param>
        /// <returns>属于通信阶段时返回true。</returns>
        private static bool IsCommunicationNodeType(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.LightSourceControl:
                case NodeType.WaitSoftTrigger:
                case NodeType.CameraShot:
                case NodeType.PLCRead:
                case NodeType.PLCWrite:
                case NodeType.ModbusRead:
                case NodeType.ModbusWrite:
                case NodeType.TCPClientRequest:
                case NodeType.TCPServerResponse:
                case NodeType.ModbusSoftTrigger:
                case NodeType.AIResultSend:
                case NodeType.CameraIO:
                case NodeType.ComSend:
                case NodeType.ERUIIO:
                case NodeType.CameraIOManual:
                case NodeType.ReadFlag:
                case NodeType.CameraExposureGain:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取通信节点类型对应的简体中文协议或设备名称。
        /// </summary>
        /// <param name="nodeType">通信节点类型。</param>
        /// <returns>用于现场检索的通信类型文本。</returns>
        private static string GetCommunicationTypeText(NodeType nodeType)
        {
            switch (nodeType)
            {
                case NodeType.LightSourceControl:
                    return "光源控制";
                case NodeType.WaitSoftTrigger:
                    return "PLC软触发等待";
                case NodeType.CameraShot:
                    return "相机拍照";
                case NodeType.PLCRead:
                    return "PLC读取";
                case NodeType.PLCWrite:
                    return "PLC写入";
                case NodeType.ModbusRead:
                    return "Modbus读取";
                case NodeType.ModbusWrite:
                    return "Modbus写入";
                case NodeType.TCPClientRequest:
                    return "TCP客户端请求";
                case NodeType.TCPServerResponse:
                    return "TCP服务器响应";
                case NodeType.ModbusSoftTrigger:
                    return "Modbus软触发等待";
                case NodeType.AIResultSend:
                    return "检测结果信号发送";
                case NodeType.CameraIO:
                    return "相机IO";
                case NodeType.ComSend:
                    return "串口发送";
                case NodeType.ERUIIO:
                    return "ERUI IO";
                case NodeType.CameraIOManual:
                    return "相机IO手动控制";
                case NodeType.ReadFlag:
                    return "内部信号等待";
                case NodeType.CameraExposureGain:
                    return "相机参数通信";
                default:
                    return nodeType.ToString();
            }
        }

        /// <summary>
        /// 仅在通信耗时超过普通慢阈值时采集运行时现场。
        /// </summary>
        /// <param name="actualElapsed">通信节点实际墙钟耗时，单位毫秒。</param>
        /// <returns>慢耗时运行时现场或未触发说明。</returns>
        private static string GetCommunicationRuntimeText(long actualElapsed)
        {
            return actualElapsed >= PerformanceSpikeDiagnostics.CommonSlowMs
                ? "慢耗时现场=" + PerformanceSpikeDiagnostics.GetRuntimeText()
                : "慢耗时现场=未触发";
        }
    }
}
