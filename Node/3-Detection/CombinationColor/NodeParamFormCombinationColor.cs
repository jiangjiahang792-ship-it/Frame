using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._3_Detection.CombinationColor
{
    /// <summary>
    /// 组合颜色识别参数窗体，按卡尺工具风格提供图像预览、ROI绘制、执行预览和参数保存。
    /// </summary>
    public partial class NodeParamFormCombinationColor : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>未保存的颜色学习模型，与运行参数隔离。</summary>
        private List<GuideColorRangeModel> _models = new List<GuideColorRangeModel>();
        /// <summary>颜色采样框的独立编辑副本。</summary>
        private ColorDetectionRegion _colorLearningRegion;
        /// <summary>当前动态矩形是否为取色框，防止写入搜索区域字段。</summary>
        private bool _editingColorRoi;

        /// <summary>保存取色框拖动结果，不修改搜索矩形或位置修正基准。</summary>
        private void CaptureColorRoi()
        {
            if (!_editingColorRoi) return;
            var rois = showImageControl1.GetAllRotatedRectInfos();
            if (rois == null || rois.Count == 0) return;
            var roi = rois[0];
            _colorLearningRegion = new ColorDetectionRegion { Row = roi.Row, Column = roi.Column,
                Phi = roi.Phi, Length1 = roi.Length1, Length2 = roi.Length2 };
        }

        /// <summary>仅绘制颜色采样框，切换前保存搜索框的编辑结果。</summary>
        private void DrawColorRoi(object sender, EventArgs e)
        {
            try
            {
                CaptureColorRoi();
                TryReadRoiToFields();
                RefreshColorPreview();
                if (_colorLearningRegion == null)
                    _colorLearningRegion = new ColorDetectionRegion { Row = 200, Column = 200, Length1 = 30, Length2 = 30 };
                ClearEditingRoi();
                var region = _colorLearningRegion;
                showImageControl1.AddRoiRotatedRect(region.Row, region.Column, region.Phi, region.Length1, region.Length2,
                    1.5F, Color.Orange, "颜色ROI（仅取色）");
                _editingColorRoi = true;
                _roiVisible = true;
                buttonConfirmRoi.Enabled = false;
                labelMeasuredArea.Text = "调整橙色取色框，然后点击学习颜色";
            }
            catch (Exception ex) { MessageBoxTD.Show("绘制颜色ROI失败：" + ex.Message); }
        }
        /// <summary>从当前旋转框取主色；学习后可重新绘制搜索区或关闭搜索区进行全图识别。</summary>
        private void LearnColor(object sender, EventArgs e)
        {
            try
            {
                CaptureColorRoi();
                if (_colorLearningRegion == null) throw new InvalidOperationException("请先点击绘制颜色ROI，框选目标颜色。");
                using (var input = NodeCombinationColor.GetColorImage(GetInputImage(), out bool owns))
                using (var mask = new Mat(input.Size(), MatType.CV_8UC1, Scalar.All(0)))
                {
                    var region = _colorLearningRegion;
                    var roi = new RotatedRect(new Point2f(region.Column, region.Row),
                        new Size2f(region.Length1 * 2, region.Length2 * 2), (float)(region.Phi * 180 / Math.PI));
                    Cv2.FillConvexPoly(mask, Array.ConvertAll(roi.Points(), p => new OpenCvSharp.Point(p.X,p.Y)),Scalar.All(255));
                    var pixels = new List<Vec3b>();
                    // 仅学习时收集样本；逐帧识别走 OpenCV 通道阈值。
                    for(int y=0;y<input.Rows;y++) for(int x=0;x<input.Cols;x++)
                        if(mask.At<byte>(y,x)!=0) pixels.Add(input.At<Vec3b>(y,x));
                    _models = CombinationColorLearning.Learn(pixels);
                }
                if (_editingColorRoi) ClearEditingRoi();
                labelMeasuredArea.Text = "颜色已学习，搜索区域保持不变";
            }
            catch(Exception ex) { MessageBoxTD.Show("取色失败："+ex.Message); }
        }


        /// <summary>
        /// 标记界面正在反写控件，避免ROI刷新事件递归。
        /// </summary>
        private bool _isSyncingRoi;

        /// <summary>
        /// 标记当前图像控件是否显示编辑ROI。
        /// </summary>
        private bool _roiVisible;



        /// <summary>当前编辑ROI对应的修正快照，保存后随节点参数持久化。</summary>
        private PositionCorrectionInfo _regionCorrectionReference;

        /// <summary>快照对应的订阅节点，切换来源后重新记录绘制基准。</summary>
        private string _referenceSource;

        /// <summary>快照对应的订阅结果名称。</summary>
        private string _referenceResult;

        /// <summary>
        /// 初始化组合颜色识别参数窗体。
        /// </summary>
        public NodeParamFormCombinationColor()
        {
            InitializeComponent();
            InitializeOutputRegionModeCombo();
            BindRoiRefreshEvents();
            
            UpdateDetectionRegionState();
        }

        /// <summary>
        /// 获取或设置节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>数值参数仅由用户主动展开，不影响搜索区或取色框的数据。</summary>
        private bool _rectangleSettingsExpanded;

        /// <summary>每次打开窗体恢复默认收起状态。</summary>
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && buttonRectangleSettings != null) SetRectangleSettingsExpanded(false);
        }

        /// <summary>点击矩形参数设置时切换数值面板。</summary>
        private void buttonRectangleSettings_Click(object sender, EventArgs e)
        {
            SetRectangleSettingsExpanded(!_rectangleSettingsExpanded);
        }

        /// <summary>按当前缩放后的控件尺寸收放面板，不保留被隐藏面板的空白。</summary>
        private void SetRectangleSettingsExpanded(bool expanded)
        {
            _rectangleSettingsExpanded = expanded;
            SuspendLayout();
            try
            {
                int gap = Math.Max(4, buttonRectangleSettings.Height / 5);
                groupBoxRegion.Top = buttonRectangleSettings.Bottom + gap;
                groupBoxRegion.Visible = expanded;
                groupBoxColor.Top = expanded ? groupBoxRegion.Bottom + gap : groupBoxRegion.Top;
                labelMeasuredArea.Top = groupBoxColor.Bottom + gap;
                buttonRectangleSettings.Text = expanded ? "收起矩形参数" : "矩形参数设置";
            }
            finally { ResumeLayout(); }
        }

        /// <summary>
        /// 初始化订阅控件所属节点和可订阅结果类型。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            _node = node;
            nodeSubscriptionImage.SetExpectedValueType<OutputImage>();
            nodeSubscriptionImage.Init(node);
            nodeSubscriptionPositionCorrection.SetExpectedValueType<List<PositionCorrectionInfo>>();
            nodeSubscriptionPositionCorrection.Init(node);
        }

        /// <summary>
        /// 将反序列化后的参数恢复到窗体控件。
        /// </summary>
        public void SetParam2Form()
        {
            labelMeasuredArea.Text = "识别区域面积：未执行";
            NodeParamCombinationColor param = Params as NodeParamCombinationColor;
            if (param == null)
                return;

            var searchRegion = param.GetSearchRegion();
            _colorLearningRegion = param.ColorLearningRegion == null ? null : ColorDetectionRegion.FromParameters(param.ColorLearningRegion.ToParameters());
            _isSyncingRoi = true;
            try
            {
                _regionCorrectionReference = searchRegion.RegionCorrectionReference;
                _referenceSource = param.CorrectionText1;
                _referenceResult = param.CorrectionText2;
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                checkBoxEnableDetectionRegion.Checked = param.EnableDetectionRegion;
                checkBoxUsePositionCorrection.Checked =
                    !string.IsNullOrWhiteSpace(param.CorrectionText1) &&
                    !string.IsNullOrWhiteSpace(param.CorrectionText2);
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                numericMinimumArea.Value = param.MinimumArea;
                numericSensitivity.Value = param.Sensitivity;
                comboColorSpace.SelectedItem = param.ColorSpace;
                _models = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GuideColorRangeModel>>(Newtonsoft.Json.JsonConvert.SerializeObject(param.Models)) ?? new List<GuideColorRangeModel>();
                comboBoxOutputRegionMode.SelectedItem = param.OutputRegionMode;
                textBoxRegionColumn.Text = searchRegion.RegionColumn.ToString("F2");
                textBoxRegionRow.Text = searchRegion.RegionRow.ToString("F2");
                textBoxRegionLength1.Text = searchRegion.RegionLength1.ToString("F2");
                textBoxRegionLength2.Text = searchRegion.RegionLength2.ToString("F2");
                textBoxRegionPhi.Text = (searchRegion.RegionPhi * 180.0 / Math.PI).ToString("F2");
                UpdateDetectionRegionState();
            }
            finally
            {
                _isSyncingRoi = false;
            }

            ClearEditingRoi();

        }

        /// <summary>
        /// 读取订阅的输入图像。
        /// </summary>
        /// <returns>上游输出图像。</returns>
        internal OutputImage GetInputImage()
        {
            return nodeSubscriptionImage.GetValue<OutputImage>();
        }

        /// <summary>
        /// 读取订阅的位置修正区域列表。
        /// </summary>
        /// <returns>位置修正列表；未启用修正时返回空列表。</returns>
        internal List<PositionCorrectionInfo> GetPositionCorrections()
        {
            if (!checkBoxEnableDetectionRegion.Checked || !checkBoxUsePositionCorrection.Checked)
                return new List<PositionCorrectionInfo>();

            List<PositionCorrectionInfo> corrections = nodeSubscriptionPositionCorrection.GetValue<List<PositionCorrectionInfo>>();
            return corrections ?? new List<PositionCorrectionInfo>();
        }

        /// <summary>
        /// 绑定ROI参数变更事件，使文本参数和图像区域联动。
        /// </summary>
        private void BindRoiRefreshEvents()
        {
            textBoxRegionColumn.TextChanged += OnRoiParamChanged;
            textBoxRegionRow.TextChanged += OnRoiParamChanged;
            textBoxRegionLength1.TextChanged += OnRoiParamChanged;
            textBoxRegionLength2.TextChanged += OnRoiParamChanged;
            textBoxRegionPhi.TextChanged += OnRoiParamChanged;
        }

        /// <summary>
        /// 初始化输出区域模式下拉框。
        /// </summary>
        private void InitializeOutputRegionModeCombo()
        {
            comboBoxOutputRegionMode.Items.Add(ColorOutputRegionMode.All);
            comboBoxOutputRegionMode.Items.Add(ColorOutputRegionMode.Max);
            
            comboBoxOutputRegionMode.Format += (sender, e) => e.Value = GetOutputRegionModeText((ColorOutputRegionMode)e.ListItem);
            comboBoxOutputRegionMode.SelectedItem = ColorOutputRegionMode.All;
        }



        /// <summary>
        /// ROI参数变更后刷新图像上的编辑区域。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi)
                return;

            _regionCorrectionReference = null;
            if (!_roiVisible || _editingColorRoi) return;

            RefreshRoiFromFields();
        }



        /// <summary>
        /// 输出区域模式切换后实时刷新效果。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void OnOutputRegionModeChanged(object sender, EventArgs e)
        {
            RefreshPreviewQuietly();
        }

        /// <summary>
        /// 保存按钮点击后写入参数并关闭窗体。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
            {
                ClearEditingRoi();
                Hide();
            }
        }

        /// <summary>
        /// 执行按钮点击后按当前参数运行一次组合颜色识别并显示结果。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            labelMeasuredArea.Text = "识别区域面积：未执行";
            if (!SaveParams())
                return;

            Mat temporaryColor = null;
            NodeResultCombinationColor result = null;
            try
            {
                Mat gray = NodeCombinationColor.GetColorImage(GetInputImage(), out bool ownsColor);
                if (ownsColor)
                    temporaryColor = gray;
                result = NodeCombinationColor.ExecuteAnalysis(
                    gray,
                    (NodeParamCombinationColor)Params,
                    GetPositionCorrections());
                // 预览始终使用本次输入原图，识别结果通过显示叠加绘制。
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(gray));
                showImageControl1.SetDisplayResult(result.Result);
                labelMeasuredArea.Text = $"区域：{result.Count} 面积：{result.Area:F0} 长：{result.Height:F1} 宽：{result.Width:F1}";
            }
            catch (Exception ex)
            {
                labelMeasuredArea.Text = "识别区域面积：执行失败";
                MessageBoxTD.Show("执行异常：" + ex.Message);
                if (_node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({_node.ID}.{_node.NodeName})组合颜色识别预览异常，原因：{ex.Message}", true);
            }
            finally
            {
                temporaryColor?.Dispose();
                NodeResultResourceManager.Release(result);
            }
        }

        /// <summary>
        /// 刷新按钮点击后显示订阅图像。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            labelMeasuredArea.Text = "识别区域面积：未执行";
            try
            {
                RefreshColorPreview();
                showImageControl1.ShowFit();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("刷新图像失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 绘制ROI按钮点击后按当前参数生成可拖拽区域。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonDrawRoi_Click(object sender, EventArgs e)
        {
            try
            {
                CaptureColorRoi();
                TryReadRoiToFields();
                ClearEditingRoi();
                _regionCorrectionReference = null;
                GetRegionCorrectionReference();
                RefreshColorPreview();
                RefreshRoiFromFields();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("绘制ROI失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 确认ROI按钮点击后将图像上的区域反写到文本参数。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            try
            {
                TryReadRoiToFields();
                ClearEditingRoi();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("确认ROI失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 检测区域开关改变时同步区域和位置修正控件状态。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBoxEnableDetectionRegion_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDetectionRegionState();
        }

        /// <summary>
        /// 位置修正开关改变时同步订阅控件状态。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void checkBoxUsePositionCorrection_CheckedChanged(object sender, EventArgs e)
        {
            if (!_isSyncingRoi)
            {
                _regionCorrectionReference = null;

            }
            UpdateDetectionRegionState();
        }

        /// <summary>
        /// 保存当前控件参数。
        /// </summary>
        /// <returns>保存成功返回 true。</returns>
        private bool SaveParams()
        {
            try
            {
                CaptureColorRoi();
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                    string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                    throw new Exception("请选择输入图像。");

                if (checkBoxEnableDetectionRegion.Checked)
                    TryReadRoiToFields();

                if (checkBoxEnableDetectionRegion.Checked &&
                    checkBoxUsePositionCorrection.Checked &&
                    (string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText1()) ||
                     string.IsNullOrWhiteSpace(nodeSubscriptionPositionCorrection.GetText2())))
                    throw new Exception("启用位置修正后，请选择位置修正结果。");

                if (_models.Count == 0) throw new Exception("请先绘制取色区域并学习颜色。");

                Params = new NodeParamCombinationColor
                {
                    ColorLearningRegion = SnapshotColorRoi(),
                    RegionCorrectionReference = GetRegionCorrectionReference(),
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                    CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                    CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                    MinimumArea = (int)numericMinimumArea.Value,
                    Models = new List<GuideColorRangeModel>(_models),
                    ColorSpace = comboColorSpace.Text,
                    Sensitivity = numericSensitivity.Value,
                    OutputRegionMode = GetSelectedOutputRegionMode(),
                    RegionColumn = ParseFloat(textBoxRegionColumn, "中心X"),
                    RegionRow = ParseFloat(textBoxRegionRow, "中心Y"),
                    RegionLength1 = ParsePositiveFloat(textBoxRegionLength1, "半宽"),
                    RegionLength2 = ParsePositiveFloat(textBoxRegionLength2, "半高"),
                    RegionPhi = (float)(ParseFloat(textBoxRegionPhi, "角度") * Math.PI / 180.0)
                };

                if (_node != null)
                    _node.NotifyOutputDefinitionChanged();
                return true;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("参数设置异常：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 根据检测区域和位置修正开关启用或禁用相关控件。
        /// </summary>
        private void UpdateDetectionRegionState()
        {
            bool regionEnabled = checkBoxEnableDetectionRegion.Checked;
            groupBoxRegion.Enabled = regionEnabled;
            checkBoxUsePositionCorrection.Enabled = regionEnabled;
            nodeSubscriptionPositionCorrection.Enabled = regionEnabled && checkBoxUsePositionCorrection.Checked;
        }

        /// <summary>
        /// 显示分析实际使用的彩色原图，并清除上一次分析叠加，避免将旧面积误认为当前结果。
        /// </summary>
        private void RefreshColorPreview()
        {
            Mat gray = NodeCombinationColor.GetColorImage(GetInputImage(), out bool ownsColor);
            try
            {
                if (gray == null || gray.Empty())
                    throw new InvalidOperationException("没有有效的输入图像，请先运行图像来源节点。");
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(gray));
                showImageControl1.SetDisplayResult(null);
                labelMeasuredArea.Text = "识别区域面积：未执行";
            }
            finally
            {
                // 上游缓存只读复用，仅释放本次彩色转彩色产生的临时图像。
                if (ownsColor) gray?.Dispose();
            }
        }



        /// <summary>
        /// 在不弹出错误框的情况下刷新颜色识别效果，适合滑块拖动实时预览。
        /// </summary>
        private void RefreshPreviewQuietly()
        {
            if (_isSyncingRoi) return;
            Mat temporaryColor = null;
            NodeResultCombinationColor result = null;
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                    string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                    return;

                NodeParamCombinationColor param = BuildParamFromControls(false);
                Mat gray = NodeCombinationColor.GetColorImage(GetInputImage(), out bool ownsColor);
                if (ownsColor)
                    temporaryColor = gray;
                result = NodeCombinationColor.ExecuteAnalysis(
                    gray,
                    param,
                    GetPositionCorrections());
                // 预览始终使用本次输入原图，识别结果通过显示叠加绘制。
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(gray));
                showImageControl1.SetDisplayResult(result.Result);
                labelMeasuredArea.Text = $"区域：{result.Count} 面积：{result.Area:F0} 长：{result.Height:F1} 宽：{result.Width:F1}";
            }
            catch
            {
                labelMeasuredArea.Text = "识别区域面积：暂无结果";
            }
            finally
            {
                temporaryColor?.Dispose();
                NodeResultResourceManager.Release(result);
            }
        }

        /// <summary>显示本次阈值及区域筛选后的面积，多ROI重叠像素只统计一次，无前景时显示0。</summary>
        /// <param name="area">实际分析得到的前景像素数量。</param>
        private void ShowMeasuredArea(double area)
        {
            labelMeasuredArea.Text = $"识别区域面积：{area:F0} 像素²";
        }

        /// <summary>
        /// 从当前控件构建颜色识别参数。
        /// </summary>
        /// <param name="requireSubscription">是否强制校验订阅。</param>
        /// <returns>组合颜色识别参数。</returns>
        private NodeParamCombinationColor BuildParamFromControls(bool requireSubscription)
        {
            if (requireSubscription &&
                (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                 string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2())))
                throw new Exception("请选择输入图像。");

            return new NodeParamCombinationColor
            {
                ColorLearningRegion = SnapshotColorRoi(),
                RegionCorrectionReference = GetRegionCorrectionReference(),
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                MinimumArea = (int)numericMinimumArea.Value,
                    Models = new List<GuideColorRangeModel>(_models),
                    ColorSpace = comboColorSpace.Text,
                Sensitivity = numericSensitivity.Value,
                OutputRegionMode = GetSelectedOutputRegionMode(),
                RegionColumn = ParseFloat(textBoxRegionColumn, "中心X"),
                RegionRow = ParseFloat(textBoxRegionRow, "中心Y"),
                RegionLength1 = ParsePositiveFloat(textBoxRegionLength1, "半宽"),
                RegionLength2 = ParsePositiveFloat(textBoxRegionLength2, "半高"),
                RegionPhi = (float)(ParseFloat(textBoxRegionPhi, "角度") * Math.PI / 180.0)
            };
        }

        /// <summary>
        /// 从当前文本参数刷新可拖拽ROI。
        /// </summary>
        private void RefreshRoiFromFields()
        {
            try
            {
                float column = ParseFloat(textBoxRegionColumn, string.Empty);
                float row = ParseFloat(textBoxRegionRow, string.Empty);
                float length1 = ParsePositiveFloat(textBoxRegionLength1, string.Empty);
                float length2 = ParsePositiveFloat(textBoxRegionLength2, string.Empty);
                float phi = (float)(ParseFloat(textBoxRegionPhi, string.Empty) * Math.PI / 180.0);

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddRoiRotatedRect(row, column, phi, length1, length2, 1.5F, Color.DodgerBlue, "颜色搜索区域");
                _roiVisible = true;
            }
            catch
            {
            }
        }

        /// <summary>记录当前绘制坐标对应的修正快照；同一ROI后续预览和保存复用快照，不随运行结果漂移。</summary>
        /// <returns>独立于上游可变结果的快照，未启用修正时为空。</returns>
        private PositionCorrectionInfo GetRegionCorrectionReference()
        {
            if (!checkBoxEnableDetectionRegion.Checked || !checkBoxUsePositionCorrection.Checked)
                return null;
            string source = nodeSubscriptionPositionCorrection.GetText1();
            string result = nodeSubscriptionPositionCorrection.GetText2();
            if (_regionCorrectionReference == null || _referenceSource != source || _referenceResult != result)
            {
                var correction = GetPositionCorrections().Find(item => item != null && item.IsValid);
                PositionCorrectionHelper.EnsureValid(correction);
                _regionCorrectionReference = new PositionCorrectionInfo
                {
                    IsValid = true,
                    BaseX = correction.BaseX, BaseY = correction.BaseY, BaseAngle = correction.BaseAngle,
                    BaseScaleX = correction.BaseScaleX, BaseScaleY = correction.BaseScaleY,
                    CurrentX = correction.CurrentX, CurrentY = correction.CurrentY, CurrentAngle = correction.CurrentAngle,
                    CurrentScaleX = correction.CurrentScaleX, CurrentScaleY = correction.CurrentScaleY
                };
                _referenceSource = source;
                _referenceResult = result;
            }
            return _regionCorrectionReference;
        }

        /// <summary>
        /// 将图像控件上的动态ROI反写到文本参数。
        /// </summary>
        private void TryReadRoiToFields()
        {
            if (_editingColorRoi || !_roiVisible) return;
            var rois = showImageControl1.GetAllRotatedRectInfos();
            if (rois == null || rois.Count == 0)
                return;

            var roi = rois[0];
            _isSyncingRoi = true;
            try
            {
                textBoxRegionRow.Text = roi.Row.ToString("F2");
                textBoxRegionColumn.Text = roi.Column.ToString("F2");
                textBoxRegionLength1.Text = roi.Length1.ToString("F2");
                textBoxRegionLength2.Text = roi.Length2.ToString("F2");
                textBoxRegionPhi.Text = (roi.Phi * 180.0 / Math.PI).ToString("F2");
            }
            finally
            {
                _isSyncingRoi = false;
            }
        }

        /// <summary>
        /// 清理当前编辑ROI。
        /// </summary>
        private void ClearEditingRoi()
        {
            showImageControl1.ClearDynamicRoi();
            _roiVisible = false;
            _editingColorRoi = false;
            buttonConfirmRoi.Enabled = true;
        }

        /// <summary>生成取色框副本，保存后继续拖动不会更改运行参数。</summary>
        private ColorDetectionRegion SnapshotColorRoi()
        {
            return _colorLearningRegion == null ? null : ColorDetectionRegion.FromParameters(_colorLearningRegion.ToParameters());
        }

        /// <summary>
        /// 获取当前选择的输出区域模式。
        /// </summary>
        /// <returns>输出区域模式。</returns>
        private ColorOutputRegionMode GetSelectedOutputRegionMode()
        {
            if (comboBoxOutputRegionMode.SelectedItem is ColorOutputRegionMode mode)
                return mode;
            return ColorOutputRegionMode.All;
        }

        /// <summary>
        /// 获取输出区域模式的中文文本。
        /// </summary>
        /// <param name="mode">输出区域模式。</param>
        /// <returns>中文文本。</returns>
        private static string GetOutputRegionModeText(ColorOutputRegionMode mode)
        {
            switch (mode)
            {
                case ColorOutputRegionMode.Max:
                    return "最大区域";
                
                default:
                    return "所有区域";
            }
        }

        /// <summary>
        /// 解析浮点输入。
        /// </summary>
        /// <param name="textBox">输入框。</param>
        /// <param name="name">参数名称。</param>
        /// <returns>浮点值。</returns>
        private static float ParseFloat(TextBox textBox, string name)
        {
            float value;
            if (!float.TryParse(textBox.Text, out value))
                throw new Exception((string.IsNullOrWhiteSpace(name) ? "参数" : name) + "不是有效数字。");
            return value;
        }

        /// <summary>
        /// 解析正数浮点输入。
        /// </summary>
        /// <param name="textBox">输入框。</param>
        /// <param name="name">参数名称。</param>
        /// <returns>大于零的浮点值。</returns>
        private static float ParsePositiveFloat(TextBox textBox, string name)
        {
            float value = ParseFloat(textBox, name);
            if (value <= 0)
                throw new Exception((string.IsNullOrWhiteSpace(name) ? "参数" : name) + "必须大于0。");
            return value;
        }
    }
}
