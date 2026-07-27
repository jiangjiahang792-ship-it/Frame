using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2
{
    /// <summary>
    /// ROI结果绘制2参数窗体，只向用户提供文本和自动ROI两种绘制项。
    /// </summary>
    public partial class NodeParamFormResultOverlayDraw2 : FormBase, INodeParamForm
    {
        /// <summary>
        /// ROI订阅允许接收的统一数据类别。
        /// </summary>
        private static readonly SubscriptionDataCategory[] RoiCategories =
        {
            SubscriptionDataCategory.Point,
            SubscriptionDataCategory.PointCollection,
            SubscriptionDataCategory.Line,
            SubscriptionDataCategory.Circle,
            SubscriptionDataCategory.Ellipse,
            SubscriptionDataCategory.Rectangle,
            SubscriptionDataCategory.Contour,
            SubscriptionDataCategory.Region,
            SubscriptionDataCategory.MeasurementResult,
            SubscriptionDataCategory.AlgorithmResult
        };

        /// <summary>
        /// 文本订阅允许接收的统一数据类别。
        /// </summary>
        private static readonly SubscriptionDataCategory[] TextCategories =
        {
            SubscriptionDataCategory.Boolean,
            SubscriptionDataCategory.Number,
            SubscriptionDataCategory.Text,
            SubscriptionDataCategory.AlgorithmResult
        };

        /// <summary>
        /// 当前参数窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 当前编辑中的绘制项集合。
        /// </summary>
        private readonly List<ResultOverlayDraw2Item> _items = new List<ResultOverlayDraw2Item>();

        /// <summary>
        /// 旧方案中的多条颜色规则，只做兼容保存和运行，不再提供新增编辑入口。
        /// </summary>
        private readonly List<ResultOverlayDraw2ColorRule> _legacyRules = new List<ResultOverlayDraw2ColorRule>();

        /// <summary>
        /// 旧方案颜色规则的组合方式。
        /// </summary>
        private ResultOverlayColorRuleMode _legacyRuleMode = ResultOverlayColorRuleMode.AllTrue;

        /// <summary>
        /// 当前选中的绘制项索引。
        /// </summary>
        private int _selectedItemIndex = -1;

        /// <summary>
        /// 控件同步标志，避免加载界面时反向修改参数。
        /// </summary>
        private bool _syncing;

        /// <summary>
        /// 高级文本设置是否展开。
        /// </summary>
        private bool _advancedExpanded;

        /// <summary>
        /// 初始化ROI结果绘制2参数窗体。
        /// </summary>
        public NodeParamFormResultOverlayDraw2()
        {
            InitializeComponent();
            InitializeOptions();
            SetAdvancedExpanded(false);
            RefreshItemsGrid();
        }

        /// <summary>
        /// 获取或设置当前窗体保存的节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗体所属节点并初始化三个订阅控件。
        /// </summary>
        /// <param name="node">所属节点。</param>
        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            _node = node;

            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);

            nodeSubscriptionJudge.SetExpectedValueType<bool>();
            nodeSubscriptionJudge.Init(node);

            nodeSubscriptionSource.SetInputContract(CreateTextInputContract());
            nodeSubscriptionSource.Init(node);
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
            if (param.HasNewJudgeSubscription)
                nodeSubscriptionJudge.SetText(param.JudgeText1, param.JudgeText2);
            else
                nodeSubscriptionJudge.ClearText();
            panelOkColor.BackColor = param.OkColor;
            panelNgColor.BackColor = param.NgColor;
            _syncing = false;

            _items.Clear();
            if (param.Items != null)
                _items.AddRange(param.Items.Where(item => item != null).Select(item => item.Clone()));

            _legacyRules.Clear();
            if (param.ColorRules != null)
                _legacyRules.AddRange(param.ColorRules.Where(rule => rule != null).Select(rule => rule.Clone()));
            _legacyRuleMode = param.RuleMode;

            UpdateLegacyRulesNotice(param.HasNewJudgeSubscription);
            RefreshItemsGrid();
            SelectItem(_items.Count > 0 ? 0 : -1);
        }

        /// <summary>
        /// 初始化文本位置和坐标系选项。
        /// </summary>
        private void InitializeOptions()
        {
            comboBoxTextPosition.DisplayMember = "Text";
            comboBoxTextPosition.ValueMember = "Value";
            comboBoxTextPosition.DataSource = new List<EnumOption<DisplayTextPosition>>
            {
                new EnumOption<DisplayTextPosition>("左上角", DisplayTextPosition.TopLeft),
                new EnumOption<DisplayTextPosition>("右上角", DisplayTextPosition.TopRight),
                new EnumOption<DisplayTextPosition>("左下角", DisplayTextPosition.BottomLeft),
                new EnumOption<DisplayTextPosition>("右下角", DisplayTextPosition.BottomRight)
            };
            comboBoxTextCoordinateMode.DisplayMember = "Text";
            comboBoxTextCoordinateMode.ValueMember = "Value";
            comboBoxTextCoordinateMode.DataSource = new List<EnumOption<DisplayTextCoordinateMode>>
            {
                new EnumOption<DisplayTextCoordinateMode>("图像坐标系", DisplayTextCoordinateMode.Image),
                new EnumOption<DisplayTextCoordinateMode>("控件坐标系", DisplayTextCoordinateMode.Control)
            };
        }

        /// <summary>
        /// 创建文本项订阅输入契约。
        /// </summary>
        /// <returns>文本可显示数据契约。</returns>
        private static SubscriptionInputContract CreateTextInputContract()
        {
            return SubscriptionInputContract.ForCategories(TextCategories);
        }

        /// <summary>
        /// 创建自动ROI项订阅输入契约。
        /// </summary>
        /// <returns>全部受支持几何数据契约。</returns>
        private static SubscriptionInputContract CreateRoiInputContract()
        {
            return SubscriptionInputContract.ForCategories(RoiCategories);
        }

        /// <summary>
        /// 添加一个文本绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddText_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Text);
        }

        /// <summary>
        /// 添加一个按实际订阅类型自动识别的ROI绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonAddRoi_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDraw2ItemType.Roi);
        }

        /// <summary>
        /// 删除当前选中的绘制项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonDeleteItem_Click(object sender, EventArgs e)
        {
            int index = GetSelectedItemIndex();
            if (index < 0 || index >= _items.Count)
                return;

            _items.RemoveAt(index);
            RefreshItemsGrid();
            SelectItem(Math.Min(index, _items.Count - 1));
        }

        /// <summary>
        /// 打开OK颜色选择器。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonChooseOkColor_Click(object sender, EventArgs e)
        {
            ChoosePanelColor(panelOkColor);
        }

        /// <summary>
        /// 打开NG颜色选择器。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonChooseNgColor_Click(object sender, EventArgs e)
        {
            ChoosePanelColor(panelNgColor);
        }

        /// <summary>
        /// 预览当前输入图像和绘制结果。
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
                ResultOverlayDraw2Builder.Build(
                    _node,
                    (NodeParamResultOverlayDraw2)Params,
                    out outputImage,
                    out displayResult,
                    out isOk,
                    out diagnostics);

                if (outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || outputImage.Bitmaps[0] == null)
                    throw new Exception("预览图像为空！");

                labelPreviewState.Text = diagnostics;
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
        /// 保存参数并隐藏窗体。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams(true))
                Hide();
        }

        /// <summary>
        /// 切换高级设置的展开状态。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonToggleAdvanced_Click(object sender, EventArgs e)
        {
            SetAdvancedExpanded(!_advancedExpanded);
        }

        /// <summary>
        /// 绘制项列表选择变化时保存旧项并载入新项。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void dataGridViewItems_SelectionChanged(object sender, EventArgs e)
        {
            if (_syncing)
                return;

            SaveCurrentItemFromControls();
            SelectItem(GetSelectedItemIndex());
        }

        /// <summary>
        /// 绘制项编辑控件变化时同步当前数据。
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
        /// 创建并添加指定用户可见类型的绘制项。
        /// </summary>
        /// <param name="type">文本或自动ROI类型。</param>
        private void AddItem(ResultOverlayDraw2ItemType type)
        {
            SaveCurrentItemFromControls();
            ResultOverlayDraw2Item item = new ResultOverlayDraw2Item
            {
                ItemType = type,
                Name = type == ResultOverlayDraw2ItemType.Text ? "文本" : "ROI",
                UseManualText = type == ResultOverlayDraw2ItemType.Text,
                ManualText = type == ResultOverlayDraw2ItemType.Text ? "OK" : string.Empty
            };
            _items.Add(item);
            RefreshItemsGrid();
            SelectItem(_items.Count - 1);
        }

        /// <summary>
        /// 校验并生成节点参数。
        /// </summary>
        /// <param name="showMessage">校验失败时是否弹出提示。</param>
        /// <returns>保存成功返回 true。</returns>
        private bool SaveParams(bool showMessage)
        {
            SaveCurrentItemFromControls();

            if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
            {
                ShowValidationMessage(showMessage, "请先订阅输入图像！");
                return false;
            }

            for (int index = 0; index < _items.Count; index++)
            {
                ResultOverlayDraw2Item item = _items[index];
                if (item == null || !item.Enabled)
                    continue;

                bool needSource = item.ItemType != ResultOverlayDraw2ItemType.Text || !item.UseManualText;
                if (needSource &&
                    (string.IsNullOrWhiteSpace(item.SourceText1) || string.IsNullOrWhiteSpace(item.SourceText2)))
                {
                    ShowValidationMessage(showMessage, $"第{index + 1}个绘制项还没有选择订阅结果！");
                    return false;
                }
            }

            string judgeText1 = nodeSubscriptionJudge.GetText1();
            string judgeText2 = nodeSubscriptionJudge.GetText2();
            bool hasJudgeNode = !string.IsNullOrWhiteSpace(judgeText1);
            bool hasJudgeResult = !string.IsNullOrWhiteSpace(judgeText2);
            if (hasJudgeNode != hasJudgeResult)
            {
                ShowValidationMessage(showMessage, "颜色判定订阅不完整，请重新选择布尔结果或清空订阅！");
                return false;
            }

            bool hasNewJudge = hasJudgeNode && hasJudgeResult;
            List<ResultOverlayDraw2ColorRule> rulesToSave = hasNewJudge
                ? new List<ResultOverlayDraw2ColorRule>()
                : _legacyRules.Select(rule => rule.Clone()).ToList();

            Params = new NodeParamResultOverlayDraw2
            {
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                JudgeText1 = hasNewJudge ? judgeText1 : null,
                JudgeText2 = hasNewJudge ? judgeText2 : null,
                OkColor = panelOkColor.BackColor,
                NgColor = panelNgColor.BackColor,
                RuleMode = _legacyRuleMode,
                Items = _items.Select(item => item.Clone()).ToList(),
                ColorRules = rulesToSave
            };

            if (hasNewJudge)
                _legacyRules.Clear();
            UpdateLegacyRulesNotice(hasNewJudge);
            return true;
        }

        /// <summary>
        /// 按需显示参数校验提示。
        /// </summary>
        /// <param name="showMessage">是否弹出提示。</param>
        /// <param name="message">简体中文提示内容。</param>
        private static void ShowValidationMessage(bool showMessage, string message)
        {
            if (showMessage)
                MessageBoxTD.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 刷新绘制项列表并恢复原选择。
        /// </summary>
        private void RefreshItemsGrid()
        {
            int selectedIndex = _selectedItemIndex;
            _syncing = true;
            dataGridViewItems.Rows.Clear();
            foreach (ResultOverlayDraw2Item item in _items)
            {
                dataGridViewItems.Rows.Add(
                    item.Enabled ? "是" : "否",
                    GetItemTypeText(item.ItemType),
                    GetItemSummary(item));
            }

            if (selectedIndex >= 0 && selectedIndex < dataGridViewItems.Rows.Count)
            {
                dataGridViewItems.ClearSelection();
                dataGridViewItems.Rows[selectedIndex].Selected = true;
            }
            _syncing = false;
        }

        /// <summary>
        /// 选中指定绘制项并加载其设置。
        /// </summary>
        /// <param name="index">绘制项索引。</param>
        private void SelectItem(int index)
        {
            _syncing = true;
            _selectedItemIndex = index;
            if (index >= 0 && index < _items.Count)
            {
                if (index < dataGridViewItems.Rows.Count)
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
        /// 把一个绘制项加载到设置控件。
        /// </summary>
        /// <param name="item">待编辑绘制项。</param>
        private void LoadItemToControls(ResultOverlayDraw2Item item)
        {
            ApplySourceContract(item.ItemType);
            checkBoxEnabled.Checked = item.Enabled;
            labelCurrentTypeValue.Text = GetItemTypeText(item.ItemType);
            checkBoxManualText.Checked = item.UseManualText;
            textBoxManualText.Text = item.ManualText ?? string.Empty;
            textBoxTextPrefix.Text = item.TextPrefix ?? string.Empty;
            numericFontSize.Value = ClampNumeric(numericFontSize, item.FontSize);
            numericLineWidth.Value = ClampNumeric(numericLineWidth, item.LineWidth);
            numericMargin.Value = ClampNumeric(numericMargin, item.TextMargin);
            SetTextPosition(item.TextPosition);
            SetTextCoordinateMode(item.TextCoordinateMode);

            if (string.IsNullOrWhiteSpace(item.SourceText1))
                nodeSubscriptionSource.ClearText();
            else
                nodeSubscriptionSource.SetText(item.SourceText1, item.SourceText2);
        }

        /// <summary>
        /// 清空绘制项设置控件。
        /// </summary>
        private void ClearItemControls()
        {
            ApplySourceContract(ResultOverlayDraw2ItemType.Text);
            checkBoxEnabled.Checked = false;
            labelCurrentTypeValue.Text = "未选择";
            checkBoxManualText.Checked = true;
            textBoxManualText.Text = string.Empty;
            textBoxTextPrefix.Text = string.Empty;
            numericFontSize.Value = 18;
            numericLineWidth.Value = 2;
            numericMargin.Value = 10;
            SetTextPosition(DisplayTextPosition.TopLeft);
            SetTextCoordinateMode(DisplayTextCoordinateMode.Image);
            nodeSubscriptionSource.ClearText();
        }

        /// <summary>
        /// 将当前设置控件写回选中的绘制项。
        /// </summary>
        private void SaveCurrentItemFromControls()
        {
            if (_syncing || _selectedItemIndex < 0 || _selectedItemIndex >= _items.Count)
                return;

            ResultOverlayDraw2Item item = _items[_selectedItemIndex];
            item.Enabled = checkBoxEnabled.Checked;
            item.SourceText1 = nodeSubscriptionSource.GetText1();
            item.SourceText2 = nodeSubscriptionSource.GetText2();
            item.UseManualText = item.ItemType == ResultOverlayDraw2ItemType.Text && checkBoxManualText.Checked;
            item.ManualText = textBoxManualText.Text;
            item.TextPrefix = textBoxTextPrefix.Text;
            item.FontSize = (int)numericFontSize.Value;
            item.LineWidth = (int)numericLineWidth.Value;
            item.TextMargin = (int)numericMargin.Value;
            item.TextPosition = GetTextPosition();
            item.TextCoordinateMode = GetTextCoordinateMode();
            item.Name = BuildAutomaticItemName(item);
        }

        /// <summary>
        /// 根据绘制项类型切换来源订阅契约。
        /// </summary>
        /// <param name="type">绘制项类型。</param>
        private void ApplySourceContract(ResultOverlayDraw2ItemType type)
        {
            nodeSubscriptionSource.SetInputContract(type == ResultOverlayDraw2ItemType.Text
                ? CreateTextInputContract()
                : CreateRoiInputContract());
        }

        /// <summary>
        /// 根据当前绘制项类型更新设置控件的显示与可用状态。
        /// </summary>
        private void UpdateControlState()
        {
            bool hasItem = _selectedItemIndex >= 0 && _selectedItemIndex < _items.Count;
            groupBoxItemSetting.Enabled = hasItem;
            buttonDeleteItem.Enabled = hasItem;
            if (!hasItem)
                return;

            ResultOverlayDraw2Item item = _items[_selectedItemIndex];
            bool isText = item.ItemType == ResultOverlayDraw2ItemType.Text;
            panelManualText.Visible = isText;
            panelTextPrefix.Visible = isText;
            panelTextStyle.Visible = isText;
            panelRoiStyle.Visible = !isText;
            checkBoxManualText.Enabled = isText;
            textBoxManualText.Enabled = isText && checkBoxManualText.Checked;
            textBoxTextPrefix.Enabled = isText && !checkBoxManualText.Checked;
            nodeSubscriptionSource.Enabled = !isText || !checkBoxManualText.Checked;
            panelAdvanced.Visible = _advancedExpanded;
        }

        /// <summary>
        /// 设置高级设置展开状态并同步按钮文字。
        /// </summary>
        /// <param name="expanded">是否展开。</param>
        private void SetAdvancedExpanded(bool expanded)
        {
            _advancedExpanded = expanded;
            panelAdvanced.Visible = expanded;
            buttonToggleAdvanced.Text = expanded ? "收起高级设置" : "展开高级设置";
        }

        /// <summary>
        /// 更新旧版颜色规则兼容提示。
        /// </summary>
        /// <param name="hasNewJudge">是否已经配置新版单布尔判定。</param>
        private void UpdateLegacyRulesNotice(bool hasNewJudge)
        {
            labelLegacyRules.Visible = !hasNewJudge && _legacyRules.Count > 0;
            labelLegacyRules.Text = labelLegacyRules.Visible
                ? $"当前兼容运行旧版颜色规则（{_legacyRules.Count}条），选择新的颜色判定后将替换旧规则。"
                : string.Empty;
        }

        /// <summary>
        /// 获取当前列表选择的绘制项索引。
        /// </summary>
        /// <returns>没有选择时返回-1。</returns>
        private int GetSelectedItemIndex()
        {
            if (dataGridViewItems.SelectedRows.Count == 0)
                return -1;
            return dataGridViewItems.SelectedRows[0].Index;
        }

        /// <summary>
        /// 获取用户可见的绘制项类型文本。
        /// </summary>
        /// <param name="type">内部绘制项类型。</param>
        /// <returns>文本、ROI或ROI旧配置。</returns>
        private static string GetItemTypeText(ResultOverlayDraw2ItemType type)
        {
            if (type == ResultOverlayDraw2ItemType.Text)
                return "文本";
            if (type == ResultOverlayDraw2ItemType.Roi)
                return "ROI";
            return "ROI（旧配置）";
        }

        /// <summary>
        /// 生成列表使用的绘制内容摘要。
        /// </summary>
        /// <param name="item">绘制项。</param>
        /// <returns>手动文本或订阅来源摘要。</returns>
        private static string GetItemSummary(ResultOverlayDraw2Item item)
        {
            if (item == null)
                return string.Empty;
            if (item.ItemType == ResultOverlayDraw2ItemType.Text && item.UseManualText)
                return string.IsNullOrWhiteSpace(item.ManualText) ? "手动文本" : item.ManualText;
            return string.IsNullOrWhiteSpace(item.SourceDisplay) ? "未选择订阅" : item.SourceDisplay;
        }

        /// <summary>
        /// 根据类型和订阅来源生成稳定名称，用户无需手动维护名称字段。
        /// </summary>
        /// <param name="item">绘制项。</param>
        /// <returns>自动名称。</returns>
        private static string BuildAutomaticItemName(ResultOverlayDraw2Item item)
        {
            string typeText = item.ItemType == ResultOverlayDraw2ItemType.Text ? "文本" : "ROI";
            string summary = GetItemSummary(item);
            if (string.IsNullOrWhiteSpace(summary) || summary == "未选择订阅")
                return typeText;
            return typeText + "－" + summary;
        }

        /// <summary>
        /// 获取当前文本坐标系。
        /// </summary>
        /// <returns>图像或控件坐标系。</returns>
        private DisplayTextCoordinateMode GetTextCoordinateMode()
        {
            EnumOption<DisplayTextCoordinateMode> option =
                comboBoxTextCoordinateMode.SelectedItem as EnumOption<DisplayTextCoordinateMode>;
            return option == null ? DisplayTextCoordinateMode.Image : option.Value;
        }

        /// <summary>
        /// 获取当前文本四角位置。
        /// </summary>
        /// <returns>当前选择的文本位置。</returns>
        private DisplayTextPosition GetTextPosition()
        {
            EnumOption<DisplayTextPosition> option =
                comboBoxTextPosition.SelectedItem as EnumOption<DisplayTextPosition>;
            return option == null ? DisplayTextPosition.TopLeft : option.Value;
        }

        /// <summary>
        /// 根据枚举值选中对应的简体中文文本位置。
        /// </summary>
        /// <param name="position">文本四角位置。</param>
        private void SetTextPosition(DisplayTextPosition position)
        {
            foreach (object item in comboBoxTextPosition.Items)
            {
                EnumOption<DisplayTextPosition> option = item as EnumOption<DisplayTextPosition>;
                if (option != null && option.Value == position)
                {
                    comboBoxTextPosition.SelectedItem = option;
                    return;
                }
            }

            if (comboBoxTextPosition.Items.Count > 0)
                comboBoxTextPosition.SelectedIndex = 0;
        }

        /// <summary>
        /// 设置当前文本坐标系。
        /// </summary>
        /// <param name="mode">文本坐标系。</param>
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
        /// 打开颜色选择器并写回颜色面板。
        /// </summary>
        /// <param name="panel">目标颜色面板。</param>
        private static void ChoosePanelColor(Panel panel)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = panel.BackColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                    panel.BackColor = dialog.Color;
            }
        }

        /// <summary>
        /// 把整数值限制到数字控件的有效范围。
        /// </summary>
        /// <param name="control">数字控件。</param>
        /// <param name="value">待限制值。</param>
        /// <returns>有效范围内的十进制值。</returns>
        private static decimal ClampNumeric(NumericUpDown control, int value)
        {
            return Math.Max(control.Minimum, Math.Min(control.Maximum, value));
        }

        /// <summary>
        /// 为下拉框提供简体中文文字和枚举值。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        private sealed class EnumOption<T>
        {
            /// <summary>
            /// 初始化枚举显示选项。
            /// </summary>
            /// <param name="text">简体中文显示文本。</param>
            /// <param name="value">枚举值。</param>
            public EnumOption(string text, T value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>获取显示文本。</summary>
            public string Text { get; private set; }

            /// <summary>获取枚举值。</summary>
            public T Value { get; private set; }
        }
    }
}
