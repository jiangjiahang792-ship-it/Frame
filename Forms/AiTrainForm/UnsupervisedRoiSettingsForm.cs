using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.NewContourMatch;
using CvRect = OpenCvSharp.Rect;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督截取 ROI 与模板初匹配集中设置窗体。
    /// </summary>
    public partial class UnsupervisedRoiSettingsForm : Form
    {
        /// <summary>基准图片路径。</summary>
        private readonly string _imagePath;
        /// <summary>当前模板初匹配设置。</summary>
        private UnsupervisedTemplateMatchSettings _templateMatchSettings;
        /// <summary>窗体完成布局后恢复的图像坐标 ROI。</summary>
        private readonly CvRect? _initialRoi;

        /// <summary>初始化 ROI 设置窗体。</summary>
        public UnsupervisedRoiSettingsForm(string imagePath, CvRect? initialRoi, bool cropEnabled, UnsupervisedTemplateMatchSettings templateMatchSettings)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("基准图片路径不能为空。", nameof(imagePath));
            _imagePath = imagePath;
            _initialRoi = initialRoi;
            _templateMatchSettings = templateMatchSettings;
            InitializeComponent();
            imageRoiEditor.MaximumRoiCount = 1;
            LoadPreview();
            checkBoxEnableCrop.Checked = cropEnabled;
            checkBoxEnableTemplateMatch.Checked = cropEnabled && templateMatchSettings != null;
            UpdateControlState();
        }

        /// <summary>用户是否启用截取 ROI。</summary>
        public bool CropEnabled { get; private set; }
        /// <summary>用户确认的图像坐标 ROI。</summary>
        public CvRect? RoiRect { get; private set; }
        /// <summary>用户确认的模板初匹配设置。</summary>
        public UnsupervisedTemplateMatchSettings TemplateMatchSettings { get; private set; }

        /// <summary>加载预览图和已有 ROI。</summary>
        private void LoadPreview()
        {
            using (var source = new Bitmap(_imagePath))
            {
                imageRoiEditor.SetImage(new Bitmap(source));
            }
            imageRoiEditor.RoiChanged += imageRoiEditor_RoiChanged;
            UpdateRoiStatus();
        }

        /// <summary>首次显示并完成布局后恢复已有 ROI，避免按设计时控件尺寸换算坐标。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_initialRoi.HasValue) imageRoiEditor.SetImageRectangleRoi(_initialRoi.Value);
            UpdateRoiStatus();
        }

        /// <summary>截取 ROI 开关变化时同步模板匹配的可用状态。</summary>
        private void checkBoxEnableCrop_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBoxEnableCrop.Checked) checkBoxEnableTemplateMatch.Checked = false;
            UpdateControlState();
        }

        /// <summary>模板初匹配开关变化后刷新设置按钮。</summary>
        private void checkBoxEnableTemplateMatch_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlState();
        }

        /// <summary>进入矩形 ROI 绘制状态。</summary>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            if (imageRoiEditor.GetRoiCount() >= 1)
            {
                MessageBoxTD.Show(this, "当前只允许一个 ROI，请先清空已有 ROI。");
                return;
            }
            imageRoiEditor.BeginDrawRoi(comboBoxRoiShape.SelectedIndex == 1
                ? TDJS_Vision.Forms.ShapeDraw.ROIType.Circle
                : TDJS_Vision.Forms.ShapeDraw.ROIType.Rectangle);
        }

        /// <summary>清空当前 ROI。</summary>
        private void buttonClearRoi_Click(object sender, EventArgs e)
        {
            imageRoiEditor.ClearROI();
        }

        /// <summary>打开最新轮廓模板编辑器，并在基准图上验证默认匹配参数。</summary>
        private void buttonConfigureTemplate_Click(object sender, EventArgs e)
        {
            try
            {
                using (Mat image = Cv2.ImRead(_imagePath))
                using (var editor = new ContourTemplateEditor())
                {
                    if (image.Empty()) throw new InvalidDataException("基准图片无法读取。");
                    editor.Configure(_templateMatchSettings?.Parameters, image);
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    NodeParamNewContourMatch parameters = editor.Params;
                    if (parameters == null) throw new InvalidOperationException("请先创建并保存模板。");
                    parameters = UnsupervisedTemplateRoiMatcher.CreateRuntimeParameters(parameters);
                    _templateMatchSettings = UnsupervisedTemplateRoiMatcher.CreateSettings(image, parameters);
                    checkBoxEnableTemplateMatch.Checked = true;
                    UpdateControlState();
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(this, "设置模板初匹配失败：" + ex.Message);
            }
        }

        /// <summary>确认设置并返回主训练窗体。</summary>
        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (!checkBoxEnableCrop.Checked)
            {
                CropEnabled = false;
                RoiRect = null;
                TemplateMatchSettings = null;
                DialogResult = DialogResult.OK;
                return;
            }

            List<CvRect> rects = imageRoiEditor.GetImageROIRects();
            if (imageRoiEditor.GetRoiCount() != 1 || rects.Count == 0 || rects[0].Width <= 0 || rects[0].Height <= 0)
            {
                MessageBoxTD.Show(this, "启用截取 ROI 后必须绘制一个有效矩形区域。");
                return;
            }
            if (checkBoxEnableTemplateMatch.Checked && _templateMatchSettings == null)
            {
                MessageBoxTD.Show(this, "启用模板匹配后必须先创建模板。");
                return;
            }

            CropEnabled = true;
            RoiRect = rects[0];
            TemplateMatchSettings = checkBoxEnableTemplateMatch.Checked ? _templateMatchSettings : null;
            DialogResult = DialogResult.OK;
        }

        /// <summary>ROI 变化时更新坐标提示。</summary>
        private void imageRoiEditor_RoiChanged(object sender, EventArgs e)
        {
            UpdateRoiStatus();
        }

        /// <summary>刷新控件依赖关系和模板参数摘要。</summary>
        private void UpdateControlState()
        {
            bool cropEnabled = checkBoxEnableCrop.Checked;
            imageRoiEditor.Enabled = cropEnabled;
            buttonDrawRoi.Enabled = cropEnabled;
            buttonClearRoi.Enabled = cropEnabled;
            checkBoxEnableTemplateMatch.Enabled = cropEnabled;
            buttonConfigureTemplate.Enabled = cropEnabled && checkBoxEnableTemplateMatch.Checked;
            labelTemplateDefaults.Text = _templateMatchSettings == null
                ? "默认参数：数量1，角度-360°～360°，分数0.5；模板未设置"
                : "默认参数：数量1，角度-360°～360°，分数0.5；模板已设置";
        }

        /// <summary>刷新当前 ROI 坐标文字。</summary>
        private void UpdateRoiStatus()
        {
            if (imageRoiEditor.GetRoiCount() != 1)
            {
                labelRoiStatus.Text = "ROI：未绘制";
                return;
            }
            List<CvRect> rects = imageRoiEditor.GetImageROIRects();
            CvRect rect = rects[0];
            labelRoiStatus.Text = $"ROI：X={rect.X}，Y={rect.Y}，宽={rect.Width}，高={rect.Height}";
        }
    }
}
