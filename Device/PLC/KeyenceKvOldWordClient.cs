using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Profinet.Keyence;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>
    /// 当前库的旧系列 Read 忽略长度、Write 只使用前两个字节。
    /// 按连续 DM 字地址调用原协议，保证多字整数、浮点和字符串不会被截断。
    /// </summary>
    internal sealed class KeyenceKvOldWordClient : KeyenceKvOld
    {
        /// <summary>
        /// 本客户端的同步、异步和整字写入共用锁，防止读改写期间被另一个本地写入覆盖。
        /// 该锁不能阻止PLC程序或其他客户端同时修改同一DM字。
        /// </summary>
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);

        /// <summary>创建旧系列客户端，沿用库的字节转换和长连接管理。</summary>
        public KeyenceKvOldWordClient(string ip, int port) : base(ip, port) { }

        /// <summary>解析 DM100.1 这类字内位地址；位编号从 0 到 15。</summary>
        private static OperateResult<string, int> ParseWordBitAddress(string address)
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

        /// <summary>按低位在前的顺序从原始16位字中提取布尔值，不受数值显示格式影响。</summary>
        private static OperateResult<bool[]> ExtractBits(OperateResult<byte[]> read, int startBit, ushort length)
        {
            if (!read.IsSuccess)
                return OperateResult.CreateFailedResult<bool[]>(read);
            int requiredBytes = ((startBit + length + 15) / 16) * 2;
            if (read.Content == null || read.Content.Length < requiredBytes)
                return new OperateResult<bool[]>("DM位读取响应长度不足！");
            var values = new bool[length];
            for (int i = 0; i < values.Length; i++)
            {
                int bitIndex = startBit + i;
                values[i] = (read.Content[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
            }
            return OperateResult.CreateSuccessResult(values);
        }

        /// <summary>字内位先读取DM字再提取；普通继电器地址继续使用旧系列原协议。</summary>
        public override OperateResult<bool> ReadBool(string address)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBool(address);
            var result = ReadBool(address, 1);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(result.Content[0])
                : OperateResult.CreateFailedResult<bool>(result);
        }

        /// <summary>异步读取单个DM位或原有继电器位。</summary>
        public override async Task<OperateResult<bool>> ReadBoolAsync(string address)
        {
            if (address == null || !address.Contains("."))
                return await base.ReadBoolAsync(address).ConfigureAwait(false);
            var result = await ReadBoolAsync(address, 1).ConfigureAwait(false);
            return result.IsSuccess
                ? OperateResult.CreateSuccessResult(result.Content[0])
                : OperateResult.CreateFailedResult<bool>(result);
        }

        /// <summary>读取DM字内连续位，跨过第15位时继续读取下一个DM字。</summary>
        public override OperateResult<bool[]> ReadBool(string address, ushort length)
        {
            if (address == null || !address.Contains("."))
                return base.ReadBool(address, length);
            var parsed = ParseWordBitAddress(address);
            if (!parsed.IsSuccess)
                return OperateResult.CreateFailedResult<bool[]>(parsed);
            if (length == 0)
                return new OperateResult<bool[]>("DM位读取长度必须大于0！");
            ushort wordCount = (ushort)((parsed.Content2 + length + 15) / 16);
            return ExtractBits(Read(parsed.Content1, wordCount), parsed.Content2, length);
        }

        /// <summary>异步读取DM字内连续位，按实际覆盖范围读取需要的字。</summary>
        public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
        {
            if (address == null || !address.Contains("."))
                return await base.ReadBoolAsync(address, length).ConfigureAwait(false);
            var parsed = ParseWordBitAddress(address);
            if (!parsed.IsSuccess)
                return OperateResult.CreateFailedResult<bool[]>(parsed);
            if (length == 0)
                return new OperateResult<bool[]>("DM位读取长度必须大于0！");
            ushort wordCount = (ushort)((parsed.Content2 + length + 15) / 16);
            var read = await ReadAsync(parsed.Content1, wordCount).ConfigureAwait(false);
            return ExtractBits(read, parsed.Content2, length);
        }

        /// <summary>在已读取的字节中仅修改指定的位，其他位保持读取时的状态。</summary>
        private static void SetBits(byte[] bytes, int startBit, bool[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int bitIndex = startBit + i;
                int byteIndex = bitIndex / 8;
                byte mask = (byte)(1 << (bitIndex % 8));
                bytes[byteIndex] = values[i]
                    ? (byte)(bytes[byteIndex] | mask)
                    : (byte)(bytes[byteIndex] & ~mask);
            }
        }

        /// <summary>写入单个DM字内位；原有继电器写入继续使用库的置位和复位命令。</summary>
        public override OperateResult Write(string address, bool value)
        {
            return address != null && address.Contains(".")
                ? Write(address, new[] { value })
                : base.Write(address, value);
        }

        /// <summary>异步写入单个DM字内位或原有继电器位。</summary>
        public override Task<OperateResult> WriteAsync(string address, bool value)
        {
            return address != null && address.Contains(".")
                ? WriteAsync(address, new[] { value })
                : base.WriteAsync(address, value);
        }

        /// <summary>在同一个写入锁内读改写DM连续位；读取失败时绝不发出写入。</summary>
        public override OperateResult Write(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return new OperateResult("布尔写入数量必须在1到65535之间！");
            if (address == null || !address.Contains("."))
                return value.Length == 1 ? base.Write(address, value[0]) : base.Write(address, value);
            var parsed = ParseWordBitAddress(address);
            if (!parsed.IsSuccess)
                return parsed;
            ushort wordCount = (ushort)((parsed.Content2 + value.Length + 15) / 16);
            _writeGate.Wait();
            try
            {
                var read = Read(parsed.Content1, wordCount);
                if (!read.IsSuccess)
                    return read;
                SetBits(read.Content, parsed.Content2, value);
                return WriteWordsCore(parsed.Content1, read.Content);
            }
            finally { _writeGate.Release(); }
        }

        /// <summary>异步串行完成DM连续位的读取、修改和写回，不占用额外工作线程。</summary>
        public override async Task<OperateResult> WriteAsync(string address, bool[] value)
        {
            if (value == null || value.Length == 0 || value.Length > ushort.MaxValue)
                return new OperateResult("布尔写入数量必须在1到65535之间！");
            if (address == null || !address.Contains("."))
                return value.Length == 1
                    ? await base.WriteAsync(address, value[0]).ConfigureAwait(false)
                    : await base.WriteAsync(address, value).ConfigureAwait(false);
            var parsed = ParseWordBitAddress(address);
            if (!parsed.IsSuccess)
                return parsed;
            ushort wordCount = (ushort)((parsed.Content2 + value.Length + 15) / 16);
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var read = await ReadAsync(parsed.Content1, wordCount).ConfigureAwait(false);
                if (!read.IsSuccess)
                    return read;
                SetBits(read.Content, parsed.Content2, value);
                return await WriteWordsCoreAsync(parsed.Content1, read.Content).ConfigureAwait(false);
            }
            finally { _writeGate.Release(); }
        }

        /// <summary>生成单字请求地址；仅明确支持连续 DM 区的多字操作。</summary>
        private static OperateResult<string[]> GetWordAddresses(string address, int count)
        {
            if (string.IsNullOrWhiteSpace(address) || count <= 0)
                return new OperateResult<string[]>("读写地址和字数不能为空！");
            if (count == 1)
                return OperateResult.CreateSuccessResult(new[] { address });
            uint start;
            if (!address.StartsWith("DM", StringComparison.OrdinalIgnoreCase) ||
                !uint.TryParse(address.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out start) ||
                start > uint.MaxValue - (uint)(count - 1))
                return new OperateResult<string[]>("基恩士KV300/Older多字读写仅支持连续DM地址，例如DM100。");
            var addresses = new string[count];
            for (int i = 0; i < count; i++)
                addresses[i] = "DM" + (start + (uint)i).ToString(CultureInfo.InvariantCulture);
            return OperateResult.CreateSuccessResult(addresses);
        }

        /// <summary>逐字读取并拼接字节；协议库返回的无效字长必须判为失败。</summary>
        public override OperateResult<byte[]> Read(string address, ushort length)
        {
            var addresses = GetWordAddresses(address, length);
            if (!addresses.IsSuccess)
                return OperateResult.CreateFailedResult<byte[]>(addresses);
            var bytes = new byte[length * 2];
            for (int i = 0; i < length; i++)
            {
                var result = base.Read(addresses.Content[i], 1);
                if (!result.IsSuccess)
                    return OperateResult.CreateFailedResult<byte[]>(result);
                if (result.Content == null || result.Content.Length != 2)
                    return new OperateResult<byte[]>("基恩士旧系列单字响应长度错误！");
                Buffer.BlockCopy(result.Content, 0, bytes, i * 2, 2);
            }
            return OperateResult.CreateSuccessResult(bytes);
        }

        /// <summary>异步逐字读取，复用现有 TCP 长连接。</summary>
        public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
        {
            var addresses = GetWordAddresses(address, length);
            if (!addresses.IsSuccess)
                return OperateResult.CreateFailedResult<byte[]>(addresses);
            var bytes = new byte[length * 2];
            for (int i = 0; i < length; i++)
            {
                var result = await base.ReadAsync(addresses.Content[i], 1).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return OperateResult.CreateFailedResult<byte[]>(result);
                if (result.Content == null || result.Content.Length != 2)
                    return new OperateResult<byte[]>("基恩士旧系列单字响应长度错误！");
                Buffer.BlockCopy(result.Content, 0, bytes, i * 2, 2);
            }
            return OperateResult.CreateSuccessResult(bytes);
        }

        /// <summary>逐字写入，拒绝不完整字节以避免库静默截断数据。</summary>
        public override OperateResult Write(string address, byte[] value)
        {
            _writeGate.Wait();
            try { return WriteWordsCore(address, value); }
            finally { _writeGate.Release(); }
        }

        /// <summary>调用方持有写入锁时执行完整字写入，避免读改写重入同一锁。</summary>
        private OperateResult WriteWordsCore(string address, byte[] value)
        {
            if (value == null || value.Length == 0 || value.Length % 2 != 0)
                return new OperateResult("基恩士旧系列写入数据必须包含完整的16位字！");
            var addresses = GetWordAddresses(address, value.Length / 2);
            if (!addresses.IsSuccess)
                return addresses;
            var word = new byte[2];
            for (int i = 0; i < addresses.Content.Length; i++)
            {
                Buffer.BlockCopy(value, i * 2, word, 0, 2);
                var result = base.Write(addresses.Content[i], word);
                if (!result.IsSuccess)
                    return result;
            }
            return OperateResult.CreateSuccessResult();
        }

        /// <summary>异步逐字写入，不并行发送同一连接上的多个字。</summary>
        public override async Task<OperateResult> WriteAsync(string address, byte[] value)
        {
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try { return await WriteWordsCoreAsync(address, value).ConfigureAwait(false); }
            finally { _writeGate.Release(); }
        }

        /// <summary>调用方持有写入锁时异步执行完整字写入。</summary>
        private async Task<OperateResult> WriteWordsCoreAsync(string address, byte[] value)
        {
            if (value == null || value.Length == 0 || value.Length % 2 != 0)
                return new OperateResult("基恩士旧系列写入数据必须包含完整的16位字！");
            var addresses = GetWordAddresses(address, value.Length / 2);
            if (!addresses.IsSuccess)
                return addresses;
            var word = new byte[2];
            for (int i = 0; i < addresses.Content.Length; i++)
            {
                Buffer.BlockCopy(value, i * 2, word, 0, 2);
                var result = await base.WriteAsync(addresses.Content[i], word).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return result;
            }
            return OperateResult.CreateSuccessResult();
        }
    }
}
