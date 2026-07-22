using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    public class NodeParamLineLineAngle : INodeParam
    {
        public string ImageText1 { get; set; }
        public string ImageText2 { get; set; }
        public MeasurementDataSourceMode SourceMode { get; set; } = MeasurementDataSourceMode.Draw;
        public string Line1Text1 { get; set; }
        public string Line1Text2 { get; set; }
        public string Line2Text1 { get; set; }
        public string Line2Text2 { get; set; }
        public bool UsePositionCorrection { get; set; }
        public string CorrectionText1 { get; set; }
        public string CorrectionText2 { get; set; }
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;
        public float Line1StartX { get; set; } = 120;
        public float Line1StartY { get; set; } = 160;
        public float Line1EndX { get; set; } = 420;
        public float Line1EndY { get; set; } = 160;
        public float Line2StartX { get; set; } = 160;
        public float Line2StartY { get; set; } = 360;
        public float Line2EndX { get; set; } = 420;
        public float Line2EndY { get; set; } = 240;
        public float CaliperWidth { get; set; } = 20;
        public float CaliperHeight { get; set; } = 120;
        public int Count { get; set; } = 15;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;

        public float? Line1CaliperWidth { get; set; }
        public float? Line1CaliperHeight { get; set; }
        public int? Line1Count { get; set; }
        public int? Line1EdgeStrength { get; set; }
        public CaliperEdgePolarity? Line1Polarity { get; set; }
        public CaliperEdgeFindMode? Line1FindMode { get; set; }
        public int? Line1Direction { get; set; }
        public int? Line1BlurSize { get; set; }
        /// <summary>获取或设置直线1卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode Line1SamplingMode { get; set; } = CaliperSamplingMode.Fast;

        public float? Line2CaliperWidth { get; set; }
        public float? Line2CaliperHeight { get; set; }
        public int? Line2Count { get; set; }
        public int? Line2EdgeStrength { get; set; }
        public CaliperEdgePolarity? Line2Polarity { get; set; }
        public CaliperEdgeFindMode? Line2FindMode { get; set; }
        public int? Line2Direction { get; set; }
        public int? Line2BlurSize { get; set; }
        /// <summary>获取或设置直线2卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode Line2SamplingMode { get; set; } = CaliperSamplingMode.Fast;

        public float GetCaliperWidth(bool firstLine) => (firstLine ? Line1CaliperWidth : Line2CaliperWidth) ?? CaliperWidth;
        public float GetCaliperHeight(bool firstLine) => (firstLine ? Line1CaliperHeight : Line2CaliperHeight) ?? CaliperHeight;
        public int GetCount(bool firstLine) => (firstLine ? Line1Count : Line2Count) ?? Count;
        public int GetEdgeStrength(bool firstLine) => (firstLine ? Line1EdgeStrength : Line2EdgeStrength) ?? EdgeStrength;
        public CaliperEdgePolarity GetPolarity(bool firstLine) => (firstLine ? Line1Polarity : Line2Polarity) ?? Polarity;
        public CaliperEdgeFindMode GetFindMode(bool firstLine) => (firstLine ? Line1FindMode : Line2FindMode) ?? FindMode;
        public int GetDirection(bool firstLine) => (firstLine ? Line1Direction : Line2Direction) ?? Direction;
        public int GetBlurSize(bool firstLine) => (firstLine ? Line1BlurSize : Line2BlurSize) ?? BlurSize;
        /// <summary>获取指定直线卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode GetSamplingMode(bool firstLine) => firstLine ? Line1SamplingMode : Line2SamplingMode;
    }
}
