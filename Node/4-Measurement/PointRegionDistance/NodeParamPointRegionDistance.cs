using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointRegionDistance
{
    public class NodeParamPointRegionDistance : INodeParam
    {
        public string ImageText1 { get; set; }
        public string ImageText2 { get; set; }
        public MeasurementDataSourceMode SourceMode { get; set; } = MeasurementDataSourceMode.Draw;
        public string PointText1 { get; set; }
        public string PointText2 { get; set; }
        public MeasurementPointRole PointRole { get; set; } = MeasurementPointRole.Auto;
        public string RegionText1 { get; set; }
        public string RegionText2 { get; set; }
        public bool UsePositionCorrection { get; set; }
        public string CorrectionText1 { get; set; }
        public string CorrectionText2 { get; set; }
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;
        public float PointCenterX { get; set; } = 240;
        public float PointCenterY { get; set; } = 220;
        public float PointRadius { get; set; } = 50;
        public List<PointF> RegionPoints { get; set; } = new List<PointF>
        {
            new PointF(120, 120),
            new PointF(440, 120),
            new PointF(440, 340),
            new PointF(120, 340)
        };
        public float CaliperWidth { get; set; } = 10;
        public float CaliperHeight { get; set; } = 60;
        public int Count { get; set; } = 30;
        public int EdgeStrength { get; set; } = 20;
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        public int Direction { get; set; }
        /// <summary>获取或设置点圆卡尺的灰度剖面采样模式。</summary>
        public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
        public int BlurSize { get; set; } = 3;
    }
}
