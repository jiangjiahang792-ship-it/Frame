using Logger;
using Sunny.UI;
using System;
using System.IO.Ports;
using System.Windows.Forms;
using TDJS_Vision.Device.PLC;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.PLCAdd
{
    public partial class FrmPLCNew : FormBase
    {
        /// <summary>
        /// PLC添加事件
        /// </summary>
        public static event EventHandler<PLCParms> PLCAddEvent;

        public FrmPLCNew()
        {
            InitializeComponent();
            InitPortComboBox();
        }

        /// <summary>
        /// 松下Mewtocol,搜索串口并添加到下拉框
        /// </summary>
        public void InitPortComboBox()
        {
            // 串口号获取
            comboBoxCom1.Items.Clear();
            comboBoxCom1.Items.AddRange(SerialPort.GetPortNames());
            if (comboBoxCom1.Items.Count > 0)
                this.comboBoxCom1.SelectedIndex = 0;

            // 波特率
            comboBoxBaute1.SelectedIndex = 1;
            // 数据位
            comboBoxDataBit1.SelectedIndex = 3;
            // 停止位
            comboBoxStopBit1.SelectedIndex = 0;
            // 校验位
            comboBoxParity1.SelectedIndex = 0;
        }

        /// <summary>
        /// 添加基恩士 Nano OverTcp，沿用现有 PLC 添加事件和重复设备检查规则。
        /// </summary>
        private void buttonConfirmKeyence_Click(object sender, EventArgs e)
        {
            AddKeyenceDevice(textBoxNameKeyence.Text, uiipTextBoxKeyence.Text,
                textBoxPortKeyence.Text, KeyenceProtocol.NanoOverTcp);
        }

        /// <summary>添加基恩士 KV300/Older 上位链路设备。</summary>
        private void buttonConfirmKeyenceOld_Click(object sender, EventArgs e)
        {
            AddKeyenceDevice(textBoxNameKeyenceOld.Text, uiipTextBoxKeyenceOld.Text,
                textBoxPortKeyenceOld.Text, KeyenceProtocol.Kv300Older);
        }

        /// <summary>两种基恩士协议共用现有参数校验、重复检查与 PLC 添加事件。</summary>
        private void AddKeyenceDevice(string name, string ipAddress, string portText, KeyenceProtocol protocol)
        {
            string deviceName = name.Trim();
            string ip = ipAddress.Trim();
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                MessageBoxTD.Show("请输入设备名称！");
                return;
            }

            if (!System.Net.IPAddress.TryParse(ip, out var address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                MessageBoxTD.Show("请输入有效的IP地址！");
                return;
            }

            if (!int.TryParse(portText.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBoxTD.Show("请输入1到65535之间的端口号！");
                return;
            }

            bool exist = Solution.Instance.PlcDevices.Exists(plc =>
                (plc.PLCParms.PlcConType == PlcConType.ETHERNET && plc.PLCParms.EthernetParms.IP == ip) ||
                plc.UserDefinedName == deviceName);
            if (exist)
            {
                MessageBoxTD.Show("PLC的IP地址或用户自定义名称已存在！");
                LogHelper.AddLog(MsgLevel.Warn, "PLC的IP地址或用户自定义名称已存在！", true);
                return;
            }

            try
            {
                var parms = new PLCParms
                {
                    DeviceBrand = Device.DeviceBrand.Keyence,
                    KeyenceProtocol = protocol,
                    UserDefinedName = deviceName,
                    PlcConType = PlcConType.ETHERNET,
                    EthernetParms = new EthernetParms(ip, port)
                };
                PLCAddEvent?.Invoke(this, parms);
                Hide();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"添加基恩士PLC失败：{ex}", true);
                MessageBoxTD.Show("添加基恩士PLC失败，请检查通信参数！");
            }
        }

        /// <summary>
        /// 点击添加三菱MC(Binary)设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConfirm1_Click(object sender, EventArgs e)
        {
            if(textBoxPort1.Text.IsNullOrEmpty() || textBoxName1.Text.IsNullOrEmpty())
            {
                MessageBoxTD.Show("请输入端口和设备名！"); return;
            }

            // 已添加设备冲突判断
            bool exist = Solution.Instance.PlcDevices.Exists(plc => plc.PLCParms.EthernetParms.IP == uiipTextBoxIP1.Text || plc.UserDefinedName == textBoxName1.Text);
            if (exist)
            {
                MessageBoxTD.Show("PLC的IP地址和用户自定义名称已存在！");
                LogHelper.AddLog(MsgLevel.Warn, "PLC的IP地址和用户自定义名称已存在！", true);
                return;
            }

            try
            {
                PLCParms pLCParms = new PLCParms();
                pLCParms.DeviceBrand = Device.DeviceBrand.Melsec;
                pLCParms.EthernetParms = new EthernetParms();
                pLCParms.UserDefinedName = textBoxName1.Text;
                pLCParms.PlcConType = PlcConType.ETHERNET;
                pLCParms.EthernetParms.IP = uiipTextBoxIP1.Text;
                pLCParms.EthernetParms.Port = int.Parse(textBoxPort1.Text);

                PLCAddEvent?.Invoke(this, pLCParms);
                Hide();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, "请检查PLC参数是否有误！\n" + ex, true);
                MessageBoxTD.Show("请检查PLC参数是否有误！");
            }
        }
        /// <summary>
        /// 松下Mewtocol，点击确定添加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConfirm2_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.textBoxName2.Text))
            {

                // 已添加设备冲突判断
                bool exist = Solution.Instance.PlcDevices.Exists(plc => plc.PLCParms.SerialParms.PortName == comboBoxCom1.Text || plc.UserDefinedName == textBoxName2.Text);
                if (exist)
                {
                    MessageBoxTD.Show("PLC的串口号和用户自定义名不能相同！");
                    LogHelper.AddLog(MsgLevel.Warn, "PLC的串口号和用户自定义名不能相同！", true);
                    return;
                }
                try
                {
                    PLCParms pLCParms = new PLCParms();
                    pLCParms.DeviceBrand = Device.DeviceBrand.Panasonic;
                    pLCParms.EthernetParms = new EthernetParms();

                    pLCParms.UserDefinedName = textBoxName2.Text;
                    pLCParms.PlcConType = PlcConType.COM;

                    pLCParms.SerialParms.PortName = comboBoxCom1.Text;
                    pLCParms.SerialParms.BaudRate = int.Parse(comboBoxBaute1.Text);
                    pLCParms.SerialParms.DataBits = int.Parse(comboBoxDataBit1.Text);
                    pLCParms.SerialParms.StopBits = comboBoxStopBit1.SelectedIndex == 0 ? StopBits.One : (comboBoxStopBit1.SelectedIndex == 1 ? StopBits.OnePointFive : StopBits.Two);
                    pLCParms.SerialParms.Parity = comboBoxParity1.SelectedIndex == 0 ? Parity.None : (comboBoxParity1.SelectedIndex == 1 ? Parity.Odd : Parity.Even);

                    PLCAddEvent?.Invoke(this, pLCParms);
                    Hide();
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Warn, "添加PLC时参数设置错误！\n" + ex, true);
                    MessageBoxTD.Show("请检查PLC参数是否有误！");
                }
            }
            else
            {
                MessageBoxTD.Show("请添加PLC设备名称！");
            }
        }
        /// <summary>
        /// 松下Mewtocol OverTcp， 点击确认添加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonConfirm3_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.textBoxName3.Text))
            {

                // 已添加设备冲突判断
                bool exist = Solution.Instance.PlcDevices.Exists(plc => plc.PLCParms.EthernetParms.IP == uiipTextBoxIP2.Text || plc.UserDefinedName == textBoxName3.Text);
                if (exist)
                {
                    MessageBoxTD.Show("PLC的IP地址和用户自定义名称已存在！");
                    LogHelper.AddLog(MsgLevel.Warn, "PLC的IP地址和用户自定义名称已存在！", true);
                    return;
                }
                try
                {
                    PLCParms pLCParms = new PLCParms();
                    pLCParms.DeviceBrand = Device.DeviceBrand.Panasonic;
                    pLCParms.EthernetParms = new EthernetParms();
                    pLCParms.UserDefinedName = textBoxName3.Text;
                    pLCParms.PlcConType = PlcConType.ETHERNET;
                    pLCParms.EthernetParms.IP = uiipTextBoxIP2.Text;
                    pLCParms.EthernetParms.Port = int.Parse(textBoxPort2.Text);

                    PLCAddEvent?.Invoke(this, pLCParms);
                    Hide();
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Warn, "添加PLC时参数设置错误！\n" + ex, true);
                    MessageBoxTD.Show("请检查PLC参数是否有误！");
                }
            }
            else
            {
                MessageBoxTD.Show("请添加PLC设备名称！");
            }
        }
    }
}
