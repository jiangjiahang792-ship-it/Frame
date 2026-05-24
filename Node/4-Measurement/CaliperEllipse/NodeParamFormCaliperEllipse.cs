using Logger;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperEllipse
{
    public partial class NodeParamFormCaliperEllipse : FormBase, INodeParamForm
    {
        private readonly NodeBase node;
        private bool _isSyncingRoi;
        private bool _roiVisible;

        public NodeParamFormCaliperEllipse(Process process, NodeBase node)
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
            var param = Params as NodeParamCaliperEllipse;
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
                textBoxWidth.Text = param.Width.ToString();
                textBoxHeight.Text = param.Height.ToString();
                textBoxAngle.Text = param.Angle.ToString();
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

        internal CaliperEllipseMeasureResult ExecuteMeasure(NodeParamCaliperEllipse param, out Mat output)
        {
            using (Mat source = GetInputMat())
            using (Mat gray = CaliperMeasurementAlgorithm.ToGray(source))
            {
                NodeParamCaliperEllipse runtimeParam = BuildRuntimeParam(param);
                var algorithmParam = new CaliperEllipseParams
                {
                    CenterX = runtimeParam.CenterX,
                    CenterY = runtimeParam.CenterY,
                    Width = runtimeParam.Width,
                    Height = runtimeParam.Height,
                    Angle = runtimeParam.Angle,
                    CaliperWidth = runtimeParam.CaliperWidth,
                    CaliperHeight = runtimeParam.CaliperHeight,
                    Count = runtimeParam.Count,
                    EdgeStrength = runtimeParam.EdgeStrength,
                    Polarity = runtimeParam.Polarity,
                    FindMode = runtimeParam.FindMode,
                    Direction = runtimeParam.Direction,
                    BlurSize = runtimeParam.BlurSize
                };

                CaliperEllipseMeasureResult result = CaliperMeasurementAlgorithm.FindEllipse(gray, algorithmParam);
                output = MeasurementNodeHelper.EnsureBgr(source);

                return result;
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

            comboBoxDirection.Items.Add("由内向外");
            comboBoxDirection.Items.Add("由外向内");
            comboBoxDirection.SelectedIndex = 0;
        }

        private void BindRoiRefreshEvents()
        {
            textBoxCenterX.TextChanged += OnRoiParamChanged;
            textBoxCenterY.TextChanged += OnRoiParamChanged;
            textBoxWidth.TextChanged += OnRoiParamChanged;
            textBoxHeight.TextChanged += OnRoiParamChanged;
            textBoxAngle.TextChanged += OnRoiParamChanged;
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
            var rois = showImageControl1.GetAllDynamicEllipseCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxCenterX)) textBoxCenterX.Text = roi.CX.ToString("F2");
                if (!ReferenceEquals(source, textBoxCenterY)) textBoxCenterY.Text = roi.CY.ToString("F2");
                if (!ReferenceEquals(source, textBoxWidth)) textBoxWidth.Text = roi.W.ToString("F2");
                if (!ReferenceEquals(source, textBoxHeight)) textBoxHeight.Text = roi.H.ToString("F2");
                if (!ReferenceEquals(source, textBoxAngle)) textBoxAngle.Text = roi.Phi.ToString("F2");
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

        private Mat GetInputMat()
        {
            OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
            return MeasurementNodeHelper.GetFirstMat(outputImage);
        }

        private NodeParamCaliperEllipse BuildRuntimeParam(NodeParamCaliperEllipse param)
        {
            if (param == null || !param.UsePositionCorrection)
                return param;

            PositionCorrectionInfo correctionInfo = nodeSubscriptionPositionCorrection.GetValue<PositionCorrectionInfo>();
            PositionCorrectionHelper.EnsureValid(correctionInfo);
            PointF center = correctionInfo.TransformPoint(param.CenterX, param.CenterY);

            return new NodeParamCaliperEllipse
            {
                Text1 = param.Text1,
                Text2 = param.Text2,
                UsePositionCorrection = param.UsePositionCorrection,
                CorrectionText1 = param.CorrectionText1,
                CorrectionText2 = param.CorrectionText2,
                CenterX = center.X,
                CenterY = center.Y,
                Width = param.Width,
                Height = param.Height,
                Angle = param.Angle + (float)correctionInfo.DeltaAngle,
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

                Params = new NodeParamCaliperEllipse
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
                    UsePositionCorrection = checkBoxUsePositionCorrection.Checked,
                    CorrectionText1 = nodeSubscriptionPositionCorrection.GetText1(),
                    CorrectionText2 = nodeSubscriptionPositionCorrection.GetText2(),
                    CenterX = ParseFloat(textBoxCenterX, "中心X"),
                    CenterY = ParseFloat(textBoxCenterY, "中心Y"),
                    Width = ParseFloat(textBoxWidth, "宽度"),
                    Height = ParseFloat(textBoxHeight, "高度"),
                    Angle = ParseFloat(textBoxAngle, "角度"),
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
                Mat output;
                CaliperEllipseMeasureResult result = ExecuteMeasure((NodeParamCaliperEllipse)Params, out output);
                using (output)
                {
                    SetPreview(output);
                    showImageControl1.SetDisplayResult(NodeCaliperEllipse.BuildDisplayResult(result));
                }

                if (!result.Success)
                    MessageBoxTD.Show(string.IsNullOrWhiteSpace(result.ErrorMessage) ? $"卡尺找椭圆失败，边缘点数量：{result.PointCount}" : result.ErrorMessage);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})卡尺找椭圆异常，原因：{ex.Message}", true);
            }
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                using (Mat input = GetInputMat())
                {
                    SetPreview(input);
                    showImageControl1.ShowFit();
                }
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
                float width = ParseFloat(textBoxWidth, string.Empty);
                float height = ParseFloat(textBoxHeight, string.Empty);
                float angle = ParseFloat(textBoxAngle, string.Empty);
                float caliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
                float caliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
                int count = ParseInt(textBoxCount, string.Empty);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicEllipseCaliper(
                    centerX,
                    centerY,
                    width,
                    height,
                    angle,
                    caliperWidth,
                    caliperHeight,
                    count,
                    Color.DodgerBlue,
                    "Ellipse");

                var rois = showImageControl1.GetAllDynamicEllipseCalipers();
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
            TryReadRoiToFields();
            ClearEditingRoi();
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
            var rois = showImageControl1.GetAllDynamicEllipseCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            _isSyncingRoi = true;
            try
            {
                textBoxCenterX.Text = roi.CX.ToString("F2");
                textBoxCenterY.Text = roi.CY.ToString("F2");
                textBoxWidth.Text = roi.W.ToString("F2");
                textBoxHeight.Text = roi.H.ToString("F2");
                textBoxAngle.Text = roi.Phi.ToString("F2");
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
