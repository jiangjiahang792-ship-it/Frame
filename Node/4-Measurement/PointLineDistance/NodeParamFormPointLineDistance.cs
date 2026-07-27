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

namespace TDJS_Vision.Node._4_Measurement.PointLineDistance
{
    /// <summary>
    /// 点到线距离测量参数窗体。
    /// </summary>
    public partial class NodeParamFormPointLineDistance : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点，用于订阅上游数据和写日志。
        /// </summary>
        private readonly NodeBase node;

        /// <summary>唯一编辑ROI的目标归属与坐标变换服务。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();

        /// <summary>
        /// 是否正在同步控件与 ROI，避免事件递归刷新。
        /// </summary>
        private bool _isSyncingRoi;

        /// <summary>
        /// 当前预览图上是否已经绘制动态 ROI。
        /// </summary>
        private bool _roiVisible;

        /// <summary>
        /// 当前正在编辑的运行参数对象，0 表示点，1 表示直线。
        /// </summary>
        private int _selectedRunRoiIndex;

        /// <summary>
        /// 点圆卡尺和直线卡尺各自的运行参数。
        /// </summary>
        private readonly RoiRunSettings[] _roiRunSettings =
        {
            new RoiRunSettings(10, 60, 30, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3),
            new RoiRunSettings(20, 120, 15, 20, CaliperEdgePolarity.Both, CaliperEdgeFindMode.Best, 0, 3)
        };

        /// <summary>
        /// 单个绘制对象的卡尺运行参数。
        /// </summary>
        private sealed class RoiRunSettings
        {
            /// <summary>
            /// 创建卡尺运行参数。
            /// </summary>
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

            /// <summary>
            /// 卡尺宽度。
            /// </summary>
            public float CaliperWidth { get; set; }

            /// <summary>
            /// 卡尺高度。
            /// </summary>
            public float CaliperHeight { get; set; }

            /// <summary>
            /// 卡尺数量。
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// 边缘强度阈值。
            /// </summary>
            public int EdgeStrength { get; set; }

            /// <summary>
            /// 边缘极性。
            /// </summary>
            public CaliperEdgePolarity Polarity { get; set; }

            /// <summary>
            /// 边缘查找模式。
            /// </summary>
            public CaliperEdgeFindMode FindMode { get; set; }

            /// <summary>
            /// 查找方向。
            /// </summary>
            public int Direction { get; set; }

            /// <summary>
            /// 平滑核大小。
            /// </summary>
            public int BlurSize { get; set; }

            /// <summary>
            /// 当前运行 ROI 的灰度剖面采样模式。
            /// </summary>
            public CaliperSamplingMode SamplingMode { get; set; } = CaliperSamplingMode.Fast;
        }

        /// <summary>
        /// 显示中文文本并保存原始枚举值的下拉框项。
        /// </summary>
        private sealed class ComboItem<T>
        {
            /// <summary>
            /// 创建下拉框项。
            /// </summary>
            public ComboItem(T value, string text)
            {
                Value = value;
                Text = text;
            }

            /// <summary>
            /// 业务枚举值。
            /// </summary>
            public T Value { get; }

            /// <summary>
            /// 界面显示文本。
            /// </summary>
            public string Text { get; }

            /// <summary>
            /// 返回界面显示文本。
            /// </summary>
            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>
        /// 创建点到线距离测量参数窗体。
        /// </summary>
        public NodeParamFormPointLineDistance(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeCombos();
            InitializeRunRoiTargets();
            BindRoiRefreshEvents();
            nodeSubscriptionPoint.HideText2();
            nodeSubscriptionLine.HideText2();
            UpdateSourceModeEnabled();
        }

        /// <summary>
        /// 当前节点参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 初始化订阅控件所属节点。
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPoint.Init(node);
            nodeSubscriptionLine.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
            nodeSubscriptionPositionCorrection.Init(node);
        }

