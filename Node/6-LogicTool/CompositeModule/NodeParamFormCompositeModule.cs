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
    /// 组合模块参数配置窗口。
    /// </summary>
    public partial class NodeParamFormCompositeModule : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前节点所属流程，用于排除自引用流程。
        /// </summary>
        private Process _ownerProcess;

        /// <summary>
        /// 当前参数窗口所属节点。
        /// </summary>
        private NodeBase _ownerNode;

        /// <summary>
        /// 界面上已导入但尚未保存到节点的快照参数。
        /// </summary>
        private NodeParamCompositeModule _pendingParam;

        /// <summary>
        /// 标记流程下拉框正在刷新，避免刷新过程中触发选择逻辑。
        /// </summary>
        private bool _loadingProcessItems;

        /// <summary>
        /// 输入端口绑定表格数据。
        /// </summary>
        private BindingList<InputBindingViewModel> _inputBindings;

        /// <summary>
        /// 创建组合模块参数配置窗口。
        /// </summary>
        /// <param name="ownerProcess">当前节点所属流程。</param>
        public NodeParamFormCompositeModule(Process ownerProcess)
        {
            _ownerProcess = ownerProcess;
            InitializeComponent();
            _inputBindings = new BindingList<InputBindingViewModel>();
            ConfigureInputGrid();
            BindSourceModeColumn();
            dataGridViewInputBindings.DataSource = _inputBindings;
            UpdatePreviewFromParam(null);
            NodeBase.RefreshNodeSubControl += NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
        }

        /// <summary>
        /// 节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗口所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            if (_ownerProcess != null)
                _ownerProcess.ConnectionsChanged -= Process_ConnectionsChanged;

            _ownerNode = node;
            if (_ownerProcess == null && node != null)
                _ownerProcess = node.Process;

            if (_ownerProcess != null)
                _ownerProcess.ConnectionsChanged += Process_ConnectionsChanged;
        }

        /// <summary>
        /// 将反序列化参数回写到界面。
        /// </summary>
        public void SetParam2Form()
        {
            _pendingParam = CompositeModuleSnapshotBuilder.Clone(Params as NodeParamCompositeModule);
            LoadProcessItems();
            UpdatePreviewFromParam(_pendingParam);
            RefreshSourceTree();
        }

        /// <summary>
        /// 窗口显示时刷新可选来源流程。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void NodeParamFormCompositeModule_Shown(object sender, EventArgs e)
        {
            if (_pendingParam == null)
                _pendingParam = CompositeModuleSnapshotBuilder.Clone(Params as NodeParamCompositeModule);

            LoadProcessItems();
            UpdatePreviewFromParam(_pendingParam);
            RefreshSourceTree();
        }

        /// <summary>
        /// 刷新按钮点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            LoadProcessItems();
        }

        /// <summary>
        /// 导入快照按钮点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonImport_Click(object sender, EventArgs e)
        {
            ImportSelectedProcess(true);
        }

        /// <summary>
        /// 保存按钮点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (ShouldImportSelectedProcess() && !ImportSelectedProcess(false))
                return;

            if (_pendingParam == null || _pendingParam.NodeInfos == null || _pendingParam.NodeInfos.Count == 0)
            {
                MessageBoxTD.Show("请先导入来源流程快照！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            NodeParamCompositeModule param = CompositeModuleSnapshotBuilder.Clone(_pendingParam);
            string moduleName = (textBoxModuleName.Text ?? string.Empty).Trim();
            param.ModuleName = string.IsNullOrWhiteSpace(moduleName)
                ? (string.IsNullOrWhiteSpace(param.SourceProcessName) ? "组合模块" : param.SourceProcessName)
                : moduleName;
            param.InputBindings = BuildInputBindingsFromForm(param);

            Params = param;
            _pendingParam = CompositeModuleSnapshotBuilder.Clone(param);
            Hide();
        }

        /// <summary>
        /// 取消按钮点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _pendingParam = CompositeModuleSnapshotBuilder.Clone(Params as NodeParamCompositeModule);
            Hide();
        }

        /// <summary>
        /// 来源流程选择变化事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void comboBoxSourceProcess_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingProcessItems)
                return;

            Process selectedProcess = GetSelectedProcess();
            if (selectedProcess == null)
                return;

            if (_pendingParam == null || _pendingParam.SourceProcessId != selectedProcess.ID)
            {
                textBoxNodeCount.Text = "未导入";
                textBoxConnectionCount.Text = "未导入";
                textBoxModuleName.Text = selectedProcess.ProcessName;
                textBoxInputCount.Text = "未导入";
                textBoxOutputCount.Text = "未导入";
                _inputBindings.Clear();
            }
        }

        /// <summary>
        /// 外部流程连线变化后刷新输入绑定候选来源。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void Process_ConnectionsChanged(object sender, EventArgs e)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 节点重命名后刷新输入绑定显示。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">重命名信息。</param>
        private void NodeBase_RefreshNodeSubControl(object sender, RenameResult e)
        {
            RefreshSourceTree();
            foreach (InputBindingViewModel row in _inputBindings)
            {
                if (row.SourceNodeId == e.NodeId)
                    row.SourceNodeText = e.NodeId + "." + e.NodeNameNew;
            }

            _inputBindings.ResetBindings();
        }

        /// <summary>
        /// 节点删除后刷新候选来源。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="deletedNode">删除的节点。</param>
        private void NodeBase_NodeDeletedEvent(object sender, NodeBase deletedNode)
        {
            RefreshSourceTree();
        }

        /// <summary>
        /// 重新加载流程下拉框。
        /// </summary>
        private void LoadProcessItems()
        {
            int selectedProcessId = GetPreferredProcessId();

            _loadingProcessItems = true;
            comboBoxSourceProcess.BeginUpdate();
            comboBoxSourceProcess.Items.Clear();

            if (Solution.Instance != null && Solution.Instance.AllProcesses != null)
            {
                foreach (Process process in Solution.Instance.AllProcesses)
                {
                    if (process == null || IsOwnerProcess(process))
                        continue;

                    comboBoxSourceProcess.Items.Add(new ProcessListItem(process));
                }
            }

            comboBoxSourceProcess.SelectedItem = FindItemByProcessId(selectedProcessId);
            if (comboBoxSourceProcess.SelectedItem == null && comboBoxSourceProcess.Items.Count > 0 && _pendingParam == null)
                comboBoxSourceProcess.SelectedIndex = 0;

            comboBoxSourceProcess.EndUpdate();
            _loadingProcessItems = false;
        }

        /// <summary>
        /// 获取界面应优先选中的流程 ID。
        /// </summary>
        /// <returns>流程 ID。</returns>
        private int GetPreferredProcessId()
        {
            if (_pendingParam != null && _pendingParam.SourceProcessId > 0)
                return _pendingParam.SourceProcessId;

            if (Params is NodeParamCompositeModule param && param.SourceProcessId > 0)
                return param.SourceProcessId;

            return 0;
        }

        /// <summary>
        /// 从下拉框中查找指定流程 ID 的选项。
        /// </summary>
        /// <param name="processId">流程 ID。</param>
        /// <returns>流程选项。</returns>
        private ProcessListItem FindItemByProcessId(int processId)
        {
            if (processId <= 0)
                return null;

            foreach (object item in comboBoxSourceProcess.Items)
            {
                ProcessListItem processItem = item as ProcessListItem;
                if (processItem != null && processItem.Process.ID == processId)
                    return processItem;
            }

            return null;
        }

        /// <summary>
        /// 判断指定流程是否为当前节点所在流程。
        /// </summary>
        /// <param name="process">待判断流程。</param>
        /// <returns>是当前流程时返回 true。</returns>
        private bool IsOwnerProcess(Process process)
        {
            if (process == null || _ownerProcess == null)
                return false;

            return ReferenceEquals(process, _ownerProcess) || process.ID == _ownerProcess.ID;
        }

        /// <summary>
        /// 判断保存前是否需要导入当前选中的流程。
        /// </summary>
        /// <returns>需要导入时返回 true。</returns>
        private bool ShouldImportSelectedProcess()
        {
            Process selectedProcess = GetSelectedProcess();
            if (selectedProcess == null)
                return _pendingParam == null;

            return _pendingParam == null ||
                   _pendingParam.NodeInfos == null ||
                   _pendingParam.NodeInfos.Count == 0 ||
                   _pendingParam.SourceProcessId != selectedProcess.ID;
        }

        /// <summary>
        /// 导入当前选中的来源流程快照。
        /// </summary>
        /// <param name="showMessage">是否显示导入完成消息。</param>
        /// <returns>导入成功时返回 true。</returns>
        private bool ImportSelectedProcess(bool showMessage)
        {
            Process sourceProcess = GetSelectedProcess();
            if (sourceProcess == null)
            {
                MessageBoxTD.Show("请选择来源流程！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (IsOwnerProcess(sourceProcess))
            {
                MessageBoxTD.Show("不能选择当前流程作为组合模块来源！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (sourceProcess.Nodes == null || sourceProcess.Nodes.Count == 0)
            {
                MessageBoxTD.Show("来源流程没有节点，不能导入为空模块！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            try
            {
                _pendingParam = CompositeModuleSnapshotBuilder.CreateFromProcess(sourceProcess);
                _pendingParam.InputBindings = CompositeModuleSnapshotBuilder.SyncInputBindings(
                    _pendingParam.InputPorts,
                    _pendingParam.InputBindings);
                textBoxModuleName.Text = _pendingParam.ModuleName;
                UpdatePreviewFromParam(_pendingParam);

                if (showMessage)
                {
                    MessageBoxTD.Show(
                        $"已导入流程“{sourceProcess.ProcessName}”，内部节点 {_pendingParam.NodeInfos.Count} 个。",
                        "提示",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"导入组合模块失败：{ex.Message}", "异常", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }
        }

        /// <summary>
        /// 获取当前选中的来源流程。
        /// </summary>
        /// <returns>来源流程。</returns>
        private Process GetSelectedProcess()
        {
            ProcessListItem item = comboBoxSourceProcess.SelectedItem as ProcessListItem;
            return item == null ? null : item.Process;
        }

        /// <summary>
        /// 根据参数更新模块名称、节点数和连线数预览。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        private void UpdatePreviewFromParam(NodeParamCompositeModule param)
        {
            if (param == null)
            {
                textBoxModuleName.Text = string.Empty;
                textBoxNodeCount.Text = "0";
                textBoxConnectionCount.Text = "0";
                textBoxInputCount.Text = "0";
                textBoxOutputCount.Text = "0";
                BindInputBindings(null);
                return;
            }

            textBoxModuleName.Text = string.IsNullOrWhiteSpace(param.ModuleName)
                ? param.SourceProcessName
                : param.ModuleName;
            textBoxNodeCount.Text = (param.NodeInfos == null ? 0 : param.NodeInfos.Count).ToString();
            textBoxConnectionCount.Text = (param.ConnectionInfos == null ? 0 : param.ConnectionInfos.Count).ToString();
            textBoxInputCount.Text = (param.InputPorts == null ? 0 : param.InputPorts.Count).ToString();
            textBoxOutputCount.Text = (param.OutputPorts == null ? 0 : param.OutputPorts.Count).ToString();
            BindInputBindings(param);
        }

        /// <summary>
        /// 配置输入绑定表格。
        /// </summary>
        private void ConfigureInputGrid()
        {
            dataGridViewInputBindings.AutoGenerateColumns = false;
            dataGridViewInputBindings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewInputBindings.RowTemplate.Height = 30;
            ColumnInputName.DataPropertyName = "PortName";
            ColumnInputType.DataPropertyName = "ValueTypeText";
            ColumnInputMode.DataPropertyName = "SourceMode";
            ColumnInputConstant.DataPropertyName = "ConstantText";
            ColumnInputSource.DataPropertyName = "SourceSummary";
            ColumnInputNote.DataPropertyName = "Note";
        }

        /// <summary>
        /// 绑定输入来源模式下拉选项。
        /// </summary>
        private void BindSourceModeColumn()
        {
            ColumnInputMode.DataSource = GetSourceModeOptions();
            ColumnInputMode.DisplayMember = "Text";
            ColumnInputMode.ValueMember = "Value";
        }

        /// <summary>
        /// 设置输入绑定表格数据。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        private void BindInputBindings(NodeParamCompositeModule param)
        {
            _inputBindings = new BindingList<InputBindingViewModel>();
            if (param != null)
            {
                param.InputBindings = CompositeModuleSnapshotBuilder.SyncInputBindings(
                    param.InputPorts,
                    param.InputBindings);

                if (param.InputBindings != null)
                {
                    foreach (CompositeInputBinding binding in param.InputBindings)
                        _inputBindings.Add(InputBindingViewModel.FromBinding(binding));
                }
            }

            dataGridViewInputBindings.DataSource = _inputBindings;
        }

        /// <summary>
        /// 从界面构建输入绑定参数。
        /// </summary>
        /// <param name="param">组合模块参数。</param>
        /// <returns>输入绑定列表。</returns>
        private List<CompositeInputBinding> BuildInputBindingsFromForm(NodeParamCompositeModule param)
        {
            EndInputGridEdit();
            List<CompositeInputBinding> bindings = new List<CompositeInputBinding>();
            if (param == null || param.InputPorts == null)
                return bindings;

            foreach (InputBindingViewModel row in _inputBindings)
            {
                if (row == null)
                    continue;

                if (row.SourceMode == CompositePortValueSourceMode.Subscription &&
                    (row.SourceNodeId <= 0 || string.IsNullOrWhiteSpace(row.PropertyPath)))
                {
                    throw new Exception("输入端口“" + row.PortName + "”选择了订阅值，但没有选择外部来源。");
                }

                bindings.Add(row.ToBinding());
            }

            return CompositeModuleSnapshotBuilder.SyncInputBindings(param.InputPorts, bindings);
        }

        /// <summary>
        /// 应用当前选择的外部上游结果到当前输入端口。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSelectInputSource_Click(object sender, EventArgs e)
        {
            ApplySelectedSourceToCurrentInput();
        }

        /// <summary>
        /// 双击外部来源结果时应用到当前输入端口。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">树节点事件。</param>
        private void treeViewSources_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ApplySelectedSourceToCurrentInput();
        }

        /// <summary>
        /// 输入绑定表格数据错误处理。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">数据错误事件。</param>
        private void dataGridViewInputBindings_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        /// <summary>
        /// 应用当前来源树选择。
        /// </summary>
        private void ApplySelectedSourceToCurrentInput()
        {
            ResultPropertyOption option = treeViewSources.SelectedNode == null
                ? null
                : treeViewSources.SelectedNode.Tag as ResultPropertyOption;
            if (option == null)
                return;

            int index = GetCurrentInputRowIndex();
            if (index < 0)
                return;

            InputBindingViewModel row = _inputBindings[index];
            row.SourceMode = CompositePortValueSourceMode.Subscription;
            row.SourceNodeId = option.SourceNodeId;
            row.SourceNodeText = option.SourceNodeText;
            row.PropertyPath = option.PropertyPath;
            row.PropertyDisplayName = option.PropertyDisplayName;
            row.ValueTypeName = option.ValueType == null ? string.Empty : option.ValueType.FullName;
            _inputBindings.ResetItem(index);
        }

        /// <summary>
        /// 刷新外部上游结果树。
        /// </summary>
        private void RefreshSourceTree()
        {
            treeViewSources.BeginUpdate();
            treeViewSources.Nodes.Clear();
            treeViewSources.Nodes.Add(new TreeNode("无"));

            if (_ownerNode != null && _ownerNode.Process != null)
            {
                List<NodeBase> upstreamNodes = _ownerNode.Process.GetUpstreamNodes(_ownerNode);
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
        /// 添加动态变量来源。
        /// </summary>
        /// <param name="sourceTreeNode">来源树节点。</param>
        /// <param name="sourceNode">来源节点。</param>
        private void AddDynamicVariableNodes(TreeNode sourceTreeNode, NodeBase sourceNode)
        {
            List<string> names = DynamicResultVariableResolver.GetVariableNames(sourceNode);
            if (names.Count == 0)
                return;

            TreeNode groupNode = new TreeNode("输出变量");
            sourceTreeNode.Nodes.Add(groupNode);
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
                groupNode.Nodes.Add(variableNode);
            }
        }

        /// <summary>
        /// 递归添加普通结果属性来源。
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
        /// 获取当前输入行索引。
        /// </summary>
        /// <returns>当前行索引。</returns>
        private int GetCurrentInputRowIndex()
        {
            return dataGridViewInputBindings.CurrentRow == null
                ? -1
                : dataGridViewInputBindings.CurrentRow.Index;
        }

        /// <summary>
        /// 结束输入绑定表格编辑。
        /// </summary>
        private void EndInputGridEdit()
        {
            dataGridViewInputBindings.EndEdit();
            BindingContext[dataGridViewInputBindings.DataSource]?.EndCurrentEdit();
        }

        /// <summary>
        /// 获取来源模式选项。
        /// </summary>
        /// <returns>来源模式选项。</returns>
        private static List<SourceModeOption> GetSourceModeOptions()
        {
            return new List<SourceModeOption>
            {
                new SourceModeOption("常量", CompositePortValueSourceMode.Constant),
                new SourceModeOption("订阅值", CompositePortValueSourceMode.Subscription)
            };
        }

        /// <summary>
        /// 窗体关闭时解除事件订阅。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_ownerProcess != null)
                _ownerProcess.ConnectionsChanged -= Process_ConnectionsChanged;

            NodeBase.RefreshNodeSubControl -= NodeBase_RefreshNodeSubControl;
            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 来源流程下拉框显示项。
        /// </summary>
        private class ProcessListItem
        {
            /// <summary>
            /// 创建流程显示项。
            /// </summary>
            /// <param name="process">流程对象。</param>
            public ProcessListItem(Process process)
            {
                Process = process;
            }

            /// <summary>
            /// 流程对象。
            /// </summary>
            public Process Process { get; private set; }

            /// <summary>
            /// 获取下拉框显示文本。
            /// </summary>
            /// <returns>流程显示文本。</returns>
            public override string ToString()
            {
                return Process == null ? string.Empty : $"{Process.ID}.{Process.ProcessName}";
            }
        }

        /// <summary>
        /// 来源模式下拉选项。
        /// </summary>
        private sealed class SourceModeOption
        {
            /// <summary>
            /// 创建来源模式选项。
            /// </summary>
            /// <param name="text">显示文本。</param>
            /// <param name="value">来源模式。</param>
            public SourceModeOption(string text, CompositePortValueSourceMode value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 显示文本。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 来源模式。
            /// </summary>
            public CompositePortValueSourceMode Value { get; private set; }
        }

        /// <summary>
        /// 结果属性选择项。
        /// </summary>
        private sealed class ResultPropertyOption
        {
            /// <summary>
            /// 来源节点 ID。
            /// </summary>
            public int SourceNodeId { get; set; }

            /// <summary>
            /// 来源节点显示文本。
            /// </summary>
            public string SourceNodeText { get; set; }

            /// <summary>
            /// 结果属性路径。
            /// </summary>
            public string PropertyPath { get; set; }

            /// <summary>
            /// 结果属性显示名称。
            /// </summary>
            public string PropertyDisplayName { get; set; }

            /// <summary>
            /// 结果值类型。
            /// </summary>
            public Type ValueType { get; set; }
        }

        /// <summary>
        /// 输入绑定表格视图模型。
        /// </summary>
        private sealed class InputBindingViewModel
        {
            /// <summary>
            /// 输入端口名称。
            /// </summary>
            public string PortName { get; set; }

            /// <summary>
            /// 输入端口值类型。
            /// </summary>
            public CompositePortValueType ValueType { get; set; }

            /// <summary>
            /// 值类型中文文本。
            /// </summary>
            public string ValueTypeText
            {
                get { return CompositePortValueHelper.GetValueTypeText(ValueType); }
            }

            /// <summary>
            /// 输入值来源。
            /// </summary>
            public CompositePortValueSourceMode SourceMode { get; set; }

            /// <summary>
            /// 常量输入文本。
            /// </summary>
            public string ConstantText { get; set; }

            /// <summary>
            /// 外部订阅来源节点 ID。
            /// </summary>
            public int SourceNodeId { get; set; }

            /// <summary>
            /// 外部订阅来源节点显示文本。
            /// </summary>
            public string SourceNodeText { get; set; }

            /// <summary>
            /// 外部订阅结果属性路径。
            /// </summary>
            public string PropertyPath { get; set; }

            /// <summary>
            /// 外部订阅结果属性显示名。
            /// </summary>
            public string PropertyDisplayName { get; set; }

            /// <summary>
            /// 外部订阅结果类型名称。
            /// </summary>
            public string ValueTypeName { get; set; }

            /// <summary>
            /// 备注。
            /// </summary>
            public string Note { get; set; }

            /// <summary>
            /// 来源摘要。
            /// </summary>
            public string SourceSummary
            {
                get
                {
                    if (SourceMode == CompositePortValueSourceMode.Constant)
                        return string.Empty;
                    if (string.IsNullOrWhiteSpace(SourceNodeText))
                        return string.Empty;

                    return SourceNodeText + "." + (PropertyDisplayName ?? PropertyPath ?? string.Empty);
                }
            }

            /// <summary>
            /// 从输入绑定创建视图模型。
            /// </summary>
            /// <param name="binding">输入绑定。</param>
            /// <returns>视图模型。</returns>
            public static InputBindingViewModel FromBinding(CompositeInputBinding binding)
            {
                if (binding == null)
                    binding = new CompositeInputBinding();

                return new InputBindingViewModel
                {
                    PortName = binding.PortName,
                    ValueType = binding.ValueType,
                    SourceMode = binding.SourceMode,
                    ConstantText = binding.ConstantText,
                    SourceNodeId = binding.SourceNodeId,
                    SourceNodeText = binding.SourceNodeText,
                    PropertyPath = binding.PropertyPath,
                    PropertyDisplayName = binding.PropertyDisplayName,
                    ValueTypeName = binding.ValueTypeName,
                    Note = binding.Note
                };
            }

            /// <summary>
            /// 转换为输入绑定参数。
            /// </summary>
            /// <returns>输入绑定。</returns>
            public CompositeInputBinding ToBinding()
            {
                return new CompositeInputBinding
                {
                    PortName = PortName,
                    ValueType = ValueType,
                    SourceMode = SourceMode,
                    ConstantText = ConstantText,
                    SourceNodeId = SourceNodeId,
                    SourceNodeText = SourceNodeText,
                    PropertyPath = PropertyPath,
                    PropertyDisplayName = PropertyDisplayName,
                    ValueTypeName = ValueTypeName,
                    Note = Note
                };
            }
        }
    }
}
