using System;
using System.Collections.Generic;

namespace TDJS_Vision.Device._3D
{
    /// <summary>
    /// 3D 相机设备接口，隔离流程节点、显示控件和具体厂商 SDK。
    /// </summary>
    public interface I3DCamera : IDevice, IDisposable
    {
        /// <summary>
        /// 3D 帧数据到达事件。
        /// </summary>
        event EventHandler<Camera3DFrameArrivedEventArgs> FrameArrived;

        /// <summary>
        /// 3D 相机状态变化事件。
        /// </summary>
        event EventHandler<Camera3DStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 3D 相机错误事件。
        /// </summary>
        event EventHandler<Camera3DErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 当前是否已打开设备。
        /// </summary>
        bool IsOpen { get; set; }

        /// <summary>
        /// 当前是否正在采集。
        /// </summary>
        bool IsGrabbing { get; }

        /// <summary>
        /// 方案加载后是否自动打开并开始采集。
        /// </summary>
        bool AutoConnectOnLoad { get; set; }

        /// <summary>
        /// 设备序列号。
        /// </summary>
        string SN { get; set; }

        /// <summary>
        /// 设备 IP 地址。
        /// </summary>
        string IP { get; set; }

        /// <summary>
        /// 图像获取超时时间，单位毫秒。
        /// </summary>
        uint GetImageTimeOut { get; set; }

        /// <summary>
        /// 3D 相机连接配置。
        /// </summary>
        Camera3DConnectionConfig ConnectionConfig { get; set; }

        /// <summary>
        /// 3D 相机采集配置。
        /// </summary>
        Camera3DGrabConfig GrabConfig { get; set; }

        /// <summary>
        /// 枚举在线 3D 相机设备。
        /// </summary>
        /// <returns>在线 3D 相机设备列表。</returns>
        IReadOnlyList<Camera3DDeviceInfo> EnumerateDevices();

        /// <summary>
        /// 打开当前配置指向的 3D 相机。
        /// </summary>
        /// <returns>打开成功返回 true。</returns>
        bool Open();

        /// <summary>
        /// 关闭当前 3D 相机。
        /// </summary>
        void Close();

        /// <summary>
        /// 开始采集 3D 数据。
        /// </summary>
        void StartGrabbing();

        /// <summary>
        /// 停止采集 3D 数据。
        /// </summary>
        void StopGrabbing();

        /// <summary>
        /// 获取当前采集状态。
        /// </summary>
        /// <returns>正在采集返回 true。</returns>
        bool GetGrabStatus();

        /// <summary>
        /// 执行一次软触发。
        /// </summary>
        void GrabOne();

        /// <summary>
        /// 主动获取一帧 3D 数据。
        /// </summary>
        /// <returns>3D 帧数据。</returns>
        Camera3DFrameData GetOneFrameData();

        /// <summary>
        /// 使用 SDK 原生显示器显示最近一次点云帧。
        /// </summary>
        /// <param name="windowHandle">显示宿主窗口句柄。</param>
        /// <returns>显示成功返回 true。</returns>
        bool DisplayLatestPointCloud(IntPtr windowHandle);
    }
}
