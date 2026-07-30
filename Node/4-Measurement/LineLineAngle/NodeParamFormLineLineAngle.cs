using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TDJS_Vision.Node._4_Measurement.LineLineAngle
{
    public partial class NodeParamFormLineLineAngle : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        /// <summary>唯一编辑ROI的目标归属与坐标变换服务。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();
        private bool _isSyncingRoi;
        private bool _roiVisible;
        private int _selectedRunRoiIndex;
        private readonly RoiRunSettings[] _roiRunSettings =
        {
            new RoiRunSettings(20, 120, 15, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3),
            new RoiRunSettings(20, 120, 15, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3)
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

        /// <summary>
        /// 线线测量的输入数据，包含订阅线段和可用于统计的边缘点集。
        /// </summary>
        private sealed class LineMeasurementInput
        {
            /// <summary>
            /// 上游输出的基础线段。
            /// </summary>
            public MeasuredLine Line { get; set; }

            /// <summary>
            /// 上游输出且确实落在该直线上的边缘点。
            /// </summary>
            public List<PointF> Points { get; set; } = new List<PointF>();

            /// <summary>
            /// 上游节点显示名称，用于诊断和后续扩展。
            /// </summary>
            public string SourceName { get; set; }
        }

        /// <summary>
        /// 两条线之间的点到线垂距统计结果。
        /// </summary>
        private sealed class LineDistanceStatistics
        {
            /// <summary>
            /// 平均绝对垂直距离。
            /// </summary>
            public double AverageDistance { get; set; }

            /// <summary>
            /// 最小绝对垂直距离。
            /// </summary>
            public double MinDistance { get; set; }

            /// <summary>
            /// 最大绝对垂直距离。
            /// </summary>
            public double MaxDistance { get; set; }

            /// <summary>
            /// 参与距离统计的点数量。
            /// </summary>
            public int PointCount { get; set; }
        }

        public NodeParamFormLineLineAngle(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeCombos();
            InitializeRunRoiTargets();
            BindRoiRefreshEvents();
            nodeSubscriptionLine1.HideText2();
            nodeSubscriptionLine2.HideText2();
            UpdateSourceModeEnabled();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionLine1.Init(node);
            nodeSubscriptionLine2.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
            nodeSubscriptionPositionCorrection.Init(node);
        }

        public void SetParam2Form()
        {
            var param = Params as NodeParamLineLineAngle;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                radioButtonSubscribe.Checked = param.SourceMode == MeasurementDataSourceMode.Subscribe;
                radioButtonDraw.Checked = param.SourceMode != MeasurementDataSourceMode.Subscribe;
                nodeSubscriptionLine1.SetText(param.Line1Text1, param.Line1Text2);
                nodeSubscriptionLine2.SetText(param.Line2Text1, param.Line2Text2);
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                comboBoxMeasureMode.SelectedItem = param.MeasureMode;
                textBoxL1StartX.Text = param.Line1StartX.ToString();
                textBoxL1StartY.Text = param.Line1StartY.ToString();
                textBoxL1EndX.Text = param.Line1EndX.ToString();
                textBoxL1EndY.Text = param.Line1EndY.ToString();
                textBoxL2StartX.Text = param.Line2StartX.ToString();
                textBoxL2StartY.Text = param.Line2StartY.ToString();
                textBoxL2EndX.Text = param.Line2EndX.ToString();
                textBoxL2EndY.Text = param.Line2EndY.ToString();
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

        /// <summary>按目标顺序执行全部线线夹角测量，订阅模式按目标编号配对两侧直线。</summary>
        internal List<LineLineAngleTargetResult> ExecuteMeasures(NodeParamLineLineAngle param, CancellationToken token)
        {
            if (param == null)
                throw new Exception("线到线夹角参数为空。");

            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
                return ExecuteSubscribedMeasures(param, token);

            IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
            if (corrections.Count == 0)
                return new List<LineLineAngleTargetResult>();

            Mat sharedGray = null;
            bool disposeGrayAfterUse = false;
            if (param.SourceMode == MeasurementDataSourceMode.Draw)
                sharedGray = GetInputGrayMat(out disposeGrayAfterUse);

            try
            {
                return MultiTargetMeasurementRunner.Run(
                    corrections,
                    token,
                    correction =>
                    {
                        NodeParamLineLineAngle runtimeParam = BuildRuntimeParam(param, correction);
                        return CreateTargetResult(ExecuteSingleMeasure(runtimeParam, sharedGray));
                    },
                    CreateFailure);
            }
            finally
            {
                if (disposeGrayAfterUse)
                    sharedGray?.Dispose();
            }
        }

        /// <summary>
        /// 一次读取两侧订阅直线的全部目标明细，按目标编号配对后串行计算夹角。
        /// </summary>
        private List<LineLineAngleTargetResult> ExecuteSubscribedMeasures(
            NodeParamLineLineAngle param,
            CancellationToken token)
        {
            List<IndexedMeasurementValue<LineMeasurementInput>> lines1 =
                ReadSubscribedLineTargets(nodeSubscriptionLine1, "直线1");
            List<IndexedMeasurementValue<LineMeasurementInput>> lines2 =
                ReadSubscribedLineTargets(nodeSubscriptionLine2, "直线2");
            List<IndexedMeasurementPair<LineMeasurementInput, LineMeasurementInput>> pairs =
                MultiTargetMeasurementPairer.PairByTargetIndex(lines1, lines2, "直线1结果", "直线2结果");
            if (pairs.Count == 0)
                return new List<LineLineAngleTargetResult>();

            var pairMap = new Dictionary<int, IndexedMeasurementPair<LineMeasurementInput, LineMeasurementInput>>(pairs.Count);
            var corrections = new List<PositionCorrectionInfo>(pairs.Count);
            foreach (IndexedMeasurementPair<LineMeasurementInput, LineMeasurementInput> pair in pairs)
            {
                pairMap.Add(pair.TargetIndex, pair);
                corrections.Add(ResolvePairCorrection(pair));
            }

            return MultiTargetMeasurementRunner.Run(
                corrections,
                token,
                correction =>
                {
                    IndexedMeasurementPair<LineMeasurementInput, LineMeasurementInput> pair = pairMap[correction.TargetIndex];
                    if (!pair.IsOk)
                    {
                        string reason = string.IsNullOrWhiteSpace(pair.ErrorMessage)
                            ? $"目标{pair.TargetIndex}的两条直线结果无效。"
                            : pair.ErrorMessage;
                        throw new Exception(reason);
                    }

                    return CreateTargetResult(ExecuteSubscribedMeasure(param, pair.First.Value, pair.Second.Value));
                },
                CreateFailure);
        }

        /// <summary>使用已经完成目标配对的两条直线计算一次夹角，不再重复读取上游结果。</summary>
        private LineLineAngleMeasureResult ExecuteSubscribedMeasure(
            NodeParamLineLineAngle param,
            LineMeasurementInput input1,
            LineMeasurementInput input2)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new LineLineAngleMeasureResult();
            CaptureDisplayImageSize(result, null);
            return CompleteLineMeasure(param, input1, input2, result, stopwatch);
        }

        /// <summary>执行一次已经完成坐标变换的线线夹角测量。</summary>
        private LineLineAngleMeasureResult ExecuteSingleMeasure(NodeParamLineLineAngle param, Mat sharedGray)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new LineLineAngleMeasureResult();
            CaptureDisplayImageSize(result, sharedGray);

            LineMeasurementInput input1;
            LineMeasurementInput input2;
            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
            {
                input1 = ReadSubscribedLineInput(nodeSubscriptionLine1, "直线1");
                input2 = ReadSubscribedLineInput(nodeSubscriptionLine2, "直线2");
            }
            else
            {
                Mat gray = sharedGray;
                bool disposeGrayAfterUse = false;
                if (gray == null)
                    gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    CaliperLineMeasureResult line1Result = CaliperMeasurementAlgorithm.FindLine(gray, BuildLineParams(param, true));
                    CaliperLineMeasureResult line2Result = CaliperMeasurementAlgorithm.FindLine(gray, BuildLineParams(param, false));

                    if (!line1Result.Success)
                        return FinishFailed(result, stopwatch, "直线1卡尺找线失败");
                    if (!line2Result.Success)
                        return FinishFailed(result, stopwatch, "直线2卡尺找线失败");

                    input1 = new LineMeasurementInput
                    {
                        Line = new MeasuredLine { Start = line1Result.LineStart, End = line1Result.LineEnd, Source = "DrawLine1" },
                        Points = line1Result.EdgePoints.ToList()
                    };
                    input2 = new LineMeasurementInput
                    {
                        Line = new MeasuredLine { Start = line2Result.LineStart, End = line2Result.LineEnd, Source = "DrawLine2" },
                        Points = line2Result.EdgePoints.ToList()
                    };
                }
                finally
                {
                    if (disposeGrayAfterUse)
                        gray?.Dispose();
                }
            }

            return CompleteLineMeasure(param, input1, input2, result, stopwatch);
        }

        /// <summary>完成两条已读取直线的公共计算，供单目标与多目标订阅路径复用。</summary>
        private static LineLineAngleMeasureResult CompleteLineMeasure(
            NodeParamLineLineAngle param,
            LineMeasurementInput input1,
            LineMeasurementInput input2,
            LineLineAngleMeasureResult result,
            Stopwatch stopwatch)
        {
            if (input1 == null || input1.Line == null || !input1.Line.IsValid)
                return FinishFailed(result, stopwatch, "直线1无效");
            if (input2 == null || input2.Line == null || !input2.Line.IsValid)
                return FinishFailed(result, stopwatch, "直线2无效");

            MeasuredLine line1 = BuildStableLine(input1, param.MeasureMode);
            MeasuredLine line2 = BuildStableLine(input2, param.MeasureMode);
            result.Line1EdgePoints.AddRange(input1.Points);
            result.Line2EdgePoints.AddRange(input2.Points);
            result.Line1 = line1;
            result.Line2 = line2;
            result.Angle = GeometryMeasurementAlgorithm.CalculateLineLineAngle(
                line1.Start,
                line1.End,
                line2.Start,
                line2.End,
                param.MeasureMode,
                out PointF usedL1Start,
                out PointF usedL1End,
                out PointF usedL2Start,
                out PointF usedL2End,
                out PointF? intersectionPoint);

            result.Line1 = new MeasuredLine { Start = usedL1Start, End = usedL1End, Source = line1.Source };
            result.Line2 = new MeasuredLine { Start = usedL2Start, End = usedL2End, Source = line2.Source };
            result.IntersectionPoint = intersectionPoint;
            LineDistanceStatistics distanceStatistics = CalculateDistanceStatistics(input1, input2, result.Line1, result.Line2, param.MeasureMode);
            result.AverageDistance = distanceStatistics.AverageDistance;
            result.MinDistance = distanceStatistics.MinDistance;
            result.MaxDistance = distanceStatistics.MaxDistance;
            result.DistancePointCount = distanceStatistics.PointCount;
            result.Success = true;
            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private void InitializeCombos()
        {
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

            comboBoxDirection.Items.Add("垂直主轴");
            comboBoxDirection.Items.Add("沿主轴");
            comboBoxDirection.SelectedIndex = 0;

            comboBoxSamplingMode.Items.Add("快速采样");
            comboBoxSamplingMode.Items.Add("抗干扰采样");
            comboBoxSamplingMode.SelectedIndex = 0;
        }

        private void InitializeRunRoiTargets()
        {
            comboBoxRunRoiTarget.Items.Add("Line1");
            comboBoxRunRoiTarget.Items.Add("Line2");
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

        private void LoadRunSettingsFromParam(NodeParamLineLineAngle param)
        {
            _roiRunSettings[0].CaliperWidth = param.Line1CaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[0].CaliperHeight = param.Line1CaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[0].Count = param.Line1Count ?? param.Count;
            _roiRunSettings[0].EdgeStrength = param.Line1EdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[0].Polarity = param.Line1Polarity ?? param.Polarity;
            _roiRunSettings[0].FindMode = param.Line1FindMode ?? param.FindMode;
            _roiRunSettings[0].Direction = param.Line1Direction ?? param.Direction;
            _roiRunSettings[0].BlurSize = param.Line1BlurSize ?? param.BlurSize;
            _roiRunSettings[0].SamplingMode = param.Line1SamplingMode;

            _roiRunSettings[1].CaliperWidth = param.Line2CaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[1].CaliperHeight = param.Line2CaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[1].Count = param.Line2Count ?? param.Count;
            _roiRunSettings[1].EdgeStrength = param.Line2EdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[1].Polarity = param.Line2Polarity ?? param.Polarity;
            _roiRunSettings[1].FindMode = param.Line2FindMode ?? param.FindMode;
            _roiRunSettings[1].Direction = param.Line2Direction ?? param.Direction;
            _roiRunSettings[1].BlurSize = param.Line2BlurSize ?? param.BlurSize;
            _roiRunSettings[1].SamplingMode = param.Line2SamplingMode;
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

        private void SaveRunSettingsFromRoi(RoiRunSettings settings, TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiCaliper roi)
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

            var rois = showImageControl1.GetAllDynamicCalipers();
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

        /// <summary>
        /// 记录当前输入图像尺寸，供结果 ROI 将测量线延长并裁剪到图像边界。
        /// </summary>
        private void CaptureDisplayImageSize(LineLineAngleMeasureResult result, Mat measuredImage)
        {
            if (result == null)
                return;

            if (measuredImage != null && !measuredImage.Empty())
            {
                result.DisplayImageWidth = measuredImage.Width;
                result.DisplayImageHeight = measuredImage.Height;
                return;
            }

            Mat preview = GetPreviewMat();
            result.DisplayImageWidth = preview.Width;
            result.DisplayImageHeight = preview.Height;
        }

        private LineMeasurementInput ReadSubscribedLineInput(NodeSubscription subscription, string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadLine(sourceNode.Result, out MeasuredLine line))
                throw new Exception($"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用直线结果。");

            List<PointF> points;
            if (!MeasurementResultReader.TryReadLinePoints(sourceNode.Result, out points))
                points = new List<PointF>();

            return new LineMeasurementInput
            {
                Line = line,
                Points = points,
                SourceName = $"{sourceNode.ID}.{sourceNode.NodeName}"
            };
        }

        /// <summary>
        /// 使用订阅线点集重新拟合测量线，点集不可用时回退到订阅线端点。
        /// </summary>
        private static MeasuredLine BuildStableLine(LineMeasurementInput input, GeometryMeasureMode mode)
        {
            if (input == null || input.Line == null)
                return null;

            List<PointF> points = GetValidModePoints(input.Points, mode);
            if (points.Count < 2)
                return ApplyMode(input.Line, mode);

            double centerX = points.Average(point => point.X);
            double centerY = points.Average(point => point.Y);
            double xx = 0;
            double xy = 0;
            double yy = 0;
            foreach (PointF point in points)
            {
                double dx = point.X - centerX;
                double dy = point.Y - centerY;
                xx += dx * dx;
                xy += dx * dy;
                yy += dy * dy;
            }

            double angle = 0.5 * Math.Atan2(2.0 * xy, xx - yy);
            PointF direction = new PointF((float)Math.Cos(angle), (float)Math.Sin(angle));
            PointF center = new PointF((float)centerX, (float)centerY);
            double minProjection = double.MaxValue;
            double maxProjection = double.MinValue;
            foreach (PointF point in points)
            {
                double projection = Dot(Subtract(point, center), direction);
                minProjection = Math.Min(minProjection, projection);
                maxProjection = Math.Max(maxProjection, projection);
            }

            return new MeasuredLine
            {
                Start = Add(center, Scale(direction, minProjection)),
                End = Add(center, Scale(direction, maxProjection)),
                Source = input.Line.Source
            };
        }

        /// <summary>
        /// 计算两条线点集到对向直线的绝对垂距统计，没有点集时使用线段端点兜底。
        /// </summary>
        private static LineDistanceStatistics CalculateDistanceStatistics(
            LineMeasurementInput input1,
            LineMeasurementInput input2,
            MeasuredLine line1,
            MeasuredLine line2,
            GeometryMeasureMode mode)
        {
            var distances = new List<double>();
            AddPointDistances(distances, input1?.Points, line2, mode);
            AddPointDistances(distances, input2?.Points, line1, mode);

            if (distances.Count == 0)
            {
                AddPointDistances(distances, BuildEndpointPoints(line1), line2, mode);
                AddPointDistances(distances, BuildEndpointPoints(line2), line1, mode);
            }

            if (distances.Count == 0)
                return new LineDistanceStatistics();

            return new LineDistanceStatistics
            {
                AverageDistance = distances.Average(),
                MinDistance = distances.Min(),
                MaxDistance = distances.Max(),
                PointCount = distances.Count
            };
        }

        /// <summary>
        /// 将有效点转换到当前测量精度模式。
        /// </summary>
        private static List<PointF> GetValidModePoints(IEnumerable<PointF> points, GeometryMeasureMode mode)
        {
            if (points == null)
                return new List<PointF>();

            var result = new List<PointF>();
            foreach (PointF point in points)
            {
                if (float.IsNaN(point.X) || float.IsNaN(point.Y) ||
                    float.IsInfinity(point.X) || float.IsInfinity(point.Y))
                {
                    continue;
                }

                result.Add(GeometryMeasurementAlgorithm.ApplyMode(point, mode));
            }

            return result;
        }

        /// <summary>
        /// 将点集到目标线的垂距加入统计集合。
        /// </summary>
        private static void AddPointDistances(List<double> distances, IEnumerable<PointF> points, MeasuredLine targetLine, GeometryMeasureMode mode)
        {
            if (distances == null || targetLine == null || !targetLine.IsValid)
                return;

            foreach (PointF point in GetValidModePoints(points, mode))
                distances.Add(PointToLineDistance(point, targetLine));
        }

        /// <summary>
        /// 构建线段端点集合，用于没有边缘点时兜底统计。
        /// </summary>
        private static IEnumerable<PointF> BuildEndpointPoints(MeasuredLine line)
        {
            if (line == null || !line.IsValid)
                yield break;

            yield return line.Start;
            yield return line.End;
        }

        /// <summary>
        /// 计算点到无限直线的绝对垂直距离。
        /// </summary>
        private static double PointToLineDistance(PointF point, MeasuredLine line)
        {
            double dx = line.End.X - line.Start.X;
            double dy = line.End.Y - line.Start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-8)
                return 0;

            return Math.Abs((point.X - line.Start.X) * dy - (point.Y - line.Start.Y) * dx) / length;
        }

        /// <summary>
        /// 将订阅线段转换到当前测量精度模式。
        /// </summary>
        private static MeasuredLine ApplyMode(MeasuredLine line, GeometryMeasureMode mode)
        {
            return new MeasuredLine
            {
                Start = GeometryMeasurementAlgorithm.ApplyMode(line.Start, mode),
                End = GeometryMeasurementAlgorithm.ApplyMode(line.End, mode),
                Source = line.Source
            };
        }

        private static PointF Add(PointF a, PointF b)
        {
            return new PointF(a.X + b.X, a.Y + b.Y);
        }

        private static PointF Subtract(PointF a, PointF b)
        {
            return new PointF(a.X - b.X, a.Y - b.Y);
        }

        private static PointF Scale(PointF point, double scale)
        {
            return new PointF((float)(point.X * scale), (float)(point.Y * scale));
        }

        private static double Dot(PointF a, PointF b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        /// <summary>读取全部位置修正；仅绘制模式启用修正时展开多目标。</summary>
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamLineLineAngle param)
        {
            if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };

            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>把单次算法结果转换为强类型目标结果。</summary>
        private static LineLineAngleTargetResult CreateTargetResult(LineLineAngleMeasureResult measure)
        {
            if (measure == null)
                throw new Exception("线到线夹角算法没有返回结果。");

            var item = new LineLineAngleTargetResult
            {
                IsOk = measure.Success,
                ErrorMessage = measure.Success ? string.Empty : measure.Message,
                AlgorithmMs = measure.Success ? MeasurementResultRounder.Round(measure.AlgorithmMs) : 0,
                RawResult = measure
            };
            if (!measure.Success)
                return item;

            item.Angle = MeasurementResultRounder.Round(measure.Angle);
            item.Line1StartX = MeasurementResultRounder.Round(measure.Line1.Start.X);
            item.Line1StartY = MeasurementResultRounder.Round(measure.Line1.Start.Y);
            item.Line1EndX = MeasurementResultRounder.Round(measure.Line1.End.X);
            item.Line1EndY = MeasurementResultRounder.Round(measure.Line1.End.Y);
            item.Line2StartX = MeasurementResultRounder.Round(measure.Line2.Start.X);
            item.Line2StartY = MeasurementResultRounder.Round(measure.Line2.Start.Y);
            item.Line2EndX = MeasurementResultRounder.Round(measure.Line2.End.X);
            item.Line2EndY = MeasurementResultRounder.Round(measure.Line2.End.Y);
            if (measure.IntersectionPoint.HasValue)
            {
                item.IntersectionX = MeasurementResultRounder.Round(measure.IntersectionPoint.Value.X);
                item.IntersectionY = MeasurementResultRounder.Round(measure.IntersectionPoint.Value.Y);
            }
            item.AverageDistance = MeasurementResultRounder.Round(measure.AverageDistance);
            item.MinDistance = MeasurementResultRounder.Round(measure.MinDistance);
            item.MaxDistance = MeasurementResultRounder.Round(measure.MaxDistance);
            item.DistancePointCount = measure.DistancePointCount;
            return item;
        }

        /// <summary>创建保留目标顺序且数值为零的失败项。</summary>
        private static LineLineAngleTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            string message = exception == null ? "线到线夹角失败。" : exception.Message;
            return new LineLineAngleTargetResult
            {
                IsOk = false,
                ErrorMessage = message,
                RawResult = new LineLineAngleMeasureResult { Success = false, Message = message }
            };
        }

        /// <summary>读取订阅节点的全部目标直线及其边缘点；没有明细时回退为目标1摘要。</summary>
        private List<IndexedMeasurementValue<LineMeasurementInput>> ReadSubscribedLineTargets(
            NodeSubscription subscription,
            string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadMultiTargetItems(
                sourceNode.Result,
                out List<IMultiTargetMeasurementItem> sourceItems))
            {
                LineMeasurementInput input = ReadSubscribedLineInput(subscription, name);
                return new List<IndexedMeasurementValue<LineMeasurementInput>>
                {
                    new IndexedMeasurementValue<LineMeasurementInput>(1, true, input, string.Empty)
                };
            }

            string sourceName = $"{sourceNode.ID}.{sourceNode.NodeName}";
            var targets = new List<IndexedMeasurementValue<LineMeasurementInput>>(sourceItems.Count);
            for (int i = 0; i < sourceItems.Count; i++)
            {
                IMultiTargetMeasurementItem item = sourceItems[i];
                int targetIndex = item == null ? i + 1 : item.TargetIndex;
                string error = item == null
                    ? $"{name}订阅节点({sourceName})的目标{targetIndex}结果为空。"
                    : item.ErrorMessage;
                MeasuredLine line = null;
                bool isOk = item != null && item.IsOk &&
                    MeasurementResultReader.TryReadLine((object)item, out line);
                if (!isOk && string.IsNullOrWhiteSpace(error))
                    error = $"{name}订阅节点({sourceName})的目标{targetIndex}没有可用直线结果。";

                var input = new LineMeasurementInput
                {
                    Line = isOk ? line : null,
                    SourceName = sourceName
                };
                if (isOk && MeasurementResultReader.TryReadLinePoints((object)item, out List<PointF> points))
                    input.Points = points;

                targets.Add(new IndexedMeasurementValue<LineMeasurementInput>(
                    targetIndex,
                    isOk,
                    input,
                    error,
                    item == null ? null : item.Correction));
            }

            return targets;
        }

        /// <summary>创建不改变坐标的单目标修正项。</summary>
        private static PositionCorrectionInfo CreateIdentityCorrection(int targetIndex = 1)
        {
            return new PositionCorrectionInfo
            {
                TargetIndex = targetIndex,
                IsValid = true,
                BaseScaleX = 1,
                BaseScaleY = 1,
                CurrentScaleX = 1,
                CurrentScaleY = 1
            };
        }

        /// <summary>优先沿用两侧上游目标的位置修正信息，否则创建对应编号的恒等修正。</summary>
        private static PositionCorrectionInfo ResolvePairCorrection(
            IndexedMeasurementPair<LineMeasurementInput, LineMeasurementInput> pair)
        {
            PositionCorrectionInfo firstCorrection = pair.First.Context as PositionCorrectionInfo;
            if (firstCorrection != null && firstCorrection.TargetIndex == pair.TargetIndex)
                return firstCorrection;

            PositionCorrectionInfo secondCorrection = pair.Second.Context as PositionCorrectionInfo;
            if (secondCorrection != null && secondCorrection.TargetIndex == pair.TargetIndex)
                return secondCorrection;

            return CreateIdentityCorrection(pair.TargetIndex);
        }

        /// <summary>为单个模板目标构造已经仿射变换的运行参数。</summary>
        private NodeParamLineLineAngle BuildRuntimeParam(NodeParamLineLineAngle param, PositionCorrectionInfo correction)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionHelper.EnsureValid(correction);
            PointF l1Start = _transformService.TransformPoint(new PointF(param.Line1StartX, param.Line1StartY), correction);
            PointF l1End = _transformService.TransformPoint(new PointF(param.Line1EndX, param.Line1EndY), correction);
            PointF l2Start = _transformService.TransformPoint(new PointF(param.Line2StartX, param.Line2StartY), correction);
            PointF l2End = _transformService.TransformPoint(new PointF(param.Line2EndX, param.Line2EndY), correction);
            double scale = GetAverageScale(correction);

            return new NodeParamLineLineAngle
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                SourceMode = param.SourceMode,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                MeasureMode = param.MeasureMode,
                Line1StartX = l1Start.X,
                Line1StartY = l1Start.Y,
                Line1EndX = l1End.X,
                Line1EndY = l1End.Y,
                Line2StartX = l2Start.X,
                Line2StartY = l2Start.Y,
                Line2EndX = l2End.X,
                Line2EndY = l2End.Y,
                CaliperWidth = (float)(param.CaliperWidth * scale),
                CaliperHeight = (float)(param.CaliperHeight * scale),
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize,
                Line1CaliperWidth = (float)(param.Line1CaliperWidth * scale),
                Line1CaliperHeight = (float)(param.Line1CaliperHeight * scale),
                Line1Count = param.Line1Count,
                Line1EdgeStrength = param.Line1EdgeStrength,
                Line1Polarity = param.Line1Polarity,
                Line1FindMode = param.Line1FindMode,
                Line1Direction = param.Line1Direction,
                Line1BlurSize = param.Line1BlurSize,
                Line1SamplingMode = param.Line1SamplingMode,
                Line2CaliperWidth = (float)(param.Line2CaliperWidth * scale),
                Line2CaliperHeight = (float)(param.Line2CaliperHeight * scale),
                Line2Count = param.Line2Count,
                Line2EdgeStrength = param.Line2EdgeStrength,
                Line2Polarity = param.Line2Polarity,
                Line2FindMode = param.Line2FindMode,
                Line2Direction = param.Line2Direction,
                Line2BlurSize = param.Line2BlurSize,
                Line2SamplingMode = param.Line2SamplingMode
            };
        }

        /// <summary>计算各向缩放的平均值，供卡尺宽高等标量尺寸使用。</summary>
        private static double GetAverageScale(PositionCorrectionInfo correction)
        {
            double scaleX = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleX) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleX);
            double scaleY = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleY) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleY);
            return (scaleX + scaleY) * 0.5;
        }

        private static CaliperLineParams BuildLineParams(NodeParamLineLineAngle param, bool firstLine)
        {
            return new CaliperLineParams
            {
                StartX = firstLine ? param.Line1StartX : param.Line2StartX,
                StartY = firstLine ? param.Line1StartY : param.Line2StartY,
                EndX = firstLine ? param.Line1EndX : param.Line2EndX,
                EndY = firstLine ? param.Line1EndY : param.Line2EndY,
                CaliperWidth = param.GetCaliperWidth(firstLine),
                CaliperHeight = param.GetCaliperHeight(firstLine),
                Count = param.GetCount(firstLine),
                EdgeStrength = param.GetEdgeStrength(firstLine),
                Polarity = param.GetPolarity(firstLine),
                FindMode = param.GetFindMode(firstLine),
                Direction = param.GetDirection(firstLine),
                SamplingMode = param.GetSamplingMode(firstLine),
                BlurSize = param.GetBlurSize(firstLine)
            };
        }

        private static LineLineAngleMeasureResult FinishFailed(LineLineAngleMeasureResult result, Stopwatch stopwatch, string message)
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
                    if (string.IsNullOrWhiteSpace(nodeSubscriptionLine1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionLine2.GetText1()))
                        throw new Exception("请选择两条上游直线结果。");
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

                RoiRunSettings line1Settings = _roiRunSettings[0];
                RoiRunSettings line2Settings = _roiRunSettings[1];
                Params = new NodeParamLineLineAngle
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    SourceMode = sourceMode,
                    Line1Text1 = nodeSubscriptionLine1.GetText1(),
                    Line1Text2 = nodeSubscriptionLine1.GetText2(),
                    Line2Text1 = nodeSubscriptionLine2.GetText1(),
                    Line2Text2 = nodeSubscriptionLine2.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    MeasureMode = (GeometryMeasureMode)comboBoxMeasureMode.SelectedItem,
                    Line1StartX = ParseFloat(textBoxL1StartX, "直线1起点X"),
                    Line1StartY = ParseFloat(textBoxL1StartY, "直线1起点Y"),
                    Line1EndX = ParseFloat(textBoxL1EndX, "直线1终点X"),
                    Line1EndY = ParseFloat(textBoxL1EndY, "直线1终点Y"),
                    Line2StartX = ParseFloat(textBoxL2StartX, "直线2起点X"),
                    Line2StartY = ParseFloat(textBoxL2StartY, "直线2起点Y"),
                    Line2EndX = ParseFloat(textBoxL2EndX, "直线2终点X"),
                    Line2EndY = ParseFloat(textBoxL2EndY, "直线2终点Y"),
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem,
                    FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem,
                    Direction = line1Settings.Direction,
                    Line1CaliperWidth = line1Settings.CaliperWidth,
                    Line1CaliperHeight = line1Settings.CaliperHeight,
                    Line1Count = line1Settings.Count,
                    Line1EdgeStrength = line1Settings.EdgeStrength,
                    Line1BlurSize = line1Settings.BlurSize,
                    Line1Polarity = line1Settings.Polarity,
                    Line1FindMode = line1Settings.FindMode,
                    Line1Direction = line1Settings.Direction,
                    Line1SamplingMode = line1Settings.SamplingMode,
                    Line2CaliperWidth = line2Settings.CaliperWidth,
                    Line2CaliperHeight = line2Settings.CaliperHeight,
                    Line2Count = line2Settings.Count,
                    Line2EdgeStrength = line2Settings.EdgeStrength,
                    Line2BlurSize = line2Settings.BlurSize,
                    Line2Polarity = line2Settings.Polarity,
                    Line2FindMode = line2Settings.FindMode,
                    Line2Direction = line2Settings.Direction,
                    Line2SamplingMode = line2Settings.SamplingMode
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
                PointF l1Start = new PointF(ParseFloat(textBoxL1StartX, string.Empty), ParseFloat(textBoxL1StartY, string.Empty));
                PointF l1End = new PointF(ParseFloat(textBoxL1EndX, string.Empty), ParseFloat(textBoxL1EndY, string.Empty));
                PointF l2Start = new PointF(ParseFloat(textBoxL2StartX, string.Empty), ParseFloat(textBoxL2StartY, string.Empty));
                PointF l2End = new PointF(ParseFloat(textBoxL2EndX, string.Empty), ParseFloat(textBoxL2EndY, string.Empty));
                if (correctionInfo != null)
                {
                    l1Start = correctionInfo.TransformPoint(l1Start.X, l1Start.Y);
                    l1End = correctionInfo.TransformPoint(l1End.X, l1End.Y);
                    l2Start = correctionInfo.TransformPoint(l2Start.X, l2Start.Y);
                    l2End = correctionInfo.TransformPoint(l2End.X, l2End.Y);
                }

                showImageControl1.ClearDynamicRoi();
                double displayScale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
                showImageControl1.AddDynamicCaliper(l1Start.X, l1Start.Y, l1End.X, l1End.Y, (float)(_roiRunSettings[0].CaliperWidth * displayScale), (float)(_roiRunSettings[0].CaliperHeight * displayScale), _roiRunSettings[0].Count, Color.Lime, "Line1");
                showImageControl1.AddDynamicCaliper(l2Start.X, l2Start.Y, l2End.X, l2End.Y, (float)(_roiRunSettings[1].CaliperWidth * displayScale), (float)(_roiRunSettings[1].CaliperHeight * displayScale), _roiRunSettings[1].Count, Color.DeepSkyBlue, "Line2");
                var rois = showImageControl1.GetAllDynamicCalipers();
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
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count < 2)
                return;

            PointF l1Start = new PointF(rois[0].SX, rois[0].SY);
            PointF l1End = new PointF(rois[0].EX, rois[0].EY);
            PointF l2Start = new PointF(rois[1].SX, rois[1].SY);
            PointF l2End = new PointF(rois[1].EX, rois[1].EY);
            PointF roiCenter = MeasurementNodeHelper.CalculateBoundsCenter(new[] { l1Start, l1End, l2Start, l2End });
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, requireCorrection);
            if (correctionInfo != null)
            {
                l1Start = _transformService.InverseTransformPoint(l1Start, correctionInfo);
                l1End = _transformService.InverseTransformPoint(l1End, correctionInfo);
                l2Start = _transformService.InverseTransformPoint(l2Start, correctionInfo);
                l2End = _transformService.InverseTransformPoint(l2End, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxL1StartX.Text = l1Start.X.ToString("F2");
                textBoxL1StartY.Text = l1Start.Y.ToString("F2");
                textBoxL1EndX.Text = l1End.X.ToString("F2");
                textBoxL1EndY.Text = l1End.Y.ToString("F2");
                textBoxL2StartX.Text = l2Start.X.ToString("F2");
                textBoxL2StartY.Text = l2Start.Y.ToString("F2");
                textBoxL2EndX.Text = l2End.X.ToString("F2");
                textBoxL2EndY.Text = l2End.Y.ToString("F2");
                SaveRunSettingsFromRoi(_roiRunSettings[0], rois[0]);
                SaveRunSettingsFromRoi(_roiRunSettings[1], rois[1]);
                if (correctionInfo != null)
                {
                    double scale = GetAverageScale(correctionInfo);
                    _roiRunSettings[0].CaliperWidth = (float)(_roiRunSettings[0].CaliperWidth / scale);
                    _roiRunSettings[0].CaliperHeight = (float)(_roiRunSettings[0].CaliperHeight / scale);
                    _roiRunSettings[1].CaliperWidth = (float)(_roiRunSettings[1].CaliperWidth / scale);
                    _roiRunSettings[1].CaliperHeight = (float)(_roiRunSettings[1].CaliperHeight / scale);
                }
                LoadRunSettingsToControls(_roiRunSettings[_selectedRunRoiIndex]);
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count < 2)
                return;

            PointF l1Start = new PointF(rois[0].SX, rois[0].SY);
            PointF l1End = new PointF(rois[0].EX, rois[0].EY);
            PointF l2Start = new PointF(rois[1].SX, rois[1].SY);
            PointF l2End = new PointF(rois[1].EX, rois[1].EY);
            PointF roiCenter = MeasurementNodeHelper.CalculateBoundsCenter(new[] { l1Start, l1End, l2Start, l2End });
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, false);
            if (correctionInfo != null)
            {
                l1Start = _transformService.InverseTransformPoint(l1Start, correctionInfo);
                l1End = _transformService.InverseTransformPoint(l1End, correctionInfo);
                l2Start = _transformService.InverseTransformPoint(l2Start, correctionInfo);
                l2End = _transformService.InverseTransformPoint(l2End, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxL1StartX)) textBoxL1StartX.Text = l1Start.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxL1StartY)) textBoxL1StartY.Text = l1Start.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxL1EndX)) textBoxL1EndX.Text = l1End.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxL1EndY)) textBoxL1EndY.Text = l1End.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxL2StartX)) textBoxL2StartX.Text = l2Start.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxL2StartY)) textBoxL2StartY.Text = l2Start.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxL2EndX)) textBoxL2EndX.Text = l2End.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxL2EndY)) textBoxL2EndY.Text = l2End.Y.ToString("F2");
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

            var calipers = showImageControl1.GetAllDynamicCalipers();
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
                List<LineLineAngleTargetResult> items = ExecuteMeasures((NodeParamLineLineAngle)Params, CancellationToken.None);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodeLineLineAngle.BuildDisplayResult(items));

                if (items.Exists(item => !item.IsOk))
                    MessageBoxTD.Show("部分模板目标线到线夹角失败，失败目标结果已保留为0。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})线到线夹角异常，原因：{ex.Message}", true);
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
