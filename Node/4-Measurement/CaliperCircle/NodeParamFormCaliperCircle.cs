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

namespace TDJS_Vision.Node._4_Measurement.CaliperCircle
{
    public partial class NodeParamFormCaliperCircle : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        /// <summary>唯一 ROI 的目标归属与坐标变换服务。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();
        private bool _isSyncingRoi;
        private bool _roiVisible;

        public NodeParamFormCaliperCircle(Process process, NodeBase node)
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
            var param = Params as NodeParamCaliperCircle;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                checkBoxUsePositionCorrection.Checked = param.UsePositionCorrection;
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                textBoxCenterX.Text = param.CenterX.ToString();
                textBoxCenterY.Text = param.CenterY.ToString();
                textBoxRadius.Text = param.Radius.ToString();
                textBoxStartAngle.Text = param.StartAngle.ToString();
                textBoxEndAngle.Text = param.EndAngle.ToString();
                textBoxCaliperWidth.Text = param.CaliperWidth.ToString();
                textBoxCaliperHeight.Text = param.CaliperHeight.ToString();
                textBoxCount.Text = param.Count.ToString();
                checkBoxEnableFitValidPointCount.Checked = param.EnableFitValidPointCount;
                textBoxFitValidPointCount.Text = NormalizeFitValidPointCount(param.FitValidPointCount).ToString();
                textBoxEdgeStrength.Text = param.EdgeStrength.ToString();
                textBoxBlurSize.Text = param.BlurSize.ToString();
                comboBoxPolarity.SelectedItem = param.Polarity;
                comboBoxFindMode.SelectedItem = param.FindMode;
                comboBoxDirection.SelectedIndex = param.Direction == 1 ? 1 : 0;
                comboBoxSamplingMode.SelectedIndex = param.SamplingMode == CaliperSamplingMode.AntiInterference ? 1 : 0;
                UpdatePositionCorrectionEnabled();
                UpdateFitValidPointCountEnabled();
            }
            finally
            {
                _isSyncingRoi = false;
            }

            ClearEditingRoi();
        }

        /// <summary>一次读取灰度图并按位置修正列表顺序执行全部目标的卡尺找圆。</summary>
        internal List<CaliperCircleTargetResult> ExecuteMeasures(NodeParamCaliperCircle param, CancellationToken token)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = ReadCorrections(param);
            if (corrections.Count == 0)
                return new List<CaliperCircleTargetResult>();

            bool disposeGrayAfterUse;
            Mat gray = GetInputGrayMat(out disposeGrayAfterUse);
            try
            {
                // 多目标统一按模板顺序串行测量，避免业务工具之间出现不同调度语义。
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
        private IReadOnlyList<PositionCorrectionInfo> ReadCorrections(NodeParamCaliperCircle param)
        {
            if (param == null)
                throw new Exception("卡尺找圆参数为空。");
            if (!param.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { CreateIdentityCorrection() };
            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            corrections = corrections ?? new List<PositionCorrectionInfo>();
            return corrections;
        }

        /// <summary>为单个模板目标构造已经仿射变换的运行参数。</summary>
        private NodeParamCaliperCircle BuildRuntimeParam(NodeParamCaliperCircle param, PositionCorrectionInfo correction)
        {
            PointF center = new PointF(param.CenterX, param.CenterY);
            double lengthScale = 1.0;
            float deltaAngle = 0;
            if (param.UsePositionCorrection)
            {
                PositionCorrectionHelper.EnsureValid(correction);
                center = _transformService.TransformPoint(center, correction);
                lengthScale = GetAverageScale(correction);
                deltaAngle = (float)correction.DeltaAngle;
            }
            bool fullCircle = Math.Abs(Math.Abs(param.EndAngle - param.StartAngle) - 360.0f) < 0.1f;
            float startAngle = param.StartAngle + deltaAngle;
            float endAngle = fullCircle ? startAngle + 360.0f : param.EndAngle + deltaAngle;
            return new NodeParamCaliperCircle
            {
                Text1 = param.Text1,
                Text2 = param.Text2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                CenterX = center.X,
                CenterY = center.Y,
                Radius = (float)(param.Radius * lengthScale),
                StartAngle = startAngle,
                EndAngle = endAngle,
                CaliperWidth = (float)(param.CaliperWidth * lengthScale),
                CaliperHeight = (float)(param.CaliperHeight * lengthScale),
                Count = param.Count,
                EnableFitValidPointCount = param.EnableFitValidPointCount,
                FitValidPointCount = NormalizeFitValidPointCount(param.FitValidPointCount),
                EdgeStrength = param.EdgeStrength,
                BlurSize = param.BlurSize,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                SamplingMode = param.SamplingMode,
                Direction = param.Direction
            };
        }

        /// <summary>执行单个目标并把算法结果转换为强类型目标项。</summary>
        private CaliperCircleTargetResult ExecuteOne(Mat gray, NodeParamCaliperCircle runtimeParam, PositionCorrectionInfo correction)
        {
            var algorithmParam = new CaliperCircleParams
            {
                CenterX = runtimeParam.CenterX,
                CenterY = runtimeParam.CenterY,
                Radius = runtimeParam.Radius,
                StartAngle = runtimeParam.StartAngle,
                EndAngle = runtimeParam.EndAngle,
                CaliperWidth = runtimeParam.CaliperWidth,
                CaliperHeight = runtimeParam.CaliperHeight,
                Count = runtimeParam.Count,
                EnableFitValidPointCount = runtimeParam.EnableFitValidPointCount,
                FitValidPointCount = NormalizeFitValidPointCount(runtimeParam.FitValidPointCount),
                EdgeStrength = runtimeParam.EdgeStrength,
                Polarity = runtimeParam.Polarity,
                FindMode = runtimeParam.FindMode,
                SamplingMode = runtimeParam.SamplingMode,
                Direction = runtimeParam.Direction,
                BlurSize = runtimeParam.BlurSize
            };
            CaliperCircleMeasureResult measure = CaliperMeasurementAlgorithm.FindCircle(gray, algorithmParam);
            if (measure == null)
                throw new Exception("卡尺找圆算法没有返回结果。");
            var item = new CaliperCircleTargetResult
            {
                IsOk = measure.Success,
                ErrorMessage = measure.Success ? string.Empty : (string.IsNullOrWhiteSpace(measure.ErrorMessage) ? "未找到有效圆。" : measure.ErrorMessage),
                EdgePointCount = measure.Success ? measure.PointCount : 0,
                EdgePoints = MeasurementResultRounder.RoundPoints(measure.EdgePoints),
                AlgorithmMs = measure.Success ? MeasurementResultRounder.Round(measure.AlgorithmMs) : 0
            };
            if (measure.Success)
            {
                item.CenterX = MeasurementResultRounder.Round(measure.Center.X);
                item.CenterY = MeasurementResultRounder.Round(measure.Center.Y);
                item.Radius = MeasurementResultRounder.Round(measure.Radius);
                item.Diameter = MeasurementResultRounder.Round(measure.Radius * 2.0);
            }
            return item;
        }

        /// <summary>创建保留目标顺序且所有数值为零的失败项。</summary>
        private CaliperCircleTargetResult CreateFailure(PositionCorrectionInfo correction, Exception exception)
        {
            return new CaliperCircleTargetResult
            {
                IsOk = false,
                ErrorMessage = exception == null ? "卡尺找圆失败。" : exception.Message,
                EdgePointCount = 0,
                CenterX = 0,
                CenterY = 0,
                Radius = 0,
                Diameter = 0,
                AlgorithmMs = 0
            };
        }

        /// <summary>创建不改变坐标的单目标修正项。</summary>
        private static PositionCorrectionInfo CreateIdentityCorrection()
        {
            return new PositionCorrectionInfo { TargetIndex = 1, IsValid = true, BaseScaleX = 1, BaseScaleY = 1, CurrentScaleX = 1, CurrentScaleY = 1 };
        }

        /// <summary>计算各向尺度平均值，用于圆半径和卡尺尺寸。</summary>
        private static double GetAverageScale(PositionCorrectionInfo correction)
        {
            double scaleX = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleX) / PositionCorrectionInfo.NormalizeScale(correction.BaseScaleX);
            double scaleY = PositionCorrectionInfo.NormalizeScale(correction.CurrentScaleY) / PositionCorrectionInfo.NormalizeScale(correction.BaseScaleY);
            return (scaleX + scaleY) * 0.5;
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

            comboBoxDirection.Items.Add("由内向外");
            comboBoxDirection.Items.Add("由外向内");
            comboBoxDirection.SelectedIndex = 0;

            comboBoxSamplingMode.Items.Add("快速采样");
            comboBoxSamplingMode.Items.Add("抗干扰采样");
            comboBoxSamplingMode.SelectedIndex = 0;
        }

        private void BindRoiRefreshEvents()
        {
            textBoxCenterX.TextChanged += OnRoiParamChanged;
            textBoxCenterY.TextChanged += OnRoiParamChanged;
            textBoxRadius.TextChanged += OnRoiParamChanged;
            textBoxStartAngle.TextChanged += OnRoiParamChanged;
            textBoxEndAngle.TextChanged += OnRoiParamChanged;
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
            var rois = showImageControl1.GetAllDynamicCircleCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            PointF center = new PointF(roi.CX, roi.CY);
            float radius = roi.Radius;
            float caliperWidth = roi.CaliperWidth;
            float caliperHeight = roi.CaliperHeight;
            float startAngle = roi.StartAngle;
            float endAngle = roi.EndAngle;
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(center, false);
            if (correctionInfo != null)
            {
                center = correctionInfo.InverseTransformPoint(center.X, center.Y);
                ApplyInverseAngleCorrection(ref startAngle, ref endAngle, correctionInfo);
                double scale = GetAverageScale(correctionInfo);
                radius = (float)(radius / scale);
                caliperWidth = (float)(caliperWidth / scale);
                caliperHeight = (float)(caliperHeight / scale);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxCenterX)) textBoxCenterX.Text = center.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxCenterY)) textBoxCenterY.Text = center.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxRadius)) textBoxRadius.Text = radius.ToString("F2");
                if (!ReferenceEquals(source, textBoxStartAngle)) textBoxStartAngle.Text = startAngle.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndAngle)) textBoxEndAngle.Text = endAngle.ToString("F2");
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
        /// 获取参数界面预览用的只读原图引用，不接管 Mat 生命周期。
        /// </summary>
        private Mat GetPreviewMat()
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

        /// <summary>获取默认显示唯一 ROI 的第一目标修正信息。</summary>
        private PositionCorrectionInfo GetDefaultEditingCorrectionInfo(bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            return corrections == null || corrections.Count == 0 ? null : corrections[0];
        }

        /// <summary>按唯一圆 ROI 中心解析其所属模板目标。</summary>
        private PositionCorrectionInfo ResolveEditingCorrection(PointF roiCenter, bool require)
        {
            IReadOnlyList<PositionCorrectionInfo> corrections = GetEditingCorrections(require);
            if (corrections == null || corrections.Count == 0)
                return null;
            int index = _transformService.ResolveAnchorIndex(roiCenter, corrections);
            return corrections[index];
        }

        private static void ApplyInverseAngleCorrection(ref float startAngle, ref float endAngle, PositionCorrectionInfo correctionInfo)
        {
            float span = endAngle - startAngle;
            if (Math.Abs(Math.Abs(span) - 360.0f) < 0.1f)
            {
                startAngle = 0.0f;
                endAngle = 360.0f;
                return;
            }

            startAngle = (float)PositionCorrectionHelper.NormalizeAngle(startAngle - correctionInfo.DeltaAngle);
            endAngle = startAngle + span;
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

                Params = new NodeParamCaliperCircle
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    CenterX = ParseFloat(textBoxCenterX, "圆心X"),
                    CenterY = ParseFloat(textBoxCenterY, "圆心Y"),
                    Radius = ParseFloat(textBoxRadius, "半径"),
                    StartAngle = ParseFloat(textBoxStartAngle, "起始角度"),
                    EndAngle = ParseFloat(textBoxEndAngle, "结束角度"),
                    CaliperWidth = ParseFloat(textBoxCaliperWidth, "卡尺宽度"),
                    CaliperHeight = ParseFloat(textBoxCaliperHeight, "卡尺高度"),
                    Count = ParseInt(textBoxCount, "卡尺数量"),
                    EnableFitValidPointCount = checkBoxEnableFitValidPointCount.Checked,
                    FitValidPointCount = ParseFitValidPointCount(),
                    EdgeStrength = ParseInt(textBoxEdgeStrength, "边缘阈值"),
                    BlurSize = ParseInt(textBoxBlurSize, "平滑核"),
                    Polarity = (CaliperEdgePolarity)comboBoxPolarity.SelectedItem,
                    FindMode = (CaliperEdgeFindMode)comboBoxFindMode.SelectedItem,
                    SamplingMode = comboBoxSamplingMode.SelectedIndex == 1
                        ? CaliperSamplingMode.AntiInterference
                        : CaliperSamplingMode.Fast,
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

        /// <summary>
        /// 兼容旧参数，旧流程未保存该值时回退为默认拟合有效点数。
        /// </summary>
        private static int NormalizeFitValidPointCount(int value)
        {
            return value <= 0 ? 10 : value;
        }

        /// <summary>
        /// 读取拟合有效点数阈值，确保启用时阈值为正整数。
        /// </summary>
        private int ParseFitValidPointCount()
        {
            int value = ParseInt(textBoxFitValidPointCount, "拟合有效点数");
            if (value <= 0)
                throw new Exception("拟合有效点数必须大于0！");
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
                List<CaliperCircleTargetResult> items = ExecuteMeasures((NodeParamCaliperCircle)Params, CancellationToken.None);
                SetPreview(GetPreviewMat());
                showImageControl1.SetDisplayResult(NodeCaliperCircle.BuildDisplayResult(items));

                if (items.Exists(item => !item.IsOk))
                    MessageBoxTD.Show("部分模板目标卡尺找圆失败，失败目标结果已保留为0。");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})卡尺找圆异常，原因：{ex.Message}", true);
            }
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
                float centerX = ParseFloat(textBoxCenterX, string.Empty);
                float centerY = ParseFloat(textBoxCenterY, string.Empty);
                float radius = ParseFloat(textBoxRadius, string.Empty);
                float startAngle = ParseFloat(textBoxStartAngle, string.Empty);
                float endAngle = ParseFloat(textBoxEndAngle, string.Empty);
                float originalStartAngle = startAngle;
                float caliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
                float caliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
                int count = ParseInt(textBoxCount, string.Empty);

                PositionCorrectionInfo correctionInfo = GetDefaultEditingCorrectionInfo(false);
                PointF center = new PointF(centerX, centerY);
                if (correctionInfo != null)
                {
                    center = correctionInfo.TransformPoint(center.X, center.Y);
                    double scale = GetAverageScale(correctionInfo);
                    radius = (float)(radius * scale);
                    caliperWidth = (float)(caliperWidth * scale);
                    caliperHeight = (float)(caliperHeight * scale);
                    float deltaAngle = (float)correctionInfo.DeltaAngle;
                    startAngle += deltaAngle;
                    bool fullCircle = Math.Abs(Math.Abs(endAngle - originalStartAngle) - 360.0f) < 0.1f;
                    endAngle = fullCircle ? startAngle + 360.0f : endAngle + deltaAngle;
                }

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(
                    center.X,
                    center.Y,
                    radius,
                    startAngle,
                    endAngle,
                    caliperWidth,
                    caliperHeight,
                    count,
                    Color.DodgerBlue,
                    "Circle");

                var rois = showImageControl1.GetAllDynamicCircleCalipers();
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

        /// <summary>
        /// 切换拟合有效点数输入框可编辑状态。
        /// </summary>
        private void checkBoxEnableFitValidPointCount_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFitValidPointCountEnabled();
        }

        private void UpdatePositionCorrectionEnabled()
        {
            if (nodeSubscriptionPositionCorrection != null)
                nodeSubscriptionPositionCorrection.Enabled = checkBoxUsePositionCorrection.Checked;
        }

        /// <summary>
        /// 根据启用开关控制拟合有效点数配置项是否可编辑。
        /// </summary>
        private void UpdateFitValidPointCountEnabled()
        {
            if (labelFitValidPointCount != null)
                labelFitValidPointCount.Enabled = checkBoxEnableFitValidPointCount.Checked;
            if (textBoxFitValidPointCount != null)
                textBoxFitValidPointCount.Enabled = checkBoxEnableFitValidPointCount.Checked;
        }

        private void ClearEditingRoi()
        {
            showImageControl1.ClearDynamicRoi();
            _roiVisible = false;
        }

        private void TryReadRoiToFields()
        {
            var rois = showImageControl1.GetAllDynamicCircleCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            PointF center = new PointF(roi.CX, roi.CY);
            float radius = roi.Radius;
            float caliperWidth = roi.CaliperWidth;
            float caliperHeight = roi.CaliperHeight;
            float startAngle = roi.StartAngle;
            float endAngle = roi.EndAngle;
            PositionCorrectionInfo correctionInfo = ResolveEditingCorrection(center, true);
            if (correctionInfo != null)
            {
                center = correctionInfo.InverseTransformPoint(center.X, center.Y);
                ApplyInverseAngleCorrection(ref startAngle, ref endAngle, correctionInfo);
                double scale = GetAverageScale(correctionInfo);
                radius = (float)(radius / scale);
                caliperWidth = (float)(caliperWidth / scale);
                caliperHeight = (float)(caliperHeight / scale);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxCenterX.Text = center.X.ToString("F2");
                textBoxCenterY.Text = center.Y.ToString("F2");
                textBoxRadius.Text = radius.ToString("F2");
                textBoxStartAngle.Text = startAngle.ToString("F2");
                textBoxEndAngle.Text = endAngle.ToString("F2");
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
