using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    /// <summary>
    /// 表示单个模板目标的找点结果，失败时所有数值和集合保持为零或空。
    /// </summary>
    public sealed class FindPointTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置边缘点数量。</summary>
        public int PointCount { get; set; }

        /// <summary>获取或设置轮廓数量。</summary>
        public int ContourCount { get; set; }

        /// <summary>获取或设置边缘点中心 X 坐标。</summary>
        public double CenterX { get; set; }

        /// <summary>获取或设置边缘点中心 Y 坐标。</summary>
        public double CenterY { get; set; }

        /// <summary>获取或设置展开到当前目标后的搜索区域。</summary>
        public List<List<PointF>> Regions { get; set; } = new List<List<PointF>>();

        /// <summary>获取或设置找到的边缘点集合。</summary>
        public List<PointF> Points { get; set; } = new List<PointF>();

        /// <summary>获取或设置主轮廓点集合。</summary>
        public List<PointF> RegionPoints { get; set; } = new List<PointF>();

        /// <summary>获取或设置全部轮廓集合。</summary>
        public List<List<PointF>> Contours { get; set; } = new List<List<PointF>>();

        /// <summary>获取或设置单目标结果信息。</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }

        /// <summary>获取或设置实际处理的区域数量。</summary>
        public int ProcessedRegionCount { get; set; }

        /// <summary>获取或设置实际处理的像素数量。</summary>
        public long ProcessedPixelCount { get; set; }
    }

    public class NodeResultFindPoint : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的找点结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标找点结果")]
        public List<FindPointTargetResult> Items { get; set; } = new List<FindPointTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("边缘点数量")]
        public int PointCount { get; set; }

        [SubscriptionOutput]
        [DisplayName("轮廓数量")]
        public int ContourCount { get; set; }

        [SubscriptionOutput]
        [DisplayName("中心X")]
        public double? CenterX { get; set; }

        [SubscriptionOutput]
        [DisplayName("中心Y")]
        public double? CenterY { get; set; }

        [SubscriptionOutput(SubscriptionDataCategory.PointCollection, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("点集合")]
        public List<PointF> Points { get; set; } = new List<PointF>();

        [SubscriptionOutput(SubscriptionDataCategory.Region, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("区域点集")]
        public List<PointF> RegionPoints { get; set; } = new List<PointF>();

        [SubscriptionOutput(SubscriptionDataCategory.Contour, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("轮廓集合")]
        public List<List<PointF>> Contours { get; set; } = new List<List<PointF>>();

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("信息")]
        public string Message { get; set; }

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
