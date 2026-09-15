using Logger;
using MvCamCtrl.NET;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TDJS_Vision.Diagnostics;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Device.Camera
{
    /// <summary>
    /// 使用海康官方MVCamera类直接访问原生SDK的二维相机实现。
    /// </summary>
    public class CameraHik : ICamera, IProductionCameraStartupGraceProvider
    {
        /// <summary>每台相机至少保留的原始帧缓冲数量。</summary>
        private const int RawFrameBufferCountMinimum = 2;

        /// <summary>每台相机最多预分配的原始帧缓冲数量。</summary>
        private const int RawFrameBufferCountMaximum = 8;

        /// <summary>相机原始帧总预算占流程图像内存预算的默认比例。</summary>
        private const double RawFrameMemoryBudgetRatio = 0.25D;

        /// <summary>尚未生成运行资源档案时使用的全方案原始帧保守预算。</summary>
        private const int RawFrameFallbackMemoryBudgetMb = 256;

        /// <summary>GigE相机断线检测心跳超时，单位毫秒。</summary>
        private const long GigEHeartbeatTimeoutMilliseconds = 1000L;

        /// <summary>Windows原生内存复制函数，用于在SDK回调返回前快速接管原始帧。</summary>
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr destination, IntPtr source, uint count);

        /// <summary>SDK全局初始化状态，0表示未初始化，1表示已经初始化。</summary>
        private static int _sdkInitialized;

        /// <summary>保护相机打开、关闭和销毁操作。</summary>
        private readonly object _deviceLock = new object();

        /// <summary>串行化开始、停止、主动取图和销毁，停止等待期间不占用设备锁。</summary>
        private readonly object _cameraLifecycleLock = new object();

        /// <summary>保护回调拥有者集合与原生回调注册状态。</summary>
        private readonly object _imageCallbackLock = new object();

        /// <summary>保护单台相机的预分配转换缓冲区和关闭过程。</summary>
        private readonly object _callbackSyncRoot = new object();

        /// <summary>保护SDK回调原始复制与停止取流后的缓冲释放边界。</summary>
        private readonly object _sdkCallbackLifetimeLock = new object();

        /// <summary>保护线路选择与线路反转组成的原子IO设置。</summary>
        private readonly object _ioLock = new object();

        /// <summary>需要接收相机帧的业务拥有者集合。</summary>
        private readonly HashSet<object> _imageCallbackOwners = new HashSet<object>();

        /// <summary>不加锁读取的有效回调拥有者数量。</summary>
        private int _imageCallbackOwnerCount;

        /// <summary>保护原始帧缓冲池首次创建和释放。</summary>
        private readonly object _rawFramePipelineLock = new object();

        /// <summary>SDK回调可立即租用的预分配原始帧缓冲。</summary>
        private readonly ConcurrentQueue<HikRawFramePacket> _availableRawFrames = new ConcurrentQueue<HikRawFramePacket>();

        /// <summary>等待相机专用线程转换的原始帧。</summary>
        private readonly ConcurrentQueue<HikRawFramePacket> _pendingRawFrames = new ConcurrentQueue<HikRawFramePacket>();

        /// <summary>通知相机专用转换线程存在新帧。</summary>
        private readonly AutoResetEvent _rawFrameAvailable = new AutoResetEvent(false);

        /// <summary>相机专用像素转换和业务分发线程。</summary>
        private Thread _frameConversionWorker;

        /// <summary>请求相机专用转换线程停止的原子标记。</summary>
        private int _frameConversionStopRequested;

        /// <summary>取消当前相机尚在等待的全局图像转换许可。</summary>
        private CancellationTokenSource _frameConversionCancellation;

        /// <summary>当前每块原始帧缓冲容量，单位为字节。</summary>
        private int _rawFrameBufferSize;

        /// <summary>当前相机实际分配的原始帧缓冲数量。</summary>
        private int _rawFrameBufferCount;

        /// <summary>当前相机已经从全方案预算管理器取得的原始帧内存租约。</summary>
        private ICameraRawFrameMemoryLease _rawFrameMemoryLease;

        /// <summary>对象完成永久销毁后置为1，禁止重新创建已释放的同步对象。</summary>
        private int _disposed;

        /// <summary>原生SDK句柄真实打开后置为1；不参与方案序列化，避免把保存时连接状态误当成重启后的真实状态。</summary>
        private int _nativeDeviceOpened;

        /// <summary>原生句柄打开后的网络、采集模式、触发源和回调配置全部完成后置为1。</summary>
        private int _deviceConfigurationCompleted;

        /// <summary>方案加载后需要恢复上次连接时置为1，与原生句柄真实状态相互独立。</summary>
        private int _restoreConnectionRequested;

        /// <summary>当前连接会话的首个生产帧宽限是否已经提交，0表示可预留，1表示已提交。</summary>
        private int _initialProductionFrameGraceReserved;

        /// <summary>串行化首帧宽限预留、提交以及相机连接会话重置。</summary>
        private readonly object _initialProductionFrameGraceLock = new object();

        /// <summary>缓冲暂不可用时累计的帧入口失败数量，详细满载策略后续独立设计。</summary>
        private long _rawFrameIngressFailureCount;

        /// <summary>由转换线程异步报告的最后一个SDK回调入口异常。</summary>
        private Exception _rawFrameIngressException;

        /// <summary>尚未由转换线程逐项报告的原始帧入口故障，保留每帧自己的预览与生产会话归属。</summary>
        private readonly ConcurrentQueue<RawFrameIngressFailure> _rawFrameIngressFailures =
            new ConcurrentQueue<RawFrameIngressFailure>();

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

        /// <summary>获取原生SDK设备当前是否真实打开；设置值仅用于兼容外部连接意图。</summary>
        [JsonIgnore]
        public bool IsOpen
        {
            get => Volatile.Read(ref _nativeDeviceOpened) != 0;
            set => Volatile.Write(ref _restoreConnectionRequested, value ? 1 : 0);
        }

        /// <summary>获取方案反序列化后是否需要恢复上次的相机连接。</summary>
        [JsonIgnore]
        public bool RestoreConnectionRequested => Volatile.Read(ref _restoreConnectionRequested) != 0;

        /// <summary>兼容旧方案中的IsOpen字段，并在保存时记录下次是否需要恢复连接。</summary>
        [JsonProperty("IsOpen")]
        private bool PersistedConnectionRequested
        {
            get => IsOpen || RestoreConnectionRequested;
            set => Volatile.Write(ref _restoreConnectionRequested, value ? 1 : 0);
        }

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

        /// <summary>预留当前连接会话唯一的首个生产帧冷启动宽限。</summary>
        /// <returns>相机已打开且资格尚未提交时返回待提交对象。</returns>
        public IProductionCameraStartupGraceReservation TryReserveInitialProductionFrameGrace()
        {
            Monitor.Enter(_initialProductionFrameGraceLock);
            if (!IsOpen || Volatile.Read(ref _initialProductionFrameGraceReserved) != 0)
            {
                Monitor.Exit(_initialProductionFrameGraceLock);
                return null;
            }
            return new InitialProductionFrameGraceReservation(this);
        }

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
            ThrowIfDisposed();
            ThrowIfCalledFromFrameConversionWorker("创建设备句柄");
            try
            {
                lock (_cameraLifecycleLock)
                {
                    HikNativeDeviceInfo deviceInfo = FindCamera()
                        .FirstOrDefault(item => string.Equals(item.SerialNumber, SN, StringComparison.OrdinalIgnoreCase));
                    if (deviceInfo == null)
                        throw new InvalidOperationException(string.Format("未找到序列号为{0}的相机。", SN));

                    SetDeviceIdentity(deviceInfo);
                    CreateNativeDevice(deviceInfo);
                }
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
            ThrowIfDisposed();
            ThrowIfCalledFromFrameConversionWorker("打开相机");
            lock (_cameraLifecycleLock)
            {
                lock (_deviceLock)
                {
                    if (Volatile.Read(ref _nativeDeviceOpened) != 0 &&
                        Volatile.Read(ref _deviceConfigurationCompleted) != 0)
                    {
                        Volatile.Write(ref _restoreConnectionRequested, 1);
                        return true;
                    }
                    if (_device == null || !_deviceCreated)
                    {
                        HikNativeDeviceInfo deviceInfo = FindCamera()
                            .FirstOrDefault(item => string.Equals(item.SerialNumber, SN, StringComparison.OrdinalIgnoreCase));
                        if (deviceInfo == null)
                            throw new InvalidOperationException(string.Format("相机（{0}）不在线。", UserDefinedName));
                        SetDeviceIdentity(deviceInfo);
                        CreateNativeDeviceCore(deviceInfo);
                    }

                    if (Volatile.Read(ref _nativeDeviceOpened) == 0)
                    {
                        int result = _device.MV_CC_OpenDevice_NET();
                        if (result != MyCamera.MV_OK)
                            throw CreateSdkException(string.Format("打开相机（{0}）", UserDefinedName), result);
                        Volatile.Write(ref _nativeDeviceOpened, 1);
                    }

                    Volatile.Write(ref _restoreConnectionRequested, 1);
                    try
                    {
                        ConfigureGigENetwork();
                        ConfigureGigEHeartbeat();
                        TrySetEnumValue("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                        lock (_initialProductionFrameGraceLock)
                        {
                            lock (_imageCallbackLock)
                            {
                                TriggerSource = GetTriggerSourceCore();
                                RegisterNativeCallbackCore();
                                Volatile.Write(ref _deviceConfigurationCompleted, 1);
                            }
                            // 配置完成标记与新会话资格在同一锁内发布，领取方不可能观察到中间状态。
                            Volatile.Write(ref _initialProductionFrameGraceReserved, 0);
                        }
                    }
                    catch (Exception configurationException)
                    {
                        Volatile.Write(ref _deviceConfigurationCompleted, 0);
                        lock (_imageCallbackLock)
                        {
                            int closeResult = _device.MV_CC_CloseDevice_NET();
                            if (closeResult == MyCamera.MV_OK)
                            {
                                Volatile.Write(ref _nativeDeviceOpened, 0);
                                _callbackRegistered = false;
                                throw;
                            }

                            throw new AggregateException(
                                "相机打开后的配置失败，并且SDK回滚关闭也失败；已保留真实打开状态并标记配置未完成。",
                                configurationException,
                                CreateSdkException("回滚关闭相机", closeResult));
                        }
                    }
                }
            }

            ConnectStatusEvent?.Invoke(this, true);
            return true;
        }

        /// <summary>注册需要相机帧回调的拥有者。</summary>
        /// <param name="owner">回调拥有者。</param>
        public void RegisterImageCallbackOwner(object owner)
        {
            ThrowIfDisposed();
            if (owner == null)
                return;
            lock (_imageCallbackLock)
            {
                bool ownerAdded = _imageCallbackOwners.Add(owner);
                try
                {
                    Volatile.Write(ref _imageCallbackOwnerCount, _imageCallbackOwners.Count);
                    EnsureNativeCallbackRegisteredCore();
                }
                catch
                {
                    if (ownerAdded)
                        _imageCallbackOwners.Remove(owner);
                    Volatile.Write(ref _imageCallbackOwnerCount, _imageCallbackOwners.Count);
                    throw;
                }
            }
        }

        /// <summary>注销相机帧回调拥有者。</summary>
        /// <param name="owner">回调拥有者。</param>
        public void UnregisterImageCallbackOwner(object owner)
        {
            if (owner == null)
                return;
            lock (_imageCallbackLock)
            {
                _imageCallbackOwners.Remove(owner);
                Volatile.Write(ref _imageCallbackOwnerCount, _imageCallbackOwners.Count);
            }
        }

        /// <summary>开始相机取流。</summary>
        public void StartGrabbing()
        {
            ThrowIfDisposed();
            ThrowIfCalledFromFrameConversionWorker("开始相机取流");
            lock (_cameraLifecycleLock)
            {
                int payloadSize;
                lock (_deviceLock)
                {
                    EnsureDeviceOpen();
                    if (_isGrabbing)
                        return;

                    lock (_imageCallbackLock)
                        EnsureNativeCallbackRegisteredCore();
                    EnsureSuccess(_device.MV_CC_SetImageNodeNum_NET(1), "设置相机取流缓存节点");

                    payloadSize = checked((int)GetIntValue("PayloadSize").CurValue);
                }

                PrepareFrameConversionWorkerForStart();
                string productionCameraKey = CameraProductionIdentity.GetStableKey(this);
                CameraTriggerTicketRegistry productionRegistry =
                    Solution.Instance.CameraTriggerTicketRegistry;
                long preparedFaultGeneration =
                    productionRegistry.PrepareCameraStreamReset(productionCameraKey);
                try
                {
                    EnsureRawFrameBuffers(payloadSize);
                    StartFrameConversionWorker();

                    int result;
                    lock (_deviceLock)
                    {
                        Volatile.Write(ref _isGrabbing, true);
                        result = _device.MV_CC_StartGrabbing_NET();
                        if (result != MyCamera.MV_OK)
                            Volatile.Write(ref _isGrabbing, false);
                    }

                    if (result != MyCamera.MV_OK)
                        throw CreateSdkException("开始相机取流", result);
                }
                catch
                {
                    lock (_deviceLock)
                    {
                        if (Volatile.Read(ref _isGrabbing) && _device != null)
                        {
                            try
                            {
                                _device.MV_CC_StopGrabbing_NET();
                            }
                            catch
                            {
                            }
                        }
                        Volatile.Write(ref _isGrabbing, false);
                    }
                    StopFrameConversionWorker();
                    throw;
                }

                try
                {
                    // 只有真实停流、回调排空并成功重新取流后，才能解除迟到生产帧门禁。
                    productionRegistry.ConfirmCameraStreamReset(
                        productionCameraKey,
                        preparedFaultGeneration);
                }
                catch (Exception confirmationException)
                {
                    try
                    {
                        StopGrabbing();
                    }
                    catch (Exception stopException)
                    {
                        throw new AggregateException(
                            "相机重新取流后的生产帧清场确认失败，并且回滚停流失败。",
                            confirmationException,
                            stopException);
                    }
                    throw;
                }
            }
        }

        /// <summary>停止相机取流。</summary>
        public void StopGrabbing()
        {
            ThrowIfDisposed();
            ThrowIfCalledFromFrameConversionWorker("停止相机取流");
            lock (_cameraLifecycleLock)
            {
                bool sdkWasGrabbing;
                lock (_deviceLock)
                {
                    if (_device == null)
                        return;

                    sdkWasGrabbing = _isGrabbing;
                    if (sdkWasGrabbing)
                    {
                        int result = _device.MV_CC_StopGrabbing_NET();
                        if (result != MyCamera.MV_OK)
                            throw CreateSdkException("停止相机取流", result);
                        Volatile.Write(ref _isGrabbing, false);
                    }
                }

                if (!sdkWasGrabbing && !HasFrameConversionWorker())
                    return;
                lock (_sdkCallbackLifetimeLock)
                {
                }
                StopFrameConversionWorker();
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

        /// <summary>判断当前SDK句柄是否仍然连接到物理相机。</summary>
        /// <returns>SDK确认设备在线时返回true。</returns>
        public bool IsDeviceConnected()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return false;

            lock (_deviceLock)
            {
                if (_device == null ||
                    !_deviceCreated ||
                    Volatile.Read(ref _nativeDeviceOpened) == 0 ||
                    Volatile.Read(ref _deviceConfigurationCompleted) == 0)
                {
                    return false;
                }

                try
                {
                    return _device.MV_CC_IsDeviceConnected_NET();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>释放异常SDK句柄并按序列号重新枚举打开相机。</summary>
        /// <returns>重新打开成功时返回true。</returns>
        public bool TryReconnect()
        {
            if (Volatile.Read(ref _disposed) != 0 || string.IsNullOrWhiteSpace(SN))
                return false;

            bool hadConnection = false;
            try
            {
                ThrowIfCalledFromFrameConversionWorker("自动重连相机");
                lock (_cameraLifecycleLock)
                    hadConnection = ReleaseNativeConnectionForReconnectCore();

                if (hadConnection)
                    ConnectStatusEvent?.Invoke(this, false);

                bool opened = Open();
                if (opened)
                {
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        string.Format("相机【{0}】自动重连成功，SN={1}，IP={2}。", UserDefinedName, SN, IP),
                        true);
                }
                return opened;
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    string.Format("相机【{0}】自动重连失败：{1}", UserDefinedName, exception.Message),
                    true);
                return false;
            }
        }

        /// <summary>主动获取一帧图像并转换为独立Bitmap。</summary>
        /// <returns>成功返回Bitmap，失败返回null。</returns>
        public Bitmap GetOneFrameImage()
        {
            ThrowIfDisposed();
            ThrowIfCalledFromFrameConversionWorker("主动取图");
            lock (_cameraLifecycleLock)
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
            EnsureDeviceOpen();
            return GetTriggerSourceCore();
        }

        /// <summary>在Open配置事务内部读取触发模式和触发源，不提前放开配置完成门禁。</summary>
        /// <returns>当前触发源。</returns>
        private TriggerSource GetTriggerSourceCore()
        {
            CameraEnumEntry triggerModeEntry = GetEnumValueCore("TriggerMode").CurEnumEntry;
            TriggerModel = triggerModeEntry != null &&
                (string.Equals(triggerModeEntry.Symbolic, "On", StringComparison.OrdinalIgnoreCase) || triggerModeEntry.Value == 1)
                    ? TriggerModel.On
                    : TriggerModel.Off;
            if (TriggerModel == TriggerModel.Off)
                return TriggerSource.Auto;

            TriggerSource = MapTriggerSource(GetEnumValueCore("TriggerSource").CurEnumEntry);
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
            using (ISoftwareTriggerCommandLease triggerLease =
                Solution.Instance.CameraTriggerTicketRegistry.EnterSoftwareTriggerCommand(
                    CameraProductionIdentity.GetStableKey(this)))
            {
                triggerLease.MarkCommandStarted();
                EnsureSuccess(_device.MV_CC_SetCommandValue_NET("TriggerSoftware"), "执行软件触发");
                triggerLease.MarkCommandSucceeded();
            }
        }

        /// <summary>按当前宽高和最坏的BGR源图加灰度缓存估算生产帧长期持有字节。</summary>
        public long GetProductionFrameMemoryEstimateBytes()
        {
            EnsureDeviceOpen();
            long width = GetIntValue("Width").CurValue;
            long height = GetIntValue("Height").CurValue;
            if (width <= 0L || height <= 0L)
                throw new InvalidOperationException("相机宽高无效，无法在触发前计算生产帧内存预算。 ");
            return checked(width * height * 4L);
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
            if (Volatile.Read(ref _disposed) != 0)
                return;
            Volatile.Write(ref _restoreConnectionRequested, 0);
            ThrowIfCalledFromFrameConversionWorker("关闭相机");
            bool notifyDisconnected = false;
            lock (_cameraLifecycleLock)
            {
                lock (_deviceLock)
                {
                    if (_device == null)
                    {
                        Volatile.Write(ref _nativeDeviceOpened, 0);
                        Volatile.Write(ref _deviceConfigurationCompleted, 0);
                        _callbackRegistered = false;
                    }
                    else if (_isGrabbing)
                    {
                        int stopResult = _device.MV_CC_StopGrabbing_NET();
                        if (stopResult != MyCamera.MV_OK)
                            throw CreateSdkException("停止相机取流", stopResult);
                        Volatile.Write(ref _isGrabbing, false);
                    }
                }

                lock (_sdkCallbackLifetimeLock)
                {
                }
                StopFrameConversionWorker();
                lock (_callbackSyncRoot)
                    ReleaseConversionBuffer();

                lock (_deviceLock)
                {
                    lock (_imageCallbackLock)
                    {
                        if (_device != null && Volatile.Read(ref _nativeDeviceOpened) != 0)
                        {
                            int closeResult = _device.MV_CC_CloseDevice_NET();
                            if (closeResult != MyCamera.MV_OK)
                                throw CreateSdkException("关闭相机", closeResult);
                            notifyDisconnected = true;
                        }

                        Volatile.Write(ref _nativeDeviceOpened, 0);
                        Volatile.Write(ref _deviceConfigurationCompleted, 0);
                        _callbackRegistered = false;
                    }
                }
                // 只有原生关闭、回调排空和转换资源释放全部成功后，才结束当前连接会话。
                lock (_initialProductionFrameGraceLock)
                    Volatile.Write(ref _initialProductionFrameGraceReserved, 0);
            }

            if (notifyDisconnected)
                ConnectStatusEvent?.Invoke(this, false);
        }

        /// <summary>为自动重连释放当前SDK连接，释放过程吞掉SDK断线后的二次错误并重置托管状态。</summary>
        /// <returns>释放前存在SDK句柄或打开状态时返回true。</returns>
        private bool ReleaseNativeConnectionForReconnectCore()
        {
            bool hadConnection;
            lock (_deviceLock)
            {
                hadConnection = _device != null || _deviceCreated || Volatile.Read(ref _nativeDeviceOpened) != 0;
                if (_device != null && _isGrabbing)
                {
                    try
                    {
                        int stopResult = _device.MV_CC_StopGrabbing_NET();
                        if (stopResult != MyCamera.MV_OK)
                            LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("自动重连前停止相机取流", stopResult), true);
                    }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, string.Format("相机【{0}】自动重连前停止取流异常：{1}", UserDefinedName, exception.Message), true);
                    }
                    Volatile.Write(ref _isGrabbing, false);
                }
            }

            lock (_sdkCallbackLifetimeLock)
            {
            }

            try
            {
                StopFrameConversionWorker();
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Warn, string.Format("相机【{0}】自动重连前停止转换线程异常：{1}", UserDefinedName, exception.Message), true);
                // 未排空旧帧前禁止打开新连接，下一轮守护继续尝试恢复。
                throw;
            }

            lock (_callbackSyncRoot)
                ReleaseConversionBuffer();

            lock (_deviceLock)
            {
                lock (_imageCallbackLock)
                {
                    if (_device != null && Volatile.Read(ref _nativeDeviceOpened) != 0)
                    {
                        try
                        {
                            int closeResult = _device.MV_CC_CloseDevice_NET();
                            if (closeResult != MyCamera.MV_OK)
                                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("自动重连前关闭相机", closeResult), true);
                        }
                        catch (Exception exception)
                        {
                            LogHelper.AddLog(MsgLevel.Warn, string.Format("相机【{0}】自动重连前关闭设备异常：{1}", UserDefinedName, exception.Message), true);
                        }
                    }

                    if (_device != null && _deviceCreated)
                    {
                        try
                        {
                            int destroyResult = _device.MV_CC_DestroyDevice_NET();
                            if (destroyResult != MyCamera.MV_OK)
                                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("自动重连前销毁相机句柄", destroyResult), true);
                        }
                        catch (Exception exception)
                        {
                            LogHelper.AddLog(MsgLevel.Warn, string.Format("相机【{0}】自动重连前销毁句柄异常：{1}", UserDefinedName, exception.Message), true);
                        }
                    }

                    _deviceCreated = false;
                    _callbackRegistered = false;
                    _nativeImageCallback = null;
                    _device = null;
                    _deviceInfo = null;
                    Volatile.Write(ref _nativeDeviceOpened, 0);
                    Volatile.Write(ref _deviceConfigurationCompleted, 0);
                    Volatile.Write(ref _isGrabbing, false);
                }
            }

            lock (_initialProductionFrameGraceLock)
                Volatile.Write(ref _initialProductionFrameGraceReserved, 0);

            return hadConnection;
        }

        /// <summary>关闭相机并销毁原生设备句柄。</summary>
        public void Dispose()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;
            Volatile.Write(ref _restoreConnectionRequested, 0);
            ThrowIfCalledFromFrameConversionWorker("销毁相机");
            lock (_cameraLifecycleLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;
                Close();
                lock (_deviceLock)
                {
                    if (_device != null && _deviceCreated)
                    {
                        int result = _device.MV_CC_DestroyDevice_NET();
                        if (result != MyCamera.MV_OK)
                            throw CreateSdkException("销毁相机句柄", result);
                    }

                    _deviceCreated = false;
                    Volatile.Write(ref _nativeDeviceOpened, 0);
                    Volatile.Write(ref _deviceConfigurationCompleted, 0);
                    Volatile.Write(ref _restoreConnectionRequested, 0);
                    _callbackRegistered = false;
                    _nativeImageCallback = null;
                    _device = null;
                    _deviceInfo = null;
                }
                Volatile.Write(ref _disposed, 1);
            }
            _rawFrameAvailable.Dispose();
        }

        /// <summary>SDK图像回调：只复制原始帧并投递到相机专用转换线程。</summary>
        /// <param name="sourceData">SDK帧数据地址。</param>
        /// <param name="frameInfo">SDK帧信息。</param>
        /// <param name="userData">用户数据，本实现未使用。</param>
        private void GetImageCallBack(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, IntPtr userData)
        {
            if (LogHelper.CanRecord(MsgLevel.Debug))
            {
                LogHelper.TryAddDiagnosticLog(
                    string.Format(
                        "相机进入回调-{0}-{1:yyyy-MM-dd HH:mm:ss.fff}；SDKFrameNum={2}；尺寸={3}x{4}；FrameLen={5}；PixelType={6}；sourceData为空={7}；正在取流={8}；回调拥有者={9}；线程={10}",
                        DevName,
                        DateTime.Now,
                        frameInfo.nFrameNum,
                        frameInfo.nWidth,
                        frameInfo.nHeight,
                        frameInfo.nFrameLen,
                        frameInfo.enPixelType,
                        sourceData == IntPtr.Zero ? "是" : "否",
                        Volatile.Read(ref _isGrabbing) ? "是" : "否",
                        Volatile.Read(ref _imageCallbackOwnerCount),
                        Thread.CurrentThread.ManagedThreadId),
                    true);
            }
            long callbackStartedTimestamp = Stopwatch.GetTimestamp();
            lock (_sdkCallbackLifetimeLock)
                EnqueueRawFrameCore(sourceData, ref frameInfo, callbackStartedTimestamp);
        }

        /// <summary>在SDK回调生命周期锁内复制并投递一帧原始数据。</summary>
        /// <param name="sourceData">SDK帧数据地址。</param>
        /// <param name="frameInfo">SDK帧信息。</param>
        /// <param name="callbackStartedTimestamp">进入SDK托管回调后、取得任何锁之前冻结的单调时钟。</param>
        private void EnqueueRawFrameCore(
            IntPtr sourceData,
            ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo,
            long callbackStartedTimestamp)
        {
            if (!Volatile.Read(ref _isGrabbing) || sourceData == IntPtr.Zero)
                return;
            CameraTriggerTicketRegistry frameRoutingRegistry;
            bool hasActiveProductionRegistry;
            Solution.Instance.GetCameraFrameRoutingSnapshot(
                out frameRoutingRegistry,
                out hasActiveProductionRegistry);
            if (Volatile.Read(ref _imageCallbackOwnerCount) == 0)
            {
                string cameraKey = CameraProductionIdentity.GetStableKey(this);
                if (!hasActiveProductionRegistry ||
                    !frameRoutingRegistry.ShouldCaptureBufferedHardwareFrame(cameraKey))
                    return;
            }

            CameraTriggerTicketRegistry productionRegistry =
                hasActiveProductionRegistry ? frameRoutingRegistry : null;

            long frameId = Interlocked.Increment(ref _callbackFrameSequence);
            bool diagnosticEnabled = PerformanceSpikeDiagnostics.IsDiagnosticLogEnabled(MsgLevel.Debug);
            HikRawFramePacket packet = null;
            bool packetPublished = false;

            try
            {
                int frameLength = checked((int)frameInfo.nFrameLen);
                EnsureRawFrameBuffers(frameLength);
                if (!_availableRawFrames.TryDequeue(out packet))
                {
                    RecordRawFrameIngressFailure(
                        frameId,
                        callbackStartedTimestamp,
                        null,
                        frameRoutingRegistry,
                        productionRegistry);
                    Interlocked.Increment(ref _rawFrameIngressFailureCount);
                    _rawFrameAvailable.Set();
                    return;
                }

                long copyStartedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                CopyMemory(packet.Buffer, sourceData, frameInfo.nFrameLen);
                long copyCompletedTimestamp = diagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                packet.FrameInfo = frameInfo;
                packet.FrameId = frameId;
                packet.CallbackStartedTimestamp = callbackStartedTimestamp;
                packet.CopyStartedTimestamp = copyStartedTimestamp;
                packet.CopyCompletedTimestamp = copyCompletedTimestamp;
                packet.CallbackThreadId = Thread.CurrentThread.ManagedThreadId;
                packet.DiagnosticEnabled = diagnosticEnabled;
                packet.FrameRoutingRegistry = frameRoutingRegistry;
                packet.ProductionRegistry = productionRegistry;
                _pendingRawFrames.Enqueue(packet);
                packetPublished = true;
                _rawFrameAvailable.Set();
                packet.MarkCallbackCompleted(Stopwatch.GetTimestamp());
                packet = null;
            }
            catch (Exception exception)
            {
                if (packet != null && packetPublished)
                {
                    packet.MarkCallbackCompleted(Stopwatch.GetTimestamp());
                }
                else if (packet != null)
                {
                    packet.Reset();
                    _availableRawFrames.Enqueue(packet);
                }
                RecordRawFrameIngressFailure(
                    frameId,
                    callbackStartedTimestamp,
                    exception,
                    frameRoutingRegistry,
                    productionRegistry);
                Interlocked.Exchange(ref _rawFrameIngressException, exception);
                Interlocked.Increment(ref _rawFrameIngressFailureCount);
                _rawFrameAvailable.Set();
            }
        }

        /// <summary>SDK回调只把入口故障写入无锁队列，不执行票据锁、日志或停机。</summary>
        private void RecordRawFrameIngressFailure(
            long frameId,
            long callbackStartedTimestamp,
            Exception exception,
            CameraTriggerTicketRegistry frameRoutingRegistry,
            CameraTriggerTicketRegistry productionRegistry)
        {
            RawFrameIngressFailure failure = new RawFrameIngressFailure(
                frameId,
                callbackStartedTimestamp,
                exception,
                frameRoutingRegistry,
                productionRegistry);
            _rawFrameIngressFailures.Enqueue(failure);
        }

        /// <summary>确保原始帧缓冲池已经按当前有效载荷完成一次性分配。</summary>
        /// <param name="requiredSize">当前原始帧有效字节数。</param>
        private void EnsureRawFrameBuffers(int requiredSize)
        {
            if (requiredSize <= 0)
                throw new InvalidOperationException("相机原始帧长度无效。");
            if (Volatile.Read(ref _rawFrameBufferSize) >= requiredSize)
                return;

            lock (_rawFramePipelineLock)
            {
                if (_rawFrameBufferSize >= requiredSize)
                    return;
                if (_rawFrameBufferSize != 0)
                    throw new InvalidOperationException("相机取流期间原始帧尺寸发生变化，请停止取流后重新开始。");

                int desiredRawFrameBufferCount = CalculateRawFrameBufferCount(requiredSize);
                long totalRawFrameBudgetBytes = GetRawFrameMemoryBudgetBytes();
                ICameraRawFrameMemoryLease memoryLease =
                    Solution.Instance.CameraRawFrameMemoryBudgetManager.Reserve(
                        DevName,
                        totalRawFrameBudgetBytes,
                        requiredSize,
                        desiredRawFrameBufferCount,
                        RawFrameBufferCountMinimum);
                int rawFrameBufferCount = memoryLease.BufferCount;
                List<HikRawFramePacket> allocatedPackets = new List<HikRawFramePacket>(rawFrameBufferCount);
                try
                {
                    for (int index = 0; index < rawFrameBufferCount; index++)
                        allocatedPackets.Add(new HikRawFramePacket(requiredSize));
                    foreach (HikRawFramePacket packet in allocatedPackets)
                        _availableRawFrames.Enqueue(packet);
                }
                catch
                {
                    foreach (HikRawFramePacket packet in allocatedPackets)
                        packet.Dispose();
                    memoryLease.Dispose();
                    throw;
                }
                _rawFrameMemoryLease = memoryLease;
                Volatile.Write(ref _rawFrameBufferCount, rawFrameBufferCount);
                Volatile.Write(ref _rawFrameBufferSize, requiredSize);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format(
                        "【相机原始帧缓冲】相机={0}；数量={1}；单块={2}字节；合计={3:F1}MB；配置相机={4}；预算比例={5:P0}；全局已预留={6:F1}MB；全局预算={7:F1}MB",
                        DevName,
                        rawFrameBufferCount,
                        requiredSize,
                        (long)rawFrameBufferCount * requiredSize / 1024D / 1024D,
                        GetConfiguredCameraCount(),
                        RawFrameMemoryBudgetRatio,
                        Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentReservedBytes / 1024D / 1024D,
                        Solution.Instance.CameraRawFrameMemoryBudgetManager.CurrentBudgetBytes / 1024D / 1024D),
                    true);
            }
        }

        /// <summary>按机器图像预算、相机数量和当前Payload计算单相机缓冲数量。</summary>
        /// <param name="requiredSize">单块原始帧字节数。</param>
        /// <returns>当前相机应预分配的缓冲数量。</returns>
        private int CalculateRawFrameBufferCount(int requiredSize)
        {
            int cameraCount = GetConfiguredCameraCount();
            long totalBudgetBytes = GetRawFrameMemoryBudgetBytes();
            long perCameraBudgetBytes = totalBudgetBytes / cameraCount;
            long minimumRequiredBytes = (long)requiredSize * RawFrameBufferCountMinimum;
            if (perCameraBudgetBytes < minimumRequiredBytes)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "相机{0}原始帧缓冲预算不足：配置相机={1}，单帧={2:F1}MB，每相机预算={3:F1}MB，至少需要={4:F1}MB。请提高流程图像内存预算或减少同时启用的相机。",
                        DevName,
                        cameraCount,
                        requiredSize / 1024D / 1024D,
                        perCameraBudgetBytes / 1024D / 1024D,
                        minimumRequiredBytes / 1024D / 1024D));
            }

            long budgetCount = perCameraBudgetBytes / requiredSize;
            return (int)Math.Max(
                RawFrameBufferCountMinimum,
                Math.Min(RawFrameBufferCountMaximum, budgetCount));
        }

        /// <summary>读取当前全方案允许用于相机原始帧缓冲的总字节数。</summary>
        /// <returns>运行资源档案预算的25%，无档案时使用256MB保守预算。</returns>
        private static long GetRawFrameMemoryBudgetBytes()
        {
            AutomaticResourceProfile profile = Solution.Instance.CurrentResourceProfile;
            int flowImageBudgetMb = profile?.FlowImageMemoryBudgetMb ?? 0;
            if (flowImageBudgetMb <= 0)
                return RawFrameFallbackMemoryBudgetMb * 1024L * 1024L;
            return (long)Math.Floor(
                flowImageBudgetMb * 1024D * 1024D * RawFrameMemoryBudgetRatio);
        }

        /// <summary>读取资源档案中的二维与三维相机总数，缺少档案时使用当前设备列表。</summary>
        /// <returns>至少为1的配置相机数量。</returns>
        private static int GetConfiguredCameraCount()
        {
            int profileCameraCount = Solution.Instance.CurrentResourceProfile?.Workload?.CameraCount ?? 0;
            if (profileCameraCount > 0)
                return profileCameraCount;
            return Math.Max(1, Solution.Instance.CameraDevices.Count + Solution.Instance.Camera3DDevices.Count);
        }

        /// <summary>开始本轮取流前清理已经退出的旧转换线程，活动旧线程一律拒绝重入。</summary>
        private void PrepareFrameConversionWorkerForStart()
        {
            lock (_rawFramePipelineLock)
            {
                if (_frameConversionWorker == null)
                    return;
                if (_frameConversionWorker.IsAlive)
                {
                    throw new InvalidOperationException("上一次相机转换线程尚未退出，禁止重新开始取流。");
                }

                _frameConversionWorker = null;
                _frameConversionCancellation?.Dispose();
                _frameConversionCancellation = null;
                ReleaseRawFrameBuffersCore();
            }
        }

        /// <summary>启动当前相机独占的顺序消费线程；任一启动异常都会回滚CTS、缓冲和内存租约。</summary>
        private void StartFrameConversionWorker()
        {
            lock (_rawFramePipelineLock)
            {
                if (_frameConversionWorker != null)
                    throw new InvalidOperationException("相机转换线程状态未清理，禁止重复启动。");

                Volatile.Write(ref _frameConversionStopRequested, 0);
                CancellationTokenSource cancellation = new CancellationTokenSource();
                Thread worker = null;
                try
                {
                    worker = new Thread(FrameConversionWorkerLoop)
                    {
                        IsBackground = true,
                        Name = string.Format("HikFrameConvert-{0}", UserDefinedName ?? SN ?? "Camera"),
                        Priority = ThreadPriority.Normal
                    };
                    _frameConversionCancellation = cancellation;
                    _frameConversionWorker = worker;
                    worker.Start();
                }
                catch
                {
                    _frameConversionWorker = null;
                    _frameConversionCancellation = null;
                    cancellation.Cancel();
                    cancellation.Dispose();
                    ReleaseRawFrameBuffersCore();
                    throw;
                }
            }
        }

        /// <summary>停止相机专用转换线程并释放全部原始帧缓冲。</summary>
        private void StopFrameConversionWorker()
        {
            Thread worker;
            CancellationTokenSource cancellation;
            lock (_rawFramePipelineLock)
            {
                worker = _frameConversionWorker;
                cancellation = _frameConversionCancellation;
                if (worker == null)
                {
                    _frameConversionCancellation = null;
                    cancellation?.Cancel();
                    cancellation?.Dispose();
                    ReleaseRawFrameBuffersCore();
                    return;
                }
                Volatile.Write(ref _frameConversionStopRequested, 1);
                cancellation?.Cancel();
                _rawFrameAvailable.Set();
            }

            if (worker == Thread.CurrentThread)
                throw new InvalidOperationException(
                    string.Format("不能在相机{0}的帧回调线程中同步停止转换线程，请从流程控制线程执行停止或关闭。", DevName));

            if (!worker.Join(5000))
            {
                throw new InvalidOperationException(
                    string.Format("相机{0}转换线程在5秒内未停止，已禁止释放帧缓冲和关闭设备句柄。", DevName));
            }

            lock (_rawFramePipelineLock)
            {
                _frameConversionWorker = null;
                _frameConversionCancellation = null;
                ReleaseRawFrameBuffersCore();
            }
            cancellation?.Dispose();
        }

        /// <summary>持续按入队顺序转换原始帧并在回调线程之外完成业务分发。</summary>
        private void FrameConversionWorkerLoop()
        {
            CancellationToken cancellationToken;
            lock (_rawFramePipelineLock)
                cancellationToken = _frameConversionCancellation?.Token ?? CancellationToken.None;

            while (Volatile.Read(ref _frameConversionStopRequested) == 0 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    HikRawFramePacket packet;
                    if (!_pendingRawFrames.TryDequeue(out packet))
                    {
                        ReportRawFrameIngressFailures();
                        _rawFrameAvailable.WaitOne();
                        continue;
                    }

                    try
                    {
                        // 原始入口一旦丢帧，先失败生产票据再处理队列中余帧，避免满载时迟迟等不到队列清空。
                        ReportRawFrameIngressFailures();
                        packet.WaitUntilCallbackCompleted();
                        ProcessRawFrame(packet, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Exception,
                            string.Format("相机{0}转换线程处理FrameId={1}时发生未处理异常：{2}", DevName, packet.FrameId, exception.Message),
                            true);
                    }
                    finally
                    {
                        packet.Reset();
                        _availableRawFrames.Enqueue(packet);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        string.Format("相机{0}转换线程循环异常：{1}", DevName, exception.Message),
                        true);
                    Thread.Sleep(10);
                }
            }
            try
            {
                ReportRawFrameIngressFailures();
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, string.Format("相机{0}停止转换线程时报告诊断失败：{1}", DevName, exception.Message), true);
            }
        }

        /// <summary>禁止订阅者在当前相机转换线程中同步停止或销毁自身，避免线程自等待。</summary>
        /// <param name="operationName">准备执行的生命周期操作名称。</param>
        private void ThrowIfCalledFromFrameConversionWorker(string operationName)
        {
            Thread worker;
            lock (_rawFramePipelineLock)
                worker = _frameConversionWorker;
            if (worker == Thread.CurrentThread)
                throw new InvalidOperationException(
                    string.Format("不能在相机{0}的帧回调线程中执行{1}，请切换到流程控制线程。", DevName, operationName));
        }

        /// <summary>检查相机是否仍持有待停止或待清理的转换线程。</summary>
        /// <returns>存在转换线程时返回true。</returns>
        private bool HasFrameConversionWorker()
        {
            lock (_rawFramePipelineLock)
                return _frameConversionWorker != null;
        }

        /// <summary>在本相机顺序消费线程中独立转换并分发Mat，不等待其他相机或普通CPU任务的许可。</summary>
        /// <param name="packet">已经脱离SDK生命周期的原始帧。</param>
        /// <param name="cancellationToken">停止相机时跳过尚未转换的帧，并释放转换期间取消的Mat。</param>
        private void ProcessRawFrame(HikRawFramePacket packet, CancellationToken cancellationToken)
        {
            MyCamera.MV_FRAME_OUT_INFO_EX frameInfo = packet.FrameInfo;
            long conversionWorkerDequeuedTimestamp = packet.DiagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
            long callbackElapsedMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                packet.CallbackStartedTimestamp,
                packet.CallbackCompletedTimestamp);
            long rawCopyMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                packet.CopyStartedTimestamp,
                packet.CopyCompletedTimestamp);
            long cameraQueueMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                packet.CallbackCompletedTimestamp,
                conversionWorkerDequeuedTimestamp);

            Mat image = null;
            // 保留旧日志字段供现场同比；独立相机通道不再申请共享CPU许可。
            const double cpuPermitWaitMilliseconds = 0D;
            long conversionStartedTimestamp = 0L;
            long conversionCompletedTimestamp = 0L;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                conversionStartedTimestamp = packet.DiagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                image = ConvertFrameToIndependentMat(packet.Buffer, ref frameInfo, packet.FrameId, packet.DiagnosticEnabled, cancellationToken);
                conversionCompletedTimestamp = packet.DiagnosticEnabled ? Stopwatch.GetTimestamp() : 0L;
                // SDK转换不能强制中断，返回后检查停止状态，禁止再发布已经取消的图像。
                cancellationToken.ThrowIfCancellationRequested();

                long totalConversionWaitMilliseconds = CameraFrameTraceInfo.GetElapsedMilliseconds(
                    packet.CallbackCompletedTimestamp,
                    conversionStartedTimestamp);
                long cpuPermitWaitThresholdMilliseconds = (long)Math.Ceiling(cpuPermitWaitMilliseconds);

                CameraFrameTraceInfo frameTrace = packet.DiagnosticEnabled
                    ? new CameraFrameTraceInfo(
                        DevName,
                        packet.FrameId,
                        packet.CallbackStartedTimestamp,
                        conversionCompletedTimestamp,
                        frameInfo.nWidth,
                        frameInfo.nHeight,
                        frameInfo.enPixelType.ToString())
                    : null;
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format("【完整耗时-相机帧】相机={0}；FrameId={1}；SDKFrameNum={2}；阶段=SDK回调开始；线程={3}", DevName, packet.FrameId, frameInfo.nFrameNum, packet.CallbackThreadId),
                    true);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format("【完整耗时-相机帧】相机={0}；FrameId={1}；SDKFrameNum={2}；阶段=SDK回调结束；回调总耗时={3}ms；原始复制={4}ms；线程={5}", DevName, packet.FrameId, frameInfo.nFrameNum, callbackElapsedMilliseconds, rawCopyMilliseconds, packet.CallbackThreadId),
                    true);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format("【完整耗时-相机帧】相机={0}；FrameId={1}；SDKFrameNum={2}；阶段=转换线程开始；排队耗时={3}ms；CPU许可等待={4:F3}ms；转换总等待={5}ms；线程={6}；转换通道=每相机独立；共享CPU许可=不参与", DevName, packet.FrameId, frameInfo.nFrameNum, cameraQueueMilliseconds, cpuPermitWaitMilliseconds, totalConversionWaitMilliseconds, Thread.CurrentThread.ManagedThreadId),
                    true);
                PerformanceSpikeDiagnostics.LogSlowIfEnabled(
                    MsgLevel.Debug,
                    PerformanceSpikeDiagnostics.FrameConvertSlowMs,
                    () => string.Format("【慢诊断-相机回调】相机={0}；FrameId={1}；回调总耗时={2}ms；原始复制={3}ms；相机排队={4}ms；CPU许可等待={5:F3}ms；转换总等待={6}ms；{7}", DevName, packet.FrameId, callbackElapsedMilliseconds, rawCopyMilliseconds, cameraQueueMilliseconds, cpuPermitWaitMilliseconds, totalConversionWaitMilliseconds, PerformanceSpikeDiagnostics.GetRuntimeText()),
                    true,
                    callbackElapsedMilliseconds,
                    rawCopyMilliseconds,
                    cameraQueueMilliseconds,
                    cpuPermitWaitThresholdMilliseconds,
                    totalConversionWaitMilliseconds);
                PerformanceSpikeDiagnostics.LogIfEnabled(
                    MsgLevel.Debug,
                    () => string.Format(
                        "【完整耗时-相机帧】相机={0}；FrameId={1}；SDKFrameNum={2}；阶段=Mat转换完成；回调到转换={3}ms；尺寸={4}x{5}；PixelType={6}",
                        DevName,
                        packet.FrameId,
                        frameInfo.nFrameNum,
                        frameTrace?.CallbackToConversionMilliseconds ?? 0,
                        frameInfo.nWidth,
                        frameInfo.nHeight,
                        frameInfo.enPixelType),
                    true);
                string cameraKey = CameraProductionIdentity.GetStableKey(this);
                CameraTriggerTicketRegistry frameRoutingRegistry = packet.FrameRoutingRegistry;
                if (frameRoutingRegistry != null &&
                    frameRoutingRegistry.TryRouteFrame(
                    cameraKey,
                    packet.FrameId,
                    frameInfo.nFrameNum,
                    packet.CallbackStartedTimestamp,
                    image,
                    frameTrace))
                {
                    // 返回true表示注册表已经接管或安全释放Mat，转换线程不得再次释放或分发。
                    image = null;
                }
                else
                {
                    RaiseMatImageReceived(image, frameTrace);
                    image = null;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                image?.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                image?.Dispose();
                bool previewFailureHandled = packet.FrameRoutingRegistry != null &&
                    packet.FrameRoutingRegistry.TryAcknowledgePreviewFrameFailure(
                        CameraProductionIdentity.GetStableKey(this));
                if (!previewFailureHandled)
                {
                    TryFailProductionFrame(
                        packet.FrameId,
                        packet.CallbackStartedTimestamp,
                        exception,
                        packet.ProductionRegistry);
                }
                LogHelper.AddLog(MsgLevel.Exception, string.Format("相机{0}后台转换Mat失败：FrameId={1}；{2}", DevName, packet.FrameId, exception.Message), true);
            }
        }

        /// <summary>在相机转换线程中失败当前相机的生产票据；没有生产票据时保持预览兼容行为。</summary>
        /// <param name="exception">导致当前生产帧无法完成的异常。</param>
        private void TryFailProductionFrame(
            long frameId,
            long callbackStartedTimestamp,
            Exception exception,
            CameraTriggerTicketRegistry productionRegistry)
        {
            if (productionRegistry == null)
                return;
            try
            {
                productionRegistry.TryFailFrame(
                    CameraProductionIdentity.GetStableKey(this),
                    frameId,
                    callbackStartedTimestamp,
                    exception ?? new InvalidOperationException("相机生产帧转换失败。"));
            }
            catch (ObjectDisposedException)
            {
                // 方案重置已经关闭注册表时，运行入口与相机生命周期层负责停止，不重复报告。
            }
            catch (Exception reportException)
            {
                LogHelper.AddLog(
                    MsgLevel.Exception,
                    string.Format("相机{0}报告生产帧失败时发生异常：{1}", DevName, reportException.Message),
                    true);
            }
        }

        /// <summary>由转换线程集中报告回调入口未取得缓冲的次数。</summary>
        private void ReportRawFrameIngressFailures()
        {
            long failureCount = Interlocked.Exchange(ref _rawFrameIngressFailureCount, 0L);
            Exception exception = Interlocked.Exchange(ref _rawFrameIngressException, null);
            if (failureCount <= 0 && exception == null && _rawFrameIngressFailures.IsEmpty)
                return;

            InvalidOperationException productionFailure = new InvalidOperationException(
                string.Format(
                    "相机{0}有{1}帧未完成原始帧入口交接；最后异常={2}。生产帧身份已经不可信，必须停流排空后重启。",
                    DevName,
                    failureCount,
                    exception?.Message ?? "无"),
                exception);
            RawFrameIngressFailure ingressFailure;
            while (_rawFrameIngressFailures.TryDequeue(out ingressFailure))
            {
                bool previewFailureHandled = ingressFailure.FrameRoutingRegistry != null &&
                    ingressFailure.FrameRoutingRegistry.TryAcknowledgePreviewFrameFailure(
                        CameraProductionIdentity.GetStableKey(this));
                if (!previewFailureHandled)
                {
                    TryFailProductionFrame(
                        ingressFailure.FrameId,
                        ingressFailure.CallbackStartedTimestamp,
                        productionFailure,
                        ingressFailure.ProductionRegistry);
                }
            }

            LogHelper.AddLog(
                MsgLevel.Exception,
                string.Format(
                    "相机{0}有{1}帧未完成原始帧入口交接；最后异常={2}；生产票据存在时已故障停机，预览模式保留最新可交付帧。",
                    DevName,
                    failureCount,
                    exception?.Message ?? "无"),
                true);
        }

        /// <summary>释放待转换队列和可用池中的全部非托管原始帧缓冲。</summary>
        private void ReleaseRawFrameBuffersCore()
        {
            HikRawFramePacket packet;
            while (_pendingRawFrames.TryDequeue(out packet))
                packet.Dispose();
            while (_availableRawFrames.TryDequeue(out packet))
                packet.Dispose();
            ICameraRawFrameMemoryLease memoryLease = _rawFrameMemoryLease;
            _rawFrameMemoryLease = null;
            memoryLease?.Dispose();
            Volatile.Write(ref _rawFrameBufferCount, 0);
            Volatile.Write(ref _rawFrameBufferSize, 0);
        }

        /// <summary>持有首帧宽限锁直到生产票据成功登记或调用方回滚。</summary>
        private sealed class InitialProductionFrameGraceReservation : IProductionCameraStartupGraceReservation
        {
            /// <summary>拥有当前连接会话资格的相机。</summary>
            private readonly CameraHik _owner;

            /// <summary>释放状态，0表示仍持有锁，1表示已经释放。</summary>
            private int _disposed;

            /// <summary>创建已经持有相机宽限锁的预留对象。</summary>
            /// <param name="owner">拥有资格状态和锁的相机。</param>
            public InitialProductionFrameGraceReservation(CameraHik owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            /// <summary>确认生产票据已经成功入队，并永久消费当前连接的唯一宽限。</summary>
            public void Commit()
            {
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(InitialProductionFrameGraceReservation));
                Volatile.Write(ref _owner._initialProductionFrameGraceReserved, 1);
            }

            /// <summary>释放预留锁；未调用提交时资格保持可用，供下一个成功票据领取。</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                Monitor.Exit(_owner._initialProductionFrameGraceLock);
            }
        }

        /// <summary>SDK回调入口故障发生时冻结的首帧身份和单调时钟。</summary>
        private sealed class RawFrameIngressFailure
        {
            /// <summary>创建不可变入口故障。</summary>
            public RawFrameIngressFailure(
                long frameId,
                long callbackStartedTimestamp,
                Exception exception,
                CameraTriggerTicketRegistry frameRoutingRegistry,
                CameraTriggerTicketRegistry productionRegistry)
            {
                FrameId = frameId;
                CallbackStartedTimestamp = callbackStartedTimestamp;
                Exception = exception;
                FrameRoutingRegistry = frameRoutingRegistry;
                ProductionRegistry = productionRegistry;
            }

            /// <summary>获取故障帧的相机内部编号。</summary>
            public long FrameId { get; }

            /// <summary>获取故障帧进入SDK回调时的单调时钟刻度。</summary>
            public long CallbackStartedTimestamp { get; }

            /// <summary>获取SDK入口异常；缓冲暂不可用时为空。</summary>
            public Exception Exception { get; }

            /// <summary>获取该故障进入回调时所属的生产会话注册表。</summary>
            public CameraTriggerTicketRegistry ProductionRegistry { get; }

            /// <summary>获取该故障进入回调时用于归还预览计数的路由注册表。</summary>
            public CameraTriggerTicketRegistry FrameRoutingRegistry { get; }
        }

        /// <summary>保存一帧已脱离海康SDK生命周期的原始数据和诊断元数据。</summary>
        private sealed class HikRawFramePacket : IDisposable
        {
            /// <summary>回调完成标记，发布到队列后由转换线程等待。</summary>
            private int _callbackCompleted;

            /// <summary>获取预分配的非托管原始帧内存。</summary>
            public IntPtr Buffer { get; private set; }

            /// <summary>获取或设置当前帧SDK信息副本。</summary>
            public MyCamera.MV_FRAME_OUT_INFO_EX FrameInfo { get; set; }

            /// <summary>获取或设置框架帧序号。</summary>
            public long FrameId { get; set; }

            /// <summary>获取或设置SDK回调开始计时刻度。</summary>
            public long CallbackStartedTimestamp { get; set; }

            /// <summary>获取或设置原始内存复制开始计时刻度。</summary>
            public long CopyStartedTimestamp { get; set; }

            /// <summary>获取或设置SDK回调完成计时刻度。</summary>
            public long CallbackCompletedTimestamp { get; set; }

            /// <summary>获取或设置原始内存复制完成计时刻度。</summary>
            public long CopyCompletedTimestamp { get; set; }

            /// <summary>获取或设置海康SDK回调线程托管编号。</summary>
            public int CallbackThreadId { get; set; }

            /// <summary>获取或设置本帧是否启用完整耗时诊断。</summary>
            public bool DiagnosticEnabled { get; set; }

            /// <summary>获取或设置该帧进入回调时所属的生产会话注册表。</summary>
            public CameraTriggerTicketRegistry ProductionRegistry { get; set; }

            /// <summary>获取或设置该帧进入回调时用于归还预览计数或接管生产帧的注册表。</summary>
            public CameraTriggerTicketRegistry FrameRoutingRegistry { get; set; }

            /// <summary>按固定容量分配一块可重复使用的非托管原始帧缓冲。</summary>
            /// <param name="capacity">缓冲容量，单位为字节。</param>
            public HikRawFramePacket(int capacity)
            {
                Buffer = Marshal.AllocHGlobal(capacity);
            }

            /// <summary>在调用方遗漏显式释放时兜底回收非托管缓冲。</summary>
            ~HikRawFramePacket()
            {
                ReleaseBuffer();
            }

            /// <summary>记录尽可能接近SDK回调返回点的时间并发布全部帧元数据。</summary>
            /// <param name="completedTimestamp">SDK回调入口工作完成计时刻度。</param>
            public void MarkCallbackCompleted(long completedTimestamp)
            {
                CallbackCompletedTimestamp = completedTimestamp;
                Volatile.Write(ref _callbackCompleted, 1);
            }

            /// <summary>等待SDK回调完成当前帧元数据发布。</summary>
            public void WaitUntilCallbackCompleted()
            {
                SpinWait spinWait = new SpinWait();
                while (Volatile.Read(ref _callbackCompleted) == 0)
                    spinWait.SpinOnce();
            }

            /// <summary>清除本帧元数据，缓冲内存保留供下一帧复用。</summary>
            public void Reset()
            {
                FrameInfo = default(MyCamera.MV_FRAME_OUT_INFO_EX);
                FrameId = 0L;
                CallbackStartedTimestamp = 0L;
                CopyStartedTimestamp = 0L;
                CopyCompletedTimestamp = 0L;
                CallbackCompletedTimestamp = 0L;
                CallbackThreadId = 0;
                DiagnosticEnabled = false;
                ProductionRegistry = null;
                FrameRoutingRegistry = null;
                Volatile.Write(ref _callbackCompleted, 0);
            }

            /// <summary>释放当前非托管原始帧缓冲。</summary>
            public void Dispose()
            {
                ReleaseBuffer();
                GC.SuppressFinalize(this);
            }

            /// <summary>以幂等方式释放当前非托管缓冲。</summary>
            private void ReleaseBuffer()
            {
                IntPtr buffer = Buffer;
                Buffer = IntPtr.Zero;
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>使用原生SDK转换帧并复制为拥有独立生命周期的Mat。</summary>
        /// <param name="sourceData">源帧地址。</param>
        /// <param name="frameInfo">源帧信息。</param>
        /// <param name="frameId">诊断帧号。</param>
        /// <param name="diagnosticEnabled">是否记录完整耗时。</param>
        /// <param name="cancellationToken">生产取图的停止令牌；主动取图由相机生命周期锁保护。</param>
        /// <returns>调用方负责释放的独立Mat。</returns>
        private Mat ConvertFrameToIndependentMat(IntPtr sourceData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, long frameId, bool diagnosticEnabled, CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_callbackSyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
            if (Volatile.Read(ref _nativeDeviceOpened) == 0 ||
                Volatile.Read(ref _deviceConfigurationCompleted) == 0 ||
                _device == null ||
                _callbackRegistered)
                return;

            RegisterNativeCallbackCore();
        }

        /// <summary>在设备锁和回调锁保护下执行原生回调注册，不修改配置完成状态。</summary>
        private void RegisterNativeCallbackCore()
        {
            if (_device == null || _callbackRegistered)
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
            if (Volatile.Read(ref _nativeDeviceOpened) != 0 || _isGrabbing)
                throw new InvalidOperationException("相机打开时不能重新创建设备句柄。");
            if (_device != null && _deviceCreated)
            {
                int destroyResult = _device.MV_CC_DestroyDevice_NET();
                if (destroyResult != MyCamera.MV_OK)
                    throw CreateSdkException("重建前销毁旧相机句柄", destroyResult);
            }

            _deviceCreated = false;
            _callbackRegistered = false;
            _nativeImageCallback = null;
            _device = null;
            _deviceInfo = null;
            Volatile.Write(ref _nativeDeviceOpened, 0);
            Volatile.Write(ref _deviceConfigurationCompleted, 0);

            MyCamera newDevice = new MyCamera();
            MyCamera.MV_CC_DEVICE_INFO nativeInfo = deviceInfo.NativeDeviceInfo;
            EnsureSuccess(newDevice.MV_CC_CreateDevice_NET(ref nativeInfo), "创建相机句柄");
            _device = newDevice;
            _deviceCreated = true;
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

        /// <summary>为GigE相机设置SDK心跳超时，便于断网后快速进入自动重连。</summary>
        private void ConfigureGigEHeartbeat()
        {
            if (_deviceInfo == null || _deviceInfo.TransportLayerType != MyCamera.MV_GIGE_DEVICE)
                return;

            int result = _device.MV_CC_SetIntValueEx_NET("GevHeartbeatTimeout", GigEHeartbeatTimeoutMilliseconds);
            if (result != MyCamera.MV_OK)
                LogHelper.AddLog(MsgLevel.Warn, FormatSdkError("设置GigE相机心跳超时", result), true);
            else if (LogHelper.CanRecord(MsgLevel.Debug))
                LogHelper.TryAddDiagnosticLog(
                    string.Format("相机{0}设置GigE心跳超时：{1}ms", UserDefinedName, GigEHeartbeatTimeoutMilliseconds),
                    true);
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
            return GetEnumValueCore(key);
        }

        /// <summary>在相机Open配置事务内读取原生枚举参数，不检查尚未完成的配置门禁。</summary>
        /// <param name="key">GenICam参数键。</param>
        /// <returns>框架枚举参数。</returns>
        private CameraEnumValue GetEnumValueCore(string key)
        {
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
            ThrowIfDisposed();
            if (_device == null || !_deviceCreated)
                throw new InvalidOperationException("相机对象为空。");
            if (Volatile.Read(ref _nativeDeviceOpened) == 0)
                throw new InvalidOperationException(string.Format("相机（{0}）尚未打开。", UserDefinedName));
            if (Volatile.Read(ref _deviceConfigurationCompleted) == 0)
                throw new InvalidOperationException(string.Format("相机（{0}）已打开但初始化配置尚未完成。", UserDefinedName));
        }

        /// <summary>对象完成永久销毁后禁止再次访问已经释放的SDK与同步资源。</summary>
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(GetType().FullName, string.Format("相机{0}已经销毁，不能重新使用。", DevName));
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
