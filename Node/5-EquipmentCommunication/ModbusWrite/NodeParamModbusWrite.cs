using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite
{
    /// <summary>保存Modbus写入节点的持久化配置与运行时设备绑定。</summary>
    public class NodeParamModbusWrite : INodeParam
    {
        /// <summary>获取或设置运行时Modbus设备，方案文件不序列化该对象。</summary>
        [JsonIgnore]
        public IModbus Device { get; set; }

        /// <summary>获取或设置用于恢复设备绑定的用户设备名。</summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 待发送的数据
        /// </summary>
        public string Data { get; set; }
        /// <summary>
        /// 标记写入的数据是订阅节点的还是自定义的
        /// </summary>
        public bool IsSubscribed {  get; set; }

        /// <summary>获取或设置订阅来源节点显示文本。</summary>
        public string Text1 { get; set; }

        /// <summary>获取或设置订阅来源结果显示文本。</summary>
        public string Text2 { get; set; }

        /// <summary>获取或设置Modbus寄存器起始地址。</summary>
        public string StartAddress { get; set; }

        /// <summary>获取或设置Modbus写入数据类型。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public RegistersType DataType { get; set; }
        
    }
}
