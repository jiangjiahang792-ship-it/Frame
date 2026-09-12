using Basler.Pylon;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using TDJS_Vision.Forms.ImageViewer;

namespace TDJS_Vision.Device.Camera
{
    /// <summary>
    /// 为相机连接后的首个生产帧提供一次性冷启动宽限。
    /// </summary>
    public interface IProductionCameraStartupGraceReservation : IDisposable
    {
        /// <summary>
        /// 在生产票据成功登记后提交本次宽限资格。
        /// </summary>
        void Commit();
    }

    /// <summary>
    /// 为相机连接后的首个生产帧提供一次性冷启动宽限。
    /// </summary>
    public interface IProductionCameraStartupGraceProvider
    {
        /// <summary>
        /// 预留当前连接会话的首个生产帧宽限；票据登记失败时释放对象会自动归还资格。
        /// </summary>
        /// <returns>取得唯一预留时返回待提交对象；资格已提交或相机未连接时返回null。</returns>
        IProductionCameraStartupGraceReservation TryReserveInitialProductionFrameGrace();
    }

    /// <summary>
    /// 相机基类
    /// </summary>
    public interface ICamera : IDevice
    {
        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        event EventHandler<bool> ConnectStatusEvent;

        /// <summary>
        /// 供外部硬触发流程
        /// </summary>
        event Action<Bitmap> OnImageReceived;
        /// <summary>
        /// 供外部硬触发流程使用的 OpenCV 图像帧，避免相机回调阶段执行 SDK ToBitmap。
        /// </summary>
        event Action<Mat> OnMatReceived;
        /// <summary>
        /// 注册需要相机帧回调的拥有者，首个拥有者会启用底层取流回调。
        /// </summary>
        /// <param name="owner">回调拥有者。</param>
        void RegisterImageCallbackOwner(object owner);
        /// <summary>
        /// 注销需要相机帧回调的拥有者，全部注销后底层回调不再接管和转换业务帧。
        /// </summary>
        /// <param name="owner">回调拥有者。</param>
        void UnregisterImageCallbackOwner(object owner);
        /// <summary>
        /// 硬件硬件名称
        /// </summary>
        string DevName { get; set; }
        /// <summary>
        /// 用户自定义设备名
        /// </summary>
        string UserDefinedName { get; set; }
        /// <summary>
        /// 相机是否连接
        /// </summary>
        bool IsOpen { get; set; }
        /// <summary>
        /// 获取方案反序列化后是否需要恢复上次的相机连接。
        /// </summary>
        bool RestoreConnectionRequested { get; }
        /// <summary>
        /// 设备序列号
        /// </summary>
        string SN { get; }
        /// <summary>
        /// 相机IP地址。
        /// </summary>
        string IP { get; set; }
        /// <summary>
        /// 相机品牌
        /// </summary>
        DeviceBrand Brand { get; set; }
        string ClassName { get; set; }
        /// <summary>
        /// 设备类型
        /// </summary>
        DevType DevType { get; set; }
        /// <summary>
        /// 图像获取超时时间
        /// </summary>
        uint GetImageTimeOut { get; set; }
        /// <summary>
        /// 创建设备，反序列化用
        /// </summary>
        void CreateDevice();
        /// <summary>
        /// 开启相机
        /// </summary>
        /// <returns></returns>
        bool Open();

        /// <summary>
        /// 开始取流
        /// </summary>
        /// <returns></returns>
        void StartGrabbing();

        /// <summary>
        /// 停止取流
        /// </summary>
        /// <returns></returns>
        void StopGrabbing();

        /// <summary>
        /// 获取相机取流状态
        /// </summary>
        /// <returns></returns>
        bool GetGrabStatus();

        /// <summary>
        /// 判断相机SDK当前连接是否仍然有效。
        /// </summary>
        /// <returns>SDK确认设备在线时返回true。</returns>
        bool IsDeviceConnected();

        /// <summary>
        /// 释放失效连接并按保存的相机身份重新枚举打开。
        /// </summary>
        /// <returns>重新打开成功时返回true。</returns>
        bool TryReconnect();

        /// <summary>
        /// 获取相机触发模式
        /// </summary>
        /// <returns></returns>
        TriggerModel GetTriggerMode();

        /// <summary>
        /// 设置相机触发模式
        /// </summary>
        /// <param name="isTrigger"></param>
        void SetTriggerMode(TriggerModel triggerModel);

        /// <summary>
        /// 设置软硬触发
        /// </summary>
        /// <param name="triggerSource"></param>
        /// <returns></returns>
        void SetTriggerSource(TriggerSource triggerSource);

        /// <summary>
        /// 采集一帧图像
        /// </summary>
        /// <returns></returns>
        Bitmap GetOneFrameImage();

        TriggerSource GetTriggerSource();

