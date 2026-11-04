using Newtonsoft.Json;
using TDJS_Vision.Device.Camera.LineScan;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Node._1_Acquisition.LineScanImageSource
{
    /// <summary>线扫图像源节点参数。</summary>
    public sealed class NodeParamLineScanImageSource : INodeParam
    {
        /// <summary>运行时绑定的线扫相机，不写入方案。</summary>
        [JsonIgnore]
        public ILineScanCamera Camera { get; set; }

        /// <summary>获取或设置线扫相机用户名称。</summary>
        public string CameraName { get; set; }

        /// <summary>获取或设置完整图像高度。</summary>
        public long Height { get; set; } = 1000;

        /// <summary>获取或设置每行图像宽度。</summary>
        public long Width { get; set; } = 4096;

        /// <summary>获取或设置曝光时间。</summary>
        public double ExposureTime { get; set; } = 100;

        /// <summary>获取或设置增益。</summary>
        public double Gain { get; set; }

        /// <summary>获取或设置触发选择器。</summary>
        public LineScanTriggerSelector TriggerSelector { get; set; } = LineScanTriggerSelector.LineStart;

        /// <summary>获取或设置线触发模式。</summary>
        public TriggerModel TriggerMode { get; set; }

        /// <summary>获取或设置线触发源。</summary>
        public TriggerSource TriggerSource { get; set; } = TriggerSource.LINE0;

        /// <summary>获取或设置线触发极性。</summary>
        public TriggerEdge TriggerEdge { get; set; } = TriggerEdge.Rising;
    }
}
