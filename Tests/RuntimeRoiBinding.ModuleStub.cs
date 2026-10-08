using System.Reflection;
using TDJS_Vision.Node;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>源码隔离测试用模块壳，避免构造生产模块参数窗体；只提供只读内部作用域。</summary>
    public sealed class NodeCompositeModule : NodeBase
    {
        /// <summary>模拟已经创建的内部流程。</summary>
        internal Process ExistingRuntimeProcess { get; set; }
        /// <summary>注入父流程、稳定编号及内部流程，不初始化生产设备。</summary>
        public NodeCompositeModule(Process parent, int id, Process inner)
        {
            typeof(NodeBase).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, id);
            Process = parent; NodeName = "测试模块"; ExistingRuntimeProcess = inner;
        }
        /// <summary>目录只读返回已有测试流程。</summary>
        public Process GetEditableProcess() => ExistingRuntimeProcess;
    }
}
