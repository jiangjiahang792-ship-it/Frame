using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using Newtonsoft.Json;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal partial class ParamFormTDAI : Form, INodeParamForm
    {
        private NodeBase _node;
        private List<AIInputInfo> tmpInfoList;
        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        public ParamFormTDAI()
        {
            InitializeComponent();
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
        }
        /// <summary>
        /// 刷新从SolRunParamControl设置的参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SolRunParamControl_RefreshParamView(object sender, EventArgs e)
        {
            if(Params is NodeParamTDAI param)
            {
                uiSwitch_Learning.Active = param.IsAutoStudy;
            }
        }

        /// <summary>
        /// 用于节点参数界面需要订阅结果的情况调用
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
            _node = node;
        }

        /// <summary>
        /// 获取订阅的图片
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            return (Bitmap)nodeSubscription1.GetValue<Bitmap>().Clone();
        }

        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamTDAI param)
            {
                // 还原界面显示
                nodeSubscription1.SetText(param.Text1, param.Text2);
                textBoxConfigPath.Text = param.ConfigPath;
            }
        }

        /// <summary>
        /// 点击选择标签文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bt_chooseLabel_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Json文件|*.json";
            openFileDialog1.Title = "选择配置文件";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBoxConfigPath.Text = openFileDialog1.FileName;
                string text = File.ReadAllText(textBoxConfigPath.Text);
                try
                {
                    tmpInfoList = JsonConvert.DeserializeObject<List<AIInputInfo>>(text);// 反序列化配置
                    comboBoxModelIndex.Items.Clear();
                    for (int i = 0; i < tmpInfoList.Count; i++)
                    {
                        comboBoxModelIndex.Items.Add(i.ToString());
                    }
                    if (comboBoxModelIndex.Items.Count > 0)
                        comboBoxModelIndex.SelectedIndex = 0; // 默认选择第一个模型
                    else
                        throw new Exception("解析到模型个数为0！");
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"AI配置文件解析失败！原因：{ex.Message}", true);
                }
            }
        }
        /// <summary>
        /// 设置参数
        /// </summary>
        /// <returns></returns>
        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrEmpty(nodeSubscription1.GetText1())
                || string.IsNullOrEmpty(nodeSubscription1.GetText2())
                || string.IsNullOrEmpty(textBoxConfigPath.Text)
                )
                {
                    throw new Exception();
                }
                NodeParamTDAI nodeParamTDAI = new NodeParamTDAI();
                nodeParamTDAI.Text1 = nodeSubscription1.GetText1();
                nodeParamTDAI.Text2 = nodeSubscription1.GetText2();
                nodeParamTDAI.ConfigPath = textBoxConfigPath.Text;
                string text = File.ReadAllText(textBoxConfigPath.Text);
                nodeParamTDAI.AIInputInfoList = tmpInfoList;
                nodeParamTDAI.SelectedModelIndex = comboBoxModelIndex.SelectedIndex;
                nodeParamTDAI.IsAutoStudy = uiSwitch_Learning.Active;
                Params = nodeParamTDAI;
                // 异步加载模型
                //Task.Run(() => LoadModel(nodeParamTDAI));
            }
            catch (Exception)
            {
                MessageBox.Show("参数设置异常，请检查参数设置是否合理！");
                return false;
            }
            return true;
        }
        /// <summary>
        /// 加载模型
        /// </summary>
        private void LoadModel(NodeParamTDAI param)
        {
            try
            {
                var modelInfo = param.AIInputInfoList[param.SelectedModelIndex];
                IntPtr handle = IntPtr.Zero;
                // 是否加密模型
                if (modelInfo.ModelInfo.IsEncrypted)
                {
                    byte[] Key = Encoding.UTF8.GetBytes("12345678912345671234567891234567"); // 32字节密钥
                    byte[] IV = Encoding.UTF8.GetBytes("1234567891234567");   // 16字节IV

                    // 1. 端子解密模型到临时文件
                    string tempXmlPath = Path.GetTempFileName();
                    string tempBinPath = Path.GetTempFileName();

                    YoloV8.DecryptFile(modelInfo.ModelInfo.XmlPath, tempXmlPath, Key, IV);
                    YoloV8.DecryptFile(modelInfo.ModelInfo.BinPath, tempBinPath, Key, IV);

                    //handle = YoloV8.CreateDetector(modelInfo.ModelInfo.XmlPath, modelInfo.ModelInfo.BinPath, modelInfo.ModelInfo.ClassNames, modelInfo.ModelInfo.ClassCount, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                }
                else
                {

                }
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"加载模型失败！原因：{e.Message}", true);
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }
    }
}
