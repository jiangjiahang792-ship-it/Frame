using System.ComponentModel;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSummarize
{
    internal class NodeResultSummarize : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("算法汇总结果")]
        public ResultViewData SummaryResult { get; set; }
    }
}
