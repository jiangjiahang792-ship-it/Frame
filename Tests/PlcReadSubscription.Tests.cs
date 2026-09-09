using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HslCommunication.Profinet.Keyence;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Device;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;

/// <summary>使用真实订阅目录、PLC读取节点及多条件节点验证单值订阅。</summary>
internal static class PlcReadSubscriptionTests
{
    /// <summary>断言测试条件。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>获取与多条件界面相同输入契约下的实际可见输出。</summary>
    private static IReadOnlyList<SubscriptionOutputDescriptor> Outputs(NodeBase node)
    {
        return SubscriptionPortCatalog.GetOutputs(node, new SubscriptionInputContract(
            new[] { SubscriptionDataCategory.Boolean, SubscriptionDataCategory.Number, SubscriptionDataCategory.Text },
            null, new[] { SubscriptionValueMultiplicity.Single }, NumericConversionMode.SafeWidening), false, string.Empty);
    }

    /// <summary>运行本机模拟通信和订阅验证。</summary>
    [STAThread]
    private static int Main()
    {
        var server = new KeyenceNanoServer();
        PlcKeyenceNano plc = null;
        try
        {
            var process = new TDJS_Vision.Process("PLC订阅验证");
            using (var source = new NodePlcRead(1, "PLC读取", process, NodeType.PLCRead))
            using (var condition = new NodeMultiCondition(2, "多条件判定", process, NodeType.MultiCondition))
            {
                process.Nodes.Add(source);
                process.Nodes.Add(condition);
                process.Connections.Add(new ProcessConnection { FromNodeId = 1, ToNodeId = 2 });
                Check(Outputs(source).Count == 0, "未配置节点不能发布虚假值。");
                Type[] types = { typeof(bool), typeof(short), typeof(int), typeof(long), typeof(float), typeof(string) };
                for (int i = 0; i < types.Length; i++)
                {
                    var param = new NodeParamPlcRead { PlcName = "测试设备", Address = "DM100", DataType = types[i].Name, Length = 4 };
                    source.ParamForm.Params = JsonConvert.DeserializeObject<NodeParamPlcRead>(JsonConvert.SerializeObject(param));
                    source.ParamForm.SetParam2Form();
                    var combo = (ComboBox)source.ParamForm.GetType().GetField("comboBoxDataType", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source.ParamForm);
                    Check(combo.SelectedIndex == i, "参数恢复后类型选项错误：" + types[i].Name);
                    var outputs = Outputs(source);
                    Check(outputs.Count == 1 && outputs[0].ValueType == types[i], "运行前未公开正确单值类型：" + types[i].Name);
                }
                foreach (Type type in types.Take(4))
                {
                    source.ParamForm.Params = new NodeParamPlcRead { Address = "DM100-DM104-DM100", DataType = type.Name };
                    var outputs = Outputs(source);
                    Check(outputs.Count == 3 && outputs.All(item => item.ValueType == type), "多地址类型或数量错误。");
                    Check(outputs[0].PropertyPath != outputs[2].PropertyPath, "重复地址必须保留各自的读取序号。");
                }
                source.ParamForm.Params = new NodeParamPlcRead { Address = "MR100", DataType = "Boolean" };
                var path = Outputs(source).Single().PropertyPath;
                ((NodeResultPlcRead)source.Result).ReadResult = new PlcReadResult(true, "Boolean", "MR100");
                var savedResult = JsonConvert.DeserializeObject<PlcReadResult>(
                    JsonConvert.SerializeObject(((NodeResultPlcRead)source.Result).ReadResult));
                Check(savedResult.Address == "MR100" && Equals(savedResult.Data, true), "读取结果序列化兼容失败。");
                object value;
                Check(DynamicResultVariableResolver.TryGetValue(source.Result, path, out value) && value is bool && (bool)value,
                    "动态取值不能读到布尔值。");
                source.ParamForm.Params = new NodeParamPlcRead { Address = "MR200", DataType = "Boolean" };
                Check(Outputs(source).Single().PropertyPath != path, "配置改变后仍发布旧地址。");
                Check(!DynamicResultVariableResolver.TryGetValue(source.Result, Outputs(source).Single().PropertyPath, out value),
                    "新地址不能读取上轮旧地址数据。");
                typeof(NodeParamFormMultiCondition).GetMethod("RefreshSourceTree", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(condition.ParamForm, null);
                var tree = (TreeView)condition.ParamForm.GetType().GetField("treeViewSources", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(condition.ParamForm);
                Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.Contains("PLC读取") && item.Nodes.Count == 1),
                    "多条件界面订阅树未显示PLC单值。");

                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                server.ServerStart(port);
                plc = new PlcKeyenceNano(new PLCParms { DeviceBrand = DeviceBrand.Keyence, PlcConType = PlcConType.ETHERNET,
                    UserDefinedName = "PLC订阅本机验证", EthernetParms = new EthernetParms("127.0.0.1", port) });
                Check(plc.Connect(), "本机模拟PLC连接失败。");
                object[] samples = { true, (short)-123, 123456, 5000000000L, 12.5f, "ABCD" };
                Check(server.Write("MR100", true).IsSuccess, "初始化位失败。");
                Check(server.Write("DM100", (short)-123).IsSuccess, "初始化短整数失败。");
                Check(server.Write("DM110", 123456).IsSuccess, "初始化整数失败。");
                Check(server.Write("DM120", 5000000000L).IsSuccess, "初始化长整数失败。");
                Check(server.Write("DM130", 12.5f).IsSuccess, "初始化浮点失败。");
                Check(plc.WriteString("DM140", "ABCD").IsSuccess, "初始化字符串失败。");
                string[] addresses = { "MR100", "DM100", "DM110", "DM120", "DM130", "DM140" };
                SynchronizationContext.SetSynchronizationContext(null);
                for (int i = 0; i < types.Length; i++)
                {
                    source.ParamForm.Params = new NodeParamPlcRead { Plc = plc, Address = addresses[i], DataType = types[i].Name, Length = 2 };
                    source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    var output = Outputs(source).Single();
                    Check(DynamicResultVariableResolver.TryGetValue(source.Result, output.PropertyPath, out value) && value.GetType() == types[i],
                        "运行后单值类型不符：" + types[i].Name);
                    Check(Equals(value, samples[i]), "运行后读取值不符：" + types[i].Name + "=" + value);
                    Check(((NodeResultPlcRead)source.Result).ReadResult.DataType == types[i].Name, "原结果类型标记错误。");
                    condition.ParamForm.Params = new NodeParamMultiCondition { Conditions = new List<MultiConditionItem> {
                        new MultiConditionItem { Name = "PLC值判断", SourceNodeId = 1, PropertyPath = output.PropertyPath,
                            ValueTypeName = types[i].FullName, Operator = MultiConditionOperator.Equals,
                            Value1 = Convert.ToString(samples[i], System.Globalization.CultureInfo.InvariantCulture) } } };
                    condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    Check(((NodeResultMultiCondition)condition.Result).ConditionResult, "多条件实际比较失败：" + types[i].Name);
                }
                source.ParamForm.Params = new NodeParamPlcRead { Plc = plc, Address = "DM100-DM110-DM100", DataType = "Int16" };
                source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                var multiOutputs = Outputs(source);
                Check(multiOutputs.Count == 3, "多地址输出数量错误。");
                Check(DynamicResultVariableResolver.TryGetValue(source.Result, multiOutputs[2].PropertyPath, out value) && Equals(value, (short)-123),
                    "多地址元素读取不符。");
                Check(((NodeResultPlcRead)source.Result).ReadResult.DataType == "Int16[]", "数组结果错误标记为布尔数组。");
                plc.Disconnect();
                try { source.Run(CancellationToken.None, false).GetAwaiter().GetResult(); }
                catch (Exception) { }
                Check(((NodeResultPlcRead)source.Result).ReadResult == null, "失败后保留了上轮值。");
                source.ParamForm.Params = null;
                Check(Outputs(source).Count == 0, "删除参数后仍公开旧结果。");
                ((Form)source.ParamForm).Dispose();
                ((Form)condition.ParamForm).Dispose();
            }
            Console.WriteLine("PASS: 六种标量、四种多地址类型、未运行订阅、参数恢复、订阅树、PLC实际读取、多条件实际比较和失败清理。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { plc?.Release(); server.ServerClose(); server.Dispose(); }
    }
}
