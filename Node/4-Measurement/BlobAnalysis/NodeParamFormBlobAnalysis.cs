using Logger;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具参数窗体，按卡尺工具风格提供图像预览、ROI绘制、执行预览和参数保存。
    /// </summary>
    public partial class NodeParamFormBlobAnalysis : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 标记界面正在反写控件，避免ROI刷新事件递归。
        /// </summary>
        private bool _isSyncingRoi;

        /// <summary>
        /// 标记当前图像控件是否显示编辑ROI。
        /// </summary>
        private bool _roiVisible;

        /// <summary>
        /// 标记灰度控件正在同步，避免滑块和数值框互相触发递归。
        /// </summary>
        private bool _isSyncingGray;

        /// <summary>
        /// 初始化Blob分析工具参数窗体。
        /// </summary>
        public NodeParamFormBlobAnalysis()
        {
            InitializeComponent();
            InitializeOutputRegionModeCombo();
            BindRoiRefreshEvents();
            BindGrayRefreshEvents();
            UpdateDetectionRegionState();
        }

        /// <summary>
        /// 获取或设置节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

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
            NodeParamBlobAnalysis param = Params as NodeParamBlobAnalysis;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                checkBoxEnableDetectionRegion.Checked = param.EnableDetectionRegion;
                checkBoxUsePositionCorrection.Checked =
                    !string.IsNullOrWhiteSpace(param.CorrectionText1) &&
                    !string.IsNullOrWhiteSpace(param.CorrectionText2);
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                SyncGrayControls(ClampGray(param.MinGray), ClampGray(param.MaxGray), false);
                comboBoxOutputRegionMode.SelectedItem = param.OutputRegionMode;
                textBoxRegionColumn.Text = param.RegionColumn.ToString("F2");
                textBoxRegionRow.Text = param.RegionRow.ToString("F2");
                textBoxRegionLength1.Text = param.RegionLength1.ToString("F2");
                textBoxRegionLength2.Text = param.RegionLength2.ToString("F2");
                textBoxRegionPhi.Text = (param.RegionPhi * 180.0 / Math.PI).ToString("F2");
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
            comboBoxOutputRegionMode.Items.Add(BlobOutputRegionMode.All);
            comboBoxOutputRegionMode.Items.Add(BlobOutputRegionMode.Max);
            comboBoxOutputRegionMode.Items.Add(BlobOutputRegionMode.Min);
            comboBoxOutputRegionMode.Format += (sender, e) => e.Value = GetOutputRegionModeText((BlobOutputRegionMode)e.ListItem);
            comboBoxOutputRegionMode.SelectedItem = BlobOutputRegionMode.All;
        }

        /// <summary>
        /// 绑定灰度滑块、数值框和区域模式的实时预览事件。
        /// </summary>
        private void BindGrayRefreshEvents()
        {
            numericMinGray.ValueChanged += OnGrayControlValueChanged;
            numericMaxGray.ValueChanged += OnGrayControlValueChanged;
            trackBarMinGray.ValueChanged += OnGrayControlValueChanged;
            trackBarMaxGray.ValueChanged += OnGrayControlValueChanged;
            comboBoxOutputRegionMode.SelectedIndexChanged += OnOutputRegionModeChanged;
        }

        /// <summary>
        /// ROI参数变更后刷新图像上的编辑区域。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void OnRoiParamChanged(object sender, EventArgs e)
        {
            if (_isSyncingRoi || !_roiVisible)
                return;

            RefreshRoiFromFields();
        }

        /// <summary>
        /// 灰度滑块或数值框变化后同步另一侧控件并实时刷新效果。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void OnGrayControlValueChanged(object sender, EventArgs e)
        {
            if (_isSyncingGray)
                return;

            int minGray = sender == trackBarMinGray ? trackBarMinGray.Value : (int)numericMinGray.Value;
            int maxGray = sender == trackBarMaxGray ? trackBarMaxGray.Value : (int)numericMaxGray.Value;
            if (sender == trackBarMinGray || sender == numericMinGray)
                maxGray = Math.Max(maxGray, minGray);
            else
                minGray = Math.Min(minGray, maxGray);

            SyncGrayControls(minGray, maxGray, true);
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
        /// 执行按钮点击后按当前参数运行一次Blob分析并显示结果。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            if (!SaveParams())
                return;

            Mat temporaryGray = null;
            NodeResultBlobAnalysis result = null;
            try
            {
                Mat gray = NodeBlobAnalysis.GetGrayImage(GetInputImage(), out bool ownsGray);
                if (ownsGray)
                    temporaryGray = gray;
                result = NodeBlobAnalysis.ExecuteAnalysis(
                    gray,
                    (NodeParamBlobAnalysis)Params,
                    GetPositionCorrections());
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(result.OutputImage.SrcImg));
                showImageControl1.SetDisplayResult(result.Result);
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("执行异常：" + ex.Message);
                if (_node != null)
                    LogHelper.AddLog(MsgLevel.Exception, $"节点({_node.ID}.{_node.NodeName})Blob分析预览异常，原因：{ex.Message}", true);
            }
            finally
            {
                temporaryGray?.Dispose();
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
            try
            {
                SetPreview(MeasurementNodeHelper.GetReadOnlyPreviewMat(GetInputImage()));
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
            showImageControl1.SetDisplayResult(null);
            RefreshRoiFromFields();
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

                if (numericMinGray.Value > numericMaxGray.Value)
                    throw new Exception("最小灰度值不能大于最大灰度值。");

                Params = new NodeParamBlobAnalysis
                {
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                    CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                    CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                    MinGray = (int)numericMinGray.Value,
                    MaxGray = (int)numericMaxGray.Value,
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
        /// 设置预览图像。
        /// </summary>
        /// <param name="image">待显示图像。</param>
        private void SetPreview(Mat image)
        {
            showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(image));
        }

        /// <summary>
        /// 同步灰度滑块和数值框。
        /// </summary>
        /// <param name="minGray">最小灰度值。</param>
        /// <param name="maxGray">最大灰度值。</param>
        /// <param name="refreshPreview">是否刷新预览。</param>
        private void SyncGrayControls(int minGray, int maxGray, bool refreshPreview)
        {
            minGray = ClampGray(minGray);
            maxGray = ClampGray(maxGray);
            if (minGray > maxGray)
                maxGray = minGray;

            _isSyncingGray = true;
            try
            {
                numericMinGray.Value = minGray;
                numericMaxGray.Value = maxGray;
                trackBarMinGray.Value = minGray;
                trackBarMaxGray.Value = maxGray;
            }
            finally
            {
                _isSyncingGray = false;
            }

            if (refreshPreview)
                RefreshPreviewQuietly();
        }

        /// <summary>
        /// 在不弹出错误框的情况下刷新Blob效果，适合滑块拖动实时预览。
        /// </summary>
        private void RefreshPreviewQuietly()
        {
            Mat temporaryGray = null;
            NodeResultBlobAnalysis result = null;
            try
            {
                if (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                    string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2()))
                    return;

                NodeParamBlobAnalysis param = BuildParamFromControls(false);
                Mat gray = NodeBlobAnalysis.GetGrayImage(GetInputImage(), out bool ownsGray);
                if (ownsGray)
                    temporaryGray = gray;
                result = NodeBlobAnalysis.ExecuteAnalysis(
                    gray,
                    param,
                    GetPositionCorrections());
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(result.OutputImage.SrcImg));
                showImageControl1.SetDisplayResult(result.Result);
            }
            catch
            {
            }
            finally
            {
                temporaryGray?.Dispose();
                NodeResultResourceManager.Release(result);
            }
        }

        /// <summary>
        /// 从当前控件构建Blob参数。
        /// </summary>
        /// <param name="requireSubscription">是否强制校验订阅。</param>
        /// <returns>Blob分析参数。</returns>
        private NodeParamBlobAnalysis BuildParamFromControls(bool requireSubscription)
        {
            if (requireSubscription &&
                (string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText1()) ||
                 string.IsNullOrWhiteSpace(nodeSubscriptionImage.GetText2())))
                throw new Exception("请选择输入图像。");

            return new NodeParamBlobAnalysis
            {
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                MinGray = (int)numericMinGray.Value,
                MaxGray = (int)numericMaxGray.Value,
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
                showImageControl1.AddRoiRotatedRect(row, column, phi, length1, length2, 1.5F, Color.DodgerBlue, "Blob ROI");
                _roiVisible = true;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 将图像控件上的动态ROI反写到文本参数。
        /// </summary>
        private void TryReadRoiToFields()
        {
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
        }

        /// <summary>
        /// 将灰度值限制在0到255之间。
        /// </summary>
        /// <param name="value">待限制灰度值。</param>
        /// <returns>合法灰度值。</returns>
        private static int ClampGray(int value)
        {
            if (value < 0)
                return 0;
            if (value > 255)
                return 255;
            return value;
        }

        /// <summary>
        /// 获取当前选择的输出区域模式。
        /// </summary>
        /// <returns>输出区域模式。</returns>
        private BlobOutputRegionMode GetSelectedOutputRegionMode()
        {
            if (comboBoxOutputRegionMode.SelectedItem is BlobOutputRegionMode mode)
                return mode;
            return BlobOutputRegionMode.All;
        }

        /// <summary>
        /// 获取输出区域模式的中文文本。
        /// </summary>
        /// <param name="mode">输出区域模式。</param>
        /// <returns>中文文本。</returns>
        private static string GetOutputRegionModeText(BlobOutputRegionMode mode)
        {
            switch (mode)
            {
                case BlobOutputRegionMode.Max:
                    return "最大区域";
                case BlobOutputRegionMode.Min:
                    return "最小区域";
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
