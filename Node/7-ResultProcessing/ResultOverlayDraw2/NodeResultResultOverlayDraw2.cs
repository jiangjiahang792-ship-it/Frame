using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// ROI结果绘制2节点运行结果。
    /// </summary>
    public class NodeResultResultOverlayDraw2 : INodeResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 带有显示层的输出图像。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        /// <summary>
        /// 当前节点生成的绘制结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("绘制结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 多条颜色规则聚合后的最终判定结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("颜色判定")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 颜色规则运行明细，便于下游显示或排查。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("判定明细")]
        public string RuleDiagnostics { get; set; }
    }
}
