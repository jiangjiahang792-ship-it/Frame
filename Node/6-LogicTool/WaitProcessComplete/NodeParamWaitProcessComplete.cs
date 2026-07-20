
using System.Collections.Generic;
using System.Windows.Documents;

namespace TDJS_Vision.Node._6_LogicTool.WaitProcessComplete
{
    public class NodeParamWaitProcessComplete : INodeParam
    {
        /// <summary>
        /// 当前流程名称
        /// </summary>
        public string CurProcessName { get; set; }
        /// <summary>
        /// 等待的处理流程ID列表
        /// </summary>
        public List<int> ProcessIDs = new List<int>();
    }
}
