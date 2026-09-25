using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._6_LogicTool.If;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
/// <summary>真实程序集组合编辑行为测试。</summary>
internal static class CompositeEditingTests
{
    /// <summary>断言数。</summary>
    private static int _checks;
    /// <summary>验证条件。</summary>
    private static void Check(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    /// <summary>调用可编辑内部流程入口，兼容旧程序集编译以验证失败。</summary>
    private static Process Open(NodeCompositeModule node) { var method = node.GetType().GetMethod("GetEditableProcess"); Check(method != null, "缺少 GetEditableProcess 入口"); return (Process)method.Invoke(node, null); }
    /// <summary>保存当前内部编辑。</summary>
    private static void Save(NodeCompositeModule node) { node.GetType().GetMethod("SaveEditableProcess").Invoke(node, null); }
    /// <summary>实际执行模块，验证输入绑定、自动连线、图像与原始对象不丢类型。</summary>
    private static void VerifyRun()
    {
        var parent = new Process("外部流程");
        using (var image = OutputImage.FromOwnedSingleImage(new Mat(2, 3, MatType.CV_8UC1, Scalar.All(8))))
        using (var source = new NodeCompositeInput(99, "外部源", parent, NodeType.CompositeInput))
        using (var module = new NodeCompositeModule(1, "运行模块", parent, NodeType.CompositeModule))
        {
            var raw = new List<int> { 3, 8 };
            var values = new Dictionary<string, object> { { "判定", true }, { "图像", image }, { "原对象", raw } };
            var ports = values.Select(pair => new CompositeInputPortDefinition { Name = pair.Key, ValueType = pair.Key == "判定" ? CompositePortValueType.Boolean : pair.Key == "图像" ? CompositePortValueType.Image : CompositePortValueType.Any }).ToList();
            source.ParamForm.Params = new NodeParamCompositeInput { Ports = ports };
            source.SetRuntimeInputValues(values);
            source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            parent.AddNode(source);
            var process = Open(module);
            var input = new NodeCompositeInput(2, "内部输入", process, NodeType.CompositeInput);
            input.ParamForm.Params = new NodeParamCompositeInput { Ports = ports };
            process.AddNode(input);
            var output = new NodeCompositeOutput(3, "内部输出", process, NodeType.CompositeOutput);
            output.ParamForm.Params = new NodeParamCompositeOutput { Ports = ports.Select(port => new CompositeOutputPortDefinition { Name = port.Name, ValueType = port.ValueType, ValueTypeName = values[port.Name].GetType().AssemblyQualifiedName, SourceNodeId = input.ID, PropertyPath = DynamicResultVariableResolver.ToPropertyPath(port.Name) }).ToList() };
            process.AddNode(output);
            Save(module);
            var param = (NodeParamCompositeModule)module.ParamForm.Params;
            foreach (var binding in param.InputBindings) { binding.SourceMode = CompositePortValueSourceMode.Subscription; binding.SourceNodeId = source.ID; binding.PropertyPath = DynamicResultVariableResolver.ToPropertyPath(binding.PortName); }
            Check(process.Connections.Any(edge => edge.FromNodeId == input.ID && edge.ToNodeId == output.ID), "配置输出须自动补齐运行连线");
            Task.Run(() => module.Run(CancellationToken.None, false)).GetAwaiter().GetResult();
            var result = (NodeResultCompositeModule)module.Result;
            Check(result.InternalSuccess && result.OutputValues["判定"] is bool && (bool)result.OutputValues["判定"], "内部流程须实际发布布尔结果");
            Check(ReferenceEquals(result.OutputValues["图像"], image), "图像输出不得转换或释放");
            Check(ReferenceEquals(result.OutputValues["原对象"], raw), "自动类型必须保留原始对象引用");
            Type type;
            Check(module.TryGetDynamicResultVariableType("原对象", out type) && type == typeof(List<int>), "动态目录须返回原始对象类型");
            Check(module.TryGetDynamicResultVariableType("图像", out type) && type == typeof(OutputImage), "图像端口类型应可订阅");
            Check(ReferenceEquals(process, Open(module)), "运行不得替换编辑画布");
            var descriptors = SubscriptionPortCatalog.GetOutputs(module, SubscriptionInputContract.AnyVisible(), true, "");
            Check(descriptors.Any(item => item.PropertyPath == "$variable:原对象" && item.ValueType == typeof(List<int>)), "外部目录须提供对象端口");
            using (var direct = new NodeCompositeModule(600, "直接图像输入", parent, NodeType.CompositeModule))
            {
                direct.UpdateInputPorts(ports, param.InputBindings);
                var directProcess = Open(direct);
                direct.UpdateModulePorts(ports, param.InputBindings, ports.Select(port => new CompositeOutputPortDefinition
                    { Name = port.Name, ValueType = port.ValueType, SourceNodeId = directProcess.ModuleInputSource.SubscriptionNode.ID,
                      PropertyPath = DynamicResultVariableResolver.ToPropertyPath(port.Name) }).ToList());
                direct.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                var directResult = (NodeResultCompositeModule)direct.Result;
                Check(directProcess.Nodes.Count == 0 && ReferenceEquals(directResult.OutputValues["图像"], image), "直接模块图像输入错误或新增了内部输入节点");
                Check(ReferenceEquals(directResult.OutputValues["原对象"], raw), "直接模块输入丢失原始对象类型或引用");
            }
            var restored = JsonConvert.DeserializeObject<NodeParamCompositeModule>(JsonConvert.SerializeObject(param));
            module.ParamForm.Params = restored;
            var rebuilt = Open(module);
            Check(!ReferenceEquals(process, rebuilt) && rebuilt.Nodes.Count == 2, "替换参数必须重建缓存");
            Task.Run(() => module.Run(CancellationToken.None, false)).GetAwaiter().GetResult();
            Check(((NodeResultCompositeModule)module.Result).OutputValues["判定"] is bool, "快照保存恢复后须可运行");
            restored.HasCanvasGraph = false;
            restored.ConnectionInfos.Clear();
            module.ParamForm.Params = JsonConvert.DeserializeObject<NodeParamCompositeModule>(JsonConvert.SerializeObject(restored));
            Check(Open(module).Connections.Count > 0, "旧顺序快照须恢复兼容连线");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                bool rejected = false;
                try { Task.Run(() => module.Run(cancelled.Token, false)).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { rejected = true; }
                Check(rejected, "单独运行须遵守调用方取消令牌");
            }
        }
    }
    /// <summary>真实条件节点的真假结果都应被自动输出，且用户手工分支保持不变。</summary>
    private static void VerifyConditionalOutput()
    {
        foreach (bool flag in new[] { false, true })
        foreach (bool controlled in new[] { false, true })
        using (var module = new NodeCompositeModule(1, "条件输出", new Process("父流程"), NodeType.CompositeModule))
        {
            var process = Open(module);
            var input = new NodeCompositeInput(2, "布尔输入", process, NodeType.CompositeInput);
            input.ParamForm.Params = new NodeParamCompositeInput { Ports = new List<CompositeInputPortDefinition> { new CompositeInputPortDefinition { Name = "标记", ValueType = CompositePortValueType.Boolean, DefaultValue = flag.ToString() } } };
            process.AddNode(input);
            var condition = new NodeMultiCondition(3, "条件", process, NodeType.MultiCondition);
            condition.ParamForm.Params = new NodeParamMultiCondition { Conditions = new List<MultiConditionItem> { new MultiConditionItem { Name = "标记为真", SourceNodeId = 2, PropertyPath = "$variable:标记", Operator = MultiConditionOperator.IsTrue } } };
            process.AddNode(condition);
            var output = new NodeCompositeOutput(4, "组合输出", process, NodeType.CompositeOutput);
            output.ParamForm.Params = new NodeParamCompositeOutput { Ports = new List<CompositeOutputPortDefinition> { new CompositeOutputPortDefinition { Name = "判定", SourceNodeId = 3, PropertyPath = "ConditionResult", ValueType = CompositePortValueType.Boolean } } };
            process.AddNode(output);
            process.Connections.Add(new ProcessConnection { FromNodeId = 2, ToNodeId = 3, FromAnchor = "Right", ToAnchor = "Left" });
            if (controlled) process.Connections.Add(new ProcessConnection { ID = "用户条件连线", FromNodeId = 3, ToNodeId = 4, FromAnchor = "Right", ToAnchor = "Left", Branch = ProcessConnectionBranch.True });
            Save(module);
            Task.Run(() => module.Run(CancellationToken.None, false)).GetAwaiter().GetResult();
            object result;
            bool published = ((NodeResultCompositeModule)module.Result).OutputValues.TryGetValue("判定", out result);
            if (controlled && !flag) Check(!published, "用户条件分支不允许被自动依赖绕过");
            else Check(published && result is bool && (bool)result == flag, "自动输出不得丢失条件结果：" + flag);
        }
    }

    /// <summary>If 没有公共默认出口，真假分支均应发布所订阅的执行结果。</summary>
    private static void VerifyIfOutput()
    {
        foreach (bool flag in new[] { false, true })
        using (var module = new NodeCompositeModule(1, "If输出", new Process("父流程"), NodeType.CompositeModule))
        {
            var process = Open(module);
            Solution.Instance.SharedVariable.SetValue("组合If测试", new SharedVarValue(flag, typeof(bool)));
            var source = new NodeSharedVariable(2, "共享源", process, NodeType.SharedVariable);
            source.ParamForm.Params = new NodeParamSharedVariable { IsRead = true, ReadName = "组合If测试", Type = SharedVarTypeEnum.Bool };
            process.AddNode(source);
            var condition = new NodeIf(3, "判断", process, NodeType.If);
            process.AddNode(condition);
            process.Connections.Add(new ProcessConnection { FromNodeId = 2, ToNodeId = 3, FromAnchor = "Right", ToAnchor = "Left" });
            condition.ParamForm.Params = new NodeParamElse { Text1 = "2.共享源", Text2 = "读到的共享变量" };
            condition.ParamForm.SetParam2Form();
            var output = new NodeCompositeOutput(4, "输出", process, NodeType.CompositeOutput);
            output.ParamForm.Params = new NodeParamCompositeOutput { Ports = new List<CompositeOutputPortDefinition> { new CompositeOutputPortDefinition { Name = "耗时", SourceNodeId = 3, PropertyPath = "RunTime", ValueType = CompositePortValueType.Number } } };
            process.AddNode(output);
            Save(module);
            Task.Run(() => module.Run(CancellationToken.None, false)).GetAwaiter().GetResult();
            Check(((NodeResultCompositeModule)module.Result).OutputValues.ContainsKey("耗时"), "If 两个分支都应允许结果输出：" + flag);
        }
    }

    /// <summary>等效全局入口：父流程已运行，内部新节点尚未显式保存。</summary>
    private static void VerifyImplicitSaveBeforeRun()
    {
        var parent = new Process("已启动父流程");
        using (var module = new NodeCompositeModule(1, "未返回模块", parent, NodeType.CompositeModule))
        {
            var process = Open(module);
            var input = new NodeCompositeInput(2, "新输入", process, NodeType.CompositeInput);
            input.ParamForm.Params = new NodeParamCompositeInput { Ports = new List<CompositeInputPortDefinition> { new CompositeInputPortDefinition { Name = "结果", ValueType = CompositePortValueType.Boolean, DefaultValue = "true" } } };
            process.AddNode(input);
            var output = new NodeCompositeOutput(3, "新输出", process, NodeType.CompositeOutput);
            output.ParamForm.Params = new NodeParamCompositeOutput { Ports = new List<CompositeOutputPortDefinition> { new CompositeOutputPortDefinition { Name = "最新结果", SourceNodeId = 2, PropertyPath = "$variable:结果", ValueType = CompositePortValueType.Boolean } } };
            process.AddNode(output);
            parent.IsRuning = true;
            Task.Run(() => module.Run(CancellationToken.None, false)).GetAwaiter().GetResult();
            parent.IsRuning = false;
            Check(((NodeResultCompositeModule)module.Result).InternalSuccess, "全局入口须提交尚未返回的内部编辑");
            Check(((NodeParamCompositeModule)module.ParamForm.Params).NodeInfos.Count == 2, "运行前须同步新节点快照");
            Check((bool)((NodeResultCompositeModule)module.Result).OutputValues["最新结果"], "运行前须同步最新输出端口");
            Check(ReferenceEquals(process, Open(module)), "运行前保存须保持内部画布实例");
        }
    }
    /// <summary>执行无需现场设备的测试。</summary>
    [STAThread] private static int Main()
    {
        try
        {
            int count = Solution.Instance.AllProcesses.Count;
            var owner = new Process("测试父流程");
            using (var module = new NodeCompositeModule(1, "模块", owner, NodeType.CompositeModule))
            {
                var process = Open(module);
                Check(ReferenceEquals(process, Open(module)), "重复进入应复用同一流程");
                Check(process.Nodes.Count == 0, "未配置模块应为空画布");
                Check(Solution.Instance.AllProcesses.Count == count, "内部流程不得注册顶层");
                var original = (NodeParamCompositeModule)module.ParamForm.Params;
                original.SourceProcessId = 123;
                original.SourceProcessName = "来源";
                var child = new NodeCompositeModule(2, "深层模块", process, NodeType.CompositeModule);
                process.AddNode(child);
                var nested = Open(child);
                var input = new NodeCompositeInput(3, "组合输入", nested, NodeType.CompositeInput);
                input.ParamForm.Params = new NodeParamCompositeInput { Ports = new System.Collections.Generic.List<CompositeInputPortDefinition> { new CompositeInputPortDefinition { Name = "判定", ValueType = CompositePortValueType.Boolean, DefaultValue = "true" } } };
                nested.AddNode(input);
                Save(module);
                var saved = (NodeParamCompositeModule)module.ParamForm.Params;
                Check(saved.SourceProcessId == 123 && saved.SourceProcessName == "来源", "保存须保留模块来源");
                Check(((NodeParamCompositeModule)saved.NodeInfos.Single().NodeParam).NodeInfos.Count == 1, "深层编辑必须由内到外保存");
                Check(ReferenceEquals(process, Open(module)) && ReferenceEquals(nested, Open(child)), "保存不得替换画布对象");
                var childParam = (NodeParamCompositeModule)child.ParamForm.Params;
                childParam.InputBindings[0].SourceMode = CompositePortValueSourceMode.Subscription;
                childParam.InputBindings[0].SourceNodeId = 77;
                Save(module);
                Check(((NodeParamCompositeModule)module.ParamForm.Params).NodeInfos.Count == 1, "重复保存应保留子节点");
                Check(((NodeParamCompositeModule)child.ParamForm.Params).InputBindings[0].SourceNodeId == 77, "保存不得丢失外部订阅");
                process.IsRuning = true;
                Save(module);
                Check(ReferenceEquals(process, Open(module)), "运行时保存不得替换内部流程");
                process.IsRuning = false;
                Check(typeof(NodeCompositeModule).GetMethod("ConfigurePorts") != null, "缺少端口配置入口");
                var unopened = new NodeCompositeModule(5, "未打开", process, NodeType.CompositeModule);
                unopened.ParamForm.Params = new NodeParamCompositeModule();
                process.AddNode(unopened);
                Save(module);
                Check(typeof(NodeCompositeModule).GetField("_runtimeProcess", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(unopened) == null, "递归保存不得实例化未打开模块");
                process.IsRuning = true;
                bool blocked = false;
                try { module.GetType().GetMethod("ConfigurePorts").Invoke(module, new object[] { true, null }); }
                catch (TargetInvocationException ex) { blocked = ex.InnerException is InvalidOperationException; }
                Check(blocked, "运行期间必须拒绝端口配置");
                process.IsRuning = false;
            }
            VerifyDirectInputs();
            VerifyVariableDialog();
            VerifyRun();
            VerifyImplicitSaveBeforeRun();
            VerifyConditionalOutput();
            VerifyIfOutput();
            Console.WriteLine("组合编辑测试通过：" + _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>模块输入无需画布节点即可订阅，定义与运行值及时更新且可保存恢复。</summary>
    private static void VerifyDirectInputs()
    {
        var parent = new Process("直接输入验证");
        using (var module = new NodeCompositeModule(500, "直接输入", parent, NodeType.CompositeModule))
        {
            var update = typeof(NodeCompositeModule).GetMethod("UpdateInputPorts");
            Check(update != null, "缺少模块直接输入更新入口");
            var ports = new List<CompositeInputPortDefinition> { new CompositeInputPortDefinition { Name = "判定", ValueType = CompositePortValueType.Boolean, DefaultValue = "false" } };
            update.Invoke(module, new object[] { ports, new List<CompositeInputBinding>() });
            var process = Open(module);
            Check(process.Nodes.Count == 0, "定义模块输入不应添加画布节点");
            var output = new NodeCompositeOutput(1, "结果", process, NodeType.CompositeOutput);
            process.AddNode(output);
            var source = process.GetUpstreamNodes(output).Single(n => n.NodeName == "模块输入");
            Check(NearestSubscriptionSourceSelector.Instance.GetUpstreamNodes(output).Contains(source), "公共订阅控件未显示模块输入");
            output.ParamForm.Params = new NodeParamCompositeOutput { Ports = new List<CompositeOutputPortDefinition> { new CompositeOutputPortDefinition { Name = "判定", ValueType = CompositePortValueType.Boolean, SourceNodeId = source.ID, PropertyPath = "$variable:判定" } } };
            module.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check((bool)((NodeResultCompositeModule)module.Result).OutputValues["判定"] == false, "直接模块输入未发布当轮值");
            using (var subscription = new NodeSubscription())
            {
                subscription.SetExpectedValueType<bool>();
                subscription.Init(output);
                Check(ReferenceEquals(subscription.GetSelectedNode(), source), "实际订阅控件不能直接选择模块输入");
                Check(subscription.GetValue<bool>() == false, "实际订阅控件无法读取模块输入值");
                var sourceCombo = (System.Windows.Forms.ComboBox)typeof(NodeSubscription).GetField("comboBox1", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(subscription);
                var resultCombo = (System.Windows.Forms.ComboBox)typeof(NodeSubscription).GetField("comboBox2", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(subscription);
                for (int index = 0; index < 5; index++)
                {
                    sourceCombo.SelectedIndex = -1;
                    sourceCombo.SelectedIndex = 0;
                    Check(sourceCombo.Items.Count == 1, "手动选择模块输入产生重复来源");
                    Check(resultCombo.Items.Count == 1 && subscription.GetValue<bool>() == false, "手动选择后右侧变量或订阅值丢失");
                }
            }
            ports[0].DefaultValue = "true";
            update.Invoke(module, new object[] { ports, new List<CompositeInputBinding>() });
            module.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check((bool)((NodeResultCompositeModule)module.Result).OutputValues["判定"], "修改输入后沿用了旧值");
            Check(ReferenceEquals(source, process.GetUpstreamNodes(output).Single(n => n.NodeName == "模块输入")), "输入更新替换了订阅源对象");
            module.GetType().GetMethod("UpdateModulePorts").Invoke(module, new object[] { ports, new List<CompositeInputBinding>(), ((NodeParamCompositeOutput)output.ParamForm.Params).Ports });
            process.Nodes.Remove(output);
            output.Dispose();
            module.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            Check((bool)((NodeResultCompositeModule)module.Result).OutputValues["判定"] && process.Nodes.Count == 0, "直接输入输出仍依赖额外画布节点");
            ports[0].Name = "阈值"; ports[0].ValueType = CompositePortValueType.Number; ports[0].DefaultValue = "3";
            update.Invoke(module, new object[] { ports, new List<CompositeInputBinding>() });
            var outputs = SubscriptionPortCatalog.GetOutputs(source, SubscriptionInputContract.AnyVisible(), true, "");
            Check(outputs.Any(p => p.PropertyPath == "$variable:阈值" && p.ValueType == typeof(double)) && !outputs.Any(p => p.PropertyPath == "$variable:判定"), "改名改类型未及时更新订阅目录");
            Save(module);
            var param = (NodeParamCompositeModule)module.ParamForm.Params;
            Check(param.InputPorts.Single().Name == "阈值" && !param.NodeInfos.Any(n => n.NodeType == NodeType.CompositeInput), "保存丢失模块输入或产生额外输入节点");
            module.ParamForm.Params = JsonConvert.DeserializeObject<NodeParamCompositeModule>(JsonConvert.SerializeObject(param));
            var restored = Open(module);
            Check(restored.ModuleInputSource != null && restored.Nodes.Count == 0, "恢复方案丢失直接输入源或添加了画布节点");
        }
    }

    /// <summary>实际打开统一变量窗体，验证新增保存和取消均无需内部输入节点。</summary>
    private static void VerifyVariableDialog()
    {
        using (var module = new NodeCompositeModule(700, "变量窗口", new Process("窗体验证"), NodeType.CompositeModule))
        using (var timer = new System.Windows.Forms.Timer { Interval = 150 })
        {
            Exception failure = null;
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                try
                {
                    var form = (NodeParamFormCompositeModule)module.ParamForm;
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var tabs = (System.Windows.Forms.TabControl)form.GetType().GetField("tabVariables", flags).GetValue(form);
                    Check(tabs.TabPages.Count == 2, "齿轮窗口未提供输入输出分页");
                    var split = (System.Windows.Forms.SplitContainer)form.GetType().GetField("splitContainerPorts", flags).GetValue(form);
                    Check(split.Panel2.Width > 200, "分页布局把外部订阅树压缩到不可用宽度");
                    ((System.Windows.Forms.Button)form.GetType().GetField("buttonAddInput", flags).GetValue(form)).PerformClick();
                    var grid = (System.Windows.Forms.DataGridView)form.GetType().GetField("dataGridViewInputBindings", flags).GetValue(form);
                    grid.Rows[0].Cells["ColumnInputName"].Value = "即时值";
                    grid.Rows[0].Cells["ColumnInputType"].Value = CompositePortValueType.Boolean;
                    grid.Rows[0].Cells["ColumnInputConstant"].Value = "true";
                    ((System.Windows.Forms.Button)form.GetType().GetField("buttonSave", flags).GetValue(form)).PerformClick();
                }
                catch (Exception ex) { failure = ex; ((System.Windows.Forms.Form)module.ParamForm).Hide(); }
            };
            timer.Start();
            module.ConfigurePorts(false, null);
            if (failure != null) throw failure;
            var param = (NodeParamCompositeModule)module.ParamForm.Params;
            Check(param.InputPorts.Single().Name == "即时值" && param.InputPorts.Single().ValueType == CompositePortValueType.Boolean, "窗体保存没有更新直接输入");
            Check(Open(module).Nodes.Count == 0, "变量窗口额外创建输入或输出节点");
            using (var cancelTimer = new System.Windows.Forms.Timer { Interval = 150 })
            {
                cancelTimer.Tick += (sender, args) =>
                {
                    cancelTimer.Stop();
                    var form = (NodeParamFormCompositeModule)module.ParamForm;
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    ((System.Windows.Forms.Button)form.GetType().GetField("buttonAddInput", flags).GetValue(form)).PerformClick();
                    ((System.Windows.Forms.Button)form.GetType().GetField("buttonCancel", flags).GetValue(form)).PerformClick();
                };
                cancelTimer.Start();
                Check(!module.ConfigurePorts(false, null), "取消变量编辑错误地报告保存成功");
                Check(((NodeParamCompositeModule)module.ParamForm.Params).InputPorts.Count == 1, "取消变量编辑仍然保存了新增输入");
            }
        }
    }
}
