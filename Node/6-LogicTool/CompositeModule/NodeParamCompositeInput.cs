using System.Collections.Generic;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输入节点参数。
    /// </summary>
    public class NodeParamCompositeInput : INodeParam
    {
        /// <summary>
        /// 当前输入节点声明的输入端口集合。
        /// </summary>
        public List<CompositeInputPortDefinition> Ports { get; set; } = new List<CompositeInputPortDefinition>();
    }
}
