using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperEllipse
{
    public class NodeParamCaliperEllipse : INodeParam
    {
        public string Text1 { get; set; }
        public string Text2 { get; set; }
        public float CenterX { get; set; } = 250;
        public float CenterY { get; set; } = 250;
        public float Width { get; set; } = 220;
        public float Height { get; set; } = 120;
        public float Angle { get; set; }
        public int Count { get; set; } = 30;
        public float CaliperWidth { get; set; } = 10;
        public float CaliperHeight { get; set; } = 60;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        public int BlurSize { get; set; } = 3;
    }
}
