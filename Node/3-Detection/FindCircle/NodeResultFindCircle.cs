using OpenCvSharp;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.FindCircle
{
    internal class NodeResultFindCircle : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("找到的圆")]
        public CircleSegment Circle { get; set; }

        [DisplayName("输出图像")]
        public Bitmap OutputImage { get; set; }

        [DisplayName("算法结果")]
        public ResultViewData Result { get; set; }
    }
}
