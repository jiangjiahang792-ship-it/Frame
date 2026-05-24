using Logger;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.CaliperCircle
{
    public partial class NodeParamFormCaliperCircle : FormBase, INodeParamForm
    {
        private readonly Process process;
        private readonly NodeBase node;
        private bool _isSyncingRoi;
        private bool _roiVisible;

        public NodeParamFormCaliperCircle(Process process, NodeBase node)
        {
            InitializeComponent();
            this.process = process;
            this.node = node;
            InitializeCombos();
            BindRoiRefreshEvents();
        }

        public INodeParam Params { get; set; }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.Init(node);
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
                textBoxCenterX.Text = param.CenterX.ToString();
                textBoxCenterY.Text = param.CenterY.ToString();
                textBoxRadius.Text = param.Radius.ToString();
                textBoxStartAngle.Text = param.StartAngle.ToString();
                textBoxEndAngle.Text = param.EndAngle.ToString();
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

            ClearEditingRoi();
        }

        internal CaliperCircleMeasureResult ExecuteMeasure(NodeParamCaliperCircle param, out Mat output)
        {
            using (Mat source = GetInputMat())
            using (Mat gray = CaliperMeasurementAlgorithm.ToGray(source))
            {
                var algorithmParam = new CaliperCircleParams
                {
                    CenterX = param.CenterX,
                    CenterY = param.CenterY,
                    Radius = param.Radius,
                    StartAngle = param.StartAngle,
                    EndAngle = param.EndAngle,
                    CaliperWidth = param.CaliperWidth,
                    CaliperHeight = param.CaliperHeight,
                    Count = param.Count,
                    EdgeStrength = param.EdgeStrength,
                    Polarity = param.Polarity,
                    FindMode = param.FindMode,
                    Direction = param.Direction,
                    BlurSize = param.BlurSize
                };

                CaliperCircleMeasureResult result = CaliperMeasurementAlgorithm.FindCircle(gray, algorithmParam);
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
            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxCenterX)) textBoxCenterX.Text = roi.CX.ToString("F2");
                if (!ReferenceEquals(source, textBoxCenterY)) textBoxCenterY.Text = roi.CY.ToString("F2");
                if (!ReferenceEquals(source, textBoxRadius)) textBoxRadius.Text = roi.Radius.ToString("F2");
                if (!ReferenceEquals(source, textBoxStartAngle)) textBoxStartAngle.Text = roi.StartAngle.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndAngle)) textBoxEndAngle.Text = roi.EndAngle.ToString("F2");
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

        private bool SaveParams()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscription1.GetText1()) || string.IsNullOrWhiteSpace(nodeSubscription1.GetText2()))
                    throw new Exception("请选择输入图像！");

                TryReadRoiToFields();

                Params = new NodeParamCaliperCircle
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
                    CenterX = ParseFloat(textBoxCenterX, "圆心X"),
                    CenterY = ParseFloat(textBoxCenterY, "圆心Y"),
                    Radius = ParseFloat(textBoxRadius, "半径"),
                    StartAngle = ParseFloat(textBoxStartAngle, "起始角度"),
                    EndAngle = ParseFloat(textBoxEndAngle, "结束角度"),
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
                Mat output;
                CaliperCircleMeasureResult result = ExecuteMeasure((NodeParamCaliperCircle)Params, out output);
                using (output)
                {
                    SetPreview(output);
                    showImageControl1.SetDisplayResult(NodeCaliperCircle.BuildDisplayResult(result));
                }

                if (!result.Success)
                    MessageBoxTD.Show($"卡尺找圆失败，边缘点数量：{result.PointCount}");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})卡尺找圆异常，原因：{ex.Message}", true);
            }
        }

        private async void buttonRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                await process.RunForUpdateImages(node);
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
                float radius = ParseFloat(textBoxRadius, string.Empty);
                float startAngle = ParseFloat(textBoxStartAngle, string.Empty);
                float endAngle = ParseFloat(textBoxEndAngle, string.Empty);
                float caliperWidth = ParseFloat(textBoxCaliperWidth, string.Empty);
                float caliperHeight = ParseFloat(textBoxCaliperHeight, string.Empty);
                int count = ParseInt(textBoxCount, string.Empty);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCircleCaliper(
                    centerX,
                    centerY,
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
            RefreshRoiFromFields();
        }

        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            TryReadRoiToFields();
            ClearEditingRoi();
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
            _isSyncingRoi = true;
            try
            {
                textBoxCenterX.Text = roi.CX.ToString("F2");
                textBoxCenterY.Text = roi.CY.ToString("F2");
                textBoxRadius.Text = roi.Radius.ToString("F2");
                textBoxStartAngle.Text = roi.StartAngle.ToString("F2");
                textBoxEndAngle.Text = roi.EndAngle.ToString("F2");
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
