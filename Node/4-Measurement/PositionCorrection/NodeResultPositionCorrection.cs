using System.ComponentModel;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    public class NodeResultPositionCorrection : INodeResult, IJudgmentResult
    {
        public int RunTime { get; set; }

        [DisplayName("位置修正信息")]
        public PositionCorrectionInfo CorrectionInfo { get; set; } = new PositionCorrectionInfo();

        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        [DisplayName("当前X")]
        public double CurrentX { get; set; }

        [DisplayName("当前Y")]
        public double CurrentY { get; set; }

        [DisplayName("当前角度")]
        public double CurrentAngle { get; set; }

        [DisplayName("X偏移")]
        public double DeltaX { get; set; }

        [DisplayName("Y偏移")]
        public double DeltaY { get; set; }

        [DisplayName("角度偏移")]
        public double DeltaAngle { get; set; }
    }
}
