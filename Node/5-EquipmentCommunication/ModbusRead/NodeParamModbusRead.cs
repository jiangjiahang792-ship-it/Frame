using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead
{
    public class NodeParamModbusRead : INodeParam
    {
        [JsonIgnore]
        public IModbus Device { get; set; }
        public string DeviceName { get; set; }
        public ushort Count { get; set; } = 0;
        public string StartAddress { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public RegistersType DataType { get; set; }

    }

    /// <summary>
    /// Modbus数据类型
    /// </summary>
    public enum RegistersType
    {
        Bool,
        Short,
        UShort,
        Int,
        UInt,
        Float,
        Double,
        Long,
        ULong,
        线圈,
        离散输入
    }

}
