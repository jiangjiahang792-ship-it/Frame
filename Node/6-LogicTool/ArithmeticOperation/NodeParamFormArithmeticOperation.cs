using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算参数配置窗体。
    /// </summary>
    public partial class NodeParamFormArithmeticOperation : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 运算行绑定集合。
        /// </summary>
        private BindingList<ArithmeticRowViewModel> _rows;

        /// <summary>
        /// 创建四则运算参数窗体。
        /// </summary>
        public NodeParamFormArithmeticOperation()
        {
            InitializeComponent();
            _rows = new BindingList<ArithmeticRowViewModel>();
            ConfigureGridColumns();
            BindComboColumns();
            numericUpDownDecimalPlaces.Value = 3;
            dataGridViewRows.DataSource = _rows;
            if (_rows.Count == 0)
                _rows.Add(CreateDefaultRow(0));

            NodeBase.RefreshNodeSubControl += NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
        }

        /// <summary>
        /// 当前节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 初始化订阅来源所属节点。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            _node = node;
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged += Process_ConnectionsChanged;

            RefreshSourceTree();
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 将反序列化参数写回界面。
        /// </summary>
        public void SetParam2Form()
        {
            NodeParamArithmeticOperation param = Params as NodeParamArithmeticOperation;
            if (param == null)
                param = new NodeParamArithmeticOperation();

            numericUpDownDecimalPlaces.Value = Math.Max(
                numericUpDownDecimalPlaces.Minimum,
                Math.Min(numericUpDownDecimalPlaces.Maximum, param.DecimalPlaces));

            _rows = new BindingList<ArithmeticRowViewModel>();
            if (param.Rows != null)
            {
                for (int index = 0; index < param.Rows.Count; index++)
                    _rows.Add(ArithmeticRowViewModel.FromRow(param.Rows[index]));
            }

            if (_rows.Count == 0)
                _rows.Add(CreateDefaultRow(0));

            dataGridViewRows.DataSource = _rows;
            RefreshSourceTree();
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 窗体关闭时解除事件订阅。
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            NodeBase.RefreshNodeSubControl -= NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 执行一次本地预览计算。
        /// </summary>
        internal ArithmeticOperationMeasureResult ExecuteMeasure(NodeParamArithmeticOperation param)
        {
            return ArithmeticOperationAlgorithm.Execute(_node, param);
        }

        /// <summary>
        /// 配置运算行表格。
        /// </summary>
        private void ConfigureGridColumns()
        {
            dataGridViewRows.AutoGenerateColumns = false;
            dataGridViewRows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewRows.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridViewRows.RowTemplate.Height = 32;
            dataGridViewRows.ColumnHeadersHeight = 32;

            ColumnEnabled.DataPropertyName = "Enabled";
            ColumnOperator.DataPropertyName = "Operator";
            ColumnSourceMode.DataPropertyName = "SourceMode";
            ColumnSourceSummary.DataPropertyName = "SourceSummary";
            ColumnConstantText.DataPropertyName = "ConstantText";
            ColumnVariableName.DataPropertyName = "VariableName";
            ColumnResultVariableName.DataPropertyName = "ResultVariableName";
            ColumnNote.DataPropertyName = "Note";
        }

        /// <summary>
        /// 绑定枚举列显示值。
        /// </summary>
        private void BindComboColumns()
        {
            ColumnOperator.DataSource = GetOperatorOptions();
            ColumnOperator.DisplayMember = "Text";
            ColumnOperator.ValueMember = "Value";

            ColumnSourceMode.DataSource = GetSourceModeOptions();
            ColumnSourceMode.DisplayMember = "Text";
            ColumnSourceMode.ValueMember = "Value";
        }

        /// <summary>
        /// 获取运算符下拉选项。
        /// </summary>
        private static List<ComboOption<ArithmeticOperator>> GetOperatorOptions()
        {
            return new List<ComboOption<ArithmeticOperator>>
            {
                new ComboOption<ArithmeticOperator>("加", ArithmeticOperator.Add),
                new ComboOption<ArithmeticOperator>("减", ArithmeticOperator.Subtract),
                new ComboOption<ArithmeticOperator>("乘", ArithmeticOperator.Multiply),
                new ComboOption<ArithmeticOperator>("除", ArithmeticOperator.Divide)
            };
        }

        /// <summary>
        /// 获取来源模式下拉选项。
        /// </summary>
        private static List<ComboOption<ArithmeticOperandSourceMode>> GetSourceModeOptions()
        {
            return new List<ComboOption<ArithmeticOperandSourceMode>>
            {
                new ComboOption<ArithmeticOperandSourceMode>("常量", ArithmeticOperandSourceMode.Constant),
                new ComboOption<ArithmeticOperandSourceMode>("订阅值", ArithmeticOperandSourceMode.Subscription),
                new ComboOption<ArithmeticOperandSourceMode>("内部变量", ArithmeticOperandSourceMode.Variable)
            };
        }

        /// <summary>
        /// 新增运算行。
        /// </summary>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            _rows.Add(CreateDefaultRow(_rows.Count));
            SelectRow(_rows.Count - 1);
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 删除当前运算行。
        /// </summary>
        private void buttonRemove_Click(object sender, EventArgs e)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            if (index < 0 || _rows.Count == 0)
                return;

            _rows.RemoveAt(index);
            if (_rows.Count == 0)
                _rows.Add(CreateDefaultRow(0));

            SelectRow(Math.Min(index, _rows.Count - 1));
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 上移当前运算行。
        /// </summary>
        private void buttonMoveUp_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(-1);
        }

        /// <summary>
        /// 下移当前运算行。
        /// </summary>
        private void buttonMoveDown_Click(object sender, EventArgs e)
        {
            MoveCurrentRow(1);
        }

        /// <summary>
        /// 选择上游属性给当前行。
        /// </summary>
        private void buttonSelectProperty_Click(object sender, EventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        /// <summary>
        /// 双击上游属性时直接应用。
        /// </summary>
        private void treeViewSources_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ApplySelectedPropertyToCurrentRow();
        }

        /// <summary>
        /// 双击内部变量时直接应用。
        /// </summary>
        private void listBoxVariables_DoubleClick(object sender, EventArgs e)
        {
            ApplySelectedVariableToCurrentRow();
        }

        /// <summary>
        /// 点击变量应用按钮。
        /// </summary>
        private void buttonUseVariable_Click(object sender, EventArgs e)
        {
            ApplySelectedVariableToCurrentRow();
        }

        /// <summary>
        /// 运行预览。
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                NodeParamArithmeticOperation param = BuildParamFromForm();
                ArithmeticOperationMeasureResult measureResult = ExecuteMeasure(param);
                NodeResultArithmeticOperation result = NodeArithmeticOperation.BuildResult(measureResult, param.DecimalPlaces);
                UpdateRuntimeStatus(result);
            }
            catch (Exception ex)
            {
                UpdateRuntimeStatus(NodeArithmeticOperation.BuildFailureResult(ex.Message));
            }
        }

        /// <summary>
        /// 保存参数并关闭窗体。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                Params = BuildParamFromForm();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常：{ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Hide();
        }

        /// <summary>
        /// 取消修改并恢复旧参数。
        /// </summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            SetParam2Form();
            Hide();
        }

        /// <summary>
        /// 表格选择变化时刷新可用内部变量。
        /// </summary>
        private void dataGridViewRows_SelectionChanged(object sender, EventArgs e)
        {
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 表格编辑结束后刷新摘要列。
        /// </summary>
        private void dataGridViewRows_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _rows.Count)
                _rows.ResetItem(e.RowIndex);

            RefreshInternalVariableList();
        }

        /// <summary>
        /// 忽略组合列数据错误，避免旧参数枚举值导致界面弹窗。
        /// </summary>
        private void dataGridViewRows_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        /// <summary>
        /// 流程连线变化后刷新上游属性树。
        /// </summary>
        private void Process_ConnectionsChanged(object sender, EventArgs e)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 节点重命名时同步订阅文本。
        /// </summary>
        private void NodeBase_RefreshNodeSubControl(object sender, RenameResult e)
        {
            if (_node == null || _node.Process == null)
                return;

            foreach (ArithmeticRowViewModel row in _rows)
            {
                if (row.SourceNodeId == e.NodeId)
                    row.SourceNodeText = e.NodeId + "." + e.NodeNameNew;
            }

            _rows.ResetBindings();
            RefreshSourceTree();
        }

        /// <summary>
        /// 节点删除后刷新上游属性树。
        /// </summary>
        private void NodeBase_NodeDeletedEvent(object sender, NodeBase deletedNode)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 从界面构建参数。
        /// </summary>
        private NodeParamArithmeticOperation BuildParamFromForm()
        {
            EndGridEdit();

            var param = new NodeParamArithmeticOperation
            {
                DecimalPlaces = (int)numericUpDownDecimalPlaces.Value,
                Rows = new List<ArithmeticOperationRow>()
            };

            foreach (ArithmeticRowViewModel viewModel in _rows)
                param.Rows.Add(viewModel.ToRow());

            ValidateParam(param);
            return param;
        }

        /// <summary>
        /// 校验参数完整性。
        /// </summary>
        private static void ValidateParam(NodeParamArithmeticOperation param)
        {
            int enabledCount = 0;
            var generatedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < param.Rows.Count; index++)
            {
                ArithmeticOperationRow row = param.Rows[index];
                if (row == null || !row.Enabled)
                    continue;

                enabledCount++;
                ValidateRowSource(row, generatedVariables, index);

                string resultVariableName = (row.ResultVariableName ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(resultVariableName))
                    generatedVariables.Add(resultVariableName);
            }

            if (enabledCount == 0)
                throw new Exception("至少需要启用一行运算。");
        }

        /// <summary>
        /// 校验单行来源设置。
        /// </summary>
        private static void ValidateRowSource(
            ArithmeticOperationRow row,
            HashSet<string> generatedVariables,
            int rowIndex)
        {
            switch (row.SourceMode)
            {
                case ArithmeticOperandSourceMode.Constant:
                    ArithmeticOperationAlgorithm.ConvertToDouble(row.ConstantText, $"第{rowIndex + 1}行常量");
                    break;
                case ArithmeticOperandSourceMode.Subscription:
                    if (row.SourceNodeId <= 0 && string.IsNullOrWhiteSpace(row.SourceNodeText))
                        throw new Exception($"第{rowIndex + 1}行没有选择订阅节点。");
                    if (string.IsNullOrWhiteSpace(row.PropertyPath))
                        throw new Exception($"第{rowIndex + 1}行没有选择订阅属性。");
                    break;
                case ArithmeticOperandSourceMode.Variable:
                    string variableName = (row.VariableName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(variableName))
                        throw new Exception($"第{rowIndex + 1}行没有选择内部变量。");
                    if (!generatedVariables.Contains(variableName))
                        throw new Exception($"第{rowIndex + 1}行引用的内部变量“{variableName}”尚未由前面行生成。");
                    break;
                default:
                    throw new Exception($"第{rowIndex + 1}行来源模式无效。");
            }
        }

        /// <summary>
        /// 应用选中的上游属性到当前行。
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

            ArithmeticRowViewModel row = _rows[index];
            row.SourceMode = ArithmeticOperandSourceMode.Subscription;
            row.SourceNodeId = option.SourceNodeId;
            row.SourceNodeText = option.SourceNodeText;
            row.PropertyPath = option.PropertyPath;
            row.PropertyDisplayName = option.PropertyDisplayName;
            row.ValueTypeName = option.ValueType == null ? string.Empty : option.ValueType.FullName;
            row.VariableName = string.Empty;
            _rows.ResetItem(index);
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 应用选中的内部变量到当前行。
        /// </summary>
        private void ApplySelectedVariableToCurrentRow()
        {
            string variableName = listBoxVariables.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            int index = EnsureCurrentRow();
            if (index < 0)
                return;

            ArithmeticRowViewModel row = _rows[index];
            row.SourceMode = ArithmeticOperandSourceMode.Variable;
            row.VariableName = variableName;
            row.SourceNodeId = 0;
            row.SourceNodeText = string.Empty;
            row.PropertyPath = string.Empty;
            row.PropertyDisplayName = string.Empty;
            row.ValueTypeName = string.Empty;
            _rows.ResetItem(index);
        }

        /// <summary>
        /// 刷新上游数值属性树。
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
                    TreeNode sourceTreeNode = new TreeNode(GetNodeText(sourceNode));
                    treeViewSources.Nodes.Add(sourceTreeNode);
                    if (sourceNode.Result != null)
                        AddMemberNodes(sourceTreeNode, sourceNode, sourceNode.Result.GetType(), string.Empty, string.Empty, 0);
                }
            }

            treeViewSources.ExpandAll();
            treeViewSources.EndUpdate();
        }

        /// <summary>
        /// 递归添加可订阅的数值属性。
        /// </summary>
        private void AddMemberNodes(
            TreeNode parentNode,
            NodeBase sourceNode,
            Type ownerType,
            string pathPrefix,
            string displayPrefix,
            int depth)
        {
            foreach (MemberInfo member in ArithmeticOperationAlgorithm.GetReadableMembers(ownerType))
            {
                Type memberType = ArithmeticOperationAlgorithm.GetMemberType(member);
                string displayName = ArithmeticOperationAlgorithm.GetDisplayName(member);
                string propertyPath = string.IsNullOrEmpty(pathPrefix) ? member.Name : pathPrefix + "." + member.Name;
                string propertyDisplay = string.IsNullOrEmpty(displayPrefix) ? displayName : displayPrefix + "." + displayName;

                if (ArithmeticOperationAlgorithm.IsSelectableNumericMemberType(memberType))
                {
                    TreeNode propertyNode = new TreeNode(displayName);
                    propertyNode.Tag = new ResultPropertyOption
                    {
                        SourceNodeId = sourceNode.ID,
                        SourceNodeText = GetNodeText(sourceNode),
                        PropertyPath = propertyPath,
                        PropertyDisplayName = propertyDisplay,
                        ValueType = memberType
                    };
                    parentNode.Nodes.Add(propertyNode);
                    continue;
                }

                if (depth >= 2 || !ArithmeticOperationAlgorithm.CanInspectMemberType(memberType))
                    continue;

                TreeNode groupNode = new TreeNode(displayName);
                parentNode.Nodes.Add(groupNode);
                AddMemberNodes(groupNode, sourceNode, memberType, propertyPath, propertyDisplay, depth + 1);
                if (groupNode.Nodes.Count == 0)
                    parentNode.Nodes.Remove(groupNode);
            }
        }

        /// <summary>
        /// 刷新当前行可引用的内部变量。
        /// </summary>
        private void RefreshInternalVariableList()
        {
            int currentIndex = GetCurrentRowIndex();
            listBoxVariables.Items.Clear();
            if (currentIndex <= 0)
                return;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < currentIndex && index < _rows.Count; index++)
            {
                ArithmeticRowViewModel row = _rows[index];
                if (row == null || !row.Enabled)
                    continue;

                string variableName = (row.ResultVariableName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(variableName) || names.Contains(variableName))
                    continue;

                names.Add(variableName);
                listBoxVariables.Items.Add(variableName);
            }
        }

        /// <summary>
        /// 更新本地运行结果显示。
        /// </summary>
        private void UpdateRuntimeStatus(NodeResultArithmeticOperation result)
        {
            if (result == null)
            {
                labelRuntimeStatus.Text = "暂无运行结果";
                return;
            }

            if (result.IsOk)
            {
                labelRuntimeStatus.Text = $"成功：{result.ExpressionText}";
                if (!string.IsNullOrWhiteSpace(result.VariablesText))
                    labelRuntimeStatus.Text += $"；变量：{result.VariablesText}";
            }
            else
            {
                labelRuntimeStatus.Text = $"失败：{result.Message}";
            }
        }

        /// <summary>
        /// 上下移动当前行。
        /// </summary>
        private void MoveCurrentRow(int offset)
        {
            EndGridEdit();
            int index = GetCurrentRowIndex();
            int newIndex = index + offset;
            if (index < 0 || newIndex < 0 || newIndex >= _rows.Count)
                return;

            ArithmeticRowViewModel row = _rows[index];
            _rows.RemoveAt(index);
            _rows.Insert(newIndex, row);
            SelectRow(newIndex);
            RefreshInternalVariableList();
        }

        /// <summary>
        /// 确保存在当前行。
        /// </summary>
        private int EnsureCurrentRow()
        {
            int index = GetCurrentRowIndex();
            if (index >= 0)
                return index;

            _rows.Add(CreateDefaultRow(_rows.Count));
            SelectRow(_rows.Count - 1);
            return GetCurrentRowIndex();
        }

        /// <summary>
        /// 结束表格编辑。
        /// </summary>
        private void EndGridEdit()
        {
            dataGridViewRows.EndEdit();
            CurrencyManager manager = BindingContext[_rows] as CurrencyManager;
            if (manager != null)
                manager.EndCurrentEdit();
        }

        /// <summary>
        /// 获取当前选中行索引。
        /// </summary>
        private int GetCurrentRowIndex()
        {
            if (dataGridViewRows.CurrentRow == null)
                return -1;

            return dataGridViewRows.CurrentRow.Index;
        }

        /// <summary>
        /// 选中指定行。
        /// </summary>
        private void SelectRow(int index)
        {
            if (index < 0 || index >= dataGridViewRows.Rows.Count)
                return;

            dataGridViewRows.ClearSelection();
            dataGridViewRows.Rows[index].Selected = true;
            dataGridViewRows.CurrentCell = dataGridViewRows.Rows[index].Cells[0];
        }

        /// <summary>
        /// 创建默认行。
        /// </summary>
        private static ArithmeticRowViewModel CreateDefaultRow(int index)
        {
            return new ArithmeticRowViewModel
            {
                Enabled = true,
                Operator = ArithmeticOperator.Add,
                SourceMode = ArithmeticOperandSourceMode.Constant,
                ConstantText = index == 0 ? "0" : "1",
                ResultVariableName = index == 0 ? "V1" : string.Empty
            };
        }

        /// <summary>
        /// 获取节点显示文本。
        /// </summary>
        private static string GetNodeText(NodeBase node)
        {
            return node.ID + "." + node.NodeName;
        }

        /// <summary>
        /// 通用下拉选项。
        /// </summary>
        private sealed class ComboOption<T>
        {
            /// <summary>
            /// 创建下拉选项。
            /// </summary>
            public ComboOption(string text, T value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 显示文本。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 实际值。
            /// </summary>
            public T Value { get; private set; }
        }

        /// <summary>
        /// 结果属性选择项。
        /// </summary>
        private sealed class ResultPropertyOption
        {
            /// <summary>
            /// 源节点ID。
            /// </summary>
            public int SourceNodeId { get; set; }

            /// <summary>
            /// 源节点显示文本。
            /// </summary>
            public string SourceNodeText { get; set; }

            /// <summary>
            /// 属性路径。
            /// </summary>
            public string PropertyPath { get; set; }

            /// <summary>
            /// 属性显示名称。
            /// </summary>
            public string PropertyDisplayName { get; set; }

            /// <summary>
            /// 属性值类型。
            /// </summary>
            public Type ValueType { get; set; }
        }

        /// <summary>
        /// 运算行界面模型。
        /// </summary>
        private sealed class ArithmeticRowViewModel
        {
            /// <summary>
            /// 是否启用。
            /// </summary>
            public bool Enabled { get; set; }

            /// <summary>
            /// 运算符。
            /// </summary>
            public ArithmeticOperator Operator { get; set; }

            /// <summary>
            /// 来源模式。
            /// </summary>
            public ArithmeticOperandSourceMode SourceMode { get; set; }

            /// <summary>
            /// 常量文本。
            /// </summary>
            public string ConstantText { get; set; }

            /// <summary>
            /// 源节点ID。
            /// </summary>
            public int SourceNodeId { get; set; }

            /// <summary>
            /// 源节点文本。
            /// </summary>
            public string SourceNodeText { get; set; }

            /// <summary>
            /// 属性路径。
            /// </summary>
            public string PropertyPath { get; set; }

            /// <summary>
            /// 属性显示名。
            /// </summary>
            public string PropertyDisplayName { get; set; }

            /// <summary>
            /// 值类型名称。
            /// </summary>
            public string ValueTypeName { get; set; }

            /// <summary>
            /// 引用变量名。
            /// </summary>
            public string VariableName { get; set; }

            /// <summary>
            /// 输出变量名。
            /// </summary>
            public string ResultVariableName { get; set; }

            /// <summary>
            /// 注释。
            /// </summary>
            public string Note { get; set; }

            /// <summary>
            /// 来源摘要。
            /// </summary>
            public string SourceSummary
            {
                get
                {
                    switch (SourceMode)
                    {
                        case ArithmeticOperandSourceMode.Constant:
                            return "常量：" + (ConstantText ?? string.Empty);
                        case ArithmeticOperandSourceMode.Subscription:
                            return (SourceNodeText ?? string.Empty) + "." + (PropertyDisplayName ?? PropertyPath ?? string.Empty);
                        case ArithmeticOperandSourceMode.Variable:
                            return "变量：" + (VariableName ?? string.Empty);
                        default:
                            return string.Empty;
                    }
                }
            }

            /// <summary>
            /// 从参数行创建界面模型。
            /// </summary>
            public static ArithmeticRowViewModel FromRow(ArithmeticOperationRow row)
            {
                if (row == null)
                    row = new ArithmeticOperationRow();

                return new ArithmeticRowViewModel
                {
                    Enabled = row.Enabled,
                    Operator = row.Operator,
                    SourceMode = row.SourceMode,
                    ConstantText = row.ConstantText,
                    SourceNodeId = row.SourceNodeId,
                    SourceNodeText = row.SourceNodeText,
                    PropertyPath = row.PropertyPath,
                    PropertyDisplayName = row.PropertyDisplayName,
                    ValueTypeName = row.ValueTypeName,
                    VariableName = row.VariableName,
                    ResultVariableName = row.ResultVariableName,
                    Note = row.Note
                };
            }

            /// <summary>
            /// 转换为参数行。
            /// </summary>
            public ArithmeticOperationRow ToRow()
            {
                return new ArithmeticOperationRow
                {
                    Enabled = Enabled,
                    Operator = Operator,
                    SourceMode = SourceMode,
                    ConstantText = ConstantText,
                    SourceNodeId = SourceNodeId,
                    SourceNodeText = SourceNodeText,
                    PropertyPath = PropertyPath,
                    PropertyDisplayName = PropertyDisplayName,
                    ValueTypeName = ValueTypeName,
                    VariableName = VariableName,
                    ResultVariableName = ResultVariableName,
                    Note = Note
                };
            }
        }
    }
}
