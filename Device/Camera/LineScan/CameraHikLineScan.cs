using Logger;
using MvCamCtrl.NET;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OpenCvSharp;

namespace TDJS_Vision.Device.Camera.LineScan
{
    /// <summary>
    /// 海康线扫相机独立实现；直接依赖海康MVS SDK，不继承也不调用现有面阵相机类。
    /// </summary>
    public sealed class CameraHikLineScan : ILineScanCamera
    {
        /// <summary>串行化设备生命周期操作。</summary>
        private readonly object _lifecycleLock = new object();

        /// <summary>海康SDK相机对象。</summary>
        private MyCamera _device;

        /// <summary>当前对象是否已经永久释放。</summary>
        private bool _disposed;

        /// <summary>当前相机是否正在取流。</summary>
        private bool _isGrabbing;

        /// <summary>保持MVS原生回调委托存活，防止被垃圾回收。</summary>
        private MyCamera.cbOutputExdelegate _nativeImageCallback;

        /// <summary>原生图像回调是否已注册。</summary>
        private bool _callbackRegistered;

        /// <summary>相机PixelFormat节点报告的支持格式。</summary>
        private readonly List<string> _supportedPixelFormats = new List<string>();

        /// <summary>连接状态变化事件。</summary>
        public event EventHandler<bool> ConnectStatusEvent;

        /// <summary>相机输出一幅完整线扫帧时触发。</summary>
        public event Action<Mat> FrameReceived;

        /// <summary>获取或设置硬件设备名称。</summary>
        public string DevName { get; set; }

        /// <summary>获取或设置用户自定义名称。</summary>
        public string UserDefinedName { get; set; }

        /// <summary>获取或设置设备类型。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.CAMERA;

        /// <summary>获取或设置设备品牌。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceBrand Brand { get; set; } = DeviceBrand.HikVision;

        /// <summary>获取或设置用于方案反序列化的完整类名。</summary>
        public string ClassName { get; set; } = typeof(CameraHikLineScan).FullName;

        /// <summary>获取相机是否已经打开。</summary>
        [JsonIgnore]
        public bool IsOpen { get; private set; }

        /// <summary>获取相机是否正在取流。</summary>
        [JsonIgnore]
        public bool IsGrabbing => _isGrabbing;

        /// <summary>获取或设置设备序列号。</summary>
        public string SerialNumber { get; set; }

        /// <summary>获取或设置设备IP地址。</summary>
        public string IpAddress { get; set; }

        /// <summary>获取相机当前像素格式的GenICam符号名称。</summary>
        [JsonIgnore]
        public string CurrentPixelFormat { get; private set; } = string.Empty;

        /// <summary>获取相机PixelFormat节点实际支持的全部格式快照。</summary>
        [JsonIgnore]
        public IReadOnlyList<string> SupportedPixelFormats
        {
            get
            {
                lock (_lifecycleLock)
                    return _supportedPixelFormats.ToArray();
            }
        }

        /// <summary>获取或设置方案加载后是否自动打开相机。</summary>
        public bool AutoConnectOnLoad { get; set; } = true;

        /// <summary>初始化用于方案反序列化的空线扫相机对象。</summary>
        [JsonConstructor]
        public CameraHikLineScan()
        {
        }

        /// <summary>绑定设备发现服务返回的海康原生设备信息。</summary>
        /// <param name="deviceInfo">海康原生设备信息。</param>
        /// <param name="userDefinedName">框架内用户名称。</param>
        public CameraHikLineScan(HikNativeDeviceInfo deviceInfo, string userDefinedName)
        {
            if (deviceInfo == null)
                throw new ArgumentNullException(nameof(deviceInfo));
            UserDefinedName = userDefinedName;
            SerialNumber = deviceInfo.SerialNumber;
            IpAddress = deviceInfo.IpAddress;
            DevName = string.IsNullOrWhiteSpace(deviceInfo.UserDefinedName)
                ? string.Format("{0}{1}({2})", deviceInfo.ManufacturerName, deviceInfo.ModelName, deviceInfo.SerialNumber)
                : string.Format("{0}({1})", deviceInfo.UserDefinedName, deviceInfo.SerialNumber);
            CreateNativeDevice(deviceInfo);
        }

