using System.Collections.Generic;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输出节点参数。
    /// </summary>
    public class NodeParamCompositeOutput : INodeParam
    {
        /// <summary>
        /// 当前输出节点声明的输出端口集合。
        /// </summary>
        public List<CompositeOutputPortDefinition> Ports { get; set; } = new List<CompositeOutputPortDefinition>();
    }
}
