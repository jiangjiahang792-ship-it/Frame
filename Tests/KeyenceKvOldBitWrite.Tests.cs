using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Profinet.Keyence;
using TDJS_Vision.Device;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcWirte;
using TDJS_Vision.ResourceManagement;

/// <summary>通过随机本机端口验证DM字内位写入及真实PLC写入节点，不操作现场设备。</summary>
internal static class KeyenceKvOldBitWriteTests
{
    /// <summary>断言测试条件。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>限时等待写入，及时报告同步和异步混用造成的死锁。</summary>
    private static T Wait<T>(Task<T> task)
    {
        Check(task.Wait(10000), "写入超时，可能存在锁重入。");
        return task.GetAwaiter().GetResult();
    }

    /// <summary>从模拟PLC直接核对完整字，检查目标位以外的数据未被改动。</summary>
    private static void CheckWord(KeyenceNanoServer server, string address, ushort expected)
    {
        var read = server.ReadUInt16(address);
        Check(read.IsSuccess && read.Content == expected,
            address + "期望=" + expected + "，实际=" + read.Content);
    }

    /// <summary>走现有节点的参数解析、有序提交和布尔数组写入入口。</summary>
    private static void CheckNode(PlcKeyenceKvOld plc, KeyenceNanoServer server, string value, ushort expected)
    {
        using (var node = new NodePlcWrite(9401, "布尔写入验证", null, NodeType.PLCWrite))
        using (var coordinator = new OrderedSignalCoordinator())
        {
            node.ParamForm.Params = new NodeParamPlcWrite { Plc = plc, PlcName = plc.UserDefinedName,
                Address = "DM100.1", DataType = "Boolean", Value = value };
            // 测试没有WinForms消息循环，不能保留控件创建时安装的同步上下文。
            SynchronizationContext.SetSynchronizationContext(null);
            var identity = new WorkpieceIdentity(Guid.NewGuid(), "位写入验证", 1);
            var context = new WorkpieceExecutionContext(identity, 1, "位写入验证", "本机测试",
                DateTime.UtcNow, CancellationToken.None);
            coordinator.RegisterWorkpiece(context);
            var result = Wait(node.SubmitOrderedSignalAsync(context, "流程/PLC写入", coordinator, CancellationToken.None));
            Check(result.Status == OrderedSignalSendStatus.DeviceAcknowledged, "节点写入未收到设备确认：" + value);
            coordinator.CompleteWorkpieceAsync(context, WorkpieceTerminalState.Succeeded).GetAwaiter().GetResult();
            CheckWord(server, "DM100", expected);
            ((Form)node.ParamForm).Dispose();
        }
    }

