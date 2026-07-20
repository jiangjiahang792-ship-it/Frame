using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    public class NodeResultLineLineAngle : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [DisplayName("夹角")]
        public double? Angle { get; set; }

        [DisplayName("交点X")]
        public double? IntersectionX { get; set; }

        [DisplayName("交点Y")]
        public double? IntersectionY { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的平均绝对垂直距离。
        /// </summary>
        [DisplayName("平均线距")]
        public double? AverageDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最小绝对垂直距离。
        /// </summary>
        [DisplayName("最小线距")]
        public double? MinDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最大绝对垂直距离。
        /// </summary>
        [DisplayName("最大线距")]
        public double? MaxDistance { get; set; }

        /// <summary>
        /// 参与线距统计的点数量。
        /// </summary>
        [DisplayName("距离点数")]
        public int DistancePointCount { get; set; }

        [DisplayName("直线1起点X")]
        public double? Line1StartX { get; set; }

        [DisplayName("直线1起点Y")]
        public double? Line1StartY { get; set; }

        [DisplayName("直线1终点X")]
        public double? Line1EndX { get; set; }

        [DisplayName("直线1终点Y")]
        public double? Line1EndY { get; set; }

        [DisplayName("直线2起点X")]
        public double? Line2StartX { get; set; }

        [DisplayName("直线2起点Y")]
        public double? Line2StartY { get; set; }

        [DisplayName("直线2终点X")]
        public double? Line2EndX { get; set; }

        [DisplayName("直线2终点Y")]
        public double? Line2EndY { get; set; }

        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
