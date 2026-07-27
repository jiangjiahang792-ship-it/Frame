using System.Collections.Generic;
using System.ComponentModel;
using TDJS_Vision.Node._3_Detection.MatchTemplate;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 定义模板第一目标基准与当前全部目标位置修正信息的公共数据契约。
    /// </summary>
    public class MultiTargetPositionCorrectionResult
    {
        /// <summary>
        /// 获取或设置与模板匹配目标顺序一致的位置修正集合。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.PositionCorrection, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("位置修正信息列表")]
        public List<PositionCorrectionInfo> Items { get; set; } = new List<PositionCorrectionInfo>();

        /// <summary>
        /// 获取或设置创建基准时保存的模板第一目标位姿。
        /// </summary>
        public TemplateMatchPose BasePose { get; set; }

        /// <summary>
        /// 获取当前有效位置修正项数量。
        /// </summary>
        public int Count
        {
            get { return Items == null ? 0 : Items.Count; }
        }

        /// <summary>
        /// 获取或设置当前修正集合是否有效。
        /// </summary>
        public bool IsValid { get; set; }
    }
}
