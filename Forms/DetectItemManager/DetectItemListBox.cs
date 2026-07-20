using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.DetectItemManager
{
    public partial class DetectItemListBox : UserControl
    {
        public bool IsSelected = false;
        bool isExpand = false;
        public string ItemName => labelName.Text;
        public DetectItemListBox()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 单击选中/取消选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tableLayoutPanel1_MouseClick(object sender, MouseEventArgs e)
        {
            IsSelected = !IsSelected;
            SetSelect(IsSelected);
        }
        /// <summary>
        /// 设置选中状态
        /// </summary>
        /// <param name="isSelect"></param>
        private void SetSelect(bool isSelect)
        {
            if (isSelect)
            {
                labelName.BackColor = SystemColors.ActiveCaption;
                tableLayoutPanel1.BackColor = SystemColors.ActiveCaption;
                labelTips.BackColor = SystemColors.ActiveCaption;
            }
            else
            {
                labelName.BackColor = SystemColors.InactiveCaption;
                tableLayoutPanel1.BackColor = SystemColors.InactiveCaption;
                labelTips.BackColor = SystemColors.InactiveCaption;
            }
        }

        /// <summary>
        /// 加载检测项
        /// </summary>
        /// <param name="info"></param>
        public void LoadConfig(string name, List<DetectItemInfo> infos)
        {
            DetectItemLanguage.NormalizeItems(infos);
            labelName.Text = name;
            myDataGridViewForm1.LoadConfig(infos);
        }

        /// <summary>
        /// 获取检测项配置
        /// </summary>
        /// <returns></returns>
        public (string, List<DetectItemInfo>) GetConfig()
        {
            return (ItemName, myDataGridViewForm1.GetConfig());
        }
        /// <summary>
        /// 点击展开/收起
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void labelTips_Click(object sender, System.EventArgs e)
        {
            isExpand = !isExpand;
            SetExpand(isExpand);
        }
        /// <summary>
        /// 双击选中且展开收起
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void labelName_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            isExpand = !isExpand;
            SetExpand(isExpand);
        }
        /// <summary>
        /// 展开检测项
        /// </summary>
        public void SetExpand(bool expand)
        {
            if (expand)
            {
                labelTips.Text = "收起 ▼";
                panelDown.Visible = true;
                myDataGridViewForm1.Visible = true;
            }
            else
            {
                labelTips.Text = "展开 ▶";
                panelDown.Visible = false;
                myDataGridViewForm1.Visible = false;
            }
        }
    }
}
