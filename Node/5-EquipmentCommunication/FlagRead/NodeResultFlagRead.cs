using System.ComponentModel;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    public class NodeResultFlagRead : INodeResult
    {
        public int RunTime { get; set; }

        [SubscriptionOutput]
        [DisplayName("读取结果")]
        public FlagReadResult ReadResult { get; set; } = new FlagReadResult(false);
    }

    /// <summary>
    /// 监听信号读取结果。
    /// </summary>
    public class FlagReadResult
    {
        public object Data;
        public string DataType;

        public FlagReadResult(bool data)
        {
            Data = data;
            DataType = typeof(bool).Name;
        }
    }
}
