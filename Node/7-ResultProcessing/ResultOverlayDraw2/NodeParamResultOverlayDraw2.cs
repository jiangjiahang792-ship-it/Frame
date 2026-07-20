using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// ROI结果绘制2节点参数，保存输入图像、绘制项和多条布尔颜色判定规则。
    /// </summary>
    public class NodeParamResultOverlayDraw2 : INodeParam
    {
        /// <summary>
        /// 输入图像订阅节点文本，格式为“节点ID.节点名”。
        /// </summary>
        public string ImageText1 { get; set; }

        /// <summary>
        /// 输入图像订阅结果显示名。
        /// </summary>
        public string ImageText2 { get; set; }

        /// <summary>
        /// 全部规则为真时使用的OK颜色ARGB值。
        /// </summary>
        public int OkColorArgb { get; set; } = Color.Lime.ToArgb();

        /// <summary>
        /// 任一规则不满足时使用的NG颜色ARGB值。
        /// </summary>
        public int NgColorArgb { get; set; } = Color.Red.ToArgb();

        /// <summary>
        /// 多条布尔规则的聚合方式。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ResultOverlayColorRuleMode RuleMode { get; set; } = ResultOverlayColorRuleMode.AllTrue;

        /// <summary>
        /// 需要叠加显示的绘制项集合。
        /// </summary>
        public List<ResultOverlayDraw2Item> Items { get; set; } = new List<ResultOverlayDraw2Item>();

        /// <summary>
        /// 用于决定OK/NG颜色的布尔订阅规则集合。
        /// </summary>
        public List<ResultOverlayDraw2ColorRule> ColorRules { get; set; } = new List<ResultOverlayDraw2ColorRule>();

        /// <summary>
        /// OK颜色对象，序列化时使用 <see cref="OkColorArgb"/>。
        /// </summary>
        [JsonIgnore]
        public Color OkColor
        {
            get { return Color.FromArgb(OkColorArgb); }
            set { OkColorArgb = value.ToArgb(); }
        }

        /// <summary>
        /// NG颜色对象，序列化时使用 <see cref="NgColorArgb"/>。
        /// </summary>
        [JsonIgnore]
        public Color NgColor
        {
            get { return Color.FromArgb(NgColorArgb); }
            set { NgColorArgb = value.ToArgb(); }
        }
    }

    /// <summary>
    /// ROI结果绘制2的单个绘制项，描述要绘制的文本或几何结果。
    /// </summary>
    public class ResultOverlayDraw2Item
    {
        /// <summary>
        /// 是否启用该绘制项。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 绘制项类型。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ResultOverlayDraw2ItemType ItemType { get; set; } = ResultOverlayDraw2ItemType.Text;

        /// <summary>
        /// 绘制项名称，用于界面列表显示。
        /// </summary>
        public string Name { get; set; } = "文本";

        /// <summary>
        /// 绘制内容订阅节点文本。
        /// </summary>
        public string SourceText1 { get; set; }

        /// <summary>
        /// 绘制内容订阅结果显示名。
        /// </summary>
        public string SourceText2 { get; set; }

        /// <summary>
        /// 文本项是否使用手动文本。
        /// </summary>
        public bool UseManualText { get; set; } = true;

        /// <summary>
        /// 文本项启用手动文本时的显示内容。
        /// </summary>
        public string ManualText { get; set; } = "OK";

        /// <summary>
        /// 文本项使用订阅值时追加在值前面的前缀。
        /// </summary>
        public string TextPrefix { get; set; }

        /// <summary>
        /// 文本字号。
        /// </summary>
        public int FontSize { get; set; } = 18;

        /// <summary>
        /// 线条宽度。
        /// </summary>
        public int LineWidth { get; set; } = 2;

        /// <summary>
        /// 文本距离边缘的边距。
        /// </summary>
        public int TextMargin { get; set; } = 10;

        /// <summary>
        /// 文本显示位置。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextPosition TextPosition { get; set; } = DisplayTextPosition.TopLeft;

        /// <summary>
        /// 文本四角定位使用的坐标系，默认按图像显示区域定位。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextCoordinateMode TextCoordinateMode { get; set; } = DisplayTextCoordinateMode.Image;

        /// <summary>
        /// 订阅显示文本，界面列表使用。
        /// </summary>
        [JsonIgnore]
        public string SourceDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SourceText1))
                    return string.Empty;
                if (string.IsNullOrWhiteSpace(SourceText2))
                    return SourceText1;
                return SourceText1 + " / " + SourceText2;
            }
        }

        /// <summary>
        /// 创建当前绘制项的深拷贝，避免参数窗体直接修改已保存对象。
        /// </summary>
        /// <returns>复制后的绘制项。</returns>
        public ResultOverlayDraw2Item Clone()
        {
            return new ResultOverlayDraw2Item
            {
                Enabled = Enabled,
                ItemType = ItemType,
                Name = Name,
                SourceText1 = SourceText1,
                SourceText2 = SourceText2,
                UseManualText = UseManualText,
                ManualText = ManualText,
                TextPrefix = TextPrefix,
                FontSize = FontSize,
                LineWidth = LineWidth,
                TextMargin = TextMargin,
                TextPosition = TextPosition,
                TextCoordinateMode = TextCoordinateMode
            };
        }
    }

    /// <summary>
    /// 决定绘制颜色的单条布尔订阅规则。
    /// </summary>
    public class ResultOverlayDraw2ColorRule
    {
        /// <summary>
        /// 是否启用当前规则。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 布尔结果订阅节点文本。
        /// </summary>
        public string SourceText1 { get; set; }

        /// <summary>
        /// 布尔结果订阅属性显示名。
        /// </summary>
        public string SourceText2 { get; set; }

        /// <summary>
        /// 多条件节点中的条件项名称；为空或“整体结果”时读取整体判定。
        /// </summary>
        public string ConditionName { get; set; }

        /// <summary>
        /// 规则显示文本，界面列表使用。
        /// </summary>
        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                string source = string.IsNullOrWhiteSpace(SourceText1)
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(SourceText2) ? SourceText1 : SourceText1 + " / " + SourceText2;
                if (string.IsNullOrWhiteSpace(ConditionName) || ConditionName == "整体结果")
                    return source;
                return source + " / " + ConditionName;
            }
        }

        /// <summary>
        /// 创建当前颜色规则的深拷贝。
        /// </summary>
        /// <returns>复制后的颜色规则。</returns>
        public ResultOverlayDraw2ColorRule Clone()
        {
            return new ResultOverlayDraw2ColorRule
            {
                Enabled = Enabled,
                SourceText1 = SourceText1,
                SourceText2 = SourceText2,
                ConditionName = ConditionName
            };
        }
    }

    /// <summary>
    /// 多条布尔规则组合成最终OK/NG的方式。
    /// </summary>
    public enum ResultOverlayColorRuleMode
    {
        /// <summary>
        /// 所有启用规则都为真时判定OK。
        /// </summary>
        AllTrue,

        /// <summary>
        /// 任意一条启用规则为真时判定OK。
        /// </summary>
        AnyTrue
    }

    /// <summary>
    /// ROI结果绘制2支持的绘制项类型。
    /// </summary>
    public enum ResultOverlayDraw2ItemType
    {
        /// <summary>
        /// 绘制文本。
        /// </summary>
        Text,

        /// <summary>
        /// 绘制线段。
        /// </summary>
        Line,

        /// <summary>
        /// 绘制矩形。
        /// </summary>
        Rectangle,

        /// <summary>
        /// 绘制区域或轮廓。
        /// </summary>
        Region
    }
}
