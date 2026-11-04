using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;
using TDJS_Vision.Device.Camera.LineScan;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.LineScanImageSource
{
    /// <summary>线扫图像源参数窗体。</summary>
    public partial class ParamFormLineScanImageSource : FormBase, INodeParamForm
    {
        /// <summary>当前相机支持的触发选择器。</summary>
        private readonly List<LineScanTriggerSelector> _supportedTriggerSelectors = new List<LineScanTriggerSelector>();
        /// <summary>当前相机实际支持的触发源，顺序与界面选项一致。</summary>
        private readonly List<TriggerSource> _supportedTriggerSources = new List<TriggerSource>();
        /// <summary>当前触发源实际支持的触发极性，顺序与界面选项一致。</summary>
        private readonly List<TriggerEdge> _supportedTriggerEdges = new List<TriggerEdge>();
        /// <summary>批量刷新控件时阻止选择事件重复查询相机。</summary>
        private bool _updatingTriggerControls;
        /// <summary>获取或设置节点参数。</summary>
        public INodeParam Params { get; set; }

        /// <summary>初始化参数窗体。</summary>
        public ParamFormLineScanImageSource()
        {
            InitializeComponent();
            Shown += ParamFormLineScanImageSource_Shown;
        }

        /// <summary>设置所属节点；当前窗体不需要保留节点引用。</summary>
        public void SetNodeBelong(NodeBase node) { }

        /// <summary>显示窗体时刷新线扫相机列表。</summary>
        private void ParamFormLineScanImageSource_Shown(object sender, EventArgs e)
        {
            RefreshCameraList();
            RefreshTriggerSelectors(null);
            RefreshTriggerControls(null, null);
        }

        /// <summary>刷新按钮事件。</summary>
        private void buttonRefresh_Click(object sender, EventArgs e) => RefreshCameraList();

        /// <summary>保存参数。</summary>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            ILineScanCamera camera = Solution.Instance.LineScanCameraDevices.FirstOrDefault(item => item.UserDefinedName == comboBoxCamera.Text);
            if (camera == null)
            {
                MessageBoxTD.Show("请选择线扫相机！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (comboTriggerMode.SelectedIndex == 1 && !GetTriggerSourceOrNull().HasValue)
            {
                MessageBoxTD.Show("触发模式开启时必须从已连接相机读取并选择触发源！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            TriggerSource selectedSource = GetTriggerSource();
            if (comboTriggerMode.SelectedIndex == 1 && selectedSource != TriggerSource.SOFT && comboTriggerEdge.SelectedIndex < 0)
            {
                MessageBoxTD.Show("当前线路触发源没有可选择的触发极性！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Params = new NodeParamLineScanImageSource
            {
                Camera = camera,
                CameraName = camera.UserDefinedName,
                Width = (long)numericWidth.Value,
                Height = (long)numericHeight.Value,
                ExposureTime = (double)numericExposure.Value,
                Gain = (double)numericGain.Value,
                TriggerSelector = GetTriggerSelector(),
                TriggerMode = comboTriggerMode.SelectedIndex == 1 ? Device.Camera.TriggerModel.On : Device.Camera.TriggerModel.Off,
                TriggerSource = selectedSource,
                TriggerEdge = GetTriggerEdge()
            };
            Hide();
        }

        /// <summary>把已保存参数还原到窗体。</summary>
        public void SetParam2Form()
        {
            NodeParamLineScanImageSource param = Params as NodeParamLineScanImageSource;
            if (param == null)
                return;
            RefreshCameraList();
            comboBoxCamera.SelectedItem = param.CameraName;
            numericWidth.Value = Clamp(param.Width, numericWidth);
            numericHeight.Value = Clamp(param.Height, numericHeight);
            numericExposure.Value = Clamp((decimal)param.ExposureTime, numericExposure);
            numericGain.Value = Clamp((decimal)param.Gain, numericGain);
            RefreshTriggerSelectors(param.TriggerSelector);
            comboTriggerMode.SelectedIndex = param.TriggerMode == TriggerModel.On ? 1 : 0;
            RefreshTriggerControls(param.TriggerSource, param.TriggerEdge);
            param.Camera = Solution.Instance.LineScanCameraDevices.FirstOrDefault(item => item.UserDefinedName == param.CameraName);
        }

        /// <summary>刷新当前方案中的线扫相机名称。</summary>
        private void RefreshCameraList()
        {
            string selected = comboBoxCamera.Text;
            comboBoxCamera.Items.Clear();
            foreach (ILineScanCamera camera in Solution.Instance.LineScanCameraDevices)
                comboBoxCamera.Items.Add(camera.UserDefinedName);
            if (comboBoxCamera.Items.Contains(selected))
                comboBoxCamera.SelectedItem = selected;
            else if (comboBoxCamera.Items.Count > 0)
                comboBoxCamera.SelectedIndex = 0;
        }

        /// <summary>把数值限制在控件范围内。</summary>
        private static decimal Clamp(decimal value, NumericUpDown control) => Math.Max(control.Minimum, Math.Min(control.Maximum, value));

        /// <summary>相机选择变化时重新读取当前相机的触发能力。</summary>
        private void comboBoxCamera_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshTriggerSelectors(null);
            RefreshTriggerControls(null, null);
        }

        /// <summary>触发选择器变化时重新读取其下属触发源和触发极性。</summary>
        private void comboTriggerSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_updatingTriggerControls)
                RefreshTriggerControls(null, null);
        }

        /// <summary>触发模式变化时更新触发源与触发极性的可用状态。</summary>
        private void comboTriggerMode_SelectedIndexChanged(object sender, EventArgs e) => RefreshTriggerControls(null, null);

        /// <summary>触发源变化时只读取该触发源支持的触发极性。</summary>
        private void comboTriggerSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_updatingTriggerControls)
                RefreshTriggerEdges(null);
        }

        /// <summary>根据相机实际能力刷新触发源；关闭触发时不访问相关GenICam节点。</summary>
        private void RefreshTriggerControls(TriggerSource? preferredSource, TriggerEdge? preferredEdge)
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                bool triggerEnabled = comboTriggerMode.SelectedIndex == 1;
                comboTriggerSource.Enabled = false;
                comboTriggerEdge.Enabled = false;
                if (!triggerEnabled)
                {
                    comboTriggerSource.Items.Clear();
                    comboTriggerEdge.Items.Clear();
                    _supportedTriggerSources.Clear();
                    _supportedTriggerEdges.Clear();
                    labelTriggerHint.Text = "触发模式关闭，不读取触发源和触发极性";
                    return;
                }

                ILineScanCamera camera = GetSelectedCamera();
                if (camera == null || !camera.IsOpen)
                {
                    comboTriggerSource.Items.Clear();
                    comboTriggerEdge.Items.Clear();
                    labelTriggerHint.Text = "请先连接线扫相机，再读取触发能力";
                    return;
                }
                if (camera.IsGrabbing)
                {
                    labelTriggerHint.Text = "相机正在取流，停止取流后可读取触发能力";
                    return;
                }

                TriggerSource? oldSource = preferredSource ?? GetTriggerSourceOrNull();
                _supportedTriggerSources.Clear();
                _supportedTriggerSources.AddRange(camera.GetSupportedTriggerSources(GetTriggerSelector()));
                comboTriggerSource.Items.Clear();
                foreach (TriggerSource source in _supportedTriggerSources)
                    comboTriggerSource.Items.Add(GetTriggerSourceText(source));
                int selectedIndex = oldSource.HasValue ? _supportedTriggerSources.IndexOf(oldSource.Value) : -1;
                comboTriggerSource.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_supportedTriggerSources.Count > 0 ? 0 : -1);
                comboTriggerSource.Enabled = _supportedTriggerSources.Count > 0;
                labelTriggerHint.Text = _supportedTriggerSources.Count > 0 ? "已读取相机支持的触发源" : "相机未开放可用的触发源";
            }
            catch (Exception exception)
            {
                labelTriggerHint.Text = "读取触发源失败：" + exception.Message;
            }
            finally
            {
                _updatingTriggerControls = false;
            }
            RefreshTriggerEdges(preferredEdge);
        }

        /// <summary>读取当前触发源实际支持的触发极性；软件触发没有触发极性。</summary>
        private void RefreshTriggerEdges(TriggerEdge? preferredEdge)
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                comboTriggerEdge.Enabled = false;
                comboTriggerEdge.Items.Clear();
                _supportedTriggerEdges.Clear();
                TriggerSource? source = GetTriggerSourceOrNull();
                if (!source.HasValue)
                    return;
                if (source.Value == TriggerSource.SOFT)
                {
                    labelTriggerHint.Text = "软件触发没有触发极性";
                    return;
                }
                ILineScanCamera camera = GetSelectedCamera();
                if (camera == null || !camera.IsOpen || camera.IsGrabbing)
                    return;
                _supportedTriggerEdges.AddRange(camera.GetSupportedTriggerEdges(GetTriggerSelector(), source.Value));
                foreach (TriggerEdge edge in _supportedTriggerEdges)
                    comboTriggerEdge.Items.Add(GetTriggerEdgeText(edge));
                int selectedIndex = preferredEdge.HasValue ? _supportedTriggerEdges.IndexOf(preferredEdge.Value) : -1;
                comboTriggerEdge.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_supportedTriggerEdges.Count > 0 ? 0 : -1);
                comboTriggerEdge.Enabled = _supportedTriggerEdges.Count > 0;
                labelTriggerHint.Text = _supportedTriggerEdges.Count > 0 ? "已读取当前触发源支持的触发极性" : "当前触发源没有可用的触发极性";
            }
            catch (Exception exception)
            {
                labelTriggerHint.Text = "读取触发极性失败：" + exception.Message;
            }
            finally
            {
                _updatingTriggerControls = false;
            }
        }

        /// <summary>获取当前选择的线扫相机。</summary>
        private ILineScanCamera GetSelectedCamera() => Solution.Instance.LineScanCameraDevices.FirstOrDefault(item => item.UserDefinedName == comboBoxCamera.Text);

        /// <summary>刷新触发选择器；连接状态正常时以相机实际能力为准。</summary>
        private void RefreshTriggerSelectors(LineScanTriggerSelector? preferredSelector)
        {
            if (_updatingTriggerControls)
                return;
            _updatingTriggerControls = true;
            try
            {
                LineScanTriggerSelector current = preferredSelector ?? GetTriggerSelector();
                _supportedTriggerSelectors.Clear();
                ILineScanCamera camera = GetSelectedCamera();
                if (camera != null && camera.IsOpen && !camera.IsGrabbing)
                    _supportedTriggerSelectors.AddRange(camera.GetSupportedTriggerSelectors());
                if (_supportedTriggerSelectors.Count == 0)
                {
                    _supportedTriggerSelectors.Add(LineScanTriggerSelector.FrameBurstStart);
                    _supportedTriggerSelectors.Add(LineScanTriggerSelector.LineStart);
                }
                comboTriggerSelector.Items.Clear();
                foreach (LineScanTriggerSelector selector in _supportedTriggerSelectors)
                    comboTriggerSelector.Items.Add(GetTriggerSelectorText(selector));
                int index = _supportedTriggerSelectors.IndexOf(current);
                comboTriggerSelector.SelectedIndex = index >= 0 ? index : 0;
                comboTriggerSelector.Enabled = camera == null || !camera.IsGrabbing;
            }
            catch (Exception exception)
            {
                labelTriggerHint.Text = "读取触发选择器失败：" + exception.Message;
            }
            finally
            {
                _updatingTriggerControls = false;
            }
        }

        /// <summary>获取当前触发选择器。</summary>
        private LineScanTriggerSelector GetTriggerSelector() => comboTriggerSelector.SelectedIndex >= 0 && comboTriggerSelector.SelectedIndex < _supportedTriggerSelectors.Count ? _supportedTriggerSelectors[comboTriggerSelector.SelectedIndex] : LineScanTriggerSelector.LineStart;

        /// <summary>获取当前触发源；无有效选项时返回空。</summary>
        private TriggerSource? GetTriggerSourceOrNull() => comboTriggerSource.SelectedIndex >= 0 && comboTriggerSource.SelectedIndex < _supportedTriggerSources.Count ? _supportedTriggerSources[comboTriggerSource.SelectedIndex] : (TriggerSource?)null;

        /// <summary>获取保存使用的触发源。</summary>
        private TriggerSource GetTriggerSource() => GetTriggerSourceOrNull() ?? TriggerSource.SOFT;

        /// <summary>把中文触发极性选项转换为框架枚举。</summary>
        private TriggerEdge GetTriggerEdge() => comboTriggerEdge.SelectedIndex >= 0 && comboTriggerEdge.SelectedIndex < _supportedTriggerEdges.Count ? _supportedTriggerEdges[comboTriggerEdge.SelectedIndex] : TriggerEdge.Rising;

        /// <summary>取得触发源的中文名称。</summary>
        private static string GetTriggerSourceText(TriggerSource source)
        {
            if (source == TriggerSource.SOFT) return "软件触发";
            if (source == TriggerSource.EncoderModuleOut) return "编码器模块输出";
            if (source == TriggerSource.FrequencyConverter) return "分频器输出";
            return source.ToString().Replace("LINE", "线路");
        }

        /// <summary>取得触发选择器中文名称。</summary>
        private static string GetTriggerSelectorText(LineScanTriggerSelector selector) => selector == LineScanTriggerSelector.LineStart ? "行开始" : "帧触发开始";

        /// <summary>取得触发极性的中文名称。</summary>
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
    }
}
