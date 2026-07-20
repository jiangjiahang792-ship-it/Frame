using Newtonsoft.Json;
using TDJS_Vision.Device.COM;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ComSend
{
    public class NodeParamComSend : INodeParam
    {
        [JsonIgnore]
        public ComDevice Dev { get; set; }
        public string DevName { get; set; }
        public string Encoding { get; set; }
        public string Cmd { get; set; }
    }
}
