using Logger;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TDJS_Vision.Node._4_Measurement.PointPointDistance
{
    public partial class NodeParamFormPointPointDistance : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        private bool _isSyncingRoi;
        private bool _roiVisible;
        private int _selectedRunRoiIndex;
        private readonly RoiRunSettings[] _roiRunSettings =
        {
            new RoiRunSettings(10, 60, 30, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3),
            new RoiRunSettings(10, 60, 30, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3)
        };

        private sealed class RoiRunSettings
        {
            public RoiRunSettings(
                float caliperWidth,
                float caliperHeight,
                int count,
                int edgeStrength,
                CaliperEdgePolarity polarity,
                CaliperEdgeFindMode findMode,
                int direction,
                int blurSize)
            {
                CaliperWidth = caliperWidth;
                CaliperHeight = caliperHeight;
                Count = count;
                EdgeStrength = edgeStrength;
                Polarity = polarity;
                FindMode = findMode;
                Direction = direction;
                BlurSize = blurSize;
            }

            public float CaliperWidth { get; set; }
            public float CaliperHeight { get; set; }
            public int Count { get; set; }
            public int EdgeStrength { get; set; }
            public CaliperEdgePolarity Polarity { get; set; }
            public CaliperEdgeFindMode FindMode { get; set; }
            public int Direction { get; set; }
            public int BlurSize { get; set; }
        }

        public NodeParamFormPointPointDistance(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeCombos();
            InitializeRunRoiTargets();
            BindRoiRefreshEvents();
            nodeSubscriptionPoint1.HideText2();
            nodeSubscriptionPoint2.HideText2();
            UpdateSourceModeEnabled();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPoint1.Init(node);
            nodeSubscriptionPoint2.Init(node);
            nodeSubscriptionPositionCorrection.Init(node);
        }

        public void SetParam2Form()
        {
            var param = Params as NodeParamPointPointDistance;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                radioButtonSubscribe.Checked = param.SourceMode == MeasurementDataSourceMode.Subscribe;
                radioButtonDraw.Checked = param.SourceMode != MeasurementDataSourceMode.Subscribe;
                nodeSubscriptionPoint1.SetText(param.Point1Text1, param.Point1Text2);
                comboBoxPoint1Role.SelectedItem = param.Point1Role;
                nodeSubscriptionPoint2.SetText(param.Point2Text1, param.Point2Text2);
                comboBoxPoint2Role.SelectedItem = param.Point2Role;
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                comboBoxMeasureMode.SelectedItem = param.MeasureMode;
                textBoxP1CenterX.Text = param.Point1CenterX.ToString();
                textBoxP1CenterY.Text = param.Point1CenterY.ToString();
                textBoxP1Radius.Text = param.Point1Radius.ToString();
                textBoxP2CenterX.Text = param.Point2CenterX.ToString();
                textBoxP2CenterY.Text = param.Point2CenterY.ToString();
                textBoxP2Radius.Text = param.Point2Radius.ToString();
                LoadRunSettingsFromParam(param);
                _selectedRunRoiIndex = 0;
                comboBoxRunRoiTarget.SelectedIndex = 0;
                LoadRunSettingsToControls(_roiRunSettings[0]);
            }
            finally
            {
                _isSyncingRoi = false;
            }

            UpdateSourceModeEnabled();
            ClearEditingRoi();
        }

        internal PointPointDistanceMeasureResult ExecuteMeasure(NodeParamPointPointDistance param)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PointPointDistanceMeasureResult();
            PointF point1;
            PointF point2;

            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
            {
                point1 = ReadSubscribedPoint(nodeSubscriptionPoint1, param.Point1Role, "点1");
                point2 = ReadSubscribedPoint(nodeSubscriptionPoint2, param.Point2Role, "点2");
            }
            else
            {
                bool disposeGrayAfterUse;
                Mat gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    NodeParamPointPointDistance runtimeParam = BuildRuntimeParam(param);
                    CaliperCircleMeasureResult point1Result = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(runtimeParam, true));
                    CaliperCircleMeasureResult point2Result = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(runtimeParam, false));
                    result.Point1EdgePoints.AddRange(point1Result.EdgePoints);
                    result.Point2EdgePoints.AddRange(point2Result.EdgePoints);

                    if (!point1Result.Success)
                        return FinishFailed(result, stopwatch, "点1圆卡尺找圆失败");
                    if (!point2Result.Success)
                        return FinishFailed(result, stopwatch, "点2圆卡尺找圆失败");

                    point1 = point1Result.Center;
                    point2 = point2Result.Center;
                }
                finally
                {
                    if (disposeGrayAfterUse)
                        gray?.Dispose();
                }
            }

            result.Distance = GeometryMeasurementAlgorithm.CalculatePointDistance(
                point1,
                point2,
                param.MeasureMode,
                out PointF usedPoint1,
                out PointF usedPoint2);
            result.Point1 = usedPoint1;
            result.Point2 = usedPoint2;
            result.Success = true;
            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private void InitializeCombos()
        {
            comboBoxPoint1Role.Items.Add(MeasurementPointRole.Auto);
            comboBoxPoint1Role.Items.Add(MeasurementPointRole.Center);
            comboBoxPoint1Role.Items.Add(MeasurementPointRole.StartPoint);
            comboBoxPoint1Role.Items.Add(MeasurementPointRole.EndPoint);
            comboBoxPoint1Role.SelectedItem = MeasurementPointRole.Auto;

            comboBoxPoint2Role.Items.Add(MeasurementPointRole.Auto);
            comboBoxPoint2Role.Items.Add(MeasurementPointRole.Center);
            comboBoxPoint2Role.Items.Add(MeasurementPointRole.StartPoint);
            comboBoxPoint2Role.Items.Add(MeasurementPointRole.EndPoint);
            comboBoxPoint2Role.SelectedItem = MeasurementPointRole.Auto;

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

        private void InitializeRunRoiTargets()
        {
            comboBoxRunRoiTarget.Items.Add("Point1");
            comboBoxRunRoiTarget.Items.Add("Point2");
            comboBoxRunRoiTarget.SelectedIndex = 0;
            comboBoxRunRoiTarget.SelectedIndexChanged += comboBoxRunRoiTarget_SelectedIndexChanged;
            showImageControl1.MouseUp += showImageControl1_MouseUp;
        }

        private void BindRoiRefreshEvents()
        {
            foreach (Control control in groupBoxGeometry.Controls)
            {
                if (control is TextBox)
                    control.TextChanged += OnRoiParamChanged;
            }

            textBoxCaliperWidth.TextChanged += OnRoiParamChanged;
            textBoxCaliperHeight.TextChanged += OnRoiParamChanged;
            textBoxCount.TextChanged += OnRoiParamChanged;
            comboBoxDirection.SelectedIndexChanged += OnRoiParamChanged;
            radioButtonSubscribe.CheckedChanged += SourceMode_CheckedChanged;
            radioButtonDraw.CheckedChanged += SourceMode_CheckedChanged;
        }

        private void LoadRunSettingsFromParam(NodeParamPointPointDistance param)
        {
            _roiRunSettings[0].CaliperWidth = param.Point1CaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[0].CaliperHeight = param.Point1CaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[0].Count = param.Point1Count ?? param.Count;
            _roiRunSettings[0].EdgeStrength = param.Point1EdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[0].Polarity = param.Point1Polarity ?? param.Polarity;
            _roiRunSettings[0].FindMode = param.Point1FindMode ?? param.FindMode;
            _roiRunSettings[0].Direction = param.Point1Direction ?? param.Direction;
            _roiRunSettings[0].BlurSize = param.Point1BlurSize ?? param.BlurSize;

            _roiRunSettings[1].CaliperWidth = param.Point2CaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[1].CaliperHeight = param.Point2CaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[1].Count = param.Point2Count ?? param.Count;
            _roiRunSettings[1].EdgeStrength = param.Point2EdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[1].Polarity = param.Point2Polarity ?? param.Polarity;
            _roiRunSettings[1].FindMode = param.Point2FindMode ?? param.FindMode;
            _roiRunSettings[1].Direction = param.Point2Direction ?? param.Direction;
            _roiRunSettings[1].BlurSize = param.Point2BlurSize ?? param.BlurSize;
        }

        private void LoadRunSettingsToControls(RoiRunSettings settings)
        {
            if (settings == null)
                return;

            textBoxCaliperWidth.Text = settings.CaliperWidth.ToString();
            textBoxCaliperHeight.Text = settings.CaliperHeight.ToString();
            textBoxCount.Text = settings.Count.ToString();
            textBoxEdgeStrength.Text = settings.EdgeStrength.ToString();
            textBoxBlurSize.Text = settings.BlurSize.ToString();
            comboBoxPolarity.SelectedItem = settings.Polarity;
            comboBoxFindMode.SelectedItem = settings.FindMode;
            comboBoxDirection.SelectedIndex = settings.Direction == 1 ? 1 : 0;
        }

        private void SaveCurrentRunSettingsFromControls()
        {
            if (_selectedRunRoiIndex < 0 || _selectedRunRoiIndex >= _roiRunSettings.Length)
                return;

            RoiRunSettings settings = _roiRunSettings[_selectedRunRoiIndex];
            settings.CaliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
            settings.CaliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
            settings.Count = ParseInt(textBoxCount, string.Empty);
            settings.EdgeStrength = ParseInt(textBoxEdgeStrength, string.Empty);
            settings.BlurSize = ParseInt(textBoxBlurSize, string.Empty);
            settings.Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem;
            settings.FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem;
            settings.Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;
        }

        private void SaveRunSettingsFromRoi(RoiRunSettings settings, TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiCircleCaliper roi)
        {
            settings.CaliperWidth = roi.CaliperWidth;
            settings.CaliperHeight = roi.CaliperHeight;
            settings.Count = roi.Count;
            settings.Direction = roi.Direction;
        }

        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            SyncCurrentRoiToFieldsExcept(sender);
            if (_roiVisible)
                RefreshRoiFromFields();
        }

        private void SourceMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSourceModeEnabled();
        }

        private void comboBoxRunRoiTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            try
            {
                SaveCurrentRunSettingsFromControls();
                _selectedRunRoiIndex = Math.Max(0, comboBoxRunRoiTarget.SelectedIndex);
                _isSyncingRoi = true;
                LoadRunSettingsToControls(_roiRunSettings[_selectedRunRoiIndex]);
            }
            catch
            {
            }
            finally
            {
                _isSyncingRoi = false;
            }

            SelectDynamicRoi(_selectedRunRoiIndex);
        }

        private void showImageControl1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isSyncingRoi || !_roiVisible || radioButtonSubscribe.Checked)
                return;

            var rois = showImageControl1.GetAllDynamicCircleCalipers();
            int selectedIndex = rois.FindIndex(roi => roi.IsSelected);
            if (selectedIndex < 0 || selectedIndex >= _roiRunSettings.Length)
                return;

            try
            {
                SaveCurrentRunSettingsFromControls();
                _selectedRunRoiIndex = selectedIndex;
                _isSyncingRoi = true;
                comboBoxRunRoiTarget.SelectedIndex = selectedIndex;
                ReadRoiToFields(false);
                LoadRunSettingsToControls(_roiRunSettings[_selectedRunRoiIndex]);
            }
            catch
            {
            }
            finally
            {
                _isSyncingRoi = false;
            }
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

        private PointF ReadSubscribedPoint(NodeSubscription subscription, MeasurementPointRole role, string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadPoint(sourceNode.Result, role, out PointF point))
                throw new Exception($"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用点结果。");

            return point;
        }

        private NodeParamPointPointDistance BuildRuntimeParam(NodeParamPointPointDistance param)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            PointF p1 = correctionInfo.TransformPoint(param.Point1CenterX, param.Point1CenterY);
            PointF p2 = correctionInfo.TransformPoint(param.Point2CenterX, param.Point2CenterY);

            return new NodeParamPointPointDistance
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                SourceMode = param.SourceMode,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                MeasureMode = param.MeasureMode,
                Point1CenterX = p1.X,
                Point1CenterY = p1.Y,
                Point1Radius = param.Point1Radius,
                Point2CenterX = p2.X,
                Point2CenterY = p2.Y,
                Point2Radius = param.Point2Radius,
                CaliperWidth = param.CaliperWidth,
                CaliperHeight = param.CaliperHeight,
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize,
                Point1CaliperWidth = param.Point1CaliperWidth,
                Point1CaliperHeight = param.Point1CaliperHeight,
                Point1Count = param.Point1Count,
                Point1EdgeStrength = param.Point1EdgeStrength,
                Point1Polarity = param.Point1Polarity,
                Point1FindMode = param.Point1FindMode,
                Point1Direction = param.Point1Direction,
                Point1BlurSize = param.Point1BlurSize,
                Point2CaliperWidth = param.Point2CaliperWidth,
                Point2CaliperHeight = param.Point2CaliperHeight,
                Point2Count = param.Point2Count,
                Point2EdgeStrength = param.Point2EdgeStrength,
                Point2Polarity = param.Point2Polarity,
                Point2FindMode = param.Point2FindMode,
                Point2Direction = param.Point2Direction,
                Point2BlurSize = param.Point2BlurSize
            };
        }

        private static CaliperCircleParams BuildCircleParams(NodeParamPointPointDistance param, bool firstPoint)
        {
            return new CaliperCircleParams
            {
                CenterX = firstPoint ? param.Point1CenterX : param.Point2CenterX,
                CenterY = firstPoint ? param.Point1CenterY : param.Point2CenterY,
                Radius = firstPoint ? param.Point1Radius : param.Point2Radius,
                StartAngle = 0,
                EndAngle = 360,
                CaliperWidth = param.GetCaliperWidth(firstPoint),
                CaliperHeight = param.GetCaliperHeight(firstPoint),
                Count = param.GetCount(firstPoint),
                EdgeStrength = param.GetEdgeStrength(firstPoint),
                Polarity = param.GetPolarity(firstPoint),
                FindMode = param.GetFindMode(firstPoint),
                Direction = param.GetDirection(firstPoint),
                BlurSize = param.GetBlurSize(firstPoint)
            };
        }

        private static PointPointDistanceMeasureResult FinishFailed(PointPointDistanceMeasureResult result, Stopwatch stopwatch, string message)
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
                if (sourceMode == MeasurementDataSourceMode.Subscribe)
                {
                    if (string.IsNullOrWhiteSpace(nodeSubscriptionPoint1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionPoint2.GetText1()))
                        throw new Exception("请选择两个上游点结果。");
                }
                else
                {
                    SaveCurrentRunSettingsFromControls();
                    TryReadRoiToFields();
                }

                if (checkBoxUsePositionCorrection.Checked &&
                    sourceMode == MeasurementDataSourceMode.Draw &&
                    (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                     string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2())))
                {
                    throw new Exception("请选择位置修正信息。");
                }

                RoiRunSettings point1Settings = _roiRunSettings[0];
                RoiRunSettings point2Settings = _roiRunSettings[1];
                Params = new NodeParamPointPointDistance
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    SourceMode = sourceMode,
                    Point1Text1 = nodeSubscriptionPoint1.GetText1(),
                    Point1Text2 = nodeSubscriptionPoint1.GetText2(),
                    Point1Role = (MeasurementPointRole)comboBoxPoint1Role.SelectedItem,
                    Point2Text1 = nodeSubscriptionPoint2.GetText1(),
                    Point2Text2 = nodeSubscriptionPoint2.GetText2(),
                    Point2Role = (MeasurementPointRole)comboBoxPoint2Role.SelectedItem,
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    MeasureMode = (GeometryMeasureMode)comboBoxMeasureMode.SelectedItem,
                    Point1CenterX = ParseFloat(textBoxP1CenterX, "点1中心X"),
                    Point1CenterY = ParseFloat(textBoxP1CenterY, "点1中心Y"),
                    Point1Radius = ParseFloat(textBoxP1Radius, "点1半径"),
                    Point2CenterX = ParseFloat(textBoxP2CenterX, "点2中心X"),
                    Point2CenterY = ParseFloat(textBoxP2CenterY, "点2中心Y"),
                    Point2Radius = ParseFloat(textBoxP2Radius, "点2半径"),
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem,
                    FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem,
                    Direction = point1Settings.Direction,
                    Point1CaliperWidth = point1Settings.CaliperWidth,
                    Point1CaliperHeight = point1Settings.CaliperHeight,
                    Point1Count = point1Settings.Count,
                    Point1EdgeStrength = point1Settings.EdgeStrength,
                    Point1BlurSize = point1Settings.BlurSize,
                    Point1Polarity = point1Settings.Polarity,
                    Point1FindMode = point1Settings.FindMode,
                    Point1Direction = point1Settings.Direction,
                    Point2CaliperWidth = point2Settings.CaliperWidth,
                    Point2CaliperHeight = point2Settings.CaliperHeight,
                    Point2Count = point2Settings.Count,
                    Point2EdgeStrength = point2Settings.EdgeStrength,
                    Point2BlurSize = point2Settings.BlurSize,
                    Point2Polarity = point2Settings.Polarity,
                    Point2FindMode = point2Settings.FindMode,
                    Point2Direction = point2Settings.Direction
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
                SaveCurrentRunSettingsFromControls();
                PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
                PointF p1 = new PointF(ParseFloat(textBoxP1CenterX, string.Empty), ParseFloat(textBoxP1CenterY, string.Empty));
                PointF p2 = new PointF(ParseFloat(textBoxP2CenterX, string.Empty), ParseFloat(textBoxP2CenterY, string.Empty));
                if (correctionInfo != null)
                {
                    p1 = correctionInfo.TransformPoint(p1.X, p1.Y);
                    p2 = correctionInfo.TransformPoint(p2.X, p2.Y);
                }

                float p1Radius = ParseFloat(textBoxP1Radius, string.Empty);
                float p2Radius = ParseFloat(textBoxP2Radius, string.Empty);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(p1.X, p1.Y, p1Radius, 0, 360, _roiRunSettings[0].CaliperWidth, _roiRunSettings[0].CaliperHeight, _roiRunSettings[0].Count, Color.Lime, "P1");
                showImageControl1.AddDynamicCircleCaliper(p2.X, p2.Y, p2Radius, 0, 360, _roiRunSettings[1].CaliperWidth, _roiRunSettings[1].CaliperHeight, _roiRunSettings[1].Count, Color.DeepSkyBlue, "P2");
                var rois = showImageControl1.GetAllDynamicCircleCalipers();
                if (rois.Count > 0)
                    rois[0].Direction = _roiRunSettings[0].Direction;
                if (rois.Count > 1)
                    rois[1].Direction = _roiRunSettings[1].Direction;
                SelectDynamicRoi(_selectedRunRoiIndex);
                _roiVisible = true;
            }
            catch
            {
            }
        }

        private void TryReadRoiToFields()
        {
            SaveCurrentRunSettingsFromControls();
            ReadRoiToFields(true);
        }

        private void ReadRoiToFields(bool requireCorrection)
        {
            var rois = showImageControl1.GetAllDynamicCircleCalipers();
            if (rois.Count < 2)
                return;

            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(requireCorrection);
            PointF p1 = new PointF(rois[0].CX, rois[0].CY);
            PointF p2 = new PointF(rois[1].CX, rois[1].CY);
            if (correctionInfo != null)
            {
                p1 = correctionInfo.InverseTransformPoint(p1.X, p1.Y);
                p2 = correctionInfo.InverseTransformPoint(p2.X, p2.Y);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxP1CenterX.Text = p1.X.ToString("F2");
                textBoxP1CenterY.Text = p1.Y.ToString("F2");
                textBoxP1Radius.Text = rois[0].Radius.ToString("F2");
                textBoxP2CenterX.Text = p2.X.ToString("F2");
                textBoxP2CenterY.Text = p2.Y.ToString("F2");
                textBoxP2Radius.Text = rois[1].Radius.ToString("F2");
                SaveRunSettingsFromRoi(_roiRunSettings[0], rois[0]);
                SaveRunSettingsFromRoi(_roiRunSettings[1], rois[1]);
                LoadRunSettingsToControls(_roiRunSettings[_selectedRunRoiIndex]);
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var rois = showImageControl1.GetAllDynamicCircleCalipers();
            if (rois.Count < 2)
                return;

            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
            PointF p1 = new PointF(rois[0].CX, rois[0].CY);
            PointF p2 = new PointF(rois[1].CX, rois[1].CY);
            if (correctionInfo != null)
            {
                p1 = correctionInfo.InverseTransformPoint(p1.X, p1.Y);
                p2 = correctionInfo.InverseTransformPoint(p2.X, p2.Y);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxP1CenterX)) textBoxP1CenterX.Text = p1.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxP1CenterY)) textBoxP1CenterY.Text = p1.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxP1Radius)) textBoxP1Radius.Text = rois[0].Radius.ToString("F2");
                if (!ReferenceEquals(source, textBoxP2CenterX)) textBoxP2CenterX.Text = p2.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxP2CenterY)) textBoxP2CenterY.Text = p2.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxP2Radius)) textBoxP2Radius.Text = rois[1].Radius.ToString("F2");
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private void SelectDynamicRoi(int index)
        {
            for (int i = 0; i < showImageControl1.DynamicRoiCount; i++)
            {
                var roi = showImageControl1.GetDynamicRoi(i);
                if (roi != null)
                    roi.IsSelected = false;
            }

            var calipers = showImageControl1.GetAllDynamicCircleCalipers();
            if (index >= 0 && index < calipers.Count)
                calipers[index].IsSelected = true;

            showImageControl1.Invalidate();
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
                PointPointDistanceMeasureResult result = ExecuteMeasure((NodeParamPointPointDistance)Params);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodePointPointDistance.BuildDisplayResult(result));

                if (!result.Success)
                    MessageBoxTD.Show($"点到点距离失败：{result.Message}");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})点到点距离异常，原因：{ex.Message}", true);
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
