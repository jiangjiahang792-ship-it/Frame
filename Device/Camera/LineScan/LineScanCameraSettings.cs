namespace TDJS_Vision.Device.Camera.LineScan
{
    /// <summary>线扫相机触发选择器，对应海康GenICam的TriggerSelector节点。</summary>
    public enum LineScanTriggerSelector
    {
        /// <summary>帧组开始触发，对应海康线扫相机实际使用的FrameBurstStart。</summary>
        FrameBurstStart,
        /// <summary>旧版本兼容项；新版界面以相机返回的FrameBurstStart为准。</summary>
        FrameStart,
        /// <summary>每一行开始触发，对应LineStart。</summary>
        LineStart
    }

    /// <summary>
    /// 保存线扫相机第一阶段需要开放的核心采集参数。
    /// </summary>
    public sealed class LineScanCameraSettings
    {
        /// <summary>获取或设置每行图像宽度，单位为像素。</summary>
        public long Width { get; set; }

        /// <summary>获取或设置组成一帧图像的扫描行数。</summary>
        public long Height { get; set; }

        /// <summary>获取或设置水平ROI偏移，单位为像素。</summary>
        public long OffsetX { get; set; }

        /// <summary>获取或设置曝光时间，单位由相机节点定义，通常为微秒。</summary>
        public double ExposureTime { get; set; }

        /// <summary>获取或设置模拟增益。</summary>
        public double Gain { get; set; }

        /// <summary>获取或设置触发选择器。</summary>
        public LineScanTriggerSelector TriggerSelector { get; set; } = LineScanTriggerSelector.LineStart;

        /// <summary>获取或设置线触发模式。</summary>
        public TDJS_Vision.Device.Camera.TriggerModel TriggerMode { get; set; }

        /// <summary>获取或设置线触发源。</summary>
        public TDJS_Vision.Device.Camera.TriggerSource TriggerSource { get; set; } = TDJS_Vision.Device.Camera.TriggerSource.LINE0;

        /// <summary>获取或设置线触发极性。</summary>
        public TDJS_Vision.Device.Camera.TriggerEdge TriggerEdge { get; set; } = TDJS_Vision.Device.Camera.TriggerEdge.Rising;

        /// <summary>获取或设置像素格式的GenICam符号名称。</summary>
        public string PixelFormat { get; set; } = "Mono8";
    }
}
