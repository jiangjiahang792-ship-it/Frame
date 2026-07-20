using System.Collections.Generic;
using System.Drawing;

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    /// <summary>
    /// 找点工具运行参数。
    /// </summary>
    public class NodeParamFindPoint : INodeParam
    {
        /// <summary>
        /// 订阅图像节点文本。
        /// </summary>
        public string ImageText1 { get; set; }

        /// <summary>
        /// 订阅图像结果文本。
        /// </summary>
        public string ImageText2 { get; set; }

        /// <summary>
        /// 是否启用位置修正跟随。
        /// </summary>
        public bool UsePositionCorrection { get; set; }

        /// <summary>
        /// 订阅位置修正节点文本。
        /// </summary>
        public string CorrectionText1 { get; set; }

        /// <summary>
        /// 订阅位置修正结果文本。
        /// </summary>
        public string CorrectionText2 { get; set; }

        /// <summary>
        /// 基准坐标系下的已确认 ROI 区域。
        /// </summary>
        public List<List<PointF>> Regions { get; set; } = new List<List<PointF>>();

        /// <summary>
        /// Canny 低阈值。
        /// </summary>
        public int LowThreshold { get; set; } = 40;

        /// <summary>
        /// Canny 高阈值。
        /// </summary>
        public int HighThreshold { get; set; } = 120;

        /// <summary>
        /// 高斯平滑核大小。
        /// </summary>
        public int BlurSize { get; set; } = 3;

        /// <summary>
        /// 最小轮廓点数。
        /// </summary>
        public int MinContourPoints { get; set; } = 10;

        /// <summary>
        /// 轮廓点采样步长。
        /// </summary>
        public int SampleStep { get; set; } = 1;

        /// <summary>
        /// 最大输出点数，0 表示不限制。
        /// </summary>
        public int MaxPointCount { get; set; } = 5000;
    }
}
