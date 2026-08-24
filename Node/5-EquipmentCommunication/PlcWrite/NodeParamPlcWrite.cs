using Newtonsoft.Json;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte
{
    /// <summary>保存PLC写入节点的可编辑参数。</summary>
    public class NodeParamPlcWrite : INodeParam
    {
        /// <summary>获取或设置方案中的PLC对象。</summary>
        [JsonIgnore]
        public IPlc Plc { get; set; }

        /// <summary>获取或设置用于方案恢复的PLC名称。</summary>
        public string PlcName { get; set; }

        /// <summary>获取或设置PLC写入地址。</summary>
        public string Address { get; set; }

        /// <summary>获取或设置写入值的.NET类型名称。</summary>
        public string DataType { get; set; }

        /// <summary>获取或设置待解析的写入值文本。</summary>
        public string Value { get; set; }
    }
}
