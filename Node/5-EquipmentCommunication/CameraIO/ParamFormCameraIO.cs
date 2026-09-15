using Logger;
using System;
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
            // 获取当前选中的相机
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                // 如果相机设备的用户定义名称与相机列表中的文本相同
                if (camera.UserDefinedName == comboBoxCameraList.Text)
                {
                    m_camera = camera;
                }
            }
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
                Solution.Instance.TryExecuteManualExternalSignal(
                    () => RefreshLineOptions(lineMode));
            }
            catch (Exception)
            {
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
            comboBoxLines.Items.Clear();
            // 获取当前相机所有支持主动输出的线路。
            foreach (var line in m_camera.GetLineSelector().SupportEnumEntries)
            {
                m_camera.SetLineSelector(line.Symbolic);
                foreach (var mode in m_camera.GetLineMode().SupportEnumEntries)
                {
                    bool supportsOutput = mode.Symbolic == "Output";
                    if (supportsOutput && !comboBoxLines.Items.Contains(line.Symbolic))
                    {
                        comboBoxLines.Items.Add(line.Symbolic);
                    }
                }
            }
            int index = comboBoxLines.Items.IndexOf(currentLine);
            comboBoxLines.SelectedIndex = index == -1 && comboBoxLines.Items.Count > 0 ? 0 : index;
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
