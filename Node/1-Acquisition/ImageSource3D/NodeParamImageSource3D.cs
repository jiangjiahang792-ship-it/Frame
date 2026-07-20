using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Device._3D;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource3D
{
    /// <summary>
    /// 3D 图像源节点参数，保存所选 3D 相机和取帧输出配置。
    /// </summary>
    public class NodeParamImageSource3D : INodeParam
    {
        /// <summary>
        /// 运行时使用的 3D 相机对象，序列化时通过相机名称重新绑定。
        /// </summary>
        [JsonIgnore]
        public I3DCamera Camera { get; set; }

        /// <summary>
        /// 3D 相机用户自定义名称。
        /// </summary>
        public string CameraName { get; set; } = string.Empty;

        /// <summary>
        /// 图像模式。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DImageMode ImageMode { get; set; } = Camera3DImageMode.Range;

        /// <summary>
        /// 采图超时时间，单位毫秒。
        /// </summary>
        public uint TimeOut { get; set; } = 2000;

        /// <summary>
        /// 相机未打开时是否由节点自动打开。
        /// </summary>
        public bool AutoOpenCamera { get; set; }

        /// <summary>
        /// 相机未取流时是否由节点自动启动连续采集。
        /// </summary>
        public bool AutoStartGrabbing { get; set; }

        /// <summary>
        /// 是否缓存 SDK 原生点云帧。
        /// </summary>
        public bool EnableNativePointCloudCache { get; set; } = true;

        /// <summary>
        /// 是否生成托管点云抽样数据。
        /// </summary>
        public bool BuildManagedPointCloudFallback { get; set; }

        /// <summary>
        /// 托管点云抽样最大点数。
        /// </summary>
        public int MaxDisplayPointCount { get; set; } = 350000;
    }
}
