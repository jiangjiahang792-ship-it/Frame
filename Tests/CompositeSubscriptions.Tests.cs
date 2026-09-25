using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using System.Collections.Generic;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._2_ImagePreprocessing.ImageRotate;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Forms.ImageViewer;

/// <summary>用现场方案副本验证组合订阅的恢复、拖入和手动选择。</summary>
internal static class CompositeSubscriptionsTests
{
    /// <summary>断言计数。</summary>
    private static int _checks;
    /// <summary>读取控件字段。</summary>
    private static T Field<T>(object owner, string name) where T : class
    { return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner); }
    /// <summary>验证行为并提供失败上下文。</summary>
    private static void Check(bool value, string message)
    { _checks++; if (!value) throw new Exception(message); }
    /// <summary>旧方案只要节点编号仍存在，订阅来源名称变更后也必须恢复结果列表。</summary>
    private static void VerifyLegacyRenamedSourceRestoresById()
    {
        var process = new Process("旧名称订阅恢复");
        using (var source = new NodeResultOverlayDraw(267, "ROI结果绘制_左棱角", process, NodeType.ResultOverlayDraw))
        using (var consumer = new NodeImageShow(271, "图像显示", process, NodeType.ImageShow))
        {
            process.AddNode(source);
            process.AddNode(consumer);
            process.Connections.Add(new ProcessConnection { FromNodeId = source.ID, ToNodeId = consumer.ID });
            process.NotifyConnectionsChanged();
            var subscription = Field<NodeSubscription>(consumer.ParamForm, "nodeSubscription1");
            subscription.SetText("267.ROI结果绘制", "输出图像");
            var resultBox = Field<ComboBox>(subscription, "comboBox2");
            Check(subscription.GetSelectedNode() == source, "旧来源名称没有按节点ID恢复到真实节点");
            Check(subscription.GetText1() == "267.ROI结果绘制_左棱角", "恢复后没有规范为当前节点名称");
            Check(resultBox.Items.Count > 0 && resultBox.SelectedIndex >= 0 && subscription.GetText2() == "输出图像",
                "旧来源名称恢复后结果列表为空");
            source.NodeName = "ROI结果绘制_左棱角_改名";
            process.NotifyConnectionsChanged();
            subscription.SetText("267.ROI结果绘制", "输出图像");
            Check(subscription.GetSelectedNode() == source && subscription.GetText1() == "267.ROI结果绘制_左棱角_改名",
                "再次重命名后旧订阅没有按编号恢复");
        }
    }
    /// <summary>以真实图像计算验证输入、内部处理、模块输出和外部订阅的完整链路。</summary>
    private static void VerifyImageConnections()
    {
        var root = new Process("图像链路回归");
        foreach (var type in new[] { MatType.CV_8UC1, MatType.CV_8UC3 })
        using (var image = OutputImage.FromOwnedSingleImage(new Mat(5, 7, type, Scalar.All(23))))
        using (var source = new NodeCompositeInput(801, "图像源", root, NodeType.CompositeInput))
        using (var module = new NodeCompositeModule(802, "图像模块", root, NodeType.CompositeModule))
        using (var consumer = new NodeImageRotate(803, "外部旋转", root, NodeType.ImageRotate))
        {
            var ports = new List<CompositeInputPortDefinition> { new CompositeInputPortDefinition { Name = "图像", ValueType = CompositePortValueType.Image } };
            source.ParamForm.Params = new NodeParamCompositeInput { Ports = ports };
            source.SetRuntimeInputValues(new Dictionary<string, object> { { "图像", image } });
            source.Run(CancellationToken.None, false).GetAwaiter().GetResult();
            root.AddNode(source); root.AddNode(module); root.AddNode(consumer);
            var inputEdge = new ProcessConnection { FromNodeId = source.ID, ToNodeId = module.ID };
            var outputEdge = new ProcessConnection { FromNodeId = module.ID, ToNodeId = consumer.ID };
            root.Connections.Add(inputEdge); root.Connections.Add(outputEdge);
            var bindings = new List<CompositeInputBinding> { new CompositeInputBinding { PortName = "图像", ValueType = CompositePortValueType.Image, SourceMode = CompositePortValueSourceMode.Subscription, SourceNodeId = source.ID, PropertyPath = "$variable:图像" } };
            module.UpdateInputPorts(ports, bindings);
            var inner = module.GetEditableProcess();
            var rotate = new NodeImageRotate(804, "内部旋转", inner, NodeType.ImageRotate);
            inner.AddNode(rotate);
            rotate.ParamForm.Params = new NodeParamImageRotate { Angle = 0, Text1 = int.MaxValue + ".模块输入", Text2 = "图像" };
            rotate.ParamForm.SetParam2Form();
            var outputs = new List<CompositeOutputPortDefinition> { new CompositeOutputPortDefinition { Name = "处理图像", ValueType = CompositePortValueType.Image, SourceNodeId = rotate.ID, PropertyPath = "OutputImage" } };
            module.UpdateModulePorts(ports, bindings, outputs);
            root.NotifyConnectionsChanged();
            var subscription = Field<NodeSubscription>(consumer.ParamForm, "nodeSubscription1");
            subscription.SetText(module.ID + "." + module.NodeName, "处理图像");
            consumer.ParamForm.Params = new NodeParamImageRotate { Angle = 0 };
            for (int pass = 0; pass < 5; pass++)
            {
                module.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                var value = subscription.GetValue<OutputImage>();
                Check(value.SrcImg.Width == 7 && value.SrcImg.At<byte>(0, 0) == 23, "内部图像处理或外部输出订阅错误");
                consumer.Run(CancellationToken.None, false).GetAwaiter().GetResult();
                Check(((NodeResultImageRotate)consumer.Result).OutputImage.SrcImg.Width == 7 && !image.SrcImg.IsDisposed, "外部消费者连续运行释放了借用的源图像");
                root.Connections.Remove(outputEdge); root.NotifyConnectionsChanged();
                bool unavailable = false;
                try { subscription.GetSelectedNode(); } catch { unavailable = true; }
                Check(unavailable, "删除外部连线后仍能选择已断开的模块");
                root.Connections.Add(outputEdge); root.NotifyConnectionsChanged();
                Check(subscription.GetSelectedNode() == module && ReferenceEquals(value, subscription.GetValue<OutputImage>()), "重新连线没有恢复原输出订阅");
                var resultBox = Field<ComboBox>(subscription, "comboBox2");
                resultBox.SelectedIndex = -1; resultBox.SelectedIndex = 0;
                Check(subscription.GetValue<OutputImage>() != null, "手动重选输出失败");
            }
            root.Nodes.Clear();
            root.Connections.Clear();
        }
    }
    /// <summary>确认真实目标分类参数界面能取得作用域图像。</summary>
    private static void VerifySelector(NodeBase node, Process process, bool restored = true)
    {
        var subscription = Field<NodeSubscription>(node.ParamForm, "nodeSubscription1");
        if (restored) Check(subscription.GetSelectedNode() == process.ModuleInputSource.SubscriptionNode, "恢复方案后未解析模块输入：" + subscription.GetText1());
        var sources = Field<ComboBox>(subscription, "comboBox1");
        var results = Field<ComboBox>(subscription, "comboBox2");
        for (int pass = 0; pass < 5; pass++)
        {
            sources.SelectedIndex = -1;
            sources.SelectedIndex = 0;
            Check(sources.Items.Count == 1, "手动选择模块输入出现重复来源");
            Check(results.Items.Count == 1 && results.SelectedIndex == 0, "模块输入图像没有出现在结果框");
            results.SelectedIndex = -1;
            results.SelectedIndex = 0;
            Check(subscription.GetText1() == int.MaxValue + ".模块输入", "手动选择未保存稳定标识");
        }
        subscription.SetText("模块输入", "");
        Check(subscription.GetSelectedNode() == process.ModuleInputSource.SubscriptionNode && results.SelectedIndex == 0, "旧错误显示名未迁移为有效订阅");
    }
    /// <summary>隔离设备和模型加载，仅恢复方案中的流程与参数控件。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        LanguageManager.Initialize();
        try
        {
            var gate = typeof(Process).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null))
            {
                VerifyImageConnections();
                VerifyLegacyRenamedSourceRestoresById();
                var scheme = JObject.Parse(File.ReadAllText(args[0]));
                foreach (string windowName in scheme.Descendants().OfType<JProperty>().Where(p => p.Name == "WindowName")
                    .Select(p => (string)p.Value).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct())
                    FrmImageViewer.FrmSingleImages.Add(new FrmSingleImage(windowName));
                var config = scheme["ProcessInfos"][0].ToObject<ProcessConfig>();
                using (var form = new FormNewProcessWizard())
                using (var editor = new ProcessEditPanel(config.ProcessName, false, config))
                {
                    var root = editor.RootProcess;
                    var module = root.Nodes.OfType<NodeCompositeModule>().Single();
                    var process = module.GetEditableProcess();
                    VerifySelector(process.Nodes.Single(n => n.NodeType == NodeType.AITD), process, false);
                    // 原现场参数来源为空但残留旧结果名；先手动修复，再模拟保存后的重新打开。
                    var targetParam = process.Nodes.Single().ParamForm.Params;
                    targetParam.GetType().GetProperty("Text1").SetValue(targetParam, int.MaxValue + ".模块输入");
                    targetParam.GetType().GetProperty("Text2").SetValue(targetParam, "输入图像");
                    module.SaveEditableProcess();
                    module.ParamForm.Params = Newtonsoft.Json.JsonConvert.DeserializeObject<NodeParamCompositeModule>(
                        Newtonsoft.Json.JsonConvert.SerializeObject(module.ParamForm.Params));
                    process = module.GetEditableProcess();
                    VerifySelector(process.Nodes.Single(), process);
                    editor.NavigateToComposite(new[] { module.ID });
                    var canvas = Field<ProcessFlowCanvas>(editor, "processFlowCanvas");
                    var data = new DataObject("DragData", new FormNewProcessWizard.DragData("目标分类回归", NodeType.AITD));
                    typeof(ProcessEditPanel).GetMethod("NodeEditPanel_DragDrop", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(editor, new object[] { canvas, new DragEventArgs(data, 0, 400, 300, DragDropEffects.Copy, DragDropEffects.Copy) });
                    Check(process.Nodes.Count == 2, "真实拖入入口未创建目标分类节点");
                    VerifySelector(process.Nodes.Last(), process);
                    Check(typeof(ProcessEditPanel).GetField("buttonCompositeSettings", BindingFlags.Instance | BindingFlags.NonPublic) == null, "组合内部仍保留模块设置按钮");
                    Check(((Form)module.ParamForm).Padding == Padding.Empty, "变量窗口仍有外侧白色框");
                    if (args.Contains("--preview"))
                    {
                        var tabs = Field<TabControl>(form, "tabControl1");
                        var page = new TabPage(config.ProcessName);
                        editor.Dock = DockStyle.Fill; page.Controls.Add(editor); tabs.TabPages.Add(page);
                        form.Text = "组合订阅验证（Fakra副本）"; form.Size = new System.Drawing.Size(1280, 820);
                        Application.Run(form);
                    }
                }
            }
            Console.WriteLine("PASS: 现场方案组合订阅 {0} 项断言", _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

