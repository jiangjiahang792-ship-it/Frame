namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>
    /// 向导第二步颜色通道范围模型。
    /// </summary>
    public sealed class GuideColorRangeModel
    {
        /// <summary>颜色空间名称。</summary>
        public string ColorSpaceName { get; set; }

        /// <summary>第一通道下限。</summary>
        public int Channel1Low { get; set; }

        /// <summary>第一通道上限。</summary>
        public int Channel1High { get; set; }

        /// <summary>第二通道下限。</summary>
        public int Channel2Low { get; set; }

        /// <summary>第二通道上限。</summary>
        public int Channel2High { get; set; }

        /// <summary>第三通道下限。</summary>
        public int Channel3Low { get; set; }

        /// <summary>第三通道上限。</summary>
        public int Channel3High { get; set; }

        /// <summary>第一通道实际采样下限，色相允许跨零。</summary>
        public int SampleChannel1Low { get; set; }
        /// <summary>第一通道实际采样上限。</summary>
        public int SampleChannel1High { get; set; }
        /// <summary>第二通道实际采样下限。</summary>
        public int SampleChannel2Low { get; set; }
        /// <summary>第二通道实际采样上限。</summary>
        public int SampleChannel2High { get; set; }
        /// <summary>第三通道实际采样下限。</summary>
        public int SampleChannel3Low { get; set; }
        /// <summary>第三通道实际采样上限。</summary>
        public int SampleChannel3High { get; set; }
        /// <summary>是否包含原始采样范围，兼容旧模型。</summary>
        public bool HasSampleRange { get; set; }
    }
}
