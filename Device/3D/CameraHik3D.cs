using Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace TDJS_Vision.Device._3D
{
    /// <summary>
    /// 海康 3D 线激光轮廓相机设备，封装 Mv3dLpNet SDK 的枚举、连接、采集和数据转换。
    /// </summary>
    public class CameraHik3D : I3DCamera
    {
        /// <summary>
        /// SDK 初始化锁，保证进程内只初始化一次 3DVM SDK。
        /// </summary>
        private static readonly object SdkInitializeLock = new object();

        /// <summary>
        /// SDK 是否已经初始化。
        /// </summary>
        private static bool _isSdkInitialized;

        /// <summary>
        /// 设备句柄和采集线程锁。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 最近一帧数据锁，主动取帧时可复用采集线程已经拿到的数据。
        /// </summary>
        private readonly object _latestFrameLock = new object();

        /// <summary>
        /// SDK 原生点云帧缓存锁。
        /// </summary>
        private readonly object _nativePointCloudLock = new object();

        /// <summary>
        /// 当前 SDK 设备句柄。
        /// </summary>
        private IntPtr _deviceHandle = IntPtr.Zero;

        /// <summary>
        /// 最近一次 SDK 枚举得到的原始设备向量。
        /// </summary>
        private MV3D_LP_DEVICE_INFO_VECTOR _sdkDeviceVector;

        /// <summary>
        /// 最近一次枚举得到的界面设备列表。
        /// </summary>
        private List<Camera3DDeviceInfo> _devices = new List<Camera3DDeviceInfo>();

        /// <summary>
        /// 后台采集线程。
        /// </summary>
        private Thread _grabThread;

        /// <summary>
        /// 后台采集循环标记。
        /// </summary>
        private volatile bool _isGrabbing;

        /// <summary>
        /// 采集帧率统计计时器。
        /// </summary>
        private readonly Stopwatch _frameRateStopwatch = new Stopwatch();

        /// <summary>
        /// 当前帧率统计窗口内的帧数。
        /// </summary>
        private int _frameRateCounter;

        /// <summary>
        /// 最近一次计算出的采集帧率。
        /// </summary>
        private double _latestFrameRate;

        /// <summary>
        /// 最近一次 SDK 原生点云帧元数据。
        /// </summary>
        private MV3D_LP_IMAGE_DATA _latestNativePointCloudImage = new MV3D_LP_IMAGE_DATA();

        /// <summary>
        /// 最近一次 SDK 原生点云帧数据。
        /// </summary>
        private byte[] _latestNativePointCloudBytes = new byte[0];

        /// <summary>
        /// 最近一次转换后的 3D 帧数据。
        /// </summary>
        private Camera3DFrameData _latestFrameData;

        /// <summary>
        /// 连接状态改变事件。
        /// </summary>
        public event EventHandler<bool> ConnectStatusEvent;

        /// <summary>
        /// 3D 帧数据到达事件。
        /// </summary>
        public event EventHandler<Camera3DFrameArrivedEventArgs> FrameArrived;

        /// <summary>
        /// 3D 相机状态变化事件。
        /// </summary>
        public event EventHandler<Camera3DStatusChangedEventArgs> StatusChanged;

        /// <summary>
        /// 3D 相机错误事件。
        /// </summary>
        public event EventHandler<Camera3DErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 当前是否已打开设备；反序列化写入 true 时不会自动打开真实设备。
        /// </summary>
        [JsonIgnore]
        public bool IsOpen
        {
            get => _deviceHandle != IntPtr.Zero;
            set
            {
                if (!value && _deviceHandle != IntPtr.Zero)
                    Close();
            }
        }

        /// <summary>
        /// 当前是否正在采集。
        /// </summary>
        [JsonIgnore]
        public bool IsGrabbing => _isGrabbing;

        /// <summary>
        /// 硬件设备名称。
        /// </summary>
        public string DevName { get; set; }

        /// <summary>
        /// 用户自定义设备名。
        /// </summary>
        public string UserDefinedName { get; set; }

        /// <summary>
        /// 设备类型。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.CAMERA;

        /// <summary>
        /// 设备品牌。
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceBrand Brand { get; set; } = DeviceBrand.HikVision;

        /// <summary>
        /// 反序列化类型标识。
        /// </summary>
        public string ClassName { get; set; } = typeof(CameraHik3D).FullName;

        /// <summary>
        /// 设备序列号。
        /// </summary>
        public string SN { get; set; }

        /// <summary>
        /// 设备 IP 地址。
        /// </summary>
        public string IP { get; set; } = "无";

        /// <summary>
        /// 图像获取超时时间，单位毫秒。
        /// </summary>
        public uint GetImageTimeOut { get; set; } = 2000;

        /// <summary>
        /// 方案加载后是否自动打开并开始采集。
        /// </summary>
        public bool AutoConnectOnLoad { get; set; } = true;

        /// <summary>
        /// 3D 相机连接配置。
        /// </summary>
        public Camera3DConnectionConfig ConnectionConfig { get; set; } = new Camera3DConnectionConfig();

        /// <summary>
        /// 3D 相机采集配置。
        /// </summary>
        public Camera3DGrabConfig GrabConfig { get; set; } = new Camera3DGrabConfig();

        /// <summary>
        /// 创建用于反序列化的海康 3D 相机对象。
        /// </summary>
        [JsonConstructor]
        public CameraHik3D()
        {
        }

        /// <summary>
        /// 根据枚举到的设备信息创建海康 3D 相机对象。
        /// </summary>
        /// <param name="deviceInfo">3D 相机设备信息。</param>
        /// <param name="userDefinedName">用户自定义设备名。</param>
        public CameraHik3D(Camera3DDeviceInfo deviceInfo, string userDefinedName)
        {
            if (deviceInfo == null)
                throw new ArgumentNullException(nameof(deviceInfo));

            UserDefinedName = userDefinedName;
            DevName = deviceInfo.DisplayName;
            SN = deviceInfo.SerialNumber;
            IP = string.IsNullOrWhiteSpace(deviceInfo.IpAddress) ? "无" : deviceInfo.IpAddress;
            ConnectionConfig = new Camera3DConnectionConfig
            {
                OpenMode = Camera3DOpenMode.SerialNumber,
                ModelName = deviceInfo.ModelName,
                SerialNumber = deviceInfo.SerialNumber,
                IpAddress = deviceInfo.IpAddress,
                ManufacturerName = deviceInfo.ManufacturerName,
                GetImageTimeoutMs = GetImageTimeOut
            };
        }

        /// <summary>
        /// 查找在线海康 3D 相机设备。
        /// </summary>
        /// <returns>在线 3D 相机设备列表。</returns>
        public static List<Camera3DDeviceInfo> FindCamera()
        {
            try
            {
                return EnumerateSdkDevices().Devices;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"获取海康3D相机列表失败：{ex.Message}", true);
                return new List<Camera3DDeviceInfo>();
            }
        }

        /// <summary>
        /// 根据 3D 相机设备信息生成界面设备名称。
        /// </summary>
        /// <param name="deviceInfo">3D 相机设备信息。</param>
        /// <returns>界面设备名称。</returns>
        public static string GetDevNameByDevInfo(Camera3DDeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(deviceInfo.DisplayName))
                return deviceInfo.DisplayName;

            return $"{deviceInfo.ModelName}({deviceInfo.SerialNumber})";
        }

        /// <summary>
        /// 创建设备对象，反序列化后同步基础属性，真实 SDK 句柄由 Open 方法创建。
        /// </summary>
        public void CreateDevice()
        {
            SyncBasicPropertiesFromConfig();
        }

        /// <summary>
        /// 枚举在线海康 3D 相机设备。
        /// </summary>
        /// <returns>在线 3D 相机设备列表。</returns>
        public IReadOnlyList<Camera3DDeviceInfo> EnumerateDevices()
        {
            lock (_syncRoot)
            {
                SdkDeviceEnumerationResult result = EnumerateSdkDevices();
                _sdkDeviceVector = result.SdkDeviceVector;
                _devices = result.Devices;
                RaiseStatus(_devices.Count == 0 ? "未发现海康3D相机" : $"发现 {_devices.Count} 台海康3D相机");
                return _devices.AsReadOnly();
            }
        }

        /// <summary>
        /// 打开当前配置指向的海康 3D 相机。
        /// </summary>
        /// <returns>打开成功返回 true。</returns>
        public bool Open()
        {
            lock (_syncRoot)
            {
                EnsureSdkInitialized();
                SyncBasicPropertiesFromConfig();
                if (IsOpen)
                    Close();

                int ret;
                if (ConnectionConfig.OpenMode == Camera3DOpenMode.IpAddress)
                {
                    string ipAddress = GetRequiredIpAddress();
                    ret = Mv3dLpSDK.MV3D_LP_OpenDeviceByIP(ref _deviceHandle, ipAddress);
                    ThrowIfSdkFailed("通过IP打开海康3D相机失败", ret);
                }
                else
                {
                    string serialNumber = GetRequiredSerialNumber();
                    ret = Mv3dLpSDK.MV3D_LP_OpenDeviceBySN(ref _deviceHandle, serialNumber);
                    ThrowIfSdkFailed("通过序列号打开海康3D相机失败", ret);
                }

                try
                {
                    ApplyImageMode(GetConfiguredImageMode());
                }
                catch
                {
                    Close();
                    throw;
                }

                RaiseConnectStatus(true);
                RaiseStatus($"已打开海康3D相机 {DevName}");
                return true;
            }
        }

        /// <summary>
        /// 关闭当前海康 3D 相机。
        /// </summary>
        public void Close()
        {
            StopGrabbing();
            lock (_syncRoot)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    IntPtr handle = _deviceHandle;
                    int ret = Mv3dLpSDK.MV3D_LP_CloseDevice(ref handle);
                    _deviceHandle = IntPtr.Zero;
                    if (ret != Mv3dLpSDK.MV3D_LP_OK)
                        RaiseError("关闭海康3D相机失败", ret);
                }

                ClearFrameCaches();
                RaiseConnectStatus(false);
                RaiseStatus("3D 相机已关闭");
            }
        }

        /// <summary>
        /// 开始采集 3D 数据。
        /// </summary>
        public void StartGrabbing()
        {
            lock (_syncRoot)
            {
                EnsureOpen();
                if (_isGrabbing)
                    return;

                ApplyImageMode(GetConfiguredImageMode());
                Mv3dLpSDK.MV3D_LP_ClearDataBuffer(_deviceHandle);
                ClearFrameCaches();
                int ret = Mv3dLpSDK.MV3D_LP_StartMeasure(_deviceHandle);
                ThrowIfSdkFailed("开始采集海康3D数据失败", ret);

                _isGrabbing = true;
                _frameRateCounter = 0;
                _latestFrameRate = 0D;
                _frameRateStopwatch.Restart();
                _grabThread = new Thread(GrabLoop)
                {
                    IsBackground = true,
                    Name = "海康3D相机采集线程"
                };
                _grabThread.Start();
                RaiseStatus("海康3D相机采集中");
            }
        }

        /// <summary>
        /// 停止采集 3D 数据。
        /// </summary>
        public void StopGrabbing()
        {
            Thread threadToJoin = null;
            lock (_syncRoot)
            {
                if (!_isGrabbing)
                    return;

                _isGrabbing = false;
                threadToJoin = _grabThread;
                _grabThread = null;
            }

            if (threadToJoin != null && threadToJoin != Thread.CurrentThread)
                threadToJoin.Join(1000);

            lock (_syncRoot)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    int ret = Mv3dLpSDK.MV3D_LP_StopMeasure(_deviceHandle);
                    if (ret != Mv3dLpSDK.MV3D_LP_OK)
                        RaiseError("停止海康3D采集失败", ret);
                }

                _frameRateStopwatch.Reset();
                ClearFrameCaches();
                RaiseStatus(IsOpen ? "3D 相机采集已停止" : "3D 相机未打开");
            }
        }

        /// <summary>
        /// 获取当前采集状态。
        /// </summary>
        /// <returns>正在采集返回 true。</returns>
        public bool GetGrabStatus()
        {
            return IsGrabbing;
        }

        /// <summary>
        /// 执行一次软触发。
        /// </summary>
        public void GrabOne()
        {
            lock (_syncRoot)
            {
                EnsureOpen();
                int ret = Mv3dLpSDK.MV3D_LP_SoftTrigger(_deviceHandle);
                ThrowIfSdkFailed("海康3D相机软触发失败", ret);
                RaiseStatus("已执行海康3D相机软触发");
            }
        }

        /// <summary>
        /// 主动获取一帧 3D 数据。
        /// </summary>
        /// <returns>3D 帧数据。</returns>
        public Camera3DFrameData GetOneFrameData()
        {
            Camera3DFrameData latestFrame = TakeLatestFrameData();
            if (_isGrabbing && latestFrame != null)
                return latestFrame;

            bool needStopMeasure = false;
            IntPtr handle;
            lock (_syncRoot)
            {
                EnsureOpen();
                handle = _deviceHandle;
                if (!_isGrabbing)
                {
                    ApplyImageMode(GetConfiguredImageMode());
                    Mv3dLpSDK.MV3D_LP_ClearDataBuffer(handle);
                    int startRet = Mv3dLpSDK.MV3D_LP_StartMeasure(handle);
                    ThrowIfSdkFailed("主动取帧启动海康3D采集失败", startRet);
                    needStopMeasure = true;
                }
            }

            try
            {
                if (ShouldSoftTrigger())
                    GrabOne();

                MV3D_LP_IMAGE_DATA imageData = new MV3D_LP_IMAGE_DATA();
                int ret = Mv3dLpSDK.MV3D_LP_GetImage(handle, imageData, GetConfiguredTimeout());
                ThrowIfSdkFailed("主动获取海康3D图像失败", ret);

                Camera3DFrameData frameData = ConvertFrame(imageData);
                ClearNativePointCloudCache();
                return frameData;
            }
            finally
            {
                if (needStopMeasure)
                {
                    lock (_syncRoot)
                    {
                        if (_deviceHandle != IntPtr.Zero)
                        {
                            int stopRet = Mv3dLpSDK.MV3D_LP_StopMeasure(_deviceHandle);
                            if (stopRet != Mv3dLpSDK.MV3D_LP_OK)
                                RaiseError("主动取帧停止海康3D采集失败", stopRet);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 使用 SDK 原生显示器显示最近一次点云帧。
        /// </summary>
        /// <param name="windowHandle">显示宿主窗口句柄。</param>
        /// <returns>显示成功返回 true。</returns>
        public bool DisplayLatestPointCloud(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            byte[] pointCloudBytes;
            MV3D_LP_IMAGE_DATA pointCloudImage;
            lock (_nativePointCloudLock)
            {
                if (_latestNativePointCloudBytes == null || _latestNativePointCloudBytes.Length == 0)
                    return false;

                pointCloudBytes = _latestNativePointCloudBytes;
                pointCloudImage = CloneImageDataMetadata(_latestNativePointCloudImage);
            }

            GCHandle dataHandle = GCHandle.Alloc(pointCloudBytes, GCHandleType.Pinned);
            try
            {
                pointCloudImage.pData = dataHandle.AddrOfPinnedObject();
                int ret = Mv3dLpSDK.MV3D_LP_DisplayImage(pointCloudImage, windowHandle, Mv3dLpSDK.DisplayType_Auto, 0, 0);
                if (ret != Mv3dLpSDK.MV3D_LP_OK)
                {
                    RaiseError("SDK 原生点云显示失败", ret);
                    return false;
                }
                return true;
            }
            finally
            {
                dataHandle.Free();
            }
        }

        /// <summary>
        /// 释放海康 3D 相机资源。
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 枚举 SDK 设备并返回托管设备列表和原始设备向量。
        /// </summary>
        /// <returns>SDK 设备枚举结果。</returns>
        private static SdkDeviceEnumerationResult EnumerateSdkDevices()
        {
            EnsureSdkInitialized();

            uint deviceCount = 0;
            int ret = Mv3dLpSDK.MV3D_LP_GetDeviceNumber(ref deviceCount);
            ThrowIfFailed("获取海康3D设备数量失败", ret);

            SdkDeviceEnumerationResult result = new SdkDeviceEnumerationResult
            {
                Devices = new List<Camera3DDeviceInfo>(),
                SdkDeviceVector = new MV3D_LP_DEVICE_INFO_VECTOR((int)deviceCount)
            };

            if (deviceCount == 0)
                return result;

            for (uint i = 0; i < deviceCount; i++)
                result.SdkDeviceVector.Add(new MV3D_LP_DEVICE_INFO());

            ret = Mv3dLpSDK.MV3D_LP_GetDeviceList(result.SdkDeviceVector[0], deviceCount, ref deviceCount);
            ThrowIfFailed("获取海康3D设备列表失败", ret);

            for (int i = 0; i < deviceCount; i++)
            {
                MV3D_LP_DEVICE_INFO sdkDeviceInfo = result.SdkDeviceVector[i];
                Camera3DDeviceInfo deviceInfo = new Camera3DDeviceInfo
                {
                    Index = i,
                    ManufacturerName = TrimSdkString(sdkDeviceInfo.chManufacturerName),
                    ModelName = TrimSdkString(sdkDeviceInfo.chModelName),
                    SerialNumber = TrimSdkString(sdkDeviceInfo.chSerialNumber),
                    IpAddress = TrimSdkString(sdkDeviceInfo.chCurrentIp)
                };
                result.Devices.Add(deviceInfo);
            }

            return result;
        }

        /// <summary>
        /// 确保 3DVM SDK 已初始化。
        /// </summary>
        private static void EnsureSdkInitialized()
        {
            lock (SdkInitializeLock)
            {
                if (_isSdkInitialized)
                    return;

                int ret = Mv3dLpSDK.MV3D_LP_Initialize();
                ThrowIfFailed("初始化海康3D SDK失败", ret);
                _isSdkInitialized = true;
            }
        }

        /// <summary>
        /// 从连接配置同步基础设备属性。
        /// </summary>
        private void SyncBasicPropertiesFromConfig()
        {
            if (ConnectionConfig == null)
                ConnectionConfig = new Camera3DConnectionConfig();

            if (GrabConfig == null)
                GrabConfig = new Camera3DGrabConfig();

            if (string.IsNullOrWhiteSpace(ConnectionConfig.SerialNumber) && !string.IsNullOrWhiteSpace(SN))
                ConnectionConfig.SerialNumber = SN;

            if (string.IsNullOrWhiteSpace(ConnectionConfig.IpAddress) && !string.IsNullOrWhiteSpace(IP) && IP != "无")
                ConnectionConfig.IpAddress = IP;

            if (ConnectionConfig.GetImageTimeoutMs == 0)
                ConnectionConfig.GetImageTimeoutMs = GetImageTimeOut == 0 ? 2000 : GetImageTimeOut;

            SN = ConnectionConfig.SerialNumber;
            IP = string.IsNullOrWhiteSpace(ConnectionConfig.IpAddress) ? "无" : ConnectionConfig.IpAddress;
            GetImageTimeOut = ConnectionConfig.GetImageTimeoutMs;
            if (string.IsNullOrWhiteSpace(DevName))
                DevName = $"{ConnectionConfig.ModelName}({ConnectionConfig.SerialNumber})";
        }

        /// <summary>
        /// 将设备设置为当前配置的图像模式。
        /// </summary>
        /// <param name="imageMode">图像模式。</param>
        private void ApplyImageMode(Camera3DImageMode imageMode)
        {
            EnsureOpen();

            MV3D_LP_PARAM param = new MV3D_LP_PARAM();
            MV3D_LP_ENUMPARAM enumParam = new MV3D_LP_ENUMPARAM
            {
                nCurValue = (uint)imageMode
            };
            param.set_enumparam(enumParam);
            int ret = Mv3dLpSDK.MV3D_LP_SetParam(_deviceHandle, Mv3dLpSDK.MV3D_LP_ENUM_IMAGEMODE, param);
            ThrowIfSdkFailed("设置海康3D图像模式失败", ret);
        }

        /// <summary>
        /// 后台采集线程循环。
        /// </summary>
        private void GrabLoop()
        {
            while (_isGrabbing)
            {
                try
                {
                    MV3D_LP_IMAGE_DATA imageData = new MV3D_LP_IMAGE_DATA();
                    int ret = Mv3dLpSDK.MV3D_LP_GetImage(_deviceHandle, imageData, 50);
                    if (ret == Mv3dLpSDK.MV3D_LP_OK)
                    {
                        Camera3DFrameData frameData = ConvertFrame(imageData);
                        StoreLatestFrameData(frameData);
                        RaiseFrameArrived(frameData);
                    }
                    else if (ret != Mv3dLpSDK.MV3D_LP_E_NODATA)
                    {
                        RaiseError("获取海康3D图像失败", ret);
                    }
                }
                catch (Exception ex)
                {
                    RaiseError(ex.Message, Mv3dLpSDK.MV3D_LP_E_UNKNOW);
                }
            }
        }

        /// <summary>
        /// 将 SDK 图像帧转换为业务层 3D 帧数据。
        /// </summary>
        /// <param name="imageData">SDK 图像数据。</param>
        /// <returns>业务层 3D 帧数据。</returns>
        private Camera3DFrameData ConvertFrame(MV3D_LP_IMAGE_DATA imageData)
        {
            int width = (int)imageData.nWidth;
            int height = (int)imageData.nHeight;
            int[] depthValues = CanConvertDepthValues(imageData)
                ? ConvertDepthValues(imageData, width, height)
                : new int[0];
            int totalPointCount;
            Camera3DNativePointCloudFrame nativePointCloudFrame;
            Camera3DPointCloudPoint[] pointCloudPoints = ConvertPointCloud(imageData, out totalPointCount, out nativePointCloudFrame);

            bool hasDepth = depthValues != null && depthValues.Length > 0;
            bool hasPointCloud = totalPointCount > 0
                || (pointCloudPoints != null && pointCloudPoints.Length > 0)
                || (nativePointCloudFrame != null && nativePointCloudFrame.DataLength > 0);

            return new Camera3DFrameData
            {
                ContentType = GetFrameContentType(hasDepth, hasPointCloud),
                Width = width,
                Height = height,
                FrameNumber = imageData.nFrameNum,
                DepthValues = depthValues,
                DepthInvalidValue = short.MinValue,
                PointCloudPoints = pointCloudPoints,
                TotalPointCount = totalPointCount,
                NativePointCloudFrame = nativePointCloudFrame,
                FrameRate = UpdateFrameRate(),
                DisplayFrameRate = _latestFrameRate,
                XScale = imageData.fXScale,
                YScale = imageData.fYScale,
                ZScale = imageData.fZScale
            };
        }

        /// <summary>
        /// 从 SDK 深度图解析 short 深度值。
        /// </summary>
        /// <param name="imageData">SDK 图像数据。</param>
        /// <param name="width">图像宽度。</param>
        /// <param name="height">图像高度。</param>
        /// <returns>深度值数组。</returns>
        private static int[] ConvertDepthValues(MV3D_LP_IMAGE_DATA imageData, int width, int height)
        {
            int pixelCount = Math.Max(0, width * height);
            int[] depthValues = new int[pixelCount];
            if (pixelCount == 0 || imageData.pData == IntPtr.Zero || imageData.nDataLen == 0)
                return depthValues;

            int byteCount = Math.Min((int)imageData.nDataLen, pixelCount * 2);
            byte[] bytes = new byte[byteCount];
            Marshal.Copy(imageData.pData, bytes, 0, byteCount);
            int valueCount = byteCount / 2;
            for (int i = 0; i < valueCount; i++)
                depthValues[i] = (short)((bytes[i * 2] & 0xFF) | ((bytes[i * 2 + 1] & 0xFF) << 8));

            return depthValues;
        }

        /// <summary>
        /// 判断当前 SDK 图像是否适合按 16 位深度图解析。
        /// </summary>
        /// <param name="imageData">SDK 图像数据。</param>
        /// <returns>适合解析深度值时返回 true。</returns>
        private static bool CanConvertDepthValues(MV3D_LP_IMAGE_DATA imageData)
        {
            return imageData.enImageType != Mv3dLpSDK.ImageType_PointCloud
                && imageData.enImageType != Mv3dLpSDK.ImageType_RGB24_Packed
                && imageData.enImageType != Mv3dLpSDK.ImageType_Jpeg;
        }

        /// <summary>
        /// 将 SDK 深度图转换为点云，并按配置生成原生缓存或托管抽样点。
        /// </summary>
        /// <param name="imageData">SDK 深度图或点云图。</param>
        /// <param name="totalPointCount">输出原始总点数。</param>
        /// <param name="nativePointCloudFrame">输出 SDK 原生点云帧。</param>
        /// <returns>用于托管显示的抽样点云。</returns>
        private Camera3DPointCloudPoint[] ConvertPointCloud(MV3D_LP_IMAGE_DATA imageData, out int totalPointCount, out Camera3DNativePointCloudFrame nativePointCloudFrame)
        {
            totalPointCount = 0;
            nativePointCloudFrame = null;
            if (imageData.pData == IntPtr.Zero || imageData.nDataLen == 0)
                return new Camera3DPointCloudPoint[0];

            MV3D_LP_IMAGE_DATA pointCloudImage;
            if (imageData.enImageType == Mv3dLpSDK.ImageType_PointCloud)
            {
                pointCloudImage = imageData;
            }
            else
            {
                pointCloudImage = new MV3D_LP_IMAGE_DATA();
                int ret = Mv3dLpSDK.MV3D_LP_MapDepthToPointCloud(imageData, pointCloudImage);
                if (ret != Mv3dLpSDK.MV3D_LP_OK || pointCloudImage.pData == IntPtr.Zero || pointCloudImage.nDataLen == 0)
                {
                    if (ret != Mv3dLpSDK.MV3D_LP_OK)
                        RaiseError("海康3D深度图转点云失败", ret);

                    return new Camera3DPointCloudPoint[0];
                }
            }

            int byteCount = (int)pointCloudImage.nDataLen;
            totalPointCount = byteCount / 12;
            if (totalPointCount <= 0)
                return new Camera3DPointCloudPoint[0];

            bool needNativeCache = GrabConfig != null && GrabConfig.EnableNativePointCloudCache;
            bool needManagedPoints = GrabConfig != null && GrabConfig.BuildManagedPointCloudFallback;
            if (!needNativeCache && !needManagedPoints)
                return new Camera3DPointCloudPoint[0];

            byte[] bytes = new byte[byteCount];
            Marshal.Copy(pointCloudImage.pData, bytes, 0, bytes.Length);
            if (needNativeCache)
            {
                CacheNativePointCloud(pointCloudImage, bytes);
                nativePointCloudFrame = BuildNativePointCloudFrame(pointCloudImage, bytes);
            }

            if (!needManagedPoints)
                return new Camera3DPointCloudPoint[0];

            return BuildManagedPointCloudPoints(bytes, totalPointCount);
        }

        /// <summary>
        /// 根据点云原始字节构建托管抽样点。
        /// </summary>
        /// <param name="bytes">点云原始字节，每个点按 X/Y/Z 三个 float 存储。</param>
        /// <param name="totalPointCount">原始总点数。</param>
        /// <returns>抽样后的托管点云。</returns>
        private Camera3DPointCloudPoint[] BuildManagedPointCloudPoints(byte[] bytes, int totalPointCount)
        {
            int maxPointCount = Math.Max(1000, GrabConfig == null ? 350000 : GrabConfig.MaxDisplayPointCount);
            int step = Math.Max(1, (totalPointCount + maxPointCount - 1) / maxPointCount);
            int displayPointCount = (totalPointCount + step - 1) / step;
            Camera3DPointCloudPoint[] points = new Camera3DPointCloudPoint[displayPointCount];
            int targetIndex = 0;

            for (int sourcePointIndex = 0; sourcePointIndex < totalPointCount && targetIndex < points.Length; sourcePointIndex += step)
            {
                int byteIndex = sourcePointIndex * 12;
                if (byteIndex + 11 >= bytes.Length)
                    break;

                float x = BitConverter.ToSingle(bytes, byteIndex);
                float y = BitConverter.ToSingle(bytes, byteIndex + 4);
                float z = BitConverter.ToSingle(bytes, byteIndex + 8);
                Camera3DPointCloudPoint point = new Camera3DPointCloudPoint(x, y, z, z);
                if (!point.IsFinite())
                    continue;

                points[targetIndex++] = point;
            }

            if (targetIndex != points.Length)
                Array.Resize(ref points, targetIndex);

            return points;
        }

        /// <summary>
        /// 缓存 SDK 原生点云帧，后续交给 MV3D_LP_DisplayImage 直接显示。
        /// </summary>
        /// <param name="sourceImage">SDK 点云帧元数据。</param>
        /// <param name="pointCloudBytes">SDK 点云帧数据。</param>
        private void CacheNativePointCloud(MV3D_LP_IMAGE_DATA sourceImage, byte[] pointCloudBytes)
        {
            lock (_nativePointCloudLock)
            {
                _latestNativePointCloudImage = CloneImageDataMetadata(sourceImage);
                _latestNativePointCloudBytes = pointCloudBytes ?? new byte[0];
            }
        }

        /// <summary>
        /// 创建对外可传递的 SDK 原生点云帧数据。
        /// </summary>
        /// <param name="sourceImage">SDK 点云帧元数据。</param>
        /// <param name="pointCloudBytes">SDK 点云帧数据。</param>
        /// <returns>原生点云帧。</returns>
        private static Camera3DNativePointCloudFrame BuildNativePointCloudFrame(MV3D_LP_IMAGE_DATA sourceImage, byte[] pointCloudBytes)
        {
            return new Camera3DNativePointCloudFrame
            {
                Width = (int)sourceImage.nWidth,
                Height = (int)sourceImage.nHeight,
                ImageType = sourceImage.enImageType,
                FrameNumber = sourceImage.nFrameNum,
                DataBytes = pointCloudBytes ?? new byte[0],
                XScale = sourceImage.fXScale,
                YScale = sourceImage.fYScale,
                ZScale = sourceImage.fZScale,
                XOffset = sourceImage.nXOffset,
                YOffset = sourceImage.nYOffset,
                ZOffset = sourceImage.nZOffset
            };
        }

        /// <summary>
        /// 复制 SDK 图像元数据，数据指针由显示调用时重新设置为受控缓存地址。
        /// </summary>
        /// <param name="sourceImage">源 SDK 图像。</param>
        /// <returns>复制后的 SDK 图像元数据。</returns>
        private static MV3D_LP_IMAGE_DATA CloneImageDataMetadata(MV3D_LP_IMAGE_DATA sourceImage)
        {
            return new MV3D_LP_IMAGE_DATA
            {
                nWidth = sourceImage.nWidth,
                nHeight = sourceImage.nHeight,
                nDataLen = sourceImage.nDataLen,
                enImageType = sourceImage.enImageType,
                nFrameNum = sourceImage.nFrameNum,
                fXScale = sourceImage.fXScale,
                fYScale = sourceImage.fYScale,
                fZScale = sourceImage.fZScale,
                nXOffset = sourceImage.nXOffset,
                nYOffset = sourceImage.nYOffset,
                nZOffset = sourceImage.nZOffset,
                pData = IntPtr.Zero,
                pIntensityData = IntPtr.Zero,
                nIntensityDataLen = 0
            };
        }

        /// <summary>
        /// 更新并返回采集帧率。
        /// </summary>
        /// <returns>采集帧率。</returns>
        private double UpdateFrameRate()
        {
            _frameRateCounter++;
            if (!_frameRateStopwatch.IsRunning)
                _frameRateStopwatch.Start();

            double elapsedSeconds = _frameRateStopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds >= 1D)
            {
                _latestFrameRate = _frameRateCounter / elapsedSeconds;
                _frameRateCounter = 0;
                _frameRateStopwatch.Restart();
            }

            return _latestFrameRate;
        }

        /// <summary>
        /// 取出最近一次转换后的 3D 帧数据并清空缓存，避免流程重复消费同一帧。
        /// </summary>
        /// <returns>最近一帧数据。</returns>
        private Camera3DFrameData TakeLatestFrameData()
        {
            lock (_latestFrameLock)
            {
                Camera3DFrameData frameData = _latestFrameData;
                _latestFrameData = null;
                if (frameData != null)
                    ClearNativePointCloudCache();
                return frameData;
            }
        }

        /// <summary>
        /// 保存最近一次转换后的 3D 帧数据。
        /// </summary>
        /// <param name="frameData">3D 帧数据。</param>
        private void StoreLatestFrameData(Camera3DFrameData frameData)
        {
            lock (_latestFrameLock)
            {
                _latestFrameData = frameData;
            }
        }

        /// <summary>
        /// 清空帧缓存，防止关闭后继续显示旧点云。
        /// </summary>
        private void ClearFrameCaches()
        {
            lock (_latestFrameLock)
            {
                _latestFrameData = null;
            }

            ClearNativePointCloudCache();
        }

        /// <summary>
        /// 清空最近一次 SDK 原生点云缓存，避免旧点云被误当作当前帧显示。
        /// </summary>
        private void ClearNativePointCloudCache()
        {
            lock (_nativePointCloudLock)
            {
                _latestNativePointCloudImage = new MV3D_LP_IMAGE_DATA();
                _latestNativePointCloudBytes = new byte[0];
            }
        }

        /// <summary>
        /// 获取当前配置的图像模式。
        /// </summary>
        /// <returns>图像模式。</returns>
        private Camera3DImageMode GetConfiguredImageMode()
        {
            return GrabConfig == null ? Camera3DImageMode.Range : GrabConfig.ImageMode;
        }

        /// <summary>
        /// 获取当前配置的取图超时时间。
        /// </summary>
        /// <returns>取图超时时间，单位毫秒。</returns>
        private uint GetConfiguredTimeout()
        {
            if (ConnectionConfig != null && ConnectionConfig.GetImageTimeoutMs > 0)
                return ConnectionConfig.GetImageTimeoutMs;

            return GetImageTimeOut == 0 ? 2000 : GetImageTimeOut;
        }

        /// <summary>
        /// 判断当前配置是否需要软件触发。
        /// </summary>
        /// <returns>需要软件触发返回 true。</returns>
        private bool ShouldSoftTrigger()
        {
            return GrabConfig != null
                && GrabConfig.TriggerMode == Camera3DTriggerMode.On
                && GrabConfig.TriggerSource == Camera3DTriggerSource.Soft;
        }

        /// <summary>
        /// 按是否存在深度图和点云数据推断帧内容类型。
        /// </summary>
        /// <param name="hasDepth">是否包含深度图。</param>
        /// <param name="hasPointCloud">是否包含点云。</param>
        /// <returns>帧内容类型。</returns>
        private static Camera3DFrameContentType GetFrameContentType(bool hasDepth, bool hasPointCloud)
        {
            if (hasDepth && hasPointCloud)
                return Camera3DFrameContentType.DepthAndPointCloud;

            if (hasDepth)
                return Camera3DFrameContentType.Depth;

            if (hasPointCloud)
                return Camera3DFrameContentType.PointCloud;

            return Camera3DFrameContentType.Unknown;
        }

        /// <summary>
        /// 获取必填序列号。
        /// </summary>
        /// <returns>序列号。</returns>
        private string GetRequiredSerialNumber()
        {
            string serialNumber = ConnectionConfig == null ? string.Empty : ConnectionConfig.SerialNumber;
            if (string.IsNullOrWhiteSpace(serialNumber))
                serialNumber = SN;

            if (string.IsNullOrWhiteSpace(serialNumber))
                throw new InvalidOperationException("海康3D相机序列号为空，无法打开设备。");

            return serialNumber;
        }

        /// <summary>
        /// 获取必填 IP 地址。
        /// </summary>
        /// <returns>IP 地址。</returns>
        private string GetRequiredIpAddress()
        {
            string ipAddress = ConnectionConfig == null ? string.Empty : ConnectionConfig.IpAddress;
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "无")
                ipAddress = IP;

            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "无")
                throw new InvalidOperationException("海康3D相机IP地址为空，无法打开设备。");

            return ipAddress;
        }

        /// <summary>
        /// 确保设备已打开。
        /// </summary>
        private void EnsureOpen()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("海康3D相机未打开。");
        }

        /// <summary>
        /// SDK 调用失败时抛出异常。
        /// </summary>
        /// <param name="message">业务说明。</param>
        /// <param name="errorCode">SDK 错误码。</param>
        private static void ThrowIfFailed(string message, int errorCode)
        {
            if (errorCode != Mv3dLpSDK.MV3D_LP_OK)
                throw CreateSdkException(message, errorCode);
        }

        /// <summary>
        /// SDK 调用失败时先触发错误事件再抛出异常。
        /// </summary>
        /// <param name="message">业务说明。</param>
        /// <param name="errorCode">SDK 错误码。</param>
        private void ThrowIfSdkFailed(string message, int errorCode)
        {
            if (errorCode == Mv3dLpSDK.MV3D_LP_OK)
                return;

            RaiseError(message, errorCode);
            throw CreateSdkException(message, errorCode);
        }

        /// <summary>
        /// 创建 SDK 异常。
        /// </summary>
        /// <param name="message">业务说明。</param>
        /// <param name="errorCode">SDK 错误码。</param>
        /// <returns>SDK 异常对象。</returns>
        private static InvalidOperationException CreateSdkException(string message, int errorCode)
        {
            return new InvalidOperationException($"{message}：{GetErrorText(errorCode)}（0x{errorCode:X8}）");
        }

        /// <summary>
        /// 去除 SDK 字符串尾部空字符。
        /// </summary>
        /// <param name="value">SDK 字符串。</param>
        /// <returns>普通字符串。</returns>
        private static string TrimSdkString(string value)
        {
            return (value ?? string.Empty).TrimEnd('\0').Trim();
        }

        /// <summary>
        /// 将常见 SDK 错误码转换为中文说明。
        /// </summary>
        /// <param name="errorCode">SDK 错误码。</param>
        /// <returns>错误说明。</returns>
        private static string GetErrorText(int errorCode)
        {
            switch (errorCode)
            {
                case Mv3dLpSDK.MV3D_LP_OK: return "成功";
                case Mv3dLpSDK.MV3D_LP_E_HANDLE: return "句柄错误或无效";
                case Mv3dLpSDK.MV3D_LP_E_SUPPORT: return "功能不支持";
                case Mv3dLpSDK.MV3D_LP_E_BUFOVER: return "缓存已满";
                case Mv3dLpSDK.MV3D_LP_E_CALLORDER: return "调用顺序错误";
                case Mv3dLpSDK.MV3D_LP_E_PARAMETER: return "参数错误";
                case Mv3dLpSDK.MV3D_LP_E_RESOURCE: return "资源申请失败";
                case Mv3dLpSDK.MV3D_LP_E_NODATA: return "无数据";
                case Mv3dLpSDK.MV3D_LP_E_PRECONDITION: return "前置条件错误";
                case Mv3dLpSDK.MV3D_LP_E_VERSION: return "版本不匹配";
                case Mv3dLpSDK.MV3D_LP_E_NOENOUGH_BUF: return "缓存不足";
                case Mv3dLpSDK.MV3D_LP_E_ABNORMAL_IMAGE: return "图像异常";
                case Mv3dLpSDK.MV3D_LP_E_LOAD_LIBRARY: return "加载动态库失败";
                case Mv3dLpSDK.MV3D_LP_E_ALGORITHM: return "算法处理失败";
                case Mv3dLpSDK.MV3D_LP_E_DEVICE_OFFLINE: return "设备离线";
                case Mv3dLpSDK.MV3D_LP_E_ACCESS_DENIED: return "设备访问被拒绝";
                case Mv3dLpSDK.MV3D_LP_E_OUTOFRANGE: return "参数超出范围";
                case Mv3dLpSDK.MV3D_LP_E_UNKNOW: return "未知错误";
                default: return $"错误码 0x{errorCode:X8}";
            }
        }

        /// <summary>
        /// 触发连接状态变化事件。
        /// </summary>
        /// <param name="isConnected">是否已连接。</param>
        private void RaiseConnectStatus(bool isConnected)
        {
            ConnectStatusEvent?.Invoke(this, isConnected);
        }

        /// <summary>
        /// 触发状态变化事件。
        /// </summary>
        /// <param name="message">状态文本。</param>
        private void RaiseStatus(string message)
        {
            StatusChanged?.Invoke(this, new Camera3DStatusChangedEventArgs(message));
        }

        /// <summary>
        /// 触发错误事件。
        /// </summary>
        /// <param name="message">错误说明。</param>
        /// <param name="errorCode">SDK 错误码。</param>
        protected void RaiseError(string message, int errorCode)
        {
            ErrorOccurred?.Invoke(this, new Camera3DErrorEventArgs($"{message}：{GetErrorText(errorCode)}", errorCode));
        }

        /// <summary>
        /// 触发 3D 帧到达事件。
        /// </summary>
        /// <param name="frame">3D 帧数据。</param>
        protected void RaiseFrameArrived(Camera3DFrameData frame)
        {
            if (frame == null)
                return;

            FrameArrived?.Invoke(this, new Camera3DFrameArrivedEventArgs(frame));
        }

        /// <summary>
        /// SDK 枚举结果，保留打开设备时可能需要的原始向量。
        /// </summary>
        private sealed class SdkDeviceEnumerationResult
        {
            /// <summary>
            /// 托管设备列表。
            /// </summary>
            public List<Camera3DDeviceInfo> Devices { get; set; }

            /// <summary>
            /// SDK 原始设备向量。
            /// </summary>
            public MV3D_LP_DEVICE_INFO_VECTOR SdkDeviceVector { get; set; }
        }
    }
}
