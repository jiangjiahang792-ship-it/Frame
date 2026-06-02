using Logger;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperLine
{
    public partial class NodeParamFormCaliperLine : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
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
            nodeSubscription1.Init(node);
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
                UpdatePositionCorrectionEnabled();
            }
            finally
            {
                _isSyncingRoi = false;
            }

            ClearEditingRoi();
        }

        internal CaliperLineMeasureResult ExecuteMeasure(NodeParamCaliperLine param)
        {
            bool disposeGrayAfterUse;
            Mat gray = GetInputGrayMat(out disposeGrayAfterUse);

            try
            {
                NodeParamCaliperLine runtimeParam = BuildRuntimeParam(param);

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
                    Direction = runtimeParam.Direction,
                    BlurSize = runtimeParam.BlurSize
                };

                return CaliperMeasurementAlgorithm.FindLine(gray, algorithmParam);
            }
            finally
            {
                if (disposeGrayAfterUse)
                    gray?.Dispose();
            }
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
            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
            if (correctionInfo != null)
            {
                start = correctionInfo.InverseTransformPoint(start.X, start.Y);
                end = correctionInfo.InverseTransformPoint(end.X, end.Y);
            }

            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxStartX)) textBoxStartX.Text = start.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxStartY)) textBoxStartY.Text = start.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndX)) textBoxEndX.Text = end.X.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndY)) textBoxEndY.Text = end.Y.ToString("F2");
                if (!ReferenceEquals(source, textBoxCaliperWidth)) textBoxCaliperWidth.Text = roi.CaliperWidth.ToString("F2");
                if (!ReferenceEquals(source, textBoxCaliperHeight)) textBoxCaliperHeight.Text = roi.CaliperHeight.ToString("F2");
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

        private NodeParamCaliperLine BuildRuntimeParam(NodeParamCaliperLine param)
        {
            if (param == null || !param.UsePositionCorrection)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            PointF start = correctionInfo.TransformPoint(param.StartX, param.StartY);
            PointF end = correctionInfo.TransformPoint(param.EndX, param.EndY);

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
                CaliperWidth = param.CaliperWidth,
                CaliperHeight = param.CaliperHeight,
                Count = param.Count,
                EdgeStrength = param.EdgeStrength,
                BlurSize = param.BlurSize,
                Polarity = param.Polarity,
                FindMode = param.FindMode,
                Direction = param.Direction
            };
        }

        private PositionCorrectionInfo GetEditingCorrectionInfo(bool require)
        {
            if (!checkBoxUsePositionCorrection.Checked)
                return null;

            if (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2()))
            {
                if (require)
                    throw new Exception("Position correction subscription is empty.");
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
                CaliperLineMeasureResult result = ExecuteMeasure((NodeParamCaliperLine)Params);
                SetPreview(GetPreviewSourceMat());
                showImageControl1.SetDisplayResult(NodeCaliperLine.BuildDisplayResult(result));

                if (!result.Success)
                    MessageBoxTD.Show($"卡尺找线失败，边缘点数量：{result.PointCount}");
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

                PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(false);
                PointF start = new PointF(startX, startY);
                PointF end = new PointF(endX, endY);
                if (correctionInfo != null)
                {
                    start = correctionInfo.TransformPoint(start.X, start.Y);
                    end = correctionInfo.TransformPoint(end.X, end.Y);
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
            PositionCorrectionInfo correctionInfo = GetEditingCorrectionInfo(true);
            if (correctionInfo != null)
            {
                start = correctionInfo.InverseTransformPoint(start.X, start.Y);
                end = correctionInfo.InverseTransformPoint(end.X, end.Y);
            }

            _isSyncingRoi = true;
            try
            {
                textBoxStartX.Text = start.X.ToString("F2");
                textBoxStartY.Text = start.Y.ToString("F2");
                textBoxEndX.Text = end.X.ToString("F2");
                textBoxEndY.Text = end.Y.ToString("F2");
                textBoxCaliperWidth.Text = roi.CaliperWidth.ToString("F2");
                textBoxCaliperHeight.Text = roi.CaliperHeight.ToString("F2");
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
