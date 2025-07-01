using System.ComponentModel;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead
{
    internal class NodeResultModbusRead : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("读取结果")]
        public object ReadData { get; set; }
    }
}
