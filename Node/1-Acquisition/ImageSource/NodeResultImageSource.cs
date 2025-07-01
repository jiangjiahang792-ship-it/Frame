using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    internal class NodeResultImageSource : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 相机采集到的图像
        /// </summary>
        [DisplayName("图像")]
        public Bitmap Bitmap { get; set; }
        /// <summary>
        /// 文件夹所有的图像
        /// </summary>
        [DisplayName("图像集合")]
        public List<Bitmap> Bitmaps { get; set; }

    }
}
