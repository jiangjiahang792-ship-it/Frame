using System;
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
        /// 创建组合模块参数配置窗口。
        /// </summary>
        /// <param name="ownerProcess">当前节点所属流程。</param>
        public NodeParamFormCompositeModule(Process ownerProcess)
        {
            _ownerProcess = ownerProcess;
            InitializeComponent();
            UpdatePreviewFromParam(null);
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
            _ownerNode = node;
            if (_ownerProcess == null && node != null)
                _ownerProcess = node.Process;
        }

        /// <summary>
        /// 将反序列化参数回写到界面。
        /// </summary>
        public void SetParam2Form()
        {
            _pendingParam = CompositeModuleSnapshotBuilder.Clone(Params as NodeParamCompositeModule);
            LoadProcessItems();
            UpdatePreviewFromParam(_pendingParam);
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
            }
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
                return;
            }

            textBoxModuleName.Text = string.IsNullOrWhiteSpace(param.ModuleName)
                ? param.SourceProcessName
                : param.ModuleName;
            textBoxNodeCount.Text = (param.NodeInfos == null ? 0 : param.NodeInfos.Count).ToString();
            textBoxConnectionCount.Text = (param.ConnectionInfos == null ? 0 : param.ConnectionInfos.Count).ToString();
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
    }
}
