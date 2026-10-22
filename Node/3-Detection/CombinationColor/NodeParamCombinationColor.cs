namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>
    /// 颜色输出区域筛选模式。
    /// </summary>
    public enum ColorOutputRegionMode
    {
        /// <summary>
        /// 输出所有满足颜色范围及面积阈值的区域。
        /// </summary>
        All,

        /// <summary>
        /// 仅输出面积最大的区域。
        /// </summary>
        Max
    }

    /// <summary>
    /// 组合颜色识别参数，保存输入图像、可选检测区域和学习颜色和面积阈值。
    /// </summary>
    public class NodeParamCombinationColor : INodeParam
    {
        /// <summary>学习的四种颜色空间模型，保存方案后可直接恢复。</summary>
        public System.Collections.Generic.List<GuideColorRangeModel> Models { get; set; } = new System.Collections.Generic.List<GuideColorRangeModel>();
        /// <summary>识别使用的颜色空间。</summary>
        public string ColorSpace { get; set; } = "HSV";
        /// <summary>独立的颜色采样框，只用于学习，不参与搜索区域和位置跟随。</summary>
        public ColorDetectionRegion ColorLearningRegion { get; set; }

        /// <summary>仅用于读取早期版本方案的区域列表，实际只采用第一个区域；新保存方案使用单区域字段。</summary>
        public System.Collections.Generic.List<ColorDetectionRegion> Regions { get; set; }
        /// <summary>兼容早期列表数据，但始终只返回一个搜索区域。</summary>
        internal NodeParamCombinationColor GetSearchRegion()
        {
            return Regions != null && Regions.Count > 0 && Regions[0] != null ? Regions[0].ToParameters() : this;
        }
        /// <summary>
        /// 获取或设置输入图像订阅的节点名称。
        /// </summary>
        public string ImageText1 { get; set; }

        /// <summary>
        /// 获取或设置输入图像订阅的结果名称。
        /// </summary>
        public string ImageText2 { get; set; }

        /// <summary>
        /// 获取或设置是否启用位置修正输出的检测区域。
        /// </summary>
        public bool EnableDetectionRegion { get; set; }

        /// <summary>
        /// 获取或设置位置修正订阅的节点名称。
        /// </summary>
        public string CorrectionText1 { get; set; }

        /// <summary>
        /// 获取或设置位置修正订阅的结果名称。
        /// </summary>
        public string CorrectionText2 { get; set; }

        /// <summary>绘制ROI时的位置修正快照；空值兼容旧方案以位置修正原始基准保存ROI的行为。</summary>
        public TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo RegionCorrectionReference { get; set; }

        /// <summary>
        /// 获取或设置连通区域最小像素面积（等于此值保留）。
        /// </summary>
        public int MinimumArea { get; set; }

        /// <summary>
        /// 获取或设置颜色范围灵敏度，50 为参考算法默认值。
        /// </summary>
        public decimal Sensitivity { get; set; } = 50;

        /// <summary>
        /// 获取或设置二值化后输出区域的筛选模式。
        /// </summary>
        public ColorOutputRegionMode OutputRegionMode { get; set; } = ColorOutputRegionMode.All;

        /// <summary>
        /// 获取或设置检测区域中心行坐标。
        /// </summary>
        public float RegionRow { get; set; } = 200;

        /// <summary>
        /// 获取或设置检测区域中心列坐标。
        /// </summary>
        public float RegionColumn { get; set; } = 200;

        /// <summary>
        /// 获取或设置检测区域角度，单位为弧度。
        /// </summary>
        public float RegionPhi { get; set; }

        /// <summary>
        /// 获取或设置检测区域半宽。
        /// </summary>
        public float RegionLength1 { get; set; } = 100;

        /// <summary>
        /// 获取或设置检测区域半高。
        /// </summary>
        public float RegionLength2 { get; set; } = 100;
    }

    /// <summary>独立检测区域，保存绘制坐标和该区域的位置修正基准。</summary>
    public class ColorDetectionRegion
    {
        /// <summary>中心行坐标。</summary>
        public float Row { get; set; }
        /// <summary>中心列坐标。</summary>
        public float Column { get; set; }
        /// <summary>角度，单位弧度。</summary>
        public float Phi { get; set; }
        /// <summary>半宽。</summary>
        public float Length1 { get; set; }
        /// <summary>半高。</summary>
        public float Length2 { get; set; }
        /// <summary>绘制时的位置修正快照。</summary>
        public TDJS_Vision.Node._4_Measurement.Common.PositionCorrectionInfo CorrectionReference { get; set; }

        /// <summary>转换为单区域几何参数，复用已有坐标修正算法。</summary>
        public NodeParamCombinationColor ToParameters()
        {
            return new NodeParamCombinationColor { RegionRow = Row, RegionColumn = Column, RegionPhi = Phi,
                RegionLength1 = Length1, RegionLength2 = Length2, RegionCorrectionReference = CorrectionReference };
        }

        /// <summary>从单区域参数创建区域副本，兼容已有方案。</summary>
        public static ColorDetectionRegion FromParameters(NodeParamCombinationColor param)
        {
            return new ColorDetectionRegion { Row = param.RegionRow, Column = param.RegionColumn, Phi = param.RegionPhi,
                Length1 = param.RegionLength1, Length2 = param.RegionLength2, CorrectionReference = param.RegionCorrectionReference };
        }
    }
}