        /// <summary>
        /// 将参数对象同步到界面控件。
        /// </summary>
        public void SetParam2Form()
        {
            var param = Params as NodeParamPointLineDistance;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                radioButtonSubscribe.Checked = param.SourceMode == MeasurementDataSourceMode.Subscribe;
                radioButtonDraw.Checked = param.SourceMode != MeasurementDataSourceMode.Subscribe;
                nodeSubscriptionPoint.SetText(param.PointText1, param.PointText2);
                SelectComboValue(comboBoxPointRole, param.PointRole);
                nodeSubscriptionLine.SetText(param.LineText1, param.LineText2);
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                SelectComboValue(comboBoxMeasureMode, param.MeasureMode);
                textBoxPointCenterX.Text = param.PointCenterX.ToString();
                textBoxPointCenterY.Text = param.PointCenterY.ToString();
                textBoxPointRadius.Text = param.PointRadius.ToString();
                textBoxLineStartX.Text = param.LineStartX.ToString();
                textBoxLineStartY.Text = param.LineStartY.ToString();
                textBoxLineEndX.Text = param.LineEndX.ToString();
                textBoxLineEndY.Text = param.LineEndY.ToString();
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

        /// <summary>
        /// 按目标顺序执行全部点到线距离测量；订阅模式按目标编号配对点和直线。
        /// </summary>
        internal List<PointLineDistanceTargetResult> ExecuteMeasures(NodeParamPointLineDistance param, CancellationToken token)
        {
            if (param == null)
                throw new Exception("点到线距离参数为空。");

            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
                return ExecuteSubscribedMeasures(param, token);

            IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
            if (corrections.Count == 0)
                return new List<PointLineDistanceTargetResult>();

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

        /// <summary>
        /// 一次读取订阅点和直线的全部目标明细，按目标编号配对后串行计算距离。
        /// </summary>
        private List<PointLineDistanceTargetResult> ExecuteSubscribedMeasures(
            NodeParamPointLineDistance param,
            CancellationToken token)
        {
            List<IndexedMeasurementValue<PointF>> points = ReadSubscribedPointTargets(
                nodeSubscriptionPoint,
                param.PointRole,
                "点");
            List<IndexedMeasurementValue<MeasuredLine>> lines = ReadSubscribedLineTargets(
                nodeSubscriptionLine,
                "直线");
            List<IndexedMeasurementPair<PointF, MeasuredLine>> pairs =
                MultiTargetMeasurementPairer.PairByTargetIndex(points, lines, "点结果", "直线结果");
            if (pairs.Count == 0)
                return new List<PointLineDistanceTargetResult>();

            var pairMap = new Dictionary<int, IndexedMeasurementPair<PointF, MeasuredLine>>(pairs.Count);
            var corrections = new List<PositionCorrectionInfo>(pairs.Count);
            foreach (IndexedMeasurementPair<PointF, MeasuredLine> pair in pairs)
            {
                pairMap.Add(pair.TargetIndex, pair);
                corrections.Add(ResolvePairCorrection(pair));
            }

            return MultiTargetMeasurementRunner.Run(
                corrections,
                token,
                correction =>
                {
                    IndexedMeasurementPair<PointF, MeasuredLine> pair = pairMap[correction.TargetIndex];
                    if (!pair.IsOk)
                    {
                        string reason = string.IsNullOrWhiteSpace(pair.ErrorMessage)
                            ? $"目标{pair.TargetIndex}的点或直线结果无效。"
                            : pair.ErrorMessage;
                        throw new Exception(reason);
                    }

                    return CreateTargetResult(ExecuteSubscribedMeasure(
                        param,
                        pair.First.Value,
                        pair.Second.Value));
                },
                CreateFailure);
        }

        /// <summary>
        /// 使用已经完成目标配对的点和直线计算一次距离，不再重复读取上游节点结果。
        /// </summary>
        private static PointLineDistanceMeasureResult ExecuteSubscribedMeasure(
            NodeParamPointLineDistance param,
            PointF targetPoint,
            MeasuredLine line)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PointLineDistanceMeasureResult();
            if (line == null || !line.IsValid)
                return FinishFailed(result, stopwatch, "直线结果无效");

            try
            {
                result.DistanceResult = GeometryMeasurementAlgorithm.CalculatePointLineDistance(
                    targetPoint,
                    line.Start,
                    line.End,
                    param.MeasureMode);
                result.Success = true;
            }
            catch (Exception ex)
            {
                return FinishFailed(result, stopwatch, ex.Message);
            }

            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>执行一次已经完成坐标变换的点到线距离测量。</summary>
        private PointLineDistanceMeasureResult ExecuteSingleMeasure(NodeParamPointLineDistance param, Mat sharedGray)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PointLineDistanceMeasureResult();
            PointF targetPoint;
            MeasuredLine line;

            if (param.SourceMode == MeasurementDataSourceMode.Subscribe)
            {
                targetPoint = ReadSubscribedPoint(nodeSubscriptionPoint, param.PointRole, "点");
                line = ReadSubscribedLine(nodeSubscriptionLine, "直线");
            }
            else
            {
                Mat gray = sharedGray;
                bool disposeGrayAfterUse = false;
                if (gray == null)
                    gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    CaliperCircleMeasureResult pointResult = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(param));
                    CaliperLineMeasureResult lineResult = CaliperMeasurementAlgorithm.FindLine(gray, BuildLineParams(param));
                    result.PointEdgePoints.AddRange(pointResult.EdgePoints);
                    result.LineEdgePoints.AddRange(lineResult.EdgePoints);

                    if (!pointResult.Success)
                        return FinishFailed(result, stopwatch, "点圆卡尺找点失败");
                    if (!lineResult.Success)
                        return FinishFailed(result, stopwatch, "直线卡尺找线失败");

                    targetPoint = pointResult.Center;
                    line = new MeasuredLine { Start = lineResult.LineStart, End = lineResult.LineEnd, Source = "绘制直线" };
                }
                finally
                {
                    if (disposeGrayAfterUse)
                        gray?.Dispose();
                }
            }

            if (line == null || !line.IsValid)
                return FinishFailed(result, stopwatch, "直线结果无效");

            try
            {
                result.DistanceResult = GeometryMeasurementAlgorithm.CalculatePointLineDistance(
                    targetPoint,
                    line.Start,
                    line.End,
                    param.MeasureMode);
            }
            catch (Exception ex)
            {
                return FinishFailed(result, stopwatch, ex.Message);
            }

