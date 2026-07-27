using System.Collections.Generic;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    /// <summary>表示单个模板目标的线线夹角结果，失败时全部数值为零。</summary>
    public sealed class LineLineAngleTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置夹角。</summary>
        public double Angle { get; set; }
        /// <summary>获取或设置交点 X。</summary>
        public double IntersectionX { get; set; }
        /// <summary>获取或设置交点 Y。</summary>
        public double IntersectionY { get; set; }
        /// <summary>获取或设置平均线距。</summary>
        public double AverageDistance { get; set; }
        /// <summary>获取或设置最小线距。</summary>
        public double MinDistance { get; set; }
        /// <summary>获取或设置最大线距。</summary>
        public double MaxDistance { get; set; }
        /// <summary>获取或设置距离统计点数。</summary>
        public int DistancePointCount { get; set; }
        /// <summary>获取或设置直线1起点 X。</summary>
        public double Line1StartX { get; set; }
        /// <summary>获取或设置直线1起点 Y。</summary>
        public double Line1StartY { get; set; }
        /// <summary>获取或设置直线1终点 X。</summary>
        public double Line1EndX { get; set; }
        /// <summary>获取或设置直线1终点 Y。</summary>
        public double Line1EndY { get; set; }
        /// <summary>获取或设置直线2起点 X。</summary>
        public double Line2StartX { get; set; }
        /// <summary>获取或设置直线2起点 Y。</summary>
        public double Line2StartY { get; set; }
        /// <summary>获取或设置直线2终点 X。</summary>
        public double Line2EndX { get; set; }
        /// <summary>获取或设置直线2终点 Y。</summary>
        public double Line2EndY { get; set; }
        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
        /// <summary>获取或设置单目标原始结果，供叠加绘制复用。</summary>
        internal LineLineAngleMeasureResult RawResult { get; set; }
    }

    public class NodeResultLineLineAngle : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的夹角结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标线线夹角结果")]
        public List<LineLineAngleTargetResult> Items { get; set; } = new List<LineLineAngleTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("夹角")]
        public double? Angle { get; set; }

        [SubscriptionOutput]
        [DisplayName("交点X")]
        public double? IntersectionX { get; set; }

        [SubscriptionOutput]
        [DisplayName("交点Y")]
        public double? IntersectionY { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的平均绝对垂直距离。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("平均线距")]
        public double? AverageDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最小绝对垂直距离。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("最小线距")]
        public double? MinDistance { get; set; }

        /// <summary>
        /// 两条线点集到对向直线的最大绝对垂直距离。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("最大线距")]
        public double? MaxDistance { get; set; }

        /// <summary>
        /// 参与线距统计的点数量。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("距离点数")]
        public int DistancePointCount { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线1起点X")]
        public double? Line1StartX { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线1起点Y")]
        public double? Line1StartY { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线1终点X")]
        public double? Line1EndX { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线1终点Y")]
        public double? Line1EndY { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线2起点X")]
        public double? Line2StartX { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线2起点Y")]
        public double? Line2StartY { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线2终点X")]
        public double? Line2EndX { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线2终点Y")]
        public double? Line2EndY { get; set; }

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
