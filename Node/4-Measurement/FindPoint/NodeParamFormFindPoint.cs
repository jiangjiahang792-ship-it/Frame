using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
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
        /// Binds subscription controls to the owning node.
        /// </summary>
        /// <param name="node">Node that owns this parameter form.</param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.Init(node);
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
        /// <param name="output">Clean preview image, with overlays stored separately in result metadata.</param>
        /// <returns>Measurement result containing ordered contours and flattened points.</returns>
        internal FindPointMeasureResult ExecuteMeasure(NodeParamFindPoint param, CancellationToken token, out Mat output)
        {
            using (Mat source = GetInputMat())
            {
                NodeParamFindPoint runtimeParam = BuildRuntimeParam(param);
                output = MeasurementNodeHelper.EnsureBgr(source);
                return FindPointAlgorithm.Execute(source, runtimeParam, token);
            }
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
        /// Reads the subscribed output image as an OpenCV matrix.
        /// </summary>
        /// <returns>A cloned input image matrix.</returns>
        private Mat GetInputMat()
        {
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetFirstMat(outputImage);
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
                if (showImageControlPreview.Image == null)
                {
                    using (Mat input = GetInputMat())
                    {
                        SetPreview(input);
                        showImageControlPreview.ShowFit();
                    }
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
                using (Mat input = GetInputMat())
                {
                    SetPreview(input);
                    showImageControlPreview.ShowFit();
                    RedrawConfirmedRegions();
                }
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
                Mat output;
                FindPointMeasureResult result = ExecuteMeasure((NodeParamFindPoint)Params, CancellationToken.None, out output);
                using (output)
                {
                    SetPreview(output);
                    showImageControlPreview.SetDisplayResult(NodeFindPoint.BuildDisplayResult(result));
                }

                labelStatus.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "点数：{0}，轮廓：{1}，耗时：{2:F1} ms",
                    result.PointCount,
                    result.ContourCount,
                    result.AlgorithmMs);
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

            foreach (ShowImageControl.RoiPolygon polygon in polygons)
            {
                if (polygon.Points == null || polygon.Points.Count < 3)
                    continue;

                List<PointF> region = polygon.Points.Select(point => new PointF(point.X, point.Y)).ToList();
                PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(true);
                if (correctionInfo != null)
                    region = GeometryMeasurementAlgorithm.InverseTransformPoints(region, correctionInfo);

                confirmedRegions.Add(region);
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
            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
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

        /// <summary>
        /// Builds runtime parameters and applies current position correction to saved baseline ROI regions.
        /// </summary>
        /// <param name="param">Saved baseline parameters.</param>
        /// <returns>Runtime parameters in current image coordinates.</returns>
        private NodeParamFindPoint BuildRuntimeParam(NodeParamFindPoint param)
        {
            if (param == null || !param.UsePositionCorrection)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);

            return new NodeParamFindPoint
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                Regions = TransformRegions(param.Regions, correctionInfo),
                LowThreshold = param.LowThreshold,
                HighThreshold = param.HighThreshold,
                BlurSize = param.BlurSize,
                MinContourPoints = param.MinContourPoints,
                SampleStep = param.SampleStep,
                MaxPointCount = param.MaxPointCount
            };
        }

        /// <summary>
        /// Reads current editing correction information from the position-correction subscription.
        /// </summary>
        /// <param name="require">Whether invalid correction should throw.</param>
        /// <returns>Valid correction info, or null when correction is disabled or optional.</returns>
        private PositionCorrectionInfo GetEditingCorrectionInfo(bool require)
        {
            if (!checkBoxUsePositionCorrection.Checked)
                return null;

            if (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2()))
            {
                if (require)
                    throw new Exception("位置修正订阅为空。");
                return null;
            }

            try
            {
                PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
                PositionCorrectionHelper.EnsureValid(correctionInfo);
                return correctionInfo;
            }
            catch
            {
                if (require)
                    throw;
                return null;
            }
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
}
