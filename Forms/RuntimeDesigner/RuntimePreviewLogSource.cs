using System;
using System.Collections.Generic;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>设计预览专用内存日志，永不订阅生产入口。</summary>
    public sealed class RuntimePreviewLogSource : IRuntimeLogSource
    {
        /// <summary>是否提供示例记录；正式订阅接入之前使用空源。</summary>
        private readonly bool includeExamples;
        /// <summary>构造内存预览源。</summary>
        public RuntimePreviewLogSource(bool includeExamples = true) { this.includeExamples = includeExamples; }
        /// <summary>每个控件独立获取示例快照。</summary>
        public IRuntimeLogSubscription Subscribe(int? processId, int capacity)
        {
            var entries = new List<RuntimeLogEntry>();
            if (includeExamples)
            {
                string root = "示例流程" + (processId.HasValue ? processId.Value.ToString() : "1");
                var now = DateTime.Now;
                entries.Add(new RuntimeLogEntry(now, RuntimeLogLevel.Info, root, "流程开始"));
                entries.Add(new RuntimeLogEntry(now.AddMilliseconds(8), RuntimeLogLevel.Info, root + " / 检测模块 / 目标检测", "节点运行成功（8 ms）"));
                entries.Add(new RuntimeLogEntry(now.AddMilliseconds(10), RuntimeLogLevel.Info, root, "流程结束：成功（10 ms）"));
            }
            return new Subscription(entries);
        }
        /// <summary>只提供一次示例，清空后不重新注入。</summary>
        private sealed class Subscription : IRuntimeLogSubscription
        {
            /// <summary>有限示例队列。</summary>
            private readonly Queue<RuntimeLogEntry> entries;
            /// <summary>复制预览数据。</summary>
            public Subscription(IEnumerable<RuntimeLogEntry> values) { entries = new Queue<RuntimeLogEntry>(values); }
            /// <summary>取一批示例。</summary>
            public IReadOnlyList<RuntimeLogEntry> Drain(int maxCount)
            {
                var batch = new List<RuntimeLogEntry>();
                while (entries.Count > 0 && batch.Count < maxCount) batch.Add(entries.Dequeue());
                return batch;
            }
            /// <summary>清除剩余示例。</summary>
            public void Clear() { entries.Clear(); }
            /// <summary>释放尚未显示的示例。</summary>
            public void Dispose() { Clear(); }
        }
    }
}
