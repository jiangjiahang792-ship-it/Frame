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

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    public partial class NodeParamFormCaliperLine : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        /// <summary>唯一 ROI 的目标归属与坐标变换服务。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();
        private bool _isSyncingRoi;
        private bool _roiVisible;

        public NodeParamFormCaliperLine(Process process, NodeBase node)
        {
            InitializeComponent();
            this.node = node;
            InitializeCombos();
            BindRoiRefreshEvents();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
            nodeSubscriptionPositionCorrection.Init(node);
        }

        public void SetParam2Form()
        {
            var param = Params as NodeParamCaliperLine;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                textBoxStartX.Text = param.StartX.ToString();
                textBoxStartY.Text = param.StartY.ToString();
                textBoxEndX.Text = param.EndX.ToString();
                textBoxEndY.Text = param.EndY.ToString();
                textBoxCaliperWidth.Text = param.CaliperWidth.ToString();
                textBoxCaliperHeight.Text = param.CaliperHeight.ToString();
                textBoxCount.Text = param.Count.ToString();
                textBoxEdgeStrength.Text = param.EdgeStrength.ToString();
                textBoxBlurSize.Text = param.BlurSize.ToString();
                comboBoxPolarity.SelectedItem = param.Polarity;
                comboBoxFindMode.SelectedItem = param.FindMode;
                comboBoxDirection.SelectedIndex = param.Direction == 1 ? 1 : 0;
                comboBoxSamplingMode.SelectedIndex = param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;
                comboBoxMeasureMode.SelectedIndex = param.MeasureMode == GeometryMeasureMode.Pixel ? 1 : 0;
                checkBoxEnableQualityValidation.Checked = param.EnableQualityValidation;
                textBoxMinimumValidPointRatio.Text = param.MinimumValidPointRatio.ToString();
                textBoxMaximumAverageResidual.Text = param.MaximumAverageResidual.ToString();
                textBoxMaximumResidual.Text = param.MaximumResidual.ToString();
                textBoxMinimumCoverageRatio.Text = param.MinimumCoverageRatio.ToString();
                textBoxMaximumAngleDeviation.Text = param.MaximumAngleDeviationDegrees.ToString();
                UpdatePositionCorrectionEnabled();
            }
            finally
            {
                _isSyncingRoi = false;
            }

            ClearEditingRoi();
        }

        /// <summary>
        /// 一次读取灰度图并按位置修正列表顺序执行全部目标的卡尺找线。
        /// </summary>
        internal List<CaliperLineTargetResult> ExecuteMeasures(NodeParamCaliperLine param, CancellationToken token)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
            if (corrections.Count == 0)
                return new List<CaliperLineTargetResult>();

            bool disposeGrayAfterUse;
            Mat gray = GetInputGrayMat(out disposeGrayAfterUse);

            try
            {
                return MultiTargetMeasurementRunner.Run(
                    corrections,
                    token,
                    correction => ExecuteOne(gray, BuildRuntimeParam(param, correction), correction),
                    CreateFailure);
            }
            finally
            {
                if (disposeGrayAfterUse)
                    gray?.Dispose();
            }
        }

        /// <summary>读取全部位置修正信息；未启用修正时返回一个恒等项。</summary>
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamCaliperLine param)
        {
            if (param == null)
                throw new Exception("卡尺找线参数为空。");
            if (!param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };

            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>为单个模板目标构造已经仿射变换的运行参数。</summary>
        private NodeParamCaliperLine BuildRuntimeParam(NodeParamCaliperLine param, PositionCorrectionInfo correction)
        {
            PointF start = new PointF(param.StartX, param.StartY);
            PointF end = new PointF(param.EndX, param.EndY);
            double lengthScale = 1.0;
            if (param.UsePositionCorrection)
            {
                PositionCorrectionHelper.EnsureValid(correction);
                start = _transformService.TransformPoint(start, correction);
                end = _transformService.TransformPoint(end, correction);
                lengthScale = GetAverageScale(correction);
            }

            return new NodeParamCaliperLine
            {
                Text1 = param.Text1,
                Text2 = param.Text2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                StartX = start.X,
                StartY = start.Y,
                EndX = end.X,
                EndY = end.Y,
                CaliperWidth = (float)(param.CaliperWidth * lengthScale),
                CaliperHeight = (float)(param.CaliperHeight * lengthScale),
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                BlurSize = param.BlurSize,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                SamplingMode = param.SamplingMode,
                MeasureMode = param.MeasureMode,
                Direction = param.Direction,
                EnableQualityValidation = param.EnableQualityValidation,
                MinimumValidPointRatio = param.MinimumValidPointRatio,
                MaximumAverageResidual = param.MaximumAverageResidual,
                MaximumResidual = param.MaximumResidual,
                MinimumCoverageRatio = param.MinimumCoverageRatio,
                MaximumAngleDeviationDegrees = param.MaximumAngleDeviationDegrees
            };
        }

        /// <summary>执行单个目标并把算法结果转换为强类型目标项。</summary>
        private CaliperLineTargetResult ExecuteOne(Mat gray, NodeParamCaliperLine runtimeParam, PositionCorrectionInfo correction)
        {
            var algorithmParam = new CaliperLineParams
            {
                StartX = runtimeParam.StartX,
                StartY = runtimeParam.StartY,
                EndX = runtimeParam.EndX,
                EndY = runtimeParam.EndY,
                CaliperWidth = runtimeParam.CaliperWidth,
                CaliperHeight = runtimeParam.CaliperHeight,
                Count = runtimeParam.Count,
                EdgeStrength = runtimeParam.EdgeStrength,
                Polarity = runtimeParam.Polarity,
                FindMode = runtimeParam.FindMode,
                SamplingMode = runtimeParam.SamplingMode,
                MeasureMode = runtimeParam.MeasureMode,
                Direction = runtimeParam.Direction,
                BlurSize = runtimeParam.BlurSize,
                EnableQualityValidation = runtimeParam.EnableQualityValidation,
                MinimumValidPointRatio = runtimeParam.MinimumValidPointRatio,
                MaximumAverageResidual = runtimeParam.MaximumAverageResidual,
                MaximumResidual = runtimeParam.MaximumResidual,
                MinimumCoverageRatio = runtimeParam.MinimumCoverageRatio,
                MaximumAngleDeviationDegrees = runtimeParam.MaximumAngleDeviationDegrees
            };
            CaliperLineMeasureResult measure = CaliperMeasurementAlgorithm.FindLine(gray, algorithmParam);
            if (measure == null)
                throw new Exception("卡尺找线算法没有返回结果。");

            var item = new CaliperLineTargetResult
            {
                IsOk = measure.Success,
                ErrorMessage = measure.Success ? string.Empty : measure.ErrorMessage,
                CandidatePointCount = measure.Quality.CandidatePointCount,
                EdgePointCount = measure.PointCount,
                EdgePoints = MeasurementResultRounder.RoundPoints(measure.EdgePoints),
                ValidPointRatio = MeasurementResultRounder.Round(measure.Quality.ValidPointRatio),
                AverageResidual = MeasurementResultRounder.Round(measure.Quality.AverageResidual),
                MaximumResidual = MeasurementResultRounder.Round(measure.Quality.MaximumResidual),
                CoverageRatio = MeasurementResultRounder.Round(measure.Quality.CoverageRatio),
                AngleDeviation = MeasurementResultRounder.Round(measure.Quality.AngleDeviationDegrees),
                AlgorithmMs = MeasurementResultRounder.Round(measure.AlgorithmMs)
            };
            if (measure.Success)
            {
                item.StartX = MeasurementResultRounder.Round(measure.LineStart.X);
                item.StartY = MeasurementResultRounder.Round(measure.LineStart.Y);
                item.EndX = MeasurementResultRounder.Round(measure.LineEnd.X);
                item.EndY = MeasurementResultRounder.Round(measure.LineEnd.Y);
                item.Length = MeasurementResultRounder.Round(CalculateDistance(measure.LineStart, measure.LineEnd));
                item.Angle = MeasurementResultRounder.Round(CalculateAbsoluteAngle(measure.LineStart, measure.LineEnd));
                item.WarpageDifference = MeasurementResultRounder.Round(CalculateWarpageDifference(
                    measure.EdgePoints,
                    measure.LineStart,
                    measure.LineEnd));
            }
            return item;
        }

        /// <summary>创建保留目标顺序且所有数值为零的失败项。</summary>
        private CaliperLineTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            return new CaliperLineTargetResult
            {
                IsOk = false,
                ErrorMessage = exception == null ? "卡尺找线失败。" : exception.Message,
                CandidatePointCount = 0,
                EdgePointCount = 0,
                StartX = 0,
                StartY = 0,
                EndX = 0,
                EndY = 0,
                Length = 0,
                Angle = 0,
                WarpageDifference = 0,
                ValidPointRatio = 0,
                AverageResidual = 0,
                MaximumResidual = 0,
                CoverageRatio = 0,
                AngleDeviation = 0,
                AlgorithmMs = 0
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

        /// <summary>计算各向尺度的平均值，用于卡尺宽高等标量尺寸。</summary>
        private static double GetAverageScale(PositionCorrectionInfo correction)
        {
            double scaleX = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleX) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleX);
            double scaleY = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleY) /
                PositionCorrectionInfo.NormalizeScale(correction.BaseScaleY);
            return (scaleX + scaleY) * 0.5;
        }

        /// <summary>计算两点之间的距离。</summary>
        private static double CalculateDistance(PointF start, PointF end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>计算线段的绝对角度。</summary>
        private static double CalculateAbsoluteAngle(PointF start, PointF end)
        {
            return Math.Abs(Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI);
        }

        /// <summary>计算边缘点到拟合线距离的最大值与最小值差，作为翘曲度差值输出。</summary>
        private static double CalculateWarpageDifference(IReadOnlyList<PointF> edgePoints, PointF lineStart, PointF lineEnd)
        {
            if (edgePoints == null || edgePoints.Count == 0)
                return 0;

            double minDistance = double.MaxValue;
            double maxDistance = 0;
            foreach (PointF point in edgePoints)
            {
                double distance = CalculatePointLineDistance(point, lineStart, lineEnd);
                if (distance < minDistance)
                    minDistance = distance;
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            return minDistance == double.MaxValue ? 0 : maxDistance - minDistance;
        }

        /// <summary>计算点到拟合直线的垂直距离。</summary>
        private static double CalculatePointLineDistance(PointF point, PointF lineStart, PointF lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-8)
                return 0;

            return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
        }

        private void InitializeCombos()
        {
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

            comboBoxMeasureMode.Items.Add("亚像素");
            comboBoxMeasureMode.Items.Add("像素");
            comboBoxMeasureMode.SelectedIndex = 0;
        }

        private void BindRoiRefreshEvents()
        {
            textBoxStartX.TextChanged += OnRoiParamChanged;
            textBoxStartY.TextChanged += OnRoiParamChanged;
            textBoxEndX.TextChanged += OnRoiParamChanged;
            textBoxEndY.TextChanged += OnRoiParamChanged;
            textBoxCaliperWidth.TextChanged += OnRoiParamChanged;
            textBoxCaliperHeight.TextChanged += OnRoiParamChanged;
            textBoxCount.TextChanged += OnRoiParamChanged;
            comboBoxDirection.SelectedIndexChanged += OnRoiParamChanged;
        }

        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            SyncCurrentRoiToFieldsExcept(sender);
            if (_roiVisible)
                RefreshRoiFromFields();
        }

        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            PointF start = new PointF(roi.SX, roi.SY);
            PointF end = new PointF(roi.EX, roi.EY);
            float caliperWidth = roi.CaliperWidth;
            float caliperHeight = roi.CaliperHeight;
            PointF roiCenter = new PointF((start.X + end.X) * 0.5f, (start.Y + end.Y) * 0.5f);
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, false);
            if (correctionInfo != null)
            {
                start = correctionInfo.InverseTransformPoint(start.X, start.Y);
                end = correctionInfo.InverseTransformPoint(end.X, end.Y);
                double scale = GetAverageScale(correctionInfo);
                caliperWidth = (float)(caliperWidth / scale);
                caliperHeight = (float)(caliperHeight / scale);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxStartX)) textBoxStartX.Text = start.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxStartY)) textBoxStartY.Text = start.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndX)) textBoxEndX.Text = end.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndY)) textBoxEndY.Text = end.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxCaliperWidth)) textBoxCaliperWidth.Text = caliperWidth.ToString("F2");
                if (!ReferenceEquals(source, textBoxCaliperHeight)) textBoxCaliperHeight.Text = caliperHeight.ToString("F2");
                if (!ReferenceEquals(source, textBoxCount)) textBoxCount.Text = roi.Count.ToString();
                if (!ReferenceEquals(source, comboBoxDirection)) comboBoxDirection.SelectedIndex = roi.Direction == 1 ? 1 : 0;
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        private Mat GetInputGrayMat(out bool disposeAfterUse)
        {
            OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetReadOnlyGrayMat(outputImage, out disposeAfterUse);
        }

        /// <summary>
        /// 获取参数界面预览用的原图引用，预览只读转换为 Bitmap，不接管 Mat 生命周期。
        /// </summary>
        private Mat GetPreviewSourceMat()
        {
            OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetReadOnlyPreviewMat(outputImage);
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

        /// <summary>获取默认用于显示唯一 ROI 的第一目标修正信息。</summary>
        private PositionCorrectionInfo GetDefaultEditingCorrectionInfo(bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            return corrections == null || corrections.Count == 0 ? null : corrections[0];
        }

        /// <summary>按唯一 ROI 中心解析其所属模板目标。</summary>
        private PositionCorrectionInfo ResolveEditingCorrection(PointF roiCenter, bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            if (corrections == null || corrections.Count == 0)
                return null;
            int index = _transformService.ResolveAnchorIndex(roiCenter, corrections);
            return corrections[index];
        }

        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscription1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscription1.GetText2()))
                    throw new Exception("请选择输入图像！");
                if (checkBoxUsePositionCorrection.Checked &&
                    (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2())))
                    throw new Exception("请选择位置修正信息。");

                TryReadRoiToFields();

                Params = new NodeParamCaliperLine
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    StartX = ParseFloat(textBoxStartX, "起点X"),
                    StartY = ParseFloat(textBoxStartY, "起点Y"),
                    EndX = ParseFloat(textBoxEndX, "终点X"),
                    EndY = ParseFloat(textBoxEndY, "终点Y"),
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem,
                    FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem,
                    SamplingMode = comboBoxSamplingMode.SelectedIndex == 1
                        ? CaliperSamplingMode.AntiInterference
                        : CaliperSamplingMode.Fast,
                    MeasureMode = comboBoxMeasureMode.SelectedIndex == 1
                        ? GeometryMeasureMode.Pixel
                        : GeometryMeasureMode.SubPixel,
                    Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0,
                    EnableQualityValidation = checkBoxEnableQualityValidation.Checked,
                    MinimumValidPointRatio = ParseRatio(textBoxMinimumValidPointRatio, "最低有效点比例"),
                    MaximumAverageResidual = ParsePositiveFloat(textBoxMaximumAverageResidual, "平均残差上限"),
                    MaximumResidual = ParsePositiveFloat(textBoxMaximumResidual, "最大残差上限"),
                    MinimumCoverageRatio = ParseRatio(textBoxMinimumCoverageRatio, "最低覆盖率"),
                    MaximumAngleDeviationDegrees = ParsePositiveFloat(textBoxMaximumAngleDeviation, "方向偏差上限")
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
            float value;
            if (!float.TryParse(textBox.Text, out value))
                throw new Exception($"{name}不是有效数字！");
            return value;
        }

        private static int ParseInt(TextBox textBox, string name)
        {
            int value;
            if (!int.TryParse(textBox.Text, out value))
                throw new Exception($"{name}不是有效整数！");
            return value;
        }

        /// <summary>读取大于零且不超过一的比例参数。</summary>
        private static float ParseRatio(TextBox textBox, string name)
        {
            float value = ParseFloat(textBox, name);
            if (value <= 0 || value > 1)
                throw new Exception($"{name}必须大于0且不超过1！");
            return value;
        }

        /// <summary>读取严格大于零的浮点参数。</summary>
        private static float ParsePositiveFloat(TextBox textBox, string name)
        {
            float value = ParseFloat(textBox, name);
            if (value <= 0)
                throw new Exception($"{name}必须大于0！");
            return value;
        }

        private static OpenCvSharp.Point ToPoint(PointF point)
        {
            return new OpenCvSharp.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
            {
                ClearEditingRoi();
                Hide();
            }
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                List<CaliperLineTargetResult> items = ExecuteMeasures((NodeParamCaliperLine)Params, CancellationToken.None);
                SetPreview(GetPreviewSourceMat());
                showImageControl1.SetDisplayResult(NodeCaliperLine.BuildDisplayResult(items));

                if (items.Exists(item => !item.IsOk))
                    MessageBoxTD.Show("部分模板目标卡尺找线失败，失败目标结果已保留为0。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})卡尺找线异常，原因：{ex.Message}", true);
            }
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                SetPreview(GetPreviewSourceMat());
                showImageControl1.ShowFit();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"刷新图像失败：{ex.Message}");
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
                float startX = ParseFloat(textBoxStartX, string.Empty);
                float startY = ParseFloat(textBoxStartY, string.Empty);
                float endX = ParseFloat(textBoxEndX, string.Empty);
                float endY = ParseFloat(textBoxEndY, string.Empty);
                float caliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
                float caliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
                int count = ParseInt(textBoxCount, string.Empty);

                PositionCorrectionInfo correctionInfo = GetDefaultEditingCorrectionInfo(false);
                PointF start = new PointF(startX, startY);
                PointF end = new PointF(endX, endY);
                if (correctionInfo != null)
                {
                    start = correctionInfo.TransformPoint(start.X, start.Y);
                    end = correctionInfo.TransformPoint(end.X, end.Y);
                    double scale = GetAverageScale(correctionInfo);
                    caliperWidth = (float)(caliperWidth * scale);
                    caliperHeight = (float)(caliperHeight * scale);
                }

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCaliper(
                    start.X,
                    start.Y,
                    end.X,
                    end.Y,
                    caliperWidth,
                    caliperHeight,
                    count,
                    Color.DodgerBlue,
                    "Caliper");

                var rois = showImageControl1.GetAllDynamicCalipers();
                if (rois.Count > 0)
                    rois[0].Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;

                _roiVisible = true;
            }
            catch
            {
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
                MessageBoxTD.Show("Confirm ROI failed: " + ex.Message);
            }
        }

        private void checkBoxUsePositionCorrection_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePositionCorrectionEnabled();
        }

        private void UpdatePositionCorrectionEnabled()
        {
            if (nodeSubscriptionPositionCorrection != null)
                nodeSubscriptionPositionCorrection.Enabled = checkBoxUsePositionCorrection.Checked;
        }

        private void ClearEditingRoi()
        {
            showImageControl1.ClearDynamicRoi();
            _roiVisible = false;
        }

        private void TryReadRoiToFields()
        {
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            PointF start = new PointF(roi.SX, roi.SY);
            PointF end = new PointF(roi.EX, roi.EY);
            float caliperWidth = roi.CaliperWidth;
            float caliperHeight = roi.CaliperHeight;
            PointF roiCenter = new PointF((start.X + end.X) * 0.5f, (start.Y + end.Y) * 0.5f);
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(roiCenter, true);
            if (correctionInfo != null)
            {
                start = correctionInfo.InverseTransformPoint(start.X, start.Y);
                end = correctionInfo.InverseTransformPoint(end.X, end.Y);
                double scale = GetAverageScale(correctionInfo);
                caliperWidth = (float)(caliperWidth / scale);
                caliperHeight = (float)(caliperHeight / scale);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxStartX.Text = start.X.ToString("F2");
                textBoxStartY.Text = start.Y.ToString("F2");
                textBoxEndX.Text = end.X.ToString("F2");
                textBoxEndY.Text = end.Y.ToString("F2");
                textBoxCaliperWidth.Text = caliperWidth.ToString("F2");
                textBoxCaliperHeight.Text = caliperHeight.ToString("F2");
                textBoxCount.Text = roi.Count.ToString();
                comboBoxDirection.SelectedIndex = roi.Direction == 1 ? 1 : 0;
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }
    }
}
