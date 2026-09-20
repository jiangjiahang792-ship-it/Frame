using Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Device;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;
using TDJS_Vision.Startup;

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

        /// <summary>
        /// 当前界面暂存的 ROI 检测区域，保存参数时写入节点参数。
        /// </summary>
        private List<TDAIRoiRegion> _roiRegions = new List<TDAIRoiRegion>();

        /// <summary>
        /// 原生AI模型初始化锁，避免多个节点同时加载同一个加密模型导致底层DLL失败。
        /// </summary>
        private static readonly object ModelLoadLock = new object();
        /// <summary>
        /// 原生模型初始化失败后的重试次数。
        /// </summary>
        private const int ModelLoadRetryCount = 2;

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
            // 新解析器携带参数实例，防止不同流程的同名节点一起停止学习；旧事件仍按名称匹配。
            bool isCurrentNode = sender is NodeParamTDAI source
                ? ReferenceEquals(Params, source)
                : _node != null && _node.NodeName == e;
            if (isCurrentNode)
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
            nodeSubscription1.SetInputContract(SubscriptionInputContract.ForCategories(new[]
            {
                SubscriptionDataCategory.Image,
                SubscriptionDataCategory.StructuredObject
            }));
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
                uiSwitch_RoiEnable.Active = param.RoiEnable;
                _roiRegions = CloneRoiRegions(param.RoiRegions);
                RefreshRoiEditLinkText();

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
                StartupAiRuntimeLoadGate.RunOrDeferTDAILoad(BuildStartupLoadDescription(param), () => StartLoadModel(param));
                
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
            // 枚举标识符不能包含连字符，恢复时映射到用户指定的界面名称。
            if (enumValue is ModelName && enumString == nameof(ModelName.AST_工位1模型))
                enumString = "AST-工位1模型";

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
                nodeParamTDAI.RoiEnable = uiSwitch_RoiEnable.Active;
                nodeParamTDAI.RoiRegions = CloneRoiRegions(_roiRegions);

                if (nodeParamTDAI.RoiEnable && (nodeParamTDAI.RoiRegions == null || nodeParamTDAI.RoiRegions.Count == 0))
                    throw new Exception("启用ROI后请先点击“绘制检测区域”并至少绘制一个ROI。");

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
                    case "XM_Fakra模型":
                        nodeParamTDAI.ModelName = ModelName.XM_Fakra模型;
                        break;
                    case "多端子模型":
                        nodeParamTDAI.ModelName = ModelName.多端子模型;
                        break;
                    case "超声波焊接侧面三类模型":
                        nodeParamTDAI.ModelName = ModelName.超声波焊接侧面三类模型;
                        break;
                    case "AST-工位1模型":
                        nodeParamTDAI.ModelName = ModelName.AST_工位1模型;
                        break;
                    default:
                        throw new Exception($"未知的模型名称：{comboBoxModelName.Text}");
                }
                Params = nodeParamTDAI;

                StartLoadModel(nodeParamTDAI);
            }
            catch (Exception e)
            {
                MessageBoxTD.Show($"参数设置异常，原因：{e.Message}");
                return false;
            }
            return true;
        }
        /// <summary>
        /// 启动AI模型异步加载任务。
        /// </summary>
        /// <param name="param"></param>
        private void StartLoadModel(NodeParamTDAI param)
        {
            if (param == null)
                return;

            int modelLoadVersion = param.NextModelLoadVersion();
            param.ModelLoadTask = Task.Run(() => LoadModel(param, modelLoadVersion));
        }

        /// <summary>
        /// 生成启动期 TDAI 延迟加载日志描述。
        /// </summary>
        /// <param name="param">TDAI 节点参数。</param>
        /// <returns>用于日志显示的模型描述。</returns>
        private static string BuildStartupLoadDescription(NodeParamTDAI param)
        {
            if (param == null)
                return "未设置TDAI参数";

            string nodeName = string.IsNullOrWhiteSpace(param.NodeName) ? "未命名TDAI节点" : param.NodeName;
            string modelPath = param.AIInputInfo == null
                ? "未设置模型路径"
                : param.AIInputInfo.ModelInfo.ModelPath;
            return $"{nodeName} / {modelPath}";
        }
        /// <summary>
        /// 加载模型
        /// </summary>
        /// <param name="param"></param>
        /// <param name="modelLoadVersion"></param>
        private void LoadModel(NodeParamTDAI param, int modelLoadVersion)
        {
            string modelPath = "未知模型";
            IYolo8 newModel = null;
            try
            {
                if (param == null)
                    throw new Exception("节点参数为空，无法加载模型！");
                if (param.AIInputInfo == null)
                    throw new Exception("AI配置未加载或解析失败，无法加载模型！");

                modelPath = param.AIInputInfo.ModelInfo.ModelPath;
                if (string.IsNullOrWhiteSpace(modelPath))
                    throw new Exception("AI模型路径为空，无法加载模型！");

                LogHelper.AddLog(MsgLevel.Info, $"开始加载模型：{modelPath}", true);
                var modelInfo = param.AIInputInfo;

                lock (ModelLoadLock)
                    newModel = CreateModelHandleWithRetry(modelInfo, modelPath);

                if (!param.IsCurrentModelLoadVersion(modelLoadVersion))
                {
                    ModelHandleManager.Destroy(newModel);
                    return;
                }

                var oldModel = param.Yolo8;

                // 统一注册模型句柄到模型管理，替换成功后释放旧句柄。
                ModelHandleManager.Add(newModel);
                param.Yolo8 = newModel;
                param.LoadedModelPath = modelPath;
                param.LastModelLoadError = string.Empty;
                if (oldModel != null && !ReferenceEquals(oldModel, newModel))
                {
                    try
                    {
                        ModelHandleManager.Destroy(oldModel);
                    }
                    catch (Exception destroyEx)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"释放旧AI模型句柄失败，原因：{destroyEx.Message}", true);
                    }
                }

                LogHelper.AddLog(MsgLevel.Info, $"模型：{modelPath} 加载完成！", true);
            }
            catch (Exception e)
            {
                if (param != null && param.IsCurrentModelLoadVersion(modelLoadVersion))
                    param.LastModelLoadError = e.Message;
                if (newModel != null)
                    ModelHandleManager.Destroy(newModel);
                LogHelper.AddLog(MsgLevel.Exception, $"加载模型: {modelPath} 失败！原因：{e.Message}", true);
            }
        }

        /// <summary>
        /// 在原生DLL初始化失败时进行短暂重试。
        /// </summary>
        /// <param name="modelInfo"></param>
        /// <param name="modelPath"></param>
        /// <returns></returns>
        private static IYolo8 CreateModelHandleWithRetry(AIInputInfo modelInfo, string modelPath)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= ModelLoadRetryCount; attempt++)
            {
                try
                {
                    return CreateModelHandle(modelInfo);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt >= ModelLoadRetryCount)
                        break;

                    LogHelper.AddLog(MsgLevel.Warn, $"模型：{modelPath} 第{attempt}次加载失败，准备重试。原因：{ex.Message}", true);
                    Thread.Sleep(200);
                }
            }

            throw lastException;
        }

        /// <summary>
        /// 根据AI配置创建对应类型的模型句柄。
        /// </summary>
        /// <param name="modelInfo"></param>
        /// <returns></returns>
        private static IYolo8 CreateModelHandle(AIInputInfo modelInfo)
        {
            if (!modelInfo.ModelInfo.IsEncrypted)
                throw new NotImplementedException($"模型({modelInfo.ModelInfo.ModelPath})未加密，当前版本不支持加载未加密模型！");

            IYolo8 model = null;
            string[] classNames = modelInfo.ClassNames == null ? new string[0] : modelInfo.ClassNames.ToArray();

            try
            {
                EnsureGpuRuntimeIfNeeded(modelInfo);

                if (modelInfo.ModelInfo.ModelType == ModelType.DET)
                {
                    model = new Yolo8Det();
                    model.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ModelInfo.DeviceType, classNames, modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                }
                else if (modelInfo.ModelInfo.ModelType == ModelType.OBB)
                {
                    model = new Yolo8Obb();
                    model.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ModelInfo.DeviceType, classNames, modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                }
                else if (modelInfo.ModelInfo.ModelType == ModelType.SEG)
                {
                    model = new Yolo8Seg();
                    model.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ModelInfo.DeviceType, classNames, modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS);
                }
                else if (modelInfo.ModelInfo.ModelType == ModelType.POSE)
                {
                    model = new Yolo8Pose();
                    model.Init(modelInfo.ModelInfo.ModelPath, modelInfo.ModelInfo.DeviceType, classNames, modelInfo.ModelSize, modelInfo.ScoreThreshold, modelInfo.ScoreNMS, modelInfo.ModelInfo.KeyPointNum);
                }
                else
                {
                    throw new Exception($"模型类型({modelInfo.ModelInfo.ModelType})不支持！");
                }

                return model;
            }
            catch
            {
                if (model != null)
                    model.Destroy();
                throw;
            }
        }

        /// <summary>
        /// 在GPU模型创建前初始化对应显卡的YOLO GPU DLL运行环境。
        /// </summary>
        /// <param name="modelInfo">AI模型配置信息。</param>
        private static void EnsureGpuRuntimeIfNeeded(AIInputInfo modelInfo)
        {
            if (modelInfo == null || modelInfo.ModelInfo.DeviceType != DeviceType.GPU)
                return;

            YoloGpuRuntimeBootstrapper.Initialize();
            string gpuTypeText = YoloGpuRuntimeBootstrapper.IsGtx750 ? "GTX 750" : "1050Ti及其它显卡";
            LogHelper.AddLog(MsgLevel.Info, $"GPU模型运行环境已初始化，显卡信息：{YoloGpuRuntimeBootstrapper.GpuSummary}，匹配环境：{gpuTypeText}，DLL目录：{YoloGpuRuntimeBootstrapper.RuntimeDirectory}", true);
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
        /// ROI 启用开关变化后刷新绘制链接提示。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="value">当前开关值。</param>
        private void uiSwitch_RoiEnable_ValueChanged(object sender, bool value)
        {
            RefreshRoiEditLinkText();
        }

        /// <summary>
        /// 打开 ROI 绘制窗口。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private async void linkLabelRoiEdit_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                using (Bitmap bitmap = await GetCurrentRoiBitmapAsync())
                using (ParamFormTDAIRoiEditor editor = new ParamFormTDAIRoiEditor(bitmap, _roiRegions, GetCurrentRoiBitmapAsync))
                {
                    if (editor.ShowDialog(this) == DialogResult.OK)
                    {
                        _roiRegions = CloneRoiRegions(editor.RoiRegions);
                        RefreshRoiEditLinkText();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("打开ROI绘制窗口失败，原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 刷新上游节点并获取当前可用于 ROI 绘制的图像。
        /// </summary>
        /// <returns>最新订阅图像对应的位图，调用方负责释放。</returns>
        private async Task<Bitmap> GetCurrentRoiBitmapAsync()
        {
            if (_node == null)
                throw new Exception("当前节点为空，无法绘制ROI。");
            if (string.IsNullOrWhiteSpace(nodeSubscription1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscription1.GetText2()))
                throw new Exception("请先选择输入图像。");

            await _node.Process.RunForUpdateImages(_node);
            OutputImage outputImage = GetOutputImage();
            Mat sourceImage = GetFirstValidImage(outputImage);
            if (sourceImage == null || sourceImage.Empty())
                throw new Exception("订阅的图像为空，无法绘制ROI。");

            return BitmapConverter.ToBitmap(sourceImage);
        }

        /// <summary>
        /// 刷新 ROI 绘制链接文本，显示当前启用状态和区域数量。
        /// </summary>
        private void RefreshRoiEditLinkText()
        {
            int count = _roiRegions == null ? 0 : _roiRegions.Count;
            string stateText = uiSwitch_RoiEnable.Active ? "已启用" : "未启用";
            linkLabelRoiEdit.Text = $"绘制检测区域({stateText}/{count})";
        }

        /// <summary>
        /// 从订阅图像中获取用于 ROI 绘制的第一张有效图像。
        /// </summary>
        /// <param name="outputImage">订阅图像输出。</param>
        /// <returns>有效图像；没有有效图像时返回 null。</returns>
        private static Mat GetFirstValidImage(OutputImage outputImage)
        {
            if (outputImage == null)
                return null;
            if (outputImage.Bitmaps != null && outputImage.Bitmaps.Count > 0 && OutputImage.HasValidImage(outputImage.Bitmaps[0]))
                return outputImage.Bitmaps[0];
            return OutputImage.HasValidImage(outputImage.SrcImg) ? outputImage.SrcImg : null;
        }

        /// <summary>
        /// 复制 ROI 参数列表，避免窗体和节点参数共用同一个集合实例。
        /// </summary>
        /// <param name="regions">原始 ROI 参数集合。</param>
        /// <returns>复制后的 ROI 参数集合。</returns>
        private static List<TDAIRoiRegion> CloneRoiRegions(IEnumerable<TDAIRoiRegion> regions)
        {
            return regions == null
                ? new List<TDAIRoiRegion>()
                : regions.Where(region => region != null).Select(region => region.Clone()).ToList();
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
                    if (TDAICommuntionParams == null)
                        TDAICommuntionParams = new BindingList<TDAICommuntionParam>();

                    NodeTDAI.EnsureCommunicationDetectItemConfigs(TDAICommuntionParams, comboBoxDetectConfig2.Text);
                    InitDetectItemList(comboBoxDetectConfig1);
                    InitDetectItemList(comboBoxDetectConfig2);

                    TDAICommuntionDetectionConfig tDAICommuntionDetectionConfig = new TDAICommuntionDetectionConfig(commountionDevice, TDAICommuntionParams, Adress, comboBoxDetectConfig2.Text);
                    tDAICommuntionDetectionConfig.ShowDialog();

                    TDAICommuntionParams = tDAICommuntionDetectionConfig.TDAICommuntionParamsDb;
                    Adress = tDAICommuntionDetectionConfig.Adress;
                    NodeTDAI.EnsureCommunicationDetectItemConfigs(TDAICommuntionParams, comboBoxDetectConfig2.Text);
                    InitDetectItemList(comboBoxDetectConfig1);
                    InitDetectItemList(comboBoxDetectConfig2);
                }
                else throw new Exception();
            }
            catch (Exception ex){
                MessageBoxTD.Show($"改通信设备异常请检查双方之间的通信. {ex}");
            }
        }
    }
}
