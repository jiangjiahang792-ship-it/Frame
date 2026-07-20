using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.SelectDetectItem
{
    public partial class FormSelectDetectItem : FormBase
    {
        public EventHandler<List<string>> SendDetectItems;
        public FormSelectDetectItem()
        {
            InitializeComponent();
            BindLanguage();
            Shown += FormSelectDetectItem_Shown;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void FormSelectDetectItem_Shown(object sender, EventArgs e)
        {
            InitData(Solution.Instance.DetectItemDic);
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "SelectDetectItem.Title");
            LanguageManager.Bind(button1, "Common.Close");
            LanguageManager.Bind(button2, "SelectDetectItem.SelectCurrent");
            LanguageManager.Bind(button3, "SelectDetectItem.CancelCurrent");
            LanguageManager.Bind(button4, "SelectDetectItem.SelectAll");
            LanguageManager.Bind(button5, "SelectDetectItem.CancelAll");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
            InitData(Solution.Instance.DetectItemDic);
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
        }

        /// <summary>
        /// 获取当前选中的检测项列表
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// 获取所有 TabPage 中 CheckedListBox 的选中项文本
        /// </summary>
        /// <returns>所有选中项的文本列表</returns>
        public List<string> GetSelectedItems()
        {
            List<string> selectedItems = new List<string>();

            // 遍历每个 TabPage
            foreach (TabPage tabPage in tabControl1.TabPages)
            {
                // 遍历 TabPage 中的控件
                foreach (Control control in tabPage.Controls)
                {
                    if (control is CheckedListBox checkedListBox)
                    {
                        // 遍历所有选中的项
                        for (int i = 0; i < checkedListBox.CheckedItems.Count; i++)
                        {
                            if (checkedListBox.CheckedItems[i] is DetectItemListEntry entry)
                                selectedItems.Add(entry.Key);
                            else
                                selectedItems.Add(DetectItemLanguage.NormalizeName(checkedListBox.CheckedItems[i].ToString()));
                        }
                    }
                }
            }

            return selectedItems;
        }

        /// <summary>
        /// 初始化并绑定数据
        /// </summary>
        public void InitData(Dictionary<string, List<DetectItemInfo>> dataMap)
        {
            // 清空原有 TabPages（避免重复添加）
            tabControl1.TabPages.Clear();

            foreach (var kvp in dataMap)
            {
                string name = kvp.Key;
                List<DetectItemInfo> detectItems = kvp.Value;

                // 创建新的 TabPage
                TabPage tabPage = new TabPage(name);

                // 创建 CheckedListBox
                CheckedListBox checkedListBox = new CheckedListBox();
                checkedListBox.Dock = DockStyle.Fill; // 填满 TabPage
                checkedListBox.CheckOnClick = true;   // 点击即切换选中状态

                // 填充数据（调用你已实现的方法）
                UpdateListBoxView(checkedListBox, detectItems);

                // 添加到 TabPage
                tabPage.Controls.Add(checkedListBox);

                // 添加 TabPage 到 TabControl
                tabControl1.TabPages.Add(tabPage);
            }
        }

        /// <summary>
        /// 更新检测项列表视图
        /// </summary>
        /// <param name="aIInputInfo"></param>
        private void UpdateListBoxView(CheckedListBox clb, List<DetectItemInfo> items)
        {
            try
            {
                clb.Items.Clear();

                foreach (var item in items)
                {
                    // 添加项到 CheckedListBox
                    clb.Items.Add(new DetectItemListEntry(item.Name), item.Enable); // 添加名称并设置初始选中状态
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, LanguageManager.Format("SelectDetectItem.RefreshFailed", ex.Message), true);
            }
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            SendDetectItems?.Invoke(this, GetSelectedItems());
            Hide();
        }
        /// <summary>
        /// 全选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            // 遍历 TabPage 中的控件
            foreach (Control control in tabControl1.SelectedTab.Controls)
            {
                if (control is CheckedListBox checkedListBox)
                {
                    // 全选
                    for (int i = 0; i < checkedListBox.Items.Count; i++)
                    {
                        checkedListBox.SetItemChecked(i, true);
                    }
                }
            }
        }
        /// <summary>
        /// 全部取消选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            // 遍历 TabPage 中的控件
            foreach (Control control in tabControl1.SelectedTab.Controls)
            {
                if (control is CheckedListBox checkedListBox)
                {
                    // 全选
                    for (int i = 0; i < checkedListBox.Items.Count; i++)
                    {
                        checkedListBox.SetItemChecked(i, false);
                    }
                }
            }
        }
        /// <summary>
        /// 全选所有页面的选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button4_Click(object sender, EventArgs e)
        {
            foreach (TabPage page in tabControl1.TabPages)
            {
                // 遍历 TabPage 中的控件
                foreach (Control control in page.Controls)
                {
                    if (control is CheckedListBox checkedListBox)
                    {
                        // 全选
                        for (int i = 0; i < checkedListBox.Items.Count; i++)
                        {
                            checkedListBox.SetItemChecked(i, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 取消所有页面的选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button5_Click(object sender, EventArgs e)
        {
            foreach (TabPage page in tabControl1.TabPages)
            {
                // 遍历 TabPage 中的控件
                foreach (Control control in page.Controls)
                {
                    if (control is CheckedListBox checkedListBox)
                    {
                        // 全选
                        for (int i = 0; i < checkedListBox.Items.Count; i++)
                        {
                            checkedListBox.SetItemChecked(i, false);
                        }
                    }
                }
            }
        }

        private sealed class DetectItemListEntry
        {
            public DetectItemListEntry(string name)
            {
                Key = DetectItemLanguage.NormalizeName(name);
            }

            public string Key { get; }

            public override string ToString()
            {
                return DetectItemLanguage.GetDisplayName(Key);
            }
        }
    }
}
