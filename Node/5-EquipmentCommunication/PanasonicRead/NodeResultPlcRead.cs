using System.ComponentModel;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PanasonicRead
{
    internal class NodeResultPlcRead : INodeResult
    {
        public int RunTime { get; set; }
        [DisplayName("读取结果")]
        public PlcResult<bool, int, string, byte[]> ReadData { get; set; }

        [DisplayName("二维码字符串")]
        public string Code { get; set; }
    }
}
