using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.CaliperEllipse
{
    public class NodeResultCaliperEllipse : INodeResult, IJudgmentResult
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

        [DisplayName("中心X")]
        public double? CenterX { get; set; }

        [DisplayName("中心Y")]
        public double? CenterY { get; set; }

        [DisplayName("宽度")]
        public double? Width { get; set; }

        [DisplayName("高度")]
        public double? Height { get; set; }

        [DisplayName("长轴")]
        public double? MajorAxis { get; set; }

        [DisplayName("短轴")]
        public double? MinorAxis { get; set; }

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
