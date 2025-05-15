using System;
using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal partial class TDJSParameterGrid : UserControl
    {
        private int itemCount = 0; // 记录已添加项数，用于计算行列位置
        private const int RowHeight = 40; // 设置每行固定高度
        public LabelData LabelData { get; set; }// 标签数据作为界面显示的参数
        public TDJSParameterGrid()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 添加一项参数控件
        /// </summary>
        /// <param name="check">checkbox状态</param>
        /// <param name="label">标签文字</param>
        /// <param name="min">最小值</param>
        /// <param name="value">当前值</param>
        /// <param name="max">最大值</param>
        public void AddItem(bool check, string className, string min, string value, string max)
        {
            int col = itemCount % 2;           // 列号：0 或 1
            int row = itemCount / 2;           // 行号：每两个控件换行

            if (col == 0)
            {
                // 如果是新行的第一个控件，则添加新行
                if (row >= tableLayoutPanel1.RowCount)
                {
                    tableLayoutPanel1.RowCount++;
                    tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
                }
            }

            int rowIndex = tableLayoutPanel1.RowCount;

            // 创建子表格：5列1行
            var subPanel = new TableLayoutPanel
            {
                ColumnCount = 5,
                RowCount = 1,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // 设置各列宽度（可调整）
            subPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F)); // CheckBox
            subPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // Label
            subPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));  // Min
            subPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));  // Value
            subPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));  // Max

            // 创建控件
            var font = new Font("宋体", 10.8F, FontStyle.Regular, GraphicsUnit.Point);
            var checkBox = new CheckBox { Checked = check, Dock = DockStyle.Right };
            var lbl = new Label { Text = className, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = font };
            var txtMin = new TextBox { Text = min, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = font };
            var txtValue = new TextBox { Text = value, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = font };
            var txtMax = new TextBox { Text = max, Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center, Font = font };

            // 添加到子表格
            subPanel.Controls.Add(checkBox, 0, 0);
            subPanel.Controls.Add(lbl, 1, 0);
            subPanel.Controls.Add(txtMin, 2, 0);
            subPanel.Controls.Add(txtValue, 3, 0);
            subPanel.Controls.Add(txtMax, 4, 0);

            // 确保子表格中的控件水平居中
            foreach (Control control in subPanel.Controls)
            {
                control.Anchor = AnchorStyles.None;
                control.Margin = new Padding(3); // 可根据需要调整间距
            }

            // 添加到主表格指定位置
            tableLayoutPanel1.Controls.Add(subPanel, col, row);
            itemCount++; // 增加计数器
        }

        /// <summary>
        /// 加载界面参数
        /// </summary>
        /// <param name="labelFile"></param>
        public void LoadLabel(string labelFile)
        {
            // 清空现有控件
            tableLayoutPanel1.Controls.Clear();
            tableLayoutPanel1.RowStyles.Clear();
            itemCount = 0; // 重置计数器

            // 添加参数项
            LabelData = new LabelData(labelFile);

            foreach (var label in LabelData.Labels)
            {
                AddItem(label.Enable, label.ClassName, label.MinValue.ToString(), "", label.MaxValue.ToString());
            }
        }

        /// <summary>
        /// 加载界面参数
        /// </summary>
        /// <param name="labelFile"></param>
        public void LoadLabel(LabelData labels)
        {
            // 清空现有控件
            tableLayoutPanel1.Controls.Clear();
            tableLayoutPanel1.RowStyles.Clear();
            itemCount = 0; // 重置计数器

            // 添加参数项
            LabelData = labels;

            foreach (var label in LabelData.Labels)
            {
                AddItem(label.Enable, label.ClassName, label.MinValue.ToString(), "", label.MaxValue.ToString());
            }
        }
    }
}
