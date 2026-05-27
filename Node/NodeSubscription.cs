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

        public NodeSubscription()
        {
            InitializeComponent();
            NodeBase.RefreshNodeSubControl += RenameChangeEvent;
        }

        /// <summary>
        /// 只刷新一次
        /// </summary>
        private void RenameChangeEvent(object sender, RenameResult e)
        {
            if (_node == null || _node.Process == null)
                return;

            // 只有重命名节点仍是图上游节点时，才需要更新订阅候选名称。
            if (_node.Process.GetUpstreamNodes(_node).Exists(node => node.ID == e.NodeId))
            {
                string oldText = $"{e.NodeId}.{e.NodeNameOld}";
                string newText = $"{e.NodeId}.{e.NodeNameNew}";
                if (_text1 == oldText)
                    _text1 = newText;

                UpdateComboBoxItem(oldText, newText);
            }
        }

        public void UpdateComboBoxItem(string oldText, string newText)
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
            else
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
            // 移除的节点是当前实例的订阅节点，要置空
            if (_selectedNode != null && e.ID == _selectedNode.ID)
                ClearSelectedNode();

            // 更新节点列表
            comboBox1.Items.Remove($"{e.ID}.{e.NodeName}");
            if (comboBox1.Items.Count == 0)
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

            if (comboBox1.Items.Count == 0)
            {
                ClearSelectedNode();
                return;
            }

            if (!selectedNodeText.IsNullOrEmpty())
            {
                int selectedIndex = comboBox1.Items.IndexOf(selectedNodeText);
                if (selectedIndex >= 0)
                {
                    comboBox1.SelectedIndex = selectedIndex;
                    SetSelectedNode(selectedNodeText, selectedResultText);
                    return;
                }

                // 原订阅节点断开了上游关系，保留候选列表但清空当前订阅。
                ClearSelectedNode();
                return;
            }

            comboBox1.SelectedIndex = 0;
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
            SetSelectedNode(comboBox1.Text, _text2);
        }

        /// <summary>
        /// 初始化节点的属性到下拉框中
        /// </summary>
        /// <param name="nodeBase"></param>
        private void InitProperties(NodeBase nodeBase, string text2 = null, bool needRefresh = false)
        {
            comboBox2.Items.Clear();
            Type nodeResult = nodeBase.Result.GetType();
            var properties = nodeResult.GetProperties();

            foreach (var property in properties) //遍历属性
            {
                var displayNameAttribute = property.GetCustomAttribute<DisplayNameAttribute>();
                if (displayNameAttribute != null)
                {
                    comboBox2.Items.Add(displayNameAttribute.DisplayName);
                }
            }

            foreach (string variableName in DynamicResultVariableResolver.GetVariableNames(nodeBase))
            {
                string displayName = DynamicResultVariableResolver.ToDisplayName(variableName);
                if (!comboBox2.Items.Contains(displayName))
                    comboBox2.Items.Add(displayName);
            }

            if (comboBox2.Items.Count > 0)
            {
                if (needRefresh || text2 == null)
                {
                    comboBox2.SelectedIndex = 0;
                    _text2 = comboBox2.Text;
                }
                else
                {
                    int index1 = comboBox2.Items.IndexOf(text2);
                    if (index1 == -1 && !string.IsNullOrWhiteSpace(text2))
                        index1 = comboBox2.Items.IndexOf(DynamicResultVariableResolver.ToDisplayName(text2));
                    comboBox2.SelectedIndex = index1 == -1 ? 0 : index1;
                    _text2 = text2;
                }
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
                comboBox1.SelectedIndex = index;
                SetSelectedNode(text1, text2);
            }
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
        }

        private void ClearSelectedNode()
        {
            _selectedNode = null;
            _text1 = string.Empty;
            _text2 = string.Empty;
            comboBox1.SelectedIndex = -1;
            comboBox2.Items.Clear();
            comboBox2.Text = string.Empty;
        }

        private static string GetNodeText(NodeBase node)
        {
            return $"{node.ID}.{node.NodeName}";
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            _text2 = comboBox2.Text;
        }

        /// <summary>
        /// 将订阅值转换为调用方期望类型。
        /// </summary>
        private static T ConvertSubscriptionValue<T>(object value)
        {
            if (value == null)
                return default(T);
            if (value is T)
                return (T)value;

            Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(object))
                return (T)value;

            try
            {
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                throw new InvalidCastException($"订阅值类型不匹配，期望类型为{typeof(T).Name}，实际类型为{value.GetType().Name}!");
            }
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
    }
}
