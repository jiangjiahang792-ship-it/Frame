using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.CaliperCircle;
using TDJS_Vision.Node._6_LogicTool.ArithmeticOperation;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._7_ResultProcessing.ResultSend;
using TDJS_Vision.ResourceManagement;

/// <summary>真实程序集、参数窗体及随机本机端口的结果发送专项验证。</summary>
internal static class ResultSendTests
{
    /// <summary>断言条件。</summary>
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    /// <summary>断言无效配置或设备错误被明确拒绝。</summary>
    private static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (Exception) { failed = true; }
        Check(failed, message);
    }
    /// <summary>从系统分配一个临时本机TCP端口。</summary>
    private static int Port()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop(); return port;
    }
    /// <summary>获取设计器控件。</summary>
    private static T Control<T>(object form, string name) where T : class =>
        (T)form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    /// <summary>调用参数页操作方法。</summary>
    private static void Click(object form, string name) => form.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
    /// <summary>给测试节点注入算法结果，保留项目结果替换入口。</summary>
    private static void SetResult(NodeBase node, INodeResult result) => typeof(NodeBase).GetProperty("Result").SetValue(node, result);
    /// <summary>测试线程安装WinForms上下文后，用消息循环等待异步通信。</summary>
    private static void Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        Check(task.IsCompleted, "异步操作超时。");
        task.GetAwaiter().GetResult();
    }
    /// <summary>创建独立基础测试行。</summary>
    private static ResultSendRow Row(ResultSendType type, string address = "DM100") => new ResultSendRow
    {
        Source = new ResultSendSource { NodeId = 1, Path = "Value", DisplayName = "测试值" }, Address = address, DataType = type
    };
    /// <summary>准备一个标量类型的发送快照。</summary>
    private static ResultSendPreparedRow Value(ResultSendType type, object value, string address = "DM100") =>
        ResultSendValueConverter.Prepare(Row(type, address), new List<object> { value }, 1);

    /// <summary>记录整批发送顺序的可插拔测试适配器。</summary>
    private sealed class RecordingWriter : IResultSendWriter
    {
        /// <summary>对应已有设备。</summary>
        public IDevice Device { get; set; }
        /// <summary>共用原PLC端点。</summary>
        public string EndpointKey => new PlcResultSendWriter((IPlc)Device).EndpointKey;
        /// <summary>已调用的地址和值副本。</summary>
        public List<Tuple<string, Array>> Calls { get; } = new List<Tuple<string, Array>>();
        /// <summary>指定在第几次写入抛出异常。</summary>
        public int FailAt { get; set; }
        /// <summary>暂停首个写入，以验证发送快照及完成等待。</summary>
        public TaskCompletionSource<bool> Gate { get; set; }
        /// <summary>复用真实设备预检。</summary>
        public void Validate(string address, ResultSendType type, Array values) => new PlcResultSendWriter((IPlc)Device).Validate(address, type, values);
        /// <summary>记录写入，在指定位置模拟失败，不接触设备。</summary>
        public Task WriteAsync(string address, ResultSendType type, Array values)
        {
            Calls.Add(Tuple.Create(address, (Array)values.Clone()));
            if (Calls.Count == FailAt) throw new InvalidOperationException("模拟设备拒绝。");
            return Calls.Count == 1 && Gate != null ? Gate.Task : Task.CompletedTask;
        }
    }

    /// <summary>确认结果发送与其他外发节点均不再参与有序外发检查。</summary>
    private static void VerifyCompiler()
    {
        var config = new ProcessConfig { ProcessName = "结果发送编译检查", HasCanvasGraph = true };
        config.NodeInfos.Add(new NodeConfig { ID = 1, NodeName = "结果发送", NodeType = NodeType.ResultSend, Active = true, IsStartNode = true });
        var plan = new FlowExecutionPlanCompiler().Compile(config, 1, "测试域");
        Check(plan.IsProductionReady && plan.PotentialSignals.Count == 0, "结果发送仍被登记为有序外发。");
        Check(!typeof(IOrderedExternalSignalNode).IsAssignableFrom(typeof(NodeResultSend)), "结果发送仍实现有序外发接口。");
        config.NodeInfos[0].NodeType = NodeType.UNKNOWN;
        config.NodeInfos.Add(new NodeConfig { ID = 2, NodeType = NodeType.ResultSend, Active = true });
        config.NodeInfos.Add(new NodeConfig { ID = 3, NodeType = NodeType.ResultSend, Active = true });
        config.NodeInfos.Add(new NodeConfig { ID = 4, NodeType = NodeType.CameraIO, Active = true });
        for (int id = 2; id <= 4; id++)
            config.ConnectionInfos.Add(new ProcessConnectionConfig { ID = "并行" + id, FromNodeId = 1, ToNodeId = id });
        plan = new FlowExecutionPlanCompiler().Compile(config, 1, "测试域");
        Check(plan.IsProductionReady && plan.PotentialSignals.Count == 0,
            "并行结果发送或相机IO仍被登记为有序信号。");
        config.NodeInfos[3].NodeType = NodeType.PLCWrite;
        plan = new FlowExecutionPlanCompiler().Compile(config, 1, "测试域");
        Check(plan.IsProductionReady && plan.PotentialSignals.Count == 0,
            "独立PLC写入仍被登记为有序信号。");
    }

    /// <summary>验证严格转换、空集合、容量及固定字节字符串。</summary>
    private static void VerifyConversion()
    {
        Check(((uint[])Value(ResultSendType.UInt32, "4294967295").Values)[0] == uint.MaxValue, "uint高位丢失。");
        Check(((ushort[])Value(ResultSendType.UInt16, 65535).Values)[0] == ushort.MaxValue, "ushort转换失败。");
        Check(((short[])Value(ResultSendType.Int16, -32768).Values)[0] == short.MinValue, "short转换失败。");
        Reject(() => Value(ResultSendType.Int16, 32768), "short溢出必须拒绝。");
        Reject(() => Value(ResultSendType.UInt16, -1), "无符号负值必须拒绝。");
        Reject(() => Value(ResultSendType.Int32, 1.5), "小数不能截断。");
        Reject(() => Value(ResultSendType.Double, "1,2"), "错误分隔符不能吞掉。");
        Reject(() => Value(ResultSendType.Double, double.NaN), "NaN不能发送。");
        Reject(() => Value(ResultSendType.Single, double.MaxValue), "float溢出必须拒绝。");
        Reject(() => Value(ResultSendType.Boolean, 2), "非0/1不能隐式转换布尔。");
        Check(((double[])Value(ResultSendType.Double, null).Values)[0] == 0, "空值应发送0。");
        var row = Row(ResultSendType.Double); row.Mode = ResultSendMode.Index; row.Index = 2;
        Check(((double[])ResultSendValueConverter.Prepare(row, new List<object> { 10, 20, 30 }, 1).Values)[0] == 20, "指定项错误。");
        row.Index = 4;
        Check(((double[])ResultSendValueConverter.Prepare(row, new List<object> { 1 }, 1).Values)[0] == 0, "未检出指定明细应发送0。");
        row.Mode = ResultSendMode.All; row.Capacity = 4; row.CountAddress = "DM200";
        var prepared = ResultSendValueConverter.Prepare(row, new List<object> { 1, 2 }, 1);
        Check(prepared.Count == 2 && ((double[])prepared.Values).SequenceEqual(new double[] { 1, 2, 0, 0 }), "容量补零错误。");
        prepared = ResultSendValueConverter.Prepare(row, new List<object>(), 1);
        Check(prepared.Count == 0 && prepared.Values.Length == 4, "零目标必须写0数量并清空数据。");
        row.Capacity = 1;
        Reject(() => ResultSendValueConverter.Prepare(row, new List<object> { 1, 2 }, 1), "超容量不能截断。");
        row.Mode = ResultSendMode.Aggregate; row.Aggregate = ResultSendAggregate.Average;
        Check(((double[])ResultSendValueConverter.Prepare(row, new List<object> { 2, 4 }, 1).Values)[0] == 3, "汇总错误。");
        row.Aggregate = ResultSendAggregate.AllTrue; row.DataType = ResultSendType.Boolean;
        Reject(() => ResultSendValueConverter.Prepare(row, new List<object> { false, "无效" }, 1), "布尔汇总不能短路隐藏无效值。");
        var text = Row(ResultSendType.String); text.StringBytes = 4;
        Check(((byte[])ResultSendValueConverter.Prepare(text, new List<object> { "AB" }, 1).Values).SequenceEqual(new byte[] { 65, 66, 0, 0 }), "字符串补零错误。");
        Reject(() => ResultSendValueConverter.Prepare(text, new List<object> { "ABCDE" }, 1), "字符串不能截断。");
        text.EncodingName = "ASCII";
        Reject(() => ResultSendValueConverter.Prepare(text, new List<object> { "中文" }, 1), "不兼容编码不能替换字符。");
    }

    /// <summary>覆盖八种类型、全部取值方式及连续发送的缺失默认值。</summary>
    private static void VerifyMissingValues()
    {
        foreach (ResultSendType type in Enum.GetValues(typeof(ResultSendType)))
        foreach (ResultSendMode mode in Enum.GetValues(typeof(ResultSendMode)))
        {
            var row = Row(type);
            row.Mode = mode; row.CountAddress = "DM200"; row.Capacity = 3;
            foreach (bool fill in new[] { false, true })
            {
                row.ClearRemaining = fill;
                var prepared = ResultSendValueConverter.Prepare(row, new List<object>(), 1);
                Check(prepared.Count == (mode == ResultSendMode.All ? 0 : 1), "默认值不能虚增连续发送的目标数量。");
                Check(prepared.Values.Length > 0, "零目标仍应写入默认值。");
                if (type == ResultSendType.String)
                {
                    var bytes = (byte[])prepared.Values;
                    for (int offset = 0; offset < bytes.Length; offset += row.StringBytes)
                        Check(System.Text.Encoding.UTF8.GetString(bytes, offset, row.StringBytes).TrimEnd('\0') == "无效", "空字符串目标应写入无效。");
                }
                else foreach (object value in prepared.Values)
                    Check(type == ResultSendType.Boolean ? (bool)value : Convert.ToDouble(value) == 0, "空目标的类型默认值错误。");
            }
        }
        var flags = Row(ResultSendType.Boolean); flags.Mode = ResultSendMode.All; flags.Capacity = 4; flags.CountAddress = "DM200";
        Check(((bool[])ResultSendValueConverter.Prepare(flags, new List<object> { false, null, true }, 1).Values)
            .SequenceEqual(new[] { false, true, true, true }), "默认值不能覆盖已有false，且空项必须保留位置。");
        var legacy = JsonConvert.DeserializeObject<ResultSendRow>(JsonConvert.SerializeObject(Row(ResultSendType.Double)).TrimEnd('}') + ",\"Transform\":1,\"Scale\":100,\"Offset\":50}");
        Check(((double[])ResultSendValueConverter.Prepare(legacy, new List<object> { 2 }, 1).Values)[0] == 2 && !JsonConvert.SerializeObject(legacy).Contains("Transform"), "旧值处理参数不应隐式继续生效或保存。");
    }

    /// <summary>验证八种真实PLC/Modbus协议写入以及断开后的失败返回。</summary>
    private static void VerifyDevices()
    {
        var server = new KeyenceNanoServer();
        var modServer = new ModbusTcpServer();
        int plcPort = Port(), modbusPort = Port();
        var param = new PLCParms { UserDefinedName = "结果发送本机PLC", DeviceBrand = DeviceBrand.Keyence, PlcConType = PlcConType.ETHERNET,
            KeyenceProtocol = KeyenceProtocol.Kv300Older, EthernetParms = new EthernetParms("127.0.0.1", plcPort) };
        var plc = new PlcKeyenceKvOld(param);
        var modbus = new ModbusTcpPoll(new ModbusTcpParam(DevType.ModbusTcpPoll, "127.0.0.1", modbusPort, "本机Modbus", "本机Modbus"));
        try
        {
            server.ServerStart(plcPort); modServer.ServerStart(modbusPort);
            Check(plc.Connect(), "本机PLC连接失败。"); modbus.Connect();
            IResultSendWriter[] writers = { new PlcResultSendWriter(plc), new ModbusResultSendWriter(modbus) };
            foreach (var writer in writers)
            {
                string address = writer is PlcResultSendWriter ? "DM100" : "100";
                var cases = new[] { Value(ResultSendType.Int16, -1234, address), Value(ResultSendType.UInt16, 60000, address), Value(ResultSendType.Int32, -12345678, address), Value(ResultSendType.UInt32, 4000000000U, address), Value(ResultSendType.Single, 12.5, address), Value(ResultSendType.Double, 12345.6789, address), Value(ResultSendType.String, "AB", address) };
                foreach (var item in cases)
                {
                    writer.WriteAsync(address, item.Configuration.DataType, item.Values).GetAwaiter().GetResult();
                    bool isPlc = writer is PlcResultSendWriter;
                    switch (item.Configuration.DataType)
                    {
                        case ResultSendType.Int16: Check((isPlc ? server.ReadInt16(address).Content : modServer.ReadInt16(address).Content) == -1234, "short报文错误。"); break;
                        case ResultSendType.UInt16: Check((isPlc ? server.ReadUInt16(address).Content : modServer.ReadUInt16(address).Content) == 60000, "ushort报文错误。"); break;
                        case ResultSendType.Int32: Check((isPlc ? server.ReadInt32(address).Content : modServer.ReadInt32(address).Content) == -12345678, "int报文错误。"); break;
                        case ResultSendType.UInt32: Check((isPlc ? server.ReadUInt32(address).Content : modServer.ReadUInt32(address).Content) == 4000000000U, "uint报文错误。"); break;
                        case ResultSendType.Single: Check((isPlc ? server.ReadFloat(address).Content : modServer.ReadFloat(address).Content) == 12.5f, "float报文错误。"); break;
                        case ResultSendType.Double: Check((isPlc ? server.ReadDouble(address).Content : modServer.ReadDouble(address).Content) == 12345.6789, "double报文错误。"); break;
                        case ResultSendType.String: Check(isPlc ? server.Read(address, 2).Content.Take(4).SequenceEqual(new byte[] { 65, 66, 0, 0 }) : modServer.ReadUInt16(address).Content == 0x4142, "string编码字节或填充错误。"); break;
                    }
                }
                string bitAddress = writer is PlcResultSendWriter ? "DM150.1" : "150";
                writer.WriteAsync(bitAddress, ResultSendType.Boolean, new bool[] { true }).GetAwaiter().GetResult();
                Check(writer is PlcResultSendWriter ? (server.ReadUInt16("DM150").Content & 2) != 0 : modServer.ReadBool("150").Content, "bool写入错误。");
            }
            // 分批发送超过单报文长度的double数组，检查边界处的地址步长。
            double[] large = Enumerable.Range(0, 70).Select(index => index + .25).ToArray();
            writers[1].WriteAsync("1000", ResultSendType.Double, large).GetAwaiter().GetResult();
            Check(modServer.ReadDouble("1000", 70).Content.SequenceEqual(large), "Modbus分批地址或字序错误。");
            Reject(() => writers[1].Validate("65535", ResultSendType.Double, new double[1]), "Modbus越界不能写入。");
            VerifyNodeAndUi(plc, modbus, modServer);
            plc.Disconnect(); modbus.Disconnect();
            Reject(() => writers[0].WriteAsync("DM100", ResultSendType.Int32, new int[] { 1 }).GetAwaiter().GetResult(), "断开PLC不能报成功。");
            Reject(() => writers[1].WriteAsync("100", ResultSendType.Int32, new int[] { 1 }).GetAwaiter().GetResult(), "断开Modbus不能报成功。");
        }
        finally { plc.Release(); modbus.Disconnect(); server.ServerClose(); server.Dispose(); modServer.ServerClose(); modServer.Dispose(); }
    }

    /// <summary>验证真实节点订阅、配置恢复、设计器布局和直接发送入口。</summary>
    private static void VerifyNodeAndUi(IPlc plc, TDJS_Vision.Device.Modbus.IModbus modbus, ModbusTcpServer modServer)
    {
        Console.WriteLine("开始节点与窗体验证。");
        var process = new TDJS_Vision.Process("结果发送验证");
        var sender = (NodeResultSend)NodeFactory.CreateNode(5, "结果发送", process, NodeType.ResultSend);
        sender.Active = true;
        var arithmetic = new NodeArithmeticOperation(1, "四则运算", process, NodeType.ArithmeticOperation);
        arithmetic.ParamForm.Params = new NodeParamArithmeticOperation { Rows = new List<ArithmeticOperationRow> { new ArithmeticOperationRow { OutputVariableName = "长度" } } };
        SetResult(arithmetic, new NodeResultArithmeticOperation { Value = 12.5, Variables = new Dictionary<string, double> { { "长度", 12.5 } } });
        var ai = new NodeBase(2, "AI检测", process, NodeType.AITD);
        SetResult(ai, new NodeResultTDAI());
        var aiResult = ((NodeResultTDAI)ai.Result).AlgorithmResult;
        aiResult.DetectResults["DetectItem.BackRivetArea"] = new List<SingleDetectResult> { new SingleDetectResult("DetectItem.BackRivetArea", "12.3", true), new SingleDetectResult("DetectItem.BackRivetArea", "15.6", false) };
        var measure = new NodeBase(3, "圆测量", process, NodeType.CaliperCircle);
        SetResult(measure, new NodeResultCaliperCircle { Radius = 6.25,
            Items = new List<CaliperCircleTargetResult> { new CaliperCircleTargetResult { TargetIndex = 1, IsOk = true, Radius = 6.25 }, new CaliperCircleTargetResult { TargetIndex = 2, IsOk = true, Radius = 8.5 } } });
        var condition = new NodeMultiCondition(4, "多条件判定", process, NodeType.MultiCondition);
        SetResult(condition, new NodeResultMultiCondition { ConditionResult = true });
        var nodes = new NodeBase[] { arithmetic, ai, measure, condition, sender };
        process.Nodes.AddRange(nodes);
        foreach (var node in nodes.Take(4)) process.Connections.Add(new ProcessConnection { FromNodeId = node.ID, ToNodeId = 5 });
        Solution.Instance.AllDevices.Add(plc);
        Solution.Instance.AllDevices.Add(modbus);
        try
        {
            Console.WriteLine("检查四类订阅目录。");
            var selections = nodes.Take(4).SelectMany(ResultSendSourceReader.GetSources).ToList();
            Check(selections.Any(item => item.NodeId == 1 && item.Path.StartsWith("$variable:")), "四则动态变量不可订阅。");
            Check(selections.Any(item => item.NodeId == 2 && item.Kind == ResultSendSourceKind.AiFlags), "AI明细判定不可订阅。");
            var radiusItems = selections.Single(item => item.NodeId == 3 && item.Kind == ResultSendSourceKind.Items && item.Member == "Radius");
            Check(ResultSendSourceReader.Read(sender, radiusItems, false).SequenceEqual(new object[] { 6.25, 8.5 }), "测量多目标读取错误。");
            var conditionSource = selections.Single(item => item.NodeId == 4 && item.Path == "ConditionResult");
            Check((bool)ResultSendSourceReader.Read(sender, conditionSource, false)[0], "多条件订阅错误。");
            var aiSource = selections.Single(item => item.NodeId == 2 && item.Kind == ResultSendSourceKind.AiValues && item.ItemName == "DetectItem.BackRivetArea");
            Check(aiSource.DisplayName.Contains("后铆脚面积") && !aiSource.DisplayName.Contains("DetectItem."), "订阅目录必须显示中文检测项。");
            var oldSource = aiSource.Copy(); oldSource.ItemName = "后铆脚面积";
            Check(ResultSendSourceReader.Read(sender, oldSource, false).SequenceEqual(new object[] { "12.3", "15.6" }), "中文旧订阅不能读取规范键。");
            var param = new NodeParamResultSend { DeviceName = plc.UserDefinedName, Rows = new List<ResultSendRow>
            {
                new ResultSendRow { Source = selections.Single(item => item.NodeId == 1 && item.Path == "Value"), Address = "DM300", DataType = ResultSendType.Double },
                new ResultSendRow { Source = aiSource, Address = "DM310", Mode = ResultSendMode.All, DataType = ResultSendType.Single, Capacity = 5, CountAddress = "DM350" },
                new ResultSendRow { Source = radiusItems, Address = "DM360", Mode = ResultSendMode.Index, Index = 2, DataType = ResultSendType.Double },
                new ResultSendRow { Source = conditionSource, Address = "DM380.1", DataType = ResultSendType.Boolean }
            }};
            var savedNode = JsonConvert.DeserializeObject<NodeConfig>(JsonConvert.SerializeObject(new NodeConfig { ID = 5, NodeName = "结果发送", NodeType = NodeType.ResultSend, NodeParam = param }));
            Check(savedNode.NodeType == NodeType.ResultSend && savedNode.NodeParam is NodeParamResultSend, "现有方案多态转换器不能恢复新节点参数。");
            sender.ParamForm.Params = savedNode.NodeParam;
            sender.ParamForm.SetParam2Form();
            Console.WriteLine("参数恢复完成。");
            Check(sender.GetSubscriptionDependencyNodeIds().OrderBy(value => value).SequenceEqual(new[] { 1, 2, 3, 4 }), "订阅依赖未注册。");
            var form = (ParamFormResultSend)sender.ParamForm;
            Check(Control<ComboBox>(form, "comboType").Items.Count == 8, "写入类型不是八种。");
            Check(Control<TreeView>(form, "treeSources").Nodes.Count == 4, "真实结果树没有四个节点。");
            Check(Control<DataGridView>(form, "gridRows").Rows.Count == 4, "保存恢复后表格行数错误。");
            var writer = new PlcResultSendWriter(plc);
            Reject(() => sender.PrepareRows(param, writer, true), "不能发送没有本轮成功标记的旧结果。");
            typeof(TDJS_Vision.Process).GetProperty("CurrentRunId").SetValue(process, 1);
            foreach (var node in nodes.Take(4)) node.SetRunResult(DateTime.Now, NodeStatus.Successful);
            VerifyMissingAi(sender, aiResult, aiSource, writer);
            var prepared = sender.PrepareRows(param, writer, true);
            Console.WriteLine("准备实际发送四行。");
            var result = new NodeResultResultSend();
            Pump(NodeResultSend.SendPreparedAsync(writer, prepared, result));
            Check(result.Success && result.SentCount == 4 && plc.ReadInt16("DM350").Content == 2, "结果批量发送或数量写入错误。");
            Check(plc.ReadFloat("DM310", 5).Content.SequenceEqual(new[] { 12.3f, 15.6f, 0f, 0f, 0f }), "PLC数组补零错误。");
            var failureWriter = new RecordingWriter { Device = plc, FailAt = 2 };
            var failureResult = new NodeResultResultSend();
            Reject(() => Pump(NodeResultSend.SendPreparedAsync(failureWriter, prepared, failureResult)), "设备失败必须停止发送。");
            Check(!failureResult.Success && failureResult.SentCount == 1 && failureResult.FailedRow == 2 && failureWriter.Calls.Count == 2, "部分失败的行号或成功条数不准确。");
            var invalid = param.Copy(); invalid.Rows[1].CountAddress = "DM312";
            Reject(() => sender.PrepareRows(invalid, writer, true), "数量地址与数据重叠必须拒绝。");
            ((NodeResultCaliperCircle)measure.Result).Items[1].IsOk = false;
            Check(((double[])sender.PrepareRows(param, writer, true)[2].Values)[0] == 0, "未测得的目标应发送默认值。");
            ((NodeResultCaliperCircle)measure.Result).Items[1].IsOk = true;
            // 没有工件上下文时，PLC和Modbus都通过真实Run入口直接写入。
            Pump(sender.Run(CancellationToken.None, false));
            Check(((NodeResultResultSend)sender.Result).Success && ((NodeResultResultSend)sender.Result).SentCount == 4, "PLC无工件上下文直接发送失败。");
            var modParam = param.Copy();
            modParam.DeviceName = modbus.UserDefinedName;
            for (int index = 0; index < modParam.Rows.Count; index++) modParam.Rows[index].Address = (2000 + index * 100).ToString();
            modParam.Rows[1].CountAddress = "2500";
            sender.ParamForm.Params = modParam;
            Pump(sender.Run(CancellationToken.None, false));
            Check(((NodeResultResultSend)sender.Result).Success && modServer.ReadDouble("2000").Content == 12.5 &&
                modServer.ReadFloat("2100", 5).Content.SequenceEqual(new[] { 12.3f, 15.6f, 0f, 0f, 0f }) &&
                modServer.ReadUInt16("2500").Content == 2 && modServer.ReadBool("2300").Content, "Modbus真实Run直接发送或数量写入失败。");
            sender.ParamForm.Params = param.Copy();
            var coordinator = Solution.Instance.OrderedSignalCoordinator;
            var epoch = Guid.NewGuid();
            var first = new WorkpieceExecutionContext(new WorkpieceIdentity(epoch, "结果测试", 1), 1, "结果测试", "测试", DateTime.UtcNow, CancellationToken.None);
            var second = new WorkpieceExecutionContext(new WorkpieceIdentity(epoch, "结果测试", 2), 1, "结果测试", "测试", DateTime.UtcNow, CancellationToken.None);
            coordinator.RegisterWorkpiece(first);
            coordinator.RegisterWorkpiece(second);
            using (Solution.Instance.WorkpieceContextAccessor.Push(second))
            {
                Pump(sender.Run(CancellationToken.None, false));
                Check(((NodeResultResultSend)sender.Result).Success, "前序工件未完成时不能直接发送。");
                Pump(coordinator.CompleteWorkpieceAsync(first, WorkpieceTerminalState.Faulted));
                Pump(sender.Run(CancellationToken.None, false));
                Check(((NodeResultResultSend)sender.Result).Success, "前序工件故障阻塞了直接发送。");
            }
            var recording = new RecordingWriter { Device = plc, Gate = new TaskCompletionSource<bool>() };
            ResultSendWriterFactory.Register(device => ReferenceEquals(device, plc) ? recording : null);
            {
                var send = sender.Run(CancellationToken.None, false);
                Check(recording.Calls.Count == 1 && !send.IsCompleted && !((NodeResultResultSend)sender.Result).Success, "写入未完成就报告了节点成功。");
                var replacement = param.Copy(); replacement.Rows[1].Address = "DM999"; sender.ParamForm.Params = replacement;
                aiResult.DetectResults["DetectItem.BackRivetArea"][0].Value = "999";
                recording.Gate.SetResult(true); Pump(send);
                Check(recording.Calls.Any(call => call.Item1 == "DM310" && call.Item2 is float[] values && values[0] == 12.3f) && !recording.Calls.Any(call => call.Item1 == "DM999"), "发送期间的参数或结果变化污染了快照。");
                Check(recording.Calls.Select(call => call.Item1).SequenceEqual(new[] { "DM300", "DM310", "DM350", "DM360", "DM380.1" }), "单次调用内数据、数量及行顺序错误。");
                aiResult.DetectResults["DetectItem.BackRivetArea"][0].Value = "12.3";
                sender.ParamForm.Params = param.Copy();
            }
            recording = new RecordingWriter { Device = plc };
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var send = sender.Run(cancelled.Token, false);
                Reject(() => Pump(send), "发送前取消未返回取消。");
                Check(send.IsCanceled && recording.Calls.Count == 0, "发送前取消仍然写入了设备。");
            }
            sender.ParamForm.Params = invalid;
            Reject(() => Pump(sender.Run(CancellationToken.None, false)), "后续行配置无效时没有拒绝整批发送。");
            Check(recording.Calls.Count == 0 && ((NodeResultResultSend)sender.Result).FailedRow == 2, "整批预检完成前已写入设备或失败行错误。");
            sender.ParamForm.Params = param.Copy();
            recording = new RecordingWriter { Device = plc, FailAt = 2 };
            Reject(() => Pump(sender.Run(CancellationToken.None, false)), "实际Run吞掉了设备写入失败。");
            var failed = (NodeResultResultSend)sender.Result;
            Check(!failed.Success && failed.FailedRow == 2 && failed.SentCount == 1 && recording.Calls.Count == 2, "部分失败后发生自动重试或继续发送。");
            recording = new RecordingWriter { Device = plc };
            Pump(sender.Run(CancellationToken.None, false));
            Check(((NodeResultResultSend)sender.Result).Success && recording.Calls.Count == 5, "上次失败冻结了后续直接发送。");
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false; form.Show();
            Console.WriteLine("实际窗体布局与保存检查。");
            Control<DataGridView>(form, "gridRows").CurrentCell = Control<DataGridView>(form, "gridRows").Rows[1].Cells[1];
            Application.DoEvents(); form.PerformLayout();
            Check(Control<TextBox>(form, "textAddress").Text == "DM310", "选中行与下方编辑器不一致。");
            Check(Control<TextBox>(form, "textItem").Text == "后铆脚面积", "检测项编辑框必须显示中文。");
            Check(((ResultSendRow)Control<DataGridView>(form, "gridRows").Rows[1].DataBoundItem).SourceText.Contains("后铆脚面积"), "发送列表必须显示中文检测项。");
            Check(form.GetType().GetField("comboTransform", BindingFlags.Instance | BindingFlags.NonPublic) == null, "不应保留值处理控件。");
            Check(!Control<NumericUpDown>(form, "numberIndex").Visible && !Control<ComboBox>(form, "comboEncoding").Visible && Control<NumericUpDown>(form, "numberCapacity").Visible, "当前行显示了无关参数。");
            Control<ComboBox>(form, "comboType").SelectedValue = ResultSendType.String;
            Control<ComboBox>(form, "comboMode").SelectedValue = ResultSendMode.Index;
            Check(Control<NumericUpDown>(form, "numberIndex").Visible && Control<ComboBox>(form, "comboEncoding").Visible && !Control<NumericUpDown>(form, "numberCapacity").Visible, "切换取值或类型后参数未正确显示。");
            Check(Control<TableLayoutPanel>(form, "editorLayout").GetCellPosition(Control<ComboBox>(form, "comboEncoding")).Row == 1, "适用参数应紧凑排布。");
            Control<ComboBox>(form, "comboType").SelectedValue = ResultSendType.Single;
            Control<ComboBox>(form, "comboMode").SelectedValue = ResultSendMode.All;
            Control<TextBox>(form, "textAddress").Text = "DM320";
            Check(((NodeParamResultSend)form.Params).Rows[1].Address == "DM310", "未保存的编辑不能影响运行参数。");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("ResultSend-parameters.png"); }
            Click(form, "SaveClicked");
            var saved = (NodeParamResultSend)form.Params;
            Check(saved.Rows.Count == 4 && saved.Rows[1].Address == "DM320" && saved.Rows[0].Address == "DM300" && saved.Rows[1].DataType == ResultSendType.Single && saved.DeviceName == plc.UserDefinedName && saved.Rows[1].Source.ItemName == "DetectItem.BackRivetArea", "实际保存事件丢失参数、检测项键或修改了错误的行。");
            var stale = param.Copy(); process.Connections.RemoveAll(connection => connection.FromNodeId == 2);
            Reject(() => sender.PrepareRows(stale, writer, true), "断开上游连线后仍能发送旧订阅。");
        }
        finally
        {
            Solution.Instance.AllDevices.Remove(plc);
            Solution.Instance.AllDevices.Remove(modbus);
            foreach (var node in nodes) { (node.ParamForm as Form)?.Dispose(); node.Dispose(); }
        }
    }

    /// <summary>验证AI缺失项、空明细及空检测值经过真实节点发送到本机PLC。</summary>
    private static void VerifyMissingAi(NodeResultSend sender, AlgorithmResult algorithm, ResultSendSource source, IResultSendWriter writer)
    {
        var original = algorithm.DetectResults[source.ItemName];
        try
        {
            var cases = new[] { null, new List<SingleDetectResult>(), new List<SingleDetectResult> { null },
                new List<SingleDetectResult> { new SingleDetectResult(source.ItemName, "", true) } };
            foreach (var items in cases)
            {
                if (items == null) algorithm.DetectResults.Remove(source.ItemName);
                else algorithm.DetectResults[source.ItemName] = items;
                var parameter = new NodeParamResultSend { Rows = new List<ResultSendRow>
                {
                    new ResultSendRow { Source = source.Copy(), Address = "DM600", Mode = ResultSendMode.Index, DataType = ResultSendType.Int32 },
                    new ResultSendRow { Source = source.Copy(), Address = "DM610.1", Mode = ResultSendMode.Index, DataType = ResultSendType.Boolean },
                    new ResultSendRow { Source = source.Copy(), Address = "DM620", Mode = ResultSendMode.Index, DataType = ResultSendType.String }
                }};
                Pump(NodeResultSend.SendPreparedAsync(writer, sender.PrepareRows(parameter, writer, true), new NodeResultResultSend()));
                var plc = (IPlc)writer.Device;
                Check(plc.ReadInt32("DM600").Content == 0 && plc.ReadBool("DM610.1").Content &&
                    System.Text.Encoding.UTF8.GetString(plc.ReadBytes("DM620", 16).Content).TrimEnd('\0') == "无效", "AI未检出时PLC未收到0、true和无效。");
                if (items == null || items.Count == 0 || items[0] == null)
                    foreach (var kind in new[] { ResultSendSourceKind.AiFlags, ResultSendSourceKind.AiJudgment })
                    {
                        var row = parameter.Rows[1].Copy(); row.Source.Kind = kind;
                        Check(((bool[])ResultSendValueConverter.Prepare(row, ResultSendSourceReader.Read(sender, row.Source, true), 1).Values)[0], "空明细判定应发送true。");
                    }
            }
        }
        finally { algorithm.DetectResults[source.ItemName] = original; }
    }

    /// <summary>模拟重启恢复后的空运行结果，验证全部配置项及通信模板在运行前即可订阅。</summary>
    private static void VerifyConfiguredAiSources()
    {
        var savedDefinitions = Solution.Instance.DetectItemDic;
        var savedCache = TDAI.DetectItemMap;
        var process = new TDJS_Vision.Process("运行前检测目录验证");
        var ai = new NodeTDAI(1, "未运行AI检测", process, NodeType.AITD);
        var sender = new NodeResultSend(2, "结果发送", process, NodeType.ResultSend);
        process.Nodes.Add(ai); process.Nodes.Add(sender);
        process.Connections.Add(new ProcessConnection { FromNodeId = 1, ToNodeId = 2 });
        try
        {
            var solution = new SolConfig { DetectItemDic = new Dictionary<string, List<DetectItemInfo>>
            {
                { "固定模板", new List<DetectItemInfo>
                    { new DetectItemInfo { Name = "DetectItem.BackRivetArea", Enable = true },
                      new DetectItemInfo { Name = "线芯长度", Enable = false },
                      new DetectItemInfo { Name = "DetectItem.CoreLength", Enable = false } } },
                { "切换模板", new List<DetectItemInfo> { new DetectItemInfo { Name = "DetectItem.CoreHeight", Enable = false } } },
                { "无关模板", new List<DetectItemInfo> { new DetectItemInfo { Name = "DetectItem.Dirty", Enable = true } } }
            }};
            Solution.Instance.DetectItemDic = JsonConvert.DeserializeObject<SolConfig>(JsonConvert.SerializeObject(solution)).DetectItemDic;
            TDAI.DetectItemMap = new Dictionary<string, List<DetectItemInfo>>();
            var nodeConfig = new NodeConfig { NodeType = NodeType.AITD, NodeParam = new NodeParamTDAI
                { IsFixed = true, DetectItemName1 = "固定模板", CurDetectItemName = string.Empty } };
            ai.ParamForm.Params = JsonConvert.DeserializeObject<NodeConfig>(JsonConvert.SerializeObject(nodeConfig)).NodeParam;
            var sources = ResultSendSourceReader.GetSources(ai).Where(source => source.Kind >= ResultSendSourceKind.AiValues).ToList();
            Check(sources.Count == 6 && sources.Count(source => source.ItemName == "DetectItem.CoreLength") == 3,
                "重启恢复后未运行时应列出启用及未启用项，中文旧键与规范键不得重复。");
            Check(sources.All(source => !source.DisplayName.Contains("DetectItem.") && !string.IsNullOrWhiteSpace(source.ItemName)), "运行前目录出现英文键或指定检测项占位。");
            Check(((NodeResultTDAI)ai.Result).AlgorithmResult.DetectResults.Count == 0 && TDAI.DetectItemMap.Count == 0, "目录发现不应执行检测或创建运行时缓存。");
            var form = (ParamFormResultSend)sender.ParamForm;
            sender.ParamForm.SetParam2Form();
            var tree = Control<TreeView>(form, "treeSources");
            Check(tree.Nodes.Count == 1 && tree.Nodes[0].Nodes.Cast<TreeNode>().Count(node => node.Tag is ResultSendSource source && source.Kind >= ResultSendSourceKind.AiValues) == 6,
                "参数窗体在运行前未展开完整检测目录。");
            var disabled = sources.Single(source => source.ItemName == "DetectItem.CoreLength" && source.Kind == ResultSendSourceKind.AiValues);
            var row = new ResultSendRow { Source = disabled, Address = "DM100", Mode = ResultSendMode.Index, DataType = ResultSendType.Boolean };
            Check(((bool[])ResultSendValueConverter.Prepare(row, ResultSendSourceReader.Read(sender, disabled, false), 1).Values)[0], "未启用项无结果时仍应使用既定默认值。");

            var parameter = (NodeParamTDAI)ai.ParamForm.Params;
            parameter.IsFixed = false; parameter.DetectItemName2 = "固定模板";
            parameter.TDAICommuntionParams.Add(new TDAICommuntionParam { TriggerVal = "1", DetectionName = "切换模板" });
            sources = ResultSendSourceReader.GetSources(ai).Where(source => source.Kind >= ResultSendSourceKind.AiValues).ToList();
            Check(sources.Count == 9 && sources.All(source => source.ItemName != "DetectItem.Dirty"), "通信模板应合并默认和映射配置，不能混入无关模板。");
            // 修改启用状态和重新打开窗体都不需要先运行算法。
            Solution.Instance.DetectItemDic["固定模板"][0].Enable = false;
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents();
            Check(tree.Nodes[0].IsExpanded && tree.Nodes[0].Nodes.Cast<TreeNode>().Count(node => node.Tag is ResultSendSource source && source.Kind >= ResultSendSourceKind.AiValues) == 9,
                "重新打开窗体未刷新或展开全部检测项。");
            form.Hide();
            Solution.Instance.DetectItemDic["切换模板"].Add(new DetectItemInfo { Name = "DetectItem.TerminalCount", Enable = false });
            form.Show(); Application.DoEvents();
            Check(tree.Nodes[0].Nodes.Cast<TreeNode>().Any(node => node.Tag is ResultSendSource source && source.ItemName == "DetectItem.TerminalCount"), "重新打开窗体未读取最新未启用项。");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("ResultSend-before-run.png"); }
            Console.WriteLine("运行前目录专项通过：方案反序列化、空运行缓存、未启用项、中文键去重、通信模板和重新打开刷新。");
        }
        finally
        {
            (sender.ParamForm as Form)?.Dispose(); sender.Dispose();
            (ai.ParamForm as Form)?.Dispose(); ai.Dispose();
            Solution.Instance.DetectItemDic = savedDefinitions; TDAI.DetectItemMap = savedCache;
        }
    }

    /// <summary>执行验证并返回进程退出码。</summary>
    [STAThread]
    private static int Main()
    {
        try { LanguageManager.SetLanguage("zh-CN", false); VerifyConversion(); VerifyMissingValues(); VerifyCompiler(); Console.WriteLine("转换专项完成，开始本机通信。"); VerifyDevices(); VerifyConfiguredAiSources(); Console.WriteLine("结果发送专项通过：中文检测项、八类型默认值与协议、四类订阅、多目标、方案恢复、精简窗体、直接发送、前序未完成及故障不阻塞、快照隔离、发送完成等待、发送前取消和部分失败。"); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
