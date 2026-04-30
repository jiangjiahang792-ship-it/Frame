using System;
using Logger;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ERUIIO
{
    public partial class ParamFormERUIIO : FormBase, INodeParamForm
    {
        public ParamFormERUIIO()
        {
            InitializeComponent();
            InitModbusComboBox();
            comboBoxModbusDev.SelectedIndex = 0;
            comboBoxSelectedOperation.SelectedIndex = 0;
            comboBoxIONumber.SelectedIndex = 0;
        }
        /// <summary>
        /// 显示时初始化Modbus列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormERUIIO_Shown(object sender, EventArgs e)
        {
            // 初始化Modbus列表
            InitModbusComboBox();
        }

        /// <summary>
        /// 初始化Modbus下拉框
        /// </summary>
        private void InitModbusComboBox()
        {
            string text1 = comboBoxModbusDev.Text;

            comboBoxModbusDev.Items.Clear();
            comboBoxModbusDev.Items.Add("[未设置]");
            // 初始化Modbus列表,只显示添加的Modbus用户自定义名称
            foreach (var modbus in Solution.Instance.ModbusDevices)
            {
                if (modbus.ModbusParam.DevType == Device.DevType.ModbusRTUPoll
                || modbus.ModbusParam.DevType == Device.DevType.ModbusTcpPoll)
                    comboBoxModbusDev.Items.Add(modbus.UserDefinedName);
            }
            int index1 = comboBoxModbusDev.Items.IndexOf(text1);
            comboBoxModbusDev.SelectedIndex = index1 == -1 ? 0 : index1;
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node) 
        {
            getIOValue1.Init(node);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamERUIIO param)
            {
                // modbus设备列表初始化
                comboBoxModbusDev.Items.Clear();
                foreach (var modbus in Solution.Instance.ModbusDevices)
                {
                    comboBoxModbusDev.Items.Add(modbus.UserDefinedName);
                    if (modbus.UserDefinedName == param.ModbusName)
                    {
                        param.Modbus = modbus;
                    }
                }
                int index = comboBoxModbusDev.Items.IndexOf(param.ModbusName);
                comboBoxModbusDev.SelectedIndex = index == -1 ? 0 : index;
                // 读写操作
                comboBoxSelectedOperation.SelectedIndex = param.Operation == OperationType.读取输入信号 ? 0
                    : param.Operation == OperationType.读取输出信号 ? 1 : 2;
                // 是否读取指定位
                checkBoxSpecifiedBit.Checked = param.ReadSpecifiedBit;
                // IO编号
                comboBoxIONumber.Text = param.IONumber.ToString();
                // 是否固定信号
                if (param.IsFixed)
                    radioButtonIsFixed.Checked = true;
                else
                    radioButtonIsDynamic.Checked = true;
                // 固定信号值
                ioCheck1.SetIOCheckedStatus(param.FixedSignals);
                // 动态信号设置
                getIOValue1.SetIOSettings(param.IOSettings);
            }
        }
        /// <summary>
        /// 获取写入的动态信号值
        /// </summary>
        /// <returns></returns>
        public bool[] GetWriteValues()
        {
            return getIOValue1.GetValues();
        }
        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {

                if (string.IsNullOrEmpty(comboBoxModbusDev.Text) || comboBoxModbusDev.Text == "[未设置]")
                {
                    LogHelper.AddLog(MsgLevel.Exception, "Modbus不能为空！", true);
                    MessageBoxTD.Show("Modbus不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                //查找当前选择的Modbus
                IModbus modbus = null;
                foreach (var dev in Solution.Instance.ModbusDevices)
                {
                    if (dev.UserDefinedName == comboBoxModbusDev.Text)
                    {
                        modbus = dev;
                        break;
                    }
                }
                NodeParamERUIIO nodeParam = new NodeParamERUIIO();
                nodeParam.Modbus = modbus;
                nodeParam.ModbusName = modbus.UserDefinedName;
                nodeParam.Operation = comboBoxSelectedOperation.SelectedIndex == 0 ? OperationType.读取输入信号 
                    : comboBoxSelectedOperation.SelectedIndex == 1 ? OperationType.读取输出信号 : OperationType.写入输出信号;
                nodeParam.ReadSpecifiedBit = checkBoxSpecifiedBit.Checked;
                nodeParam.IONumber = uint.Parse(comboBoxIONumber.Text);
                nodeParam.IsContinue = checkBoxContinue.Checked;
                nodeParam.IsFixed = radioButtonIsFixed.Checked;
                nodeParam.FixedSignals = ioCheck1.GetValues();
                nodeParam.IOSettings = getIOValue1.GetIOSettings();
                Params = nodeParam;
                Hide();
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置不合法！");
                return;
            }
        }
        /// <summary>
        /// 切换固定信号和动态信号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonIsFixed.Checked)
            {
                ioCheck1.Enabled = true;
                getIOValue1.Enabled = false;
            }
            else
            {
                ioCheck1.Enabled = false;
                getIOValue1.Enabled = true;
            }
        }
        /// <summary>
        /// 选择的操作改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxSelectedOperation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(comboBoxSelectedOperation.SelectedIndex == 0 || comboBoxSelectedOperation.SelectedIndex == 1)
            {
                checkBoxSpecifiedBit.Enabled = true;
                comboBoxIONumber.Enabled = checkBoxSpecifiedBit.Checked;
                checkBoxContinue.Enabled = checkBoxSpecifiedBit.Checked;
                tableLayoutPanel6.Enabled = false;
                ioCheck1.Enabled = false;
                getIOValue1.Enabled = false;

            }
            else
            {
                checkBoxSpecifiedBit.Enabled = false;
                comboBoxIONumber.Enabled = false;
                checkBoxContinue.Enabled = false;
                tableLayoutPanel6.Enabled = true;

                ioCheck1.Enabled = radioButtonIsFixed.Checked;
                getIOValue1.Enabled = !radioButtonIsFixed.Checked;
            }
        }
        /// <summary>
        /// 读取指定位勾选改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxSpecifiedBit_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxIONumber.Enabled = checkBoxSpecifiedBit.Checked;
            checkBoxContinue.Enabled = checkBoxSpecifiedBit.Checked;
        }
    }
}