        /// <summary>
        /// 方案反序列化后的设备恢复入口；设备发现服务接入前明确拒绝隐式复用面阵枚举逻辑。
        /// </summary>
        public void CreateDevice()
        {
            ThrowIfDisposed();
            lock (_lifecycleLock)
            {
                if (_device != null)
                    return;
                HikNativeDeviceInfo deviceInfo = HikLineScanDeviceDiscovery.FindDevices()
                    .FirstOrDefault(item => string.Equals(item.SerialNumber, SerialNumber, StringComparison.OrdinalIgnoreCase));
                if (deviceInfo == null)
                    throw new InvalidOperationException(string.Format("未找到序列号为{0}的海康线扫相机。", SerialNumber));
                CreateNativeDevice(deviceInfo);
            }
        }

        /// <summary>打开线扫相机。</summary>
        /// <returns>打开成功时返回true。</returns>
        public bool Open()
        {
            ThrowIfDisposed();
            lock (_lifecycleLock)
            {
                if (IsOpen)
                    return true;
                if (_device == null)
                {
                    // 原生SDK句柄不会进入方案文件，软件重启后必须按序列号重新枚举并绑定。
                    HikNativeDeviceInfo deviceInfo = HikLineScanDeviceDiscovery.FindDevices()
                        .FirstOrDefault(item => string.Equals(item.SerialNumber, SerialNumber, StringComparison.OrdinalIgnoreCase));
                    if (deviceInfo == null)
                        throw new InvalidOperationException(string.Format("线扫相机（{0}）不在线，序列号：{1}。", UserDefinedName, SerialNumber));
                    CreateNativeDevice(deviceInfo);
                }
                EnsureSuccess(_device.MV_CC_OpenDevice_NET(), "打开海康线扫相机");
                IsOpen = true;
                RefreshPixelFormatCapabilities();
                RegisterNativeImageCallback();
                ConnectStatusEvent?.Invoke(this, true);
                return true;
            }
        }

        /// <summary>开始线扫取流。</summary>
        public void StartGrabbing()
        {
            EnsureOpen();
            lock (_lifecycleLock)
            {
                if (_isGrabbing)
                    return;
                EnsureSuccess(_device.MV_CC_StartGrabbing_NET(), "启动海康线扫相机取流");
                _isGrabbing = true;
            }
        }

        /// <summary>停止线扫取流。</summary>
        public void StopGrabbing()
        {
            lock (_lifecycleLock)
            {
                if (!_isGrabbing || _device == null)
                    return;
                EnsureSuccess(_device.MV_CC_StopGrabbing_NET(), "停止海康线扫相机取流");
                _isGrabbing = false;
            }
        }

        /// <summary>执行一次软件触发命令。</summary>
        public void ExecuteSoftwareTrigger()
        {
            EnsureOpen();
            lock (_lifecycleLock)
            {
                if (!_isGrabbing)
                    throw new InvalidOperationException("执行线扫软件触发前必须先启动取流。");
                EnsureSuccess(_device.MV_CC_SetCommandValue_NET("TriggerSoftware"), "执行线扫相机软件触发");
            }
        }

        /// <summary>等待相机输出一帧已经拼接完成的Mono8线扫图像。</summary>
        /// <param name="timeoutMilliseconds">等待超时时间，单位为毫秒。</param>
        /// <returns>调用方负责释放的独立Mat。</returns>
        public Mat GetOneFrame(int timeoutMilliseconds)
        {
            EnsureOpen();
            if (!_isGrabbing)
                StartGrabbing();
            MyCamera.MV_FRAME_OUT frame = new MyCamera.MV_FRAME_OUT();
            int result = _device.MV_CC_GetImageBuffer_NET(ref frame, Math.Max(1, timeoutMilliseconds));
            EnsureSuccess(result, "获取海康线扫完整帧");
            try
            {
                MyCamera.MV_FRAME_OUT_INFO_EX info = frame.stFrameInfo;
                if (frame.pBufAddr == IntPtr.Zero || info.nFrameLen == 0)
                    throw new InvalidOperationException("海康线扫相机返回的完整帧数据无效。");
                return ConvertFrameToMat(frame.pBufAddr, ref info);
            }
            finally
            {
                _device.MV_CC_FreeImageBuffer_NET(ref frame);
            }
        }

