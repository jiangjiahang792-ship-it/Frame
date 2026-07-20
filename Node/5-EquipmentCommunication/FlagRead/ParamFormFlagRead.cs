using Logger;
using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._5_EquipmentCommunication.PlcRead
{
    public partial class ParamFormFlagRead : FormBase, INodeParamForm
    {
        public INodeParam Params { get; set; }

        public ParamFormFlagRead()
        {
            InitializeComponent();
            InitSignalComboBox();
        }

        public void SetNodeBelong(NodeBase node) { }

        /// <summary>
        /// 初始化监听信号下拉框，来源为全局信号设置中的 dataGridView3。
        /// </summary>
        private void InitSignalComboBox()
        {
            string text = comboBoxPlcList.Text;

            comboBoxPlcList.Items.Clear();
            comboBoxPlcList.Items.Add("[未设置]");

            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals != null)
            {
                foreach (var signal in signals)
                {
                    if (signal == null || !signal.Enable)
                        continue;

                    comboBoxPlcList.Items.Add(GetSignalDisplayName(signal));
                }
            }

            int index = comboBoxPlcList.Items.IndexOf(text);
            comboBoxPlcList.SelectedIndex = index == -1 ? 0 : index;
            checkBox1.Checked = false;
            checkBoxWait.Checked = true;
        }

        public static string BuildSignalKey(SingleGlobalSignalSettings signal)
        {
            if (signal == null)
                return string.Empty;

            return $"{signal.DeviceName}|{signal.Address}|{signal.Type}|{signal.Value}";
        }

        private static string GetSignalDisplayName(SingleGlobalSignalSettings signal)
        {
            return $"{signal.DeviceName} - {signal.Address} - {signal.Type}={signal.Value}";
        }

        private SingleGlobalSignalSettings GetSelectedSignal()
        {
            var signals = Solution.Instance.GlobalSignal?.ListenSignals;
            if (signals == null)
                return null;

            foreach (var signal in signals)
            {
                if (GetSignalDisplayName(signal) == comboBoxPlcList.Text)
                    return signal;
            }

            return null;
        }

        /// <summary>
        /// 点击保存当前参数配置。
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(comboBoxPlcList.Text) || comboBoxPlcList.Text == "[未设置]")
                {
                    LogHelper.AddLog(MsgLevel.Exception, "监听信号不能为空！", true);
                    MessageBoxTD.Show("监听信号不能为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var signal = GetSelectedSignal();
                if (signal == null)
                {
                    LogHelper.AddLog(MsgLevel.Exception, "未找到选择的监听信号！", true);
                    MessageBoxTD.Show("未找到选择的监听信号！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                NodeParamFlagRead nodeParamRead = new NodeParamFlagRead
                {
                    Signal = signal,
                    SignalKey = BuildSignalKey(signal),
                    SignalName = GetSignalDisplayName(signal),
                    Reset = checkBox1.Checked,
                    WaitForSignal = checkBoxWait.Checked
                };

                Params = nodeParamRead;
                Hide();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"保存失败，请检查参数是否有误！原因：{ex.Message}", true);
                MessageBoxTD.Show($"保存失败，请检查参数是否有误！原因：{ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 窗口每次显示时都刷新监听信号下拉框。
        /// </summary>
        private void ParamFormRead_Shown(object sender, EventArgs e)
        {
            InitSignalComboBox();
            SetParam2Form();
        }

        /// <summary>
        /// 反序列化需要设置参数给回界面。
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamFlagRead param)
            {
                int index = comboBoxPlcList.Items.IndexOf(param.SignalName);
                comboBoxPlcList.SelectedIndex = index == -1 ? 0 : index;
                checkBox1.Checked = param.Reset;
                checkBoxWait.Checked = param.WaitForSignal;

                var signals = Solution.Instance.GlobalSignal?.ListenSignals;
                if (signals == null)
                    return;

                foreach (var signal in signals)
                {
                    if (BuildSignalKey(signal) == param.SignalKey)
                    {
                        param.Signal = signal;
                        break;
                    }
                }
            }
        }
    }
}
