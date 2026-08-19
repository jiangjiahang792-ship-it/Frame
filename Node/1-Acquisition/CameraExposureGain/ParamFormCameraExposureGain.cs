using Logger;
using Sunny.UI;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.CameraExposureGain
{
    /// <summary>
    /// 相机曝光增益设置参数窗口。
    /// </summary>
    public partial class ParamFormCameraExposureGain : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前参数窗体所属节点名称，用于日志提示。
        /// </summary>
        private readonly string _nodeName;

        /// <summary>
        /// 当前参数窗体所属流程，用于日志提示。
        /// </summary>
        private readonly Process _process;

        /// <summary>
        /// 当前下拉框选中的相机对象。
        /// </summary>
        private ICamera _camera;

        /// <summary>
        /// 节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 初始化相机曝光增益设置参数窗口。
        /// </summary>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">节点所属流程。</param>
        public ParamFormCameraExposureGain(string nodeName, Process process)
        {
            InitializeComponent();
            _nodeName = nodeName;
            _process = process;
            InitCameraList();
        }

        /// <summary>
        /// 设置参数窗体所属节点；当前节点无需订阅上游结果。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node) { }

        /// <summary>
        /// 窗口显示时刷新相机列表，确保新增或恢复的相机能立即选择。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void ParamFormCameraExposureGain_Shown(object sender, EventArgs e)
        {
            InitCameraList();
        }

        /// <summary>
        /// 初始化相机下拉框并保留当前选择。
        /// </summary>
        private void InitCameraList()
        {
            string currentCamera = comboBoxCamera.Text;
            comboBoxCamera.Items.Clear();
            comboBoxCamera.Items.Add("[未设置]");

            foreach (ICamera camera in Solution.Instance.CameraDevices)
            {
                comboBoxCamera.Items.Add(camera.UserDefinedName);
            }

            int index = comboBoxCamera.Items.IndexOf(currentCamera);
            comboBoxCamera.SelectedIndex = index == -1 ? 0 : index;
        }

        /// <summary>
        /// 相机选择变化后刷新曝光、增益的可设置范围和当前值提示。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void comboBoxCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            _camera = null;

            foreach (ICamera camera in Solution.Instance.CameraDevices)
            {
                if (camera.UserDefinedName == comboBoxCamera.Text)
                {
                    _camera = camera;
                    RefreshCameraParameterRange(camera);
                    return;
                }
            }

            labelExposure.Text = "曝光(us)";
            labelGain.Text = "增益";
        }

        /// <summary>
        /// 按相机 SDK 返回的范围刷新数值框，避免保存越界参数。
        /// </summary>
        /// <param name="camera">当前选中的相机。</param>
        private void RefreshCameraParameterRange(ICamera camera)
        {
            try
            {
                var exposure = camera.GetExposureTime();
                numericUpDownExposure.Minimum = (decimal)exposure.Min;
                numericUpDownExposure.Maximum = (decimal)exposure.Max;
                SetNumericUpDownValueInRange(numericUpDownExposure, (decimal)exposure.CurValue);
                labelExposure.Text = $"曝光(当前{exposure.CurValue})";

                var (gainInt, gainFloat) = camera.GetGain();
                if (gainInt != null)
                {
                    numericUpDownGain.DecimalPlaces = 0;
                    numericUpDownGain.Increment = 1;
                    numericUpDownGain.Minimum = gainInt.Min;
                    numericUpDownGain.Maximum = gainInt.Max;
                    SetNumericUpDownValueInRange(numericUpDownGain, gainInt.CurValue);
                    labelGain.Text = $"增益(当前{gainInt.CurValue})";
                }
                else
                {
                    numericUpDownGain.DecimalPlaces = 2;
                    numericUpDownGain.Increment = 0.1M;
                    numericUpDownGain.Minimum = (decimal)gainFloat.Min;
                    numericUpDownGain.Maximum = (decimal)gainFloat.Max;
                    SetNumericUpDownValueInRange(numericUpDownGain, (decimal)gainFloat.CurValue);
                    labelGain.Text = $"增益(当前{gainFloat.CurValue})";
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"读取相机({camera.UserDefinedName})曝光增益范围失败：{ex.Message}", true);
                labelExposure.Text = "曝光(us)";
                labelGain.Text = "增益";
            }
        }

        /// <summary>
        /// 将数值设置到 NumericUpDown 支持范围内，避免旧参数或相机范围变化导致界面异常。
        /// </summary>
        /// <param name="numericUpDown">待设置的数值控件。</param>
        /// <param name="value">待设置的数值。</param>
        private static void SetNumericUpDownValueInRange(NumericUpDown numericUpDown, decimal value)
        {
            if (value < numericUpDown.Minimum)
            {
                numericUpDown.Value = numericUpDown.Minimum;
                return;
            }

            if (value > numericUpDown.Maximum)
            {
                numericUpDown.Value = numericUpDown.Maximum;
                return;
            }

            numericUpDown.Value = value;
        }

        /// <summary>
        /// 保存界面参数到节点参数对象。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (comboBoxCamera.Text.IsNullOrEmpty() || comboBoxCamera.Text == "[未设置]" || _camera == null)
            {
                string message = $"节点[{_process.ProcessName}.{_nodeName}]相机未设置！";
                LogHelper.AddLog(MsgLevel.Warn, message, true);
                MessageBoxTD.Show(message, "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Params = new NodeParamCameraExposureGain
            {
                Camera = _camera,
                CameraName = comboBoxCamera.Text,
                ExposureTime = (double)numericUpDownExposure.Value,
                Gain = (double)numericUpDownGain.Value
            };

            Hide();
        }

        /// <summary>
        /// 反序列化后把节点参数还原到界面，并重新绑定当前方案中的相机对象。
        /// </summary>
        public void SetParam2Form()
        {
            if (!(Params is NodeParamCameraExposureGain param))
            {
                return;
            }

            InitCameraList();
            int index = comboBoxCamera.Items.IndexOf(param.CameraName);
            comboBoxCamera.SelectedIndex = index == -1 ? 0 : index;
            SetNumericUpDownValueInRange(numericUpDownExposure, (decimal)param.ExposureTime);
            SetNumericUpDownValueInRange(numericUpDownGain, (decimal)param.Gain);

            foreach (ICamera camera in Solution.Instance.CameraDevices)
            {
                if (camera.UserDefinedName == param.CameraName)
                {
                    param.Camera = camera;
                    _camera = camera;
                    break;
                }
            }
        }
    }
}
