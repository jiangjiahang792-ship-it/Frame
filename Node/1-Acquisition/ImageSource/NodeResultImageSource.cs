using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeResultImageSource : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 相机采集到的图像
        /// </summary>
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

    }
    /// <summary>
    /// 节点输出图像数据，图像源会同时提供原图、常规图像列表和灰度图缓存。
    /// </summary>
    public class OutputImage 
    {
        /// <summary>
        /// 原图像
        /// </summary>
        public Mat SrcImg { get; set; } = new Mat();

        /// <summary>
        /// 灰度图缓存，供卡尺、测量等只读灰度算法直接订阅，避免每个节点重复整图转灰度。
        /// </summary>
        public Mat GrayImg { get; set; } = new Mat();

        /// <summary>
        /// 节点输出多张图像就存在图像列表；兼容旧节点时第一张通常也是原图。
        /// </summary>
        public List<Mat> Bitmaps { get; set; } = new List<Mat>();
        /// <summary>
        /// 截图一类节点会输出裁剪的图像相对原图的偏移量列表
        /// </summary>
        public List<Rect> Rectangles { get; set; } = new List<Rect>();

        /// <summary>
        /// 显示叠加层结果。图像数据保持干净，检测框、线、圆、文本等只在显示控件中绘制。
        /// </summary>
        public AlgorithmResult DisplayResult { get; set; } = new AlgorithmResult();
    }

}
