using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>多运行界面持久化测试，不初始化方案单例或生产设备。</summary>
internal static class RuntimeInterfacePagesTests
{
    /// <summary>累计断言数量。</summary>
    private static int checks;

    /// <summary>通过正式程序集的配置模型验证集合与旧格式兼容。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            var property = typeof(SolConfig).GetProperty("RuntimeInterfaces");
            Check(property != null, "方案必须支持多个运行界面，而不是覆盖唯一布局");
            var first = new RuntimeLayoutDocument { Name = "工位一" };
            var second = new RuntimeLayoutDocument { Name = "工位二" };
            string json = JsonConvert.SerializeObject(new
            {
                SolName = "example.Sol", RunInterval = 35, NodeCount = 17,
                RuntimeInterfaces = new[] { new { Id = "one", Layout = first }, new { Id = "two", Layout = second } }
            });
            var config = JsonConvert.DeserializeObject<SolConfig>(json);
            var pages = (IList)property.GetValue(config);
            Check(pages.Count == 2, "两个布局必须分别保留");
            var saved = JObject.Parse(JsonConvert.SerializeObject(config));
            Check(saved["RuntimeInterfaces"].Count() == 2, "两个布局随方案保存");
            Check(saved["RuntimeInterface"] == null, "新集合格式不能额外保存重复的单布局");
            Check(config.RunInterval == 35 && config.NodeCount == 17 && config.SolName == "example.Sol", "其它方案字段保持原值");

            var legacy = JsonConvert.DeserializeObject<SolConfig>(JsonConvert.SerializeObject(new { RuntimeInterface = first }));
            var migrated = (IList)property.GetValue(legacy);
            Check(migrated != null && migrated.Count == 1, "原单界面自动迁移为集合中的一项");
            Check(JObject.Parse(JsonConvert.SerializeObject(legacy))["RuntimeInterfaces"][0]["Layout"]["Name"].Value<string>() == "工位一", "迁移保留原布局名称");
            var none = JsonConvert.DeserializeObject<SolConfig>("{}");
            Check(property.GetValue(none) == null || ((IList)property.GetValue(none)).Count == 0, "没有界面的旧方案不自动生成页面");

