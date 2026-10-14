using System;
using System.Globalization;
using System.Threading.Tasks;
using HslCommunication;
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

        /// <summary>解析 DM20000.0 这类字内位地址；位编号从 0 到 15。</summary>
        private static OperateResult<string, int> ParseDmBitAddress(string address)
        {
            string text = address == null ? string.Empty : address.Trim();
            int separator = text.LastIndexOf('.');
            uint word;
            int bit;
            if (separator <= 2 || !text.StartsWith("DM", StringComparison.OrdinalIgnoreCase) ||
                !uint.TryParse(text.Substring(2, separator - 2), NumberStyles.None, CultureInfo.InvariantCulture, out word) ||
                !int.TryParse(text.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out bit) ||
                bit < 0 || bit > 15)
                return new OperateResult<string, int>("DM位地址格式错误，请使用DM100.1这类格式，位编号为0到15！");
            return OperateResult.CreateSuccessResult("DM" + word.ToString(CultureInfo.InvariantCulture), bit);
        }

        /// <summary>从读取到的 UInt16 字数组中提取连续位。</summary>
        private static OperateResult<bool[]> ExtractBits(OperateResult<ushort[]> read, int startBit, ushort length)
        {
            if (!read.IsSuccess)
                return OperateResult.CreateFailedResult<bool[]>(read);
            if (read.Content == null || read.Content.Length == 0)
                return new OperateResult<bool[]>("DM位读取响应长度不足！");
            int requiredWords = (startBit + length + 15) / 16;
            if (read.Content.Length < requiredWords)
                return new OperateResult<bool[]>("DM位读取响应字数不足！");
            var values = new bool[length];
            for (int i = 0; i < values.Length; i++)
            {
                int bitIndex = startBit + i;
                values[i] = (read.Content[bitIndex / 16] & (1 << (bitIndex % 16))) != 0;
            }
            return OperateResult.CreateSuccessResult(values);
        }

        /// <summary>读取单个 DM 字内位；其他地址继续交给 HSL 原生布尔读取。</summary>
        public override OperateResult<bool> ReadBool(string address)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBool(address);
            var result = ReadBool(address, 1);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(result.Content[0])
                : OperateResult.CreateFailedResult<bool>(result);
        }

        /// <summary>异步读取单个 DM 字内位；其他地址继续交给 HSL 原生布尔读取。</summary>
        public override Task<OperateResult<bool>> ReadBoolAsync(string address)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBoolAsync(address);
            return Task.FromResult(ReadBool(address));
        }

        /// <summary>读取 DM 字内连续位，跨过第 15 位时继续读取下一个 DM 字。</summary>
        public override OperateResult<bool[]> ReadBool(string address, ushort length)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBool(address, length);
            var parsed = ParseDmBitAddress(address);
            if (!parsed.IsSuccess)
                return OperateResult.CreateFailedResult<bool[]>(parsed);
            if (length == 0)
                return new OperateResult<bool[]>("DM位读取长度必须大于0！");
            ushort wordCount = (ushort)((parsed.Content2 + length + 15) / 16);
            return ExtractBits(Client.ReadUInt16(parsed.Content1, wordCount), parsed.Content2, length);
        }

        /// <summary>异步读取 DM 字内连续位，按实际覆盖范围读取需要的字。</summary>
        public override Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBoolAsync(address, length);
            return Task.FromResult(ReadBool(address, length));
        }

        /// <summary>按输入顺序读取离散布尔地址，确保 DM 字内位地址也走兼容逻辑。</summary>
        public override OperateResult<bool[]> ReadBool(string[] address)
        {
            if (address == null || address.Length == 0)
                return new OperateResult<bool[]>("读取地址不能为空！");
            var values = new bool[address.Length];
            for (int i = 0; i < address.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(address[i]))
                    return new OperateResult<bool[]>($"读取地址不能为空，索引：{i}！");
                var result = ReadBool(address[i]);
                if (!result.IsSuccess)
                    return OperateResult.CreateFailedResult<bool[]>(result);
                values[i] = result.Content;
            }
            return OperateResult.CreateSuccessResult(values);
        }

        /// <summary>异步按输入顺序读取离散布尔地址，确保 DM 字内位地址也走兼容逻辑。</summary>
        public override Task<OperateResult<bool[]>> ReadBoolAsync(string[] address)
        {
            return Task.FromResult(ReadBool(address));
        }

        /// <summary>直接调用 HSL 布尔写入；DM 字内位的具体写入方式由库实现。</summary>
        public override OperateResult WriteBool(string address, bool value) => Client.Write(address, value);

        /// <summary>直接调用 HSL 原生异步布尔写入，不添加框架写锁。</summary>
        public override Task<OperateResult> WriteBoolAsync(string address, bool value) => Client.WriteAsync(address, value);

        /// <summary>直接写入布尔数组，单元素使用与单点一致的原生接口。</summary>
        public override OperateResult WriteBool(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return new OperateResult("布尔写入数量必须在1到65535之间！");
            return value.Length == 1 ? Client.Write(address, value[0]) : Client.Write(address, value);
        }

        /// <summary>直接异步写入布尔数组，保留底层接口的失败结果。</summary>
        public override Task<OperateResult> WriteBoolAsync(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return Task.FromResult(new OperateResult("布尔写入数量必须在1到65535之间！"));
            return value.Length == 1 ? Client.WriteAsync(address, value[0]) : Client.WriteAsync(address, value);
        }
    }
}