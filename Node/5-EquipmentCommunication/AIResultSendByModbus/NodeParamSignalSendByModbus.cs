using System.ComponentModel;
using Newtonsoft.Json;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Node._5_EquipmentCommunication.AIResultSendByModbus
{
    internal class NodeParamSignalSendByModbus : INodeParam
    {
        // 订阅的AI结果文本
        public string Text1;
        public string Text2;

        [JsonIgnore]
        public IModbus ModbusDevice;

        // Modbus设备名称
        public string ModbusName;

        // 信号列表绑定的数据
        public BindingList<DetectItemRow> BindingData;
    }
}
