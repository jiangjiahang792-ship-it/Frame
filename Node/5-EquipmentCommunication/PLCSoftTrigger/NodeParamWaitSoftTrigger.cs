using JsonSubTypes;
using Newtonsoft.Json;
using System;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger
{
    /// <summary>PLC软触发监听节点参数，保存监听地址、读取类型和触发对比值。</summary>
    public class NodeParamWaitSoftTrigger : INodeParam
    {
        /// <summary>当前方案中的PLC设备实例，不参与方案序列化。</summary>
        [JsonIgnore]
        public IPlc Plc { get; set; }
        /// <summary>
        /// 用于PLC反序列化
        /// </summary>
        public string PlcName {  get; set; }
        /// <summary>
        /// 初始监听信号，有时需要更改监听信号的情况，
        /// 确保临时更改后不影响方案中节点的监听初始地址
        /// </summary>
        public string InitAddress { get; set; }
        public string Address { get; set; }
        /// <summary>读取的数据类型名称，旧方案默认按布尔触发兼容。</summary>
        public string DataType { get; set; } = typeof(bool).Name;
        /// <summary>触发条件的对比值，读取值等于该值时节点通过。</summary>
        public string CompareValue { get; set; } = "True";
        /// <summary>字符串读取长度，仅数据类型为字符串时使用。</summary>
        public ushort StringLength { get; set; } = 1;
        /// <summary>监听到触发后是否复位布尔地址。</summary>
        public bool Reset { get; set; }

    }
}
