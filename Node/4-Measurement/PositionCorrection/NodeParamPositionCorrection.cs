namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    public class NodeParamPositionCorrection : INodeParam
    {
        public string TextX1 { get; set; }
        public string TextX2 { get; set; }
        public string TextY1 { get; set; }
        public string TextY2 { get; set; }
        public string TextAngle1 { get; set; }
        public string TextAngle2 { get; set; }
        public bool HasBaseline { get; set; }
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double BaseAngle { get; set; }
    }
}
