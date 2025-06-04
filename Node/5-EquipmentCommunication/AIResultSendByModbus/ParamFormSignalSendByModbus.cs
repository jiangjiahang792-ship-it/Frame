using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.AIResultSendByModbus
{
    internal partial class ParamFormSignalSendByModbus : Form, INodeParamForm
    {
        private NodeBase nodeBase;
        private Dictionary<string, DetectItemAddress> mapping;
        public ParamFormSignalSendByModbus()
        {
            InitializeComponent();
            InitModbusComboBox();
            InitializeDataGridView();
            toolTip1.SetToolTip(checkBoxReSet, "发送信号后在保持时间结束自动重置信号");
            toolTip1.SetToolTip(buttonAuto1, "清空信号列表，对当前勾选的每一项检测项，在信号列表中生成一行信号配置");
            toolTip1.SetToolTip(buttonAuto2, "清空信号列表，对当前勾选的所有检测项，在信号列表中仅生成一行信号配置");
            toolTip1.SetToolTip(buttonAdd, "不清空信号列表，在原有信号列表上新增，被勾选的每一项检测项均在信号列表中生成一行信号配置");
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            InitModbusComboBox();

            // 获取AI检测节点的检测项配置
            foreach (var node in nodeBase.Process.Nodes)
            {
                if (node is NodeTDAI nodeTDAI)
                {
                    var param = (NodeParamTDAI)nodeTDAI.ParamForm.Params;
                    UpdataListBoxView(param.AIInputInfo);
                }
            }
        }

        public INodeParam Params { get; set; }
        /// <summary>
        /// 获取订阅的AI结果
        /// </summary>
        /// <returns></returns>
        public List<DetectItemResult> GetAiResult()
        {
            return nodeSubscription1.GetValue<List<DetectItemResult>>();
        }

        /// <summary>
        /// 构建“检测项-（Modbus设备，Modbus地址）”的映射字典
        /// </summary>
        /// <param name="bindingData"></param>
        /// <returns></returns>
        private Dictionary<string, DetectItemAddress> BuildMapping(BindingList<DetectItemRow> bindingData)
        {
            var mapping = new Dictionary<string, DetectItemAddress>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in bindingData)
            {
                if (string.IsNullOrWhiteSpace(row.DetectItems))
                    continue;
                // .NET Framework 4.8 的 string.Split 方法不支持带 StringSplitOptions 的重载，只能用 char[] 作为分隔符。
                string[] items = row.DetectItems.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in items)
                {
                    string detectItem = item.Trim();
                    if (string.IsNullOrEmpty(detectItem))
                        continue;

                    mapping[detectItem] = new DetectItemAddress
                    {
                        CommModbus = row.CommModbus,
                        SignalAddress = row.SignalAddress,
                        Judgment = row.Judgment
                    };
                }
            }
            return mapping;
        }
        /// <summary>
        /// 获取检测项与Modbus设备地址的映射字典
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, DetectItemAddress> GetMapping()
        {
            return mapping;
        }
        /// <summary>
        /// 方案加载时节点参数界面数据初始化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamSignalSendByModbus param)
            {
                // AI结果配置
                nodeSubscription1.SetText(param.Text1, param.Text2);

                // 设置Modbus设备下拉框
                int index1 = comboBoxModBusList.Items.IndexOf(param.ModbusName);
                comboBoxModBusList.SelectedIndex = index1 == -1 ? 0 : index1;
                // 反序列化后方案中的Modbus设备对象才是有效的，需要赋值给用到Modbus的节点参数中
                foreach (var dev in Solution.Instance.ModbusDevices)
                {
                    if (dev.UserDefinedName == param.ModbusName)
                    {
                        param.ModbusDevice = dev;
                        break;
                    }
                }
                // 自动重置信号
                checkBoxReSet.Checked = param.IsAutoReset;

                // 信号保持时间
                textBoxHoldTime.Text = param.SignalHoldTime.ToString();

                // 设置信号列表绑定数据
                dataGridView1.DataSource = param.BindingData;
                mapping = BuildMapping(param.BindingData);
            }

        }
        /// <summary>
        /// 初始化Modbus下拉框
        /// </summary>
        private void InitModbusComboBox()
        {
            string text1 = comboBoxModBusList.Text;

            comboBoxModBusList.Items.Clear();
            comboBoxModBusList.Items.Add("[未设置]");
            // 初始化Modbus列表,只显示添加的Modbus用户自定义名称
            foreach (var modbus in Solution.Instance.ModbusDevices)
            {
                // 从站作为服务器不能发起请求
                if (modbus.ModbusParam.DevType == Device.DevType.ModbusSlave)
                    continue;
                comboBoxModBusList.Items.Add(modbus.UserDefinedName);
            }
            int index1 = comboBoxModBusList.Items.IndexOf(text1);
            comboBoxModBusList.SelectedIndex = index1 == -1 ? 0 : index1;
        }

        void INodeParamForm.SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
            nodeSubscription1.Init(node);
            nodeBase = node;
        }

        /// <summary>
        /// 更新检测项列表视图
        /// </summary>
        /// <param name="aIInputInfo"></param>
        private void UpdataListBoxView(AIInputInfo aIInputInfo)
        {
            try
            {
                checkedListBox1.Items.Clear();

                if (aIInputInfo == null)
                    return;

                // 展平所有 DetectItems
                var allDetectItems = aIInputInfo.DetectItems.ToList();

                foreach (var item in allDetectItems)
                {
                    // 添加项到 CheckedListBox
                    checkedListBox1.Items.Add(item.Name, item.Enable); // 添加名称并设置初始选中状态
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"刷新检测项配置失败！原因：{ex.Message}", true);
            }
        }
        /// <summary>
        /// 自动配置1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            GenerateDataGridViewData1();
        }
        /// <summary>
        /// 自动配置2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            GenerateDataGridViewData2();
        }
        /// <summary>
        /// 增量配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            GenerateDataGridViewDataInc();
        }
        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            try
            {
                var param = new NodeParamSignalSendByModbus();
                param.Text1 = nodeSubscription1.GetText1();
                param.Text2 = nodeSubscription1.GetText2();
                param.ModbusName = comboBoxModBusList.Text;
                param.ModbusDevice = Solution.Instance.ModbusDevices
                    .FirstOrDefault(dev => dev.UserDefinedName == param.ModbusName);
                param.IsAutoReset = checkBoxReSet.Checked;
                param.SignalHoldTime = int.Parse(textBoxHoldTime.Text);
                param.BindingData = dataGridView1.DataSource as BindingList<DetectItemRow>;
                mapping = BuildMapping(param.BindingData);
                Params = param;
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数保存异常！原因:{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 初始化DataGridView控件
        /// </summary>
        private void InitializeDataGridView()
        {
            dataGridView1.AutoGenerateColumns = false;

            // 清除已有列
            dataGridView1.Columns.Clear();
            var color = Color.SteelBlue;

            // 列1：检测项（不可编辑字符串，多个检测项用分号分隔）
            var detectItemsColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DetectItems",
                HeaderText = "检测项",
                Name = "DetectItemsColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            };
            detectItemsColumn.HeaderCell.Style.BackColor = color;
            dataGridView1.Columns.Add(detectItemsColumn);

            // 判定列：下拉选项为 "检测项OK时", "检测项NG时"
            var judgmentItems = new List<string> { "检测项OK时", "检测项NG时" };

            var judgmentColumn = new DataGridViewComboBoxColumn
            {
                Name = "JudgmentColumn",
                HeaderText = "判定(可选)",
                DataPropertyName = "Judgment", // 对应 DetectItemRow.Judgment 属性
                DataSource = new BindingSource(judgmentItems, null),
                ValueType = typeof(string),     // 确保绑定类型正确
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };
            judgmentColumn.HeaderCell.Style.BackColor = color;
            dataGridView1.Columns.Add(judgmentColumn);

            // 新增列3：通信Modbus（不可编辑字符串）
            var commModbusColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CommModbus",
                HeaderText = "通信Modbus",
                Name = "CommModbusColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            };
            commModbusColumn.HeaderCell.Style.BackColor = color;
            dataGridView1.Columns.Add(commModbusColumn);

            // 列4：信号地址（可编辑字符串）
            var signalAddressColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SignalAddress",
                HeaderText = "信号地址(可编辑)",
                Name = "SignalAddressColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            signalAddressColumn.HeaderCell.Style.BackColor = color;
            dataGridView1.Columns.Add(signalAddressColumn);
        }

        /// <summary>
        /// 自动配置1的数据源构造
        /// </summary>
        private void GenerateDataGridViewData1()
        {
            BindingList<DetectItemRow> dataSource = new BindingList<DetectItemRow>();

            int signalAddressCounter = 0;

            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    var itemText = checkedListBox1.Items[i].ToString();

                    dataSource.Add(new DetectItemRow
                    {
                        DetectItems = itemText,
                        Judgment = "检测项NG时",
                        SignalAddress = signalAddressCounter.ToString(),
                        CommModbus = comboBoxModBusList.Text,
                    });

                    signalAddressCounter++;
                }
            }

            dataGridView1.DataSource = dataSource;
        }
        /// <summary>
        /// 自动配置2的数据源构造
        /// </summary>
        private void GenerateDataGridViewData2()
        {
            BindingList<DetectItemRow> dataSource = new BindingList<DetectItemRow>();
            string names = "";
            for (int i = 0; i < checkedListBox1.CheckedItems.Count; i++)
            {
                var itemText = checkedListBox1.CheckedItems[i].ToString();
                names += itemText;
                if (i != (checkedListBox1.CheckedItems.Count - 1))
                    names += ";";
            }
            dataSource.Add(new DetectItemRow
            {
                DetectItems = names,
                Judgment = "检测项NG时",
                SignalAddress = "0",
                CommModbus = comboBoxModBusList.Text,
            });
            dataGridView1.DataSource = dataSource;
        }
        /// <summary>
        /// 增量配置的数据源构造
        /// </summary>
        private void GenerateDataGridViewDataInc()
        {
            var dataSource = dataGridView1.DataSource as BindingList<DetectItemRow>;
            int signalAddressCounter = 0;
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                if (checkedListBox1.GetItemChecked(i))
                {
                    var itemText = checkedListBox1.Items[i].ToString();

                    dataSource.Add(new DetectItemRow
                    {
                        DetectItems = itemText,
                        Judgment = "检测项NG时",
                        SignalAddress = signalAddressCounter.ToString(),
                        CommModbus = comboBoxModBusList.Text,
                    });

                    signalAddressCounter++;
                }
            }
            dataGridView1.DataSource = dataSource;
        }
        /// <summary>
        /// 一键全选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxAutoSelect_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxAutoSelect.Checked)
            {
                // 全选
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    checkedListBox1.SetItemChecked(i, true);
                }
            }
            else
            {
                // 取消全选
                for (int i = 0; i < checkedListBox1.Items.Count; i++)
                {
                    checkedListBox1.SetItemChecked(i, false);
                }
            }
        }

    }

    /// <summary>
    /// 信号列表数据源类
    /// </summary>
    internal class DetectItemRow
    {
        // 检测项，例如："露线芯长度;咬胶长度"
        public string DetectItems { get; set; }

        // 判定结果，“检测项OK时” 或 “检测项NG时”
        public string Judgment { get; set; }

        // Modbus线圈地址，例如："0"
        public string SignalAddress { get; set; }

        // 通信Modbus
        public string CommModbus { get; set; }

    }
}
