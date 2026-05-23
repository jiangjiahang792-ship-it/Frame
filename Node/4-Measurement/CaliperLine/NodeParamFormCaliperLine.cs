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
        private readonly Process process;
        private readonly NodeBase node;
        private bool _isSyncingRoi;

        public NodeParamFormCaliperLine(Process process, NodeBase node)
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
            var param = Params as NodeParamCaliperLine;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
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
            }
            finally
            {
                _isSyncingRoi = false;
            }

            RefreshRoiFromFields();
        }

        internal CaliperLineMeasureResult ExecuteMeasure(NodeParamCaliperLine param, out Mat output)
        {
            using (Mat source = GetInputMat())
            using (Mat gray = CaliperMeasurementAlgorithm.ToGray(source))
            {
                var algorithmParam = new CaliperLineParams
                {
                    StartX = param.StartX,
                    StartY = param.StartY,
                    EndX = param.EndX,
                    EndY = param.EndY,
                    CaliperWidth = param.CaliperWidth,
                    CaliperHeight = param.CaliperHeight,
                    Count = param.Count,
                    EdgeStrength = param.EdgeStrength,
                    Polarity = param.Polarity,
                    FindMode = param.FindMode,
                    Direction = param.Direction,
                    BlurSize = param.BlurSize
                };

                CaliperLineMeasureResult result = CaliperMeasurementAlgorithm.FindLine(gray, algorithmParam);
                output = MeasurementNodeHelper.EnsureBgr(source);
                MeasurementNodeHelper.DrawEdgePoints(output, result.EdgePoints, new Scalar(0, 255, 255));
                if (result.Success)
                {
                    Cv2.Line(output, ToPoint(result.LineStart), ToPoint(result.LineEnd), new Scalar(0, 255, 0), 2);
                    Cv2.PutText(output, $"Line {result.PointCount}", new OpenCvSharp.Point(20, 35), HersheyFonts.HersheySimplex, 1, new Scalar(0, 255, 0), 2);
                }

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
            RefreshRoiFromFields();
        }

        private void SyncCurrentRoiToFieldsExcept(object source)
        {
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            _isSyncingRoi = true;
            try
            {
                if (!ReferenceEquals(source, textBoxStartX)) textBoxStartX.Text = roi.SX.ToString("F2");
                if (!ReferenceEquals(source, textBoxStartY)) textBoxStartY.Text = roi.SY.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndX)) textBoxEndX.Text = roi.EX.ToString("F2");
                if (!ReferenceEquals(source, textBoxEndY)) textBoxEndY.Text = roi.EY.ToString("F2");
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

                Params = new NodeParamCaliperLine
                {
                    Text1 = nodeSubscription1.GetText1(),
                    Text2 = nodeSubscription1.GetText2(),
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
                Hide();
        }

        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            try
            {
                Mat output;
                CaliperLineMeasureResult result = ExecuteMeasure((NodeParamCaliperLine)Params, out output);
                using (output)
                {
                    SetPreview(output);
                }

                if (!result.Success)
                    MessageBoxTD.Show($"卡尺找线失败，边缘点数量：{result.PointCount}");
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"执行异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.NodeName})卡尺找线异常，原因：{ex.Message}", true);
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
                    RefreshRoiFromFields();
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
            RefreshRoiFromFields();
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

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddDynamicCaliper(
                    startX,
                    startY,
                    endX,
                    endY,
                    caliperWidth,
                    caliperHeight,
                    count,
                    Color.DodgerBlue,
                    "Caliper");

                var rois = showImageControl1.GetAllDynamicCalipers();
                if (rois.Count > 0)
                    rois[0].Direction = comboBoxDirection.SelectedIndex == 1 ? 1 : 0;
            }
            catch
            {
            }
        }

        private void TryReadRoiToFields()
        {
            var rois = showImageControl1.GetAllDynamicCalipers();
            if (rois.Count == 0)
                return;

            var roi = rois[0];
            _isSyncingRoi = true;
            try
            {
                textBoxStartX.Text = roi.SX.ToString("F2");
                textBoxStartY.Text = roi.SY.ToString("F2");
                textBoxEndX.Text = roi.EX.ToString("F2");
                textBoxEndY.Text = roi.EY.ToString("F2");
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
