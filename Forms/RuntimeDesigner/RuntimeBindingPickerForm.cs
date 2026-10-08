using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>按流程、组合模块、节点和输出层级选择只读运行数据源。</summary>
    public partial class RuntimeBindingPickerForm : Form
    {
        /// <summary>用户明确确认后的独立绑定，取消时始终为空。</summary>
        private RuntimeDataBinding confirmedBinding;
        /// <summary>复用设计器窗口的标题拖动、八向缩放和窗口按钮交互。</summary>
        private readonly RuntimeDesignerWindowChrome windowChrome;

        /// <summary>取得确认结果的深副本，调用方不能修改选择器持有的节点路径。</summary>
        public RuntimeDataBinding SelectedBinding => confirmedBinding?.Copy();

        /// <summary>供 Visual Studio 设计器显示固定控件布局。</summary>
        public RuntimeBindingPickerForm() : this(new RuntimeBindingChoice[0], null) { }

        /// <summary>读取目录快照，并按稳定作用域路径预选现有绑定。</summary>
        public RuntimeBindingPickerForm(IReadOnlyList<RuntimeBindingChoice> sources, RuntimeDataBinding selected)
        {
            InitializeComponent();
            windowChrome = new RuntimeDesignerWindowChrome(this, titleLabel, minimizeButton, maximizeButton, closeButton, windowToolTip);
            sourceTree.BeforeSelect += SourceBeforeSelect;
            sourceTree.AfterSelect += SourceAfterSelect;
            sourceTree.NodeMouseDoubleClick += SourceDoubleClicked;
            confirmButton.Click += ConfirmClicked;
            cancelButton.Click += CancelClicked;
            sourceTree.BeginUpdate();
            try
            {
                TreeNode selectedNode = null;
                if (sources != null)
                    foreach (var source in sources)
                        if (source != null) sourceTree.Nodes.Add(CreateNode(source, selected, ref selectedNode));
                foreach (TreeNode root in sourceTree.Nodes) root.Expand();
                if (selectedNode != null)
                {
                    sourceTree.SelectedNode = selectedNode;
                    selectedNode.EnsureVisible();
                }
                emptyLabel.Visible = sourceTree.Nodes.Count == 0;
                sourceTree.Visible = !emptyLabel.Visible;
                UpdateSelection();
            }
            finally { sourceTree.EndUpdate(); }
        }

        /// <summary>复制目录绑定，分组即使误带绑定也不能成为可确认项。</summary>
        private static TreeNode CreateNode(RuntimeBindingChoice source, RuntimeDataBinding selected, ref TreeNode selectedNode)
        {
            var node = new TreeNode(source.Name ?? string.Empty);
            if (source.Children != null)
                foreach (var child in source.Children)
                    if (child != null) node.Nodes.Add(CreateNode(child, selected, ref selectedNode));
            if ((source.Children == null || source.Children.Count == 0) && source.Binding != null)
            {
                node.Tag = source.Binding.Copy();
                node.ToolTipText = source.Binding.DisplayPath ?? string.Empty;
                if (selectedNode == null && IsSameSource(source.Binding, selected)) selectedNode = node;
            }
            return node;
        }

        /// <summary>按稳定路径识别同一输出，显示名称变化不影响预选。</summary>
        private static bool IsSameSource(RuntimeDataBinding left, RuntimeDataBinding right) => right != null &&
            left.Kind == right.Kind && left.ProcessId == right.ProcessId &&
            string.Equals(left.PropertyPath ?? string.Empty, right.PropertyPath ?? string.Empty, StringComparison.Ordinal) &&
            (left.NodePath ?? new int[0]).SequenceEqual(right.NodePath ?? new int[0]);

        /// <summary>允许展开分组，但禁止将分组或无绑定节点设为选择结果。</summary>
        private void SourceBeforeSelect(object sender, TreeViewCancelEventArgs e) => e.Cancel = !(e.Node.Tag is RuntimeDataBinding) || e.Node.Nodes.Count != 0;

        /// <summary>叶子选择变化时更新路径和确认按钮。</summary>
        private void SourceAfterSelect(object sender, TreeViewEventArgs e) => UpdateSelection();

        /// <summary>只展示当前候选，不在浏览目录期间生成提交结果。</summary>
        private void UpdateSelection()
        {
            var binding = sourceTree.SelectedNode?.Tag as RuntimeDataBinding;
            confirmButton.Enabled = binding != null;
            selectionLabel.Text = binding == null ? "未选择数据源" :
                string.IsNullOrEmpty(binding.DisplayPath) ? sourceTree.SelectedNode.FullPath : binding.DisplayPath;
        }

        /// <summary>双击有效叶子等同于确认，双击分组仅保留树的展开行为。</summary>
        private void SourceDoubleClicked(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !(e.Node.Tag is RuntimeDataBinding) || e.Node.Nodes.Count != 0) return;
            sourceTree.SelectedNode = e.Node;
            ConfirmClicked(sender, EventArgs.Empty);
        }

        /// <summary>确认后才复制选中绑定，避免浏览和取消污染宿主文档。</summary>
        private void ConfirmClicked(object sender, EventArgs e)
        {
            var binding = sourceTree.SelectedNode?.Tag as RuntimeDataBinding;
            if (binding == null || sourceTree.SelectedNode.Nodes.Count != 0) return;
            confirmedBinding = binding.Copy();
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>取消时不返回绑定，也不修改传入的现有选择。</summary>
        private void CancelClicked(object sender, EventArgs e)
        {
            confirmedBinding = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>标题关闭和系统关闭均视为取消，只有显式确认才保留返回绑定。</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
            {
                confirmedBinding = null;
                DialogResult = DialogResult.Cancel;
            }
            base.OnFormClosing(e);
        }

        /// <summary>保留原生移动、缩放和窗口命令，由客户端统一绘制标题外观。</summary>
        protected override CreateParams CreateParams => RuntimeDesignerWindowChrome.ConfigureStyles(base.CreateParams);

        /// <summary>复用非客户区重画保护、边缘命中及最大化工作区限制。</summary>
        protected override void WndProc(ref Message message)
        {
            if (!RuntimeDesignerWindowChrome.ProcessMessage(this, ref message)) base.WndProc(ref message);
        }
    }
}
