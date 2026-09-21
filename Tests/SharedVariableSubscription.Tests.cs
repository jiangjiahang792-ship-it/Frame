using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._5_EquipmentCommunication.PlcRead;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

/// <summary>验证共享变量的可见性、真实值读取、多条件判断和旧输出兼容。</summary>
internal static class SharedVariableSubscriptionTests
{
    /// <summary>已执行的行为断言数量。</summary>
    private static int _checks;

    /// <summary>断言生产行为符合预期。</summary>
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>使用多条件界面的真实输入契约查询可订阅值。</summary>
    private static IReadOnlyList<SubscriptionOutputDescriptor> Outputs(NodeBase node)
    {
        return SubscriptionPortCatalog.GetOutputs(node, new SubscriptionInputContract(
            new[] { SubscriptionDataCategory.Boolean, SubscriptionDataCategory.Number, SubscriptionDataCategory.Text },
            null, new[] { SubscriptionValueMultiplicity.Single }, NumericConversionMode.SafeWidening), false, string.Empty);
    }

    /// <summary>通过真实多条件节点执行比较，覆盖保存恢复后的动态路径。</summary>
    private static bool Evaluate(NodeMultiCondition condition, string path, Type type, string expected)
    {
        var param = new NodeParamMultiCondition { Conditions = new List<MultiConditionItem> {
            new MultiConditionItem { Name = "共享值判断", SourceNodeId = 3, PropertyPath = path,
                ValueTypeName = type.FullName, Operator = MultiConditionOperator.Equals, Value1 = expected } } };
        condition.ParamForm.Params = JsonConvert.DeserializeObject<NodeParamMultiCondition>(JsonConvert.SerializeObject(param));
        condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
        return ((NodeResultMultiCondition)condition.Result).ConditionResult;
    }

    /// <summary>执行离线订阅集成回归。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        var writeProcess = new TDJS_Vision.Process("共享写入验证");
        var readProcess = new TDJS_Vision.Process("共享读取验证");
        var nodes = new List<NodeBase>();
        try
        {
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) VerifyProvidedSolution(args[0]);
            var plc = new NodePlcRead(1, "PLC读取", writeProcess, NodeType.PLCRead);
            var writer = new NodeSharedVariable(2, "共享写入", writeProcess, NodeType.SharedVariable);
            var reader = new NodeSharedVariable(3, "共享读取", readProcess, NodeType.SharedVariable);
            var condition = new NodeMultiCondition(4, "多条件判定", readProcess, NodeType.MultiCondition);
            nodes.AddRange(new NodeBase[] { plc, writer, reader, condition });
            writeProcess.Nodes.AddRange(new NodeBase[] { plc, writer });
            readProcess.Nodes.AddRange(new NodeBase[] { reader, condition });
            writeProcess.Connections.Add(new ProcessConnection { FromNodeId = 1, ToNodeId = 2 });
            readProcess.Connections.Add(new ProcessConnection { FromNodeId = 3, ToNodeId = 4 });
            writeProcess.NotifyConnectionsChanged();
            readProcess.NotifyConnectionsChanged();
            Solution.Instance.AllProcesses.Add(writeProcess);
            Solution.Instance.AllProcesses.Add(readProcess);
            Check(Outputs(reader).Count == 0, "未配置读取节点不能公开虚假值。");

            Type[] types = { typeof(bool), typeof(short), typeof(int), typeof(long), typeof(float), typeof(string) };
            object[] samples = { true, (short)-12, 123, 5000000000L, 12.5f, "0012中文" };
            string[] expected = { "True", "-12", "123", "5000000000", "12.5", "0012中文" };
            string path = null;
            for (int i = 0; i < types.Length; i++)
            {
                Solution.Instance.SharedVariable.ClearAll();
                ((NodeResultSharedVariable)reader.Result).Value = null;
                plc.ParamForm.Params = new NodeParamPlcRead { Address = "DM100", DataType = types[i].Name };
                writer.ParamForm.Params = new NodeParamSharedVariable { IsRead = false, WriteName = "测试共享值", ReadName = "[未选择]",
                    Text1 = "1.PLC读取", Text2 = "变量.值01(地址DM100)" };
                reader.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "测试共享值" };
                writer.ParamForm.SetParam2Form();
                var outputs = Outputs(reader);
                Check(outputs.Count == 1 && outputs[0].ValueType == types[i], "首次运行前跨流程共享值未提供正确类型：" + types[i].Name);
                path = outputs[0].PropertyPath;
                Check(Outputs(writer).Count == 0, "写入节点不应提供读取值。");

