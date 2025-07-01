using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageCrop
{
    internal class NodeResultImageCrop : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("裁切图像")]
        public Bitmap Image { get; set; }

        [DisplayName("裁切矩形")]
        public Rectangle Rectangle { get; set; }
    }
}
