using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TDJS_Vision.Device;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>设备写入的基础类型；明确位宽，不根据数值大小猜测。</summary>
    public enum ResultSendType
    {
        /// <summary>布尔。</summary>
        Boolean,
        /// <summary>32位有符号整数。</summary>
        Int32,
        /// <summary>32位无符号整数。</summary>
        UInt32,
        /// <summary>16位无符号整数。</summary>
        UInt16,
        /// <summary>16位有符号整数。</summary>
        Int16,
        /// <summary>固定字节长度字符串。</summary>
        String,
        /// <summary>64位浮点。</summary>
        Double,
        /// <summary>32位浮点。</summary>
        Single
    }

    /// <summary>多个输出值的选择方式。</summary>
    public enum ResultSendMode
    {
        /// <summary>直接读取一个标量，多个值时拒绝隐式选取。</summary>
        Single,
        /// <summary>读取从1开始的指定项。</summary>
        Index,
        /// <summary>依上游顺序连续发送全部值。</summary>
        All,
        /// <summary>汇总为一个值。</summary>
        Aggregate
    }

    /// <summary>多值汇总方式。</summary>
    public enum ResultSendAggregate
    {
        /// <summary>最大值。</summary>
        Maximum,
        /// <summary>最小值。</summary>
        Minimum,
        /// <summary>平均值。</summary>
        Average,
        /// <summary>总和。</summary>
        Sum,
        /// <summary>全部为真。</summary>
        AllTrue,
        /// <summary>任意为真。</summary>
        AnyTrue
    }

    /// <summary>订阅字段种类，避免使用可执行表达式读取结果。</summary>
    public enum ResultSendSourceKind
    {
        /// <summary>静态属性或动态变量。</summary>
        Property,
        /// <summary>集合内的标量成员。</summary>
        Items,
        /// <summary>AI检测明细值。</summary>
        AiValues,
        /// <summary>AI检测明细布尔判定。</summary>
        AiFlags,
        /// <summary>一个AI检测项全部明细的合并判定。</summary>
        AiJudgment
    }

    /// <summary>可持久化的订阅引用，不保存节点或控件对象。</summary>
    public sealed class ResultSendSource
    {
        /// <summary>源节点ID。</summary>
        public int NodeId { get; set; }
        /// <summary>源结果属性路径或动态变量路径。</summary>
        public string Path { get; set; }
        /// <summary>集合内成员名称。</summary>
        public string Member { get; set; }
        /// <summary>AI检测项的原始名称键。</summary>
        public string ItemName { get; set; }
        /// <summary>字段种类。</summary>
        public ResultSendSourceKind Kind { get; set; }
        /// <summary>配置界面中显示的中文路径。</summary>
        public string DisplayName { get; set; }
        /// <summary>是否声明为多个值，配置前可见。</summary>
        public bool Multiple { get; set; }
        /// <summary>是否声明为布尔字段。</summary>
        public bool Boolean { get; set; }
        /// <summary>复制只含标量的订阅引用。</summary>
        public ResultSendSource Copy() => (ResultSendSource)MemberwiseClone();
    }

    /// <summary>发送表格中的单行配置。</summary>
    public sealed class ResultSendRow
    {
        /// <summary>是否启用本行。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>上游订阅引用。</summary>
        public ResultSendSource Source { get; set; } = new ResultSendSource();
        /// <summary>显示完整来源名称。</summary>
        [JsonIgnore]
        public string SourceText
        {
            get
            {
                if (Source == null || string.IsNullOrEmpty(Source.DisplayName)) return "未选择订阅结果";
                if (Source.Kind < ResultSendSourceKind.AiValues) return Source.DisplayName;
                int separator = Source.DisplayName.IndexOf(" → ", StringComparison.Ordinal);
                string node = separator < 0 ? Source.NodeId.ToString() : Source.DisplayName.Substring(0, separator);
                string field = Source.Kind == ResultSendSourceKind.AiValues ? "明细检测值" : Source.Kind == ResultSendSourceKind.AiFlags ? "明细判定" : "检测项判定";
                return node + " → " + (string.IsNullOrWhiteSpace(Source.ItemName) ? "指定检测项" : DetectItemLanguage.GetDisplayName(Source.ItemName)) + " / " + field;
            }
        }
        /// <summary>目标地址，交由协议解释。</summary>
        public string Address { get; set; } = string.Empty;
        /// <summary>目标基础类型。</summary>
        public ResultSendType DataType { get; set; } = ResultSendType.Double;
        /// <summary>取值方式。</summary>
        public ResultSendMode Mode { get; set; }
        /// <summary>从1开始的明细序号。</summary>
        public int Index { get; set; } = 1;
        /// <summary>汇总方式。</summary>
        public ResultSendAggregate Aggregate { get; set; }
        /// <summary>连续发送预留元素数量，超出时失败。</summary>
        public int Capacity { get; set; } = 10;
        /// <summary>实际数量寄存器，固定写入ushort，数据完成后发送。</summary>
        public string CountAddress { get; set; } = string.Empty;
        /// <summary>是否将未使用的预留元素写入对应类型的默认值。</summary>
        public bool ClearRemaining { get; set; } = true;
        /// <summary>每个字符串的固定字节容量，必须为正偶数。</summary>
        public int StringBytes { get; set; } = 32;
        /// <summary>字符串编码名称。</summary>
        public string EncodingName { get; set; } = "UTF-8";
        /// <summary>复制配置，防止发送排队期间界面修改影响已冻结数据。</summary>
        public ResultSendRow Copy()
        {
            var copy = (ResultSendRow)MemberwiseClone();
            copy.Source = Source?.Copy();
            return copy;
        }
    }

    /// <summary>结果发送节点参数，设备按名称重新绑定现有连接。</summary>
    public sealed class NodeParamResultSend : INodeParam
    {
        /// <summary>设备的用户自定义名称。</summary>
        public string DeviceName { get; set; }
        /// <summary>按发送顺序保存的行。</summary>
        public List<ResultSendRow> Rows { get; set; } = new List<ResultSendRow>();
        /// <summary>复制参数及行，构建独立运行快照。</summary>
        public NodeParamResultSend Copy() => new NodeParamResultSend
        {
            DeviceName = DeviceName,
            Rows = Rows == null ? new List<ResultSendRow>() : Rows.ConvertAll(row => row?.Copy())
        };
    }

    /// <summary>结果发送运行结果，不把业务NG误认为通信失败。</summary>
    public sealed class NodeResultResultSend : INodeResult
    {
        /// <summary>运行耗时。</summary>
        public int RunTime { get; set; }
        /// <summary>全部启用行是否发送成功。</summary>
        [SubscriptionOutput, DisplayName("发送成功")]
        public bool Success { get; set; }
        /// <summary>已完成数据及数量写入的行数。</summary>
        [SubscriptionOutput, DisplayName("成功条数")]
        public int SentCount { get; set; }
        /// <summary>失败行序号，从1开始，0表示尚未定位到具体行。</summary>
        [SubscriptionOutput, DisplayName("失败行")]
        public int FailedRow { get; set; }
        /// <summary>失败或取消原因。</summary>
        [SubscriptionOutput, DisplayName("错误信息")]
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>设备写入适配器接口，新通信对象可独立实现。</summary>
    public interface IResultSendWriter
    {
        /// <summary>对应设备。</summary>
        IDevice Device { get; }
        /// <summary>与既有写入节点共享的物理端点键。</summary>
        string EndpointKey { get; }
        /// <summary>发送前检查地址、范围及设备类型能力。</summary>
        void Validate(string address, ResultSendType type, Array values);
        /// <summary>执行已经转换的值；失败必须抛出异常。</summary>
        Task WriteAsync(string address, ResultSendType type, Array values);
    }

    /// <summary>一行已经转换和冻结的发送内容。</summary>
    public sealed class ResultSendPreparedRow
    {
        /// <summary>原表格行序号。</summary>
        public int RowNumber { get; set; }
        /// <summary>独立行配置快照。</summary>
        public ResultSendRow Configuration { get; set; }
        /// <summary>已转换的数组；字符串保存为固定长度编码字节。</summary>
        public Array Values { get; set; }
        /// <summary>补零之前的有效值数量。</summary>
        public ushort Count { get; set; }
    }
}
