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

        /// <summary>当前编辑ROI对应的修正快照，保存后随节点参数持久化。</summary>
        private PositionCorrectionInfo _regionCorrectionReference;

        /// <summary>快照对应的订阅节点，切换来源后重新记录绘制基准。</summary>
        private string _referenceSource;

        /// <summary>快照对应的订阅结果名称。</summary>
        private string _referenceResult;

        /// <summary>区域编辑副本，未保存前不改变运行参数。</summary>
        private readonly List<BlobDetectionRegion> _regions = new List<BlobDetectionRegion>();
        /// <summary>当前字段及动态ROI对应的区域索引。</summary>
        private int _selectedRegionIndex = -1;
        /// <summary>刷新区域列表期间禁止选择事件回写。</summary>
        private bool _syncingRegions;

        /// <summary>当前显示的编辑框，用于识别是否真正发生拖动，避免仅查看就改变保存几何。</summary>
        private BlobDetectionRegion _displayedRegion;
        /// <summary>编辑框所在图像的定位快照，提交拖动时使用，不读取随后运行产生的新位姿。</summary>
        private PositionCorrectionInfo _editingCorrectionReference;
        /// <summary>保留编辑框刷新失败原因，显式绘制时提示，输入数值未完成时不弹窗。</summary>
        private string _roiRefreshError;

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
            _regions.Add(BlobDetectionRegion.FromParameters(new NodeParamBlobAnalysis()));
            RefreshRegionList(0);
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
            labelMeasuredArea.Text = "识别区域面积：未执行";
            labelRegionCount.Text = "区域数量：未执行";
            NodeParamBlobAnalysis param = Params as NodeParamBlobAnalysis;
            if (param == null)
                return;

            _isSyncingRoi = true;
            try
            {
                _regionCorrectionReference = param.RegionCorrectionReference;
                _referenceSource = param.CorrectionText1;
                _referenceResult = param.CorrectionText2;
                nodeSubscriptionImage.SetText(param.ImageText1, param.ImageText2);
                checkBoxEnableDetectionRegion.Checked = param.EnableDetectionRegion;
                checkBoxUsePositionCorrection.Checked =
                    !string.IsNullOrWhiteSpace(param.CorrectionText1) &&
                    !string.IsNullOrWhiteSpace(param.CorrectionText2);
                nodeSubscriptionPositionCorrection.SetText(param.CorrectionText1, param.CorrectionText2);
                SyncGrayControls(ClampGray(param.MinGray), ClampGray(param.MaxGray), false);
                comboBoxOutputRegionMode.SelectedItem = param.OutputRegionMode;
                checkBoxAreaFilter.Checked = param.EnableAreaFilter;
                numericMinArea.Value = Math.Max(0, param.MinArea);
                numericMaxArea.Value = Math.Max(0, param.MaxArea);
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
            _regions.Clear();
            if (param.Regions != null && param.Regions.Count > 0)
                foreach (var region in param.Regions)
                    _regions.Add(BlobDetectionRegion.FromParameters(region.ToParameters()));
            else _regions.Add(BlobDetectionRegion.FromParameters(param));
            RefreshRegionList(0);
            LoadSelectedRegion(false);
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
            if (_isSyncingRoi)
                return;

            _regionCorrectionReference = null;
            if (!_roiVisible) return;

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
            if (!_isSyncingRoi) RefreshPreviewQuietly();
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
            labelMeasuredArea.Text = "识别区域面积：未执行";
            labelRegionCount.Text = "区域数量：未执行";
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
                ShowMeasuredArea(result.Area);
                labelRegionCount.Text = "区域数量：" + result.Count;
            }
            catch (Exception ex)
            {
                labelMeasuredArea.Text = "识别区域面积：执行失败";
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
            labelMeasuredArea.Text = "识别区域面积：未执行";
            labelRegionCount.Text = "区域数量：未执行";
            try
            {
                TryReadRoiToFields();
                RefreshGrayPreview();
                if (_roiVisible) RefreshRoiFromFields();
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
                TryReadRoiToFields();
                GetRegionCorrectionReference();
                RefreshGrayPreview();
                RefreshRoiFromFields();
                if (!_roiVisible) throw new InvalidOperationException(_roiRefreshError ?? "无法显示当前图像的ROI。");
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
                foreach (var region in _regions) region.CorrectionReference = null;
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

                if (checkBoxAreaFilter.Checked && numericMinArea.Value > numericMaxArea.Value)
                    throw new Exception("最小面积不能大于最大面积。");
                Params = new NodeParamBlobAnalysis
                {
                    Regions = SnapshotRegions(),
                    RegionCorrectionReference = GetRegionCorrectionReference(),
                    ImageText1 = nodeSubscriptionImage.GetText1(),
                    ImageText2 = nodeSubscriptionImage.GetText2(),
                    EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                    CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                    CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                    MinGray = (int)numericMinGray.Value,
                    MaxGray = (int)numericMaxGray.Value,
                    OutputRegionMode = GetSelectedOutputRegionMode(),
                    EnableAreaFilter = checkBoxAreaFilter.Checked,
                    MinArea = (int)numericMinArea.Value,
                    MaxArea = (int)numericMaxArea.Value,
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
        /// 显示分析实际使用的灰度原图，并清除上一次分析叠加，避免将旧面积误认为当前结果。
        /// </summary>
        private void RefreshGrayPreview()
        {
            Mat gray = NodeBlobAnalysis.GetGrayImage(GetInputImage(), out bool ownsGray);
            try
            {
                if (gray == null || gray.Empty())
                    throw new InvalidOperationException("没有有效的输入图像，请先运行图像来源节点。");
                showImageControl1.SetImage(MeasurementNodeHelper.ToPreviewBitmap(gray));
                showImageControl1.SetDisplayResult(null);
                labelMeasuredArea.Text = "识别区域面积：未执行";
            labelRegionCount.Text = "区域数量：未执行";
            }
            finally
            {
                // 上游缓存只读复用，仅释放本次彩色转灰度产生的临时图像。
                if (ownsGray) gray?.Dispose();
            }
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
            if (_isSyncingRoi || _syncingRegions) return;
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
                ShowMeasuredArea(result.Area);
                labelRegionCount.Text = "区域数量：" + result.Count;
            }
            catch
            {
                labelMeasuredArea.Text = "识别区域面积：暂无结果";
                labelRegionCount.Text = "区域数量：暂无结果";
            }
            finally
            {
                temporaryGray?.Dispose();
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
                Regions = SnapshotRegions(),
                RegionCorrectionReference = GetRegionCorrectionReference(),
                ImageText1 = nodeSubscriptionImage.GetText1(),
                ImageText2 = nodeSubscriptionImage.GetText2(),
                EnableDetectionRegion = checkBoxEnableDetectionRegion.Checked,
                CorrectionText1 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText1() : string.Empty,
                CorrectionText2 = checkBoxUsePositionCorrection.Checked ? nodeSubscriptionPositionCorrection.GetText2() : string.Empty,
                MinGray = (int)numericMinGray.Value,
                MaxGray = (int)numericMaxGray.Value,
                OutputRegionMode = GetSelectedOutputRegionMode(),
                    EnableAreaFilter = checkBoxAreaFilter.Checked,
                    MinArea = (int)numericMinArea.Value,
                    MaxArea = (int)numericMaxArea.Value,
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

                PositionCorrectionInfo current = null;
                if (checkBoxEnableDetectionRegion.Checked && checkBoxUsePositionCorrection.Checked)
                {
                    GetRegionCorrectionReference();
                    current = GetPositionCorrections().Find(item => item != null && item.IsValid);
                    PositionCorrectionHelper.EnsureValid(current);
                    // 与运行共用顶点变换，旧图像的绘制基准不能被当前定位直接覆盖。
                    var points = NodeBlobAnalysis.BuildRegionPoints(new NodeParamBlobAnalysis
                    {
                        RegionColumn = column, RegionRow = row, RegionLength1 = length1,
                        RegionLength2 = length2, RegionPhi = phi, RegionCorrectionReference = _regionCorrectionReference
                    }, current);
                    var vertices = new List<Point2f>();
                    foreach (var point in points) vertices.Add(new Point2f(point.X, point.Y));
                    var rectangle = Cv2.MinAreaRect(vertices);
                    column = rectangle.Center.X; row = rectangle.Center.Y;
                    length1 = rectangle.Size.Width / 2; length2 = rectangle.Size.Height / 2;
                    phi = (float)(rectangle.Angle * Math.PI / 180.0);
                }

                showImageControl1.ClearDynamicRoi();
                showImageControl1.AddRoiRotatedRect(row, column, phi, length1, length2, 1.5F, Color.DodgerBlue, "Blob ROI");
                _displayedRegion = new BlobDetectionRegion { Row = row, Column = column, Phi = phi, Length1 = length1, Length2 = length2 };
                _editingCorrectionReference = current == null ? null : CopyCorrection(current);
                _roiRefreshError = null;
                _roiVisible = true;
            }
            catch (Exception error)
            {
                ClearEditingRoi();
                _roiRefreshError = error.Message;
            }
        }

        /// <summary>复制定位数值，避免下一次上游运行改变正在编辑的图像基准。</summary>
        private static PositionCorrectionInfo CopyCorrection(PositionCorrectionInfo correction)
        {
            return new PositionCorrectionInfo
            {
                IsValid = correction.IsValid, TargetIndex = correction.TargetIndex,
                BaseX = correction.BaseX, BaseY = correction.BaseY, BaseAngle = correction.BaseAngle,
                BaseScaleX = correction.BaseScaleX, BaseScaleY = correction.BaseScaleY,
                CurrentX = correction.CurrentX, CurrentY = correction.CurrentY, CurrentAngle = correction.CurrentAngle,
                CurrentScaleX = correction.CurrentScaleX, CurrentScaleY = correction.CurrentScaleY
            };
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
                if (_referenceSource != source || _referenceResult != result)
                    foreach (var region in _regions) region.CorrectionReference = null;
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
            var rois = showImageControl1.GetAllRotatedRectInfos();
            if (rois == null || rois.Count == 0)
                return;

            var roi = rois[0];
            if (_displayedRegion != null &&
                Math.Abs(roi.Row - _displayedRegion.Row) < 0.001 && Math.Abs(roi.Column - _displayedRegion.Column) < 0.001 &&
                Math.Abs(roi.Phi - _displayedRegion.Phi) < 0.00001 && Math.Abs(roi.Length1 - _displayedRegion.Length1) < 0.001 &&
                Math.Abs(roi.Length2 - _displayedRegion.Length2) < 0.001)
                return;
            // 用户修改的是当前图像中的框，坐标与基准必须一起更新，防止下一帧重复叠加偏移。
            _regionCorrectionReference = _editingCorrectionReference;
            _displayedRegion = new BlobDetectionRegion { Row = roi.Row, Column = roi.Column, Phi = roi.Phi, Length1 = roi.Length1, Length2 = roi.Length2 };
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
            _displayedRegion = null;
            _editingCorrectionReference = null;
        }

        /// <summary>提交当前区域的拖动结果、数值参数及独立位置修正基准。</summary>
        private void CommitSelectedRegion()
        {
            if (_selectedRegionIndex < 0 || _selectedRegionIndex >= _regions.Count) return;
            TryReadRoiToFields();
            _regions[_selectedRegionIndex] = new BlobDetectionRegion
            {
                Row = ParseFloat(textBoxRegionRow, "中心Y"), Column = ParseFloat(textBoxRegionColumn, "中心X"),
                Length1 = ParsePositiveFloat(textBoxRegionLength1, "半宽"), Length2 = ParsePositiveFloat(textBoxRegionLength2, "半高"),
                Phi = (float)(ParseFloat(textBoxRegionPhi, "角度") * Math.PI / 180.0),
                CorrectionReference = checkBoxEnableDetectionRegion.Checked && checkBoxUsePositionCorrection.Checked
                    ? GetRegionCorrectionReference() : _regionCorrectionReference
            };
        }

        /// <summary>构建区域快照供运行或保存，避免后续界面编辑修改已保存列表。</summary>
        private List<BlobDetectionRegion> SnapshotRegions()
        {
            CommitSelectedRegion();
            var snapshot = new List<BlobDetectionRegion>(_regions.Count);
            foreach (var region in _regions)
            {
                // 首次启用或更换修正来源时，未建立快照的区域也以当前绘制坐标为基准。
                if (region.CorrectionReference == null && checkBoxEnableDetectionRegion.Checked && checkBoxUsePositionCorrection.Checked)
                    region.CorrectionReference = GetRegionCorrectionReference();
                snapshot.Add(BlobDetectionRegion.FromParameters(region.ToParameters()));
            }
            return snapshot;
        }

        /// <summary>更新区域名称并选中指定项，始终保留至少一个检测区域。</summary>
        private void RefreshRegionList(int selected)
        {
            _syncingRegions = true;
            try
            {
                comboBoxRegions.Items.Clear();
                for (int i = 0; i < _regions.Count; i++) comboBoxRegions.Items.Add("区域" + (i + 1));
                _selectedRegionIndex = selected;
                comboBoxRegions.SelectedIndex = selected;
                buttonRemoveRegion.Enabled = _regions.Count > 1;
            }
            finally { _syncingRegions = false; }
        }

        /// <summary>恢复所选区域的字段和绘制基准，可选显示动态ROI。</summary>
        private void LoadSelectedRegion(bool draw)
        {
            var region = _regions[_selectedRegionIndex];
            ClearEditingRoi();
            _isSyncingRoi = true;
            try
            {
                textBoxRegionColumn.Text = region.Column.ToString("F2");
                textBoxRegionRow.Text = region.Row.ToString("F2");
                textBoxRegionLength1.Text = region.Length1.ToString("F2");
                textBoxRegionLength2.Text = region.Length2.ToString("F2");
                textBoxRegionPhi.Text = (region.Phi * 180.0 / Math.PI).ToString("F2");
                _regionCorrectionReference = region.CorrectionReference;
            }
            finally { _isSyncingRoi = false; }
            if (draw) RefreshRoiFromFields();
        }

        /// <summary>切换区域前提交当前ROI，避免丢失未确认的拖动。</summary>
        private void comboBoxRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_syncingRegions || comboBoxRegions.SelectedIndex < 0) return;
            try
            {
                CommitSelectedRegion();
                _selectedRegionIndex = comboBoxRegions.SelectedIndex;
                LoadSelectedRegion(true);
            }
            catch (Exception ex)
            {
                RefreshRegionList(_selectedRegionIndex);
                MessageBoxTD.Show("切换区域失败：" + ex.Message);
            }
        }

        /// <summary>添加区域并开始编辑，复制尺寸并稍微错开中心方便识别。</summary>
        private void buttonAddRegion_Click(object sender, EventArgs e)
        {
            try
            {
                CommitSelectedRegion();
                var region = BlobDetectionRegion.FromParameters(_regions[_selectedRegionIndex].ToParameters());
                region.Column += 20; region.Row += 20;
                _regions.Add(region);
                RefreshRegionList(_regions.Count - 1);
                LoadSelectedRegion(false);
                GetRegionCorrectionReference();
                RefreshRoiFromFields();
            }
            catch (Exception ex) { MessageBoxTD.Show("添加区域失败：" + ex.Message); }
        }

        /// <summary>删除当前区域，最后一个区域保留，避免启用区域却意外检测全图。</summary>
        private void buttonRemoveRegion_Click(object sender, EventArgs e)
        {
            if (_regions.Count <= 1) return;
            _regions.RemoveAt(_selectedRegionIndex);
            RefreshRegionList(Math.Min(_selectedRegionIndex, _regions.Count - 1));
            LoadSelectedRegion(true);
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
