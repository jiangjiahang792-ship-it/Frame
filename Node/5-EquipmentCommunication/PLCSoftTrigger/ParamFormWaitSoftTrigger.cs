using Logger;
using Sunny.UI;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PLCSoftTrigger
{
    public partial class ParamFormWaitSoftTrigger : FormBase, INodeParamForm
    {
        public INodeParam Params { get; set; }
        
        public ParamFormWaitSoftTrigger()
        {
            InitializeComponent();
            Shown += ParamFormWaitSoftTrigger_Shown;
            InitPLCComboBox();
            InitDataTypeComboBox();
        }

        private void ParamFormWaitSoftTrigger_Shown(object sender, EventArgs e)
        {
            InitPLCComboBox();
            InitDataTypeComboBox();
        }

        public void SetNodeBelong(NodeBase node) { }

        /// <summary>
        /// 初始化PLC下拉框
        /// </summary>
        private void InitPLCComboBox()
        {
            string text1 = comboBox1.Text;

            comboBox1.Items.Clear();
            comboBox1.Items.Add("[未设置]");
            // 初始化PLC列表,只显示添加的PLC用户自定义名称
            foreach (var plc in Solution.Instance.PlcDevices)
            {
                comboBox1.Items.Add(plc.UserDefinedName);
            }
            int index1 = comboBox1.Items.IndexOf(text1);
            if (index1 == -1)
                comboBox1.SelectedIndex = 0;
            else
                comboBox1.SelectedIndex = index1;
        }

        /// <summary>初始化读取数据类型下拉框，保持与PLC读取节点一致的中文显示。</summary>
        private void InitDataTypeComboBox()
        {
            string text = comboBoxDataType.Text;
            comboBoxDataType.Items.Clear();
            comboBoxDataType.Items.Add("布尔");
            comboBoxDataType.Items.Add("16位整数");
            comboBoxDataType.Items.Add("32位整数");
            comboBoxDataType.Items.Add("64位整数");
            comboBoxDataType.Items.Add("浮点");
            comboBoxDataType.Items.Add("字符串");
            int index = comboBoxDataType.Items.IndexOf(text);
            comboBoxDataType.SelectedIndex = index >= 0 ? index : 0;
            RefreshStringLengthEnabled();
        }

        /// <summary>
        /// 点击保存当前参数配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.Text.IsNullOrEmpty() || comboBox1.Text == "[未设置]")
            {
                LogHelper.AddLog(MsgLevel.Exception, "PLC不能为空！", true);
                MessageBoxTD.Show("PLC不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(this.textBox1.Text))
            {
                LogHelper.AddLog(MsgLevel.Exception, "信号地址为空", true);
                MessageBoxTD.Show("信号地址为空", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(this.textBoxCompareValue.Text))
            {
                LogHelper.AddLog(MsgLevel.Exception, "触发对比值为空", true);
                MessageBoxTD.Show("触发对比值为空", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string dataType = GetDataTypeName(comboBoxDataType.Text);
            ushort stringLength = 1;
            if (dataType == typeof(string).Name &&
                (!ushort.TryParse(textBoxStringLength.Text, out stringLength) || stringLength == 0))
            {
                LogHelper.AddLog(MsgLevel.Exception, "字符串读取长度必须为1到65535的整数！", true);
                MessageBoxTD.Show("字符串读取长度必须为1到65535的整数！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string compareValue;
            try
            {
                compareValue = PlcSoftTriggerValueMatcher.NormalizeCompareValue(dataType, textBoxCompareValue.Text);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"触发对比值格式错误：{ex.Message}", true);
                MessageBoxTD.Show($"触发对比值格式错误：{ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            //查找当前选择的PLC
            IPlc plc = null;
            foreach (var plcTmp in Solution.Instance.PlcDevices)
            {
                if(plcTmp.UserDefinedName == comboBox1.Text)
                {
                    plc = plcTmp;
                    break;
                }
            }

            NodeParamWaitSoftTrigger param = new NodeParamWaitSoftTrigger();
            param.Plc = plc;
            param.PlcName = comboBox1.Text;
            param.InitAddress = this.textBox1.Text;
            param.Address = this.textBox1.Text;
            param.DataType = dataType;
            param.CompareValue = compareValue;
            param.StringLength = stringLength;
            param.Reset = checkBoxReset.Checked;
            Params = param;
            Hide();
        }

        /// <summary>
        /// 窗口每次显示时都要刷新下拉框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormRead_Shown(object sender, EventArgs e)
        {
            InitPLCComboBox();
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamWaitSoftTrigger param)
            {
                int index = comboBox1.Items.IndexOf(param.PlcName);
                comboBox1.SelectedIndex = index == -1 ? 0 : index;
                param.Address = param.InitAddress; // 节点监听信号地址以初始值为准
                textBox1.Text = param.Address;
                comboBoxDataType.Text = GetDataTypeDisplayName(param.DataType);
                textBoxCompareValue.Text = string.IsNullOrWhiteSpace(param.CompareValue) ? "True" : param.CompareValue;
                textBoxStringLength.Text = param.StringLength <= 0 ? "1" : param.StringLength.ToString();
                RefreshStringLengthEnabled();
                checkBoxReset.Checked = param.Reset;
                foreach (var plc in Solution.Instance.PlcDevices)
                {
                    if(plc.UserDefinedName == param.PlcName)
                    {
                        param.Plc = plc;
                        break;
                    }
                }
            }
        }

        /// <summary>数据类型改变时只在字符串类型启用读取长度。</summary>
        private void comboBoxDataType_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshStringLengthEnabled();
        }

        /// <summary>刷新字符串长度控件启用状态。</summary>
        private void RefreshStringLengthEnabled()
        {
            bool isString = comboBoxDataType.Text == "字符串";
            labelStringLength.Enabled = isString;
            textBoxStringLength.Enabled = isString;
        }

        /// <summary>将界面中文数据类型转换为方案保存的真实类型名。</summary>
        private static string GetDataTypeName(string displayName)
        {
            switch (displayName)
            {
                case "布尔": return typeof(bool).Name;
                case "16位整数": return typeof(short).Name;
                case "32位整数": return typeof(int).Name;
                case "64位整数": return typeof(long).Name;
                case "浮点": return typeof(float).Name;
                case "字符串": return typeof(string).Name;
                default: return typeof(bool).Name;
            }
        }

        /// <summary>将方案保存的真实类型名转换为界面中文显示。</summary>
        private static string GetDataTypeDisplayName(string dataType)
        {
            if (dataType == typeof(short).Name) return "16位整数";
            if (dataType == typeof(int).Name) return "32位整数";
            if (dataType == typeof(long).Name) return "64位整数";
            if (dataType == typeof(float).Name) return "浮点";
            if (dataType == typeof(string).Name) return "字符串";
            return "布尔";
        }
    }
}
