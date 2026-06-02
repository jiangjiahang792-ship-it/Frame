using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TDJS_Vision.Node._4_Measurement.PointRegionDistance
{
    public partial class NodeParamFormPointRegionDistance : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        private bool _isSyncingRoi;
        private bool _roiVisible;

        public NodeParamFormPointRegionDistance(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeCombos();
            BindRoiRefreshEvents();
            nodeSubscriptionPoint.HideText2();
            nodeSubscriptionRegion.HideText2();
            UpdateSourceModeEnabled();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPoint.Init(node);
            nodeSubscriptionRegion.Init(node);
            nodeSubscriptionPositionCorrection.Init(node);
        }

        public void SetParam2Form()
        {
            var param = Params as NodeParamPointRegionDistance;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                radioButtonSubscribe.Checked = param.SourceMode == MeasurementDataSourceMode.Subscribe;
                radioButtonDraw.Checked = param.SourceMode != MeasurementDataSourceMode.Subscribe;
                nodeSubscriptionPoint.SetText(param.PointText1, param.PointText2);
                comboBoxPointRole.SelectedItem = param.PointRole;
                nodeSubscriptionRegion.SetText(param.RegionText1, param.RegionText2);
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                comboBoxMeasureMode.SelectedItem = param.MeasureMode;
                textBoxPointCenterX.Text = param.PointCenterX.ToString();
                textBoxPointCenterY.Text = param.PointCenterY.ToString();
                textBoxPointRadius.Text = param.PointRadius.ToString();
                textBoxRegionPoints.Text = FormatRegionPoints(param.RegionPoints);
                textBoxCaliperWidth.Text = param.CaliperWidth.ToString();
                textBoxCaliperHeight.Text = param.CaliperHeight.ToString();
                textBoxCount.Text = param.Count.ToString();
                textBoxEdgeStrength.Text = param.EdgeStrength.ToString();
                textBoxBlurSize.Text = param.BlurSize.ToString();
                comboBoxPolarity.SelectedItem = param.Polarity;
                comboBoxFindMode.SelectedItem = param.FindMode;
                comboBoxDirection.SelectedIndex = param.Direction == 1 ? 1 : 0;
            }
            finally
            {
                _isSyncingRoi = false;
            }

            UpdateSourceModeEnabled();
            ClearEditingRoi();
        }

        internal PointRegionDistanceMeasureResult ExecuteMeasure(NodeParamPointRegionDistance param)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PointRegionDistanceMeasureResult();
            PointF targetPoint;
            List<PointF> regionPoints;

            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
            {
                targetPoint = ReadSubscribedPoint(nodeSubscriptionPoint, param.PointRole);
                regionPoints = ReadSubscribedRegion(nodeSubscriptionRegion);
            }
            else
            {
                bool disposeGrayAfterUse;
                Mat gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    NodeParamPointRegionDistance runtimeParam = BuildRuntimeParam(param);
                    CaliperCircleMeasureResult pointResult = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(runtimeParam));
                    result.PointEdgePoints.AddRange(pointResult.EdgePoints);
                    if (!pointResult.Success)
                        return FinishFailed(result, stopwatch, "目标点圆卡尺找圆失败");

                    targetPoint = pointResult.Center;
                    regionPoints = runtimeParam.RegionPoints == null ? new List<PointF>() : runtimeParam.RegionPoints.ToList();
                }
                finally
                {
                    if (disposeGrayAfterUse)
                        gray?.Dispose();
                }
            }

            if (regionPoints == null || regionPoints.Count < 3)
                return FinishFailed(result, stopwatch, "区域点数不足");

            result.DistanceResult = GeometryMeasurementAlgorithm.CalculatePointRegionDistance(regionPoints, targetPoint, param.MeasureMode);
            result.Success = true;
            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private void InitializeCombos()
        {
            comboBoxPointRole.Items.Add(MeasurementPointRole.Auto);
            comboBoxPointRole.Items.Add(MeasurementPointRole.Center);
            comboBoxPointRole.Items.Add(MeasurementPointRole.StartPoint);
            comboBoxPointRole.Items.Add(MeasurementPointRole.EndPoint);
            comboBoxPointRole.SelectedItem = MeasurementPointRole.Auto;

            comboBoxMeasureMode.Items.Add(GeometryMeasureMode.SubPixel);
            comboBoxMeasureMode.Items.Add(GeometryMeasureMode.Pixel);
            comboBoxMeasureMode.SelectedItem = GeometryMeasureMode.SubPixel;

            comboBoxPolarity.Items.Add(CaliperEdgePolarity.Both);
            comboBoxPolarity.Items.Add(CaliperEdgePolarity.DarkToLight);
            comboBoxPolarity.Items.Add(CaliperEdgePolarity.LightToDark);
            comboBoxPolarity.SelectedItem = CaliperEdgePolarity.Both;

            comboBoxFindMode.Items.Add(CaliperEdgeFindMode.Best);
            comboBoxFindMode.Items.Add(CaliperEdgeFindMode.First);
            comboBoxFindMode.Items.Add(CaliperEdgeFindMode.Last);
            comboBoxFindMode.SelectedItem = CaliperEdgeFindMode.Best;

            comboBoxDirection.Items.Add("由内向外");
            comboBoxDirection.Items.Add("由外向内");
            comboBoxDirection.SelectedIndex = 0;
        }

        private void BindRoiRefreshEvents()
        {
            textBoxPointCenterX.TextChanged += OnRoiParamChanged;
            textBoxPointCenterY.TextChanged += OnRoiParamChanged;
            textBoxPointRadius.TextChanged += OnRoiParamChanged;
            textBoxRegionPoints.TextChanged += OnRoiParamChanged;
            textBoxCaliperWidth.TextChanged += OnRoiParamChanged;
            textBoxCaliperHeight.TextChanged += OnRoiParamChanged;
            textBoxCount.TextChanged += OnRoiParamChanged;
            comboBoxDirection.SelectedIndexChanged += OnRoiParamChanged;
            radioButtonSubscribe.CheckedChanged += SourceMode_CheckedChanged;
            radioButtonDraw.CheckedChanged += SourceMode_CheckedChanged;
        }

        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            if (!ReferenceEquals(sender, textBoxRegionPoints))
                SyncCurrentRoiToFieldsExcept(sender);
            if (_roiVisible)
                RefreshRoiFromFields();
        }

        private void SourceMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSourceModeEnabled();
        }

        private Mat GetInputGrayMat(out bool disposeAfterUse)
        {
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetReadOnlyGrayMat(outputImage, out disposeAfterUse);
        }

        /// <summary>
        /// 获取参数界面预览用的只读原图引用，不接管 Mat 生命周期。
        /// </summary>
        private Mat GetPreviewMat()
        {
            OutputImage outputImage = nodeSubscriptionImage.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetReadOnlyPreviewMat(outputImage);
        }

        private PointF ReadSubscribedPoint(NodeSubscription subscription, MeasurementPointRole role)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadPoint(sourceNode.Result, role, out PointF point))
                throw new Exception($"点订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用点结果。");

            return point;
        }

        private List<PointF> ReadSubscribedRegion(NodeSubscription subscription)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadRegion(sourceNode.Result, out List<PointF> regionPoints))
                throw new Exception($"区域订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用区域/轮廓结果。");

            return regionPoints;
        }

        private NodeParamPointRegionDistance BuildRuntimeParam(NodeParamPointRegionDistance param)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            PointF point = correctionInfo.TransformPoint(param.PointCenterX, param.PointCenterY);

            return new NodeParamPointRegionDistance
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                SourceMode = param.SourceMode,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                MeasureMode = param.MeasureMode,
                PointCenterX = point.X,
                PointCenterY = point.Y,
                PointRadius = param.PointRadius,
                RegionPoints = GeometryMeasurementAlgorithm.TransformPoints(param.RegionPoints, correctionInfo),
                CaliperWidth = param.CaliperWidth,
                CaliperHeight = param.CaliperHeight,
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize
            };
        }

        private static CaliperCircleParams BuildCircleParams(NodeParamPointRegionDistance param)
        {
            return new CaliperCircleParams
            {
                CenterX = param.PointCenterX,
                CenterY = param.PointCenterY,
                Radius = param.PointRadius,
                StartAngle = 0,
                EndAngle = 360,
                CaliperWidth = param.CaliperWidth,
                CaliperHeight = param.CaliperHeight,
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize
            };
        }

        private static PointRegionDistanceMeasureResult FinishFailed(PointRegionDistanceMeasureResult result, Stopwatch stopwatch, string message)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Message = message;
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                    throw new Exception("请选择输入图像。");

                MeasurementDataSourceMode sourceMode = radioButtonSubscribe.Checked ? MeasurementDataSourceMode.Subscribe : MeasurementDataSourceMode.Draw;
                List<PointF> regionPoints = ParseRegionPoints(textBoxRegionPoints.Text);
                if (sourceMode == MeasurementDataSourceMode.Subscribe)
                {
                    if (string.IsNullOrWhiteSpace(nodeSubscriptionPoint.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionRegion.GetText1()))
                        throw new Exception("请选择上游点和区域结果。");
                }
                else
                {
                    TryReadRoiToFields();
                    regionPoints = ParseRegionPoints(textBoxRegionPoints.Text);
                    if (regionPoints.Count < 3)
                        throw new Exception("区域至少需要 3 个点。");
                }

                if (checkBoxUsePositionCorrection.Checked &&
                    sourceMode == MeasurementDataSourceMode.Draw &&
                    (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                     string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2())))
                {
                    throw new Exception("请选择位置修正信息。");
                }

                Params = new NodeParamPointRegionDistance
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    SourceMode = sourceMode,
                    PointText1 = nodeSubscriptionPoint.GetText1(),
                    PointText2 = nodeSubscriptionPoint.GetText2(),
                    PointRole = (MeasurementPointRole)comboBoxPointRole.SelectedItem,
                    RegionText1 = nodeSubscriptionRegion.GetText1(),
                    RegionText2 = nodeSubscriptionRegion.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    MeasureMode = (GeometryMeasureMode)comboBoxMeasureMode.SelectedItem,
                    PointCenterX = ParseFloat(textBoxPointCenterX, "目标点中心X"),
                    PointCenterY = ParseFloat(textBoxPointCenterY, "目标点中心Y"),
                    PointRadius = ParseFloat(textBoxPointRadius, "目标点半径"),
                    RegionPoints = regionPoints,
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem,
                    FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem,
                    Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0
                };

                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常：{ex.Message}");
                return false;
            }
        }

        private static float ParseFloat(TextBox textBox, string name)
        {
            if (!float.TryParse(textBox.Text, out float value))
                throw new Exception($"{name}不是有效数字！");
            return value;
        }

        private static int ParseInt(TextBox textBox, string name)
        {
            if (!int.TryParse(textBox.Text, out int value))
                throw new Exception($"{name}不是有效整数！");
            return value;
        }

        private static List<PointF> ParseRegionPoints(string text)
        {
            var points = new List<PointF>();
            if (string.IsNullOrWhiteSpace(text))
                return points;

            string[] items = text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in items)
            {
                string[] xy = item.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (xy.Length < 2)
                    continue;

                if (float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    points.Add(new PointF(x, y));
                    continue;
                }

                if (float.TryParse(xy[0], out x) && float.TryParse(xy[1], out y))
                    points.Add(new PointF(x, y));
            }

            return points;
        }

        private static string FormatRegionPoints(IEnumerable<PointF> points)
        {
            if (points == null)
                return string.Empty;

            return string.Join("; ", points.Select(point => $"{point.X:F2},{point.Y:F2}"));
        }

        private PositionCorrectionInfo GetEditingCorrectionInfo(bool require)
        {
            if (!checkBoxUsePositionCorrection.Checked || !radioButtonDraw.Checked)
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

        private void SetPreview(Mat image)
        {
            showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(image));
        }

        private void RefreshRoiFromFields()
        {
            if (_isSyncingRoi)
                return;

            try
            {
                PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
                PointF target = new PointF(ParseFloat(textBoxPointCenterX, string.Empty), ParseFloat(textBoxPointCenterY, string.Empty));
                List<PointF> region = ParseRegionPoints(textBoxRegionPoints.Text);
                if (correctionInfo != null)
                {
                    target = correctionInfo.TransformPoint(target.X, target.Y);
                    region = GeometryMeasurementAlgorithm.TransformPoints(region, correctionInfo);
                }

                float radius = ParseFloat(textBoxPointRadius, string.Empty);
                float caliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
                float caliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
                int count = ParseInt(textBoxCount, string.Empty);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(target.X, target.Y, radius, 0, 360, caliperWidth, caliperHeight, count, Color.Red, "Target");
                if (region.Count >= 3)
                    showImageControl1.AddDynamicPolygon(region, Color.Lime, "Region");

                var rois = showImageControl1.GetAllDynamicCircleCalipers();
                foreach (var roi in rois)
                    roi.Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;
                _roiVisible = true;
            }
            catch
            {
            }
        }

        private void TryReadRoiToFields()
        {
            var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
            var polygonRois = showImageControl1.GetAllDynamicPolygons();
            if (circleRois.Count == 0 && polygonRois.Count == 0)
                return;

            int edgeStrength = ParseInt(textBoxEdgeStrength, string.Empty);
            int blurSize = ParseInt(textBoxBlurSize, string.Empty);
            CaliperEdgePolarity polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem;
            CaliperEdgeFindMode findMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem;
            int directionIndex = comboBoxDirection.SelectedIndex;
            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(true);
            _isSyncingRoi = true;
            try
            {
                if (circleRois.Count > 0)
                {
                    PointF target = new PointF(circleRois[0].CX, circleRois[0].CY);
                    if (correctionInfo != null)
                        target = correctionInfo.InverseTransformPoint(target.X, target.Y);
                    textBoxPointCenterX.Text = target.X.ToString("F2");
                    textBoxPointCenterY.Text = target.Y.ToString("F2");
                    textBoxPointRadius.Text = circleRois[0].Radius.ToString("F2");
                    textBoxCaliperWidth.Text = circleRois[0].CaliperWidth.ToString("F2");
                    textBoxCaliperHeight.Text = circleRois[0].CaliperHeight.ToString("F2");
                    textBoxCount.Text = circleRois[0].Count.ToString();
                }

                if (polygonRois.Count > 0)
                {
                    List<PointF> region = polygonRois[0].Points.ToList();
                    if (correctionInfo != null)
                        region = GeometryMeasurementAlgorithm.InverseTransformPoints(region, correctionInfo);
                    textBoxRegionPoints.Text = FormatRegionPoints(region);
                }

                textBoxEdgeStrength.Text = edgeStrength.ToString();
                textBoxBlurSize.Text = blurSize.ToString();
                comboBoxPolarity.SelectedItem = polarity;
                comboBoxFindMode.SelectedItem = findMode;
                comboBoxDirection.SelectedIndex = directionIndex;
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
            if (circleRois.Count == 0)
                return;

            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
            PointF target = new PointF(circleRois[0].CX, circleRois[0].CY);
            if (correctionInfo != null)
                target = correctionInfo.InverseTransformPoint(target.X, target.Y);

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxPointCenterX)) textBoxPointCenterX.Text = target.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointCenterY)) textBoxPointCenterY.Text = target.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointRadius)) textBoxPointRadius.Text = circleRois[0].Radius.ToString("F2");
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private void UpdateRunPageVisibility(bool visible)
        {
            bool contains = tabControlMain.TabPages.Contains(tabPageRun);
            if (visible)
            {
                if (!contains)
                    tabControlMain.TabPages.Add(tabPageRun);
            }
            else if (contains)
            {
                tabControlMain.TabPages.Remove(tabPageRun);
            }
        }

        private void UpdateSourceModeEnabled()
        {
            bool subscribe = radioButtonSubscribe.Checked;
            groupBoxSubscribed.Enabled = subscribe;
            groupBoxGeometry.Enabled = !subscribe;
            buttonDrawRoi.Enabled = !subscribe;
            buttonConfirmRoi.Enabled = !subscribe;
            checkBoxUsePositionCorrection.Enabled = !subscribe;
            nodeSubscriptionPositionCorrection.Enabled = !subscribe && checkBoxUsePositionCorrection.Checked;
            if (subscribe)
                ClearEditingRoi();
            UpdateRunPageVisibility(!subscribe);
        }

        private void ClearEditingRoi()
        {
            showImageControl1.ClearDynamicRoi();
            _roiVisible = false;
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                SetPreview(GetPreviewMat());
                showImageControl1.ShowFit();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"刷新图像失败：{ex.Message}");
            }
        }

        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            RefreshRoiFromFields();
        }

        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            try
            {
                TryReadRoiToFields();
                ClearEditingRoi();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"确认ROI失败：{ex.Message}");
            }
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                PointRegionDistanceMeasureResult result = ExecuteMeasure((NodeParamPointRegionDistance)Params);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodePointRegionDistance.BuildDisplayResult(result));

                if (!result.Success)
                    MessageBoxTD.Show($"点到区域距离失败：{result.Message}");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})点到区域距离异常，原因：{ex.Message}", true);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
            {
                ClearEditingRoi();
                Hide();
            }
        }

        private void checkBoxUsePositionCorrection_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSourceModeEnabled();
            if (_roiVisible)
                RefreshRoiFromFields();
        }
    }
}
