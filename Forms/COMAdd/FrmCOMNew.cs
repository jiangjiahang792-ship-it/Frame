using Logger;
using System;
using System.IO.Ports;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using TDJS_Vision.Device;
using TDJS_Vision.Device.COM;
using TDJS_Vision.Device.Light;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.COMAdd
{
    public partial class FrmCOMNew : FormBase
    {
        /// <summary>
        /// COM添加事件
        /// </summary>
        public event EventHandler<SerialPortConfig> ComAddEvent;

        public FrmCOMNew()
        {
            InitializeComponent();
            InitPortComboBox();
        }

        /// <summary>
        /// 搜索串口并添加到下拉框
        /// </summary>
        public void InitPortComboBox()
        {
            // 串口号获取
            comboBoxPortName.Items.Clear();
            foreach (var com in SerialPort.GetPortNames())
            {
                comboBoxPortName.Items.Add(com);
            }
            if (comboBoxPortName.Items.Count > 0)
                this.comboBoxPortName.SelectedIndex = 0;

            // 波特率
            comboBoxBauteRate.SelectedIndex = 1;
            // 数据位
            comboBoxDataBits.SelectedIndex = 3;
            // 停止位
            comboBoxStopBits.SelectedIndex = 0;
            // 校验位
            comboBoxParity.SelectedIndex = 0;

        }

        /// <summary>
        /// 确认按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            SerialPortConfig comParam = new SerialPortConfig();
            try
            {
                comParam.PortName = this.comboBoxPortName.Text;
                comParam.BaudRate = int.Parse(this.comboBoxBauteRate.Text);
                comParam.DataBits = int.Parse(this.comboBoxDataBits.Text);
                comParam.StopBits = comboBoxStopBits.Text == "1" ? StopBits.One : comboBoxStopBits.Text == "1.5" ? StopBits.OnePointFive : StopBits.Two;
                comParam.Parity = comboBoxParity.Text == "奇" ? Parity.Odd : comboBoxParity.Text == "偶" ? Parity.Even : Parity.None;

                // 已添加设备冲突判断
                foreach (var dev in Solution.Instance.AllDevices)
                {
                    if(dev is ComDevice comDev)
                    {
                        if (comDev.ComParams.PortName == comParam.PortName)
                        {
                            MessageBoxTD.Show("该设备已存在！");
                            return;
                        }
                    }
                }

                ComAddEvent?.Invoke(this, comParam);
                this.Hide();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, "添加串口时参数设置错误！\n" + ex, true);
                MessageBoxTD.Show("请检查参数是否有误！");
            }
        }
    }
}
