using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Threading.Tasks;
using HslCommunication.Core;
using HslCommunication.Reflection;
using HslCommunication;

namespace TDJS_Vision.Device.Modbus
{
    public interface IModbus : IDevice
    {
        string DevName { get; set; }
        string UserDefinedName { get; set; }

        [JsonConverter(typeof(PolyConverter))]
        IModbusParam ModbusParam { get; set; }

        bool IsConnect { get; set; }

        DevType DevType { get; set; }
        DeviceBrand Brand { get; set; }
        string ClassName { get; set; }

        event EventHandler<bool> ConnectStatusEvent;

        void Connect();

        #region 读取操作

        bool[] ReadCoils(string address, ushort length);

        bool[] ReadDiscretes(string address, ushort length);

        bool[] ReadBool(string address, ushort length);

        short[] ReadInt16(string address, ushort length);

        ushort[] ReadUInt16(string address, ushort length);

        int[] ReadInt32(string address, ushort length);

        uint[] ReadUInt32(string address, ushort length);

        float[] ReadFloat(string address, ushort length);

        long[] ReadInt64(string address, ushort length);

        ulong[] ReadUInt64(string address, ushort length);

        double[] ReadDouble(string address, ushort length);

        #endregion

        #region 写入操作

        void Write(string address, bool value);

        void Write(string address, bool[] values);

        void Write(string address, short value);

        void Write(string address, short[] values);

        void Write(string address, ushort value);

        void Write(string address, ushort[] values);

        void Write(string address, int value);

        void Write(string address, int[] values);

        void Write(string address, uint value);

        void Write(string address, uint[] values);

        void Write(string address, float value);

        void Write(string address, float[] values);

        void Write(string address, long value);

        void Write(string address, long[] values);

        void Write(string address, ulong value);

        void Write(string address, ulong[] values);

        void Write(string address, double value);

        void Write(string address, double[] values);

        #endregion

        void Disconnect();
    }
    /// <summary>
    /// Modbus参数接口
    /// </summary>
    public interface IModbusParam
    {
        [JsonConverter(typeof(StringEnumConverter))]
        DevType DevType { get; set; }
        string DevName { get; set; }
        string UserDefinedName { get; set; }
        ushort HoldTime { get; set; }
        string ClassName { get; set; } //反序列化用来标识接口类型
    }
}
