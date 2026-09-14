using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication.ModBus;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite;
using TDJS_Vision.ResourceManagement;
using IModbus = TDJS_Vision.Device.Modbus.IModbus;

/// <summary>直接Modbus写入回归，仅使用假设备和随机本机端口。</summary>
internal static class ModbusDirectWriteTests
{
    /// <summary>验证条件，失败时中断测试。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>验证异常不会被当作成功吞掉。</summary>
    private static void Reject(Action action, string message)
    {
        try { action(); } catch { return; }
        throw new InvalidOperationException(message);
    }

    /// <summary>不访问硬件的接口代理，记录每次真实Write调用。</summary>
    private sealed class RecordingModbus : RealProxy
    {
        /// <summary>按调用次序保存独立的值数组。</summary>
        public readonly List<Array> Writes = new List<Array>();
        /// <summary>控制发送前连接检查。</summary>
        public bool Connected = true;
        /// <summary>模拟真实写入时失败。</summary>
        public bool FailWrite;
        /// <summary>选择TCP或RTU参数而不打开端口。</summary>
        public bool Rtu;
        /// <summary>构造透明代理。</summary>
        public RecordingModbus() : base(typeof(IModbus)) { }
        /// <summary>获取供节点使用的假设备接口。</summary>
        public IModbus Device => (IModbus)GetTransparentProxy();
        /// <summary>分派属性读取及写入，未使用的接口返回默认值。</summary>
        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                object result = null;
                switch (call.MethodName)
                {
                    case "get_IsConnect": result = Connected; break;
                    case "get_ModbusParam":
                        result = Rtu ? (object)new ModbusRTUParam { PortName = "COM200", DevType = DevType.ModbusRTUPoll }
                            : new ModbusTcpParam(DevType.ModbusTcpPoll, "127.0.0.1", 502, "假设备", "假设备");
                        break;
                    case "Write":
                        Writes.Add((Array)((Array)call.Args[1]).Clone());
                        if (FailWrite) throw new InvalidOperationException("模拟写入失败");
                        break;
                    default:
                        var type = ((MethodInfo)call.MethodBase).ReturnType;
                        if (type != typeof(void) && type.IsValueType) result = Activator.CreateInstance(type);
                        break;
                }
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
            catch (Exception exception) { return new ReturnMessage(exception, call); }
        }
    }

    /// <summary>创建不依赖生产会话的直接写入节点。</summary>
    private static NodeModbusWrite CreateNode(IModbus device, RegistersType type, string value)
    {
        var node = new NodeModbusWrite(9701, "直接写入测试", null, NodeType.ModbusWrite);
        node.Active = true;
        node.ParamForm.Params = new NodeParamModbusWrite
        {
            Device = device, DeviceName = "假设备", StartAddress = "100", DataType = type,
            Data = value, IsSubscribed = false
        };
        return node;
    }

    /// <summary>构造同组不同序号的测试工件。</summary>
    private static WorkpieceExecutionContext Context(Guid epoch, long sequence) =>
        new WorkpieceExecutionContext(new WorkpieceIdentity(epoch, "DIRECT", sequence), 1,
            "DIRECT", "直接写入测试", DateTime.UtcNow, CancellationToken.None);

    /// <summary>验证实际节点入口、无序直接执行、取消、失败以及数值校验。</summary>
    private static void VerifyDirectNode()
    {
        Check(!typeof(IOrderedExternalSignalNode).IsAssignableFrom(typeof(NodeModbusWrite)), "普通Modbus节点仍接入有序外发接口。");
        var fake = new RecordingModbus();
        using (var node = CreateNode(fake.Device, RegistersType.Bool, "true"))
        using (var form = (System.Windows.Forms.Form)node.ParamForm)
        using (var coordinator = new OrderedSignalCoordinator())
        {
            // 前序工件未封口，以及随后故障封口，都不能阻止直接Write。
            var epoch = Guid.NewGuid();
            var first = Context(epoch, 1);
            var second = Context(epoch, 2);
            coordinator.RegisterWorkpiece(first);
            coordinator.RegisterWorkpiece(second);
            using (Solution.Instance.WorkpieceContextAccessor.Push(second))
            {
                node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(fake.Writes.Count == 1 && ((bool[])fake.Writes[0])[0], "前序未完成时未直接置On。");
                coordinator.CompleteWorkpieceAsync(first, WorkpieceTerminalState.Faulted).GetAwaiter().GetResult();
                node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(fake.Writes.Count == 2 && fake.Writes.All(value => ((bool[])value)[0]), "被顺序域拦截或发生自动复位。");
            }
            ((NodeParamModbusWrite)node.ParamForm.Params).Data = "false";
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(fake.Writes.Count == 3 && !((bool[])fake.Writes[2])[0], "无工件上下文时不能按配置置Off。");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Reject(() => node.Run(cancel.Token, false).GetAwaiter().GetResult(), "已取消节点仍然成功。");
                Check(fake.Writes.Count == 3, "取消后仍调用Write。");
            }
            fake.FailWrite = true;
            Reject(() => node.Run(CancellationToken.None, false).GetAwaiter().GetResult(), "协议失败被吞掉。");
            Check(fake.Writes.Count == 4, "失败后自动重试。");
            fake.FailWrite = false;
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(fake.Writes.Count == 5, "上次失败冻结了后续直接写入。");
            fake.Connected = false;
            Reject(() => node.Run(CancellationToken.None, false).GetAwaiter().GetResult(), "未连接仍然成功。");
            Check(fake.Writes.Count == 5, "未连接仍然调用Write。");
        }
        fake = new RecordingModbus { Rtu = true };
        using (var node = CreateNode(fake.Device, RegistersType.Int, "1,-2"))
        using (var form = (System.Windows.Forms.Form)node.ParamForm)
        {
            node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(((int[])fake.Writes.Single()).SequenceEqual(new[] { 1, -2 }), "RTU直接写入解析错误。");
        }
        NodeModbusWrite.ValidateWriteValue(RegistersType.Double, "1e-3");
        Reject(() => NodeModbusWrite.ValidateWriteValue(RegistersType.Float, "NaN"), "NaN未拒绝。");
        Reject(() => NodeModbusWrite.ValidateWriteValue(RegistersType.Bool, "abc"), "非法布尔值未拒绝。");
    }

    /// <summary>验证并行Modbus节点不再被编译器误当作无序外发错误。</summary>
    private static void VerifyCompiler()
    {
        var config = new ProcessConfig { ProcessName = "直接写入并行图", HasCanvasGraph = true };
        config.NodeInfos.Add(new NodeConfig { ID = 1, NodeType = NodeType.UNKNOWN, Active = true, IsStartNode = true });
        config.NodeInfos.Add(new NodeConfig { ID = 2, NodeType = NodeType.ModbusWrite, Active = true });
        config.NodeInfos.Add(new NodeConfig { ID = 3, NodeType = NodeType.CameraIO, Active = true });
        config.ConnectionInfos.Add(new ProcessConnectionConfig { ID = "a", FromNodeId = 1, ToNodeId = 2 });
        config.ConnectionInfos.Add(new ProcessConnectionConfig { ID = "b", FromNodeId = 1, ToNodeId = 3 });
        var plan = new FlowExecutionPlanCompiler().Compile(config, 1, "DIRECT");
        Check(plan.IsProductionReady && plan.PotentialSignals.Count == 0,
            "Modbus和相机IO直接发送仍被登记为有序信号。");
    }

    /// <summary>用本机Modbus服务验证真实On保持及显式Off，不访问现场设备。</summary>
    private static void VerifyLocalProtocol()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var server = new ModbusTcpServer();
        var device = new ModbusTcpPoll(new ModbusTcpParam(DevType.ModbusTcpPoll, "127.0.0.1", port, "本机测试", "本机测试"));
        try
        {
            server.ServerStart(port);
            device.Connect();
            using (var node = CreateNode(device, RegistersType.Bool, "true"))
            using (var form = (System.Windows.Forms.Form)node.ParamForm)
            {
                node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(server.ReadBool("100").Content, "真实协议未置On。");
                Thread.Sleep(100);
                Check(server.ReadBool("100").Content, "On被自动复位。");
                ((NodeParamModbusWrite)node.ParamForm.Params).Data = "false";
                node.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(!server.ReadBool("100").Content, "显式Off未写入。");
            }
        }
        finally { device.Disconnect(); server.ServerClose(); }
    }

    /// <summary>运行全部测试并返回退出码。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyDirectNode(); VerifyCompiler(); VerifyLocalProtocol();
            Console.WriteLine("Modbus直接写入通过：真实Run入口、On保持/Off、无工件上下文、前序未完成及故障不阻塞、取消、失败不重试、RTU参数、并行图、本机协议。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
