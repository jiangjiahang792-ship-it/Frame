using TDJS_Vision.Device.Camera;
using Newtonsoft.Json;
using TDJS_Vision.Device.Modbus;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public class NodeParamERUIIO : INodeParam
    {
        /// <summary>
        /// Modbus对象
        /// </summary>
        [JsonIgnore]
        public IModbus Modbus { get; set; }
        /// <summary>
        /// Modbus名称
        /// </summary>
        public string ModbusName { get; set; }
        /// <summary>
        /// 选择的操作
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationType Operation { get; set; }
        /// <summary>
        /// 读取指定位
        /// </summary>
        public bool ReadSpecifiedBit {  get; set; }
        /// <summary>
        /// 指定位信号
        /// </summary>
        public uint IONumber { get; set; }
        /// <summary>
        /// 是否持续监听指定位信号
        /// </summary>
        public bool IsContinue { get; set; }
        /// <summary>
        /// 是否是固定信号
        /// </summary>
        public bool IsFixed { get; set; }
        /// <summary>
        /// 固定的信号值
        /// </summary>
        public bool[] FixedSignals { get; set; }
        /// <summary>
        /// 动态的信号设置
        /// </summary>
        public IOSettings IOSettings {  get; set; }

    }
    public enum OperationType
    {
        读取输入信号,
        读取输出信号,
        写入输出信号
    }
}
