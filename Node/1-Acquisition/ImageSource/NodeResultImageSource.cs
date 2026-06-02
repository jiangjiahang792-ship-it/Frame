using System.Collections.Generic;
using System.ComponentModel;
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

        /// <summary>
        /// 使用单张图像构建标准输出，兼容旧节点的 Bitmaps[0]，并尽量复用已有灰度图缓存。
        /// </summary>
        /// <param name="source">输出图像。</param>
        /// <param name="grayImage">可复用的灰度图缓存。</param>
        /// <returns>标准图像输出对象。</returns>
        public static OutputImage FromSingleImage(Mat source, Mat grayImage = null)
        {
            return new OutputImage
            {
                SrcImg = source ?? new Mat(),
                Bitmaps = new List<Mat> { source },
                GrayImg = HasValidImage(grayImage) ? grayImage : BuildGrayImage(source)
            };
        }

        /// <summary>
        /// 判断图像对象是否可读。
        /// </summary>
        /// <param name="image">待检查的图像。</param>
        /// <returns>图像非空且有有效像素时返回 true。</returns>
        public static bool HasValidImage(Mat image)
        {
            return image != null && !image.Empty();
        }

        /// <summary>
        /// 构建灰度图缓存；单通道图像直接复用原图引用，彩色图像只转换一次。
        /// </summary>
        /// <param name="source">源图像。</param>
        /// <returns>灰度图缓存。</returns>
        public static Mat BuildGrayImage(Mat source)
        {
            if (!HasValidImage(source))
                return new Mat();

            int channels = source.Channels();
            if (channels == 1)
                return source;

            Mat gray = new Mat();
            if (channels == 4)
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
            else
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

            return gray;
        }
    }

}