        /// <summary>
        /// 获取触发源 SDK 可选枚举。
        /// </summary>
        /// <returns>触发源 SDK 枚举值。</returns>
        CameraEnumValue GetTriggerSourceOptions();

        /// <summary>
        /// 获取触发极性 SDK 可选枚举。
        /// </summary>
        /// <returns>触发极性 SDK 枚举值。</returns>
        CameraEnumValue GetTriggerActivationOptions();

        /// <summary>
        /// 设置硬件触发时的触发沿
        /// </summary>
        /// <param name="triggerEdge"></param>
        /// <returns></returns>
        void SetTriggerEdge(TriggerEdge triggerEdge);

        /// <summary>
        /// 软触发一次
        /// </summary>
        /// <returns></returns>
        void GrabOne();

        /// <summary>
        /// 在真实触发前估算标准图像输出将持有的源Mat和灰度缓存最大字节数。
        /// </summary>
        /// <returns>基于当前相机宽高和转换格式的保守正数字节数。</returns>
        long GetProductionFrameMemoryEstimateBytes();

        /// <summary>
        ///  设置增益
        /// </summary>
        /// <param name="gainValue"></param>
        void SetGain(double gainValue);

        /// <summary>
        /// 获取相机曝光
        /// </summary>
        /// <returns></returns>
        CameraFloatValue GetExposureTime();

        /// <summary>
        /// 获取相机增益
        /// </summary>
        /// <returns></returns>
        (CameraIntValue, CameraFloatValue) GetGain();

        /// <summary>
        /// 获取触发延迟
        /// </summary>
        /// <returns></returns>
        CameraFloatValue GetTriggerDelay();

        /// <summary>
        /// 获取线路选择器
        /// </summary>
        CameraEnumValue GetLineSelector();

        /// <summary>
        /// 设置线路选择器
        /// </summary>
        void SetLineSelector(string line);

        /// <summary>
        /// 获取线路模式
        /// </summary>
        /// <returns></returns>
        CameraEnumValue GetLineMode();

        /// <summary>
        /// 设置线路模式
        /// </summary>
        /// <param name="lineMode"></param>
        void SetLineMode(string lineMode);

        /// <summary>
        /// 获取使能
        /// </summary>
        /// <returns></returns>
        bool GetStrobeEnable();

        /// <summary>
        /// 设置使能
        /// </summary>
        /// <param name="enable"></param>
        void SetStrobeEnable(bool enable);

        /// <summary>
        /// 设置线路反转
        /// </summary>
        /// <param name="inverter"></param>
        void SetLineInverter(bool inverter);

        /// <summary>
        /// 加锁设置线路信号
        /// </summary>
        void SetIO(string lineSelector, bool inverter);
        

        /// <summary>
        /// 设置曝光
        /// </summary>
        /// <param name="ExposureTime"></param>
        /// <returns></returns>
        void SetExposureTime(double time);

        /// <summary>
        /// 设置触发延迟
        /// </summary>
        /// <param name="time"></param>
        void SetTriggerDelay(double time);

        /// <summary>
        /// 关闭相机
        /// </summary>
        /// <returns></returns>
        void Close();

        /// <summary>
        /// 释放相机资源
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// 各个品牌相机设备信息
    /// </summary>
    public struct CameraDevInfo
    {
        public HikNativeDeviceInfo cameraInfo;
        public CameraDevInfo(HikNativeDeviceInfo Info)
        {
            cameraInfo = Info;
        }
    }

    /// <summary>
    /// 触发模式
    /// </summary>
    public enum TriggerModel
    {
        Off,
        On
    }

    /// <summary>
    /// 触发方式
    /// </summary>
    public enum TriggerSource 
    {
        Auto,
        SOFT,
        LINE0,
        LINE1,
        LINE2,
        LINE3,
        LINE4,
    }

    /// <summary>
    /// 硬触发设置的触发沿
    /// </summary>
    public enum TriggerEdge 
    {
        /// <summary>
        /// 上升沿
        /// </summary>
        Rising,
        /// <summary>
        /// 下降沿
        /// </summary>
        Falling,
        /// <summary>
        /// 高电平
        /// </summary>
        Hight,
        /// <summary>
        /// 低电平
        /// </summary>
        Low,
        /// <summary>
        /// 包括上升沿和下降沿
        /// </summary>
        Any,
    }

    /// <summary>
    /// 相机品牌
    /// </summary>
    public enum CameraBrand 
    {
        /// <summary>
        /// 海康威视
        /// </summary>
        HiKVision,
        /// <summary>
        /// 巴斯勒
        /// </summary>
        Basler,
        /// <summary>
        /// 大恒相机
        /// </summary>
        DaHeng,
        /// <summary>
        /// 大华相机
        /// </summary>
        DaHua,
        /// <summary>
        /// 华睿相机
        /// </summary>
        HuaRui,
        /// <summary>
        /// 其他
        /// </summary>
        Other
    }
}
