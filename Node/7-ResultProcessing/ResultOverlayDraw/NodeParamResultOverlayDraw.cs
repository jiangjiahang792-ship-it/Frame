using System.Collections.Generic;
using System.Drawing;
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
        public int OkColorArgb { get; set; } = Color.Lime.ToArgb();

        /// <summary>
        /// 来源结果自动判定为NG时使用的ARGB颜色值。
        /// </summary>
        public int NgColorArgb { get; set; } = Color.Red.ToArgb();

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

        /// <summary>
        /// 订阅文本显示前缀。
        /// </summary>
        public string TextPrefix { get; set; }

        /// <summary>
        /// 固定颜色ARGB，兼容旧配置。
        /// </summary>
        public int ColorArgb { get; set; } = Color.Lime.ToArgb();

        /// <summary>
        /// 是否按源结果的判定OK状态自动选择OK/NG颜色。
        /// </summary>
        public bool UseJudgeColor { get; set; }

        /// <summary>
        /// 判定OK时使用的颜色ARGB。
        /// </summary>
        public int OkColorArgb { get; set; } = Color.Lime.ToArgb();

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
        /// 文本四角定位使用的坐标系，默认按图像显示区域定位。
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
        Roi
    }
}
