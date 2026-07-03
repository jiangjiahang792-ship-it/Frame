using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
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
        /// 无监督训练服务。
        /// </summary>
        private readonly UnsupervisedTrainingService _trainingService = new UnsupervisedTrainingService();

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
            comboBoxModelType.SelectedIndex = 0;
            comboBoxDevice.SelectedIndex = 0;
            buttonStopTraining.Enabled = false;
            imageROIEditControlPreview.RoiChanged += imageROIEditControlPreview_RoiChanged;
            textBoxTemplateName.Text = "无监督模板_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            UpdateGeneratedModelPath();
            RefreshRuntimeStatus();
            UpdateImageCountText();
            UpdateRoiStatus();
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
        /// 进入 ROI 绘制状态。
        /// </summary>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            if (_selectedImage == null)
            {
                MessageBoxTD.Show("请先选择一张预览图像。");
                return;
            }

            if (imageROIEditControlPreview.GetRoiCount() >= 1)
            {
                MessageBoxTD.Show("当前只允许一个 ROI，请先清空已有 ROI。");
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
                    MessageBoxTD.Show("模板文件必须保存到运行目录 Model 文件夹内。");
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
                MessageBoxTD.Show("无监督模板生成完成：" + result.TemplatePath);
            }
            catch (OperationCanceledException)
            {
                AppendTrainingLog("训练已取消。");
            }
            catch (Exception ex)
            {
                AppendTrainingLog("训练失败：" + ex.Message);
                LogHelper.AddLog(MsgLevel.Exception, "无监督训练失败：" + ex.Message, true);
                MessageBoxTD.Show("训练失败：" + ex.Message);
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
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                MessageBoxTD.Show("请选择有效的图像路径。");
                return;
            }

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
                MessageBoxTD.Show("加载图像失败：" + ex.Message);
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
            ClearImageCards();
            IEnumerable<UnsupervisedImageItem> source = _currentFilter == UnsupervisedImageCategory.All
                ? _images
                : _images.Where(item => item.Category == _currentFilter);

            foreach (UnsupervisedImageItem item in source)
            {
                flowLayoutPanelImages.Controls.Add(CreateImageCard(item));
            }

            HighlightFilterButton();
        }

        /// <summary>
        /// 创建单张缩略图卡片。
        /// </summary>
        /// <param name="item">图片信息。</param>
        /// <returns>缩略图控件。</returns>
        private Control CreateImageCard(UnsupervisedImageItem item)
        {
            var card = new Panel
            {
                Width = 156,
                Height = 128,
                Margin = new Padding(6),
                BackColor = Color.White,
                Tag = item,
                Cursor = Cursors.Hand
            };
            card.Paint += imageCard_Paint;
            card.Click += imageCard_Click;

            var picture = new PictureBox
            {
                Image = item.Thumbnail,
                Location = new Point(10, 10),
                Size = new Size(136, 86),
                SizeMode = PictureBoxSizeMode.Zoom,
                Tag = item,
                Cursor = Cursors.Hand
            };
            picture.Click += imageCard_Click;
            card.Controls.Add(picture);

            var label = new Label
            {
                Text = item.DisplayName,
                Location = new Point(8, 100),
                Size = new Size(140, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                Tag = item,
                Cursor = Cursors.Hand
            };
            label.Click += imageCard_Click;
            card.Controls.Add(label);

            return card;
        }

        /// <summary>
        /// 绘制 OK/NG 缩略图边框。
        /// </summary>
        private void imageCard_Paint(object sender, PaintEventArgs e)
        {
            if (!(sender is Control control) || !(control.Tag is UnsupervisedImageItem item)) return;

            Color color = item.Category == UnsupervisedImageCategory.NG ? Color.IndianRed : Color.SeaGreen;
            if (ReferenceEquals(item, _selectedImage))
            {
                color = Color.DodgerBlue;
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
            _selectedImage = item;
            try
            {
                SetPreviewImage(LoadBitmapWithoutLock(item.FilePath));
                labelPreviewTitle.Text = "图像预览 / ROI编辑：" + item.DisplayName;
                UpdateRoiStatus();
                flowLayoutPanelImages.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("预览图像失败：" + ex.Message);
            }
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
            button.BackColor = selected ? Color.FromArgb(225, 238, 255) : SystemColors.Control;
            button.FlatStyle = selected ? FlatStyle.Flat : FlatStyle.Standard;
        }

        /// <summary>
        /// 设置预览图片。
        /// </summary>
        /// <param name="bitmap">预览位图。</param>
        private void SetPreviewImage(Bitmap bitmap)
        {
            Bitmap old = _previewBitmap;
            _previewBitmap = bitmap;
            imageROIEditControlPreview.SetImage(bitmap);
            old?.Dispose();
        }

        /// <summary>
        /// 清空缩略图控件。
        /// </summary>
        private void ClearImageCards()
        {
            foreach (Control control in flowLayoutPanelImages.Controls)
            {
                control.Dispose();
            }
            flowLayoutPanelImages.Controls.Clear();
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
        /// 窗体关闭时释放图片和取消后台任务。
        /// </summary>
        /// <param name="e">关闭事件参数。</param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _loadCancellation?.Cancel();
            _trainingCancellation?.Cancel();
            _trainingService.CancelActiveTraining();
            DisposeImageItems();
            SetPreviewImage(null);
            _loadCancellation?.Dispose();
            _trainingCancellation?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
