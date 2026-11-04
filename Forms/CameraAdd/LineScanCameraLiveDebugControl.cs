using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device.Camera.LineScan;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>线扫相机实时预览与常用参数设置控件。</summary>
    public partial class LineScanCameraLiveDebugControl : UserControl
    {
        /// <summary>当前线扫相机。</summary>
        private readonly ILineScanCamera _camera;
        /// <summary>当前控件是否启动了取流。</summary>
        private bool _startedGrabbing;
        /// <summary>当前相机实际支持的触发选择器。</summary>
        private readonly List<LineScanTriggerSelector> _supportedTriggerSelectors = new List<LineScanTriggerSelector>();
        /// <summary>当前相机实际支持的触发源。</summary>
        private readonly List<TriggerSource> _supportedTriggerSources = new List<TriggerSource>();
        /// <summary>当前触发源实际支持的触发极性。</summary>
        private readonly List<TriggerEdge> _supportedTriggerEdges = new List<TriggerEdge>();
        /// <summary>防止刷新选项时重复进入选择事件。</summary>
        private bool _updatingTriggerControls;

        /// <summary>创建线扫相机调试控件。</summary>
        public LineScanCameraLiveDebugControl(ILineScanCamera camera)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            InitializeComponent();
            comboTriggerMode.SelectedIndex = 0;
            labelCameraInfo.Text = string.Format("{0}  SN:{1}  像素格式:{2}", _camera.UserDefinedName, _camera.SerialNumber, _camera.CurrentPixelFormat);
            RefreshTriggerSelectors();
            RefreshTriggerControls();
        }

        /// <summary>尺寸变化时固定保留右侧参数区，防止预览区把参数区挤出界面。</summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (splitContainerMain == null || splitContainerMain.IsHandleCreated == false || Width < splitContainerMain.Panel1MinSize + splitContainerMain.Panel2MinSize + splitContainerMain.SplitterWidth)
                return;
            int settingsWidth = Math.Min(460, Math.Max(360, Width / 4));
            int maximumDistance = splitContainerMain.Width - splitContainerMain.Panel2MinSize - splitContainerMain.SplitterWidth;
            int requestedDistance = splitContainerMain.Width - settingsWidth - splitContainerMain.SplitterWidth;
            splitContainerMain.SplitterDistance = Math.Max(splitContainerMain.Panel1MinSize, Math.Min(maximumDistance, requestedDistance));
        }

        /// <summary>应用当前界面中的线扫参数。</summary>
        private async void buttonApply_Click(object sender, EventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (!_camera.IsOpen) _camera.Open();
                    if (_camera.IsGrabbing) _camera.StopGrabbing();
                    _camera.ApplyAcquisitionSettings(BuildSettings());
                });
                labelStatus.Text = "参数设置成功";
            }
            catch (Exception exception) { ShowError("参数设置失败", exception); }
        }

        /// <summary>开始或停止线扫实时预览。</summary>
        private async void buttonPreview_Click(object sender, EventArgs e)
        {
            try
            {
                if (_startedGrabbing)
                {
                    _camera.FrameReceived -= Camera_FrameReceived;
                    await Task.Run(() => _camera.StopGrabbing());
                    _startedGrabbing = false;
                    buttonPreview.Text = "开始预览";
                    labelStatus.Text = "预览已停止";
                    return;
                }
                LineScanCameraSettings settings = BuildSettings();
                await Task.Run(() =>
                {
                    if (!_camera.IsOpen) _camera.Open();
                    if (_camera.IsGrabbing) _camera.StopGrabbing();
                    _camera.ApplyAcquisitionSettings(settings);
                    _camera.FrameReceived += Camera_FrameReceived;
                    _camera.StartGrabbing();
                    if (settings.TriggerMode == TriggerModel.On && settings.TriggerSource == TriggerSource.SOFT)
                        _camera.ExecuteSoftwareTrigger();
                });
                _startedGrabbing = true;
                buttonPreview.Text = "停止预览";
                labelStatus.Text = "正在等待完整线扫帧";
            }
            catch (Exception exception)
            {
                _camera.FrameReceived -= Camera_FrameReceived;
                if (_camera.IsGrabbing)
                {
                    try { _camera.StopGrabbing(); }
                    catch (Exception stopException) { LogHelper.AddLog(MsgLevel.Warn, "预览失败后停止线扫取流失败：" + stopException.Message, true); }
                }
                _startedGrabbing = false;
                ShowError("预览启动失败", exception);
            }
        }

        /// <summary>将相机回调帧刷新到预览窗口。</summary>
        private void Camera_FrameReceived(Mat image)
        {
            try
            {
                Bitmap bitmap = BitmapConverter.ToBitmap(image);
                image.Dispose();
                if (!IsHandleCreated || IsDisposed) { bitmap.Dispose(); return; }
                BeginInvoke(new Action(() =>
                {
                    Image old = pictureBoxPreview.Image;
                    pictureBoxPreview.Image = bitmap;
                    old?.Dispose();
                    labelStatus.Text = string.Format("图像尺寸：{0}×{1}", bitmap.Width, bitmap.Height);
                }));
            }
            catch (Exception exception)
            {
                image?.Dispose();
                LogHelper.AddLog(MsgLevel.Exception, "线扫预览刷新失败：" + exception.Message, true);
            }
        }

        /// <summary>由界面值构建线扫参数。</summary>
        private LineScanCameraSettings BuildSettings()
        {
            if (comboTriggerMode.SelectedIndex == 1 && (comboTriggerSource.SelectedIndex < 0 || comboTriggerSource.SelectedIndex >= _supportedTriggerSources.Count))
                throw new InvalidOperationException("触发模式开启时必须先从已连接相机读取并选择触发源。");
            TriggerSource source = comboTriggerSource.SelectedIndex >= 0 && comboTriggerSource.SelectedIndex < _supportedTriggerSources.Count ? _supportedTriggerSources[comboTriggerSource.SelectedIndex] : TriggerSource.SOFT;
            if (comboTriggerMode.SelectedIndex == 1 && source != TriggerSource.SOFT && (comboTriggerEdge.SelectedIndex < 0 || comboTriggerEdge.SelectedIndex >= _supportedTriggerEdges.Count))
                throw new InvalidOperationException("当前线路触发源没有可选择的触发极性。");
            TriggerEdge edge = comboTriggerEdge.SelectedIndex >= 0 && comboTriggerEdge.SelectedIndex < _supportedTriggerEdges.Count ? _supportedTriggerEdges[comboTriggerEdge.SelectedIndex] : TriggerEdge.Rising;
            return new LineScanCameraSettings
            {
                Width = (long)numericWidth.Value,
                Height = (long)numericHeight.Value,
                ExposureTime = (double)numericExposure.Value,
                Gain = (double)numericGain.Value,
                TriggerSelector = GetTriggerSelector(),
                TriggerMode = comboTriggerMode.SelectedIndex == 1 ? TriggerModel.On : TriggerModel.Off,
                TriggerSource = source,
                TriggerEdge = edge
            };
        }

        /// <summary>触发模式改变时刷新相机实际支持的触发源。</summary>
        private void comboTriggerMode_SelectedIndexChanged(object sender, EventArgs e) => RefreshTriggerControls();

        /// <summary>触发选择器改变时重新读取其下属触发源和触发极性。</summary>
        private void comboTriggerSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_updatingTriggerControls)
                RefreshTriggerControls();
        }

        /// <summary>触发源改变时读取该触发源实际支持的触发极性。</summary>
        private void comboTriggerSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_updatingTriggerControls)
                RefreshTriggerEdges();
        }

        /// <summary>根据触发模式刷新触发选项；关闭触发时不读取相关节点。</summary>
        private void RefreshTriggerControls()
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                comboTriggerSource.Enabled = false;
                comboTriggerEdge.Enabled = false;
                comboTriggerSource.Items.Clear();
                comboTriggerEdge.Items.Clear();
                _supportedTriggerSources.Clear();
                _supportedTriggerEdges.Clear();
                if (comboTriggerMode.SelectedIndex != 1)
                {
                    labelStatus.Text = "触发模式关闭，不读取触发源和触发极性";
                    return;
                }
                if (!_camera.IsOpen || _camera.IsGrabbing)
                {
                    labelStatus.Text = _camera.IsGrabbing ? "相机正在取流，停止预览后可读取触发能力" : "请先连接相机，再读取触发能力";
                    return;
                }
                _supportedTriggerSources.AddRange(_camera.GetSupportedTriggerSources(GetTriggerSelector()));
                foreach (TriggerSource source in _supportedTriggerSources)
                    comboTriggerSource.Items.Add(GetTriggerSourceText(source));
                comboTriggerSource.SelectedIndex = _supportedTriggerSources.Count > 0 ? 0 : -1;
                comboTriggerSource.Enabled = _supportedTriggerSources.Count > 0;
            }
            catch (Exception exception) { ShowError("读取触发源失败", exception); }
            finally { _updatingTriggerControls = false; }
            RefreshTriggerEdges();
        }

        /// <summary>刷新当前触发源支持的触发极性，软件触发时保持禁用。</summary>
        private void RefreshTriggerEdges()
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                comboTriggerEdge.Enabled = false;
                comboTriggerEdge.Items.Clear();
                _supportedTriggerEdges.Clear();
                if (comboTriggerSource.SelectedIndex < 0 || comboTriggerSource.SelectedIndex >= _supportedTriggerSources.Count)
                    return;
                TriggerSource source = _supportedTriggerSources[comboTriggerSource.SelectedIndex];
                if (source == TriggerSource.SOFT)
                {
                    labelStatus.Text = "软件触发没有触发极性";
                    return;
                }
                _supportedTriggerEdges.AddRange(_camera.GetSupportedTriggerEdges(GetTriggerSelector(), source));
                foreach (TriggerEdge edge in _supportedTriggerEdges)
                    comboTriggerEdge.Items.Add(GetTriggerEdgeText(edge));
                comboTriggerEdge.SelectedIndex = _supportedTriggerEdges.Count > 0 ? 0 : -1;
                comboTriggerEdge.Enabled = _supportedTriggerEdges.Count > 0;
                labelStatus.Text = _supportedTriggerEdges.Count > 0 ? "已读取当前触发源支持的触发极性" : "当前触发源没有可用的触发极性";
            }
            catch (Exception exception) { ShowError("读取触发极性失败", exception); }
            finally { _updatingTriggerControls = false; }
        }

        /// <summary>取得触发源中文名称。</summary>
        private static string GetTriggerSourceText(TriggerSource source)
        {
            if (source == TriggerSource.SOFT) return "软件触发";
            if (source == TriggerSource.EncoderModuleOut) return "编码器模块输出";
            if (source == TriggerSource.FrequencyConverter) return "分频器输出";
            return source.ToString().Replace("LINE", "线路");
        }

        /// <summary>读取并显示相机支持的触发选择器。</summary>
        private void RefreshTriggerSelectors()
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                LineScanTriggerSelector current = GetTriggerSelector();
                _supportedTriggerSelectors.Clear();
                if (_camera.IsOpen && !_camera.IsGrabbing)
                    _supportedTriggerSelectors.AddRange(_camera.GetSupportedTriggerSelectors());
                if (_supportedTriggerSelectors.Count == 0)
                {
                    _supportedTriggerSelectors.Add(LineScanTriggerSelector.FrameBurstStart);
                    _supportedTriggerSelectors.Add(LineScanTriggerSelector.LineStart);
                }
                comboTriggerSelector.Items.Clear();
                foreach (LineScanTriggerSelector selector in _supportedTriggerSelectors)
                    comboTriggerSelector.Items.Add(GetTriggerSelectorText(selector));
                int index = _supportedTriggerSelectors.IndexOf(current);
                comboTriggerSelector.SelectedIndex = index >= 0 ? index : _supportedTriggerSelectors.IndexOf(LineScanTriggerSelector.LineStart);
                if (comboTriggerSelector.SelectedIndex < 0)
                    comboTriggerSelector.SelectedIndex = 0;
                comboTriggerSelector.Enabled = !_camera.IsGrabbing;
            }
            catch (Exception exception) { ShowError("读取触发选择器失败", exception); }
            finally { _updatingTriggerControls = false; }
        }

        /// <summary>获取当前触发选择器。</summary>
        private LineScanTriggerSelector GetTriggerSelector() => comboTriggerSelector.SelectedIndex >= 0 && comboTriggerSelector.SelectedIndex < _supportedTriggerSelectors.Count ? _supportedTriggerSelectors[comboTriggerSelector.SelectedIndex] : LineScanTriggerSelector.LineStart;

        /// <summary>取得触发选择器中文名称。</summary>
        private static string GetTriggerSelectorText(LineScanTriggerSelector selector) => selector == LineScanTriggerSelector.LineStart ? "行开始" : "帧触发开始";

        /// <summary>取得触发极性中文名称。</summary>
        private static string GetTriggerEdgeText(TriggerEdge edge)
        {
            switch (edge)
            {
                case TriggerEdge.Falling: return "下降沿";
                case TriggerEdge.Any: return "双边沿";
                case TriggerEdge.Hight: return "高电平";
                case TriggerEdge.Low: return "低电平";
                default: return "上升沿";
            }
        }

        /// <summary>显示并记录线扫调试错误。</summary>
        private void ShowError(string operation, Exception exception)
        {
            labelStatus.Text = operation + "：" + exception.Message;
            LogHelper.AddLog(MsgLevel.Exception, labelStatus.Text, true);
        }

        /// <summary>释放预览订阅和图像资源。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _camera.FrameReceived -= Camera_FrameReceived;
                if (_startedGrabbing) { try { _camera.StopGrabbing(); } catch { } }
                pictureBoxPreview.Image?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
