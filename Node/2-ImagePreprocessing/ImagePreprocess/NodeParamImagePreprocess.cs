namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    /// <summary>
    /// 图像预处理算法类型。
    /// </summary>
    public enum ImagePreprocessMode
    {
        /// <summary>
        /// emphasize 风格的边缘增强。
        /// </summary>
        Emphasize,

        /// <summary>
        /// Laws 纹理滤波。
        /// </summary>
        TextureLaws,

        /// <summary>
        /// 中值滤波。
        /// </summary>
        Median
    }

    /// <summary>
    /// Laws 纹理滤波核类型。
    /// </summary>
    public enum LawsTextureKernel
    {
        /// <summary>
        /// 横纵边缘能量组合。
        /// </summary>
        EdgeEnergy,

        /// <summary>
        /// L5E5 竖向边缘纹理核。
        /// </summary>
        L5E5,

        /// <summary>
        /// E5L5 横向边缘纹理核。
        /// </summary>
        E5L5,

        /// <summary>
        /// E5E5 边缘交叉纹理核。
        /// </summary>
        E5E5,

        /// <summary>
        /// S5S5 斑点纹理核。
        /// </summary>
        S5S5,

        /// <summary>
        /// R5R5 波纹纹理核。
        /// </summary>
        R5R5
    }

    /// <summary>
    /// 图像预处理节点参数。
    /// </summary>
    public class NodeParamImagePreprocess : INodeParam
    {
        /// <summary>
        /// 订阅的节点名称。
        /// </summary>
        public string Text1 { get; set; }

        /// <summary>
        /// 订阅的节点结果属性名。
        /// </summary>
        public string Text2 { get; set; }

        /// <summary>
        /// 当前启用的图像预处理模式。
        /// </summary>
        public ImagePreprocessMode Mode { get; set; } = ImagePreprocessMode.Emphasize;

        /// <summary>
        /// 边缘增强横向掩膜宽度。
        /// </summary>
        public int EmphasizeMaskWidth { get; set; } = 7;

        /// <summary>
        /// 边缘增强纵向掩膜高度。
        /// </summary>
        public int EmphasizeMaskHeight { get; set; } = 7;

        /// <summary>
        /// 边缘增强强度系数。
        /// </summary>
        public double EmphasizeFactor { get; set; } = 1.0;

        /// <summary>
        /// Laws 纹理滤波核。
        /// </summary>
        public LawsTextureKernel LawsKernel { get; set; } = LawsTextureKernel.EdgeEnergy;

        /// <summary>
        /// Laws 纹理能量平滑窗口。
        /// </summary>
        public int LawsEnergySize { get; set; } = 15;

        /// <summary>
        /// 中值滤波窗口大小。
        /// </summary>
        public int MedianKernelSize { get; set; } = 3;
    }
}
