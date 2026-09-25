using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

/// <summary>用真实程序恢复迁移方案，验证层级、快照和界面订阅；不执行设备节点。</summary>
internal static class StationCompositeMigrationTests
{
    /// <summary>累计验证数。</summary>
    private static int _checks;
    /// <summary>独立记录原方案已有的订阅问题，迁移前后对比而不静默忽略。</summary>
    private static readonly List<string> SubscriptionIssues = new List<string>();
    /// <summary>递归统计方案节点，组合模块内部节点也计入总数。</summary>
    private static int CountNodeInfos(JToken scope)
    {
        int total = 0;
        var nodeInfos = scope["NodeInfos"] as JArray;
        if (nodeInfos == null) return total;
        foreach (var node in nodeInfos)
        {
            total++;
            if ((string)node["NodeType"] != "CompositeModule") continue;
            var parameter = node["NodeParam"]?["TDJS_Vision.Node._6_LogicTool.CompositeModule.NodeParamCompositeModule"];
            if (parameter != null) total += CountNodeInfos(parameter);
        }
        return total;
    }

    /// <summary>计算顶层工位主流程数量，用于校验组合模块导航次数。</summary>
    private static int CountStationMainProcesses(JObject scheme)
    {
        return scheme["ProcessInfos"].Count(token => ((string)token["ProcessName"] ?? string.Empty).EndsWith("主流程"));
    }

    /// <summary>验证条件。</summary>
    private static void Check(bool condition, string message)
    { _checks++; if (!condition) throw new Exception(message); }
    /// <summary>枚举参数窗体中真实订阅控件。</summary>
    private static IEnumerable<NodeSubscription> Subscriptions(Control control)
    {
        if (control is NodeSubscription subscription) yield return subscription;
        foreach (Control child in control.Controls)
            foreach (var item in Subscriptions(child)) yield return item;
    }
    /// <summary>验证当前作用域，并递归打开所有组合模块。</summary>
    private static int Verify(Process process)
    {
        int total = process.Nodes.Count;
        foreach (var node in process.Nodes)
        {
            Check(node.ParamForm.Params != null, "节点参数未恢复：" + node.NodeName);
            foreach (var subscription in Subscriptions((Control)node.ParamForm))
            {
                // 共享变量读取模式不使用写入页保留的历史订阅文本。
                if (node.ParamForm.Params is TDJS_Vision.Node._6_LogicTool.SharedVariable.NodeParamSharedVariable shared && shared.IsRead) continue;
                if (string.IsNullOrWhiteSpace(subscription.GetText1())) continue;
                try { Check(subscription.GetSelectedNode() != null, "来源未恢复：" + node.NodeName); }
                catch { SubscriptionIssues.Add("节点 " + node.ID + "." + node.NodeName + " 订阅来源失效：" + subscription.GetText1() + " / " + subscription.GetText2()); continue; }
                var descriptor = (SubscriptionOutputDescriptor)typeof(NodeSubscription)
                    .GetField("_selectedOutput", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(subscription);
                if (descriptor == null || descriptor.IsMissing) SubscriptionIssues.Add("节点 " + node.ID + "." + node.NodeName + " 订阅结果失效：" + subscription.GetText2());
                else Check(true, "订阅结果已恢复");
            }
            var module = node as NodeCompositeModule;
            if (module == null) continue;
            var param = (NodeParamCompositeModule)module.ParamForm.Params;
            var child = module.GetEditableProcess();
            Check(child.Nodes.Count == param.NodeInfos.Count && child.Connections.Count == param.ConnectionInfos.Count, "内部图恢复不完整");
            total += Verify(child);
            var before = JsonConvert.SerializeObject(param.NodeInfos);
            module.SaveEditableProcess();
            var after = JsonConvert.SerializeObject(((NodeParamCompositeModule)module.ParamForm.Params).NodeInfos);
            var beforeToken = JArray.Parse(before);
            var afterToken = JArray.Parse(after);
            // 打开内部画布会清除编辑器选中态，它不属于运行配置。
            foreach (var token in new[] { beforeToken, afterToken })
                foreach (var selected in token.Descendants().OfType<JProperty>().Where(p => p.Name == "Selected")) selected.Value = false;
            Check(JToken.DeepEquals(beforeToken, afterToken), "打开和保存内部流程改变了原始节点参数：" + module.NodeName);
        }
        return total;
    }
    /// <summary>加载全部顶层流程后，再打开内部模块，使共享变量类型目录完整。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        LanguageManager.Initialize();
        var editors = new List<ProcessEditPanel>();
        try
        {
            var scheme = JObject.Parse(File.ReadAllText(args[0]));
            int expectedTopLevelProcesses = scheme["ProcessInfos"].Count();
            int expectedNodeCount = scheme["ProcessInfos"].Sum(CountNodeInfos);
            int expectedStationCount = CountStationMainProcesses(scheme);
            // 离线宿主创建方案引用的显示窗口，与主程序先初始化窗口再加载方案的顺序一致。
            foreach (string name in scheme.Descendants().OfType<JProperty>().Where(p => p.Name == "WindowName")
                .Select(p => (string)p.Value).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
                FrmImageViewer.FrmSingleImages.Add(new FrmSingleImage(name));
            foreach (var token in scheme["ProcessInfos"])
            {
                var config = token.ToObject<ProcessConfig>();
                editors.Add(new ProcessEditPanel(config.ProcessName, false, config));
            }
            bool baseline = args.Contains("--baseline");
            Check(editors.Count == expectedTopLevelProcesses, "顶层流程数量错误");
            Check(editors.Sum(editor => Verify(editor.RootProcess)) == expectedNodeCount, "递归节点数量错误");
            Check(baseline || editors.Count(e => e.RootProcess.ProcessName.EndsWith("主流程")) == expectedStationCount, "工位主流程数量错误");
            foreach (var editor in editors.Where(e => !baseline && e.RootProcess.ProcessName.EndsWith("主流程")))
            {
                var recipe = editor.RootProcess.Nodes.OfType<NodeCompositeModule>().Single();
                int recipeNodeCount = recipe.GetEditableProcess().Nodes.Count;
                var save = recipe.GetEditableProcess().Nodes.OfType<NodeCompositeModule>().Single();
                Check(editor.NavigateToComposite(new[] { recipe.ID, save.ID }), "无法进入嵌套存图模块");
                Check(editor.CurrentProcess.Nodes.Count == 17, "存图模块节点丢失");
                Check(editor.NavigateToParent() && editor.CurrentProcess.Nodes.Count == recipeNodeCount, "返回配方模块错误");
                Check(editor.NavigateToParent() && editor.CurrentProcess == editor.RootProcess, "返回工位主流程错误");
            }
            Check(Solution.Instance.AllProcesses.Count == editors.Count, "内部模块意外注册到顶层");
            foreach (string issue in SubscriptionIssues.OrderBy(x => x)) Console.WriteLine("原配置核对：" + issue);
            Console.WriteLine("PASS: 组合方案加载、订阅、保存与导航 {0} 项断言", _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            foreach (var editor in editors) editor.Dispose();
            foreach (var window in FrmImageViewer.FrmSingleImages.ToArray()) window.Dispose();
            FrmImageViewer.FrmSingleImages.Clear();
        }
    }
}
