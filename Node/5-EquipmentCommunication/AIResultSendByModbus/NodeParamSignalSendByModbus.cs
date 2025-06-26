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

        // 是否发送到寄存器
        public bool IsRegister;
        
        // 信号自动重置
        public bool IsAutoReset;

        // 信号保持时间
        public int SignalHoldTime;

        // 信号列表绑定的数据
        public BindingList<DetectItemRow> BindingData;
    }
}
