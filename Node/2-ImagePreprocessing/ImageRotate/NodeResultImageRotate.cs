using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate
{
    internal class NodeResultImageRotate : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 旋转后的图片
        /// </summary>
        [DisplayName("旋转后图像")]
        public Bitmap Image { get; set; }
    }
}
