using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Logger;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>冻结宿主方案的根流程实例，读取日志时不访问当前方案或生产设备。</summary>
    public sealed class RuntimeFrameworkLogSource : IRuntimeLogSource
    {
        /// <summary>宿主创建来源时的根流程快照。</summary>
        private readonly Process[] roots;

        /// <summary>只保留指定实例，不跟随方案切换和同编号流程重建。</summary>
        public RuntimeFrameworkLogSource(IEnumerable<Process> roots)
        { this.roots = (roots ?? throw new ArgumentNullException(nameof(roots))).Where(root => root != null).ToArray(); }

        /// <summary>全部模式同样限定到捕获实例；指定编号失效时仅报告一次，不回退到全部。</summary>
        public IRuntimeLogSubscription Subscribe(int? processId, int capacity)
        {
            if (capacity < 1 || capacity > 10000) throw new ArgumentOutOfRangeException(nameof(capacity));
            var selected = processId.HasValue ? roots.Where(root => root.ID == processId.Value).ToArray() : roots;
            if (processId.HasValue && selected.Length != 1)
                return new InvalidSubscription();
            return new Subscription(RuntimeLogStream.Subscribe(selected.Cast<object>().ToArray(), capacity));
        }

        /// <summary>失效模板订阅只显示一次错误，不中断同页其它有效控件。</summary>
        private sealed class InvalidSubscription : IRuntimeLogSubscription
        {
            /// <summary>惰性消费一次后释放，不订阅任何生产根流程。</summary>
            private RuntimeLogEntry error = new RuntimeLogEntry(DateTime.Now, RuntimeLogLevel.Exception,
                "日志订阅", "指定日志流程已失效或编号重复，请重新选择。");
            /// <summary>非正批次不消费错误，后续调用不会重复提示。</summary>
            public IReadOnlyList<RuntimeLogEntry> Drain(int maxCount)
            {
                var value = maxCount > 0 ? Interlocked.Exchange(ref error, null) : null;
                return value == null ? new RuntimeLogEntry[0] : new[] { value };
            }
            /// <summary>清空未显示的错误。</summary>
            public void Clear() { Interlocked.Exchange(ref error, null); }
            /// <summary>释放未显示的错误，不触碰生产日志。</summary>
            public void Dispose() { Clear(); }
        }

        /// <summary>仅将纯值记录转换为界面契约，不把生产实例带入列表。</summary>
        private sealed class Subscription : IRuntimeLogSubscription
        {
            /// <summary>只属于此视图的流订阅。</summary>
            private readonly RuntimeLogStream.Subscription source;
            /// <summary>接管缓冲区生命周期。</summary>
            internal Subscription(RuntimeLogStream.Subscription source) { this.source = source; }
            /// <summary>批量转换有限条目，不与其它窗口争用消费游标。</summary>
            public IReadOnlyList<RuntimeLogEntry> Drain(int maxCount)
            {
                var values = source.Drain(maxCount);
                var result = new List<RuntimeLogEntry>(values.Count);
                foreach (var value in values)
                    result.Add(new RuntimeLogEntry(value.Timestamp, Level(value.Level), value.Source, value.Message));
                return result;
            }
            /// <summary>清空本窗口尚未显示的记录。</summary>
            public void Clear() => source.Clear();
            /// <summary>关闭本窗口的订阅，不干预文件日志或其它窗口。</summary>
            public void Dispose() => source.Dispose();
            /// <summary>显式映射等级，避免依赖两个枚举的底层数值一致。</summary>
            private static RuntimeLogLevel Level(MsgLevel level)
            {
                switch (level)
                {
                    case MsgLevel.Debug: return RuntimeLogLevel.Debug;
                    case MsgLevel.Warn: return RuntimeLogLevel.Warn;
                    case MsgLevel.Exception: return RuntimeLogLevel.Exception;
                    case MsgLevel.Fatal: return RuntimeLogLevel.Fatal;
                    default: return RuntimeLogLevel.Info;
                }
            }
        }
    }
}
