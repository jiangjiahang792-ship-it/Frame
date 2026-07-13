namespace TDJS_Vision.Node._3_Detection.LargeModel
{
    /// <summary>
    /// 大模型调用节点参数。
    /// </summary>
    public class NodeParamLargeModelDetection : INodeParam
    {
        /// <summary>
        /// 默认图像阈值，0 表示使用 bank 中自动阈值。
        /// </summary>
        public const float DefaultImageThreshold = 0F;

        /// <summary>
        /// 默认面积阈值，过滤过小异常区域。
        /// </summary>
        public const int DefaultAreaThreshold = 1000;

        /// <summary>
        /// 默认最多返回异常框数量。
        /// </summary>
        public const int DefaultMaxBoxes = 128;

        /// <summary>
        /// 输入图像订阅节点文本。
        /// </summary>
        public string Text1 { get; set; }

        /// <summary>
        /// 输入图像订阅结果文本。
        /// </summary>
        public string Text2 { get; set; }

        /// <summary>
        /// 训练窗口生成的大模型模板路径。
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// 推理阶段使用的图像级异常阈值，0 表示使用模板 bank 自动阈值。
        /// </summary>
        public float ImageThreshold { get; set; } = DefaultImageThreshold;

        /// <summary>
        /// 推理阶段过滤异常区域使用的最小面积。
        /// </summary>
        public int AreaThreshold { get; set; } = DefaultAreaThreshold;

        /// <summary>
        /// native 推理最多返回的异常框数量。
        /// </summary>
        public int MaxBoxes { get; set; } = DefaultMaxBoxes;
    }
}
