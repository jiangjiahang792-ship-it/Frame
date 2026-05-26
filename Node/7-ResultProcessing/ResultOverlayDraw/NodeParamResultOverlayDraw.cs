using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    public class NodeParamResultOverlayDraw : INodeParam
    {
        public string ImageText1 { get; set; }
        public string ImageText2 { get; set; }
        public List<ResultOverlayDrawItem> Items { get; set; } = new List<ResultOverlayDrawItem>();
    }

    public class ResultOverlayDrawItem
    {
        public bool Enabled { get; set; } = true;

        [JsonConverter(typeof(StringEnumConverter))]
        public ResultOverlayDrawItemType ItemType { get; set; } = ResultOverlayDrawItemType.Text;

        public string Name { get; set; } = "Text";
        public string SourceText1 { get; set; }
        public string SourceText2 { get; set; }
        public bool UseManualText { get; set; } = true;
        public string ManualText { get; set; } = "OK";
        public string TextPrefix { get; set; }
        public int ColorArgb { get; set; } = Color.Lime.ToArgb();
        public int FontSize { get; set; } = 18;
        public int LineWidth { get; set; } = 2;
        public int TextMargin { get; set; } = 10;

        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayTextPosition TextPosition { get; set; } = DisplayTextPosition.TopLeft;

        [JsonIgnore]
        public Color Color
        {
            get { return Color.FromArgb(ColorArgb); }
            set { ColorArgb = value.ToArgb(); }
        }

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
                FontSize = FontSize,
                LineWidth = LineWidth,
                TextMargin = TextMargin,
                TextPosition = TextPosition
            };
        }
    }

    public enum ResultOverlayDrawItemType
    {
        Text,
        Line,
        Rectangle,
        Region
    }
}
