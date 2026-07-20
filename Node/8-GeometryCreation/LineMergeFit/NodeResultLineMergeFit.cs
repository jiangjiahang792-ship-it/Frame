using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._8_GeometryCreation.LineMergeFit
{
    /// <summary>
    /// 线组合拟合节点结果。
    /// </summary>
    public class NodeResultLineMergeFit : INodeResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 组合拟合是否成功。
        /// </summary>
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 输出线段起点 X。
        /// </summary>
        [DisplayName("起点X")]
        public double? StartX { get; set; }

        /// <summary>
        /// 输出线段起点 Y。
        /// </summary>
        [DisplayName("起点Y")]
        public double? StartY { get; set; }

        /// <summary>
        /// 输出线段终点 X。
        /// </summary>
        [DisplayName("终点X")]
        public double? EndX { get; set; }

        /// <summary>
        /// 输出线段终点 Y。
        /// </summary>
        [DisplayName("终点Y")]
        public double? EndY { get; set; }

        /// <summary>
        /// 输出线段中心 X。
        /// </summary>
        [DisplayName("中心X")]
        public double? CenterX { get; set; }

        /// <summary>
        /// 输出线段中心 Y。
        /// </summary>
        [DisplayName("中心Y")]
        public double? CenterY { get; set; }

        /// <summary>
        /// 输出线段长度。
        /// </summary>
        [DisplayName("长度")]
        public double? Length { get; set; }

        /// <summary>
        /// 输出线段角度。
        /// </summary>
        [DisplayName("角度")]
        public double? Angle { get; set; }

        /// <summary>
        /// 平行中线模式下两条源线距离。
        /// </summary>
        [DisplayName("线间距")]
        public double? LineDistance { get; set; }

        /// <summary>
        /// 两条源线方向夹角差。
        /// </summary>
        [DisplayName("角度差")]
        public double? AngleDifference { get; set; }

        /// <summary>
        /// 拟合误差。
        /// </summary>
        [DisplayName("拟合误差")]
        public double? FitError { get; set; }

        /// <summary>
        /// 参与线组合拟合或显示的源点集合；平行中线模式下这些点来自两条源线，不代表输出中线自身的点集。
        /// </summary>
        [DisplayName("源点集合")]
        public List<PointF> SourcePoints { get; set; } = new List<PointF>();

        /// <summary>
        /// 状态信息。
        /// </summary>
        [DisplayName("信息")]
        public string Message { get; set; }

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
