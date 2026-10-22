using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    /// <summary>
    /// ROI结果绘制节点参数，保存输入图像、自动判定颜色和文本/ROI绘制项。
    /// 旧绘制项类型和每项颜色字段继续保留，用于已有方案兼容。
    /// </summary>
    public class NodeParamResultOverlayDraw : INodeParam
    {
        /// <summary>全部文本共用的坐标系，包含订阅文本、布尔文本和独立固定内容。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextCoordinateMode TextCoordinateMode { get; set; } = DisplayTextCoordinateMode.Image;

        /// <summary>是否额外显示独立的右下角固定文本，默认关闭。</summary>
        public bool UseCornerTextBox { get; set; }

        /// <summary>右下角固定输入的文本内容，与订阅绘制项无关。</summary>
        public string CornerText { get; set; } = "0°";

        /// <summary>右下角固定文本的独立字号。</summary>
        public int CornerFontSize { get; set; } = 18;

        /// <summary>右下角固定文本背景色，默认白色。</summary>
        public int CornerBackgroundArgb { get; set; } = Color.White.ToArgb();

        /// <summary>右下角固定文本文字色，默认绿色。</summary>
        public int CornerForegroundArgb { get; set; } = Color.Green.ToArgb();

        /// <summary>
        /// 输入图像订阅节点文本。
        /// </summary>
        public string ImageText1 { get; set; }

        /// <summary>
        /// 输入图像订阅结果文本。
        /// </summary>
        public string ImageText2 { get; set; }

        /// <summary>
        /// 来源结果自动判定为OK时使用的ARGB颜色值。
        /// </summary>
        public int OkColorArgb { get; set; } = Color.Green.ToArgb();

        /// <summary>
        /// 来源结果自动判定为NG时使用的ARGB颜色值。
        /// </summary>
        public int NgColorArgb { get; set; } = Color.Red.ToArgb();

        /// <summary>
        /// 输出图像和叠加结果的顺时针旋转角度，仅支持0、90、180、270。
        /// </summary>
        public int RotationAngle { get; set; } = 0;

        /// <summary>
        /// 需要叠加绘制的项目列表。
        /// </summary>
        public List<ResultOverlayDrawItem> Items { get; set; } = new List<ResultOverlayDrawItem>();

        /// <summary>
        /// 获取或设置自动判定为OK时的颜色对象。
        /// </summary>
        [JsonIgnore]
        public Color OkColor
        {
            get { return Color.FromArgb(OkColorArgb); }
            set { OkColorArgb = value.ToArgb(); }
        }

        /// <summary>
        /// 获取或设置自动判定为NG时的颜色对象。
        /// </summary>
        [JsonIgnore]
        public Color NgColor
        {
            get { return Color.FromArgb(NgColorArgb); }
            set { NgColorArgb = value.ToArgb(); }
        }
    }

    /// <summary>
    /// ROI结果绘制的单个文本或几何绘制项。
    /// </summary>
    public class ResultOverlayDrawItem
    {
        /// <summary>
        /// 当前绘制项是否启用。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 绘制项类型。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ResultOverlayDrawItemType ItemType { get; set; } = ResultOverlayDrawItemType.Text;

        /// <summary>
        /// 绘制项名称。
        /// </summary>
        public string Name { get; set; } = "Text";

        /// <summary>
        /// 绘制内容订阅节点文本。
        /// </summary>
        public string SourceText1 { get; set; }

        /// <summary>
        /// 绘制内容订阅结果文本。
        /// </summary>
        public string SourceText2 { get; set; }

        /// <summary>
        /// 文本项是否使用手填文本。
        /// </summary>
        public bool UseManualText { get; set; } = true;

        /// <summary>
        /// 手填文本内容。
        /// </summary>
        public string ManualText { get; set; } = "OK";

        /// <summary>布尔订阅为真（OK）时显示的自定义文本。</summary>
        public string BooleanOkText { get; set; } = "合格";

        /// <summary>布尔订阅为假（NG）时显示的自定义文本。</summary>
        public string BooleanNgText { get; set; } = "不合格";

        /// <summary>组合布尔文本的逻辑，默认要求全部订阅满足。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public OverlayBooleanMatchMode BooleanMatchMode { get; set; } = OverlayBooleanMatchMode.All;

        /// <summary>组合布尔文本的独立订阅列表，不影响旧版单个布尔文本字段。</summary>
        public List<OverlayBooleanSource> BooleanSources { get; set; } = new List<OverlayBooleanSource>();

        /// <summary>
        /// 订阅文本显示前缀。
        /// </summary>
        public string TextPrefix { get; set; }

        /// <summary>
        /// 固定颜色ARGB，兼容旧配置。
        /// </summary>
        public int ColorArgb { get; set; } = Color.Green.ToArgb();

        /// <summary>
        /// 是否按源结果的判定OK状态自动选择OK/NG颜色。
        /// </summary>
        public bool UseJudgeColor { get; set; }

        /// <summary>
        /// 判定OK时使用的颜色ARGB。
        /// </summary>
        public int OkColorArgb { get; set; } = Color.Green.ToArgb();

        /// <summary>
        /// 判定NG时使用的颜色ARGB。
        /// </summary>
        public int NgColorArgb { get; set; } = Color.Red.ToArgb();

        /// <summary>
        /// 文本字号。
        /// </summary>
        public int FontSize { get; set; } = 18;

        /// <summary>
        /// 图形线宽。
        /// </summary>
        public int LineWidth { get; set; } = 2;

        /// <summary>
        /// 文本距离图像边缘的边距。
        /// </summary>
        public int TextMargin { get; set; } = 10;

        /// <summary>
        /// 文本显示位置。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextPosition TextPosition { get; set; } = DisplayTextPosition.TopLeft;

        /// <summary>
        /// 旧方案文本坐标系兼容字段；运行时以节点级统一坐标系为准。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextCoordinateMode TextCoordinateMode { get; set; } = DisplayTextCoordinateMode.Image;

        /// <summary>
        /// 固定绘制颜色。
        /// </summary>
        [JsonIgnore]
        public Color Color
        {
            get { return Color.FromArgb(ColorArgb); }
            set { ColorArgb = value.ToArgb(); }
        }

        /// <summary>
        /// 判定OK绘制颜色。
        /// </summary>
        [JsonIgnore]
        public Color OkColor
        {
            get { return Color.FromArgb(OkColorArgb); }
            set { OkColorArgb = value.ToArgb(); }
        }

        /// <summary>
        /// 判定NG绘制颜色。
        /// </summary>
        [JsonIgnore]
        public Color NgColor
        {
            get { return Color.FromArgb(NgColorArgb); }
            set { NgColorArgb = value.ToArgb(); }
        }

        /// <summary>
        /// 订阅源显示文本。
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
        /// 克隆绘制项参数，避免界面编辑直接修改已保存参数实例。
        /// </summary>
        public ResultOverlayDrawItem Clone()
        {
            return new ResultOverlayDrawItem
            {
                Enabled = Enabled,
                ItemType = ItemType,
                Name = Name,
                SourceText1 = SourceText1,
                SourceText2 = SourceText2,
                UseManualText = UseManualText,
                ManualText = ManualText,
                BooleanOkText = BooleanOkText,
                BooleanNgText = BooleanNgText,
                BooleanMatchMode = BooleanMatchMode,
                BooleanSources = BooleanSources == null ? new List<OverlayBooleanSource>()
                    : BooleanSources.Select(source => source?.Clone()).ToList(),
                TextPrefix = TextPrefix,
                ColorArgb = ColorArgb,
                UseJudgeColor = UseJudgeColor,
                OkColorArgb = OkColorArgb,
                NgColorArgb = NgColorArgb,
                FontSize = FontSize,
                LineWidth = LineWidth,
                TextMargin = TextMargin,
                TextPosition = TextPosition,
                TextCoordinateMode = TextCoordinateMode
            };
        }
    }

    /// <summary>
    /// ROI结果绘制支持的绘制项类型。
    /// </summary>
    public enum ResultOverlayDrawItemType
    {
        /// <summary>
        /// 绘制文本。
        /// </summary>
        Text,

        /// <summary>
        /// 旧方案专用线段绘制类型。
        /// </summary>
        Line,

        /// <summary>
        /// 旧方案专用矩形绘制类型。
        /// </summary>
        Rectangle,

        /// <summary>
        /// 旧方案专用区域或轮廓绘制类型。
        /// </summary>
        Region,

        /// <summary>
        /// 按订阅值实际类型自动绘制全部ROI几何。
        /// </summary>
        Roi,

        /// <summary>订阅单个布尔结果，按真/假显示自定义OK/NG文本。</summary>
        BooleanText,

        /// <summary>组合多个布尔订阅，按全部或任一满足显示一个判定文本。</summary>
        CombinedBooleanText
    }

    /// <summary>组合布尔文本支持的判定方式。</summary>
    public enum OverlayBooleanMatchMode
    {
        /// <summary>全部订阅为真。</summary>
        All,
        /// <summary>至少一个订阅为真；所有订阅均必须有效。</summary>
        Any
    }

    /// <summary>组合布尔文本的单个订阅，复用现有订阅控件的节点和结果标识。</summary>
    public sealed class OverlayBooleanSource
    {
        /// <summary>节点编号及名称。</summary>
        public string SourceText1 { get; set; }
        /// <summary>订阅输出名称。</summary>
        public string SourceText2 { get; set; }
        /// <summary>用于订阅列表显示的中文说明。</summary>
        [JsonIgnore]
        public string DisplayText => SourceText1 + " / " + SourceText2;
        /// <summary>复制编辑数据，避免修改已保存配置。</summary>
        public OverlayBooleanSource Clone() => (OverlayBooleanSource)MemberwiseClone();
    }
}
