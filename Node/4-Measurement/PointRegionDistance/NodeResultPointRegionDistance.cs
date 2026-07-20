using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.PointRegionDistance
{
    public class NodeResultPointRegionDistance : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [DisplayName("最小距离")]
        public double? MinDistance { get; set; }

        [DisplayName("最大距离")]
        public double? MaxDistance { get; set; }

        [DisplayName("点在区域内")]
        public bool IsInsideRegion { get; set; }

        [DisplayName("目标点X")]
        public double? TargetX { get; set; }

        [DisplayName("目标点Y")]
        public double? TargetY { get; set; }

        [DisplayName("最近点X")]
        public double? NearestX { get; set; }

        [DisplayName("最近点Y")]
        public double? NearestY { get; set; }

        [DisplayName("最远点X")]
        public double? FarthestX { get; set; }

        [DisplayName("最远点Y")]
        public double? FarthestY { get; set; }

        [DisplayName("区域点数")]
        public int RegionPointCount { get; set; }

        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
