using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperCircle
{
    /// <summary>
    /// 表示单个模板目标的卡尺找圆结果，失败时数值保持为零。
    /// </summary>
    public sealed class CaliperCircleTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置找到的边缘点数量。</summary>
        public int EdgePointCount { get; set; }

        /// <summary>获取或设置找到的边缘点集合。</summary>
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();

        /// <summary>获取或设置圆心 X 坐标。</summary>
        public double CenterX { get; set; }

        /// <summary>获取或设置圆心 Y 坐标。</summary>
        public double CenterY { get; set; }

        /// <summary>获取或设置半径。</summary>
        public double Radius { get; set; }

        /// <summary>获取或设置直径。</summary>
        public double Diameter { get; set; }

        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
    }

    public class NodeResultCaliperCircle : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的找圆结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标卡尺找圆结果")]
        public List<CaliperCircleTargetResult> Items { get; set; } = new List<CaliperCircleTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("边缘点数量")]
        public int EdgePointCount { get; set; }

        [SubscriptionOutput]
        [DisplayName("圆心X")]
        public double? CenterX { get; set; }

        [SubscriptionOutput]
        [DisplayName("圆心Y")]
        public double? CenterY { get; set; }

        [SubscriptionOutput]
        [DisplayName("半径")]
        public double? Radius { get; set; }

        [SubscriptionOutput]
        [DisplayName("直径")]
        public double? Diameter { get; set; }

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
