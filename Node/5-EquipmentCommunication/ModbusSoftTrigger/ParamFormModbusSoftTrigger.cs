using Logger;
using Sunny.UI;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusSoftTrigger
{
    public partial class ParamFormModbusSoftTrigger : FormBase, INodeParamForm
    {
        public INodeParam Params { get; set; }

        public ParamFormModbusSoftTrigger()
        {
            InitializeComponent();
            InitModBusComboBox();
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormModbusSoftTrigger_Load(object sender, EventArgs e)
        {
            InitModBusComboBox();
        }

        /// <summary>
        /// 初始化modbus下拉框
        /// </summary>
        private void InitModBusComboBox()
        {
            string text1 = comboBoxModBusList.Text;

            comboBoxModBusList.Items.Clear();
            comboBoxModBusList.Items.Add("[未设置]");
            // 初始化ModBus列表,只显示添加的ModBus用户自定义名称
            foreach (var mod in Solution.Instance.ModbusDevices)
            {
                comboBoxModBusList.Items.Add(mod.UserDefinedName);
            }
            int index1 = comboBoxModBusList.Items.IndexOf(text1);
            if (index1 == -1)
                comboBoxModBusList.SelectedIndex = 0;
            else
                comboBoxModBusList.SelectedIndex = index1;
        }
        public void SetNodeBelong(NodeBase node){}
        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBoxModBusList.Text.IsNullOrEmpty() || comboBoxModBusList.Text == "[未设置]")
            {
                LogHelper.AddLog(MsgLevel.Exception, "PLC不能为空！", true);
                MessageBoxTD.Show("PLC不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(this.textBoxAddress.Text))
            {
                LogHelper.AddLog(MsgLevel.Exception, "信号地址为空", true);
                MessageBoxTD.Show("信号地址为空", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //查找当前选择的modbus
            IModbus modBus = null;
            foreach (var modbusTmp in Solution.Instance.ModbusDevices)
            {
                if (modbusTmp.UserDefinedName == comboBoxModBusList.Text)
                {
                    modBus = modbusTmp;
                    break;
                }
            }

            NodeParamModbusSoftTrigger param = new NodeParamModbusSoftTrigger();
            param.modbus = modBus;
            param.ModBusName = comboBoxModBusList.Text;
            param.InitAddress = this.textBoxAddress.Text;
            param.Address = this.textBoxAddress.Text;
            switch (this.comboBoxDataType.Text)
            {
                case "Bool":
                    param.Type = RegistersType.Bool;
                    break;
                case "Short":
                    param.Type = RegistersType.Short;
                    break;
                case "UShort":
                    param.Type = RegistersType.UShort;
                    break;
                case "Int":
                    param.Type = RegistersType.Int;
                    break;
                case "UInt":
                    param.Type = RegistersType.UInt;
                    break;
                case "Float":
                    param.Type = RegistersType.Float;
                    break;
                case "Double":
                    param.Type = RegistersType.Double;
                    break;
                case "Long":
                    param.Type = RegistersType.Long;
                    break;
                case "ULong":
                    param.Type = RegistersType.ULong;
                    break;
                case "线圈":
                    param.Type = RegistersType.线圈;
                    break;
                case "离散输入":
                    param.Type = RegistersType.离散输入;
                    break;
                default:
                    break;
            }
            param.Reset = checkBoxReset.Checked;
            Params = param;
            Hide();
        }

        /// <summary>
        /// 反序列化代码
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamModbusSoftTrigger param)
            {
                int index = comboBoxModBusList.Items.IndexOf(param.ModBusName);
                comboBoxModBusList.SelectedIndex = index == -1 ? 0 : index;
                param.Address = param.InitAddress; // 节点监听信号地址以初始值为准
                textBoxAddress.Text = param.Address;
                foreach (var modbus in Solution.Instance.ModbusDevices)
                {
                    if (modbus.UserDefinedName == param.ModBusName)
                    {
                        param.modbus = modbus;
                        break;
                    }
                }
                switch (param.Type)
                {
                    case RegistersType.Bool:
                        comboBoxDataType.SelectedIndex = 0;
                        break;
                    case RegistersType.Short:
                        comboBoxDataType.SelectedIndex = 1;
                        break;
                    case RegistersType.UShort:
                        comboBoxDataType.SelectedIndex = 2;
                        break;
                    case RegistersType.Int:
                        comboBoxDataType.SelectedIndex = 3;
                        break;
                    case RegistersType.UInt:
                        comboBoxDataType.SelectedIndex = 4;
                        break;
                    case RegistersType.Float:
                        comboBoxDataType.SelectedIndex = 5;
                        break;
                    case RegistersType.Double:
                        comboBoxDataType.SelectedIndex = 6;
                        break;
                    case RegistersType.Long:
                        comboBoxDataType.SelectedIndex = 7;
                        break;
                    case RegistersType.ULong:
                        comboBoxDataType.SelectedIndex = 8;
                        break;
                    case RegistersType.线圈:
                        comboBoxDataType.SelectedIndex = 9;
                        break;
                    case RegistersType.离散输入:
                        comboBoxDataType.SelectedIndex = 10;
                        break;
                    default:
                        comboBoxDataType.SelectedIndex = -1;
                        break;
                }

                checkBoxReset.Checked = param.Reset;
            }
        }
    }
}
