using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    public class NodeResultResultOverlayDraw : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        [DisplayName("绘制结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();
    }
}
