using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    public partial class ParamFormTDAI : FormBase, INodeParamForm
    {
        private NodeBase _node;
        AIInputInfo aIInputInfo;
        /// <summary>
        /// 添加AI模型信息事件
        /// </summary>
        public static EventHandler<(string, AIInputInfo)> AddAIInputInfoEvent;

        /// <summary>
        /// TDAI检测项监听地址
        /// </summary>
        private string Adress;

        /// <summary>
        /// TDAI检测项配置
        /// </summary>
        private BindingList<TDAICommuntionParam> TDAICommuntionParams { get; set; }

        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        public ParamFormTDAI()
        {
            InitializeComponent();
            SolRunParamControl.RefreshParamView += SolRunParamControl_RefreshParamView;
            ParseCommon.CloseAutoStudyEvent += ParamFormTDAI_CloseAutoStudyEvent;
            Shown += ParamFormTDAI_Shown;
        }

        private void ParamFormTDAI_Shown(object sender, EventArgs e)
        {
            InitDetectItemList(comboBoxDetectConfig1);
            InitDevice();
            InitDetectItemList(comboBoxDetectConfig2);
        }
        /// <summary>
        /// 初始化检测项列表下拉框
        /// </summary>
        /// <param name="combo"></param>
        private void InitDetectItemList(ComboBox combo)
        {
            if (Solution.Instance.DetectItemDic == null)
                return;
            string text1 = combo.Text;
            combo.Items.Clear();
            foreach (var pair in Solution.Instance.DetectItemDic)
            {
                combo.Items.Add(pair.Key);
            }
            int index1 = combo.Items.IndexOf(text1);
            if (index1 == -1)
                combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
            else
                combo.SelectedIndex = index1;
        }
        /// <summary>
        /// 初始化设备列表
        /// </summary>
        private void InitDevice()
        {
            string text1 = comboBoxDevice.Text;

            comboBoxDevice.Items.Clear();
            comboBoxDevice.Items.Add("[未设置]");
            foreach (var plc in Solution.Instance.PlcDevices)
            {
                comboBoxDevice.Items.Add(plc.UserDefinedName);
            }
            foreach (var mod in Solution.Instance.ModbusDevices)
            {
                comboBoxDevice.Items.Add(mod.UserDefinedName);
            }
            int index1 = comboBoxDevice.Items.IndexOf(text1);
            if (index1 == -1)
                comboBoxDevice.SelectedIndex = 0;
            else
                comboBoxDevice.SelectedIndex = index1;
        }
        /// <summary>
        /// 关闭对应自动学习事件处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ParamFormTDAI_CloseAutoStudyEvent(object sender, string e)
        {
            if (_node.NodeName == e)
            {
                if (Params is NodeParamTDAI param)
                {
                    param.IsAutoStudy = false; // 关闭自动学习
                    uiSwitch_Learning.Active = false; // 更新界面显示
                    LogHelper.AddLog(MsgLevel.Info, $"节点({param.NodeName})自动学习已关闭。", true);
                }
            }
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
        /// 获取订阅的节点类型
        /// </summary>
        /// <returns></returns>
        private NodeType GetNodeType()
        {
            return nodeSubscription1.GetNodeType();
        }

        /// <summary>
        /// 获取订阅的图片
        /// </summary>
        /// <returns></returns>
        public OutputImage GetOutputImage()
        {
            OutputImage inputImage = null;
            var type = GetNodeType();
            try
            {
                if (type == NodeType.SharedVariable)
                {
                    // 获取共享变量中的图像
                    var obj = nodeSubscription1.GetValue<SharedVarValue>();
                    inputImage = ((OutputImage)obj.Data);
                }
                else
                {
                    // 获取图像源节点的图像
                    inputImage = nodeSubscription1.GetValue<OutputImage>();
                }
            }
            catch (Exception)
            {
                return null;
            }
            return inputImage;
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

                // 先初始化下拉框
                InitDetectItemList(comboBoxDetectConfig1);
                InitDevice();
                InitDetectItemList(comboBoxDetectConfig2);

                Adress = param.Adress;
                TDAICommuntionParams = param.TDAICommuntionParams;

                radioButton1.Checked = param.IsFixed;
                radioButton2.Checked = !radioButton1.Checked;
                comboBoxDetectConfig1.Text = param.DetectItemName1;
                param.Device = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == param.DeviceName);
                comboBoxDevice.Text = param.DeviceName;
                comboBoxDetectConfig2.Text = param.DetectItemName2;
                uiSwitch_Convert.Active = param.NeedConvert;
                textBox_Scale.Text = param.Scale+"";

                SetComboBoxToEnumValue<ModelName>(comboBoxModelName, param.ModelName);
                uiSwitch_Learning.Active = param.IsAutoStudy;
                if(!string.IsNullOrEmpty(param.ConfigPath) && File.Exists(param.ConfigPath))
                {
                    try
                    {
                        string text = File.ReadAllText(textBoxConfigPath.Text);
                        text = StringCipher.Decrypt(text);
                        // 反序列化配置
                        aIInputInfo = JsonConvert.DeserializeObject<AIInputInfo>(text);
                        // 还原该配置到节点参数
                        param.AIInputInfo = aIInputInfo;
                    }
                    catch (Exception ex)
                    {
                        aIInputInfo = null;
                        LogHelper.AddLog(MsgLevel.Exception, $"AI配置文件解析失败！原因：{ex.Message}", true);
                    }
                }
                Task.Run(() => { LoadModel(param); });
                
            }
        }
        /// <summary>
        /// 设置ComboBox选中输入值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comboBox"></param>
        /// <param name="enumValue"></param>
        private void SetComboBoxToEnumValue<T>(ComboBox comboBox, T enumValue)
        {
            string enumString = enumValue.ToString();

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i].ToString() == enumString)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            // 如果没有找到匹配项，可以选择设置为 -1（不选中）
            comboBox.SelectedIndex = -1;
        }

        /// <summary>
        /// 点击选择标签文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bt_chooseLabel_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "选择AI配置文件";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBoxConfigPath.Text = openFileDialog1.FileName;
                try
                {
                    string text = File.ReadAllText(textBoxConfigPath.Text);
                    text = StringCipher.Decrypt(text);
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
                || (radioButton1.Checked && string.IsNullOrEmpty(comboBoxDetectConfig1.Text))
                || (radioButton2.Checked && string.IsNullOrEmpty(comboBoxDevice.Text))
                || (radioButton2.Checked && comboBoxDevice.Text == "[未设置]")
                || (radioButton2.Checked && string.IsNullOrEmpty(comboBoxDetectConfig2.Text))
                || string.IsNullOrEmpty(textBox_StudyNum.Text)
                || string.IsNullOrEmpty(textBox_Scale.Text)
                )
                {
                    throw new Exception("参数未设置完整！");
                }
                if (aIInputInfo == null)
                    throw new Exception("配置文件解析失败！");
                NodeParamTDAI nodeParamTDAI = new NodeParamTDAI();
                nodeParamTDAI.Text1 = nodeSubscription1.GetText1();
                nodeParamTDAI.Text2 = nodeSubscription1.GetText2();
                nodeParamTDAI.ConfigPath = textBoxConfigPath.Text;
                nodeParamTDAI.AIInputInfo = aIInputInfo;
                nodeParamTDAI.IsFixed = radioButton1.Checked;
                nodeParamTDAI.CurDetectItemName = radioButton1.Checked ? comboBoxDetectConfig1.Text : "";
                nodeParamTDAI.DetectItemName1 = comboBoxDetectConfig1.Text;
                nodeParamTDAI.Device = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == comboBoxDevice.Text);
                nodeParamTDAI.DeviceName = comboBoxDevice.Text;
                nodeParamTDAI.DetectItemName2 = comboBoxDetectConfig2.Text;
                nodeParamTDAI.IsAutoStudy = uiSwitch_Learning.Active;
                nodeParamTDAI.StudyNum = int.Parse(textBox_StudyNum.Text);
                nodeParamTDAI.StudyPercentage = float.Parse(textBox_studyPercentage.Text);
                nodeParamTDAI.NeedConvert = uiSwitch_Convert.Active;
                nodeParamTDAI.Scale = float.Parse(textBox_Scale.Text);
                nodeParamTDAI.NodeName = _node.NodeName;
                nodeParamTDAI.Adress = Adress;
                nodeParamTDAI.TDAICommuntionParams = TDAICommuntionParams;

                // 设置模型解析的名称
                switch (comboBoxModelName.Text)
                {
                   
                    case "RL_12类线芯模型":
                        nodeParamTDAI.ModelName = ModelName.RL_12类线芯模型;
                        break;
                    case "RL_线芯截面":
                        nodeParamTDAI.ModelName = ModelName.RL_线芯截面;
                        break;
                    case "合压模型":
                        nodeParamTDAI.ModelName = ModelName.合压模型;
                        break;
                    default:
                        throw new Exception($"未知的模型名称：{comboBoxModelName.Text}");
                }
                Params = nodeParamTDAI;

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

                // 如果是加密模型
                if (modelInfo.ModelInfo.IsEncrypted)
                {
                    if (modelInfo.ModelInfo.ModelType == ModelType.DET)
                    {
                        var detModel = new Yolo8Det();
                        detModel.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ClassNames.ToArray(), modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                        param.Yolo8 = detModel;
                    }
                    else if (modelInfo.ModelInfo.ModelType == ModelType.OBB)
                    {
                        var obbModel = new Yolo8Obb();
                        obbModel.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ClassNames.ToArray(), modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                        param.Yolo8 = obbModel;
                    }
                    else if (modelInfo.ModelInfo.ModelType == ModelType.SEG)
                    {
                        var segModel = new Yolo8Seg();
                        segModel.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ClassNames.ToArray(), modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                        param.Yolo8 = segModel;
                    }
                    else if (modelInfo.ModelInfo.ModelType == ModelType.POSE)
                    {
                        var poseModel = new Yolo8Pose();
                        poseModel.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ClassNames.ToArray(), modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS, modelInfo.ModelInfo.KeyPointNum);
                        param.Yolo8 = poseModel;
                    }
                    else
                    {
                        throw new Exception($"模型类型({modelInfo.ModelInfo.ModelType})不支持！");
                    }
                }
                else
                    throw new NotImplementedException($"模型({modelInfo.ModelInfo.ModelPath})未加密，当前版本不支持加载未加密模型！");
                
                //统一注册模型句柄到模型管理
                ModelHandleManager.Add(param.Yolo8);

                LogHelper.AddLog(MsgLevel.Info, $"模型：{param.AIInputInfo.ModelInfo.ModelPath} 加载完成！", true);
            }
            catch (Exception e)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"加载模型: {param.AIInputInfo.ModelInfo.ModelPath} 失败！原因：{e.Message}", true);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        private void uiSwitch_Convert_ValueChanged(object sender, bool value)
        {
            label_scale.Enabled = value;
            textBox_Scale.Enabled = value;
        }

        private void uiSwitch_Learning_ValueChanged(object sender, bool value)
        {
            label_studyNum.Enabled = value;
            textBox_StudyNum.Enabled = value;
            label_studyPercentage.Enabled = value;
            textBox_studyPercentage.Enabled = value;
        }
        /// <summary>
        /// 单选按钮切换处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void radioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                comboBoxDetectConfig1.Enabled = true;

                comboBoxDevice.Enabled = false;
                label6.Enabled = false;
                label7.Enabled = false;
                comboBoxDetectConfig2.Enabled = false;
                label8.Enabled = false;
                linkLabel1.Enabled = false;
            }
            else
            {
                comboBoxDetectConfig1.Enabled = true;

                comboBoxDevice.Enabled = true;
                label6.Enabled = true;
                label7.Enabled = true;
                comboBoxDetectConfig2.Enabled = true;
                label8.Enabled = true;

                linkLabel1.Enabled = true;
            }
        }



        /// <summary>
        /// 打开配置项目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                IDevice commountionDevice = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == comboBoxDevice.Text);
                if (commountionDevice != null)
                {
                    TDAICommuntionDetectionConfig tDAICommuntionDetectionConfig = new TDAICommuntionDetectionConfig(commountionDevice, TDAICommuntionParams,Adress);
                    tDAICommuntionDetectionConfig.ShowDialog();

                    TDAICommuntionParams = tDAICommuntionDetectionConfig.TDAICommuntionParamsDb;
                    Adress = tDAICommuntionDetectionConfig.Adress;
                }
                else throw new Exception();
            }
            catch (Exception ex){
                MessageBoxTD.Show($"改通信设备异常请检查双方之间的通信. {ex}");
            }
        }
    }
}
