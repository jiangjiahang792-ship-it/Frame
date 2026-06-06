using OpenCvSharp;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    /// <summary>
    /// 颜色识别节点运行结果，包含干净输出图像和结构化算法结果。
    /// </summary>
    public class NodeResultColorDiscern : INodeResult
    {
        /// <summary>
        /// 节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 输出图像，图像像素保持干净，ROI 和文本通过 DisplayResult 叠加显示。
        /// </summary>
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        /// <summary>
        /// 颜色识别结构化结果，供显示控件、结果汇总和通信模块订阅。
        /// </summary>
        [DisplayName("算法结果")]
        public NodetColorResult Result { get; set; } = new NodetColorResult();
    }

    /// <summary>
    /// 颜色识别显示结果，沿用标准 AlgorithmResult 的 ROI、文本和 OK/NG 字段。
    /// </summary>
    public class NodetColorResult : AlgorithmResult
    {
    }

    /// <summary>
    /// 识别结果对象
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// 颜色名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 识别到的区域。
        /// </summary>
        public Rect Region { get; set; }

        /// <summary>
        /// 匹配区域面积。
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// 匹配区域面积占比。
        /// </summary>
        public double AreaB { get; set; }

        /// <summary>
        /// 当前检测项是否合格。
        /// </summary>
        public bool IsOk { get; set; }

        /// <summary>
        /// 是否在参数界面预览中显示面积。
        /// </summary>
        public bool ShowArea { get; set; }

        /// <summary>
        /// 是否在参数界面预览中显示面积占比。
        /// </summary>
        public bool ShowRatio { get; set; }
    }
}
