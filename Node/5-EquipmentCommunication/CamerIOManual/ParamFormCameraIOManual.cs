using Logger;
using System;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._5_EquipmentCommunication.CamerIOImprovement
{
    public partial class ParamFormCameraIOManual : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前相机
        /// </summary>
        private Device.Camera.ICamera m_camera;

        public ParamFormCameraIOManual()
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


        /// <summary>
        /// 反序列化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamCameraIOManual param)
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
                comboBox1.SelectedIndex = param.Semaphore ? 0 : 1;
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
            if (comboBoxLineMode.Text == "输出")
            {
                checkLineDirection("输出");
            }
            else
            {
                checkLineDirection("输入");
            }
        }

        /// <summary>
        /// 检查线路方向
        /// </summary>
        /// <param name="lineMode"></param>
        private void checkLineDirection(string lineMode)
        {
            try
            {
                if (lineMode == "输出")
                {
                    string currentLine = comboBoxLines.Text;
                    comboBoxLines.Items.Clear();
                    // 获取当前相机所有线路
                    foreach (var line in m_camera.GetLineSelector().SupportEnumEntries)
                    {
                        // 设置当前线路
                        m_camera.SetLineSelector(line.Symbolic);
                        // 获取当前线路方向
                        foreach (var mode in m_camera.GetLineMode().SupportEnumEntries)
                        {
                            if (mode.Symbolic == "Strobe")
                            {
                                comboBoxLines.Items.Add(line.Symbolic);
                            }
                            if (mode.Symbolic == "Output")
                            {
                                comboBoxLines.Items.Add(line.Symbolic);
                            }
                        }
                    }
                    int index = comboBoxLines.Items.IndexOf(currentLine);
                    comboBoxLines.SelectedIndex = index == -1 ? 0 : index;
                }
                else
                {
                    string currentLine = comboBoxLines.Text;
                    comboBoxLines.Items.Clear();
                    // 获取当前相机所有线路
                    foreach (var line in m_camera.GetLineSelector().SupportEnumEntries)
                    {
                        // 设置当前线路
                        m_camera.SetLineSelector(line.Symbolic);
                        // 获取当前线路方向
                        foreach (var mode in m_camera.GetLineMode().SupportEnumEntries)
                        {
                            if (mode.Symbolic == "Input")
                            {
                                comboBoxLines.Items.Add(line.Symbolic);
                            }
                        }
                    }
                    int index = comboBoxLines.Items.IndexOf(currentLine);
                    comboBoxLines.SelectedIndex = index == -1 ? 0 : index;
                }
            }
            catch (Exception)
            {
            }
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

                NodeParamCameraIOManual nodeParam = new NodeParamCameraIOManual();
                nodeParam.Camera = m_camera;
                nodeParam.CameraName = comboBoxCameraList.Text;
                nodeParam.LineMode = comboBoxLineMode.Text;
                nodeParam.LineSelector = comboBoxLines.Text;
                nodeParam.Semaphore = comboBox1.SelectedIndex == 0 ? true : false;
                Params = nodeParam;
                Hide();
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置不合法！");
                return;
            }
        }

        public void SetNodeBelong(NodeBase node)
        {
            
        }
    }
}
