using Logger;
using MvCameraControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using OpenCvSharp.Dnn;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TDJS_Vision.Device.Camera
{
    /// <summary>
    /// 海康相机类
    /// </summary>
    public class CameraHik : ICamera
    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        public event EventHandler<bool> ConnectStatusEvent;
        public event Action<Bitmap> OnImageReceived;

        /// <summary>
        /// 单个海康相机信息
        /// </summary>
        private MvCameraControl.IDevice device = null;

        /// <summary>
        /// 是否正在取流
        /// </summary>
        private bool _isGrabbing = false;

        /// <summary>
        /// 设备是否启用
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// 相机的触发模式
        /// </summary>
        public TriggerModel TriggerModel { get; set; }

        /// <summary>
        /// 相机的触发源
        /// </summary>
        public TriggerSource TriggerSource { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.CAMERA;

        /// <summary>
        /// 相机SN
        /// </summary>
        public string SN { get; set; }

        /// <summary>
        /// 相机IP
        /// </summary>
        public string IP { get; set; } = "无";

        /// <summary>
        /// 厂商名称
        /// </summary>
        public string ManufacturerName { get; set; }

        /// <summary>
        /// 相机品牌
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceBrand Brand { get; set; }
        public string ClassName { get; set; } = typeof(CameraHik).FullName;

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DevName {  get; set; }

        /// <summary>
        /// 用户定义名称
        /// </summary>
        public string UserDefinedName { get; set; }
        /// <summary>
        /// 图像获取超时时间
        /// </summary>
        public uint GetImageTimeOut { get; set; } = 2000;

        #region 反序列化相关函数

        [JsonConstructor]
        /// <summary>
        /// 无参构造提供给反序列化使用
        /// 反序列化相机对象步骤：
        /// 1.使用无参构造函数创建对象
        /// 2.通过反序列化得到的SN对比找到对应IDevice类相机对象
        /// 3.调用打开相机
        /// </summary>
        public CameraHik() { }

        /// <summary>
        /// 创建相机设备
        /// </summary>
        /// <param name="devIP"></param>
        /// <returns></returns>
        public void CreateDevice()
        {
            try
            {
                /*只枚举网口类型的相机*/
                List<IDeviceInfo> devInfoList = null;
                int ret = DeviceEnumerator.EnumDevices(DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice | DeviceTLayerType.MvGenTLCameraLinkDevice
            | DeviceTLayerType.MvGenTLCXPDevice | DeviceTLayerType.MvGenTLXoFDevice, out devInfoList);
                if (ret == MvError.MV_OK)
                {
                    foreach (IDeviceInfo devInfo in devInfoList)
                    {
                        if (devInfo.SerialNumber == SN)
                        {
                            device = DeviceFactory.CreateDevice(devInfo);
                            if (devInfo is IGigEDeviceInfo gigeInfo)
                                IP = ConvertUInt32ToIP(gigeInfo.CurrentIp); // 更新相机IP
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{ex.Message}", true);
            }
        }


        #endregion

        /// <summary>
        /// 使用相机信息构造相机对象
        /// </summary>
        /// <param name="DeviceInfo"></param>
        internal CameraHik(IDeviceInfo devInfo, string userName)
        {
            try
            {
                device = DeviceFactory.CreateDevice(devInfo);
                ManufacturerName = device.DeviceInfo.ManufacturerName;  // 获取相机厂商
                //Brand = ManufacturerName == "Basler" ? DeviceBrand.Basler : ManufacturerName == "HikVision" ? DeviceBrand.HikVision : DeviceBrand.Unknow;
                Brand = ManufacturerName == "Basler" ? DeviceBrand.Basler : ManufacturerName == "Huaray Technology" ? DeviceBrand.HuarayTechnology : ManufacturerName == "HikVision" ? DeviceBrand.HikVision : DeviceBrand.Unknow;
                var a = device.DeviceInfo; // 无其他用意，调用一次仅为下面能获取到SN
                DevName = GetDevNameByDevInfo(device.DeviceInfo);
                UserDefinedName = userName;
                Task.Run(() =>
                {
                    do
                    {
                        SN = devInfo.SerialNumber;
                    } while (SN.IsNullOrEmpty());
                });
                if (devInfo is IGigEDeviceInfo info)
                {
                    IP = ConvertUInt32ToIP(info.CurrentIp); // 获取相机IP
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        private static string ConvertUInt32ToIP(uint ipValue)
        {
            byte[] bytes = BitConverter.GetBytes(ipValue);

            // 如果是小端序（x86 或 x64 架构），需要反转字节顺序
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return new IPAddress(bytes).ToString();
        }

        /// <summary>
        /// 初始化SDK
        /// </summary>
        public static void InitSDK()
        {
            SDKSystem.Initialize();
        }

        /// <summary>
        /// 反初始化SDK
        /// </summary>
        public static void Finalize()
        {
            SDKSystem.Finalize();
        }

        /// <summary>
        /// 根据设备信息获取设备名称
        /// </summary>
        /// <param name="cameraDevInfo"></param>
        /// <returns></returns>
        public static string GetDevNameByDevInfo(IDeviceInfo cameraDevInfo)
        {
            string name = "";
            if (cameraDevInfo.UserDefinedName != "")
            {
                name = (cameraDevInfo.UserDefinedName + "(" + cameraDevInfo.SerialNumber + ")");
            }
            else
            {
                name = (cameraDevInfo.ManufacturerName + cameraDevInfo.ModelName + " (" + cameraDevInfo.SerialNumber + ")");
            }
            return name;
        }

        /// <summary>
        /// 查找相机
        /// </summary>
        /// <returns></returns>
        public static List<IDeviceInfo> FindCamera()
        {
            GC.Collect();
            int nRet;
            List<IDeviceInfo> infoList = null;
            nRet = DeviceEnumerator.EnumDevices(DeviceTLayerType.MvGigEDevice | DeviceTLayerType.MvUsbDevice | DeviceTLayerType.MvGenTLCameraLinkDevice
            | DeviceTLayerType.MvGenTLCXPDevice | DeviceTLayerType.MvGenTLXoFDevice, out infoList);
            if (MvError.MV_OK != nRet)
            {
                LogHelper.AddLog(MsgLevel.Exception, "获取相机列表失败", true);
            }
            return infoList;
        }


        /// <summary>
        /// Bitmap转换为 BGR24 格式的 byte[]
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static byte[] ConvertBitmapToBgr24(Bitmap bitmap)
        {
            // 创建一个新的 24bpp BGR 格式的 Bitmap
            Bitmap bgrBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(bgrBitmap))
            {
                g.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            }

            // 锁定图像内存
            var rect = new Rectangle(0, 0, bgrBitmap.Width, bgrBitmap.Height);
            var bmpData = bgrBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bufferSize = Math.Abs(bmpData.Stride) * bgrBitmap.Height;
            byte[] buffer = new byte[bufferSize];

            // 复制图像数据到 byte[]
            Marshal.Copy(bmpData.Scan0, buffer, 0, bufferSize);
            bgrBitmap.UnlockBits(bmpData);

            return buffer;
        }

        /// <summary>
        /// 打开相机
        /// </summary>
        /// <param name="CamerSerialization"></param>
        /// <returns></returns>
        public bool Open()
        {
            if (device == null) throw new Exception("相机对象为空！");

            int nRet = 0;
            nRet = device.Open();
            if (nRet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"相机（{UserDefinedName}）打开失败！原因：{nRet}", true);
                throw new Exception($"相机（{UserDefinedName}）打开失败！");
            }
            IsOpen = true;
            TriggerSource = GetTriggerSource();
            ConnectStatusEvent?.Invoke(this, true);
            //ch: 判断是否为gige设备 | en: Determine whether it is a GigE device
            if (device is IGigEDevice)
            {
                //ch: 转换为gigE设备 | en: Convert to Gige device
                IGigEDevice gigEDevice = device as IGigEDevice;

                // ch:探测网络最佳包大小(只对GigE相机有效) | en:Detection network optimal package size(It only works for the GigE camera)
                int optionPacketSize;
                nRet = gigEDevice.GetOptimalPacketSize(out optionPacketSize);
                if (nRet != MvError.MV_OK)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"获取网络最佳包大小失败！{nRet}");
                    return false;
                }
                else
                {
                    nRet = device.Parameters.SetIntValue("GevSCPSPacketSize", (long)optionPacketSize);
                    LogHelper.AddLog(MsgLevel.Info, $"设置网络最佳包大小：{optionPacketSize}", true);
                    if (nRet != MvError.MV_OK)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"设置网络最佳包大小失败！错误码：{nRet}");
                        return false;
                    }
                }
                // 订阅图像回调函数
                //device.StreamGrabber.FrameGrabedEvent += GetImageCallBack;
            }
            return true;
        }

        int count = 0;

        /// <summary>
        /// 取流回调
        /// </summary>
        private void GetImageCallBack(object sender, FrameGrabbedEventArgs e)
        {
            LogHelper.AddLog(MsgLevel.Exception, $"进入到回调函数{DevName} , 触发次数:{count} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
            // 处理每一帧图像
            var image = ConvertImageFormat(e.FrameOut.Image, device.PixelTypeConverter);
            count++;
            OnImageReceived?.Invoke(image);
        }

        /// <summary>
        /// 主动获取一帧图像
        /// </summary>
        /// <returns></returns>
        public Bitmap GetOneFrameImage()
        {
            try
            {
                if (TriggerSource == TriggerSource.SOFT)
                {
                    GrabOne();
                }
                IFrameOut frame;
                int ret = device.StreamGrabber.GetImageBuffer(GetImageTimeOut, out frame);
                if (ret != MvError.MV_OK)
                    return null;
                var image = ConvertImageFormat(frame.Image, device.PixelTypeConverter);
                device.StreamGrabber.FreeImageBuffer(frame);
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 将 IImage 转换为 Bitmap
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private static Bitmap ConvertImageFormat(IImage image, IPixelTypeConverter pixelType)
        {
            IImage inputImage = image;
            uint nChannelNum = 0;
            try
            {
                MvGvspPixelType dstPixelType = MvGvspPixelType.PixelType_Gvsp_Undefined;
                if (IsColorPixelFormat(image.PixelType))
                {
                    dstPixelType = MvGvspPixelType.PixelType_Gvsp_RGB8_Packed;
                    nChannelNum = 3;
                }
                else if (IsMonoPixelFormat(image.PixelType))
                {
                    dstPixelType = MvGvspPixelType.PixelType_Gvsp_Mono8;
                    nChannelNum = 1;
                }
                pixelType.ConvertPixelType(inputImage, out inputImage, dstPixelType);
                if (nChannelNum == 1)
                {
                    //通过设置调色板从伪彩改为灰度
                    var pal = inputImage.ToBitmap().Palette;
                    for (int j = 0; j < 256; j++)
                        pal.Entries[j] = Color.FromArgb(j, j, j);
                    inputImage.ToBitmap().Palette = pal;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return inputImage.ToBitmap();
        }

        public TriggerModel GetTriggerMode()
        {
            if (device == null) throw new Exception("相机对象为空！");
            IEnumValue isTriggerMode;
            device.Parameters.GetEnumValue("TriggerMode", out isTriggerMode);
            return isTriggerMode.CurEnumEntry.Value == 1u ? TriggerModel.On : TriggerModel.Off;
        }

        /// <summary>
        /// 设置相机触发模式
        /// </summary>
        /// <param name="isTrigger"></param>
        public void SetTriggerMode(TriggerModel triggerModel)
        {
            if (device == null) throw new Exception("相机对象为空！");
            int result = device.Parameters.SetEnumValue("TriggerMode", triggerModel==TriggerModel.On ? 1u : 0u);
        }

        /// <summary>
        /// 获取相机触发源
        /// </summary>
        /// <param name="triggerSource"></param>
        public TriggerSource GetTriggerSource()
        {
            if (device == null) throw new Exception("相机对象为空！");
            if (!(GetTriggerMode() == TriggerModel.Off))
                return TriggerSource.Auto;

            IEnumValue triggerSourceEnum;
            device.Parameters.GetEnumValue("TriggerSource", out triggerSourceEnum);

            switch (triggerSourceEnum.CurEnumEntry.Value)
            {
                case 0:
                    TriggerSource = ManufacturerName == "Basler" ? TriggerSource.SOFT : TriggerSource.LINE0;
                    return TriggerSource;
                case 1:
                    TriggerSource = ManufacturerName == "Basler" ? TriggerSource.LINE1 : TriggerSource.LINE1;
                    return TriggerSource;
                case 2:
                    TriggerSource = ManufacturerName == "Basler" ? TriggerSource.LINE2 : TriggerSource.LINE2;
                    return TriggerSource;
                case 3:
                    TriggerSource = ManufacturerName == "Basler" ? TriggerSource.LINE3 : TriggerSource.LINE3;
                    return TriggerSource;
                case 7:
                    TriggerSource = TriggerSource.SOFT;
                    return TriggerSource;
                default:
                    TriggerSource = TriggerSource.Auto;
                    return TriggerSource;
            }
        }

        /// <summary>
        /// 设置触发源，AUTO表示自动触发（非触发模式），SOFT表示软件触发，LINE0-3表示硬件触发
        /// </summary>
        /// <param name="trigBySoft"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public void SetTriggerSource(TriggerSource triggerSource)
        {
            if (device == null) throw new Exception("相机对象为空！");
            // ch:触发源选择:0 - Line0; | en:Trigger source select:0 - Line0;
            //           1 - Line1;
            //           2 - Line2;
            //           3 - Line3;
            //           4 - Counter;
            //           7 - Software;
            if (ManufacturerName == "Basler")
            {
                switch (triggerSource)
                {
                    case TriggerSource.Auto:
                        SetTriggerMode(TriggerModel.Off);
                        break;
                    case TriggerSource.SOFT:
                        device.Parameters.SetEnumValue("TriggerSource", 0);
                        break;
                    case TriggerSource.LINE1:
                        device.Parameters.SetEnumValue("TriggerSource", 1);
                        break;
                    case TriggerSource.LINE2:
                        device.Parameters.SetEnumValue("TriggerSource", 2);
                        break;
                    case TriggerSource.LINE3:
                        device.Parameters.SetEnumValue("TriggerSource", 3);
                        break;
                    default:
                        break;
                }
            }
            else if (ManufacturerName == "Huaray Technology")
            {
                switch (triggerSource)
                {
                    case TriggerSource.Auto:
                        SetTriggerMode(TriggerModel.Off);
                        break;
                    case TriggerSource.SOFT:
                        device.Parameters.SetEnumValue("TriggerSource", 0);
                        break;
                    case TriggerSource.LINE1:
                        device.Parameters.SetEnumValue("TriggerSource", 2);
                        break;
                    case TriggerSource.LINE2:
                        device.Parameters.SetEnumValue("TriggerSource", 3);
                        break;
                    case TriggerSource.LINE3:
                        device.Parameters.SetEnumValue("TriggerSource", 3);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (triggerSource)
                {
                    case TriggerSource.Auto:
                        SetTriggerMode(TriggerModel.Off);
                        break;
                    case TriggerSource.SOFT:
                        int result = device.Parameters.SetEnumValue("TriggerSource", 7);
                        break;
                    case TriggerSource.LINE0:
                        device.Parameters.SetEnumValue("TriggerSource", 0);
                        break;
                    case TriggerSource.LINE1:
                        device.Parameters.SetEnumValue("TriggerSource", 1);
                        break;
                    case TriggerSource.LINE2:
                        device.Parameters.SetEnumValue("TriggerSource", 2);
                        break;
                    case TriggerSource.LINE3:
                        device.Parameters.SetEnumValue("TriggerSource", 3);
                        break;
                    default:
                        break;
                }
            }
            TriggerSource = triggerSource;
        }

        /// <summary>
        /// 设置硬触发触发沿
        /// </summary>
        public void SetTriggerEdge(TriggerEdge triggerEdge)
        {
            if (device == null) throw new Exception("相机对象为空！");
            switch (triggerEdge)
            {
                case TriggerEdge.Rising:
                    device.Parameters.SetEnumValueByString("TriggerActivation", "RisingEdge");
                    break;
                case TriggerEdge.Falling:
                    device.Parameters.SetEnumValueByString("TriggerActivation", "FallingEdge");
                    break;
                case TriggerEdge.Low:
                    device.Parameters.SetEnumValueByString("TriggerActivation", "LevelLow");
                    break;
                case TriggerEdge.Hight:
                    device.Parameters.SetEnumValueByString("TriggerActivation", "LevelHigh");
                    break;
                case TriggerEdge.Any:
                    device.Parameters.SetEnumValueByString("TriggerActivation", "AnyEdge");
                    break;
            }
        }

        /// <summary>
        /// 软触发一次
        /// </summary>
        /// <returns></returns>
        public void GrabOne()
        {
            if (device == null) throw new Exception("相机对象为空！");
            int result = device.Parameters.SetCommandValue("TriggerSoftware");
        }

        /// <summary>
        /// 获取曝光
        /// </summary>
        public IFloatValue GetExposureTime()
        {
            if (device == null) throw new Exception("相机对象为空！");
            if (ManufacturerName == "Basler")
            {
                device.Parameters.GetFloatValue("ExposureTimeAbs", out IFloatValue exposureTime);
                return exposureTime;
            }
            else
            {
                device.Parameters.GetFloatValue("ExposureTime", out IFloatValue exposureTime);
                return exposureTime;
            }
        }

        /// <summary>
        /// 获取增益
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public (IIntValue, IFloatValue) GetGain()
        {
            if (device == null) throw new Exception("相机对象为空！");

            if (ManufacturerName == "Basler")
            {
                device.Parameters.GetIntValue("GainRaw", out IIntValue gain);
                return (gain, null);
            }
            else
            {
                device.Parameters.GetFloatValue("Gain", out IFloatValue gain);
                return (null, gain);
            }
        }

        /// <summary>
        /// 获取触发延迟时间
        /// </summary>
        /// <returns></returns>
        public IFloatValue GetTriggerDelay()
        {
            if (device == null) throw new Exception("相机对象为空！");
            if (ManufacturerName == "Basler")
            {
                device.Parameters.GetFloatValue("TriggerDelayAbs", out IFloatValue triggerDelay);
                return triggerDelay;
            }
            else
            {
                device.Parameters.GetFloatValue("TriggerDelay", out IFloatValue triggerDelay);
                return triggerDelay;
            }
        }

        /// <summary>
        /// 获取线路选择器
        /// </summary>
        /// <returns></returns>
        public IEnumValue GetLineSelector()
        {
            if (device == null) throw new Exception("相机对象为空！");
            IEnumValue enumValue;
            int Rnet;
            Rnet = device.Parameters.GetEnumValue("LineSelector", out enumValue);
            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"获取线路选择器失败！错误码：{Rnet}", true);
            }
            return enumValue;
        }

        /// <summary>
        /// 设置线路
        /// </summary>
        /// <param name="line"></param>
        public void SetLineSelector(string line)
        {
            if (device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;
            switch (line)
            {
                case "Line0":
                    Rnet = device.Parameters.SetEnumValue("LineSelector", 0);
                    break;
                case "Line1":
                    Rnet = device.Parameters.SetEnumValue("LineSelector", 1);
                    break;
               case "Line2":
                    Rnet = device.Parameters.SetEnumValue("LineSelector", 2);
                    break;
                default:
                    break;
            }
            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"设置线路选择器失败！错误码：{Rnet}", true);
            }
        }

        /// <summary>
        /// 获取线路模式
        /// </summary>
        /// <returns></returns>
        public IEnumValue GetLineMode()
        { 
            if (device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;
            IEnumValue enumValue;
            device.Parameters.GetEnumValue("LineMode", out enumValue);
            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"获取线路模式失败！错误码：{Rnet}", true);
            }
            return enumValue;
        }

        /// <summary>
        /// 设置线路模式
        /// </summary>
        /// <param name="lineMode"></param>
        public void SetLineMode(string lineMode)
        {
            if (device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;
            switch (lineMode)
            { 
                case "输入":
                    Rnet = device.Parameters.SetEnumValue("LineMode", 0);
                    break;
                case "输出":
                    if (Brand==DeviceBrand.HuarayTechnology)
                    {
                        Rnet = device.Parameters.SetEnumValue("LineMode", 1);
                    }else Rnet = device.Parameters.SetEnumValue("LineMode", 8);
                    break;
                default:
                    break;
            }
            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"设置线路模式失败！错误码：{Rnet}", true);
            }
        }

        /// <summary>
        /// 获取使能
        /// </summary>
        /// <returns></returns>
        public bool GetStrobeEnable() 
        { 
            if(device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;
            bool enable;
            Rnet = device.Parameters.GetBoolValue("StrobeEnable", out enable);

            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"设置线路源失败！错误码：{Rnet}", true);
            }

            return enable;
        }

        /// <summary>
        /// 设置使能
        /// </summary>
        /// <param name="enable"></param>
        public void SetStrobeEnable(bool enable)
        {
            if (device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;

            Rnet = device.Parameters.SetBoolValue("StrobeEnable", enable);

            if (Rnet != MvError.MV_OK)
            { 
                LogHelper.AddLog(MsgLevel.Exception, $"设置线路源失败！错误码：{Rnet}", true);
            }
        }

        /// <summary>
        /// 设置线路反转
        /// </summary>
        /// <param name="inverter"></param>
        public void SetLineInverter(bool inverter)
        {
            if (device == null) throw new Exception("相机对象为空！");
            int Rnet = 0;
            Rnet = device.Parameters.SetBoolValue("LineInverter", inverter);
            if (Rnet != MvError.MV_OK)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"设置线路反转失败！错误码：{Rnet}  相机品牌:{DevName.ToString()}", true);
            } 
        }

        private readonly object _ioLock = new object();

        
        public void SetIO(string lineSelector,bool inverter) {
            lock (_ioLock)
            {
                SetLineSelector(lineSelector);
                SetLineInverter(inverter);
            }
        }

        /// <summary>
        /// 设置增益
        /// </summary>
        /// <param name="gainValue"></param>
        public void SetGain(double gainValue)
        {
            if (device == null) throw new Exception("相机对象为空！");
            device.Parameters.SetEnumValue("GainAuto", 0);
            if (ManufacturerName == "Basler")
            {
                device.Parameters.SetIntValue("GainRaw", (int)gainValue);
            }
            else
            {
                device.Parameters.SetFloatValue("Gain", (float)gainValue);
            }
        }

        /// <summary>
        /// 设置曝光 
        /// </summary>
        /// <param name="time"></param>
        public void SetExposureTime(double time)
        {
            if (device == null) throw new Exception("相机对象为空！");
            device.Parameters.SetEnumValue("ExposureAuto", 0);

            if (ManufacturerName == "Basler")
            {
                device.Parameters.SetFloatValue("ExposureTimeAbs", (float)time);
            }
            else
            {
                device.Parameters.SetFloatValue("ExposureTime", (float)time);
            }
        }

        /// <summary>
        /// 设置触发延迟
        /// </summary>
        /// <param name="time">单位us</param>
        public void SetTriggerDelay(double time)
        {
            if (device == null) throw new Exception("相机对象为空！");

            if (ManufacturerName == "Basler")
            {
                device.Parameters.SetFloatValue("TriggerDelayAbs", (float)time);
            }
            else
            {
                device.Parameters.SetFloatValue("TriggerDelay", (float)time);
            }
        }

        /// <summary>
        /// 开始取流
        /// </summary>
        /// <returns></returns>
        public void StartGrabbing()
        {
            if (device == null) throw new Exception("相机对象为空！");
            if (!_isGrabbing)
            {
                device.StreamGrabber.SetImageNodeNum(1);
                device.StreamGrabber.StartGrabbing();
                _isGrabbing = true;
            }
        }

        /// <summary>
        /// 停止取流
        /// </summary>
        /// <returns></returns>
        public void StopGrabbing()
        {
            if (device == null) throw new Exception("相机对象为空！");
            device.StreamGrabber.StopGrabbing();
            _isGrabbing = false;
        }

        /// <summary>
        /// 获取相机取流状态
        /// </summary>
        /// <returns></returns>
        public bool GetGrabStatus()
        {
            if (device == null) throw new Exception("相机对象为空！");
            return _isGrabbing;
        }

        /// <summary>
        /// 关闭相机
        /// </summary>
        /// <returns></returns>
        public void Close()
        {
            if (device == null) throw new Exception("相机对象为空！");
            //先停止取流
            if (_isGrabbing)
            {
                StopGrabbing();
            }
            //device.StreamGrabber.FrameGrabedEvent -= GetImageCallBack;
            ConnectStatusEvent?.Invoke(this, false);
            device.Close();
            IsOpen = false;
            ConnectStatusEvent?.Invoke(this, false);
        }

        /// <summary>
        /// 释放相机资源
        /// </summary>
        public void Dispose()
        {
            if (device != null)
            {
                if (device.IsConnected) { device.Close(); }
                device?.Dispose();
            }
        }

        /// <summary>
        /// 判断是否为彩色图像
        /// </summary>
        /// <param name="enType"></param>
        /// <returns></returns>
        private static bool IsColorPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BGR8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_RGBA8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BGRA8_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断图像是否是黑白图像
        /// </summary>
        /// <param name="enType"></param>
        /// <returns></returns>
        private static bool IsMonoPixelFormat(MvGvspPixelType enType)
        {
            switch (enType)
            {
                case MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    return true;
                default:
                    return false;
            }
        }

       
    }
}
