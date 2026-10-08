using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>可移植日志等级，不引用生产日志组件。</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RuntimeLogLevel
    {
        /// <summary>调试。</summary>
        Debug,
        /// <summary>信息。</summary>
        Info,
        /// <summary>警告。</summary>
        Warn,
        /// <summary>异常。</summary>
        Exception,
        /// <summary>严重错误。</summary>
        Fatal
    }

    /// <summary>日志显示不可变快照，仅存于内存，不进入方案文档。</summary>
    public sealed class RuntimeLogEntry
    {
        /// <summary>日志发生时间。</summary>
        public DateTime Timestamp { get; }
        /// <summary>日志等级。</summary>
        public RuntimeLogLevel Level { get; }
        /// <summary>根流程、组合模块与节点的显示路径。</summary>
        public string Source { get; }
        /// <summary>日志正文。</summary>
        public string Message { get; }
        /// <summary>建立不持有生产对象的显示记录，限制单条文本占用。</summary>
        public RuntimeLogEntry(DateTime timestamp, RuntimeLogLevel level, string source, string message)
        {
            Timestamp = timestamp; Level = level;
            Source = Limit(source, 1024); Message = Limit(message, 4096);
        }
        /// <summary>截断仅影响显示快照，不改变原始日志文件。</summary>
        private static string Limit(string value, int maximum)
        { value = value ?? string.Empty; return value.Length <= maximum ? value : value.Substring(0, maximum - 3) + "..."; }
    }

    /// <summary>每个日志框拥有独立有界订阅，不能抢占其它窗口的数据。</summary>
    public interface IRuntimeLogSubscription : IDisposable
    {
        /// <summary>按发生顺序获取一批记录，不执行磁盘或设备操作。</summary>
        IReadOnlyList<RuntimeLogEntry> Drain(int maxCount);
        /// <summary>清空本订阅待显示的记录，不删除日志文件。</summary>
        void Clear();
    }

    /// <summary>方案作用域内的日志订阅工厂，失效流程不得回退为全部流程。</summary>
    public interface IRuntimeLogSource
    {
        /// <summary>创建独立订阅，空编号表示该方案全部根流程。</summary>
        IRuntimeLogSubscription Subscribe(int? processId, int capacity);
    }

    /// <summary>日志可订阅被动及禁用流程，不复用仅可启动流程的运行按钮目录。</summary>
    public interface IRuntimeLogProcessCatalog
    {
        /// <summary>获取方案中全部根流程的轻量目录。</summary>
        IReadOnlyList<RuntimeProcessChoice> GetLogProcesses();
    }
}