            result.Success = true;
            stopwatch.Stop();
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// 初始化下拉框，界面显示中文，业务层仍保存枚举值。
        /// </summary>
        private void InitializeCombos()
        {
            AddComboItem(comboBoxPointRole, MeasurementPointRole.Auto, "自动");
            AddComboItem(comboBoxPointRole, MeasurementPointRole.Center, "中心点");
            AddComboItem(comboBoxPointRole, MeasurementPointRole.StartPoint, "首个点");
            AddComboItem(comboBoxPointRole, MeasurementPointRole.EndPoint, "最后点");
            SelectComboValue(comboBoxPointRole, MeasurementPointRole.Auto);

            AddComboItem(comboBoxMeasureMode, GeometryMeasureMode.SubPixel, "亚像素");
            AddComboItem(comboBoxMeasureMode, GeometryMeasureMode.Pixel, "像素");
            SelectComboValue(comboBoxMeasureMode, GeometryMeasureMode.SubPixel);

            AddComboItem(comboBoxPolarity, CaliperEdgePolarity.Both, "任意边缘");
            AddComboItem(comboBoxPolarity, CaliperEdgePolarity.DarkToLight, "暗到亮");
            AddComboItem(comboBoxPolarity, CaliperEdgePolarity.LightToDark, "亮到暗");
            SelectComboValue(comboBoxPolarity, CaliperEdgePolarity.Both);

            AddComboItem(comboBoxFindMode, CaliperEdgeFindMode.Best, "最佳");
            AddComboItem(comboBoxFindMode, CaliperEdgeFindMode.First, "第一个");
            AddComboItem(comboBoxFindMode, CaliperEdgeFindMode.Last, "最后一个");
            SelectComboValue(comboBoxFindMode, CaliperEdgeFindMode.Best);

            UpdateDirectionItems(true);

            comboBoxSamplingMode.Items.Add("快速采样");
            comboBoxSamplingMode.Items.Add("抗干扰采样");
            comboBoxSamplingMode.SelectedIndex = 0;
        }

        /// <summary>
        /// 初始化点/线运行参数选择框。
        /// </summary>
        private void InitializeRunRoiTargets()
        {
            comboBoxRunRoiTarget.Items.Add("点");
            comboBoxRunRoiTarget.Items.Add("直线");
            comboBoxRunRoiTarget.SelectedIndex = 0;
            comboBoxRunRoiTarget.SelectedIndexChanged += comboBoxRunRoiTarget_SelectedIndexChanged;
            showImageControl1.MouseUp += showImageControl1_MouseUp;
        }

        /// <summary>
        /// 绑定需要刷新 ROI 的控件事件。
        /// </summary>
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

        /// <summary>
        /// 将参数对象中的点/线卡尺设置加载到运行参数缓存。
        /// </summary>
        private void LoadRunSettingsFromParam(NodeParamPointLineDistance param)
        {
            _roiRunSettings[0].CaliperWidth = param.PointCaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[0].CaliperHeight = param.PointCaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[0].Count = param.PointCount ?? param.Count;
            _roiRunSettings[0].EdgeStrength = param.PointEdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[0].Polarity = param.PointPolarity ?? param.Polarity;
            _roiRunSettings[0].FindMode = param.PointFindMode ?? param.FindMode;
            _roiRunSettings[0].Direction = param.PointDirection ?? param.Direction;
            _roiRunSettings[0].BlurSize = param.PointBlurSize ?? param.BlurSize;
            _roiRunSettings[0].SamplingMode = param.PointSamplingMode;

            _roiRunSettings[1].CaliperWidth = param.LineCaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[1].CaliperHeight = param.LineCaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[1].Count = param.LineCount ?? param.Count;
            _roiRunSettings[1].EdgeStrength = param.LineEdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[1].Polarity = param.LinePolarity ?? param.Polarity;
            _roiRunSettings[1].FindMode = param.LineFindMode ?? param.FindMode;
            _roiRunSettings[1].Direction = param.LineDirection ?? param.Direction;
            _roiRunSettings[1].BlurSize = param.LineBlurSize ?? param.BlurSize;
            _roiRunSettings[1].SamplingMode = param.LineSamplingMode;
        }

        /// <summary>
        /// 将当前选中对象的运行参数加载到控件。
        /// </summary>
        private void LoadRunSettingsToControls(RoiRunSettings settings)
        {
            if (settings == null)
                return;

            UpdateDirectionItems(_selectedRunRoiIndex == 0);
            textBoxCaliperWidth.Text = settings.CaliperWidth.ToString();
            textBoxCaliperHeight.Text = settings.CaliperHeight.ToString();
            textBoxCount.Text = settings.Count.ToString();
            textBoxEdgeStrength.Text = settings.EdgeStrength.ToString();
            textBoxBlurSize.Text = settings.BlurSize.ToString();
            SelectComboValue(comboBoxPolarity, settings.Polarity);
            SelectComboValue(comboBoxFindMode, settings.FindMode);
            comboBoxDirection.SelectedIndex = settings.Direction == 1 ? 1 : 0;
            comboBoxSamplingMode.SelectedIndex = settings.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;
        }

        /// <summary>
        /// 将当前控件值保存到当前选中对象的运行参数缓存。
        /// </summary>
        private void SaveCurrentRunSettingsFromControls()
        {
            if (_selectedRunRoiIndex < 0 || _selectedRunRoiIndex >= _roiRunSettings.Length)
                return;

            RoiRunSettings settings = _roiRunSettings[_selectedRunRoiIndex];
            settings.CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度");
            settings.CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度");
            settings.Count = ParseInt(textBoxCount, "卡尺数量");
            settings.EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值");
            settings.BlurSize = ParseInt(textBoxBlurSize, "平滑核");
            settings.Polarity = GetComboValue<CaliperEdgePolarity>(comboBoxPolarity);
            settings.FindMode = GetComboValue<CaliperEdgeFindMode>(comboBoxFindMode);
            settings.Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;
            settings.SamplingMode = comboBoxSamplingMode.SelectedIndex == 1
                ? CaliperSamplingMode.AntiInterference
                : CaliperSamplingMode.Fast;
        }

