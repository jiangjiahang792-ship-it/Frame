using MvCamCtrl.NET;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TDJS_Vision.Device.Camera.LineScan
{
    /// <summary>独立枚举海康线扫设备，不依赖现有面阵相机实现。</summary>
    public static class HikLineScanDeviceDiscovery
    {
        /// <summary>SDK初始化标志。</summary>
        private static int _initialized;

        /// <summary>枚举当前在线的海康CL或CIS系列线扫相机。</summary>
        /// <returns>线扫设备列表。</returns>
        public static List<HikNativeDeviceInfo> FindDevices()
        {
            EnsureInitialized();
            MyCamera.MV_CC_DEVICE_INFO_LIST list = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            int result = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref list);
            if (result != MyCamera.MV_OK)
                throw new InvalidOperationException(string.Format("枚举海康线扫相机失败，SDK错误码：0x{0:X8}。", result));
            List<HikNativeDeviceInfo> devices = new List<HikNativeDeviceInfo>();
            for (int index = 0; index < list.nDeviceNum; index++)
            {
                MyCamera.MV_CC_DEVICE_INFO nativeInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
                    list.pDeviceInfo[index], typeof(MyCamera.MV_CC_DEVICE_INFO));
                HikNativeDeviceInfo info = Parse(index, nativeInfo);
                if (IsHikLineScan(info))
                    devices.Add(info);
            }
            return devices;
        }

        /// <summary>确保海康SDK已经初始化。</summary>
        private static void EnsureInitialized()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
                return;
            int result = MyCamera.MV_CC_Initialize_NET();
            if (result != MyCamera.MV_OK)
            {
                Interlocked.Exchange(ref _initialized, 0);
                throw new InvalidOperationException(string.Format("初始化海康线扫SDK失败，SDK错误码：0x{0:X8}。", result));
            }
        }

        /// <summary>判断枚举项是否属于海康线扫产品。</summary>
        private static bool IsHikLineScan(HikNativeDeviceInfo info)
        {
            string manufacturer = info.ManufacturerName ?? string.Empty;
            string model = info.ModelName ?? string.Empty;
            bool isHik = manufacturer.IndexOf("Hik", StringComparison.OrdinalIgnoreCase) >= 0 ||
                manufacturer.Equals("GEV", StringComparison.OrdinalIgnoreCase);
            return isHik && (model.IndexOf("MV-CL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                model.IndexOf("CIS", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>把SDK设备联合体转换成框架设备信息。</summary>
        private static HikNativeDeviceInfo Parse(int index, MyCamera.MV_CC_DEVICE_INFO nativeInfo)
        {
            HikNativeDeviceInfo info = new HikNativeDeviceInfo
            {
                Index = index,
                TransportLayerType = nativeInfo.nTLayerType,
                NativeDeviceInfo = nativeInfo,
                ManufacturerName = "未知厂商",
                ModelName = "未知型号"
            };
            if (nativeInfo.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO_EX data = (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                    nativeInfo.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
                info.ManufacturerName = Clean(data.chManufacturerName);
                info.ModelName = Clean(data.chModelName);
                info.SerialNumber = Clean(data.chSerialNumber);
                info.UserDefinedName = Clean(data.chUserDefinedName);
                info.IpAddress = string.Format("{0}.{1}.{2}.{3}", (data.nCurrentIp >> 24) & 255, (data.nCurrentIp >> 16) & 255, (data.nCurrentIp >> 8) & 255, data.nCurrentIp & 255);
            }
            else if (nativeInfo.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO_EX data = (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                    nativeInfo.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
                info.ManufacturerName = Clean(data.chManufacturerName);
                info.ModelName = Clean(data.chModelName);
                info.SerialNumber = Clean(data.chSerialNumber);
                info.UserDefinedName = Clean(data.chUserDefinedName);
            }
            return info;
        }

        /// <summary>清理SDK固定长度字符串。</summary>
        private static string Clean(string value) => string.IsNullOrEmpty(value) ? string.Empty : value.TrimEnd('\0', ' ');

        /// <summary>清理SDK固定长度字节字符串。</summary>
        private static string Clean(byte[] value)
        {
            if (value == null)
                return string.Empty;
            int length = Array.IndexOf(value, (byte)0);
            if (length < 0)
                length = value.Length;
            return Encoding.Default.GetString(value, 0, length).Trim();
        }
    }
}
