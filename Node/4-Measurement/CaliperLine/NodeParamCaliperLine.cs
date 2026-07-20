using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    public class NodeParamCaliperLine : INodeParam
    {
        public string Text1 { get; set; }
        public string Text2 { get; set; }
        public bool UsePositionCorrection { get; set; }
        public string CorrectionText1 { get; set; }
        public string CorrectionText2 { get; set; }
        public float StartX { get; set; } = 100;
        public float StartY { get; set; } = 100;
        public float EndX { get; set; } = 400;
        public float EndY { get; set; } = 100;
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
