using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>编辑器内联页签专项测试，隐藏窗口运行且不访问设备。</summary>
internal static class RuntimeInlinePagesTests
{
    /// <summary>已执行的行为断言数量。</summary>
    private static int checks;
    /// <summary>先检查工厂契约，旧程序集必须因缺少工厂而失败。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Console.WriteLine("被测程序集：" + typeof(RuntimeInterfaceEditorForm).Assembly.Location);
            Check(typeof(RuntimeInterfaceEditorForm).GetMethod("CreateForPages", BindingFlags.Public | BindingFlags.Static) != null,
                "编辑器缺少公共多页工厂 CreateForPages。");
            Application.EnableVisualStyles();
            VerifyPages();
            VerifyEmptyAndSaving();
            VerifySaveReentrancy();
            VerifyOverflow();
            VerifyLegacy();
            Console.WriteLine("通过：内联页签专项测试，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("失败（已执行 " + checks + " 项断言）：" + ex); return 1; }
    }

    /// <summary>验证页签新增、选择隔离、撤销隔离、属性改名和输入深复制。</summary>
    private static void VerifyPages()
    {
        var source = new[] { Page("one", "图像"), Page("two", "图像1") };
        IReadOnlyList<RuntimeInterfacePage> saved = null;
        using (var editor = Create(source, pages => { saved = pages; return true; }))
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var first = Field<RuntimeLayoutSession>(editor, "session");
            Check(!Dirty(editor), "已有集合打开时不应变脏");
            var image = first.Add(RuntimeWidgetKind.Image, Point.Empty);
            canvas.SelectElement(image.Id);
            first.Edit(document => document.Elements[0].RoiBinding = new RuntimeDataBinding {
                Kind = RuntimeBindingKind.Roi, ProcessId = 1, NodePath = new[] { 3 }, PropertyPath = "OutputRegion" });
            var firstSnapshot = RuntimeLayoutSerializer.Serialize(first.Document);
            Invoke(editor, "SelectPage", "two");
            var second = Field<RuntimeLayoutSession>(editor, "session");
            Check(!ReferenceEquals(first, second) && !second.CanUndo && canvas.SelectedId == null, "不同页必须使用独立会话和撤销栈");
            var text = second.Add(RuntimeWidgetKind.Text, Point.Empty);
            canvas.SelectElement(text.Id);
            second.Undo();
            Check(second.Document.Elements.Count == 0 && RuntimeLayoutSerializer.Serialize(first.Document) == firstSnapshot, "撤销第二页不能修改第一页面及 ROI");
            second.Redo(); canvas.SelectElement(text.Id);
            Invoke(editor, "SelectPage", "one");
            Check(ReferenceEquals(Field<RuntimeLayoutSession>(editor, "session"), first) && canvas.SelectedId == image.Id, "切回页面保留原会话与组件选择");
            Invoke(editor, "SelectPage", "two");
            Check(canvas.SelectedId == text.Id, "第二页面选择状态也应独立恢复");
            canvas.SelectElement(null);
            var pageProperties = (RuntimePageProperties)Field<PropertyGrid>(editor, "propertyGrid").SelectedObject;
            pageProperties.Name = "检测图像";
            Invoke(editor, "PropertyChanged", null, null);
            Check(Tabs(editor).Any(tab => tab.Text == "检测图像" && tab.Checked), "页面属性改名应立即反映在选中页签");
            second.Undo();
            Check(Tabs(editor).Any(tab => tab.Text == "图像1" && tab.Checked), "撤销名称修改应刷新页签");
            Field<ToolStripButton>(editor, "addPageButton").PerformClick();
            var third = Field<RuntimeLayoutSession>(editor, "session");
            Check(third.Document.Name == "图像2" && third.Document.Elements.Count == 0 && !third.CanUndo, "加号直接新增唯一默认名称的空布局");
            Check(Tabs(editor).Count() == 3 && Tabs(editor).Count(tab => tab.Checked) == 1, "新增后只选中新页面");
            Check(source.All(page => page.Layout.Elements.Count == 0) && source[1].Layout.Name == "图像1", "全部未保存编辑不能污染输入方案");
            Check(Dirty(editor) && Field<ToolStripStatusLabel>(editor, "stateLabel").Text.Contains("未保存"), "非当前页修改也参与全页脏状态");
            Check((bool)Invoke(editor, "SaveDocument") && saved.Count == 3 && saved[0].Id == "one" && saved[1].Id == "two" &&
                saved[0].Layout.Elements[0].RoiBinding != null, "保存必须提交按原顺序排列的全部页面和非当前页 ROI");
            var firstTab = Tabs(editor).First();
            string cached = Field<string>(firstTab.Tag, "Snapshot");
            third.Edit(document => document.Name = "当前页更名");
            Check(ReferenceEquals(cached, Field<string>(firstTab.Tag, "Snapshot")), "当前页编辑不应重新序列化未改变页面");
            var menu = Field<ContextMenuStrip>(editor, "pageTabMenu");
            menu.Tag = "one";
            typeof(ToolStripDropDown).GetMethod("OnClosed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu,
                new object[] { new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.ItemClicked) });
            Check((string)menu.Tag == "one", "菜单因点击关闭时不能提前丢失待删除页面编号");
        }
    }

    /// <summary>保存回调中的消息循环若带来新编辑，提交前基准不能把新编辑标为已保存。</summary>
    private static void VerifySaveReentrancy()
    {
        RuntimeInterfaceEditorForm editor = null;
        IReadOnlyList<RuntimeInterfacePage> saved = null;
        int calls = 0;
        editor = Create(new[] { Page("page", "原始页面") }, pages =>
        {
            saved = pages;
            calls++;
            Field<RuntimeLayoutSession>(editor, "session").Edit(document => document.Name = "回调期间编辑" + calls);
            return true;
        });
        using (editor)
        {
            Field<RuntimeLayoutSession>(editor, "session").Edit(document => document.Name = "提交页面");
            Check((bool)Invoke(editor, "SaveDocument") && saved[0].Layout.Name == "提交页面" && Dirty(editor), "提交成功仅记录提交快照，回调期间的新编辑仍未保存");
            Check(!(bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Yes) && calls == 2 && Dirty(editor), "关闭保存期间再次编辑必须继续保护草稿");
        }
    }

    /// <summary>验证全页保存深复制、回调失败及取消、最后一页删除和空集合保存。</summary>
    private static void VerifyEmptyAndSaving()
    {
        bool allow = false;
        int calls = 0;
        IReadOnlyList<RuntimeInterfacePage> saved = null;
        using (var editor = Create(new RuntimeInterfacePage[0], pages =>
        {
            calls++; saved = pages;
            if (pages.Count > 0) pages[0].Layout.Name = "回调隔离副本";
            return allow;
        }))
        {
            Check(Tabs(editor).Count() == 1 && Dirty(editor), "空方案默认生成一页并标记未保存");
            Check(!(bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Cancel) && calls == 0, "取消关闭不得调用保存或丢失草稿");
            Check(!(bool)Invoke(editor, "CanCloseWithChoice", DialogResult.Yes) && calls == 1 && Dirty(editor), "保存返回失败必须阻止关闭并保留未保存状态");
            Check(Field<RuntimeLayoutSession>(editor, "session").Document.Name == "图像", "失败保存回调修改副本不得污染草稿");
            allow = true;
            Check((bool)Invoke(editor, "SaveDocument") && !Dirty(editor), "成功全页保存后才清除脏状态");
            Field<RuntimeLayoutSession>(editor, "session").Edit(document => document.Name = "后续编辑");
            Check(saved[0].Layout.Name == "回调隔离副本" && Dirty(editor), "后续编辑不得改变交给宿主的保存副本");
            Check((bool)Invoke(editor, "CanCloseWithChoice", DialogResult.No) && Dirty(editor), "放弃关闭不应伪装成已保存");
            string id = saved[0].Id;
            Invoke(editor, "RemovePage", id);
            Check(Tabs(editor).Count() == 0 && Field<RuntimeLayoutSession>(editor, "session") == null, "删除最后一页必须进入真正空集合");
            Check(Field<PropertyGrid>(editor, "propertyGrid").SelectedObject == null &&
                !Field<ToolStripButton>(editor, "previewButton").Enabled && !Field<ListView>(editor, "toolbox").Enabled, "空态不能编辑或预览隐含页面");
            Check((bool)Invoke(editor, "SaveDocument") && saved.Count == 0 && !Dirty(editor), "允许保存删除全部页面后的空集合");
            Field<ToolStripButton>(editor, "addPageButton").PerformClick();
            Check(Tabs(editor).Count() == 1 && Field<RuntimeLayoutSession>(editor, "session") != null && Dirty(editor), "空态加号可以重新创建页面");
        }
        using (var editor = Create(new[] { Page("first", "图像"), Page("second", "图像1") }, pages => true))
        {
            var first = Field<RuntimeLayoutSession>(editor, "session");
            first.Add(RuntimeWidgetKind.Text, Point.Empty);
            Invoke(editor, "SelectPage", "second");
            Invoke(editor, "RemovePage", "first");
            Check(Tabs(editor).Count() == 1 && Field<RuntimeLayoutSession>(editor, "session").Document.Name == "图像1", "删除非当前页不能替换当前会话");
        }
    }

    /// <summary>固定三十二像素页签条与加号可用，长名称通过溢出菜单访问而不重叠。</summary>
    private static void VerifyOverflow()
    {
        using (var editor = Create(Enumerable.Range(0, 18).Select(index => Page("page" + index, "图像页面长名称" + index)), pages => true))
        {
            IntPtr handle = editor.Handle;
            editor.Size = editor.MinimumSize;
            editor.PerformLayout();
            var strip = Field<ToolStrip>(editor, "pageTabs");
            strip.PerformLayout();
            var add = Field<ToolStripButton>(editor, "addPageButton");
            Check(strip.Height * 96f / editor.DeviceDpi <= 32.5f, "页签条逻辑高度不得超过三十二像素");
            Check(add.Placement == ToolStripItemPlacement.Main && add.Bounds.Right <= strip.ClientSize.Width, "窄窗口加号始终可见且不越界");
            Check(Tabs(editor).Any(tab => tab.Placement == ToolStripItemPlacement.Overflow), "长页签必须进入溢出下拉");
            var visible = strip.Items.Cast<ToolStripItem>().Where(item => item.Placement == ToolStripItemPlacement.Main).ToArray();
            Check(!visible.Any(left => visible.Any(right => !ReferenceEquals(left, right) && left.Bounds.IntersectsWith(right.Bounds))), "可见标签与加号不能重叠");
            Tabs(editor).Last().PerformClick();
            Check(Field<RuntimeLayoutSession>(editor, "session").Document.Name.EndsWith("17"), "溢出页签可正常切换草稿");
        }
    }

    /// <summary>旧六参数构造仍为单文档模式，保存行为保持兼容。</summary>
    private static void VerifyLegacy()
    {
        RuntimeLayoutDocument saved = null;
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "旧入口", document => { saved = document; return true; }))
        {
            Field<RuntimeLayoutSession>(editor, "session").Add(RuntimeWidgetKind.Text, Point.Empty);
            Check((bool)Invoke(editor, "SaveDocument") && saved.Elements.Count == 1, "旧构造保持单文档保存契约");
            Check(Field<TableLayoutPanel>(editor, "centerLayout").RowStyles[0].Height == 0, "旧单文档入口不占用多页栏空间");
        }
    }

    /// <summary>通过反射调用新增工厂，让同一测试可在旧程序集上观察缺契约失败。</summary>
    private static RuntimeInterfaceEditorForm Create(IEnumerable<RuntimeInterfacePage> pages, Func<IReadOnlyList<RuntimeInterfacePage>, bool> save) =>
        (RuntimeInterfaceEditorForm)typeof(RuntimeInterfaceEditorForm).GetMethod("CreateForPages").Invoke(null, new object[] { pages, "页签测试", save, null, null, null });
    /// <summary>构造只含内存布局的来源页面。</summary>
    private static RuntimeInterfacePage Page(string id, string name) => new RuntimeInterfacePage { Id = id, Layout = new RuntimeLayoutDocument { Name = name } };
    /// <summary>枚举动态页面按钮，不包含固定加号。</summary>
    private static IEnumerable<ToolStripButton> Tabs(RuntimeInterfaceEditorForm editor) => Field<ToolStrip>(editor, "pageTabs").Items.OfType<ToolStripButton>().Where(button => button != Field<ToolStripButton>(editor, "addPageButton"));
    /// <summary>读取全页脏状态。</summary>
    private static bool Dirty(RuntimeInterfaceEditorForm editor) => (bool)Invoke(editor, "HasUnsavedChanges");
    /// <summary>读取设计器控件或独立工作会话。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>调用实际页面操作，不弹出确认或操作桌面。</summary>
    private static object Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    /// <summary>记录断言并在行为不符时中止测试。</summary>
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
}
