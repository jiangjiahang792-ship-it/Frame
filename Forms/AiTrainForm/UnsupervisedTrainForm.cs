using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using CvRect = OpenCvSharp.Rect;
using Logger;

namespace TDJS_Vision.Forms.AiTrainForm
{
    /// <summary>
    /// 无监督训练窗体，负责图片检查、单 ROI 设置、训练参数配置和模板生成。
    /// </summary>
    public partial class UnsupervisedTrainForm : Form
    {
        /// <summary>
        /// 已加载的训练图片。
        /// </summary>
        private readonly List<UnsupervisedImageItem> _images = new List<UnsupervisedImageItem>();

        /// <summary>
        /// 当前已渲染的缩略图卡片索引，用于局部刷新选中边框。
        /// </summary>
        private readonly Dictionary<UnsupervisedImageItem, Control> _imageCards = new Dictionary<UnsupervisedImageItem, Control>();

        /// <summary>
        /// 本次软件运行期间最近一次加载成功的图像路径，用于关闭再打开训练窗体时恢复现场。
        /// </summary>
        private static string _lastImageFolder;

        /// <summary>
        /// 无监督训练服务。
        /// </summary>
        private readonly UnsupervisedTrainingService _trainingService = new UnsupervisedTrainingService();

        /// <summary>
        /// 检查页主背景色。
        /// </summary>
        private static readonly Color InspectionBackColor = Color.FromArgb(48, 48, 48);

        /// <summary>
        /// 缩略图网格背景色。
        /// </summary>
        private static readonly Color InspectionGridBackColor = Color.FromArgb(42, 42, 42);

        /// <summary>
        /// 缩略图卡片背景色。
        /// </summary>
        private static readonly Color InspectionCardBackColor = Color.FromArgb(58, 58, 58);

        /// <summary>
        /// 检查页强调色。
        /// </summary>
        private static readonly Color InspectionAccentColor = Color.FromArgb(0, 188, 188);

        /// <summary>
        /// 缩略图选中边框颜色，按用户习惯使用明确蓝色。
        /// </summary>
        private static readonly Color InspectionSelectedBorderColor = Color.FromArgb(0, 122, 204);

        /// <summary>
        /// 顶部导航栏背景色。
        /// </summary>
        private static readonly Color NavigationBackColor = Color.FromArgb(54, 54, 54);

        /// <summary>
        /// 输入框暗色背景色。
        /// </summary>
        private static readonly Color DarkInputBackColor = Color.FromArgb(38, 38, 38);

        /// <summary>
        /// 分类按钮字体，复用静态实例降低频繁刷新时的 GDI 对象压力。
        /// </summary>
        private static readonly Font FilterButtonFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point, 134);

