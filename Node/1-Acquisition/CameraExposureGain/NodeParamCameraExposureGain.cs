using Newtonsoft.Json;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Node._1_Acquisition.CameraExposureGain
{
    /// <summary>
    /// 相机曝光增益节点参数，保存相机名称与本次要写入的曝光、增益值。
    /// </summary>
    public class NodeParamCameraExposureGain : INodeParam
    {
        /// <summary>
        /// 反序列化用的默认构造函数。
        /// </summary>
        [JsonConstructor]
        public NodeParamCameraExposureGain() { }

        /// <summary>
        /// 运行时使用的相机对象，不参与方案文件序列化。
        /// </summary>
        [JsonIgnore]
        public ICamera Camera { get; set; }

        /// <summary>
        /// 相机自定义名称，用于保存方案后重新绑定相机对象。
        /// </summary>
        public string CameraName { get; set; }

        /// <summary>
        /// 曝光时间，单位跟相机 SDK 返回保持一致，通常为微秒。
        /// </summary>
        public double ExposureTime { get; set; }

        /// <summary>
        /// 相机增益值，兼容整数增益和浮点增益相机。
        /// </summary>
        public double Gain { get; set; }
    }
}
