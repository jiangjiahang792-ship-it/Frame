using System;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Device.Modbus;

namespace TDJS_Vision.Forms.SolRunParam
{
    internal partial class FormSolRunParam : Form
    {
        public FormSolRunParam()
        {
            InitializeComponent();
            comboBoxCoilValue.SelectedIndex = 1;
        }

        /// <summary>
        /// 窗体显示事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormSolRunParam_Shown(object sender, System.EventArgs e)
        {
            #region 创建选项卡

            RemoveAllExceptSpecifiedPage(tabControl1, "Modbus通信");//保留“Modbus通信”选项卡,删除所有
            foreach (var process in Solution.Instance.AllProcesses)
            {
                

                SolRunParamControl solRunParam = new SolRunParamControl();
                TabPage page = new TabPage();
                page.Text = process.ProcessName;
                page.Controls.Add(solRunParam);
                tabControl1.Controls.Add(page);
                try
                {
                    solRunParam.Init(process);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"“{process.ProcessName}”加载设置参数失败！原因：{ex.Message}", true);
                }
            }

            #endregion

            #region 显示Modbus通信参数

            if(Solution.Instance.ModbusDevices.Count > 0)
            {
                foreach (var modbus in Solution.Instance.ModbusDevices)
                {
                    if(modbus is ModbusPoll poll)
                    {
                        uiipTextBoxIP.Text = poll.ModbusParam.IP;
                        textBoxPort.Text = poll.ModbusParam.Port.ToString();
                        textBoxHoldTime.Text = poll.ModbusParam.HoldTime.ToString();
                    }
                }
            }

            #endregion
        }
        /// <summary>
        /// 除了保留指定的选项卡外，删除所有选项卡
        /// </summary>
        /// <param name="tabControl"></param>
        /// <param name="pageNameToKeep"></param>
        private void RemoveAllExceptSpecifiedPage(TabControl tabControl, string pageNameToKeep)
        {
            // 由于在遍历集合时不能直接修改该集合（如添加或移除项），我们需要使用逆序遍历或者创建一个新的列表来存储需要移除的项。
            // 这里我们选择直接在循环中操作，但使用了倒序遍历以避免索引问题。
            for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
            {
                if (tabControl.TabPages[i].Text != pageNameToKeep)
                {
                    tabControl.TabPages.RemoveAt(i);
                }
            }
        }
        /// <summary>
        /// 连接Modbus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (Solution.Instance.ModbusDevices.Count == 0)
            {
                MessageBox.Show("设备尚未添加！");
                return;
            }
            if (string.IsNullOrEmpty(uiipTextBoxIP.Text) || string.IsNullOrEmpty(textBoxPort.Text))
            {
                MessageBox.Show("参数不能为空！");
                return;
            }
            // 连接Modbus
            foreach (var modbus in Solution.Instance.ModbusDevices)
            {
                if (modbus is ModbusPoll poll)
                {
                    poll.Disconnect();
                    poll.ModbusParam.IP = uiipTextBoxIP.Text;
                    poll.ModbusParam.Port = int.Parse(textBoxPort.Text);
                    poll.ModbusParam.HoldTime = ushort.Parse(textBoxHoldTime.Text);
                    try
                    {
                        poll.Connect();
                        labelConnectInfo.Text = $"设备连接成功连接！";
                        labelConnectInfo.ForeColor = System.Drawing.Color.Green;
                    }
                    catch (Exception ex)
                    {
                        labelConnectInfo.Text = $"设备连接失败！原因：{ex.Message}";
                        labelConnectInfo.ForeColor = System.Drawing.Color.Red;
                    }
                    break;
                }
            }
        }
        /// <summary>
        /// 测试读取线圈值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRead_Click(object sender, EventArgs e)
        {
            if (Solution.Instance.ModbusDevices.Count == 0)
            {
                MessageBox.Show("设备尚未添加！");
                return;
            }
            bool hasPoll = false;
            foreach (var modbus in Solution.Instance.ModbusDevices)
            {
                if (modbus is ModbusPoll poll)
                {
                    try
                    {
                        if (poll == null || !poll.IsConnect) throw new Exception("Modbus设备已失效或未打开");
                        var value = poll.ReadCoils(ushort.Parse(textBoxAddress.Text), 1);
                        labelTestInfo.Text = $"读取线圈值成功！值为：{value[0]}";
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                    }
                    catch (Exception ex)
                    {
                        labelTestInfo.Text = $"读取线圈值失败！原因：{ex.Message}";
                        labelTestInfo.ForeColor = System.Drawing.Color.Red;
                    }
                    hasPoll = true;
                    break;
                }
            }
            if(!hasPoll)
            {
                MessageBox.Show("设备尚未添加！");
                return;
            }
        }
        /// <summary>
        /// 测试写入线圈值
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonWrite_Click(object sender, EventArgs e)
        {
            if (Solution.Instance.ModbusDevices.Count == 0)
            {
                MessageBox.Show("设备尚未添加！");
                return;
            }
            bool hasPoll = false;
            foreach (var modbus in Solution.Instance.ModbusDevices)
            {
                if (modbus is ModbusPoll poll)
                {
                    try
                    {
                        if (poll == null || !poll.IsConnect) throw new Exception("Modbus设备已失效或未打开");
                        poll.WriteSingleCoil(ushort.Parse(textBoxAddress.Text), comboBoxCoilValue.Text == "1");
                        labelTestInfo.Text = $"写入线圈值成功！";
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                    }
                    catch (Exception ex)
                    {
                        labelTestInfo.Text = $"写入线圈值失败！原因：{ex.Message}";
                        labelTestInfo.ForeColor = System.Drawing.Color.Red;
                    }
                    hasPoll = true;
                    break;
                }
            }
            if (!hasPoll)
            {
                MessageBox.Show("设备尚未添加！");
                return;
            }
        }
    }
}