                typeof(NodeParamFormMultiCondition).GetMethod("RefreshSourceTree", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(condition.ParamForm, null);
                var tree = (TreeView)condition.ParamForm.GetType().GetField("treeViewSources", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(condition.ParamForm);
                Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.Contains("共享读取") && item.Nodes.Count == 1),
                    "多条件参数树没有共享变量实际值。");

                ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult(samples[i], types[i].Name, "DM100");
                SynchronizationContext.SetSynchronizationContext(null);
                writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                object value;
                Check(DynamicResultVariableResolver.TryGetValue(reader.Result, path, out value) && Equals(value, samples[i]),
                    "读取实际值失败：" + types[i].Name);
                Check(Evaluate(condition, path, types[i], expected[i]), "多条件相等判断失败：" + types[i].Name);
                Check(!Evaluate(condition, path, types[i], i == 0 ? "False" : "999"), "多条件不相等时仍输出通过。");
                Check(((NodeResultSharedVariable)reader.Result).Value.Data.Equals(samples[i]), "旧包装输出被破坏。");
                // 现场方案使用整包读取结果，必须与动态单值订阅同时覆盖，防止只修复一种配置。
                ((NodeParamSharedVariable)writer.ParamForm.Params).Text2 = "读取结果";
                writer.ParamForm.SetParam2Form();
                Check(Outputs(reader).SingleOrDefault()?.ValueType == types[i], "PLC整包首次运行前类型推导失败：" + types[i].Name);
                writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(Evaluate(condition, path, types[i], expected[i]), "PLC整包实际值比较失败：" + types[i].Name);
                Check(((NodeResultSharedVariable)reader.Result).Value.Data is PlcReadResult, "PLC整包旧输出不应被改为标量。");
            }

            plc.NodeName = "重命名后的PLC读取";
            Check(Outputs(reader).SingleOrDefault()?.ValueType == typeof(string), "写入源重命名后丢失共享值类型。");
            var savedWriterParam = JsonConvert.DeserializeObject<NodeParamSharedVariable>(JsonConvert.SerializeObject(writer.ParamForm.Params));
            ((Form)writer.ParamForm).Dispose();
            writer.ParamForm = new NodeParamFormSharedVariable();
            writer.ParamForm.SetNodeBelong(writer);
            writer.ParamForm.Params = savedWriterParam;
            writer.ParamForm.SetParam2Form();
            writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(string), "0012中文"), "改名后重建窗体不能恢复实际写入订阅。");
            plc.ParamForm.Params = new NodeParamPlcRead { Address = "DM100", DataType = "Boolean" };
            Check(Outputs(reader).SingleOrDefault()?.ValueType == typeof(bool), "配置改为布尔后仍沿用旧字符串类型。");
            plc.ParamForm.Params = new NodeParamPlcRead { Address = "MR100-MR101", DataType = "Boolean" };
            Check(Outputs(reader).Count == 0, "PLC多地址整包不能冒充单值。");
            ((NodeParamSharedVariable)reader.ParamForm.Params).Flag = true;
            ((NodeParamSharedVariable)reader.ParamForm.Params).Index = 1;
            Check(Outputs(reader).SingleOrDefault()?.ValueType == typeof(bool), "PLC多地址整包未提供索引元素类型。");
            ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult(new[] { false, true }, "Boolean[]", "MR100-MR101");
            writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(bool), "True"), "PLC多地址整包索引元素比较失败。");

            writer.ParamForm.Params = null;
            reader.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "数组值", Flag = true, Index = 1 };
            Solution.Instance.SharedVariable.SetValue("数组值", new SharedVarValue(new[] { false, true }, typeof(bool[])));
            Check(Outputs(reader).Single().ValueType == typeof(bool), "按索引读取没有识别元素类型。");
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(bool), "True"), "数组元素不能参与多条件判断。");
            ((NodeParamSharedVariable)reader.ParamForm.Params).Flag = false;
            Check(Outputs(reader).Count == 0, "整个数组不能冒充多条件单值。");
            Solution.Instance.SharedVariable.SetValue("数组值", new SharedVarValue(new[] { "A", "B" }, typeof(string[])));
            Check(Outputs(reader).Count == 0, "字符串数组不能被当作单个文本订阅。");
            Solution.Instance.SharedVariable.SetValue("数组值", new SharedVarValue(new List<string> { "A", "B" }, typeof(List<string>)));
            ((NodeParamSharedVariable)reader.ParamForm.Params).Flag = true;
            Check(Outputs(reader).Single().ValueType == typeof(string), "列表索引未识别字符串类型。");
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(string), "B"), "列表索引值比较失败。");
            Solution.Instance.SharedVariable.SetValue("数组值", new SharedVarValue(
                new SharedVarValue(new[] { false, true }, typeof(bool[])), typeof(SharedVarValue)));
            Check(Outputs(reader).Single().ValueType == typeof(bool), "嵌套数组未识别元素类型。");
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(bool), "True"), "嵌套数组按索引读取失败。");
            Solution.Instance.SharedVariable.SetValue("数组值", new SharedVarValue(new string[] { "A", null }, typeof(string[])));
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(!Evaluate(condition, path, typeof(string), "A"), "数组空元素应作为空值判定。");

            reader.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "嵌套值" };
            Solution.Instance.SharedVariable.SetValue("嵌套值", new SharedVarValue(new SharedVarValue("0007", typeof(string)), typeof(SharedVarValue)));
            Check(Outputs(reader).Single().ValueType == typeof(string), "嵌套共享包装未识别实际字符串。");
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(Evaluate(condition, path, typeof(string), "0007"), "嵌套字符串比较丢失前导零。");

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                try { reader.Run(canceled.Token, false).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
                object canceledValue;
                Check(!DynamicResultVariableResolver.TryGetValue(reader.Result, path, out canceledValue), "取消后仍暴露旧值。");
            }
            Solution.Instance.SharedVariable.SetValue("嵌套值", new SharedVarValue(null, typeof(string)));
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(!Evaluate(condition, path, typeof(string), "0007"), "空值错误判定为通过。");
            Solution.Instance.SharedVariable.SetValue("嵌套值", new SharedVarValue("0007", typeof(string)));
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();

            Solution.Instance.SharedVariable.RemoveOne("嵌套值");
            bool failed = false;
            try { reader.Run(CancellationToken.None, false).GetAwaiter().GetResult(); }
            catch (Exception) { failed = true; }
            object staleValue;
            Check(failed && !DynamicResultVariableResolver.TryGetValue(reader.Result, path, out staleValue), "读取失败后仍暴露旧值。");
            Check(!Evaluate(condition, path, typeof(string), "0007"), "读取失败后多条件误用旧值判定通过。");
            reader.ParamForm.Params = null;
            Check(Outputs(reader).Count == 0, "删除参数后仍显示旧输出。");
            Console.WriteLine("PASS: 共享变量订阅 {0} 项断言。", _checks);
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            Solution.Instance.AllProcesses.Remove(writeProcess);
            Solution.Instance.AllProcesses.Remove(readProcess);
            foreach (NodeBase node in nodes) { ((Form)node.ParamForm).Dispose(); node.Dispose(); }
            Solution.Instance.SharedVariable.ClearAll();
        }
    }

    /// <summary>直接读取现场方案的参数、分组和连线，离线重建订阅链路，不初始化设备或修改原方案。</summary>
    private static void VerifyProvidedSolution(string solutionPath)
    {
        JObject solution = JObject.Parse(File.ReadAllText(solutionPath));
        var processes = new List<TDJS_Vision.Process>();
        var nodes = new Dictionary<int, NodeBase>();
        try
        {
            foreach (JToken processInfo in solution["ProcessInfos"])
            {
                var process = new TDJS_Vision.Process((string)processInfo["ProcessName"])
                { Group = (ProcessGroup)Enum.Parse(typeof(ProcessGroup), (string)processInfo["Group"]) };
                processes.Add(process);
                Solution.Instance.AllProcesses.Add(process);
                foreach (JToken nodeInfo in processInfo["NodeInfos"])
                {
                    int id = (int)nodeInfo["ID"];
                    string name = (string)nodeInfo["NodeName"];
                    var type = (NodeType)Enum.Parse(typeof(NodeType), (string)nodeInfo["NodeType"]);
                    NodeBase node;
                    switch (type)
                    {
                        case NodeType.PLCRead: node = new NodePlcRead(id, name, process, type); break;
                        case NodeType.SharedVariable: node = new NodeSharedVariable(id, name, process, type); break;
                        case NodeType.MultiCondition: node = new NodeMultiCondition(id, name, process, type); break;
                        default: node = new NodeBase(id, name, process, type); break;
                    }
                    process.Nodes.Add(node);
                    nodes.Add(id, node);
                    if (node.ParamForm != null && nodeInfo["NodeParam"] is JObject paramObject)
                    {
                        JProperty param = paramObject.Properties().Single();
                        node.ParamForm.Params = (INodeParam)param.Value.ToObject(typeof(NodeBase).Assembly.GetType(param.Name, true));
                    }
                }
                process.Connections.AddRange(processInfo["ConnectionInfos"].ToObject<List<ProcessConnection>>());
                process.NotifyConnectionsChanged();
            }
            foreach (NodeSharedVariable shared in nodes.Values.OfType<NodeSharedVariable>()) shared.ParamForm.SetParam2Form();
            var plc = (NodePlcRead)nodes[10];
            var writer = (NodeSharedVariable)nodes[12];
            var reader = (NodeSharedVariable)nodes[14];
            var condition = (NodeMultiCondition)nodes[18];
            Check(writer.Process.Group != reader.Process.Group, "现场方案未保留跨组结构。");
            Check(((NodeParamSharedVariable)writer.ParamForm.Params).Text2 == "读取结果", "测试必须保留现场PLC整包订阅。");
            var outputs = Outputs(reader);
            Check(outputs.Count == 1 && outputs[0].ValueType == typeof(string), "现场方案14号共享节点首次运行前仍未提供字符串端口。");
            typeof(NodeParamFormMultiCondition).GetMethod("RefreshSourceTree", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(condition.ParamForm, null);
            var tree = (TreeView)condition.ParamForm.GetType().GetField("treeViewSources", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(condition.ParamForm);
            Check(tree.Nodes.Cast<TreeNode>().Any(item => item.Text.StartsWith("14.") && item.Nodes.Count == 1),
                "现场方案18号多条件树仍缺少14号共享变量。");
            // 先创建真实句柄，再展开树项，确保导出的参数树包含可选实际值。
            IntPtr treeHandle = tree.Handle;
            tree.ExpandAll();
            using (var preview = new Bitmap(tree.Width, tree.Height))
            {
                tree.DrawToBitmap(preview, new Rectangle(Point.Empty, tree.Size));
                preview.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "共享变量现场方案订阅树.png"));
            }
            var plcParam = (NodeParamPlcRead)plc.ParamForm.Params;
            ((NodeResultPlcRead)plc.Result).ReadResult = new PlcReadResult("0012工艺A", "String", plcParam.Address);
            SynchronizationContext.SetSynchronizationContext(null);
            writer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            reader.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(((NodeResultSharedVariable)reader.Result).Value.Data is PlcReadResult, "旧整包输出必须保留PLC结果对象。");
            object actual;
            Check(DynamicResultVariableResolver.TryGetValue(reader.Result, outputs[0].PropertyPath, out actual) && Equals(actual, "0012工艺A"),
                "现场方案共享变量没有解出PLC字符串。");
            var conditionParam = condition.ParamForm.Params as NodeParamMultiCondition ?? new NodeParamMultiCondition();
            condition.ParamForm.Params = conditionParam;
            conditionParam.Conditions.Add(new MultiConditionItem { Name = "工艺名称验证", SourceNodeId = 14,
                PropertyPath = outputs[0].PropertyPath, ValueTypeName = typeof(string).FullName,
                Operator = MultiConditionOperator.Equals, Value1 = "0012工艺A" });
            condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(((NodeResultMultiCondition)condition.Result).ConditionResult, "现场方案工艺名称相等判断失败。");
            conditionParam.Conditions[0].Value1 = "其他工艺";
            condition.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check(!((NodeResultMultiCondition)condition.Result).ConditionResult, "现场方案工艺名称不相等却判定通过。");
            Console.WriteLine("PASS: 现场方案10→12跨组14→18，首次配置订阅树及实际工艺字符串判断。");
        }
        finally
        {
            foreach (var process in processes) Solution.Instance.AllProcesses.Remove(process);
            foreach (NodeBase node in nodes.Values) { (node.ParamForm as Form)?.Dispose(); node.Dispose(); }
            Solution.Instance.SharedVariable.ClearAll();
        }
    }
}
