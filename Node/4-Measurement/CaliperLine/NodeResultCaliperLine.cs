using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    /// <summary>
    /// 表示单个模板目标的卡尺找线结果，失败时保留质量诊断并把几何测量值置零。
    /// </summary>
    public sealed class CaliperLineTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>获取或设置扫描得到的候选边缘点数量。</summary>
        public int CandidatePointCount { get; set; }

        /// <summary>获取或设置找到的边缘点数量。</summary>
        public int EdgePointCount { get; set; }

        /// <summary>获取或设置找到的边缘点集合。</summary>
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();

        /// <summary>获取或设置拟合线起点 X 坐标。</summary>
        public double StartX { get; set; }

        /// <summary>获取或设置拟合线起点 Y 坐标。</summary>
        public double StartY { get; set; }

        /// <summary>获取或设置拟合线终点 X 坐标。</summary>
        public double EndX { get; set; }

        /// <summary>获取或设置拟合线终点 Y 坐标。</summary>
        public double EndY { get; set; }

        /// <summary>获取或设置拟合线长度。</summary>
        public double Length { get; set; }

        /// <summary>获取或设置拟合线绝对角度。</summary>
        public double Angle { get; set; }

        /// <summary>获取或设置边缘点到拟合线的最大距离与最小距离差值。</summary>
        public double WarpageDifference { get; set; }

        /// <summary>获取或设置拟合内点占请求卡尺数量的比例。</summary>
        public double ValidPointRatio { get; set; }

        /// <summary>获取或设置内点到拟合线的平均垂直距离，单位为像素。</summary>
        public double AverageResidual { get; set; }

        /// <summary>获取或设置内点到拟合线的最大垂直距离，单位为像素。</summary>
        public double MaximumResidual { get; set; }

        /// <summary>获取或设置内点投影跨度占卡尺主轴长度的比例。</summary>
        public double CoverageRatio { get; set; }

        /// <summary>获取或设置拟合线相对卡尺主轴的最小方向偏差，单位为度。</summary>
        public double AngleDeviation { get; set; }

        /// <summary>获取或设置单目标算法耗时。</summary>
        public double AlgorithmMs { get; set; }
    }

    /// <summary>汇总卡尺找线的首目标订阅结果和全部多目标明细。</summary>
    public class NodeResultCaliperLine : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>获取或设置按模板目标顺序排列的找线结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标卡尺找线结果")]
        public List<CaliperLineTargetResult> Items { get; set; } = new List<CaliperLineTargetResult>();

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [SubscriptionOutput]
        [DisplayName("边缘点数量")]
        public int EdgePointCount { get; set; }

        /// <summary>获取或设置第一目标扫描得到的候选边缘点数量。</summary>
        [SubscriptionOutput]
        [DisplayName("候选点数量")]
        public int CandidatePointCount { get; set; }

        /// <summary>
        /// 卡尺实际找到的边缘点集合，供后续线组合拟合等图形创建工具复用。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.PointCollection, Multiplicity = SubscriptionValueMultiplicity.Collection, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("边缘点集合")]
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();

        [SubscriptionOutput]
        [DisplayName("起点X")]
        public double? StartX { get; set; }

        [SubscriptionOutput]
        [DisplayName("起点Y")]
        public double? StartY { get; set; }

        [SubscriptionOutput]
        [DisplayName("终点X")]
        public double? EndX { get; set; }

        [SubscriptionOutput]
        [DisplayName("终点Y")]
        public double? EndY { get; set; }

        [SubscriptionOutput]
        [DisplayName("长度")]
        public double? Length { get; set; }

        [SubscriptionOutput]
        [DisplayName("角度")]
        public double? Angle { get; set; }

        /// <summary>
        /// 卡尺边缘点到拟合线的最大垂直距离减最小垂直距离，用于表示翘曲度差值。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("翘曲度差值")]
        public double? WarpageDifference { get; set; }

        /// <summary>获取或设置第一目标拟合内点占请求卡尺数量的比例。</summary>
        [SubscriptionOutput]
        [DisplayName("有效点比例")]
        public double? ValidPointRatio { get; set; }

        /// <summary>获取或设置第一目标的平均拟合残差，单位为像素。</summary>
        [SubscriptionOutput]
        [DisplayName("平均拟合残差")]
        public double? AverageResidual { get; set; }

        /// <summary>获取或设置第一目标的最大拟合残差，单位为像素。</summary>
        [SubscriptionOutput]
        [DisplayName("最大拟合残差")]
        public double? MaximumResidual { get; set; }

        /// <summary>获取或设置第一目标的有效点覆盖率。</summary>
        [SubscriptionOutput]
        [DisplayName("有效点覆盖率")]
        public double? CoverageRatio { get; set; }

        /// <summary>获取或设置第一目标拟合线相对卡尺主轴的方向偏差。</summary>
        [SubscriptionOutput]
        [DisplayName("拟合方向偏差")]
        public double? AngleDeviation { get; set; }

        /// <summary>获取或设置第一目标失败时的明确原因。</summary>
        [SubscriptionOutput]
        [DisplayName("失败原因")]
        public string ErrorMessage { get; set; } = string.Empty;

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
