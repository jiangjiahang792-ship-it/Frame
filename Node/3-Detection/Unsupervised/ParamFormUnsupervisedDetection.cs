using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using TDJS_Vision.Forms.AiTrainForm;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._6_LogicTool.SharedVariable;

namespace TDJS_Vision.Node._3_Detection.Unsupervised
{
    /// <summary>
    /// 无监督检测节点参数窗体。
    /// </summary>
    public partial class ParamFormUnsupervisedDetection : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 初始化无监督检测参数窗体。
        /// </summary>
        public ParamFormUnsupervisedDetection()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 设置当前参数窗体所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            _node = node;
            nodeSubscription1.SetInputContract(SubscriptionInputContract.ForCategories(new[]
            {
                SubscriptionDataCategory.Image,
                SubscriptionDataCategory.StructuredObject
            }));
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 将反序列化参数还原到窗体。
        /// </summary>
        public void SetParam2Form()
        {
            NodeParamUnsupervisedDetection param = Params as NodeParamUnsupervisedDetection;
            if (param == null)
                return;

            nodeSubscription1.SetText(param.Text1, param.Text2);
            textBoxTemplatePath.Text = param.TemplatePath;
            SetNumericValue(numericUpDownThreshold, (decimal)GetValidThreshold(param.Threshold));
            SetNumericValue(numericUpDownInferenceBatchSize, GetValidInferenceBatchSize(param.InferenceBatchSize));
            SetNumericValue(numericUpDownMiniArea, (decimal)GetValidMiniArea(param.MiniArea));
            textBoxMaxBoxes.Text = GetValidMaxBoxes(param.MaxBoxes).ToString(CultureInfo.InvariantCulture);
            RefreshTemplateInfo(param.InferenceBatchSize <= 0);
        }

