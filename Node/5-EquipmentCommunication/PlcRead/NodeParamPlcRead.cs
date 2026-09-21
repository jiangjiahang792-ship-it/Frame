using System;
using Newtonsoft.Json;
using TDJS_Vision.Device.PLC;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    /// <summary>PLC读取节点配置，保存到方案并恢复到参数界面。</summary>
    public class NodeParamPlcRead : INodeParam
    {
        /// <summary>当前方案的PLC设备实例，不重复序列化。</summary>
        [JsonIgnore]
        public IPlc Plc { get; set; }
        /// <summary>用于恢复设备引用的名称。</summary>
        public string PlcName { get; set; }
        /// <summary>字符串读取长度；指定高低位时以16位寄存器字数计。</summary>
        public ushort Length { get; set; } = 0;
        /// <summary>起始地址，多个地址使用短横线分隔。</summary>
        public string Address { get; set; } // 多个地址使用-间隔
        /// <summary>读取的数据类型名称。</summary>
        public string DataType { get; set; }
        /// <summary>字符串字节顺序；旧方案未保存该字段时保持设备默认读取行为。</summary>
        public PlcStringByteOrder StringByteOrder { get; set; } = PlcStringByteOrder.DeviceDefault;
    }
}
