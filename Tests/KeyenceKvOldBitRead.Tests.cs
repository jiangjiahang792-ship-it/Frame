using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;

/// <summary>复现旧DLL的DM位地址问题，并验证提供方案中的实际读取和多条件调用链。</summary>
internal static class KeyenceKvOldBitReadTests
{
    /// <summary>断言测试条件。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>使用本机随机端口模拟PLC，不连接方案中保存的设备。</summary>
    [STAThread]
    private static int Main(string[] args)
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
            Check(server.Write("DM100", (ushort)2).IsSuccess, "初始化DM100失败。");
            // 对比同一个项目DLL：原生旧系列布尔接口拒绝DM位地址。
            using (var native = new KeyenceKvOld("127.0.0.1", port))
            {
                native.ConnectServer();
                var before = native.ReadBool("DM100.1");
                Check(!before.IsSuccess, "旧DLL复现结果变化，请重新检查版本。");
                Console.WriteLine("旧DLL复现：" + before.Message);
            }

            JObject scheme = args.Length > 0 ? JObject.Parse(File.ReadAllText(args[0])) : null;
            var deviceJson = scheme == null ? null : scheme["Devices"].Children<JProperty>()
                .Select(item => (JObject)item.Value).First(item => (string)item["ClassName"] == typeof(PlcKeyenceKvOld).FullName);
            var parms = deviceJson == null ? new PLCParms { DeviceBrand = DeviceBrand.Keyence,
                KeyenceProtocol = KeyenceProtocol.Kv300Older, PlcConType = PlcConType.ETHERNET,
                UserDefinedName = "位读取验证" } : deviceJson["PLCParms"].ToObject<PLCParms>();
            parms.EthernetParms = new EthernetParms("127.0.0.1", port);
            plc = new PlcKeyenceKvOld(parms);
            Check(plc.Connect(), "适配器连接失败。");
            Check(plc.ReadBool("DM100.1").IsSuccess && plc.ReadBool("DM100.1").Content, "DM100.1单点读取失败。");
            Check(!plc.ReadBool("DM100.0").Content, "DM100.0应为假，不能直接把整个DM字转布尔。");

            for (int bit = 0; bit < 16; bit++)
            {
                Check(server.Write("DM100", (ushort)(1 << bit)).IsSuccess, "初始化边界位失败。");
                string address = "DM100." + bit;
                var scalar = plc.ReadBool(address);
                var asyncScalar = plc.ReadBoolAsync(address).GetAwaiter().GetResult();
                var one = plc.ReadBool(address, 1);
                var asyncOne = plc.ReadBoolAsync(address, 1).GetAwaiter().GetResult();
                Check(scalar.IsSuccess && scalar.Content && asyncScalar.IsSuccess && asyncScalar.Content &&
                    one.IsSuccess && one.Content[0] && asyncOne.IsSuccess && asyncOne.Content[0], "位顺序错误：" + address);
                Check(!plc.ReadBool("DM100." + ((bit + 1) % 16)).Content, "相邻位不能混淆。");
            }
            Check(server.Write("DM100", (ushort)32768).IsSuccess && server.Write("DM101", (ushort)2).IsSuccess, "初始化跨字位失败。");
            var crossing = plc.ReadBool("DM100.15", 3);
            var asyncCrossing = plc.ReadBoolAsync("DM100.15", 3).GetAwaiter().GetResult();
            Check(crossing.IsSuccess && crossing.Content.SequenceEqual(new[] { true, false, true }) &&
                asyncCrossing.IsSuccess && asyncCrossing.Content.SequenceEqual(crossing.Content), "跨DM字位读取错误。");
            Check(plc.WriteBool("R100", true).IsSuccess, "原有继电器写入失败。");
            var mixed = plc.ReadBoolAsync(new[] { "DM100.15", "DM101.1", "R100" }).GetAwaiter().GetResult();
            Check(mixed.IsSuccess && mixed.Content.All(value => value), "混合位地址读取错误。");
            foreach (string invalid in new[] { "DM100.16", "DM100.-1", "DM100.x", "DM100.1.2", "DM.1", "DM100.", "R100.1" })
            {
                Check(!plc.ReadBool(invalid).IsSuccess && !plc.ReadBoolAsync(invalid).GetAwaiter().GetResult().IsSuccess,
                    "错误地址未被拒绝：" + invalid);
            }
            Check(!plc.ReadBool("DM100.1", 0).IsSuccess && !plc.ReadBool("DM4294967295.15", 2).IsSuccess, "长度或地址溢出未被拒绝。");

