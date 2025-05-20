using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    internal class NodeResultImageDraw : INodeResult
    {
        [DisplayName("运行状态")]
        public NodeStatus Status { get; set; }

        [DisplayName("节点耗时")]
        public long RunTime { get; set; }

        [DisplayName("运行状态码")]
        public NodeRunStatusCode RunStatusCode { get; set; }

        [DisplayName("结果图像")]
        public Bitmap RenderImage { get; set; }
    }
}
