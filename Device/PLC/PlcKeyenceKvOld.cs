using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>将基恩士 KV300/Older 上位链路协议适配为统一 PLC 设备。</summary>
    public sealed class PlcKeyenceKvOld : PlcKeyenceTcpBase
    {
        /// <summary>供方案反序列化使用，随后由设备列表恢复客户端。</summary>
        [JsonConstructor]
        public PlcKeyenceKvOld() { }

        /// <summary>创建旧系列基恩士设备，添加时不主动连接。</summary>
        public PlcKeyenceKvOld(PLCParms parms) : base(parms) { CreateDevice(); }

        /// <summary>使用 DLL 中独立的旧系列协议客户端。</summary>
        protected override DeviceTcpNet CreateClient(EthernetParms parms)
        {
            return new KeyenceKvOldWordClient(parms.IP, parms.Port);
        }

        /// <summary>适配现有 PLC 读取节点的单点数组接口；旧系列库提供独立的单个位读取方法。</summary>
        public override OperateResult<bool[]> ReadBool(string address, ushort length)
        {
            if (length != 1)
                return base.ReadBool(address, length);
            var result = ReadBool(address);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(new[] { result.Content })
                : OperateResult.CreateFailedResult<bool[]>(result);
        }

        /// <summary>异步适配单个位读取，保留库返回的协议错误信息。</summary>
        public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
        {
            if (length != 1)
                return await base.ReadBoolAsync(address, length).ConfigureAwait(false);
            var result = await ReadBoolAsync(address).ConfigureAwait(false);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(new[] { result.Content })
                : OperateResult.CreateFailedResult<bool[]>(result);
        }
    }
}