        /// <summary>
        /// 保存点圆卡尺 ROI 上被交互修改的运行参数。
        /// </summary>
        private void SaveRunSettingsFromRoi(RoiRunSettings settings, TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiCircleCaliper roi)
        {
            settings.CaliperWidth = roi.CaliperWidth;
            settings.CaliperHeight = roi.CaliperHeight;
            settings.Count = roi.Count;
            settings.Direction = roi.Direction;
        }

        /// <summary>
        /// 保存直线卡尺 ROI 上被交互修改的运行参数。
        /// </summary>
        private void SaveRunSettingsFromRoi(RoiRunSettings settings, TDJS_Vision.Forms.DispShowImage.ShowImageControl.RoiCaliper roi)
        {
            settings.CaliperWidth = roi.CaliperWidth;
            settings.CaliperHeight = roi.CaliperHeight;
            settings.Count = roi.Count;
            settings.Direction = roi.Direction;
        }

        /// <summary>
        /// ROI 参数变更时刷新预览区域。
        /// </summary>
        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            SyncCurrentRoiToFieldsExcept(sender);
            if (_roiVisible)
                RefreshRoiFromFields();
        }

        /// <summary>
        /// 数据来源切换时更新控件可用状态。
        /// </summary>
        private void SourceMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSourceModeEnabled();
        }

        /// <summary>
        /// 点/线运行参数对象切换事件。
        /// </summary>
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

        /// <summary>
        /// 图像区域鼠标抬起后同步当前选中的 ROI。
        /// </summary>
        private void showImageControl1_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isSyncingRoi || !_roiVisible || radioButtonSubscribe.Checked)
                return;

            int selectedIndex = -1;
            var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
            if (circleRois.Exists(roi => roi.IsSelected))
                selectedIndex = 0;

            var lineRois = showImageControl1.GetAllDynamicCalipers();
            if (lineRois.Exists(roi => roi.IsSelected))
                selectedIndex = 1;

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

        /// <summary>
        /// 获取输入灰度图像，算法只读使用，不修改图像数据。
        /// </summary>
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
        /// 读取订阅点结果。
        /// </summary>
        private PointF ReadSubscribedPoint(NodeSubscription subscription, MeasurementPointRole role, string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadPoint(sourceNode.Result, role, out PointF point))
                throw new Exception($"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用点结果。");

            return point;
        }

        /// <summary>
        /// 读取订阅节点的全部目标点；没有多目标明细时回退为目标1摘要点。
        /// </summary>
        private List<IndexedMeasurementValue<PointF>> ReadSubscribedPointTargets(
            NodeSubscription subscription,
            MeasurementPointRole role,
            string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadMultiTargetItems(
                sourceNode.Result,
                out List<IMultiTargetMeasurementItem> sourceItems))
            {
                PointF point = ReadSubscribedPoint(subscription, role, name);
                return new List<IndexedMeasurementValue<PointF>>
                {
                    new IndexedMeasurementValue<PointF>(1, true, point, string.Empty)
                };
            }

            var targets = new List<IndexedMeasurementValue<PointF>>(sourceItems.Count);
            for (int i = 0; i < sourceItems.Count; i++)
            {
                IMultiTargetMeasurementItem item = sourceItems[i];
                int targetIndex = item == null ? i + 1 : item.TargetIndex;
                string error = item == null
                    ? $"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})的目标{targetIndex}结果为空。"
                    : item.ErrorMessage;
                PointF point = PointF.Empty;
                bool isOk = item != null && item.IsOk &&
                    MeasurementResultReader.TryReadPoint((object)item, role, out point);
                if (!isOk && string.IsNullOrWhiteSpace(error))
                    error = $"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})的目标{targetIndex}没有可用点结果。";

                targets.Add(new IndexedMeasurementValue<PointF>(
                    targetIndex,
                    isOk,
                    isOk ? point : PointF.Empty,
                    error,
                    item == null ? null : item.Correction));
            }

            return targets;
        }

        /// <summary>
        /// 读取订阅直线结果。
        /// </summary>
        private MeasuredLine ReadSubscribedLine(NodeSubscription subscription, string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadLine(sourceNode.Result, out MeasuredLine line))
                throw new Exception($"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})没有可用直线结果。");

            return line;
        }

        /// <summary>
        /// 读取订阅节点的全部目标直线；没有多目标明细时回退为目标1摘要直线。
        /// </summary>
        private List<IndexedMeasurementValue<MeasuredLine>> ReadSubscribedLineTargets(
            NodeSubscription subscription,
            string name)
        {
            NodeBase sourceNode = subscription.GetSelectedNode();
            if (!MeasurementResultReader.TryReadMultiTargetItems(
                sourceNode.Result,
                out List<IMultiTargetMeasurementItem> sourceItems))
            {
                MeasuredLine line = ReadSubscribedLine(subscription, name);
                return new List<IndexedMeasurementValue<MeasuredLine>>
                {
                    new IndexedMeasurementValue<MeasuredLine>(1, true, line, string.Empty)
                };
            }

            var targets = new List<IndexedMeasurementValue<MeasuredLine>>(sourceItems.Count);
            for (int i = 0; i < sourceItems.Count; i++)
            {
                IMultiTargetMeasurementItem item = sourceItems[i];
                int targetIndex = item == null ? i + 1 : item.TargetIndex;
                string error = item == null
                    ? $"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})的目标{targetIndex}结果为空。"
                    : item.ErrorMessage;
                MeasuredLine line = null;
                bool isOk = item != null && item.IsOk &&
                    MeasurementResultReader.TryReadLine((object)item, out line);
                if (!isOk && string.IsNullOrWhiteSpace(error))
                    error = $"{name}订阅节点({sourceNode.ID}.{sourceNode.NodeName})的目标{targetIndex}没有可用直线结果。";

                targets.Add(new IndexedMeasurementValue<MeasuredLine>(
                    targetIndex,
                    isOk,
                    isOk ? line : null,
                    error,
                    item == null ? null : item.Correction));
            }

            return targets;
        }

        /// <summary>
        /// 读取全部位置修正；仅绘制模式启用修正时展开多目标。
        /// </summary>
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamPointLineDistance param)
        {
            if (param.SourceMode != MeasurementDataSourceMode.Draw || !param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };

            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>把单次算法结果转换为强类型目标结果。</summary>
        private static PointLineDistanceTargetResult CreateTargetResult(PointLineDistanceMeasureResult measure)
        {
            if (measure == null)
                throw new Exception("点到线距离算法没有返回结果。");

            bool success = measure.Success && measure.DistanceResult != null;
            var item = new PointLineDistanceTargetResult
            {
                IsOk = success,
                ErrorMessage = success ? string.Empty : (string.IsNullOrWhiteSpace(measure.Message) ? "点到线距离结果为空。" : measure.Message),
                AlgorithmMs = success ? MeasurementResultRounder.Round(measure.AlgorithmMs) : 0,
                RawResult = measure
            };
            if (!success)
                return item;

            PointLineDistanceResult distance = measure.DistanceResult;
            item.Distance = MeasurementResultRounder.Round(distance.Distance);
            item.PointX = MeasurementResultRounder.Round(distance.TargetPoint.X);
            item.PointY = MeasurementResultRounder.Round(distance.TargetPoint.Y);
            item.StartX = MeasurementResultRounder.Round(distance.LineStart.X);
            item.StartY = MeasurementResultRounder.Round(distance.LineStart.Y);
            item.EndX = MeasurementResultRounder.Round(distance.LineEnd.X);
            item.EndY = MeasurementResultRounder.Round(distance.LineEnd.Y);
            item.FootX = MeasurementResultRounder.Round(distance.FootPoint.X);
            item.FootY = MeasurementResultRounder.Round(distance.FootPoint.Y);
            return item;
        }

        /// <summary>创建保留目标顺序且数值为零的失败项。</summary>
        private static PointLineDistanceTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            string message = exception == null ? "点到线距离失败。" : exception.Message;
            return new PointLineDistanceTargetResult
            {
                IsOk = false,
                ErrorMessage = message,
                RawResult = new PointLineDistanceMeasureResult { Success = false, Message = message }
            };
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

        /// <summary>
        /// 优先沿用上游目标携带的位置修正信息；缺失时创建只保留目标编号的恒等修正。
        /// </summary>
        private static PositionCorrectionInfo ResolvePairCorrection(
            IndexedMeasurementPair<PointF, MeasuredLine> pair)
        {
            PositionCorrectionInfo pointCorrection = pair.First.Context as PositionCorrectionInfo;
            if (pointCorrection != null && pointCorrection.TargetIndex == pair.TargetIndex)
                return pointCorrection;

            PositionCorrectionInfo lineCorrection = pair.Second.Context as PositionCorrectionInfo;
            if (lineCorrection != null && lineCorrection.TargetIndex == pair.TargetIndex)
                return lineCorrection;

            return CreateIdentityCorrection(pair.TargetIndex);
        }

        /// <summary>根据位置修正结果构造运行时参数，让绘制ROI跟随当前模板目标。</summary>
        private NodeParamPointLineDistance BuildRuntimeParam(NodeParamPointLineDistance param, PositionCorrectionInfo correction)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionHelper.EnsureValid(correction);
            PointF point = _transformService.TransformPoint(new PointF(param.PointCenterX, param.PointCenterY), correction);
            PointF lineStart = _transformService.TransformPoint(new PointF(param.LineStartX, param.LineStartY), correction);
            PointF lineEnd = _transformService.TransformPoint(new PointF(param.LineEndX, param.LineEndY), correction);
            double scale = GetAverageScale(correction);

            return new NodeParamPointLineDistance
            {
                ImageText1 = param.ImageText1,
                ImageText2 = param.ImageText2,
                SourceMode = param.SourceMode,
                PointText1 = param.PointText1,
                PointText2 = param.PointText2,
                PointRole = param.PointRole,
                LineText1 = param.LineText1,
                LineText2 = param.LineText2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                MeasureMode = param.MeasureMode,
                PointCenterX = point.X,
                PointCenterY = point.Y,
                PointRadius = (float)(param.PointRadius * scale),
                LineStartX = lineStart.X,
                LineStartY = lineStart.Y,
                LineEndX = lineEnd.X,
                LineEndY = lineEnd.Y,
                CaliperWidth = (float)(param.CaliperWidth * scale),
                CaliperHeight = (float)(param.CaliperHeight * scale),
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize,
                PointCaliperWidth = (float)(param.PointCaliperWidth * scale),
                PointCaliperHeight = (float)(param.PointCaliperHeight * scale),
                PointCount = param.PointCount,
                PointEdgeStrength = param.PointEdgeStrength,
                PointPolarity = param.PointPolarity,
                PointFindMode = param.PointFindMode,
                PointDirection = param.PointDirection,
                PointBlurSize = param.PointBlurSize,
                PointSamplingMode = param.PointSamplingMode,
                LineCaliperWidth = (float)(param.LineCaliperWidth * scale),
                LineCaliperHeight = (float)(param.LineCaliperHeight * scale),
                LineCount = param.LineCount,
                LineEdgeStrength = param.LineEdgeStrength,
                LinePolarity = param.LinePolarity,
                LineFindMode = param.LineFindMode,
                LineDirection = param.LineDirection,
                LineBlurSize = param.LineBlurSize,
                LineSamplingMode = param.LineSamplingMode
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

        /// <summary>
        /// 构造圆卡尺找点参数。
        /// </summary>
        private static CaliperCircleParams BuildCircleParams(NodeParamPointLineDistance param)
        {
            return new CaliperCircleParams
            {
                CenterX = param.PointCenterX,
                CenterY = param.PointCenterY,
                Radius = param.PointRadius,
                StartAngle = 0,
                EndAngle = 360,
                CaliperWidth = param.GetCaliperWidth(true),
                CaliperHeight = param.GetCaliperHeight(true),
                Count = param.GetCount(true),
                EdgeStrength = param.GetEdgeStrength(true),
                Polarity = param.GetPolarity(true),
                FindMode = param.GetFindMode(true),
                Direction = param.GetDirection(true),
                SamplingMode = param.GetSamplingMode(true),
                BlurSize = param.GetBlurSize(true)
            };
        }

        /// <summary>
        /// 构造直线卡尺找线参数。
        /// </summary>
        private static CaliperLineParams BuildLineParams(NodeParamPointLineDistance param)
        {
            return new CaliperLineParams
            {
                StartX = param.LineStartX,
                StartY = param.LineStartY,
                EndX = param.LineEndX,
                EndY = param.LineEndY,
                CaliperWidth = param.GetCaliperWidth(false),
                CaliperHeight = param.GetCaliperHeight(false),
                Count = param.GetCount(false),
                EdgeStrength = param.GetEdgeStrength(false),
                Polarity = param.GetPolarity(false),
                FindMode = param.GetFindMode(false),
                Direction = param.GetDirection(false),
                SamplingMode = param.GetSamplingMode(false),
                BlurSize = param.GetBlurSize(false)
            };
        }

        /// <summary>
        /// 填充失败结果并结束计时。
        /// </summary>
        private static PointLineDistanceMeasureResult FinishFailed(PointLineDistanceMeasureResult result, Stopwatch stopwatch, string message)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Message = message;
            result.AlgorithmMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// 保存界面参数。
        /// </summary>
        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                    throw new Exception("请选择输入图像。");

                MeasurementDataSourceMode sourceMode = radioButtonSubscribe.Checked ? MeasurementDataSourceMode.Subscribe : MeasurementDataSourceMode.Draw;
                if (sourceMode == MeasurementDataSourceMode.Subscribe)
                {
                    if (string.IsNullOrWhiteSpace(nodeSubscriptionPoint.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionLine.GetText1()))
                        throw new Exception("请选择上游点结果和直线结果。");
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

                RoiRunSettings pointSettings = _roiRunSettings[0];
                RoiRunSettings lineSettings = _roiRunSettings[1];
                Params = new NodeParamPointLineDistance
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    SourceMode = sourceMode,
                    PointText1 = nodeSubscriptionPoint.GetText1(),
                    PointText2 = nodeSubscriptionPoint.GetText2(),
                    PointRole = GetComboValue<MeasurementPointRole>(comboBoxPointRole),
                    LineText1 = nodeSubscriptionLine.GetText1(),
                    LineText2 = nodeSubscriptionLine.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    MeasureMode = GetComboValue<GeometryMeasureMode>(comboBoxMeasureMode),
                    PointCenterX = ParseFloat(textBoxPointCenterX, "点X"),
                    PointCenterY = ParseFloat(textBoxPointCenterY, "点Y"),
                    PointRadius = ParseFloat(textBoxPointRadius, "点半径"),
                    LineStartX = ParseFloat(textBoxLineStartX, "直线起点X"),
                    LineStartY = ParseFloat(textBoxLineStartY, "直线起点Y"),
                    LineEndX = ParseFloat(textBoxLineEndX, "直线终点X"),
                    LineEndY = ParseFloat(textBoxLineEndY, "直线终点Y"),
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = GetComboValue<CaliperEdgePolarity>(comboBoxPolarity),
                    FindMode = GetComboValue<CaliperEdgeFindMode>(comboBoxFindMode),
                    Direction = pointSettings.Direction,
                    PointCaliperWidth = pointSettings.CaliperWidth,
                    PointCaliperHeight = pointSettings.CaliperHeight,
                    PointCount = pointSettings.Count,
                    PointEdgeStrength = pointSettings.EdgeStrength,
                    PointBlurSize = pointSettings.BlurSize,
                    PointPolarity = pointSettings.Polarity,
                    PointFindMode = pointSettings.FindMode,
                    PointDirection = pointSettings.Direction,
                    PointSamplingMode = pointSettings.SamplingMode,
                    LineCaliperWidth = lineSettings.CaliperWidth,
                    LineCaliperHeight = lineSettings.CaliperHeight,
                    LineCount = lineSettings.Count,
                    LineEdgeStrength = lineSettings.EdgeStrength,
                    LineBlurSize = lineSettings.BlurSize,
                    LinePolarity = lineSettings.Polarity,
                    LineFindMode = lineSettings.FindMode,
                    LineDirection = lineSettings.Direction,
                    LineSamplingMode = lineSettings.SamplingMode
                };

                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解析浮点数输入。
        /// </summary>
        private static float ParseFloat(TextBox textBox, string name)
        {
            if (!float.TryParse(textBox.Text, out float value))
                throw new Exception($"{GetFieldName(name)}不是有效数字！");
            return value;
        }

        /// <summary>
        /// 解析整数输入。
        /// </summary>
        private static int ParseInt(TextBox textBox, string name)
        {
            if (!int.TryParse(textBox.Text, out int value))
                throw new Exception($"{GetFieldName(name)}不是有效整数！");
            return value;
        }

        /// <summary>
        /// 获取用于异常提示的字段名。
        /// </summary>
        private static string GetFieldName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "参数" : name;
        }

        /// <summary>
        /// 读取编辑阶段全部位置修正信息。
        /// </summary>
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

        /// <summary>
        /// 设置预览图像。
        /// </summary>
        private void SetPreview(Mat image)
        {
            showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(image));
        }

        /// <summary>
        /// 根据当前字段刷新图像上的动态 ROI。
        /// </summary>
        private void RefreshRoiFromFields()
        {
            if (_isSyncingRoi)
                return;

            try
            {
                SaveCurrentRunSettingsFromControls();
                PositionCorrectionInfo correctionInfo = GetDefaultEditingCorrectionInfo(false);
                PointF point = new PointF(ParseFloat(textBoxPointCenterX, string.Empty), ParseFloat(textBoxPointCenterY, string.Empty));
                PointF lineStart = new PointF(ParseFloat(textBoxLineStartX, string.Empty), ParseFloat(textBoxLineStartY, string.Empty));
                PointF lineEnd = new PointF(ParseFloat(textBoxLineEndX, string.Empty), ParseFloat(textBoxLineEndY, string.Empty));
                if (correctionInfo != null)
                {
                    point = correctionInfo.TransformPoint(point.X, point.Y);
                    lineStart = correctionInfo.TransformPoint(lineStart.X, lineStart.Y);
                    lineEnd = correctionInfo.TransformPoint(lineEnd.X, lineEnd.Y);
                }

                float pointRadius = ParseFloat(textBoxPointRadius, string.Empty);
                double displayScale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
                pointRadius = (float)(pointRadius * displayScale);
                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(point.X, point.Y, pointRadius, 0, 360, (float)(_roiRunSettings[0].CaliperWidth * displayScale), (float)(_roiRunSettings[0].CaliperHeight * displayScale), _roiRunSettings[0].Count, Color.Lime, "点");
                showImageControl1.AddDynamicCaliper(lineStart.X, lineStart.Y, lineEnd.X, lineEnd.Y, (float)(_roiRunSettings[1].CaliperWidth * displayScale), (float)(_roiRunSettings[1].CaliperHeight * displayScale), _roiRunSettings[1].Count, Color.DeepSkyBlue, "直线");

                var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
                if (circleRois.Count > 0)
                    circleRois[0].Direction = _roiRunSettings[0].Direction;

                var lineRois = showImageControl1.GetAllDynamicCalipers();
                if (lineRois.Count > 0)
                    lineRois[0].Direction = _roiRunSettings[1].Direction;

                SelectDynamicRoi(_selectedRunRoiIndex);
                _roiVisible = true;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 尝试将当前图像上的 ROI 写回字段。
        /// </summary>
        private void TryReadRoiToFields()
        {
            SaveCurrentRunSettingsFromControls();
            ReadRoiToFields(true);
        }

        /// <summary>
        /// 将当前图像上的 ROI 写回字段。
        /// </summary>
        private void ReadRoiToFields(bool requireCorrection)
        {
            var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
            var lineRois = showImageControl1.GetAllDynamicCalipers();
            if (circleRois.Count < 1 || lineRois.Count < 1)
                return;

            PointF point = new PointF(circleRois[0].CX, circleRois[0].CY);
            PointF lineStart = new PointF(lineRois[0].SX, lineRois[0].SY);
            PointF lineEnd = new PointF(lineRois[0].EX, lineRois[0].EY);
            PointF roiCenter = MeasurementNodeHelper.CalculateBoundsCenter(new[] { point, lineStart, lineEnd });
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, requireCorrection);
            double scale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
            if (correctionInfo != null)
            {
                point = _transformService.InverseTransformPoint(point, correctionInfo);
                lineStart = _transformService.InverseTransformPoint(lineStart, correctionInfo);
                lineEnd = _transformService.InverseTransformPoint(lineEnd, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxPointCenterX.Text = point.X.ToString("F2");
                textBoxPointCenterY.Text = point.Y.ToString("F2");
                textBoxPointRadius.Text = (circleRois[0].Radius / scale).ToString("F2");
                textBoxLineStartX.Text = lineStart.X.ToString("F2");
                textBoxLineStartY.Text = lineStart.Y.ToString("F2");
                textBoxLineEndX.Text = lineEnd.X.ToString("F2");
                textBoxLineEndY.Text = lineEnd.Y.ToString("F2");
                SaveRunSettingsFromRoi(_roiRunSettings[0], circleRois[0]);
                SaveRunSettingsFromRoi(_roiRunSettings[1], lineRois[0]);
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

        /// <summary>
        /// 将交互 ROI 同步回字段，但保留当前正在输入的控件内容。
        /// </summary>
        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
            var lineRois = showImageControl1.GetAllDynamicCalipers();
            if (circleRois.Count < 1 || lineRois.Count < 1)
                return;

            PointF point = new PointF(circleRois[0].CX, circleRois[0].CY);
            PointF lineStart = new PointF(lineRois[0].SX, lineRois[0].SY);
            PointF lineEnd = new PointF(lineRois[0].EX, lineRois[0].EY);
            PointF roiCenter = MeasurementNodeHelper.CalculateBoundsCenter(new[] { point, lineStart, lineEnd });
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, false);
            double scale = correctionInfo == null ? 1.0 : GetAverageScale(correctionInfo);
            if (correctionInfo != null)
            {
                point = _transformService.InverseTransformPoint(point, correctionInfo);
                lineStart = _transformService.InverseTransformPoint(lineStart, correctionInfo);
                lineEnd = _transformService.InverseTransformPoint(lineEnd, correctionInfo);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxPointCenterX)) textBoxPointCenterX.Text = point.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointCenterY)) textBoxPointCenterY.Text = point.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointRadius)) textBoxPointRadius.Text = (circleRois[0].Radius / scale).ToString("F2");
                if (!ReferenceEquals(source, textBoxLineStartX)) textBoxLineStartX.Text = lineStart.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxLineStartY)) textBoxLineStartY.Text = lineStart.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxLineEndX)) textBoxLineEndX.Text = lineEnd.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxLineEndY)) textBoxLineEndY.Text = lineEnd.Y.ToString("F2");
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        /// <summary>
        /// 根据当前对象索引选中动态图形。
        /// </summary>
        private void SelectDynamicRoi(int index)
        {
            for (int i = 0; i < showImageControl1.DynamicRoiCount; i++)
            {
                var roi = showImageControl1.GetDynamicRoi(i);
                if (roi != null)
                    roi.IsSelected = false;
            }

            if (index == 0)
            {
                var circleRois = showImageControl1.GetAllDynamicCircleCalipers();
                if (circleRois.Count > 0)
                    circleRois[0].IsSelected = true;
            }
            else if (index == 1)
            {
                var lineRois = showImageControl1.GetAllDynamicCalipers();
                if (lineRois.Count > 0)
                    lineRois[0].IsSelected = true;
            }

            showImageControl1.Invalidate();
        }

        /// <summary>
        /// 根据数据来源显示或隐藏运行参数页。
        /// </summary>
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

        /// <summary>
        /// 更新订阅模式和绘制模式下控件的可用状态。
        /// </summary>
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

        /// <summary>
        /// 清空动态图形。
        /// </summary>
        private void ClearEditingRoi()
        {
            showImageControl1.ClearDynamicRoi();
            _roiVisible = false;
        }

        /// <summary>
        /// 刷新图像按钮事件。
        /// </summary>
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

        /// <summary>
        /// 绘制区域按钮事件。
        /// </summary>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            showImageControl1.SetDisplayResult(null);
            RefreshRoiFromFields();
        }

        /// <summary>
        /// 确认区域按钮事件。
        /// </summary>
        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            try
            {
                TryReadRoiToFields();
                ClearEditingRoi();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"确认区域失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 运行按钮事件。
        /// </summary>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                List<PointLineDistanceTargetResult> items = ExecuteMeasures((NodeParamPointLineDistance)Params, CancellationToken.None);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodePointLineDistance.BuildDisplayResult(items));

                if (items.Exists(item => !item.IsOk))
                    MessageBoxTD.Show("部分模板目标点到线距离失败，失败目标结果已保留为0。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})点到线距离异常，原因：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 确定按钮事件。
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
        /// 位置修正勾选变化事件。
        /// </summary>
        private void checkBoxUsePositionCorrection_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSourceModeEnabled();
            if (_roiVisible)
                RefreshRoiFromFields();
        }

        /// <summary>
        /// 添加下拉框项。
        /// </summary>
        private static void AddComboItem<T>(ComboBox comboBox, T value, string text)
        {
            comboBox.Items.Add(new ComboItem<T>(value, text));
        }

        /// <summary>
        /// 按枚举值选中下拉框项。
        /// </summary>
        private static void SelectComboValue<T>(ComboBox comboBox, T value)
        {
            foreach (object item in comboBox.Items)
            {
                if (item is ComboItem<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 获取下拉框选中项的枚举值。
        /// </summary>
        private static T GetComboValue<T>(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboItem<T> option)
                return option.Value;
            if (comboBox.SelectedItem is T value)
                return value;
            throw new InvalidOperationException("下拉框没有选中有效项。");
        }

        /// <summary>
        /// 根据当前对象刷新方向下拉框文本。
        /// </summary>
        private void UpdateDirectionItems(bool pointTarget)
        {
            int selectedIndex = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;
            comboBoxDirection.Items.Clear();
            if (pointTarget)
            {
                comboBoxDirection.Items.Add("由内向外");
                comboBoxDirection.Items.Add("由外向内");
            }
            else
            {
                comboBoxDirection.Items.Add("垂直主轴");
                comboBoxDirection.Items.Add("沿主轴");
            }

            comboBoxDirection.SelectedIndex = selectedIndex;
        }
    }
}
