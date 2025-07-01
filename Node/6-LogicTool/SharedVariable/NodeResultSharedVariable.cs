using System;
using System.ComponentModel;

namespace TDJS_Vision.Node._6_LogicTool.SharedVariable
{
    internal class NodeResultSharedVariable : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("读到的共享变量")]
        public object Value { get; set; }

        [DisplayName("共享变量的类型")]
        public SharedVarTypeEnum Type { get; set; }
    }
}
