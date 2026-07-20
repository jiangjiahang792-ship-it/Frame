using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    /// <summary>
    /// 线组合拟合模式。
    /// </summary>
    public enum LineMergeFitMode
    {
        /// <summary>
        /// 将两条近似共线的线拟合成一条覆盖两段范围的新线段。
        /// </summary>
        CollinearMerge,

        /// <summary>
        /// 将两条近似平行的线拟合成位于中间的基准线段。
        /// </summary>
        ParallelCenterLine
    }

    /// <summary>
    /// 线组合拟合节点参数。
    /// </summary>
    public class NodeParamLineMergeFit : INodeParam
    {
        /// <summary>
        /// 第一条订阅线所属节点文本。
        /// </summary>
        public string Line1Text1 { get; set; }

        /// <summary>
        /// 第一条订阅线结果文本，当前线订阅隐藏结果选择但保留字段用于兼容。
        /// </summary>
        public string Line1Text2 { get; set; }

        /// <summary>
        /// 第二条订阅线所属节点文本。
        /// </summary>
        public string Line2Text1 { get; set; }

        /// <summary>
        /// 第二条订阅线结果文本，当前线订阅隐藏结果选择但保留字段用于兼容。
        /// </summary>
        public string Line2Text2 { get; set; }

        /// <summary>
        /// 组合拟合模式。
        /// </summary>
        public LineMergeFitMode FitMode { get; set; } = LineMergeFitMode.CollinearMerge;

        /// <summary>
        /// 坐标计算精度模式。
        /// </summary>
        public GeometryMeasureMode MeasureMode { get; set; } = GeometryMeasureMode.SubPixel;

        /// <summary>
        /// 优先使用上游卡尺边缘点重新拟合；不可用时退化为线段端点拟合。
        /// </summary>
        public bool PreferEdgePoints { get; set; } = true;
    }
}
