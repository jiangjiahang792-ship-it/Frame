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

        [JsonIgnore]
        public SingleGlobalSignalSettings Signal { get; set; }
    }
}
