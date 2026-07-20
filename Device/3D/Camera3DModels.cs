using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Drawing;

namespace TDJS_Vision.Device._3D
{
    /// <summary>
    /// 3D 相机打开方式。
    /// </summary>
    public enum Camera3DOpenMode
    {
        /// <summary>
        /// 通过设备序列号打开。
        /// </summary>
        SerialNumber,

        /// <summary>
        /// 通过设备 IP 地址打开。
        /// </summary>
        IpAddress
    }

    /// <summary>
    /// 3D 相机图像模式，数值预留给海康 3DVM SDK 参数映射使用。
    /// </summary>
    public enum Camera3DImageMode
    {
        /// <summary>
        /// 原始图。
        /// </summary>
        Origin = 1,

        /// <summary>
        /// 点云图。
        /// </summary>
        PointCloud = 4,

        /// <summary>
        /// 深度图。
        /// </summary>
        Range = 7,

        /// <summary>
        /// 亮度图。
        /// </summary>
        Intensity = 10
    }

    /// <summary>
    /// 3D 相机触发模式。
    /// </summary>
    public enum Camera3DTriggerMode
    {
        /// <summary>
        /// 关闭触发，连续采集。
        /// </summary>
        Off,

        /// <summary>
        /// 开启触发。
        /// </summary>
        On
    }

    /// <summary>
    /// 3D 相机触发源。
    /// </summary>
    public enum Camera3DTriggerSource
    {
        /// <summary>
        /// 自动模式。
        /// </summary>
        Auto,

        /// <summary>
        /// 软件触发。
        /// </summary>
        Soft,

        /// <summary>
        /// 硬件线路 0。
        /// </summary>
        Line0,

        /// <summary>
        /// 硬件线路 1。
        /// </summary>
        Line1,

        /// <summary>
        /// 硬件线路 2。
        /// </summary>
        Line2,

        /// <summary>
        /// 硬件线路 3。
        /// </summary>
        Line3
    }

    /// <summary>
    /// 3D 相机输出内容类型。
    /// </summary>
    public enum Camera3DFrameContentType
    {
        /// <summary>
        /// 未知内容。
        /// </summary>
        Unknown,

        /// <summary>
        /// 深度图。
        /// </summary>
        Depth,

        /// <summary>
        /// 点云数据。
        /// </summary>
        PointCloud,

        /// <summary>
        /// 深度图和点云数据。
        /// </summary>
        DepthAndPointCloud
    }

    /// <summary>
    /// 3D 相机设备信息，用于枚举列表、设备添加和反序列化定位。
    /// </summary>
    public sealed class Camera3DDeviceInfo
    {
        /// <summary>
        /// 设备列表索引。
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 设备型号名称。
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// 设备序列号。
        /// </summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// 设备当前 IP 地址。
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 设备厂商名称。
        /// </summary>
        public string ManufacturerName { get; set; } = string.Empty;

        /// <summary>
        /// 界面显示名称。
        /// </summary>
        public string DisplayName => $"{ModelName}({SerialNumber})";

        /// <summary>
        /// 返回设备显示文本，方便 ComboBox 直接绑定。
        /// </summary>
        /// <returns>设备显示文本。</returns>
        public override string ToString()
        {
            return $"{Index + 1}. {ModelName}  IP:{IpAddress}  SN:{SerialNumber}";
        }
    }

    /// <summary>
    /// 3D 相机连接配置。
    /// </summary>
    public sealed class Camera3DConnectionConfig
    {
        /// <summary>
        /// 设备打开方式。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DOpenMode OpenMode { get; set; } = Camera3DOpenMode.SerialNumber;

        /// <summary>
        /// 设备型号名称。
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// 设备序列号。
        /// </summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>
        /// 设备 IP 地址。
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 设备厂商名称。
        /// </summary>
        public string ManufacturerName { get; set; } = "Hikrobot";

        /// <summary>
        /// 采图超时时间，单位毫秒。
        /// </summary>
        public uint GetImageTimeoutMs { get; set; } = 2000;
    }

