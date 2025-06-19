using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using Newtonsoft.Json;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal partial class ParamFormTDAI : FormBase, INodeParamForm
    {
        private NodeBase _node;
        AIInputInfo aIInputInfo;
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
                uiSwitch_Learning.Active = param.IsAutoStudy;
                if(!string.IsNullOrEmpty(param.ConfigPath) && File.Exists(param.ConfigPath))
                {
                    try
                    {
                        string text = File.ReadAllText(textBoxConfigPath.Text);
                        aIInputInfo = JsonConvert.DeserializeObject<AIInputInfo>(text);// 反序列化配置
                        param.AIInputInfo = aIInputInfo;// 还原该配置到节点参数
                    }
                    catch (Exception ex)
                    {
                        aIInputInfo = null;
                        LogHelper.AddLog(MsgLevel.Exception, $"AI配置文件解析失败！原因：{ex.Message}", true);
                    }
                }
                // 异步加载模型
                Task.Run(() => LoadModel(param));
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
                try
                {
                    string text = File.ReadAllText(textBoxConfigPath.Text);
                    aIInputInfo = JsonConvert.DeserializeObject<AIInputInfo>(text);// 反序列化配置
                }
                catch (Exception ex)
                {
                    aIInputInfo = null;
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
                if (aIInputInfo == null)
                    throw new Exception("配置文件解析失败！");
                nodeParamTDAI.AIInputInfo = aIInputInfo;
                nodeParamTDAI.IsAutoStudy = uiSwitch_Learning.Active;
                Params = nodeParamTDAI;
                // 异步加载模型
                Task.Run(() => LoadModel(nodeParamTDAI));
            }
            catch (Exception e)
            {
                MessageBoxTD.Show($"参数设置异常，原因：{e.Message}");
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
                LogHelper.AddLog(MsgLevel.Info, $"开始加载模型：{param.AIInputInfo.ModelInfo.ModelPath}", true);
                var modelInfo = param.AIInputInfo;
                // 是否加密模型
                if (modelInfo.ModelInfo.IsEncrypted)
                {
                    param.Handle = YoloV8Det.CreateDetector(modelInfo.ModelInfo.ModelPath, modelInfo.ClassNames.ToArray(), modelInfo.ClassNames.Count, modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                }
                else
                {

                }
                LogHelper.AddLog(MsgLevel.Info, $"模型：{param.AIInputInfo.ModelInfo.ModelPath} 加载完成！", true);
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