            var duplicate = JObject.Parse(json);
            duplicate["RuntimeInterfaces"][1]["Id"] = "one";
            Reject(duplicate.ToString(), "重复页面编号不能进入方案");
            duplicate["RuntimeInterfaces"][1]["Id"] = "";
            Reject(duplicate.ToString(), "空页面编号不能进入方案");
            duplicate["RuntimeInterfaces"][1]["Id"] = "two";
            duplicate["RuntimeInterfaces"][1]["Layout"] = null;
            Reject(duplicate.ToString(), "缺失布局不能进入方案");
            TestPageEditor();
            TestSaveCallbackEdits();
            TestLegacyEditor();
            TestIndependentWindows();
            Console.WriteLine("PASS: " + checks + " multiple runtime interface assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>多页编辑器隔离输入与保存集合，页签切换不串数据、编号和撤销历史。</summary>
    private static void TestPageEditor()
    {
        var source = new[] { TestPage("first", "工位一", "甲"), TestPage("second", "工位二", "乙") };
        string original = JsonConvert.SerializeObject(source);
        IReadOnlyList<RuntimeInterfacePage> saved = null;
        IReadOnlyList<RuntimeInterfacePage> rejected = null;
        bool allowSave = false;
        using (var form = CreatePageEditor(source, pages =>
        {
            // 不替被测代码克隆：宿主收到的对象必须已经与编辑草稿隔离。
            if (allowSave) { saved = pages; return true; }
            rejected = pages;
            pages[0].Layout.Elements[0].Text = "回调修改提交副本";
            pages[0].Layout.Elements[0].RoiBinding.NodePath[0] = 999;
            return false;
        }))
        {
            var first = Field<RuntimeLayoutSession>(form, "session");
            Check(first.Document.Name == "工位一", "工厂从第一页建立活动工作副本");
            Check(!ReferenceEquals(first.Document, source[0].Layout), "多页工厂深复制输入布局");
            first.Edit(doc => doc.Elements[0].Text = "甲修改");
            SelectPage(form, "second");
            var second = Field<RuntimeLayoutSession>(form, "session");
            Check(second.Document.Name == "工位二" && second.Document.Elements[0].Text == "乙", "同组件编号的两页内容互不覆盖");
            Check(!second.CanUndo, "第二页不能撤销第一页编辑");
            second.Edit(doc => doc.Elements[0].Text = "乙修改");
            SelectPage(form, "first");
            first = Field<RuntimeLayoutSession>(form, "session");
            Check(first.Document.Elements[0].Text == "甲修改", "切回第一页保留其未保存内容");
            first.Undo();
            Check(first.Document.Elements[0].Text == "甲", "第一页撤销只恢复自己的组件");
            first.Redo();
            Check(!SaveEditor(form), "宿主拒绝保存时编辑器必须返回失败");
            Check(rejected != null && rejected.Count == 2, "保存提交整个集合而非仅当前页面");
            Check(first.Document.Elements[0].Text == "甲修改" && first.Document.Elements[0].RoiBinding.NodePath[0] == 11,
                "失败回调修改提交副本不能污染草稿或嵌套绑定");
            Check(Dirty(form) && Field<ToolStripButton>(form, "saveButton").Enabled, "保存失败保留未保存状态及重试入口");
            allowSave = true;
            Check(SaveEditor(form), "失败后可原样重试保存");
            Check(saved.Count == 2 && saved[0].Id == "first" && saved[1].Id == "second", "保存所有页面并保留顺序及稳定ID");
            Check(saved[0].Layout.Elements[0].Text == "甲修改" && saved[1].Layout.Elements[0].Text == "乙修改", "非当前页的未保存修改也必须提交");
            Check(!ReferenceEquals(saved[0].Layout, first.Document) && !ReferenceEquals(saved[0].Layout, rejected[0].Layout),
                "每次提交都是独立布局，不复用失败提交或活动草稿");
            Check(!Dirty(form), "成功保存清除整个集合的未保存状态");
            saved[0].Layout.Elements[0].Text = "宿主后续修改";
            saved[0].Layout.Elements[0].RoiBinding.NodePath[0] = 777;
            Check(first.Document.Elements[0].Text == "甲修改" && first.Document.Elements[0].RoiBinding.NodePath[0] == 11,
                "宿主后续修改已保存数据也不能影响编辑器");
            first.Edit(doc => doc.Name = "工位一改名");
            SelectPage(form, "second");
            Check(Dirty(form), "活动页干净时仍识别其它页的脏状态");
            Check(SaveEditor(form) && saved[0].Id == "first" && saved[0].Layout.Name == "工位一改名", "改名后从其它页保存仍保留稳定ID");
            Check(JsonConvert.SerializeObject(source) == original, "编辑及多次提交均不直接修改传入方案集合");
            Check(!Dirty(form), "从其它页保存完成后集合恢复干净状态");
            var imported = new RuntimeLayoutDocument { Name = "导入的模板" };
            imported.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, Text = "模板内容" });
            var current = Field<RuntimeLayoutSession>(form, "session");
            current.Replace(imported);
            Check(SaveEditor(form) && saved[1].Id == "second" && saved[1].Layout.Name == "导入的模板"
                && saved[0].Id == "first" && saved[0].Layout.Name == "工位一改名", "导入仅替换活动页布局，保持两页稳定ID");
            current.Undo();
            Check(current.Document.Elements[0].Text == "乙修改" && current.Document.Name == "工位二", "导入撤销恢复当前页且不串其它页内容");
            Field<RuntimeLayoutSession>(form, "session").Edit(doc => doc.Name = "未保存草稿");
            // 直接释放模拟宿主放弃草稿，避免触发需要人工选择的对话框。
        }
        Check(JsonConvert.SerializeObject(source) == original, "放弃多页草稿不污染传入方案");
    }

    /// <summary>保存回调的消息重入可以切页和编辑，不能把未提交的新修改误记为已保存或直接关闭。</summary>
    private static void TestSaveCallbackEdits()
    {
        RuntimeInterfaceEditorForm editor = null;
        IReadOnlyList<RuntimeInterfacePage> submitted = null;
        bool editDuringSave = true;
        int calls = 0;
        using (editor = CreatePageEditor(new[] { TestPage("first", "工位一", "甲"), TestPage("second", "工位二", "乙") }, pages =>
        {
            calls++; submitted = pages;
            if (editDuringSave)
            {
                Field<RuntimeLayoutSession>(editor, "session").Edit(doc => doc.Elements[0].Text = "回调期间甲");
                SelectPage(editor, "second");
                Field<RuntimeLayoutSession>(editor, "session").Edit(doc => doc.Elements[0].Text = "回调期间乙");
            }
            return true;
        }))
        {
            Field<RuntimeLayoutSession>(editor, "session").Edit(doc => doc.Name = "工位一修改");
            Check(!(bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Yes), "保存回调期间产生新编辑时必须阻止关闭");
            Check(calls == 1 && Dirty(editor) && submitted[0].Layout.Elements[0].Text == "甲"
                && submitted[1].Layout.Elements[0].Text == "乙", "保存基线固定在提交前，回调重入不污染提交快照");
            Check(Field<RuntimeLayoutSession>(editor, "session").Document.Elements[0].Text == "回调期间乙", "保存回调切页后保留当前页的新内容");
            Check(!(bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Cancel) && calls == 1, "取消关闭不重复保存或丢弃新编辑");
            editDuringSave = false;
            Check((bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Yes) && calls == 2 && !Dirty(editor), "再次保存包含全部重入编辑后才允许关闭");
            Check(submitted[0].Layout.Elements[0].Text == "回调期间甲" && submitted[1].Layout.Elements[0].Text == "回调期间乙",
                "第二次提交同时保留活动和非活动页的回调编辑");
        }
    }

    /// <summary>原构造器仍使用单文档回调，不要求旧调用方迁移为页面集合。</summary>
    private static void TestLegacyEditor()
    {
        var source = TestPage("legacy", "旧单页", "原内容").Layout;
        RuntimeLayoutDocument saved = null;
        using (var form = new RuntimeInterfaceEditorForm(source, "旧方案", doc => { saved = doc; return true; }, null, null, null))
        {
            var session = Field<RuntimeLayoutSession>(form, "session");
            session.Edit(doc => doc.Elements[0].Text = "单页修改");
            Check(SaveEditor(form) && saved.Name == "旧单页" && saved.Elements[0].Text == "单页修改", "旧构造器按原单布局契约提交");
            Check(source.Elements[0].Text == "原内容" && !ReferenceEquals(saved, session.Document), "旧单页输入与提交仍保持隔离");
            saved.Elements[0].RoiBinding.NodePath[0] = 900;
            Check(session.Document.Elements[0].RoiBinding.NodePath[0] == 11, "旧单页保存副本中的嵌套绑定也独立");
            Check(!Dirty(form), "旧单页保存成功后清除脏状态");
        }
    }

    /// <summary>两页故意使用同一个组件ID，验证页面作用域而非全局编号。</summary>
    private static RuntimeInterfacePage TestPage(string id, string name, string text)
    {
        var document = new RuntimeLayoutDocument { Name = name };
        document.Elements.Add(new RuntimeLayoutElement { Id = "shared-widget", Kind = RuntimeWidgetKind.Image, Text = text,
            RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 1, NodePath = new[] { 11 }, PropertyPath = "Result" } });
        return new RuntimeInterfacePage { Id = id, Layout = document };
    }

    /// <summary>验证公开工厂契约；旧程序集应明确失败，而非回退到已弃用管理器。</summary>
    private static RuntimeInterfaceEditorForm CreatePageEditor(IEnumerable<RuntimeInterfacePage> pages,
        Func<IReadOnlyList<RuntimeInterfacePage>, bool> save)
    {
        var factory = typeof(RuntimeInterfaceEditorForm).GetMethod("CreateForPages", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(IEnumerable<RuntimeInterfacePage>), typeof(string), typeof(Func<IReadOnlyList<RuntimeInterfacePage>, bool>),
                typeof(IRuntimeBindingCatalog), typeof(Func<RuntimeLayoutDocument, IRuntimeBindingDataSource>), typeof(IRuntimeRunController) }, null);
        Check(factory != null, "必须提供约定的CreateForPages公开多页工厂");
        return (RuntimeInterfaceEditorForm)factory.Invoke(null, new object[] { pages, "测试方案", save, null, null, null });
    }

    /// <summary>切换编辑页，不显示窗体或触发生产入口。</summary>
    private static void SelectPage(RuntimeInterfaceEditorForm form, string id) => Invoke(form, "SelectPage", id);

    /// <summary>走保存按钮共用逻辑，并检查其返回的宿主提交状态。</summary>
    private static bool SaveEditor(RuntimeInterfaceEditorForm form) => (bool)Invoke(form, "SaveDocument");

    /// <summary>直接验证关闭和状态栏共用的脏状态，不要求保存按钮必须禁用。</summary>
    private static bool Dirty(RuntimeInterfaceEditorForm form) => (bool)Invoke(form, "HasUnsavedChanges");

    /// <summary>调用确定的私有模型入口，缺失时输出清晰契约错误。</summary>
    private static object Invoke(object instance, string name, params object[] args)
    {
        var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new Exception("缺少编辑器测试入口：" + name);
        return method.Invoke(instance, args);
    }

    /// <summary>两个真实显示窗体分别持有订阅生命周期和窗口名称。</summary>
    private static void TestIndependentWindows()
    {
        var firstSource = new EmptySource();
        var secondSource = new EmptySource();
        using (var first = new RuntimeInterfacePreviewForm(new RuntimeLayoutDocument(), firstSource, () => true))
        using (var second = new RuntimeInterfacePreviewForm(new RuntimeLayoutDocument(), secondSource, () => true))
        {
            first.ConfigureRuntimeWindow("工位一", 0, 2);
            second.ConfigureRuntimeWindow("工位二", 1, 2);
            first.Show(); second.Show(); Application.DoEvents();
            Check(first.Handle != second.Handle && first.Visible && second.Visible, "两个布局创建两个独立原生窗口");
            Check(first.WindowState == FormWindowState.Maximized && second.WindowState == FormWindowState.Maximized, "所有运行窗口默认最大化");
            first.WindowState = second.WindowState = FormWindowState.Normal;
            Check(first.Location != second.Location, "两窗口保留不同还原位置");
            typeof(RuntimeInterfacePreviewForm).GetMethod("RefreshLive", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(second, new object[] { null, EventArgs.Empty });
            Check(second.Text.StartsWith("工位二"), "数据刷新不覆盖独立窗口名称");
            first.Close();
            Check(firstSource.Disposed && !secondSource.Disposed && second.Visible, "关闭只退订自身数据源");
            second.Close();
            Check(secondSource.Disposed, "第二个窗口单独释放数据源");
        }
    }

    /// <summary>读取设计器定义控件，不调用外部自动化或设备。</summary>
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

    /// <summary>记录显示订阅释放，不创建任何生产结果。</summary>
    private sealed class EmptySource : IRuntimeBindingDataSource
    {
        /// <summary>独立订阅是否释放。</summary>
        public bool Disposed;
        /// <summary>没有模拟帧可提取。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() => new RuntimeValueUpdate[0];
        /// <summary>仅标记本订阅释放。</summary>
        public void Dispose() { Disposed = true; }
    }

    /// <summary>确认非法配置在反序列化阶段被拒绝。</summary>
    private static void Reject(string json, string reason)
    {
        try { JsonConvert.DeserializeObject<SolConfig>(json); }
        catch (Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
                if (current is InvalidDataException) { Check(true, reason); return; }
            throw;
        }
        throw new Exception(reason);
    }

    /// <summary>失败时终止隔离测试。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
}
