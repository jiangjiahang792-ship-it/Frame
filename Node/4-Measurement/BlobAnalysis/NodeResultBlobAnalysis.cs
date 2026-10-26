using System.ComponentModel;
using System.Collections.Generic;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具结果，输出二值化后的灰度图、面积和显示叠加信息。
    /// </summary>
    public class NodeResultBlobAnalysis : INodeResult, IJudgmentResult, IMultiTargetMeasurementItem, INodeResultResourceOwner
    {

        /// <summary>按输入定位顺序保留全部目标结果，失败目标也占位。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget)]
        [DisplayName("多目标检测结果")]
        public List<NodeResultBlobAnalysis> Items { get; set; } = new List<NodeResultBlobAnalysis>();
        /// <summary>目标序号；节点汇总结果为零。</summary>
        public int TargetIndex { get; set; }
        /// <summary>目标失败原因。</summary>
        [SubscriptionOutput, DisplayName("失败原因")]
        public string ErrorMessage { get; set; }
        /// <summary>当前目标的位置修正信息。</summary>
        public PositionCorrectionInfo Correction { get; set; }
        /// <summary>释放汇总及每个目标独立拥有的图像，支持重复释放。</summary>
        public void ReleaseResources()
        {
            OutputImage?.Dispose();
            foreach (var item in Items) item.ReleaseResources();
        }
        /// <summary>
        /// 获取或设置节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 获取或设置本次Blob分析是否成功。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 获取或设置后续条件节点回写后的判定状态。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 获取或设置二值化后感兴趣灰度区域的像素面积。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("面积")]
        public double Area { get; set; }
        /// <summary>面积过滤及输出模式选择后保留的八连通区域数量，不包含背景。</summary>
        [SubscriptionOutput]
        [DisplayName("区域数量")]
        public int Count { get; set; }

        /// <summary>
        /// 获取或设置二值结果外接区域的宽度。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("宽度")]
        public double Width { get; set; }

        /// <summary>
        /// 获取或设置二值结果外接区域的高度。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("高度")]
        public double Height { get; set; }

        /// <summary>
        /// 获取或设置当前检测区域的旋转矩形，供下游绘制工具订阅为矩形框。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.Rectangle, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("检测区域")]
        public ColorRotatedRect DetectionRegion { get; set; }

        /// <summary>全部位置修正后的检测区域；原单区域输出保留第一个区域以兼容旧订阅。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.Rectangle, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("检测区域列表")]
        public System.Collections.Generic.List<ColorRotatedRect> DetectionRegions { get; set; }

        /// <summary>
        /// 获取或设置二值化处理后的灰度图。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("二值化处理后的灰度图")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        /// <summary>
        /// 获取或设置用于界面叠加显示的算法结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();
    }
}
