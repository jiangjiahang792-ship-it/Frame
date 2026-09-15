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
        /// 当前参数窗体所属的图像源节点。
        /// </summary>
        private readonly NodeImageSource _imageSourceNode;

        /// <summary>
        /// 是否正在从方案恢复控件，恢复期间禁止相机选择事件读取或改写硬件参数。
        /// </summary>
        private bool _isRestoringParameters;

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
                SetNumericUpDownValueInRange(numericUpDownExposureTime, (decimal)paramSrc.ExposureTime);
                SetNumericUpDownValueInRange(numericUpDownGain, (decimal)paramSrc.Gain);
            }
        }

        /// <summary>
        /// 扩展控件范围并完整回填方案数值，禁止显示后再保存时静默截断方案。
        /// </summary>
        /// <param name="control">需要回填的数值控件。</param>
        /// <param name="value">待回填的节点参数值。</param>
        private static void SetNumericUpDownValueInRange(NumericUpDown control, decimal value)
        {
            if (control == null)
                return;

            if (value < control.Minimum)
                control.Minimum = value;
            if (value > control.Maximum)
                control.Maximum = value;

            control.Value = value;
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamImageSoucre param)
            {
                ICamera cameraToConfigure = null;
                _isRestoringParameters = true;
                try
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

                        // 先按方案中的相机名称绑定当前设备对象，不能依赖上次运行留下的对象引用。
                        cameraToConfigure = Solution.Instance.ResolveImageSourceCamera(param);

                        // 还原选中的相机。
                        comboBoxChoiceCamera.Items.Clear();
                        comboBoxChoiceCamera.Items.Add("[未设置]");
                        foreach (var camera in Solution.Instance.CameraDevices)
                        {
                            comboBoxChoiceCamera.Items.Add(camera.UserDefinedName);
                        }
                        int index = comboBoxChoiceCamera.Items.IndexOf(param.CameraName);
                        comboBoxChoiceCamera.SelectedIndex = index == -1 ? 0 : index;
                        // 还原选中的触发源。
                        comboBoxTriggerMode.Text = GetTriggerSourceDisplayText(param.TriggerSource);
                        // 还原硬触发沿和方案参数；相机当前值不参与恢复。
                        comboBoxTriggerEdge.SelectedIndex = (int)param.TriggerEdge;
                        SetNumericUpDownValueInRange(numericUpDownTriggerDelay, param.TriggerDelay);
                        SetNumericUpDownValueInRange(numericUpDownExposureTime, (decimal)param.ExposureTime);
                        SetNumericUpDownValueInRange(numericUpDownGain, (decimal)param.Gain);
                        SetNumericUpDownValueInRange(numericUpDownTimeOut, param.TimeOut);
                        int index1 = comboBoxStrobe.Items.IndexOf(param.IsEveryTime ? "是" : "否");
                        comboBoxStrobe.SelectedIndex = index1 == -1 ? 0 : index1;
                    }
                    else if (param.ImageSource == "共享变量")
                    {
                        RefreshSharedVariableList(param.SharedVariableName);
                    }
                }
                finally
                {
                    _isRestoringParameters = false;
                }

                // 相机在线时立即把方案值写入硬件；离线只保留方案和绑定关系，不阻断节点恢复。
                if (cameraToConfigure != null && cameraToConfigure.IsOpen)
                    TryApplySchemeCameraParameters(cameraToConfigure, param, "恢复图像源方案");
            }
        }

        /// <summary>
        /// 参数窗体释放时清理静态事件订阅。
        /// </summary>
        private void ParamFormImageSource_Disposed(object sender, EventArgs e)
        {
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
            if (camera == null)
                throw new InvalidOperationException("图像源没有可用相机对象。");

            TriggerSource effectiveTriggerSource = triggerSource == TriggerSource.Auto
                ? TriggerSource.SOFT
                : triggerSource;
            camera.SetTriggerMode(triggerModel);        // 设置触发模式
            camera.SetTriggerSource(effectiveTriggerSource); // 设置触发源
            if (IsHardwareTriggerSource(effectiveTriggerSource))
                camera.SetTriggerEdge(triggerEdge);     // 只有线路硬触发存在触发极性
            //if (triggerSource != TriggerSource.Auto)
            //    camera.SetTriggerMode(true);                // 设置触发模式（除了自动取流外均设置）
            camera.SetTriggerDelay(delay);              // 设置触发延迟
            camera.SetExposureTime(exposureTime);       // 设置曝光时间
            camera.SetGain(gain);                       // 设置增益
            camera.GetImageTimeOut = timeOut;           // 设置采图超时xw
        }

        /// <summary>
        /// 尝试把方案参数写入在线相机，写入失败只记录原因，不能破坏方案恢复。
        /// </summary>
        /// <param name="camera">目标相机。</param>
        /// <param name="param">图像源方案参数。</param>
        /// <param name="operation">日志中的操作名称。</param>
        private void TryApplySchemeCameraParameters(ICamera camera, NodeParamImageSoucre param, string operation)
        {
            bool resumeGrabbing = false;
            try
            {
                bool wasGrabbing = camera.GetGrabStatus();
                if (wasGrabbing)
                {
                    camera.StopGrabbing();
                    resumeGrabbing = true;
                }

                SetCameraParams(
                    camera,
                    TriggerModel.On,
                    param.TriggerSource,
                    param.TriggerEdge,
                    param.TriggerDelay,
                    param.ExposureTime,
                    param.Gain,
                    param.TimeOut);
                LogHelper.AddLog(
                    MsgLevel.Info,
                    $"{operation}已按方案写入相机【{camera.UserDefinedName}】：曝光={param.ExposureTime}us，增益={param.Gain}。",
                    true);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(
                    MsgLevel.Warn,
                    $"{operation}向相机【{camera.UserDefinedName}】写入方案参数失败，方案仍已正常恢复。原因：{ex.Message}",
                    true);
            }
            finally
            {
                if (resumeGrabbing && camera.IsOpen)
                {
                    try
                    {
                        camera.StartGrabbing();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Exception,
                            $"{operation}后恢复相机【{camera.UserDefinedName}】取流失败：{ex.Message}",
                            true);
                    }
                }
            }
        }

        /// <summary>
        /// 判断触发源是否为线路硬触发。
        /// </summary>
        /// <param name="triggerSource">待判断的触发源。</param>
        /// <returns>Line0至Line4时返回true。</returns>
        private static bool IsHardwareTriggerSource(TriggerSource triggerSource)
        {
            return triggerSource >= TriggerSource.LINE0 && triggerSource <= TriggerSource.LINE4;
        }
        /// <summary>
        /// 选择相机改变事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxChoiceCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            labelTriggerDelay.Text = "触发延迟(us)";
            labelExposureTime.Text = "曝光(us)";
            labelGain.Text = "增益";
            if (_isRestoringParameters)
                return;

            // 只刷新相机支持的触发源，不读取相机当前曝光、增益或延迟覆盖方案值。
            foreach (var camera in Solution.Instance.CameraDevices)
            {
                if (camera.UserDefinedName == comboBoxChoiceCamera.Text)
                {
                    try
                    {
                        RefreshTriggerSourceItems(camera);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(
                            MsgLevel.Warn,
                            $"刷新相机【{camera.UserDefinedName}】支持的触发源失败，保留方案中的触发设置。原因：{ex.Message}",
                            true);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// 按当前相机SDK真实支持项刷新触发源，避免把Counter0误显示为Line4。
        /// </summary>
        /// <param name="camera">当前选中的二维相机。</param>
        private void RefreshTriggerSourceItems(ICamera camera)
        {
            if (camera == null || !camera.IsOpen)
                return;

            string previousText = comboBoxTriggerMode.Text;
            CameraEnumValue options = camera.GetTriggerSourceOptions();
            List<string> items = options.SupportEnumEntries
                .Take((int)Math.Min(options.SupportedNum, (uint)options.SupportEnumEntries.Length))
                .Select(GetTriggerSourceDisplayText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct()
                .ToList();
            if (items.Count == 0)
                items.AddRange(new[] { "软触发", "Line0", "Line1", "Line2", "Line3" });

            comboBoxTriggerMode.Items.Clear();
            comboBoxTriggerMode.Items.AddRange(items.Cast<object>().ToArray());
            comboBoxTriggerMode.SelectedItem = items.Contains(previousText) ? previousText : items[0];
        }

        #endregion

        private bool SaveParams()
        {
            NodeParamImageSoucre _nodeParamImageSource = new NodeParamImageSoucre();
            _nodeParamImageSource.ImageSource = comboBoxImgSource.Text;
            if (comboBoxImgSource.SelectedIndex == 0)
            {
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

                int delay;
                double exposureTime;
                uint timeOut;
                double gain;
                if (comboBoxChoiceCamera.Text == "[未设置]" || comboBoxChoiceCamera.Text.IsNullOrEmpty())
                {
                    MessageBoxTD.Show("相机为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                delay = (int)numericUpDownTriggerDelay.Value;
                exposureTime = (double)numericUpDownExposureTime.Value;
                gain = (double)numericUpDownGain.Value;
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
                if (_nodeParamImageSource.Camera == null)
                {
                    MessageBoxTD.Show("方案中的相机当前不存在！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

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

                // 在线且不是逐次设置时立即应用；离线也允许保存，运行前会按名称重新绑定并再次应用。
                if (!_nodeParamImageSource.IsEveryTime && _nodeParamImageSource.Camera.IsOpen)
                    TryApplySchemeCameraParameters(_nodeParamImageSource.Camera, _nodeParamImageSource, "保存图像源方案");

            }
            else if (comboBoxImgSource.SelectedIndex == 2)
            {
                if (comboBoxSharedVariable.Text.IsNullOrEmpty() || comboBoxSharedVariable.Text == "[未选择]")
                {
                    MessageBoxTD.Show("共享变量为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                _nodeParamImageSource.SharedVariableName = comboBoxSharedVariable.Text;
            }

            Params = _nodeParamImageSource;
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
        /// 获取SDK触发源枚举项在参数界面中的显示文本。
        /// </summary>
        /// <param name="entry">SDK触发源枚举项。</param>
        /// <returns>框架支持的触发源文本；不支持的类型返回空文本。</returns>
        private static string GetTriggerSourceDisplayText(CameraEnumEntry entry)
        {
            string symbolic = entry?.Symbolic ?? string.Empty;
            if (symbolic.Equals("Software", StringComparison.OrdinalIgnoreCase))
                return "软触发";
            switch (symbolic.ToUpperInvariant())
            {
                case "LINE0":
                    return "Line0";
                case "LINE1":
                    return "Line1";
                case "LINE2":
                    return "Line2";
                case "LINE3":
                    return "Line3";
                case "LINE4":
                    return "Line4";
                default:
                    return string.Empty;
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
