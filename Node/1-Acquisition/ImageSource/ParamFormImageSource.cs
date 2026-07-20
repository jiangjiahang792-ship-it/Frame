using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public partial class ParamFormImageSource : FormBase, INodeParamForm
    {
        /// <summary>
        /// 窗口高度
        /// </summary>
        private int _formHeight;

        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        private NodeBase _node;
        /// <summary>
        /// 当前已绑定回调的相机，切换相机或删除节点时用于解绑事件。
        /// </summary>
        private ICamera _callbackCamera;
        /// <summary>
        /// 当前参数窗体所属的图像源节点，相机回调直接注册到该节点方法。
        /// </summary>
        private readonly NodeImageSource _imageSourceNode;
        public ParamFormImageSource(NodeBase node)
        {
            InitializeComponent();
            // 获取最初窗口高度
            _formHeight = this.Height;
            _node = node;
            _imageSourceNode = node as NodeImageSource;
            comboBoxImgSource.SelectedIndex = 0;
            Shown += ParamFormImageSource_Shown;
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
            NodeBase.NodeDeletedEvent += NodeBase_NodeDeletedEvent;
            Disposed += ParamFormImageSource_Disposed;
        }

        /// <summary>
        /// 窗体显示时刷新共享变量列表，保证新建或反序列化后的变量能被选择。
        /// </summary>
        private void ParamFormImageSource_Shown(object sender, EventArgs e)
        {
            if (comboBoxImgSource.Text == "共享变量")
                RefreshSharedVariableList(comboBoxSharedVariable.Text);
        }

        private void SolRunParamControl_RefreshParamView(object sender, EventArgs e)
        {
            if(Params is NodeParamImageSoucre paramSrc)
            {
                numericUpDownExposureTime.Value = (decimal)paramSrc.ExposureTime;
                numericUpDownGain.Value = (decimal)paramSrc.Gain;
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamImageSoucre param)
            {
                comboBoxImgSource.Text = param.ImageSource;

                if (param.ImageSource == "本地图像")
                {
                    textBoxImgPath.Text = param.PathText;
                    checkBoxAuto.Checked = param.IsAutoLoop;
                    if (param.ImagePaths.Count != 0)
                    {
                        checkBoxAuto.Enabled = true;
                    }
                    // “选择目录”需要获取目录下所有图像文件
                    if (Directory.Exists(param.PathText))
                    {
                        var extensions = new[] { ".bmp", ".jpg", ".jpeg", ".png" };
                        var files = Directory.GetFiles(textBoxImgPath.Text)
                                             .Where(s => extensions.Any(e => s.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                                             .ToList();

                        param.ImagePaths = files;
                    }
                }
                else if (param.ImageSource == "相机")
                {
                    // 旧方案可能保存了连续取流模式；新版本统一迁移为回调触发模式。
                    if (param.TriggerModel == TriggerModel.Off)
                    {
                        param.TriggerModel = TriggerModel.On;
                        LogHelper.AddLog(
                            MsgLevel.Info,
                            $"流程【{_imageSourceNode?.Process?.ProcessName}】图像源节点({_imageSourceNode?.ID}.{_imageSourceNode?.NodeName})的旧触发模式 Off 已迁移为 On。",
                            true);
                    }

                    // 还原选中的相机 
                    comboBoxChoiceCamera.Items.Clear();
                    comboBoxChoiceCamera.Items.Add("[未设置]");
                    foreach (var camera in Solution.Instance.CameraDevices)
                    {
                        comboBoxChoiceCamera.Items.Add(camera.UserDefinedName);
                    }
                    int index = comboBoxChoiceCamera.Items.IndexOf(param.CameraName);
                    comboBoxChoiceCamera.SelectedIndex = index == -1 ? 0 : index;
                    // 还原选中的触发源
                    comboBoxTriggerMode.Text = GetTriggerSourceDisplayText(param.TriggerSource);
                    // 硬触发沿
                    comboBoxTriggerEdge.SelectedIndex = (int)param.TriggerEdge;
                    //// 设置延迟、曝光、增益
                    numericUpDownTriggerDelay.Text = param.TriggerDelay.ToString();
                    numericUpDownExposureTime.Text = param.ExposureTime.ToString();
                    numericUpDownGain.Text = param.Gain.ToString();
                    numericUpDownTimeOut.Value = param.TimeOut;
                    // 是否频闪
                    int index1 = comboBoxStrobe.Items.IndexOf(param.IsEveryTime ? "是" : "否");
                    comboBoxStrobe.SelectedIndex = index1 == -1 ? 0 : index1;
                    // 还原节点使用的相机
                    foreach (var camera in Solution.Instance.CameraDevices)
                    {
                        if (camera.UserDefinedName == param.CameraName)
                        {
                            param.Camera = camera;
                            // 不是频闪应用可以在参数界面只设置一次,是频闪的话相机需要在节点运行时每次设置
                            if (!param.IsEveryTime)
                                SetCameraParams(param.Camera, TriggerModel.On, param.TriggerSource, param.TriggerEdge, param.TriggerDelay
                                    , param.ExposureTime, param.Gain, param.TimeOut);
                        }
                    }

                    // 相机图像源统一使用回调取图，不再依赖流程菜单开关。
                    SyncCameraCallbackBinding();
                }
                else if (param.ImageSource == "共享变量")
                {
                    RefreshSharedVariableList(param.SharedVariableName);
                }

            }
        }

        /// <summary>
        /// 根据当前有效相机参数同步图像源节点的回调订阅。
        /// </summary>
        public void SyncCameraCallbackBinding()
        {
            ICamera camera = GetCallbackCameraFromParams();
            if (camera == null)
            {
                UnbindCameraCallback();
                return;
            }

            BindCameraCallback(camera);
        }

        /// <summary>
        /// 从当前节点参数中获取用于回调触发的相机。
        /// </summary>
        private ICamera GetCallbackCameraFromParams()
        {
            if (Params is NodeParamImageSoucre param && param.ImageSource == "相机")
                return param.Camera;

            return null;
        }

        /// <summary>
        /// 绑定相机回调到当前图像源节点，避免每帧回调时再查找节点。
        /// </summary>
        private void BindCameraCallback(ICamera camera)
        {
            if (_imageSourceNode == null || camera == null)
                return;

            if (ReferenceEquals(_callbackCamera, camera))
            {
                if (camera != null)
                {
                    camera.OnMatReceived -= _imageSourceNode.HandleCameraCallbackFrame;
                    camera.OnMatReceived += _imageSourceNode.HandleCameraCallbackFrame;
                    camera.RegisterImageCallbackOwner(_imageSourceNode);
                }
                return;
            }

            UnbindCameraCallback();
            _callbackCamera = camera;
            if (_callbackCamera != null)
            {
                _callbackCamera.OnMatReceived -= _imageSourceNode.HandleCameraCallbackFrame;
                _callbackCamera.OnMatReceived += _imageSourceNode.HandleCameraCallbackFrame;
                _callbackCamera.RegisterImageCallbackOwner(_imageSourceNode);
            }
        }

        /// <summary>
        /// 解除当前相机回调绑定。
        /// </summary>
        private void UnbindCameraCallback()
        {
            if (_callbackCamera != null && _imageSourceNode != null)
            {
                _callbackCamera.OnMatReceived -= _imageSourceNode.HandleCameraCallbackFrame;
                _callbackCamera.UnregisterImageCallbackOwner(_imageSourceNode);
            }
            _callbackCamera = null;
        }

        /// <summary>
        /// 节点删除时解绑相机回调，避免旧节点继续响应相机帧。
        /// </summary>
        private void NodeBase_NodeDeletedEvent(object sender, NodeBase node)
        {
            if (ReferenceEquals(node, _node))
                UnbindCameraCallback();
        }

        /// <summary>
        /// 参数窗体释放时清理事件订阅。
        /// </summary>
        private void ParamFormImageSource_Disposed(object sender, EventArgs e)
        {
            UnbindCameraCallback();
            NodeBase.NodeDeletedEvent -= NodeBase_NodeDeletedEvent;
            SolRunParamControl.RefreshParamView -= SolRunParamControl_RefreshParamView;
        }


        /// <summary>
        /// 节点订阅结果
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node){}

        /// <summary>
        /// 选择图像源
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxImgSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 本地图像
            if (comboBoxImgSource.SelectedIndex == 0)
            {
                // 调整控件可视状态
                tableLayoutPanelChoiceImage.Visible = true;
                tableLayoutPanelCamera.Visible = false;
                tableLayoutPanelSharedVariable.Visible = false;
                // 设置窗口高度
                this.Height = _formHeight - tableLayoutPanelCamera.Height - tableLayoutPanelSharedVariable.Height;
            }
            // 相机
            else if (comboBoxImgSource.SelectedIndex == 1)
            {
                tableLayoutPanelChoiceImage.Visible = false;
                tableLayoutPanelCamera.Visible = true;
                tableLayoutPanelSharedVariable.Visible = false;
                this.Height = _formHeight - tableLayoutPanelChoiceImage.Height - tableLayoutPanelSharedVariable.Height;
                comboBoxTriggerMode.SelectedIndex = 0;
                comboBoxTriggerEdge.SelectedIndex = 0;
                comboBoxStrobe.SelectedIndex = 0;
                InitCameraList();
            }
            // 共享变量
            else if (comboBoxImgSource.SelectedIndex == 2)
            {
                tableLayoutPanelChoiceImage.Visible = false;
                tableLayoutPanelCamera.Visible = false;
                tableLayoutPanelSharedVariable.Visible = true;
                this.Height = _formHeight - tableLayoutPanelChoiceImage.Height - tableLayoutPanelCamera.Height;
                RefreshSharedVariableList(comboBoxSharedVariable.Text);
            }

        }

        #region 共享变量

        /// <summary>
        /// 刷新共享变量下拉框，并尽量保留指定变量名。
        /// </summary>
        /// <param name="selectedName">希望保留的共享变量名称。</param>
        private void RefreshSharedVariableList(string selectedName = null)
        {
            string variableName = string.IsNullOrWhiteSpace(selectedName) ? comboBoxSharedVariable.Text : selectedName;
            comboBoxSharedVariable.Items.Clear();
            comboBoxSharedVariable.Items.Add("[未选择]");
            foreach (var name in Solution.Instance.SharedVariable.GetNames())
            {
                comboBoxSharedVariable.Items.Add(name);
            }

            // 旧方案反序列化时，共享变量写入节点可能尚未完成还原，先保留已保存的变量名。
            if (!string.IsNullOrWhiteSpace(variableName) &&
                variableName != "[未选择]" &&
                !comboBoxSharedVariable.Items.Contains(variableName))
            {
                comboBoxSharedVariable.Items.Add(variableName);
            }

            int index = comboBoxSharedVariable.Items.IndexOf(variableName);
            comboBoxSharedVariable.SelectedIndex = index == -1 ? 0 : index;
        }

        #endregion

        #region 本地图像
        // 选择图像
        private void buttonChoiceImg_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "图片文件 (*.BMP, *.JPG, *.JPEG, *.PNG)|*.BMP;*.JPG;*.JPEG;*.PNG|所有文件 (*.*)|*.*";
            openFileDialog1.Multiselect = true;
            openFileDialog1.Title = "请选择图片";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(openFileDialog1.FileName))
                {
                    MessageBoxTD.Show("文件夹路径不能为空");
                    return;
                }
                textBoxImgPath.Text = openFileDialog1.FileName;
                checkBoxAuto.Checked = false;
                checkBoxAuto.Enabled = false;
            }
        }
        // 选择文件夹
        private void buttonChoiceImageCatalog_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.Description = "选择文件夹";

            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                if (string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                {
                    MessageBoxTD.Show("文件夹路径不能为空");
                    return;
                }
                textBoxImgPath.Text = folderBrowserDialog1.SelectedPath;
                checkBoxAuto.Enabled = true;
                ((NodeImageSource)_node).LastIndex = 0;
            }
        }
        #endregion

        #region 相机
        /// <summary>
        /// 初始化相机列表
        /// </summary>
        /// <param name="process"></param>
        /// <param name="node"></param>
        private void InitCameraList()
        {
            // 相机自定义名
            string text1 = comboBoxChoiceCamera.Text;
            comboBoxChoiceCamera.Items.Clear();
            comboBoxChoiceCamera.Items.Add("[未设置]");
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                comboBoxChoiceCamera.Items.Add(camera.UserDefinedName);
            }
            int index1 = comboBoxChoiceCamera.Items.IndexOf(text1);
            if (index1 == -1)
                comboBoxChoiceCamera.SelectedIndex = 0;
            else
                comboBoxChoiceCamera.SelectedIndex = index1;
        }
        /// <summary>
        /// 软硬触发切换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxTriggerMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 软触发
            if (0 == comboBoxTriggerMode.SelectedIndex)
                comboBoxTriggerEdge.Enabled = false;
            // 硬触发（Line0-Line4）
            else
                comboBoxTriggerEdge.Enabled = true;
        }

        /// <summary>
        /// 相机参数设置
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="triggerSource"></param>
        /// <param name="triggerEdge"></param>
        /// <param name="delay"></param>
        /// <param name="exposureTime"></param>
        /// <param name="gain"></param>
        private void SetCameraParams(ICamera camera, TriggerModel triggerModel, TriggerSource triggerSource, TriggerEdge triggerEdge, int delay, double exposureTime, double gain, uint timeOut)
        {
            camera.SetTriggerSource(triggerSource);     // 设置触发源
            camera.SetTriggerEdge(triggerEdge);         // 设置硬触发边沿
            camera.SetTriggerMode(triggerModel);        //设置触发模式
            //if (triggerSource != TriggerSource.Auto)
            //    camera.SetTriggerMode(true);                // 设置触发模式（除了自动取流外均设置）
            camera.SetTriggerDelay(delay);              // 设置触发延迟
            camera.SetExposureTime(exposureTime);       // 设置曝光时间
            camera.SetGain(gain);                       // 设置增益
            camera.GetImageTimeOut = timeOut;           // 设置采图超时xw
        }
        /// <summary>
        /// 选择相机改变事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxChoiceCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 获取对应相机曝光和增益参数
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                if (camera.UserDefinedName == comboBoxChoiceCamera.Text)
                {
                    // 当前相机触发延迟
                    var _triggerDelay = camera.GetTriggerDelay();
                    numericUpDownTriggerDelay.Minimum = (decimal)_triggerDelay.Min;
                    numericUpDownTriggerDelay.Maximum = (decimal)_triggerDelay.Max;
                    labelTriggerDelay.Text = $"触发延迟(当前{_triggerDelay.CurValue})";

                    // 当前相机曝光
                    var _exposurTime = camera.GetExposureTime();
                    numericUpDownExposureTime.Minimum = (decimal)_exposurTime.Min;
                    numericUpDownExposureTime.Maximum = (decimal)_exposurTime.Max;
                    labelExposureTime.Text = $"曝光(当前{_exposurTime.CurValue})";

                    // 当前相机增益
                    var (gainInt, gainFloat) = camera.GetGain();
                    if (gainInt != null)
                    {
                        numericUpDownGain.Minimum = gainInt.Min;
                        numericUpDownGain.Maximum = gainInt.Max;
                        labelGain.Text = $"增益(当前{gainInt.CurValue})";
                    }
                    else
                    {
                        numericUpDownGain.Minimum = (decimal)gainFloat.Min;
                        numericUpDownGain.Maximum = (decimal)gainFloat.Max;
                        labelGain.Text = $"增益(当前{gainFloat.CurValue})";
                    }
                    return;
                }
            }
            labelTriggerDelay.Text = "触发延迟(us)";
            labelExposureTime.Text = "曝光(us)";
            labelGain.Text = "增益";
        }

        #endregion

        private bool SaveParams()
        {
            NodeParamImageSoucre _nodeParamImageSource = new NodeParamImageSoucre();
            _nodeParamImageSource.ImageSource = comboBoxImgSource.Text;
            if (comboBoxImgSource.SelectedIndex == 0)
            {
                UnbindCameraCallback();
                if (string.IsNullOrEmpty(textBoxImgPath.Text))
                {
                    MessageBoxTD.Show("未选择图片");
                    LogHelper.AddLog(MsgLevel.Fatal, "未选择图片", true);
                    return false;
                }

                _nodeParamImageSource.PathText = textBoxImgPath.Text;
                // 判断是否为单张图片
                if (textBoxImgPath.Text.EndsWith(".bmp") || textBoxImgPath.Text.EndsWith(".jpg") 
                    || textBoxImgPath.Text.EndsWith(".jpeg") || textBoxImgPath.Text.EndsWith(".png")) 
                {
                    _nodeParamImageSource.ImagePath = this.textBoxImgPath.Text;
                }
                else
                {
                    var extensions = new[] { ".bmp", ".jpg", ".jpeg", ".png" }; // 你想要支持的所有扩展名
                    var files = Directory.GetFiles(textBoxImgPath.Text)
                                         .Where(s => extensions.Any(e => s.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                                         .ToList();

                    _nodeParamImageSource.ImagePaths = files;
                }

                _nodeParamImageSource.IsAutoLoop = checkBoxAuto.Checked;
            }
            else if (comboBoxImgSource.SelectedIndex == 1)
            {
                #region 参数合法校验

                int delay, exposureTime;
                uint timeOut;
                float gain;
                if (comboBoxChoiceCamera.Text == "[未设置]" || comboBoxChoiceCamera.Text.IsNullOrEmpty())
                {
                    MessageBoxTD.Show("相机为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                delay = (int)numericUpDownTriggerDelay.Value;
                exposureTime = (int)numericUpDownExposureTime.Value;
                gain = (float)numericUpDownGain.Value;
                timeOut = (uint)numericUpDownTimeOut.Value;

                #endregion

                #region 参数赋值

                //通过遍历方案设备查找选择的相机
                foreach (var camera in Solution.Instance.CameraDevices)
                {
                    if (camera.UserDefinedName == comboBoxChoiceCamera.Text)
                    {
                        _nodeParamImageSource.Camera = camera;
                        break;
                    }
                }
                // 保存相机名称参与序列化
                _nodeParamImageSource.CameraName = comboBoxChoiceCamera.Text;

                // 相机图像源统一使用回调触发模式。
                _nodeParamImageSource.TriggerModel = TriggerModel.On;

                _nodeParamImageSource.TriggerSource = ParseTriggerSourceText(comboBoxTriggerMode.Text);

                //硬触发设置触发沿
                switch (comboBoxTriggerEdge.Text)
                {
                    case "上升沿":
                        _nodeParamImageSource.TriggerEdge = TriggerEdge.Rising;
                        break;
                    case "下降沿":
                        _nodeParamImageSource.TriggerEdge = TriggerEdge.Falling;
                        break;
                    case "高电平":
                        _nodeParamImageSource.TriggerEdge = TriggerEdge.Hight;
                        break;
                    case "低电平":
                        _nodeParamImageSource.TriggerEdge = TriggerEdge.Low;
                        break;
                }



                //触发延迟
                _nodeParamImageSource.TriggerDelay = delay;
                //曝光设置
                _nodeParamImageSource.ExposureTime = exposureTime;
                //增益设置
                _nodeParamImageSource.Gain = gain;
                //采图超时时间
                _nodeParamImageSource.TimeOut = timeOut;
                // 是否每次设置参数
                _nodeParamImageSource.IsEveryTime = comboBoxStrobe.Text == "否" ? false : true;

                #endregion

                // 不是频闪应用可以在参数界面只设置一次,是频闪的话相机需要在节点运行时每次设置
                if (!_nodeParamImageSource.IsEveryTime)
                    SetCameraParams(_nodeParamImageSource.Camera, TriggerModel.On, _nodeParamImageSource.TriggerSource, _nodeParamImageSource.TriggerEdge, _nodeParamImageSource.TriggerDelay
                        , _nodeParamImageSource.ExposureTime, _nodeParamImageSource.Gain, _nodeParamImageSource.TimeOut);

            }
            else if (comboBoxImgSource.SelectedIndex == 2)
            {
                UnbindCameraCallback();
                if (comboBoxSharedVariable.Text.IsNullOrEmpty() || comboBoxSharedVariable.Text == "[未选择]")
                {
                    MessageBoxTD.Show("共享变量为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                _nodeParamImageSource.SharedVariableName = comboBoxSharedVariable.Text;
            }

            Params = _nodeParamImageSource;
            SyncCameraCallbackBinding();
            return true;
        }

        /// <summary>
        /// 获取触发源在参数界面中的显示文本。
        /// </summary>
        /// <param name="triggerSource">触发源枚举。</param>
        /// <returns>参数界面下拉框文本。</returns>
        private static string GetTriggerSourceDisplayText(TriggerSource triggerSource)
        {
            switch (triggerSource)
            {
                case TriggerSource.SOFT:
                    return "软触发";
                case TriggerSource.LINE0:
                    return "Line0";
                case TriggerSource.LINE1:
                    return "Line1";
                case TriggerSource.LINE2:
                    return "Line2";
                case TriggerSource.LINE3:
                    return "Line3";
                case TriggerSource.LINE4:
                    return "Line4";
                default:
                    return "软触发";
            }
        }

        /// <summary>
        /// 将参数界面文本解析为触发源枚举，兼容旧方案反显出的枚举名。
        /// </summary>
        /// <param name="text">参数界面下拉框文本。</param>
        /// <returns>触发源枚举。</returns>
        private static TriggerSource ParseTriggerSourceText(string text)
        {
            switch (text)
            {
                case "软触发":
                case "SOFT":
                    return TriggerSource.SOFT;
                case "Line0":
                case "LINE0":
                    return TriggerSource.LINE0;
                case "Line1":
                case "LINE1":
                    return TriggerSource.LINE1;
                case "Line2":
                case "LINE2":
                    return TriggerSource.LINE2;
                case "Line3":
                case "LINE3":
                    return TriggerSource.LINE3;
                case "Line4":
                case "LINE4":
                    return TriggerSource.LINE4;
                default:
                    return TriggerSource.SOFT;
            }
        }

        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if(SaveParams())
                Hide();
        }

        private void ParamFormImageSource_Load(object sender, EventArgs e)
        {
            if(comboBoxImgSource.SelectedIndex == 1)
                InitCameraList();
        }

    }

    /// <summary>
    /// 硬触发结果
    /// </summary>
    public struct HardTriggerResult
    {
        public DateTime StartTime;
        public bool IsSuccess;
        public string CameraName;
        public Bitmap Bitmap;
        public HardTriggerResult(DateTime StartTime, bool IsSuccess, string cameraName, Bitmap bitmap)
        {
            this.StartTime = StartTime;
            this.IsSuccess = IsSuccess;
            this.CameraName = cameraName;
            this.Bitmap = bitmap;
        }
    }
}