        /// <summary>直接写入流程节点配置的线扫采集参数；调用前必须停止取流。</summary>
        /// <param name="settings">待应用参数。</param>
        public void ApplyAcquisitionSettings(LineScanCameraSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            EnsureOpen();
            if (_isGrabbing)
                throw new InvalidOperationException("修改线扫图像尺寸和行频前必须停止取流。");
            if (settings.Width > 0)
                SetInteger("Width", settings.Width);
            SetInteger("Height", settings.Height);
            SetFloat("ExposureTime", settings.ExposureTime);
            SetFloat("Gain", settings.Gain);
            ApplyTriggerSettings(settings);
        }

        /// <summary>关闭相机并释放当前连接。</summary>
        public void Close()
        {
            lock (_lifecycleLock)
            {
                if (_isGrabbing)
                {
                    EnsureSuccess(_device.MV_CC_StopGrabbing_NET(), "停止海康线扫相机取流");
                    _isGrabbing = false;
                }
                if (!IsOpen || _device == null)
                    return;
                EnsureSuccess(_device.MV_CC_CloseDevice_NET(), "关闭海康线扫相机");
                IsOpen = false;
                _callbackRegistered = false;
                ConnectStatusEvent?.Invoke(this, false);
            }
        }

        /// <summary>释放相机句柄。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            Close();
            if (_device != null)
                EnsureSuccess(_device.MV_CC_DestroyDevice_NET(), "销毁海康线扫相机句柄");
            _device = null;
            _nativeImageCallback = null;
            _callbackRegistered = false;
            _disposed = true;
        }

        /// <summary>根据原生设备信息创建独立SDK句柄。</summary>
        /// <param name="deviceInfo">原生设备信息。</param>
        private void CreateNativeDevice(HikNativeDeviceInfo deviceInfo)
        {
            MyCamera camera = new MyCamera();
            MyCamera.MV_CC_DEVICE_INFO nativeInfo = deviceInfo.NativeDeviceInfo;
            EnsureSuccess(camera.MV_CC_CreateDevice_NET(ref nativeInfo), "创建海康线扫相机句柄");
            _device = camera;
            _callbackRegistered = false;
        }

        /// <summary>向MVS注册原生完整帧回调。</summary>
        private void RegisterNativeImageCallback()
        {
            if (_callbackRegistered)
                return;
            _nativeImageCallback = OnNativeImageReceived;
            EnsureSuccess(_device.MV_CC_RegisterImageCallBackEx_NET(_nativeImageCallback, IntPtr.Zero), "注册线扫相机图像回调");
            _callbackRegistered = true;
        }

        /// <summary>MVS原生回调入口，将相机完整帧转换为独立Mat后分发。</summary>
        private void OnNativeImageReceived(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, IntPtr userData)
        {
            if (!_isGrabbing || sourceData == IntPtr.Zero || frameInfo.nFrameLen == 0)
                return;
            Mat image = null;
            try
            {
                image = ConvertFrameToMat(sourceData, ref frameInfo);
                RaiseFrameReceived(image);
                image = null;
            }
            catch (Exception exception)
            {
                image?.Dispose();
                Logger.LogHelper.AddLog(Logger.MsgLevel.Exception,
                    string.Format("线扫相机【{0}】图像回调转换失败：{1}", UserDefinedName, exception.Message), true);
            }
        }

