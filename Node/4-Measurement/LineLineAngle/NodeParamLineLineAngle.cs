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
    }
}
