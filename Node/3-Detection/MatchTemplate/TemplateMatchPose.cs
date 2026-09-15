namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// 描述单个模板匹配目标的中心、方向、尺度、得分和匹配框信息。
    /// </summary>
    public sealed class TemplateMatchPose
    {
        /// <summary>
        /// 初始化模板匹配位姿，并把默认尺度设置为不缩放的一。
        /// </summary>
        public TemplateMatchPose()
        {
            ScaleX = 1.0;
            ScaleY = 1.0;
        }

        /// <summary>
        /// 获取或设置从一开始的目标序号。
        /// </summary>
        public int TargetIndex { get; set; }

        /// <summary>
        /// 获取或设置目标中心的 X 坐标。
        /// </summary>
        public double CenterX { get; set; }

        /// <summary>
        /// 获取或设置目标中心的 Y 坐标。
        /// </summary>
        public double CenterY { get; set; }

        /// <summary>
        /// 获取或设置目标相对基准模板的角度偏差，单位为度。
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 获取或设置目标 X 方向尺度，一表示保持原尺寸。
        /// </summary>
        public double ScaleX { get; set; }

        /// <summary>
        /// 获取或设置目标 Y 方向尺度，一表示保持原尺寸。
        /// </summary>
        public double ScaleY { get; set; }

        /// <summary>
        /// 获取或设置模板匹配得分。
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 获取或设置目标匹配框宽度。
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 获取或设置目标匹配框高度。
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 获取或设置当前位姿是否有效。
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 创建当前位姿的独立副本，避免基准数据被后续运行结果修改。
        /// </summary>
        /// <returns>包含相同位姿数据的新对象。</returns>
        public TemplateMatchPose Clone()
        {
            return new TemplateMatchPose
            {
                TargetIndex = TargetIndex,
                CenterX = CenterX,
                CenterY = CenterY,
                Angle = Angle,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                Score = Score,
                Width = Width,
                Height = Height,
                IsValid = IsValid
            };
        }
    }
}
