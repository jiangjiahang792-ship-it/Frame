using HslCommunication;
using HslCommunication.Core.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace TDJS_Vision.Device.PLC
{
    public interface IPlc : IDevice
    {
        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        event EventHandler<bool> ConnectStatusEvent;
        /// <summary>
        /// PLC参数
        /// </summary>
        PLCParms PLCParms { get; set; }

        /// <summary>
        /// PLC连接状态
        /// </summary>
        bool IsConnect { get; set; }

        /// <summary>获取或设置PLC单次连接与收发操作的有限超时时间，单位为毫秒。</summary>
        int OperationTimeoutMs { get; set; }

        /// <summary>
        /// 硬件硬件名称
        /// </summary>
        string DevName { get; set; }

        /// <summary>
        /// 设备品牌
        /// </summary>
        DeviceBrand Brand { get; set; }

        /// <summary>
        /// 用户自定义设备名
        /// </summary>
        string UserDefinedName { get; set; }

        /// <summary>
        /// 设备类型
        /// </summary>
        DevType DevType { get; set; }
        string ClassName { get; set; }
        /// <summary>
        /// 创建设备，反序列化用
        /// </summary>
        void CreateDevice();

        /// <summary>
        /// PLC连接
        /// </summary>
        /// <returns></returns>
        bool Connect();

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 释放Plc资源
        /// </summary>
        void Release();

        #region 读取多个字节
        /// <summary>
        /// 同步读取多个字节
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<byte[]> ReadBytes(string address, ushort length);
        /// <summary>
        /// 异步读取多个字节
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<byte[]>> ReadBytesAsync(string address, ushort length);
        #endregion

        #region 读取单个/多个布尔
        /// <summary>
        /// 同步读取单个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<bool> ReadBool(string address);
        /// <summary>
        /// 异步读取单个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<bool>> ReadBoolAsync(string address);
        /// <summary>
        /// 同步读取多个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<bool[]> ReadBool(string address, ushort length);
        /// <summary>
        /// 异步读取多个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length);
        /// <summary>
        /// 同步读取多个同类型地址的多个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<bool[]> ReadBool(string[] address);
        /// <summary>
        /// 异步读取多个同类型地址的多个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<bool[]>> ReadBoolAsync(string[] address);
        #endregion

        #region 读取单个/多个整型
        /// <summary>
        /// 同步读取单个Int16值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<short> ReadInt16(string address);
        /// <summary>
        /// 异步读取单个Int值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<short>> ReadInt16Async(string address);
        /// <summary>
        /// 同步读取单个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<int> ReadInt32(string address);
        /// <summary>
        /// 异步读取单个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<int>> ReadInt32Async(string address);
        /// <summary>
        /// 同步读取单个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<long> ReadInt64(string address);
        /// <summary>
        /// 异步读取单个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<long>> ReadInt64Async(string address);
        /// <summary>
        /// 同步读取多个Int16值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<short[]> ReadInt16(string address, ushort length);
        /// <summary>
        /// 异步读取多个Int16值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<short[]>> ReadInt16Async(string address, ushort length);
        /// <summary>
        /// 同步读取多个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<int[]> ReadInt32(string address, ushort length);
        /// <summary>
        /// 异步读取多个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<int[]>> ReadInt32Async(string address, ushort length);
        /// <summary>
        /// 同步读取多个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<long[]> ReadInt64(string address, ushort length);
        /// <summary>
        /// 异步读取多个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<long[]>> ReadInt64Async(string address, ushort length);
        /// <summary>
        /// 同步读取多个同类型地址的多个Int16值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<short[]> ReadInt16(string[] address);
        /// <summary>
        /// 异步读取多个同类型地址的多个Int16值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<short[]>> ReadInt16Async(string[] address);
        /// <summary>
        /// 同步读取多个同类型地址的多个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<int[]> ReadInt32(string[] address);
        /// <summary>
        /// 异步读取多个同类型地址的多个Int32值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<int[]>> ReadInt32Async(string[] address);
        /// <summary>
        /// 同步读取多个同类型地址的多个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<long[]> ReadInt64(string[] address);
        /// <summary>
        /// 异步读取多个同类型地址的多个Int64值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<long[]>> ReadInt64Async(string[] address);
        #endregion

        #region 读取单个/多个浮点数
        /// <summary>
        /// 同步读取单个Float值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperateResult<float> ReadFloat(string address);
        /// <summary>
        /// 异步读取单个float值
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperateResult<float>> ReadFloatAsync(string address);
        /// <summary>
        /// 同步读取多个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<float[]> ReadFloat(string address, ushort length);
        /// <summary>
        /// 异步读取多个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<float[]>> ReadFloatAsync(string address, ushort length);
        #endregion

        #region 读取单个字符串
        /// <summary>
        /// 同步读取单个string值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        OperateResult<string> ReadString(string address, ushort length);
        /// <summary>
        /// 异步读取单个string值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<OperateResult<string>> ReadStringAsync(string address, ushort length);
        #endregion

        #region 写入多个字节
        /// <summary>
        /// 同步写入多个字节
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteBytes(string address, byte[] value);
        /// <summary>
        /// 异步写入多个字节
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteBytesAsync(string address, byte[] value);
        #endregion

        #region 写入单个/多个布尔值
        /// <summary>
        /// 同步写入单个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteBool(string address, bool value);
        /// <summary>
        /// 异步写入单个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteBoolAsync(string address, bool value);
        /// <summary>
        /// 同步写入多个bool值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteBool(string address, bool[] value);
        /// <summary>
        /// 异步写入多个bool值,写入单个布尔值实测用这个接口更快
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteBoolAsync(string address, bool[] value);
        #endregion

        #region 写入单个/多个整型
        /// <summary>
        /// 同步写入单个int值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteInt(string address, int value);
        /// <summary>
        /// 异步写入单个int值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteIntAsync(string address, int value);
        /// <summary>
        /// 同步写入多个int值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteInt(string address, int[] value);
        /// <summary>
        /// 异步写入多个int值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteIntAsync(string address, int[] value);
        #endregion

        #region 写入单个/多个浮点值
        /// <summary>
        /// 同步写入单个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteFloat(string address, float value);
        /// <summary>
        /// 异步写入单个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteFloatAsync(string address, float value);
        /// <summary>
        /// 同步写入多个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteFloat(string address, float[] value);
        /// <summary>
        /// 异步写入多个float值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteFloatAsync(string address, float[] value);
        #endregion

        #region 写入单个字符串
        /// <summary>
        /// 同步写入单个string值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        OperateResult WriteString(string address, string value);
        /// <summary>
        /// 异步写入单个string值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<OperateResult> WriteStringAsync(string address, string value);
        #endregion

        #region 异步等待某个地址的值变为指定值
        /// <summary>
        /// 异步等待某个地址的bool值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, bool waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的short值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, short waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的ushort值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, ushort waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的int值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, int waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的uint值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, uint waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的long值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, long waitValue, int readInterval = 100, int waitTimeout = -1);
        /// <summary>
        /// 异步等待某个地址的ulong值变为指定值
        /// </summary>
        /// <param name="address"></param>
        /// <param name="waitValue"></param>
        /// <param name="readInterval"></param>
        /// <param name="waitTimeout"></param>
        /// <returns></returns>
        Task<OperateResult<TimeSpan>> WaitAsync(string address, ulong waitValue, int readInterval = 100, int waitTimeout = -1);

        #endregion
    }

    /// <summary>
    /// PLC参数
    /// </summary>
    public struct PLCParms
    {
        public string UserDefinedName; //PLC用户自定义名称
        [JsonConverter(typeof(StringEnumConverter))]
        public PlcConType PlcConType; //PLC通信方式
        public SerialParms SerialParms; //串口连接参数
        public EthernetParms EthernetParms; //网口连接参数
        public DeviceBrand DeviceBrand; //设备品牌
        /// <summary>基恩士具体通信协议，旧方案未保存时默认为 Nano OverTcp。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public KeyenceProtocol KeyenceProtocol;
    }

    /// <summary>基恩士 TCP 通信协议种类，首项兼容既有方案。</summary>
    public enum KeyenceProtocol
    {
        /// <summary>基恩士 Nano 上位链路 TCP 协议。</summary>
        NanoOverTcp = 0,
        /// <summary>基恩士 KV300 及旧系列上位链路 TCP 协议。</summary>
        Kv300Older = 1
    }

    /// <summary>
    /// 串口连接参数
    /// </summary>
    public struct SerialParms
    {
        public string PortName; // 串口号
        public int BaudRate;  // 波特率
        public int DataBits;  // 数据位
        [JsonConverter(typeof(StringEnumConverter))]
        public StopBits StopBits;  // 停止位
        [JsonConverter(typeof(StringEnumConverter))]
        public Parity Parity; // 校验位
        public SerialParms(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            StopBits = stopBits;
            Parity = parity;
        }
    }

    /// <summary>
    /// 网口连接参数
    /// </summary>
    public struct EthernetParms
    {
        public string IP; //IP地址
        public int Port;  //端口号
        public EthernetParms(string ip, int port)
        {
            IP = ip;
            Port = port;
        }
    }

    /// <summary>
    /// PLC通信方式
    /// </summary>
    public enum PlcConType
    {
        COM,
        ETHERNET
    }

    /// <summary>
    /// 数据类型
    /// </summary>
    public enum DataType
    {
        BOOL,
        FLOAT,
        INT,
        STRING,
        Bytes
    }
}
