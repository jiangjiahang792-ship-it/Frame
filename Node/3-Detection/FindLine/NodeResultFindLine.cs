using OpenCvSharp;
using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._3_Detection.FindLine
{
    internal class NodeResultFindLine : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("直线结果")]
        public LineSegmentPoint Line { get; set; }

        [DisplayName("输出图像")]
        public Bitmap OutputImage { get; set; }
    }
}
