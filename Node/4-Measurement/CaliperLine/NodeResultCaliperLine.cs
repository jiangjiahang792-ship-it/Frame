using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    public class NodeResultCaliperLine : INodeResult, IJudgmentResult
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
        public int EdgePointCount { get; set; }

        /// <summary>
        /// 卡尺实际找到的边缘点集合，供后续线组合拟合等图形创建工具复用。
        /// </summary>
        [DisplayName("边缘点集合")]
        public List<PointF> EdgePoints { get; set; } = new List<PointF>();

        [DisplayName("起点X")]
        public double? StartX { get; set; }

        [DisplayName("起点Y")]
        public double? StartY { get; set; }

        [DisplayName("终点X")]
        public double? EndX { get; set; }

        [DisplayName("终点Y")]
        public double? EndY { get; set; }

        [DisplayName("长度")]
        public double? Length { get; set; }

        [DisplayName("角度")]
        public double? Angle { get; set; }

        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
