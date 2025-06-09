using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;

namespace TDJS_Vision.Device.Modbus
{
    internal interface IModbus : IDevice
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

        void Disconnect();
    }
    /// <summary>
    /// Modbus参数接口
    /// </summary>
    internal interface IModbusParam
    {
        [JsonConverter(typeof(StringEnumConverter))]
        DevType DevType { get; set; }
        string DevName { get; set; }
        string UserDefinedName { get; set; }
        ushort HoldTime { get; set; }
        byte ID { get; set; } // 站号
        string ClassName { get; set; } //反序列化用来标识接口类型
    }
}
