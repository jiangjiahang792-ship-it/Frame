using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// ROI结果绘制2参数窗体，提供输入图像、颜色规则和绘制项的简化配置界面。
    /// </summary>
    public partial class NodeParamFormResultOverlayDraw2 : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前参数窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 当前编辑中的绘制项集合。
        /// </summary>
        private readonly List<ResultOverlayDraw2Item> _items = new List<ResultOverlayDraw2Item>();

        /// <summary>
        /// 当前编辑中的颜色判定规则集合。
        /// </summary>
        private readonly List<ResultOverlayDraw2ColorRule> _rules = new List<ResultOverlayDraw2ColorRule>();

        /// <summary>
        /// 当前选中的绘制项索引。
        /// </summary>
        private int _selectedItemIndex = -1;

        /// <summary>
        /// 控件同步标志，避免加载界面时触发保存。
        /// </summary>
        private bool _syncing;

        /// <summary>
        /// 初始化ROI结果绘制2参数窗体。
        /// </summary>
        public NodeParamFormResultOverlayDraw2()
        {
            InitializeComponent();
            InitCombos();
            RefreshItemsGrid();
            RefreshRulesGrid();
        }

        /// <summary>
        /// 当前窗体保存的节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗体所属节点并初始化订阅控件。
        /// </summary>
        /// <param name="node">所属节点。</param>
        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            _node = node;
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionSource.Init(node);
            nodeSubscriptionRule.Init(node);
            RefreshRuleConditionItems("整体结果");
        }

        /// <summary>
        /// 将已保存参数加载到界面控件。
        /// </summary>
        public void SetParam2Form()
        {
            NodeParamResultOverlayDraw2 param = Params as NodeParamResultOverlayDraw2;
            if (param == null)
                return;

            _syncing = true;
            nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
            panelOkColor.BackColor = param.OkColor;
            panelNgColor.BackColor = param.NgColor;
            SetRuleMode(param.RuleMode);
            _syncing = false;

            _items.Clear();
            if (param.Items != null)
                _items.AddRange(param.Items.Select(item => item.Clone()));

            _rules.Clear();
            if (param.ColorRules != null)
                _rules.AddRange(param.ColorRules.Select(rule => rule.Clone()));

            if (_rules.Count > 0)
            {
                ResultOverlayDraw2ColorRule firstRule = _rules[0];
                nodeSubscriptionRule.SetText(firstRule.SourceText1, firstRule.SourceText2);
                RefreshRuleConditionItems(firstRule.ConditionName);
            }

            RefreshItemsGrid();
            RefreshRulesGrid();
            SelectItem(_items.Count > 0 ? 0 : -1);
        }

        /// <summary>
        /// 初始化绘制类型和判定方式下拉框。
        /// </summary>
        private void InitCombos()
        {
            comboBoxItemType.DisplayMember = "Text";
            comboBoxItemType.ValueMember = "Value";
            comboBoxItemType.DataSource = new List<EnumOption<ResultOverlayDraw2ItemType>>
            {
                new EnumOption<ResultOverlayDraw2ItemType>("文本", ResultOverlayDraw2ItemType.Text),
                new EnumOption<ResultOverlayDraw2ItemType>("线", ResultOverlayDraw2ItemType.Line),
                new EnumOption<ResultOverlayDraw2ItemType>("矩形", ResultOverlayDraw2ItemType.Rectangle),
                new EnumOption<ResultOverlayDraw2ItemType>("区域/轮廓", ResultOverlayDraw2ItemType.Region)
            };

            comboBoxRuleMode.DisplayMember = "Text";
            comboBoxRuleMode.ValueMember = "Value";
            comboBoxRuleMode.DataSource = new List<EnumOption<ResultOverlayColorRuleMode>>
            {
                new EnumOption<ResultOverlayColorRuleMode>("全部为真=OK", ResultOverlayColorRuleMode.AllTrue),
                new EnumOption<ResultOverlayColorRuleMode>("任一为真=OK", ResultOverlayColorRuleMode.AnyTrue)
            };

            comboBoxTextPosition.DataSource = Enum.GetValues(typeof(DisplayTextPosition));
            comboBoxTextCoordinateMode.DisplayMember = "Text";
            comboBoxTextCoordinateMode.ValueMember = "Value";
            comboBoxTextCoordinateMode.DataSource = new List<EnumOption<DisplayTextCoordinateMode>>
            {
                new EnumOption<DisplayTextCoordinateMode>("图像坐标系", DisplayTextCoordinateMode.Image),
                new EnumOption<DisplayTextCoordinateMode>("控件坐标系", DisplayTextCoordinateMode.Control)
            };
            comboBoxRuleCondition.Items.Clear();
            comboBoxRuleCondition.Items.Add("整体结果");
            comboBoxRuleCondition.SelectedIndex = 0;
        }

        /// <summary>
        /// 添加文本绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddText_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Text);
        }

        /// <summary>
        /// 添加线段绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddLine_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Line);
        }

        /// <summary>
        /// 添加矩形绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddRectangle_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Rectangle);
        }

        /// <summary>
        /// 添加区域绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddRegion_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Region);
        }

        /// <summary>
        /// 删除当前选中的绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonDeleteItem_Click(object sender, EventArgs e)
        {
            int index = GetItemSelectedIndex();
            if (index < 0 || index >= _items.Count)
                return;

            _items.RemoveAt(index);
            RefreshItemsGrid();
            SelectItem(Math.Min(index, _items.Count - 1));
        }

        /// <summary>
        /// 选择OK显示颜色。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonChooseOkColor_Click(object sender, EventArgs e)
        {
            ChoosePanelColor(panelOkColor);
        }

        /// <summary>
        /// 选择NG显示颜色。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonChooseNgColor_Click(object sender, EventArgs e)
        {
            ChoosePanelColor(panelNgColor);
        }

        /// <summary>
        /// 刷新当前颜色规则订阅源中的多条件项列表。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRefreshRuleCondition_Click(object sender, EventArgs e)
        {
            RefreshRuleConditionItems(GetRuleConditionName());
        }

        /// <summary>
        /// 将当前订阅选择添加为颜色判定规则。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddRule_Click(object sender, EventArgs e)
        {
            string sourceText1 = nodeSubscriptionRule.GetText1();
            string sourceText2 = nodeSubscriptionRule.GetText2();
            if (string.IsNullOrWhiteSpace(sourceText1) || string.IsNullOrWhiteSpace(sourceText2))
            {
                MessageBoxTD.Show("请先选择一个布尔订阅结果！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _rules.Add(new ResultOverlayDraw2ColorRule
            {
                Enabled = true,
                SourceText1 = sourceText1,
                SourceText2 = sourceText2,
                ConditionName = GetRuleConditionName()
            });

            RefreshRulesGrid();
        }

        /// <summary>
        /// 删除当前选中的颜色判定规则。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonDeleteRule_Click(object sender, EventArgs e)
        {
            int index = GetRuleSelectedIndex();
            if (index < 0 || index >= _rules.Count)
                return;

            _rules.RemoveAt(index);
            RefreshRulesGrid();
        }

        /// <summary>
        /// 预览当前绘制效果。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonPreview_Click(object sender, EventArgs e)
        {
            if (!SaveParams(false))
                return;

            try
            {
                OutputImage outputImage;
                AlgorithmResult displayResult;
                bool isOk;
                string diagnostics;
                ResultOverlayDraw2Builder.Build(_node, (NodeParamResultOverlayDraw2)Params, out outputImage, out displayResult, out isOk, out diagnostics);
                if (outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || outputImage.Bitmaps[0] == null)
                    throw new Exception("预览图像为空！");

                labelPreviewState.Text = isOk ? "当前颜色：OK" : "当前颜色：NG";
                labelPreviewState.ForeColor = isOk ? panelOkColor.BackColor : panelNgColor.BackColor;
                showImageControl1.SetImage(BitmapConverter.ToBitmap(outputImage.Bitmaps[0]), displayResult);
                showImageControl1.ShowFit();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("预览失败：" + ex.Message, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 保存参数并关闭窗口。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams(true))
                Hide();
        }

        /// <summary>
        /// 绘制项表格选中项变化时同步右侧编辑区域。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void dataGridViewItems_SelectionChanged(object sender, EventArgs e)
        {
            if (_syncing)
                return;

            SaveCurrentItemFromControls();
            SelectItem(GetItemSelectedIndex());
        }

        /// <summary>
        /// 绘制项编辑控件变化时保存到当前绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void ItemControl_ValueChanged(object sender, EventArgs e)
        {
            if (_syncing)
                return;

            SaveCurrentItemFromControls();
            RefreshItemsGrid();
            UpdateControlState();
        }

        /// <summary>
        /// 创建并添加指定类型的绘制项。
        /// </summary>
        /// <param name="type">绘制项类型。</param>
        private void AddItem(ResultOverlayDraw2ItemType type)
        {
            SaveCurrentItemFromControls();

            ResultOverlayDraw2Item item = new ResultOverlayDraw2Item
            {
                ItemType = type,
                Name = GetItemTypeText(type),
                UseManualText = type == ResultOverlayDraw2ItemType.Text,
                ManualText = type == ResultOverlayDraw2ItemType.Text ? "OK" : string.Empty
            };

            _items.Add(item);
            RefreshItemsGrid();
            SelectItem(_items.Count - 1);
        }

        /// <summary>
        /// 保存界面参数。
        /// </summary>
        /// <param name="showMessage">校验失败时是否弹窗提示。</param>
        /// <returns>保存成功返回 true。</returns>
        private bool SaveParams(bool showMessage)
        {
            SaveCurrentItemFromControls();

            if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
            {
                if (showMessage)
                    MessageBoxTD.Show("请先订阅输入图像！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            foreach (ResultOverlayDraw2Item item in _items.Where(i => i.Enabled))
            {
                bool needSource = item.ItemType != ResultOverlayDraw2ItemType.Text || !item.UseManualText;
                if (needSource && string.IsNullOrWhiteSpace(item.SourceText1))
                {
                    if (showMessage)
                        MessageBoxTD.Show($"绘制项“{item.Name}”还没有选择订阅源！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            foreach (ResultOverlayDraw2ColorRule rule in _rules.Where(i => i.Enabled))
            {
                if (string.IsNullOrWhiteSpace(rule.SourceText1) || string.IsNullOrWhiteSpace(rule.SourceText2))
                {
                    if (showMessage)
                        MessageBoxTD.Show("颜色判定规则中存在空订阅，请删除后重新添加！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            Params = new NodeParamResultOverlayDraw2
            {
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                OkColor = panelOkColor.BackColor,
                NgColor = panelNgColor.BackColor,
                RuleMode = GetRuleMode(),
                Items = _items.Select(item => item.Clone()).ToList(),
                ColorRules = _rules.Select(rule => rule.Clone()).ToList()
            };
            return true;
        }

        /// <summary>
        /// 刷新绘制项列表。
        /// </summary>
        private void RefreshItemsGrid()
        {
            _syncing = true;
            int selected = GetItemSelectedIndex();
            dataGridViewItems.Rows.Clear();

            foreach (ResultOverlayDraw2Item item in _items)
            {
                dataGridViewItems.Rows.Add(
                    item.Enabled ? "是" : "否",
                    GetItemTypeText(item.ItemType),
                    item.Name,
                    item.SourceDisplay);
            }

            if (selected >= 0 && selected < dataGridViewItems.Rows.Count)
                dataGridViewItems.Rows[selected].Selected = true;

            _syncing = false;
        }

        /// <summary>
        /// 刷新颜色判定规则列表。
        /// </summary>
        private void RefreshRulesGrid()
        {
            dataGridViewRules.Rows.Clear();
            foreach (ResultOverlayDraw2ColorRule rule in _rules)
            {
                dataGridViewRules.Rows.Add(
                    rule.Enabled ? "是" : "否",
                    string.IsNullOrWhiteSpace(rule.SourceText2) ? rule.SourceText1 : rule.SourceText1 + " / " + rule.SourceText2,
                    string.IsNullOrWhiteSpace(rule.ConditionName) ? "整体结果" : rule.ConditionName);
            }
        }

        /// <summary>
        /// 选中指定绘制项并加载到编辑区。
        /// </summary>
        /// <param name="index">绘制项索引。</param>
        private void SelectItem(int index)
        {
            _syncing = true;
            _selectedItemIndex = index;

            if (index >= 0 && index < _items.Count)
            {
                if (dataGridViewItems.Rows.Count > index)
                {
                    dataGridViewItems.ClearSelection();
                    dataGridViewItems.Rows[index].Selected = true;
                }

                LoadItemToControls(_items[index]);
            }
            else
            {
                ClearItemControls();
            }

            _syncing = false;
            UpdateControlState();
        }

        /// <summary>
        /// 将绘制项加载到编辑控件。
        /// </summary>
        /// <param name="item">绘制项。</param>
        private void LoadItemToControls(ResultOverlayDraw2Item item)
        {
            checkBoxEnabled.Checked = item.Enabled;
            SetItemType(item.ItemType);
            textBoxName.Text = item.Name ?? string.Empty;
            checkBoxManualText.Checked = item.UseManualText;
            textBoxManualText.Text = item.ManualText ?? string.Empty;
            textBoxTextPrefix.Text = item.TextPrefix ?? string.Empty;
            numericFontSize.Value = ClampNumeric(numericFontSize, item.FontSize);
            numericLineWidth.Value = ClampNumeric(numericLineWidth, item.LineWidth);
            numericMargin.Value = ClampNumeric(numericMargin, item.TextMargin);
            comboBoxTextPosition.SelectedItem = item.TextPosition;
            SetTextCoordinateMode(item.TextCoordinateMode);

            if (string.IsNullOrWhiteSpace(item.SourceText1))
                nodeSubscriptionSource.ClearText();
            else
                nodeSubscriptionSource.SetText(item.SourceText1, item.SourceText2);
        }

        /// <summary>
        /// 清空绘制项编辑控件。
        /// </summary>
        private void ClearItemControls()
        {
            checkBoxEnabled.Checked = false;
            SetItemType(ResultOverlayDraw2ItemType.Text);
            textBoxName.Text = string.Empty;
            checkBoxManualText.Checked = true;
            textBoxManualText.Text = string.Empty;
            textBoxTextPrefix.Text = string.Empty;
            numericFontSize.Value = 18;
            numericLineWidth.Value = 2;
            numericMargin.Value = 10;
            comboBoxTextPosition.SelectedItem = DisplayTextPosition.TopLeft;
            SetTextCoordinateMode(DisplayTextCoordinateMode.Image);
            nodeSubscriptionSource.ClearText();
        }

        /// <summary>
        /// 将当前编辑控件保存到选中绘制项。
        /// </summary>
        private void SaveCurrentItemFromControls()
        {
            if (_syncing || _selectedItemIndex < 0 || _selectedItemIndex >= _items.Count)
                return;

            ResultOverlayDraw2Item item = _items[_selectedItemIndex];
            item.Enabled = checkBoxEnabled.Checked;
            item.ItemType = GetSelectedItemType();
            item.Name = string.IsNullOrWhiteSpace(textBoxName.Text) ? GetItemTypeText(item.ItemType) : textBoxName.Text.Trim();
            item.SourceText1 = nodeSubscriptionSource.GetText1();
            item.SourceText2 = nodeSubscriptionSource.GetText2();
            item.UseManualText = checkBoxManualText.Checked;
            item.ManualText = textBoxManualText.Text;
            item.TextPrefix = textBoxTextPrefix.Text;
            item.FontSize = (int)numericFontSize.Value;
            item.LineWidth = (int)numericLineWidth.Value;
            item.TextMargin = (int)numericMargin.Value;
            item.TextPosition = comboBoxTextPosition.SelectedItem is DisplayTextPosition
                ? (DisplayTextPosition)comboBoxTextPosition.SelectedItem
                : item.TextPosition;
            item.TextCoordinateMode = GetTextCoordinateMode();
        }

        /// <summary>
        /// 根据当前绘制类型更新编辑控件可用状态。
        /// </summary>
        private void UpdateControlState()
        {
            bool hasItem = _selectedItemIndex >= 0 && _selectedItemIndex < _items.Count;
            groupBoxItemSetting.Enabled = hasItem;
            buttonDeleteItem.Enabled = hasItem;
            if (!hasItem)
                return;

            ResultOverlayDraw2ItemType type = GetSelectedItemType();
            bool isText = type == ResultOverlayDraw2ItemType.Text;
            checkBoxManualText.Enabled = isText;
            textBoxManualText.Enabled = isText && checkBoxManualText.Checked;
            labelTextPrefix.Enabled = isText && !checkBoxManualText.Checked;
            textBoxTextPrefix.Enabled = isText && !checkBoxManualText.Checked;
            // 几何项查找失败时也会绘制红色“未查到”文本，所以提示样式对全部绘制项开放。
            labelFontSize.Enabled = true;
            numericFontSize.Enabled = true;
            labelTextPosition.Enabled = true;
            comboBoxTextPosition.Enabled = true;
            labelTextCoordinateMode.Enabled = true;
            comboBoxTextCoordinateMode.Enabled = true;
            labelMargin.Enabled = true;
            numericMargin.Enabled = true;
            nodeSubscriptionSource.Enabled = !isText || !checkBoxManualText.Checked;
        }

        /// <summary>
        /// 刷新多条件项下拉框。
        /// </summary>
        /// <param name="selectedName">期望选中的条件名称。</param>
        private void RefreshRuleConditionItems(string selectedName)
        {
            comboBoxRuleCondition.Items.Clear();
            comboBoxRuleCondition.Items.Add("整体结果");

            NodeBase sourceNode = FindUpstreamNode(nodeSubscriptionRule.GetText1());
            AddConditionNamesFromParam(sourceNode);
            AddConditionNamesFromResult(sourceNode);

            SetRuleConditionName(selectedName);
        }

        /// <summary>
        /// 从多条件节点参数添加条件名称。
        /// </summary>
        /// <param name="sourceNode">源节点。</param>
        private void AddConditionNamesFromParam(NodeBase sourceNode)
        {
            if (sourceNode == null || sourceNode.NodeType != NodeType.MultiCondition)
                return;

            NodeParamMultiCondition param = sourceNode.ParamForm == null ? null : sourceNode.ParamForm.Params as NodeParamMultiCondition;
            if (param == null || param.Conditions == null)
                return;

            for (int i = 0; i < param.Conditions.Count; i++)
                AddRuleConditionName(GetConditionDisplayName(param.Conditions[i], i));
        }

        /// <summary>
        /// 从多条件节点运行明细添加条件名称。
        /// </summary>
        /// <param name="sourceNode">源节点。</param>
        private void AddConditionNamesFromResult(NodeBase sourceNode)
        {
            NodeResultMultiCondition result = sourceNode == null ? null : sourceNode.Result as NodeResultMultiCondition;
            if (result == null || result.Details == null)
                return;

            for (int i = 0; i < result.Details.Count; i++)
            {
                NodeConditionEvaluation detail = result.Details[i];
                AddRuleConditionName(string.IsNullOrWhiteSpace(detail == null ? null : detail.Name) ? "条件" + (i + 1) : detail.Name);
            }
        }

        /// <summary>
        /// 添加条件名称并避免重复项。
        /// </summary>
        /// <param name="name">条件名称。</param>
        private void AddRuleConditionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            foreach (object item in comboBoxRuleCondition.Items)
                if (string.Equals(Convert.ToString(item), name, StringComparison.OrdinalIgnoreCase))
                    return;

            comboBoxRuleCondition.Items.Add(name);
        }

        /// <summary>
        /// 获取当前颜色规则条件名称。
        /// </summary>
        /// <returns>条件名称。</returns>
        private string GetRuleConditionName()
        {
            return comboBoxRuleCondition.SelectedItem == null
                ? "整体结果"
                : Convert.ToString(comboBoxRuleCondition.SelectedItem);
        }

        /// <summary>
        /// 设置颜色规则条件名称。
        /// </summary>
        /// <param name="name">条件名称。</param>
        private void SetRuleConditionName(string name)
        {
            string target = string.IsNullOrWhiteSpace(name) ? "整体结果" : name;
            int index = comboBoxRuleCondition.Items.IndexOf(target);
            comboBoxRuleCondition.SelectedIndex = index >= 0 ? index : 0;
        }

        /// <summary>
        /// 查找当前节点的上游节点。
        /// </summary>
        /// <param name="nodeText">订阅节点文本。</param>
        /// <returns>匹配的上游节点。</returns>
        private NodeBase FindUpstreamNode(string nodeText)
        {
            if (_node == null || _node.Process == null || string.IsNullOrWhiteSpace(nodeText))
                return null;

            return _node.Process.GetUpstreamNodes(_node).FirstOrDefault(node => GetNodeText(node) == nodeText);
        }

        /// <summary>
        /// 获取节点订阅文本。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>订阅文本。</returns>
        private static string GetNodeText(NodeBase node)
        {
            return $"{node.ID}.{node.NodeName}";
        }

        /// <summary>
        /// 获取条件参数显示名称。
        /// </summary>
        /// <param name="condition">条件参数。</param>
        /// <param name="index">条件索引。</param>
        /// <returns>显示名称。</returns>
        private static string GetConditionDisplayName(MultiConditionItem condition, int index)
        {
            if (condition != null && !string.IsNullOrWhiteSpace(condition.Name))
                return condition.Name;

            return "条件" + (index + 1);
        }

        /// <summary>
        /// 获取当前选中的绘制项行索引。
        /// </summary>
        /// <returns>绘制项索引。</returns>
        private int GetItemSelectedIndex()
        {
            if (dataGridViewItems.SelectedRows.Count == 0)
                return -1;
            return dataGridViewItems.SelectedRows[0].Index;
        }

        /// <summary>
        /// 获取当前选中的颜色规则行索引。
        /// </summary>
        /// <returns>规则索引。</returns>
        private int GetRuleSelectedIndex()
        {
            if (dataGridViewRules.SelectedRows.Count == 0)
                return -1;
            return dataGridViewRules.SelectedRows[0].Index;
        }

        /// <summary>
        /// 获取当前绘制项类型。
        /// </summary>
        /// <returns>绘制项类型。</returns>
        private ResultOverlayDraw2ItemType GetSelectedItemType()
        {
            EnumOption<ResultOverlayDraw2ItemType> option = comboBoxItemType.SelectedItem as EnumOption<ResultOverlayDraw2ItemType>;
            return option == null ? ResultOverlayDraw2ItemType.Text : option.Value;
        }

        /// <summary>
        /// 设置当前绘制项类型。
        /// </summary>
        /// <param name="type">绘制项类型。</param>
        private void SetItemType(ResultOverlayDraw2ItemType type)
        {
            foreach (object item in comboBoxItemType.Items)
            {
                EnumOption<ResultOverlayDraw2ItemType> option = item as EnumOption<ResultOverlayDraw2ItemType>;
                if (option != null && option.Value == type)
                {
                    comboBoxItemType.SelectedItem = option;
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前规则聚合方式。
        /// </summary>
        /// <returns>规则聚合方式。</returns>
        private ResultOverlayColorRuleMode GetRuleMode()
        {
            EnumOption<ResultOverlayColorRuleMode> option = comboBoxRuleMode.SelectedItem as EnumOption<ResultOverlayColorRuleMode>;
            return option == null ? ResultOverlayColorRuleMode.AllTrue : option.Value;
        }

        /// <summary>
        /// 设置规则聚合方式。
        /// </summary>
        /// <param name="mode">规则聚合方式。</param>
        private void SetRuleMode(ResultOverlayColorRuleMode mode)
        {
            foreach (object item in comboBoxRuleMode.Items)
            {
                EnumOption<ResultOverlayColorRuleMode> option = item as EnumOption<ResultOverlayColorRuleMode>;
                if (option != null && option.Value == mode)
                {
                    comboBoxRuleMode.SelectedItem = option;
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前文本坐标系。
        /// </summary>
        /// <returns>文本四角定位坐标系。</returns>
        private DisplayTextCoordinateMode GetTextCoordinateMode()
        {
            EnumOption<DisplayTextCoordinateMode> option = comboBoxTextCoordinateMode.SelectedItem as EnumOption<DisplayTextCoordinateMode>;
            return option == null ? DisplayTextCoordinateMode.Image : option.Value;
        }

        /// <summary>
        /// 设置当前文本坐标系。
        /// </summary>
        /// <param name="mode">文本四角定位坐标系。</param>
        private void SetTextCoordinateMode(DisplayTextCoordinateMode mode)
        {
            foreach (object item in comboBoxTextCoordinateMode.Items)
            {
                EnumOption<DisplayTextCoordinateMode> option = item as EnumOption<DisplayTextCoordinateMode>;
                if (option != null && option.Value == mode)
                {
                    comboBoxTextCoordinateMode.SelectedItem = option;
                    return;
                }
            }

            if (comboBoxTextCoordinateMode.Items.Count > 0)
                comboBoxTextCoordinateMode.SelectedIndex = 0;
        }

        /// <summary>
        /// 打开颜色选择器并写回指定面板。
        /// </summary>
        /// <param name="panel">颜色面板。</param>
        private void ChoosePanelColor(Panel panel)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = panel.BackColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    panel.BackColor = dialog.Color;
            }
        }

        /// <summary>
        /// 将整数值限制到数值控件允许范围内。
        /// </summary>
        /// <param name="control">数值控件。</param>
        /// <param name="value">目标值。</param>
        /// <returns>修正后的值。</returns>
        private static decimal ClampNumeric(NumericUpDown control, int value)
        {
            return Math.Min(control.Maximum, Math.Max(control.Minimum, value));
        }

        /// <summary>
        /// 获取绘制项类型中文显示文本。
        /// </summary>
        /// <param name="type">绘制项类型。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetItemTypeText(ResultOverlayDraw2ItemType type)
        {
            switch (type)
            {
                case ResultOverlayDraw2ItemType.Text:
                    return "文本";
                case ResultOverlayDraw2ItemType.Line:
                    return "线";
                case ResultOverlayDraw2ItemType.Rectangle:
                    return "矩形";
                case ResultOverlayDraw2ItemType.Region:
                    return "区域/轮廓";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// 枚举下拉框显示项。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        private sealed class EnumOption<T>
        {
            /// <summary>
            /// 初始化枚举显示项。
            /// </summary>
            /// <param name="text">显示文本。</param>
            /// <param name="value">枚举值。</param>
            public EnumOption(string text, T value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 下拉框显示文本。
            /// </summary>
            public string Text { get; private set; }

            /// <summary>
            /// 对应的枚举值。
            /// </summary>
            public T Value { get; private set; }

            /// <summary>
            /// 返回显示文本，兼容没有绑定显示成员的场景。
            /// </summary>
            /// <returns>显示文本。</returns>
            public override string ToString()
            {
                return Text;
            }
        }
    }
}
