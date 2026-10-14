using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Core.Device;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>基恩士 Nano OverTcp 适配器，布尔读写直接使用 HSL 原生接口。</summary>
    public sealed class PlcKeyenceNano : PlcKeyenceTcpBase
    {
        /// <summary>供方案反序列化使用，随后由设备列表恢复客户端。</summary>
        [JsonConstructor]
        public PlcKeyenceNano() { }

        /// <summary>创建 Nano OverTcp 设备，添加时不主动连接。</summary>
        public PlcKeyenceNano(PLCParms parms) : base(parms) { CreateDevice(); }

        /// <summary>创建与连续测试 demo 相同的上位链路客户端。</summary>
        protected override DeviceTcpNet CreateClient(EthernetParms parms)
        {
            return new KeyenceNanoSerialOverTcp(parms.IP, parms.Port);
        }

        /// <summary>直接读取布尔位，地址解析和错误信息由 HSL 提供。</summary>
        public override OperateResult<bool> ReadBool(string address) => Client.ReadBool(address);

        /// <summary>使用 HSL 原生异步接口读取布尔位。</summary>
        public override Task<OperateResult<bool>> ReadBoolAsync(string address) => Client.ReadBoolAsync(address);

        /// <summary>读取连续布尔位；节点的单点数组调用与 demo 的单点接口保持一致。</summary>
        public override OperateResult<bool[]> ReadBool(string address, ushort length)
        {
            if (length != 1) return Client.ReadBool(address, length);
            var result = ReadBool(address);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(new[] { result.Content })
                : OperateResult.CreateFailedResult<bool[]>(result);
        }

        /// <summary>异步读取连续布尔位，单点调用使用原生单点接口。</summary>
        public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
        {
            if (length != 1) return await Client.ReadBoolAsync(address, length).ConfigureAwait(false);
            var result = await ReadBoolAsync(address).ConfigureAwait(false);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(new[] { result.Content })
                : OperateResult.CreateFailedResult<bool[]>(result);
        }

        /// <summary>按地址顺序使用原生异步接口读取，失败时保留底层错误。</summary>
        public override async Task<OperateResult<bool[]>> ReadBoolAsync(string[] address)
        {
            if (address == null || address.Length == 0)
                return new OperateResult<bool[]>("读取地址不能为空！");
            for (int i = 0; i < address.Length; i++)
                if (string.IsNullOrWhiteSpace(address[i]))
                    return new OperateResult<bool[]>($"读取地址不能为空，索引：{i}！");
            var values = new bool[address.Length];
            for (int i = 0; i < address.Length; i++)
            {
                var result = await ReadBoolAsync(address[i]).ConfigureAwait(false);
                if (!result.IsSuccess) return OperateResult.CreateFailedResult<bool[]>(result);
                values[i] = result.Content;
            }
            return OperateResult.CreateSuccessResult(values);
        }

        /// <summary>直接写入布尔位；框架不再读取并回写整个 DM 字。</summary>
        public override OperateResult WriteBool(string address, bool value) => Client.Write(address, value);

        /// <summary>使用 HSL 原生异步接口写入布尔位。</summary>
        public override Task<OperateResult> WriteBoolAsync(string address, bool value) => Client.WriteAsync(address, value);

        /// <summary>直接写入连续布尔位，单元素数组使用原生单点写入。</summary>
        public override OperateResult WriteBool(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return new OperateResult("布尔写入数量必须在1到65535之间！");
            return value.Length == 1 ? Client.Write(address, value[0]) : Client.Write(address, value);
        }

        /// <summary>异步写入连续布尔位，单元素数组使用原生单点写入。</summary>
        public override Task<OperateResult> WriteBoolAsync(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return Task.FromResult(new OperateResult("布尔写入数量必须在1到65535之间！"));
            return value.Length == 1 ? Client.WriteAsync(address, value[0]) : Client.WriteAsync(address, value);
        }
    }
}