using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输出参数窗体，用于把内部节点结果映射为外部可订阅输出变量。
    /// </summary>
    public partial class NodeParamFormCompositeOutput : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 输出端口绑定列表。
        /// </summary>
        private BindingList<OutputPortViewModel> _ports;

        /// <summary>
        /// 创建组合输出参数窗体。
        /// </summary>
        public NodeParamFormCompositeOutput()
        {
            InitializeComponent();
            _ports = new BindingList<OutputPortViewModel>();
            ConfigureGrid();
            BindValueTypeColumn();
            dataGridViewPorts.DataSource = _ports;
            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            NodeBase.RefreshNodeSubControl += NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
        }

        /// <summary>
        /// 节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗体所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            _node = node;
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged += Process_ConnectionsChanged;

            RefreshSourceTree();
        }

        /// <summary>
        /// 将反序列化参数回写到界面。
        /// </summary>
        public void SetParam2Form()
        {
            NodeParamCompositeOutput param = Params as NodeParamCompositeOutput;
            _ports = new BindingList<OutputPortViewModel>();
            if (param != null && param.Ports != null)
            {
                for (int index = 0; index < param.Ports.Count; index++)
                    _ports.Add(OutputPortViewModel.FromPort(param.Ports[index], index));
            }

            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            dataGridViewPorts.DataSource = _ports;
            RefreshSourceTree();
        }

        /// <summary>
        /// 窗体关闭时解除事件订阅。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            NodeBase.RefreshNodeSubControl -= NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 配置端口表格。
        /// </summary>
        private void ConfigureGrid()
        {
            dataGridViewPorts.AutoGenerateColumns = false;
            dataGridViewPorts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewPorts.RowTemplate.Height = 30;
            ColumnName.DataPropertyName = "Name";
            ColumnValueType.DataPropertyName = "ValueType";
            ColumnSource.DataPropertyName = "SourceSummary";
            ColumnNote.DataPropertyName = "Note";
        }

        /// <summary>
        /// 绑定端口类型下拉选项。
        /// </summary>
        private void BindValueTypeColumn()
        {
            ColumnValueType.DataSource = GetValueTypeOptions();
            ColumnValueType.DisplayMember = "Text";
            ColumnValueType.ValueMember = "Value";
        }

        /// <summary>
        /// 新增输出端口。
        /// </summary>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            _ports.Add(CreateDefaultPort(_ports.Count));
            SelectRow(_ports.Count - 1);
        }

        /// <summary>
        /// 删除输出端口。
        /// </summary>
        private void buttonRemove_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            if (index < 0)
                return;

            _ports.RemoveAt(index);
            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            SelectRow(Math.Min(index, _ports.Count - 1));
        }

        /// <summary>
        /// 上移输出端口。
        /// </summary>
        private void buttonMoveUp_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(-1);
        }

        /// <summary>
        /// 下移输出端口。
        /// </summary>
        private void buttonMoveDown_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(1);
        }

        /// <summary>
        /// 应用当前选择的内部结果属性。
        /// </summary>
        private void buttonSelectProperty_Click(object sender, EventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        /// <summary>
        /// 双击属性时直接应用到当前输出端口。
        /// </summary>
        private void treeViewSources_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        /// <summary>
        /// 保存输出端口配置。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                Params = BuildParamFromForm();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 取消编辑。
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            SetParam2Form();
            Hide();
        }

        /// <summary>
        /// 表格数据错误处理。
        /// </summary>
        private void dataGridViewPorts_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        /// <summary>
        /// 流程连线变化后刷新内部来源树。
        /// </summary>
        private void Process_ConnectionsChanged(object sender, EventArgs e)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 节点重命名后刷新来源树并同步显示文本。
        /// </summary>
        private void NodeBase_RefreshNodeSubControl(object sender, RenameResult e)
        {
            if (_node == null || _node.Process == null)
                return;

            RefreshSourceTree();
            foreach (OutputPortViewModel row in _ports)
            {
                if (row.SourceNodeId == e.NodeId)
                    row.SourceNodeText = e.NodeId + "." + e.NodeNameNew;
            }

            _ports.ResetBindings();
        }

        /// <summary>
        /// 节点删除后刷新来源树。
        /// </summary>
        private void NodeBase_NodeDeletedEvent(object sender, NodeBase deletedNode)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 应用当前选择的结果属性到当前输出端口。
        /// </summary>
        private void ApplySelectedPropertyToCurrentRow()
        {
            ResultPropertyOption option = treeViewSources.SelectedNode == null
                ? null
                : treeViewSources.SelectedNode.Tag as ResultPropertyOption;
            if (option == null)
                return;

            int index = EnsureCurrentRow();
            if (index < 0)
                return;

            OutputPortViewModel row = _ports[index];
            row.SourceNodeId = option.SourceNodeId;
            row.SourceNodeText = option.SourceNodeText;
            row.PropertyPath = option.PropertyPath;
            row.PropertyDisplayName = option.PropertyDisplayName;
            row.ValueTypeName = option.ValueType == null ? string.Empty : option.ValueType.FullName;
            if (row.ValueType == CompositePortValueType.Any)
                row.ValueType = CompositePortValueHelper.InferValueType(option.ValueType);

            _ports.ResetItem(index);
        }

        /// <summary>
        /// 刷新内部来源结果树。
        /// </summary>
        private void RefreshSourceTree()
        {
            treeViewSources.BeginUpdate();
            treeViewSources.Nodes.Clear();
            treeViewSources.Nodes.Add(new TreeNode("无"));

            if (_node != null && _node.Process != null)
            {
                List<NodeBase> upstreamNodes = _node.Process.GetUpstreamNodes(_node);
                foreach (NodeBase sourceNode in upstreamNodes)
                {
                    TreeNode sourceTreeNode = new TreeNode(CompositePortValueHelper.GetNodeText(sourceNode));
                    treeViewSources.Nodes.Add(sourceTreeNode);
                    AddDynamicVariableNodes(sourceTreeNode, sourceNode);
                    if (sourceNode.Result != null)
                        AddMemberNodes(sourceTreeNode, sourceNode, sourceNode.Result.GetType(), string.Empty, string.Empty, 0);
                }
            }

            treeViewSources.CollapseAll();
            treeViewSources.EndUpdate();
        }

        /// <summary>
        /// 添加动态变量选项。
        /// </summary>
        /// <param name="sourceTreeNode">来源节点树节点。</param>
        /// <param name="sourceNode">来源节点。</param>
        private void AddDynamicVariableNodes(TreeNode sourceTreeNode, NodeBase sourceNode)
        {
            List<string> names = DynamicResultVariableResolver.GetVariableNames(sourceNode);
            if (names.Count == 0)
                return;

            TreeNode variableGroup = new TreeNode("输出变量");
            sourceTreeNode.Nodes.Add(variableGroup);
            foreach (string name in names)
            {
                Type valueType = DynamicResultVariableResolver.GetVariableValueType(sourceNode, name);
                TreeNode variableNode = new TreeNode(name);
                variableNode.Tag = new ResultPropertyOption
                {
                    SourceNodeId = sourceNode.ID,
                    SourceNodeText = CompositePortValueHelper.GetNodeText(sourceNode),
                    PropertyPath = DynamicResultVariableResolver.ToPropertyPath(name),
                    PropertyDisplayName = name,
                    ValueType = valueType
                };
                variableGroup.Nodes.Add(variableNode);
            }
        }

        /// <summary>
        /// 递归添加普通结果属性选项。
        /// </summary>
        private void AddMemberNodes(
            TreeNode parentNode,
            NodeBase sourceNode,
            Type ownerType,
            string pathPrefix,
            string displayPrefix,
            int depth)
        {
            foreach (MemberInfo member in CompositePortValueHelper.GetReadableMembers(ownerType))
            {
                Type memberType = CompositePortValueHelper.GetMemberType(member);
                string displayName = CompositePortValueHelper.GetDisplayName(member);
                string propertyPath = string.IsNullOrEmpty(pathPrefix) ? member.Name : pathPrefix + "." + member.Name;
                string propertyDisplay = string.IsNullOrEmpty(displayPrefix) ? displayName : displayPrefix + "." + displayName;

                if (CompositePortValueHelper.IsSelectableMemberType(memberType))
                {
                    TreeNode propertyNode = new TreeNode(displayName);
                    propertyNode.Tag = new ResultPropertyOption
                    {
                        SourceNodeId = sourceNode.ID,
                        SourceNodeText = CompositePortValueHelper.GetNodeText(sourceNode),
                        PropertyPath = propertyPath,
                        PropertyDisplayName = propertyDisplay,
                        ValueType = memberType
                    };
                    parentNode.Nodes.Add(propertyNode);
                    continue;
                }

                if (depth >= 2 || !CompositePortValueHelper.CanInspectMemberType(memberType))
                    continue;

                TreeNode groupNode = new TreeNode(displayName);
                parentNode.Nodes.Add(groupNode);
                AddMemberNodes(groupNode, sourceNode, memberType, propertyPath, propertyDisplay, depth + 1);
                if (groupNode.Nodes.Count == 0)
                    parentNode.Nodes.Remove(groupNode);
            }
        }

        /// <summary>
        /// 从界面构建输出参数。
        /// </summary>
        /// <returns>组合输出参数。</returns>
        private NodeParamCompositeOutput BuildParamFromForm()
        {
            EndGridEdit();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamCompositeOutput param = new NodeParamCompositeOutput();
            for (int index = 0; index < _ports.Count; index++)
            {
                OutputPortViewModel row = _ports[index];
                if (row == null)
                    continue;

                string name = (row.Name ?? string.Empty).Trim();
                ValidatePortName(name, names, index);
                if (row.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(row.PropertyPath))
                    throw new Exception("第" + (index + 1) + "行输出端口未选择内部结果来源。");

                param.Ports.Add(row.ToPort());
            }

            if (param.Ports.Count == 0)
                throw new Exception("至少需要配置一个输出端口。");

            return param;
        }

        /// <summary>
        /// 校验端口名称。
        /// </summary>
        private static void ValidatePortName(string name, HashSet<string> names, int index)
        {
            string message;
            if (!DynamicResultVariableResolver.IsValidVariableName(name, out message))
                throw new Exception("第" + (index + 1) + "行输出端口名无效：" + message);

            if (!names.Add(name))
                throw new Exception("第" + (index + 1) + "行输出端口名“" + name + "”重复。");
        }

        /// <summary>
        /// 创建默认端口。
        /// </summary>
        private static OutputPortViewModel CreateDefaultPort(int index)
        {
            return new OutputPortViewModel
            {
                Name = "输出" + (index + 1),
                ValueType = CompositePortValueType.Any,
                Note = string.Empty
            };
        }

        /// <summary>
        /// 获取端口类型选项。
        /// </summary>
        private static List<ValueTypeOption> GetValueTypeOptions()
        {
            return new List<ValueTypeOption>
            {
                new ValueTypeOption("自动", CompositePortValueType.Any),
                new ValueTypeOption("数值", CompositePortValueType.Number),
                new ValueTypeOption("布尔", CompositePortValueType.Boolean),
                new ValueTypeOption("文本", CompositePortValueType.String),
                new ValueTypeOption("图像", CompositePortValueType.Image)
            };
        }

        /// <summary>
        /// 结束表格编辑。
        /// </summary>
        private void EndGridEdit()
        {
            dataGridViewPorts.EndEdit();
            BindingContext[dataGridViewPorts.DataSource]?.EndCurrentEdit();
        }

        /// <summary>
        /// 获取当前行索引。
        /// </summary>
        private int GetCurrentRowIndex()
        {
            return dataGridViewPorts.CurrentRow == null ? -1 : dataGridViewPorts.CurrentRow.Index;
        }

        /// <summary>
        /// 确保有当前行。
        /// </summary>
        private int EnsureCurrentRow()
        {
            int index = GetCurrentRowIndex();
            if (index >= 0)
                return index;

            if (_ports.Count == 0)
                _ports.Add(CreateDefaultPort(0));

            SelectRow(0);
            return 0;
        }

        /// <summary>
        /// 选择指定行。
        /// </summary>
        private void SelectRow(int index)
        {
            if (index < 0 || index >= dataGridViewPorts.Rows.Count)
                return;

            dataGridViewPorts.ClearSelection();
            dataGridViewPorts.Rows[index].Selected = true;
            dataGridViewPorts.CurrentCell = dataGridViewPorts.Rows[index].Cells[0];
        }

        /// <summary>
        /// 移动当前行。
        /// </summary>
        private void MoveCurrentRow(int direction)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= _ports.Count)
                return;

            OutputPortViewModel row = _ports[index];
            _ports.RemoveAt(index);
            _ports.Insert(targetIndex, row);
            SelectRow(targetIndex);
        }

        /// <summary>
        /// 端口类型选项。
        /// </summary>
        private sealed class ValueTypeOption
        {
            /// <summary>
            /// 创建端口类型选项。
            /// </summary>
            public ValueTypeOption(string text, CompositePortValueType value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 显示文本。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 端口类型。
            /// </summary>
            public CompositePortValueType Value { get; private set; }
        }

        /// <summary>
        /// 结果属性选择项。
        /// </summary>
        private sealed class ResultPropertyOption
        {
            public int SourceNodeId { get; set; }

            public string SourceNodeText { get; set; }

            public string PropertyPath { get; set; }

            public string PropertyDisplayName { get; set; }

            public Type ValueType { get; set; }
        }

        /// <summary>
        /// 输出端口表格视图模型。
        /// </summary>
        private sealed class OutputPortViewModel
        {
            public string Name { get; set; }

            public CompositePortValueType ValueType { get; set; }

            public string SourceNodeText { get; set; }

            public string PropertyDisplayName { get; set; }

            public string Note { get; set; }

            public int SourceNodeId { get; set; }

            public string PropertyPath { get; set; }

            public string ValueTypeName { get; set; }

            public string SourceSummary
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(SourceNodeText))
                        return string.Empty;

                    return SourceNodeText + "." + (PropertyDisplayName ?? PropertyPath ?? string.Empty);
                }
            }

            public static OutputPortViewModel FromPort(CompositeOutputPortDefinition port, int index)
            {
                if (port == null)
                    return CreateDefaultPort(index);

                return new OutputPortViewModel
                {
                    Name = string.IsNullOrWhiteSpace(port.Name) ? "输出" + (index + 1) : port.Name,
                    ValueType = port.ValueType,
                    SourceNodeId = port.SourceNodeId,
                    SourceNodeText = port.SourceNodeText,
                    PropertyPath = port.PropertyPath,
                    PropertyDisplayName = port.PropertyDisplayName,
                    ValueTypeName = port.ValueTypeName,
                    Note = port.Note
                };
            }

            public CompositeOutputPortDefinition ToPort()
            {
                return new CompositeOutputPortDefinition
                {
                    Name = (Name ?? string.Empty).Trim(),
                    SourceNodeId = SourceNodeId,
                    SourceNodeText = SourceNodeText,
                    PropertyPath = PropertyPath,
                    PropertyDisplayName = PropertyDisplayName,
                    ValueTypeName = ValueTypeName,
                    ValueType = ValueType,
                    Note = Note
                };
            }
        }
    }
}
