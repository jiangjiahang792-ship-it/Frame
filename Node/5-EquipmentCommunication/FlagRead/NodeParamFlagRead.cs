using Newtonsoft.Json;
using TDJS_Vision.Forms.GlobalSignalSettings;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    public class NodeParamFlagRead : INodeParam
    {
        /// <summary>
        /// 选择的监听信号唯一标识。
        /// </summary>
        public string SignalKey { get; set; }

        /// <summary>
        /// 界面显示的监听信号名称。
        /// </summary>
        public string SignalName { get; set; }

        /// <summary>
        /// 读取到信号后是否重置为 false。
        /// </summary>
        public bool Reset { get; set; } = false;

        /// <summary>
        /// 是否等待监听信号变为 true；旧方案默认等待以保持原有运行逻辑。
        /// </summary>
        public bool WaitForSignal { get; set; } = true;

        /// <summary>
        /// 运行时解析到的全局监听信号对象，不参与方案序列化。
        /// </summary>
        [JsonIgnore]
        public SingleGlobalSignalSettings Signal { get; set; }
    }
}
