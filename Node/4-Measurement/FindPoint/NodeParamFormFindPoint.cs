using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.FindPoint
{
    /// <summary>
    /// FindPoint node parameter form. It owns image subscription, ROI confirmation, run parameters, and preview execution.
    /// </summary>
    public partial class NodeParamFormFindPoint : FormBase, INodeParamForm
    {
        /// <summary>
        /// The node that owns this parameter form, used for subscription filtering and preview result publishing logs.
        /// </summary>
        private readonly NodeBase node;

        /// <summary>
        /// 负责搜索区域逐点变换和编辑区域目标归属判断。
        /// </summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();

        /// <summary>
        /// Cached confirmed ROI regions. Runtime reads a copy through <see cref="SaveParams"/>.
        /// </summary>
        private readonly List<List<PointF>> confirmedRegions = new List<List<PointF>>();

        /// <summary>
        /// Initializes a FindPoint parameter form.
        /// </summary>
        /// <param name="process">Current process. Reserved for compatibility with other measurement forms.</param>
        /// <param name="node">Node that owns this form.</param>
        public NodeParamFormFindPoint(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeDefaults();
        }

        /// <summary>
        /// Persisted node parameters.
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 最近一次找点运行的分段耗时信息。
        /// </summary>
        internal FindPointTimingInfo LastTimingInfo { get; private set; }

        /// <summary>
        /// Binds subscription controls to the owning node.
        /// </summary>
        /// <param name="node">Node that owns this parameter form.</param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
            nodeSubscriptionPositionCorrection.Init(node);
        }

        /// <summary>
        /// Restores saved parameters back to the WinForms controls.
        /// </summary>
        public void SetParam2Form()
        {
            var param = Params as NodeParamFindPoint;
            if (param == null)
                return;

            nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
            checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
            nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
            textBoxLowThreshold.Text = param.LowThreshold.ToString(CultureInfo.InvariantCulture);
            textBoxHighThreshold.Text = param.HighThreshold.ToString(CultureInfo.InvariantCulture);
            textBoxBlurSize.Text = param.BlurSize.ToString(CultureInfo.InvariantCulture);
            textBoxMinContourPoints.Text = param.MinContourPoints.ToString(CultureInfo.InvariantCulture);
            textBoxSampleStep.Text = param.SampleStep.ToString(CultureInfo.InvariantCulture);
            textBoxMaxPointCount.Text = param.MaxPointCount.ToString(CultureInfo.InvariantCulture);

            confirmedRegions.Clear();
            if (param.Regions != null)
                confirmedRegions.AddRange(CloneRegions(param.Regions));

            RefreshRegionList();
            UpdatePositionCorrectionEnabled();
            ClearEditingRoi();
        }

        /// <summary>
        /// Runs the edge point extraction algorithm from the current subscribed image and saved parameters.
        /// </summary>
        /// <param name="param">Runtime parameters.</param>
        /// <param name="token">Cancellation token from process runtime.</param>
        /// <returns>Measurement result containing ordered contours and flattened points.</returns>
        internal List<FindPointTargetResult> ExecuteMeasures(NodeParamFindPoint param, CancellationToken token)
        {
            Stopwatch executeMeasureWatch = Stopwatch.StartNew();
            LastTimingInfo = new FindPointTimingInfo();
            bool disposeGrayAfterUse = false;
            Mat gray = null;
            try
            {
                IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
                if (corrections.Count == 0)
                    return new List<FindPointTargetResult>();

                gray = GetInputGrayMat(out disposeGrayAfterUse);
                List<FindPointTargetResult> items = MultiTargetMeasurementRunner.Run(
                    corrections,
                    token,
                    correction =>
                    {
                        Stopwatch runtimeParamWatch = Stopwatch.StartNew();
                        NodeParamFindPoint runtimeParam = BuildRuntimeParam(param, correction);
                        runtimeParamWatch.Stop();
                        LastTimingInfo.RuntimeParamMs += runtimeParamWatch.Elapsed.TotalMilliseconds;
                        return ExecuteOne(gray, runtimeParam, correction, token);
                    },
                    CreateFailure);

                LastTimingInfo.AlgorithmMs = items.Sum(item => item.AlgorithmMs);
                LastTimingInfo.ProcessedRegionCount = items.Sum(item => item.ProcessedRegionCount);
                LastTimingInfo.ProcessedPixelCount = items.Sum(item => item.ProcessedPixelCount);
                return items;
            }
            finally
            {
                executeMeasureWatch.Stop();
                if (LastTimingInfo != null)
                    LastTimingInfo.ExecuteMeasureMs = executeMeasureWatch.Elapsed.TotalMilliseconds;

                if (disposeGrayAfterUse)
                    gray?.Dispose();
            }
        }

        /// <summary>读取全部位置修正信息；未启用修正时返回一个恒等项。</summary>
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamFindPoint param)
        {
            if (param == null)
                throw new Exception("找点参数为空。");
            if (!param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };
            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>为单个模板目标构造逐点仿射变换后的找点参数。</summary>
        private NodeParamFindPoint BuildRuntimeParam(NodeParamFindPoint param, PositionCorrectionInfo correction)
        {
            List<List<PointF>> regions = param.Regions == null
                ? new List<List<PointF>>()
                : param.Regions
                    .Where(region => region != null && region.Count >= 3)
                    .Select(region => param.UsePositionCorrection
                        ? _transformService.TransformPoints(region, correction)
                        : region.Select(point => new PointF(point.X, point.Y)).ToList())
                    .ToList();
            return new NodeParamFindPoint
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                Regions = regions,
                LowThreshold = param.LowThreshold,
                HighThreshold = param.HighThreshold,
                BlurSize = param.BlurSize,
                MinContourPoints = param.MinContourPoints,
                SampleStep = param.SampleStep,
                MaxPointCount = param.MaxPointCount
            };
        }

        /// <summary>执行单个模板目标的找点算法并构造强类型目标项。</summary>
        private FindPointTargetResult ExecuteOne(Mat gray, NodeParamFindPoint runtimeParam, PositionCorrectionInfo correction, CancellationToken token)
        {
            FindPointMeasureResult measure = FindPointAlgorithm.Execute(gray, runtimeParam, token);
            if (measure == null)
                throw new Exception("找点算法没有返回结果。");
            if (!measure.Success)
            {
                return new FindPointTargetResult
                {
                    IsOk = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(measure.Message) ? "未找到有效边缘点。" : measure.Message,
                    Message = string.IsNullOrWhiteSpace(measure.Message) ? "未找到有效边缘点。" : measure.Message,
                    Regions = CloneRegions(runtimeParam.Regions),
                    PointCount = 0,
                    ContourCount = 0,
                    CenterX = 0,
                    CenterY = 0,
                    AlgorithmMs = 0,
                    ProcessedRegionCount = 0,
                    ProcessedPixelCount = 0
                };
            }

            List<PointF> points = MeasurementResultRounder.RoundPoints(measure.Points);
            return new FindPointTargetResult
            {
                IsOk = true,
                PointCount = measure.PointCount,
                ContourCount = measure.ContourCount,
                CenterX = points.Count == 0 ? 0 : MeasurementResultRounder.Round(points.Average(point => point.X)),
                CenterY = points.Count == 0 ? 0 : MeasurementResultRounder.Round(points.Average(point => point.Y)),
                Points = points,
                RegionPoints = MeasurementResultRounder.RoundPoints(measure.PrimaryContour),
                Contours = MeasurementResultRounder.RoundPointGroups(measure.Contours),
                Regions = CloneRegions(measure.Regions),
                Message = measure.Message,
                AlgorithmMs = MeasurementResultRounder.Round(measure.AlgorithmMs),
                ProcessedRegionCount = measure.ProcessedRegionCount,
                ProcessedPixelCount = measure.ProcessedPixelCount
            };
        }

        /// <summary>创建保留目标顺序且所有数值为零的找点失败项。</summary>
        private FindPointTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            string message = exception == null ? "找点失败。" : exception.Message;
            return new FindPointTargetResult
            {
                IsOk = false,
                ErrorMessage = message,
                Message = message,
                PointCount = 0,
                ContourCount = 0,
                CenterX = 0,
                CenterY = 0,
                AlgorithmMs = 0,
                ProcessedRegionCount = 0,
                ProcessedPixelCount = 0
            };
        }

        /// <summary>创建不改变坐标的单目标修正项。</summary>
        private static PositionCorrectionInfo CreateIdentityCorrection()
        {
            return new PositionCorrectionInfo { TargetIndex = 1, IsValid = true, BaseScaleX = 1, BaseScaleY = 1, CurrentScaleX = 1, CurrentScaleY = 1 };
        }

        /// <summary>
        /// Applies default textbox values and a stable preview state.
        /// </summary>
        private void InitializeDefaults()
        {
            textBoxLowThreshold.Text = "40";
            textBoxHighThreshold.Text = "120";
            textBoxBlurSize.Text = "3";
            textBoxMinContourPoints.Text = "10";
            textBoxSampleStep.Text = "1";
            textBoxMaxPointCount.Text = "5000";
            RefreshRegionList();
        }

        /// <summary>
        /// Reads the subscribed output image as a read-only grayscale OpenCV matrix.
        /// </summary>
        private Mat GetInputGrayMat(out bool disposeAfterUse)
        {
            FindPointTimingInfo timing = LastTimingInfo ?? new FindPointTimingInfo();
            LastTimingInfo = timing;

            Stopwatch subscriptionWatch = Stopwatch.StartNew();
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            subscriptionWatch.Stop();
            timing.SubscriptionReadMs = subscriptionWatch.Elapsed.TotalMilliseconds;

            Stopwatch grayAcquireWatch = Stopwatch.StartNew();
            Mat gray = GetReadOnlyGrayMat(outputImage, timing, out disposeAfterUse);
            grayAcquireWatch.Stop();
            timing.GrayAcquireMs = grayAcquireWatch.Elapsed.TotalMilliseconds;
            timing.TemporaryGrayCreated = disposeAfterUse;
            return gray;
        }

        /// <summary>
        /// 获取只读灰度图并记录来源；优先复用图像源的 GrayImg，只有缺失时才临时转换。
        /// </summary>
        private static Mat GetReadOnlyGrayMat(OutputImage outputImage, FindPointTimingInfo timing, out bool disposeAfterUse)
        {
            disposeAfterUse = false;
            if (outputImage == null)
                throw new Exception("订阅图像为空。");

            if (outputImage.GrayImg != null && !outputImage.GrayImg.Empty())
                return CaptureInputInfo(outputImage.GrayImg, "GrayImg", timing);

            Mat source = GetReadOnlyPreviewMat(outputImage, timing);
            if (source.Channels() == 1)
                return CaptureInputInfo(source, timing.InputSource, timing);

            disposeAfterUse = true;
            Mat gray = CaliperMeasurementAlgorithm.ToGray(source);
            return CaptureInputInfo(gray, "运行时灰度转换(" + timing.InputSource + ")", timing);
        }

        /// <summary>
        /// 选择上游输出中的第一张有效预览图引用，并记录图像来源。
        /// </summary>
        private static Mat GetReadOnlyPreviewMat(OutputImage outputImage, FindPointTimingInfo timing)
        {
            if (outputImage == null)
                throw new Exception("订阅图像为空。");

            if (outputImage.Bitmaps != null && outputImage.Bitmaps.Count > 0 && outputImage.Bitmaps[0] != null && !outputImage.Bitmaps[0].Empty())
                return CaptureInputInfo(outputImage.Bitmaps[0], "Bitmaps[0]", timing);

            if (outputImage.SrcImg != null && !outputImage.SrcImg.Empty())
                return CaptureInputInfo(outputImage.SrcImg, "SrcImg", timing);

            if (outputImage.GrayImg != null && !outputImage.GrayImg.Empty())
                return CaptureInputInfo(outputImage.GrayImg, "GrayImg", timing);

            throw new Exception("订阅节点没有输出有效图像。");
        }

        /// <summary>
        /// 记录输入算法的图像基础信息后返回原始 Mat 引用，找点算法只读使用。
        /// </summary>
        private static Mat CaptureInputInfo(Mat source, string inputSource, FindPointTimingInfo timing)
        {
            timing.InputSource = inputSource;
            timing.InputWidth = source.Width;
            timing.InputHeight = source.Height;
            timing.InputChannels = source.Channels();
            timing.InputBytes = source.Total() * source.ElemSize();
            return source;
        }

        /// <summary>
        /// Reads the subscribed output image as a read-only preview matrix.
        /// </summary>
        private Mat GetPreviewMat()
        {
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetReadOnlyPreviewMat(outputImage);
        }

        /// <summary>
        /// Saves UI values into <see cref="Params"/> after validation.
        /// </summary>
        /// <returns>True when parameters are valid and saved.</returns>
        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                    string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                {
                    throw new Exception("请选择输入图像。");
                }

                if (checkBoxUsePositionCorrection.Checked &&
                    (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                     string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2())))
                {
                    throw new Exception("请选择位置修正信息。");
                }

                TryReadDynamicRoisToConfirmedRegions();
                if (confirmedRegions.Count == 0)
                    throw new Exception("请至少绘制并确认一个区域。");

                Params = new NodeParamFindPoint
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    Regions = CloneRegions(confirmedRegions),
                    LowThreshold = ParseInt(textBoxLowThreshold, "低阈值", 0, 255),
                    HighThreshold = ParseInt(textBoxHighThreshold, "高阈值", 1, 255),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核", 1, 999),
                    MinContourPoints = ParseInt(textBoxMinContourPoints, "最小轮廓点数", 1, int.MaxValue),
                    SampleStep = ParseInt(textBoxSampleStep, "采样步长", 1, int.MaxValue),
                    MaxPointCount = ParseInt(textBoxMaxPointCount, "最大点数", 0, int.MaxValue)
                };

                var param = (NodeParamFindPoint)Params;
                if (param.LowThreshold >= param.HighThreshold)
                    throw new Exception("低阈值必须小于高阈值。");

                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("找点参数设置异常：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Parses an integer textbox and enforces an inclusive range.
        /// </summary>
        /// <param name="textBox">TextBox that owns the value.</param>
        /// <param name="name">Field name used in error messages.</param>
        /// <param name="min">Inclusive minimum.</param>
        /// <param name="max">Inclusive maximum.</param>
        /// <returns>Validated integer value.</returns>
        private static int ParseInt(TextBox textBox, string name, int min, int max)
        {
            int value;
            if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new Exception(name + "不是有效整数。");
            if (value < min || value > max)
                throw new Exception(name + "超出有效范围。");
            return value;
        }

        /// <summary>
        /// Adds a default rectangular polygon ROI to the preview for fast editing.
        /// </summary>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            try
            {
                showImageControlPreview.SetDisplayResult(null);
                if (showImageControlPreview.Image == null)
                {
                    SetPreview(GetPreviewMat());
                    showImageControlPreview.ShowFit();
                }

                RectangleF rect = BuildDefaultRoiRect();
                showImageControlPreview.AddDynamicPolygon(
                    new List<PointF>
                    {
                        new PointF(rect.Left, rect.Top),
                        new PointF(rect.Right, rect.Top),
                        new PointF(rect.Right, rect.Bottom),
                        new PointF(rect.Left, rect.Bottom)
                    },
                    Color.DodgerBlue,
                    "区域");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("绘制区域失败：" + ex.Message);
            }
        }

        /// <summary>
        /// Confirms all dynamic polygon ROIs and stores them as runtime regions.
        /// </summary>
        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            try
            {
                TryReadDynamicRoisToConfirmedRegions();
                ClearEditingRoi();
                RefreshRegionList();
                RedrawConfirmedRegions();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("确认区域失败：" + ex.Message);
            }
        }

        /// <summary>
        /// Clears confirmed and editing ROI regions.
        /// </summary>
        private void buttonClearRoi_Click(object sender, EventArgs e)
        {
            confirmedRegions.Clear();
            ClearEditingRoi();
            RefreshRegionList();
            showImageControlPreview.SetDisplayResult(null);
        }

        /// <summary>
        /// Refreshes the preview image from the subscribed upstream image output.
        /// </summary>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                SetPreview(GetPreviewMat());
                showImageControlPreview.ShowFit();
                RedrawConfirmedRegions();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("刷新图像失败：" + ex.Message);
            }
        }

        /// <summary>
        /// Executes a preview run and draws result overlays in the preview control.
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                List<FindPointTargetResult> items = ExecuteMeasures((NodeParamFindPoint)Params, CancellationToken.None);
                SetPreview(GetPreviewMat());
                showImageControlPreview.SetDisplayResult(NodeFindPoint.BuildDisplayResult(items));

                labelStatus.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "目标：{0}，总点数：{1}，总体：{2}",
                    items.Count,
                    items.Sum(item => item.PointCount),
                    items.All(item => item.IsOk) ? "OK" : "NG");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("执行异常：" + ex.Message);
                if (node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})找点预览异常，原因：{ex.Message}", true);
            }
        }

        /// <summary>
        /// Saves parameters and closes the form.
        /// </summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
            {
                ClearEditingRoi();
                Hide();
            }
        }

        /// <summary>
        /// Converts and displays a preview image.
        /// </summary>
        /// <param name="image">OpenCV image to preview.</param>
        private void SetPreview(Mat image)
        {
            showImageControlPreview.SetImage(MeasurementNodeHelper.ToPreviewBitmap(image));
        }

        /// <summary>
        /// Reads currently edited polygon ROIs and appends them to the confirmed region list.
        /// </summary>
        private void TryReadDynamicRoisToConfirmedRegions()
        {
            List<ShowImageControl.RoiPolygon> polygons = showImageControlPreview.GetAllDynamicPolygons();
            if (polygons.Count == 0)
                return;

            List<List<PointF>> regions = polygons
                .Where(polygon => polygon.Points != null && polygon.Points.Count >= 3)
                .Select(polygon => polygon.Points.Select(point => new PointF(point.X, point.Y)).ToList())
                .ToList();
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(regions, true);
            foreach (List<PointF> region in regions)
            {
                List<PointF> normalized = correctionInfo == null
                    ? region
                    : _transformService.InverseTransformPoints(region, correctionInfo);
                confirmedRegions.Add(normalized);
            }

            ClearEditingRoi();
        }

        /// <summary>
        /// Clears dynamic editing shapes only; confirmed regions stay in memory.
        /// </summary>
        private void ClearEditingRoi()
        {
            showImageControlPreview.ClearDynamicRoi();
        }

        /// <summary>
        /// Updates the ROI count label from the confirmed region cache.
        /// </summary>
        private void RefreshRegionList()
        {
            labelRoiCount.Text = "区域数量：" + confirmedRegions.Count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Draws confirmed ROI regions as static overlays.
        /// </summary>
        private void RedrawConfirmedRegions()
        {
            var display = new TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult();
            PositionCorrectionInfo correctionInfo = GetDefaultEditingCorrectionInfo(false);
            foreach (List<PointF> region in confirmedRegions)
            {
                List<PointF> displayRegion = TransformRegion(region, correctionInfo);
                display.Contours.Add(new TDJS_Vision.Node._3_Detection.TDAI.ColorContour(displayRegion, Color.DodgerBlue)
                {
                    LineWidth = 1.2F
                });
            }

            showImageControlPreview.SetDisplayResult(display);
        }

        /// <summary>读取编辑状态使用的全部位置修正信息。</summary>
        private IReadOnlyList<PositionCorrectionInfo> GetEditingCorrections(bool require)
        {
            if (!checkBoxUsePositionCorrection.Checked)
                return null;

            if (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2()))
            {
                if (require)
                    throw new Exception("位置修正信息列表订阅为空。");
                return null;
            }

            try
            {
                List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
                if (corrections == null || corrections.Count == 0)
                    throw new Exception("位置修正信息列表为空。");
                for (int i = 0; i < corrections.Count; i++)
                    PositionCorrectionHelper.EnsureValid(corrections[i]);
                return corrections;
            }
            catch
            {
                if (require)
                    throw;
                return null;
            }
        }

        /// <summary>获取默认用于静态预览的第一目标修正信息。</summary>
        private PositionCorrectionInfo GetDefaultEditingCorrectionInfo(bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            return corrections == null || corrections.Count == 0 ? null : corrections[0];
        }

        /// <summary>根据整组编辑区域包围盒中心解析所属模板目标。</summary>
        private PositionCorrectionInfo ResolveEditingCorrection(IEnumerable<List<PointF>> regions, bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            if (corrections == null || corrections.Count == 0)
                return null;
            List<PointF> points = regions == null
                ? new List<PointF>()
                : regions.Where(region => region != null).SelectMany(region => region).ToList();
            if (points.Count == 0)
                throw new Exception("编辑区域没有有效顶点。");
            float minX = points.Min(point => point.X);
            float maxX = points.Max(point => point.X);
            float minY = points.Min(point => point.Y);
            float maxY = points.Max(point => point.Y);
            var center = new PointF((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            int index = _transformService.ResolveAnchorIndex(center, corrections);
            return corrections[index];
        }

        /// <summary>
        /// Enables or disables the position-correction subscription selector.
        /// </summary>
        private void UpdatePositionCorrectionEnabled()
        {
            if (nodeSubscriptionPositionCorrection != null)
                nodeSubscriptionPositionCorrection.Enabled = checkBoxUsePositionCorrection.Checked;
        }

        /// <summary>
        /// Handles the position-correction checkbox state and refreshes ROI display if needed.
        /// </summary>
        private void checkBoxUsePositionCorrection_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePositionCorrectionEnabled();
            RedrawConfirmedRegions();
        }

        /// <summary>
        /// Applies position correction to a single region for current-image display or runtime.
        /// </summary>
        /// <param name="region">Baseline region.</param>
        /// <param name="correctionInfo">Position correction info.</param>
        /// <returns>Transformed point list.</returns>
        private static List<PointF> TransformRegion(IEnumerable<PointF> region, PositionCorrectionInfo correctionInfo)
        {
            return GeometryMeasurementAlgorithm.TransformPoints(region, correctionInfo);
        }

        /// <summary>
        /// Applies position correction to all saved ROI regions.
        /// </summary>
        /// <param name="regions">Baseline regions.</param>
        /// <param name="correctionInfo">Position correction info.</param>
        /// <returns>Transformed region groups.</returns>
        private static List<List<PointF>> TransformRegions(IEnumerable<List<PointF>> regions, PositionCorrectionInfo correctionInfo)
        {
            if (regions == null)
                return new List<List<PointF>>();

            return regions
                .Where(region => region != null && region.Count >= 3)
                .Select(region => TransformRegion(region, correctionInfo))
                .ToList();
        }

        /// <summary>
        /// Builds a default ROI in the central area of the current image.
        /// </summary>
        /// <returns>Default rectangular image-space ROI.</returns>
        private RectangleF BuildDefaultRoiRect()
        {
            Image image = showImageControlPreview.Image;
            if (image == null)
                return new RectangleF(50, 50, 200, 160);

            float width = Math.Max(20, image.Width * 0.5F);
            float height = Math.Max(20, image.Height * 0.5F);
            float left = (image.Width - width) / 2F;
            float top = (image.Height - height) / 2F;
            return new RectangleF(left, top, width, height);
        }

        /// <summary>
        /// Creates a deep copy of ROI point groups to keep saved parameters isolated from UI mutations.
        /// </summary>
        /// <param name="regions">Source regions.</param>
        /// <returns>Deep-copied point groups.</returns>
        private static List<List<PointF>> CloneRegions(IEnumerable<List<PointF>> regions)
        {
            return regions
                .Where(region => region != null && region.Count >= 3)
                .Select(region => region.Select(point => new PointF(point.X, point.Y)).ToList())
                .ToList();
        }
    }

    /// <summary>
    /// 找点工具单次运行的分段耗时信息，用于确认灰度图复用和算法外开销。
    /// </summary>
    internal sealed class FindPointTimingInfo
    {
        /// <summary>
        /// 订阅读取上游图像结果耗时，单位毫秒。
        /// </summary>
        public double SubscriptionReadMs { get; set; }

        /// <summary>
        /// 获取只读灰度图引用或临时灰度图的耗时，单位毫秒。
        /// </summary>
        public double GrayAcquireMs { get; set; }

        /// <summary>
        /// 构建运行时参数和位置修正后的 ROI 耗时，单位毫秒。
        /// </summary>
        public double RuntimeParamMs { get; set; }

        /// <summary>
        /// 找点算法内部耗时，单位毫秒。
        /// </summary>
        public double AlgorithmMs { get; set; }

        /// <summary>
        /// ExecuteMeasure 方法整体耗时，单位毫秒。
        /// </summary>
        public double ExecuteMeasureMs { get; set; }

        /// <summary>
        /// 输入图像来源，例如 GrayImg、Bitmaps[0] 或运行时灰度转换来源。
        /// </summary>
        public string InputSource { get; set; } = string.Empty;

        /// <summary>
        /// 输入算法的图像宽度，单位像素。
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 输入算法的图像高度，单位像素。
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 输入算法的图像通道数。
        /// </summary>
        public int InputChannels { get; set; }

        /// <summary>
        /// 输入算法的图像估算字节数。
        /// </summary>
        public long InputBytes { get; set; }

        /// <summary>
        /// 本次运行是否因为上游没有 GrayImg 而临时创建灰度图。
        /// </summary>
        public bool TemporaryGrayCreated { get; set; }

        /// <summary>
        /// 本次算法实际处理的 ROI 区域数量。
        /// </summary>
        public int ProcessedRegionCount { get; set; }

        /// <summary>
        /// 本次算法实际处理的局部图像像素数。
        /// </summary>
        public long ProcessedPixelCount { get; set; }

        /// <summary>
        /// 本次算法实际处理的局部图像容量，单位百万像素。
        /// </summary>
        public double ProcessedMegaPixels
        {
            get { return ProcessedPixelCount <= 0 ? 0 : ProcessedPixelCount / 1000000.0; }
        }

        /// <summary>
        /// 输入算法的图像估算容量，单位 MB。
        /// </summary>
        public double InputMegaBytes
        {
            get { return InputBytes <= 0 ? 0 : InputBytes / 1024.0 / 1024.0; }
        }
    }
}
