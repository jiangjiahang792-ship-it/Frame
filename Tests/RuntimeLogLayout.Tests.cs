using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>日志布局契约回归，不加载现场方案或运行设备。</summary>
internal static class RuntimeLogLayoutTests
{
    /// <summary>断言计数。</summary>
    private static int checks;
    /// <summary>使用旧程序集也可编译，先报告缺失的组件行为。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Check(Enum.IsDefined(typeof(RuntimeWidgetKind), "Log"), "工具箱缺少日志框类型");
            var kind = (RuntimeWidgetKind)Enum.Parse(typeof(RuntimeWidgetKind), "Log");
            var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
            var item = session.Add(kind, new Point(20, 20));
            Check(RuntimeLayoutSession.GetName(kind) == "日志框", "组件中文名称");
            Check((int)Get(item, "LogCapacity") == 2000, "默认保留2000条");
            Check(Get(item, "LogProcessId") == null, "默认订阅全部流程");
            session.Edit(document => { Set(item, "LogProcessId", 2); Set(item, "LogCapacity", 500); Set(item, "LogAutoScroll", false); });
            var clone = RuntimeLayoutSerializer.Clone(session.Document).Elements[0];
            Check((int)Get(clone, "LogProcessId") == 2 && (int)Get(clone, "LogCapacity") == 500 && !(bool)Get(clone, "LogAutoScroll"), "模板往返保留订阅属性");
            var copy = session.Duplicate(item.Id);
            Check(copy.Id != item.Id && (int)Get(copy, "LogProcessId") == 2, "复制订阅且标识独立");
            session.Undo(); session.Undo();
            Check(Get(session.Find(item.Id), "LogProcessId") == null, "撤销订阅修改");
            session.Redo();
            Check((int)Get(session.Find(item.Id), "LogProcessId") == 2, "重做订阅修改");
            foreach (int invalid in new[] { 0, 99, 10001 })
            {
                var document = RuntimeLayoutSerializer.Clone(session.Document);
                Set(document.Elements[0], "LogCapacity", invalid);
                Reject(() => RuntimeLayoutSerializer.Validate(document), "拒绝非法容量 " + invalid);
            }
            var bad = RuntimeLayoutSerializer.Clone(session.Document);
            Set(bad.Elements[0], "LogProcessId", -1);
            Reject(() => RuntimeLayoutSerializer.Validate(bad), "拒绝负流程编号");
            bad = RuntimeLayoutSerializer.Clone(session.Document);
            bad.Elements[0].Kind = RuntimeWidgetKind.Text;
            Reject(() => RuntimeLayoutSerializer.Validate(bad), "拒绝给其它控件附加日志流程");
            var old = RuntimeLayoutSerializer.Deserialize("{\"Version\":1,\"Width\":1280,\"Height\":720,\"Elements\":[]}");
            Check(old.Elements.Count == 0, "旧模板无需日志配置");
            using (var canvas = new RuntimeDesignCanvas { Size = new Size(1280, 720) })
            using (var bitmap = new Bitmap(1280, 720))
            {
                canvas.Bind(session); canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Check(true, "日志框画布绘制已注册");
            }
            Check(TypeDescriptor.GetProperties(new RuntimeElementProperties(session.Find(item.Id)))["LogCapacity"] != null, "日志属性可编辑");
            Check(TypeDescriptor.GetProperties(new RuntimeElementProperties(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image }))["LogCapacity"] == null, "其它组件不显示日志属性");
            using (var controller = new RuntimePreviewRunController(new[] { new RuntimeProcessChoice { Id = 1, Name = "主动流程" } }))
            using (var editor = new RuntimeInterfaceEditorForm(session.Document, "测试方案", null, new LogCatalog(), null, controller))
            {
                var canvas = Read<RuntimeDesignCanvas>(editor, "canvas");
                canvas.SelectElement(item.Id);
                var combo = Read<ComboBox>(editor, "runProcessCombo");
                Check(combo.Items.Count == 3, "日志目录包含全部根流程，而非仅可启动流程");
                Check(Read<Label>(editor, "runProcessLabel").Text == "订阅流程（包含组合模块）", "日志订阅标签");
                combo.SelectedIndex = 1;
                var draft = Read<RuntimeLayoutSession>(editor, "session");
                Check((int)Get(draft.Find(item.Id), "LogProcessId") == 1, "指定流程选择可保存");
                draft.Undo();
                Check((int)Get(draft.Find(item.Id), "LogProcessId") == 2, "流程下拉修改可撤销");
                combo.SelectedIndex = 0;
                Check(Get(draft.Find(item.Id), "LogProcessId") == null, "全流程选择保存空编号");
            }
            using (var preview = new RuntimeInterfacePreviewForm(session.Document))
            {
                preview.WindowState = FormWindowState.Normal;
                preview.StartPosition = FormStartPosition.Manual;
                preview.Location = new Point(-24000, -24000);
                preview.ClientSize = new Size(1000, 600);
                preview.Show(); Application.DoEvents();
                var canvas = Read<RuntimeDesignCanvas>(preview, "canvas");
                Check(canvas.Controls.Count == 1, "预览创建原生日志控件");
                using (var bitmap = new Bitmap(preview.Width, preview.Height))
                {
                    preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log-preview.png"));
                }
                Check(preview.IsCurrentDocument, "日志预览不执行生产命令");
            }
            var source = new TestLogSource();
            using (var first = new RuntimeInterfacePreviewForm(session.Document))
            using (var second = new RuntimeInterfacePreviewForm(session.Document))
            {
                first.AttachLogSource(source); second.AttachLogSource(source);
                Check(source.Subscriptions.Count == 2 && source.LastProcessId == 2, "两个窗口按指定编号独立订阅");
                first.Dispose();
                Check(source.Subscriptions[0].Disposed && !source.Subscriptions[1].Disposed, "关闭一个窗口只退订自己的日志");
                second.AttachLogSource(source);
                Check(source.Subscriptions[1].Disposed && !source.Subscriptions[2].Disposed, "换绑先释放旧订阅");
            }
            Check(source.Subscriptions[2].Disposed, "最后窗口关闭退订");
            Check(!RuntimeLayoutSerializer.Serialize(session.Document).Contains("流程开始"), "示例日志正文不写入模板");
            var mixed = RuntimeLayoutSerializer.Clone(session.Document);
            mixed.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed, Width = 240, Height = 80,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 2 } });
            using (var editor = new RuntimeInterfaceEditorForm(mixed, "混合预览", null, null, document => new EmptyDataSource()))
            using (var preview = (RuntimeInterfacePreviewForm)typeof(RuntimeInterfaceEditorForm).GetMethod("CreatePreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null))
            {
                var widget = Read<RuntimeDesignCanvas>(preview, "canvas").Controls.OfType<RuntimeLogWidget>().Single();
                widget.RefreshLogs();
                Check(Read<int>(widget, "entryCount") > 0, "混合实时图像或耗时绑定的编辑预览也应显示示例日志");
            }
            Console.WriteLine("通过：日志布局，" + checks + " 项断言。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    /// <summary>读取新增属性而不让旧程序集测试在编译阶段失败。</summary>
    private static object Get(RuntimeLayoutElement item, string name) => typeof(RuntimeLayoutElement).GetProperty(name).GetValue(item);
    /// <summary>写入新增属性。</summary>
    private static void Set(RuntimeLayoutElement item, string name, object value) => typeof(RuntimeLayoutElement).GetProperty(name).SetValue(item, value);
    /// <summary>断言非法模板被明确拒绝。</summary>
    private static void Reject(Action action, string message)
    { try { action(); } catch (InvalidDataException) { Check(true, message); return; } throw new InvalidOperationException(message); }
    /// <summary>报告行为失败。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    /// <summary>读取窗口内部状态以验证实际属性选择接线。</summary>
    private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>区分可订阅的全部流程目录与运行按钮的主动流程目录。</summary>
    private sealed class LogCatalog : IRuntimeBindingCatalog, IRuntimeLogProcessCatalog
    {
        /// <summary>不提供图像和耗时绑定。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind) => new RuntimeBindingChoice[0];
        /// <summary>本测试不需要绑定名称。</summary>
        public string Describe(RuntimeDataBinding binding) => string.Empty;
        /// <summary>被动流程仍可订阅日志。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetLogProcesses() => new[] { new RuntimeProcessChoice { Id = 1, Name = "主动流程" }, new RuntimeProcessChoice { Id = 2, Name = "被动流程" } };
    }
    /// <summary>记录窗口订阅与退订，禁止执行生产操作。</summary>
    private sealed class TestLogSource : IRuntimeLogSource
    {
        /// <summary>每个窗口拥有不同的订阅。</summary>
        internal readonly List<TestSubscription> Subscriptions = new List<TestSubscription>();
        /// <summary>最近选择的流程。</summary>
        internal int? LastProcessId;
        /// <summary>创建独立记录实例。</summary>
        public IRuntimeLogSubscription Subscribe(int? processId, int capacity)
        { LastProcessId = processId; var subscription = new TestSubscription(); Subscriptions.Add(subscription); return subscription; }
    }
    /// <summary>不生成日志，仅验证生命周期。</summary>
    private sealed class TestSubscription : IRuntimeLogSubscription
    {
        /// <summary>释放标记。</summary>
        internal bool Disposed;
        /// <summary>不产生数据。</summary>
        public IReadOnlyList<RuntimeLogEntry> Drain(int maxCount) => new RuntimeLogEntry[0];
        /// <summary>没有缓存。</summary>
        public void Clear() { }
        /// <summary>记录退订。</summary>
        public void Dispose() { Disposed = true; }
    }
    /// <summary>混合预览使用的无生产副作用数据源。</summary>
    private sealed class EmptyDataSource : IRuntimeBindingDataSource
    {
        /// <summary>不提供图像或耗时更新。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() => new RuntimeValueUpdate[0];
        /// <summary>没有生产资源需要释放。</summary>
        public void Dispose() { }
    }
}