        /// <summary>
        /// 操作按钮字体。
        /// </summary>
        private static readonly Font ActionButtonFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 134);

        /// <summary>
        /// 自适应文本字体缓存，避免每次刷新按钮状态都创建新的 GDI 字体对象。
        /// </summary>
        private static readonly Dictionary<string, Font> AutoFitFontCache = new Dictionary<string, Font>();

        /// <summary>
        /// 当前分类筛选。
        /// </summary>
        private UnsupervisedImageCategory _currentFilter = UnsupervisedImageCategory.All;

        /// <summary>
        /// 当前选中的图片。
        /// </summary>
        private UnsupervisedImageItem _selectedImage;

        /// <summary>
        /// 当前预览位图。
        /// </summary>
        private Bitmap _previewBitmap;

        /// <summary>
        /// 图片加载取消源。
        /// </summary>
        private CancellationTokenSource _loadCancellation;

        /// <summary>
        /// 训练取消源。
        /// </summary>
        private CancellationTokenSource _trainingCancellation;

        /// <summary>
        /// 输出路径是否由用户手动选择。
        /// </summary>
        private bool _outputPathUserSelected;

        /// <summary>
        /// 是否正在修正最大化边界，避免 Resize 递归。
        /// </summary>
        private bool _applyingMaximizedBounds;

        /// <summary>
        /// 运行期图片资源是否已经释放，避免关闭和 Dispose 重复释放。
        /// </summary>
        private bool _runtimeResourcesReleased;

        /// <summary>
        /// 初始化无监督训练窗体。
        /// </summary>
        public UnsupervisedTrainForm()
        {
            InitializeComponent();
            InitializeFormState();
        }

        /// <summary>
        /// 初始化窗体默认值和事件。
        /// </summary>
        private void InitializeFormState()
        {
            ApplyRuntimeTabHeaderMode();
            EnableDoubleBufferedScrolling(flowLayoutPanelImages);
            ApplyInspectionTheme();
            comboBoxModelType.SelectedIndex = 0;
            comboBoxDevice.SelectedIndex = 0;
            buttonStopTraining.Enabled = false;
            imageROIEditControlPreview.RoiChanged += imageROIEditControlPreview_RoiChanged;
            textBoxTemplateName.Text = "无监督模板_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            RestoreLastImageFolder();
            UpdateGeneratedModelPath();
            RefreshRuntimeStatus();
            UpdateImageCountText();
            UpdateSelectionActionState();
            UpdateRoiStatus();
        }

        /// <summary>
        /// 运行时收起原生页签头；设计器中保留页签，方便直接切换和设计训练页。
        /// </summary>
        private void ApplyRuntimeTabHeaderMode()
        {
            if (IsDesignerHosted())
                return;

            tabControlMain.ItemSize = new Size(1, 1);
            tabControlMain.Padding = Point.Empty;
            tabControlMain.SizeMode = TabSizeMode.Fixed;
            tabControlMain.TabStop = false;
        }

        /// <summary>
        /// 判断当前窗体是否运行在 WinForms 设计器中。
        /// </summary>
        /// <returns>在设计器中返回 true。</returns>
        private bool IsDesignerHosted()
        {
            return DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }

        /// <summary>
        /// 窗体首次显示后修正最大化边界，避免底部露出主界面。
        /// </summary>
        private async void UnsupervisedTrainForm_Shown(object sender, EventArgs e)
        {
            EnsureMaximizedBounds();
            await TryAutoReloadLastImageFolderAsync();
        }

        /// <summary>
        /// 从最小化恢复到最大化后再次修正边界。
        /// </summary>
        private void UnsupervisedTrainForm_Resize(object sender, EventArgs e)
        {
            RefreshAdaptiveText();

            if (WindowState != FormWindowState.Maximized || IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(EnsureMaximizedBounds));
        }

        /// <summary>
        /// 切换到检查页。
        /// </summary>
        private void buttonNavCheck_Click(object sender, EventArgs e)
        {
            SelectNavigationTab(0);
        }

        /// <summary>
        /// 切换到训练页。
        /// </summary>
        private void buttonNavTraining_Click(object sender, EventArgs e)
        {
            SelectNavigationTab(1);
        }

        /// <summary>
        /// TabControl 选中项变化时同步顶部导航按钮状态。
        /// </summary>
        private void tabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateNavigationButtonState();
        }

        /// <summary>
        /// 使用自定义导航按钮切换隐藏页签。
        /// </summary>
        /// <param name="tabIndex">目标页签索引。</param>
        private void SelectNavigationTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabControlMain.TabPages.Count)
                return;

            tabControlMain.SelectedIndex = tabIndex;
            UpdateNavigationButtonState();
        }

        /// <summary>
        /// 选择图片路径。
        /// </summary>
        private async void buttonSelectImageFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(textBoxImageFolder.Text))
                {
                    dialog.SelectedPath = textBoxImageFolder.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    textBoxImageFolder.Text = dialog.SelectedPath;
                    await LoadImagesAsync(dialog.SelectedPath);
                }
            }
        }

        /// <summary>
        /// 重新加载图片。
        /// </summary>
        private async void buttonReloadImages_Click(object sender, EventArgs e)
        {
            await LoadImagesAsync(textBoxImageFolder.Text);
        }

        /// <summary>
        /// 切换到全部图片。
        /// </summary>
        private void buttonFilterAll_Click(object sender, EventArgs e)
        {
            SetFilter(UnsupervisedImageCategory.All);
        }

        /// <summary>
        /// 切换到 OK 图片。
        /// </summary>
        private void buttonFilterOk_Click(object sender, EventArgs e)
        {
            SetFilter(UnsupervisedImageCategory.OK);
        }

        /// <summary>
        /// 切换到 NG 图片。
        /// </summary>
        private void buttonFilterNg_Click(object sender, EventArgs e)
        {
            SetFilter(UnsupervisedImageCategory.NG);
        }

        /// <summary>
        /// 将当前选中图片标记为 OK。
        /// </summary>
        private void buttonSetSelectedOk_Click(object sender, EventArgs e)
        {
            SetSelectedImageCategory(UnsupervisedImageCategory.OK);
        }

        /// <summary>
        /// 将当前选中图片标记为 NG。
        /// </summary>
        private void buttonSetSelectedNg_Click(object sender, EventArgs e)
        {
            SetSelectedImageCategory(UnsupervisedImageCategory.NG);
        }

        /// <summary>
        /// 进入 ROI 绘制状态。
        /// </summary>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            if (_selectedImage == null)
            {
                MessageBoxTD.Show(this, "请先选择一张预览图像。");
                return;
            }

            if (imageROIEditControlPreview.GetRoiCount() >= 1)
            {
                MessageBoxTD.Show(this, "当前只允许一个 ROI，请先清空已有 ROI。");
                return;
            }

            imageROIEditControlPreview.BeginDrawRectangleRoi();
            AppendTrainingLog("已进入 ROI 绘制状态。");
        }

        /// <summary>
        /// 清空 ROI。
        /// </summary>
        private void buttonClearRoi_Click(object sender, EventArgs e)
        {
            imageROIEditControlPreview.ClearROI();
            UpdateRoiStatus();
        }

        /// <summary>
        /// 模板名称变更后自动刷新输出路径。
        /// </summary>
        private void textBoxTemplateName_TextChanged(object sender, EventArgs e)
        {
            if (!_outputPathUserSelected)
            {
                UpdateGeneratedModelPath();
            }
        }

        /// <summary>
        /// 手动选择模板输出路径。
        /// </summary>
        private void buttonSelectModelOutput_Click(object sender, EventArgs e)
        {
            string modelRoot = UnsupervisedRuntimeBootstrapper.EnsureModelRoot();
            using (var dialog = new SaveFileDialog())
            {
                dialog.InitialDirectory = modelRoot;
                dialog.Filter = "无监督模板|*.tdunsup";
                dialog.FileName = Path.GetFileName(textBoxModelOutputPath.Text);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                if (!IsPathUnderRoot(dialog.FileName, modelRoot))
                {
                    MessageBoxTD.Show(this, "模板文件必须保存到运行目录 Model 文件夹内。");
                    return;
                }

                _outputPathUserSelected = true;
                textBoxModelOutputPath.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// 开始训练。
        /// </summary>
        private async void buttonStartTraining_Click(object sender, EventArgs e)
        {
            try
            {
                UnsupervisedTrainingRequest request = BuildTrainingRequest();
                _trainingCancellation = new CancellationTokenSource();
                SetTrainingBusy(true);
                progressBarTraining.Value = 0;
                textBoxTrainingLog.Clear();

                var progress = new Progress<int>(value => progressBarTraining.Value = Math.Max(0, Math.Min(100, value)));
                var log = new Progress<string>(AppendTrainingLog);

                UnsupervisedTrainingResult result = await Task.Run(() =>
                    _trainingService.Train(request, progress, log, _trainingCancellation.Token));

                AppendTrainingLog("训练完成，模板已生成：" + result.TemplatePath);
                MessageBoxTD.Show(this, "无监督模板生成完成：" + result.TemplatePath);
            }
            catch (OperationCanceledException)
            {
                AppendTrainingLog("训练已取消。");
            }
            catch (Exception ex)
            {
                AppendTrainingLog("训练失败：" + ex.Message);
                LogHelper.AddLog(MsgLevel.Exception, "无监督训练失败：" + ex.Message, true);
                MessageBoxTD.Show(this, "训练失败：" + ex.Message);
            }
            finally
            {
                SetTrainingBusy(false);
                _trainingCancellation?.Dispose();
                _trainingCancellation = null;
                RefreshRuntimeStatus();
            }
        }

        /// <summary>
        /// 停止训练。
        /// </summary>
        private void buttonStopTraining_Click(object sender, EventArgs e)
        {
            _trainingCancellation?.Cancel();
            _trainingService.CancelActiveTraining();
            AppendTrainingLog("正在请求停止训练。");
        }

        /// <summary>
        /// ROI 变化时刷新状态文字。
        /// </summary>
        private void imageROIEditControlPreview_RoiChanged(object sender, EventArgs e)
        {
            UpdateRoiStatus();
        }

        /// <summary>
        /// 异步加载图片。
        /// </summary>
        /// <param name="folder">图片目录。</param>
        private async Task LoadImagesAsync(string folder)
        {
            _runtimeResourcesReleased = false;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBoxTD.Show(this, "请选择有效的图像路径。");
                return;
            }

            RememberLastImageFolder(folder);
            textBoxImageFolder.Text = folder;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            CancellationToken token = _loadCancellation.Token;

            SetImageLoadingBusy(true);
            DisposeImageItems();
            ClearImageCards();
            _selectedImage = null;
            SetPreviewImage(null);
            UpdateImageCountText();
            UpdateSelectionActionState();
            UpdateRoiStatus();

            try
            {
                Size thumbnailSize = new Size(132, 86);
                List<UnsupervisedImageItem> loaded = await Task.Run(() => UnsupervisedImageLoader.LoadImages(folder, thumbnailSize, token), token);
                token.ThrowIfCancellationRequested();

                _images.AddRange(loaded);
                RenderImageCards();
                UpdateImageCountText();
                if (_images.Count > 0)
                {
                    SelectImage(_images[0]);
                }
                AppendTrainingLog("已加载图像：" + _images.Count.ToString(CultureInfo.InvariantCulture));
            }
            catch (OperationCanceledException)
            {
                AppendTrainingLog("图像加载已取消。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(this, "加载图像失败：" + ex.Message);
                LogHelper.AddLog(MsgLevel.Exception, "加载无监督训练图像失败：" + ex.Message, true);
            }
            finally
            {
                SetImageLoadingBusy(false);
            }
        }

        /// <summary>
        /// 设置分类筛选。
        /// </summary>
        /// <param name="category">目标分类。</param>
        private void SetFilter(UnsupervisedImageCategory category)
        {
            _currentFilter = category;
            RenderImageCards();
        }

        /// <summary>
        /// 渲染缩略图卡片。
        /// </summary>
        private void RenderImageCards()
        {
            flowLayoutPanelImages.SuspendLayout();
            ClearImageCards();
            try
            {
                List<Control> cards = GetFilteredImages()
                    .Select(CreateImageCard)
                    .ToList();
                if (cards.Count > 0)
                    flowLayoutPanelImages.Controls.AddRange(cards.ToArray());
            }
            finally
            {
                flowLayoutPanelImages.ResumeLayout(true);
                flowLayoutPanelImages.PerformLayout();
            }

            HighlightFilterButton();
        }

        /// <summary>
        /// 记录最近一次成功使用的图像路径。
        /// </summary>
        /// <param name="folder">图像文件夹。</param>
        private static void RememberLastImageFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            _lastImageFolder = folder;
        }

        /// <summary>
        /// 恢复本次软件运行期间最近一次加载的图像路径。
        /// </summary>
        private void RestoreLastImageFolder()
        {
            if (string.IsNullOrWhiteSpace(_lastImageFolder) || !Directory.Exists(_lastImageFolder))
                return;

            textBoxImageFolder.Text = _lastImageFolder;
        }

        /// <summary>
        /// 窗体重新打开时自动加载上次路径，避免用户重复选择图像目录。
        /// </summary>
        private async Task TryAutoReloadLastImageFolderAsync()
        {
            string folder = textBoxImageFolder.Text;
            if (_images.Count > 0 || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            await LoadImagesAsync(folder);
        }

        /// <summary>
        /// 创建单张缩略图卡片。
        /// </summary>
        /// <param name="item">图片信息。</param>
        /// <returns>缩略图控件。</returns>
        private Control CreateImageCard(UnsupervisedImageItem item)
        {
            var card = new UnsupervisedImageCardControl(item)
            {
                Width = 172,
                Height = 172,
                Margin = new Padding(4),
                BackColor = InspectionCardBackColor,
                Tag = item,
                Cursor = Cursors.Hand,
                Selected = ReferenceEquals(item, _selectedImage)
            };
            card.Click += imageCard_Click;
            _imageCards[item] = card;
            return card;
        }

        /// <summary>
        /// 绘制 OK/NG 缩略图边框。
        /// </summary>
        private void imageCard_Paint(object sender, PaintEventArgs e)
        {
            if (!(sender is Control control) || !(control.Tag is UnsupervisedImageItem item)) return;

            Color color = item.Category == UnsupervisedImageCategory.NG ? Color.FromArgb(230, 40, 40) : Color.FromArgb(0, 190, 100);
            if (ReferenceEquals(item, _selectedImage))
            {
                color = InspectionSelectedBorderColor;
            }

            using (var pen = new Pen(color, 3))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawRectangle(pen, 1, 1, control.Width - 3, control.Height - 3);
            }
        }

        /// <summary>
        /// 缩略图点击后切换预览图。
        /// </summary>
        private void imageCard_Click(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control?.Tag is UnsupervisedImageItem item)
            {
                SelectImage(item);
            }
        }

        /// <summary>
        /// 选择预览图。
        /// </summary>
        /// <param name="item">图片信息。</param>
        private void SelectImage(UnsupervisedImageItem item)
        {
            UnsupervisedImageItem previous = _selectedImage;
            _selectedImage = item;
            try
            {
                SetPreviewImage(LoadBitmapWithoutLock(item.FilePath));
                labelPreviewTitle.Text = "图像预览 / ROI编辑：" + item.DisplayName;
                FitLabelText(labelPreviewTitle, 10F, 7F, FontStyle.Bold);
                InvalidateImageCard(previous);
                InvalidateImageCard(item);
                UpdateSelectionActionState();
                UpdateRoiStatus();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(this, "预览图像失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 修改当前选中图片的训练分类。
        /// </summary>
        /// <param name="category">目标分类。</param>
        private void SetSelectedImageCategory(UnsupervisedImageCategory category)
        {
            if (_selectedImage == null)
            {
                MessageBoxTD.Show(this, "请先选择一张图片。");
                return;
            }

            _selectedImage.Category = category;
            UpdateImageCountText();
            RefreshSelectedImageAfterCategoryChange();
        }

        /// <summary>
        /// 分类变化后刷新缩略图并保持合理的选中状态。
        /// </summary>
        private void RefreshSelectedImageAfterCategoryChange()
        {
            UnsupervisedImageItem selected = _selectedImage;
            RenderImageCards();
            if (selected != null && IsImageVisibleByCurrentFilter(selected))
            {
                InvalidateImageCard(selected);
                return;
            }

            UnsupervisedImageItem fallback = GetFilteredImages().FirstOrDefault();
            if (fallback != null)
            {
                SelectImage(fallback);
                return;
            }

            ClearSelection();
        }

        /// <summary>
        /// 构建训练请求。
        /// </summary>
        /// <returns>训练请求。</returns>
        private UnsupervisedTrainingRequest BuildTrainingRequest()
        {
            if (_images.Count == 0)
                throw new InvalidOperationException("请先加载训练图片。");

            string templateName = textBoxTemplateName.Text.Trim();
            if (string.IsNullOrWhiteSpace(templateName))
                throw new InvalidOperationException("模板名称不能为空。");

            if (string.IsNullOrWhiteSpace(textBoxModelOutputPath.Text))
                UpdateGeneratedModelPath();

            string modelRoot = UnsupervisedRuntimeBootstrapper.EnsureModelRoot();
            if (!IsPathUnderRoot(textBoxModelOutputPath.Text, modelRoot))
                throw new InvalidOperationException("模板文件必须保存到运行目录 Model 文件夹。");

            CvRect? roiRect = GetTrainingRoiRectOrNull();
            FullImageTrainingWhenNoRoi(roiRect);

            return new UnsupervisedTrainingRequest
            {
                TemplateName = templateName,
                SourceFolder = textBoxImageFolder.Text,
                OutputTemplatePath = textBoxModelOutputPath.Text,
                Images = _images.ToList(),
                ModelType = comboBoxModelType.Text,
                Threshold = (float)numericUpDownThreshold.Value,
                MiniArea = (float)numericUpDownMiniArea.Value,
                InputWidth = (int)numericUpDownInputWidth.Value,
                InputHeight = (int)numericUpDownInputHeight.Value,
                MaxEpochs = (int)numericUpDownEpochs.Value,
                BatchSize = (int)numericUpDownBatchSize.Value,
                Device = comboBoxDevice.Text,
                RoiRect = roiRect
            };
        }

        /// <summary>
        /// 获取训练 ROI；没有 ROI 时返回空并按整图训练。
        /// </summary>
        /// <returns>图像坐标系 ROI，空表示整图。</returns>
        private CvRect? GetTrainingRoiRectOrNull()
        {
            int roiCount = imageROIEditControlPreview.GetRoiCount();
            if (roiCount == 0)
                return null;
            if (roiCount > 1)
                throw new InvalidOperationException("当前只支持一个 ROI，请删除多余 ROI。");
            if (_selectedImage == null)
                throw new InvalidOperationException("请先选择一张用于读取 ROI 坐标的图像。");

            using (Bitmap bitmap = LoadBitmapWithoutLock(_selectedImage.FilePath))
            {
                List<CvRect> rects = imageROIEditControlPreview.GetImageROIRects(bitmap.Width, bitmap.Height);
                if (rects.Count == 0)
                    return null;

                CvRect rect = ClampRect(rects[0], bitmap.Width, bitmap.Height);
                if (rect.Width <= 0 || rect.Height <= 0)
                    throw new InvalidOperationException("ROI 区域无效，请重新绘制。");

                return rect;
            }
        }

        /// <summary>
        /// 无 ROI 时使用整图训练的显式标记方法。
        /// </summary>
        /// <param name="roiRect">ROI 矩形。</param>
        /// <returns>没有 ROI 返回 true。</returns>
        private static bool FullImageTrainingWhenNoRoi(CvRect? roiRect)
        {
            return !roiRect.HasValue;
        }

        /// <summary>
        /// 刷新无监督运行环境状态。
        /// </summary>
        private void RefreshRuntimeStatus()
        {
            UnsupervisedRuntimeStatus status = UnsupervisedRuntimeBootstrapper.ValidateRuntime();
            labelRuntimeStatus.Text = status.Message;
            labelRuntimeStatus.ForeColor = status.IsReady ? Color.SeaGreen : Color.IndianRed;
        }

        /// <summary>
        /// 更新输出模板路径。
        /// </summary>
        private void UpdateGeneratedModelPath()
        {
            string modelRoot = UnsupervisedRuntimeBootstrapper.EnsureModelRoot();
            string name = SafeFileName(textBoxTemplateName.Text);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "无监督模板";
            }
            textBoxModelOutputPath.Text = Path.Combine(modelRoot, name + ".tdunsup");
        }

        /// <summary>
        /// 更新图片数量状态。
        /// </summary>
        private void UpdateImageCountText()
        {
            int okCount = _images.Count(item => item.Category == UnsupervisedImageCategory.OK);
            int ngCount = _images.Count(item => item.Category == UnsupervisedImageCategory.NG);
            labelImageCount.Text = string.Format(CultureInfo.InvariantCulture, "图像总数：{0}    OK：{1}    NG：{2}", _images.Count, okCount, ngCount);
            buttonFilterAll.Text = "All " + _images.Count.ToString(CultureInfo.InvariantCulture);
            buttonFilterOk.Text = "OK " + okCount.ToString(CultureInfo.InvariantCulture);
            buttonFilterNg.Text = "NG " + ngCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 根据当前筛选条件返回需要显示的图片集合。
        /// </summary>
        /// <returns>筛选后的图片集合。</returns>
        private IEnumerable<UnsupervisedImageItem> GetFilteredImages()
        {
            return _currentFilter == UnsupervisedImageCategory.All
                ? _images
                : _images.Where(IsImageVisibleByCurrentFilter);
        }

        /// <summary>
        /// 判断图片是否属于当前筛选范围。
        /// </summary>
        /// <param name="item">图片信息。</param>
        /// <returns>需要显示时返回 true。</returns>
        private bool IsImageVisibleByCurrentFilter(UnsupervisedImageItem item)
        {
            return item != null && (_currentFilter == UnsupervisedImageCategory.All || item.Category == _currentFilter);
        }

        /// <summary>
        /// 更新 ROI 状态。
        /// </summary>
        private void UpdateRoiStatus()
        {
            try
            {
                CvRect? rect = GetTrainingRoiPreviewRectOrNull();
                if (!rect.HasValue)
                {
                    labelRoiStatus.Text = "ROI：未使用，训练时按整图";
                    return;
                }

                CvRect value = rect.Value;
                labelRoiStatus.Text = string.Format(CultureInfo.InvariantCulture, "ROI：X={0}, Y={1}, W={2}, H={3}", value.X, value.Y, value.Width, value.Height);
            }
            catch (Exception ex)
            {
                labelRoiStatus.Text = "ROI：" + ex.Message;
            }
        }

        /// <summary>
        /// 获取状态栏显示用 ROI。
        /// </summary>
        /// <returns>ROI 矩形。</returns>
        private CvRect? GetTrainingRoiPreviewRectOrNull()
        {
            if (_previewBitmap == null || imageROIEditControlPreview.GetRoiCount() == 0)
                return null;
            if (imageROIEditControlPreview.GetRoiCount() > 1)
                throw new InvalidOperationException("超过一个 ROI");

            List<CvRect> rects = imageROIEditControlPreview.GetImageROIRects(_previewBitmap.Width, _previewBitmap.Height);
            if (rects.Count == 0)
                return null;
            return ClampRect(rects[0], _previewBitmap.Width, _previewBitmap.Height);
        }

        /// <summary>
        /// 设置图片加载忙碌状态。
        /// </summary>
        /// <param name="busy">是否忙碌。</param>
        private void SetImageLoadingBusy(bool busy)
        {
            buttonSelectImageFolder.Enabled = !busy;
            buttonReloadImages.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            UpdateSelectionActionState();
        }

        /// <summary>
        /// 设置训练忙碌状态。
        /// </summary>
        /// <param name="busy">是否忙碌。</param>
        private void SetTrainingBusy(bool busy)
        {
            buttonStartTraining.Enabled = !busy;
            buttonStopTraining.Enabled = busy;
            groupBoxTrainingParams.Enabled = !busy;
            buttonSelectModelOutput.Enabled = !busy;
            buttonSelectImageFolder.Enabled = !busy;
            buttonReloadImages.Enabled = !busy;
            UpdateSelectionActionState();
        }

        /// <summary>
        /// 高亮当前分类按钮。
        /// </summary>
        private void HighlightFilterButton()
        {
            ApplyFilterButtonStyle(buttonFilterAll, _currentFilter == UnsupervisedImageCategory.All);
            ApplyFilterButtonStyle(buttonFilterOk, _currentFilter == UnsupervisedImageCategory.OK);
            ApplyFilterButtonStyle(buttonFilterNg, _currentFilter == UnsupervisedImageCategory.NG);
        }

        /// <summary>
        /// 设置分类按钮样式。
        /// </summary>
        /// <param name="button">分类按钮。</param>
        /// <param name="selected">是否选中。</param>
        private static void ApplyFilterButtonStyle(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = selected ? 1 : 0;
            button.FlatAppearance.BorderColor = InspectionAccentColor;
            button.BackColor = selected ? InspectionAccentColor : Color.FromArgb(82, 82, 82);
            button.ForeColor = Color.White;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            FitButtonText(button, FilterButtonFont.SizeInPoints, 8F, FontStyle.Bold);
        }

        /// <summary>
        /// 应用类似标注工具的深色检查工作区主题。
        /// </summary>
        private void ApplyInspectionTheme()
        {
            BackColor = InspectionBackColor;
            tableLayoutPanelMainRoot.BackColor = InspectionBackColor;
            panelMainNavigation.BackColor = InspectionBackColor;
            tabControlMain.BackColor = NavigationBackColor;
            tabControlMain.ForeColor = Color.WhiteSmoke;
            tabPageCheck.BackColor = InspectionBackColor;
            tabPageCheck.UseVisualStyleBackColor = false;
            tableLayoutPanelCheckRoot.BackColor = InspectionBackColor;
            tableLayoutPanelCheckBody.BackColor = InspectionBackColor;
            panelCheckToolbar.BackColor = InspectionBackColor;
            panelCategory.BackColor = Color.FromArgb(52, 52, 52);
            flowLayoutPanelImages.BackColor = InspectionGridBackColor;
            flowLayoutPanelImages.BorderStyle = BorderStyle.None;
            tableLayoutPanelPreview.BackColor = InspectionGridBackColor;
            panelRoiToolbar.BackColor = InspectionGridBackColor;
            panelCheckStatus.BackColor = Color.FromArgb(56, 56, 56);
            imageROIEditControlPreview.BackColor = Color.Black;

            labelImageFolder.ForeColor = Color.Gainsboro;
            labelCategoryTitle.ForeColor = InspectionAccentColor;
            labelPreviewTitle.ForeColor = Color.Gainsboro;
            labelImageCount.ForeColor = Color.Gainsboro;
            labelRoiStatus.ForeColor = Color.Gainsboro;
            textBoxImageFolder.BackColor = Color.FromArgb(38, 38, 38);
            textBoxImageFolder.ForeColor = Color.White;
            textBoxImageFolder.BorderStyle = BorderStyle.FixedSingle;

            ApplyActionButtonStyle(buttonSelectImageFolder);
            ApplyActionButtonStyle(buttonReloadImages);
            ApplyActionButtonStyle(buttonSetSelectedOk);
            ApplyActionButtonStyle(buttonSetSelectedNg);
            ApplyActionButtonStyle(buttonDrawRoi);
            ApplyActionButtonStyle(buttonClearRoi);
            HighlightFilterButton();
            ApplyTrainingTheme();
            UpdateNavigationButtonState();
            RefreshAdaptiveText();
        }

        /// <summary>
        /// 刷新顶部自定义导航按钮选中态。
        /// </summary>
        private void UpdateNavigationButtonState()
        {
            ApplyNavigationButtonStyle(buttonNavCheck, tabControlMain.SelectedIndex == 0);
            ApplyNavigationButtonStyle(buttonNavTraining, tabControlMain.SelectedIndex == 1);
        }

        /// <summary>
        /// 应用顶部导航按钮样式，保证导航条背景与工作区同色。
        /// </summary>
        /// <param name="button">导航按钮。</param>
        /// <param name="selected">是否选中。</param>
        private static void ApplyNavigationButtonStyle(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = selected ? InspectionGridBackColor : InspectionBackColor;
            button.ForeColor = selected ? InspectionAccentColor : Color.WhiteSmoke;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            FitButtonText(button, 12F, 8F, FontStyle.Bold);
        }

        /// <summary>
        /// 应用训练页深色工作区主题，使训练页和检查页保持一致背景。
        /// </summary>
        private void ApplyTrainingTheme()
        {
            tabPageTraining.BackColor = InspectionBackColor;
            tabPageTraining.UseVisualStyleBackColor = false;
            tableLayoutPanelTrainingRoot.BackColor = InspectionBackColor;
            groupBoxTrainingParams.BackColor = InspectionBackColor;
            groupBoxTrainingParams.ForeColor = Color.Gainsboro;
            groupBoxTrainingOutput.BackColor = InspectionBackColor;
            groupBoxTrainingOutput.ForeColor = Color.Gainsboro;
            tableLayoutPanelTrainingParams.BackColor = InspectionBackColor;
            tableLayoutPanelTrainingOutput.BackColor = InspectionBackColor;
            panelTrainingButtons.BackColor = InspectionBackColor;
            textBoxTrainingLog.BackColor = DarkInputBackColor;
            textBoxTrainingLog.ForeColor = Color.Gainsboro;
            textBoxTrainingLog.BorderStyle = BorderStyle.FixedSingle;

            ApplyTrainingLabelStyle(labelModelType);
            ApplyTrainingLabelStyle(labelThreshold);
            ApplyTrainingLabelStyle(labelMiniArea);
            ApplyTrainingLabelStyle(labelInputWidth);
            ApplyTrainingLabelStyle(labelInputHeight);
            ApplyTrainingLabelStyle(labelEpochs);
            ApplyTrainingLabelStyle(labelBatchSize);
            ApplyTrainingLabelStyle(labelDevice);
            ApplyTrainingLabelStyle(labelTemplateName);
            ApplyTrainingLabelStyle(labelModelOutputPath);

            ApplyTrainingInputStyle(comboBoxModelType);
            ApplyTrainingInputStyle(numericUpDownThreshold);
            ApplyTrainingInputStyle(numericUpDownMiniArea);
            ApplyTrainingInputStyle(numericUpDownInputWidth);
            ApplyTrainingInputStyle(numericUpDownInputHeight);
            ApplyTrainingInputStyle(numericUpDownEpochs);
            ApplyTrainingInputStyle(numericUpDownBatchSize);
            ApplyTrainingInputStyle(comboBoxDevice);
            ApplyTrainingInputStyle(textBoxTemplateName);
            ApplyTrainingInputStyle(textBoxModelOutputPath);

            ApplyActionButtonStyle(buttonSelectModelOutput);
            ApplyActionButtonStyle(buttonStartTraining);
            ApplyActionButtonStyle(buttonStopTraining);
        }

        /// <summary>
        /// 应用训练页标签颜色。
        /// </summary>
        /// <param name="label">标签控件。</param>
        private static void ApplyTrainingLabelStyle(Label label)
        {
            label.ForeColor = Color.Gainsboro;
        }

        /// <summary>
        /// 应用训练页输入控件颜色。
        /// </summary>
        /// <param name="control">输入控件。</param>
        private static void ApplyTrainingInputStyle(Control control)
        {
            control.BackColor = DarkInputBackColor;
            control.ForeColor = Color.White;
        }

        /// <summary>
        /// 应用检查页操作按钮样式。
        /// </summary>
        /// <param name="button">按钮控件。</param>
        private static void ApplyActionButtonStyle(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(95, 95, 95);
            button.BackColor = Color.FromArgb(68, 68, 68);
            button.ForeColor = Color.White;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            FitButtonText(button, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
        }

        /// <summary>
        /// 刷新按钮和标题的文字自适应，避免不同 DPI 或窗口尺寸下文字被裁切。
        /// </summary>
        private void RefreshAdaptiveText()
        {
            FitButtonText(buttonNavCheck, 12F, 8F, FontStyle.Bold);
            FitButtonText(buttonNavTraining, 12F, 8F, FontStyle.Bold);
            FitButtonText(buttonFilterAll, FilterButtonFont.SizeInPoints, 8F, FontStyle.Bold);
            FitButtonText(buttonFilterOk, FilterButtonFont.SizeInPoints, 8F, FontStyle.Bold);
            FitButtonText(buttonFilterNg, FilterButtonFont.SizeInPoints, 8F, FontStyle.Bold);
            FitButtonText(buttonSelectImageFolder, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonReloadImages, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonDrawRoi, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonClearRoi, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonSelectModelOutput, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonStartTraining, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitButtonText(buttonStopTraining, ActionButtonFont.SizeInPoints, 7F, FontStyle.Bold);
            FitLabelText(labelPreviewTitle, 10F, 7F, FontStyle.Bold);
        }

        /// <summary>
        /// 按按钮可用空间缩小字体，保证文本尽量完整显示。
        /// </summary>
        /// <param name="button">按钮控件。</param>
        /// <param name="preferredSize">首选字号。</param>
        /// <param name="minSize">最小字号。</param>
        /// <param name="style">字体样式。</param>
        private static void FitButtonText(Button button, float preferredSize, float minSize, FontStyle style)
        {
            if (button == null || string.IsNullOrEmpty(button.Text))
                return;

            Size available = new Size(Math.Max(1, button.ClientSize.Width - 8), Math.Max(1, button.ClientSize.Height - 6));
            float fittedSize = CalculateFittedFontSize(button.Text, available, preferredSize, minSize, style);
            button.Font = GetAutoFitFont(fittedSize, style);
        }

        /// <summary>
        /// 按标签可用空间缩小字体，保证预览标题尽量展示更多文件名。
        /// </summary>
        /// <param name="label">标签控件。</param>
        /// <param name="preferredSize">首选字号。</param>
        /// <param name="minSize">最小字号。</param>
        /// <param name="style">字体样式。</param>
        private static void FitLabelText(Label label, float preferredSize, float minSize, FontStyle style)
        {
            if (label == null || string.IsNullOrEmpty(label.Text))
                return;

            Size available = new Size(Math.Max(1, label.ClientSize.Width - 6), Math.Max(1, label.ClientSize.Height - 4));
            float fittedSize = CalculateFittedFontSize(label.Text, available, preferredSize, minSize, style);
            label.Font = GetAutoFitFont(fittedSize, style);
        }

        /// <summary>
        /// 根据文本和控件可用空间计算适配字号。
        /// </summary>
        /// <param name="text">显示文本。</param>
        /// <param name="available">可用区域。</param>
        /// <param name="preferredSize">首选字号。</param>
        /// <param name="minSize">最小字号。</param>
        /// <param name="style">字体样式。</param>
        /// <returns>适配后的字号。</returns>
        private static float CalculateFittedFontSize(string text, Size available, float preferredSize, float minSize, FontStyle style)
        {
            float size = preferredSize;
            while (size > minSize)
            {
                Font font = GetAutoFitFont(size, style);
                Size measured = TextRenderer.MeasureText(text, font, available, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                if (measured.Width <= available.Width && measured.Height <= available.Height)
                    return size;

                size -= 0.5F;
            }

            return minSize;
        }

        /// <summary>
        /// 获取自适应字体缓存项。
        /// </summary>
        /// <param name="size">字号。</param>
        /// <param name="style">字体样式。</param>
        /// <returns>缓存字体。</returns>
        private static Font GetAutoFitFont(float size, FontStyle style)
        {
            string key = style.ToString() + "_" + size.ToString("0.0", CultureInfo.InvariantCulture);
            Font font;
            if (!AutoFitFontCache.TryGetValue(key, out font))
            {
                font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point, 134);
                AutoFitFontCache[key] = font;
            }

            return font;
        }

        /// <summary>
        /// 修正最大化边界，覆盖父窗口区域，避免最小化后再最大化露出主界面底部。
        /// </summary>
        private void EnsureMaximizedBounds()
        {
            if (_applyingMaximizedBounds || IsDisposed)
                return;

            _applyingMaximizedBounds = true;
            try
            {
                Rectangle targetBounds = GetMaximizedTargetBounds();
                if (MaximizedBounds != targetBounds)
                {
                    MaximizedBounds = targetBounds;
                }

                StartPosition = FormStartPosition.Manual;
                if (WindowState != FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Maximized;
                }
            }
            finally
            {
                _applyingMaximizedBounds = false;
            }
        }

        /// <summary>
        /// 获取最大化目标边界；优先覆盖父窗口，避免对话框底部露出主界面。
        /// </summary>
        /// <returns>最大化目标边界。</returns>
        private Rectangle GetMaximizedTargetBounds()
        {
            if (Owner != null && !Owner.IsDisposed && Owner.WindowState != FormWindowState.Minimized)
            {
                return Owner.Bounds;
            }

            return Screen.FromControl(this).Bounds;
        }

        /// <summary>
        /// 设置预览图片。
        /// </summary>
        /// <param name="bitmap">预览位图。</param>
        private void SetPreviewImage(Bitmap bitmap)
        {
            Bitmap old = _previewBitmap;
            _previewBitmap = bitmap;
            if (imageROIEditControlPreview != null && !imageROIEditControlPreview.IsDisposed)
            {
                imageROIEditControlPreview.SetImage(bitmap);
                old = null;
            }
            old?.Dispose();
        }

        /// <summary>
        /// 清空缩略图控件。
        /// </summary>
        private void ClearImageCards()
        {
            if (flowLayoutPanelImages == null) return;

            Control[] controls = flowLayoutPanelImages.Controls.Cast<Control>().ToArray();
            flowLayoutPanelImages.Controls.Clear();
            foreach (Control control in controls)
            {
                control.Dispose();
            }
            _imageCards.Clear();
        }

        /// <summary>
        /// 清空当前选中图片和预览图。
        /// </summary>
        private void ClearSelection()
        {
            UnsupervisedImageItem previous = _selectedImage;
            _selectedImage = null;
            SetPreviewImage(null);
            labelPreviewTitle.Text = "图像预览 / ROI编辑";
            InvalidateImageCard(previous);
            UpdateSelectionActionState();
            UpdateRoiStatus();
        }

        /// <summary>
        /// 局部刷新指定图片卡片。
        /// </summary>
        /// <param name="item">图片信息。</param>
        private void InvalidateImageCard(UnsupervisedImageItem item)
        {
            if (item == null) return;
            Control card;
            if (_imageCards.TryGetValue(item, out card) && card != null && !card.IsDisposed)
            {
                if (card is UnsupervisedImageCardControl imageCard)
                    imageCard.Selected = ReferenceEquals(item, _selectedImage);
                card.Invalidate();
            }
        }

        /// <summary>
        /// 更新选中图片分类按钮可用状态。
        /// </summary>
        private void UpdateSelectionActionState()
        {
            bool enabled = _selectedImage != null && buttonSelectImageFolder.Enabled;
            buttonSetSelectedOk.Enabled = enabled;
            buttonSetSelectedNg.Enabled = enabled;
        }

        /// <summary>
        /// 释放已加载图片对象。
        /// </summary>
        private void DisposeImageItems()
        {
            foreach (UnsupervisedImageItem item in _images)
            {
                item.Dispose();
            }
            _images.Clear();
        }

        /// <summary>
        /// 写入训练日志。
        /// </summary>
        /// <param name="message">日志内容。</param>
        private void AppendTrainingLog(string message)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendTrainingLog), message);
                return;
            }

            string line = "[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message;
            textBoxTrainingLog.AppendText(line + Environment.NewLine);
            LogHelper.AddLog(MsgLevel.Info, "【无监督训练】" + message, true);
        }

        /// <summary>
        /// 不锁文件读取位图。
        /// </summary>
        /// <param name="file">图片路径。</param>
        /// <returns>位图。</returns>
        private static Bitmap LoadBitmapWithoutLock(string file)
        {
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image image = Image.FromStream(stream, false, false))
            {
                return new Bitmap(image);
            }
        }

        /// <summary>
        /// 为滚动容器开启双缓冲，减少大量缩略图滚动时的闪烁和卡顿。
        /// </summary>
        /// <param name="control">滚动容器。</param>
        private static void EnableDoubleBufferedScrolling(Control control)
        {
            if (control == null) return;
            PropertyInfo property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(control, true, null);
        }

        /// <summary>
        /// 限制 ROI 在图像范围内。
        /// </summary>
        /// <param name="rect">原始 ROI。</param>
        /// <param name="imageWidth">图像宽度。</param>
        /// <param name="imageHeight">图像高度。</param>
        /// <returns>限制后的 ROI。</returns>
        private static CvRect ClampRect(CvRect rect, int imageWidth, int imageHeight)
        {
            int x = Math.Max(0, Math.Min(rect.X, imageWidth - 1));
            int y = Math.Max(0, Math.Min(rect.Y, imageHeight - 1));
            int width = Math.Max(0, Math.Min(rect.Width, imageWidth - x));
            int height = Math.Max(0, Math.Min(rect.Height, imageHeight - y));
            return new CvRect(x, y, width, height);
        }

        /// <summary>
        /// 判断路径是否位于指定根目录内。
        /// </summary>
        /// <param name="path">待判断路径。</param>
        /// <param name="root">根目录。</param>
        /// <returns>在根目录内返回 true。</returns>
        private static bool IsPathUnderRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 确保目录路径带末尾分隔符。
        /// </summary>
        /// <param name="path">目录路径。</param>
        /// <returns>带末尾分隔符的目录路径。</returns>
        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// 安全化文件名。
        /// </summary>
        /// <param name="value">原始文件名。</param>
        /// <returns>安全文件名。</returns>
        private static string SafeFileName(string value)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "无监督模板" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(c, '_');
            }
            return text;
        }

        /// <summary>
        /// 释放运行期加载的图片、缩略图和后台任务。
        /// </summary>
        private void ReleaseRuntimeResources()
        {
            if (_runtimeResourcesReleased)
                return;

            _runtimeResourcesReleased = true;
            _loadCancellation?.Cancel();
            _trainingCancellation?.Cancel();
            _trainingService.CancelActiveTraining();
            ClearImageCards();
            DisposeImageItems();
            _selectedImage = null;
            SetPreviewImage(null);
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _trainingCancellation?.Dispose();
            _trainingCancellation = null;
        }

        /// <summary>
        /// 窗体开始关闭时释放图片和取消后台任务。
        /// </summary>
        /// <param name="e">关闭事件参数。</param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ReleaseRuntimeResources();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 窗体关闭后再次确保资源已释放。
        /// </summary>
        /// <param name="e">关闭事件参数。</param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ReleaseRuntimeResources();
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 无监督图片缩略图自绘卡片，替代 PictureBox+Label 组合以降低滚动时控件数量。
        /// </summary>
        private sealed class UnsupervisedImageCardControl : Control
        {
            /// <summary>
            /// 卡片对应的图片信息。
            /// </summary>
            private readonly UnsupervisedImageItem _item;

            /// <summary>
            /// 初始化缩略图卡片。
            /// </summary>
            /// <param name="item">图片信息。</param>
            public UnsupervisedImageCardControl(UnsupervisedImageItem item)
            {
                _item = item;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            /// <summary>
            /// 是否为当前选中图片。
            /// </summary>
            public bool Selected { get; set; }

            /// <summary>
            /// 计算缩略图在卡片图片区域中的等比显示矩形。
            /// </summary>
            /// <param name="sourceSize">缩略图原始尺寸。</param>
            /// <param name="targetSize">显示区域尺寸。</param>
            /// <returns>相对显示区域左上角的绘制矩形。</returns>
            private static Rectangle CalculateFitRect(Size sourceSize, Size targetSize)
            {
                if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                    return new Rectangle(0, 0, Math.Max(1, targetSize.Width), Math.Max(1, targetSize.Height));

                float scale = Math.Min((float)targetSize.Width / sourceSize.Width, (float)targetSize.Height / sourceSize.Height);
                int width = Math.Max(1, (int)(sourceSize.Width * scale));
                int height = Math.Max(1, (int)(sourceSize.Height * scale));
                int x = (targetSize.Width - width) / 2;
                int y = (targetSize.Height - height) / 2;
                return new Rectangle(x, y, width, height);
            }

            /// <summary>
            /// 绘制缩略图、文件名和 OK/NG/选中边框。
            /// </summary>
            /// <param name="e">绘制参数。</param>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush cardBrush = new SolidBrush(InspectionCardBackColor))
                {
                    e.Graphics.FillRectangle(cardBrush, ClientRectangle);
                }

                Rectangle imageRect = new Rectangle(10, 10, Width - 20, 126);
                using (Brush imageBrush = new SolidBrush(Color.Black))
                {
                    e.Graphics.FillRectangle(imageBrush, imageRect);
                }

                if (_item?.Thumbnail != null)
                {
                    Rectangle target = CalculateFitRect(_item.Thumbnail.Size, imageRect.Size);
                    target.Offset(imageRect.Location);
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    e.Graphics.DrawImage(_item.Thumbnail, target);
                }

                Rectangle textRect = new Rectangle(8, 140, Width - 16, 24);
                TextRenderer.DrawText(e.Graphics, _item?.DisplayName ?? string.Empty, Font, textRect, Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                Color borderColor = Selected
                    ? InspectionSelectedBorderColor
                    : (_item != null && _item.Category == UnsupervisedImageCategory.NG ? Color.FromArgb(230, 40, 40) : Color.FromArgb(0, 190, 100));
                using (Pen pen = new Pen(borderColor, Selected ? 4 : 3))
                {
                    e.Graphics.DrawRectangle(pen, 2, 2, Width - 5, Height - 5);
                }
            }
        }
    }
}
