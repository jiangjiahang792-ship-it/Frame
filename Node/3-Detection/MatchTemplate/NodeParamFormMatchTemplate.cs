using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public partial class NodeParamFormMatchTemplate : FormBase, INodeParamForm
    {
        private Process process;
        private NodeBase node;
        private Bitmap src;
        private Bitmap templateImage;
        private Bitmap templateOriginalImage;
        private Bitmap templateEraseMask;
        private string templateSourceName = string.Empty;
        private double toleranceAngle = 80.0;
        private double angleStep = 0.0;
        private double maxOverlap = 40.0;
        private float score = 0.5f;
        private int resultNum = 1;
        private bool coarseMatch = false;
        private bool showOutlineStatus = true;
        private bool showOutRegionStatus = true;
        private bool allSearch = true;
        private float searchRegionCenterX;
        private float searchRegionCenterY;
        private float searchRegionWidth;
        private float searchRegionHeight;
        private float searchRegionAngle;
        private bool searchRegionConfirmed = true;
        private Timer continuousRunTimer;
        private bool continuousRunBusy = false;

        public NodeParamFormMatchTemplate(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            node = nodeBase;
            Shown += NodeParamFormQRCodeIdentification_Shown;

            toolTip1.SetToolTip(label2, "模板旋转搜索范围，单位度。");
            toolTip1.SetToolTip(label4, "匹配可靠性阈值，支持 0~1 或 0~100。");
            toolTip1.SetToolTip(labelAngleStep, "旋转搜索步长，0 表示由算法自动选择。");
            toolTip1.SetToolTip(labelMaxOverlap, "多个目标之间允许的最大重叠率，支持 0~100。");
            toolTip1.SetToolTip(showImageControlSource, "用于绘制搜索区域，也用于查看执行预览。");
            toolTip1.SetToolTip(pictureBoxTemplate, "青色轮廓为当前模板参与匹配的边缘。");

            comboBoxPolarity.SelectedIndex = 0;
            comboBoxScaleMode.SelectedIndex = 0;
            checkBoxAllSearch.Checked = true;
            continuousRunTimer = new Timer { Interval = 500 };
            continuousRunTimer.Tick += continuousRunTimer_Tick;

            RefreshTemplateStatus(0);
            UpdateRunStatus(null);
            UpdateSearchRegionControls();
        }

        /// <summary>
        /// 窗口显示时刷新订阅图像。
        /// </summary>
        private void NodeParamFormQRCodeIdentification_Shown(object sender, EventArgs e)
        {
            UpdataImage();
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 反序列化后还原界面参数。
        /// </summary>
        public void SetParam2Form()
        {
            if (!(Params is NodeParamMatchTemplate param))
                return;

            LoadTemplateFromParam(param);
            nodeSubscription1.SetText(param.Text1, param.Text2);
            toleranceAngle = param.ToleranceAngle <= 0 ? 80.0 : param.ToleranceAngle;
            angleStep = Math.Max(0, param.AngleStep);
            maxOverlap = param.MaxOverlap <= 0 ? 40.0 : param.MaxOverlap;
            score = param.MinScore <= 0 ? 0.5f : param.MinScore;
            resultNum = param.ResultNum <= 0 ? 1 : param.ResultNum;
            coarseMatch = param.CoarseMatch;
            showOutlineStatus = param.ShowOutlineStatus;
            showOutRegionStatus = param.ShowOutRegionStatus;
            allSearch = param.AllSearch || param.SearchRegionWidth <= 0 || param.SearchRegionHeight <= 0;
            searchRegionCenterX = param.SearchRegionCenterX;
            searchRegionCenterY = param.SearchRegionCenterY;
            searchRegionWidth = param.SearchRegionWidth;
            searchRegionHeight = param.SearchRegionHeight;
            searchRegionAngle = param.SearchRegionAngle;
            searchRegionConfirmed = allSearch || (searchRegionWidth > 0 && searchRegionHeight > 0);

            textBoxScale.Text = toleranceAngle.ToString("G");
            textBoxAngleStep.Text = angleStep.ToString("G");
            textBoxMaxOverlap.Text = maxOverlap.ToString("G");
            textBoxMinScore.Text = score.ToString("G");
            textBoxResultNum.Text = resultNum.ToString();
            checkBoxCoarseMatch.Checked = coarseMatch;
            checkBoxShowOutline.Checked = showOutlineStatus;
            checkBoxShowMatchBox.Checked = showOutRegionStatus;
            checkBoxAllSearch.Checked = allSearch;
            ApplySearchRegionToView();
        }

        /// <summary>
        /// 初始化订阅节点。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 刷新输入图像。
        /// </summary>
        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }

        /// <summary>
        /// 更新当前订阅图像。
        /// </summary>
        public void UpdataImage()
        {
            Bitmap newSource = null;
            try
            {
                var image = nodeSubscription1.GetValue<OutputImage>();
                if (image?.Bitmaps != null && image.Bitmaps.Count > 0)
                    newSource = image.Bitmaps[0].ToBitmap();
            }
            catch (Exception)
            {
                newSource = null;
            }

            ReplaceSourceImage(newSource);
        }

        private void ReplaceSourceImage(Bitmap newSource)
        {
            src?.Dispose();
            src = newSource;

            if (src == null)
            {
                showImageControlSource.ClearDisplay();
                return;
            }

            showImageControlSource.SetImage((Bitmap)src.Clone());
            showImageControlSource.ShowFit();
            ApplySearchRegionToView();
        }

        private void ApplySearchRegionToView()
        {
            if (showImageControlSource == null)
                return;

            showImageControlSource.ClearDynamicRoi();
            if (src == null)
            {
                UpdateSearchRegionControls();
                return;
            }

            if (allSearch)
            {
                UpdateSearchRegionControls();
                return;
            }

            EnsureSearchRegionFields();
            showImageControlSource.AddDynamicRect(
                searchRegionCenterX,
                searchRegionCenterY,
                searchRegionWidth,
                searchRegionHeight,
                Color.FromArgb(255, 132, 0),
                "搜索区域");
            searchRegionConfirmed = false;
            UpdateSearchRegionControls();
        }

        private void EnsureSearchRegionFields()
        {
            if (src == null)
                return;

            bool invalid = searchRegionWidth <= 0
                || searchRegionHeight <= 0
                || searchRegionCenterX < 0
                || searchRegionCenterY < 0
                || searchRegionCenterX > src.Width
                || searchRegionCenterY > src.Height;

            if (!invalid)
                return;

            searchRegionCenterX = src.Width / 2f;
            searchRegionCenterY = src.Height / 2f;
            searchRegionWidth = Math.Max(20, src.Width * 0.65f);
            searchRegionHeight = Math.Max(20, src.Height * 0.65f);
            searchRegionAngle = 0;
        }

        private void ResetSearchRegion()
        {
            if (src == null)
                return;

            searchRegionCenterX = src.Width / 2f;
            searchRegionCenterY = src.Height / 2f;
            searchRegionWidth = Math.Max(20, src.Width * 0.65f);
            searchRegionHeight = Math.Max(20, src.Height * 0.65f);
            searchRegionAngle = 0;
            showImageControlSource.ClearDynamicRoi();
            showImageControlSource.AddDynamicRect(
                searchRegionCenterX,
                searchRegionCenterY,
                searchRegionWidth,
                searchRegionHeight,
                Color.FromArgb(255, 132, 0),
                "搜索区域");
            UpdateSearchRegionControls();
        }

        private void checkBoxAllSearch_CheckedChanged(object sender, EventArgs e)
        {
            allSearch = checkBoxAllSearch.Checked;
            if (src != null)
            {
                if (allSearch)
                {
                    showImageControlSource.ClearDynamicRoi();
                    searchRegionConfirmed = true;
                }
                else
                    ResetSearchRegion();
            }

            UpdateSearchRegionControls();
        }

        private void buttonResetRoi_Click(object sender, EventArgs e)
        {
            allSearch = false;
            if (checkBoxAllSearch.Checked)
                checkBoxAllSearch.Checked = false;
            else
                ResetSearchRegion();
        }

        private void buttonConfirmSearchRoi_Click(object sender, EventArgs e)
        {
            try
            {
                ConfirmSearchRegion();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message);
            }
        }

        private void UpdateSearchRegionControls()
        {
            buttonResetRoi.Enabled = src != null;
            buttonConfirmSearchRoi.Enabled = src != null && !allSearch;
            labelSearchStatus.Text = allSearch
                ? "当前：整图搜索"
                : $"{(searchRegionConfirmed ? "已确定" : "未确定")}：X {searchRegionCenterX:F1}, Y {searchRegionCenterY:F1}, W {searchRegionWidth:F1}, H {searchRegionHeight:F1}";
        }

        /// <summary>
        /// 点击执行模板匹配。
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MatchTemplate();
                UpdateRunStatus(result);
                if (result.Matches.Count == 0)
                    MessageBoxTD.Show("未找到模板匹配目标。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模板匹配异常，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 模板匹配。show=false 时输出干净原图，结果由 DisplayResult 叠加层显示。
        /// </summary>
        public FastTemplateMatchResult MatchTemplate(bool show = true)
        {
            Bitmap sourceImage = null;
            Bitmap matchImage = null;
            try
            {
                ReadFormValues();
                EnsureTemplateLoaded();
                if (src == null)
                    throw new ArgumentException("源图像不能为空。");
                if (templateImage == null)
                    throw new ArgumentException("模板图像不能为空。");

                sourceImage = (Bitmap)src.Clone();
                Rectangle searchBounds = GetSearchBounds(sourceImage.Size);
                matchImage = allSearch
                    ? (Bitmap)sourceImage.Clone()
                    : sourceImage.Clone(searchBounds, PixelFormat.Format24bppRgb);

                var result = FastTemplateMatcher.Match(BuildRuntimeParam(false), matchImage, templateImage);
                if (!allSearch)
                    OffsetResult(result, searchBounds.X, searchBounds.Y);

                if (show)
                {
                    using (var previewSource = (Bitmap)sourceImage.Clone())
                    {
                        result.OutputBitmap = FastTemplateMatcher.DrawPreview(previewSource, result, showOutRegionStatus, showOutlineStatus);
                    }
                    showImageControlSource.SetImage(result.OutputBitmap);
                }
                else
                {
                    result.OutputBitmap = (Bitmap)sourceImage.Clone();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("模板匹配失败。", ex);
            }
            finally
            {
                matchImage?.Dispose();
                sourceImage?.Dispose();
            }
        }

        private Rectangle GetSearchBounds(Size imageSize)
        {
            if (allSearch)
                return new Rectangle(0, 0, imageSize.Width, imageSize.Height);

            CaptureSearchRegion();
            PointF[] corners = GetRotatedRectCorners(
                searchRegionCenterX,
                searchRegionCenterY,
                searchRegionWidth,
                searchRegionHeight,
                searchRegionAngle);

            float minX = corners.Min(p => p.X);
            float minY = corners.Min(p => p.Y);
            float maxX = corners.Max(p => p.X);
            float maxY = corners.Max(p => p.Y);

            int x = Math.Max(0, (int)Math.Floor(minX));
            int y = Math.Max(0, (int)Math.Floor(minY));
            int right = Math.Min(imageSize.Width, (int)Math.Ceiling(maxX));
            int bottom = Math.Min(imageSize.Height, (int)Math.Ceiling(maxY));
            int width = Math.Max(1, right - x);
            int height = Math.Max(1, bottom - y);
            return new Rectangle(x, y, width, height);
        }

        private static PointF[] GetRotatedRectCorners(float cx, float cy, float width, float height, float angle)
        {
            float halfW = width / 2f;
            float halfH = height / 2f;
            var points = new[]
            {
                new PointF(-halfW, -halfH),
                new PointF( halfW, -halfH),
                new PointF( halfW,  halfH),
                new PointF(-halfW,  halfH)
            };

            using (var matrix = new Matrix())
            {
                matrix.Rotate(angle);
                matrix.Translate(cx, cy, MatrixOrder.Append);
                matrix.TransformPoints(points);
            }

            return points;
        }

        private static void OffsetResult(FastTemplateMatchResult result, float offsetX, float offsetY)
        {
            foreach (var match in result.Matches)
            {
                match.Box.CenterX += offsetX;
                match.Box.CenterY += offsetY;
            }

            foreach (var outline in result.Outlines)
            {
                for (int i = 0; i < outline.Points.Count; i++)
                {
                    PointF point = outline.Points[i];
                    outline.Points[i] = new PointF(point.X + offsetX, point.Y + offsetY);
                }
            }
        }

        private void EnsureTemplateLoaded()
        {
            if (templateImage != null)
                return;

            if (Params is NodeParamMatchTemplate param)
                LoadTemplateFromParam(param);
        }

        private NodeParamMatchTemplate BuildRuntimeParam(bool includeTemplateImage = true)
        {
            return new NodeParamMatchTemplate
            {
                TemplateFileName = string.Empty,
                TemplateImageBytes = includeTemplateImage ? FastTemplateMatcher.BitmapToPngBytes(templateImage) : null,
                TemplateEraseMaskBytes = FastTemplateMatcher.BitmapToPngBytes(templateEraseMask),
                TemplateSourceName = templateSourceName,
                Text1 = nodeSubscription1.GetText1(),
                Text2 = nodeSubscription1.GetText2(),
                Scale = 1,
                MinScore = score,
                ResultNum = resultNum,
                AllSearch = allSearch,
                SearchRegionCenterX = searchRegionCenterX,
                SearchRegionCenterY = searchRegionCenterY,
                SearchRegionWidth = searchRegionWidth,
                SearchRegionHeight = searchRegionHeight,
                SearchRegionAngle = searchRegionAngle,
                ToleranceAngle = toleranceAngle,
                AngleStep = angleStep,
                MaxOverlap = maxOverlap,
                CoarseMatch = coarseMatch,
                ShowOutlineStatus = showOutlineStatus,
                ShowOutRegionStatus = showOutRegionStatus
            };
        }

        /// <summary>
        /// 点击保存参数。
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        private void buttonContinuousRun_Click(object sender, EventArgs e)
        {
            if (continuousRunTimer.Enabled)
            {
                continuousRunTimer.Stop();
                buttonContinuousRun.Text = "连续执行";
                return;
            }

            continuousRunTimer.Start();
            buttonContinuousRun.Text = "停止连续";
        }

        private void continuousRunTimer_Tick(object sender, EventArgs e)
        {
            if (continuousRunBusy)
                return;

            try
            {
                continuousRunBusy = true;
                var result = MatchTemplate();
                UpdateRunStatus(result);
            }
            catch (Exception ex)
            {
                continuousRunTimer.Stop();
                buttonContinuousRun.Text = "连续执行";
                MessageBoxTD.Show($"连续匹配停止：{ex.Message}");
            }
            finally
            {
                continuousRunBusy = false;
            }
        }

        /// <summary>
        /// 设置参数。
        /// </summary>
        private bool SaveParams()
        {
            try
            {
                ReadFormValues();
                if (templateImage == null)
                    throw new Exception("请先创建或载入模板。");

                Params = BuildRuntimeParam(true);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常，原因：{ex.Message}");
                return false;
            }

            return true;
        }

        private void UpdateRunStatus(FastTemplateMatchResult result)
        {
            if (result == null)
            {
                labelResultSummary.Text = "暂无运行结果";
                return;
            }

            labelResultSummary.Text = $"匹配 {result.Matches.Count}/{result.ExpectedCount} 个，耗时 {result.AlgorithmMs:F2} ms，阈值 {result.ExpectedScore * 100.0:F2}";
        }

        private void ReadFormValues()
        {
            resultNum = int.Parse(textBoxResultNum.Text);
            if (resultNum <= 0 || resultNum > 1000)
                throw new Exception("最大匹配个数设置区间为 [1,1000]。");

            score = float.Parse(textBoxMinScore.Text);
            if (score > 100 || score < 0)
                throw new Exception("最小匹配分数区间为 [0,1] 或 [0,100]。");

            toleranceAngle = double.Parse(textBoxScale.Text);
            if (toleranceAngle < 0 || toleranceAngle > 360)
                throw new Exception("角度范围设置区间为 [0,360]。");

            angleStep = double.Parse(textBoxAngleStep.Text);
            if (angleStep < 0 || angleStep > 360)
                throw new Exception("角度步长设置区间为 [0,360]。");

            maxOverlap = double.Parse(textBoxMaxOverlap.Text);
            if (maxOverlap < 0 || maxOverlap > 100)
                throw new Exception("最大重叠率设置区间为 [0,100]。");

            coarseMatch = checkBoxCoarseMatch.Checked;
            showOutlineStatus = checkBoxShowOutline.Checked;
            showOutRegionStatus = checkBoxShowMatchBox.Checked;
            allSearch = checkBoxAllSearch.Checked;
            CaptureSearchRegion();
        }

        private void CaptureSearchRegion()
        {
            if (allSearch || src == null)
                return;

            var rect = showImageControlSource.GetAllDynamicRects().FirstOrDefault();
            if (rect == null)
            {
                ResetSearchRegion();
                rect = showImageControlSource.GetAllDynamicRects().FirstOrDefault();
            }

            if (rect == null)
                throw new Exception("请先绘制搜索区域。");

            if (!searchRegionConfirmed)
                throw new Exception("请先点击“确定ROI”，确认搜索区域后再运行。");

            if (IsSearchRegionChanged(rect))
                throw new Exception("搜索区域已调整，请重新点击“确定ROI”。");
        }

        private void ConfirmSearchRegion()
        {
            if (allSearch || src == null)
            {
                searchRegionConfirmed = true;
                UpdateSearchRegionControls();
                return;
            }

            var rect = showImageControlSource.GetAllDynamicRects().FirstOrDefault();
            if (rect == null)
                throw new Exception("请先绘制搜索区域。");

            searchRegionCenterX = rect.CX;
            searchRegionCenterY = rect.CY;
            searchRegionWidth = Math.Max(1, rect.W);
            searchRegionHeight = Math.Max(1, rect.H);
            searchRegionAngle = rect.Phi;
            searchRegionConfirmed = true;
            UpdateSearchRegionControls();
        }

        private bool IsSearchRegionChanged(TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiRotatedRect rect)
        {
            const float tolerance = 0.5f;
            return Math.Abs(rect.CX - searchRegionCenterX) > tolerance
                || Math.Abs(rect.CY - searchRegionCenterY) > tolerance
                || Math.Abs(rect.W - searchRegionWidth) > tolerance
                || Math.Abs(rect.H - searchRegionHeight) > tolerance
                || Math.Abs(rect.Phi - searchRegionAngle) > tolerance;
        }

        /// <summary>
        /// 点击选择模板图像文件。文件只作为导入源，保存后模板数据跟随方案。
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            LoadTemplateImage(openFileDialog1.FileName);
        }

        private void buttonCreateTemplate_Click(object sender, EventArgs e)
        {
            OpenTemplateDialog(null, "当前ROI模板");
        }

        private void buttonEditTemplate_Click(object sender, EventArgs e)
        {
            if (templateImage == null)
            {
                MessageBoxTD.Show("请先创建或载入模板。");
                return;
            }

            OpenTemplateDialog(templateImage, templateSourceName);
        }

        private void OpenTemplateDialog(Bitmap initialTemplate, string initialSourceName)
        {
            try
            {
                if (src == null && initialTemplate == null)
                    throw new InvalidOperationException("源图像不能为空，请先刷新或订阅图像。");

                using (var form = new TemplateCreateForm(src, initialTemplate, templateEraseMask, initialSourceName))
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                        return;

                    Bitmap eraseMask = form.GetTemplateEraseMask();
                    SetTemplateImage(form.GetTemplateImage(), form.TemplateSourceName, true, eraseMask);
                    eraseMask?.Dispose();
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模板编辑失败，原因：{ex.Message}");
            }
        }

        private void buttonRestoreTemplate_Click(object sender, EventArgs e)
        {
            if (templateOriginalImage == null)
                return;

            SetTemplateImage((Bitmap)templateOriginalImage.Clone(), templateSourceName, false);
        }

        private void buttonDeleteTemplate_Click(object sender, EventArgs e)
        {
            templateImage?.Dispose();
            templateImage = null;
            templateOriginalImage?.Dispose();
            templateOriginalImage = null;
            templateEraseMask?.Dispose();
            templateEraseMask = null;
            templateSourceName = string.Empty;
            UpdateTemplatePreview();
        }

        private void LoadTemplateImage(string fileName)
        {
            using (var raw = new Bitmap(fileName))
            {
                SetTemplateImage(FastTemplateMatcher.To24Bpp(raw), Path.GetFileName(fileName), true);
            }
        }

        private void LoadTemplateFromParam(NodeParamMatchTemplate param)
        {
            if (param == null)
                return;

            try
            {
                if (param.TemplateImageBytes != null && param.TemplateImageBytes.Length > 0)
                {
                    Bitmap eraseMask = param.TemplateEraseMaskBytes == null || param.TemplateEraseMaskBytes.Length == 0
                        ? null
                        : FastTemplateMatcher.PngBytesToBitmap(param.TemplateEraseMaskBytes);
                    SetTemplateImage(FastTemplateMatcher.PngBytesToBitmap(param.TemplateImageBytes), param.TemplateSourceName, true, eraseMask);
                    eraseMask?.Dispose();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(param.TemplateFileName) && File.Exists(param.TemplateFileName))
                    LoadTemplateImage(param.TemplateFileName);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模板还原失败：{ex.Message}");
            }
        }

        private void SetTemplateImage(Bitmap image, string sourceName, bool resetOriginal, Bitmap eraseMask = null)
        {
            templateImage?.Dispose();
            templateImage = image;
            templateEraseMask?.Dispose();
            templateEraseMask = NormalizeTemplateEraseMask(eraseMask, templateImage?.Size ?? Size.Empty);
            templateSourceName = string.IsNullOrWhiteSpace(sourceName) ? "方案内嵌模板" : sourceName;

            if (resetOriginal)
            {
                templateOriginalImage?.Dispose();
                templateOriginalImage = templateImage == null ? null : (Bitmap)templateImage.Clone();
            }

            UpdateTemplatePreview();
        }

        private static Bitmap NormalizeTemplateEraseMask(Bitmap eraseMask, Size templateSize)
        {
            if (eraseMask == null || templateSize.Width <= 0 || templateSize.Height <= 0)
                return null;

            if (eraseMask.Width == templateSize.Width && eraseMask.Height == templateSize.Height)
                return FastTemplateMatcher.To24Bpp(eraseMask);

            var resized = new Bitmap(templateSize.Width, templateSize.Height);
            resized.SetResolution(96, 96);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(eraseMask, new Rectangle(0, 0, resized.Width, resized.Height), 0, 0, eraseMask.Width, eraseMask.Height, GraphicsUnit.Pixel);
            }
            return resized;
        }

        private void UpdateTemplatePreview()
        {
            Image oldImage = pictureBoxTemplate.Image;
            pictureBoxTemplate.Image = null;
            oldImage?.Dispose();

            int contourCount = 0;
            if (templateImage != null)
            {
                var contour = FastTemplateMatcher.BuildTemplateContour(templateImage, templateEraseMask);
                contourCount = contour.Count;
                pictureBoxTemplate.Image = FastTemplateMatcher.DrawTemplateContourPreview(templateImage, contour);
            }

            RefreshTemplateStatus(contourCount);
        }

        private void RefreshTemplateStatus(int contourCount)
        {
            bool hasTemplate = templateImage != null;
            buttonEditTemplate.Enabled = hasTemplate;
            buttonDeleteTemplate.Enabled = hasTemplate;
            buttonRestoreTemplate.Enabled = hasTemplate && templateOriginalImage != null;
            listBoxTemplate.Items.Clear();

            if (!hasTemplate)
            {
                textBoxModelPath.Text = "未创建模板";
                labelTemplatePreview.Text = "模板轮廓预览";
                return;
            }

            textBoxModelPath.Text = $"{templateSourceName} | {templateImage.Width}x{templateImage.Height} | 轮廓点 {contourCount}";
            labelTemplatePreview.Text = "模板轮廓预览";
            listBoxTemplate.Items.Add(textBoxModelPath.Text);
        }

        /// <summary>
        /// 角度容差输入改变时。
        /// </summary>
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            try
            {
                toleranceAngle = double.Parse(textBoxScale.Text);
            }
            catch (Exception)
            {
                if (textBoxScale.Text.Length != 0)
                    MessageBoxTD.Show("角度范围参数不合法！");
            }
        }

        /// <summary>
        /// 输入得分改变。
        /// </summary>
        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            try
            {
                score = float.Parse(textBoxMinScore.Text);
            }
            catch (Exception)
            {
                if (textBoxMinScore.Text.Length != 0)
                    MessageBoxTD.Show("得分参数不合法！");
            }
        }

        private void DisposeImages()
        {
            src?.Dispose();
            src = null;
            templateImage?.Dispose();
            templateImage = null;
            templateOriginalImage?.Dispose();
            templateOriginalImage = null;
            templateEraseMask?.Dispose();
            templateEraseMask = null;
            if (pictureBoxTemplate != null)
            {
                Image oldImage = pictureBoxTemplate.Image;
                pictureBoxTemplate.Image = null;
                oldImage?.Dispose();
            }
            continuousRunTimer?.Stop();
            continuousRunTimer?.Dispose();
            continuousRunTimer = null;
        }

        private void tabControlParams_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabPage = tabControlParams.TabPages[e.Index];
            bool selected = e.Index == tabControlParams.SelectedIndex;
            Rectangle bounds = e.Bounds;
            using (var backBrush = new SolidBrush(selected ? Color.FromArgb(255, 244, 236) : Color.White))
            {
                e.Graphics.FillRectangle(backBrush, bounds);
                TextRenderer.DrawText(
                    e.Graphics,
                    tabPage.Text,
                    new Font("Microsoft YaHei UI", 10.5F, selected ? FontStyle.Bold : FontStyle.Regular),
                    bounds,
                    selected ? Color.FromArgb(30, 30, 30) : Color.FromArgb(90, 90, 90),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (selected)
            {
                using (var pen = new Pen(Color.FromArgb(255, 112, 0), 3))
                    e.Graphics.DrawLine(pen, bounds.Left + 8, bounds.Bottom - 2, bounds.Right - 8, bounds.Bottom - 2);
            }
        }
    }
}
