using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public partial class NodeParamFormMultiCondition : FormBase, INodeParamForm
    {
        private NodeBase _node;
        private BindingList<ConditionRowViewModel> _conditionRows;

        public NodeParamFormMultiCondition()
        {
            InitializeComponent();
            _conditionRows = new BindingList<ConditionRowViewModel>();
            EnsureConditionGridColumns();
            InitializeOperatorColumn();
            comboBoxMatchMode.SelectedIndex = 0;
            dataGridViewConditions.DataSource = _conditionRows;
            NodeBase.RefreshNodeSubControl += NodeBase_RefreshNodeSubControl;
            NodeBase.OutputDefinitionChanged += NodeBase_OutputDefinitionChanged;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            _node = node;
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged += Process_ConnectionsChanged;

            RefreshSourceTree();
        }

        public void SetParam2Form()
        {
            NodeParamMultiCondition param = Params as NodeParamMultiCondition;
            if (param == null)
                param = new NodeParamMultiCondition();

            SetMatchMode(param.MatchMode);
            _conditionRows = new BindingList<ConditionRowViewModel>();
            if (param.Conditions != null)
            {
                for (int index = 0; index < param.Conditions.Count; index++)
                    _conditionRows.Add(ConditionRowViewModel.FromCondition(param.Conditions[index], index));
            }

            dataGridViewConditions.DataSource = _conditionRows;
            RefreshSourceTree();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            NodeBase.RefreshNodeSubControl -= NodeBase_RefreshNodeSubControl;
            NodeBase.OutputDefinitionChanged -= NodeBase_OutputDefinitionChanged;
            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 用户点击标题栏关闭时隐藏参数窗体，保留连线刷新事件订阅，避免再次打开时使用旧的上游列表。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }

        /// <summary>
        /// 每次显示窗体时主动重算上游节点树，兜底处理隐藏期间新增或删除连线的场景。
        /// </summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible)
                RefreshSourceTree();
        }

        private void InitializeOperatorColumn()
        {
            EnsureConditionGridColumns();
            ColumnOperator.DataSource = GetOperatorOptions();
            ColumnOperator.DisplayMember = "Text";
            ColumnOperator.ValueMember = "Value";
        }

        private void EnsureConditionGridColumns()
        {
            dataGridViewConditions.AutoGenerateColumns = false;

            if (ColumnName == null)
                ColumnName = new DataGridViewTextBoxColumn();
            if (ColumnSourceNode == null)
                ColumnSourceNode = new DataGridViewTextBoxColumn();
            if (ColumnProperty == null)
                ColumnProperty = new DataGridViewTextBoxColumn();
            if (ColumnOperator == null)
                ColumnOperator = new DataGridViewComboBoxColumn();
            if (ColumnValue1 == null)
                ColumnValue1 = new DataGridViewTextBoxColumn();
            if (ColumnValue2 == null)
                ColumnValue2 = new DataGridViewTextBoxColumn();
            if (ColumnRunParam == null)
                ColumnRunParam = new DataGridViewCheckBoxColumn();
            if (ColumnNote == null)
                ColumnNote = new DataGridViewTextBoxColumn();

            ConfigureConditionGridColumns();

            if (dataGridViewConditions.Columns.Count == 0)
            {
                dataGridViewConditions.Columns.AddRange(new DataGridViewColumn[]
                {
                    ColumnName,
                    ColumnSourceNode,
                    ColumnProperty,
                    ColumnOperator,
                    ColumnValue1,
                    ColumnValue2,
                    ColumnRunParam,
                    ColumnNote
                });
            }
        }

        private void ConfigureConditionGridColumns()
        {
            dataGridViewConditions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewConditions.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridViewConditions.RowTemplate.Height = 32;
            dataGridViewConditions.ColumnHeadersHeight = 32;

            ColumnName.DataPropertyName = "Name";
            ColumnName.HeaderText = "名称";
            ColumnName.MinimumWidth = 80;
            ColumnName.Name = "ColumnName";
            ColumnName.FillWeight = 90F;

            ColumnSourceNode.DataPropertyName = "SourceNodeText";
            ColumnSourceNode.HeaderText = "节点";
            ColumnSourceNode.MinimumWidth = 150;
            ColumnSourceNode.Name = "ColumnSourceNode";
            ColumnSourceNode.ReadOnly = true;
            ColumnSourceNode.FillWeight = 165F;

            ColumnProperty.DataPropertyName = "PropertyDisplayName";
            ColumnProperty.HeaderText = "属性";
            ColumnProperty.MinimumWidth = 190;
            ColumnProperty.Name = "ColumnProperty";
            ColumnProperty.ReadOnly = true;
            ColumnProperty.FillWeight = 230F;

            ColumnOperator.DataPropertyName = "Operator";
            ColumnOperator.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            ColumnOperator.HeaderText = "操作";
            ColumnOperator.MinimumWidth = 145;
            ColumnOperator.Name = "ColumnOperator";
            ColumnOperator.Resizable = DataGridViewTriState.True;
            ColumnOperator.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnOperator.FillWeight = 155F;

            ColumnValue1.DataPropertyName = "Value1";
            ColumnValue1.HeaderText = "值1";
            ColumnValue1.MinimumWidth = 80;
            ColumnValue1.Name = "ColumnValue1";
            ColumnValue1.FillWeight = 95F;

            ColumnValue2.DataPropertyName = "Value2";
            ColumnValue2.HeaderText = "值2";
            ColumnValue2.MinimumWidth = 80;
            ColumnValue2.Name = "ColumnValue2";
            ColumnValue2.FillWeight = 95F;

            ColumnRunParam.DataPropertyName = "EnableRunParamAdjust";
            ColumnRunParam.HeaderText = "运行参数";
            ColumnRunParam.MinimumWidth = 75;
            ColumnRunParam.Name = "ColumnRunParam";
            ColumnRunParam.FillWeight = 85F;

            ColumnNote.DataPropertyName = "Note";
            ColumnNote.HeaderText = "注释";
            ColumnNote.MinimumWidth = 130;
            ColumnNote.Name = "ColumnNote";
            ColumnNote.FillWeight = 150F;
        }

        private static List<OperatorOption> GetOperatorOptions()
        {
            return new List<OperatorOption>
            {
                new OperatorOption("等于", MultiConditionOperator.Equals),
                new OperatorOption("不等于", MultiConditionOperator.NotEquals),
                new OperatorOption("大于", MultiConditionOperator.GreaterThan),
                new OperatorOption("大于等于", MultiConditionOperator.GreaterThanOrEqual),
                new OperatorOption("小于", MultiConditionOperator.LessThan),
                new OperatorOption("小于等于", MultiConditionOperator.LessThanOrEqual),
                new OperatorOption("文本包含", MultiConditionOperator.Contains),
                new OperatorOption("文本不包含", MultiConditionOperator.NotContains),
                new OperatorOption("为True/OK", MultiConditionOperator.IsTrue),
                new OperatorOption("为False/NG", MultiConditionOperator.IsFalse),
                new OperatorOption("在范围内", MultiConditionOperator.Between),
                new OperatorOption("不在范围内", MultiConditionOperator.NotBetween),
                new OperatorOption("为空", MultiConditionOperator.IsNull),
                new OperatorOption("不为空", MultiConditionOperator.IsNotNull)
            };
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            ConditionRowViewModel row = new ConditionRowViewModel
            {
                Name = "条件" + (_conditionRows.Count + 1),
                Operator = MultiConditionOperator.Equals
            };
            _conditionRows.Add(row);
            SelectRow(_conditionRows.Count - 1);
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            int index = GetCurrentRowIndex();
            if (index < 0)
                return;

            _conditionRows.RemoveAt(index);
            if (_conditionRows.Count > 0)
                SelectRow(Math.Min(index, _conditionRows.Count - 1));
        }

        private void buttonSelectProperty_Click(object sender, EventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        private void treeViewSources_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                Params = BuildParamFromForm();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常原因：{ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Hide();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            SetParam2Form();
            Hide();
        }

        private NodeParamMultiCondition BuildParamFromForm()
        {
            dataGridViewConditions.EndEdit();
            if (_conditionRows.Count == 0)
                throw new Exception("至少需要添加一个条件！");

            NodeParamMultiCondition param = new NodeParamMultiCondition
            {
                MatchMode = GetSelectedMatchMode(),
                Conditions = new List<MultiConditionItem>()
            };

            for (int index = 0; index < _conditionRows.Count; index++)
            {
                MultiConditionItem condition = _conditionRows[index].ToCondition(index);
                if (condition.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(condition.PropertyPath))
                    throw new Exception($"第{index + 1}行没有选择上游节点结果属性！");

                param.Conditions.Add(condition);
            }

            return param;
        }

        private void ApplySelectedPropertyToCurrentRow()
        {
            ResultPropertyOption option = treeViewSources.SelectedNode == null
                ? null
                : treeViewSources.SelectedNode.Tag as ResultPropertyOption;
            if (option == null)
                return;

            int index = GetCurrentRowIndex();
            if (index < 0)
            {
                buttonAdd_Click(this, EventArgs.Empty);
                index = GetCurrentRowIndex();
            }

            if (index < 0)
                return;

            ConditionRowViewModel row = _conditionRows[index];
            row.SourceNodeId = option.SourceNodeId;
            row.SourceNodeText = option.SourceNodeText;
            row.PropertyPath = option.PropertyPath;
            row.PropertyDisplayName = option.PropertyDisplayName;
            row.ValueTypeName = option.ValueType == null ? string.Empty : option.ValueType.FullName;

            if (option.ValueType == typeof(bool) ||
                Nullable.GetUnderlyingType(option.ValueType) == typeof(bool))
            {
                row.Operator = MultiConditionOperator.IsTrue;
            }

            _conditionRows.ResetItem(index);
        }

        private int GetCurrentRowIndex()
        {
            if (dataGridViewConditions.CurrentRow == null)
                return -1;

            return dataGridViewConditions.CurrentRow.Index;
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= dataGridViewConditions.Rows.Count)
                return;

            dataGridViewConditions.ClearSelection();
            dataGridViewConditions.Rows[index].Selected = true;
            dataGridViewConditions.CurrentCell = dataGridViewConditions.Rows[index].Cells[0];
        }

        private MultiConditionMatchMode GetSelectedMatchMode()
        {
            return comboBoxMatchMode.SelectedIndex == 1
                ? MultiConditionMatchMode.Any
                : MultiConditionMatchMode.All;
        }

        private void SetMatchMode(MultiConditionMatchMode matchMode)
        {
            comboBoxMatchMode.SelectedIndex = matchMode == MultiConditionMatchMode.Any ? 1 : 0;
        }

        private void Process_ConnectionsChanged(object sender, EventArgs e)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 同一流程的节点输出定义变化后刷新可选条件来源。
        /// </summary>
        private void NodeBase_OutputDefinitionChanged(object sender, NodeBase sourceNode)
        {
            if (sourceNode == null || _node == null || sourceNode.Process != _node.Process)
                return;

            RefreshSourceTree();
        }

        private void NodeBase_RefreshNodeSubControl(object sender, RenameResult e)
        {
            if (_node == null || _node.Process == null)
                return;

            RefreshSourceTree();
            foreach (ConditionRowViewModel row in _conditionRows)
            {
                if (row.SourceNodeId == e.NodeId)
                    row.SourceNodeText = e.NodeId + "." + e.NodeNameNew;
            }

            _conditionRows.ResetBindings();
        }

        private void NodeBase_NodeDeletedEvent(object sender, NodeBase deletedNode)
        {
            if (deletedNode == null)
                return;

            RefreshSourceTree();
        }

        private void RefreshSourceTree()
        {
            treeViewSources.BeginUpdate();
            treeViewSources.Nodes.Clear();

            SubscriptionInputContract inputContract = new SubscriptionInputContract(
                new[]
                {
                    SubscriptionDataCategory.Boolean,
                    SubscriptionDataCategory.Number,
                    SubscriptionDataCategory.Text
                },
                null,
                new[] { SubscriptionValueMultiplicity.Single },
                NumericConversionMode.SafeWidening);

            TreeNode noneNode = new TreeNode("无");
            treeViewSources.Nodes.Add(noneNode);

            if (_node != null && _node.Process != null)
            {
                List<NodeBase> upstreamNodes = _node.Process.GetUpstreamNodes(_node);
                foreach (NodeBase sourceNode in upstreamNodes)
                {
                    TreeNode sourceTreeNode = new TreeNode(GetNodeText(sourceNode));
                    treeViewSources.Nodes.Add(sourceTreeNode);
                    AddCatalogOutputNodes(sourceTreeNode, sourceNode, inputContract);
                    if (sourceTreeNode.Nodes.Count == 0)
                        treeViewSources.Nodes.Remove(sourceTreeNode);
                }
            }

            treeViewSources.CollapseAll();
            treeViewSources.EndUpdate();
        }

        /// <summary>
        /// 添加统一目录中与多条件基础值输入兼容的输出节点。
        /// </summary>
        /// <param name="sourceTreeNode">来源节点树项。</param>
        /// <param name="sourceNode">来源节点。</param>
        /// <param name="inputContract">布尔、数值和文本输入契约。</param>
        private static void AddCatalogOutputNodes(
            TreeNode sourceTreeNode,
            NodeBase sourceNode,
            SubscriptionInputContract inputContract)
        {
            IReadOnlyList<SubscriptionOutputDescriptor> outputs = SubscriptionPortCatalog.GetOutputs(
                sourceNode,
                inputContract,
                false,
                string.Empty);
            foreach (SubscriptionOutputDescriptor output in outputs)
            {
                TreeNode outputNode = new TreeNode(output.DisplayName);
                outputNode.Tag = new ResultPropertyOption
                {
                    SourceNodeId = sourceNode.ID,
                    SourceNodeText = GetNodeText(sourceNode),
                    PropertyPath = output.PropertyPath,
                    PropertyDisplayName = output.DisplayName,
                    ValueType = output.ValueType
                };
                sourceTreeNode.Nodes.Add(outputNode);
            }
        }

        private static string GetNodeText(NodeBase node)
        {
            return node.ID + "." + node.NodeName;
        }

        private sealed class OperatorOption
        {
            public OperatorOption(string text, MultiConditionOperator value)
            {
                Text = text;
                Value = value;
            }

            public string Text { get; private set; }

            public MultiConditionOperator Value { get; private set; }
        }

        private sealed class ResultPropertyOption
        {
            public int SourceNodeId { get; set; }

            public string SourceNodeText { get; set; }

            public string PropertyPath { get; set; }

            public string PropertyDisplayName { get; set; }

            public Type ValueType { get; set; }
        }

        private sealed class ConditionRowViewModel
        {
            /// <summary>
            /// 当前条件是否参与检测。该状态与是否加入手动调参表相互独立。
            /// </summary>
            public bool Enabled { get; set; } = true;

            public string Name { get; set; }

            public string SourceNodeText { get; set; }

            public string PropertyDisplayName { get; set; }

            public MultiConditionOperator Operator { get; set; }

            public string Value1 { get; set; }

            public string Value2 { get; set; }

            /// <summary>
            /// 是否把当前条件加入单图运行参数表。
            /// </summary>
            public bool EnableRunParamAdjust { get; set; }

            public string Note { get; set; }

            public int SourceNodeId { get; set; }

            public string PropertyPath { get; set; }

            public string ValueTypeName { get; set; }

            public static ConditionRowViewModel FromCondition(MultiConditionItem condition, int index)
            {
                if (condition == null)
                    condition = new MultiConditionItem();

                return new ConditionRowViewModel
                {
                    Enabled = condition.Enabled,
                    Name = string.IsNullOrWhiteSpace(condition.Name) ? "条件" + (index + 1) : condition.Name,
                    SourceNodeText = condition.SourceNodeText,
                    PropertyDisplayName = condition.PropertyDisplayName,
                    Operator = NormalizeLoadedOperator(condition),
                    Value1 = condition.Value1,
                    Value2 = condition.Value2,
                    EnableRunParamAdjust = condition.EnableRunParamAdjust,
                    Note = condition.Note,
                    SourceNodeId = condition.SourceNodeId,
                    PropertyPath = condition.PropertyPath,
                    ValueTypeName = condition.ValueTypeName
                };
            }

            /// <summary>
            /// 兼容早期枚举值或误选操作，数值条件同时带值2时按范围条件显示。
            /// </summary>
            private static MultiConditionOperator NormalizeLoadedOperator(MultiConditionItem condition)
            {
                if (condition == null)
                    return MultiConditionOperator.Equals;

                if ((condition.Operator != MultiConditionOperator.Contains &&
                     condition.Operator != MultiConditionOperator.NotContains) ||
                    string.IsNullOrWhiteSpace(condition.Value2) ||
                    !IsNumericValueTypeName(condition.ValueTypeName))
                {
                    return condition.Operator;
                }

                return condition.Operator == MultiConditionOperator.Contains
                    ? MultiConditionOperator.Between
                    : MultiConditionOperator.NotBetween;
            }

            /// <summary>
            /// 判断保存的值类型名称是否为常见数值类型。
            /// </summary>
            private static bool IsNumericValueTypeName(string valueTypeName)
            {
                switch ((valueTypeName ?? string.Empty).Trim())
                {
                    case "System.Byte":
                    case "System.SByte":
                    case "System.Int16":
                    case "System.UInt16":
                    case "System.Int32":
                    case "System.UInt32":
                    case "System.Int64":
                    case "System.UInt64":
                    case "System.Single":
                    case "System.Double":
                    case "System.Decimal":
                        return true;
                    default:
                        return false;
                }
            }

            public MultiConditionItem ToCondition(int index)
            {
                return new MultiConditionItem
                {
                    Enabled = Enabled,
                    Name = string.IsNullOrWhiteSpace(Name) ? "条件" + (index + 1) : Name.Trim(),
                    Note = Note,
                    SourceNodeId = SourceNodeId,
                    SourceNodeText = SourceNodeText,
                    PropertyPath = PropertyPath,
                    PropertyDisplayName = PropertyDisplayName,
                    ValueTypeName = ValueTypeName,
                    Operator = Operator,
                    Value1 = Value1,
                    Value2 = Value2,
                    EnableRunParamAdjust = EnableRunParamAdjust
                };
            }
        }
    }
}
