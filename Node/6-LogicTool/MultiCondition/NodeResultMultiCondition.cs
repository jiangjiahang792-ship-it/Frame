using System.Collections.Generic;
using System.ComponentModel;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public class NodeResultMultiCondition : INodeResult
    {
        public int RunTime { get; set; }

        [DisplayName("条件结果")]
        public bool ConditionResult { get; set; }

        [DisplayName("条件明细")]
        public List<NodeConditionEvaluation> Details { get; set; } = new List<NodeConditionEvaluation>();
    }
}
