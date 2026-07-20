using Logger;
using MvCameraControl;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Forms.CameraAdd.Quality;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>
    /// 相机实时预览和参数调试控件。
    /// </summary>
    public partial class CameraLiveDebugControl : UserControl
    {
        /// <summary>
        /// 当前 2D 相机对象。
        /// </summary>
        private readonly ICamera _camera;

        /// <summary>
        /// 当前 3D 相机对象。
        /// </summary>
        private readonly I3DCamera _camera3D;

        /// <summary>
        /// 控件显示帧率节流时间，避免相机高帧率时频繁刷新界面。
        /// </summary>
        private readonly TimeSpan _previewFrameInterval = TimeSpan.FromMilliseconds(33);

        /// <summary>
        /// 图像质量分析节流时间，避免分析任务挤占实时预览刷新。
        /// </summary>
        private readonly TimeSpan _qualityAnalysisInterval = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// 预览图像质量分析器。
        /// </summary>
        private readonly ImageQualityAnalyzer _qualityAnalyzer = new ImageQualityAnalyzer();

        /// <summary>
        /// 清晰度滑动平均器。
        /// </summary>
        private readonly MovingAverageFilter _qualitySharpnessFilter = new MovingAverageFilter(5);

        /// <summary>
        /// 预览图像质量判定阈值。
        /// </summary>
        private readonly CameraImageQualityThresholds _qualityThresholds = new CameraImageQualityThresholds();

        /// <summary>
        /// 相机 ROI 缓存访问锁，用于在关闭并重新打开调试界面后恢复用户编辑的 ROI。
        /// </summary>
        private static readonly object _qualityRoiCacheSync = new object();

        /// <summary>
        /// 按相机唯一键保存的图像质量分析 ROI。
        /// </summary>
        private static readonly Dictionary<string, Rectangle> _qualityRoiByCameraKey = new Dictionary<string, Rectangle>();

        /// <summary>
        /// 是否正在采集。
        /// </summary>
        private bool _isCollecting;

        /// <summary>
        /// 是否保持当前图像。
        /// </summary>
        private bool _isImageHeld;

        /// <summary>
        /// 2D 相机取流是否由当前控件启动。
        /// </summary>
        private bool _started2DGrabbing;

        /// <summary>
        /// 3D 相机取流是否由当前控件启动。
        /// </summary>
        private bool _started3DGrabbing;

        /// <summary>
        /// 是否正在初始化参数，避免初始化赋值触发 SDK 写入。
        /// </summary>
        private bool _isInitializingParameters;

        /// <summary>
        /// 当前触发源是否为软件触发，供后台取帧线程安全读取。
        /// </summary>
        private volatile bool _isSoftwareTriggerSourceSelected;

        /// <summary>
        /// 图像质量分析是否正在运行，0 表示空闲，1 表示忙碌。
        /// </summary>
        private int _qualityAnalysisRunning;

        /// <summary>
        /// 图像质量分析 ROI 访问锁，避免预览线程和 UI 线程同时读写 ROI。
        /// </summary>
        private readonly object _qualityAnalysisRoiSync = new object();

        /// <summary>
        /// 图像坐标系下的亮度 / 清晰度分析 ROI。
        /// </summary>
        private Rectangle _qualityAnalysisRoi = Rectangle.Empty;

        /// <summary>
        /// 当前质量分析 ROI 对应的图像尺寸。
        /// </summary>
        private System.Drawing.Size _qualityAnalysisImageSize = System.Drawing.Size.Empty;

        /// <summary>
        /// 是否正在由代码同步 ROI 输入框，避免触发重复更新。
        /// </summary>
        private bool _isUpdatingRoiInputs;

        /// <summary>
        /// 是否处于 ROI 编辑模式。
        /// </summary>
        private bool _isEditingQualityRoi;

        /// <summary>
        /// 是否正在拖动 ROI。
        /// </summary>
        private bool _isDraggingQualityRoi;

        /// <summary>
        /// ROI 拖动开始时的鼠标位置。
        /// </summary>
        private System.Drawing.Point _qualityRoiDragStartPoint;

        /// <summary>
        /// ROI 拖动开始时的显示坐标矩形。
        /// </summary>
        private Rectangle _qualityRoiDragStartDisplayRoi;

        /// <summary>
        /// 当前拖动命中的 ROI 区域。
        /// </summary>
        private RoiHitArea _qualityRoiHitArea = RoiHitArea.无;

        /// <summary>
        /// 进入实时调试前的 2D 相机触发状态快照，用于退出调试后恢复流程节点状态。
        /// </summary>
        private Camera2DDebugStateSnapshot _camera2DDebugStateSnapshot;

        /// <summary>
        /// 是否已经请求过默认实时预览，避免句柄重建时重复启动采集。
        /// </summary>
        private bool _hasRequestedDefaultPreviewStart;

        /// <summary>
        /// 最近一次刷新界面的时间。
        /// </summary>
        private DateTime _lastPreviewFrameTime = DateTime.MinValue;

        /// <summary>
        /// 最近一次启动质量分析的时间。
        /// </summary>
        private DateTime _lastQualityAnalysisTime = DateTime.MinValue;

        /// <summary>
        /// 2D 预览主动取帧取消器，用于在 SDK 回调无图时兜底显示。
        /// </summary>
        private CancellationTokenSource _previewPollingTokenSource;

        /// <summary>
        /// 属性树分组节点字体。
        /// </summary>
        private Font _treeGroupFont;

        /// <summary>
        /// ROI 鼠标命中区域。
        /// </summary>
        private enum RoiHitArea
        {
            /// <summary>未命中。</summary>
            无,
            /// <summary>移动整个 ROI。</summary>
            移动,
            /// <summary>左边缘。</summary>
            左,
            /// <summary>右边缘。</summary>
            右,
            /// <summary>上边缘。</summary>
            上,
            /// <summary>下边缘。</summary>
            下,
            /// <summary>左上角。</summary>
            左上,
            /// <summary>右上角。</summary>
            右上,
            /// <summary>左下角。</summary>
            左下,
            /// <summary>右下角。</summary>
            右下
        }

        /// <summary>
        /// 2D 相机进入调试前的触发状态快照。
        /// </summary>
        private sealed class Camera2DDebugStateSnapshot
        {
            /// <summary>
            /// 原始触发模式。
            /// </summary>
            public TriggerModel TriggerMode { get; set; }

            /// <summary>
            /// 原始触发源。
            /// </summary>
            public TriggerSource TriggerSource { get; set; }

            /// <summary>
            /// 原始触发延迟。
            /// </summary>
            public double TriggerDelay { get; set; }

            /// <summary>
            /// 原始触发极性，读取失败时为空。
            /// </summary>
            public TriggerEdge? TriggerEdge { get; set; }
        }

        /// <summary>
        /// 创建 2D 相机实时调试控件。
        /// </summary>
        /// <param name="camera">2D 相机对象。</param>
        public CameraLiveDebugControl(ICamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            InitializeComponent();
            InitializeView();
            LoadCameraInfo();
        }

        /// <summary>
        /// 创建 3D 相机实时调试控件。
        /// </summary>
        /// <param name="camera3D">3D 相机对象。</param>
        public CameraLiveDebugControl(I3DCamera camera3D)
        {
            _camera3D = camera3D ?? throw new ArgumentNullException(nameof(camera3D));
            InitializeComponent();
            InitializeView();
            LoadCameraInfo();
        }

        /// <summary>
        /// 控件句柄创建后启动默认实时预览，确保 BeginInvoke 可以安全刷新界面。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
                BeginInvokeSafe(StartDefaultContinuousPreview);
        }

        /// <summary>
        /// 初始化界面选项和事件。
        /// </summary>
        private void InitializeView()
        {
            _isInitializingParameters = true;
            labelEmptyPreview.BringToFront();
            InitComboBoxItems();
            BindControlEvents();
            SelectPropertyPage("Basic");
            _isInitializingParameters = false;
        }

        /// <summary>
        /// 初始化下拉框选项。
        /// </summary>
        private void InitComboBoxItems()
        {
            treeViewProperty.Nodes.Clear();
            treeViewProperty.ItemHeight = 36;
            TreeNode propertyRootNode = new TreeNode("属性树") { Name = "nodePropertyRoot" };
            TreeNode commonNode = new TreeNode("常用属性") { Name = "nodeCommon" };
            TreeNode triggerNode = new TreeNode("触发") { Name = "nodeTriggerRoot" };
            TreeNode advancedNode = new TreeNode("高级属性") { Name = "nodeAdvanced" };
            _treeGroupFont?.Dispose();
            _treeGroupFont = new Font(treeViewProperty.Font, FontStyle.Bold);
            propertyRootNode.NodeFont = _treeGroupFont;
            commonNode.NodeFont = _treeGroupFont;
            triggerNode.NodeFont = _treeGroupFont;
            advancedNode.NodeFont = _treeGroupFont;
            commonNode.Nodes.Add(new TreeNode("基本属性") { Name = "nodeBasic", Tag = "Basic" });
            triggerNode.Nodes.Add(new TreeNode("IO 输入") { Name = "nodeTrigger", Tag = "Trigger" });
            triggerNode.Nodes.Add(new TreeNode("IO 输出") { Name = "nodeIoOutput", Tag = "IoOutput" });
            advancedNode.Nodes.Add(new TreeNode("ROI") { Name = "nodeRoi", Tag = "Roi" });
            treeViewProperty.Nodes.Add(propertyRootNode);
            treeViewProperty.Nodes.Add(commonNode);
            treeViewProperty.Nodes.Add(triggerNode);
            treeViewProperty.Nodes.Add(advancedNode);
            treeViewProperty.ExpandAll();

            SetComboBoxItems(comboTriggerMode, new[] { "关闭", "打开" }, "关闭");
            SetComboBoxItems(comboTriggerSource, new[] { "连续采集", "软件触发", "线路0", "线路1", "线路2", "线路3" }, "连续采集");
            SetComboBoxItems(comboTriggerEdge, new[] { "上升沿", "下降沿", "高电平", "低电平", "任意沿" }, "上升沿");
            SetComboBoxItems(comboLineSelector, new[] { "线路0", "线路1", "线路2", "线路3" }, "线路0");
            SetComboBoxItems(comboLineMode, new[] { "输入", "输出" }, "输入");
            ApplyTriggerParameterVisibility();
            ApplyIoOutputParameterVisibility();
            TreeNode[] basicNodes = treeViewProperty.Nodes.Find("nodeBasic", true);
            if (basicNodes.Length > 0)
                treeViewProperty.SelectedNode = basicNodes[0];
        }

        /// <summary>
        /// 绑定控件事件。
        /// </summary>
        private void BindControlEvents()
        {
            buttonStartCollect.Click += ButtonStartCollect_Click;
            buttonHoldImage.Click += ButtonHoldImage_Click;
            treeViewProperty.AfterSelect += TreeViewProperty_AfterSelect;
            numericExposure.ValueChanged += NumericExposure_ValueChanged;
            numericGain.ValueChanged += NumericGain_ValueChanged;
            buttonSyncBrightnessToProcess.Click += ButtonSyncBrightnessToProcess_Click;
            numericTriggerDelay.ValueChanged += NumericTriggerDelay_ValueChanged;
            comboTriggerMode.SelectedIndexChanged += ComboTriggerMode_SelectedIndexChanged;
            comboTriggerSource.SelectedIndexChanged += ComboTriggerSource_SelectedIndexChanged;
            comboTriggerEdge.SelectedIndexChanged += ComboTriggerEdge_SelectedIndexChanged;
            comboLineSelector.SelectedIndexChanged += ComboLineSelector_SelectedIndexChanged;
            comboLineMode.SelectedIndexChanged += ComboLineMode_SelectedIndexChanged;
            checkStrobeEnable.CheckedChanged += CheckStrobeEnable_CheckedChanged;
            checkLineInverter.CheckedChanged += CheckLineInverter_CheckedChanged;
            numericRoiX.ValueChanged += UpdateQualityRoiFromInputs;
            numericRoiY.ValueChanged += UpdateQualityRoiFromInputs;
            numericRoiWidth.ValueChanged += UpdateQualityRoiFromInputs;
            numericRoiHeight.ValueChanged += UpdateQualityRoiFromInputs;
            pictureBoxPreview.Paint += PictureBoxPreview_Paint;
            pictureBoxPreview.Resize += PictureBoxPreview_Resize;
            pictureBoxPreview.MouseDown += PictureBoxPreview_MouseDown;
            pictureBoxPreview.MouseMove += PictureBoxPreview_MouseMove;
            pictureBoxPreview.MouseUp += PictureBoxPreview_MouseUp;
            buttonEditRoi.Click += ButtonEditRoi_Click;
            buttonCompleteRoi.Click += ButtonCompleteRoi_Click;
            buttonRestoreRoi.Click += ButtonRestoreRoi_Click;
            buttonSoftTrigger.Click += ButtonSoftTrigger_Click;
        }

        /// <summary>
        /// 加载当前相机信息到界面。
        /// </summary>
        private void LoadCameraInfo()
        {
            _isInitializingParameters = true;
            if (_camera != null)
            {
                labelPreviewTitle.Text = $"{GetDeviceDisplayName()} ({GetSafeText(_camera.SN, "无SN")})";
                labelCurrentCameraValue.Text = GetDeviceDisplayName();
                labelDeviceModelValue.Text = GetSafeText(_camera.DevName, "未获取到型号");
                labelSnIpValue.Text = $"{GetSafeText(_camera.SN, "-")} / {GetCameraIp()}";
                labelImageSizeValue.Text = "由相机输出决定";
                labelStatusValue.Text = _camera.IsOpen ? "已连接，未开始采集" : "未连接";
                Load2DCameraSdkOptionItems();
                Capture2DDebugStateForDebug();
                TryLoadCameraParameterValues();
                SetDefaultContinuousPreviewSelection();
                ApplyTriggerParameterVisibility();
                ApplyIoOutputParameterVisibility();
                SetRoiEditState(true);
            }
            else
            {
                labelPreviewTitle.Text = $"{GetDeviceDisplayName()} ({GetSafeText(_camera3D.SN, "无SN")})";
                labelCurrentCameraValue.Text = GetDeviceDisplayName();
                labelDeviceModelValue.Text = GetSafeText(_camera3D.DevName, "3D 相机");
                labelSnIpValue.Text = $"{GetSafeText(_camera3D.SN, "-")} / {GetSafeText(_camera3D.IP, "-")}";
                labelImageSizeValue.Text = "由 3D 帧数据决定";
                labelStatusValue.Text = _camera3D.IsOpen ? "已连接，未开始采集" : "未连接";
                Set3DParameterEditState(false);
                ApplyTriggerParameterVisibility();
                ApplyIoOutputParameterVisibility();
            }
            _isInitializingParameters = false;
        }

        /// <summary>
        /// 默认使用连续采集作为调试预览态，不直接覆盖流程节点保存的触发配置。
        /// </summary>
        private void SetDefaultContinuousPreviewSelection()
        {
            bool previousInitializingState = _isInitializingParameters;
            try
            {
                _isInitializingParameters = true;
                if (comboTriggerMode.Items.Contains("关闭"))
                    comboTriggerMode.SelectedItem = "关闭";
                if (comboTriggerSource.Items.Contains("连续采集"))
                    comboTriggerSource.SelectedItem = "连续采集";
            }
            finally
            {
                _isInitializingParameters = previousInitializingState;
                ApplyTriggerParameterVisibility();
            }
        }

        /// <summary>
        /// 控件显示后自动启动临时连续采集预览，方便直接调成像效果。
        /// </summary>
        private void StartDefaultContinuousPreview()
        {
            if (_hasRequestedDefaultPreviewStart || IsDisposed || _isCollecting)
                return;

            _hasRequestedDefaultPreviewStart = true;
            SetDefaultContinuousPreviewSelection();
            StartCollectingWithUi();
        }

        /// <summary>
        /// 加载 2D 相机 SDK 当前支持的下拉选项。
        /// </summary>
        private void Load2DCameraSdkOptionItems()
        {
            if (_camera == null || !_camera.IsOpen)
                return;

            try
            {
                IEnumValue lineSelectorOptions = _camera.GetLineSelector();
                SetComboBoxItems(comboTriggerSource, new[] { "连续采集" }.Concat(GetEnumDisplayItems(_camera.GetTriggerSourceOptions(), GetTriggerSourceDisplayText, new[] { "软件触发", "线路0", "线路1", "线路2", "线路3" })), comboTriggerSource.Text);
                SetComboBoxItems(comboTriggerEdge, GetEnumDisplayItems(_camera.GetTriggerActivationOptions(), GetTriggerEdgeDisplayText, new[] { "上升沿", "下降沿", "高电平", "低电平", "任意沿" }), comboTriggerEdge.Text);
                SetComboBoxItems(comboLineSelector, GetEnumDisplayItems(lineSelectorOptions, GetLineSelectorDisplayText, new[] { "线路0", "线路1", "线路2" }), GetCurrentEnumDisplayText(lineSelectorOptions, GetLineSelectorDisplayText, comboLineSelector.Text));
                RefreshLineModeItemsForSelectedLine();
                checkStrobeEnable.Checked = _camera.GetStrobeEnable();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"读取相机 SDK 参数可选项失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 按当前线路选择器重新读取该线路支持的线路模式。
        /// </summary>
        private void RefreshLineModeItemsForSelectedLine()
        {
            if (_camera == null || !_camera.IsOpen || string.IsNullOrWhiteSpace(comboLineSelector.Text))
                return;

            bool previousInitializingState = _isInitializingParameters;
            try
            {
                _isInitializingParameters = true;
                _camera.SetLineSelector(GetLineName(comboLineSelector.Text));
                IEnumValue lineModeOptions = _camera.GetLineMode();
                SetComboBoxItems(comboLineMode, GetEnumDisplayItems(lineModeOptions, GetLineModeDisplayText, new[] { "输入", "输出" }), GetCurrentEnumDisplayText(lineModeOptions, GetLineModeDisplayText, comboLineMode.Text));
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"读取当前线路模式失败：{ex.Message}", true);
            }
            finally
            {
                _isInitializingParameters = previousInitializingState;
                ApplyIoOutputParameterVisibility();
            }
        }

        /// <summary>
        /// 加载 2D 相机当前曝光、增益和触发参数。
        /// </summary>
        private void TryLoadCameraParameterValues()
        {
            if (_camera == null || !_camera.IsOpen)
                return;

            bool previousInitializingState = _isInitializingParameters;
            try
            {
                _isInitializingParameters = true;
                SetNumericValue(numericExposure, GetSdkNumericValue(_camera.GetExposureTime(), (double)numericExposure.Value));
                var gain = _camera.GetGain();
                double gainValue = GetSdkNumericValue(gain.Item1, double.NaN);
                if (double.IsNaN(gainValue))
                    gainValue = GetSdkNumericValue(gain.Item2, (double)numericGain.Value);
                SetNumericValue(numericGain, gainValue);
                SetNumericValue(numericTriggerDelay, GetSdkNumericValue(_camera.GetTriggerDelay(), (double)numericTriggerDelay.Value));
                comboTriggerSource.SelectedItem = GetTriggerSourceText(_camera.GetTriggerSource());
                comboTriggerMode.SelectedItem = _camera.GetTriggerMode() == TriggerModel.On ? "打开" : "关闭";
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"读取相机实时参数失败：{ex.Message}", true);
            }
            finally
            {
                _isInitializingParameters = previousInitializingState;
            }
        }

        /// <summary>
        /// 设置 3D 相机暂不支持的参数编辑状态。
        /// </summary>
        /// <param name="enabled">是否启用编辑。</param>
        private void Set3DParameterEditState(bool enabled)
        {
            numericExposure.Enabled = enabled;
            numericGain.Enabled = enabled;
            comboLineSelector.Enabled = enabled;
            comboLineMode.Enabled = enabled;
            checkStrobeEnable.Enabled = enabled;
            checkLineInverter.Enabled = enabled;
            SetRoiEditState(enabled);
        }

        /// <summary>
        /// 设置 ROI 判定区域编辑状态。
        /// </summary>
        /// <param name="enabled">是否允许编辑 ROI。</param>
        private void SetRoiEditState(bool enabled)
        {
            numericRoiX.Enabled = enabled;
            numericRoiY.Enabled = enabled;
            numericRoiWidth.Enabled = enabled;
            numericRoiHeight.Enabled = enabled;
            buttonEditRoi.Enabled = enabled && !_isEditingQualityRoi;
            buttonCompleteRoi.Enabled = enabled && _isEditingQualityRoi;
            buttonRestoreRoi.Enabled = enabled;
        }

        /// <summary>
        /// 选择参数页面。
        /// </summary>
        /// <param name="pageKey">页面键。</param>
        private void SelectPropertyPage(string pageKey)
        {
            panelBasicPage.Visible = pageKey == "Basic";
            panelTriggerPage.Visible = pageKey == "Trigger";
            panelIoOutputPage.Visible = pageKey == "IoOutput";
            panelRoiPage.Visible = pageKey == "Roi";
        }

        /// <summary>
        /// 根据触发模式和触发源显示有效的触发参数。
        /// </summary>
        private void ApplyTriggerParameterVisibility()
        {
            bool triggerEnabled = comboTriggerMode.Text == "打开";
            bool softwareTriggerSource = comboTriggerSource.Text == "软件触发";
            _isSoftwareTriggerSourceSelected = softwareTriggerSource;
            SetTableRowVisible(tableLayoutPanelTrigger, 2, triggerEnabled, 46F, labelTriggerSource, comboTriggerSource);
            SetTableRowVisible(tableLayoutPanelTrigger, 3, triggerEnabled && IsHardwareTriggerSource(), 46F, labelTriggerEdge, comboTriggerEdge);
            SetTableRowVisible(tableLayoutPanelTrigger, 4, triggerEnabled, 46F, labelTriggerDelay, numericTriggerDelay);
            SetTableRowVisible(tableLayoutPanelTrigger, 5, triggerEnabled && softwareTriggerSource, 46F, labelSoftTrigger, buttonSoftTrigger);
            RefreshTableHeight(tableLayoutPanelTrigger);
        }

        /// <summary>
        /// 根据线路模式显示有效的 IO 输出参数。
        /// </summary>
        private void ApplyIoOutputParameterVisibility()
        {
            bool outputMode = comboLineMode.Text == "输出";
            SetTableRowVisible(tableLayoutPanelIoOutput, 3, outputMode, 38F, checkStrobeEnable);
            SetTableRowVisible(tableLayoutPanelIoOutput, 4, true, 38F, checkLineInverter);
            RefreshTableHeight(tableLayoutPanelIoOutput);
        }

        /// <summary>
        /// 判断当前触发源是否为硬件线路触发。
        /// </summary>
        /// <returns>是硬件线路触发返回 true。</returns>
        private bool IsHardwareTriggerSource()
        {
            return comboTriggerSource.Text.StartsWith("线路", StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断当前触发源是否为软件触发。
        /// </summary>
        /// <returns>是软件触发返回 true。</returns>
        private bool IsSoftwareTriggerSource()
        {
            return _isSoftwareTriggerSourceSelected;
        }

        /// <summary>
        /// 设置表格行及行内控件是否显示。
        /// </summary>
        /// <param name="table">目标表格。</param>
        /// <param name="rowIndex">行号。</param>
        /// <param name="visible">是否显示。</param>
        /// <param name="visibleHeight">显示时行高。</param>
        /// <param name="controls">行内控件。</param>
        private static void SetTableRowVisible(TableLayoutPanel table, int rowIndex, bool visible, float visibleHeight, params Control[] controls)
        {
            if (table == null || rowIndex < 0 || rowIndex >= table.RowStyles.Count)
                return;

            table.RowStyles[rowIndex].SizeType = SizeType.Absolute;
            table.RowStyles[rowIndex].Height = visible ? visibleHeight : 0F;
            foreach (Control control in controls.Where(control => control != null))
            {
                control.Visible = visible;
            }
        }

        /// <summary>
        /// 根据当前可见行刷新顶层表格高度。
        /// </summary>
        /// <param name="table">目标表格。</param>
        private static void RefreshTableHeight(TableLayoutPanel table)
        {
            if (table == null)
                return;

            float height = table.Padding.Top + table.Padding.Bottom + 12F;
            foreach (RowStyle rowStyle in table.RowStyles)
            {
                if (rowStyle.SizeType == SizeType.Absolute)
                    height += rowStyle.Height;
            }
            table.Height = Math.Max(88, (int)Math.Ceiling(height));
        }

        /// <summary>
        /// 处理参数树选中变化。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">节点事件参数。</param>
        private void TreeViewProperty_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string pageKey = Convert.ToString(e.Node.Tag, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(pageKey))
                return;

            SelectPropertyPage(pageKey);
        }

        /// <summary>
        /// 开始或停止采集。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonStartCollect_Click(object sender, EventArgs e)
        {
            if (_isCollecting)
                StopCollectingWithUi();
            else
                StartCollectingWithUi();
        }

        /// <summary>
        /// 切换保持图像状态。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonHoldImage_Click(object sender, EventArgs e)
        {
            if (!_isCollecting)
            {
                SetStatus("请先开始采集后再保持图像");
                return;
            }

            _isImageHeld = !_isImageHeld;
            buttonHoldImage.BackColor = _isImageHeld ? Color.FromArgb(31, 95, 189) : Color.FromArgb(44, 48, 54);
            SetStatus(_isImageHeld ? "已保持当前图像" : "已恢复实时刷新");
        }

        /// <summary>
        /// 启动采集并刷新界面状态。
        /// </summary>
        private void StartCollectingWithUi()
        {
            try
            {
                if (_camera != null)
                    Start2DCollecting();
                else
                    Start3DCollecting();

                _isCollecting = true;
                _isImageHeld = false;
                ResetQualityAnalysisState();
                labelEmptyPreview.Visible = false;
                buttonStartCollect.Text = "停止采集";
                buttonStartCollect.BackColor = Color.FromArgb(19, 150, 93);
                buttonHoldImage.BackColor = Color.FromArgb(44, 48, 54);
                SetStatus("正在采集");
                TryLoadCameraParameterValues();
            }
            catch (Exception ex)
            {
                SetStatus($"开始采集失败：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"相机预览开始采集失败：{ex}", true);
            }
        }

        /// <summary>
        /// 停止采集并刷新界面状态。
        /// </summary>
        private void StopCollectingWithUi()
        {
            StopCollectingSilently();
            _isImageHeld = false;
            labelEmptyPreview.Visible = pictureBoxPreview.Image == null;
            buttonStartCollect.Text = "开始采集";
            buttonStartCollect.BackColor = Color.FromArgb(44, 111, 211);
            buttonHoldImage.BackColor = Color.FromArgb(44, 48, 54);
            SetStatus("已停止采集");
        }

        /// <summary>
        /// 启动 2D 相机采集。
        /// </summary>
        private void Start2DCollecting()
        {
            if (!_camera.IsOpen && !_camera.Open())
                throw new InvalidOperationException("相机打开失败");

            try
            {
                Capture2DDebugStateForDebug();
                Apply2DTriggerSettingsBeforeCollecting();
                bool alreadyGrabbing = _camera.GetGrabStatus();
                _camera.OnMatReceived -= Camera_OnMatReceived;
                _camera.OnMatReceived += Camera_OnMatReceived;
                _camera.RegisterImageCallbackOwner(this);
                if (!alreadyGrabbing)
                {
                    _camera.StartGrabbing();
                    _started2DGrabbing = true;
                }
                Start2DPreviewPolling();
            }
            catch
            {
                Stop2DPreviewPolling();
                _camera.OnMatReceived -= Camera_OnMatReceived;
                _camera.UnregisterImageCallbackOwner(this);
                _started2DGrabbing = false;
                throw;
            }
        }

        /// <summary>
        /// 开始采集前按界面当前值写入 2D 触发配置。
        /// </summary>
        private void Apply2DTriggerSettingsBeforeCollecting()
        {
            if (_camera == null)
                return;

            if (comboTriggerMode.Text != "打开")
            {
                _camera.SetTriggerSource(TriggerSource.Auto);
                comboTriggerSource.SelectedItem = "连续采集";
                ApplyTriggerParameterVisibility();
                return;
            }

            _camera.SetTriggerMode(TriggerModel.On);
            _camera.SetTriggerSource(Get2DTriggerSource(comboTriggerSource.Text));
            _camera.SetTriggerDelay((double)numericTriggerDelay.Value);
            if (IsHardwareTriggerSource())
                _camera.SetTriggerEdge(Get2DTriggerEdge(comboTriggerEdge.Text));
        }

        /// <summary>
        /// 捕获进入调试预览前的 2D 相机触发状态，避免调试态污染流程节点配置。
        /// </summary>
        private void Capture2DDebugStateForDebug()
        {
            if (_camera == null || _camera2DDebugStateSnapshot != null || !_camera.IsOpen)
                return;

            try
            {
                _camera2DDebugStateSnapshot = new Camera2DDebugStateSnapshot
                {
                    TriggerMode = _camera.GetTriggerMode(),
                    TriggerSource = _camera.GetTriggerSource(),
                    TriggerDelay = GetSdkNumericValue(_camera.GetTriggerDelay(), (double)numericTriggerDelay.Value),
                    TriggerEdge = TryGetCurrent2DTriggerEdge()
                };
            }
            catch (Exception ex)
            {
                _camera2DDebugStateSnapshot = null;
                LogHelper.AddLog(MsgLevel.Warn, $"保存相机调试前触发状态失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 调试预览结束后恢复 2D 相机触发状态，以流程节点配置为准。
        /// </summary>
        private void Restore2DDebugStateAfterDebug()
        {
            if (_camera == null || _camera2DDebugStateSnapshot == null || !_camera.IsOpen)
                return;

            try
            {
                _camera.SetTriggerMode(_camera2DDebugStateSnapshot.TriggerMode);
                _camera.SetTriggerSource(_camera2DDebugStateSnapshot.TriggerSource);
                _camera.SetTriggerDelay(_camera2DDebugStateSnapshot.TriggerDelay);
                if (_camera2DDebugStateSnapshot.TriggerEdge.HasValue)
                    _camera.SetTriggerEdge(_camera2DDebugStateSnapshot.TriggerEdge.Value);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"恢复相机调试前触发状态失败：{ex.Message}", true);
            }
            finally
            {
                _camera2DDebugStateSnapshot = null;
            }
        }

        /// <summary>
        /// 尝试读取 SDK 当前触发极性。
        /// </summary>
        /// <returns>读取成功返回触发极性，否则返回空。</returns>
        private TriggerEdge? TryGetCurrent2DTriggerEdge()
        {
            try
            {
                IEnumValue triggerEdgeOptions = _camera.GetTriggerActivationOptions();
                string triggerEdgeText = GetCurrentEnumDisplayText(triggerEdgeOptions, GetTriggerEdgeDisplayText, string.Empty);
                return string.IsNullOrWhiteSpace(triggerEdgeText) ? (TriggerEdge?)null : Get2DTriggerEdge(triggerEdgeText);
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"读取当前触发极性失败：{ex.Message}", true);
                return null;
            }
        }

        /// <summary>
        /// 启动 3D 相机采集。
        /// </summary>
        private void Start3DCollecting()
        {
            if (!_camera3D.IsOpen && !_camera3D.Open())
                throw new InvalidOperationException("3D 相机打开失败");

            try
            {
                bool alreadyGrabbing = _camera3D.GetGrabStatus();
                _camera3D.FrameArrived -= Camera3D_FrameArrived;
                _camera3D.FrameArrived += Camera3D_FrameArrived;
                if (!alreadyGrabbing)
                {
                    _camera3D.StartGrabbing();
                    _started3DGrabbing = true;
                }
            }
            catch
            {
                _camera3D.FrameArrived -= Camera3D_FrameArrived;
                _started3DGrabbing = false;
                throw;
            }
        }

        /// <summary>
        /// 安静停止采集，用于释放控件和异常回滚。
        /// </summary>
        private void StopCollectingSilently()
        {
            try
            {
                Stop2DPreviewPolling();
                if (_camera != null)
                {
                    _camera.OnMatReceived -= Camera_OnMatReceived;
                    _camera.UnregisterImageCallbackOwner(this);
                    if (_started2DGrabbing && _camera.IsOpen && _camera.GetGrabStatus())
                        _camera.StopGrabbing();
                }
                if (_camera3D != null)
                {
                    _camera3D.FrameArrived -= Camera3D_FrameArrived;
                    if (_started3DGrabbing && _camera3D.IsOpen && _camera3D.GetGrabStatus())
                        _camera3D.StopGrabbing();
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"停止相机预览采集时发生异常：{ex.Message}", true);
            }
            finally
            {
                Restore2DDebugStateAfterDebug();
                _isCollecting = false;
                _started2DGrabbing = false;
                _started3DGrabbing = false;
            }
        }

        /// <summary>
        /// 处理 2D 相机 Mat 帧回调。
        /// </summary>
        /// <param name="mat">相机图像帧。</param>
        private void Camera_OnMatReceived(Mat mat)
        {
            Bitmap bitmap = null;
            try
            {
                if (mat == null || !_isCollecting || _isImageHeld || IsDisposed)
                    return;

                if (DateTime.Now - _lastPreviewFrameTime < _previewFrameInterval)
                    return;

                _lastPreviewFrameTime = DateTime.Now;
                bitmap = BitmapConverter.ToBitmap(mat);
                ShowPreviewImageAsync(bitmap);
                bitmap = null;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"显示相机预览帧失败：{ex.Message}", true);
            }
            finally
            {
                bitmap?.Dispose();
                mat?.Dispose();
            }
        }

        /// <summary>
        /// 启动 2D 相机主动取帧兜底线程。
        /// </summary>
        private void Start2DPreviewPolling()
        {
            Stop2DPreviewPolling();
            _previewPollingTokenSource = new CancellationTokenSource();
            CancellationToken token = _previewPollingTokenSource.Token;
            Task.Run(() => Poll2DPreviewFrames(token), token);
        }

        /// <summary>
        /// 停止 2D 相机主动取帧兜底线程。
        /// </summary>
        private void Stop2DPreviewPolling()
        {
            CancellationTokenSource tokenSource = _previewPollingTokenSource;
            _previewPollingTokenSource = null;
            if (tokenSource == null)
                return;

            tokenSource.Cancel();
        }

        /// <summary>
        /// 主动获取 2D 预览帧；当 SDK 回调没有帧时仍能显示实时图像。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        private async Task Poll2DPreviewFrames(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !IsDisposed)
            {
                try
                {
                    if (!_isCollecting || _isImageHeld || _camera == null || !_camera.IsOpen || !_camera.GetGrabStatus())
                    {
                        await Task.Delay(80, token);
                        continue;
                    }

                    if (IsSoftwareTriggerSource())
                    {
                        BeginInvokeSafe(() => SetStatus("软件触发等待手动执行"));
                        await Task.Delay(120, token);
                        continue;
                    }

                    if (DateTime.Now - _lastPreviewFrameTime < TimeSpan.FromMilliseconds(120))
                    {
                        await Task.Delay(40, token);
                        continue;
                    }

                    Bitmap bitmap = _camera.GetOneFrameImage();
                    if (bitmap != null)
                    {
                        _lastPreviewFrameTime = DateTime.Now;
                        ShowPreviewImageAsync(bitmap);
                    }
                    else
                    {
                        BeginInvokeSafe(() => SetStatus("未取到图像，请检查触发源或相机状态"));
                    }

                    await Task.Delay(40, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"主动获取相机预览帧失败：{ex.Message}", true);
                    try
                    {
                        await Task.Delay(200, token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 处理 3D 相机帧到达事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">3D 帧事件参数。</param>
        private void Camera3D_FrameArrived(object sender, Camera3DFrameArrivedEventArgs e)
        {
            if (!_isCollecting || _isImageHeld || IsDisposed)
                return;

            if (DateTime.Now - _lastPreviewFrameTime < _previewFrameInterval)
                return;

            _lastPreviewFrameTime = DateTime.Now;
            int frameWidth = e.Frame.Width;
            int frameHeight = e.Frame.Height;
            uint frameNumber = e.Frame.FrameNumber;
            BeginInvokeSafe(() =>
            {
                labelEmptyPreview.Visible = false;
                labelImageSizeValue.Text = $"{frameWidth} × {frameHeight}";
                bool displayed = _camera3D.DisplayLatestPointCloud(pictureBoxPreview.Handle);
                SetStatus(displayed ? $"3D 点云显示中，帧号 {frameNumber}" : $"3D 数据采集中，帧号 {frameNumber}");
            });
        }

        /// <summary>
        /// 异步显示预览图像。
        /// </summary>
        /// <param name="bitmap">待显示图像。</param>
        private void ShowPreviewImageAsync(Bitmap bitmap)
        {
            if (bitmap == null)
                return;

            TryPrepareQualityAnalysisBitmap(bitmap, out Bitmap analysisBitmap);
            Bitmap previewBitmap = bitmap;
            bool isPosted = BeginInvokeSafe(() =>
            {
                Bitmap imageToShow = previewBitmap;
                previewBitmap = null;
                Bitmap oldImage = pictureBoxPreview.Image as Bitmap;
                try
                {
                    pictureBoxPreview.Image = imageToShow;
                    imageToShow = null;
                    labelEmptyPreview.Visible = false;
                    labelImageSizeValue.Text = $"{pictureBoxPreview.Image.Width} × {pictureBoxPreview.Image.Height}";
                    EnsureQualityRoiForImageSize(pictureBoxPreview.Image.Size);
                    pictureBoxPreview.Invalidate();
                    SetStatus(_isImageHeld ? "已保持当前图像" : "正在采集");
                }
                finally
                {
                    imageToShow?.Dispose();
                    oldImage?.Dispose();
                }
            });
            if (!isPosted)
            {
                previewBitmap?.Dispose();
                ReleasePreparedQualityAnalysisBitmap(analysisBitmap);
                return;
            }

            StartPreviewQualityAnalysis(analysisBitmap);
        }

        /// <summary>
        /// 尝试准备预览图像质量分析副本；必须在图像交给 PictureBox 前执行，避免 GDI+ 并发占用。
        /// </summary>
        /// <param name="bitmap">当前预览帧。</param>
        /// <param name="analysisBitmap">可用于后台分析的图像副本。</param>
        /// <returns>是否成功准备分析副本。</returns>
        private bool TryPrepareQualityAnalysisBitmap(Bitmap bitmap, out Bitmap analysisBitmap)
        {
            analysisBitmap = null;
            if (_camera == null || bitmap == null || !_isCollecting || _isImageHeld)
                return false;

            DateTime now = DateTime.Now;
            if (now - _lastQualityAnalysisTime < _qualityAnalysisInterval)
                return false;

            if (Interlocked.CompareExchange(ref _qualityAnalysisRunning, 1, 0) != 0)
                return false;

            try
            {
                _lastQualityAnalysisTime = now;
                analysisBitmap = new Bitmap(bitmap);
                return true;
            }
            catch (Exception ex)
            {
                analysisBitmap?.Dispose();
                analysisBitmap = null;
                Interlocked.Exchange(ref _qualityAnalysisRunning, 0);
                LogHelper.AddLog(MsgLevel.Warn, $"启动图像质量分析失败：{ex.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// 启动预览图像质量分析；分析在后台执行，不阻塞实时图像显示。
        /// </summary>
        /// <param name="analysisBitmap">当前预览帧副本。</param>
        private void StartPreviewQualityAnalysis(Bitmap analysisBitmap)
        {
            if (analysisBitmap == null)
                return;

            Task.Run(() => AnalyzePreviewQualityAsync(analysisBitmap));
        }

        /// <summary>
        /// 释放未能进入后台分析的图像副本，并释放分析并发门。
        /// </summary>
        /// <param name="analysisBitmap">当前预览帧副本。</param>
        private void ReleasePreparedQualityAnalysisBitmap(Bitmap analysisBitmap)
        {
            if (analysisBitmap == null)
                return;

            analysisBitmap.Dispose();
            Interlocked.Exchange(ref _qualityAnalysisRunning, 0);
        }

        /// <summary>
        /// 后台分析预览帧的亮度和清晰度。
        /// </summary>
        /// <param name="bitmap">独立的预览帧副本。</param>
        private void AnalyzePreviewQualityAsync(Bitmap bitmap)
        {
            try
            {
                using (bitmap)
                {
                    byte[] gray = BitmapGrayConverter.Convert(bitmap);
                    Rectangle roi = GetQualityAnalysisRoi(bitmap.Size);
                    CameraImageQualityResult rawResult = _qualityAnalyzer.Analyze(
                        gray,
                        bitmap.Width,
                        bitmap.Height,
                        bitmap.Width,
                        roi,
                        _qualityThresholds);
                    double smoothSharpness = _qualitySharpnessFilter.Add(rawResult.Sharpness);
                    CameraImageQualityResult smoothResult = rawResult.WithSmoothSharpness(smoothSharpness, _qualityThresholds);
                    BeginInvokeSafe(() => PresentQualityResult(smoothResult));
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"图像质量分析失败：{ex.Message}", true);
            }
            finally
            {
                Interlocked.Exchange(ref _qualityAnalysisRunning, 0);
            }
        }

        /// <summary>
        /// 获取图像质量分析区域。
        /// </summary>
        /// <param name="imageSize">图像尺寸。</param>
        /// <returns>分析 ROI。</returns>
        private Rectangle GetQualityAnalysisRoi(System.Drawing.Size imageSize)
        {
            lock (_qualityAnalysisRoiSync)
            {
                Rectangle roi = _qualityAnalysisRoi.IsEmpty
                    ? CreateDefaultQualityRoi(imageSize)
                    : _qualityAnalysisRoi;
                return ClampQualityRoi(roi, imageSize, 16);
            }
        }

        /// <summary>
        /// 确保当前图像尺寸下存在可用 ROI，并同步到右侧 ROI 数值框。
        /// </summary>
        /// <param name="imageSize">当前预览图像尺寸。</param>
        private void EnsureQualityRoiForImageSize(System.Drawing.Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return;

            Rectangle roi;
            lock (_qualityAnalysisRoiSync)
            {
                _qualityAnalysisImageSize = imageSize;
                if (_qualityAnalysisRoi.IsEmpty)
                    roi = TryRestoreQualityRoiSnapshot(imageSize, out Rectangle restoredRoi)
                        ? restoredRoi
                        : CreateDefaultQualityRoi(imageSize);
                else
                    roi = ClampQualityRoi(_qualityAnalysisRoi, imageSize, 16);
                _qualityAnalysisRoi = roi;
            }

            SetRoiInputsFromRectangle(roi);
        }

        /// <summary>
        /// 根据 ROI 数值框更新质量分析区域。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void UpdateQualityRoiFromInputs(object sender, EventArgs e)
        {
            if (_isUpdatingRoiInputs)
                return;

            System.Drawing.Size imageSize = GetCurrentPreviewImageSize();
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return;

            Rectangle roi = new Rectangle(
                Decimal.ToInt32(numericRoiX.Value),
                Decimal.ToInt32(numericRoiY.Value),
                Decimal.ToInt32(numericRoiWidth.Value),
                Decimal.ToInt32(numericRoiHeight.Value));
            roi = ClampQualityRoi(roi, imageSize, 16);

            lock (_qualityAnalysisRoiSync)
            {
                _qualityAnalysisImageSize = imageSize;
                _qualityAnalysisRoi = roi;
            }

            SaveQualityRoiSnapshot(roi);
            SetRoiInputsFromRectangle(roi);
            pictureBoxPreview.Invalidate();
        }

        /// <summary>
        /// 获取当前预览图像尺寸。
        /// </summary>
        /// <returns>当前预览图像尺寸。</returns>
        private System.Drawing.Size GetCurrentPreviewImageSize()
        {
            Image image = pictureBoxPreview.Image;
            if (image != null)
                return image.Size;

            lock (_qualityAnalysisRoiSync)
            {
                return _qualityAnalysisImageSize;
            }
        }

        /// <summary>
        /// 将 ROI 同步到右侧数值输入框。
        /// </summary>
        /// <param name="roi">图像坐标系下的 ROI。</param>
        private void SetRoiInputsFromRectangle(Rectangle roi)
        {
            _isUpdatingRoiInputs = true;
            try
            {
                SetNumericValue(numericRoiX, roi.X);
                SetNumericValue(numericRoiY, roi.Y);
                SetNumericValue(numericRoiWidth, roi.Width);
                SetNumericValue(numericRoiHeight, roi.Height);
            }
            finally
            {
                _isUpdatingRoiInputs = false;
            }
        }

        /// <summary>
        /// 按 demo 逻辑创建默认中央 ROI。
        /// </summary>
        /// <param name="imageSize">图像尺寸。</param>
        /// <returns>默认 ROI。</returns>
        private static Rectangle CreateDefaultQualityRoi(System.Drawing.Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return Rectangle.Empty;

            int height = Math.Max(1, imageSize.Height / 2);
            return new Rectangle(0, (imageSize.Height - height) / 2, imageSize.Width, height);
        }

        /// <summary>
        /// 将 ROI 限制到图像范围内。
        /// </summary>
        /// <param name="roi">原始 ROI。</param>
        /// <param name="imageSize">图像尺寸。</param>
        /// <param name="minimumSize">最小尺寸。</param>
        /// <returns>限制后的 ROI。</returns>
        private static Rectangle ClampQualityRoi(Rectangle roi, System.Drawing.Size imageSize, int minimumSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return Rectangle.Empty;
            if (roi.Width <= 0 || roi.Height <= 0)
                roi = CreateDefaultQualityRoi(imageSize);

            int minWidth = Math.Min(Math.Max(1, minimumSize), imageSize.Width);
            int minHeight = Math.Min(Math.Max(1, minimumSize), imageSize.Height);
            int width = Math.Min(Math.Max(minWidth, roi.Width), imageSize.Width);
            int height = Math.Min(Math.Max(minHeight, roi.Height), imageSize.Height);
            int x = Math.Min(Math.Max(0, roi.X), imageSize.Width - width);
            int y = Math.Min(Math.Max(0, roi.Y), imageSize.Height - height);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// 尝试按当前相机恢复已保存的 ROI。
        /// </summary>
        /// <param name="imageSize">当前图像尺寸。</param>
        /// <param name="restoredRoi">恢复后的 ROI。</param>
        /// <returns>是否存在可恢复 ROI。</returns>
        private bool TryRestoreQualityRoiSnapshot(System.Drawing.Size imageSize, out Rectangle restoredRoi)
        {
            restoredRoi = Rectangle.Empty;
            string key = GetQualityRoiStorageKey();
            if (string.IsNullOrWhiteSpace(key))
                return false;

            lock (_qualityRoiCacheSync)
            {
                if (!_qualityRoiByCameraKey.TryGetValue(key, out Rectangle savedRoi))
                    return false;

                restoredRoi = ClampQualityRoi(savedRoi, imageSize, 16);
                return !restoredRoi.IsEmpty;
            }
        }

        /// <summary>
        /// 保存当前 ROI 快照。
        /// </summary>
        private void SaveQualityRoiSnapshot()
        {
            Rectangle roi;
            lock (_qualityAnalysisRoiSync)
            {
                roi = _qualityAnalysisRoi;
            }

            SaveQualityRoiSnapshot(roi);
        }

        /// <summary>
        /// 保存指定 ROI 快照，供重新进入相机调试界面时恢复。
        /// </summary>
        /// <param name="roi">需要保存的 ROI。</param>
        private void SaveQualityRoiSnapshot(Rectangle roi)
        {
            if (roi.IsEmpty || roi.Width <= 0 || roi.Height <= 0)
                return;

            string key = GetQualityRoiStorageKey();
            if (string.IsNullOrWhiteSpace(key))
                return;

            lock (_qualityRoiCacheSync)
            {
                _qualityRoiByCameraKey[key] = roi;
            }
        }

        /// <summary>
        /// 获取当前相机的 ROI 缓存键。
        /// </summary>
        /// <returns>当前相机缓存键。</returns>
        private string GetQualityRoiStorageKey()
        {
            if (_camera != null)
                return $"2D|{GetSafeText(_camera.SN, "-")}|{GetCameraIp()}|{GetSafeText(_camera.DevName, "-")}";
            if (_camera3D != null)
                return $"3D|{GetSafeText(_camera3D.SN, "-")}|{GetSafeText(_camera3D.IP, "-")}|{GetSafeText(_camera3D.DevName, "-")}";
            return string.Empty;
        }

        /// <summary>
        /// 绘制预览图上的 ROI 叠加框。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">绘制参数。</param>
        private void PictureBoxPreview_Paint(object sender, PaintEventArgs e)
        {
            DrawImageRoiOverlay(e.Graphics);
        }

        /// <summary>
        /// 预览区域尺寸变化时刷新 ROI 叠加框。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void PictureBoxPreview_Resize(object sender, EventArgs e)
        {
            pictureBoxPreview.Invalidate();
        }

        /// <summary>
        /// 开始 ROI 拖动或缩放。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">鼠标事件参数。</param>
        private void PictureBoxPreview_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_isEditingQualityRoi || e.Button != MouseButtons.Left || pictureBoxPreview.Image == null)
                return;

            RoiHitArea hitArea = HitTestQualityRoi(e.Location);
            if (hitArea == RoiHitArea.无)
                return;

            _qualityRoiHitArea = hitArea;
            _isDraggingQualityRoi = true;
            _qualityRoiDragStartPoint = e.Location;
            _qualityRoiDragStartDisplayRoi = GetCurrentDisplayRoi();
            pictureBoxPreview.Capture = true;
        }

        /// <summary>
        /// 拖动或缩放 ROI。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">鼠标事件参数。</param>
        private void PictureBoxPreview_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEditingQualityRoi || pictureBoxPreview.Image == null)
                return;

            if (!_isDraggingQualityRoi)
            {
                pictureBoxPreview.Cursor = CursorForRoiHit(HitTestQualityRoi(e.Location));
                return;
            }

            Rectangle displayRoi = BuildDraggedDisplayRoi(_qualityRoiDragStartDisplayRoi, _qualityRoiHitArea,
                e.X - _qualityRoiDragStartPoint.X,
                e.Y - _qualityRoiDragStartPoint.Y);
            Rectangle imageRoi = DisplayRectangleToImageRoi(displayRoi, pictureBoxPreview.Image.Size, pictureBoxPreview.ClientSize);
            imageRoi = ClampQualityRoi(imageRoi, pictureBoxPreview.Image.Size, 16);
            lock (_qualityAnalysisRoiSync)
            {
                _qualityAnalysisImageSize = pictureBoxPreview.Image.Size;
                _qualityAnalysisRoi = imageRoi;
            }

            SaveQualityRoiSnapshot(imageRoi);
            SetRoiInputsFromRectangle(imageRoi);
            pictureBoxPreview.Invalidate();
        }

        /// <summary>
        /// 结束 ROI 拖动或缩放。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">鼠标事件参数。</param>
        private void PictureBoxPreview_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_isDraggingQualityRoi)
                return;

            _isDraggingQualityRoi = false;
            _qualityRoiHitArea = RoiHitArea.无;
            pictureBoxPreview.Capture = false;
            pictureBoxPreview.Cursor = _isEditingQualityRoi ? Cursors.SizeAll : Cursors.Default;
        }

        /// <summary>
        /// 获取当前 ROI 的显示坐标矩形。
        /// </summary>
        /// <returns>显示坐标 ROI。</returns>
        private Rectangle GetCurrentDisplayRoi()
        {
            if (pictureBoxPreview.Image == null)
                return Rectangle.Empty;

            Rectangle roi;
            lock (_qualityAnalysisRoiSync)
            {
                roi = _qualityAnalysisRoi;
            }

            return ImageRoiToDisplayRectangle(roi, pictureBoxPreview.Image.Size, pictureBoxPreview.ClientSize);
        }

        /// <summary>
        /// 检测鼠标命中的 ROI 区域。
        /// </summary>
        /// <param name="point">鼠标显示坐标。</param>
        /// <returns>命中区域。</returns>
        private RoiHitArea HitTestQualityRoi(System.Drawing.Point point)
        {
            Rectangle roi = GetCurrentDisplayRoi();
            if (roi.IsEmpty)
                return RoiHitArea.无;

            const int margin = 8;
            bool left = Math.Abs(point.X - roi.Left) <= margin && point.Y >= roi.Top - margin && point.Y <= roi.Bottom + margin;
            bool right = Math.Abs(point.X - roi.Right) <= margin && point.Y >= roi.Top - margin && point.Y <= roi.Bottom + margin;
            bool top = Math.Abs(point.Y - roi.Top) <= margin && point.X >= roi.Left - margin && point.X <= roi.Right + margin;
            bool bottom = Math.Abs(point.Y - roi.Bottom) <= margin && point.X >= roi.Left - margin && point.X <= roi.Right + margin;
            if (left && top)
                return RoiHitArea.左上;
            if (right && top)
                return RoiHitArea.右上;
            if (left && bottom)
                return RoiHitArea.左下;
            if (right && bottom)
                return RoiHitArea.右下;
            if (left)
                return RoiHitArea.左;
            if (right)
                return RoiHitArea.右;
            if (top)
                return RoiHitArea.上;
            if (bottom)
                return RoiHitArea.下;
            return roi.Contains(point) ? RoiHitArea.移动 : RoiHitArea.无;
        }

        /// <summary>
        /// 根据拖动方向生成新的显示坐标 ROI。
        /// </summary>
        /// <param name="startRoi">拖动开始时的显示 ROI。</param>
        /// <param name="hitArea">命中区域。</param>
        /// <param name="deltaX">水平移动量。</param>
        /// <param name="deltaY">垂直移动量。</param>
        /// <returns>拖动后的显示 ROI。</returns>
        private static Rectangle BuildDraggedDisplayRoi(Rectangle startRoi, RoiHitArea hitArea, int deltaX, int deltaY)
        {
            Rectangle roi = startRoi;
            switch (hitArea)
            {
                case RoiHitArea.移动:
                    roi.Offset(deltaX, deltaY);
                    break;
                case RoiHitArea.左:
                    roi.X += deltaX;
                    roi.Width -= deltaX;
                    break;
                case RoiHitArea.右:
                    roi.Width += deltaX;
                    break;
                case RoiHitArea.上:
                    roi.Y += deltaY;
                    roi.Height -= deltaY;
                    break;
                case RoiHitArea.下:
                    roi.Height += deltaY;
                    break;
                case RoiHitArea.左上:
                    roi.X += deltaX;
                    roi.Width -= deltaX;
                    roi.Y += deltaY;
                    roi.Height -= deltaY;
                    break;
                case RoiHitArea.右上:
                    roi.Width += deltaX;
                    roi.Y += deltaY;
                    roi.Height -= deltaY;
                    break;
                case RoiHitArea.左下:
                    roi.X += deltaX;
                    roi.Width -= deltaX;
                    roi.Height += deltaY;
                    break;
                case RoiHitArea.右下:
                    roi.Width += deltaX;
                    roi.Height += deltaY;
                    break;
            }

            if (roi.Width < 1)
            {
                roi.X += roi.Width - 1;
                roi.Width = 1;
            }
            if (roi.Height < 1)
            {
                roi.Y += roi.Height - 1;
                roi.Height = 1;
            }
            return roi;
        }

        /// <summary>
        /// 获取 ROI 命中区域对应的鼠标样式。
        /// </summary>
        /// <param name="hitArea">命中区域。</param>
        /// <returns>鼠标样式。</returns>
        private static Cursor CursorForRoiHit(RoiHitArea hitArea)
        {
            switch (hitArea)
            {
                case RoiHitArea.左:
                case RoiHitArea.右:
                    return Cursors.SizeWE;
                case RoiHitArea.上:
                case RoiHitArea.下:
                    return Cursors.SizeNS;
                case RoiHitArea.左上:
                case RoiHitArea.右下:
                    return Cursors.SizeNWSE;
                case RoiHitArea.右上:
                case RoiHitArea.左下:
                    return Cursors.SizeNESW;
                case RoiHitArea.移动:
                    return Cursors.SizeAll;
                default:
                    return Cursors.Default;
            }
        }

        /// <summary>
        /// 在预览图像上绘制当前 ROI。
        /// </summary>
        /// <param name="graphics">绘图对象。</param>
        private void DrawImageRoiOverlay(Graphics graphics)
        {
            if (graphics == null || pictureBoxPreview.Image == null)
                return;

            Rectangle roi;
            lock (_qualityAnalysisRoiSync)
            {
                roi = _qualityAnalysisRoi;
            }

            if (roi.IsEmpty)
                return;

            Rectangle displayRoi = ImageRoiToDisplayRectangle(roi, pictureBoxPreview.Image.Size, pictureBoxPreview.ClientSize);
            if (displayRoi.Width <= 0 || displayRoi.Height <= 0)
                return;

            using (Brush brush = new SolidBrush(Color.FromArgb(36, 30, 144, 255)))
            using (Pen pen = new Pen(Color.DodgerBlue, 2F))
            {
                graphics.FillRectangle(brush, displayRoi);
                graphics.DrawRectangle(pen, displayRoi);
            }
        }

        /// <summary>
        /// 将图像坐标系 ROI 转换成 PictureBox 显示坐标。
        /// </summary>
        /// <param name="roi">图像坐标系 ROI。</param>
        /// <param name="imageSize">图像尺寸。</param>
        /// <param name="clientSize">预览控件尺寸。</param>
        /// <returns>显示坐标系 ROI。</returns>
        private static Rectangle ImageRoiToDisplayRectangle(Rectangle roi, System.Drawing.Size imageSize, System.Drawing.Size clientSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || clientSize.Width <= 0 || clientSize.Height <= 0)
                return Rectangle.Empty;

            float scale = Math.Min(clientSize.Width / (float)imageSize.Width, clientSize.Height / (float)imageSize.Height);
            float displayWidth = imageSize.Width * scale;
            float displayHeight = imageSize.Height * scale;
            float offsetX = (clientSize.Width - displayWidth) / 2F;
            float offsetY = (clientSize.Height - displayHeight) / 2F;
            RectangleF displayRoi = new RectangleF(
                offsetX + roi.X * scale,
                offsetY + roi.Y * scale,
                roi.Width * scale,
                roi.Height * scale);
            return Rectangle.Round(displayRoi);
        }

        /// <summary>
        /// 将 PictureBox 显示坐标 ROI 转换回图像坐标系。
        /// </summary>
        /// <param name="displayRoi">显示坐标 ROI。</param>
        /// <param name="imageSize">图像尺寸。</param>
        /// <param name="clientSize">预览控件尺寸。</param>
        /// <returns>图像坐标系 ROI。</returns>
        private static Rectangle DisplayRectangleToImageRoi(Rectangle displayRoi, System.Drawing.Size imageSize, System.Drawing.Size clientSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || clientSize.Width <= 0 || clientSize.Height <= 0)
                return Rectangle.Empty;

            float scale = Math.Min(clientSize.Width / (float)imageSize.Width, clientSize.Height / (float)imageSize.Height);
            if (scale <= 0)
                return Rectangle.Empty;

            float displayWidth = imageSize.Width * scale;
            float displayHeight = imageSize.Height * scale;
            float offsetX = (clientSize.Width - displayWidth) / 2F;
            float offsetY = (clientSize.Height - displayHeight) / 2F;
            RectangleF imageRoi = new RectangleF(
                (displayRoi.X - offsetX) / scale,
                (displayRoi.Y - offsetY) / scale,
                displayRoi.Width / scale,
                displayRoi.Height / scale);
            return Rectangle.Round(imageRoi);
        }

        /// <summary>
        /// 在 UI 线程显示亮度和清晰度分析结果。
        /// </summary>
        /// <param name="result">分析结果。</param>
        private void PresentQualityResult(CameraImageQualityResult result)
        {
            if (result == null || IsDisposed)
                return;

            labelBrightnessStateValue.Text = $"{result.BrightnessLevel}  {result.MeanBrightness:F1}";
            labelBrightnessStateValue.ForeColor = GetBrightnessColor(result.BrightnessLevel);
            labelSharpnessStateValue.Text = $"{result.SharpnessLevel}  {result.Sharpness:F0}";
            labelSharpnessStateValue.ForeColor = result.SharpnessLevel == SharpnessLevel.清晰
                ? Color.FromArgb(80, 220, 140)
                : Color.FromArgb(255, 120, 120);
            labelQualitySuggestionValue.Text = BuildQualitySuggestion(result);
        }

        /// <summary>
        /// 重置预览图像质量分析状态。
        /// </summary>
        private void ResetQualityAnalysisState()
        {
            _qualitySharpnessFilter.Reset();
            _lastQualityAnalysisTime = DateTime.MinValue;
            Interlocked.Exchange(ref _qualityAnalysisRunning, 0);
            if (labelBrightnessStateValue != null)
            {
                labelBrightnessStateValue.Text = "等待图像";
                labelBrightnessStateValue.ForeColor = Color.FromArgb(200, 205, 212);
            }
            if (labelSharpnessStateValue != null)
            {
                labelSharpnessStateValue.Text = "等待图像";
                labelSharpnessStateValue.ForeColor = Color.FromArgb(200, 205, 212);
            }
            if (labelQualitySuggestionValue != null)
                labelQualitySuggestionValue.Text = "请先确认亮度，再调整焦距。";
        }

        /// <summary>
        /// 构建亮度优先的成像调试提示。
        /// </summary>
        /// <param name="result">图像质量分析结果。</param>
        /// <returns>中文调试提示。</returns>
        private string BuildQualitySuggestion(CameraImageQualityResult result)
        {
            if (result.BrightnessLevel == BrightnessLevel.暗)
                return result.UnderexposedRatio > _qualityThresholds.UnderexposedWarningRatio
                    ? "画面偏暗且欠曝，请先增加曝光时间或增益。"
                    : "画面偏暗，请先增加曝光时间或增益。";

            if (result.BrightnessLevel == BrightnessLevel.亮)
                return result.OverexposedRatio > _qualityThresholds.OverexposedWarningRatio
                    ? "画面偏亮且过曝，请先降低曝光时间或增益。"
                    : "画面偏亮，请先降低曝光时间或增益。";

            if (result.SharpnessLevel == SharpnessLevel.模糊)
                return "亮度正常，请调整焦距提升清晰度。";

            return "亮度和清晰度正常。";
        }

        /// <summary>
        /// 获取亮度等级显示颜色。
        /// </summary>
        /// <param name="brightnessLevel">亮度等级。</param>
        /// <returns>显示颜色。</returns>
        private static Color GetBrightnessColor(BrightnessLevel brightnessLevel)
        {
            switch (brightnessLevel)
            {
                case BrightnessLevel.正常:
                    return Color.FromArgb(80, 220, 140);
                case BrightnessLevel.暗:
                    return Color.FromArgb(255, 180, 80);
                default:
                    return Color.FromArgb(255, 120, 120);
            }
        }

        /// <summary>
        /// 在 UI 线程执行操作。
        /// </summary>
        /// <param name="action">需要执行的操作。</param>
        private bool BeginInvokeSafe(Action action)
        {
            if (action == null || IsDisposed || !IsHandleCreated)
                return false;

            try
            {
                if (InvokeRequired)
                    BeginInvoke(new MethodInvoker(() =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.AddLog(MsgLevel.Warn, $"相机预览界面刷新失败：{ex.Message}", true);
                        }
                    }));
                else
                    action();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"投递相机预览界面刷新失败：{ex.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// 设置曝光时间。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void NumericExposure_ValueChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("曝光时间", () => _camera.SetExposureTime((double)numericExposure.Value));
        }

        /// <summary>
        /// 设置增益。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void NumericGain_ValueChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("增益", () => _camera.SetGain((double)numericGain.Value));
        }

        /// <summary>
        /// 将当前调试好的曝光和增益同步到流程中的对应相机图像源节点。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonSyncBrightnessToProcess_Click(object sender, EventArgs e)
        {
            if (_camera == null)
            {
                SetStatus("仅 2D 相机支持同步亮度到流程");
                return;
            }

            double exposure = (double)numericExposure.Value;
            double gain = (double)numericGain.Value;
            int updatedCount = SyncBrightnessToProcessCameras(exposure, gain);
            if (updatedCount > 0)
                SetStatus($"已同步亮度到 {updatedCount} 个流程相机节点");
            else
                SetStatus("未找到使用当前相机的流程图像源节点");
        }

        /// <summary>
        /// 遍历当前方案的流程节点，把曝光和增益写入匹配的图像源参数。
        /// </summary>
        /// <param name="exposure">当前曝光时间。</param>
        /// <param name="gain">当前增益。</param>
        /// <returns>成功更新的节点数量。</returns>
        private int SyncBrightnessToProcessCameras(double exposure, double gain)
        {
            int updatedCount = 0;
            List<Process> processes = Solution.Instance.AllProcesses ?? new List<Process>();
            foreach (Process process in processes)
            {
                if (process?.Nodes == null)
                    continue;

                foreach (NodeBase node in process.Nodes)
                {
                    if (TryUpdateImageSourceBrightness(node, exposure, gain))
                        updatedCount++;
                }
            }

            if (updatedCount > 0)
                Solution.Instance.IsModify = true;

            return updatedCount;
        }

        /// <summary>
        /// 尝试更新单个图像源节点的亮度参数。
        /// </summary>
        /// <param name="node">流程节点。</param>
        /// <param name="exposure">当前曝光时间。</param>
        /// <param name="gain">当前增益。</param>
        /// <returns>是否更新成功。</returns>
        private bool TryUpdateImageSourceBrightness(NodeBase node, double exposure, double gain)
        {
            if (!(node?.ParamForm?.Params is NodeParamImageSoucre param))
                return false;

            if (param.ImageSource != "相机" || !IsSame2DCamera(param))
                return false;

            param.ExposureTime = exposure;
            param.Gain = gain;
            return true;
        }

        /// <summary>
        /// 判断流程图像源参数是否指向当前调试的 2D 相机。
        /// </summary>
        /// <param name="param">图像源参数。</param>
        /// <returns>是否为同一台 2D 相机。</returns>
        private bool IsSame2DCamera(NodeParamImageSoucre param)
        {
            if (param == null || _camera == null)
                return false;

            if (ReferenceEquals(param.Camera, _camera))
                return true;

            if (param.Camera != null && IsSameText(param.Camera.SN, _camera.SN))
                return true;

            return IsSameText(param.CameraName, _camera.UserDefinedName)
                || IsSameText(param.CameraName, _camera.DevName)
                || IsSameText(param.CameraName, GetDeviceDisplayName());
        }

        /// <summary>
        /// 设置触发延迟。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void NumericTriggerDelay_ValueChanged(object sender, EventArgs e)
        {
            if (_camera3D != null)
            {
                _camera3D.GrabConfig.TriggerDelayUs = Decimal.ToInt32(numericTriggerDelay.Value);
                SetStatus("3D 触发延迟已更新");
                return;
            }

            Execute2DCameraAction("触发延迟", () => _camera.SetTriggerDelay((double)numericTriggerDelay.Value));
        }

        /// <summary>
        /// 设置触发模式。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ComboTriggerMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboTriggerMode.Text == "关闭")
                comboTriggerSource.SelectedItem = "连续采集";
            else if (comboTriggerSource.Text == "连续采集")
                SelectFirstAvailableTriggerSource();

            if (_camera3D != null)
            {
                _camera3D.GrabConfig.TriggerMode = comboTriggerMode.Text == "打开" ? Camera3DTriggerMode.On : Camera3DTriggerMode.Off;
                SetStatus("3D 触发模式已更新");
                ApplyTriggerParameterVisibility();
                return;
            }

            Execute2DCameraAction("触发模式", () => _camera.SetTriggerMode(comboTriggerMode.Text == "打开" ? TriggerModel.On : TriggerModel.Off));
            ApplyTriggerParameterVisibility();
        }

        /// <summary>
        /// 设置触发源。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ComboTriggerSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_isInitializingParameters && comboTriggerSource.Text == "连续采集" && comboTriggerMode.Text != "关闭")
            {
                comboTriggerMode.SelectedItem = "关闭";
                ApplyTriggerParameterVisibility();
                return;
            }

            if (_camera3D != null)
            {
                _camera3D.GrabConfig.TriggerSource = Get3DTriggerSource(comboTriggerSource.Text);
                SetStatus("3D 触发源已更新");
                ApplyTriggerParameterVisibility();
                return;
            }

            Execute2DCameraAction("触发源", () => _camera.SetTriggerSource(Get2DTriggerSource(comboTriggerSource.Text)));
            ApplyTriggerParameterVisibility();
        }

        /// <summary>
        /// 设置触发极性。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ComboTriggerEdge_SelectedIndexChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("触发极性", () => _camera.SetTriggerEdge(Get2DTriggerEdge(comboTriggerEdge.Text)));
        }

        /// <summary>
        /// 设置线路选择器。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ComboLineSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("线路选择", () => _camera.SetLineSelector(GetLineName(comboLineSelector.Text)));
            RefreshLineModeItemsForSelectedLine();
            ApplyIoOutputParameterVisibility();
        }

        /// <summary>
        /// 设置线路模式。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ComboLineMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("线路模式", () => _camera.SetLineMode(comboLineMode.Text));
            ApplyIoOutputParameterVisibility();
        }

        /// <summary>
        /// 设置频闪使能。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void CheckStrobeEnable_CheckedChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("频闪使能", () => _camera.SetStrobeEnable(checkStrobeEnable.Checked));
        }

        /// <summary>
        /// 设置线路反转。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void CheckLineInverter_CheckedChanged(object sender, EventArgs e)
        {
            Execute2DCameraAction("线路反转", () => _camera.SetLineInverter(checkLineInverter.Checked));
        }

        /// <summary>
        /// 进入 ROI 编辑模式。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonEditRoi_Click(object sender, EventArgs e)
        {
            if (pictureBoxPreview.Image == null)
            {
                SetStatus("请先开始采集后再编辑 ROI");
                return;
            }

            EnsureQualityRoiForImageSize(pictureBoxPreview.Image.Size);
            _isEditingQualityRoi = true;
            _isDraggingQualityRoi = false;
            buttonEditRoi.Enabled = false;
            buttonCompleteRoi.Enabled = true;
            pictureBoxPreview.Cursor = Cursors.SizeAll;
            pictureBoxPreview.Invalidate();
            SetStatus("ROI 编辑中，可拖动或缩放蓝色区域");
        }

        /// <summary>
        /// 完成 ROI 编辑。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonCompleteRoi_Click(object sender, EventArgs e)
        {
            _isEditingQualityRoi = false;
            _isDraggingQualityRoi = false;
            _qualityRoiHitArea = RoiHitArea.无;
            buttonEditRoi.Enabled = true;
            buttonCompleteRoi.Enabled = false;
            pictureBoxPreview.Capture = false;
            pictureBoxPreview.Cursor = Cursors.Default;
            pictureBoxPreview.Invalidate();
            SetStatus("ROI 编辑完成，成像判定将使用当前 ROI");
        }

        /// <summary>
        /// 手动执行一次软件触发。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonSoftTrigger_Click(object sender, EventArgs e)
        {
            if (!_isCollecting)
            {
                SetStatus("请先开始采集，再执行软触发");
                return;
            }

            if (!IsSoftwareTriggerSource())
            {
                SetStatus("当前触发源不是软件触发");
                return;
            }

            try
            {
                _lastPreviewFrameTime = DateTime.MinValue;
                if (_camera3D != null)
                {
                    _camera3D.GrabOne();
                }
                else
                {
                    if (_camera == null || !_camera.IsOpen || !_camera.GetGrabStatus())
                    {
                        SetStatus("相机未开始采集");
                        return;
                    }
                    _camera.GrabOne();
                }
                SetStatus("已执行软触发，等待图像");
            }
            catch (Exception ex)
            {
                SetStatus($"软触发失败：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Warn, $"相机调试软触发失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 恢复 ROI 输入为最大画幅提示值。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonRestoreRoi_Click(object sender, EventArgs e)
        {
            System.Drawing.Size imageSize = GetCurrentPreviewImageSize();
            Rectangle roi = CreateDefaultQualityRoi(imageSize);
            if (roi.IsEmpty)
            {
                SetStatus("请先开始采集后再恢复 ROI");
                return;
            }

            lock (_qualityAnalysisRoiSync)
            {
                _qualityAnalysisImageSize = imageSize;
                _qualityAnalysisRoi = roi;
            }

            SaveQualityRoiSnapshot(roi);
            SetRoiInputsFromRectangle(roi);
            pictureBoxPreview.Invalidate();
            SetStatus("ROI 已恢复为默认分析区域");
        }

        /// <summary>
        /// 执行 2D 相机参数操作。
        /// </summary>
        /// <param name="name">参数名称。</param>
        /// <param name="action">写入动作。</param>
        private void Execute2DCameraAction(string name, Action action)
        {
            if (_isInitializingParameters || _camera == null || action == null)
                return;

            if (!_camera.IsOpen)
            {
                SetStatus("请先开始采集或打开相机");
                return;
            }

            try
            {
                action();
                SetStatus($"{name}已更新");
            }
            catch (Exception ex)
            {
                SetStatus($"{name}更新失败：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Warn, $"{name}更新失败：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 设置状态文本。
        /// </summary>
        /// <param name="text">状态文本。</param>
        private void SetStatus(string text)
        {
            labelStatusValue.Text = text;
        }

        /// <summary>
        /// 设置下拉框选项并尽量保留当前选择。
        /// </summary>
        /// <param name="comboBox">目标下拉框。</param>
        /// <param name="items">候选项。</param>
        /// <param name="preferredText">优先选择文本。</param>
        private static void SetComboBoxItems(ComboBox comboBox, IEnumerable<string> items, string preferredText)
        {
            if (comboBox == null)
                return;

            List<string> displayItems = (items ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .ToList();
            if (displayItems.Count == 0)
                return;

            string currentText = string.IsNullOrWhiteSpace(preferredText) ? comboBox.Text : preferredText;
            comboBox.Items.Clear();
            foreach (string item in displayItems)
            {
                comboBox.Items.Add(item);
            }

            if (!string.IsNullOrWhiteSpace(currentText) && displayItems.Contains(currentText))
                comboBox.SelectedItem = currentText;
            else
                comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 从 SDK 枚举值提取中文显示项。
        /// </summary>
        /// <param name="enumValue">SDK 枚举值。</param>
        /// <param name="mapper">枚举项中文转换器。</param>
        /// <param name="fallbackItems">读取失败时的兜底项。</param>
        /// <returns>中文显示项。</returns>
        private static IEnumerable<string> GetEnumDisplayItems(IEnumValue enumValue, Func<IEnumEntry, string> mapper, IEnumerable<string> fallbackItems)
        {
            List<string> displayItems = new List<string>();
            IEnumEntry[] entries = enumValue?.SupportEnumEntries;
            if (entries != null && mapper != null)
            {
                uint supportedCount = enumValue.SupportedNum;
                foreach (IEnumEntry entry in entries.Take((int)Math.Min(supportedCount, (uint)entries.Length)))
                {
                    string displayText = mapper(entry);
                    if (!string.IsNullOrWhiteSpace(displayText))
                        displayItems.Add(displayText);
                }
            }

            if (displayItems.Count == 0 && fallbackItems != null)
                displayItems.AddRange(fallbackItems);

            return displayItems.Distinct();
        }

        /// <summary>
        /// 获取 SDK 枚举当前项的中文显示文本。
        /// </summary>
        /// <param name="enumValue">SDK 枚举值。</param>
        /// <param name="mapper">枚举项中文转换器。</param>
        /// <param name="fallbackText">读取失败时的兜底文本。</param>
        /// <returns>当前项中文显示文本。</returns>
        private static string GetCurrentEnumDisplayText(IEnumValue enumValue, Func<IEnumEntry, string> mapper, string fallbackText)
        {
            string displayText = mapper?.Invoke(enumValue?.CurEnumEntry);
            return string.IsNullOrWhiteSpace(displayText) ? fallbackText : displayText;
        }

        /// <summary>
        /// 选择第一个真实触发源，避免触发打开后仍停留在连续采集。
        /// </summary>
        private void SelectFirstAvailableTriggerSource()
        {
            foreach (object item in comboTriggerSource.Items)
            {
                string text = item?.ToString();
                if (!string.IsNullOrWhiteSpace(text) && text != "连续采集")
                {
                    comboTriggerSource.SelectedItem = item;
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前设备显示名。
        /// </summary>
        /// <returns>设备显示名。</returns>
        private string GetDeviceDisplayName()
        {
            string name = _camera != null ? _camera.UserDefinedName : _camera3D.UserDefinedName;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            name = _camera != null ? _camera.DevName : _camera3D.DevName;
            return GetSafeText(name, "未命名相机");
        }

        /// <summary>
        /// 获取 2D 相机 IP 地址。
        /// </summary>
        /// <returns>IP 地址文本。</returns>
        private string GetCameraIp()
        {
            CameraHik hikCamera = _camera as CameraHik;
            return GetSafeText(hikCamera?.IP, "-");
        }

        /// <summary>
        /// 获取安全显示文本。
        /// </summary>
        /// <param name="value">原始文本。</param>
        /// <param name="defaultText">默认文本。</param>
        /// <returns>安全文本。</returns>
        private static string GetSafeText(string value, string defaultText)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultText : value;
        }

        /// <summary>
        /// 比较两个相机标识文本是否一致，忽略首尾空格和大小写差异。
        /// </summary>
        /// <param name="left">左侧文本。</param>
        /// <param name="right">右侧文本。</param>
        /// <returns>是否一致。</returns>
        private static bool IsSameText(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从 SDK 值对象读取数值。
        /// </summary>
        /// <param name="sdkValue">SDK 参数对象。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <returns>读取到的数值。</returns>
        private static double GetSdkNumericValue(object sdkValue, double defaultValue)
        {
            if (sdkValue == null)
                return defaultValue;

            PropertyInfo property = sdkValue.GetType().GetProperty("CurValue");
            if (property == null)
                return defaultValue;

            object value = property.GetValue(sdkValue, null);
            if (value == null)
                return defaultValue;

            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 设置数值控件值并限制在控件范围内。
        /// </summary>
        /// <param name="control">数值控件。</param>
        /// <param name="value">目标值。</param>
        private static void SetNumericValue(NumericUpDown control, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return;

            decimal decimalValue = (decimal)value;
            if (decimalValue < control.Minimum)
                decimalValue = control.Minimum;
            if (decimalValue > control.Maximum)
                decimalValue = control.Maximum;
            control.Value = decimalValue;
        }

        /// <summary>
        /// 获取触发源 SDK 枚举项中文显示文本。
        /// </summary>
        /// <param name="entry">SDK 枚举项。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetTriggerSourceDisplayText(IEnumEntry entry)
        {
            string symbolic = entry?.Symbolic ?? string.Empty;
            if (symbolic.Equals("Software", StringComparison.OrdinalIgnoreCase))
                return "软件触发";
            if (symbolic.StartsWith("Line", StringComparison.OrdinalIgnoreCase))
                return $"线路{symbolic.Substring(4)}";
            if (string.IsNullOrWhiteSpace(symbolic))
            {
                if (entry != null && entry.Value == 7)
                    return "软件触发";
                if (entry != null && entry.Value <= 3)
                    return $"线路{entry.Value}";
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取触发极性 SDK 枚举项中文显示文本。
        /// </summary>
        /// <param name="entry">SDK 枚举项。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetTriggerEdgeDisplayText(IEnumEntry entry)
        {
            string symbolic = entry?.Symbolic ?? string.Empty;
            switch (symbolic)
            {
                case "RisingEdge":
                    return "上升沿";
                case "FallingEdge":
                    return "下降沿";
                case "LevelHigh":
                    return "高电平";
                case "LevelLow":
                    return "低电平";
                case "AnyEdge":
                    return "任意沿";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 获取线路选择器 SDK 枚举项中文显示文本。
        /// </summary>
        /// <param name="entry">SDK 枚举项。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetLineSelectorDisplayText(IEnumEntry entry)
        {
            string symbolic = entry?.Symbolic ?? string.Empty;
            if (symbolic.StartsWith("Line", StringComparison.OrdinalIgnoreCase))
                return $"线路{symbolic.Substring(4)}";
            if (string.IsNullOrWhiteSpace(symbolic) && entry != null && entry.Value <= 4)
                return $"线路{entry.Value}";
            return string.Empty;
        }

        /// <summary>
        /// 获取线路模式 SDK 枚举项中文显示文本。
        /// </summary>
        /// <param name="entry">SDK 枚举项。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetLineModeDisplayText(IEnumEntry entry)
        {
            string symbolic = entry?.Symbolic ?? string.Empty;
            if (symbolic.Equals("Input", StringComparison.OrdinalIgnoreCase))
                return "输入";
            if (symbolic.Equals("Output", StringComparison.OrdinalIgnoreCase))
                return "输出";
            if (string.IsNullOrWhiteSpace(symbolic) && entry != null)
            {
                if (entry.Value == 0)
                    return "输入";
                if (entry.Value == 1 || entry.Value == 8)
                    return "输出";
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取 2D 触发源枚举。
        /// </summary>
        /// <param name="text">中文触发源。</param>
        /// <returns>2D 触发源。</returns>
        private static TriggerSource Get2DTriggerSource(string text)
        {
            switch (text)
            {
                case "软件触发":
                    return TriggerSource.SOFT;
                case "线路0":
                    return TriggerSource.LINE0;
                case "线路1":
                    return TriggerSource.LINE1;
                case "线路2":
                    return TriggerSource.LINE2;
                case "线路3":
                    return TriggerSource.LINE3;
                case "线路4":
                    return TriggerSource.LINE4;
                default:
                    return TriggerSource.Auto;
            }
        }

        /// <summary>
        /// 获取 3D 触发源枚举。
        /// </summary>
        /// <param name="text">中文触发源。</param>
        /// <returns>3D 触发源。</returns>
        private static Camera3DTriggerSource Get3DTriggerSource(string text)
        {
            switch (text)
            {
                case "软件触发":
                    return Camera3DTriggerSource.Soft;
                case "线路0":
                    return Camera3DTriggerSource.Line0;
                case "线路1":
                    return Camera3DTriggerSource.Line1;
                case "线路2":
                    return Camera3DTriggerSource.Line2;
                case "线路3":
                    return Camera3DTriggerSource.Line3;
                default:
                    return Camera3DTriggerSource.Auto;
            }
        }

        /// <summary>
        /// 获取 2D 触发极性枚举。
        /// </summary>
        /// <param name="text">中文触发极性。</param>
        /// <returns>2D 触发极性。</returns>
        private static TriggerEdge Get2DTriggerEdge(string text)
        {
            switch (text)
            {
                case "下降沿":
                    return TriggerEdge.Falling;
                case "高电平":
                    return TriggerEdge.Hight;
                case "低电平":
                    return TriggerEdge.Low;
                case "任意沿":
                    return TriggerEdge.Any;
                default:
                    return TriggerEdge.Rising;
            }
        }

        /// <summary>
        /// 获取触发源中文文本。
        /// </summary>
        /// <param name="source">触发源。</param>
        /// <returns>中文文本。</returns>
        private static string GetTriggerSourceText(TriggerSource source)
        {
            switch (source)
            {
                case TriggerSource.SOFT:
                    return "软件触发";
                case TriggerSource.LINE0:
                    return "线路0";
                case TriggerSource.LINE1:
                    return "线路1";
                case TriggerSource.LINE2:
                    return "线路2";
                case TriggerSource.LINE3:
                    return "线路3";
                case TriggerSource.LINE4:
                    return "线路4";
                default:
                    return "连续采集";
            }
        }

        /// <summary>
        /// 获取 SDK 线路名称。
        /// </summary>
        /// <param name="text">中文线路名称。</param>
        /// <returns>SDK 线路名称。</returns>
        private static string GetLineName(string text)
        {
            switch (text)
            {
                case "线路1":
                    return "Line1";
                case "线路2":
                    return "Line2";
                case "线路3":
                    return "Line3";
                case "线路4":
                    return "Line4";
                default:
                    return "Line0";
            }
        }

        /// <summary>
        /// 释放当前预览图像。
        /// </summary>
        private void DisposePreviewImage()
        {
            Bitmap oldImage = pictureBoxPreview?.Image as Bitmap;
            if (pictureBoxPreview != null)
                pictureBoxPreview.Image = null;
            oldImage?.Dispose();
        }

        /// <summary>
        /// 释放属性树分组字体。
        /// </summary>
        private void DisposeTreeGroupFont()
        {
            _treeGroupFont?.Dispose();
            _treeGroupFont = null;
        }
    }
}
