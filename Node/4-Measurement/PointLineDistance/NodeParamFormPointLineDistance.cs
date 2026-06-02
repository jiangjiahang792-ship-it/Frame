using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
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
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPoint.Init(node);
            nodeSubscriptionLine.Init(node);
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
        /// 执行点到线距离测量。
        /// </summary>
        internal PointLineDistanceMeasureResult ExecuteMeasure(NodeParamPointLineDistance param)
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
                bool disposeGrayAfterUse;
                Mat gray = GetInputGrayMat(out disposeGrayAfterUse);
                try
                {
                    NodeParamPointLineDistance runtimeParam = BuildRuntimeParam(param);
                    CaliperCircleMeasureResult pointResult = CaliperMeasurementAlgorithm.FindCircle(gray, BuildCircleParams(runtimeParam));
                    CaliperLineMeasureResult lineResult = CaliperMeasurementAlgorithm.FindLine(gray, BuildLineParams(runtimeParam));
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

            _roiRunSettings[1].CaliperWidth = param.LineCaliperWidth ?? param.CaliperWidth;
            _roiRunSettings[1].CaliperHeight = param.LineCaliperHeight ?? param.CaliperHeight;
            _roiRunSettings[1].Count = param.LineCount ?? param.Count;
            _roiRunSettings[1].EdgeStrength = param.LineEdgeStrength ?? param.EdgeStrength;
            _roiRunSettings[1].Polarity = param.LinePolarity ?? param.Polarity;
            _roiRunSettings[1].FindMode = param.LineFindMode ?? param.FindMode;
            _roiRunSettings[1].Direction = param.LineDirection ?? param.Direction;
            _roiRunSettings[1].BlurSize = param.LineBlurSize ?? param.BlurSize;
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
        /// 根据位置修正结果构造运行时参数，让绘制 ROI 跟随当前图像坐标。
        /// </summary>
        private NodeParamPointLineDistance BuildRuntimeParam(NodeParamPointLineDistance param)
        {
            if (param == null || !param.UsePositionCorrection || param.SourceMode != MeasurementDataSourceMode.Draw)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            PointF point = correctionInfo.TransformPoint(param.PointCenterX, param.PointCenterY);
            PointF lineStart = correctionInfo.TransformPoint(param.LineStartX, param.LineStartY);
            PointF lineEnd = correctionInfo.TransformPoint(param.LineEndX, param.LineEndY);

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
                PointRadius = param.PointRadius,
                LineStartX = lineStart.X,
                LineStartY = lineStart.Y,
                LineEndX = lineEnd.X,
                LineEndY = lineEnd.Y,
                CaliperWidth = param.CaliperWidth,
                CaliperHeight = param.CaliperHeight,
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction,
                BlurSize = param.BlurSize,
                PointCaliperWidth = param.PointCaliperWidth,
                PointCaliperHeight = param.PointCaliperHeight,
                PointCount = param.PointCount,
                PointEdgeStrength = param.PointEdgeStrength,
                PointPolarity = param.PointPolarity,
                PointFindMode = param.PointFindMode,
                PointDirection = param.PointDirection,
                PointBlurSize = param.PointBlurSize,
                LineCaliperWidth = param.LineCaliperWidth,
                LineCaliperHeight = param.LineCaliperHeight,
                LineCount = param.LineCount,
                LineEdgeStrength = param.LineEdgeStrength,
                LinePolarity = param.LinePolarity,
                LineFindMode = param.LineFindMode,
                LineDirection = param.LineDirection,
                LineBlurSize = param.LineBlurSize
            };
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
                    LineCaliperWidth = lineSettings.CaliperWidth,
                    LineCaliperHeight = lineSettings.CaliperHeight,
                    LineCount = lineSettings.Count,
                    LineEdgeStrength = lineSettings.EdgeStrength,
                    LineBlurSize = lineSettings.BlurSize,
                    LinePolarity = lineSettings.Polarity,
                    LineFindMode = lineSettings.FindMode,
                    LineDirection = lineSettings.Direction
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
        /// 获取当前编辑状态下的位置修正信息。
        /// </summary>
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
                PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
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
                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(point.X, point.Y, pointRadius, 0, 360, _roiRunSettings[0].CaliperWidth, _roiRunSettings[0].CaliperHeight, _roiRunSettings[0].Count, Color.Lime, "点");
                showImageControl1.AddDynamicCaliper(lineStart.X, lineStart.Y, lineEnd.X, lineEnd.Y, _roiRunSettings[1].CaliperWidth, _roiRunSettings[1].CaliperHeight, _roiRunSettings[1].Count, Color.DeepSkyBlue, "直线");

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

            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(requireCorrection);
            PointF point = new PointF(circleRois[0].CX, circleRois[0].CY);
            PointF lineStart = new PointF(lineRois[0].SX, lineRois[0].SY);
            PointF lineEnd = new PointF(lineRois[0].EX, lineRois[0].EY);
            if (correctionInfo != null)
            {
                point = correctionInfo.InverseTransformPoint(point.X, point.Y);
                lineStart = correctionInfo.InverseTransformPoint(lineStart.X, lineStart.Y);
                lineEnd = correctionInfo.InverseTransformPoint(lineEnd.X, lineEnd.Y);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxPointCenterX.Text = point.X.ToString("F2");
                textBoxPointCenterY.Text = point.Y.ToString("F2");
                textBoxPointRadius.Text = circleRois[0].Radius.ToString("F2");
                textBoxLineStartX.Text = lineStart.X.ToString("F2");
                textBoxLineStartY.Text = lineStart.Y.ToString("F2");
                textBoxLineEndX.Text = lineEnd.X.ToString("F2");
                textBoxLineEndY.Text = lineEnd.Y.ToString("F2");
                SaveRunSettingsFromRoi(_roiRunSettings[0], circleRois[0]);
                SaveRunSettingsFromRoi(_roiRunSettings[1], lineRois[0]);
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

            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
            PointF point = new PointF(circleRois[0].CX, circleRois[0].CY);
            PointF lineStart = new PointF(lineRois[0].SX, lineRois[0].SY);
            PointF lineEnd = new PointF(lineRois[0].EX, lineRois[0].EY);
            if (correctionInfo != null)
            {
                point = correctionInfo.InverseTransformPoint(point.X, point.Y);
                lineStart = correctionInfo.InverseTransformPoint(lineStart.X, lineStart.Y);
                lineEnd = correctionInfo.InverseTransformPoint(lineEnd.X, lineEnd.Y);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxPointCenterX)) textBoxPointCenterX.Text = point.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointCenterY)) textBoxPointCenterY.Text = point.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxPointRadius)) textBoxPointRadius.Text = circleRois[0].Radius.ToString("F2");
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
                PointLineDistanceMeasureResult result = ExecuteMeasure((NodeParamPointLineDistance)Params);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodePointLineDistance.BuildDisplayResult(result));

                if (!result.Success)
                    MessageBoxTD.Show($"点到线距离失败：{result.Message}");
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
