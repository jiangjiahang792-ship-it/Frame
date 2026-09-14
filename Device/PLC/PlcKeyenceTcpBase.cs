using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HslCommunication;
using HslCommunication.Core.Device;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TDJS_Vision.Device.PLC
{
    /// <summary>将基恩士上位链路 TCP 协议适配为项目统一的 PLC 设备接口。</summary>
    public abstract class PlcKeyenceTcpBase : ReconnectingCommunicationDevice, IPlc, IResultSendTypedPlc
    {
        /// <summary>当前设备独享的通信客户端，复用长连接并由通信库串行处理报文。</summary>
        private DeviceTcpNet _client;

        /// <summary>按底层协议字节序写入结果发送节点的额外数值类型。</summary>
        public Task<OperateResult> WriteTypedValuesAsync(string address, Array values)
        {
            if (values is short[] signedWords) return _client.WriteAsync(address, signedWords);
            if (values is ushort[] words) return _client.WriteAsync(address, words);
            if (values is uint[] integers) return _client.WriteAsync(address, integers);
            if (values is double[] doubles) return _client.WriteAsync(address, doubles);
            throw new NotSupportedException("不支持的PLC扩展写入类型。");
        }

        /// <summary>连接与接收的有限超时时间，单位为毫秒。</summary>
        private int _operationTimeoutMs = 5000;

        /// <summary>设备保存和恢复使用的通信参数。</summary>
        public PLCParms PLCParms { get; set; }

        /// <inheritdoc />
        protected override string CommunicationName => UserDefinedName;
        /// <inheritdoc />
        protected override DeviceCommunication CommunicationClient => _client;

        /// <summary>单次连接和收发超时，限制在 100 到 60000 毫秒。</summary>
        public int OperationTimeoutMs
        {
            get => _operationTimeoutMs;
            set
            {
                _operationTimeoutMs = Math.Max(100, Math.Min(60000, value));
                ApplyOperationTimeout();
            }
        }

        /// <summary>设备名称。</summary>
        public string DevName { get; set; }

        /// <summary>设备品牌。</summary>
        public DeviceBrand Brand { get; set; } = DeviceBrand.Keyence;

        /// <summary>用于设备列表和节点选择的用户自定义名称。</summary>
        public string UserDefinedName { get; set; }

        /// <summary>统一设备分类。</summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public DevType DevType { get; set; } = DevType.PLC;

        /// <summary>供现有方案反序列化逻辑定位具体设备类型。</summary>
        public string ClassName { get; set; }


        /// <summary>反序列化构造函数；通信客户端在 CreateDevice 中恢复。</summary>

        protected PlcKeyenceTcpBase() { ClassName = GetType().FullName; }

        /// <summary>创建指定参数的基恩士设备，添加时不主动连接 PLC。</summary>
        protected PlcKeyenceTcpBase(PLCParms parms) : this()
        {
            PLCParms = parms;
            DevName = parms.UserDefinedName;
            UserDefinedName = parms.UserDefinedName;
        }

        /// <summary>根据保存参数恢复客户端；保留保存的连接意图供设备列表重连。</summary>
        public void CreateDevice()
        {
            if (PLCParms.PlcConType != PlcConType.ETHERNET)
                throw new NotSupportedException("基恩士上位链路仅支持网口通信！");
            if (!IPAddress.TryParse(PLCParms.EthernetParms.IP, out var ip) ||
                ip.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("基恩士PLC的IP地址无效！");
            if (PLCParms.EthernetParms.Port < 1 || PLCParms.EthernetParms.Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(PLCParms), "基恩士PLC端口必须在1到65535之间！");

            _client?.Dispose();
            _client = CreateClient(PLCParms.EthernetParms);
            ApplyOperationTimeout();
        }

        /// <summary>由具体协议适配器创建通信客户端，便于扩展不同的基恩士协议。</summary>
        protected abstract DeviceTcpNet CreateClient(EthernetParms parms);

        /// <summary>建立长连接并同步设备列表中的连接状态。</summary>
        public bool Connect()
        {
            return ConnectWithRecovery();
        }

        /// <inheritdoc />
        protected override OperateResult OpenCommunicationCore()
        {
            if (_client == null)
                CreateDevice();
            return _client.ConnectServer();
        }

        /// <summary>关闭通信连接并通知设备列表。</summary>
        public void Disconnect()
        {
            DisconnectWithRecovery();
        }

        /// <inheritdoc />
        protected override void CloseCommunicationCore() => _client?.ConnectClose();

        /// <summary>释放底层连接资源。</summary>
        public void Release()
        {
            DisconnectWithRecovery();
            _client?.Dispose();
            _client = null;
            IsConnect = false;
        }

        /// <summary>将统一超时设置应用到基恩士客户端。</summary>
        private void ApplyOperationTimeout()
        {
            if (_client == null)
                return;
            _client.ConnectTimeOut = _operationTimeoutMs;
            _client.ReceiveTimeOut = _operationTimeoutMs;
        }

        /// <summary>
        /// 按请求顺序读取离散地址。地址跨区域或位地址跨进位时不能套用三菱的数值差批量算法；
        /// 连续地址的高效批量读取由带长度的重载直接交给基恩士通信库处理。
        /// </summary>
        private static OperateResult<T[]> ReadAddresses<T>(string[] addresses, Func<string, OperateResult<T>> read)
        {
            if (addresses == null || addresses.Length == 0)
                return new OperateResult<T[]>("读取地址不能为空！");
            var values = new T[addresses.Length];
            for (int i = 0; i < addresses.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(addresses[i]))
                    return new OperateResult<T[]>($"读取地址不能为空，索引：{i}！");
            }
            for (int i = 0; i < addresses.Length; i++)
            {
                var result = read(addresses[i]);
                if (!result.IsSuccess)
                    return OperateResult.CreateFailedResult<T[]>(result);
                values[i] = result.Content;
            }
            return OperateResult.CreateSuccessResult(values);
        }

        /// <summary>异步按序读取离散地址，复用同一连接并在首个失败处返回库的错误信息。</summary>
        private static async Task<OperateResult<T[]>> ReadAddressesAsync<T>(
            string[] addresses, Func<string, Task<OperateResult<T>>> read)
        {
            if (addresses == null || addresses.Length == 0)
                return new OperateResult<T[]>("读取地址不能为空！");
            var values = new T[addresses.Length];
            for (int i = 0; i < addresses.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(addresses[i]))
                    return new OperateResult<T[]>($"读取地址不能为空，索引：{i}！");
            }
            for (int i = 0; i < addresses.Length; i++)
            {
                var result = await read(addresses[i]).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return OperateResult.CreateFailedResult<T[]>(result);
                values[i] = result.Content;
            }
            return OperateResult.CreateSuccessResult(values);
        }
        /// <inheritdoc />
        public OperateResult<byte[]> ReadBytes(string address, ushort length) => _client.Read(address, length);

        /// <inheritdoc />
        public Task<OperateResult<byte[]>> ReadBytesAsync(string address, ushort length) => _client.ReadAsync(address, length);

        /// <inheritdoc />
        public OperateResult<bool> ReadBool(string address) => _client.ReadBool(address);

        /// <inheritdoc />
        public Task<OperateResult<bool>> ReadBoolAsync(string address) => _client.ReadBoolAsync(address);

        /// <inheritdoc />
        public virtual OperateResult<bool[]> ReadBool(string address, ushort length) => _client.ReadBool(address, length);

        /// <inheritdoc />
        public virtual Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length) => _client.ReadBoolAsync(address, length);

        /// <inheritdoc />
        public OperateResult<bool[]> ReadBool(string[] address) => ReadAddresses<bool>(address, item => _client.ReadBool(item));

        /// <inheritdoc />
        public Task<OperateResult<bool[]>> ReadBoolAsync(string[] address) => ReadAddressesAsync<bool>(address, item => _client.ReadBoolAsync(item));

        /// <inheritdoc />
        public OperateResult<short> ReadInt16(string address) => _client.ReadInt16(address);

        /// <inheritdoc />
        public Task<OperateResult<short>> ReadInt16Async(string address) => _client.ReadInt16Async(address);

        /// <inheritdoc />
        public OperateResult<int> ReadInt32(string address) => _client.ReadInt32(address);

        /// <inheritdoc />
        public Task<OperateResult<int>> ReadInt32Async(string address) => _client.ReadInt32Async(address);

        /// <inheritdoc />
        public OperateResult<long> ReadInt64(string address) => _client.ReadInt64(address);

        /// <inheritdoc />
        public Task<OperateResult<long>> ReadInt64Async(string address) => _client.ReadInt64Async(address);

        /// <inheritdoc />
        public OperateResult<short[]> ReadInt16(string address, ushort length) => _client.ReadInt16(address, length);

        /// <inheritdoc />
        public Task<OperateResult<short[]>> ReadInt16Async(string address, ushort length) => _client.ReadInt16Async(address, length);

        /// <inheritdoc />
        public OperateResult<int[]> ReadInt32(string address, ushort length) => _client.ReadInt32(address, length);

        /// <inheritdoc />
        public Task<OperateResult<int[]>> ReadInt32Async(string address, ushort length) => _client.ReadInt32Async(address, length);

        /// <inheritdoc />
        public OperateResult<long[]> ReadInt64(string address, ushort length) => _client.ReadInt64(address, length);

        /// <inheritdoc />
        public Task<OperateResult<long[]>> ReadInt64Async(string address, ushort length) => _client.ReadInt64Async(address, length);

        /// <inheritdoc />
        public OperateResult<short[]> ReadInt16(string[] address) => ReadAddresses<short>(address, item => _client.ReadInt16(item));

        /// <inheritdoc />
        public Task<OperateResult<short[]>> ReadInt16Async(string[] address) => ReadAddressesAsync<short>(address, item => _client.ReadInt16Async(item));

        /// <inheritdoc />
        public OperateResult<int[]> ReadInt32(string[] address) => ReadAddresses<int>(address, item => _client.ReadInt32(item));

        /// <inheritdoc />
        public Task<OperateResult<int[]>> ReadInt32Async(string[] address) => ReadAddressesAsync<int>(address, item => _client.ReadInt32Async(item));

        /// <inheritdoc />
        public OperateResult<long[]> ReadInt64(string[] address) => ReadAddresses<long>(address, item => _client.ReadInt64(item));

        /// <inheritdoc />
        public Task<OperateResult<long[]>> ReadInt64Async(string[] address) => ReadAddressesAsync<long>(address, item => _client.ReadInt64Async(item));

        /// <inheritdoc />
        public OperateResult<float> ReadFloat(string address) => _client.ReadFloat(address);

        /// <inheritdoc />
        public Task<OperateResult<float>> ReadFloatAsync(string address) => _client.ReadFloatAsync(address);

        /// <inheritdoc />
        public OperateResult<float[]> ReadFloat(string address, ushort length) => _client.ReadFloat(address, length);

        /// <inheritdoc />
        public Task<OperateResult<float[]>> ReadFloatAsync(string address, ushort length) => _client.ReadFloatAsync(address, length);

        /// <inheritdoc />
        public OperateResult<string> ReadString(string address, ushort length) => _client.ReadString(address, length);

        /// <inheritdoc />
        public Task<OperateResult<string>> ReadStringAsync(string address, ushort length) => _client.ReadStringAsync(address, length);

        /// <inheritdoc />
        public OperateResult WriteBytes(string address, byte[] value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteBytesAsync(string address, byte[] value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteBool(string address, bool value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteBoolAsync(string address, bool value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteBool(string address, bool[] value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteBoolAsync(string address, bool[] value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteInt(string address, int value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteIntAsync(string address, int value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteInt(string address, int[] value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteIntAsync(string address, int[] value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteFloat(string address, float value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteFloatAsync(string address, float value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteFloat(string address, float[] value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteFloatAsync(string address, float[] value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public OperateResult WriteString(string address, string value) => _client.Write(address, value);

        /// <inheritdoc />
        public Task<OperateResult> WriteStringAsync(string address, string value) => _client.WriteAsync(address, value);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, bool waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, short waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, ushort waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, int waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, uint waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, long waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

        /// <inheritdoc />
        public Task<OperateResult<TimeSpan>> WaitAsync(string address, ulong waitValue, int readInterval = 100, int waitTimeout = -1) => _client.WaitAsync(address, waitValue, readInterval, waitTimeout);

    }
}
