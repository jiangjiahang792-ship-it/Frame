namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合模块节点运行结果。
    /// </summary>
    public class NodeResultCompositeModule : INodeResult
    {
        /// <summary>
        /// 组合模块节点自身运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 本次运行的模块名称。
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 内部流程累计运行耗时。
        /// </summary>
        public long InternalRunTime { get; set; }

        /// <summary>
        /// 内部快照节点数量。
        /// </summary>
        public int InternalNodeCount { get; set; }

        /// <summary>
        /// 内部流程是否运行成功。
        /// </summary>
        public bool InternalSuccess { get; set; }

        /// <summary>
        /// 运行失败时记录的错误信息。
        /// </summary>
        public string Message { get; set; }
    }
}
