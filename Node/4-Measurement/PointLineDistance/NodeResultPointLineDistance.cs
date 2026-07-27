using System.Collections.Generic;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    /// <summary>表示单个模板目标的点到线距离结果，失败时全部数值为零。</summary>
    public sealed class PointLineDistanceTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置点到线距离。</summary>
        public double Distance { get; set; }
        /// <summary>获取或设置目标点 X。</summary>
        public double PointX { get; set; }
        /// <summary>获取或设置目标点 Y。</summary>
        public double PointY { get; set; }
        /// <summary>获取或设置直线起点 X。</summary>
        public double StartX { get; set; }
        /// <summary>获取或设置直线起点 Y。</summary>
        public double StartY { get; set; }
        /// <summary>获取或设置直线终点 X。</summary>
        public double EndX { get; set; }
        /// <summary>获取或设置直线终点 Y。</summary>
        public double EndY { get; set; }
        /// <summary>获取或设置垂足 X。</summary>
        public double FootX { get; set; }
        /// <summary>获取或设置垂足 Y。</summary>
        public double FootY { get; set; }
        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
        /// <summary>获取或设置单目标原始结果，供叠加绘制复用。</summary>
        internal PointLineDistanceMeasureResult RawResult { get; set; }
    }

    /// <summary>
    /// 点到线距离测量结果。
    /// </summary>
    public class NodeResultPointLineDistance : INodeResult, IJudgmentResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 测量是否成功。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的点到线距离结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标点到线距离结果")]
        public List<PointLineDistanceTargetResult> Items { get; set; } = new List<PointLineDistanceTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 点到线垂直距离。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("点到线距离")]
        public double? Distance { get; set; }

        /// <summary>
        /// 目标点 X。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("目标点X")]
        public double? PointX { get; set; }

        /// <summary>
        /// 目标点 Y。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("目标点Y")]
        public double? PointY { get; set; }

        /// <summary>
        /// 直线起点 X。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线起点X")]
        public double? StartX { get; set; }

        /// <summary>
        /// 直线起点 Y。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线起点Y")]
        public double? StartY { get; set; }

        /// <summary>
        /// 直线终点 X。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线终点X")]
        public double? EndX { get; set; }

        /// <summary>
        /// 直线终点 Y。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("直线终点Y")]
        public double? EndY { get; set; }

        /// <summary>
        /// 垂足 X。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("垂足X")]
        public double? FootX { get; set; }

        /// <summary>
        /// 垂足 Y。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("垂足Y")]
        public double? FootY { get; set; }

        /// <summary>
        /// 算法耗时。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        /// <summary>
        /// 叠加绘制结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 输出图像。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
