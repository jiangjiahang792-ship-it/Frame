using HslCommunication.Core.Device;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>基恩士 Nano OverTcp 适配器，保留既有类名以兼容已保存的方案。</summary>
    public sealed class PlcKeyenceNano : PlcKeyenceTcpBase
    {
        /// <summary>供方案反序列化使用，随后由设备列表恢复客户端。</summary>
        [JsonConstructor]
        public PlcKeyenceNano() { }

        /// <summary>创建 Nano OverTcp 设备，添加时不主动连接。</summary>
        public PlcKeyenceNano(PLCParms parms) : base(parms) { CreateDevice(); }

        /// <summary>创建基恩士 Nano 上位链路客户端。</summary>
        protected override DeviceTcpNet CreateClient(EthernetParms parms)
        {
            return new KeyenceNanoSerialOverTcp(parms.IP, parms.Port);
        }
    }
}