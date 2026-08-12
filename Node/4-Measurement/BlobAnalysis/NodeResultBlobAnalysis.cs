using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具结果，输出二值化后的灰度图、面积和显示叠加信息。
    /// </summary>
    public class NodeResultBlobAnalysis : INodeResult, IJudgmentResult
    {
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
