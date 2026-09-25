using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using TDJS_Vision;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

/// <summary>验证组合导航保持真实流程身份，不污染顶层流程列表。</summary>
internal static class CompositeNavigationTests
{
    /// <summary>已完成断言数。</summary>
    private static int _checks;
    /// <summary>可选人工检查模式，保留真实测试窗体供屏幕检查。</summary>
    private static bool _preview;
    /// <summary>检查对用户可见的状态。</summary>
    private static void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    /// <summary>兼容旧程序集运行，以行为入口缺失作为红灯。</summary>
    private static object Call(object value, string method, params object[] args)
    {
        var member = value.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        Check(member != null, "缺少组合导航入口：" + method);
        return member.Invoke(value, args);
    }
    /// <summary>读取真实窗体控件用于交互断言。</summary>
    private static T Field<T>(object value, string name) where T : class
    { return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }

    /// <summary>验证大量流程、折叠记忆以及节点名称点击跳转。</summary>
    private static void CheckNavigationPopup()
    {
        using (var form = new FormNewProcessWizard())
        {
            var tabs = Field<TabControl>(form, "tabControl1");
            var rootEditor = new ProcessEditPanel("工位1.同心度");
            var root = (TDJS_Vision.Process)rootEditor.GetType().GetProperty("RootProcess").GetValue(rootEditor);
            root.ID = 901;
            var module = new NodeCompositeModule(910, "同心度检测", root, NodeType.CompositeModule);
            module.ParamForm.Params = new NodeParamCompositeModule();
            root.AddNode(module);
            var page = new TabPage("901.工位1.同心度"); page.Controls.Add(rootEditor); rootEditor.Dock = DockStyle.Fill;
            tabs.TabPages.Add(page);
            for (int index = 0; index < 18; index++)
            {
                var editor = new ProcessEditPanel("工位" + (index + 2) + "检测与存图流程");
                var proc = Solution.Instance.AllProcesses.Last(); proc.ID = 902 + index;
                var extra = new TabPage(proc.ID + "." + proc.ProcessName); extra.Controls.Add(editor); editor.Dock = DockStyle.Fill;
                tabs.TabPages.Add(extra);
            }
            form.Size = new Size(1100, 720); form.Show(); Application.DoEvents();
            var button = Field<Button>(form, "buttonProcessNavigation");
            var popup = Field<Panel>(form, "panelProcessNavigation");
            var tree = Field<TreeView>(form, "treeProcessNavigation");
            button.PerformClick(); Application.DoEvents();
            Check(popup.Visible && tree.Nodes.Count == 19, "大量流程时导航没有完整显示");
            Check(button.Right <= button.Parent.ClientSize.Width, "导航按钮超出可见区域");
            Check(button.Bottom >= tabs.GetTabRect(0).Bottom, "顶部导航高度没有覆盖选项卡标题，露出原生滚动箭头");
            Check(button.Parent.GetChildAtPoint(new Point(button.Left + button.Width / 2, button.Top + button.Height / 2)) == button, "导航入口被选项卡遮挡");
            Check(popup.Parent.GetChildAtPoint(new Point(popup.Left + 10, popup.Top + 10)) == popup, "导航弹层被画布遮挡");
            Check(tree.Nodes[0].Nodes.Count == 1, "组合模块没有归属到父流程下");
            tree.Nodes[0].Collapse(); Application.DoEvents();
            Check(popup.Visible && !tree.Nodes[0].IsExpanded, "折叠不应关闭列表或切换画布");
            button.PerformClick(); button.PerformClick(); Application.DoEvents();
            Check(!tree.Nodes[0].IsExpanded, "重新打开丢失折叠状态");
            tree.Nodes[0].Expand(); Application.DoEvents();
            if (_preview) { Application.Run(form); return; }
            Directory.CreateDirectory("Tests/Results/CompositeNavigation");
            using (var bitmap = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("Tests/Results/CompositeNavigation/顶部折叠导航.png"); }
            var child = tree.Nodes[0].Nodes[0];
            var click = new TreeNodeMouseClickEventArgs(child, MouseButtons.Left, 1, child.Bounds.Left + 8, child.Bounds.Top + 8);
            form.GetType().GetMethod("treeProcessNavigation_NodeMouseClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { tree, click });
            Check(!popup.Visible && tabs.SelectedTab == page, "点击名称没有收起导航并选中流程");
            Check(rootEditor.GetType().GetProperty("CurrentProcess").GetValue(rootEditor) != root, "点击组合名称没有进入内部");
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("Tests/Results/CompositeNavigation/组合内部编辑.png"); }
            form.Close();
        }
    }
    /// <summary>运行真实编辑器测试，不连接现场设备。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("TDJS_VISION_PERFORMANCE_ACCEPTANCE", "1");
        Environment.SetEnvironmentVariable("TDJS_VISION_BACKGROUND_ACCEPTANCE", "1");
        LanguageManager.Initialize();
        _preview = args.Contains("--preview");
        try
        {
            using (var editor = new ProcessEditPanel("导航验证"))
            {
                var root = Solution.Instance.AllProcesses.Last();
                var module = new NodeCompositeModule(801, "检测.组合", root, NodeType.CompositeModule);
                root.AddNode(module);
                int count = Solution.Instance.AllProcesses.Count;
                Check((bool)Call(editor, "NavigateToComposite", new int[] { 801 }), "不能进入组合模块");
                Check(Solution.Instance.AllProcesses.Count == count, "内部编辑意外增加顶层流程");
                var current = (TDJS_Vision.Process)editor.GetType().GetProperty("CurrentProcess").GetValue(editor);
                Check(!ReferenceEquals(current, root), "画布没有切换到内部");
                var nested = new NodeCompositeModule(802, "检测.组合", current, NodeType.CompositeModule);
                current.AddNode(nested);
                Solution.Instance.NodeCount = 801;
                var added = (NodeBase)typeof(ProcessEditPanel).GetMethod("CreateNode", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(editor, new object[] { NodeType.CompositeModule, "新增组合", -1, null, true, true });
                Check(added != null && added.ID > 802, "内部新增节点与导入节点编号冲突");
                var snapshotType = typeof(ProcessEditPanel).GetNestedType("NodeCopySnapshot", BindingFlags.NonPublic);
                var snapshot = Activator.CreateInstance(snapshotType, true);
                snapshotType.GetField("SourceProcess").SetValue(snapshot, current);
                snapshotType.GetField("SourceProcessId").SetValue(snapshot, current.ID);
                Check((bool)snapshotType.GetMethod("IsSourceProcess").Invoke(snapshot, new object[] { current }), "同一内部流程被误判为跨流程");
                Check(!(bool)snapshotType.GetMethod("IsSourceProcess").Invoke(snapshot, new object[] { root }), "不同内部流程仅凭编号被误判为同一来源");
                Check((bool)Call(editor, "NavigateToComposite", new int[] { 801, 802 }), "重名嵌套组合不能按编号进入");
                Check((bool)Call(editor, "NavigateToParent"), "不能返回上一层");
                Check(ReferenceEquals(current, editor.GetType().GetProperty("CurrentProcess").GetValue(editor)), "返回丢失原内部对象");
                Call(editor, "FlushCompositeEdits");
                Check(((NodeParamCompositeModule)module.ParamForm.Params).NodeInfos.Any(n => n.ID == 802), "内部编辑没有保存到快照");
                Check((bool)Call(editor, "NavigateToComposite", new int[0]), "不能返回主流程");
                Check(ReferenceEquals(root, editor.GetType().GetProperty("CurrentProcess").GetValue(editor)), "返回错误主流程");
                Check(!(bool)Call(editor, "NavigateToComposite", new int[] { 999 }), "失效路径不应成功跳转");
                Check(Solution.Instance.AllProcesses.Count == count, "多次导航污染顶层流程");
                module.Dispose();
                Solution.Instance.AllProcesses.Remove(root);
            }
            CheckNavigationPopup();
            Console.WriteLine("PASS: 组合导航 {0} 项断言", _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
