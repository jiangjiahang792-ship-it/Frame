using System.ComponentModel;
using System.Collections.Generic;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>沿用卡尺多目标契约，失败保留列表位置，角度为空。</summary>
    public sealed class TerminalAngleTargetResult : MultiTargetMeasurementItemBase
    {
        /// <summary>当前目标相对原图垂线的角度，失败为空。</summary>
        public double? Angle { get; set; }
        /// <summary>局部算法及位置修正耗时。</summary>
        public double AlgorithmMilliseconds { get; set; }
        /// <summary>当前目标在原图上的结果叠加。</summary>
        public AlgorithmResult DisplayResult { get; set; } = new AlgorithmResult();
    }
    /// <summary>端子角度输出，未测到角度时保留空值，测量有效不代表产品合格。</summary>
    public sealed class NodeResultTerminalAngle : INodeResult, ISubscriptionTextFormatter
    {
        /// <summary>端子角度文本固定一位小数；同时支持单值和多目标摘要，空值不冒充零度。</summary>
        public bool TryFormatSubscriptionText(string outputName, object value, out string text)
        {
            text = null;
            if (!string.Equals(outputName, "端子角度", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(outputName, nameof(Angle), System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (value == null) { text = string.Empty; return true; }
            if (!(value is double)) return false;
            text = ((double)value).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture);
            return true;
        }
        /// <summary>整个节点本轮运行耗时，单位毫秒。</summary>
        public int RunTime { get; set; }
        /// <summary>与卡尺一致，按上游修正列表顺序输出全部端子的测量结果。</summary>
        [SubscriptionOutput(SubscriptionDataCategory.MeasurementResult, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("多目标端子角度结果")]
        public List<TerminalAngleTargetResult> Items { get; set; } = new List<TerminalAngleTargetResult>();
        /// <summary>本轮是否存在目标且全部目标测量成功。</summary>
        [SubscriptionOutput]
        [DisplayName("测量成功")] public bool Success { get; set; }
        /// <summary>保留第一目标角度订阅兼容；顶部向右为正，第一目标失败或无目标时为空。</summary>
        [SubscriptionOutput]
        [DisplayName("端子角度")] public double? Angle { get; set; }
        /// <summary>测量说明或具体失败原因。</summary>
        [SubscriptionOutput]
        [DisplayName("测量状态")] public string Message { get; set; } = "尚未测量";
        /// <summary>ROI 算法耗时，不包含输入取图或画面渲染。</summary>
        [SubscriptionOutput]
        [DisplayName("算法耗时")] public double AlgorithmMilliseconds { get; set; }
        /// <summary>供框架绘制节点订阅的原图坐标叠加层。</summary>
        [SubscriptionOutput]
        [DisplayName("算法结果")] public AlgorithmResult DisplayResult { get; set; } = new AlgorithmResult();
        /// <summary>借用输入图像并持有上游租约，叠加图形不烧入原图。</summary>
        [SubscriptionOutput]
        [DisplayName("输出图像")] public OutputImage OutputImage { get; set; }
    }
}
