using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 双相机选择、实时显示和基准测试操作界面。
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>相机1测试会话。</summary>
        private CameraBenchmarkSession _camera1;

        /// <summary>相机2测试会话。</summary>
        private CameraBenchmarkSession _camera2;

        /// <summary>相机1 UI显示计时。</summary>
        private UiTimingCollector _uiTiming1;

        /// <summary>相机2 UI显示计时。</summary>
        private UiTimingCollector _uiTiming2;

        /// <summary>当前测试参数。</summary>
        private BenchmarkSettings _settings;

        /// <summary>是否正在采集。</summary>
        private bool _running;

        /// <summary>是否已经进入自动停止流程。</summary>
        private bool _autoStopping;

        /// <summary>
        /// 创建主窗体并绑定运行事件。
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗体加载时初始化触发选项并枚举设备。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            cmbTriggerMode.Items.Clear();
            cmbTriggerMode.Items.Add("Line0硬触发（与主程序一致）");
            cmbTriggerMode.Items.Add("连续采集");
            cmbTriggerMode.SelectedIndex = 0;
            RefreshDevices();
            uiTimer.Start();
        }

        /// <summary>
        /// 重新枚举相机按钮事件。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnRefreshDevices_Click(object sender, EventArgs e)
        {
            RefreshDevices();
        }

        /// <summary>
        /// 开始双相机测试按钮事件。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_running)
                return;

            CameraDeviceItem firstDevice = cmbCamera1.SelectedItem as CameraDeviceItem;
            CameraDeviceItem secondDevice = cmbCamera2.SelectedItem as CameraDeviceItem;
            if (firstDevice == null || secondDevice == null)
            {
                MessageBox.Show("请选择两台相机。", "参数不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (firstDevice.Index == secondDevice.Index)
            {
                MessageBox.Show("相机1和相机2不能选择同一设备。", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _settings = ReadSettings();
            _uiTiming1 = new UiTimingCollector();
            _uiTiming2 = new UiTimingCollector();
            _camera1 = new CameraBenchmarkSession(1, firstDevice, _settings);
            _camera2 = new CameraBenchmarkSession(2, secondDevice, _settings);
            _autoStopping = false;

            try
            {
                _camera1.Start();
                _camera2.Start();
                _running = true;
                uiTimer.Interval = Math.Max(15, 1000 / _settings.DisplayFps);
                SetRunningState(true);
                AppendStatus("双相机已经开始采集，正式统计不会包含前" + _settings.WarmupFrames + "帧预热数据。");
            }
            catch (Exception exception)
            {
                _camera1?.Dispose();
                _camera2?.Dispose();
                _camera1 = null;
                _camera2 = null;
                AppendStatus("启动失败：" + exception.Message);
                MessageBox.Show(exception.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 停止测试并导出按钮事件。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopAndExport("用户停止");
        }

        /// <summary>
        /// UI定时器只消费最新帧和刷新计数，不进入相机回调线程。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void uiTimer_Tick(object sender, EventArgs e)
        {
            if (!_running)
                return;

            if (_settings.DisplayEnabled)
            {
                DisplayLatestFrame(_camera1, pictureCamera1, _uiTiming1);
                DisplayLatestFrame(_camera2, pictureCamera2, _uiTiming2);
            }

            UpdateRuntimeLabels();
            if (!_autoStopping &&
                _camera1.TimingCollector.IsComplete &&
                _camera2.TimingCollector.IsComplete)
            {
                _autoStopping = true;
                BeginInvoke(new Action(() => StopAndExport("达到样本目标")));
            }
        }

        /// <summary>
        /// 关闭窗体前停止相机，正在运行时保存已有样本。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_running)
            {
                StopAndExport("关闭程序");
            }

            DisposePicture(pictureCamera1);
            DisposePicture(pictureCamera2);
        }

        /// <summary>
        /// 枚举设备并默认选择前两台相机。
        /// </summary>
        private void RefreshDevices()
        {
            try
            {
                IReadOnlyList<CameraDeviceItem> devices = CameraDeviceDiscovery.Enumerate();
                cmbCamera1.Items.Clear();
                cmbCamera2.Items.Clear();
                foreach (CameraDeviceItem device in devices)
                {
                    cmbCamera1.Items.Add(device);
                    cmbCamera2.Items.Add(device);
                }

                if (devices.Count > 0)
                    cmbCamera1.SelectedIndex = 0;
                if (devices.Count > 1)
                    cmbCamera2.SelectedIndex = 1;
                lblDeviceCount.Text = "发现设备：" + devices.Count;
                AppendStatus("设备枚举完成，共发现" + devices.Count + "台相机。");
            }
            catch (Exception exception)
            {
                lblDeviceCount.Text = "设备枚举失败";
                AppendStatus("设备枚举失败：" + exception.Message);
            }
        }

        /// <summary>
        /// 从界面读取本轮测试参数。
        /// </summary>
        /// <returns>不可变使用的参数快照。</returns>
        private BenchmarkSettings ReadSettings()
        {
            return new BenchmarkSettings
            {
                WarmupFrames = decimal.ToInt32(numWarmupFrames.Value),
                SampleFrames = decimal.ToInt32(numSampleFrames.Value),
                TriggerMode = cmbTriggerMode.SelectedIndex == 0
                    ? CameraTriggerMode.Line0
                    : CameraTriggerMode.Continuous,
                DisplayEnabled = chkDisplayEnabled.Checked,
                DisplayFps = decimal.ToInt32(numDisplayFps.Value)
            };
        }

        /// <summary>
        /// 把容量1槽位中的最新Mat转换为Bitmap并替换到PictureBox。
        /// </summary>
        /// <param name="session">相机会话。</param>
        /// <param name="pictureBox">目标显示控件。</param>
        /// <param name="collector">UI计时器。</param>
        private void DisplayLatestFrame(
            CameraBenchmarkSession session,
            PictureBox pictureBox,
            UiTimingCollector collector)
        {
            if (session == null)
                return;

            Mat mat = session.LatestFrame.Take();
            if (mat == null)
                return;

            Bitmap bitmap = null;
            try
            {
                long toBitmapStarted = Stopwatch.GetTimestamp();
                bitmap = BitmapConverter.ToBitmap(mat);
                long toBitmapCompleted = Stopwatch.GetTimestamp();

                long setImageStarted = Stopwatch.GetTimestamp();
                Image previous = pictureBox.Image;
                pictureBox.Image = bitmap;
                bitmap = null;
                previous?.Dispose();
                long setImageCompleted = Stopwatch.GetTimestamp();

                collector.Add(new UiTimingRecord
                {
                    ToBitmapTicks = toBitmapCompleted - toBitmapStarted,
                    SetImageTicks = setImageCompleted - setImageStarted
                });
            }
            catch (Exception exception)
            {
                AppendStatus("显示转换失败：" + exception.Message);
            }
            finally
            {
                bitmap?.Dispose();
                mat.Dispose();
            }
        }

        /// <summary>
        /// 停止两台相机，在相机完全停止后统一计算并导出结果。
        /// </summary>
        /// <param name="reason">停止原因。</param>
        private void StopAndExport(string reason)
        {
            if (!_running)
                return;

            _running = false;
            SetRunningState(false);
            AppendStatus("正在停止相机，原因：" + reason + "。");

            _camera1.Stop();
            _camera2.Stop();
            UpdateRuntimeLabels();

            try
            {
                string baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Results");
                string resultDirectory = ResultExporter.Export(
                    baseDirectory,
                    _settings,
                    _camera1,
                    _camera2,
                    _uiTiming1,
                    _uiTiming2);
                txtResultPath.Text = resultDirectory;
                AppendStatus("测试结果已保存：" + resultDirectory);
                ShowFinalStatistics();
            }
            catch (Exception exception)
            {
                AppendStatus("导出结果失败：" + exception.Message);
                MessageBox.Show(exception.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _camera1.Dispose();
                _camera2.Dispose();
            }
        }

        /// <summary>
        /// 刷新两台相机的正式样本计数和错误数。
        /// </summary>
        private void UpdateRuntimeLabels()
        {
            if (_camera1 != null)
            {
                lblCamera1Stats.Text = string.Format(
                    "正式样本 {0}/{1}    回调错误 {2}    UI覆盖 {3}",
                    _camera1.TimingCollector.FormalSampleCount,
                    _settings.SampleFrames,
                    _camera1.CallbackErrorCount,
                    _camera1.LatestFrame.ReplacedCount);
            }

            if (_camera2 != null)
            {
                lblCamera2Stats.Text = string.Format(
                    "正式样本 {0}/{1}    回调错误 {2}    UI覆盖 {3}",
                    _camera2.TimingCollector.FormalSampleCount,
                    _settings.SampleFrames,
                    _camera2.CallbackErrorCount,
                    _camera2.LatestFrame.ReplacedCount);
            }
        }

        /// <summary>
        /// 停止后在界面显示回调总耗时的核心统计。
        /// </summary>
        private void ShowFinalStatistics()
        {
            TimingStatistics first = _camera1.TimingCollector.Calculate(item => item.CallbackTicks);
            TimingStatistics second = _camera2.TimingCollector.Calculate(item => item.CallbackTicks);
            lblCamera1Stats.Text = FormatFinalStatistics(first);
            lblCamera2Stats.Text = FormatFinalStatistics(second);
        }

        /// <summary>
        /// 格式化最终回调统计。
        /// </summary>
        /// <param name="statistics">回调统计。</param>
        /// <returns>界面显示文本。</returns>
        private static string FormatFinalStatistics(TimingStatistics statistics)
        {
            return string.Format(
                "回调平均 {0:F3}ms    P99 {1:F3}ms    最大 {2:F3}ms    >30ms {3}",
                statistics.AverageMs,
                statistics.P99Ms,
                statistics.MaximumMs,
                statistics.Over30Ms);
        }

        /// <summary>
        /// 切换测试期间允许操作的控件。
        /// </summary>
        /// <param name="running">是否正在运行。</param>
        private void SetRunningState(bool running)
        {
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
            btnRefreshDevices.Enabled = !running;
            cmbCamera1.Enabled = !running;
            cmbCamera2.Enabled = !running;
            cmbTriggerMode.Enabled = !running;
            numWarmupFrames.Enabled = !running;
            numSampleFrames.Enabled = !running;
            numDisplayFps.Enabled = !running;
            chkDisplayEnabled.Enabled = !running;
        }

        /// <summary>
        /// 向状态文本框追加低频操作日志。
        /// </summary>
        /// <param name="message">状态文本。</param>
        private void AppendStatus(string message)
        {
            txtStatus.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
        }

        /// <summary>
        /// 释放PictureBox当前持有的Bitmap。
        /// </summary>
        /// <param name="pictureBox">显示控件。</param>
        private static void DisposePicture(PictureBox pictureBox)
        {
            Image image = pictureBox.Image;
            pictureBox.Image = null;
            image?.Dispose();
        }
    }
}
