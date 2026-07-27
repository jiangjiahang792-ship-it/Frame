using System.ComponentModel;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    /// <summary>
    /// 表示位置修正节点输出的全部目标修正信息和汇总状态。
    /// </summary>
    public class NodeResultPositionCorrection : MultiTargetPositionCorrectionResult, INodeResult, IJudgmentResult
    {
        /// <summary>
        /// 获取或设置节点运行耗时，单位为毫秒。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 获取或设置第一目标的位置修正信息，用于兼容尚未迁移的单目标工具。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("位置修正信息")]
        public PositionCorrectionInfo CorrectionInfo { get; set; } = new PositionCorrectionInfo();

        /// <summary>
        /// 获取或设置有效修正目标数量。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("修正目标数量")]
        public int TargetCount { get; set; }

        /// <summary>
        /// 获取或设置节点是否输出了有效修正集合。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 获取或设置后续多条件判定后的状态。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 获取或设置第一目标当前 X 坐标，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标当前X")]
        public double CurrentX { get; set; }

        /// <summary>
        /// 获取或设置第一目标当前 Y 坐标，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标当前Y")]
        public double CurrentY { get; set; }

        /// <summary>
        /// 获取或设置第一目标当前角度，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标当前角度")]
        public double CurrentAngle { get; set; }

        /// <summary>
        /// 获取或设置第一目标 X 偏移，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标X偏移")]
        public double DeltaX { get; set; }

        /// <summary>
        /// 获取或设置第一目标 Y 偏移，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标Y偏移")]
        public double DeltaY { get; set; }

        /// <summary>
        /// 获取或设置第一目标角度偏移，供界面摘要显示。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("第一目标角度偏移")]
        public double DeltaAngle { get; set; }
    }
}
