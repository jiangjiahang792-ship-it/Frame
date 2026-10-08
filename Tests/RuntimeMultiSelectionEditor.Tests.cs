using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>只验证编辑器多选接入，使用屏幕外测试窗口，不启动生产程序。</summary>
internal static class RuntimeMultiSelectionEditorTests
{
    /// <summary>已通过的行为断言数。</summary>
    private static int assertions;

    /// <summary>在单线程单元测试入口检查真实工具栏和页面会话。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            VerifySelectionProperties();
            VerifyArrange(true);
            VerifyArrange(false);
            VerifyBatchCommands();
            VerifyPageSelection();
            Console.WriteLine("PASS RuntimeMultiSelectionEditor: " + assertions);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    /// <summary>生成不同位置的图像、耗时、运行控件，避免排列成为无操作。</summary>
    private static RuntimeLayoutDocument CreateDocument()
    {
        var document = new RuntimeLayoutDocument();
        document.Elements.Add(new RuntimeLayoutElement { Id = "image", Kind = RuntimeWidgetKind.Image, X = 30, Y = 40, Width = 100, Height = 60,
            Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 1, NodePath = new[] { 1 }, PropertyPath = "Image" },
            RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 2, NodePath = new[] { 2 }, PropertyPath = "Roi" } });
        document.Elements.Add(new RuntimeLayoutElement { Id = "elapsed", Kind = RuntimeWidgetKind.Elapsed, X = 220, Y = 210, Width = 100, Height = 60 });
        document.Elements.Add(new RuntimeLayoutElement { Id = "run", Kind = RuntimeWidgetKind.RunControl, X = 520, Y = 480, Width = 100, Height = 60 });
        return document;
    }

    /// <summary>多选必须关闭单控件属性与绑定，重新单选后恢复原有行为。</summary>
    private static void VerifySelectionProperties()
    {
        using (var editor = new RuntimeInterfaceEditorForm(CreateDocument(), "多选测试", null))
        {
            var horizontal = Field<ToolStripButton>(editor, "arrangeHorizontalButton");
            var vertical = Field<ToolStripButton>(editor, "arrangeVerticalButton");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var grid = Field<PropertyGrid>(editor, "propertyGrid");
            var rows = Field<TableLayoutPanel>(editor, "rightLayout").RowStyles;
            Check(!horizontal.Enabled && !vertical.Enabled, "空选择禁用排列");
            Check(grid.Enabled && grid.SelectedObject is RuntimePageProperties, "空选择仍显示页面属性");
            Check(horizontal.Image != null && vertical.Image != null && horizontal.DisplayStyle == ToolStripItemDisplayStyle.Image,
                "两个排列命令使用图标");
            Check(horizontal.ToolTipText.Contains("横向") && vertical.ToolTipText.Contains("纵向"), "排列命令具有中文提示");
            canvas.SelectElement("image");
            Check(grid.SelectedObject is RuntimeElementProperties && grid.Enabled, "单选保留组件属性");
            Check(rows[1].Height > 0 && rows[3].Height > 0, "单选图像显示图像和ROI绑定");
            Check(!horizontal.Enabled && !vertical.Enabled, "单选禁用排列");
            canvas.SelectElements(new[] { "image", "elapsed" });
            Check(horizontal.Enabled && vertical.Enabled, "两项选择启用两个排列命令");
            Check(grid.SelectedObject == null && !grid.Enabled, "多选不暴露单控件或页面属性");
            Check(Field<string>(editor, "propertyElementId") == null, "多选清除单控件编辑目标");
            Check(Field<Label>(editor, "propertiesLabel").Text == "选中 2 个控件", "多选显示准确数量");
            Check(rows[1].Height == 0 && rows[2].Height == 0 && rows[3].Height == 0, "多选隐藏所有单控件绑定和流程行");
            Check(!Field<ToolStripButton>(editor, "forwardButton").Enabled && !Field<ToolStripButton>(editor, "backwardButton").Enabled,
                "多选禁用单控件图层命令");
            Check(Field<ToolStripButton>(editor, "duplicateButton").Enabled && Field<ToolStripButton>(editor, "deleteButton").Enabled,
                "多选允许批量复制删除");
            string before = RuntimeLayoutSerializer.Serialize(Field<RuntimeLayoutSession>(editor, "session").Document);
            Invoke(editor, "ClearBindingClicked", null, EventArgs.Empty);
            Invoke(editor, "ClearRoiBindingClicked", null, EventArgs.Empty);
            Invoke(editor, "ClearImage", null, EventArgs.Empty);
            Invoke(editor, "ChangeLayer", 1);
            Check(before == RuntimeLayoutSerializer.Serialize(Field<RuntimeLayoutSession>(editor, "session").Document), "多选不会误改主选项绑定或图层");
            canvas.SelectElement("run");
            Check(rows[2].Height > 0 && grid.Enabled && grid.SelectedObject is RuntimeElementProperties, "恢复单选运行控件流程编辑");
            canvas.SelectElements(new[] { "image", "run" });
            Check(rows[2].Height == 0 && grid.SelectedObject == null, "混合选择隐藏流程编辑");
            canvas.SelectElement("image");
            Check(rows[1].Height > 0 && rows[3].Height > 0 && grid.Enabled, "恢复单选图像绑定编辑");
            canvas.SelectElement(null);
            Check(grid.SelectedObject is RuntimePageProperties && !horizontal.Enabled, "取消选择恢复页面属性");
        }
    }

    /// <summary>实际点击两个工具按钮，验证方向正确、间距一致且整体一次撤销。</summary>
    private static void VerifyArrange(bool horizontal)
    {
        using (var editor = new RuntimeInterfaceEditorForm(CreateDocument(), "排列测试", null))
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            string before = RuntimeLayoutSerializer.Serialize(session.Document);
            canvas.SelectElements(new[] { "run", "image", "elapsed" });
            Field<ToolStripButton>(editor, horizontal ? "arrangeHorizontalButton" : "arrangeVerticalButton").PerformClick();
            var items = session.Document.Elements.OrderBy(item => horizontal ? item.X : item.Y).ToArray();
            Check(items.Select(item => horizontal ? item.Y : item.X).Distinct().Count() == 1, "排列方向正确且交叉轴对齐");
            int firstGap = horizontal ? items[1].X - items[0].X - items[0].Width : items[1].Y - items[0].Y - items[0].Height;
            int lastGap = horizontal ? items[2].X - items[1].X - items[1].Width : items[2].Y - items[1].Y - items[1].Height;
            Check(Math.Abs(firstGap - lastGap) <= 1 && firstGap >= 0, "排列等间距");
            Check(canvas.SelectedIds.Count == 3 && session.CanUndo, "排列保留选择并创建撤销");
            Field<ToolStripButton>(editor, "undoButton").PerformClick();
            Check(RuntimeLayoutSerializer.Serialize(session.Document) == before && !session.CanUndo, "一次撤销恢复全部组件与绑定");
            Field<ToolStripButton>(editor, "redoButton").PerformClick();
            Check(RuntimeLayoutSerializer.Serialize(session.Document) != before && canvas.SelectedIds.Count == 3, "重做保留多选");
            editor.ShowInTaskbar = false;
            editor.StartPosition = FormStartPosition.Manual;
            editor.Location = new Point(-20000, -20000);
            editor.Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(editor.Width, editor.Height))
            {
                editor.DrawToBitmap(bitmap, editor.ClientRectangle);
                bitmap.Save(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, horizontal ? "横向排列.png" : "纵向排列.png"));
            }
            editor.Hide();
        }
    }

    /// <summary>复制删除使用画布批量入口，不再仅处理主选项。</summary>
    private static void VerifyBatchCommands()
    {
        using (var editor = new RuntimeInterfaceEditorForm(CreateDocument(), "批量测试", null))
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            string before = RuntimeLayoutSerializer.Serialize(session.Document);
            canvas.SelectElements(new[] { "image", "elapsed" });
            Field<ToolStripButton>(editor, "duplicateButton").PerformClick();
            Check(session.Document.Elements.Count == 5, "工具栏一次复制两个组件");
            Check(canvas.SelectedIds.Count == 2 && !canvas.SelectedIds.Contains("image") && !canvas.SelectedIds.Contains("elapsed"), "批量复制选中新副本");
            session.Undo();
            Check(RuntimeLayoutSerializer.Serialize(session.Document) == before && !session.CanUndo, "批量复制一次撤销");
            canvas.SelectElements(new[] { "image", "elapsed" });
            Field<ToolStripButton>(editor, "deleteButton").PerformClick();
            Check(session.Document.Elements.Count == 1 && session.Document.Elements[0].Id == "run", "工具栏批量删除全部选择");
            Check(canvas.SelectedIds.Count == 0 && !Field<ToolStripButton>(editor, "arrangeHorizontalButton").Enabled, "批量删除清理选择与命令状态");
            session.Undo();
            Check(RuntimeLayoutSerializer.Serialize(session.Document) == before && !session.CanUndo, "批量删除一次撤销");
        }
    }

    /// <summary>每页保留独立多选和主选顺序，空页面不启用排列。</summary>
    private static void VerifyPageSelection()
    {
        var pages = new[] { new RuntimeInterfacePage { Id = "first", Layout = CreateDocument() },
            new RuntimeInterfacePage { Id = "second", Layout = CreateDocument() } };
        using (var editor = RuntimeInterfaceEditorForm.CreateForPages(pages, "多页测试", null))
        {
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            canvas.SelectElements(new[] { "elapsed", "image" });
            Invoke(editor, "SelectPage", "second");
            Check(canvas.SelectedIds.Count == 0, "新页面不继承上一页选择");
            canvas.SelectElement("run");
            Invoke(editor, "SelectPage", "first");
            Check(canvas.SelectedIds.SequenceEqual(new[] { "elapsed", "image" }) && canvas.SelectedId == "image", "切页恢复完整多选及主选顺序");
            Check(!Field<PropertyGrid>(editor, "propertyGrid").Enabled && Field<ToolStripButton>(editor, "arrangeHorizontalButton").Enabled, "切页恢复多选属性和排列状态");
            Field<ToolStripButton>(editor, "arrangeHorizontalButton").PerformClick();
            Invoke(editor, "SelectPage", "second");
            Check(canvas.SelectedId == "run" && canvas.SelectedIds.Count == 1 && !Field<RuntimeLayoutSession>(editor, "session").CanUndo, "另一页面单选和撤销独立");
            Invoke(editor, "SelectPage", "first");
            Check(Field<RuntimeLayoutSession>(editor, "session").CanUndo && canvas.SelectedIds.Count == 2, "原页面草稿和多选保留");
            Check(pages[0].Layout.Elements[0].Y == 40 && pages[0].Layout.Elements[1].Y == 210, "多页草稿不修改宿主原方案");
            Invoke(editor, "RemovePage", "first");
            Invoke(editor, "RemovePage", "second");
            Check(!Field<ToolStripButton>(editor, "arrangeHorizontalButton").Enabled && !Field<ToolStripButton>(editor, "arrangeVerticalButton").Enabled,
                "最后一页删除后禁用排列");
        }
    }

    /// <summary>读取真实设计器字段，缺失时给出明确失败原因。</summary>
    private static T Field<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) throw new InvalidOperationException("缺少编辑器字段：" + name);
        return (T)field.GetValue(target);
    }

    /// <summary>调用不弹出对话框的现有页面和编辑事件。</summary>
    private static void Invoke(object target, string name, params object[] arguments) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

    /// <summary>统计通过项，失败时输出具体行为。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
}
