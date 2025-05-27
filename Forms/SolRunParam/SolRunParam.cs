using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.SolRunParam
{
    internal partial class SolRunParamControl : UserControl
    {
        Process process;//显示哪个流程的参数
        SolRunData solRunDataInit = new SolRunData();//界面的初始参数
        SolRunData solRunDataNew = new SolRunData();//界面的最新参数
        ICamera _camera = null;
        TriggerSource triggerSrc;
        NodeTDAI _nodeTDAI;//AI节点
        string _windowsName; // 图像显示窗口名
        EventHandler<Bitmap> preEventHandler = null;//前一个活动的相机图像发布事件处理器
        public static event EventHandler<ImageShowPamra> ImageShowChanged;
        public SolRunParamControl()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
        }

        /// <summary>
        /// 使用流程初始化
        /// </summary>
        /// <param name="process"></param>
        public void Init(Process process)
        {
            try
            {
                this.process = process;
                SetEnable(false);
                foreach (var node in process.Nodes)
                {
                    //获取相机触发模式和曝光
                    if (node is NodeImageSource nodeSource && node.ParamForm.Params
                        is NodeParamImageSoucre nodeParam)
                    {
                        if (nodeParam != null && nodeParam.Camera != null)
                        {
                            // 订阅相机图像发布事件
                            _camera = nodeParam.Camera;
                            _camera.Subscribe(_camera_PublishImageEvent);
                            preEventHandler = _camera.GetActiveSubscriber();
                            _camera.SetActiveSubscriber(_camera_PublishImageEvent);
                            try
                            {
                                //获取参数
                                solRunDataInit.ExposureTime = nodeParam.Camera.GetExposureTime().CurValue.ToString();
                                var (intV, flloatV) = nodeParam.Camera.GetGain();
                                var gain = intV == null ? flloatV.CurValue : intV.CurValue;
                                solRunDataInit.Gain = gain.ToString();
                                triggerSrc = _camera.GetTriggerSource();
                                //设置参数
                                textBoxExposureTime.Text = solRunDataInit.ExposureTime.ToString();
                                textBoxGain.Text = solRunDataInit.Gain.ToString();
                                uiSwitchContinue.Active = nodeParam.Camera.GetTriggerSource() == TriggerSource.Auto ? true : false;
                                //设置控件可用性
                                textBoxExposureTime.Enabled = true;
                                textBoxGain.Enabled = true;
                                uiSwitchContinue.Enabled = true;
                                buttonOnce.Enabled = true;
                            }
                            catch (Exception) { }
                        }
                    }
                    //获取AI参数
                    if (node is NodeTDAI nodeTDAI &&
                        node.ParamForm.Params is NodeParamTDAI paramTDAI)
                    {
                        _nodeTDAI = nodeTDAI;//临时保存AI节点
                        if (paramTDAI != null)
                        {
                            //获取参数
                            solRunDataInit.ScoreThreshold = paramTDAI.AIInputInfo.ScoreThreshold.ToString();
                            solRunDataInit.ScoreNMS = paramTDAI.AIInputInfo.ScoreNMS.ToString();
                            solRunDataInit.DetectItems = paramTDAI.AIInputInfo.DetectItems;
                            //设置参数
                            textBoxScoreThreshold.Text = solRunDataInit.ScoreThreshold.ToString();
                            textBoxScoreNMS.Text = solRunDataInit.ScoreNMS.ToString();
                            //设置控件可用性
                            textBoxScoreThreshold.Enabled = true;
                            textBoxScoreNMS.Enabled = true;
                        }
                    }
                    if (node is NodeImageShow nodeShow)
                    {
                        if (nodeShow.ParamForm.Params is NodeParamImageShow showParam)
                        {
                            _windowsName = showParam.WindowName;
                        }
                    }
                }
                try
                {
                    LoadDetectItems(solRunDataInit.DetectItems);
                }
                catch (Exception) { }
                //深拷贝
                solRunDataNew = SolRunData.Copy(solRunDataInit);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        /// <summary>
        /// 相机取图回调
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void _camera_PublishImageEvent(object sender, System.Drawing.Bitmap e)
        {
            ImageShowChanged?.Invoke(this, new ImageShowPamra(_windowsName, e));
            if(SoftTriggerFlag)
            {
                _camera.SetActiveSubscriber(preEventHandler);////恢复之前的订阅者
                SoftTriggerFlag = false;
            }
        }

        /// <summary>
        /// 加载检测内容
        /// </summary>
        /// <param name="info"></param>
        private void LoadDetectItems(List<DetectItemInfo> infos)
        {
            myDataGridViewForm1.LoadConfig(infos);
        }
        /// <summary>
        /// 设置控件可用性
        /// </summary>
        /// <param name="enable"></param>
        private void SetEnable(bool enable)
        {
            uiSwitchContinue.Enabled = enable;
            buttonOnce.Enabled = enable;
            textBoxExposureTime.Enabled = enable;
            textBoxGain.Enabled = enable;
            textBoxScoreThreshold.Enabled = enable;
            textBoxScoreNMS.Enabled = enable;
        }
        /// <summary>
        /// 单图点检
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSingleImg_Click(object sender, EventArgs e)
        {
            //实现方法;找到流程中的图像源节点，将图像路径参数设置为当前选择的图像路径
            //最后运行流程即可，并恢复原来的图像路径
            foreach (var node in process.Nodes)
            {
                if (node is NodeImageSource nodeImage)
                {
                    if (nodeImage.ParamForm.Params is NodeParamImageSoucre nodeParam)
                    {
                        var imgSrc = nodeParam.ImageSource;
                        nodeParam.ImageSource = "本地图像";
                        //打开文件对话框，选择图像文件
                        openFileDialog1.Filter = "bmp图像|*.bmp|所有文件|*.*";
                        openFileDialog1.Title = "选择图像文件";
                        string modelOldPath = nodeParam.ImagePath;
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            //设置当前图像路径
                            nodeParam.ImagePath = openFileDialog1.FileName;
                            //运行流程
                            process.Run(false);
                            //恢复原来的图像路径
                            nodeParam.ImagePath = modelOldPath;
                        }
                        nodeParam.ImageSource = imgSrc;
                        nodeImage.ParamForm.Params = nodeParam;
                    }
                }
            }
        }
        /// <summary>
        /// 多图点检
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonCatalogueImg_Click(object sender, EventArgs e)
        {
            //实现方法;找到流程中的图像源节点，将图像路径参数设置为当前选择的图像目录
            foreach (var node in process.Nodes)
            {
                if (node is NodeImageSource nodeImage)
                {
                    if (nodeImage.ParamForm.Params is NodeParamImageSoucre nodeParam)
                    {
                        folderBrowserDialog1.Description = "选择图像文件夹";

                        if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                        {
                            if (string.IsNullOrEmpty(folderBrowserDialog1.SelectedPath))
                            {
                                MessageBox.Show(this, "文件夹路径不能为空", "提示");
                                return;
                            }
                            // 只获取扩展名为 .bmp 的文件
                            string[] files = Directory.GetFiles(folderBrowserDialog1.SelectedPath, "*.bmp");
                            // 临时保存原目录
                            var imgFiles = new List<string>(nodeParam.ImagePaths);
                            var imgPath = nodeParam.ImagePath;
                            var imageSource = nodeParam.ImageSource;
                            // 设置当前运行参数
                            nodeParam.ImagePath = null;
                            nodeParam.ImagePaths = new List<string>(files);
                            nodeParam.IsAutoLoop = true;
                            NodeImageSource.LastIndex = 0;
                            nodeParam.ImageSource = "本地图像";
                            //运行流程
                            for (int i = 0; i < files.Length; i++)
                            {
                                process.Run(false);
                                MessageBox.Show($"图像{files[NodeImageSource.LastIndex]}检测完毕！");
                            }
                            //恢复原来的图像路径
                            nodeParam.ImagePaths = imgFiles;
                            nodeParam.ImagePath = imgPath;
                            nodeImage.ParamForm.Params = nodeParam;
                            nodeParam.ImageSource = imageSource;
                            nodeImage.ParamForm.Params = nodeParam;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 保存所有参数到对应节点中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSaveAllParam_Click(object sender, EventArgs e)
        {
            try
            {
                SaveConfigs();

                MessageBox.Show("当前参数已成功保存！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception)
            {
                MessageBox.Show("参数保存失败，请检查参数是否设置无误！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void SaveConfigs()
        {
            try
            {
                if(_nodeTDAI.ParamForm.Params is NodeParamTDAI aiParams)
                {
                    aiParams.AIInputInfo.ScoreThreshold = (float)decimal.Parse(solRunDataNew.ScoreThreshold);
                    aiParams.AIInputInfo.ScoreNMS = (float)decimal.Parse(solRunDataNew.ScoreNMS);
                    aiParams.AIInputInfo.DetectItems = solRunDataNew.DetectItems;
                    _nodeTDAI.ParamForm.Params = aiParams;
                    //2025年5月27日 TODO:节点参数有更新，应该触发节点参数界面刷新当前设置的值
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// 连续采图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiSwitchContinue_ValueChanged(object sender, bool value)
        {
            if (_camera == null)
                return;
            if (value)
            {
                _camera.SetActiveSubscriber(_camera_PublishImageEvent);
                if (_camera.GetTriggerMode())
                    _camera.SetTriggerMode(false);
                if (!_camera.GetGrabStatus())
                    _camera.StartGrabbing();
            }
            else
            {
                _camera.SetActiveSubscriber(preEventHandler);////恢复之前的订阅者
                if (!_camera.GetGrabStatus())
                    _camera.StartGrabbing();
                if (!_camera.GetTriggerMode())
                    _camera.SetTriggerMode(true);
            }
        }

        bool SoftTriggerFlag = false;
        /// <summary>
        /// 单次采图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonOnce_Click(object sender, EventArgs e)
        {
            SoftTriggerFlag = true;
            _camera.SetActiveSubscriber(_camera_PublishImageEvent);

            if (uiSwitchContinue.Active)
                uiSwitchContinue.Active = false;
            if (!_camera.GetTriggerMode())
                _camera.SetTriggerMode(true);
            if (!_camera.GetGrabStatus())
                _camera.StartGrabbing();
            _camera.SetTriggerSource(TriggerSource.SOFT);//设置为软触发
            _camera.GrabOne();
            _camera.SetTriggerSource(triggerSrc);//触发后恢复原来的触发源
        }

        /// <summary>
        /// 参数设置完成时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxExposureTime_Leave(object sender, EventArgs e)
        {
            if (sender is Control con)
            {
                switch (con.Name)
                {
                    case "textBoxExposureTime":
                        if (float.TryParse(textBoxExposureTime.Text, out float exposure))
                        {
                            solRunDataNew.ExposureTime = textBoxExposureTime.Text;
                            _camera?.SetExposureTime(float.Parse(textBoxExposureTime.Text));
                        }
                        break;
                    case "textBoxGain":
                        if (float.TryParse(textBoxGain.Text, out float gain))
                        {
                            solRunDataNew.Gain = textBoxGain.Text;
                            _camera?.SetGain(float.Parse(textBoxGain.Text));
                        }
                        break;
                    case "textBoxScoreThreshold":
                        if (double.TryParse(textBoxScoreThreshold.Text, out double scoreThreshold))
                        {
                            solRunDataNew.ScoreThreshold = textBoxScoreThreshold.Text;
                        }
                        break;
                    case "textBoxScoreNMS":
                        if (double.TryParse(textBoxScoreNMS.Text, out double scoreNMS))
                        {
                            solRunDataNew.ScoreNMS = textBoxScoreNMS.Text;
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// 编辑完表格时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void myDataGridViewForm1_Leave(object sender, EventArgs e)
        {
            solRunDataNew.DetectItems = myDataGridViewForm1.GetConfig();
        }
        /// <summary>
        /// SolRunParam数据类
        /// </summary>
        internal class SolRunData
        {
            public SolRunData() { }
            public string ExposureTime { get; set; }
            public string Gain { get; set; }
            public string ScoreThreshold { get; set; }
            public string ScoreNMS { get; set; }
            public List<DetectItemInfo> DetectItems { get; set; } = new List<DetectItemInfo>();
            public static SolRunData Copy(SolRunData src)
            {
                var newData = new SolRunData();
                newData.ExposureTime = src.ExposureTime;
                newData.Gain = src.Gain;
                newData.ScoreThreshold = src.ScoreThreshold;
                newData.ScoreNMS = src.ScoreNMS;
                src.DetectItems.ForEach(item => newData.DetectItems.Add(item));
                return newData;
            }
        }
    }
}
