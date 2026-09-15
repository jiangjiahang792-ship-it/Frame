using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication.Core.Device;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Keyence;
using HslCommunication.Profinet.Melsec;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node._7_ResultProcessing.ResultSend;
using TDJS_Vision.ResourceManagement;
using IModbus = TDJS_Vision.Device.Modbus.IModbus;

/// <summary>使用随机本机端口验证真实适配器，无任何现场设备读写。</summary>
internal static class CommunicationReconnectTests
{
    /// <summary>检查测试条件。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>在有限时间内等待后台状态变化。</summary>
    private static void Wait(Func<bool> condition, string message)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < 8000) Thread.Sleep(20);
        Check(condition(), message);
    }

    /// <summary>向系统申请临时端口。</summary>
    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>统一启动主站连接，初次离线是预期失败。</summary>
    private static bool Connect(ReconnectingCommunicationDevice device)
    {
        if (device is IPlc plc) return plc.Connect();
        try { ((IModbus)device).Connect(); return device.IsConnect; }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>主动断开测试设备并撤销重连意图。</summary>
    private static void Disconnect(ReconnectingCommunicationDevice device)
    {
        if (device is IPlc plc) plc.Disconnect();
        else ((IModbus)device).Disconnect();
    }

    /// <summary>取得当前客户端，用真实HSL通信锁模拟一次在途报文。</summary>
    private static DeviceCommunication Client(ReconnectingCommunicationDevice device) =>
        (DeviceCommunication)typeof(ReconnectingCommunicationDevice).GetProperty(
            "CommunicationClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(device);

    /// <summary>验证首连失败恢复、空闲断线恢复、通信锁互斥、手动关闭和持久化意图。</summary>
    private static void Exercise(string name, ReconnectingCommunicationDevice device, Action start, Action stop, Action verifyRead)
    {
        try
        {
            if (device is IPlc plc) plc.OperationTimeoutMs = 300;
            else ((IModbus)device).OperationTimeoutMs = 300;
            Check(!Connect(device), name + "离线首连应失败。");
            Check(device.RestoreConnectionRequested && !device.IsConnect, name + "首连失败丢失重试意图。");
            string offlineJson = JsonConvert.SerializeObject(device);
            Check(JObject.Parse(offlineJson).Value<bool>("IsConnect"), name + "离线保存必须保留连接意图。");
            var restored = (ReconnectingCommunicationDevice)JsonConvert.DeserializeObject(offlineJson, device.GetType());
            Check(restored.RestoreConnectionRequested && !restored.IsConnect, name + "反序列化伪装成已连接或丢失意图。");
            start();
            Wait(() => device.IsConnect, name + "服务恢复后没有后台自动连接。");
            verifyRead();
            var pipeLock = Client(device).CommunicationPipe.CommunicationLock;
            Check(pipeLock.EnterLock(1000).IsSuccess, "无法取得测试通信锁。");
            try
            {
                stop();
                Thread.Sleep(1500);
                Check(device.IsConnect, name + "守护越过正在使用的通信锁。");
            }
            finally { pipeLock.LeaveLock(); }
            Wait(() => !device.IsConnect, name + "服务断开后没有更新离线状态。");
            start();
            Wait(() => device.IsConnect, name + "中途断线后没有自动恢复。");
            verifyRead();
            Disconnect(device);
            Thread.Sleep(1300);
            Check(!device.IsConnect && !device.RestoreConnectionRequested, name + "主动断开后被自动重连。");
            Check(!JObject.Parse(JsonConvert.SerializeObject(device)).Value<bool>("IsConnect"), name + "主动断开后保存意图错误。");
            Console.WriteLine(name + ": initial failure, idle reconnect, lock isolation, persistence and manual close passed.");
        }
        finally { Disconnect(device); stop(); }
    }

    /// <summary>创建一次独立工件上下文。</summary>
    private static WorkpieceExecutionContext Context(string domain) => new WorkpieceExecutionContext(
        new WorkpieceIdentity(Guid.NewGuid(), domain, 1), 1, domain, "重连测试", DateTime.UtcNow, CancellationToken.None);

    /// <summary>模拟第一笔、后续行或数量提交时的明确未发送异常。</summary>
    private sealed class OfflineWriter : IResultSendWriter
    {
        /// <inheritdoc />
        public IDevice Device => null;
        /// <inheritdoc />
        public string EndpointKey => "TEST";
        /// <summary>第几次调用失败。</summary>
        public int FailAt;
        /// <summary>已经进入适配器的次数。</summary>
        public int Calls;
        /// <inheritdoc />
        public void Validate(string address, ResultSendType type, Array values) { }
        /// <inheritdoc />
        public Task WriteAsync(string address, ResultSendType type, Array values)
        {
            if (++Calls == FailAt) throw new DeviceSendNotStartedException("设备未连接，本次调用未发送。");
            return Task.CompletedTask;
        }
    }

    /// <summary>创建发送快照，不需要启动界面或连接真实设备。</summary>
    private static ResultSendPreparedRow Row(ResultSendMode mode = ResultSendMode.Single) =>
        ResultSendValueConverter.Prepare(new ResultSendRow {
            Source = new ResultSendSource { NodeId = 1, Path = "Value", DisplayName = "测试值" },
            Address = "0", CountAddress = "10", DataType = ResultSendType.Int16, Mode = mode
        }, new List<object> { (short)12 }, 1);

    /// <summary>验证明确未发不会冻结端点，部分写入和数量写入失败仍按Unknown保护。</summary>
    private static void VerifySignalClassification()
    {
        using (var serializer = new SignalEndpointSerializer())
        using (var coordinator = new OrderedSignalCoordinator(serializer))
        {
            var context = Context("NOT-SENT");
            coordinator.RegisterWorkpiece(context);
            var detail = new NodeResultResultSend();
            var result = context.SubmitOrderedSignalAsync(coordinator, "结果发送", OrderedSignalNodeKind.ResultSend,
                "SHARED", () => NodeResultSend.SendPreparedAsync(new OfflineWriter { FailAt = 1 }, new List<ResultSendPreparedRow> { Row() }, detail), CancellationToken.None).GetAwaiter().GetResult();
            Check(result.Status == OrderedSignalSendStatus.Rejected, "明确未发送错误被判为Unknown：" + result.Message);
            Check(!detail.Error.Contains("可能已部分写入"), "明确未发送仍显示部分写入警告。");
            using (var timeout = new CancellationTokenSource(1000))
            using (serializer.AcquireAsync("SHARED", timeout.Token).GetAwaiter().GetResult()) { }
        }
        foreach (bool countFailure in new[] { false, true })
        {
            using (var serializer = new SignalEndpointSerializer())
            using (var coordinator = new OrderedSignalCoordinator(serializer))
            {
                var context = Context("PARTIAL");
                coordinator.RegisterWorkpiece(context);
                var rows = countFailure ? new List<ResultSendPreparedRow> { Row(ResultSendMode.All) }
                    : new List<ResultSendPreparedRow> { Row(), Row() };
                var writer = new OfflineWriter { FailAt = 2 };
                var result = context.SubmitOrderedSignalAsync(coordinator, "结果发送", OrderedSignalNodeKind.ResultSend,
                    "SHARED", () => NodeResultSend.SendPreparedAsync(writer, rows, new NodeResultResultSend()), CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Status == OrderedSignalSendStatus.Unknown && writer.Calls == 2, "部分写入失败被误判为未发送或自动重试。");
                using (var timeout = new CancellationTokenSource(150))
                {
                    bool blocked = false;
                    try { serializer.AcquireAsync("SHARED", timeout.Token).GetAwaiter().GetResult().Dispose(); }
                    catch (OperationCanceledException) { blocked = true; }
                    Check(blocked, "Unknown未冻结端点。");
                }
            }
        }
        Console.WriteLine("Send classification: no-side-effect rejection and partial-write protection passed.");
    }

    /// <summary>仅使用不存在的串口验证失败不会冒充在线，以及用户能够停止重试。</summary>
    private static void VerifyMissingSerialPort()
    {
        string portName = Enumerable.Range(200, 50).Select(index => "COM" + index)
            .First(name => !SerialPort.GetPortNames().Contains(name, StringComparer.OrdinalIgnoreCase));
        var rtu = new ModbusRTUPoll(new ModbusRTUParam {
            PortName = portName, BaudRate = 9600, DataBits = 8, Parity = Parity.None, StopBits = StopBits.One,
            DevName = "TEST", UserDefinedName = "RTU离线测试"
        });
        var panasonic = new PlcPanasonic(new PLCParms {
            UserDefinedName = "松下串口离线测试", PlcConType = PlcConType.COM,
            SerialParms = new SerialParms(portName, 9600, 8, StopBits.One, Parity.None)
        });
        foreach (var device in new ReconnectingCommunicationDevice[] { rtu, panasonic })
        {
            try
            {
                var configuredPipe = Client(device).CommunicationPipe as HslCommunication.Core.Pipe.PipeSerialPort;
                Check(configuredPipe != null && configuredPipe.GetPipe().PortName == portName && !configuredPipe.IsOpen(),
                    "串口初始化或超时设置丢失用户端口配置，禁止测试连接。");
                bool connected = Connect(device);
                var serialPipe = Client(device).CommunicationPipe as HslCommunication.Core.Pipe.PipeSerialPort;
                Console.WriteLine(device.GetType().Name + ": pipe=" + Client(device).CommunicationPipe.GetType().FullName +
                    "; port=" + serialPipe?.GetPipe().PortName + "; open=" + serialPipe?.IsOpen());
                Check(serialPipe.GetPipe().PortName == portName, "重连改变了已配置的串口号。");
                Check(!connected && device.RestoreConnectionRequested && !device.IsConnect,
                    device.GetType().Name + "不存在串口" + portName + "：返回=" + connected + "；恢复意图=" + device.RestoreConnectionRequested + "；在线=" + device.IsConnect);
                Thread.Sleep(1100);
                Check(!device.IsConnect, "串口后台失败被判为在线。");
            }
            finally { Disconnect(device); }
            Check(!device.RestoreConnectionRequested, "串口主动断开未停止重试。");
        }
        Console.WriteLine("Serial startup failure and manual cancellation checks passed; no physical serial devices used.");
    }

    /// <summary>执行真实适配器的本机网络恢复和信号分类测试。</summary>
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0 && args[0] == "signals")
            {
                VerifySignalClassification();
                return 0;
            }
            if (args.Length != 0 && args[0] == "serial")
            {
                VerifyMissingSerialPort();
                return 0;
            }
            int port = Port();
            var modbus = new ModbusTcpPoll(new ModbusTcpParam { IP = "127.0.0.1", Port = port, DevName = "TEST", UserDefinedName = "Modbus重连测试" });
            var modbusServer = new ModbusTcpServer();
            Exercise("Modbus TCP", modbus, () => modbusServer.ServerStart(port), () => modbusServer.ServerClose(),
                () => Check(modbus.ReadInt16("0", 1).Length == 1, "Modbus恢复后读取失败。"));
            int plcPort = Port();
            var parms = new PLCParms { UserDefinedName = "PLC重连测试", PlcConType = PlcConType.ETHERNET,
                EthernetParms = new EthernetParms("127.0.0.1", plcPort) };
            var melsec = new PlcMelsec(parms);
            var melsecServer = new MelsecMcServer(true);
            Exercise("Melsec TCP", melsec, () => melsecServer.ServerStart(plcPort), () => melsecServer.ServerClose(),
                () => Check(melsec.ReadInt16("D0").IsSuccess, "三菱恢复后读取失败。"));
            var keyence = new PlcKeyenceNano(parms);
            var keyenceServer = new KeyenceNanoServer();
            Exercise("Keyence Nano TCP", keyence, () => keyenceServer.ServerStart(plcPort), () => keyenceServer.ServerClose(),
                () => Check(keyence.ReadInt16("DM0").IsSuccess, "基恩士恢复后读取失败。"));
            var panasonic = new PlcPanasonic(parms);
            Exercise("Panasonic MC TCP", panasonic, () => melsecServer.ServerStart(plcPort), () => melsecServer.ServerClose(),
                () => Check(panasonic.ReadInt16("D0").IsSuccess, "松下恢复后读取失败。"));
            VerifyMissingSerialPort();
            VerifySignalClassification();
            Console.WriteLine("Communication reconnect checks passed.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
