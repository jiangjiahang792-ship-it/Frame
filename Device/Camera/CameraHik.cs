using Logger;
using MvCamCtrl.NET;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TDJS_Vision.Diagnostics;

namespace TDJS_Vision.Device.Camera
{
    /// <summary>
    /// 使用海康官方MVCamera类直接访问原生SDK的二维相机实现。
    /// </summary>
    public class CameraHik : ICamera
    {
        /// <summary>SDK全局初始化状态，0表示未初始化，1表示已经初始化。</summary>
        private static int _sdkInitialized;

        /// <summary>保护相机打开、关闭和销毁操作。</summary>
        private readonly object _deviceLock = new object();

        /// <summary>保护回调拥有者集合与原生回调注册状态。</summary>
        private readonly object _imageCallbackLock = new object();

        /// <summary>保护单台相机的预分配转换缓冲区和关闭过程。</summary>
        private readonly object _callbackSyncRoot = new object();

        /// <summary>保护线路选择与线路反转组成的原子IO设置。</summary>
        private readonly object _ioLock = new object();

        /// <summary>需要接收相机帧的业务拥有者集合。</summary>
        private readonly HashSet<object> _imageCallbackOwners = new HashSet<object>();

        /// <summary>海康官方C#类实例。</summary>
        private MyCamera _device;

        /// <summary>当前相机枚举信息。</summary>
        private HikNativeDeviceInfo _deviceInfo;

        /// <summary>必须保持强引用的SDK图像回调委托。</summary>
        private MyCamera.cbOutputExdelegate _nativeImageCallback;

        /// <summary>预分配的像素格式转换缓冲区。</summary>
        private IntPtr _conversionBuffer = IntPtr.Zero;

        /// <summary>当前转换缓冲区容量，单位为字节。</summary>
        private uint _conversionBufferSize;

        /// <summary>当前原生设备句柄是否已经创建。</summary>
        private bool _deviceCreated;

        /// <summary>原生图像回调是否已经注册到当前句柄。</summary>
        private bool _callbackRegistered;

        /// <summary>当前相机是否正在取流。</summary>
        private bool _isGrabbing;

        /// <summary>当前相机回调帧序号，用于跨线程耗时诊断。</summary>
        private long _callbackFrameSequence;

        /// <summary>连接状态改变事件。</summary>
        public event EventHandler<bool> ConnectStatusEvent;

        /// <summary>兼容旧接口保留的Bitmap图像事件；当前主链路使用Mat事件。</summary>
        public event Action<Bitmap> OnImageReceived;

        /// <summary>硬触发流程使用的Mat图像事件。</summary>
        public event Action<Mat> OnMatReceived;

        /// <summary>获取或设置设备是否已经打开。</summary>
        public bool IsOpen { get; set; }

        /// <summary>获取或设置当前触发模式。</summary>
        public TriggerModel TriggerModel { get; set; }

        /// <summary>获取或设置当前触发源。</summary>
        public TriggerSource TriggerSource { get; set; }

        /// <summary>获取或设置设备类型。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.CAMERA;

        /// <summary>获取或设置相机序列号。</summary>
        public string SN { get; set; }

        /// <summary>获取或设置相机IP地址。</summary>
        public string IP { get; set; } = "无";

        /// <summary>获取或设置相机制造商名称。</summary>
        public string ManufacturerName { get; set; }

        /// <summary>获取或设置设备品牌。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceBrand Brand { get; set; }

        /// <summary>获取或设置用于反序列化的运行时类名。</summary>
        public string ClassName { get; set; } = typeof(CameraHik).FullName;

        /// <summary>获取或设置硬件设备名称。</summary>
        public string DevName { get; set; }

        /// <summary>获取或设置用户自定义设备名称。</summary>
        public string UserDefinedName { get; set; }

        /// <summary>获取或设置主动取图超时时间，单位为毫秒。</summary>
        public uint GetImageTimeOut { get; set; } = 2000;

        /// <summary>创建用于反序列化的空相机对象。</summary>
        [JsonConstructor]
        public CameraHik()
        {
        }

        /// <summary>使用原生设备信息创建相机对象。</summary>
        /// <param name="deviceInfo">相机枚举信息。</param>
        /// <param name="userName">框架内的用户自定义名称。</param>
        internal CameraHik(HikNativeDeviceInfo deviceInfo, string userName)
        {
            if (deviceInfo == null)
                throw new ArgumentNullException(nameof(deviceInfo));

            UserDefinedName = userName;
            SetDeviceIdentity(deviceInfo);
            CreateNativeDevice(deviceInfo);
        }

        /// <summary>初始化海康原生SDK；多次调用只执行一次。</summary>
        public static void InitSDK()
        {
            if (Interlocked.CompareExchange(ref _sdkInitialized, 1, 0) != 0)
                return;

            int result = MyCamera.MV_CC_Initialize_NET();
            if (result != MyCamera.MV_OK)
            {
                Interlocked.Exchange(ref _sdkInitialized, 0);
                throw CreateSdkException("初始化海康SDK", result);
            }
        }

        /// <summary>反初始化海康原生SDK。</summary>
        public static void FinalizeSDK()
        {
            if (Interlocked.Exchange(ref _sdkInitialized, 0) == 0)
                return;

            int result = MyCamera.MV_CC_Finalize_NET();
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("反初始化海康SDK", result), true);
        }

        /// <summary>枚举GigE和USB二维相机。</summary>
        /// <returns>原生相机设备信息列表。</returns>
        public static List<HikNativeDeviceInfo> FindCamera()
        {
            InitSDK();
            MyCamera.MV_CC_DEVICE_INFO_LIST deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            int result = MyCamera.MV_CC_EnumDevices_NET(
                MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE,
                ref deviceList);
            if (result != MyCamera.MV_OK)
                throw CreateSdkException("枚举相机", result);

            List<HikNativeDeviceInfo> devices = new List<HikNativeDeviceInfo>();
            for (int index = 0; index < deviceList.nDeviceNum; index++)
            {
                MyCamera.MV_CC_DEVICE_INFO nativeInfo =
                    (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
                        deviceList.pDeviceInfo[index],
                        typeof(MyCamera.MV_CC_DEVICE_INFO));
                devices.Add(ParseDeviceInfo(index, nativeInfo));
            }

            return devices;
        }

        /// <summary>根据设备信息生成界面显示名称。</summary>
        /// <param name="cameraDevInfo">原生相机设备信息。</param>
        /// <returns>设备显示名称。</returns>
        public static string GetDevNameByDevInfo(HikNativeDeviceInfo cameraDevInfo)
        {
            if (cameraDevInfo == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(cameraDevInfo.UserDefinedName))
                return string.Format("{0}({1})", cameraDevInfo.UserDefinedName, cameraDevInfo.SerialNumber);
            return string.Format("{0}{1} ({2})", cameraDevInfo.ManufacturerName, cameraDevInfo.ModelName, cameraDevInfo.SerialNumber);
        }

