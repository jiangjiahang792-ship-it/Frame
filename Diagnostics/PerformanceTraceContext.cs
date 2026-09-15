using Logger;
using System;
using System.Diagnostics;

namespace TDJS_Vision.Diagnostics
{
    /// <summary>
    /// 在UI投递和后台任务之间传递的只读流程诊断上下文，仅在Debug日志开启时创建。
    /// </summary>
    public sealed class PerformanceTraceContext
    {
        /// <summary>
        /// 创建只读流程诊断上下文。
        /// </summary>
        /// <param name="processName">流程名称。</param>
        /// <param name="traceId">流程完整耗时关联号。</param>
        /// <param name="runId">流程运行批次。</param>
        /// <param name="nodeId">来源节点编号。</param>
        /// <param name="nodeName">来源节点名称。</param>
        private PerformanceTraceContext(string processName, string traceId, int runId, int nodeId, string nodeName)
        {
            ProcessName = string.IsNullOrWhiteSpace(processName) ? "未命名流程" : processName;
            TraceId = traceId ?? string.Empty;
            RunId = runId;
            NodeId = nodeId;
            NodeName = nodeName ?? string.Empty;
            CreatedTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>获取流程名称。</summary>
        public string ProcessName { get; }

        /// <summary>获取流程完整耗时关联号。</summary>
        public string TraceId { get; }

        /// <summary>获取流程运行批次。</summary>
        public int RunId { get; }

        /// <summary>获取来源节点编号。</summary>
        public int NodeId { get; }

        /// <summary>获取来源节点名称。</summary>
        public string NodeName { get; }

        /// <summary>获取上下文创建时的高精度计时器刻度。</summary>
        public long CreatedTimestamp { get; }

        /// <summary>
        /// 在Debug开启时按当前流程和节点创建跨线程诊断上下文。
        /// </summary>
        /// <param name="process">当前流程。</param>
        /// <param name="nodeId">来源节点编号。</param>
        /// <param name="nodeName">来源节点名称。</param>
        /// <returns>Debug关闭时返回null。</returns>
        public static PerformanceTraceContext CreateIfEnabled(Process process, int nodeId, string nodeName)
        {
            if (!PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug))
                return null;

            return new PerformanceTraceContext(
                process?.ProcessName,
                process?.CurrentPerformanceTraceId,
                process?.CurrentRunId ?? 0,
                nodeId,
                nodeName);
        }

        /// <summary>
        /// 获取上下文创建到指定阶段的经过时间。
        /// </summary>
        /// <param name="completedTimestamp">完成阶段的高精度计时器刻度。</param>
        /// <returns>非负耗时，单位毫秒。</returns>
        public long GetElapsedMilliseconds(long completedTimestamp)
        {
            return CameraFrameTraceInfo.GetElapsedMilliseconds(CreatedTimestamp, completedTimestamp);
        }

        /// <summary>
        /// 生成可直接追加到完整耗时日志的流程关联文本。
        /// </summary>
        /// <returns>流程、批次和来源节点文本。</returns>
        public string ToLogText()
        {
            return $"流程={ProcessName}；TraceId={TraceId}；RunId={RunId}；来源节点={NodeId}.{NodeName}";
        }
    }
}
