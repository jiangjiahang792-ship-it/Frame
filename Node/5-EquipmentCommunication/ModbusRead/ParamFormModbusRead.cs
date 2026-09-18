using Logger;
using Sunny.UI;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead
{
    public partial class ParamFormModbusRead : FormBase, INodeParamForm
    {
        public INodeParam Params { get; set; }

        // 在 ParamFormModbusRead 类中定义一个新的委托类型
        public delegate Task AsyncEventHandler<T>(object sender, T e);

        // 定义事件
        public event AsyncEventHandler<EventArgs> RunHandler;

        /// <summary>编码下拉项对应的持久化名称，顺序与设计器一致。</summary>
        private static readonly string[] StringEncodingNames = { "us-ascii", "utf-8", "gb18030", "utf-16" };

        public ParamFormModbusRead()
        {
            InitializeComponent();
            InitModbusComboBox();
            comboBoxModbusDev.SelectedIndex = 0;
            comboBoxStringEncoding.SelectedIndex = 0;
            comboBoxStringByteOrder.SelectedIndex = 0;
            UpdateStringOptions();
        }

        public void SetReadResult(string result)
        {
            listBox1.Items.Add(result);
            // 自动滚动到最后一条
            listBox1.TopIndex = listBox1.Items.Count - 1;
        }

        public void SetNodeBelong(NodeBase node) { }

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

        /// <summary>
        /// 点击保存当前参数配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>校验并保存参数；失败时阻止关闭窗体或按旧参数执行。</summary>
        /// <returns>是否保存成功。</returns>
        private bool SaveParams()
        {
            if (comboBoxModbusDev.Text.IsNullOrEmpty() || comboBoxModbusDev.Text == "[未设置]")
            {
                LogHelper.AddLog(MsgLevel.Exception, "Modbus不能为空！", true);
                MessageBoxTD.Show("Modbus不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            ushort adress, length;
            try
            {
                adress = ushort.Parse(this.textBoxAddress.Text);
                length = ushort.Parse(this.textBoxLength.Text);
                if (length == 0)
                    throw new Exception("无效的起始地址或读取个数");
            }
            catch (Exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, "无效的起始地址或读取个数", true);
                MessageBoxTD.Show("无效的起始地址或读取个数", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (comboBox2.SelectedIndex < 0)
            {
                MessageBoxTD.Show("请选择读取数据类型。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (comboBox2.Text == "字符串" &&
                (length > 125 || (int)adress + length > 65536 ||
                 comboBoxStringEncoding.SelectedIndex < 0 || comboBoxStringByteOrder.SelectedIndex < 0))
            {
                MessageBoxTD.Show("请选择字符串编码和字节顺序；寄存器个数必须为 1 至 125，且不能超过地址范围。",
                    "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
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

            if (modbus == null)
            {
                MessageBoxTD.Show("所选 Modbus 设备不存在，请重新选择。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            NodeParamModbusRead nodeParamRead = new NodeParamModbusRead();
            nodeParamRead.Device = modbus;
            nodeParamRead.DeviceName = modbus.UserDefinedName;
            nodeParamRead.StartAddress = adress.ToString();
            switch (this.comboBox2.Text)
            {
                case "Bool":
                    nodeParamRead.DataType = RegistersType.Bool;
                    break;
                case "Short":
                    nodeParamRead.DataType = RegistersType.Short;
                    break;
                case "UShort":
                    nodeParamRead.DataType = RegistersType.UShort;
                    break;
                case "Int":
                    nodeParamRead.DataType = RegistersType.Int;
                    break;
                case "UInt":
                    nodeParamRead.DataType = RegistersType.UInt;
                    break;
                case "Float":
                    nodeParamRead.DataType = RegistersType.Float;
                    break;
                case "Double":
                    nodeParamRead.DataType = RegistersType.Double;
                    break;
                case "Long":
                    nodeParamRead.DataType = RegistersType.Long;
                    break;
                case "ULong":
                    nodeParamRead.DataType = RegistersType.ULong;
                    break;
                case "线圈":
                    nodeParamRead.DataType = RegistersType.线圈;
                    break;
                case "离散输入":
                    nodeParamRead.DataType = RegistersType.离散输入;
                    break;
                case "字符串":
                    nodeParamRead.DataType = RegistersType.String;
                    break;
                default:
                    break;
            }
            nodeParamRead.Count = length;
            nodeParamRead.StringEncodingName = StringEncodingNames[Math.Max(0, comboBoxStringEncoding.SelectedIndex)];
            nodeParamRead.StringLowByteFirst = comboBoxStringByteOrder.SelectedIndex == 1;
            Params = nodeParamRead;
            return true;
        }

        /// <summary>类型切换时刷新字符串专用参数的启用状态。</summary>
        /// <param name="sender">数据类型下拉框。</param>
        /// <param name="e">选择变更事件。</param>
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStringOptions();
        }

        /// <summary>说明字符串长度的单位，并仅在字符串类型下启用编码和字节顺序。</summary>
        private void UpdateStringOptions()
        {
            bool isString = comboBox2.Text == "字符串";
            label4.Text = isString ? "寄存器个数：\r\n（每个两字节）" : "读取个数：";
            labelStringEncoding.Enabled = comboBoxStringEncoding.Enabled = isString;
            labelStringByteOrder.Enabled = comboBoxStringByteOrder.Enabled = isString;
        }

        /// <summary>
        /// 窗口每次显示时都要刷新下拉框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormRead_Shown(object sender, EventArgs e)
        {
            InitModbusComboBox();
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamModbusRead param)
            {
                int index1 = comboBoxModbusDev.Items.IndexOf(param.DeviceName);
                comboBoxModbusDev.SelectedIndex = index1 == -1 ? 0 : index1;
                // 反序列化后方案中的Modbus设备对象才是完整的，需要赋值给用到Modbus的节点参数中
                foreach (var dev in Solution.Instance.ModbusDevices)
                {
                    if(dev.UserDefinedName == param.DeviceName)
                    {
                        param.Device = dev;
                        break;
                    }
                }
                textBoxAddress.Text = param.StartAddress.ToString();
                switch (param.DataType)
                {
                    case RegistersType.Bool:
                        comboBox2.SelectedIndex = 0;
                        break;
                    case RegistersType.Short:
                        comboBox2.SelectedIndex = 1;
                        break;
                    case RegistersType.UShort:
                        comboBox2.SelectedIndex = 2;
                        break;
                    case RegistersType.Int:
                        comboBox2.SelectedIndex = 3;
                        break;
                    case RegistersType.UInt:
                        comboBox2.SelectedIndex = 4;
                        break;
                    case RegistersType.Float:
                        comboBox2.SelectedIndex = 5;
                        break;
                    case RegistersType.Double:
                        comboBox2.SelectedIndex = 6;
                        break;
                    case RegistersType.Long:
                        comboBox2.SelectedIndex = 7;
                        break;
                    case RegistersType.ULong:
                        comboBox2.SelectedIndex = 8;
                        break;
                    case RegistersType.线圈:
                        comboBox2.SelectedIndex = 9;
                        break;
                    case RegistersType.离散输入:
                        comboBox2.SelectedIndex = 10;
                        break;
                    case RegistersType.String:
                        comboBox2.SelectedIndex = 11;
                        break;
                    default:
                        comboBox2.SelectedIndex = -1;
                        break;
                }
                textBoxLength.Text = param.Count.ToString();
                comboBoxStringEncoding.SelectedIndex = Array.IndexOf(StringEncodingNames, param.StringEncodingName);
                comboBoxStringByteOrder.SelectedIndex = param.StringLowByteFirst ? 1 : 0;
                UpdateStringOptions();
            }
        }

        /// <summary>使用当前合法参数执行读取，并显示结果或错误。</summary>
        /// <param name="sender">执行按钮。</param>
        /// <param name="e">点击事件。</param>
        private async void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams() || RunHandler == null)
                return;

            buttonRun.Enabled = false;
            try
            {
                await RunHandler.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                SetReadResult("读取失败：" + ex.Message);
            }
            finally
            {
                buttonRun.Enabled = true;
            }
        }

        private void 清空ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }
    }
}
