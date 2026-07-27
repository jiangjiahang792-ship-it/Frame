using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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
        /// <summary>唯一编辑ROI的目标归属与坐标变换服务。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();
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
            /// <summary>获取或设置当前运行 ROI 的灰度剖面采样模式。</summary>
            public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
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
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPoint1.Init(node);
            nodeSubscriptionPoint2.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
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

        /// <summary>按目标顺序执行全部点到点距离测量，订阅模式保持单结果。</summary>
        internal List<PointPointDistanceTargetResult> ExecuteMeasures(NodeParamPointPointDistance param, CancellationToken token)
        {
            if (param == null)
                throw new Exception("点到点距离参数为空。");

            IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
            if (corrections.Count == 0)
                return new List<PointPointDistanceTargetResult>();

            Mat sharedGray = null;
            bool disposeGrayAfterUse = false;
            if (param.SourceMode == MeasurementDataSourceMode.Draw)
                sharedGray = GetInputGrayMat(out disposeGrayAfterUse);

            try
            {
                return MultiTargetMeasurementRunner.Run(
                    corrections,
                    token,
                    correction => CreateTargetResult(ExecuteSingleMeasure(BuildRuntimeParam(param, correction), sharedGray)),
                    CreateFailure);
            }
            finally
            {
                if (disposeGrayAfterUse)
                    sharedGray?.Dispose();
            }
        }

        /// <summary>执行一次已经完成坐标变换的点到点距离测量。</summary>
        private PointPointDistanceMeasureResult ExecuteSingleMeasure(NodeParamPointPointDistance param, Mat sharedGray)
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
                Mat gray = sharedGray;
                bool disposeGrayAfterUse = false;
                if (gray == null)
                    gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    CaliperCircleMeasureResult point1Result = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(param, true));
                    CaliperCircleMeasureResult point2Result = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(param, false));
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

            comboBoxSamplingMode.Items.Add("快速采样");
            comboBoxSamplingMode.Items.Add("抗干扰采样");
            comboBoxSamplingMode.SelectedIndex = 0;
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
            _roiRunSettings[0].SamplingMode = param.Point1SamplingMode;

            _roiRunSettings[1].CaliperWidth = param.Point2CaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[1].CaliperHeight = param.Point2CaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[1].Count = param.Point2Count ?? param.Count;
            _roiRunSettings[1].EdgeStrength = param.Point2EdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[1].Polarity = param.Point2Polarity ?? param.Polarity;
            _roiRunSettings[1].FindMode = param.Point2FindMode ?? param.FindMode;
            _roiRunSettings[1].Direction = param.Point2Direction ?? param.Direction;
            _roiRunSettings[1].BlurSize = param.Point2BlurSize ?? param.BlurSize;
            _roiRunSettings[1].SamplingMode = param.Point2SamplingMode;
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
            comboBoxSamplingMode.SelectedIndex = settings.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;
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
            settings.SamplingMode = comboBoxSamplingMode.SelectedIndex == 1
                ? CaliperSamplingMode.AntiInterference
                : CaliperSamplingMode.Fast;
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

        /// <summary>读取全部位置修正；仅绘制模式启用修正时展开多目标。</summary>
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamPointPointDistance param)
        {
            if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };

            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>把单次算法结果转换为强类型目标结果。</summary>
        private static PointPointDistanceTargetResult CreateTargetResult(PointPointDistanceMeasureResult measure)
        {
            if (measure == null)
                throw new Exception("点到点距离算法没有返回结果。");

            var item = new PointPointDistanceTargetResult
            {
                IsOk = measure.Success,
                ErrorMessage = measure.Success ? string.Empty : measure.Message,
                AlgorithmMs = measure.Success ? MeasurementResultRounder.Round(measure.AlgorithmMs) : 0,
                RawResult = measure
            };
            if (measure.Success)
            {
                item.Distance = MeasurementResultRounder.Round(measure.Distance);
                item.Point1X = MeasurementResultRounder.Round(measure.Point1.X);
                item.Point1Y = MeasurementResultRounder.Round(measure.Point1.Y);
                item.Point2X = MeasurementResultRounder.Round(measure.Point2.X);
                item.Point2Y = MeasurementResultRounder.Round(measure.Point2.Y);
            }
            return item;
        }

        /// <summary>创建保留目标顺序且数值为零的失败项。</summary>
        private static PointPointDistanceTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            string message = exception == null ? "点到点距离失败。" : exception.Message;
            return new PointPointDistanceTargetResult
            {
                IsOk = false,
                ErrorMessage = message,
                RawResult = new PointPointDistanceMeasureResult { Success = false, Message = message }
            };
        }

        /// <summary>创建不改变坐标的单目标修正项。</summary>
        private static PositionCorrectionInfo CreateIdentityCorrection()
        {
            return new PositionCorrectionInfo
            {
                TargetIndex = 1,
                IsValid = true,
                BaseScaleX = 1,
                BaseScaleY = 1,
                CurrentScaleX = 1,
                CurrentScaleY = 1
            };
        }

        /// <summary>为单个模板目标构造已经仿射变换的运行参数。</summary>
        private NodeParamPointPointDistance BuildRuntimeParam(NodeParamPointPointDistance param, PositionCorrectionInfo correction)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionHelper.EnsureValid(correction);
            PointF p1 = _transformService.TransformPoint(new PointF(param.Point1CenterX, param.Point1CenterY), correction);
            PointF p2 = _transformService.TransformPoint(new PointF(param.Point2CenterX, param.Point2CenterY), correction);
            double scale = GetAverageScale(correction);

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
                Point1Radius = (float)(param.Point1Radius * scale),
                Point2CenterX = p2.X,
                Point2CenterY = p2.Y,
                Point2Radius = (float)(param.Point2Radius * scale),
                CaliperWidth = (float)(param.CaliperWidth * scale),
                CaliperHeight = (float)(param.CaliperHeight * scale),
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize,
                Point1CaliperWidth = (float)(param.Point1CaliperWidth * scale),
                Point1CaliperHeight = (float)(param.Point1CaliperHeight * scale),
                Point1Count = param.Point1Count,
                Point1EdgeStrength = param.Point1EdgeStrength,
                Point1Polarity = param.Point1Polarity,
                Point1FindMode = param.Point1FindMode,
                Point1Direction = param.Point1Direction,
                Point1BlurSize = param.Point1BlurSize,
                Point1SamplingMode = param.Point1SamplingMode,
                Point2CaliperWidth = (float)(param.Point2CaliperWidth * scale),
                Point2CaliperHeight = (float)(param.Point2CaliperHeight * scale),
                Point2Count = param.Point2Count,
                Point2EdgeStrength = param.Point2EdgeStrength,
                Point2Polarity = param.Point2Polarity,
                Point2FindMode = param.Point2FindMode,
                Point2Direction = param.Point2Direction,
                Point2BlurSize = param.Point2BlurSize,
                Point2SamplingMode = param.Point2SamplingMode
            };
        }

        /// <summary>计算各向缩放的平均值，供圆半径和卡尺尺寸使用。</summary>
        private static double GetAverageScale(PositionCorrectionInfo correction)
        {
            double scaleX = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleX) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleX);
            double scaleY = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleY) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleY);
            return (scaleX + scaleY) * 0.5;
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
                SamplingMode = param.GetSamplingMode(firstPoint),
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
                    Point1SamplingMode = point1Settings.SamplingMode,
                    Point2CaliperWidth = point2Settings.CaliperWidth,
                    Point2CaliperHeight = point2Settings.CaliperHeight,
                    Point2Count = point2Settings.Count,
                    Point2EdgeStrength = point2Settings.EdgeStrength,
                    Point2BlurSize = point2Settings.BlurSize,
                    Point2Polarity = point2Settings.Polarity,
                    Point2FindMode = point2Settings.FindMode,
                    Point2Direction = point2Settings.Direction,
                    Point2SamplingMode = point2Settings.SamplingMode
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

        /// <summary>读取编辑阶段全部位置修正信息。</summary>
        private List<PositionCorrectionInfo> GetEditingCorrections(bool require)
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
                List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
                if (corrections == null || corrections.Count == 0)
                    throw new Exception("位置修正信息列表为空。");
                foreach (PositionCorrectionInfo correction in corrections)
                    PositionCorrectionHelper.EnsureValid(correction);
                return corrections;
            }
            catch
            {
                if (require)
                    throw;
                return null;
            }
        }

        /// <summary>返回第一个模板目标，作为参数回显时的默认基准。</summary>
        private PositionCorrectionInfo GetDefaultEditingCorrectionInfo(bool require)
        {
            List<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            return corrections == null || corrections.Count == 0 ? null : corrections[0];
        }

        /// <summary>根据唯一编辑ROI中心确定它属于哪个模板目标。</summary>
        private PositionCorrectionInfo ResolveEditingCorrection(PointF roiCenter, bool require)
        {
            List<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            if (corrections == null || corrections.Count == 0)
                return null;
            int index = _transformService.ResolveAnchorIndex(roiCenter, corrections);
            return corrections[index];
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
                PositionCorrectionInfo correctionInfo = GetDefaultEditingCorrectionInfo(false);
                PointF p1 = new PointF(ParseFloat(textBoxP1CenterX, string.Empty), ParseFloat(textBoxP1CenterY, string.Empty));
                PointF p2 = new PointF(ParseFloat(textBoxP2CenterX, string.Empty), ParseFloat(textBoxP2CenterY, string.Empty));
                if (correctionInfo != null)
                {
                    p1 = correctionInfo.TransformPoint(p1.X, p1.Y);
                    p2 = correctionInfo.TransformPoint(p2.X, p2.Y);
                }

                float p1Radius = ParseFloat(textBoxP1Radius, string.Empty);
                float p2Radius = ParseFloat(textBoxP2Radius, string.Empty);
                double displayScale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
                p1Radius = (float)(p1Radius * displayScale);
                p2Radius = (float)(p2Radius * displayScale);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(p1.X, p1.Y, p1Radius, 0, 360, (float)(_roiRunSettings[0].CaliperWidth * displayScale), (float)(_roiRunSettings[0].CaliperHeight * displayScale), _roiRunSettings[0].Count, Color.Lime, "P1");
                showImageControl1.AddDynamicCircleCaliper(p2.X, p2.Y, p2Radius, 0, 360, (float)(_roiRunSettings[1].CaliperWidth * displayScale), (float)(_roiRunSettings[1].CaliperHeight * displayScale), _roiRunSettings[1].Count, Color.DeepSkyBlue, "P2");
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

            PointF p1 = new PointF(rois[0].CX, rois[0].CY);
            PointF p2 = new PointF(rois[1].CX, rois[1].CY);
            PointF roiCenter = new PointF((p1.X + p2.X) * 0.5f, (p1.Y + p2.Y) * 0.5f);
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, requireCorrection);
            double scale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
            if (correctionInfo != null)
            {
                p1 = _transformService.InverseTransformPoint(p1, correctionInfo);
                p2 = _transformService.InverseTransformPoint(p2, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxP1CenterX.Text = p1.X.ToString("F2");
                textBoxP1CenterY.Text = p1.Y.ToString("F2");
                textBoxP1Radius.Text = (rois[0].Radius / scale).ToString("F2");
                textBoxP2CenterX.Text = p2.X.ToString("F2");
                textBoxP2CenterY.Text = p2.Y.ToString("F2");
                textBoxP2Radius.Text = (rois[1].Radius / scale).ToString("F2");
                SaveRunSettingsFromRoi(_roiRunSettings[0], rois[0]);
                SaveRunSettingsFromRoi(_roiRunSettings[1], rois[1]);
                _roiRunSettings[0].CaliperWidth = (float)(_roiRunSettings[0].CaliperWidth / scale);
                _roiRunSettings[0].CaliperHeight = (float)(_roiRunSettings[0].CaliperHeight / scale);
                _roiRunSettings[1].CaliperWidth = (float)(_roiRunSettings[1].CaliperWidth / scale);
                _roiRunSettings[1].CaliperHeight = (float)(_roiRunSettings[1].CaliperHeight / scale);
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

            PointF p1 = new PointF(rois[0].CX, rois[0].CY);
            PointF p2 = new PointF(rois[1].CX, rois[1].CY);
            PointF roiCenter = new PointF((p1.X + p2.X) * 0.5f, (p1.Y + p2.Y) * 0.5f);
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, false);
            double scale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
            if (correctionInfo != null)
            {
                p1 = _transformService.InverseTransformPoint(p1, correctionInfo);
                p2 = _transformService.InverseTransformPoint(p2, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxP1CenterX)) textBoxP1CenterX.Text = p1.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxP1CenterY)) textBoxP1CenterY.Text = p1.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxP1Radius)) textBoxP1Radius.Text = (rois[0].Radius / scale).ToString("F2");
                if (!ReferenceEquals(source, textBoxP2CenterX)) textBoxP2CenterX.Text = p2.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxP2CenterY)) textBoxP2CenterY.Text = p2.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxP2Radius)) textBoxP2Radius.Text = (rois[1].Radius / scale).ToString("F2");
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
            showImageControl1.SetDisplayResult(null);
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
                List<PointPointDistanceTargetResult> items = ExecuteMeasures((NodeParamPointPointDistance)Params, CancellationToken.None);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodePointPointDistance.BuildDisplayResult(items));

                if (items.Exists(item => !item.IsOk))
                    MessageBoxTD.Show("部分模板目标点到点距离失败，失败目标结果已保留为0。");
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
