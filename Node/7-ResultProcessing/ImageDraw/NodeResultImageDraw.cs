using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    internal class NodeResultImageDraw : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("结果图像")]
        public Bitmap RenderImage { get; set; }
    }
}
