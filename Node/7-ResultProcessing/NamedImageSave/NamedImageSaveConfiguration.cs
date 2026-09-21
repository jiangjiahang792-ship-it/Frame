using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;

namespace TDJS_Vision.Node._7_ResultProcessing.NamedImageSave
{
    /// <summary>持久化订阅源的稳定标识，节点改名不影响读取。</summary>
    public sealed class NamedImageSource
    {
        /// <summary>当前流程内的源节点编号。</summary>
        public int SourceNodeId { get; set; }
        /// <summary>原始属性名或动态变量路径。</summary>
        public string PropertyPath { get; set; }
        /// <summary>供参数界面显示的来源描述。</summary>
        public string DisplayName { get; set; }
        /// <summary>复制不包含运行对象的订阅引用。</summary>
        public NamedImageSource Copy() => (NamedImageSource)MemberwiseClone();
    }

    /// <summary>命名片段的取值方式，旧方案没有该字段时按来源是否存在自动兼容。</summary>
    public enum NamedImageValueMode
    {
        /// <summary>使用手动填写的文字。</summary>
        Manual = 0,
        /// <summary>使用订阅结果原始值。</summary>
        Subscription = 1,
        /// <summary>使用运行当天日期。</summary>
        CurrentDate = 2,
        /// <summary>使用布尔订阅转换出的OK或NG。</summary>
        BoolOkNg = 3
    }

    /// <summary>目录行或文件名：支持手填、订阅、当前日期和布尔OK/NG。</summary>
    public sealed class NamedImageValue
    {
        /// <summary>手动填写的内容。</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>当前命名片段的取值方式。</summary>
        public NamedImageValueMode Mode { get; set; } = NamedImageValueMode.Manual;
        /// <summary>可选订阅来源。</summary>
        public NamedImageSource Source { get; set; }
        /// <summary>实际生效模式，兼容旧方案中仅有Source但没有Mode的订阅行。</summary>
        [JsonIgnore]
        public NamedImageValueMode EffectiveMode
        {
            get
            {
                if (Mode == NamedImageValueMode.CurrentDate || Mode == NamedImageValueMode.BoolOkNg) return Mode;
                return Source == null ? NamedImageValueMode.Manual : NamedImageValueMode.Subscription;
            }
        }
        /// <summary>列表中显示手填内容或订阅描述。</summary>
        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                switch (EffectiveMode)
                {
                    case NamedImageValueMode.CurrentDate:
                        return "[当前日期] yyyy-MM-dd";
                    case NamedImageValueMode.BoolOkNg:
                        return "[OK/NG] " + (Source?.DisplayName ?? "未选择布尔结果");
                    case NamedImageValueMode.Subscription:
                        return "[订阅] " + Source.DisplayName;
                    default:
                        return Text;
                }
            }
        }
        /// <summary>复制编辑草稿，防止保存后界面继续修改运行参数。</summary>
        public NamedImageValue Copy() => new NamedImageValue { Text = Text, Mode = Mode, Source = Source?.Copy() };
    }

    /// <summary>命名存图节点的全部可持久化参数。</summary>
    public sealed class NodeParamNamedImageSave : INodeParam
    {
        /// <summary>保存根盘符，例如D:\。</summary>
        public string DriveRoot { get; set; } = "D:\\";
        /// <summary>从上到下排列的各级文件夹。</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<NamedImageValue> Folders { get; set; } = new List<NamedImageValue> { new NamedImageValue { Text = "图像" } };
        /// <summary>不含扩展名的文件名，手填留空时使用当前日期时间。</summary>
        public NamedImageValue FileName { get; set; } = new NamedImageValue();
        /// <summary>从左到右拼接的文件名片段；为空时兼容旧方案的FileName。</summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<NamedImageValue> FileNameParts { get; set; } = new List<NamedImageValue> { new NamedImageValue() };
        /// <summary>输出图像订阅。</summary>
        public NamedImageSource ImageSource { get; set; }
        /// <summary>JPEG保存质量，范围1到100。</summary>
        public int JpegQuality { get; set; } = 95;
        /// <summary>深复制配置供编辑或单次运行使用。</summary>
        public NodeParamNamedImageSave Copy() => new NodeParamNamedImageSave
        {
            DriveRoot = DriveRoot,
            Folders = Folders?.Select(row => row?.Copy()).ToList(),
            FileName = FileName?.Copy(),
            FileNameParts = FileNameParts?.Select(row => row?.Copy()).ToList(),
            ImageSource = ImageSource?.Copy(),
            JpegQuality = JpegQuality
        };
    }

    /// <summary>命名存图的入队结果，异步写盘状态由保存日志报告。</summary>
    public sealed class NodeResultNamedImageSave : INodeResult
    {
        /// <summary>节点运行耗时，单位毫秒。</summary>
        public int RunTime { get; set; }
        /// <summary>本次任务是否已被后台队列接纳。</summary>
        [SubscriptionOutput]
        [DisplayName("已入队")]
        public bool Enqueued { get; set; }
        /// <summary>本次冻结的最终目标目录。</summary>
        [SubscriptionOutput]
        [DisplayName("保存目录")]
        public string DirectoryPath { get; set; }
        /// <summary>本次冻结的基础文件名，实际重名后缀由后台生成。</summary>
        [SubscriptionOutput]
        [DisplayName("图像名称")]
        public string ImageName { get; set; }
    }
}
