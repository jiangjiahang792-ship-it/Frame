using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MvCamCtrl.NET;
using OpenCvSharp;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 管理单台海康相机的官方回调、像素转换缓冲、Mat生命周期和计时记录。
    /// </summary>
    internal sealed class CameraBenchmarkSession : IDisposable
    {
        /// <summary>相机SDK对象。</summary>
        private MyCamera _camera;

        /// <summary>保持强引用的SDK回调委托。</summary>
        private MyCamera.cbOutputExdelegate _callback;

        /// <summary>SDK像素转换使用的预分配非托管缓冲区。</summary>
        private IntPtr _conversionBuffer;

        /// <summary>转换缓冲区容量。</summary>
        private uint _conversionBufferSize;

        /// <summary>保护单相机转换缓冲区和停止释放的锁。</summary>
        private readonly object _callbackSyncRoot = new object();

        /// <summary>容量为1的UI最新帧槽位。</summary>
        private readonly LatestMatSlot _latestFrame = new LatestMatSlot();

        /// <summary>本轮回调计时器。</summary>
        private readonly TimingCollector _timingCollector;

        /// <summary>本轮启动时的高精度时间戳。</summary>
        private long _startedTimestamp;

        /// <summary>是否已成功打开设备。</summary>
        private int _opened;

        /// <summary>是否正在取流。</summary>
        private int _grabbing;

        /// <summary>回调异常数量。</summary>
        private long _callbackErrorCount;

        /// <summary>最近一次错误文本。</summary>
        private string _lastError;

        /// <summary>
        /// 创建单相机测试会话。
        /// </summary>
        /// <param name="slotNumber">界面相机槽位编号。</param>
        /// <param name="deviceItem">海康设备信息。</param>
        /// <param name="settings">本轮测试参数。</param>
        public CameraBenchmarkSession(int slotNumber, CameraDeviceItem deviceItem, BenchmarkSettings settings)
        {
            if (deviceItem == null)
                throw new ArgumentNullException(nameof(deviceItem));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            SlotNumber = slotNumber;
            DeviceItem = deviceItem;
            Settings = settings;
            _timingCollector = new TimingCollector(settings.WarmupFrames, settings.SampleFrames);
        }

        /// <summary>界面相机槽位编号。</summary>
        public int SlotNumber { get; private set; }

        /// <summary>本会话连接的设备。</summary>
        public CameraDeviceItem DeviceItem { get; private set; }

        /// <summary>本轮测试参数。</summary>
        public BenchmarkSettings Settings { get; private set; }

        /// <summary>回调阶段计时器。</summary>
        public TimingCollector TimingCollector
        {
            get { return _timingCollector; }
        }

        /// <summary>UI最新帧槽位。</summary>
        public LatestMatSlot LatestFrame
        {
            get { return _latestFrame; }
        }

        /// <summary>回调异常数量。</summary>
        public long CallbackErrorCount
        {
            get { return Interlocked.Read(ref _callbackErrorCount); }
        }

        /// <summary>最近一次回调或SDK错误。</summary>
        public string LastError
        {
            get { return Volatile.Read(ref _lastError); }
        }

        /// <summary>
        /// 打开设备、配置触发方式、注册官方回调并开始取流。
        /// </summary>
        public void Start()
        {
            if (Interlocked.CompareExchange(ref _opened, 1, 0) != 0)
                throw new InvalidOperationException("相机会话已经打开。");

            _camera = new MyCamera();
            MyCamera.MV_CC_DEVICE_INFO deviceInfo = DeviceItem.DeviceInfo;
            try
            {
                EnsureSuccess(_camera.MV_CC_CreateDevice_NET(ref deviceInfo), "创建设备");
                EnsureSuccess(_camera.MV_CC_OpenDevice_NET(), "打开设备");
                ConfigureNetwork(deviceInfo);
                ConfigureTrigger();

                _callback = ImageCallback;
                EnsureSuccess(
                    _camera.MV_CC_RegisterImageCallBackEx_NET(_callback, IntPtr.Zero),
                    "注册图像回调");

                _startedTimestamp = Stopwatch.GetTimestamp();
                Volatile.Write(ref _grabbing, 1);
                EnsureSuccess(_camera.MV_CC_StartGrabbing_NET(), "开始采集");
            }
            catch
            {
                Stop();
                throw;
            }
        }

        /// <summary>
        /// 停止取流并释放设备和转换缓冲区。
        /// </summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref _grabbing, 0) != 0 && _camera != null)
            {
                _camera.MV_CC_StopGrabbing_NET();
            }

            lock (_callbackSyncRoot)
            {
                if (_camera != null && Interlocked.Exchange(ref _opened, 0) != 0)
                {
                    _camera.MV_CC_CloseDevice_NET();
                    _camera.MV_CC_DestroyDevice_NET();
                }

                _camera = null;
                _callback = null;
                ReleaseConversionBuffer();
            }

            _latestFrame.Clear();
        }

        /// <summary>
        /// 释放会话持有的全部资源。
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 海康SDK图像回调：转换到预分配内存，再建立并复制独立Mat。
        /// </summary>
        /// <param name="sourceData">SDK帧数据地址。</param>
        /// <param name="frameInfo">SDK帧信息。</param>
        /// <param name="userData">用户数据，本Demo未使用。</param>
        private void ImageCallback(
            IntPtr sourceData,
            ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo,
            IntPtr userData)
        {
            if (Volatile.Read(ref _grabbing) == 0)
                return;

            long callbackStarted = Stopwatch.GetTimestamp();
            bool shouldRecord = true;
            FrameTimingRecord record = new FrameTimingRecord
            {
                ElapsedTicks = callbackStarted - _startedTimestamp,
                FrameNumber = frameInfo.nFrameNum,
                Width = frameInfo.nWidth,
                Height = frameInfo.nHeight,
                SourcePixelType = frameInfo.enPixelType,
                ResultCode = MyCamera.MV_OK
            };

            Mat independentMat = null;
            try
            {
                lock (_callbackSyncRoot)
                {
                    if (Volatile.Read(ref _grabbing) == 0)
                    {
                        shouldRecord = false;
                        return;
                    }

                    int channelCount;
                    MyCamera.MvGvspPixelType destinationPixelType = ResolveDestinationPixelType(
                        frameInfo.enPixelType,
                        out channelCount);
                    record.DestinationPixelType = destinationPixelType;

                    ulong requiredByteCount = checked(
                        (ulong)frameInfo.nWidth * frameInfo.nHeight * (uint)channelCount);
                    if (requiredByteCount > int.MaxValue)
                        throw new InvalidOperationException("转换后的单帧图像超过可分配上限。");
                    uint requiredBytes = (uint)requiredByteCount;
                    EnsureConversionBuffer(requiredBytes);

                    MyCamera.MV_PIXEL_CONVERT_PARAM convertParameter = new MyCamera.MV_PIXEL_CONVERT_PARAM
                    {
                        nWidth = frameInfo.nWidth,
                        nHeight = frameInfo.nHeight,
                        pSrcData = sourceData,
                        nSrcDataLen = frameInfo.nFrameLen,
                        enSrcPixelType = frameInfo.enPixelType,
                        enDstPixelType = destinationPixelType,
                        pDstBuffer = _conversionBuffer,
                        nDstBufferSize = _conversionBufferSize
                    };

                    long convertStarted = Stopwatch.GetTimestamp();
                    int result = _camera.MV_CC_ConvertPixelType_NET(ref convertParameter);
                    long convertCompleted = Stopwatch.GetTimestamp();
                    record.ConvertTicks = convertCompleted - convertStarted;
                    record.ResultCode = result;
                    if (result != MyCamera.MV_OK)
                        throw new InvalidOperationException(string.Format("像素转换失败，错误码：0x{0:X8}", result));

                    MatType matType = channelCount == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                    long wrapStarted = Stopwatch.GetTimestamp();
                    using (Mat bufferView = Mat.FromPixelData(
                        checked((int)frameInfo.nHeight),
                        checked((int)frameInfo.nWidth),
                        matType,
                        _conversionBuffer))
                    {
                        long wrapCompleted = Stopwatch.GetTimestamp();
                        record.MatWrapTicks = wrapCompleted - wrapStarted;

                        long cloneStarted = Stopwatch.GetTimestamp();
                        independentMat = bufferView.Clone();
                        long cloneCompleted = Stopwatch.GetTimestamp();
                        record.MatCloneTicks = cloneCompleted - cloneStarted;
                    }
                }

                long publishStarted = Stopwatch.GetTimestamp();
                _latestFrame.Publish(independentMat);
                independentMat = null;
                long publishCompleted = Stopwatch.GetTimestamp();
                record.PublishTicks = publishCompleted - publishStarted;
            }
            catch (Exception exception)
            {
                independentMat?.Dispose();
                Interlocked.Increment(ref _callbackErrorCount);
                Volatile.Write(ref _lastError, exception.Message);
                if (record.ResultCode == MyCamera.MV_OK)
                {
                    record.ResultCode = unchecked((int)0x80000000);
                }
            }
            finally
            {
                if (shouldRecord)
                {
                    record.CallbackTicks = Stopwatch.GetTimestamp() - callbackStarted;
                    _timingCollector.Add(record);
                }
            }
        }

        /// <summary>
        /// 为GigE相机设置SDK探测出的最佳网络包大小。
        /// </summary>
        /// <param name="deviceInfo">SDK设备信息。</param>
        private void ConfigureNetwork(MyCamera.MV_CC_DEVICE_INFO deviceInfo)
        {
            if (deviceInfo.nTLayerType != MyCamera.MV_GIGE_DEVICE)
                return;

            int packetSize = _camera.MV_CC_GetOptimalPacketSize_NET();
            if (packetSize > 0)
            {
                int result = _camera.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", packetSize);
                if (result != MyCamera.MV_OK)
                {
                    Volatile.Write(
                        ref _lastError,
                        string.Format("设置最佳网络包大小失败，错误码：0x{0:X8}", result));
                }
            }
        }

        /// <summary>
        /// 配置连续采集或Line0硬件触发。
        /// </summary>
        private void ConfigureTrigger()
        {
            EnsureSuccess(
                _camera.MV_CC_SetEnumValue_NET(
                    "AcquisitionMode",
                    (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS),
                "设置连续采集模式");

            if (Settings.TriggerMode == CameraTriggerMode.Line0)
            {
                EnsureSuccess(
                    _camera.MV_CC_SetEnumValue_NET(
                        "TriggerMode",
                        (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON),
                    "开启触发模式");
                EnsureSuccess(
                    _camera.MV_CC_SetEnumValue_NET(
                        "TriggerSource",
                        (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0),
                    "设置Line0触发源");
            }
            else
            {
                EnsureSuccess(
                    _camera.MV_CC_SetEnumValue_NET(
                        "TriggerMode",
                        (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF),
                    "关闭触发模式");
            }
        }

        /// <summary>
        /// 根据输入格式选择Mono8或BGR8输出。
        /// </summary>
        /// <param name="sourcePixelType">输入像素格式。</param>
        /// <param name="channelCount">输出通道数。</param>
        /// <returns>SDK目标像素格式。</returns>
        private static MyCamera.MvGvspPixelType ResolveDestinationPixelType(
            MyCamera.MvGvspPixelType sourcePixelType,
            out int channelCount)
        {
            if (IsMonoPixelFormat(sourcePixelType))
            {
                channelCount = 1;
                return MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8;
            }

            if (IsColorPixelFormat(sourcePixelType))
            {
                channelCount = 3;
                return MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
            }

            throw new NotSupportedException("不支持的输入像素格式：" + sourcePixelType);
        }

        /// <summary>
        /// 判断是否为可转换为Mono8的单色格式。
        /// </summary>
        /// <param name="pixelType">输入像素格式。</param>
        /// <returns>是单色格式时返回true。</returns>
        private static bool IsMonoPixelFormat(MyCamera.MvGvspPixelType pixelType)
        {
            switch (pixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断是否为可转换为BGR8的彩色格式。
        /// </summary>
        /// <param name="pixelType">输入像素格式。</param>
        /// <returns>是彩色格式时返回true。</returns>
        private static bool IsColorPixelFormat(MyCamera.MvGvspPixelType pixelType)
        {
            switch (pixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 确保转换缓冲区足够大；只在首帧或分辨率增大时重新分配。
        /// </summary>
        /// <param name="requiredBytes">目标缓冲区字节数。</param>
        private void EnsureConversionBuffer(uint requiredBytes)
        {
            if (_conversionBuffer != IntPtr.Zero && _conversionBufferSize >= requiredBytes)
                return;

            ReleaseConversionBuffer();
            _conversionBuffer = Marshal.AllocHGlobal(checked((int)requiredBytes));
            if (_conversionBuffer == IntPtr.Zero)
                throw new OutOfMemoryException("无法分配相机像素转换缓冲区。");
            _conversionBufferSize = requiredBytes;
        }

        /// <summary>
        /// 释放非托管像素转换缓冲区。
        /// </summary>
        private void ReleaseConversionBuffer()
        {
            if (_conversionBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_conversionBuffer);
                _conversionBuffer = IntPtr.Zero;
                _conversionBufferSize = 0;
            }
        }

        /// <summary>
        /// 检查SDK返回码并转换为包含操作名称的异常。
        /// </summary>
        /// <param name="result">海康SDK返回码。</param>
        /// <param name="operation">操作名称。</param>
        private static void EnsureSuccess(int result, string operation)
        {
            if (result != MyCamera.MV_OK)
                throw new InvalidOperationException(string.Format("{0}失败，错误码：0x{1:X8}", operation, result));
        }
    }
}
