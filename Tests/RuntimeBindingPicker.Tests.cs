using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>数据源选择器和编辑器绑定交互的独立回归测试，不启动生产入口。</summary>
internal static class RuntimeBindingPickerTests
{
    /// <summary>已完成的行为断言数量。</summary>
    private static int assertions;

    /// <summary>在单线程单元中验证绑定交互。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Check(typeof(RuntimeInterfaceEditorForm).GetConstructors().Any(constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length >= 5 && parameters[3].IsOptional && parameters[4].IsOptional &&
                    parameters[3].ParameterType == typeof(IRuntimeBindingCatalog) &&
                    parameters[4].ParameterType == typeof(Func<RuntimeLayoutDocument, IRuntimeBindingDataSource>);
            }), "编辑器必须兼容可选的第四个目录参数和第五个实时数据源工厂参数");
            VerifyPicker();
            VerifyPickerChrome();
            VerifySelectionIdentity();
            VerifyEditorBindings();
            VerifyMissingCatalog();
            VerifyPreviewFactory();
            VerifyPreviewWithoutFactory();
            VerifyPreviewConstructionFailure();
            Console.WriteLine("通过：绑定选择器专项测试，" + assertions + " 项断言。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("失败：" + ex.GetBaseException().Message);
            return 1;
        }
    }

    /// <summary>验证单条无边框标题、最小尺寸布局和标题关闭的取消语义。</summary>
    private static void VerifyPickerChrome()
    {
        var binding = ImageBinding();
        using (var picker = new RuntimeBindingPickerForm(Choices(binding), binding))
        {
            IntPtr handle = picker.Handle;
            picker.Size = picker.MinimumSize;
            picker.PerformLayout();
            var title = Field<Label>(picker, "titleLabel");
            var minimize = Field<Button>(picker, "minimizeButton");
            var maximize = Field<Button>(picker, "maximizeButton");
            var close = Field<Button>(picker, "closeButton");
            var tooltip = Field<ToolTip>(picker, "windowToolTip");
            Check(picker.FormBorderStyle == FormBorderStyle.None, "数据源选择器必须移除原生标题栏");
            Check(title.Text == "选择数据源" && title.Parent.Height * 96f / picker.DeviceDpi <= 32.5f, "选择器应使用单条紧凑中文标题");
            Check(!title.Bounds.IntersectsWith(minimize.Bounds) && !minimize.Bounds.IntersectsWith(maximize.Bounds) &&
                !maximize.Bounds.IntersectsWith(close.Bounds) && close.Right <= close.Parent.ClientSize.Width, "最小窗口尺寸下标题和窗口按钮不能重叠或越界");
            Check(minimize.Image != null && maximize.Image != null && close.Image != null, "选择器窗口按钮应复用标准图标");
            Check(tooltip.GetToolTip(minimize) == "最小化" && tooltip.GetToolTip(maximize) == "最大化" && tooltip.GetToolTip(close) == "关闭", "选择器窗口按钮应提供中文提示");
            Check(ReferenceEquals(picker.AcceptButton, Field<Button>(picker, "confirmButton")) &&
                ReferenceEquals(picker.CancelButton, Field<Button>(picker, "cancelButton")), "无边框改造不能改变 Enter 和 Esc 的确认取消入口");
            Check(!minimize.TabStop && !maximize.TabStop && !close.TabStop, "窗口按钮不能插入原有键盘焦点顺序");
            Click(close);
            Check(picker.DialogResult == DialogResult.Cancel && picker.SelectedBinding == null, "标题关闭按钮必须取消，不能提交预选绑定");
            Check(picker.IsDisposed && binding.NodePath.SequenceEqual(new[] { 4, 7 }), "关闭选择器应释放窗口且不修改输入绑定");
        }
    }

    /// <summary>有绑定时才创建真实数据源，成功后由预览窗口独占释放责任。</summary>
    private static void VerifyPreviewFactory()
    {
        int calls = 0;
        RuntimeLayoutDocument received = null;
        var source = new TestDataSource();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "预览测试", null, null,
            document => { calls++; received = document; return source; }))
        {
            Check(calls == 0, "编辑器构造期间不能创建或订阅实时数据源");
            using (var sample = CreatePreview(editor))
            {
                Check(calls == 0 && Field<RuntimeLiveViewController>(sample, "liveController") == null, "无绑定时必须保持示例预览且不调用工厂");
            }
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var image = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            session.Edit(document => session.Find(image.Id).Binding = ImageBinding());
            using (var live = CreatePreview(editor))
            {
                Check(calls == 1 && ReferenceEquals(received, session.Document), "实时预览工厂应只接收当前编辑文档一次");
                Check(Field<RuntimeLiveViewController>(live, "liveController") != null, "有绑定且工厂可用时应创建真实预览控制器");
                Check(source.DisposeCount == 0, "预览构造成功后编辑器不能提前释放数据源");
            }
            Check(source.DisposeCount == 1, "关闭预览应恰好释放一次数据源");
            Check(source.DrainCalls == 0, "未显示的测试预览不能主动读取运行结果");
            session.Edit(document => session.Find(image.Id).Binding = null);
            using (var sample = CreatePreview(editor))
                Check(calls == 1 && Field<RuntimeLiveViewController>(sample, "liveController") == null, "清除最后一个绑定后应恢复示例预览");
        }
        Check(source.DisposeCount == 1, "编辑器释放不能重复释放已交给预览的数据源");
    }

    /// <summary>只提供旧版目录参数的调用方仍使用示例预览，即使文档包含绑定。</summary>
    private static void VerifyPreviewWithoutFactory()
    {
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "兼容预览", null, new TestCatalog()))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var image = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            session.Edit(document => session.Find(image.Id).Binding = ImageBinding());
            using (var sample = CreatePreview(editor))
                Check(Field<RuntimeLiveViewController>(sample, "liveController") == null, "无工厂时含绑定的文档也必须保持示例预览");
        }
    }

    /// <summary>用真实预览构造中的文档校验失败验证数据源异常释放，不显示窗口。</summary>
    private static void VerifyPreviewConstructionFailure()
    {
        var source = new TestDataSource();
        int calls = 0;
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "失败预览", null, null,
            document =>
            {
                calls++;
                // 仅测试故障注入：让数据源创建之后的预览文档校验失败。
                document.Width = 0;
                return source;
            }))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var image = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            session.Edit(document => session.Find(image.Id).Binding = ImageBinding());
            int width = session.Document.Width;
            bool rejected = false;
            try { using (var preview = CreatePreview(editor)) { } }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
            finally { session.Document.Width = width; }
            Check(rejected && calls == 1, "预览构造失败应保留原始校验异常且不能重复创建数据源");
            Check(source.DisposeCount == 1, "预览构造失败时编辑器必须释放尚未移交的数据源");
        }
        Check(source.DisposeCount == 1, "构造失败后的编辑器释放不能再次释放数据源");
    }

    /// <summary>调用实际预览创建流程，测试不显示模态窗体或操作桌面。</summary>
    private static RuntimeInterfacePreviewForm CreatePreview(RuntimeInterfaceEditorForm editor) =>
        (RuntimeInterfacePreviewForm)typeof(RuntimeInterfaceEditorForm).GetMethod("CreatePreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);

    /// <summary>记录所有权交接的空数据源，不依赖现场设备或生产结果。</summary>
    private sealed class TestDataSource : IRuntimeBindingDataSource
    {
        /// <summary>释放调用次数，用于发现提前释放或重复释放。</summary>
        public int DisposeCount { get; private set; }
        /// <summary>读取调用次数，隐藏预览应保持为零。</summary>
        public int DrainCalls { get; private set; }
        /// <summary>提供空更新批次，不产生任何设备访问。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() { DrainCalls++; return new RuntimeValueUpdate[0]; }
        /// <summary>记录释放动作而不连接任何外部资源。</summary>
        public void Dispose() { DisposeCount++; }
    }

    /// <summary>验证分组不能确认、叶子双击、取消与空目录不会改变输入绑定。</summary>
    private static void VerifyPicker()
    {
        var source = ImageBinding();
        var choices = Choices(source);
        using (var picker = new RuntimeBindingPickerForm(choices, null))
        {
            picker.Size = picker.MinimumSize;
            picker.PerformLayout();
            var confirm = Field<Button>(picker, "confirmButton");
            var cancel = Field<Button>(picker, "cancelButton");
            Check(!confirm.Bounds.IntersectsWith(cancel.Bounds) && cancel.Right <= cancel.Parent.ClientSize.Width, "最小选择器窗口内确认和取消按钮不能重叠或越界");
            var tree = Field<TreeView>(picker, "sourceTree");
            IntPtr handle = tree.Handle;
            Check(tree.Nodes[0].Nodes[0].Nodes[0].Nodes[0].Text == "输出图像", "选择器应保留流程、组合模块、节点、输出层级");
            tree.SelectedNode = tree.Nodes[0];
            Check(tree.SelectedNode == null && !Field<Button>(picker, "confirmButton").Enabled, "带绑定的分组也不能成为可确认项");
            DoubleClick(tree, tree.Nodes[0]);
            Check(picker.DialogResult != DialogResult.OK, "双击分组不能确认绑定");
            Click(Field<Button>(picker, "confirmButton"));
            Check(picker.DialogResult != DialogResult.OK && picker.SelectedBinding == null, "分组不能通过确认按钮提交");
            var leaf = tree.Nodes[0].Nodes[0].Nodes[0].Nodes[0];
            tree.SelectedNode = leaf;
            Check(Field<Button>(picker, "confirmButton").Enabled, "选中有效叶子后应允许确认");
            source.NodePath[0] = 99;
            DoubleClick(tree, leaf);
            Check(picker.DialogResult == DialogResult.OK && picker.SelectedBinding.NodePath[0] == 4, "双击叶子应返回目录的独立绑定快照");
            var result = picker.SelectedBinding;
            result.NodePath[0] = 88;
            Check(picker.SelectedBinding.NodePath[0] == 4, "返回结果不能暴露选择器内部的可变路径");
        }
        source = ImageBinding();
        using (var picker = new RuntimeBindingPickerForm(Choices(source), source))
        {
            Click(Field<Button>(picker, "cancelButton"));
            Check(picker.DialogResult == DialogResult.Cancel && picker.SelectedBinding == null, "取消选择不得返回待提交的绑定");
            Check(source.NodePath.SequenceEqual(new[] { 4, 7 }), "取消选择不能修改现有绑定");
        }
        using (var picker = new RuntimeBindingPickerForm(new RuntimeBindingChoice[0], null))
        {
            Check(!Field<Button>(picker, "confirmButton").Enabled, "空目录必须禁用确认");
            Check(Field<TreeView>(picker, "sourceTree").Nodes.Count == 0, "空目录不能构造伪数据源");
        }
    }

    /// <summary>预选只依赖稳定编号、完整节点路径、种类和输出路径，不依赖显示名称。</summary>
    private static void VerifySelectionIdentity()
    {
        var source = ImageBinding();
        var renamed = source.Copy();
        renamed.DisplayPath = "改名后的流程 / 输出图像";
        using (var picker = new RuntimeBindingPickerForm(Choices(renamed), source))
        {
            var tree = Field<TreeView>(picker, "sourceTree");
            Check(tree.SelectedNode != null && tree.SelectedNode.Text == "输出图像", "数据源改名后仍应预选相同稳定路径");
            Check(picker.SelectedBinding == null, "预选不等于确认");
        }
        var unrelated = source.Copy();
        unrelated.NodePath = new[] { 5, 7 };
        using (var picker = new RuntimeBindingPickerForm(Choices(unrelated), source))
            Check(Field<TreeView>(picker, "sourceTree").SelectedNode == null, "不同组合模块下的同编号节点不能误预选");
        unrelated = source.Copy();
        unrelated.PropertyPath = "OtherImage";
        using (var picker = new RuntimeBindingPickerForm(Choices(unrelated), source))
            Check(Field<TreeView>(picker, "sourceTree").SelectedNode == null, "同节点不同输出不能误预选");
    }

    /// <summary>在真实会话中检查绑定复制、取消、清除、撤销，以及重命名展示不写回文档。</summary>
    private static void VerifyEditorBindings()
    {
        var catalog = new TestCatalog();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "测试方案", null, catalog))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var image = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            canvas.SelectElement(image.Id);
            editor.Size = editor.MinimumSize;
            editor.PerformLayout();
            var path = Field<TextBox>(editor, "bindingPath");
            var choose = Field<Button>(editor, "chooseBindingButton");
            var clear = Field<Button>(editor, "clearBindingButton");
            Check(path.Width >= 100 && !path.Bounds.IntersectsWith(choose.Bounds) && !choose.Bounds.IntersectsWith(clear.Bounds), "最小编辑器窗口的数据源路径及按钮不能重叠");
            Check(clear.Right <= clear.Parent.ClientSize.Width, "清除按钮应完整显示在属性栏内");
            Check(Field<ToolTip>(editor, "windowToolTip").GetToolTip(choose) == "选择数据源" &&
                Field<ToolTip>(editor, "windowToolTip").GetToolTip(clear) == "清除数据源", "绑定图标按钮应有中文悬停提示");
            Check(Field<TextBox>(editor, "bindingPath").ReadOnly, "数据源路径只能通过选择器修改");
            Check(Field<Button>(editor, "chooseBindingButton").Enabled && !Field<Button>(editor, "clearBindingButton").Enabled, "未绑定组件应允许选择但不能清除");
            var binding = ImageBinding();
            Invoke(editor, "ApplyBinding", image.Id, binding);
            Check(session.Find(image.Id).Binding != null, "选择结果应写入当前组件");
            binding.NodePath[0] = 33;
            Check(session.Find(image.Id).Binding.NodePath[0] == 4, "会话必须复制选择结果的节点路径");
            Check(Field<TextBox>(editor, "bindingPath").Text == catalog.Description, "绑定路径应通过目录解析当前名称");
            string beforeMismatch = RuntimeLayoutSerializer.Serialize(session.Document);
            bool rejected = false;
            try { Invoke(editor, "ApplyBinding", image.Id, new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 1 }); }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
            Check(rejected && RuntimeLayoutSerializer.Serialize(session.Document) == beforeMismatch, "图像组件必须拒绝耗时绑定且不修改文档");
            string savedDisplayPath = session.Find(image.Id).Binding.DisplayPath;
            string before = RuntimeLayoutSerializer.Serialize(session.Document);
            catalog.Description = "已失效：原图像输出";
            Invoke(editor, "RefreshProperties");
            Check(Field<TextBox>(editor, "bindingPath").Text == catalog.Description, "失效数据源应显示目录提供的失效说明");
            Check(RuntimeLayoutSerializer.Serialize(session.Document) == before && session.Find(image.Id).Binding.DisplayPath == savedDisplayPath, "刷新路径不能写回名称或创建修改");
            Click(Field<Button>(editor, "clearBindingButton"));
            Check(session.Find(image.Id).Binding == null, "清除按钮应清除绑定");
            session.Undo();
            Check(session.Find(image.Id).Binding != null && session.Find(image.Id).Binding.NodePath[0] == 4, "清除绑定必须可以撤销");
            session.Undo();
            Check(session.Find(image.Id).Binding == null, "选择绑定必须可以撤销");
            session.Redo();
            Check(session.Find(image.Id).Binding != null, "绑定应能够重做");
            var elapsed = session.Add(RuntimeWidgetKind.Elapsed, new Point(40, 40));
            canvas.SelectElement(elapsed.Id);
            var processTime = new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 1, DisplayPath = "流程 / 运行耗时" };
            Invoke(editor, "ApplyBinding", elapsed.Id, processTime);
            Check(session.Find(elapsed.Id).Binding.Kind == RuntimeBindingKind.ProcessElapsed, "耗时组件应支持流程耗时");
            var nodeTime = new RuntimeDataBinding { Kind = RuntimeBindingKind.NodeElapsed, ProcessId = 1, NodePath = new[] { 4, 7 }, DisplayPath = "流程 / 节点 / 运行耗时" };
            Invoke(editor, "ApplyBinding", elapsed.Id, nodeTime);
            Check(session.Find(elapsed.Id).Binding.Kind == RuntimeBindingKind.NodeElapsed, "耗时组件应支持节点耗时");
            Check(Field<TableLayoutPanel>(editor, "rightLayout").RowStyles[1].Height > 0, "耗时组件应显示数据源条");
            foreach (var kind in new[] { RuntimeWidgetKind.Text, RuntimeWidgetKind.Signal })
            {
                var element = session.Add(kind, new Point(80, 80));
                canvas.SelectElement(element.Id);
                Check(Field<TableLayoutPanel>(editor, "rightLayout").RowStyles[1].Height == 0, "文字和信号灯应保持原属性布局");
                Invoke(editor, "ApplyBinding", element.Id, ImageBinding());
                Check(session.Find(element.Id).Binding == null, "文字和信号灯不能通过绑定提交入口获得数据源");
            }
            canvas.SelectElement(null);
            Check(Field<TableLayoutPanel>(editor, "rightLayout").RowStyles[1].Height == 0, "页面属性不能显示组件数据源条");
        }
    }

    /// <summary>缺省目录兼容旧构造调用，不允许误触绑定按钮。</summary>
    private static void VerifyMissingCatalog()
    {
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "无目录", null))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var image = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            Field<RuntimeDesignCanvas>(editor, "canvas").SelectElement(image.Id);
            Check(!Field<Button>(editor, "chooseBindingButton").Enabled && !Field<Button>(editor, "clearBindingButton").Enabled, "没有目录时选择和清除按钮均应禁用");
            session.Edit(document => session.Find(image.Id).Binding = ImageBinding());
            Check(!Field<Button>(editor, "clearBindingButton").Enabled, "无目录时已有绑定的清除按钮也必须禁用");
            Check(Field<TextBox>(editor, "bindingPath").Text == ImageBinding().DisplayPath, "无目录时应保留已有绑定的可读路径");
        }
    }

    /// <summary>提供具有完整嵌套路径的图像绑定样例。</summary>
    private static RuntimeDataBinding ImageBinding() => new RuntimeDataBinding
    {
        Kind = RuntimeBindingKind.Image, ProcessId = 1, NodePath = new[] { 4, 7 },
        PropertyPath = "OutputImage", DisplayPath = "流程 / 组合模块 / 图像节点 / 输出图像"
    };

    /// <summary>构造真实目录契约，包括不能被确认为叶子的异常分组绑定。</summary>
    private static IReadOnlyList<RuntimeBindingChoice> Choices(RuntimeDataBinding binding) => new[]
    {
        new RuntimeBindingChoice { Name = "流程", Binding = binding.Copy(), Children = new List<RuntimeBindingChoice>
        {
            new RuntimeBindingChoice { Name = "组合模块", Children = new List<RuntimeBindingChoice>
            {
                new RuntimeBindingChoice { Name = "图像节点", Children = new List<RuntimeBindingChoice>
                {
                    new RuntimeBindingChoice { Name = "输出图像", Binding = binding }
                } }
            } }
        } }
    };

    /// <summary>隔离真实设备的目录提供者；被测会话和界面使用真实实现。</summary>
    private sealed class TestCatalog : IRuntimeBindingCatalog
    {
        /// <summary>模拟宿主改名或数据源失效后的描述。</summary>
        public string Description { get; set; } = "已改名流程 / 输出图像";
        /// <summary>提供本测试所需的图像目录。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind) => Choices(ImageBinding());
        /// <summary>返回当前宿主描述，不修改输入绑定。</summary>
        public string Describe(RuntimeDataBinding binding) => Description;
    }

    /// <summary>读取真实设计器控件或工作会话。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    /// <summary>调用实际提交入口，避免显示模态窗口或控制桌面。</summary>
    private static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);

    /// <summary>直接分发按钮事件，无需鼠标操作。</summary>
    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty });

    /// <summary>分发树叶子双击事件，不激活桌面窗口。</summary>
    private static void DoubleClick(TreeView tree, TreeNode node) => typeof(TreeView).GetMethod("OnNodeMouseDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tree,
        new object[] { new TreeNodeMouseClickEventArgs(node, MouseButtons.Left, 2, 0, 0) });

    /// <summary>在行为与预期不符时提供明确的失败原因。</summary>
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
