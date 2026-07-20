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
            BindOperatorColumn();
            numericUpDownDecimalPlaces.Value = NodeParamArithmeticOperation.OutputDecimalPlaces;
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

            numericUpDownDecimalPlaces.Value = NodeParamArithmeticOperation.OutputDecimalPlaces;

            _rows = new BindingList<ArithmeticRowViewModel>();
            if (param.Rows != null)
            {
                for (int index = 0; index < param.Rows.Count; index++)
                    _rows.Add(ArithmeticRowViewModel.FromRow(param.Rows[index], index));
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
            ColumnValue1.DataPropertyName = "Value1Summary";
            ColumnOperator.DataPropertyName = "Operator";
            ColumnValue2.DataPropertyName = "Value2Summary";
            ColumnOutputVariableName.DataPropertyName = "OutputVariableName";
            ColumnNote.DataPropertyName = "Note";
        }

        /// <summary>
        /// 绑定运算符列显示值。
        /// </summary>
        private void BindOperatorColumn()
        {
            ColumnOperator.DataSource = GetOperatorOptions();
            ColumnOperator.DisplayMember = "Text";
            ColumnOperator.ValueMember = "Value";
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
        /// 应用常量到当前行的值1或值2。
        /// </summary>
        private void buttonApplyConstant_Click(object sender, EventArgs e)
        {
            int index = EnsureCurrentRow();
            if (index < 0)
                return;

            SetCurrentOperand(new ArithmeticOperand
            {
                SourceMode = ArithmeticOperandSourceMode.Constant,
                ConstantText = textBoxConstant.Text
            });
        }

        /// <summary>
        /// 选择上游属性给当前行的值1或值2。
        /// </summary>
        private void buttonSelectProperty_Click(object sender, EventArgs e)
        {
            ApplySelectedPropertyToCurrentOperand();
        }

        /// <summary>
        /// 双击上游属性时直接应用。
        /// </summary>
        private void treeViewSources_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ApplySelectedPropertyToCurrentOperand();
        }

        /// <summary>
        /// 双击内部变量时直接应用。
        /// </summary>
        private void listBoxVariables_DoubleClick(object sender, EventArgs e)
        {
            ApplySelectedVariableToCurrentOperand();
        }

        /// <summary>
        /// 点击变量应用按钮。
        /// </summary>
        private void buttonUseVariable_Click(object sender, EventArgs e)
        {
            ApplySelectedVariableToCurrentOperand();
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
                RenameOperandSource(row.Value1, e);
                RenameOperandSource(row.Value2, e);
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
                DecimalPlaces = NodeParamArithmeticOperation.OutputDecimalPlaces,
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
                ValidateOutputVariableName(row.OutputVariableName, generatedVariables, index);
                ValidateOperand(row.Value1, generatedVariables, index, "值1");
                ValidateOperand(row.Value2, generatedVariables, index, "值2");
                generatedVariables.Add(row.OutputVariableName.Trim());
            }

            if (enabledCount == 0)
                throw new Exception("至少需要启用一行运算。");
        }

        /// <summary>
        /// 校验输出变量名。
        /// </summary>
        private static void ValidateOutputVariableName(
            string variableName,
            HashSet<string> generatedVariables,
            int rowIndex)
        {
            string message;
            if (!DynamicResultVariableResolver.IsValidVariableName(variableName, out message))
                throw new Exception($"第{rowIndex + 1}行输出变量名无效：{message}");

            if (generatedVariables.Contains(variableName.Trim()))
                throw new Exception($"第{rowIndex + 1}行输出变量名“{variableName.Trim()}”重复。");
        }

        /// <summary>
        /// 校验操作数来源设置。
        /// </summary>
        private static void ValidateOperand(
            ArithmeticOperand operand,
            HashSet<string> generatedVariables,
            int rowIndex,
            string operandLabel)
        {
            if (operand == null)
                throw new Exception($"第{rowIndex + 1}行{operandLabel}未配置。");

            switch (operand.SourceMode)
            {
                case ArithmeticOperandSourceMode.Constant:
                    ArithmeticOperationAlgorithm.ConvertToDouble(operand.ConstantText, $"第{rowIndex + 1}行{operandLabel}常量");
                    break;
                case ArithmeticOperandSourceMode.Subscription:
                    if (operand.SourceNodeId <= 0 && string.IsNullOrWhiteSpace(operand.SourceNodeText))
                        throw new Exception($"第{rowIndex + 1}行{operandLabel}没有选择订阅节点。");
                    if (string.IsNullOrWhiteSpace(operand.PropertyPath))
                        throw new Exception($"第{rowIndex + 1}行{operandLabel}没有选择订阅属性。");
                    break;
                case ArithmeticOperandSourceMode.Variable:
                    string variableName = (operand.VariableName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(variableName))
                        throw new Exception($"第{rowIndex + 1}行{operandLabel}没有选择内部变量。");
                    if (!generatedVariables.Contains(variableName))
                        throw new Exception($"第{rowIndex + 1}行{operandLabel}引用的内部变量“{variableName}”尚未由前面行生成。");
                    break;
                default:
                    throw new Exception($"第{rowIndex + 1}行{operandLabel}来源模式无效。");
            }
        }

        /// <summary>
        /// 应用选中的上游属性到当前操作数。
        /// </summary>
        private void ApplySelectedPropertyToCurrentOperand()
        {
            ResultPropertyOption option = treeViewSources.SelectedNode == null
                ? null
                : treeViewSources.SelectedNode.Tag as ResultPropertyOption;
            if (option == null)
                return;

            SetCurrentOperand(new ArithmeticOperand
            {
                SourceMode = ArithmeticOperandSourceMode.Subscription,
                SourceNodeId = option.SourceNodeId,
                SourceNodeText = option.SourceNodeText,
                PropertyPath = option.PropertyPath,
                PropertyDisplayName = option.PropertyDisplayName,
                ValueTypeName = option.ValueType == null ? string.Empty : option.ValueType.FullName
            });
        }

        /// <summary>
        /// 应用选中的内部变量到当前操作数。
        /// </summary>
        private void ApplySelectedVariableToCurrentOperand()
        {
            string variableName = listBoxVariables.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(variableName))
                return;

            SetCurrentOperand(new ArithmeticOperand
            {
                SourceMode = ArithmeticOperandSourceMode.Variable,
                VariableName = variableName
            });
        }

        /// <summary>
        /// 设置当前行值1或值2。
        /// </summary>
        private void SetCurrentOperand(ArithmeticOperand operand)
        {
            int index = EnsureCurrentRow();
            if (index < 0)
                return;

            ArithmeticRowViewModel row = _rows[index];
            if (radioButtonValue2.Checked)
                row.Value2 = operand;
            else
                row.Value1 = operand;

            _rows.ResetItem(index);
            RefreshInternalVariableList();
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
                    AddDynamicVariableNodes(sourceTreeNode, sourceNode);
                    if (sourceNode.Result != null)
                        AddMemberNodes(sourceTreeNode, sourceNode, sourceNode.Result.GetType(), string.Empty, string.Empty, 0);
                }
            }

            treeViewSources.ExpandAll();
            treeViewSources.EndUpdate();
        }

        /// <summary>
        /// 添加动态输出变量节点。
        /// </summary>
        private void AddDynamicVariableNodes(TreeNode sourceTreeNode, NodeBase sourceNode)
        {
            List<string> names = DynamicResultVariableResolver.GetVariableNames(sourceNode);
            if (names.Count == 0)
                return;

            TreeNode variableGroup = new TreeNode("输出变量");
            sourceTreeNode.Nodes.Add(variableGroup);
            foreach (string name in names)
            {
                TreeNode variableNode = new TreeNode(name);
                variableNode.Tag = new ResultPropertyOption
                {
                    SourceNodeId = sourceNode.ID,
                    SourceNodeText = GetNodeText(sourceNode),
                    PropertyPath = DynamicResultVariableResolver.ToPropertyPath(name),
                    PropertyDisplayName = name,
                    ValueType = typeof(double)
                };
                variableGroup.Nodes.Add(variableNode);
            }
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

                string variableName = (row.OutputVariableName ?? string.Empty).Trim();
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

            labelRuntimeStatus.Text = result.IsOk
                ? $"成功：{result.VariablesText}"
                : $"失败：{result.Message}";
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
                Value1 = new ArithmeticOperand { SourceMode = ArithmeticOperandSourceMode.Constant, ConstantText = "0" },
                Value2 = new ArithmeticOperand { SourceMode = ArithmeticOperandSourceMode.Constant, ConstantText = "0" },
                OutputVariableName = "V" + (index + 1)
            };
        }

        /// <summary>
        /// 同步重命名后的订阅节点文本。
        /// </summary>
        private static void RenameOperandSource(ArithmeticOperand operand, RenameResult e)
        {
            if (operand == null || operand.SourceNodeId != e.NodeId)
                return;

            operand.SourceNodeText = e.NodeId + "." + e.NodeNameNew;
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
            /// 左侧操作数。
            /// </summary>
            public ArithmeticOperand Value1 { get; set; }

            /// <summary>
            /// 运算符。
            /// </summary>
            public ArithmeticOperator Operator { get; set; }

            /// <summary>
            /// 右侧操作数。
            /// </summary>
            public ArithmeticOperand Value2 { get; set; }

            /// <summary>
            /// 输出变量名。
            /// </summary>
            public string OutputVariableName { get; set; }

            /// <summary>
            /// 注释。
            /// </summary>
            public string Note { get; set; }

            /// <summary>
            /// 值1摘要。
            /// </summary>
            public string Value1Summary { get { return ArithmeticOperationAlgorithm.BuildOperandSummary(Value1); } }

            /// <summary>
            /// 值2摘要。
            /// </summary>
            public string Value2Summary { get { return ArithmeticOperationAlgorithm.BuildOperandSummary(Value2); } }

            /// <summary>
            /// 从参数行创建界面模型。
            /// </summary>
            public static ArithmeticRowViewModel FromRow(ArithmeticOperationRow row, int index)
            {
                if (row == null)
                    row = new ArithmeticOperationRow();

                ArithmeticOperand value1 = row.Value1 ?? new ArithmeticOperand();
                ArithmeticOperand value2 = row.Value2 ?? new ArithmeticOperand();

                if (string.IsNullOrWhiteSpace(row.OutputVariableName) && HasLegacyOperand(row))
                {
                    value1 = new ArithmeticOperand { SourceMode = ArithmeticOperandSourceMode.Constant, ConstantText = "0" };
                    value2 = new ArithmeticOperand
                    {
                        SourceMode = row.SourceMode,
                        ConstantText = string.IsNullOrWhiteSpace(row.ConstantText) ? "0" : row.ConstantText,
                        SourceNodeId = row.SourceNodeId,
                        SourceNodeText = row.SourceNodeText,
                        PropertyPath = row.PropertyPath,
                        PropertyDisplayName = row.PropertyDisplayName,
                        ValueTypeName = row.ValueTypeName,
                        VariableName = row.VariableName
                    };
                }

                return new ArithmeticRowViewModel
                {
                    Enabled = row.Enabled,
                    Operator = row.Operator,
                    Value1 = value1,
                    Value2 = value2,
                    OutputVariableName = string.IsNullOrWhiteSpace(row.OutputVariableName)
                        ? (string.IsNullOrWhiteSpace(row.ResultVariableName) ? "V" + (index + 1) : row.ResultVariableName)
                        : row.OutputVariableName,
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
                    Value1 = Value1,
                    Value2 = Value2,
                    OutputVariableName = OutputVariableName,
                    Note = Note
                };
            }

            /// <summary>
            /// 判断是否带有旧版累计模型字段。
            /// </summary>
            private static bool HasLegacyOperand(ArithmeticOperationRow row)
            {
                return row.SourceNodeId > 0 ||
                       !string.IsNullOrWhiteSpace(row.SourceNodeText) ||
                       !string.IsNullOrWhiteSpace(row.PropertyPath) ||
                       !string.IsNullOrWhiteSpace(row.VariableName) ||
                       !string.IsNullOrWhiteSpace(row.ResultVariableName) ||
                       !string.IsNullOrWhiteSpace(row.ConstantText);
            }
        }
    }
}
