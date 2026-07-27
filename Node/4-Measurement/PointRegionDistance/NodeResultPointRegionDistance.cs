using System.Collections.Generic;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointRegionDistance
{
    /// <summary>表示单个模板目标的点到区域距离结果，失败时全部数值为零。</summary>
    public sealed class PointRegionDistanceTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置最小距离。</summary>
        public double MinDistance { get; set; }
        /// <summary>获取或设置最大距离。</summary>
        public double MaxDistance { get; set; }
        /// <summary>获取或设置目标点是否位于区域内。</summary>
        public bool IsInsideRegion { get; set; }
        /// <summary>获取或设置目标点 X。</summary>
        public double TargetX { get; set; }
        /// <summary>获取或设置目标点 Y。</summary>
        public double TargetY { get; set; }
        /// <summary>获取或设置最近点 X。</summary>
        public double NearestX { get; set; }
        /// <summary>获取或设置最近点 Y。</summary>
        public double NearestY { get; set; }
        /// <summary>获取或设置最远点 X。</summary>
        public double FarthestX { get; set; }
        /// <summary>获取或设置最远点 Y。</summary>
        public double FarthestY { get; set; }
        /// <summary>获取或设置区域点数。</summary>
        public int RegionPointCount { get; set; }
        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
        /// <summary>获取或设置单目标原始结果，供叠加绘制复用。</summary>
        internal PointRegionDistanceMeasureResult RawResult { get; set; }
    }

    public class NodeResultPointRegionDistance : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的点到区域距离结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标点到区域距离结果")]
        public List<PointRegionDistanceTargetResult> Items { get; set; } = new List<PointRegionDistanceTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("最小距离")]
        public double? MinDistance { get; set; }

        [SubscriptionOutput]
        [DisplayName("最大距离")]
        public double? MaxDistance { get; set; }

        [SubscriptionOutput]
        [DisplayName("点在区域内")]
        public bool IsInsideRegion { get; set; }

        [SubscriptionOutput]
        [DisplayName("目标点X")]
        public double? TargetX { get; set; }

        [SubscriptionOutput]
        [DisplayName("目标点Y")]
        public double? TargetY { get; set; }

        [SubscriptionOutput]
        [DisplayName("最近点X")]
        public double? NearestX { get; set; }

        [SubscriptionOutput]
        [DisplayName("最近点Y")]
        public double? NearestY { get; set; }

        [SubscriptionOutput]
        [DisplayName("最远点X")]
        public double? FarthestX { get; set; }

        [SubscriptionOutput]
        [DisplayName("最远点Y")]
        public double? FarthestY { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("区域点数")]
        public int RegionPointCount { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
