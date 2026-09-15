using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MvCamCtrl.NET;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 可供界面选择的海康相机设备信息。
    /// </summary>
    internal sealed class CameraDeviceItem
    {
        /// <summary>枚举列表中的设备序号。</summary>
        public int Index { get; set; }

        /// <summary>海康SDK设备结构。</summary>
        public MyCamera.MV_CC_DEVICE_INFO DeviceInfo { get; set; }

        /// <summary>相机型号。</summary>
        public string ModelName { get; set; }

        /// <summary>相机序列号。</summary>
        public string SerialNumber { get; set; }

        /// <summary>GigE相机当前IP地址。</summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// 返回用于下拉框显示的中文设备描述。
        /// </summary>
        /// <returns>设备描述。</returns>
        public override string ToString()
        {
            string address = string.IsNullOrWhiteSpace(IpAddress) ? string.Empty : "，IP=" + IpAddress;
            return string.Format("[{0}] {1}，SN={2}{3}", Index, ModelName, SerialNumber, address);
        }
    }

    /// <summary>
    /// 使用海康官方旧版.NET封装枚举相机设备。
    /// </summary>
    internal static class CameraDeviceDiscovery
    {
        /// <summary>
        /// 枚举GigE和USB海康相机。
        /// </summary>
        /// <returns>可用设备列表。</returns>
        public static IReadOnlyList<CameraDeviceItem> Enumerate()
        {
            MyCamera.MV_CC_DEVICE_INFO_LIST deviceList = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            int result = MyCamera.MV_CC_EnumDevices_NET(
                MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE,
                ref deviceList);
            if (result != MyCamera.MV_OK)
                throw new InvalidOperationException(string.Format("枚举海康相机失败，错误码：0x{0:X8}", result));

            List<CameraDeviceItem> devices = new List<CameraDeviceItem>();
            for (int index = 0; index < deviceList.nDeviceNum; index++)
            {
                MyCamera.MV_CC_DEVICE_INFO info = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(
                    deviceList.pDeviceInfo[index],
                    typeof(MyCamera.MV_CC_DEVICE_INFO));

                CameraDeviceItem item = CreateItem(index, info);
                devices.Add(item);
            }

            return devices;
        }

        /// <summary>
        /// 从SDK联合体读取设备型号、序列号和IP。
        /// </summary>
        /// <param name="index">设备序号。</param>
        /// <param name="info">SDK设备信息。</param>
        /// <returns>界面设备项。</returns>
        private static CameraDeviceItem CreateItem(int index, MyCamera.MV_CC_DEVICE_INFO info)
        {
            CameraDeviceItem item = new CameraDeviceItem
            {
                Index = index,
                DeviceInfo = info,
                ModelName = "未知型号",
                SerialNumber = "未知序列号"
            };

            if (info.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                MyCamera.MV_GIGE_DEVICE_INFO_EX gigEInfo =
                    (MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                        info.SpecialInfo.stGigEInfo,
                        typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX));
                item.ModelName = Clean(gigEInfo.chModelName);
                item.SerialNumber = Clean(gigEInfo.chSerialNumber);
                item.IpAddress = string.Format(
                    "{0}.{1}.{2}.{3}",
                    (gigEInfo.nCurrentIp & 0xff000000) >> 24,
                    (gigEInfo.nCurrentIp & 0x00ff0000) >> 16,
                    (gigEInfo.nCurrentIp & 0x0000ff00) >> 8,
                    gigEInfo.nCurrentIp & 0x000000ff);
            }
            else if (info.nTLayerType == MyCamera.MV_USB_DEVICE)
            {
                MyCamera.MV_USB3_DEVICE_INFO_EX usbInfo =
                    (MyCamera.MV_USB3_DEVICE_INFO_EX)MyCamera.ByteToStruct(
                        info.SpecialInfo.stUsb3VInfo,
                        typeof(MyCamera.MV_USB3_DEVICE_INFO_EX));
                item.ModelName = Clean(usbInfo.chModelName);
                item.SerialNumber = Clean(usbInfo.chSerialNumber);
            }

            return item;
        }

        /// <summary>
        /// 清理SDK固定字符串中的结束空字符和空白。
        /// </summary>
        /// <param name="value">SDK字符串。</param>
        /// <returns>清理后的字符串。</returns>
        private static string Clean(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.TrimEnd('\0', ' ');
        }
    }
}
