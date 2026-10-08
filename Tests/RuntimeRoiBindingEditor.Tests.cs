using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>图像与区域独立绑定的编辑器专项回归，不显示窗口或访问设备。</summary>
internal static class RuntimeRoiBindingEditorTests
{
    /// <summary>已通过的独立行为断言数量。</summary>
    private static int assertions;

    /// <summary>在独立界面线程中验证真实编辑器控件。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            var field = typeof(RuntimeInterfaceEditorForm).GetField("roiBindingPath", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(TextBox))
                throw new InvalidOperationException("图像属性必须提供独立的 ROI 源只读路径控件。");
            Application.EnableVisualStyles();
            VerifyIndependentBindings();
            VerifyPicker();
            VerifyLegacyCatalog();
            VerifyRoiOnlyPreview();
            Console.WriteLine("通过：ROI 绑定编辑器专项测试，" + assertions + " 项断言。");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("失败：" + ex.GetBaseException().Message); return 1; }
    }

    /// <summary>验证两条路径、独立提交清除、撤销重做、属性更新和模板往返。</summary>
    private static void VerifyIndependentBindings()
    {
        var catalog = new RoiCatalog();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "区域测试", null, catalog))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var canvas = Field<RuntimeDesignCanvas>(editor, "canvas");
            var element = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            canvas.SelectElement(element.Id);
            editor.Size = editor.MinimumSize;
            editor.PerformLayout();
            var layout = Field<TableLayoutPanel>(editor, "rightLayout");
            var roiActions = Field<TableLayoutPanel>(editor, "roiBindingActions");
            var path = Field<TextBox>(editor, "roiBindingPath");
            var choose = Field<Button>(editor, "chooseRoiBindingButton");
            var clear = Field<Button>(editor, "clearRoiBindingButton");
            Check(Field<Label>(editor, "bindingLabel").Text == "图像源" && Field<Label>(editor, "roiBindingLabel").Text == "ROI源", "图像属性必须区分图像源和 ROI 源");
            Check(path.ReadOnly && choose.Enabled && !clear.Enabled, "未绑定 ROI 显示只读路径并允许选择");
            Check(choose.Image != null && clear.Image != null, "ROI 操作必须使用省略号和清除图标");
            Check(Field<ToolTip>(editor, "windowToolTip").GetToolTip(choose) == "选择ROI源" &&
                Field<ToolTip>(editor, "windowToolTip").GetToolTip(clear) == "清除ROI源", "ROI 按钮必须有中文提示");
            Check(path.Width >= 100 && !path.Bounds.IntersectsWith(choose.Bounds) &&
                !choose.Bounds.IntersectsWith(clear.Bounds) && clear.Right <= roiActions.ClientSize.Width, "最小窗口下 ROI 路径和按钮不能重叠或越界");
            Check(!roiActions.Bounds.IntersectsWith(Field<TableLayoutPanel>(editor, "bindingActions").Bounds) &&
                !roiActions.Bounds.IntersectsWith(Field<PropertyGrid>(editor, "propertyGrid").Bounds), "两条来源和属性网格不能重叠");

            Invoke(editor, "ApplyBinding", element.Id, Binding(RuntimeBindingKind.Image));
            var roi = Binding(RuntimeBindingKind.Roi);
            Invoke(editor, "ApplyRoiBinding", element.Id, roi);
            roi.NodePath[0] = 99;
            Check(session.Find(element.Id).RoiBinding.NodePath[0] == 4, "ROI 提交必须深复制选择结果");
            Check(session.Find(element.Id).Binding.Kind == RuntimeBindingKind.Image && clear.Enabled, "添加 ROI 不能覆盖图像源");
            Check(path.Text == catalog.Description, "ROI 路径应显示当前目录描述");
            string before = RuntimeLayoutSerializer.Serialize(session.Document);
            catalog.Description = "已失效：区域输出";
            Invoke(editor, "RefreshProperties");
            Check(path.Text == catalog.Description && RuntimeLayoutSerializer.Serialize(session.Document) == before, "失效说明不能写回文档名称");
            RejectBinding(editor, "ApplyRoiBinding", element.Id, Binding(RuntimeBindingKind.Image), before);
            RejectBinding(editor, "ApplyBinding", element.Id, Binding(RuntimeBindingKind.Roi), before);

            Click(clear);
            Check(session.Find(element.Id).RoiBinding == null && session.Find(element.Id).Binding != null, "清除 ROI 不能清除图像源");
            session.Undo();
            Check(session.Find(element.Id).RoiBinding != null && session.Find(element.Id).Binding != null, "撤销必须恢复独立 ROI 绑定");
            session.Redo();
            Check(session.Find(element.Id).RoiBinding == null && session.Find(element.Id).Binding != null, "重做必须只清除 ROI");
            session.Undo();
            Click(Field<Button>(editor, "clearBindingButton"));
            Check(session.Find(element.Id).Binding == null && session.Find(element.Id).RoiBinding != null, "清除图像源不能清除 ROI");
            session.Undo();
            var properties = (RuntimeElementProperties)Field<PropertyGrid>(editor, "propertyGrid").SelectedObject;
            properties.Name = "独立区域图像";
            Invoke(editor, "PropertyChanged", null, null);
            Check(session.Find(element.Id).Name == properties.Name && session.Find(element.Id).RoiBinding != null &&
                session.Find(element.Id).Binding != null, "提交其它属性不能丢失两条绑定");
            var clone = RuntimeLayoutSerializer.Clone(session.Document);
            Check(clone.Elements[0].Binding.Kind == RuntimeBindingKind.Image && clone.Elements[0].RoiBinding.Kind == RuntimeBindingKind.Roi, "模板往返必须保留独立的两条绑定");
            clone.Elements[0].RoiBinding.NodePath[0] = 99;
            Check(session.Find(element.Id).RoiBinding.NodePath[0] == 4, "模板副本不能共享 ROI 节点路径");
            var legacy = Newtonsoft.Json.Linq.JObject.Parse(RuntimeLayoutSerializer.Serialize(session.Document));
            foreach (var item in (Newtonsoft.Json.Linq.JArray)legacy["Elements"]) ((Newtonsoft.Json.Linq.JObject)item).Remove("RoiBinding");
            var restored = RuntimeLayoutSerializer.Deserialize(legacy.ToString());
            Check(restored.Elements[0].RoiBinding == null && restored.Elements[0].Binding != null, "旧模板缺失 ROI 字段时保留图像源且无 ROI");

            foreach (RuntimeWidgetKind kind in Enum.GetValues(typeof(RuntimeWidgetKind)))
            {
                if (kind == RuntimeWidgetKind.Image) continue;
                var other = session.Add(kind, Point.Empty);
                canvas.SelectElement(other.Id);
                Check(layout.RowStyles[layout.GetRow(roiActions)].Height == 0, "非图像组件不占用 ROI 来源行");
                Invoke(editor, "ApplyRoiBinding", other.Id, Binding(RuntimeBindingKind.Roi));
                Check(session.Find(other.Id).RoiBinding == null, "非图像组件不能接收 ROI 绑定");
            }
            canvas.SelectElement(null);
            Check(layout.RowStyles[layout.GetRow(roiActions)].Height == 0, "页面属性不能显示 ROI 来源行");
        }
    }

    /// <summary>复用真实选择器，确认目录、预选、取消和返回副本均使用 ROI 而非图像绑定。</summary>
    private static void VerifyPicker()
    {
        var catalog = new RoiCatalog();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "目录测试", null, catalog))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            Invoke(editor, "ApplyBinding", element.Id, Binding(RuntimeBindingKind.Image));
            Invoke(editor, "ApplyRoiBinding", element.Id, Binding(RuntimeBindingKind.Roi));
            var before = RuntimeLayoutSerializer.Serialize(session.Document);
            using (var picker = (RuntimeBindingPickerForm)Invoke(editor, "CreateRoiBindingPicker", session.Find(element.Id)))
            {
                var tree = Field<TreeView>(picker, "sourceTree");
                Check(catalog.RoiRequests == 1 && catalog.ImageRequests == 0, "ROI 选择器必须只读取独立 ROI 目录");
                Check(tree.SelectedNode != null && ((RuntimeDataBinding)tree.SelectedNode.Tag).Kind == RuntimeBindingKind.Roi, "打开目录应预选已有 ROI 而非图像输出");
                Check(picker.SelectedBinding == null, "ROI 预选不能提前提交");
                Click(Field<Button>(picker, "cancelButton"));
                Check(picker.SelectedBinding == null && RuntimeLayoutSerializer.Serialize(session.Document) == before, "取消 ROI 选择不能改变任一绑定");
            }
            // 从真实未绑定状态检查分组，避免手动清空预选绕过树的选择事件。
            var unbound = session.Find(element.Id).Copy();
            unbound.RoiBinding = null;
            using (var picker = (RuntimeBindingPickerForm)Invoke(editor, "CreateRoiBindingPicker", unbound))
            {
                var tree = Field<TreeView>(picker, "sourceTree");
                IntPtr handle = tree.Handle;
                tree.SelectedNode = tree.Nodes[0];
                Check(tree.SelectedNode == null && !Field<Button>(picker, "confirmButton").Enabled, "ROI 分组不能确认");
                Click(Field<Button>(picker, "confirmButton"));
                Check(picker.DialogResult != DialogResult.OK && picker.SelectedBinding == null &&
                    RuntimeLayoutSerializer.Serialize(session.Document) == before, "分组不能提交或修改原有绑定");
                tree.SelectedNode = tree.Nodes[0].Nodes[0];
                Click(Field<Button>(picker, "confirmButton"));
                Check(picker.DialogResult == DialogResult.OK && picker.SelectedBinding.Kind == RuntimeBindingKind.Roi, "ROI 叶子确认应返回正确来源类型");
                var chosen = picker.SelectedBinding;
                Invoke(editor, "ApplyRoiBinding", element.Id, chosen);
                chosen.NodePath[0] = 99;
                Check(session.Find(element.Id).RoiBinding.NodePath[0] == 4 && session.Find(element.Id).Binding.Kind == RuntimeBindingKind.Image, "确认后复制 ROI 且不改变图像源");
            }
            session.Undo();
            session.Undo();
            Check(session.Find(element.Id).RoiBinding == null && session.Find(element.Id).Binding != null, "撤销 ROI 初次选择仍保留原图像源");
            session.Redo();
            Check(session.Find(element.Id).RoiBinding != null, "重做 ROI 初次选择应恢复绑定");
        }
    }

    /// <summary>旧目录或缺省目录不能伪造 ROI 来源，已有绑定仍可查看和独立清除。</summary>
    private static void VerifyLegacyCatalog()
    {
        foreach (var catalog in new IRuntimeBindingCatalog[] { new LegacyCatalog(), null })
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "旧目录", null, catalog))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            Field<RuntimeDesignCanvas>(editor, "canvas").SelectElement(element.Id);
            Invoke(editor, "ApplyBinding", element.Id, Binding(RuntimeBindingKind.Image));
            Invoke(editor, "ApplyRoiBinding", element.Id, Binding(RuntimeBindingKind.Roi));
            Check(Field<TextBox>(editor, "roiBindingPath").Text == Binding(RuntimeBindingKind.Roi).DisplayPath, "旧目录应保留已有 ROI 原始显示路径");
            using (var picker = (RuntimeBindingPickerForm)Invoke(editor, "CreateRoiBindingPicker", session.Find(element.Id)))
                Check(Field<TreeView>(picker, "sourceTree").Nodes.Count == 0 && !Field<Button>(picker, "confirmButton").Enabled, "不支持 ROI 的目录必须提供空树且不能确认");
            if (catalog == null) Check(!Field<Button>(editor, "chooseRoiBindingButton").Enabled, "无目录不能打开 ROI 来源选择");
            Check(Field<Button>(editor, "clearRoiBindingButton").Enabled, "ROI 清除不依赖目录是否仍可用");
            Click(Field<Button>(editor, "clearRoiBindingButton"));
            Check(session.Find(element.Id).RoiBinding == null && session.Find(element.Id).Binding != null, "旧目录下清除 ROI 不能影响图像源");
        }
    }

    /// <summary>仅 ROI 订阅也应启用已有实时预览工厂，不改变运行控制器或窗口管理。</summary>
    private static void VerifyRoiOnlyPreview()
    {
        int calls = 0;
        var source = new EmptySource();
        using (var editor = new RuntimeInterfaceEditorForm(new RuntimeLayoutDocument(), "区域预览", null, new RoiCatalog(),
            document => { calls++; return source; }))
        {
            var session = Field<RuntimeLayoutSession>(editor, "session");
            var element = session.Add(RuntimeWidgetKind.Image, Point.Empty);
            Invoke(editor, "ApplyRoiBinding", element.Id, Binding(RuntimeBindingKind.Roi));
            using (var preview = (RuntimeInterfacePreviewForm)Invoke(editor, "CreatePreview"))
                Check(calls == 1 && source.Disposals == 0, "仅 ROI 绑定也应创建实时数据源并交由预览持有");
            Check(source.Disposals == 1 && source.Drains == 0, "释放隐藏预览应仅释放数据源，不读取设备结果");
            Invoke(editor, "ApplyRoiBinding", element.Id, null);
            using (var preview = (RuntimeInterfacePreviewForm)Invoke(editor, "CreatePreview"))
                Check(calls == 1, "清除最后一个 ROI 后应恢复示例预览");
        }
    }

    /// <summary>不匹配来源必须被真实提交入口拒绝，且不改变文档。</summary>
    private static void RejectBinding(RuntimeInterfaceEditorForm editor, string method, string id, RuntimeDataBinding binding, string before)
    {
        bool rejected = false;
        try { Invoke(editor, method, id, binding); }
        catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
        Check(rejected && RuntimeLayoutSerializer.Serialize(Field<RuntimeLayoutSession>(editor, "session").Document) == before, "图像与 ROI 绑定类型不能混用");
    }

    /// <summary>构造不依赖现场设备的稳定来源标识。</summary>
    private static RuntimeDataBinding Binding(RuntimeBindingKind kind) => new RuntimeDataBinding
    {
        Kind = kind, ProcessId = 1, NodePath = new[] { 4, 7 },
        PropertyPath = kind == RuntimeBindingKind.Roi ? "OutputRegion" : "OutputImage",
        DisplayPath = kind == RuntimeBindingKind.Roi ? "流程 / 区域输出" : "流程 / 图像输出"
    };

    /// <summary>只支持旧图像来源契约的目录。</summary>
    private class LegacyCatalog : IRuntimeBindingCatalog
    {
        /// <summary>图像目录被请求的次数，用于发现 ROI 错用图像目录。</summary>
        public int ImageRequests { get; private set; }
        /// <summary>返回旧目录的图像来源。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetSources(RuntimeWidgetKind kind)
        { ImageRequests++; return new[] { new RuntimeBindingChoice { Name = "图像输出", Binding = Binding(RuntimeBindingKind.Image) } }; }
        /// <summary>旧目录只认识原有图像来源。</summary>
        public virtual string Describe(RuntimeDataBinding binding) => "当前图像路径";
    }

    /// <summary>提供独立 ROI 树，并模拟来源改名与失效。</summary>
    private sealed class RoiCatalog : LegacyCatalog, IRuntimeRoiBindingCatalog
    {
        /// <summary>ROI 目录请求次数。</summary>
        public int RoiRequests { get; private set; }
        /// <summary>当前 ROI 显示说明。</summary>
        public string Description { get; set; } = "已改名流程 / 区域输出";
        /// <summary>只返回区域来源，不混入图像或耗时。</summary>
        public IReadOnlyList<RuntimeBindingChoice> GetRoiSources()
        {
            RoiRequests++;
            var binding = Binding(RuntimeBindingKind.Roi);
            binding.DisplayPath = Description;
            return new[] { new RuntimeBindingChoice { Name = "流程", Children = new List<RuntimeBindingChoice>
                { new RuntimeBindingChoice { Name = "区域输出", Binding = binding } } } };
        }
        /// <summary>改变传入副本以验证显示刷新不暴露文档绑定。</summary>
        public override string Describe(RuntimeDataBinding binding)
        {
            binding.NodePath[0] = 77;
            return binding.Kind == RuntimeBindingKind.Roi ? Description : base.Describe(binding);
        }
    }

    /// <summary>只记录生命周期的空数据源，不访问设备。</summary>
    private sealed class EmptySource : IRuntimeBindingDataSource
    {
        /// <summary>释放次数。</summary>
        public int Disposals { get; private set; }
        /// <summary>读取次数。</summary>
        public int Drains { get; private set; }
        /// <summary>返回空数据批次。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain() { Drains++; return new RuntimeValueUpdate[0]; }
        /// <summary>记录所有权释放。</summary>
        public void Dispose() { Disposals++; }
    }

    /// <summary>取得真实设计器控件及编辑会话。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>调用真实提交入口或构造选择器，不打开模态窗口。</summary>
    private static object Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    /// <summary>直接派发按钮事件，不控制桌面。</summary>
    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty });
    /// <summary>记录行为断言并报告清晰的失败原因。</summary>
    private static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
}