            var process = new TDJS_Vision.Process("用户方案位读取验证");
            var sourceJson = scheme == null ? null : scheme.Descendants().OfType<JObject>().First(item => (string)item["NodeType"] == "PLCRead");
            int sourceId = sourceJson == null ? 10 : (int)sourceJson["ID"];
            using (var source = new NodePlcRead(sourceId, "PLC读取", process, NodeType.PLCRead))
            using (var condition = new NodeMultiCondition(12, "多条件判断", process, NodeType.MultiCondition))
            {
                process.Nodes.Add(source);
                process.Nodes.Add(condition);
                process.Connections.Add(new ProcessConnection { FromNodeId = sourceId, ToNodeId = condition.ID });
                var sourceParam = sourceJson == null ? new NodeParamPlcRead { Address = "DM100", DataType = "Int16" } :
                    sourceJson["NodeParam"].Children<JProperty>().Single().Value.ToObject<NodeParamPlcRead>();
                Console.WriteLine("提供方案中保存的参数：" + sourceParam.Address + " / " + sourceParam.DataType);
                sourceParam.Plc = plc;
                source.ParamForm.Params = sourceParam;
                SynchronizationContext.SetSynchronizationContext(null);
                Check(server.Write("DM100", (ushort)2).IsSuccess, "恢复方案初始字失败。");
                source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                // 用户可能已把方案保存为DM100.1/Boolean，按实际保存类型核对初始读取。
                object initialExpected = sourceParam.DataType == "Boolean" ? (object)true : (short)2;
                Check(Equals(((NodeResultPlcRead)source.Result).ReadResult.Data, initialExpected), "方案保存参数读取失败。");
                sourceParam.Address = "DM100.1";
                sourceParam.DataType = "Boolean";
                condition.ParamForm.Params = new NodeParamMultiCondition { Conditions = {
                    new MultiConditionItem { Name = "DM100第1位", SourceNodeId = sourceId,
                        PropertyPath = DynamicResultVariableResolver.ToPropertyPath("值01(地址DM100.1)"),
                        ValueTypeName = typeof(bool).FullName, Operator = MultiConditionOperator.IsTrue } } };
                foreach (ushort word in new ushort[] { 2, 0, 32768, 3 })
                {
                    Check(server.Write("DM100", word).IsSuccess, "初始化节点测试值失败。");
                    source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    bool expected = (word & 2) != 0;
                    Check(Equals(((NodeResultPlcRead)source.Result).ReadResult.Data, expected), "现有PLC节点读取值错误。");
                    condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    Check(((NodeResultMultiCondition)condition.Result).ConditionResult == expected, "多条件判定位值错误。");
                    Console.WriteLine("DM100=" + word + "，DM100.1=" + expected + "，读取节点和多条件判定通过。");
                }
                server.ServerClose();
                plc.OperationTimeoutMs = 100;
                Check(!plc.ReadBool("DM100.1").IsSuccess, "连接失败时不能返回成功位值。");
                bool failed = false;
                try { source.Run(CancellationToken.None, false).GetAwaiter().GetResult(); }
                catch (Exception) { failed = true; }
                Check(failed && ((NodeResultPlcRead)source.Result).ReadResult == null, "失败后节点必须清空旧数据。");
                ((Form)source.ParamForm).Dispose();
                ((Form)condition.ParamForm).Dispose();
            }
            Console.WriteLine("PASS: 旧DLL报错复现、方案参数读取、DM位同步/异步/数组/跨字/混合地址、边界错误、真实节点和多条件判定。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { plc?.Release(); server.ServerClose(); server.Dispose(); }
    }
}
