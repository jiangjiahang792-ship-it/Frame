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

    /// <summary>验证寄存器高低字节读取、旧方案兼容及界面保存恢复。</summary>
    private static void VerifyStringByteOrder(NodePlcRead source, PlcKeyenceNano plc, KeyenceNanoServer server)
    {
        var orderProperty = typeof(NodeParamPlcRead).GetProperty("StringByteOrder");
        Check(orderProperty != null, "缺少字符串高低位顺序参数。");
        var legacy = JsonConvert.DeserializeObject<NodeParamPlcRead>("{\"DataType\":\"String\",\"Length\":3}");
        legacy.PlcName = plc.UserDefinedName;
        Check(orderProperty.GetValue(legacy).ToString() == "DeviceDefault", "旧方案必须保持设备默认读取顺序。");
        // 固定寄存器数值，避免用字符串写入和读取的同一转换逻辑互相抵消错误。
        Check(server.Write("DM220", new short[] { 0x2041, 0x4220, 0x4344 }).IsSuccess, "初始化高低字节样例失败。");
        Solution.Instance.AllDevices.Add(plc);
        try
        {
            using (var form = new ParamFormPlcRead())
            {
                var combo = (ComboBox)typeof(ParamFormPlcRead).GetField("comboBoxStringByteOrder", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
                Check(combo != null && combo.Items.Count == 3, "参数界面缺少字节顺序选项。");
                Check(!combo.Enabled, "非字符串类型不应启用字节顺序。");
                string[] orders = { "HighByteFirst", "LowByteFirst", "HighByteFirst" };
                string[] expected = { "AB CD", "A  BDC", "AB CD" };
                for (int i = 0; i < orders.Length; i++)
                {
                    var param = new NodeParamPlcRead { Plc = plc, PlcName = plc.UserDefinedName, Address = "DM220", DataType = "String", Length = 3 };
                    orderProperty.SetValue(param, Enum.Parse(orderProperty.PropertyType, orders[i]));
                    form.Params = JsonConvert.DeserializeObject<NodeParamPlcRead>(JsonConvert.SerializeObject(param));
                    form.SetParam2Form();
                    Check(combo.Enabled && combo.SelectedIndex == (orders[i] == "HighByteFirst" ? 1 : 2), "字符串字节顺序未恢复到界面。");
                    typeof(ParamFormPlcRead).GetMethod("button1_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
                    Check(orderProperty.GetValue(form.Params).ToString() == orders[i], "界面保存丢失字节顺序。");
                    Check(ReferenceEquals(((NodeParamPlcRead)form.Params).Plc, plc), "参数恢复未绑定方案PLC实例。");
                    source.ParamForm.Params = form.Params;
                    SynchronizationContext.SetSynchronizationContext(null);
                    source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    Check(Equals(((NodeResultPlcRead)source.Result).ReadResult.Data, expected[i]), "高低位读取或首尾空白处理错误：" + orders[i]);
                    object value;
                    Check(DynamicResultVariableResolver.TryGetValue(source.Result, Outputs(source).Single().PropertyPath, out value) && Equals(value, expected[i]), "字节顺序未应用到订阅结果。");
                }
                // 读取同一连接后默认结果必须不变，节点选项不能改动共享设备状态。
                Check(plc.ReadString("DM140", 2).Content == "ABCD", "节点字节顺序污染了共享设备默认读取。");
                // 奇数字符串最后半字及定长缓冲区允许零填充，但正文中的空字符不能吞掉后续内容。
                short[][] paddedWords = {
                    new short[] { 0x4100, 0 }, new short[] { 0x0041, 0 },
                    new short[] { 0x4100, 0x4200 }, new short[] { 0x0041, 0x0042 },
                    new short[] { 0, 0 }
                };
                string[] paddedExpected = { "A", "A", "A\0B", "A\0B", "" };
                for (int i = 0; i < paddedWords.Length; i++)
                {
                    Check(server.Write("DM240", paddedWords[i]).IsSuccess, "初始化零填充样例失败。");
                    var param = new NodeParamPlcRead { Plc = plc, Address = "DM240", DataType = "String", Length = 2 };
                    orderProperty.SetValue(param, Enum.Parse(orderProperty.PropertyType, i % 2 == 0 ? "HighByteFirst" : "LowByteFirst"));
                    source.ParamForm.Params = param;
                    source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    Check(Equals(((NodeResultPlcRead)source.Result).ReadResult.Data, paddedExpected[i]), "末尾零填充处理错误，样例：" + i);
                }
                combo.SelectedIndex = 2;
                typeof(ParamFormPlcRead).GetMethod("button1_Click", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { null, EventArgs.Empty });
                Check(orderProperty.GetValue(form.Params).ToString() == "LowByteFirst", "用户切换字节顺序后未保存选择。");
                // 真实设计器布局截图，供检查新增行是否完整显示。
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new System.Drawing.Point(-30000, -30000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PLC字符串字节顺序参数.png"));
                }
                form.Hide();
                form.Params = legacy;
                form.SetParam2Form();
                Check(combo.SelectedIndex == 0, "旧方案恢复未选择兼容默认项。");
                form.Params = new NodeParamPlcRead { PlcName = plc.UserDefinedName, DataType = "Int16" };
                form.SetParam2Form();
                Check(!combo.Enabled, "切换回数值类型后仍启用字节顺序。");
            }
        }
        finally { Solution.Instance.AllDevices.Remove(plc); }
    }

    /// <summary>运行本机模拟通信和订阅验证。</summary>
    [STAThread]
    private static int Main()
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
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
                // 字符串应仅清理边界空白，不能改变中间空格或前导零；使用偶数字节避免协议补齐干扰。
                string[] rawStrings = { "  AB  CD  ", "  0012  ", "        ", "AB  CD", " A B  ", "\tAB CD\r\n" };
                string[] trimmedStrings = { "AB  CD", "0012", "", "AB  CD", "A B", "AB CD" };
                for (int i = 0; i < rawStrings.Length; i++)
                {
                    Check(plc.WriteString("DM200", rawStrings[i]).IsSuccess, "字符串空白样例写入失败。");
                    source.ParamForm.Params = new NodeParamPlcRead { Plc = plc, Address = "DM200", DataType = "String", Length = (ushort)(rawStrings[i].Length / 2) };
                    source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                    Check(Equals(((NodeResultPlcRead)source.Result).ReadResult.Data, trimmedStrings[i]), "PLC字符串首尾空白处理错误，样例：" + i);
                    Check(DynamicResultVariableResolver.TryGetValue(source.Result, Outputs(source).Single().PropertyPath, out value) && Equals(value, trimmedStrings[i]),
                        "动态字符串订阅与处理后的读取结果不一致，样例：" + i);
                }
                VerifyStringByteOrder(source, plc, server);
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
            Console.WriteLine("PASS: 六种标量、四种多地址类型、六种字符串首尾空白样例、高低位字符串读取与界面保存恢复、未运行订阅、订阅树、PLC实际读取、多条件实际比较和失败清理。");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally { plc?.Release(); server.ServerClose(); server.Dispose(); }
    }
}
