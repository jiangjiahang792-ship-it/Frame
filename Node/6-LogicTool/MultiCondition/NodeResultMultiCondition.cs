using System.Collections.Generic;
using System.ComponentModel;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public class NodeResultMultiCondition : INodeResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 多条件最终判断结果。
        /// </summary>
        [DisplayName("条件结果")]
        public bool ConditionResult { get; set; }

        /// <summary>
        /// 多条件每一行的运行明细。
        /// </summary>
        [DisplayName("条件明细")]
        public List<NodeConditionEvaluation> Details { get; set; } = new List<NodeConditionEvaluation>();

        /// <summary>
        /// 多条件运行诊断文本，用于定位空值、未运行和动态变量缺失等问题。
        /// </summary>
        [DisplayName("诊断信息")]
        public string DiagnosticsText { get; set; }
    }
}
