namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
    /// <summary>
    /// 无监督检测节点参数。
    /// </summary>
    public class NodeParamUnsupervisedDetection : INodeParam
    {
        /// <summary>
        /// 默认异常阈值，和训练界面默认值保持一致。
        /// </summary>
        public const float DefaultThreshold = 0.3F;

        /// <summary>
        /// 默认推理批次，0 表示未显式设置并跟随模板中的批次。
        /// </summary>
        public const int DefaultInferenceBatchSize = 0;

        /// <summary>
        /// 默认最小缺陷面积，和训练界面默认值保持一致。
        /// </summary>
        public const float DefaultMiniArea = 100F;

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
        /// 训练窗口生成的无监督模板路径。
        /// </summary>
        public string TemplatePath { get; set; }

        /// <summary>
        /// 推理阶段使用的异常阈值。
        /// </summary>
        public float Threshold { get; set; } = DefaultThreshold;

        /// <summary>
        /// 推理阶段使用的批次大小。
        /// </summary>
        public int InferenceBatchSize { get; set; } = DefaultInferenceBatchSize;

        /// <summary>
        /// 推理阶段过滤异常区域使用的最小缺陷面积。
        /// </summary>
        public float MiniArea { get; set; } = DefaultMiniArea;

        /// <summary>
        /// native 推理最多返回的异常框数量。
        /// </summary>
        public int MaxBoxes { get; set; } = DefaultMaxBoxes;
    }
}
