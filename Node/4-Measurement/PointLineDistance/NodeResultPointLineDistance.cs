using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    /// <summary>
    /// 点到线距离测量结果。
    /// </summary>
    public class NodeResultPointLineDistance : INodeResult, IJudgmentResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 测量是否成功。
        /// </summary>
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 点到线垂直距离。
        /// </summary>
        [DisplayName("点到线距离")]
        public double? Distance { get; set; }

        /// <summary>
        /// 目标点 X。
        /// </summary>
        [DisplayName("目标点X")]
        public double? PointX { get; set; }

        /// <summary>
        /// 目标点 Y。
        /// </summary>
        [DisplayName("目标点Y")]
        public double? PointY { get; set; }

        /// <summary>
        /// 直线起点 X。
        /// </summary>
        [DisplayName("直线起点X")]
        public double? StartX { get; set; }

        /// <summary>
        /// 直线起点 Y。
        /// </summary>
        [DisplayName("直线起点Y")]
        public double? StartY { get; set; }

        /// <summary>
        /// 直线终点 X。
        /// </summary>
        [DisplayName("直线终点X")]
        public double? EndX { get; set; }

        /// <summary>
        /// 直线终点 Y。
        /// </summary>
        [DisplayName("直线终点Y")]
        public double? EndY { get; set; }

        /// <summary>
        /// 垂足 X。
        /// </summary>
        [DisplayName("垂足X")]
        public double? FootX { get; set; }

        /// <summary>
        /// 垂足 Y。
        /// </summary>
        [DisplayName("垂足Y")]
        public double? FootY { get; set; }

        /// <summary>
        /// 算法耗时。
        /// </summary>
        [DisplayName("算法耗时")]
        public double AlgorithmMs { get; set; }

        /// <summary>
        /// 叠加绘制结果。
        /// </summary>
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 输出图像。
        /// </summary>
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();
    }
}
