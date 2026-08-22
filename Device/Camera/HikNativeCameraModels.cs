using MvCamCtrl.NET;

namespace TDJS_Vision.Device.Camera
{
    /// <summary>
    /// 海康原生SDK枚举得到的二维相机设备信息。
    /// </summary>
    public sealed class HikNativeDeviceInfo
    {
        /// <summary>
        /// 获取或设置设备在本轮枚举结果中的序号。
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 获取或设置SDK传输层类型。
        /// </summary>
        public uint TransportLayerType { get; set; }

        /// <summary>
        /// 获取或设置官方MVCamera类创建设备所需的原生结构。
        /// </summary>
        public MyCamera.MV_CC_DEVICE_INFO NativeDeviceInfo { get; set; }

        /// <summary>
        /// 获取或设置设备制造商名称。
        /// </summary>
        public string ManufacturerName { get; set; }

        /// <summary>
        /// 获取或设置设备型号。
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// 获取或设置设备序列号。
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// 获取或设置相机内部用户自定义名称。
        /// </summary>
        public string UserDefinedName { get; set; }

        /// <summary>
        /// 获取或设置GigE设备当前IP地址。
        /// </summary>
        public string IpAddress { get; set; }
    }

    /// <summary>
    /// 框架内部使用的相机枚举项，隔离具体SDK的数据类型。
    /// </summary>
    public sealed class CameraEnumEntry
    {
        /// <summary>
        /// 获取或设置枚举项数值。
        /// </summary>
        public uint Value { get; set; }

        /// <summary>
        /// 获取或设置枚举项符号名称。
        /// </summary>
        public string Symbolic { get; set; }
    }

    /// <summary>
    /// 框架内部使用的相机枚举参数值。
    /// </summary>
    public sealed class CameraEnumValue
    {
        /// <summary>
        /// 获取或设置节点在当前相机状态下是否可读。
        /// </summary>
        public bool IsReadable { get; set; } = true;

        /// <summary>
        /// 获取或设置节点在当前相机状态下是否可写。
        /// </summary>
        public bool IsWritable { get; set; } = true;

        /// <summary>
        /// 获取或设置当前枚举项。
        /// </summary>
        public CameraEnumEntry CurEnumEntry { get; set; }

        /// <summary>
        /// 获取或设置相机支持的枚举项。
        /// </summary>
        public CameraEnumEntry[] SupportEnumEntries { get; set; } = new CameraEnumEntry[0];

        /// <summary>
        /// 获取或设置有效支持项数量。
        /// </summary>
        public uint SupportedNum { get; set; }
    }

    /// <summary>
    /// 框架内部使用的相机浮点参数值。
    /// </summary>
    public sealed class CameraFloatValue
    {
        /// <summary>
        /// 获取或设置当前值。
        /// </summary>
        public double CurValue { get; set; }

        /// <summary>
        /// 获取或设置最小值。
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// 获取或设置最大值。
        /// </summary>
        public double Max { get; set; }
    }

    /// <summary>
    /// 框架内部使用的相机整数参数值。
    /// </summary>
    public sealed class CameraIntValue
    {
        /// <summary>
        /// 获取或设置当前值。
        /// </summary>
        public long CurValue { get; set; }

        /// <summary>
        /// 获取或设置最小值。
        /// </summary>
        public long Min { get; set; }

        /// <summary>
        /// 获取或设置最大值。
        /// </summary>
        public long Max { get; set; }

        /// <summary>
        /// 获取或设置递增步长。
        /// </summary>
        public long Increment { get; set; }
    }
}
