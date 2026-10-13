using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO.Ports;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.SolRunParam
{
    public partial class FormSolRunParam : FormBase
    {
        private IDevice curDevice = null;
        private string selectedProcessName = string.Empty;
        private Process selectedProcess;
        private bool showSingleProcessOnly = false;
        // 外部指定的AI检测节点，用于多个AI节点时精准打开对应参数。
        private NodeTDAI targetAiNode = null;
        /// <summary>
        /// 外部指定的运行参数显示范围，用于图像窗口手动调参时按ROI绘制来源过滤。
        /// </summary>
        private RunParamDisplayFilter runParamDisplayFilter = null;
        /// <summary>从窗体构造到参数就绪的计时，不包含用户在窗口中的停留时间。</summary>
        private readonly Stopwatch _openStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// 初始化流程参数窗体。
        /// </summary>
        public FormSolRunParam()
        {
            InitializeComponent();
            TDJS_Vision.UiLocalization.Attach(this);
            comboBoxCoilValue.SelectedIndex = 1;
            BindLanguage();
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            Disposed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        /// <summary>显示非模态调参窗口，切回时拒绝已失效、运行中或权限不足的上下文。</summary>
        /// <param name="owner">所属窗口，关闭所属窗口时一并关闭调参窗口。</param>
        /// <param name="validate">检查当前方案、权限与流程状态的回调。</param>
        public void ShowModeless(IWin32Window owner, Action validate)
        {
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            validate();
            Activated += (sender, args) =>
            {
                try { validate(); }
                catch (InvalidOperationException ex)
                {
                    LogHelper.AddLog(MsgLevel.Warn, "手动调参窗口已失效并关闭：" + ex.Message, true);
                    // 激活发生在Show内部时，延迟到消息循环关闭，避免中断窗体创建。
                    BeginInvoke(new Action(() => { if (!IsDisposed) Close(); }));
                }
            };
            Show(owner);
        }

        /// <summary>
        /// 初始化只显示指定流程的参数窗体。
        /// </summary>
        /// <param name="processName"></param>
        public FormSolRunParam(string processName) : this()
        {
            selectedProcessName = processName ?? string.Empty;
            showSingleProcessOnly = true;
        }

        /// <summary>
        /// 初始化只显示指定流程且指定AI节点的参数窗体。
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="targetAiNode"></param>
        public FormSolRunParam(string processName, NodeTDAI targetAiNode) : this(processName)
        {
            this.targetAiNode = targetAiNode;
        }

        /// <summary>
        /// 初始化只显示指定流程、指定AI节点，并按ROI绘制来源过滤运行参数的窗体。
        /// </summary>
        /// <param name="processName">流程名称。</param>
        /// <param name="targetAiNode">目标AI检测节点。</param>
        /// <param name="runParamDisplayFilter">运行参数显示过滤范围。</param>
        public FormSolRunParam(string processName, NodeTDAI targetAiNode, RunParamDisplayFilter runParamDisplayFilter) : this(processName, targetAiNode)
        {
            this.runParamDisplayFilter = runParamDisplayFilter;
        }

        /// <summary>
        /// 初始化只显示指定流程对象的参数窗体，支持组合模块内部流程。
        /// </summary>
        /// <param name="process">指定流程对象。</param>
        /// <param name="targetAiNode">目标AI检测节点。</param>
        /// <param name="runParamDisplayFilter">运行参数显示范围。</param>
        public FormSolRunParam(Process process, NodeTDAI targetAiNode, RunParamDisplayFilter runParamDisplayFilter) : this(process == null ? string.Empty : process.ProcessName, targetAiNode, runParamDisplayFilter)
        {
            selectedProcess = process;
        }
        /// <summary>
        /// 窗体显示事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormSolRunParam_Shown(object sender, System.EventArgs e)
        {
            ApplyLanguage();
            Stopwatch stopwatch = Stopwatch.StartNew();
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【手动调参诊断】运行参数窗体Shown开始，单流程={showSingleProcessOnly}，目标流程={selectedProcessName}。",
                true);

            #region 创建选项卡

            RemoveAllExceptSpecifiedPage(tabControl1, tabPage1);//保留“Modbus通信”选项卡,删除所有
            if (showSingleProcessOnly)
            {
                RemoveAllTabPages(tabControl1);
            }
            foreach (var process in GetDisplayProcesses())
            {
                if (selectedProcess == null && showSingleProcessOnly && process.ProcessName != selectedProcessName)
                    continue;

                SolRunParamControl solRunParam = new SolRunParamControl();
                TabPage page = new TabPage();
                page.Text = targetAiNode == null ? process.ProcessName : targetAiNode.NodeName;
                Stopwatch processStopwatch = Stopwatch.StartNew();
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】开始初始化流程运行参数，流程={process.ProcessName}，节点数={(process.Nodes == null ? 0 : process.Nodes.Count)}，过滤范围={(runParamDisplayFilter == null ? "空" : runParamDisplayFilter.ToLogText())}。",
                    true);
                try
                {
                    RunParamDisplayFilter processFilter = selectedProcess != null || showSingleProcessOnly && process.ProcessName == selectedProcessName
                        ? runParamDisplayFilter
                        : null;
                    // 在挂入可见窗口前一次性填充数据，减少每行绑定触发布局和绘制。
                    solRunParam.Init(process, showSingleProcessOnly ? targetAiNode : null, processFilter);
                    page.Controls.Add(solRunParam);
                    tabControl1.Controls.Add(page);
                    LogHelper.AddLog(
                        MsgLevel.Info,
                        $"【手动调参诊断】流程运行参数初始化完成，流程={process.ProcessName}，耗时={processStopwatch.ElapsedMilliseconds}ms。",
                        true);
                }
                catch (Exception ex)
                {
                    solRunParam.Dispose();
                    page.Dispose();
                    LogHelper.AddLog(
                        MsgLevel.Exception,
                        $"【手动调参诊断】流程运行参数初始化异常，流程={process.ProcessName}，耗时={processStopwatch.ElapsedMilliseconds}ms，原因={ex.Message}",
                        true);
                    LogHelper.AddLog(MsgLevel.Fatal, LanguageManager.Format("SolRunParam.LoadProcessParamFailed", process.ProcessName, ex.Message), true);
                }
            }

            #endregion

            SelectProcessPage();

            #region 显示Modbus通信参数

            if (showSingleProcessOnly)
            {
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"【手动调参诊断】运行参数窗体已就绪，画面={runParamDisplayFilter?.WindowName}，目标AI={targetAiNode?.NodeName}，构造到就绪={_openStopwatch.ElapsedMilliseconds}ms，Shown初始化={stopwatch.ElapsedMilliseconds}ms。",
                    true);
                return;
            }

            if(Solution.Instance.ModbusDevices.Count > 0)
            {
                foreach (var modbus in Solution.Instance.ModbusDevices)
                {
                    switch (modbus.DevType)
                    {
                        case DevType.ModbusRTUPoll:
                            curDevice = modbus;
                            ShowModbusRTUParams();
                            break;
                        case DevType.ModbusTcpPoll:
                            curDevice = modbus;
                            ShowModbusTCPParams();
                            break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                // 当没有modbus通信设备时，隐藏Modbus通信参数面板
                tableLayoutPanel2.Visible = false; 
            }

            #endregion
            LogHelper.AddLog(
                MsgLevel.Info,
                $"【手动调参诊断】运行参数窗体Shown完成，耗时={stopwatch.ElapsedMilliseconds}ms。",
                true);
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "SolRunParam.Title");
            LanguageManager.Bind(tabPage1, "SolRunParam.ModbusCommunication");
            LanguageManager.Bind(groupBox1, "SolRunParam.CommunicationConnection");
            LanguageManager.Bind(groupBox2, "SolRunParam.TestConnection");
            LanguageManager.Bind(label1, "SolRunParam.IPAddress");
            LanguageManager.Bind(label2, "SolRunParam.Port");
            LanguageManager.Bind(label3, "SolRunParam.CoilAddress");
            LanguageManager.Bind(label4, "SolRunParam.SerialPort");
            LanguageManager.Bind(label5, "SolRunParam.BaudRate");
            LanguageManager.Bind(label6, "SolRunParam.WriteCoilValue");
            LanguageManager.Bind(label7, "SolRunParam.HoldTime");
            LanguageManager.Bind(label8, "SolRunParam.DataBits");
            LanguageManager.Bind(label9, "SolRunParam.Parity");
            LanguageManager.Bind(label10, "SolRunParam.StopBits");
            LanguageManager.Bind(buttonCon1, "Common.Connect");
            LanguageManager.Bind(buttonCon2, "Common.Connect");
            LanguageManager.Bind(buttonRead, "SolRunParam.TestRead");
            LanguageManager.Bind(buttonWrite, "SolRunParam.TestWrite");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
            ApplyParityItems();
            if (targetAiNode != null)
                Text = $"手动调参 - {FrmWindowName()} - {targetAiNode.NodeName}";
            else if (showSingleProcessOnly)
                Text = $"手动调参 - {FrmWindowName()}";
        }

        /// <summary>标题标明实际画面，避免组合模块内部流程名干扰操作员识别。</summary>
        private string FrmWindowName()
        {
            return string.IsNullOrEmpty(runParamDisplayFilter?.WindowName)
                ? selectedProcessName
                : TDJS_Vision.Forms.ImageViewer.FrmSingleImage.GetWindowDisplayName(runParamDisplayFilter.WindowName);
        }

        private void ApplyParityItems()
        {
            int selectedIndex = comboBoxParity.SelectedIndex;
            comboBoxParity.Items.Clear();
            comboBoxParity.Items.Add(LanguageManager.T("SolRunParam.ParityNone"));
            comboBoxParity.Items.Add(LanguageManager.T("SolRunParam.ParityOdd"));
            comboBoxParity.Items.Add(LanguageManager.T("SolRunParam.ParityEven"));
            if (selectedIndex >= 0 && selectedIndex < comboBoxParity.Items.Count)
                comboBoxParity.SelectedIndex = selectedIndex;
        }
        private void SelectProcessPage()
        {
            if (string.IsNullOrEmpty(selectedProcessName))
                return;

            foreach (TabPage page in tabControl1.TabPages)
            {
                if (page.Text == selectedProcessName)
                {
                    tabControl1.SelectedTab = page;
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前窗体需要展示的流程集合，支持直接打开组合模块内部流程。
        /// </summary>
        /// <returns>待展示的流程集合。</returns>
        private IEnumerable<Process> GetDisplayProcesses()
        {
            if (selectedProcess != null)
                return new[] { selectedProcess };

            return Solution.Instance.AllProcesses ?? Enumerable.Empty<Process>();
        }

        private void RemoveAllTabPages(TabControl tabControl)
        {
            for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
            {
                tabControl.TabPages.RemoveAt(i);
            }
        }
        /// <summary>
        /// 显示modbusRTU参数
        /// </summary>
        private void ShowModbusRTUParams()
        {
            // 隐藏TCP的参数控件
            tableLayoutPanelTCP.Visible = false;
            tableLayoutPanelRTU.Visible = true;
            tableLayoutPanelRTU.Dock = DockStyle.Fill;
            tableLayoutPanel2.Visible = true;

            // 串口号获取
            comboBoxCom.Items.Clear();
            foreach (var com in SerialPort.GetPortNames())
            {
                comboBoxCom.Items.Add(com);
            }
            var dev = curDevice as ModbusRTUPoll;

            int index1 = comboBoxCom.Items.IndexOf(((ModbusRTUParam)dev.ModbusParam).PortName);
            comboBoxCom.SelectedIndex = index1 == -1 ? -1 : index1;

            // 波特率
            int index2 = comboBoxBaute.Items.IndexOf(((ModbusRTUParam)dev.ModbusParam).BaudRate.ToString());
            comboBoxBaute.SelectedIndex = index2 == -1 ? -1 : index2;

            // 数据位
            int index3 = comboBoxDataBit.Items.IndexOf(((ModbusRTUParam)dev.ModbusParam).DataBits.ToString());
            comboBoxDataBit.SelectedIndex = index3 == -1 ? -1 : index3;

            // 停止位
            int index4 = comboBoxStopBit.Items.IndexOf(((ModbusRTUParam)dev.ModbusParam).StopBits == StopBits.One ? "1"
                : ((ModbusRTUParam)dev.ModbusParam).StopBits == StopBits.OnePointFive? "1.5" : "2");
            comboBoxStopBit.SelectedIndex = index4 == -1 ? -1 : index4;

            // 校验位
            comboBoxParity.SelectedIndex = ((ModbusRTUParam)dev.ModbusParam).Parity == Parity.Odd ? 1
               : ((ModbusRTUParam)dev.ModbusParam).Parity == Parity.Even ? 2 : 0;
        }
        /// <summary>
        /// 显示Modbus TCP参数
        /// </summary>
        private void ShowModbusTCPParams()
        {
            // 隐藏TCP的参数控件
            tableLayoutPanelTCP.Visible = true;
            tableLayoutPanelTCP.Dock = DockStyle.Fill;
            tableLayoutPanelRTU.Visible = false;
            tableLayoutPanel2.Visible = true;

            //var dev = curDevice as ModbusTcpPoll;
            var dev = curDevice as ModbusTcpPoll;
            var param = dev.ModbusParam as ModbusTcpParam;
            uiipTextBoxIP.Text = param.IP;
            textBoxPort.Text = param.Port.ToString();
            textBoxHoldTime.Text = param.HoldTime.ToString();
        }
        /// <summary>
        /// 除了保留指定的选项卡外，删除所有选项卡
        /// </summary>
        /// <param name="tabControl"></param>
        /// <param name="pageNameToKeep"></param>
        private void RemoveAllExceptSpecifiedPage(TabControl tabControl, TabPage pageToKeep)
        {
            // 由于在遍历集合时不能直接修改该集合（如添加或移除项），我们需要使用逆序遍历或者创建一个新的列表来存储需要移除的项。
            // 这里我们选择直接在循环中操作，但使用了倒序遍历以避免索引问题。
            for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
            {
                if (tabControl.TabPages[i] != pageToKeep)
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
                MessageBoxTD.Show(LanguageManager.T("SolRunParam.DeviceNotAdded"));
                return;
            }
            if (string.IsNullOrEmpty(uiipTextBoxIP.Text) || string.IsNullOrEmpty(textBoxPort.Text))
            {
                MessageBoxTD.Show(LanguageManager.T("SolRunParam.ParamRequired"));
                return;
            }
            try
            {
                // 连接Modbus
                switch (curDevice.DevType)
                {
                    case DevType.ModbusRTUPoll:
                        var rtuPoll = curDevice as ModbusRTUPoll;
                        var paramRtuPoll = rtuPoll.ModbusParam as ModbusRTUParam;
                        rtuPoll.Disconnect();
                        paramRtuPoll.PortName = comboBoxCom.Text;
                        paramRtuPoll.BaudRate = int.Parse(comboBoxBaute.Text);
                        paramRtuPoll.DataBits = int.Parse(comboBoxDataBit.Text);
                        paramRtuPoll.StopBits = (StopBits)Enum.Parse(typeof(StopBits), comboBoxStopBit.Text);
                        paramRtuPoll.Parity = comboBoxParity.SelectedIndex == 1 ? Parity.Odd : comboBoxParity.SelectedIndex == 2 ? Parity.Even : Parity.None;

                        rtuPoll.Connect();
                        curDevice = rtuPoll;
                        labelConnectInfo1.Text = LanguageManager.T("SolRunParam.DeviceConnectSuccess");
                        labelConnectInfo1.ForeColor = System.Drawing.Color.Green;
                        break;
                    case DevType.ModbusTcpPoll:
                        //var tcpPoll = curDevice as ModbusTcpPoll;
                        var tcpPoll = curDevice as ModbusTcpPoll;
                        var paramTcpPoll = tcpPoll.ModbusParam as ModbusTcpParam;
                        tcpPoll.Disconnect();
                        paramTcpPoll.IP = uiipTextBoxIP.Text;
                        paramTcpPoll.Port = int.Parse(textBoxPort.Text);
                        paramTcpPoll.HoldTime = ushort.Parse(textBoxHoldTime.Text);

                        tcpPoll.Connect();
                        curDevice = tcpPoll;
                        labelConnectInfo2.Text = LanguageManager.T("SolRunParam.DeviceConnectSuccess");
                        labelConnectInfo2.ForeColor = System.Drawing.Color.Green;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                if(curDevice.DevType == DevType.ModbusRTUPoll)
                {
                    labelConnectInfo1.Text = LanguageManager.Format("SolRunParam.DeviceConnectFailed", ex.Message);
                    labelConnectInfo1.ForeColor = System.Drawing.Color.Red;
                }
                else if (curDevice.DevType == DevType.ModbusTcpPoll)
                {
                    labelConnectInfo2.Text = LanguageManager.Format("SolRunParam.DeviceConnectFailed", ex.Message);
                    labelConnectInfo2.ForeColor = System.Drawing.Color.Red;
                    return;
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
                MessageBoxTD.Show(LanguageManager.T("SolRunParam.DeviceNotAdded"));
                return;
            }
            try
            {

                switch (curDevice.DevType)
                {
                    case DevType.ModbusRTUPoll:
                        var rtuPoll = curDevice as ModbusRTUPoll;
                        if (rtuPoll == null || !rtuPoll.IsConnect) throw new Exception(LanguageManager.T("SolRunParam.ModbusInvalidOrClosed"));
                        var valueRtu = rtuPoll.ReadBool(textBoxAddress.Text, 1);
                        labelTestInfo.Text = LanguageManager.Format("SolRunParam.ReadCoilSuccess", valueRtu[0]);
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                        break;
                    case DevType.ModbusTcpPoll:
                        var tcpPoll = curDevice as ModbusTcpPoll;
                        if (tcpPoll == null || !tcpPoll.IsConnect) throw new Exception(LanguageManager.T("SolRunParam.ModbusInvalidOrClosed"));
                        var valueTcp = tcpPoll.ReadBool(textBoxAddress.Text, 1);
                        labelTestInfo.Text = LanguageManager.Format("SolRunParam.ReadCoilSuccess", valueTcp[0]);
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                labelTestInfo.Text = LanguageManager.Format("SolRunParam.ReadCoilFailed", ex.Message);
                labelTestInfo.ForeColor = System.Drawing.Color.Red;
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
                MessageBoxTD.Show(LanguageManager.T("SolRunParam.DeviceNotAdded"));
                return;
            }
            try
            {
                switch (curDevice.DevType)
                {
                    case DevType.ModbusRTUPoll:
                        var rtuPoll = curDevice as ModbusRTUPoll;
                        if (rtuPoll == null || !rtuPoll.IsConnect) throw new Exception(LanguageManager.T("SolRunParam.ModbusInvalidOrClosed"));
                        rtuPoll.Write(textBoxAddress.Text, comboBoxCoilValue.Text == "1");
                        labelTestInfo.Text = LanguageManager.T("SolRunParam.WriteCoilSuccess");
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                        break;
                    case DevType.ModbusTcpPoll:
                        //var tcpPoll = curDevice as ModbusTcpPoll;
                        var tcpPoll = curDevice as ModbusTcpPoll;
                        if (tcpPoll == null || !tcpPoll.IsConnect) throw new Exception(LanguageManager.T("SolRunParam.ModbusInvalidOrClosed"));
                        tcpPoll.Write(textBoxAddress.Text, comboBoxCoilValue.Text == "1");
                        labelTestInfo.Text = LanguageManager.T("SolRunParam.WriteCoilSuccess");
                        labelTestInfo.ForeColor = System.Drawing.Color.Green;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                labelTestInfo.Text = LanguageManager.Format("SolRunParam.WriteCoilFailed", ex.Message);
                labelTestInfo.ForeColor = System.Drawing.Color.Red;
            }
        }
    }
}
