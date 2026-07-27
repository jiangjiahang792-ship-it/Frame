using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    public class NodeResultBatteryEar : INodeResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        [SubscriptionOutput]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }
    }
}
