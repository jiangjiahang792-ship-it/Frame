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

        /// <summary>字符串编码名称，旧方案默认使用 ASCII。</summary>
        public string StringEncodingName { get; set; } = "us-ascii";

        /// <summary>字符串读取时是否将寄存器低字节放在前面，默认高字节在前。</summary>
        public bool StringLowByteFirst { get; set; }

        /// <summary>可订阅值的数量；字符串占用多个寄存器但仅输出一个完整值。</summary>
        [JsonIgnore]
        public int OutputValueCount => DataType == RegistersType.String ? (Count > 0 ? 1 : 0) : Count;

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
        离散输入,
        /// <summary>将连续保持寄存器解码成一个字符串；Count 表示寄存器个数。</summary>
        String
    }

}