    /// <summary>执行单点、数组、并发、错误和节点调用测试。</summary>
    [STAThread]
    private static int Main()
    {
        var server = new KeyenceNanoServer();
        PlcKeyenceKvOld plc = null;
        try
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            server.ServerStart(port);
            plc = new PlcKeyenceKvOld(new PLCParms { DeviceBrand = DeviceBrand.Keyence,
                KeyenceProtocol = KeyenceProtocol.Kv300Older, PlcConType = PlcConType.ETHERNET,
                UserDefinedName = "位写入验证", EthernetParms = new EthernetParms("127.0.0.1", port) });
            Check(plc.Connect(), "连接模拟PLC失败。");

            for (int bit = 0; bit < 16; bit++)
            {
                string address = "DM100." + bit;
                Check(server.Write("DM100", (ushort)0xA55A).IsSuccess, "初始化失败。");
                Check(plc.WriteBool(address, true).IsSuccess, "同步置位失败：" + address);
                CheckWord(server, "DM100", (ushort)(0xA55A | (1 << bit)));
                Check(plc.WriteBool(address, false).IsSuccess, "同步清位失败：" + address);
                CheckWord(server, "DM100", (ushort)(0xA55A & ~(1 << bit)));
                Check(Wait(plc.WriteBoolAsync(address, true)).IsSuccess, "异步置位失败：" + address);
                CheckWord(server, "DM100", (ushort)(0xA55A | (1 << bit)));
                Check(Wait(plc.WriteBoolAsync(address, false)).IsSuccess, "异步清位失败：" + address);
                CheckWord(server, "DM100", (ushort)(0xA55A & ~(1 << bit)));
            }
            Console.WriteLine("PASS: 16个位索引同步/异步置位、清位，其他位保留。");

            Check(server.Write("DM100", (ushort)0x1234).IsSuccess && server.Write("DM101", (ushort)0xFFFC).IsSuccess, "跨字初始化失败。");
            Check(plc.WriteBool("DM100.15", new[] { true, true, false }).IsSuccess, "跨字写入失败。");
            CheckWord(server, "DM100", 0x9234);
            CheckWord(server, "DM101", 0xFFFD);
            Check(Wait(plc.WriteBoolAsync("DM100.15", new[] { false, false, true })).IsSuccess, "异步跨字写入失败。");
            CheckWord(server, "DM100", 0x1234);
            CheckWord(server, "DM101", 0xFFFE);
            Check(plc.WriteBool(" dm100.1 ", new[] { true }).IsSuccess, "单元素数组写入失败。");
            CheckWord(server, "DM100", 0x1236);
            Check(Wait(plc.WriteBoolAsync("DM100.1", new[] { false })).IsSuccess, "异步单元素数组写入失败。");
            CheckWord(server, "DM100", 0x1234);

            foreach (bool value in new[] { true, false })
            {
                Check(server.Write("DM100", value ? (ushort)0 : ushort.MaxValue).IsSuccess, "并发初始化失败。");
                var tasks = Enumerable.Range(0, 16).Select(bit => bit % 2 == 0
                    ? Task.Run(() => plc.WriteBool("DM100." + bit, value))
                    : plc.WriteBoolAsync("DM100." + bit, value)).ToArray();
                Check(Wait(Task.WhenAll(tasks)).All(result => result.IsSuccess), "混合同步异步并发写入失败。");
                CheckWord(server, "DM100", value ? ushort.MaxValue : (ushort)0);
            }
            Console.WriteLine("PASS: 数组、跨字写入及同一客户端同步异步并发不丢位。");

            Check(server.Write("DM100", (ushort)0xA55A).IsSuccess, "非法输入测试初始化失败。");
            foreach (string address in new[] { "DM100.16", "DM100.-1", "DM100.x", "DM100.1.2", "DM.1", "DM100.", "R100.1" })
            {
                Check(!plc.WriteBool(address, true).IsSuccess && !Wait(plc.WriteBoolAsync(address, false)).IsSuccess,
                    "非法位地址未拒绝：" + address);
            }
            foreach (bool[] values in new[] { (bool[])null, new bool[0], new bool[65536] })
                Check(!plc.WriteBool("DM100.1", values).IsSuccess && !Wait(plc.WriteBoolAsync("DM100.1", values)).IsSuccess,
                    "非法写入数量未拒绝。");
            Check(!plc.WriteBool("DM4294967295.15", new[] { true, true }).IsSuccess &&
                !Wait(plc.WriteBoolAsync("DM4294967295.15", new[] { true, true })).IsSuccess, "跨字地址溢出未拒绝。");
            CheckWord(server, "DM100", 0xA55A);

            server.EnableWrite = false;
            Check(!plc.WriteBool("DM100.1", false).IsSuccess && !Wait(plc.WriteBoolAsync("DM100.1", false)).IsSuccess,
                "模拟PLC拒绝写入时不应返回成功。");
            CheckWord(server, "DM100", 0xA55A);
            server.EnableWrite = true;
            Check(Wait(plc.WriteBoolAsync("DM100.1", false)).IsSuccess, "写入失败后未释放写锁。");
            CheckWord(server, "DM100", 0xA558);

            CheckNode(plc, server, "True", 0xA55A);
            CheckNode(plc, server, "False", 0xA558);
            CheckNode(plc, server, "1", 0xA55A);
            CheckNode(plc, server, "0", 0xA558);
            Check(plc.WriteBool("R100", new[] { true }).IsSuccess && plc.ReadBool("R100").Content, "原有R位写入失败。");
            Check(Wait(plc.WriteBoolAsync("R100", new[] { false })).IsSuccess && !plc.ReadBool("R100").Content, "原有R位异步写入失败。");
            Console.WriteLine("PASS: 非法输入、写入拒绝及锁释放，实际PLC写入节点True/False/1/0和R位回归。");

            server.ServerClose();
            plc.OperationTimeoutMs = 100;
            Check(!plc.WriteBool("DM100.1", true).IsSuccess && !Wait(plc.WriteBoolAsync("DM100.1", true)).IsSuccess,
                "读取原字失败时不能返回写入成功。");
            CheckWord(server, "DM100", 0xA558);
            Console.WriteLine("PASS: 断开通信后读改写失败，原字不变。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { plc?.Release(); server.ServerClose(); server.Dispose(); }
    }
}
