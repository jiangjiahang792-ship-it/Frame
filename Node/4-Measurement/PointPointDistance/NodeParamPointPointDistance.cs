using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointPointDistance
{
    public class NodeParamPointPointDistance : INodeParam
    {
        public string ImageText1 { get; set; }
        public string ImageText2 { get; set; }
        public MeasurementDataSourceMode SourceMode { get; set; } = MeasurementDataSourceMode.Draw;
        public string Point1Text1 { get; set; }
        public string Point1Text2 { get; set; }
        public MeasurementPointRole Point1Role { get; set; } = MeasurementPointRole.Auto;
        public string Point2Text1 { get; set; }
        public string Point2Text2 { get; set; }
        public MeasurementPointRole Point2Role { get; set; } = MeasurementPointRole.Auto;
        public bool UsePositionCorrection { get; set; }
        public string CorrectionText1 { get; set; }
        public string CorrectionText2 { get; set; }
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;
        public float Point1CenterX { get; set; } = 180;
        public float Point1CenterY { get; set; } = 180;
        public float Point1Radius { get; set; } = 60;
        public float Point2CenterX { get; set; } = 420;
        public float Point2CenterY { get; set; } = 180;
        public float Point2Radius { get; set; } = 60;
        public float CaliperWidth { get; set; } = 10;
        public float CaliperHeight { get; set; } = 60;
        public int Count { get; set; } = 30;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;

        public float? Point1CaliperWidth { get; set; }
        public float? Point1CaliperHeight { get; set; }
        public int? Point1Count { get; set; }
        public int? Point1EdgeStrength { get; set; }
        public CaliperEdgePolarity? Point1Polarity { get; set; }
        public CaliperEdgeFindMode? Point1FindMode { get; set; }
        public int? Point1Direction { get; set; }
        public int? Point1BlurSize { get; set; }
        /// <summary>获取或设置点1圆卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode Point1SamplingMode { get; set; } = CaliperSamplingMode.Fast;

        public float? Point2CaliperWidth { get; set; }
        public float? Point2CaliperHeight { get; set; }
        public int? Point2Count { get; set; }
        public int? Point2EdgeStrength { get; set; }
        public CaliperEdgePolarity? Point2Polarity { get; set; }
        public CaliperEdgeFindMode? Point2FindMode { get; set; }
        public int? Point2Direction { get; set; }
        public int? Point2BlurSize { get; set; }
        /// <summary>获取或设置点2圆卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode Point2SamplingMode { get; set; } = CaliperSamplingMode.Fast;

        public float GetCaliperWidth(bool firstPoint) => (firstPoint ? Point1CaliperWidth : Point2CaliperWidth) ?? CaliperWidth;
        public float GetCaliperHeight(bool firstPoint) => (firstPoint ? Point1CaliperHeight : Point2CaliperHeight) ?? CaliperHeight;
        public int GetCount(bool firstPoint) => (firstPoint ? Point1Count : Point2Count) ?? Count;
        public int GetEdgeStrength(bool firstPoint) => (firstPoint ? Point1EdgeStrength : Point2EdgeStrength) ?? EdgeStrength;
        public CaliperEdgePolarity GetPolarity(bool firstPoint) => (firstPoint ? Point1Polarity : Point2Polarity) ?? Polarity;
        public CaliperEdgeFindMode GetFindMode(bool firstPoint) => (firstPoint ? Point1FindMode : Point2FindMode) ?? FindMode;
        public int GetDirection(bool firstPoint) => (firstPoint ? Point1Direction : Point2Direction) ?? Direction;
        public int GetBlurSize(bool firstPoint) => (firstPoint ? Point1BlurSize : Point2BlurSize) ?? BlurSize;
        /// <summary>获取指定点圆卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode GetSamplingMode(bool firstPoint) => firstPoint ? Point1SamplingMode : Point2SamplingMode;
    }
}
