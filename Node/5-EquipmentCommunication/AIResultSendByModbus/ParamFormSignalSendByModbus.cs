using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.AIResultSendByModbus
{
    internal partial class ParamFormSignalSendByModbus : Form, INodeParamForm
    {
        private NodeBase nodeBase;
        public ParamFormSignalSendByModbus()
        {
            InitializeComponent();
            InitModbusComboBox();
            InitializeDataGridView();
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
                if(node is NodeTDAI nodeTDAI)
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

                // 设置信号列表绑定数据
                dataGridView1.DataSource = param.BindingData;
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

        }
        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            var param = new NodeParamSignalSendByModbus();
            param.Text1 = nodeSubscription1.GetText1();
            param.Text2 = nodeSubscription1.GetText2();
            param.ModbusName = comboBoxModBusList.Text;
            param.ModbusDevice = Solution.Instance.ModbusDevices
                .FirstOrDefault(dev => dev.UserDefinedName == param.ModbusName);
            param.BindingData = dataGridView1.DataSource as BindingList<DetectItemRow>;
            Params = param;
            Hide();
        }

        /// <summary>
        /// 初始化DataGridView控件
        /// </summary>
        private void InitializeDataGridView()
        {
            dataGridView1.AutoGenerateColumns = false;

            // 清除已有列
            dataGridView1.Columns.Clear();

            // 列1：检测项（可编辑字符串，多个检测项用分号分隔）
            var detectItemsColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DetectItems",
                HeaderText = "检测项",
                Name = "DetectItemsColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dataGridView1.Columns.Add(detectItemsColumn);

            // 判定列：下拉选项为 "检测项OK时", "检测项NG时"
            var judgmentItems = new List<string> { "检测项OK时", "检测项NG时" };

            var judgmentColumn = new DataGridViewComboBoxColumn
            {
                Name = "JudgmentColumn",
                HeaderText = "判定",
                DataPropertyName = "Judgment", // 对应 DetectItemRow.Judgment 属性
                DataSource = new BindingSource(judgmentItems, null),
                //DisplayMember = "ToString",     // 显示字符串本身
                //ValueMember = "ToString",       // 值也是字符串本身
                ValueType = typeof(string),     // 确保绑定类型正确
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            dataGridView1.Columns.Add(judgmentColumn);

            // 列3：信号地址（可编辑字符串）
            var signalAddressColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "SignalAddress",
                HeaderText = "信号地址",
                Name = "SignalAddressColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dataGridView1.Columns.Add(signalAddressColumn);

            // 新增列4：通信Modbus（可编辑字符串）
            var commModbusColumn = new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CommModbus",
                HeaderText = "通信Modbus",
                Name = "CommModbusColumn",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dataGridView1.Columns.Add(commModbusColumn);

            // 新增列5：信号等级（下拉框，选项为“一级”、“二级”、“三级”）
            var levelItems = new Dictionary<string, SignalLevel>
            {
                { "一级", SignalLevel.LVL1 },
                { "二级", SignalLevel.LVL2 },
                { "三级", SignalLevel.LVL3 }
            };
            var signalLevelColumn = new DataGridViewComboBoxColumn
            {
                Name = "SignalLevelColumn",
                HeaderText = "信号优先级",
                DataPropertyName = "Level", // 注意这里对应 DetectItemRow.Level 属性
                DataSource = new BindingSource(levelItems, null),
                DisplayMember = "Key",     // 显示中文名
                ValueMember = "Value",     // 实际值是 SignalLevel 枚举
                ValueType = typeof(SignalLevel), // 确保绑定类型匹配
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                
            };
            dataGridView1.Columns.Add(signalLevelColumn);
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
                        Judgment = "检测项OK时",
                        SignalAddress = signalAddressCounter.ToString(),
                        CommModbus = comboBoxModBusList.Text,
                        Level = SignalLevel.LVL3
                    });

                    signalAddressCounter++;
                }
            }

            dataGridView1.DataSource = dataSource;
        }
    }



    /// <summary>
    /// 信号列表数据源类
    /// </summary>
    internal class DetectItemRow
    {
        // 检测项，例如："露线芯长度;咬胶长度"
        public string DetectItems { get; set; }

        // 判定结果，“均OK时” 或 “均NG时”
        public string Judgment { get; set; }

        // Modbus线圈地址，例如："0"
        public string SignalAddress { get; set; }

        // 通信Modbus 地址
        public string CommModbus { get; set; }

        // 信号等级（1~3级）
        public SignalLevel Level { get; set; }

    }
    /// <summary>
    /// 信号优先级,1级最高
    /// </summary>
    internal enum SignalLevel
    {
        LVL1,
        LVL2,
        LVL3
    }

}
