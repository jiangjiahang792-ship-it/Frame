using Logger;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.CameraIO
{
    public partial class ParamFormCameraIO : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前相机
        /// </summary>
        private Device.Camera.ICamera m_camera;

        public ParamFormCameraIO()
        {
            InitializeComponent();
           
        }
        private void ParamFormCameraIO_Shown(object sender, EventArgs e)
        {
            // 初始化相机列表
            InitCameraList();
        }

        /// <summary>
        /// 初始化相机列表
        /// </summary>
        /// <param name="process"></param>
        /// <param name="node"></param>
        private void InitCameraList()
        {
            string currentCamera = comboBoxCameraList.Text;
            comboBoxCameraList.Items.Clear();
            // 遍历Solution.Instance.CameraDevices中的每个相机
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                // 将相机的自定义名添加到comboBox_cameraList中
                comboBoxCameraList.Items.Add(camera.UserDefinedName);
            }
            if (comboBoxCameraList.Items.Count > 0)
            {
                int index = comboBoxCameraList.Items.IndexOf(currentCamera);
                comboBoxCameraList.SelectedIndex = index == -1 ? 0 : index;
            }
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node) 
        {
            nodeSubscription1.SetExpectedValueType<AlgorithmResult>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 获取订阅的结果
        /// </summary>
        /// <returns></returns>
        public bool GetCondition()
        {
            try
            {
                return nodeSubscription1.GetValue<AlgorithmResult>().IsAllOk;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamCameraIO param)
            {
                comboBoxCameraList.Items.Clear();
                foreach (var camera in Solution.Instance.CameraDevices)
                {
                    comboBoxCameraList.Items.Add(camera.UserDefinedName);
                }
                try
                {
                    int index = comboBoxCameraList.Items.IndexOf(param.CameraName);
                    comboBoxCameraList.SelectedIndex = index == -1 ? 0 : index;
                }
                catch {
                    LogHelper.AddLog(MsgLevel.Exception, $"当前没找到记录的相机,请检查是否掉线", true);
                }   
                
                param.Camera = m_camera;
                comboBoxLineMode.Text = param.LineMode;
                comboBoxLines.Text = param.LineSelector;
                textBoxHoldTime.Text = param.HoldTime.ToString();
                nodeSubscription1.SetText(param.NodeName, param.NodeResult);
                checkBox1.Checked = param.IsAsay;
            }
        }
        /// <summary>
        /// 相机选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxCameraList_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_camera = null;
            // 获取当前选中的相机
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                // 如果相机设备的用户定义名称与相机列表中的文本相同
                if (camera.UserDefinedName == comboBoxCameraList.Text)
                {
                    m_camera = camera;
                    break;
                }
            }

            // 相机改变后必须重新读取该相机的真实线路能力。界面默认已经是“输出”时，
            // 仅依赖线路模式的 SelectedIndexChanged 事件不会再次触发刷新。
            checkLineDirection(comboBoxLineMode.Text);
        }
        /// <summary>
        /// 线路选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxIO_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkLineDirection("输出");
        }

        /// <summary>
        /// 检查线路方向
        /// </summary>
        /// <param name="lineMode"></param>
        private void checkLineDirection(string lineMode)
        {
            try
            {
                bool executed = Solution.Instance.TryExecuteManualExternalSignal(
                    () => RefreshLineOptions(lineMode));
                if (!executed)
                {
                    LogHelper.AddLog(
                        MsgLevel.Warn,
                        "方案正在运行或重置，暂时不能刷新相机IO线路。",
                        true);
                }
            }
            catch (Exception exception)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"刷新相机IO线路失败：{exception.Message}",
                    true);
            }
        }

        /// <summary>在方案运行门禁保护下枚举当前相机各线路支持的方向。</summary>
        private void RefreshLineOptions(string lineMode)
        {
            if (m_camera == null)
                return;
            if (lineMode != "输出")
                return;

            string currentLine = comboBoxLines.Text;
            Device.Camera.CameraEnumValue lineSelectorOptions = m_camera.GetLineSelector();
            string originalLine = lineSelectorOptions.CurEnumEntry?.Symbolic;
            comboBoxLines.Items.Clear();
            try
            {
                // 某些相机的输入专用线路不提供可读的 LineMode 节点。单条线路读取失败时
                // 只跳过该线路，不能中断整个枚举并让输出线路下拉框保持为空。
                foreach (var line in GetLineCandidates(lineSelectorOptions))
                {
                    try
                    {
                        m_camera.SetLineSelector(line.Symbolic);
                        Device.Camera.CameraEnumValue lineModeOptions = m_camera.GetLineMode();
                        bool supportsOutput = IsOutputLineMode(lineModeOptions.CurEnumEntry);
                        foreach (var mode in lineModeOptions.SupportEnumEntries)
                        {
                            supportsOutput = supportsOutput || IsOutputLineMode(mode);
                        }
                        if (supportsOutput && !comboBoxLines.Items.Contains(line.Symbolic))
                        {
                            comboBoxLines.Items.Add(line.Symbolic);
                        }
                    }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            $"读取相机【{m_camera.UserDefinedName}】线路【{line.Symbolic}】输出能力失败，已继续检查其他线路：{exception.Message}",
                            true);
                    }
                }
            }
            finally
            {
                // 枚举线路只用于读取能力，完成后恢复相机原来的线路选择，避免参数页改变设备状态。
                if (!string.IsNullOrWhiteSpace(originalLine))
                {
                    try
                    {
                        m_camera.SetLineSelector(originalLine);
                    }
                    catch (Exception exception)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            $"恢复相机【{m_camera.UserDefinedName}】原线路【{originalLine}】失败：{exception.Message}",
                            true);
                    }
                }
            }

            int index = comboBoxLines.Items.IndexOf(currentLine);
            comboBoxLines.SelectedIndex = index == -1 && comboBoxLines.Items.Count > 0 ? 0 : index;
            if (comboBoxLines.Items.Count == 0)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"相机【{m_camera.UserDefinedName}】没有读取到支持输出的IO线路，请检查相机连接及线路能力。",
                    true);
            }
        }

        /// <summary>
        /// 构造需要探测的相机线路候选项。
        /// </summary>
        /// <param name="lineSelectorOptions">相机SDK返回的线路选择项。</param>
        /// <returns>去重后的线路候选项；SDK未完整报告支持项时补充标准Line0至Line4。</returns>
        private static List<Device.Camera.CameraEnumEntry> GetLineCandidates(
            Device.Camera.CameraEnumValue lineSelectorOptions)
        {
            var candidates = new List<Device.Camera.CameraEnumEntry>();
            if (lineSelectorOptions?.SupportEnumEntries != null)
            {
                foreach (var line in lineSelectorOptions.SupportEnumEntries)
                {
                    if (line == null || string.IsNullOrWhiteSpace(line.Symbolic))
                        continue;
                    if (!ContainsLine(candidates, line.Symbolic))
                        candidates.Add(line);
                }
            }

            // 部分相机SDK的nSupportedNum为0，但Line0至Line4仍可正常设置和使用。
            // 逐条探测只发生在参数页刷新时，不影响生产运行性能。
            for (uint lineIndex = 0; lineIndex <= 4; lineIndex++)
            {
                string symbolic = "Line" + lineIndex;
                if (!ContainsLine(candidates, symbolic))
                {
                    candidates.Add(new Device.Camera.CameraEnumEntry
                    {
                        Value = lineIndex,
                        Symbolic = symbolic
                    });
                }
            }

            return candidates;
        }

        /// <summary>
        /// 判断候选线路集合是否已经包含指定线路。
        /// </summary>
        /// <param name="candidates">线路候选集合。</param>
        /// <param name="symbolic">线路符号名。</param>
        /// <returns>已经包含时返回true。</returns>
        private static bool ContainsLine(
            List<Device.Camera.CameraEnumEntry> candidates,
            string symbolic)
        {
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.Symbolic, symbolic, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断SDK线路模式是否表示可主动输出。
        /// </summary>
        /// <param name="mode">相机SDK返回的线路模式。</param>
        /// <returns>Output、Strobe或常见输出枚举值时返回true。</returns>
        private static bool IsOutputLineMode(Device.Camera.CameraEnumEntry mode)
        {
            if (mode == null)
                return false;

            string symbolic = mode.Symbolic ?? string.Empty;
            if (string.Equals(symbolic, "Output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(symbolic, "Strobe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(symbolic) && (mode.Value == 1 || mode.Value == 8);
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
                if (comboBoxCameraList.SelectedIndex == -1 || comboBoxLineMode.SelectedIndex == -1 || comboBoxLines.SelectedIndex == -1)
                    throw new Exception("参数未设置完整！");
                var holdTime = int.Parse(textBoxHoldTime.Text);

                NodeParamCameraIO nodeParam = new NodeParamCameraIO();
                nodeParam.Camera = m_camera;
                nodeParam.CameraName = comboBoxCameraList.Text;
                nodeParam.LineMode = comboBoxLineMode.Text;
                nodeParam.LineSelector = comboBoxLines.Text;
                nodeParam.HoldTime = holdTime;
                nodeParam.NodeName = nodeSubscription1.GetText1();
                nodeParam.NodeResult = nodeSubscription1.GetText2();
                nodeParam.IsAsay = checkBox1.Checked;
                Params = nodeParam;
                Hide();
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置不合法！");
                return;
            }
        }
    }
}
