using System;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>调参按钮的布局、流程绑定与隔离交互测试。</summary>
internal static class RuntimeTuningWidgetsTests
{
    /// <summary>已执行断言数量。</summary>
    private static int checks;
    /// <summary>不连接生产程序的测试入口。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Check(Enum.IsDefined(typeof(RuntimeWidgetKind), "ManualParameters"), "缺少手动参数控件");
            Check(Enum.IsDefined(typeof(RuntimeWidgetKind), "OneClickLearning"), "缺少一键学习控件");
            var property = typeof(RuntimeLayoutElement).GetProperty("ActionProcessId");
            Check(property != null, "缺少独立的调参流程绑定");
            foreach (string name in new[] { "ManualParameters", "OneClickLearning" })
            {
                var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
                var element = session.Add((RuntimeWidgetKind)Enum.Parse(typeof(RuntimeWidgetKind), name), Point.Empty);
                Check(property.GetValue(element) == null, "新按钮不应默认绑定任意流程");
                session.Edit(document => property.SetValue(session.Find(element.Id), (int?)7));
                Check((int?)property.GetValue(RuntimeLayoutSerializer.Clone(session.Document).Elements[0]) == 7, "模板保留指定流程");
                Check((int?)property.GetValue(RuntimeWidgetClipboardCodec.Decode(RuntimeWidgetClipboardCodec.Encode(session.Document, session.Find(element.Id)))) == 7, "复制保留指定流程");
                session.Undo(); Check(property.GetValue(session.Find(element.Id)) == null, "撤销流程绑定");
                session.Redo(); Check((int?)property.GetValue(session.Find(element.Id)) == 7, "重做流程绑定");
            }
            VerifyEditor();
            VerifyController();
            VerifyWidgets();
            VerifyPreview();
            VerifyValidation();
            Console.WriteLine("通过：调参按钮，" + checks + " 项断言。"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>设计器显示两类工具与指定流程，刷新不能清掉失效绑定。</summary>
    private static void VerifyEditor()
    {
        var catalog = new Catalog();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "测试", null, catalog))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var combo = Field<ComboBox>(editor, "runProcessCombo");
            foreach (var kind in new[] { RuntimeWidgetKind.ManualParameters, RuntimeWidgetKind.OneClickLearning })
            {
                Check(Field<ListView>(editor, "toolbox").Items.Cast<ListViewItem>().Any(item => (RuntimeWidgetKind)item.Tag == kind), "工具箱包含调参控件");
                var element = session.Add(kind, Point.Empty);
                canvas.SelectElement(element.Id);
                Check(combo.Text == "未绑定" && combo.Items.Count == 3, "默认未绑定且列出完整流程目录");
                Check(!combo.Items.Cast<object>().Any(item => (string)item.GetType().GetProperty("Name").GetValue(item) == "全流程"), "调参不提供全流程选项");
                combo.SelectedIndex = 2;
                Check(session.Find(element.Id).ActionProcessId == 7, "显式选择流程编号");
                session.Undo(); Check(combo.Text == "未绑定", "选择可撤销");
                session.Redo(); Check(session.Find(element.Id).ActionProcessId == 7, "重做恢复绑定");
                catalog.Processes[1].Name = "新名称";
                Invoke(editor, "RefreshProperties"); Check(combo.Text == "新名称", "流程改名只影响显示");
                catalog.Processes.RemoveAt(1);
                Invoke(editor, "RefreshProperties"); Check(combo.Text.Contains("已失效") && session.Find(element.Id).ActionProcessId == 7, "失效目标保留编号不能回退");
                catalog.Processes.Add(new RuntimeProcessChoice { Id = 7, Name = "流程7" });
            }
        }
    }
    /// <summary>适配器拒绝未绑定、无权限、错误动作，执行时重新读取权限。</summary>
    private static void VerifyController()
    {
        bool allowed = true; int calls = 0; int? received = null; RuntimeWidgetKind action = RuntimeWidgetKind.Text;
        var controller = new RuntimeTuningController(() => allowed, (owner, kind, id) => { calls++; received = id; action = kind; });
        Reject(() => controller.Execute(null, RuntimeWidgetKind.OneClickLearning, null), "未绑定不能执行");
        Reject(() => controller.Execute(null, RuntimeWidgetKind.Text, 7), "错误动作不能执行");
        Reject(() => controller.Execute(null, RuntimeWidgetKind.ManualParameters, -1), "非法流程不能执行");
        allowed = false;
        Reject(() => controller.Execute(null, RuntimeWidgetKind.ManualParameters, 7), "注销后不能执行");
        Check(calls == 0, "无效操作未转发");
        allowed = true;
        controller.Execute(null, RuntimeWidgetKind.OneClickLearning, 7);
        Check(calls == 1 && received == 7 && action == RuntimeWidgetKind.OneClickLearning, "学习动作只转发指定流程");
    }
    /// <summary>按钮无预览副作用、重复点击防护、错误恢复与权限刷新不额外重绘。</summary>
    private static void VerifyWidgets()
    {
        bool allowed = true; int calls = 0; int errors = 0; int enabledChanges = 0;
        using (var widget = new RuntimeTuningWidget())
        {
            var element = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.ManualParameters);
            var button = Field<Button>(widget, "actionButton");
            var controller = new RuntimeTuningController(() => allowed, (owner, kind, id) =>
            {
                calls++;
                Check(kind == RuntimeWidgetKind.ManualParameters && id == 7, "按钮转发自身动作和绑定");
                Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            });
            widget.CommandFailed += message => errors++;
            widget.Bind(null, element); Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            Check(!button.Enabled && calls == 0, "预览无入口且不执行");
            widget.Bind(controller, element); Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            Check(!button.Enabled && calls == 0, "未绑定按钮不可执行");
            element.ActionProcessId = 7; widget.Bind(controller, element);
            button.EnabledChanged += (sender, args) => enabledChanges++;
            for (int i = 0; i < 100; i++) widget.RefreshState();
            Check(enabledChanges == 0, "稳定刷新不反复改变控件状态");
            Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            Check(calls == 1 && errors == 0 && button.Enabled, "重复点击只执行一次且完成恢复");
            allowed = false; Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            Check(calls == 1 && errors == 1 && !button.Enabled, "权限变化即时拒绝且报告错误");
            allowed = true;
            widget.Bind(new RuntimeTuningController(() => true, (owner, kind, id) => { throw new InvalidOperationException("测试异常"); }), element);
            Invoke(widget, "ExecuteClicked", button, EventArgs.Empty);
            Check(errors == 2 && button.Enabled, "失败报告后解除忙碌状态");
            widget.Size = new Size(48, 32);
            Check(button.Font.Size <= 9, "小尺寸按钮缩小字体避免中文越界");
            bool stateFailure = true;
            widget.Bind(new RuntimeTuningController(() => stateFailure ? throw new InvalidOperationException("状态查询失败") : true,
                (owner, kind, id) => { calls++; }), element);
            for (int i = 0; i < 10; i++) widget.RefreshState();
            Check(!button.Enabled && errors == 3, "状态查询异常只提示一次并禁用按钮，不逃出刷新");
            stateFailure = false; widget.RefreshState();
            Check(button.Enabled, "状态查询恢复后重新启用");
        }
    }
    /// <summary>预览只创建禁用按钮，正式注入后各自绑定，填充布局与遮挡保持一致。</summary>
    private static void VerifyPreview()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument { Width = 800, Height = 450 });
        var manual = session.Add(RuntimeWidgetKind.ManualParameters, new Point(32, 32));
        var learning = session.Add(RuntimeWidgetKind.OneClickLearning, new Point(224, 32));
        session.Edit(document => { session.Find(manual.Id).ActionProcessId = 1; session.Find(learning.Id).ActionProcessId = 7; });
        int calls = 0;
        using (var preview = new RuntimeInterfacePreviewForm(session.Document))
        {
            preview.WindowState = FormWindowState.Normal;
            preview.StartPosition = FormStartPosition.Manual;
            preview.Location = new Point(-20000, -20000);
            preview.ShowInTaskbar = false;
            preview.Size = new Size(1000, 650);
            preview.Show();
            Application.DoEvents();
            preview.PerformLayout();
            var widgets = Field<Dictionary<string, RuntimeTuningWidget>>(preview, "tuningWidgets");
            Check(widgets.Count == 2 && widgets.Values.All(widget => !Field<Button>(widget, "actionButton").Enabled), "编辑预览两按钮均不能执行学习");
            preview.AttachTuningController(new RuntimeTuningController(() => true, (owner, kind, id) => { calls++; }));
            Check(widgets.Values.All(widget => Field<Button>(widget, "actionButton").Enabled), "正式注入后启用已绑定按钮");
            var canvas = Field<RuntimeDesignCanvas>(preview, "canvas");
            Check(widgets[manual.Id].Bounds == canvas.GetPreviewBounds(session.Find(manual.Id)), "原生按钮随填充画布布局");
            Check(calls == 0, "打开关闭窗口本身不执行学习");
            using (var bitmap = new Bitmap(preview.Width, preview.Height))
            {
                preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "调参按钮预览.png"));
            }
            var layout = Field<RuntimeLayoutDocument>(preview, "layout");
            var cover = RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.Image);
            cover.X = manual.X; cover.Y = manual.Y; cover.Width = manual.Width; cover.Height = manual.Height;
            layout.Elements.Add(cover);
            Invoke(preview, "ArrangeRunControls");
            Check(!widgets[manual.Id].Visible && !Field<Button>(widgets[manual.Id], "actionButton").CanSelect, "完全遮挡的调参按钮不能通过键盘选择执行");
            layout.Elements.Remove(cover);
            Invoke(preview, "ArrangeRunControls");
            Check(widgets[manual.Id].Visible, "解除遮挡后恢复按钮");
        }
    }
    /// <summary>拒绝不合法绑定及在其他组件上错误持久化动作流程。</summary>
    private static void VerifyValidation()
    {
        var document = new RuntimeLayoutDocument();
        document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, ActionProcessId = 7 });
        Reject(() => RuntimeLayoutSerializer.Serialize(document), "普通文本不能保存调参绑定");
        document.Elements[0].Kind = RuntimeWidgetKind.OneClickLearning;
        document.Elements[0].ActionProcessId = -1;
        Reject(() => RuntimeLayoutSerializer.Serialize(document), "负流程编号不能保存");
    }
    /// <summary>所有根流程的测试目录，不含真实节点。</summary>
    private sealed class Catalog : IRuntimeBindingCatalog, IRuntimeLogProcessCatalog
    {
        /// <summary>可模拟重命名和删除的流程表。</summary>
        public List<RuntimeProcessChoice> Processes = new List<RuntimeProcessChoice>
        { new RuntimeProcessChoice { Id = 1, Name = "流程1" }, new RuntimeProcessChoice { Id = 7, Name = "流程7" } };
        /// <summary>调参目录包含被动及禁用根流程，与运行按钮目录分离。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetLogProcesses() => Processes;
        /// <summary>此测试不提供数据绑定。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind) => new RuntimeBindingChoice[0];
        /// <summary>返回绑定的既有描述。</summary>
        public string Describe(RuntimeDataBinding binding) => binding.DisplayPath;
    }
    /// <summary>取得真实控件字段，不模拟界面实现。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    /// <summary>直接触发事件入口，避免操作用户桌面或鼠标。</summary>
    private static void Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);
    /// <summary>应拒绝的操作必须抛出明确异常。</summary>
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; } catch (InvalidDataException) { rejected = true; }
        Check(rejected, message);
    }
    /// <summary>检查测试预期。</summary>
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
}
