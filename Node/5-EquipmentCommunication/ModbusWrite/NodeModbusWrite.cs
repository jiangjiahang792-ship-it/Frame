using Logger;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite
{
    /// <summary>运行到节点时直接写入Modbus设备，不参与工件顺序排队。</summary>
    public class NodeModbusWrite : NodeBase
    {
        /// <summary>创建Modbus写入节点并初始化参数页与结果对象。</summary>
        public NodeModbusWrite(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            var form = new ParamFormModbusWrite();
            form.RunHandler += RunHandler;
            form.SetNodeBelong(this);
            ParamForm = form;
            Result = new NodeResultModbusWrite();
        }

        /// <summary>在参数页执行一次经过生产会话门禁保护的人工写入。</summary>
        private async Task RunHandler(object sender, EventArgs e)
        {
            ModbusWriteSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
            await Task.Run(() =>
            {
                bool executed = Solution.Instance.TryExecuteManualExternalSignal(() =>
                {
                    OrderedSignalSendResult result = ExecuteWriteSnapshotAsync(signalSnapshot)
                        .GetAwaiter()
                        .GetResult();
                    EnsureSuccessfulResult(result, CancellationToken.None);
                });
                if (!executed)
                    throw new InvalidOperationException("生产流程运行或方案重载期间不能执行Modbus人工写入。");
            }).ConfigureAwait(true);
        }

        /// <summary>运行节点并等待Modbus写入取得明确结果。</summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);
                OrderedSignalSendResult sendResult = await ExecuteDirectWriteAsync(token);
                EnsureSuccessfulResult(sendResult, token);

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                return new NodeReturn(NodeRunFlag.ContinueRun);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                SetRunResult(startTime, NodeStatus.Unexecuted);
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行失败！原因:{ex.Message}");
            }
        }

        /// <summary>冻结本次地址和值后直接写入；只保留设备通信锁，不等待前序工件，也不自动重试或复位。</summary>
        public Task<OrderedSignalSendResult> ExecuteDirectWriteAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ModbusWriteSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
            token.ThrowIfCancellationRequested();
            return ExecuteWriteSnapshotAsync(signalSnapshot);
        }

        /// <summary>只读取一次当前参数对象，并在写入前完成订阅值读取和类型解析。</summary>
        private ModbusWriteSignalSnapshot CaptureSignalSnapshot()
        {
            NodeParamModbusWrite paramSnapshot = ParamForm?.Params as NodeParamModbusWrite;
            if (paramSnapshot == null)
                throw new InvalidOperationException("Modbus写入参数尚未设置。");

            IModbus deviceSnapshot = paramSnapshot.Device;
            if (deviceSnapshot == null)
                throw new InvalidOperationException("Modbus写入设备尚未绑定。");
            if (!deviceSnapshot.IsConnect)
                throw new InvalidOperationException("Modbus设备尚未连接。");

            string addressSnapshot = NormalizeAddress(paramSnapshot.StartAddress);
            string rawValueSnapshot = paramSnapshot.IsSubscribed
                ? ((ParamFormModbusWrite)ParamForm).GetSubValue()
                : paramSnapshot.Data;
            if (string.IsNullOrWhiteSpace(rawValueSnapshot))
                throw new InvalidOperationException("Modbus写入值不能为空。");

            object parsedValue = ParseValueSnapshot(paramSnapshot.DataType, rawValueSnapshot.Trim());
            return new ModbusWriteSignalSnapshot(
                deviceSnapshot,
                CreateEndpointKey(deviceSnapshot),
                addressSnapshot,
                paramSnapshot.DataType,
                parsedValue);
        }

        /// <summary>执行一次真实写入并检查协议结果，连接内报文互斥由设备通信层负责。</summary>
        private static Task<OrderedSignalSendResult> ExecuteWriteSnapshotAsync(
            ModbusWriteSignalSnapshot signalSnapshot)
        {
            if (!signalSnapshot.Device.IsConnect)
            {
                return Task.FromResult(new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Rejected,
                    "Modbus设备在写入前已经断开连接。"));
            }

            switch (signalSnapshot.DataType)
            {
                case RegistersType.Bool:
                case RegistersType.线圈:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (bool[])signalSnapshot.Value);
                    break;
                case RegistersType.Short:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (short[])signalSnapshot.Value);
                    break;
                case RegistersType.UShort:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (ushort[])signalSnapshot.Value);
                    break;
                case RegistersType.Int:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (int[])signalSnapshot.Value);
                    break;
                case RegistersType.UInt:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (uint[])signalSnapshot.Value);
                    break;
                case RegistersType.Float:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (float[])signalSnapshot.Value);
                    break;
                case RegistersType.Double:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (double[])signalSnapshot.Value);
                    break;
                case RegistersType.Long:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (long[])signalSnapshot.Value);
                    break;
                case RegistersType.ULong:
                    signalSnapshot.Device.Write(signalSnapshot.Address, (ulong[])signalSnapshot.Value);
                    break;
                default:
                    throw new NotSupportedException($"Modbus写入不支持的数据类型：{signalSnapshot.DataType}");
            }

            string evidence =
                $"Modbus响应成功;端点={signalSnapshot.EndpointKey};地址={signalSnapshot.Address};类型={signalSnapshot.DataType}";
            return Task.FromResult(new OrderedSignalSendResult(
                OrderedSignalSendStatus.DeviceAcknowledged,
                "Modbus写入已取得协议成功响应。",
                evidence));
        }

        /// <summary>把协调器结果转换为节点运行结果。</summary>
        private static void EnsureSuccessfulResult(
            OrderedSignalSendResult sendResult,
            CancellationToken token)
        {
            if (sendResult.Status == OrderedSignalSendStatus.CancelledBeforeStart)
                throw new OperationCanceledException(sendResult.Message, token);
            if (sendResult.Status != OrderedSignalSendStatus.DeviceAcknowledged &&
                sendResult.Status != OrderedSignalSendStatus.LocalCallCompleted)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(sendResult.Message)
                        ? $"Modbus写入返回{sendResult.Status}。"
                        : sendResult.Message);
            }
        }

        /// <summary>验证参数页中的Modbus写入类型和值能否在生产前完整解析。</summary>
        public static void ValidateWriteValue(RegistersType dataType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException("Modbus写入值不能为空。");
            ParseValueSnapshot(dataType, value.Trim());
        }

        /// <summary>按目标寄存器类型解析成独立数组，避免排队期间再读取共享字符串。</summary>
        private static object ParseValueSnapshot(RegistersType dataType, string rawValue)
        {
            string[] values = SplitValues(rawValue);
            switch (dataType)
            {
                case RegistersType.Bool:
                case RegistersType.线圈:
                    return values.Select(ParseBooleanToken).ToArray();
                case RegistersType.Short:
                    return values.Select(value => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                case RegistersType.UShort:
                    return values.Select(value => ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                case RegistersType.Int:
                    return values.Select(value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                case RegistersType.UInt:
                    return values.Select(value => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                case RegistersType.Float:
                    return values.Select(ParseFiniteSingle).ToArray();
                case RegistersType.Double:
                    return values.Select(ParseFiniteDouble).ToArray();
                case RegistersType.Long:
                    return values.Select(value => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                case RegistersType.ULong:
                    return values.Select(value => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
                default:
                    throw new NotSupportedException($"Modbus写入不支持的数据类型：{dataType}");
            }
        }

        /// <summary>严格解析布尔值，只允许0、1、False和True。</summary>
        private static bool ParseBooleanToken(string value)
        {
            if (value == "1" || value.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value == "0" || value.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new FormatException($"无法把“{value}”解析为布尔值，只允许0、1、False或True。");
        }

        /// <summary>解析有限单精度值，拒绝NaN和无穷大。</summary>
        private static float ParseFiniteSingle(string value)
        {
            float parsed = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(parsed) || float.IsInfinity(parsed))
                throw new FormatException("Modbus单精度写入值必须是有限数值。");
            return parsed;
        }

        /// <summary>解析有限双精度值，拒绝NaN和无穷大。</summary>
        private static double ParseFiniteDouble(string value)
        {
            double parsed = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
                throw new FormatException("Modbus双精度写入值必须是有限数值。");
            return parsed;
        }

        /// <summary>按中英文逗号或分号拆分值，并在发送前拒绝空项。</summary>
        private static string[] SplitValues(string rawValue)
        {
            string[] values = rawValue.Split(new[] { ',', ';', '，' }, StringSplitOptions.None)
                .Select(value => value.Trim())
                .ToArray();
            if (values.Length == 0 || values.Any(string.IsNullOrEmpty))
                throw new FormatException("Modbus写入数组包含空值。");
            return values;
        }

        /// <summary>规范并验证Modbus寄存器起始地址。</summary>
        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new FormatException("Modbus起始地址不能为空。");
            ushort parsed = ushort.Parse(address.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>按通信方式和物理参数生成跨节点共享的稳定连接端点键。</summary>
        internal static string CreateEndpointKey(IModbus modbus)
        {
            if (modbus == null)
                throw new InvalidOperationException("Modbus写入设备尚未绑定。");
            if (modbus.ModbusParam is ModbusTcpParam tcpParam)
            {
                if (tcpParam.DevType == DevType.ModbusTcpSlave)
                    throw new InvalidOperationException("Modbus TCP从站不能作为写入节点的主动发送设备。");
                string ipAddress = tcpParam.IP?.Trim();
                if (string.IsNullOrWhiteSpace(ipAddress) || tcpParam.Port <= 0 || tcpParam.Port > 65535)
                    throw new InvalidOperationException("Modbus TCP物理地址或端口无效。");
                return WorkpieceExecutionContext.NormalizeKey(
                    $"MODBUS:TCP:{ipAddress}:{tcpParam.Port}",
                    nameof(modbus));
            }
            if (modbus.ModbusParam is ModbusRTUParam rtuParam)
            {
                string portName = rtuParam.PortName?.Trim();
                if (string.IsNullOrWhiteSpace(portName))
                    throw new InvalidOperationException("Modbus RTU物理串口名不能为空。");
                return WorkpieceExecutionContext.NormalizeKey(
                    $"MODBUS:RTU:{portName}",
                    nameof(modbus));
            }
            throw new InvalidOperationException("Modbus写入设备使用了不支持的通信参数。");
        }

        /// <summary>保存Modbus写入节点到达时冻结的全部动态值。</summary>
        private sealed class ModbusWriteSignalSnapshot
        {
            /// <summary>创建不可变Modbus写入快照。</summary>
            public ModbusWriteSignalSnapshot(
                IModbus device,
                string endpointKey,
                string address,
                RegistersType dataType,
                object value)
            {
                Device = device;
                EndpointKey = endpointKey;
                Address = address;
                DataType = dataType;
                Value = value;
            }

            /// <summary>获取冻结的Modbus设备对象。</summary>
            public IModbus Device { get; }

            /// <summary>获取冻结的物理连接端点键。</summary>
            public string EndpointKey { get; }

            /// <summary>获取冻结的寄存器起始地址。</summary>
            public string Address { get; }

            /// <summary>获取冻结的数据类型。</summary>
            public RegistersType DataType { get; }

            /// <summary>获取冻结并已解析的独立值数组。</summary>
            public object Value { get; }
        }
    }
}
