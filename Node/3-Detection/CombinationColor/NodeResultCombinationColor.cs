using System.ComponentModel;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>
    /// 组合颜色识别结果，输出二值化后的灰度图、面积和显示叠加信息。
    /// </summary>
    public class NodeResultCombinationColor : INodeResult, IJudgmentResult
    {
        /// <summary>筛选后区域个数。</summary>
        [SubscriptionOutput, DisplayName("区域个数")]
        public int Count { get; set; }
        /// <summary>按面积降序输出各区域实际像素面积。</summary>
        [SubscriptionOutput, DisplayName("区域面积列表")]
        public System.Collections.Generic.List<double> Areas { get; set; } = new System.Collections.Generic.List<double>();
        /// <summary>与面积列表同序的旋转外接矩形长边，单位像素。</summary>
        [SubscriptionOutput, DisplayName("区域长度列表")]
        public System.Collections.Generic.List<double> Lengths { get; set; } = new System.Collections.Generic.List<double>();
        /// <summary>与面积列表同序的旋转外接矩形短边，单位像素。</summary>
        [SubscriptionOutput, DisplayName("区域宽度列表")]
        public System.Collections.Generic.List<double> Widths { get; set; } = new System.Collections.Generic.List<double>();

        /// <summary>
        /// 获取或设置节点运行耗时，单位毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 获取或设置本次组合颜色识别是否成功。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 获取或设置后续条件节点回写后的判定状态。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 获取或设置筛选后全部颜色区域的实际像素面积总和。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("总面积")]
        public double Area { get; set; }

        /// <summary>
        /// 获取或设置最大区域旋转外接矩形的短边长度（像素）。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("最大区域宽度")]
        public double Width { get; set; }

        /// <summary>
        /// 获取或设置最大区域旋转外接矩形的长边长度（像素）。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("最大区域长度")]
        public double Height { get; set; }

        /// <summary>
        /// 获取或设置当前检测区域的旋转矩形，供下游绘制工具订阅为矩形框。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.Rectangle, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("检测区域")]
        public ColorRotatedRect DetectionRegion { get; set; }

        /// <summary>全部位置修正后的检测区域；原单区域输出保留第一个区域以兼容旧订阅。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.Rectangle, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("检测区域列表")]
        public System.Collections.Generic.List<ColorRotatedRect> DetectionRegions { get; set; }

        /// <summary>
        /// 获取或设置筛选后区域掩膜。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("筛选后区域掩膜")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        /// <summary>
        /// 获取或设置用于界面叠加显示的算法结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();
    }
}
