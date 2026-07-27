using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    public partial class NodeParamFormMatchTemplate : FormBase, INodeParamForm
    {
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
        /// <summary>
        /// native 模板匹配会话缓存，复用模板学习结果以对齐 Fastest_Image_Pattern_Matching Demo 的执行方式。
        /// </summary>
        private readonly FastTemplateMatcher.MatchSession templateMatchSession = new FastTemplateMatcher.MatchSession();

        public NodeParamFormMatchTemplate(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
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
            ClearSearchRegionEditRoi();
        }

        /// <summary>
        /// 初始化订阅节点。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 刷新输入图像。
        /// </summary>
        private void button3_Click(object sender, EventArgs e)
        {
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
            ClearSearchRegionEditRoi();
        }

        private void ClearSearchRegionEditRoi()
        {
            if (showImageControlSource == null)
                return;

            showImageControlSource.ClearDynamicRoi();
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

            EnsureSearchRegionFields();
            searchRegionConfirmed = false;
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
                else if (!HasSearchRegionValues())
                {
                    EnsureSearchRegionFields();
                    searchRegionConfirmed = false;
                    showImageControlSource.ClearDynamicRoi();
                }
            }

            UpdateSearchRegionControls();
        }

        private void buttonResetRoi_Click(object sender, EventArgs e)
        {
            try
            {
                if (src == null)
                    throw new InvalidOperationException("源图像不能为空，请先刷新或订阅图像。");

                allSearch = false;
                if (checkBoxAllSearch.Checked)
                    checkBoxAllSearch.Checked = false;
                ResetSearchRegion();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message);
            }
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
            bool hasEditRoi = showImageControlSource != null && showImageControlSource.GetAllDynamicRects().Count > 0;
            buttonConfirmSearchRoi.Enabled = src != null && !allSearch && hasEditRoi;

            if (allSearch)
            {
                labelSearchStatus.Text = "当前：整图搜索";
            }
            else if (hasEditRoi)
            {
                labelSearchStatus.Text = "搜索区域编辑中，请点击“确定ROI”。";
            }
            else if (searchRegionConfirmed && HasSearchRegionValues())
            {
                labelSearchStatus.Text = $"已确定：X {searchRegionCenterX:F1}, Y {searchRegionCenterY:F1}, W {searchRegionWidth:F1}, H {searchRegionHeight:F1}";
            }
            else
            {
                labelSearchStatus.Text = "未绘制搜索区域，请点击“绘制搜索区”。";
            }
        }

        /// <summary>
        /// 点击执行模板匹配。
        /// </summary>
        private async void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                SetSingleRunBusy(true);
                var result = await MatchTemplateAsync();
                PublishMatchResult(result);
                UpdateRunStatus(result);
                if (result.Matches.Count == 0)
                    MessageBoxTD.Show("未找到模板匹配目标。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模板匹配异常，原因：{GetDisplayExceptionMessage(ex)}");
            }
            finally
            {
                SetSingleRunBusy(false);
            }
        }

        /// <summary>
        /// 模板匹配。show=false 时输出干净原图，结果由 DisplayResult 叠加层显示。
        /// </summary>
        public FastTemplateMatchResult MatchTemplate(bool show = true)
        {
            MatchTemplateExecutionContext context = null;
            try
            {
                context = CreateMatchTemplateContext(show);
                var result = ExecuteMatchTemplateContext(context);
                ApplyMatchTemplateResult(result, show);
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("模板匹配失败。", ex);
            }
            finally
            {
                context?.Dispose();
            }
        }

        /// <summary>
        /// 异步执行模板匹配，避免 WinForms 执行按钮阻塞界面消息循环。
        /// </summary>
        private async Task<FastTemplateMatchResult> MatchTemplateAsync(bool show = true)
        {
            MatchTemplateExecutionContext context = null;
            try
            {
                context = CreateMatchTemplateContext(show);
                var result = await Task.Run(() => ExecuteMatchTemplateContext(context));
                ApplyMatchTemplateResult(result, show);
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("模板匹配失败。", ex);
            }
            finally
            {
                context?.Dispose();
            }
        }

        /// <summary>
        /// 创建一次匹配所需的图像和参数快照，保证后台线程不直接访问界面对象。
        /// </summary>
        private MatchTemplateExecutionContext CreateMatchTemplateContext(bool show)
        {
            ReadFormValues();
            EnsureTemplateLoaded();
            if (src == null)
                throw new ArgumentException("源图像不能为空。");
            if (templateImage == null)
                throw new ArgumentException("模板图像不能为空。");

            Bitmap sourceImage = null;
            Bitmap templateSnapshot = null;
            Bitmap eraseMaskSnapshot = null;
            Bitmap matchImage = null;
            try
            {
                sourceImage = (Bitmap)src.Clone();
                templateSnapshot = (Bitmap)templateImage.Clone();
                eraseMaskSnapshot = templateEraseMask == null ? null : (Bitmap)templateEraseMask.Clone();
                Rectangle searchBounds = GetSearchBounds(sourceImage.Size);
                matchImage = allSearch
                    ? (Bitmap)sourceImage.Clone()
                    : sourceImage.Clone(searchBounds, PixelFormat.Format24bppRgb);

                var context = new MatchTemplateExecutionContext
                {
                    RuntimeParam = BuildRuntimeParam(false, false),
                    SourceImage = sourceImage,
                    MatchImage = matchImage,
                    TemplateImage = templateSnapshot,
                    TemplateEraseMask = eraseMaskSnapshot,
                    SearchBounds = searchBounds,
                    IsAllSearch = allSearch,
                    ShowMatchBox = showOutRegionStatus,
                    ShowOutline = showOutlineStatus,
                    ShowPreview = show
                };

                sourceImage = null;
                templateSnapshot = null;
                eraseMaskSnapshot = null;
                matchImage = null;
                return context;
            }
            finally
            {
                matchImage?.Dispose();
                sourceImage?.Dispose();
                templateSnapshot?.Dispose();
                eraseMaskSnapshot?.Dispose();
            }
        }

        /// <summary>
        /// 执行 native 匹配并生成输出预览图像。
        /// </summary>
        private FastTemplateMatchResult ExecuteMatchTemplateContext(MatchTemplateExecutionContext context)
        {
            var result = templateMatchSession.Match(context.RuntimeParam, context.MatchImage, context.TemplateImage, context.TemplateEraseMask);
            if (!context.IsAllSearch)
                OffsetResult(result, context.SearchBounds.X, context.SearchBounds.Y);

            result.OutputBitmap = context.ShowPreview
                ? FastTemplateMatcher.DrawPreview(context.SourceImage, result, context.ShowMatchBox, context.ShowOutline)
                : (Bitmap)context.SourceImage.Clone();
            return result;
        }

        /// <summary>
        /// 将后台匹配生成的预览图显示到界面。
        /// </summary>
        private void ApplyMatchTemplateResult(FastTemplateMatchResult result, bool show)
        {
            if (show)
                showImageControlSource.SetImage(result.OutputBitmap);
        }

        /// <summary>
        /// 设置单次执行按钮忙碌状态。
        /// </summary>
        private void SetSingleRunBusy(bool busy)
        {
            buttonRun.Enabled = !busy;
            buttonRun.Text = busy ? "执行中..." : "执行";
            labelResultSummary.Text = busy ? "模板匹配执行中..." : labelResultSummary.Text;
        }

        /// <summary>
        /// 获取更适合界面显示的异常信息。
        /// </summary>
        private static string GetDisplayExceptionMessage(Exception ex)
        {
            return ex.InnerException == null ? ex.Message : ex.InnerException.Message;
        }

        /// <summary>
        /// 一次模板匹配执行所需的线程安全快照。
        /// </summary>
        private sealed class MatchTemplateExecutionContext : IDisposable
        {
            /// <summary>
            /// 本次执行使用的运行参数。
            /// </summary>
            public NodeParamMatchTemplate RuntimeParam { get; set; }

            /// <summary>
            /// 本次执行使用的源图像快照。
            /// </summary>
            public Bitmap SourceImage { get; set; }

            /// <summary>
            /// 本次执行实际送入 native 匹配的图像。
            /// </summary>
            public Bitmap MatchImage { get; set; }

            /// <summary>
            /// 本次执行使用的模板图像快照。
            /// </summary>
            public Bitmap TemplateImage { get; set; }

            /// <summary>
            /// 本次执行使用的模板涂抹蒙版快照。
            /// </summary>
            public Bitmap TemplateEraseMask { get; set; }

            /// <summary>
            /// 非整图搜索时的裁剪区域。
            /// </summary>
            public Rectangle SearchBounds { get; set; }

            /// <summary>
            /// 是否为整图搜索。
            /// </summary>
            public bool IsAllSearch { get; set; }

            /// <summary>
            /// 是否在预览图中绘制匹配外框。
            /// </summary>
            public bool ShowMatchBox { get; set; }

            /// <summary>
            /// 是否在预览图中绘制模板轮廓。
            /// </summary>
            public bool ShowOutline { get; set; }

            /// <summary>
            /// 是否生成并显示预览图。
            /// </summary>
            public bool ShowPreview { get; set; }

            /// <summary>
            /// 释放本次执行的图像快照。
            /// </summary>
            public void Dispose()
            {
                MatchImage?.Dispose();
                MatchImage = null;
                SourceImage?.Dispose();
                SourceImage = null;
                TemplateImage?.Dispose();
                TemplateImage = null;
                TemplateEraseMask?.Dispose();
                TemplateEraseMask = null;
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

        private NodeParamMatchTemplate BuildRuntimeParam(bool includeTemplateImage = true, bool includeTemplateEraseMask = true)
        {
            return new NodeParamMatchTemplate
            {
                TemplateFileName = string.Empty,
                TemplateImageBytes = includeTemplateImage ? FastTemplateMatcher.BitmapToPngBytes(templateImage) : null,
                TemplateEraseMaskBytes = includeTemplateEraseMask ? FastTemplateMatcher.BitmapToPngBytes(templateEraseMask) : null,
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

        private async void continuousRunTimer_Tick(object sender, EventArgs e)
        {
            if (continuousRunBusy)
                return;

            try
            {
                continuousRunBusy = true;
                var result = await MatchTemplateAsync();
                PublishMatchResult(result);
                UpdateRunStatus(result);
            }
            catch (Exception ex)
            {
                continuousRunTimer.Stop();
                buttonContinuousRun.Text = "连续执行";
                MessageBoxTD.Show($"连续匹配停止：{GetDisplayExceptionMessage(ex)}");
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
            if (rect != null)
            {
                if (!searchRegionConfirmed)
                    throw new Exception("请先点击“确定ROI”，确认搜索区域后再运行。");

                if (IsSearchRegionChanged(rect))
                    throw new Exception("搜索区域已调整，请重新点击“确定ROI”。");

                return;
            }

            if (!searchRegionConfirmed || !HasSearchRegionValues())
                throw new Exception("请先绘制并确认搜索区域。");
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
            showImageControlSource.ClearDynamicRoi();
            UpdateSearchRegionControls();
        }

        private bool HasSearchRegionValues()
        {
            return searchRegionWidth > 0 && searchRegionHeight > 0;
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

        private async void buttonCreateTemplate_Click(object sender, EventArgs e)
        {
            try
            {
                await EnsureSourceImageReadyForTemplateAsync();
                OpenTemplateDialog(null, "当前ROI模板");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"创建模板失败，原因：{ex.Message}");
            }
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

                    Bitmap editedTemplate = form.GetTemplateImage();
                    Bitmap eraseMask = form.GetTemplateEraseMask();
                    try
                    {
                        SetTemplateImage(editedTemplate, form.TemplateSourceName, true, eraseMask);
                    }
                    finally
                    {
                        editedTemplate?.Dispose();
                        eraseMask?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模板编辑失败，原因：{ex.Message}");
            }
        }

        private System.Threading.Tasks.Task EnsureSourceImageReadyForTemplateAsync()
        {
            UpdataImage();

            if (src == null)
                throw new InvalidOperationException("源图像不能为空，请先运行流程或主页运行按钮更新订阅图像，再点击刷新图像。");

            showImageControlSource.SetImage((Bitmap)src.Clone());
            showImageControlSource.ShowFit();
            showImageControlSource.Refresh();
            ClearSearchRegionEditRoi();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void PublishMatchResult(FastTemplateMatchResult result)
        {
            if (node is NodeMatchTemplate matchNode)
            {
                var displayParam = new NodeParamMatchTemplate
                {
                    ShowOutRegionStatus = showOutRegionStatus,
                    ShowOutlineStatus = showOutlineStatus
                };
                matchNode.PublishPreviewResult(result, displayParam);
            }
        }

        private void buttonRestoreTemplate_Click(object sender, EventArgs e)
        {
            if (templateOriginalImage == null)
                return;

            using (var restored = (Bitmap)templateOriginalImage.Clone())
                SetTemplateImage(restored, templateSourceName, false);
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
            templateMatchSession.InvalidateTemplate();
            UpdateTemplatePreview();
        }

        private void LoadTemplateImage(string fileName)
        {
            using (var raw = new Bitmap(fileName))
            using (var template = FastTemplateMatcher.To24Bpp(raw))
            {
                SetTemplateImage(template, Path.GetFileName(fileName), true);
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
                    Bitmap loadedTemplate = null;
                    Bitmap eraseMask = null;
                    try
                    {
                        loadedTemplate = FastTemplateMatcher.PngBytesToBitmap(param.TemplateImageBytes);
                        eraseMask = param.TemplateEraseMaskBytes == null || param.TemplateEraseMaskBytes.Length == 0
                            ? null
                            : FastTemplateMatcher.PngBytesToBitmap(param.TemplateEraseMaskBytes);
                        SetTemplateImage(loadedTemplate, param.TemplateSourceName, true, eraseMask);
                    }
                    finally
                    {
                        loadedTemplate?.Dispose();
                        eraseMask?.Dispose();
                    }
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
            Bitmap newTemplate = image == null ? null : FastTemplateMatcher.To24Bpp(image);
            Bitmap newEraseMask = null;
            Bitmap newOriginal = null;

            try
            {
                newEraseMask = NormalizeTemplateEraseMask(eraseMask, newTemplate?.Size ?? Size.Empty);
                if (resetOriginal)
                    newOriginal = newTemplate == null ? null : (Bitmap)newTemplate.Clone();

                templateImage?.Dispose();
                templateImage = newTemplate;
                newTemplate = null;

                templateEraseMask?.Dispose();
                templateEraseMask = newEraseMask;
                newEraseMask = null;

                templateSourceName = string.IsNullOrWhiteSpace(sourceName) ? "方案内嵌模板" : sourceName;

                if (resetOriginal)
                {
                    templateOriginalImage?.Dispose();
                    templateOriginalImage = newOriginal;
                    newOriginal = null;
                }
            }
            finally
            {
                newTemplate?.Dispose();
                newEraseMask?.Dispose();
                newOriginal?.Dispose();
            }

            templateMatchSession.InvalidateTemplate();
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
            string previewError = null;
            if (templateImage != null)
            {
                try
                {
                    var contour = FastTemplateMatcher.BuildTemplateContour(templateImage, templateEraseMask);
                    contourCount = contour.Count;
                    pictureBoxTemplate.Image = FastTemplateMatcher.DrawTemplateContourPreview(templateImage, contour);
                }
                catch (Exception ex)
                {
                    previewError = ex.Message;
                    try
                    {
                        pictureBoxTemplate.Image = FastTemplateMatcher.To24Bpp(templateImage);
                    }
                    catch
                    {
                        pictureBoxTemplate.Image = null;
                    }
                }
            }

            RefreshTemplateStatus(contourCount);
            if (!string.IsNullOrWhiteSpace(previewError))
                labelTemplatePreview.Text = "模板已还原，轮廓预览失败";
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
            templateMatchSession.Dispose();
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