        /// <summary>反序列化后根据序列号重新枚举并创建原生设备句柄。</summary>
        public void CreateDevice()
        {
            try
            {
                HikNativeDeviceInfo deviceInfo = FindCamera()
                    .FirstOrDefault(item => string.Equals(item.SerialNumber, SN, StringComparison.OrdinalIgnoreCase));
                if (deviceInfo == null)
                    throw new InvalidOperationException(string.Format("未找到序列号为{0}的相机。", SN));

                SetDeviceIdentity(deviceInfo);
                CreateNativeDevice(deviceInfo);
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, exception.Message, true);
            }
        }

        /// <summary>打开原生相机并配置GigE最佳包大小。</summary>
        /// <returns>打开成功返回true。</returns>
        public bool Open()
        {
            lock (_deviceLock)
            {
                if (IsOpen)
                    return true;
                if (_device == null || !_deviceCreated)
                {
                    HikNativeDeviceInfo deviceInfo = FindCamera()
                        .FirstOrDefault(item => string.Equals(item.SerialNumber, SN, StringComparison.OrdinalIgnoreCase));
                    if (deviceInfo == null)
                        throw new InvalidOperationException(string.Format("相机（{0}）不在线。", UserDefinedName));
                    SetDeviceIdentity(deviceInfo);
                    CreateNativeDeviceCore(deviceInfo);
                }

                int result = _device.MV_CC_OpenDevice_NET();
                if (result != MyCamera.MV_OK)
                    throw CreateSdkException(string.Format("打开相机（{0}）", UserDefinedName), result);

                try
                {
                    ConfigureGigENetwork();
                    TrySetEnumValue("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                    IsOpen = true;
                    TriggerSource = GetTriggerSource();
                    lock (_imageCallbackLock)
                        EnsureNativeCallbackRegisteredCore();
                }
                catch
                {
                    _device.MV_CC_CloseDevice_NET();
                    IsOpen = false;
                    throw;
                }
            }

            ConnectStatusEvent?.Invoke(this, true);
            return true;
        }

        /// <summary>注册需要相机帧回调的拥有者。</summary>
        /// <param name="owner">回调拥有者。</param>
        public void RegisterImageCallbackOwner(object owner)
        {
            if (owner == null)
                return;
            lock (_imageCallbackLock)
            {
                _imageCallbackOwners.Add(owner);
                EnsureNativeCallbackRegisteredCore();
            }
        }

        /// <summary>注销相机帧回调拥有者。</summary>
        /// <param name="owner">回调拥有者。</param>
        public void UnregisterImageCallbackOwner(object owner)
        {
            if (owner == null)
                return;
            lock (_imageCallbackLock)
                _imageCallbackOwners.Remove(owner);
        }

        /// <summary>开始相机取流。</summary>
        public void StartGrabbing()
        {
            lock (_deviceLock)
            {
                EnsureDeviceOpen();
                if (_isGrabbing)
                    return;

                lock (_imageCallbackLock)
                    EnsureNativeCallbackRegisteredCore();
                EnsureSuccess(_device.MV_CC_SetImageNodeNum_NET(1), "设置相机取流缓存节点");

                Volatile.Write(ref _isGrabbing, true);
                int result = _device.MV_CC_StartGrabbing_NET();
                if (result != MyCamera.MV_OK)
                {
                    Volatile.Write(ref _isGrabbing, false);
                    throw CreateSdkException("开始相机取流", result);
                }
            }
        }

        /// <summary>停止相机取流。</summary>
        public void StopGrabbing()
        {
            lock (_deviceLock)
            {
                if (_device == null || !_isGrabbing)
                    return;

                Volatile.Write(ref _isGrabbing, false);
                int result = _device.MV_CC_StopGrabbing_NET();
                if (result != MyCamera.MV_OK)
                    throw CreateSdkException("停止相机取流", result);

                lock (_callbackSyncRoot)
                {
                }
            }
        }

        /// <summary>获取当前取流状态。</summary>
        /// <returns>正在取流时返回true。</returns>
        public bool GetGrabStatus()
        {
            return Volatile.Read(ref _isGrabbing);
        }

        /// <summary>主动获取一帧图像并转换为独立Bitmap。</summary>
        /// <returns>成功返回Bitmap，失败返回null。</returns>
        public Bitmap GetOneFrameImage()
        {
            EnsureDeviceOpen();
            MyCamera.MV_FRAME_OUT frame = new MyCamera.MV_FRAME_OUT();
            try
            {
                int clearResult = _device.MV_CC_ClearImageBuffer_NET();
                if (clearResult != MyCamera.MV_OK)
                    LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("清空相机图像缓存", clearResult), true);
                if (TriggerSource == TriggerSource.SOFT)
                    GrabOne();

                int timeout = GetImageTimeOut > int.MaxValue ? int.MaxValue : (int)GetImageTimeOut;
                int result = _device.MV_CC_GetImageBuffer_NET(ref frame, timeout);
                if (result != MyCamera.MV_OK)
                {
                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        string.Format("{0}主动取图失败，错误码：0x{1:X8}，时间：{2:yyyy-MM-dd HH:mm:ss.fff}", DevName, result, DateTime.Now),
                        true);
                    return null;
                }

                using (Mat mat = ConvertFrameToIndependentMat(frame.pBufAddr, ref frame.stFrameInfo, 0, false))
                    return mat == null ? null : BitmapConverter.ToBitmap(mat);
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, string.Format("{0}主动取图异常：{1}", DevName, exception.Message), true);
                return null;
            }
            finally
            {
                if (frame.pBufAddr != IntPtr.Zero)
                    _device.MV_CC_FreeImageBuffer_NET(ref frame);
            }
        }

        /// <summary>获取相机触发模式。</summary>
        /// <returns>当前触发模式。</returns>
        public TriggerModel GetTriggerMode()
        {
            CameraEnumValue value = GetEnumValue("TriggerMode");
            CameraEnumEntry currentEntry = value.CurEnumEntry;
            TriggerModel = currentEntry != null
                && (string.Equals(currentEntry.Symbolic, "On", StringComparison.OrdinalIgnoreCase) || currentEntry.Value == 1)
                    ? TriggerModel.On
                    : TriggerModel.Off;
            return TriggerModel;
        }

        /// <summary>设置相机触发模式。</summary>
        /// <param name="triggerModel">目标触发模式。</param>
        public void SetTriggerMode(TriggerModel triggerModel)
        {
            EnsureDeviceOpen();
            string symbolic = triggerModel == TriggerModel.On ? "On" : "Off";
            int result = _device.MV_CC_SetEnumValueByString_NET("TriggerMode", symbolic);
            if (result != MyCamera.MV_OK)
                result = _device.MV_CC_SetEnumValue_NET("TriggerMode", triggerModel == TriggerModel.On ? 1u : 0u);
            EnsureSuccess(result, "设置触发模式");
            TriggerModel = triggerModel;
        }

        /// <summary>获取相机触发源。</summary>
        /// <returns>当前触发源。</returns>
        public TriggerSource GetTriggerSource()
        {
            if (GetTriggerMode() == TriggerModel.Off)
            {
                TriggerSource = TriggerSource.Auto;
                return TriggerSource;
            }

            TriggerSource = MapTriggerSource(GetEnumValue("TriggerSource").CurEnumEntry);
            return TriggerSource;
        }

        /// <summary>获取相机支持的触发源枚举项。</summary>
        /// <returns>触发源参数。</returns>
        public CameraEnumValue GetTriggerSourceOptions()
        {
            return GetEnumValue("TriggerSource");
        }

        /// <summary>获取相机支持的触发极性枚举项。</summary>
        /// <returns>触发极性参数。</returns>
        public CameraEnumValue GetTriggerActivationOptions()
        {
            if (!IsLineTriggerSource(GetTriggerSource()))
                return CreateUnavailableEnumValue();

            MyCamera.MV_XML_AccessMode accessMode;
            if (TryGetNodeAccessMode("TriggerActivation", out accessMode) && !IsNodeReadable(accessMode))
                return CreateUnavailableEnumValue();

            return GetEnumValue("TriggerActivation");
        }

        /// <summary>设置连续、软件或线路触发源。</summary>
        /// <param name="triggerSource">目标触发源。</param>
        public void SetTriggerSource(TriggerSource triggerSource)
        {
            EnsureDeviceOpen();
            if (triggerSource == TriggerSource.Auto)
            {
                SetTriggerMode(TriggerModel.Off);
                TriggerSource = triggerSource;
                return;
            }

            string symbolic = GetTriggerSourceSymbolic(triggerSource);
            int result = _device.MV_CC_SetEnumValueByString_NET("TriggerSource", symbolic);
            if (result != MyCamera.MV_OK)
            {
                uint fallbackValue;
                if (!TryGetOfficialTriggerSourceValue(triggerSource, out fallbackValue))
                    throw new NotSupportedException(string.Format("当前相机不支持触发源：{0}。", symbolic));
                result = _device.MV_CC_SetEnumValue_NET("TriggerSource", fallbackValue);
            }

            EnsureSuccess(result, "设置触发源");
            TriggerSource = triggerSource;
        }

        /// <summary>设置硬件触发极性。</summary>
        /// <param name="triggerEdge">目标触发极性。</param>
        public void SetTriggerEdge(TriggerEdge triggerEdge)
        {
            EnsureDeviceOpen();
            if (!IsLineTriggerSource(GetTriggerSource()))
                throw new InvalidOperationException("只有线路硬触发模式可以设置触发极性。");
            EnsureNodeWritable("TriggerActivation", "设置触发极性");

            string symbolic;
            switch (triggerEdge)
            {
                case TriggerEdge.Rising:
                    symbolic = "RisingEdge";
                    break;
                case TriggerEdge.Falling:
                    symbolic = "FallingEdge";
                    break;
                case TriggerEdge.Low:
                    symbolic = "LevelLow";
                    break;
                case TriggerEdge.Hight:
                    symbolic = "LevelHigh";
                    break;
                case TriggerEdge.Any:
                    symbolic = "AnyEdge";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(triggerEdge));
            }
            EnsureSuccess(_device.MV_CC_SetEnumValueByString_NET("TriggerActivation", symbolic), "设置触发极性");
        }

        /// <summary>执行一次软件触发。</summary>
        public void GrabOne()
        {
            EnsureDeviceOpen();
            EnsureSuccess(_device.MV_CC_SetCommandValue_NET("TriggerSoftware"), "执行软件触发");
        }

        /// <summary>获取当前曝光时间。</summary>
        /// <returns>曝光时间参数。</returns>
        public CameraFloatValue GetExposureTime()
        {
            return GetFloatValue(IsBaslerManufacturer() ? "ExposureTimeAbs" : "ExposureTime");
        }

        /// <summary>获取当前增益。</summary>
        /// <returns>整数增益或浮点增益参数。</returns>
        public (CameraIntValue, CameraFloatValue) GetGain()
        {
            if (IsBaslerManufacturer())
                return (GetIntValue("GainRaw"), null);
            return (null, GetFloatValue("Gain"));
        }

        /// <summary>获取当前触发延迟。</summary>
        /// <returns>触发延迟参数。</returns>
        public CameraFloatValue GetTriggerDelay()
        {
            return GetFloatValue(IsBaslerManufacturer() ? "TriggerDelayAbs" : "TriggerDelay");
        }

        /// <summary>获取线路选择器参数。</summary>
        /// <returns>线路选择器参数。</returns>
        public CameraEnumValue GetLineSelector()
        {
            return GetEnumValue("LineSelector");
        }

        /// <summary>设置当前线路选择器。</summary>
        /// <param name="line">Line0至Line4。</param>
        public void SetLineSelector(string line)
        {
            EnsureDeviceOpen();
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("线路选择器不能为空。", nameof(line));

            int result = _device.MV_CC_SetEnumValueByString_NET("LineSelector", line);
            if (result != MyCamera.MV_OK && line.StartsWith("Line", StringComparison.OrdinalIgnoreCase))
            {
                uint lineValue;
                if (uint.TryParse(line.Substring(4), out lineValue))
                    result = _device.MV_CC_SetEnumValue_NET("LineSelector", lineValue);
            }
            EnsureSuccess(result, "设置线路选择器");
        }

        /// <summary>获取当前线路模式参数。</summary>
        /// <returns>线路模式参数。</returns>
        public CameraEnumValue GetLineMode()
        {
            return GetEnumValue("LineMode");
        }

        /// <summary>设置当前线路为输入或输出模式。</summary>
        /// <param name="lineMode">中文或英文线路模式。</param>
        public void SetLineMode(string lineMode)
        {
            EnsureDeviceOpen();
            string symbolic = lineMode == "输入" || string.Equals(lineMode, "Input", StringComparison.OrdinalIgnoreCase)
                ? "Input"
                : lineMode == "输出" || string.Equals(lineMode, "Output", StringComparison.OrdinalIgnoreCase)
                    ? "Output"
                    : null;
            if (symbolic == null)
                throw new ArgumentException("不支持的线路模式：" + lineMode, nameof(lineMode));

            int result = _device.MV_CC_SetEnumValueByString_NET("LineMode", symbolic);
            if (result != MyCamera.MV_OK)
            {
                uint fallbackValue = symbolic == "Input" ? 0u : IsHuarayManufacturer() ? 1u : 8u;
                result = _device.MV_CC_SetEnumValue_NET("LineMode", fallbackValue);
            }
            EnsureSuccess(result, "设置线路模式");
        }

        /// <summary>获取闪光灯输出使能状态。</summary>
        /// <returns>已经使能时返回true。</returns>
        public bool GetStrobeEnable()
        {
            EnsureDeviceOpen();
            bool enabled = false;
            int result = _device.MV_CC_GetBoolValue_NET("StrobeEnable", ref enabled);
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("读取闪光灯使能", result), true);
            return enabled;
        }

        /// <summary>设置闪光灯输出使能状态。</summary>
        /// <param name="enable">目标使能状态。</param>
        public void SetStrobeEnable(bool enable)
        {
            EnsureDeviceOpen();
            EnsureSuccess(_device.MV_CC_SetBoolValue_NET("StrobeEnable", enable), "设置闪光灯使能");
        }

        /// <summary>设置当前线路的输出反转状态。</summary>
        /// <param name="inverter">是否反转输出。</param>
        public void SetLineInverter(bool inverter)
        {
            EnsureDeviceOpen();
            EnsureSuccess(_device.MV_CC_SetBoolValue_NET("LineInverter", inverter), "设置线路反转");
        }

        /// <summary>在线程安全边界内设置线路选择器和输出反转状态。</summary>
        /// <param name="lineSelector">线路选择器。</param>
        /// <param name="inverter">输出反转状态。</param>
        public void SetIO(string lineSelector, bool inverter)
        {
            lock (_ioLock)
            {
                SetLineSelector(lineSelector);
                SetLineInverter(inverter);
            }
        }

        /// <summary>设置手动增益。</summary>
        /// <param name="gainValue">目标增益值。</param>
        public void SetGain(double gainValue)
        {
            EnsureDeviceOpen();
            TrySetEnumValue("GainAuto", 0);
            int result = IsBaslerManufacturer()
                ? _device.MV_CC_SetIntValueEx_NET("GainRaw", checked((long)gainValue))
                : _device.MV_CC_SetFloatValue_NET("Gain", checked((float)gainValue));
            EnsureSuccess(result, "设置相机增益");
        }

        /// <summary>设置手动曝光时间。</summary>
        /// <param name="time">曝光时间，单位为微秒。</param>
        public void SetExposureTime(double time)
        {
            EnsureDeviceOpen();
            TrySetEnumValue("ExposureAuto", 0);
            string key = IsBaslerManufacturer() ? "ExposureTimeAbs" : "ExposureTime";
            EnsureSuccess(_device.MV_CC_SetFloatValue_NET(key, checked((float)time)), "设置曝光时间");
        }

        /// <summary>设置触发延迟。</summary>
        /// <param name="time">触发延迟，单位为微秒。</param>
        public void SetTriggerDelay(double time)
        {
            EnsureDeviceOpen();
            string key = IsBaslerManufacturer() ? "TriggerDelayAbs" : "TriggerDelay";
            EnsureSuccess(_device.MV_CC_SetFloatValue_NET(key, checked((float)time)), "设置触发延迟");
        }

        /// <summary>关闭相机并释放当前转换缓冲区。</summary>
        public void Close()
        {
            bool notifyDisconnected = false;
            lock (_deviceLock)
            {
                if (_device == null)
                    return;

                if (_isGrabbing)
                {
                    Volatile.Write(ref _isGrabbing, false);
                    int stopResult = _device.MV_CC_StopGrabbing_NET();
                    if (stopResult != MyCamera.MV_OK)
                        LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("停止相机取流", stopResult), true);
                }

                lock (_callbackSyncRoot)
                    ReleaseConversionBuffer();

                if (IsOpen)
                {
                    int closeResult = _device.MV_CC_CloseDevice_NET();
                    if (closeResult != MyCamera.MV_OK)
                        LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("关闭相机", closeResult), true);
                    notifyDisconnected = true;
                }

                IsOpen = false;
                _callbackRegistered = false;
            }

            if (notifyDisconnected)
                ConnectStatusEvent?.Invoke(this, false);
        }

        /// <summary>关闭相机并销毁原生设备句柄。</summary>
        public void Dispose()
        {
            Close();
            lock (_deviceLock)
            {
                if (_device != null && _deviceCreated)
                {
                    int result = _device.MV_CC_DestroyDevice_NET();
                    if (result != MyCamera.MV_OK)
                        LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("销毁相机句柄", result), true);
                }

                _deviceCreated = false;
                _callbackRegistered = false;
                _nativeImageCallback = null;
                _device = null;
                _deviceInfo = null;
            }
        }

        /// <summary>SDK图像回调：转换到预分配内存并生成独立Mat后交给现有框架事件。</summary>
        /// <param name="sourceData">SDK帧数据地址。</param>
        /// <param name="frameInfo">SDK帧信息。</param>
        /// <param name="userData">用户数据，本实现未使用。</param>
        private void GetImageCallBack(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, IntPtr userData)
        {
            if (!Volatile.Read(ref _isGrabbing) || sourceData == IntPtr.Zero)
                return;
            lock (_imageCallbackLock)
            {
                if (_imageCallbackOwners.Count == 0 || OnMatReceived == null)
                    return;
            }

            long frameId = Interlocked.Increment(ref _callbackFrameSequence);
            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            long callbackStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
            int frameWidth = frameInfo.nWidth;
            int frameHeight = frameInfo.nHeight;
            MyCamera.MvGvspPixelType sourcePixelType = frameInfo.enPixelType;
            Mat image = null;
            CameraFrameTraceInfo frameTrace = null;
            PerformanceSpikeDiagnostics.LogIfEnabled(
                MsgLevel.Debug,
                () => string.Format("【完整耗时-相机帧】相机={0}；FrameId={1}；阶段=SDK回调开始；线程={2}", DevName, frameId, Thread.CurrentThread.ManagedThreadId),
                true);

            try
            {
                image = ConvertFrameToIndependentMat(sourceData, ref frameInfo, frameId, diagnosticEnabled);
                long conversionCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                if (diagnosticEnabled)
                {
                    frameTrace = new CameraFrameTraceInfo(
                        DevName,
                        frameId,
                        callbackStartedTimestamp,
                        conversionCompletedTimestamp,
                        frameWidth,
                        frameHeight,
                        sourcePixelType.ToString());
                }

                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format(
                        "【完整耗时-相机帧】相机={0}；FrameId={1}；阶段=Mat转换完成；回调到转换={2}ms；尺寸={3}x{4}；PixelType={5}",
                        DevName,
                        frameId,
                        frameTrace?.CallbackToConversionMilliseconds ?? 0,
                        frameWidth,
                        frameHeight,
                        sourcePixelType),
                    true);
                RaiseMatImageReceived(image, frameTrace);
                image = null;
            }
            catch (Exception exception)
            {
                image?.Dispose();
                LogHelper.AddLog(MsgLevel.Exception, string.Format("回调函数{0}转换Mat失败：FrameId={1}；{2}", DevName, frameId, exception.Message), true);
            }
            finally
            {
                long callbackCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                long callbackElapsedMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(callbackStartedTimestamp, callbackCompletedTimestamp);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format("【完整耗时-相机帧】相机={0}；FrameId={1}；阶段=SDK回调结束；回调总耗时={2}ms；线程={3}", DevName, frameId, callbackElapsedMilliseconds, Thread.CurrentThread.ManagedThreadId),
                    true);
                PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                    MsgLevel.Debug,
                    PerformanceSpikeDiagnostics.FrameConvertSlowMs,
                    () => string.Format("【慢诊断-相机回调】相机={0}；FrameId={1}；回调总耗时={2}ms；{3}", DevName, frameId, callbackElapsedMilliseconds, PerformanceSpikeDiagnostics.GetRuntimeText()),
                    true,
                    callbackElapsedMilliseconds);
            }
        }

        /// <summary>使用原生SDK转换帧并复制为拥有独立生命周期的Mat。</summary>
        /// <param name="sourceData">源帧地址。</param>
        /// <param name="frameInfo">源帧信息。</param>
        /// <param name="frameId">诊断帧号。</param>
        /// <param name="diagnosticEnabled">是否记录完整耗时。</param>
        /// <returns>调用方负责释放的独立Mat。</returns>
        private Mat ConvertFrameToIndependentMat(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, long frameId, bool diagnosticEnabled)
        {
            lock (_callbackSyncRoot)
            {
                if (_device == null)
                    throw new InvalidOperationException("相机对象为空。");

                int frameWidth = frameInfo.nWidth;
                int frameHeight = frameInfo.nHeight;
                MyCamera.MvGvspPixelType sourcePixelType = frameInfo.enPixelType;
                int channelCount;
                MyCamera.MvGvspPixelType destinationPixelType = ResolveDestinationPixelType(sourcePixelType, out channelCount);
                ulong requiredByteCount = checked((ulong)frameWidth * (uint)frameHeight * (uint)channelCount);
                if (requiredByteCount > int.MaxValue)
                    throw new InvalidOperationException("转换后的单帧图像超过可分配上限。");
                EnsureConversionBuffer((uint)requiredByteCount);

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

                long conversionStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                int result = _device.MV_CC_ConvertPixelType_NET(ref convertParameter);
                long conversionCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                EnsureSuccess(result, "转换相机像素格式");

                MatType matType = channelCount == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
                Mat independentMat;
                long cloneStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                using (Mat bufferView = Mat.FromPixelData(frameHeight, frameWidth, matType, _conversionBuffer))
                    independentMat = bufferView.Clone();
                long cloneCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;

                long convertMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(conversionStartedTimestamp, conversionCompletedTimestamp);
                long cloneMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(cloneStartedTimestamp, cloneCompletedTimestamp);
                long totalMilliseconds = convertMilliseconds + cloneMilliseconds;
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format(
                        "【完整耗时-相机帧】相机={0}；FrameId={1}；阶段=ConvertPixelType完成；耗时={2}ms；PixelType={3}->{4}",
                        DevName,
                        frameId,
                        convertMilliseconds,
                        sourcePixelType,
                        destinationPixelType),
                    true);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format(
                        "【完整耗时-相机帧】相机={0}；FrameId={1}；阶段=CopyToMat完成；耗时={2}ms；尺寸={3}x{4}",
                        DevName,
                        frameId,
                        cloneMilliseconds,
                        frameWidth,
                        frameHeight),
                    true);
                PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                    MsgLevel.Debug,
                    PerformanceSpikeDiagnostics.FrameConvertSlowMs,
                    () => string.Format(
                        "【慢诊断-相机Mat转换】相机={0}；FrameId={1}；ConvertPixelType={2}ms；CopyToMat={3}ms；总耗时={4}ms；PixelType={5}->{6}；尺寸={7}x{8}；{9}",
                        DevName,
                        frameId,
                        convertMilliseconds,
                        cloneMilliseconds,
                        totalMilliseconds,
                        sourcePixelType,
                        destinationPixelType,
                        frameWidth,
                        frameHeight,
                        PerformanceSpikeDiagnostics.GetRuntimeText()),
                    true,
                    convertMilliseconds,
                    cloneMilliseconds,
                    totalMilliseconds);
                return independentMat;
            }
        }

        /// <summary>将独立Mat分发给订阅者；多订阅者时为前面的订阅者克隆图像。</summary>
        /// <param name="image">待分发图像。</param>
        /// <param name="frameTrace">相机帧诊断元数据。</param>
        private void RaiseMatImageReceived(Mat image, CameraFrameTraceInfo frameTrace)
        {
            Action<Mat> handlers = OnMatReceived;
            if (handlers == null)
            {
                image?.Dispose();
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                Mat subscriberImage = index == invocationList.Length - 1 ? image : image?.Clone();
                try
                {
                    CameraFrameTraceRegistry.Attach(subscriberImage, frameTrace);
                    ((Action<Mat>)invocationList[index]).Invoke(subscriberImage);
                    subscriberImage = null;
                }
                catch (Exception exception)
                {
                    subscriberImage?.Dispose();
                    LogHelper.AddLog(MsgLevel.Exception, string.Format("相机{0} Mat回调处理异常：{1}", DevName, exception.Message), true);
                }
            }
        }

        /// <summary>注册官方原生图像回调，调用方必须持有回调锁。</summary>
        private void EnsureNativeCallbackRegisteredCore()
        {
            if (!IsOpen || _device == null || _callbackRegistered)
                return;

            _nativeImageCallback = GetImageCallBack;
            EnsureSuccess(_device.MV_CC_RegisterImageCallBackEx_NET(_nativeImageCallback, IntPtr.Zero), "注册相机图像回调");
            _callbackRegistered = true;
        }

        /// <summary>创建原生相机句柄并替换当前未打开的句柄。</summary>
        /// <param name="deviceInfo">原生设备信息。</param>
        private void CreateNativeDevice(HikNativeDeviceInfo deviceInfo)
        {
            InitSDK();
            lock (_deviceLock)
                CreateNativeDeviceCore(deviceInfo);
        }

        /// <summary>在设备锁内创建原生相机句柄。</summary>
        /// <param name="deviceInfo">原生设备信息。</param>
        private void CreateNativeDeviceCore(HikNativeDeviceInfo deviceInfo)
        {
            if (IsOpen || _isGrabbing)
                throw new InvalidOperationException("相机打开时不能重新创建设备句柄。");
            if (_device != null && _deviceCreated)
                _device.MV_CC_DestroyDevice_NET();

            _device = new MyCamera();
            MyCamera.MV_CC_DEVICE_INFO nativeInfo = deviceInfo.NativeDeviceInfo;
            EnsureSuccess(_device.MV_CC_CreateDevice_NET(ref nativeInfo), "创建相机句柄");
            _deviceCreated = true;
            _callbackRegistered = false;
            _deviceInfo = deviceInfo;
        }

        /// <summary>把枚举信息同步到可序列化的相机属性。</summary>
        /// <param name="deviceInfo">原生设备信息。</param>
        private void SetDeviceIdentity(HikNativeDeviceInfo deviceInfo)
        {
            _deviceInfo = deviceInfo;
            ManufacturerName = deviceInfo.ManufacturerName;
            SN = deviceInfo.SerialNumber;
            IP = string.IsNullOrWhiteSpace(deviceInfo.IpAddress) ? "无" : deviceInfo.IpAddress;
            DevName = GetDevNameByDevInfo(deviceInfo);
            Brand = ResolveDeviceBrand(deviceInfo.ManufacturerName);
        }

        /// <summary>为GigE相机设置SDK探测出的最佳网络包大小。</summary>
        private void ConfigureGigENetwork()
        {
            if (_deviceInfo == null || _deviceInfo.TransportLayerType != MyCamera.MV_GIGE_DEVICE)
                return;

            int packetSize = _device.MV_CC_GetOptimalPacketSize_NET();
            if (packetSize <= 0)
            {
                LogHelper.AddLog(MsgLevel.Warn, string.Format("相机{0}获取最佳网络包大小失败：{1}", UserDefinedName, packetSize), true);
                return;
            }

            int result = _device.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", packetSize);
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("设置最佳网络包大小", result), true);
            else
                LogHelper.AddLog(MsgLevel.Info, string.Format("相机{0}设置网络最佳包大小：{1}", UserDefinedName, packetSize), true);
        }

        /// <summary>读取原生浮点参数并转换为框架参数模型。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <returns>框架浮点参数。</returns>
        private CameraFloatValue GetFloatValue(string key)
        {
            EnsureDeviceOpen();
            MyCamera.MVCC_FLOATVALUE nativeValue = new MyCamera.MVCC_FLOATVALUE();
            EnsureSuccess(_device.MV_CC_GetFloatValue_NET(key, ref nativeValue), "读取相机参数" + key);
            return new CameraFloatValue { CurValue = nativeValue.fCurValue, Min = nativeValue.fMin, Max = nativeValue.fMax };
        }

        /// <summary>读取原生整数参数并转换为框架参数模型。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <returns>框架整数参数。</returns>
        private CameraIntValue GetIntValue(string key)
        {
            EnsureDeviceOpen();
            MyCamera.MVCC_INTVALUE_EX nativeValue = new MyCamera.MVCC_INTVALUE_EX();
            EnsureSuccess(_device.MV_CC_GetIntValueEx_NET(key, ref nativeValue), "读取相机参数" + key);
            return new CameraIntValue
            {
                CurValue = nativeValue.nCurValue,
                Min = nativeValue.nMin,
                Max = nativeValue.nMax,
                Increment = nativeValue.nInc
            };
        }

        /// <summary>读取原生枚举参数和每个支持项的符号名称。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <returns>框架枚举参数。</returns>
        private CameraEnumValue GetEnumValue(string key)
        {
            EnsureDeviceOpen();
            MyCamera.MV_XML_AccessMode accessMode;
            bool hasAccessMode = TryGetNodeAccessMode(key, out accessMode);
            if (hasAccessMode && !IsNodeReadable(accessMode))
                throw new InvalidOperationException(string.Format("相机参数 {0} 在当前状态下不可读。", key));

            MyCamera.MVCC_ENUMVALUE nativeValue = new MyCamera.MVCC_ENUMVALUE();
            EnsureSuccess(_device.MV_CC_GetEnumValue_NET(key, ref nativeValue), "读取相机参数" + key);

            uint[] supportedValues = nativeValue.nSupportValue ?? new uint[0];
            int count = (int)Math.Min(nativeValue.nSupportedNum, (uint)supportedValues.Length);
            List<CameraEnumEntry> entries = new List<CameraEnumEntry>(count);
            for (int index = 0; index < count; index++)
            {
                uint value = supportedValues[index];
                entries.Add(new CameraEnumEntry { Value = value, Symbolic = GetEnumSymbolic(key, value) });
            }

            CameraEnumEntry currentEntry = entries.FirstOrDefault(item => item.Value == nativeValue.nCurValue)
                ?? new CameraEnumEntry { Value = nativeValue.nCurValue, Symbolic = GetEnumSymbolic(key, nativeValue.nCurValue) };
            return new CameraEnumValue
            {
                IsReadable = true,
                IsWritable = !hasAccessMode || IsNodeWritable(accessMode),
                CurEnumEntry = currentEntry,
                SupportEnumEntries = entries.ToArray(),
                SupportedNum = (uint)entries.Count
            };
        }

        /// <summary>尝试读取节点在相机当前状态下的访问权限。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <param name="accessMode">读取到的访问权限。</param>
        /// <returns>SDK成功返回访问权限时为true。</returns>
        private bool TryGetNodeAccessMode(string key, out MyCamera.MV_XML_AccessMode accessMode)
        {
            accessMode = MyCamera.MV_XML_AccessMode.AM_Undefined;
            return _device.MV_XML_GetNodeAccessMode_NET(key, ref accessMode) == MyCamera.MV_OK;
        }

        /// <summary>确保节点在相机当前状态下可以写入。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <param name="operation">错误信息中的操作名称。</param>
        private void EnsureNodeWritable(string key, string operation)
        {
            MyCamera.MV_XML_AccessMode accessMode;
            if (TryGetNodeAccessMode(key, out accessMode) && !IsNodeWritable(accessMode))
                throw new InvalidOperationException(string.Format("{0}失败：相机参数 {1} 在当前状态下不可写。", operation, key));
        }

        /// <summary>判断节点访问权限是否允许读取。</summary>
        /// <param name="accessMode">SDK节点访问权限。</param>
        /// <returns>只读或可读写时为true。</returns>
        private static bool IsNodeReadable(MyCamera.MV_XML_AccessMode accessMode)
        {
            return accessMode == MyCamera.MV_XML_AccessMode.AM_RO || accessMode == MyCamera.MV_XML_AccessMode.AM_RW;
        }

        /// <summary>判断节点访问权限是否允许写入。</summary>
        /// <param name="accessMode">SDK节点访问权限。</param>
        /// <returns>只写或可读写时为true。</returns>
        private static bool IsNodeWritable(MyCamera.MV_XML_AccessMode accessMode)
        {
            return accessMode == MyCamera.MV_XML_AccessMode.AM_WO || accessMode == MyCamera.MV_XML_AccessMode.AM_RW;
        }

        /// <summary>创建表示节点在当前相机状态下不可用的枚举结果。</summary>
        /// <returns>不可读且不可写的空枚举结果。</returns>
        private static CameraEnumValue CreateUnavailableEnumValue()
        {
            return new CameraEnumValue
            {
                IsReadable = false,
                IsWritable = false
            };
        }

        /// <summary>读取指定枚举值的SDK符号名称。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <param name="value">枚举值。</param>
        /// <returns>符号名称。</returns>
        private string GetEnumSymbolic(string key, uint value)
        {
            MyCamera.MVCC_ENUMENTRY entry = new MyCamera.MVCC_ENUMENTRY
            {
                nValue = value,
                chSymbolic = new byte[MyCamera.MV_MAX_SYMBOLIC_LEN],
                nReserved = new uint[4]
            };
            int result = _device.MV_CC_GetEnumEntrySymbolic_NET(key, ref entry);
            return result == MyCamera.MV_OK ? CleanByteString(entry.chSymbolic) : GetFallbackEnumSymbolic(key, value);
        }

        /// <summary>在不影响主操作的情况下尝试设置枚举参数。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <param name="value">目标枚举值。</param>
        private void TrySetEnumValue(string key, uint value)
        {
            int result = _device.MV_CC_SetEnumValue_NET(key, value);
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("设置相机参数" + key, result), true);
        }

        /// <summary>确保当前相机已经打开。</summary>
        private void EnsureDeviceOpen()
        {
            if (_device == null || !_deviceCreated)
                throw new InvalidOperationException("相机对象为空。");
            if (!IsOpen)
                throw new InvalidOperationException(string.Format("相机（{0}）尚未打开。", UserDefinedName));
        }

        /// <summary>确保转换缓冲区容量满足当前帧要求。</summary>
        /// <param name="requiredBytes">所需字节数。</param>
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

        /// <summary>释放预分配的非托管转换缓冲区。</summary>
        private void ReleaseConversionBuffer()
        {
            if (_conversionBuffer == IntPtr.Zero)
                return;
            Marshal.FreeHGlobal(_conversionBuffer);
            _conversionBuffer = IntPtr.Zero;
            _conversionBufferSize = 0;
        }

        /// <summary>根据源格式选择Mono8或BGR8目标格式。</summary>
        /// <param name="sourcePixelType">源像素格式。</param>
        /// <param name="channelCount">输出通道数。</param>
        /// <returns>目标像素格式。</returns>
        private static MyCamera.MvGvspPixelType ResolveDestinationPixelType(MyCamera.MvGvspPixelType sourcePixelType, out int channelCount)
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
            throw new NotSupportedException("不支持的相机像素格式：" + sourcePixelType);
        }

        /// <summary>判断像素格式是否为支持的单色格式。</summary>
        /// <param name="pixelType">像素格式。</param>
        /// <returns>支持转为Mono8时返回true。</returns>
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

        /// <summary>判断像素格式是否为支持的彩色格式。</summary>
        /// <param name="pixelType">像素格式。</param>
        /// <returns>支持转为BGR8时返回true。</returns>
        private static bool IsColorPixelFormat(MyCamera.MvGvspPixelType pixelType)
        {
            switch (pixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGBA8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BGRA8_Packed:
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

        /// <summary>把SDK设备联合体解析为框架设备信息。</summary>
        /// <param name="index">枚举序号。</param>
        /// <param name="nativeInfo">SDK设备结构。</param>
        /// <returns>框架设备信息。</returns>
        private static HikNativeDeviceInfo ParseDeviceInfo(int index, MyCamera.MV_CC_DEVICE_INFO nativeInfo)
        {
            HikNativeDeviceInfo deviceInfo = new HikNativeDeviceInfo
            {
                Index = index,
                TransportLayerType = nativeInfo.nTLayerType,
                NativeDeviceInfo = nativeInfo,
                ManufacturerName = "未知厂商",
                ModelName = "未知型号",
                SerialNumber = string.Empty,
                UserDefinedName = string.Empty,
                IpAddress = string.Empty
            };

            if (nativeInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO_EX gigEInfo =
                    (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(nativeInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
                deviceInfo.ManufacturerName = CleanText(gigEInfo.chManufacturerName);
                deviceInfo.ModelName = CleanText(gigEInfo.chModelName);
                deviceInfo.SerialNumber = CleanText(gigEInfo.chSerialNumber);
                deviceInfo.UserDefinedName = CleanByteString(gigEInfo.chUserDefinedName);
                deviceInfo.IpAddress = string.Format(
                    "{0}.{1}.{2}.{3}",
                    (gigEInfo.nCurrentIp & 0xff000000) >> 24,
                    (gigEInfo.nCurrentIp & 0x00ff0000) >> 16,
                    (gigEInfo.nCurrentIp & 0x0000ff00) >> 8,
                    gigEInfo.nCurrentIp & 0x000000ff);
            }
            else if (nativeInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO_EX usbInfo =
                    (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(nativeInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
                deviceInfo.ManufacturerName = CleanText(usbInfo.chManufacturerName);
                deviceInfo.ModelName = CleanText(usbInfo.chModelName);
                deviceInfo.SerialNumber = CleanText(usbInfo.chSerialNumber);
                deviceInfo.UserDefinedName = CleanByteString(usbInfo.chUserDefinedName);
            }
            return deviceInfo;
        }

        /// <summary>根据制造商名称映射框架设备品牌。</summary>
        /// <param name="manufacturerName">制造商名称。</param>
        /// <returns>框架设备品牌。</returns>
        private static DeviceBrand ResolveDeviceBrand(string manufacturerName)
        {
            string value = manufacturerName ?? string.Empty;
            if (value.IndexOf("Basler", StringComparison.OrdinalIgnoreCase) >= 0)
                return DeviceBrand.Basler;
            if (value.IndexOf("Huaray", StringComparison.OrdinalIgnoreCase) >= 0)
                return DeviceBrand.HuarayTechnology;
            if (value.IndexOf("Hik", StringComparison.OrdinalIgnoreCase) >= 0 || value.Equals("GEV", StringComparison.OrdinalIgnoreCase))
                return DeviceBrand.HikVision;
            return DeviceBrand.Unknow;
        }

        /// <summary>清理SDK固定长度字符串。</summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>清理后的字符串。</returns>
        private static string CleanText(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.TrimEnd('\0', ' ');
        }

        /// <summary>将SDK固定长度字节数组转换为本机编码字符串。</summary>
        /// <param name="bytes">原始字节数组。</param>
        /// <returns>清理后的字符串。</returns>
        private static string CleanByteString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
                length = bytes.Length;
            return Encoding.Default.GetString(bytes, 0, length).Trim();
        }

        /// <summary>获取旧型号相机无法返回符号名时的枚举兜底名称。</summary>
        /// <param name="key">参数键。</param>
        /// <param name="value">枚举值。</param>
        /// <returns>兜底符号名称。</returns>
        private static string GetFallbackEnumSymbolic(string key, uint value)
        {
            if (key == "TriggerSource")
            {
                if (value == 7)
                    return "Software";
                if (value <= 3)
                    return "Line" + value;
                if (value == 4)
                    return "Counter0";
                if (value == 8)
                    return "FrequencyConverter";
                return string.Empty;
            }
            if (key == "LineSelector")
                return value <= 4 ? "Line" + value : string.Empty;
            if (key == "LineMode")
                return value == 0 ? "Input" : value == 1 || value == 8 ? "Output" : string.Empty;
            if (key == "TriggerActivation")
            {
                switch (value)
                {
                    case 0:
                        return "RisingEdge";
                    case 1:
                        return "FallingEdge";
                    case 2:
                        return "LevelHigh";
                    case 3:
                        return "LevelLow";
                    case 4:
                        return "AnyEdge";
                }
            }
            return string.Empty;
        }

        /// <summary>将SDK触发源枚举项转换为框架触发源。</summary>
        /// <param name="entry">SDK当前触发源枚举项。</param>
        /// <returns>框架支持的触发源；不支持的SDK触发源返回Auto。</returns>
        private static TriggerSource MapTriggerSource(CameraEnumEntry entry)
        {
            if (entry == null)
                return TriggerSource.Auto;

            switch ((entry.Symbolic ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "SOFTWARE":
                    return TriggerSource.SOFT;
                case "LINE0":
                    return TriggerSource.LINE0;
                case "LINE1":
                    return TriggerSource.LINE1;
                case "LINE2":
                    return TriggerSource.LINE2;
                case "LINE3":
                    return TriggerSource.LINE3;
                case "LINE4":
                    return TriggerSource.LINE4;
            }

            if (entry.Value == (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE)
                return TriggerSource.SOFT;
            if (entry.Value <= (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE3)
                return (TriggerSource)((int)TriggerSource.LINE0 + (int)entry.Value);
            return TriggerSource.Auto;
        }

        /// <summary>获取框架触发源对应的GenICam符号名称。</summary>
        /// <param name="triggerSource">框架触发源。</param>
        /// <returns>GenICam符号名称。</returns>
        private static string GetTriggerSourceSymbolic(TriggerSource triggerSource)
        {
            switch (triggerSource)
            {
                case TriggerSource.SOFT:
                    return "Software";
                case TriggerSource.LINE0:
                    return "Line0";
                case TriggerSource.LINE1:
                    return "Line1";
                case TriggerSource.LINE2:
                    return "Line2";
                case TriggerSource.LINE3:
                    return "Line3";
                case TriggerSource.LINE4:
                    return "Line4";
                default:
                    throw new ArgumentOutOfRangeException(nameof(triggerSource), triggerSource, "不支持的触发源。");
            }
        }

        /// <summary>获取海康官方SDK明确定义的触发源数值兜底。</summary>
        /// <param name="triggerSource">框架触发源。</param>
        /// <param name="value">官方SDK枚举值。</param>
        /// <returns>存在无歧义的官方数值时为true。</returns>
        private static bool TryGetOfficialTriggerSourceValue(TriggerSource triggerSource, out uint value)
        {
            switch (triggerSource)
            {
                case TriggerSource.SOFT:
                    value = (uint)MyCamera.MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE;
                    return true;
                case TriggerSource.LINE0:
                case TriggerSource.LINE1:
                case TriggerSource.LINE2:
                case TriggerSource.LINE3:
                    value = checked((uint)((int)triggerSource - (int)TriggerSource.LINE0));
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        /// <summary>判断框架触发源是否为线路硬触发。</summary>
        /// <param name="triggerSource">框架触发源。</param>
        /// <returns>Line0至Line4时为true。</returns>
        private static bool IsLineTriggerSource(TriggerSource triggerSource)
        {
            return triggerSource >= TriggerSource.LINE0 && triggerSource <= TriggerSource.LINE4;
        }

        /// <summary>判断当前相机是否为巴斯勒制造商。</summary>
        /// <returns>巴斯勒相机返回true。</returns>
        private bool IsBaslerManufacturer()
        {
            return (ManufacturerName ?? string.Empty).IndexOf("Basler", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>判断当前相机是否为华睿制造商。</summary>
        /// <returns>华睿相机返回true。</returns>
        private bool IsHuarayManufacturer()
        {
            return (ManufacturerName ?? string.Empty).IndexOf("Huaray", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>检查SDK返回码，失败时抛出包含操作名称的异常。</summary>
        /// <param name="result">SDK返回码。</param>
        /// <param name="operation">操作名称。</param>
        private static void EnsureSuccess(int result, string operation)
        {
            if (result != MyCamera.MV_OK)
                throw CreateSdkException(operation, result);
        }

        /// <summary>创建包含SDK十六进制错误码的异常。</summary>
        /// <param name="operation">操作名称。</param>
        /// <param name="result">SDK返回码。</param>
        /// <returns>标准相机异常。</returns>
        private static InvalidOperationException CreateSdkException(string operation, int result)
        {
            return new InvalidOperationException(FormatSdkError(operation, result));
        }

        /// <summary>格式化SDK错误信息。</summary>
        /// <param name="operation">操作名称。</param>
        /// <param name="result">SDK返回码。</param>
        /// <returns>错误描述。</returns>
        private static string FormatSdkError(string operation, int result)
        {
            return string.Format("{0}失败，错误码：0x{1:X8}", operation, result);
        }
    }
}