    /// <summary>
    /// 3D 相机采集配置。
    /// </summary>
    public sealed class Camera3DGrabConfig
    {
        /// <summary>
        /// 图像模式，默认采集深度图。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DImageMode ImageMode { get; set; } = Camera3DImageMode.Range;

        /// <summary>
        /// 触发模式。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DTriggerMode TriggerMode { get; set; } = Camera3DTriggerMode.Off;

        /// <summary>
        /// 触发源。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DTriggerSource TriggerSource { get; set; } = Camera3DTriggerSource.Auto;

        /// <summary>
        /// 触发延迟，单位微秒。
        /// </summary>
        public int TriggerDelayUs { get; set; }

        /// <summary>
        /// 是否缓存 SDK 原生点云帧，用于交给 SDK 原生显示器显示。
        /// </summary>
        public bool EnableNativePointCloudCache { get; set; } = true;

        /// <summary>
        /// 是否额外构建托管点云点数组，作为无原生显示时的降级显示数据。
        /// </summary>
        public bool BuildManagedPointCloudFallback { get; set; }

        /// <summary>
        /// 托管点云显示最大点数，用于控制 UI 绘制负载。
        /// </summary>
        public int MaxDisplayPointCount { get; set; } = 350000;
    }

    /// <summary>
    /// 3D 点云单点数据，坐标单位沿用相机 SDK 输出单位。
    /// </summary>
    public struct Camera3DPointCloudPoint
    {
        /// <summary>
        /// X 坐标。
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y 坐标。
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Z 坐标。
        /// </summary>
        public float Z { get; set; }

        /// <summary>
        /// 用于伪彩色映射的高度或强度值。
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// 是否使用点自带颜色。
        /// </summary>
        public bool HasColor { get; set; }

        /// <summary>
        /// 点自带颜色，按 ARGB 整数保存。
        /// </summary>
        public int Argb { get; set; }

        /// <summary>
        /// 创建按 Z 值着色的点云点。
        /// </summary>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <param name="z">Z 坐标。</param>
        public Camera3DPointCloudPoint(float x, float y, float z)
            : this(x, y, z, z, false, 0)
        {
        }

        /// <summary>
        /// 创建带自定义伪彩色值的点云点。
        /// </summary>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <param name="z">Z 坐标。</param>
        /// <param name="value">用于着色的值。</param>
        public Camera3DPointCloudPoint(float x, float y, float z, float value)
            : this(x, y, z, value, false, 0)
        {
        }

        /// <summary>
        /// 创建带自定义颜色的点云点。
        /// </summary>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <param name="z">Z 坐标。</param>
        /// <param name="color">点颜色。</param>
        public Camera3DPointCloudPoint(float x, float y, float z, Color color)
            : this(x, y, z, z, true, color.ToArgb())
        {
        }

        /// <summary>
        /// 创建完整点云点。
        /// </summary>
        /// <param name="x">X 坐标。</param>
        /// <param name="y">Y 坐标。</param>
        /// <param name="z">Z 坐标。</param>
        /// <param name="value">用于着色的值。</param>
        /// <param name="hasColor">是否使用点自带颜色。</param>
        /// <param name="argb">点自带颜色。</param>
        public Camera3DPointCloudPoint(float x, float y, float z, float value, bool hasColor, int argb)
        {
            X = x;
            Y = y;
            Z = z;
            Value = value;
            HasColor = hasColor;
            Argb = argb;
        }

        /// <summary>
        /// 判断点坐标和值是否为有效有限数。
        /// </summary>
        /// <returns>坐标和值均有效时返回 true。</returns>
        public bool IsFinite()
        {
            return !float.IsNaN(X) && !float.IsInfinity(X)
                && !float.IsNaN(Y) && !float.IsInfinity(Y)
                && !float.IsNaN(Z) && !float.IsInfinity(Z)
                && !float.IsNaN(Value) && !float.IsInfinity(Value);
        }
    }

    /// <summary>
    /// 3D 相机 SDK 原生点云帧缓存。
    /// </summary>
    public sealed class Camera3DNativePointCloudFrame
    {
        /// <summary>
        /// 原生帧宽度。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 原生帧高度。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// SDK 图像类型数值。
        /// </summary>
        public uint ImageType { get; set; }