        /// <summary>把独立Mat分发给当前订阅者，多订阅者时为前面的订阅者克隆图像。</summary>
        private void RaiseFrameReceived(Mat image)
        {
            Action<Mat> handlers = FrameReceived;
            if (handlers == null)
            {
                image.Dispose();
                return;
            }
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                Mat subscriberImage = index == invocationList.Length - 1 ? image : image.Clone();
                try
                {
                    ((Action<Mat>)invocationList[index]).Invoke(subscriberImage);
                    subscriberImage = null;
                }
                catch (Exception exception)
                {
                    subscriberImage?.Dispose();
                    Logger.LogHelper.AddLog(Logger.MsgLevel.Exception,
                        string.Format("线扫相机【{0}】帧订阅处理失败：{1}", UserDefinedName, exception.Message), true);
                }
            }
        }

        /// <summary>读取整数节点当前值。</summary>
        /// <param name="key">GenICam节点名。</param>
        /// <returns>节点当前值。</returns>
        private long GetInteger(string key)
        {
            MyCamera.MVCC_INTVALUE_EX value = new MyCamera.MVCC_INTVALUE_EX();
            EnsureSuccess(_device.MV_CC_GetIntValueEx_NET(key, ref value), "读取线扫参数" + key);
            return value.nCurValue;
        }

        /// <summary>写入整数节点。</summary>
        private void SetInteger(string key, long value) => EnsureSuccess(_device.MV_CC_SetIntValueEx_NET(key, value), "设置线扫参数" + key);

        /// <summary>读取浮点节点当前值。</summary>
        private double GetFloat(string key)
        {
            MyCamera.MVCC_FLOATVALUE value = new MyCamera.MVCC_FLOATVALUE();
            EnsureSuccess(_device.MV_CC_GetFloatValue_NET(key, ref value), "读取线扫参数" + key);
            return value.fCurValue;
        }

        /// <summary>写入浮点节点。</summary>
        private void SetFloat(string key, double value) => EnsureSuccess(_device.MV_CC_SetFloatValue_NET(key, checked((float)value)), "设置线扫参数" + key);

        /// <summary>将框架触发配置写入海康线扫相机的Line Start触发节点。</summary>
        private void ApplyTriggerSettings(LineScanCameraSettings settings)
        {
            // 早期开发版本曾保存FrameStart；海康线扫相机实际节点符号为FrameBurstStart。
            string selectorSymbol = settings.TriggerSelector == LineScanTriggerSelector.FrameStart
                ? LineScanTriggerSelector.FrameBurstStart.ToString()
                : settings.TriggerSelector.ToString();
            SetEnumBySymbol("TriggerSelector", selectorSymbol);
            SetEnumBySymbol("TriggerMode", settings.TriggerMode == TDJS_Vision.Device.Camera.TriggerModel.On ? "On" : "Off");
            if (settings.TriggerMode != TDJS_Vision.Device.Camera.TriggerModel.On)
                return;
            string source = settings.TriggerSource == TDJS_Vision.Device.Camera.TriggerSource.SOFT
                ? "Software"
                : settings.TriggerSource.ToString().Replace("LINE", "Line");
            SetEnumBySymbol("TriggerSource", source);
            // 软件触发没有电气边沿，继续写TriggerActivation会在部分线扫型号上返回MV_E_SUPPORT。
            if (settings.TriggerSource == TDJS_Vision.Device.Camera.TriggerSource.SOFT)
                return;
            string activation;
            switch (settings.TriggerEdge)
            {
                case TDJS_Vision.Device.Camera.TriggerEdge.Falling: activation = "FallingEdge"; break;
                case TDJS_Vision.Device.Camera.TriggerEdge.Any: activation = "AnyEdge"; break;
                case TDJS_Vision.Device.Camera.TriggerEdge.Hight: activation = "LevelHigh"; break;
                case TDJS_Vision.Device.Camera.TriggerEdge.Low: activation = "LevelLow"; break;
                default: activation = "RisingEdge"; break;
            }
            SetEnumBySymbol("TriggerActivation", activation);
        }

        /// <summary>按GenICam符号名称写入枚举节点。</summary>
        private void SetEnumBySymbol(string key, string symbolic) =>
            EnsureSuccess(_device.MV_CC_SetEnumValueByString_NET(key, symbolic), "设置线扫参数" + key);

        /// <summary>读取相机实际支持的触发选择器。</summary>
        public IReadOnlyList<LineScanTriggerSelector> GetSupportedTriggerSelectors()
        {
            EnsureOpen();
            lock (_lifecycleLock)
            {
                if (_isGrabbing)
                    throw new InvalidOperationException("读取触发选择器前必须停止线扫取流。");
                List<LineScanTriggerSelector> result = new List<LineScanTriggerSelector>();
                foreach (string symbolic in GetSupportedEnumSymbols("TriggerSelector"))
                {
                    LineScanTriggerSelector selector;
                    if (Enum.TryParse(symbolic, true, out selector) && !result.Contains(selector))
                        result.Add(selector);
                }
                return result;
            }
        }

        /// <summary>读取指定触发选择器下相机实际支持的触发源。</summary>
        public IReadOnlyList<TDJS_Vision.Device.Camera.TriggerSource> GetSupportedTriggerSources(LineScanTriggerSelector selector)
        {
            EnsureOpen();
            lock (_lifecycleLock)
            {
                if (_isGrabbing)
                    throw new InvalidOperationException("读取触发能力前必须停止线扫取流。");
                string originalSelector = GetCurrentEnumSymbol("TriggerSelector");
                string originalMode = string.Empty;
                try
                {
                    SetEnumBySymbol("TriggerSelector", selector.ToString());
                    originalMode = GetCurrentEnumSymbol("TriggerMode");
                    SetEnumBySymbol("TriggerMode", "On");
                    List<TDJS_Vision.Device.Camera.TriggerSource> result = new List<TDJS_Vision.Device.Camera.TriggerSource>();
                    foreach (string symbolic in GetSupportedEnumSymbols("TriggerSource"))
                    {
                        TDJS_Vision.Device.Camera.TriggerSource source;
                        if (TryParseTriggerSource(symbolic, out source) && !result.Contains(source))
                            result.Add(source);
                    }
                    return result;
                }
                finally
                {
                    RestoreEnumBySymbol("TriggerMode", originalMode);
                    RestoreEnumBySymbol("TriggerSelector", originalSelector);
                }
            }
        }

        /// <summary>读取指定线路触发源下相机实际支持的触发极性。</summary>
        public IReadOnlyList<TDJS_Vision.Device.Camera.TriggerEdge> GetSupportedTriggerEdges(LineScanTriggerSelector selector, TDJS_Vision.Device.Camera.TriggerSource source)
        {
            EnsureOpen();
            if (source == TDJS_Vision.Device.Camera.TriggerSource.SOFT)
                return new TDJS_Vision.Device.Camera.TriggerEdge[0];
            lock (_lifecycleLock)
            {
                if (_isGrabbing)
                    throw new InvalidOperationException("读取触发极性前必须停止线扫取流。");
                string originalSelector = GetCurrentEnumSymbol("TriggerSelector");
                string originalMode = string.Empty;
                string originalSource = string.Empty;
                try
                {
                    SetEnumBySymbol("TriggerSelector", selector.ToString());
                    originalMode = GetCurrentEnumSymbol("TriggerMode");
                    SetEnumBySymbol("TriggerMode", "On");
                    originalSource = GetCurrentEnumSymbol("TriggerSource");
                    SetEnumBySymbol("TriggerSource", source.ToString().Replace("LINE", "Line"));
                    List<TDJS_Vision.Device.Camera.TriggerEdge> result = new List<TDJS_Vision.Device.Camera.TriggerEdge>();
                    foreach (string symbolic in GetSupportedEnumSymbols("TriggerActivation"))
                    {
                        TDJS_Vision.Device.Camera.TriggerEdge edge;
                        if (TryParseTriggerEdge(symbolic, out edge) && !result.Contains(edge))
                            result.Add(edge);
                    }
                    return result;
                }
                finally
                {
                    RestoreEnumBySymbol("TriggerSource", originalSource);
                    RestoreEnumBySymbol("TriggerMode", originalMode);
                    RestoreEnumBySymbol("TriggerSelector", originalSelector);
                }
            }
        }

        /// <summary>读取枚举节点当前值的GenICam符号名称。</summary>
        private string GetCurrentEnumSymbol(string key)
        {
            MyCamera.MVCC_ENUMVALUE value = new MyCamera.MVCC_ENUMVALUE();
            EnsureSuccess(_device.MV_CC_GetEnumValue_NET(key, ref value), "读取线扫参数" + key);
            return GetEnumSymbolic(key, value.nCurValue);
        }

        /// <summary>尽力恢复能力查询前的枚举状态，避免只查看界面就改变相机参数。</summary>
        private void RestoreEnumBySymbol(string key, string symbolic)
        {
            if (string.IsNullOrWhiteSpace(symbolic) || symbolic.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return;
            int result = _device.MV_CC_SetEnumValueByString_NET(key, symbolic);
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, string.Format("恢复线扫参数{0}为{1}失败，SDK错误码：0x{2:X8}。", key, symbolic, result), true);
        }

        /// <summary>读取枚举节点支持项的符号名称。</summary>
        private IEnumerable<string> GetSupportedEnumSymbols(string key)
        {
            MyCamera.MVCC_ENUMVALUE value = new MyCamera.MVCC_ENUMVALUE();
            EnsureSuccess(_device.MV_CC_GetEnumValue_NET(key, ref value), "读取线扫参数" + key);
            uint[] values = value.nSupportValue ?? new uint[0];
            int count = (int)Math.Min(value.nSupportedNum, (uint)values.Length);
            for (int index = 0; index < count; index++)
                yield return GetEnumSymbolic(key, values[index]);
        }

        /// <summary>把SDK触发源符号转换为框架枚举。</summary>
        private static bool TryParseTriggerSource(string symbolic, out TDJS_Vision.Device.Camera.TriggerSource source)
        {
            if (string.Equals(symbolic, "Software", StringComparison.OrdinalIgnoreCase)) { source = TDJS_Vision.Device.Camera.TriggerSource.SOFT; return true; }
            for (int index = 0; index <= 4; index++)
                if (string.Equals(symbolic, "Line" + index, StringComparison.OrdinalIgnoreCase)) { source = (TDJS_Vision.Device.Camera.TriggerSource)((int)TDJS_Vision.Device.Camera.TriggerSource.LINE0 + index); return true; }
            if (string.Equals(symbolic, "EncoderModuleOut", StringComparison.OrdinalIgnoreCase)) { source = TDJS_Vision.Device.Camera.TriggerSource.EncoderModuleOut; return true; }
            if (string.Equals(symbolic, "FrequencyConverter", StringComparison.OrdinalIgnoreCase)) { source = TDJS_Vision.Device.Camera.TriggerSource.FrequencyConverter; return true; }
            source = TDJS_Vision.Device.Camera.TriggerSource.Auto;
            return false;
        }

        /// <summary>把SDK触发极性符号转换为框架枚举。</summary>
        private static bool TryParseTriggerEdge(string symbolic, out TDJS_Vision.Device.Camera.TriggerEdge edge)
        {
            switch (symbolic)
            {
                case "RisingEdge": edge = TDJS_Vision.Device.Camera.TriggerEdge.Rising; return true;
                case "FallingEdge": edge = TDJS_Vision.Device.Camera.TriggerEdge.Falling; return true;
                case "AnyEdge": edge = TDJS_Vision.Device.Camera.TriggerEdge.Any; return true;
                case "LevelHigh": edge = TDJS_Vision.Device.Camera.TriggerEdge.Hight; return true;
                case "LevelLow": edge = TDJS_Vision.Device.Camera.TriggerEdge.Low; return true;
                default: edge = TDJS_Vision.Device.Camera.TriggerEdge.Rising; return false;
            }
        }

        /// <summary>读取PixelFormat当前值及相机实际支持的全部枚举项。</summary>
        private void RefreshPixelFormatCapabilities()
        {
            MyCamera.MVCC_ENUMVALUE value = new MyCamera.MVCC_ENUMVALUE();
            EnsureSuccess(_device.MV_CC_GetEnumValue_NET("PixelFormat", ref value), "读取线扫相机支持的像素格式");
            _supportedPixelFormats.Clear();
            uint[] supportedValues = value.nSupportValue ?? new uint[0];
            int count = (int)Math.Min(value.nSupportedNum, (uint)supportedValues.Length);
            for (int index = 0; index < count; index++)
            {
                string symbolic = GetEnumSymbolic("PixelFormat", supportedValues[index]);
                if (!string.IsNullOrWhiteSpace(symbolic) && !_supportedPixelFormats.Contains(symbolic))
                    _supportedPixelFormats.Add(symbolic);
            }
            CurrentPixelFormat = GetEnumSymbolic("PixelFormat", value.nCurValue);
        }

        /// <summary>取得枚举项的GenICam符号名称。</summary>
        private string GetEnumSymbolic(string key, uint value)
        {
            MyCamera.MVCC_ENUMENTRY entry = new MyCamera.MVCC_ENUMENTRY
            {
                nValue = value,
                chSymbolic = new byte[MyCamera.MV_MAX_SYMBOLIC_LEN],
                nReserved = new uint[4]
            };
            int result = _device.MV_CC_GetEnumEntrySymbolic_NET(key, ref entry);
            return result == MyCamera.MV_OK ? CleanByteString(entry.chSymbolic) : string.Format("0x{0:X8}", value);
        }

        /// <summary>把SDK固定长度字节字符串转换为托管字符串。</summary>
        private static string CleanByteString(byte[] value)
        {
            if (value == null)
                return string.Empty;
            int length = Array.IndexOf(value, (byte)0);
            if (length < 0)
                length = value.Length;
            return Encoding.Default.GetString(value, 0, length).Trim();
        }

        /// <summary>根据帧实际像素格式转换为框架统一使用的Mono8或BGR8图像。</summary>
        private Mat ConvertFrameToMat(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX info)
        {
            int channelCount;
            MyCamera.MvGvspPixelType destinationPixelType = ResolveDestinationPixelType(info.enPixelType, out channelCount);
            Mat image = new Mat((int)info.nHeight, (int)info.nWidth, channelCount == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3);
            try
            {
                MyCamera.MV_PIXEL_CONVERT_PARAM parameter = new MyCamera.MV_PIXEL_CONVERT_PARAM
                {
                    nWidth = info.nWidth,
                    nHeight = info.nHeight,
                    pSrcData = sourceData,
                    nSrcDataLen = info.nFrameLen,
                    enSrcPixelType = info.enPixelType,
                    enDstPixelType = destinationPixelType,
                    pDstBuffer = image.Data,
                    nDstBufferSize = checked((uint)(info.nWidth * info.nHeight * channelCount))
                };
                EnsureSuccess(_device.MV_CC_ConvertPixelType_NET(ref parameter),
                    string.Format("转换线扫像素格式{0}到{1}", info.enPixelType, destinationPixelType));
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }

        /// <summary>根据源像素格式选择统一输出格式。</summary>
        private MyCamera.MvGvspPixelType ResolveDestinationPixelType(MyCamera.MvGvspPixelType sourcePixelType, out int channelCount)
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
            throw new NotSupportedException(string.Format(
                "线扫相机当前像素格式{0}无法转换；当前节点={1}；相机支持={2}。",
                sourcePixelType,
                CurrentPixelFormat,
                string.Join("、", _supportedPixelFormats)));
        }

        /// <summary>判断源格式是否可以转换为Mono8。</summary>
        private static bool IsMonoPixelFormat(MyCamera.MvGvspPixelType pixelType)
        {
            switch (pixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono14:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono16:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>判断源格式是否可以转换为BGR8。</summary>
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

        /// <summary>读取布尔节点当前值。</summary>
        private bool GetBoolean(string key)
        {
            bool value = false;
            EnsureSuccess(_device.MV_CC_GetBoolValue_NET(key, ref value), "读取线扫参数" + key);
            return value;
        }

        /// <summary>确保相机已经打开。</summary>
        private void EnsureOpen()
        {
            ThrowIfDisposed();
            if (!IsOpen || _device == null)
                throw new InvalidOperationException("海康线扫相机尚未打开。");
        }

        /// <summary>确保对象尚未释放。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>将海康SDK错误码转换为明确异常。</summary>
        private static void EnsureSuccess(int result, string operation)
        {
            if (result != MyCamera.MV_OK)
                throw new InvalidOperationException(string.Format("{0}失败，SDK错误码：0x{1:X8}。", operation, result));
        }
    }
}
