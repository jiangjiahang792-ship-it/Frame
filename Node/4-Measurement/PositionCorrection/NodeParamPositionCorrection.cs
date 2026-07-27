using TDJS_Vision.Node._3_Detection.MatchTemplate;

namespace TDJS_Vision.Node._4_Measurement.PositionCorrection
{
    /// <summary>
    /// 保存位置修正节点的位姿列表订阅和第一目标基准位姿。
    /// </summary>
    public class NodeParamPositionCorrection : INodeParam
    {
        /// <summary>
        /// 获取或设置订阅节点文本。
        /// </summary>
        public string PoseText1 { get; set; }

        /// <summary>
        /// 获取或设置订阅结果文本。
        /// </summary>
        public string PoseText2 { get; set; }

        /// <summary>
        /// 获取或设置是否已经创建第一目标基准。
        /// </summary>
        public bool HasBaseline { get; set; }

        /// <summary>
        /// 获取或设置创建基准时模板匹配列表中的第一目标位姿。
        /// </summary>
        public TemplateMatchPose BasePose { get; set; }
    }
}
