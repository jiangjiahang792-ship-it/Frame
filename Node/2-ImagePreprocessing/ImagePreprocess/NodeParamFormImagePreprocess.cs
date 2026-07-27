using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._2_ImagePreprocessing.ImagePreprocess
{
    /// <summary>
    /// 图像预处理参数窗体。
    /// </summary>
    public partial class NodeParamFormImagePreprocess : FormBase, INodeParamForm
    {
        /// <summary>
        /// 所属流程，用于刷新上游图像。
        /// </summary>
        private readonly Process process;

        /// <summary>
        /// 所属节点，用于刷新图像时确定运行终点。
        /// </summary>
        private readonly NodeBase node;

        /// <summary>
        /// 控件同步标记，避免反序列化赋值时触发参数联动。
        /// </summary>
        private bool isSyncingControls;

        /// <summary>
        /// 初始化图像预处理参数窗体。
        /// </summary>
        /// <param name="process">所属流程。</param>
        /// <param name="node">所属节点。</param>
        public NodeParamFormImagePreprocess(Process process, NodeBase node)
        {
            InitializeComponent();
            this.process = process;
            this.node = node;
            InitializeCombos();
            BindControlEvents();
            UpdateParameterGroupState();
        }

        /// <summary>
        /// 当前节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 初始化订阅控件所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
        }

        /// <summary>
        /// 将反序列化参数恢复到界面。
        /// </summary>
        public void SetParam2Form()
        {
            var param = Params as NodeParamImagePreprocess;
            if (param == null)
                return;

            isSyncingControls = true;
            try
            {
                nodeSubscriptionImage.SetText(param.Text1, param.Text2);
                SelectComboValue(comboBoxMode, param.Mode);
                SelectComboValue(comboBoxLawsKernel, param.LawsKernel);
                numericMaskWidth.Value = ClampDecimal(param.EmphasizeMaskWidth, numericMaskWidth);
                numericMaskHeight.Value = ClampDecimal(param.EmphasizeMaskHeight, numericMaskHeight);
                numericFactor.Value = ClampDecimal((decimal)param.EmphasizeFactor, numericFactor);
                numericEnergySize.Value = ClampDecimal(param.LawsEnergySize, numericEnergySize);
                numericMedianKernel.Value = ClampDecimal(param.MedianKernelSize, numericMedianKernel);
            }
            finally
            {
                isSyncingControls = false;
            }

            UpdateParameterGroupState();
        }

        /// <summary>
        /// 获取图像预处理模式显示名称。
        /// </summary>
        /// <param name="mode">预处理模式。</param>
        /// <returns>中文显示名称。</returns>
        public static string GetModeDisplayName(ImagePreprocessMode mode)
        {
            switch (mode)
            {
                case ImagePreprocessMode.Emphasize:
                    return "边缘增强";
                case ImagePreprocessMode.TextureLaws:
                    return "纹理滤波";
                case ImagePreprocessMode.Median:
                    return "中值滤波";
                default:
                    return mode.ToString();
            }
        }

        /// <summary>
        /// 获取 Laws 纹理核显示名称。
        /// </summary>
        /// <param name="kernel">纹理核。</param>
        /// <returns>中文显示名称。</returns>
        private static string GetLawsKernelDisplayName(LawsTextureKernel kernel)
        {
            switch (kernel)
            {
                case LawsTextureKernel.EdgeEnergy:
                    return "边缘能量";
                case LawsTextureKernel.L5E5:
                    return "L5E5竖向边缘";
                case LawsTextureKernel.E5L5:
                    return "E5L5横向边缘";
                case LawsTextureKernel.E5E5:
                    return "E5E5交叉边缘";
                case LawsTextureKernel.S5S5:
                    return "S5S5斑点纹理";
                case LawsTextureKernel.R5R5:
                    return "R5R5波纹纹理";
                default:
                    return kernel.ToString();
            }
        }

        /// <summary>
        /// 获取上游订阅的完整图像输出。
        /// </summary>
        /// <returns>上游图像输出。</returns>
        public OutputImage GetInputOutputImage()
        {
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            if (outputImage == null)
                throw new Exception("订阅的图像为null！");

            return outputImage;
        }

        /// <summary>
        /// 获取上游输出的第一张有效图像。
        /// </summary>
        /// <param name="outputImage">上游图像输出。</param>
        /// <returns>输入图像。</returns>
        public Mat GetInputMat(OutputImage outputImage)
        {
            if (outputImage == null ||
                outputImage.Bitmaps == null ||
                outputImage.Bitmaps.Count == 0 ||
                outputImage.Bitmaps[0] == null ||
                outputImage.Bitmaps[0].Empty())
            {
                throw new Exception("订阅的图像为null！");
            }

            return outputImage.Bitmaps[0];
        }

        /// <summary>
        /// 初始化算法下拉框。
        /// </summary>
        private void InitializeCombos()
        {
            comboBoxMode.Items.Add(new ComboItem<ImagePreprocessMode>("边缘增强 emphasize", ImagePreprocessMode.Emphasize));
            comboBoxMode.Items.Add(new ComboItem<ImagePreprocessMode>("纹理滤波 texture_laws", ImagePreprocessMode.TextureLaws));
            comboBoxMode.Items.Add(new ComboItem<ImagePreprocessMode>("中值滤波", ImagePreprocessMode.Median));
            comboBoxMode.SelectedIndex = 0;

            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("边缘能量", LawsTextureKernel.EdgeEnergy));
            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("L5E5竖向边缘", LawsTextureKernel.L5E5));
            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("E5L5横向边缘", LawsTextureKernel.E5L5));
            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("E5E5交叉边缘", LawsTextureKernel.E5E5));
            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("S5S5斑点纹理", LawsTextureKernel.S5S5));
            comboBoxLawsKernel.Items.Add(new ComboItem<LawsTextureKernel>("R5R5波纹纹理", LawsTextureKernel.R5R5));
            comboBoxLawsKernel.SelectedIndex = 0;
        }

        /// <summary>
        /// 绑定界面控件事件。
        /// </summary>
        private void BindControlEvents()
        {
            comboBoxMode.SelectedIndexChanged += comboBoxMode_SelectedIndexChanged;
        }

        /// <summary>
        /// 预处理模式变化时更新参数区可用状态。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void comboBoxMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isSyncingControls)
                return;

            UpdateParameterGroupState();
        }

        /// <summary>
        /// 刷新上游图像并预览当前预处理结果。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private async void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                await process.RunForUpdateImages(node);
                RefreshPreview();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"刷新图像失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 预览按钮事件。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void buttonPreview_Click(object sender, EventArgs e)
        {
            try
            {
                RefreshPreview();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"预览图像预处理失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 保存当前参数。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            Params = BuildParamFromForm();
            Hide();
        }

        /// <summary>
        /// 根据当前界面参数刷新预览图。
        /// </summary>
        private void RefreshPreview()
        {
            OutputImage outputImage = GetInputOutputImage();
            Mat source = GetInputMat(outputImage);
            NodeParamImagePreprocess param = BuildParamFromForm();
            using (Mat processed = ImagePreprocessAlgorithm.Execute(source, param))
            {
                SetPreviewImage(processed);
            }
        }

        /// <summary>
        /// 将 Mat 图像显示到预览控件。
        /// </summary>
        /// <param name="image">待显示图像。</param>
        private void SetPreviewImage(Mat image)
        {
            if (image == null || image.Empty())
                return;

            showImageControlPreview.SetImage(image.ToBitmap());
        }

        /// <summary>
        /// 从界面控件构建节点参数。
        /// </summary>
        /// <returns>节点参数。</returns>
        private NodeParamImagePreprocess BuildParamFromForm()
        {
            return new NodeParamImagePreprocess
            {
                Text1 = nodeSubscriptionImage.GetText1(),
                Text2 = nodeSubscriptionImage.GetText2(),
                Mode = GetComboValue(comboBoxMode, ImagePreprocessMode.Emphasize),
                EmphasizeMaskWidth = ToOdd((int)numericMaskWidth.Value),
                EmphasizeMaskHeight = ToOdd((int)numericMaskHeight.Value),
                EmphasizeFactor = (double)numericFactor.Value,
                LawsKernel = GetComboValue(comboBoxLawsKernel, LawsTextureKernel.EdgeEnergy),
                LawsEnergySize = ToOdd((int)numericEnergySize.Value),
                MedianKernelSize = ToOdd(Math.Max(3, (int)numericMedianKernel.Value))
            };
        }

        /// <summary>
        /// 按当前模式启用对应参数区域。
        /// </summary>
        private void UpdateParameterGroupState()
        {
            ImagePreprocessMode mode = GetComboValue(comboBoxMode, ImagePreprocessMode.Emphasize);
            groupBoxEmphasize.Enabled = mode == ImagePreprocessMode.Emphasize;
            groupBoxLaws.Enabled = mode == ImagePreprocessMode.TextureLaws;
            groupBoxMedian.Enabled = mode == ImagePreprocessMode.Median;
        }

        /// <summary>
        /// 选择指定下拉框值。
        /// </summary>
        /// <typeparam name="T">下拉项值类型。</typeparam>
        /// <param name="comboBox">下拉框。</param>
        /// <param name="value">待选择值。</param>
        private static void SelectComboValue<T>(ComboBox comboBox, T value)
        {
            for (int index = 0; index < comboBox.Items.Count; index++)
            {
                ComboItem<T> item = comboBox.Items[index] as ComboItem<T>;
                if (item != null && item.Value.Equals(value))
                {
                    comboBox.SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>
        /// 读取下拉框值。
        /// </summary>
        /// <typeparam name="T">下拉项值类型。</typeparam>
        /// <param name="comboBox">下拉框。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>下拉框当前值。</returns>
        private static T GetComboValue<T>(ComboBox comboBox, T defaultValue)
        {
            ComboItem<T> item = comboBox.SelectedItem as ComboItem<T>;
            return item == null ? defaultValue : item.Value;
        }

        /// <summary>
        /// 将数值限制到控件可选范围内。
        /// </summary>
        /// <param name="value">输入值。</param>
        /// <param name="control">数值控件。</param>
        /// <returns>限制后的数值。</returns>
        private static decimal ClampDecimal(decimal value, NumericUpDown control)
        {
            if (value < control.Minimum)
                return control.Minimum;
            if (value > control.Maximum)
                return control.Maximum;

            return value;
        }

        /// <summary>
        /// 将输入值调整为奇数。
        /// </summary>
        /// <param name="value">输入值。</param>
        /// <returns>奇数值。</returns>
        private static int ToOdd(int value)
        {
            return value % 2 == 0 ? value + 1 : value;
        }

        /// <summary>
        /// 下拉框显示项。
        /// </summary>
        /// <typeparam name="T">值类型。</typeparam>
        private sealed class ComboItem<T>
        {
            /// <summary>
            /// 创建下拉框显示项。
            /// </summary>
            /// <param name="text">显示文本。</param>
            /// <param name="value">实际值。</param>
            public ComboItem(string text, T value)
            {
                Text = text;
                Value = value;
            }

            /// <summary>
            /// 显示文本。
            /// </summary>
            private string Text { get; }

            /// <summary>
            /// 实际值。
            /// </summary>
            public T Value { get; }

            /// <summary>
            /// 返回显示文本。
            /// </summary>
            /// <returns>显示文本。</returns>
            public override string ToString()
            {
                return Text;
            }
        }
    }
}
