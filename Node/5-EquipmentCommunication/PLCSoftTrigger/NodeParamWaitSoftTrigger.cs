using JsonSubTypes;
using Newtonsoft.Json;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger
{
    public class NodeParamWaitSoftTrigger : INodeParam
    {
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
        public bool Reset { get; set; }

    }
}
