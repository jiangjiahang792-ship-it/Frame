using OpenCvSharp;
using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    internal class NodeResultMatchTemplate : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("匹配结果图像")]
        public Bitmap Bitmap { get; set; }

        [DisplayName("匹配的位置")]
        public Rect Rect { get; set; }

        [DisplayName("匹配得分")]
        public double Score { get; set; }

        [DisplayName("匹配是否OK")]
        public bool IsOk { get; set; }
    }
}
