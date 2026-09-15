using HslCommunication;
using Logger;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte
{
    /// <summary>到达节点后直接写入PLC，不参与工件顺序排队。</summary>
    public class NodePlcWrite : NodeBase
    {
        /// <summary>创建PLC写入节点并初始化参数与结果对象。</summary>
        public NodePlcWrite(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new ParamFormPlcWrite();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultPlcWrite();
        }

        /// <summary>运行节点并等待PLC写入取得明确结果。</summary>
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

        /// <summary>冻结并预检本轮写入值，直接调用设备，不等待工件或端点发送权。</summary>
        public Task<OrderedSignalSendResult> ExecuteDirectWriteAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PlcWriteSignalSnapshot signalSnapshot = CaptureSignalSnapshot();
            token.ThrowIfCancellationRequested();
            return ExecuteWriteSnapshotAsync(signalSnapshot);
        }

        /// <summary>只读取一次当前参数对象，并在发送前完成全部值解析。</summary>
        private PlcWriteSignalSnapshot CaptureSignalSnapshot()
        {
            NodeParamPlcWrite paramSnapshot = ParamForm?.Params as NodeParamPlcWrite;
            if (paramSnapshot == null)
                throw new InvalidOperationException("PLC写入参数尚未设置。");

            IPlc plcSnapshot = paramSnapshot.Plc;
            if (plcSnapshot == null)
                throw new InvalidOperationException("PLC写入设备尚未绑定。");
            if (!plcSnapshot.IsConnect)
                throw new InvalidOperationException("PLC设备尚未连接。");
            string addressSnapshot = paramSnapshot.Address?.Trim();
            if (string.IsNullOrWhiteSpace(addressSnapshot))
                throw new InvalidOperationException("PLC写入地址不能为空。");
            string rawValueSnapshot = paramSnapshot.Value?.Trim();
            if (string.IsNullOrEmpty(rawValueSnapshot))
                throw new InvalidOperationException("PLC写入值不能为空。");

            PlcWriteValueKind valueKind;
            object parsedValue = ParseValueSnapshot(
                paramSnapshot.DataType,
                rawValueSnapshot,
                out valueKind);
            return new PlcWriteSignalSnapshot(
                plcSnapshot,
                CreateEndpointKey(plcSnapshot),
                addressSnapshot,
                valueKind,
                parsedValue);
        }

        /// <summary>使用冻结的参数执行一次真实写入，等待协议结果，不自动重试。</summary>
        private static async Task<OrderedSignalSendResult> ExecuteWriteSnapshotAsync(
            PlcWriteSignalSnapshot signalSnapshot)
        {
            if (!signalSnapshot.Plc.IsConnect)
            {
                return new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Rejected,
                    "PLC在实际写入前已经断开连接。");
            }

            OperateResult result;
            switch (signalSnapshot.ValueKind)
            {
                case PlcWriteValueKind.BooleanValues:
                    result = await signalSnapshot.Plc.WriteBoolAsync(
                        signalSnapshot.Address,
                        (bool[])signalSnapshot.Value).ConfigureAwait(false);
                    break;
                case PlcWriteValueKind.Int32:
                    result = await signalSnapshot.Plc.WriteIntAsync(
                        signalSnapshot.Address,
                        (int)signalSnapshot.Value).ConfigureAwait(false);
                    break;
                case PlcWriteValueKind.Int32Values:
                    result = await signalSnapshot.Plc.WriteIntAsync(
                        signalSnapshot.Address,
                        (int[])signalSnapshot.Value).ConfigureAwait(false);
                    break;
                case PlcWriteValueKind.Single:
                    result = await signalSnapshot.Plc.WriteFloatAsync(
                        signalSnapshot.Address,
                        (float)signalSnapshot.Value).ConfigureAwait(false);
                    break;
                case PlcWriteValueKind.SingleValues:
                    result = await signalSnapshot.Plc.WriteFloatAsync(
                        signalSnapshot.Address,
                        (float[])signalSnapshot.Value).ConfigureAwait(false);
                    break;
                case PlcWriteValueKind.String:
                    result = await signalSnapshot.Plc.WriteStringAsync(
                        signalSnapshot.Address,
                        (string)signalSnapshot.Value).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("PLC写入快照包含未知数据类型。");
            }

            if (result == null)
            {
                return new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Unknown,
                    "PLC写入API返回空结果，无法确认外部副作用。");
            }
            if (!result.IsSuccess)
            {
                return new OrderedSignalSendResult(
                    OrderedSignalSendStatus.Unknown,
                    $"PLC写入未取得成功响应：错误码={result.ErrorCode}，原因={result.Message}");
            }

            string evidence =
                $"HSL响应成功;错误码={result.ErrorCode};端点={signalSnapshot.EndpointKey};地址={signalSnapshot.Address}";
            return new OrderedSignalSendResult(
                OrderedSignalSendStatus.DeviceAcknowledged,
                "PLC写入已取得协议成功响应。",
                evidence);
        }

        /// <summary>把实际设备写入结果转换为节点运行结果。</summary>
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
                        ? $"PLC写入返回{sendResult.Status}。"
                        : sendResult.Message);
            }
        }

        /// <summary>按数据类型解析并冻结本轮写入值。</summary>
        private static object ParseValueSnapshot(
            string dataType,
            string rawValue,
            out PlcWriteValueKind valueKind)
        {
            if (dataType == typeof(bool).Name)
            {
                valueKind = PlcWriteValueKind.BooleanValues;
                return ParseBooleanValues(rawValue);
            }
            if (dataType == typeof(int).Name)
            {
                string[] tokens = SplitNumericValues(rawValue);
                if (tokens.Length == 1)
                {
                    valueKind = PlcWriteValueKind.Int32;
                    return int.Parse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                valueKind = PlcWriteValueKind.Int32Values;
                return tokens.Select(value =>
                    int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
            }
            if (dataType == typeof(float).Name)
            {
                string[] tokens = SplitNumericValues(rawValue);
                if (tokens.Length == 1)
                {
                    valueKind = PlcWriteValueKind.Single;
                    return ParseFiniteSingle(tokens[0]);
                }
                valueKind = PlcWriteValueKind.SingleValues;
                return tokens.Select(ParseFiniteSingle).ToArray();
            }
            if (dataType == typeof(string).Name)
            {
                valueKind = PlcWriteValueKind.String;
                return rawValue;
            }
            throw new InvalidOperationException($"PLC写入不支持的数据类型：{dataType}");
        }

        /// <summary>把布尔文本严格解析为至少一个布尔值。</summary>
        private static bool[] ParseBooleanValues(string rawValue)
        {
            string[] tokens = SplitValues(rawValue, true);
            return tokens.Select(ParseBooleanToken).ToArray();
        }

        /// <summary>解析单个布尔文本，非法值必须在发送前被拒绝。</summary>
        private static bool ParseBooleanToken(string value)
        {
            if (value == "1" || value.Equals("True", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value == "0" || value.Equals("False", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new FormatException($"无法把“{value}”解析为布尔值，只允许0、1、False或True。");
        }

        /// <summary>拆分整数或浮点数组，并保留单个负数的负号。</summary>
        private static string[] SplitNumericValues(string rawValue)
        {
            return SplitValues(rawValue, false);
        }

        /// <summary>解析PLC支持的有限单精度值，拒绝NaN和无穷大。</summary>
        private static float ParseFiniteSingle(string value)
        {
            float parsed = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(parsed) || float.IsInfinity(parsed))
                throw new FormatException("PLC浮点写入值必须是有限数值。");
            return parsed;
        }

        /// <summary>按逗号、分号、中文逗号或允许的旧连字符拆分值并拒绝空项。</summary>
        private static string[] SplitValues(string rawValue, bool allowLegacyHyphen)
        {
            char[] separators = allowLegacyHyphen
                ? new[] { ',', ';', '，', '-' }
                : new[] { ',', ';', '，' };
            string[] tokens = rawValue.Split(separators, StringSplitOptions.None)
                .Select(value => value.Trim())
                .ToArray();
            if (tokens.Length == 0 || tokens.Any(string.IsNullOrEmpty))
                throw new FormatException("PLC写入数组包含空值。");
            return tokens;
        }

        /// <summary>将旧连字符布尔数组文本严格转换为布尔数组。</summary>
        public static bool[] ConvertToBooleanArray(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new bool[0];
            return ParseBooleanValues(input.Trim());
        }

        /// <summary>验证参数页中的PLC写入类型和值能否在生产前完整解析。</summary>
        public static void ValidateWriteValue(string dataType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new FormatException("PLC写入值不能为空。");
            PlcWriteValueKind ignoredKind;
            ParseValueSnapshot(dataType, value.Trim(), out ignoredKind);
        }

        /// <summary>按PLC品牌和物理通信参数生成跨节点共享的稳定连接端点键。</summary>
        internal static string CreateEndpointKey(IPlc plc)
        {
            if (plc == null)
                throw new InvalidOperationException("PLC写入设备尚未绑定。");
            PLCParms parameters = plc.PLCParms;
            string physicalIdentity;
            if (parameters.PlcConType == PlcConType.ETHERNET)
            {
                string ipAddress = parameters.EthernetParms.IP?.Trim();
                int port = parameters.EthernetParms.Port;
                if (string.IsNullOrWhiteSpace(ipAddress) || port <= 0 || port > 65535)
                    throw new InvalidOperationException("PLC网口物理地址或端口无效。");
                physicalIdentity = $"ETHERNET:{ipAddress}:{port}";
            }
            else if (parameters.PlcConType == PlcConType.COM)
            {
                string portName = parameters.SerialParms.PortName?.Trim();
                if (string.IsNullOrWhiteSpace(portName))
                    throw new InvalidOperationException("PLC串口物理端口名不能为空。");
                physicalIdentity = $"COM:{portName}";
            }
            else
            {
                throw new InvalidOperationException($"不支持的PLC通信方式：{parameters.PlcConType}");
            }

            return WorkpieceExecutionContext.NormalizeKey(
                $"PLC:{plc.Brand}:{physicalIdentity}",
                nameof(plc));
        }

        /// <summary>PLC写入值的不可变运行时类别。</summary>
        private enum PlcWriteValueKind
        {
            /// <summary>一个或多个布尔值。</summary>
            BooleanValues = 1,

            /// <summary>单个32位整数。</summary>
            Int32 = 2,

            /// <summary>一个或多个32位整数。</summary>
            Int32Values = 3,

            /// <summary>单个单精度浮点数。</summary>
            Single = 4,

            /// <summary>一个或多个单精度浮点数。</summary>
            SingleValues = 5,

            /// <summary>单个字符串。</summary>
            String = 6
        }

        /// <summary>保存PLC写入节点到达时冻结的全部动态值。</summary>
        private sealed class PlcWriteSignalSnapshot
        {
            /// <summary>创建不可变PLC写入快照。</summary>
            public PlcWriteSignalSnapshot(
                IPlc plc,
                string endpointKey,
                string address,
                PlcWriteValueKind valueKind,
                object value)
            {
                Plc = plc;
                EndpointKey = endpointKey;
                Address = address;
                ValueKind = valueKind;
                Value = value;
            }

            /// <summary>获取冻结的PLC对象。</summary>
            public IPlc Plc { get; }

            /// <summary>获取冻结的物理连接端点键。</summary>
            public string EndpointKey { get; }

            /// <summary>获取冻结的写入地址。</summary>
            public string Address { get; }

            /// <summary>获取冻结的写入值类别。</summary>
            public PlcWriteValueKind ValueKind { get; }

            /// <summary>获取冻结且已经完成解析的写入值。</summary>
            public object Value { get; }
        }
    }
}
