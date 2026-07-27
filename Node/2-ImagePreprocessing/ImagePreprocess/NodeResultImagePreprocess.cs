using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    /// <summary>
    /// 图像预处理节点结果。
    /// </summary>
    public class NodeResultImagePreprocess : INodeResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 预处理后的输出图像。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("预处理图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        /// <summary>
        /// 本次运行使用的预处理模式。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("处理模式")]
        public string ModeName { get; set; }
    }
}
