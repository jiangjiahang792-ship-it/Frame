using Logger;
using Sunny.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead;

namespace TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite
{
    public partial class ParamFormModbusWrite : FormBase, INodeParamForm
    {
        public INodeParam Params { get; set; }

        // 在 ParamFormModbusRead 类中定义一个新的委托类型
        public delegate Task AsyncEventHandler<T>(object sender, T e);

        // 定义事件
        public event AsyncEventHandler<EventArgs> RunHandler;

        public ParamFormModbusWrite()
        {
            InitializeComponent();
            InitModbusComboBox();
            comboBoxType.SelectedIndex = 0;
            RefreshSubscriptionContract();
        }

        public void SetNodeBelong(NodeBase node) 
        {
            RefreshSubscriptionContract();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 获取订阅值，并按当前 Modbus 寄存器类型格式化为写入文本。
        /// </summary>
        public string GetSubValue()
        {
            object value = nodeSubscription1.GetValue<object>();
            NodeParamModbusWrite param = Params as NodeParamModbusWrite;
            RegistersType dataType = param == null ? GetSelectedDataType() : param.DataType;
            return FormatSubscribedValue(value, dataType);
        }

        /// <summary>
        /// 按目标寄存器类型格式化订阅值，保留 Short 等数值的真实内容。
        /// </summary>
        /// <param name="value">订阅读取到的真实值。</param>
        /// <param name="dataType">目标 Modbus 寄存器类型。</param>
        /// <returns>可由现有写入逻辑解析的逗号分隔文本。</returns>
        internal static string FormatSubscribedValue(object value, RegistersType dataType)
        {
            Array array = value as Array;
            if (array == null)
                return FormatSingleSubscribedValue(value, dataType);

            var values = new List<string>(array.Length);
            foreach (object item in array)
                values.Add(FormatSingleSubscribedValue(item, dataType));
            return string.Join(",", values);
        }

        /// <summary>
        /// 格式化一个订阅值，并对数值缩窄执行范围检查。
        /// </summary>
        /// <param name="value">单个订阅值。</param>
        /// <param name="dataType">目标寄存器类型。</param>
        /// <returns>使用固定区域格式的写入文本。</returns>
        private static string FormatSingleSubscribedValue(object value, RegistersType dataType)
        {
            Type targetType = ModbusReadDynamicVariable.GetValueType(dataType);
            object converted = SubscriptionTypeCompatibility.ConvertValue(
                value,
                targetType,
                NumericConversionMode.Checked);
            if (converted is bool)
                return (bool)converted ? "1" : "0";

            IFormattable formattable = converted as IFormattable;
            return formattable == null
                ? Convert.ToString(converted, CultureInfo.InvariantCulture)
                : formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 根据当前写入类型刷新订阅输入契约。
        /// </summary>
        private void RefreshSubscriptionContract()
        {
            RegistersType dataType = GetSelectedDataType();
            Type targetType = ModbusReadDynamicVariable.GetValueType(dataType);
            SubscriptionDataCategory category = targetType == typeof(bool)
                ? SubscriptionDataCategory.Boolean
                : SubscriptionDataCategory.Number;
            nodeSubscription1.SetInputContract(new SubscriptionInputContract(
                new[] { category },
                targetType,
                new[] { SubscriptionValueMultiplicity.Single },
                targetType == typeof(bool)
                    ? NumericConversionMode.None
                    : NumericConversionMode.Checked));
        }

        /// <summary>
        /// 获取界面当前选择的 Modbus 数据类型。
        /// </summary>
        /// <returns>当前寄存器数据类型。</returns>
        private RegistersType GetSelectedDataType()
        {
            switch (comboBoxType.Text)
            {
                case "Bool": return RegistersType.Bool;
                case "Short": return RegistersType.Short;
                case "UShort": return RegistersType.UShort;
                case "Int": return RegistersType.Int;
                case "UInt": return RegistersType.UInt;
                case "Float": return RegistersType.Float;
                case "Double": return RegistersType.Double;
                case "Long": return RegistersType.Long;
                case "ULong": return RegistersType.ULong;
                case "线圈": return RegistersType.线圈;
                default: return RegistersType.Bool;
            }
        }

        /// <summary>
        /// Modbus 数据类型变化后立即刷新可订阅结果范围。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void comboBoxType_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshSubscriptionContract();
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
                // 从站作为服务器不能发起请求
                if (modbus.ModbusParam.DevType == Device.DevType.ModbusTcpSlave)
                    continue;
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
            SaveParam();
            Hide();
        }

        private void SaveParam()
        {
            if (comboBoxModbusDev.Text.IsNullOrEmpty() || comboBoxModbusDev.Text == "[未设置]")
            {
                LogHelper.AddLog(MsgLevel.Exception, "Modbus不能为空！", true);
                MessageBoxTD.Show("Modbus不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ushort adress;
            try
            {
                adress = ushort.Parse(this.textBoxAddress.Text);
            }
            catch (Exception)
            {
                LogHelper.AddLog(MsgLevel.Exception, "无效的起始地址", true);
                MessageBoxTD.Show("无效的起始地址", "错误提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            NodeParamModbusWrite nodeParamWrite = new NodeParamModbusWrite();
            nodeParamWrite.Device = modbus;
            nodeParamWrite.DeviceName = modbus.UserDefinedName;
            nodeParamWrite.StartAddress = adress.ToString();
            nodeParamWrite.DataType = GetSelectedDataType();
            
            if (radioButton2.Checked)
            {
                nodeParamWrite.IsSubscribed = false;
                nodeParamWrite.Data = textBoxData.Text;
            }else
            {
                nodeParamWrite.IsSubscribed = true;
                nodeParamWrite.Text1 = nodeSubscription1.GetText1();
                nodeParamWrite.Text2 = nodeSubscription1.GetText2();
            }
            Params = nodeParamWrite;
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
            if (Params is NodeParamModbusWrite param)
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
                        comboBoxType.SelectedIndex = 0;
                        break;
                    case RegistersType.Short:
                        comboBoxType.SelectedIndex = 1;
                        break;
                    case RegistersType.UShort:
                        comboBoxType.SelectedIndex = 2;
                        break;
                    case RegistersType.Int:
                        comboBoxType.SelectedIndex = 3;
                        break;
                    case RegistersType.UInt:
                        comboBoxType.SelectedIndex = 4;
                        break;
                    case RegistersType.Float:
                        comboBoxType.SelectedIndex = 5;
                        break;
                    case RegistersType.Double:
                        comboBoxType.SelectedIndex = 6;
                        break;
                    case RegistersType.Long:
                        comboBoxType.SelectedIndex = 7;
                        break;
                    case RegistersType.ULong:
                        comboBoxType.SelectedIndex = 8;
                        break;
                    case RegistersType.线圈:
                        comboBoxType.SelectedIndex = 9;
                        break;
                    default:
                        comboBoxType.SelectedIndex = -1;
                        break;
                }

                if (param.IsSubscribed)
                {
                    radioButton1.Checked = true;
                    nodeSubscription1.SetText(param.Text1, param.Text2);
                }
                else
                {
                    radioButton2.Checked = true;
                    textBoxData.Text = param.Data.ToString();
                }
            }
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            SaveParam();
            RunHandler?.Invoke(this, EventArgs.Empty);
        }

        private void radioButton_CheckedChanged(object sender, EventArgs e)
        {
            if(radioButton1.Checked)
                tabControl1.SelectedIndex = 0;
            else
                tabControl1.SelectedIndex = 1;
        }
    }
}
