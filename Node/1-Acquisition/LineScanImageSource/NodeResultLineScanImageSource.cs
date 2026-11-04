using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._1_Acquisition.LineScanImageSource
{
    /// <summary>线扫图像源运行结果。</summary>
    public sealed class NodeResultLineScanImageSource : INodeResult
    {
        /// <summary>获取或设置运行耗时。</summary>
        public int RunTime { get; set; }

        /// <summary>获取或设置完整线扫图像。</summary>
        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
