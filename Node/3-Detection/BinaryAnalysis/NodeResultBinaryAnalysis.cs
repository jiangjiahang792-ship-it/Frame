using OpenCvSharp;
using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    public class NodeResultBinaryAnalysis : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        [DisplayName("算法结果")]
        public NodeResultBinaryAnalysisResult Result { get; set; } = new NodeResultBinaryAnalysisResult(); 
    }

    public class NodeResultBinaryAnalysisResult : AlgorithmResult
    {
        public bool IsAllOk;
    }

    /// <summary>
    /// 区域分析详细结果
    /// </summary>
    public class RegionResult
    {
        public string Name { get; set; }
        public bool IsPass { get; set; }
        public RotatedRect SearchRegion { get; set; }
        public Rect DetectedRect { get; set; }
        public double Area { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
