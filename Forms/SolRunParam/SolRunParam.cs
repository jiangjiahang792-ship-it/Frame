using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._5_EquipmentCommunication.ModbusWrite;

namespace TDJS_Vision.Forms.SolRunParam
{
    internal partial class SolRunParam : UserControl
    {
        Process process;
        public SolRunParam()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
        }
        /// <summary>
        /// 加载参数值
        /// </summary>
        /// <param name="isTrigger"></param>
        /// <param name="exposureTime"></param>
        /// <param name="scoreThrethold"></param>
        /// <param name="nmsScore"></param>
        private void LoadParamValue(bool isTrigger, string exposureTime, string scoreThrethold, string nmsScore, string address, string file)
        {
            uiSwitchTriggerMode.Active = isTrigger;
            textBoxExposureTime.Text = exposureTime;
            textBoxScoreThreshold.Text = scoreThrethold;
            textBoxScoreNMS.Text = nmsScore;
            textBoxAddress.Text = address;
            try
            {
                // 尝试获取文件名，如果路径包含非法字符会抛出异常
                Path.GetFileName(file);
                LoadLabel(file);
            }
            catch (Exception)
            {

            }
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
                bool mode = false; float exposure = 0f; ushort address = 0;
                float scoreThreshold = 0f, scoreNms = 0f; string labelFile = "";
                foreach (var node in process.Nodes)
                {
                    //获取相机触发模式和曝光
                    if (node is NodeImageSource nodeSource && node.ParamForm.Params
                        is NodeParamImageSoucre nodeParam)
                    {
                        if (nodeParam != null && nodeParam.Camera != null)
                        {
                            try
                            {
                                mode = nodeParam.Camera.GetTriggerMode();
                                exposure = nodeParam.Camera.GetExposureTime().CurValue;
                                uiSwitchTriggerMode.Enabled = true;
                                textBoxExposureTime.Enabled = true;
                            }
                            catch (Exception) { }
                        }
                    }
                    //获取AI参数
                    if (node is NodeTDAI nodeTDAI &&
                        node.ParamForm.Params is NodeParamTDAI paramTDAI)
                    {
                        if(paramTDAI != null)
                        {
                            scoreThreshold = paramTDAI.ScoreThreshold;
                            scoreNms = paramTDAI.ScoreNMS;
                            labelFile = paramTDAI.LabelPath;
                            textBoxScoreThreshold.Enabled = true;
                            textBoxScoreNMS.Enabled = true;
                        }
                    }
                    //获取通信
                    if (node is NodeModbusWrite nodeWrite && node.ParamForm.Params
                        is NodeParamModbusWrite nodeParamModbus)
                    {
                        if(nodeParamModbus != null)
                        {
                            address = nodeParamModbus.StartAddress;
                            textBoxAddress.Enabled = true;
                        }
                    }
                }
                try
                {
                    // 加载流程参数值
                    LoadParamValue(mode, exposure.ToString(), scoreThreshold.ToString(), scoreNms.ToString(), address.ToString(), labelFile);
                }
                catch (Exception){}
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        /// <summary>
        /// 加载标签文件
        /// </summary>
        /// <param name="labelFile"></param>
        private void LoadLabel(string labelFile)
        {
            tdjsParameterGrid1.LoadLabel(labelFile);
        }
        /// <summary>
        /// 设置控件可用性
        /// </summary>
        /// <param name="enable"></param>
        private void SetEnable(bool enable)
        {
            uiSwitchTriggerMode.Enabled = enable;
            textBoxExposureTime.Enabled = enable;
            textBoxScoreThreshold.Enabled = enable;
            textBoxScoreNMS.Enabled = enable;
            textBoxAddress.Enabled = enable;
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
                if(node is NodeImageSource nodeImage)
                {
                    if(nodeImage.ParamForm.Params is NodeParamImageSoucre nodeParam)
                    {
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
                        nodeParam.ImageSource = "相机";
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
                            nodeParam.ImageSource = "相机";
                        }
                        //nodeImage.ParamForm.Params = nodeParam;
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

        }
    }
}
