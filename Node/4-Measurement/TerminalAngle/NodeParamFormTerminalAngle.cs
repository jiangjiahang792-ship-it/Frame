using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._4_Measurement.TerminalAngle
{
    /// <summary>沿用卡尺输入与位置修正流程；区域在图像中编辑，结果仅叠加到图像。</summary>
    public partial class NodeParamFormTerminalAngle : FormBase, INodeParamForm
    {
        /// <summary>可替换的端子测量算法。</summary>
        private readonly ITerminalAngleMeasurer _measurer;
        /// <summary>卡尺共用的区域归属服务，自动关联当前位置的定位目标。</summary>
        private readonly IMultiTargetTransformService _transformService = new MultiTargetTransformService();
        /// <summary>独立于已保存参数的编辑副本。</summary>
        private NodeParamTerminalAngle _editing = new NodeParamTerminalAngle();
        /// <summary>参数窗口自有预览图。</summary>
        private Mat _preview;
        /// <summary>与预览图刷新时一起复制的定位列表，不在旧画面上使用新定位。</summary>
        private IReadOnlyList<PositionCorrectionInfo> _previewCorrections = new List<PositionCorrectionInfo>();
        /// <summary>当前编辑的是端子还是基座。</summary>
        private int _role;
        /// <summary>只在本帧主动绘制时允许回读动态矩形，历史区域只做静态显示。</summary>
        private bool _drawing;
        /// <summary>图像或绘制会话版本，阻止换图前排队的鼠标回调写入新帧。</summary>
        private int _drawingVersion;
        /// <summary>测量预览取消源。</summary>
        private CancellationTokenSource _cancellation;
        /// <summary>异步预览期间的操作锁。</summary>
        private bool _busy;
        /// <summary>输入框回填保护。</summary>
        private bool _syncingEditors;
        /// <summary>未提交的算法参数输入。</summary>
        private bool _editorsDirty;
        /// <summary>当前有效测量叠加，保存时保留图像上的结果。</summary>
        private AlgorithmResult _lastDisplay;
        /// <summary>框架持久化的已保存参数。</summary>
        public INodeParam Params { get; set; }
        /// <summary>设计器无设备构造入口。</summary>
        public NodeParamFormTerminalAngle() : this(null, null, new OpenCvTerminalAngleMeasurer()) { }
        /// <summary>初始化原生分组界面与算法服务。</summary>
        public NodeParamFormTerminalAngle(Process process, NodeBase node, ITerminalAngleMeasurer measurer)
        {
            _measurer = measurer ?? throw new ArgumentNullException("measurer");
            InitializeComponent();
            SyncEditors();
        }
        /// <summary>与卡尺一致，图像订阅和位置修正列表订阅均限制为明确类型。</summary>
        public void SetNodeBelong(NodeBase node)
        {
            imageSubscription.SetExpectedValueType<OutputImage>();
            correctionSubscription.SetExpectedValueType<List<PositionCorrectionInfo>>();
            if (node != null) { imageSubscription.Init(node); correctionSubscription.Init(node); }
        }
        /// <summary>恢复方案时丢弃旧编辑会话，等待刷新当前图像与定位。</summary>
        public void SetParam2Form()
        {
            CancelDrawing();
            _editing = (Params as NodeParamTerminalAngle)?.Copy() ?? new NodeParamTerminalAngle();
            _syncingEditors = true;
            try { imageSubscription.SetText(_editing.Text1 ?? "", _editing.Text2 ?? ""); }
            finally { _syncingEditors = false; }
            SyncEditors();
            _previewCorrections = new List<PositionCorrectionInfo>();
            ShowStatus("请刷新图像后绘制或执行。");
        }
        /// <summary>生产运行读取保存的图像订阅，不接受未保存的订阅修改。</summary>
        public OutputImage GetInputImage()
        {
            var saved = Params as NodeParamTerminalAngle;
            if (saved == null || saved.Text1 != imageSubscription.GetText1() || saved.Text2 != imageSubscription.GetText2())
                throw new InvalidOperationException("图像订阅已更改，请先确定保存节点参数。");
            return imageSubscription.GetValue<OutputImage>() ?? throw new InvalidOperationException("订阅图像为空。");
        }
        /// <summary>仿照卡尺读取完整修正列表，关闭修正时生成恒等项，不再提供手选目标。</summary>
        public IReadOnlyList<PositionCorrectionInfo> GetCorrections(NodeParamTerminalAngle settings, bool requireSaved)
        {
            if (requireSaved && (settings.UsePositionCorrection != correctionCheckBox.Checked ||
                (settings.UsePositionCorrection && (settings.CorrectionText1 != correctionSubscription.GetText1() ||
                settings.CorrectionText2 != correctionSubscription.GetText2()))))
                throw new InvalidOperationException("位置修正订阅已更改，请先确定保存节点参数。");
            if (!settings.UsePositionCorrection)
                return new List<PositionCorrectionInfo> { new PositionCorrectionInfo { IsValid = true, TargetIndex = 1 } };
            if (string.IsNullOrWhiteSpace(settings.CorrectionText1) || string.IsNullOrWhiteSpace(settings.CorrectionText2))
                throw new InvalidOperationException("请选择位置修正信息列表订阅。");
            var corrections = correctionSubscription.GetValue<List<PositionCorrectionInfo>>();
            // 快照仅复制轻量位姿；保留无效项和列表顺序，由共用执行器生成对应失败项。
            return corrections == null ? new List<PositionCorrectionInfo>() : corrections.Select(CopyCorrection).ToList();
        }
        /// <summary>复制上游位姿，避免刷新后定位结果改变而旧预览仍引用新数值。</summary>
        private static PositionCorrectionInfo CopyCorrection(PositionCorrectionInfo value)
        {
            if (value == null) return null;
            return new PositionCorrectionInfo
            {
                IsValid = value.IsValid, TargetIndex = value.TargetIndex,
                BaseX = value.BaseX, BaseY = value.BaseY, BaseAngle = value.BaseAngle,
                BaseScaleX = value.BaseScaleX, BaseScaleY = value.BaseScaleY,
                CurrentX = value.CurrentX, CurrentY = value.CurrentY, CurrentAngle = value.CurrentAngle,
                CurrentScaleX = value.CurrentScaleX, CurrentScaleY = value.CurrentScaleY,
                TargetWidth = value.TargetWidth, TargetHeight = value.TargetHeight, Score = value.Score
            };
        }
        /// <summary>切换订阅后取消旧画面绘制，必须刷新以使定位和图像重新成对。</summary>
        private void CorrectionSelectionChanged(object sender, EventArgs e)
        {
            if (_syncingEditors) return;
            CancelDrawing();
            _editing.UsePositionCorrection = correctionCheckBox.Checked;
            _editing.CorrectionText1 = correctionSubscription.GetText1();
            _editing.CorrectionText2 = correctionSubscription.GetText2();
            correctionSubscription.Enabled = _editing.UsePositionCorrection;
            _previewCorrections = new List<PositionCorrectionInfo>();
            ShowStatus("位置修正已更改，请刷新图像。");
        }
        /// <summary>图像订阅变化后禁止旧帧上的编辑，防止保存到另一坐标系。</summary>
        private void ImageSubscriptionChanged(object sender, EventArgs e)
        {
            if (_syncingEditors) return;
            CancelDrawing();
            _preview?.Dispose(); _preview = null;
            _previewCorrections = new List<PositionCorrectionInfo>();
            viewer.SetImage(null);
            ShowStatus("图像订阅已更改，请刷新图像。");
        }
        /// <summary>直接读取上游已有图像，不重跑流程或等待相机触发。</summary>
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var input = imageSubscription.GetValue<OutputImage>() ?? throw new InvalidOperationException("未取得上游图像。");
                using (input.AcquireLease()) LoadPreview(MeasurementNodeHelper.GetReadOnlyPreviewMat(input), false);
            }
            catch (Exception exception) { ShowFailure(new InvalidOperationException("刷新失败：" + exception.Message + " 请先运行上游图像节点。")); }
            finally { if (!IsDisposed) SetBusy(false); }
        }
        /// <summary>换图时结束旧绘制，再取得定位快照；旧帧回调不得修改新图区域。</summary>
        public void LoadPreview(Mat image, bool localPreview)
        {
            if (image == null || image.Empty()) throw new ArgumentException("预览图像为空。");
            var copy = image.Clone();
            CancelDrawing();
            var previous = _preview; _preview = copy; previous?.Dispose();
            viewer.SetImage(BitmapConverter.ToBitmap(copy)); viewer.ShowFit();
            _previewCorrections = new List<PositionCorrectionInfo>();
            try { _previewCorrections = GetCorrections(_editing, false); }
            catch (Exception exception) { ShowFailure(exception); return; }
            ShowStatus(localPreview ? "已加载测试图，请绘制区域。生产仍读取上游订阅。" : "已刷新图像，请绘制区域或执行。");
        }
        /// <summary>开始绘制端子区域。</summary>
        private void TerminalButton_Click(object sender, EventArgs e) { BeginDraw(0); }
        /// <summary>开始绘制基座区域。</summary>
        private void BaseButton_Click(object sender, EventArgs e) { BeginDraw(1); }
        /// <summary>新框始终按当前画面水平/垂直绘制，尚未画完时不破坏已有区域。</summary>
        private void BeginDraw(int role)
        {
            try
            {
                if (_preview == null) throw new InvalidOperationException("请先刷新图像。");
                ApplyEditorChanges(); CaptureRoi(); CancelDrawing(); _role = role;
                if (_editing.UsePositionCorrection && _previewCorrections.Count == 0)
                    throw new InvalidOperationException("请先运行定位并刷新图像。");
                _drawing = true;
                ShowStatus(role == 0 ? "拖动绘制端子区域，框保持水平/垂直。" : "拖动绘制基座区域。");
                viewer.EnableRectangleRoiDrawing = true;
                viewer.RoiColor = role == 0 ? Color.IndianRed : Color.Teal;
            }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>延后读取鼠标绘制，但只允许本次图像与绘制会话的回调生效。</summary>
        private void Viewer_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !IsHandleCreated || !_drawing) return;
            int version = _drawingVersion;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || version != _drawingVersion || !_drawing) return;
                try
                {
                    CaptureRoi();
                    if (viewer.GetAllDynamicRects().Count > 0) viewer.EnableRectangleRoiDrawing = false;
                }
                catch (Exception exception) { ShowFailure(exception); }
            }));
        }
        /// <summary>以当前帧矩形和自动关联的绘制位姿保存区域，不把定位角度写回新画矩形。</summary>
        private void CaptureRoi()
        {
            if (!_drawing) return;
            var roi = viewer.GetAllDynamicRects().LastOrDefault();
            if (roi == null) return;
            roi.Phi = 0; // 用户画出的水平/垂直框保持原样，定位旋转仅在跨帧测量时应用。
            TerminalAnglePoseSnapshot drawingPose = null;
            if (_editing.UsePositionCorrection)
            {
                int index = _transformService.ResolveAnchorIndex(new PointF(roi.CX, roi.CY), _previewCorrections);
                drawingPose = new TerminalAngleTransform(_previewCorrections[index]).Snapshot();
            }
            var region = new TerminalAngleRoi
            {
                Left = roi.CX - roi.W / 2, Right = roi.CX + roi.W / 2,
                Top = roi.CY - roi.H / 2, Bottom = roi.CY + roi.H / 2, DrawingPose = drawingPose
            };
            region.ToRect(_preview.Width, _preview.Height);
            var previous = _role == 0 ? _editing.TerminalRoi : _editing.BaseRoi;
            if (previous != null && Math.Abs(previous.Left - region.Left) < 0.001 && Math.Abs(previous.Right - region.Right) < 0.001 &&
                Math.Abs(previous.Top - region.Top) < 0.001 && Math.Abs(previous.Bottom - region.Bottom) < 0.001 && SamePose(previous.DrawingPose, drawingPose)) return;
            if (_role == 0) _editing.TerminalRoi = region; else _editing.BaseRoi = region;
            ShowStatus("区域已更新，请执行并确定保存。");
            viewer.Invalidate();
        }
        /// <summary>判断绘制帧位姿是否相同，未编辑的区域不反复清除测量结果。</summary>
        private static bool SamePose(TerminalAnglePoseSnapshot left, TerminalAnglePoseSnapshot right)
        {
            if (left == null || right == null) return left == right;
            return left.A == right.A && left.B == right.B && left.C == right.C && left.D == right.D && left.X == right.X && left.Y == right.Y;
        }
        /// <summary>终止旧帧绘制；保留模型中的区域，不保留可回读的旧动态矩形。</summary>
        private void CancelDrawing()
        {
            _drawingVersion++; _drawing = false;
            viewer.EnableRectangleRoiDrawing = false; viewer.ClearDynamicRoi(); _lastDisplay = null;
        }
        /// <summary>把区域与提示统一叠加到图像，无额外的结果面板。</summary>
        private void ShowStatus(string message, bool failure = false)
        {
            _lastDisplay = null;
            var display = new AlgorithmResult();
            var visibleSettings = _editing;
            if (_drawing && viewer.GetAllDynamicRects().Count == 0)
            {
                // 重画期间隐藏旧的同角色轮廓，避免把旧定位框误认为新绘制框。
                visibleSettings = _editing.Copy();
                if (_role == 0) visibleSettings.TerminalRoi = null; else visibleSettings.BaseRoi = null;
            }
            foreach (var correction in _previewCorrections)
            {
                try
                {
                    var overlay = TerminalAngleDisplay.BuildEditing(visibleSettings, new TerminalAngleTransform(correction));
                    display.Lines.AddRange(overlay.Lines);
                }
                catch (Exception) { /* 无效定位不绘制误导框，执行时保留对应失败项。 */ }
            }
            display.Texts.Add(new ColorText(message, failure ? Color.Red : Color.Gold));
            viewer.SetDisplayResult(display);
        }
        /// <summary>记录算法参数更改，清除旧测量叠加。</summary>
        private void ParameterEditor_TextChanged(object sender, EventArgs e)
        {
            if (_syncingEditors) return;
            _editorsDirty = true;
            ShowStatus("参数已改变，请重新执行并确定保存。");
        }
        /// <summary>输入结束时验证算法参数。</summary>
        private void ParameterEditor_Validated(object sender, EventArgs e)
        {
            if (_syncingEditors) return;
            try { ApplyEditorChanges(); }
            catch (Exception exception) { ShowFailure(exception); }
        }
        /// <summary>只回填保留在界面中的算法参数与修正开关。</summary>
        private void SyncEditors()
        {
            _syncingEditors = true;
            try
            {
                correctionCheckBox.Checked = _editing.UsePositionCorrection;
                correctionSubscription.SetText(_editing.CorrectionText1 ?? "", _editing.CorrectionText2 ?? "");
                correctionSubscription.Enabled = _editing.UsePositionCorrection;
                SetEditor(segmentationThresholdTextBox, _editing.Threshold);
                SetEditor(segmentationGaussianTextBox, _editing.GaussianSize);
                SetEditor(qualityMinimumWidthTextBox, _editing.MinimumWidth);
                SetEditor(qualityMaximumWidthTextBox, _editing.MaximumWidth);
                SetEditor(qualityCoverageTextBox, _editing.MinimumHeightCoverage);
                blueChannelCheckBox.Checked = _editing.UseBlueChannel;
                _editorsDirty = false;
            }
            finally { _syncingEditors = false; }
        }
        /// <summary>保留回填文本，避免未编辑参数被格式化舍入。</summary>
        private static void SetEditor(TextBox editor, double value)
        {
            editor.Text = value.ToString("0.###", CultureInfo.CurrentCulture); editor.Tag = editor.Text;
        }
        /// <summary>只解析实际编辑的有限数值。</summary>
        private static double ReadEditor(TextBox editor, double original, string caption)
        {
            if (editor.Text == (string)editor.Tag) return original;
            double value;
            if (!double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || !TerminalAngleRoi.Finite(value))
                throw new ArgumentException(caption + "请输入有效数字。");
            return value;
        }
        /// <summary>校验完整算法输入后更新快照，区域几何由鼠标编辑维护。</summary>
        private void ApplyEditorChanges()
        {
            if (!_editorsDirty) return;
            var settings = _editing.Copy();
            settings.Threshold = ReadEditor(segmentationThresholdTextBox, settings.Threshold, "分割阈值");
            double gaussian = ReadEditor(segmentationGaussianTextBox, settings.GaussianSize, "高斯尺寸");
            settings.MinimumWidth = ReadEditor(qualityMinimumWidthTextBox, settings.MinimumWidth, "最小宽度");
            settings.MaximumWidth = ReadEditor(qualityMaximumWidthTextBox, settings.MaximumWidth, "最大宽度");
            settings.MinimumHeightCoverage = ReadEditor(qualityCoverageTextBox, settings.MinimumHeightCoverage, "高度覆盖率");
            if (settings.Threshold < 0 || settings.Threshold > 255) throw new ArgumentException("分割阈值应在 0～255 之间。");
            if (gaussian < 3 || gaussian > 11 || gaussian % 2 != 1) throw new ArgumentException("高斯尺寸应为 3～11 之间的奇数。");
            if (settings.MinimumWidth < 1 || settings.MaximumWidth <= settings.MinimumWidth) throw new ArgumentException("最小宽度应不小于 1，最大宽度应大于最小宽度。");
            if (settings.MinimumHeightCoverage < 0.5 || settings.MinimumHeightCoverage > 1) throw new ArgumentException("高度覆盖率应在 0.5～1 之间。");
            settings.GaussianSize = (int)gaussian; settings.UseBlueChannel = blueChannelCheckBox.Checked;
            _editing = settings; SyncEditors();
        }
        /// <summary>执行与生产一致的全部定位目标，并直接显示在图像上。</summary>
        private async void TestButton_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            try
            {
                ApplyEditorChanges(); CaptureRoi();
                if (_preview == null) throw new InvalidOperationException("请先刷新图像。");
                _editing.Validate(int.MaxValue, int.MaxValue);
                var settings = _editing.Copy(); var corrections = _previewCorrections;
                // 结果只显示中线；清除编辑框、ROI 名称和控制点，并使延迟鼠标回调失效。
                CancelDrawing();
                _cancellation = new CancellationTokenSource(); SetBusy(true);
                var token = _cancellation.Token;
                using (var image = _preview.Clone())
                {
                    var items = await Task.Run(() => TerminalAnglePositionCorrection.MeasureAll(_measurer, image, settings, corrections, token), token);
                    if (IsDisposed) return;
                    _lastDisplay = TerminalAngleDisplay.BuildAll(items); viewer.SetDisplayResult(_lastDisplay);
                }
            }
            catch (OperationCanceledException) { if (!IsDisposed) ShowStatus("预览已取消。"); }
            catch (Exception exception) { ShowFailure(exception); }
            finally { _cancellation?.Dispose(); _cancellation = null; if (!IsDisposed) SetBusy(false); }
        }
        /// <summary>确定始终关闭窗口；尽量保留有效编辑，关闭不依赖图像、区域或定位是否就绪。</summary>
        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                ApplyEditorChanges();
                // 已执行且没有再编辑时保留测量叠加，不重复捕获同一矩形。
                CaptureRoi();
                _editing.Text1 = imageSubscription.GetText1(); _editing.Text2 = imageSubscription.GetText2();
                Params = _editing.Copy();
            }
            catch (Exception exception) { ShowFailure(exception); }
            finally
            {
                // 错误只影响编辑内容是否提交，不能阻止用户关闭；保留实例以支持再次打开。
                _cancellation?.Cancel();
                var measuredDisplay = _lastDisplay;
                CancelDrawing();
                if (measuredDisplay != null) { _lastDisplay = measuredDisplay; viewer.SetDisplayResult(measuredDisplay); }
                Hide();
            }
        }
        /// <summary>完成标记最后发布，确保再次操作时控件已经恢复。</summary>
        private void SetBusy(bool busy)
        {
            _busy = true;
            foreach (Control control in toolbar.Controls) control.Enabled = control == saveButton || !busy;
            sidebar.Enabled = !busy; viewer.Enabled = !busy; _busy = busy;
        }
        /// <summary>异常直接显示到图像，避免隐蔽状态或重复弹窗。</summary>
        private void ShowFailure(Exception exception) { if (!IsDisposed) ShowStatus(exception.Message, true); }
        /// <summary>释放自有预览并使旧回调失效。</summary>
        private void ReleasePreview()
        {
            _drawingVersion++; _cancellation?.Cancel(); _preview?.Dispose(); _preview = null;
        }
    }
}
