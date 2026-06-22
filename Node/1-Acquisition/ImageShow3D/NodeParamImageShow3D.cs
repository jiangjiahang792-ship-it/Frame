namespace TDJS_Vision.Node._1_Acquisition.ImageShow3D
{
    /// <summary>
    /// 3D 图像显示节点参数。
    /// </summary>
    public class NodeParamImageShow3D : INodeParam
    {
        /// <summary>
        /// 3D 图像窗口名称。
        /// </summary>
        public string WindowName { get; set; }

        /// <summary>
        /// 订阅节点文本。
        /// </summary>
        public string Text1 { get; set; }

        /// <summary>
        /// 订阅结果文本。
        /// </summary>
        public string Text2 { get; set; }
    }
}
