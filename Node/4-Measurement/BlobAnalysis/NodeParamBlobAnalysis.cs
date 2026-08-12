namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    /// <summary>
    /// Blob输出区域筛选模式。
    /// </summary>
    public enum BlobOutputRegionMode
    {
        /// <summary>
        /// 输出所有满足灰度范围的区域。
        /// </summary>
        All,

        /// <summary>
        /// 仅输出面积最大的区域。
        /// </summary>
        Max,

        /// <summary>
        /// 仅输出面积最小的区域。
        /// </summary>
        Min
    }

    /// <summary>
    /// Blob分析工具参数，保存输入图像、可选检测区域和灰度阈值范围。
    /// </summary>
    public class NodeParamBlobAnalysis : INodeParam
    {
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

        /// <summary>
        /// 获取或设置感兴趣区域的最小灰度值。
        /// </summary>
        public int MinGray { get; set; }

        /// <summary>
        /// 获取或设置感兴趣区域的最大灰度值。
        /// </summary>
        public int MaxGray { get; set; } = 255;

        /// <summary>
        /// 获取或设置二值化后输出区域的筛选模式。
        /// </summary>
        public BlobOutputRegionMode OutputRegionMode { get; set; } = BlobOutputRegionMode.All;

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
}