        /// <summary>
        /// 帧号。
        /// </summary>
        public uint FrameNumber { get; set; }

        /// <summary>
        /// 原生点云字节数据。
        /// </summary>
        public byte[] DataBytes { get; set; } = new byte[0];

        /// <summary>
        /// X 方向标定比例。
        /// </summary>
        public float XScale { get; set; }

        /// <summary>
        /// Y 方向标定比例。
        /// </summary>
        public float YScale { get; set; }

        /// <summary>
        /// Z 方向标定比例。
        /// </summary>
        public float ZScale { get; set; }

        /// <summary>
        /// X 方向偏移。
        /// </summary>
        public int XOffset { get; set; }

        /// <summary>
        /// Y 方向偏移。
        /// </summary>
        public int YOffset { get; set; }

        /// <summary>
        /// Z 方向偏移。
        /// </summary>
        public int ZOffset { get; set; }

        /// <summary>
        /// 原生数据长度。
        /// </summary>
        public int DataLength => DataBytes == null ? 0 : DataBytes.Length;
    }

    /// <summary>
    /// 3D 相机帧数据，保留深度图、托管点云和原生点云缓存入口。
    /// </summary>
    public sealed class Camera3DFrameData
    {
        /// <summary>
        /// 输出内容类型。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Camera3DFrameContentType ContentType { get; set; } = Camera3DFrameContentType.Unknown;

        /// <summary>
        /// 图像宽度。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 图像高度。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 帧号。
        /// </summary>
        public uint FrameNumber { get; set; }

        /// <summary>
        /// 深度原始值，按行优先排列。
        /// </summary>
        public int[] DepthValues { get; set; } = new int[0];

        /// <summary>
        /// 深度无效值。
        /// </summary>
        public int DepthInvalidValue { get; set; } = int.MinValue;

        /// <summary>
        /// 用于托管显示的点云点，可能已经抽样。
        /// </summary>
        public Camera3DPointCloudPoint[] PointCloudPoints { get; set; } = new Camera3DPointCloudPoint[0];

        /// <summary>
        /// 点云原始总点数。
        /// </summary>
        public int TotalPointCount { get; set; }

        /// <summary>
        /// SDK 原生点云帧缓存。
        /// </summary>
        public Camera3DNativePointCloudFrame NativePointCloudFrame { get; set; }

        /// <summary>
        /// 采集帧率。
        /// </summary>
        public double FrameRate { get; set; }

        /// <summary>
        /// 显示帧率。
        /// </summary>
        public double DisplayFrameRate { get; set; }

        /// <summary>
        /// X 方向标定比例。
        /// </summary>
        public float XScale { get; set; }

        /// <summary>
        /// Y 方向标定比例。
        /// </summary>
        public float YScale { get; set; }

        /// <summary>
        /// Z 方向标定比例。
        /// </summary>
        public float ZScale { get; set; }
    }

    /// <summary>
    /// 3D 相机帧到达事件参数。
    /// </summary>
    public sealed class Camera3DFrameArrivedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建 3D 相机帧到达事件参数。
        /// </summary>
        /// <param name="frame">3D 相机帧数据。</param>
        public Camera3DFrameArrivedEventArgs(Camera3DFrameData frame)
        {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        /// <summary>
        /// 3D 相机帧数据。
        /// </summary>
        public Camera3DFrameData Frame { get; }
    }

    /// <summary>
    /// 3D 相机状态变化事件参数。
    /// </summary>
    public sealed class Camera3DStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 创建 3D 相机状态变化事件参数。
        /// </summary>
        /// <param name="message">状态文本。</param>
        public Camera3DStatusChangedEventArgs(string message)
        {
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// 状态文本。
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// 3D 相机错误事件参数。
    /// </summary>
    public sealed class Camera3DErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 创建 3D 相机错误事件参数。
        /// </summary>
        /// <param name="message">错误说明。</param>
        /// <param name="errorCode">SDK 错误码。</param>
        public Camera3DErrorEventArgs(string message, int errorCode)
        {
            Message = message ?? string.Empty;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 错误说明。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// SDK 错误码。
        /// </summary>
        public int ErrorCode { get; }
    }
}
