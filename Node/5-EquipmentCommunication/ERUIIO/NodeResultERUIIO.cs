using System.ComponentModel;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public class NodeResultERUIIO : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 读取的8位输入/输出状态数组
        /// </summary>
        public bool[] ReadDatas { get; set; }
        /// <summary>
        /// 读取指定位状态
        /// </summary>
        public bool SpecifiedBit { get; set; }
    }
}
