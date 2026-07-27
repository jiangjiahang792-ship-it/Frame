using Logger;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 
    /// 【作者】: pengmude
    /// 【类名】: NodeSubscription
    /// 【创建时间】: 2024年8月22日
    /// 【描述】: 这是一个自定义控件类，用于订阅某个流程Process中的节点的运行结果。
    /// 【使用教程】：界面拖出本控件到节点参数设置界面后，还需要在节点参数设置界面类中的SetNodeBelong中
    /// 初始化,如"nodeSubscription1.Init(node)"；最后需要在节点类的构造函数设置“节点参数设置窗口”
    /// 所属的节点，如HTAI节点类：ParamForm.SetNodeBelong(this)，这样即可订阅到图上游节点结果。
    ///【注意事项】: 节点仅支持订阅当前流程图中能够连接到当前节点的上游节点结果。
    ///
    /// </summary>
    public partial class NodeSubscription : UserControl
    {
        /// <summary>
        /// 所属节点
        /// </summary>
        NodeBase _node = null;
        /// <summary>
        /// 选择的节点
        /// </summary>
        NodeBase _selectedNode = null;

        string _text1;
        string _text2;
        /// <summary>
        /// 正在由代码刷新节点下拉框，避免 SelectedIndexChanged 反复触发订阅恢复逻辑。
        /// </summary>
        private bool _isUpdatingNodeCombo;
        /// <summary>
        /// 调用方声明的统一输入契约，用于按数据类别、数量形态和 CLR 类型筛选结果。
        /// </summary>
        private SubscriptionInputContract _inputContract = SubscriptionInputContract.AnyVisible();

        /// <summary>
        /// 正在由代码刷新结果下拉框，避免刷新过程覆盖旧方案保存路径。
        /// </summary>
        private bool _isUpdatingResultCombo;

        /// <summary>
        /// 当前结果下拉项对应的端口描述。
        /// </summary>
        private SubscriptionOutputDescriptor _selectedOutput;

        public NodeSubscription()
        {
            InitializeComponent();
            NodeBase.RefreshNodeSubControl += RenameChangeEvent;
            NodeBase.OutputDefinitionChanged += NodeBase_OutputDefinitionChanged;
        }

        /// <summary>
        /// 上游节点输出定义变化后立即刷新结果下拉框，不等待节点再次运行。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="sourceNode">输出定义发生变化的节点。</param>
        private void NodeBase_OutputDefinitionChanged(object sender, NodeBase sourceNode)
        {
            if (sourceNode == null || _selectedNode == null || sourceNode.ID != _selectedNode.ID)
                return;
            if (sourceNode.Process != _selectedNode.Process || sourceNode.Result == null)
                return;

            InitProperties(_selectedNode, _text2);
        }

        /// <summary>
        /// 只刷新一次
        /// </summary>
        private void RenameChangeEvent(object sender, RenameResult e)
        {
            if (_node == null || _node.Process == null)
                return;

            string oldText = $"{e.NodeId}.{e.NodeNameOld}";
            string newText = $"{e.NodeId}.{e.NodeNameNew}";
            if (IsNodeTextForNode(_text1, e.NodeId))
                _text1 = newText;

            // 临时断线时也要同步旧订阅文本，避免节点改名后无法自动恢复。
            bool isUpstream = _node.Process.GetUpstreamNodes(_node).Exists(node => node.ID == e.NodeId);
            UpdateComboBoxItem(oldText, newText, isUpstream);
            if (isUpstream)
            {
                SetSelectedNode(_text1, _text2);
            }
        }

        public void UpdateComboBoxItem(string oldText, string newText)
        {
            UpdateComboBoxItem(oldText, newText, true);
        }

        /// <summary>
        /// 更新订阅节点下拉框中的显示文本，支持临时断线状态不输出误报日志。
        /// </summary>
        /// <param name="oldText">旧节点文本。</param>
        /// <param name="newText">新节点文本。</param>
        /// <param name="logWhenMissing">找不到旧文本时是否写日志。</param>
        private void UpdateComboBoxItem(string oldText, string newText, bool logWhenMissing)
        {
            // 检查 comboBox1.Items 是否存在 oldText
            int index = comboBox1.Items.IndexOf(oldText);

            if (index != -1)
            {
                // 存在 oldText，更新为 newText
                comboBox1.Items[index] = newText;

                // 检查当前选中的文本是否等于 oldText
                if (comboBox1.SelectedItem?.ToString() == oldText)
                {
                    // 更新选中的项
                    comboBox1.SelectedItem = newText;
                }
            }
            else if (logWhenMissing)
            {
                // 不存在 oldText，输出提示信息
                LogHelper.AddLog(MsgLevel.Warn, $"重命名节点名称时，节点“{_node.NodeName}”的订阅控件找不到该节点原名称“{oldText}”！", true);
            }
        }

        public void Init(NodeBase node)
        {
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged -= Process_ConnectionsChanged;

            _node = node;
            InitNodeIdList();
            if (_node != null && _node.Process != null)
                _node.Process.ConnectionsChanged += Process_ConnectionsChanged;

            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
        }

        /// <summary>
        /// 声明此订阅控件期望读取的结果值类型，并刷新当前结果属性列表。
        /// </summary>
        /// <typeparam name="T">期望读取的结果值类型。</typeparam>
        public void SetExpectedValueType<T>()
        {
            SetExpectedValueType(typeof(T));
        }

        /// <summary>
        /// 声明此订阅控件期望读取的结果值类型，并刷新当前结果属性列表。
        /// </summary>
        /// <param name="expectedValueType">期望读取的结果值类型；为空时取消类型筛选。</param>
        public void SetExpectedValueType(Type expectedValueType)
        {
            SetInputContract(SubscriptionInputContract.ForType(expectedValueType));
        }

        /// <summary>
        /// 声明此订阅控件接受的统一输入契约，并刷新当前结果属性列表。
        /// </summary>
        /// <param name="inputContract">输入契约；为空时使用通用可见结果契约。</param>
        public void SetInputContract(SubscriptionInputContract inputContract)
        {
            _inputContract = inputContract ?? SubscriptionInputContract.AnyVisible();
            if (_selectedNode != null)
                InitProperties(_selectedNode, _text2);
        }

        private void Process_ConnectionsChanged(object sender, EventArgs e)
        {
            RefreshNodeIdList(true);
        }

        /// <summary>
        /// 订阅处理每当有节点删除的逻辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodeBase_NodeDeletedEvent(object sender, NodeBase e)
        {
            if (e == null)
                return;

            // 真正删除订阅源时才清空；普通断线由 RefreshNodeIdList 保留待恢复订阅。
            if ((_selectedNode != null && e.ID == _selectedNode.ID) || IsNodeTextForNode(_text1, e.ID))
            {
                ClearSelectedNode();
                return;
            }

            // 更新节点列表
            comboBox1.Items.Remove($"{e.ID}.{e.NodeName}");
            if (comboBox1.Items.Count == 0 && string.IsNullOrEmpty(_text1))
                ClearSelectedNode();
        }

        /// <summary>
        /// 设置节点Id下拉框
        /// </summary>
        /// <param name="ids"></param>
        private void InitNodeIdList()
        {
            RefreshNodeIdList(false);
        }

        private void RefreshNodeIdList(bool preserveSelection)
        {
            if (_node == null || _node.Process == null)
                return;

            string selectedNodeText = preserveSelection ? _text1 : string.Empty;
            string selectedResultText = _text2;
            comboBox1.Items.Clear();

            List<NodeBase> upstreamNodes = _node.Process.GetUpstreamNodes(_node);
            foreach (NodeBase node in upstreamNodes)
                comboBox1.Items.Add(GetNodeText(node));

            if (!selectedNodeText.IsNullOrEmpty())
            {
                int selectedIndex = comboBox1.Items.IndexOf(selectedNodeText);
                if (selectedIndex >= 0)
                {
                    SetNodeComboSelectedIndex(selectedIndex);
                    SetSelectedNode(selectedNodeText, selectedResultText);
                    return;
                }

                // 原订阅节点只是暂时不在图上游时，保留订阅文本，重新连线后自动恢复。
                PreserveDisconnectedSelection(selectedNodeText, selectedResultText, true);
                return;
            }

            if (comboBox1.Items.Count == 0)
            {
                ClearSelectedNode();
                return;
            }

            SetNodeComboSelectedIndex(0);
            _text1 = (string)comboBox1.Items[0];
            SetSelectedNode(_text1, string.Empty, true);
        }

        /// <summary>
        /// 节点ID下拉框选中改变事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingNodeCombo)
                return;

            SetSelectedNode(comboBox1.Text, _text2);
        }

        /// <summary>
        /// 初始化节点的属性到下拉框中
        /// </summary>
        /// <param name="nodeBase"></param>
        private void InitProperties(NodeBase nodeBase, string text2 = null, bool needRefresh = false)
        {
            if (nodeBase == null)
                return;

            IReadOnlyList<SubscriptionOutputDescriptor> outputs = SubscriptionPortCatalog.GetOutputs(
                nodeBase,
                _inputContract,
                toolStripMenuItemShowAdvancedResults.Checked,
                text2);

            _isUpdatingResultCombo = true;
            comboBox2.BeginUpdate();
            try
            {
                comboBox2.Items.Clear();
                foreach (SubscriptionOutputDescriptor output in outputs)
                    comboBox2.Items.Add(new SubscriptionSelectionItem(output));

                if (comboBox2.Items.Count == 0)
                {
                    comboBox2.SelectedIndex = -1;
                    comboBox2.Text = string.Empty;
                    _selectedOutput = null;
                    if (needRefresh || text2 == null)
                        _text2 = string.Empty;
                    return;
                }

                int selectedIndex = -1;
                if (!needRefresh && text2 != null)
                    selectedIndex = FindResultItemIndex(text2);
                if (selectedIndex < 0 && (needRefresh || text2 == null))
                    selectedIndex = 0;

                if (selectedIndex >= 0)
                {
                    comboBox2.SelectedIndex = selectedIndex;
                    ApplySelectedResultItem();
                }
                else
                {
                    // 已保存路径无法匹配时保持原文本，绝不能静默跳到第一项。
                    _text2 = text2 ?? string.Empty;
                    _selectedOutput = null;
                    comboBox2.SelectedIndex = -1;
                }
            }
            finally
            {
                comboBox2.EndUpdate();
                _isUpdatingResultCombo = false;
            }
        }
        /// <summary>
        /// 获取节点对应结果的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetValue<T>()
        {
            if (_selectedNode == null || _text2.IsNullOrEmpty())
                throw new Exception($"节点无法获取订阅的值!");

            EnsureSelectedNodeFresh();

            var propertyInfo = _selectedNode.Result.GetType().GetProperties().ToList().Find(item => item.DisplayName() == _text2);
            if (propertyInfo != null && propertyInfo.CanRead)
            {
                var value = propertyInfo.GetValue(_selectedNode.Result);
                return ConvertSubscriptionValue<T>(value);
            }

            object dynamicValue;
            if (DynamicResultVariableResolver.TryGetValue(_selectedNode.Result, _text2, out dynamicValue))
            {
                return ConvertSubscriptionValue<T>(dynamicValue);
            }
            else
            {
                throw new Exception($"节点({_selectedNode.ID}.{_selectedNode.NodeName})获取订阅的{_text2}值失败!");
            }
        }

        public NodeBase GetSelectedNode()
        {
            if (_selectedNode == null)
                throw new Exception($"节点无法获取订阅的上游节点!");

            EnsureSelectedNodeFresh();
            return _selectedNode;
        }

        public T GetSelectedResult<T>() where T : class
        {
            NodeBase selectedNode = GetSelectedNode();
            T result = selectedNode.Result as T;
            if (result == null)
                throw new InvalidCastException($"节点({selectedNode.ID}.{selectedNode.NodeName})运行结果类型不匹配，期望类型为{typeof(T).Name}，实际类型为{selectedNode.Result?.GetType().Name ?? "null"}!");

            return result;
        }

        public NodeType GetNodeType()
        {
            if (_selectedNode == null)
                throw new Exception("订阅节点的类型未知！");
            return _selectedNode.NodeType;
        }

        public string GetText1()
        {
            return _text1;
        }

        public string GetText2()
        {
            return _text2;
        }

        public void HideText2()
        {
            label2.Visible = false;
            comboBox2.Visible = false;
        }

        public void ClearText()
        {
            ClearSelectedNode();
        }

        //反序列化使用
        public void SetText(string text1, string text2)
        {
            _text1 = text1;
            _text2 = text2;
            int index = comboBox1.Items.IndexOf(text1);
            if (index >= 0)
            {
                SetNodeComboSelectedIndex(index);
                SetSelectedNode(text1, text2);
                return;
            }

            PreserveDisconnectedSelection(text1, text2, true);
        }

        private void SetSelectedNode(string nodeText, string resultText, bool needRefresh = false)
        {
            if (_node == null || _node.Process == null || nodeText.IsNullOrEmpty())
                return;

            foreach (NodeBase node in _node.Process.GetUpstreamNodes(_node))
            {
                if (GetNodeText(node) != nodeText)
                    continue;

                _selectedNode = node;
                _text1 = nodeText;
                InitProperties(node, resultText, needRefresh);
                return;
            }

            PreserveDisconnectedSelection(nodeText, resultText, true);
        }

        private void ClearSelectedNode()
        {
            _selectedNode = null;
            _selectedOutput = null;
            _text1 = string.Empty;
            _text2 = string.Empty;
            SetNodeComboSelectedIndex(-1);
            comboBox2.Items.Clear();
            comboBox2.Text = string.Empty;
        }

        /// <summary>
        /// 保留临时断线的订阅文本，使后续重新连线后可以自动恢复。
        /// </summary>
        /// <param name="nodeText">原订阅节点文本。</param>
        /// <param name="resultText">原订阅结果文本。</param>
        /// <param name="showPendingText">是否在下拉框中显示待恢复文本。</param>
        private void PreserveDisconnectedSelection(string nodeText, string resultText, bool showPendingText)
        {
            _selectedNode = null;
            _text1 = nodeText ?? string.Empty;
            _text2 = resultText ?? string.Empty;

            if (showPendingText && !string.IsNullOrWhiteSpace(_text1) && !comboBox1.Items.Contains(_text1))
                comboBox1.Items.Add(_text1);

            int pendingIndex = comboBox1.Items.IndexOf(_text1);
            SetNodeComboSelectedIndex(pendingIndex);

            comboBox2.Items.Clear();
            comboBox2.Text = string.Empty;
            if (!string.IsNullOrWhiteSpace(_text2))
            {
                comboBox2.Items.Add(new SubscriptionSelectionItem(new SubscriptionOutputDescriptor
                {
                    PropertyPath = _text2,
                    DisplayName = _text2,
                    ValueType = typeof(object),
                    Category = SubscriptionDataCategory.Unknown,
                    Multiplicity = SubscriptionValueMultiplicity.Single,
                    Visibility = SubscriptionOutputVisibility.Hidden,
                    IsMissing = true
                }));
                comboBox2.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 由代码设置节点下拉框选择项，避免触发重复订阅刷新。
        /// </summary>
        /// <param name="selectedIndex">待选中的索引。</param>
        private void SetNodeComboSelectedIndex(int selectedIndex)
        {
            _isUpdatingNodeCombo = true;
            try
            {
                comboBox1.SelectedIndex = selectedIndex;
            }
            finally
            {
                _isUpdatingNodeCombo = false;
            }
        }

        /// <summary>
        /// 判断订阅文本是否指向指定节点编号。
        /// </summary>
        /// <param name="nodeText">订阅节点文本，格式为“编号.名称”。</param>
        /// <param name="nodeId">节点编号。</param>
        /// <returns>订阅文本指向指定节点时返回 true。</returns>
        private static bool IsNodeTextForNode(string nodeText, int nodeId)
        {
            int parsedNodeId;
            return TryGetNodeIdFromText(nodeText, out parsedNodeId) && parsedNodeId == nodeId;
        }

        /// <summary>
        /// 从订阅节点文本中解析节点编号。
        /// </summary>
        /// <param name="nodeText">订阅节点文本，格式为“编号.名称”。</param>
        /// <param name="nodeId">解析出的节点编号。</param>
        /// <returns>解析成功时返回 true。</returns>
        private static bool TryGetNodeIdFromText(string nodeText, out int nodeId)
        {
            nodeId = 0;
            if (string.IsNullOrWhiteSpace(nodeText))
                return false;

            int dotIndex = nodeText.IndexOf('.');
            if (dotIndex <= 0)
                return false;

            return int.TryParse(nodeText.Substring(0, dotIndex), out nodeId);
        }

        private static string GetNodeText(NodeBase node)
        {
            return $"{node.ID}.{node.NodeName}";
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdatingResultCombo)
                return;

            ApplySelectedResultItem();
        }

        /// <summary>
        /// 用户勾选或取消“显示高级结果”后刷新结果候选并保持当前订阅路径。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void toolStripMenuItemShowAdvancedResults_CheckedChanged(object sender, EventArgs e)
        {
            if (_selectedNode != null)
                InitProperties(_selectedNode, _text2);
        }

        /// <summary>
        /// 将订阅值转换为调用方期望类型。
        /// </summary>
        private T ConvertSubscriptionValue<T>(object value)
        {
            object converted = SubscriptionTypeCompatibility.ConvertValue(
                value,
                typeof(T),
                _inputContract == null
                    ? NumericConversionMode.Checked
                    : _inputContract.NumericConversionMode);
            return converted == null ? default(T) : (T)converted;
        }

        /// <summary>
        /// 在结果下拉框中查找与已保存路径对应的项目。
        /// </summary>
        /// <param name="persistedText">旧方案保存的结果显示名或动态变量路径。</param>
        /// <returns>对应下拉项索引；找不到时返回 -1。</returns>
        private int FindResultItemIndex(string persistedText)
        {
            for (int index = 0; index < comboBox2.Items.Count; index++)
            {
                SubscriptionSelectionItem item = comboBox2.Items[index] as SubscriptionSelectionItem;
                if (item != null && item.Matches(persistedText))
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// 把当前界面结果项写回稳定持久化文本和端口描述。
        /// </summary>
        private void ApplySelectedResultItem()
        {
            SubscriptionSelectionItem item = comboBox2.SelectedItem as SubscriptionSelectionItem;
            if (item == null)
                return;

            _text2 = item.PersistedText;
            _selectedOutput = item.Descriptor;
        }

        private void EnsureSelectedNodeFresh()
        {
            if (_selectedNode == null)
                return;

            if (_node != null &&
                _node.Process != null &&
                _node.Process.IsRuning &&
                !_selectedNode.HasSuccessfulResultForRun(_node.Process.CurrentRunId))
            {
                throw new Exception($"节点({_selectedNode.ID}.{_selectedNode.NodeName})本次流程未成功运行，不能使用上次运行结果!");
            }
        }

        /// <summary>
        /// 把端口持久化文本与带兼容状态的界面显示文字分离。
        /// </summary>
        private sealed class SubscriptionSelectionItem
        {
            /// <summary>
            /// 初始化结果下拉项。
            /// </summary>
            /// <param name="descriptor">输出端口描述。</param>
            public SubscriptionSelectionItem(SubscriptionOutputDescriptor descriptor)
            {
                Descriptor = descriptor ?? throw new ArgumentNullException("descriptor");
                PersistedText = descriptor.DisplayName ?? descriptor.PropertyPath ?? string.Empty;
            }

            /// <summary>获取输出端口描述。</summary>
            public SubscriptionOutputDescriptor Descriptor { get; private set; }

            /// <summary>获取写入旧方案兼容字段的原始文本。</summary>
            public string PersistedText { get; private set; }

            /// <summary>
            /// 判断下拉项是否对应指定的旧方案文本。
            /// </summary>
            /// <param name="text">保存的显示名、属性路径或动态变量路径。</param>
            /// <returns>对应时返回 true。</returns>
            public bool Matches(string text)
            {
                if (string.Equals(PersistedText, text, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Descriptor.PropertyPath, text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return Descriptor.IsDynamic && string.Equals(
                    DynamicResultVariableResolver.ExtractVariableName(PersistedText),
                    DynamicResultVariableResolver.ExtractVariableName(text),
                    StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// 返回带兼容或缺失状态的用户界面文本。
            /// </summary>
            /// <returns>结果下拉框显示文本。</returns>
            public override string ToString()
            {
                if (Descriptor.IsMissing)
                    return PersistedText + "（结果不存在）";
                if (Descriptor.IsLegacySelection)
                    return PersistedText + "（兼容订阅）";
                return PersistedText;
            }
        }
    }
}