        /// <summary>
        /// 获取订阅的输入图像。
        /// </summary>
        /// <returns>订阅图像。</returns>
        public OutputImage GetOutputImage()
        {
            try
            {
                if (nodeSubscription1.GetNodeType() == NodeType.SharedVariable)
                {
                    SharedVarValue sharedValue = nodeSubscription1.GetValue<SharedVarValue>();
                    return sharedValue == null ? null : sharedValue.Data as OutputImage;
                }

                return nodeSubscription1.GetValue<OutputImage>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 选择训练窗口生成的无监督模板。
        /// </summary>
        private void buttonSelectTemplate_Click(object sender, EventArgs e)
        {
            try
            {
                string modelRoot = UnsupervisedRuntimeBootstrapper.EnsureModelRoot();
                openFileDialogTemplate.InitialDirectory = modelRoot;
                if (openFileDialogTemplate.ShowDialog(this) != DialogResult.OK)
                    return;

                string templatePath = openFileDialogTemplate.FileName;
                EnsureTemplatePathInModelRoot(templatePath);
                textBoxTemplatePath.Text = templatePath;
                RefreshTemplateInfo(true);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("选择无监督模板失败，原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 刷新模板信息。
        /// </summary>
        private void buttonRefreshTemplate_Click(object sender, EventArgs e)
        {
            RefreshTemplateInfo();
        }

        /// <summary>
        /// 保存节点参数。
        /// </summary>
        private async void buttonSave_Click(object sender, EventArgs e)
        {
            NodeParamUnsupervisedDetection param;
            if (!TryBuildParams(out param))
                return;

            NodeUnsupervisedDetection unsupervisedNode = _node as NodeUnsupervisedDetection;
            if (unsupervisedNode == null)
            {
                MessageBoxTD.Show("参数设置异常，原因：当前节点不是无监督检测节点。");
                return;
            }

            SetPreloadUiState(true);
            try
            {
                await unsupervisedNode.PreloadRuntimeAsync(param);
                Hide();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("无监督模型加载失败，原模型未更改。原因：" + ex.Message);
            }
            finally
            {
                SetPreloadUiState(false);
            }
        }

        /// <summary>
        /// 保存当前界面参数到节点参数对象。
        /// </summary>
        /// <returns>保存成功返回 true。</returns>
        private bool TryBuildParams(out NodeParamUnsupervisedDetection param)
        {
            param = null;
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscription1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscription1.GetText2()))
                    throw new InvalidOperationException("请选择输入图像。");

                string templatePath = EnsureTemplatePathInModelRoot(textBoxTemplatePath.Text);
                int maxBoxes;
                if (!int.TryParse(textBoxMaxBoxes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxBoxes) || maxBoxes <= 0)
                    throw new InvalidOperationException("最大异常框必须为大于 0 的整数。");

                param = new NodeParamUnsupervisedDetection
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
                    TemplatePath = templatePath,
                    Threshold = (float)numericUpDownThreshold.Value,
                    InferenceBatchSize = (int)numericUpDownInferenceBatchSize.Value,
                    MiniArea = (float)numericUpDownMiniArea.Value,
                    MaxBoxes = maxBoxes
                };
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("参数设置异常，原因：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 切换模型预加载期间的参数窗体交互状态。
        /// </summary>
        /// <param name="isLoading">是否正在加载并预热模型。</param>
        private void SetPreloadUiState(bool isLoading)
        {
            UseWaitCursor = isLoading;
            buttonSave.Enabled = !isLoading;
            buttonSelectTemplate.Enabled = !isLoading;
            buttonRefreshTemplate.Enabled = !isLoading;
            buttonSave.Text = isLoading ? "模型加载中..." : "保存";
        }

        /// <summary>
        /// 读取并显示模板清单摘要。
        /// </summary>
        private void RefreshTemplateInfo()
        {
            RefreshTemplateInfo(false);
        }

        /// <summary>
        /// 读取并显示模板清单摘要。
        /// </summary>
        /// <param name="applyDefaultsToControls">是否把模板中的运行参数同步到界面控件。</param>
        private void RefreshTemplateInfo(bool applyDefaultsToControls)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(textBoxTemplatePath.Text))
                {
                    labelTemplateInfo.Text = "未选择模板";
                    return;
                }

                string templatePath = EnsureTemplatePathInModelRoot(textBoxTemplatePath.Text);
                UnsupervisedTemplateManifest manifest = UnsupervisedTemplatePackage.ReadManifest(templatePath);
                if (applyDefaultsToControls)
                    ApplyTemplateDefaultsToControls(manifest);

                labelTemplateInfo.Text = BuildTemplateInfo(manifest);
            }
            catch (Exception ex)
            {
                labelTemplateInfo.Text = "模板信息读取失败：" + ex.Message;
            }
        }

        /// <summary>
        /// 将模板中的默认运行参数填入界面，便于用户按训练参数直接推理。
        /// </summary>
        /// <param name="manifest">模板清单。</param>
        private void ApplyTemplateDefaultsToControls(UnsupervisedTemplateManifest manifest)
        {
            if (manifest == null)
                return;

            SetNumericValue(numericUpDownThreshold, (decimal)GetValidThreshold(manifest.Threshold));
            SetNumericValue(numericUpDownInferenceBatchSize, GetValidInferenceBatchSize(manifest.BatchSize));
            SetNumericValue(numericUpDownMiniArea, (decimal)GetValidMiniArea(manifest.MiniArea));
        }

        /// <summary>
        /// 生成模板信息显示文本。
        /// </summary>
        /// <param name="manifest">模板清单。</param>
        /// <returns>模板信息文本。</returns>
        private static string BuildTemplateInfo(UnsupervisedTemplateManifest manifest)
        {
            if (manifest == null)
                return "模板清单为空";

            string roiText = manifest.RoiEnabled
                ? string.Format(CultureInfo.InvariantCulture, "ROI=({0},{1},{2},{3})", manifest.RoiX, manifest.RoiY, manifest.RoiWidth, manifest.RoiHeight)
                : "整图推理";

            return string.Format(
                CultureInfo.InvariantCulture,
                "名称：{0}  算法：{1}  阈值：{2:G}  最小面积：{3:G}\r\n尺寸：{4}x{5}  设备：{6}  {7}",
                manifest.TemplateName,
                manifest.ModelType,
                manifest.Threshold,
                manifest.MiniArea,
                manifest.InputWidth,
                manifest.InputHeight,
                manifest.Device,
                roiText);
        }

        /// <summary>
        /// 将数值写入数值控件，并限制在控件允许范围内。
        /// </summary>
        /// <param name="control">目标数值控件。</param>
        /// <param name="value">待写入数值。</param>
        private static void SetNumericValue(NumericUpDown control, decimal value)
        {
            if (control == null)
                return;

            if (value < control.Minimum)
                value = control.Minimum;
            if (value > control.Maximum)
                value = control.Maximum;

            control.Value = value;
        }

        /// <summary>
        /// 获取有效异常阈值。
        /// </summary>
        /// <param name="value">原始阈值。</param>
        /// <returns>有效阈值。</returns>
        private static float GetValidThreshold(float value)
        {
            if (value < 0F || value > 1F)
                return NodeParamUnsupervisedDetection.DefaultThreshold;

            return value;
        }

        /// <summary>
        /// 获取有效推理批次。
        /// </summary>
        /// <param name="value">原始批次。</param>
        /// <returns>有效批次。</returns>
        private static int GetValidInferenceBatchSize(int value)
        {
            return value <= 0 ? NodeParamUnsupervisedDetection.DefaultInferenceBatchSize : value;
        }

        /// <summary>
        /// 获取有效最小缺陷面积。
        /// </summary>
        /// <param name="value">原始面积。</param>
        /// <returns>有效面积。</returns>
        private static float GetValidMiniArea(float value)
        {
            return value < 0F ? NodeParamUnsupervisedDetection.DefaultMiniArea : value;
        }

        /// <summary>
        /// 获取有效最大异常框数量。
        /// </summary>
        /// <param name="value">原始数量。</param>
        /// <returns>有效数量。</returns>
        private static int GetValidMaxBoxes(int value)
        {
            return value <= 0 ? NodeParamUnsupervisedDetection.DefaultMaxBoxes : value;
        }

        /// <summary>
        /// 确保模板来自运行目录 Model 文件夹。
        /// </summary>
        /// <param name="templatePath">模板路径。</param>
        /// <returns>模板完整路径。</returns>
        private static string EnsureTemplatePathInModelRoot(string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath))
                throw new InvalidOperationException("请选择无监督模板文件。");

            string fullTemplatePath = Path.GetFullPath(templatePath);
            if (!File.Exists(fullTemplatePath))
                throw new FileNotFoundException("未找到无监督模板文件。", fullTemplatePath);

            string modelRoot = EnsureTrailingSeparator(Path.GetFullPath(UnsupervisedRuntimeBootstrapper.GetModelRoot()));
            if (!fullTemplatePath.StartsWith(modelRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只能选择训练窗口生成到 Model 文件夹中的无监督模板。");

            return fullTemplatePath;
        }

        /// <summary>
        /// 确保目录路径以分隔符结尾。
        /// </summary>
        /// <param name="path">目录路径。</param>
        /// <returns>带尾部分隔符的目录路径。</returns>
        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
