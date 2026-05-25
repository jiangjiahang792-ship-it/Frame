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
    }
}
