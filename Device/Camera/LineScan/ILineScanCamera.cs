using System;
using System.Collections.Generic;
using OpenCvSharp;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Device.Camera.LineScan
{
    /// <summary>
    /// 定义线扫相机独有的设备能力，避免把行频、双层触发和编码器参数混入面阵相机接口。
    /// </summary>
    public interface ILineScanCamera : IDevice, IDisposable
    {
        /// <summary>相机输出一幅完整线扫帧时触发；订阅者接管收到的Mat生命周期。</summary>
        event Action<Mat> FrameReceived;

        /// <summary>获取相机是否已经打开。</summary>
        bool IsOpen { get; }

        /// <summary>获取相机是否正在取流。</summary>
        bool IsGrabbing { get; }

        /// <summary>获取设备序列号。</summary>
        string SerialNumber { get; }

        /// <summary>获取设备IP地址。</summary>
        string IpAddress { get; }

        /// <summary>获取相机当前像素格式的GenICam符号名称。</summary>
        string CurrentPixelFormat { get; }

        /// <summary>获取相机PixelFormat节点实际支持的全部格式。</summary>
        IReadOnlyList<string> SupportedPixelFormats { get; }

        /// <summary>获取或设置方案加载后是否自动打开相机。</summary>
        bool AutoConnectOnLoad { get; set; }

        /// <summary>打开相机。</summary>
        /// <returns>打开成功时返回true。</returns>
        bool Open();

        /// <summary>关闭相机。</summary>
        void Close();

        /// <summary>开始取流。</summary>
        void StartGrabbing();

        /// <summary>停止取流。</summary>
        void StopGrabbing();

        /// <summary>执行一次海康TriggerSoftware命令；调用前必须已经启动取流。</summary>
        void ExecuteSoftwareTrigger();

        /// <summary>等待并取得一帧已经由相机按图像高度拼接完成的线扫图像。</summary>
        /// <param name="timeoutMilliseconds">等待超时时间，单位为毫秒。</param>
        /// <returns>调用方负责释放的独立图像。</returns>
        Mat GetOneFrame(int timeoutMilliseconds);

        /// <summary>直接写入流程节点配置的线扫采集参数。</summary>
        /// <param name="settings">待应用参数。</param>
        void ApplyAcquisitionSettings(LineScanCameraSettings settings);

        /// <summary>读取相机实际支持的触发选择器。</summary>
        IReadOnlyList<LineScanTriggerSelector> GetSupportedTriggerSelectors();

        /// <summary>读取指定触发选择器下相机实际支持的触发源。</summary>
        IReadOnlyList<TriggerSource> GetSupportedTriggerSources(LineScanTriggerSelector selector);

        /// <summary>读取指定触发选择器和线路触发源下相机实际支持的触发极性。</summary>
        IReadOnlyList<TriggerEdge> GetSupportedTriggerEdges(LineScanTriggerSelector selector, TriggerSource source);
    }
}
