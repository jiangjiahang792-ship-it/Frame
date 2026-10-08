using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>运行控制编辑配置的独立回归测试，不调用正式运行控制器。</summary>
internal static class RuntimeRunControlEditorTests
{
    /// <summary>已经执行的断言数量。</summary>
    private static int assertions;

    /// <summary>在单线程单元中验证编辑器接口和行为，不显示桌面窗口。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Check(typeof(RuntimeInterfaceEditorForm).GetConstructors().Any(constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 6 && parameters[5].IsOptional && parameters[5].ParameterType.Name == "IRuntimeRunController";
            }), "编辑器必须提供可选的第六个运行控制器参数");
            VerifyProcessSelection();
            VerifyMissingController();
            VerifyPreviewIsolation(false);
            VerifyPreviewIsolation(true);
            Console.WriteLine("通过：运行控制编辑器专项测试，" + assertions + " 项断言。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("失败：" + ex.GetBaseException().Message);
            return 1;
        }
    }

    /// <summary>检查工具箱入口、流程选择、撤销以及失效编号保留。</summary>
    private static void VerifyProcessSelection()
    {
        var controller = new CatalogController();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "运行控制测试", null, null, null, controller))
        {
            var toolbox = Field<ListView>(editor, "toolbox");
            var entry = toolbox.Items.Cast<ListViewItem>().Single(item => (RuntimeWidgetKind)item.Tag == RuntimeWidgetKind.RunControl);
            Check(entry.Text == "运行控制" && toolbox.LargeImageList.Images.ContainsKey(entry.ImageKey), "基础控件栏必须显示运行控制名称和图标");
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var element = session.Add(RuntimeWidgetKind.RunControl, Point.Empty);
            canvas.SelectElement(element.Id);
            var combo = Field<ComboBox>(editor, "runProcessCombo");
            var layout = Field<TableLayoutPanel>(editor, "rightLayout");
            Check(element.RunProcessId == null && combo.Text == "全流程", "运行控制应默认选择全流程");
            Check(combo.DropDownStyle == ComboBoxStyle.DropDownList && combo.Items.Count == 3, "流程下拉框应仅列出全流程和目录项");
            Check(layout.RowStyles[1].Height == 0 && layout.RowStyles[2].Height > 0, "运行控制应显示运行流程配置并隐藏图像耗时绑定条");
            editor.Size = editor.MinimumSize;
            editor.PerformLayout();
            Check(combo.Width >= 150 && combo.Right <= combo.Parent.ClientSize.Width && combo.Bottom <= combo.Parent.ClientSize.Height, "最小窗口下流程下拉框应完整显示");
            combo.SelectedIndex = FindProcess(combo, 2);
            Check(session.Find(element.Id).RunProcessId == 2, "切换下拉项应提交指定流程编号");
            session.Undo();
            Check(session.Find(element.Id).RunProcessId == null && combo.Text == "全流程", "流程选择应能撤销到全流程");
            session.Redo();
            Check(session.Find(element.Id).RunProcessId == 2 && SelectedProcess(combo) == 2, "流程选择应能重做并同步下拉框");
            string before = RuntimeLayoutSerializer.Serialize(session.Document);
            controller.Processes[1].Name = "重命名流程";
            Invoke(editor, "RefreshProperties");
            Check(combo.Text == "重命名流程" && RuntimeLayoutSerializer.Serialize(session.Document) == before, "流程改名只更新显示，不改变保存编号");
            controller.Processes.RemoveAt(1);
            Invoke(editor, "RefreshProperties");
            Check(combo.Text.Contains("已失效") && SelectedProcess(combo) == 2 && session.Find(element.Id).RunProcessId == 2, "不存在的流程编号必须保留并标明失效");
            Check(RuntimeLayoutSerializer.Serialize(session.Document) == before, "失效流程刷新不能自动回退全流程或修改文档");
            combo.SelectedIndex = 0;
            Check(session.Find(element.Id).RunProcessId == null, "用户显式选择全流程时应清除指定编号");
            session.Undo();
            Check(session.Find(element.Id).RunProcessId == 2 && combo.Text.Contains("已失效"), "撤销显式回退后应恢复失效编号");
            controller.FailDirectory = true;
            Invoke(editor, "RefreshProperties");
            Check(SelectedProcess(combo) == 2 && session.Find(element.Id).RunProcessId == 2, "目录读取失败不能丢失已有流程编号");
            controller.FailDirectory = false;
            var image = session.Add(RuntimeWidgetKind.Image, new Point(20, 20));
            canvas.SelectElement(image.Id);
            Check(layout.RowStyles[1].Height > 0 && layout.RowStyles[2].Height == 0, "切换到图像时应恢复绑定条并隐藏运行流程配置");
            canvas.SelectElement(null);
            Check(layout.RowStyles[2].Height == 0, "页面属性不能显示组件运行流程配置");
            Check(controller.RunCalls == 0 && controller.StopCalls == 0 && controller.StateReads == 0, "编辑过程只能读取正式控制器目录，不能读取运行状态或执行控制");
        }
    }

    /// <summary>未注入控制器时仍允许全流程配置，已有未知编号保持失效状态。</summary>
    private static void VerifyMissingController()
    {
        using (var editor = new RuntimeInterfaceEditorForm())
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.RunControl, Point.Empty);
            Field<RuntimeDesignCanvas>(editor, "canvas").SelectElement(element.Id);
            var combo = Field<ComboBox>(editor, "runProcessCombo");
            Check(combo.Enabled && combo.Items.Count == 1 && combo.Text == "全流程", "无控制器时应允许选择全流程");
            session.Edit(document => session.Find(element.Id).RunProcessId = 99);
            Check(SelectedProcess(combo) == 99 && combo.Text.Contains("已失效"), "无目录也不能把模板保存的指定流程改成全流程");
            session.Edit(document => session.Find(element.Id).RunProcessId = null);
            using (var preview = CreatePreview(editor))
            {
                var controllers = FindControllers(preview).Distinct().ToArray();
                Check(controllers.Length > 0 && controllers.All(item => item is RuntimePreviewRunController), "无正式控制器的预览也必须注入模拟控制器");
            }
        }
    }

    /// <summary>示例与绑定结果预览都只能持有新的模拟运行控制器。</summary>
    private static void VerifyPreviewIsolation(bool liveData)
    {
        var controller = new CatalogController();
        var source = new EmptyDataSource();
        Func<RuntimeLayoutDocument, IRuntimeBindingDataSource> factory = liveData ? new Func<RuntimeLayoutDocument, IRuntimeBindingDataSource>(document => source) : null;
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "模拟预览", null, null, factory, controller))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            session.Add(RuntimeWidgetKind.RunControl, Point.Empty);
            if (liveData)
            {
                var elapsed = session.Add(RuntimeWidgetKind.Elapsed, new Point(0, 80));
                session.Edit(document => session.Find(elapsed.Id).Binding = new RuntimeDataBinding
                { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 1, DisplayPath = "流程1 / 运行耗时" });
            }
            IRuntimeRunController firstController;
            using (var preview = CreatePreview(editor))
            {
                var controllers = FindControllers(preview).Distinct().ToArray();
                Check(controllers.Length == 1 && controllers[0] is RuntimePreviewRunController && !ReferenceEquals(controllers[0], controller), "编辑预览必须注入独立模拟控制器，不能传入正式控制器");
                firstController = controllers[0];
                Check(firstController.GetProcesses().Select(item => item.Id).SequenceEqual(new[] { 1, 2 }), "模拟预览应保留宿主提供的可选流程目录");
            }
            if (!liveData)
            {
                using (var preview = CreatePreview(editor))
                    Check(!ReferenceEquals(FindControllers(preview).First(), firstController), "每次打开编辑预览都应创建新的模拟控制器");
            }
            Check(controller.RunCalls == 0 && controller.StopCalls == 0 && controller.StateReads == 0, "预览创建与关闭不能触发正式控制器状态或操作");
        }
        Check(source.DisposeCount == (liveData ? 1 : 0), "注入模拟运行控制器后仍应保持绑定数据源的原有释放责任");
    }

    /// <summary>只提供流程目录，任何正式运行调用都会使测试失败。</summary>
    private sealed class CatalogController : IRuntimeRunController
    {
        /// <summary>模拟宿主流程改名和删除的目录。</summary>
        public List<RuntimeProcessChoice> Processes { get; } = new List<RuntimeProcessChoice>
        { new RuntimeProcessChoice { Id = 1, Name = "流程1" }, new RuntimeProcessChoice { Id = 2, Name = "流程2" } };
        /// <summary>模拟目录读取失败。</summary>
        public bool FailDirectory { get; set; }
        /// <summary>正式运行入口调用次数。</summary>
        public int RunCalls { get; private set; }
        /// <summary>正式停止入口调用次数。</summary>
        public int StopCalls { get; private set; }
        /// <summary>正式运行状态读取次数。</summary>
        public int StateReads { get; private set; }
        /// <summary>编辑器不应读取正式运行状态。</summary>
        public bool IsRunning { get { StateReads++; return false; } }
        /// <summary>编辑器不应读取正式停止状态。</summary>
        public bool IsStopping { get { StateReads++; return false; } }
        /// <summary>返回测试目录或模拟读取失败。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetProcesses()
        {
            if (FailDirectory) throw new InvalidOperationException("测试目录不可用");
            return Processes;
        }
        /// <summary>禁止测试触发正式运行入口。</summary>
        public Task RunAsync(int? processId, bool continuous) { RunCalls++; throw new InvalidOperationException("不能从编辑器执行正式运行"); }
        /// <summary>禁止测试触发正式停止入口。</summary>
        public Task StopAsync() { StopCalls++; throw new InvalidOperationException("不能从编辑器执行正式停止"); }
    }

    /// <summary>用于验证实时数据预览资源移交的空结果来源。</summary>
    private sealed class EmptyDataSource : IRuntimeBindingDataSource
    {
        /// <summary>释放次数。</summary>
        public int DisposeCount { get; private set; }
        /// <summary>不产生现场结果或设备访问。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() => new RuntimeValueUpdate[0];
        /// <summary>记录预览资源释放。</summary>
        public void Dispose() { DisposeCount++; }
    }

    /// <summary>从真实预览窗口及其运行按钮控件读取实际接入的控制器。</summary>
    private static IEnumerable<IRuntimeRunController> FindControllers(Control control)
    {
        for (Type type = control.GetType(); type != null && type != typeof(Control); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (typeof(IRuntimeRunController).IsAssignableFrom(field.FieldType) && field.GetValue(control) is IRuntimeRunController controller)
                    yield return controller;
        foreach (Control child in control.Controls)
            foreach (var controller in FindControllers(child)) yield return controller;
    }

    /// <summary>取得当前下拉项保存的可空流程编号。</summary>
    private static int? SelectedProcess(ComboBox combo) => (int?)combo.SelectedItem.GetType().GetProperty("ProcessId").GetValue(combo.SelectedItem);

    /// <summary>按编号查找目录项，不依赖显示名称。</summary>
    private static int FindProcess(ComboBox combo, int id)
    {
        for (int index = 0; index < combo.Items.Count; index++)
            if ((int?)combo.Items[index].GetType().GetProperty("ProcessId").GetValue(combo.Items[index]) == id) return index;
        throw new InvalidOperationException("测试流程未出现在下拉目录中");
    }

    /// <summary>读取真实控件或会话以检查编辑结果。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>执行正常刷新入口，不发送桌面输入。</summary>
    private static void Invoke(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
    /// <summary>创建隐藏预览以检查实际控制器注入，不显示模态窗口。</summary>
    private static RuntimeInterfacePreviewForm CreatePreview(RuntimeInterfaceEditorForm editor) =>
        (RuntimeInterfacePreviewForm)editor.GetType().GetMethod("CreatePreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);

    /// <summary>在行为不符合约定时报告明确错误。</summary>
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
