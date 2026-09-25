using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

namespace TDJS_Vision.Forms.ProcessNew
{
    public partial class FormNewProcessWizard : IMessageFilter
    {
        /// <summary>折叠路径按编号保存，流程或模块重命名不会丢失展开状态。</summary>
        private readonly HashSet<string> _collapsedNavigationPaths = new HashSet<string>();
        /// <summary>树重建期间不记录自动折叠事件。</summary>
        private bool _buildingNavigation;
        /// <summary>弹层打开期间注册鼠标与退出键消息过滤。</summary>
        private bool _navigationFilterAttached;

        /// <summary>初始化顶部导航生命周期。</summary>
        private void InitializeProcessNavigation()
        {
            treeProcessNavigation.AfterCollapse += NavigationExpansionChanged;
            treeProcessNavigation.AfterExpand += NavigationExpansionChanged;
            Deactivate += (s, e) => CloseProcessNavigation();
            Disposed += (s, e) => CloseProcessNavigation();
        }

        /// <summary>为流程编辑器绑定导航刷新。</summary>
        private void AttachProcessNavigation(ProcessEditPanel editor)
        {
            editor.NavigationChanged += (s, e) =>
            {
                tabControl1.Invalidate();
                toolTipNavigation.SetToolTip(buttonProcessNavigation, editor.NavigationPath);
            };
        }

        /// <summary>切换可折叠的顶部流程列表。</summary>
        private void buttonProcessNavigation_Click(object sender, EventArgs e)
        {
            if (panelProcessNavigation.Visible) { CloseProcessNavigation(); return; }
            RefreshProcessNavigation();
            int width = Math.Min(360, panelEditorSurface.ClientSize.Width);
            int top = GetProcessHeaderHeight();
            panelProcessNavigation.Bounds = new Rectangle(Math.Max(0, panelEditorSurface.ClientSize.Width - width),
                top, width, Math.Max(80, Math.Min(420, panelEditorSurface.ClientSize.Height - top)));
            panelProcessNavigation.Visible = true;
            panelProcessNavigation.BringToFront();
            treeProcessNavigation.Focus();
            if (!_navigationFilterAttached) { Application.AddMessageFilter(this); _navigationFilterAttached = true; }
        }

        /// <summary>隐藏导航弹层并移除消息监听。</summary>
        private void CloseProcessNavigation()
        {
            panelProcessNavigation.Visible = false;
            if (_navigationFilterAttached) { Application.RemoveMessageFilter(this); _navigationFilterAttached = false; }
        }

        /// <summary>仅从已保存快照构建树，不为折叠列表创建内部节点窗体。</summary>
        public void RefreshProcessNavigation()
        {
            _buildingNavigation = true;
            treeProcessNavigation.BeginUpdate();
            try
            {
                treeProcessNavigation.Nodes.Clear();
                foreach (TabPage page in tabControl1.TabPages)
                {
                    var editor = page.Controls.OfType<ProcessEditPanel>().FirstOrDefault();
                    if (editor == null) continue;
                    editor.FlushCompositeEdits();
                    string key = editor.RootProcess.ID.ToString();
                    var root = new TreeNode(editor.RootProcess.ID + ". " + editor.RootProcess.ProcessName)
                    { Name = key, Tag = new NavigationTarget(page, editor, new int[0]) };
                    foreach (var module in editor.RootProcess.Nodes.OfType<NodeCompositeModule>())
                        AddCompositeNavigation(root, page, editor, new[] { module.ID }, module.NodeName, module.ParamForm.Params as NodeParamCompositeModule, 0);
                    treeProcessNavigation.Nodes.Add(root);
                    RestoreNavigationExpansion(root);
                }
            }
            finally { treeProcessNavigation.EndUpdate(); _buildingNavigation = false; }
        }

        /// <summary>递归添加组合层级，深度限制防止异常旧方案造成栈溢出。</summary>
        private void AddCompositeNavigation(TreeNode parent, TabPage page, ProcessEditPanel editor,
            int[] path, string name, NodeParamCompositeModule param, int depth)
        {
            var item = new TreeNode(path.Last() + ". " + name)
            { Name = parent.Name + "/" + path.Last(), Tag = new NavigationTarget(page, editor, path) };
            parent.Nodes.Add(item);
            if (param?.NodeInfos == null || depth >= 63) return;
            foreach (var child in param.NodeInfos.Where(n => n != null && n.NodeType == NodeType.CompositeModule))
                AddCompositeNavigation(item, page, editor, path.Concat(new[] { child.ID }).ToArray(), child.NodeName,
                    child.NodeParam as NodeParamCompositeModule, depth + 1);
        }

        /// <summary>恢复每一级已记住的折叠状态。</summary>
        private void RestoreNavigationExpansion(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes) RestoreNavigationExpansion(child);
            if (!_collapsedNavigationPaths.Contains(node.Name)) node.Expand();
        }

        /// <summary>箭头只修改展开状态，不触发跳转，也不关闭列表。</summary>
        private void NavigationExpansionChanged(object sender, TreeViewEventArgs e)
        {
            if (_buildingNavigation) return;
            if (e.Node.IsExpanded) _collapsedNavigationPaths.Remove(e.Node.Name);
            else _collapsedNavigationPaths.Add(e.Node.Name);
        }

        /// <summary>点击名称进入对应画布；点击加减号留在弹层中。</summary>
        private void treeProcessNavigation_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = treeProcessNavigation.HitTest(e.Location);
            if ((hit.Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.Image)) == 0) return;
            ActivateNavigationTarget(e.Node);
        }

        /// <summary>键盘上下选择，左右折叠，回车进入，退出键关闭。</summary>
        private void treeProcessNavigation_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { ActivateNavigationTarget(treeProcessNavigation.SelectedNode); e.Handled = true; }
        }

        /// <summary>用流程对象与编号路径跳转，支持重名与带句点名称。</summary>
        private void ActivateNavigationTarget(TreeNode node)
        {
            var target = node?.Tag as NavigationTarget;
            if (target == null) return;
            if (!target.Editor.NavigateToComposite(target.Path)) return;
            tabControl1.SelectedTab = target.Page;
            CloseProcessNavigation();
        }

        /// <summary>点击弹层外或按退出键关闭，不吞掉正常控件操作。</summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (!panelProcessNavigation.Visible) return false;
            if (m.Msg == 0x100 && (Keys)m.WParam.ToInt32() == Keys.Escape)
            { CloseProcessNavigation(); return true; }
            if (m.Msg == 0x201 || m.Msg == 0x204 || m.Msg == 0x207)
            {
                var control = Control.FromHandle(m.HWnd);
                if (control != buttonProcessNavigation && control != panelProcessNavigation &&
                    (control == null || !panelProcessNavigation.Contains(control))) CloseProcessNavigation();
            }
            return false;
        }

        /// <summary>一个导航项的稳定目标，避免依赖显示名称查找。</summary>
        private sealed class NavigationTarget
        {
            /// <summary>目标顶层选项卡。</summary>
            public TabPage Page { get; private set; }
            /// <summary>承载目标画布的编辑器。</summary>
            public ProcessEditPanel Editor { get; private set; }
            /// <summary>从顶层流程到目标组合的编号路径。</summary>
            public int[] Path { get; private set; }
            /// <summary>构建不可变导航目标。</summary>
            public NavigationTarget(TabPage page, ProcessEditPanel editor, int[] path)
            { Page = page; Editor = editor; Path = path; }
        }
    }
}
