using System.Collections.Generic;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointPointDistance
{
    /// <summary>表示单个模板目标的点到点距离结果，失败时全部数值为零。</summary>
    public sealed class PointPointDistanceTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置两点距离。</summary>
        public double Distance { get; set; }
        /// <summary>获取或设置点1 X。</summary>
        public double Point1X { get; set; }
        /// <summary>获取或设置点1 Y。</summary>
        public double Point1Y { get; set; }
        /// <summary>获取或设置点2 X。</summary>
        public double Point2X { get; set; }
        /// <summary>获取或设置点2 Y。</summary>
        public double Point2Y { get; set; }
        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
        /// <summary>获取或设置单目标原始结果，供叠加绘制复用。</summary>
        internal PointPointDistanceMeasureResult RawResult { get; set; }
    }

    public class NodeResultPointPointDistance : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的点到点距离结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标点到点距离结果")]
        public List<PointPointDistanceTargetResult> Items { get; set; } = new List<PointPointDistanceTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("距离")]
        public double? Distance { get; set; }

        [SubscriptionOutput]
        [DisplayName("点1X")]
        public double? Point1X { get; set; }

        [SubscriptionOutput]
        [DisplayName("点1Y")]
        public double? Point1Y { get; set; }

        [SubscriptionOutput]
        [DisplayName("点2X")]
        public double? Point2X { get; set; }

        [SubscriptionOutput]
        [DisplayName("点2Y")]
        public double? Point2Y { get; set; }

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
