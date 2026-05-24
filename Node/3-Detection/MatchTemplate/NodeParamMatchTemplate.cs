namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public class NodeParamMatchTemplate : INodeParam
    {
        /// <summary>
        /// 模版图片文件名称
        /// </summary>
        public string TemplateFileName {  get; set; }
        /// <summary>
        /// 模板图像数据。保存到方案文件中，运行时优先使用该数据。
        /// </summary>
        public byte[] TemplateImageBytes { get; set; }
        /// <summary>
        /// 模板涂抹消除蒙版。白色区域表示不参与轮廓显示/输出。
        /// </summary>
        public byte[] TemplateEraseMaskBytes { get; set; }
        /// <summary>
        /// 模板来源显示名，仅用于界面提示。
        /// </summary>
        public string TemplateSourceName { get; set; }
        /// <summary>
        /// 订阅节点的名称
        /// </summary>
        public string Text1 { get; set; }
        /// <summary>
        /// 订阅节点的属性
        /// </summary>
        public string Text2 { get; set; }
        /// <summary>
        /// 图像缩小倍数
        /// </summary>
        public float Scale { get; set; }
        /// <summary>
        /// 匹配最小得分
        /// </summary>
        public float MinScore { get; set; }
        /// <summary>
        /// 输出结果的数量
        /// </summary>
        public int ResultNum { get; set; }
        /// <summary>
        /// 是否在整幅图中搜索。为 false 时使用搜索区域。
        /// </summary>
        public bool AllSearch { get; set; } = true;
        /// <summary>
        /// 搜索区域中心 X。
        /// </summary>
        public float SearchRegionCenterX { get; set; }
        /// <summary>
        /// 搜索区域中心 Y。
        /// </summary>
        public float SearchRegionCenterY { get; set; }
        /// <summary>
        /// 搜索区域宽度。
        /// </summary>
        public float SearchRegionWidth { get; set; }
        /// <summary>
        /// 搜索区域高度。
        /// </summary>
        public float SearchRegionHeight { get; set; }
        /// <summary>
        /// 搜索区域角度。
        /// </summary>
        public float SearchRegionAngle { get; set; }
        /// <summary>
        /// 搜索角度容差，单位度。
        /// </summary>
        public double ToleranceAngle { get; set; } = 80.0;
        /// <summary>
        /// 搜索角度步长，0 表示由算法自动选择。
        /// </summary>
        public double AngleStep { get; set; } = 0.0;
        /// <summary>
        /// 最大重叠率，支持 0~1 或 0~100 百分制。
        /// </summary>
        public double MaxOverlap { get; set; } = 40.0;
        /// <summary>
        /// 极速粗匹配模式。
        /// </summary>
        public bool CoarseMatch { get; set; }
        /// <summary>
        /// 是否显示模板边缘轮廓。
        /// </summary>
        public bool ShowOutlineStatus { get; set; } = true;
        /// <summary>
        /// 是否显示匹配外框。
        /// </summary>
        public bool ShowOutRegionStatus { get; set; } = true;

    }
}
