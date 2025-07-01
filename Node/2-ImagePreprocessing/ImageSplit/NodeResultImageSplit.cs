using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImageSplit
{
    internal class NodeResultImageSplit : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 拆分图片集合
        /// </summary>
        [DisplayName("拆分图像集合")]
        public List<Bitmap> Bitmaps { get; set; }
    }
}
