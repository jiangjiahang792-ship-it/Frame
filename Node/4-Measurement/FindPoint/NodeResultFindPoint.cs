using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    public class NodeResultFindPoint : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [DisplayName("边缘点数量")]
        public int PointCount { get; set; }

        [DisplayName("轮廓数量")]
        public int ContourCount { get; set; }

        [DisplayName("中心X")]
        public double? CenterX { get; set; }

        [DisplayName("中心Y")]
        public double? CenterY { get; set; }

        [DisplayName("点集合")]
        public List<PointF> Points { get; set; } = new List<PointF>();

        [DisplayName("区域点集")]
        public List<PointF> RegionPoints { get; set; } = new List<PointF>();

        [DisplayName("轮廓集合")]
        public List<List<PointF>> Contours { get; set; } = new List<List<PointF>>();

        [DisplayName("信息")]
        public string Message { get; set; }

        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
