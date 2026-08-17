using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    /// <summary>保存卡尺找线的输入订阅、几何、采样和拟合质量参数。</summary>
    public class NodeParamCaliperLine : INodeParam
    {
        /// <summary>获取或设置输入图像节点名称。</summary>
        public string Text1 { get; set; }
        /// <summary>获取或设置输入图像输出端口名称。</summary>
        public string Text2 { get; set; }
        /// <summary>获取或设置是否使用位置修正。</summary>
        public bool UsePositionCorrection { get; set; }
        /// <summary>获取或设置位置修正节点名称。</summary>
        public string CorrectionText1 { get; set; }
        /// <summary>获取或设置位置修正输出端口名称。</summary>
        public string CorrectionText2 { get; set; }
        /// <summary>获取或设置基准卡尺主轴起点 X 坐标。</summary>
        public float StartX { get; set; } = 100;
        /// <summary>获取或设置基准卡尺主轴起点 Y 坐标。</summary>
        public float StartY { get; set; } = 100;
        /// <summary>获取或设置基准卡尺主轴终点 X 坐标。</summary>
        public float EndX { get; set; } = 400;
        /// <summary>获取或设置基准卡尺主轴终点 Y 坐标。</summary>
        public float EndY { get; set; } = 100;
        /// <summary>获取或设置单把卡尺沿主轴方向的平均宽度。</summary>
        public float CaliperWidth { get; set; } = 20;
        /// <summary>获取或设置单把卡尺沿扫描方向的高度。</summary>
        public float CaliperHeight { get; set; } = 120;
        /// <summary>获取或设置沿主轴布置的卡尺数量。</summary>
        public int Count { get; set; } = 15;
        /// <summary>获取或设置边缘强度阈值。</summary>
        public int EdgeStrength { get; set; } = 20;
        /// <summary>获取或设置边缘极性。</summary>
        public CaliperEdgePolarity Polarity { get; set; } = CaliperEdgePolarity.Both;
        /// <summary>获取或设置候选边缘选择模式。</summary>
        public CaliperEdgeFindMode FindMode { get; set; } = CaliperEdgeFindMode.Best;
        /// <summary>获取或设置卡尺灰度剖面采样模式。</summary>
        public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
        /// <summary>获取或设置边缘定位精度，默认使用亚像素插值。</summary>
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;
        /// <summary>获取或设置扫描方向，零表示垂直主轴。</summary>
        public int Direction { get; set; }
        /// <summary>获取或设置一维灰度剖面平滑核大小。</summary>
        public int BlurSize { get; set; } = 3;
        /// <summary>获取或设置是否启用直线拟合质量判定。</summary>
        public bool EnableQualityValidation { get; set; } = true;
        /// <summary>获取或设置内点数量占请求卡尺数量的最低比例。</summary>
        public float MinimumValidPointRatio { get; set; } = 0.8f;
        /// <summary>获取或设置平均拟合残差上限，单位为像素。</summary>
        public float MaximumAverageResidual { get; set; } = 1.5f;
        /// <summary>获取或设置最大拟合残差上限，单位为像素。</summary>
        public float MaximumResidual { get; set; } = 3.5f;
        /// <summary>获取或设置内点覆盖卡尺主轴的最低比例。</summary>
        public float MinimumCoverageRatio { get; set; } = 0.75f;
        /// <summary>获取或设置拟合线相对卡尺主轴的最大方向偏差，单位为度。</summary>
        public float MaximumAngleDeviationDegrees { get; set; } = 5.0f;
    }
}
