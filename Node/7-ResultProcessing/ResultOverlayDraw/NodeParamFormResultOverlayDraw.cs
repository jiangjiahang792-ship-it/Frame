using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw
{
    public partial class NodeParamFormResultOverlayDraw : FormBase, INodeParamForm
    {
        private NodeBase _node;
        private readonly List<ResultOverlayDrawItem> _items = new List<ResultOverlayDrawItem>();
        private int _selectedIndex = -1;
        private bool _syncing;

        public NodeParamFormResultOverlayDraw()
        {
            InitializeComponent();
            InitCombos();
            RefreshGrid();
        }

        public INodeParam Params { get; set; }

        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            _node = node;
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionSource.Init(node);
        }

        public void SetParam2Form()
        {
            if (!(Params is NodeParamResultOverlayDraw param))
                return;

            nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
            _items.Clear();
            if (param.Items != null)
                _items.AddRange(param.Items.Select(item => item.Clone()));

            RefreshGrid();
            SelectItem(_items.Count > 0 ? 0 : -1);
        }

        private void InitCombos()
        {
            comboBoxItemType.DataSource = Enum.GetValues(typeof(ResultOverlayDrawItemType));
            comboBoxTextPosition.DataSource = Enum.GetValues(typeof(DisplayTextPosition));
        }

        private void buttonAddText_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDrawItemType.Text);
        }

        private void buttonAddLine_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDrawItemType.Line);
        }

        private void buttonAddRectangle_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDrawItemType.Rectangle);
        }

        private void buttonAddRegion_Click(object sender, EventArgs e)
        {
            AddItem(ResultOverlayDrawItemType.Region);
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            int index = GetGridSelectedIndex();
            if (index < 0 || index >= _items.Count)
                return;

            _items.RemoveAt(index);
            RefreshGrid();
            SelectItem(Math.Min(index, _items.Count - 1));
        }

        private void buttonChooseColor_Click(object sender, EventArgs e)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = panelColor.BackColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    panelColor.BackColor = dialog.Color;
                    SaveCurrentItemFromControls();
                    RefreshGrid();
                }
            }
        }

        private void buttonApplyStyleToAll_Click(object sender, EventArgs e)
        {
            SaveCurrentItemFromControls();
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
                return;

            Color color = panelColor.BackColor;
            int fontSize = (int)numericFontSize.Value;
            int lineWidth = (int)numericLineWidth.Value;
            foreach (ResultOverlayDrawItem item in _items)
            {
                item.Color = color;
                item.FontSize = fontSize;
                item.LineWidth = lineWidth;
            }

            RefreshGrid();
            SelectItem(_selectedIndex);
        }

        private void buttonPreview_Click(object sender, EventArgs e)
        {
            if (!SaveParams(false))
                return;

            try
            {
                ResultOverlayDrawBuilder.Build(_node, (NodeParamResultOverlayDraw)Params, out var outputImage, out var displayResult);
                if (outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || outputImage.Bitmaps[0] == null)
                    throw new Exception("预览图像为空！");

                showImageControl1.SetImage(BitmapConverter.ToBitmap(outputImage.Bitmaps[0]), displayResult);
                showImageControl1.ShowFit();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("预览失败：" + ex.Message, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams(true))
                Hide();
        }

        private void dataGridViewItems_SelectionChanged(object sender, EventArgs e)
        {
            if (_syncing)
                return;

            SaveCurrentItemFromControls();
            SelectItem(GetGridSelectedIndex());
        }

        private void ItemControl_ValueChanged(object sender, EventArgs e)
        {
            if (_syncing)
                return;

            SaveCurrentItemFromControls();
            RefreshGrid();
            UpdateControlState();
        }

        private void AddItem(ResultOverlayDrawItemType type)
        {
            SaveCurrentItemFromControls();

            ResultOverlayDrawItem item = new ResultOverlayDrawItem
            {
                ItemType = type,
                Name = GetTypeText(type),
                UseManualText = type == ResultOverlayDrawItemType.Text,
                ManualText = type == ResultOverlayDrawItemType.Text ? "OK" : string.Empty,
                Color = type == ResultOverlayDrawItemType.Text ? Color.Lime : Color.OrangeRed
            };

            _items.Add(item);
            RefreshGrid();
            SelectItem(_items.Count - 1);
        }

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

            foreach (ResultOverlayDrawItem item in _items.Where(i => i.Enabled))
            {
                bool needSource = item.ItemType != ResultOverlayDrawItemType.Text || !item.UseManualText;
                if (needSource && string.IsNullOrWhiteSpace(item.SourceText1))
                {
                    if (showMessage)
                        MessageBoxTD.Show($"绘制项“{item.Name}”还没有选择订阅源！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            Params = new NodeParamResultOverlayDraw
            {
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                Items = _items.Select(item => item.Clone()).ToList()
            };
            return true;
        }

        private void RefreshGrid()
        {
            _syncing = true;
            int selected = GetGridSelectedIndex();
            dataGridViewItems.Rows.Clear();

            foreach (ResultOverlayDrawItem item in _items)
            {
                int rowIndex = dataGridViewItems.Rows.Add(
                    item.Enabled ? "是" : "否",
                    GetTypeText(item.ItemType),
                    item.Name,
                    item.SourceDisplay,
                    ColorTranslator.ToHtml(item.Color));
                dataGridViewItems.Rows[rowIndex].Cells[4].Style.ForeColor = item.Color;
            }

            if (selected >= 0 && selected < dataGridViewItems.Rows.Count)
                dataGridViewItems.Rows[selected].Selected = true;

            _syncing = false;
        }

        private void SelectItem(int index)
        {
            _syncing = true;
            _selectedIndex = index;

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

        private void LoadItemToControls(ResultOverlayDrawItem item)
        {
            checkBoxEnabled.Checked = item.Enabled;
            comboBoxItemType.SelectedItem = item.ItemType;
            textBoxName.Text = item.Name ?? string.Empty;
            checkBoxManualText.Checked = item.UseManualText;
            textBoxManualText.Text = item.ManualText ?? string.Empty;
            textBoxTextPrefix.Text = item.TextPrefix ?? string.Empty;
            numericFontSize.Value = ClampNumeric(numericFontSize, item.FontSize);
            numericLineWidth.Value = ClampNumeric(numericLineWidth, item.LineWidth);
            numericMargin.Value = ClampNumeric(numericMargin, item.TextMargin);
            comboBoxTextPosition.SelectedItem = item.TextPosition;
            panelColor.BackColor = item.Color;

            if (string.IsNullOrWhiteSpace(item.SourceText1))
                nodeSubscriptionSource.ClearText();
            else
                nodeSubscriptionSource.SetText(item.SourceText1, item.SourceText2);
        }

        private void ClearItemControls()
        {
            checkBoxEnabled.Checked = false;
            comboBoxItemType.SelectedItem = ResultOverlayDrawItemType.Text;
            textBoxName.Text = string.Empty;
            checkBoxManualText.Checked = true;
            textBoxManualText.Text = string.Empty;
            textBoxTextPrefix.Text = string.Empty;
            numericFontSize.Value = 18;
            numericLineWidth.Value = 2;
            numericMargin.Value = 10;
            comboBoxTextPosition.SelectedItem = DisplayTextPosition.TopLeft;
            panelColor.BackColor = Color.Lime;
            nodeSubscriptionSource.ClearText();
        }

        private void SaveCurrentItemFromControls()
        {
            if (_syncing || _selectedIndex < 0 || _selectedIndex >= _items.Count)
                return;

            ResultOverlayDrawItem item = _items[_selectedIndex];
            item.Enabled = checkBoxEnabled.Checked;
            item.ItemType = comboBoxItemType.SelectedItem is ResultOverlayDrawItemType
                ? (ResultOverlayDrawItemType)comboBoxItemType.SelectedItem
                : item.ItemType;
            item.Name = string.IsNullOrWhiteSpace(textBoxName.Text) ? GetTypeText(item.ItemType) : textBoxName.Text.Trim();
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
            item.Color = panelColor.BackColor;
        }

        private void UpdateControlState()
        {
            bool hasItem = _selectedIndex >= 0 && _selectedIndex < _items.Count;
            groupBoxItemSetting.Enabled = hasItem;
            buttonDelete.Enabled = hasItem;
            if (!hasItem)
                return;

            ResultOverlayDrawItemType type = comboBoxItemType.SelectedItem is ResultOverlayDrawItemType
                ? (ResultOverlayDrawItemType)comboBoxItemType.SelectedItem
                : ResultOverlayDrawItemType.Text;

            bool isText = type == ResultOverlayDrawItemType.Text;
            checkBoxManualText.Enabled = isText;
            textBoxManualText.Enabled = isText && checkBoxManualText.Checked;
            labelTextPrefix.Enabled = isText && !checkBoxManualText.Checked;
            textBoxTextPrefix.Enabled = isText && !checkBoxManualText.Checked;
            labelFontSize.Enabled = isText;
            numericFontSize.Enabled = isText;
            labelTextPosition.Enabled = isText;
            comboBoxTextPosition.Enabled = isText;
            labelMargin.Enabled = isText;
            numericMargin.Enabled = isText;
            nodeSubscriptionSource.Enabled = !isText || !checkBoxManualText.Checked;
            buttonApplyStyleToAll.Enabled = _items.Count > 0;
        }

        private int GetGridSelectedIndex()
        {
            if (dataGridViewItems.SelectedRows.Count == 0)
                return -1;
            return dataGridViewItems.SelectedRows[0].Index;
        }

        private static decimal ClampNumeric(NumericUpDown control, int value)
        {
            return Math.Min(control.Maximum, Math.Max(control.Minimum, value));
        }

        private static string GetTypeText(ResultOverlayDrawItemType type)
        {
            switch (type)
            {
                case ResultOverlayDrawItemType.Text:
                    return "文本";
                case ResultOverlayDrawItemType.Line:
                    return "线";
                case ResultOverlayDrawItemType.Rectangle:
                    return "矩形";
                case ResultOverlayDrawItemType.Region:
                    return "区域/轮廓";
                default:
                    return type.ToString();
            }
        }
    }
}
