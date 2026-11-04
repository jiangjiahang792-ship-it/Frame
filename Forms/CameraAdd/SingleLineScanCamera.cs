using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Device.Camera.LineScan;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>相机管理列表中的单个线扫相机控件。</summary>
    public partial class SingleLineScanCamera : UserControl
    {
        /// <summary>当前窗口创建的线扫相机控件。</summary>
        public static List<SingleLineScanCamera> CameraControls { get; } = new List<SingleLineScanCamera>();
        /// <summary>用户选择线扫相机事件。</summary>
        public static event EventHandler<SingleLineScanCamera> SelectedChange;
        /// <summary>用户移除线扫相机事件。</summary>
        public static event EventHandler<SingleLineScanCamera> RemoveRequested;
        /// <summary>获取对应线扫相机。</summary>
        public ILineScanCamera Camera { get; }

        /// <summary>使用已存在的线扫相机创建设备控件。</summary>
        public SingleLineScanCamera(ILineScanCamera camera)
        {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            InitializeComponent();
            labelName.Text = camera.UserDefinedName;
            Camera.ConnectStatusEvent += Camera_ConnectStatusEvent;
            SetConnectSwitch(camera.IsOpen);
            CameraControls.Add(this);
            // 方案反序列化只恢复托管属性，后台重新创建句柄并按保存的连接意图自动打开。
            TDJS_Vision.Startup.StartupDeviceConnectionBatch.RunOrQueue(camera.UserDefinedName, () =>
            {
                Camera.CreateDevice();
                if (Camera.AutoConnectOnLoad)
                    Camera.Open();
            });
        }

        /// <summary>使用添加参数创建新的海康线扫相机。</summary>
        public SingleLineScanCamera(LineScanCameraParam param) : this(new CameraHikLineScan(param.DevInfo, param.UserDefinedName))
        {
            Solution.Instance.AllDevices.Add(Camera);
        }

        /// <summary>连接开关变化时打开或关闭相机。</summary>
        private async void uiSwitchConnect_ValueChanged(object sender, bool value)
        {
            try
            {
                Camera.AutoConnectOnLoad = value;
                await Task.Run(() => { if (value) Camera.Open(); else Camera.Close(); });
            }
            catch (Exception exception)
            {
                SetConnectSwitch(Camera.IsOpen);
                LogHelper.AddLog(MsgLevel.Fatal, string.Format("线扫相机【{0}】{1}失败：{2}",
                    Camera.UserDefinedName, value ? "连接" : "断开", exception.Message), true);
            }
        }

        /// <summary>连接状态变化后安全刷新界面开关。</summary>
        private void Camera_ConnectStatusEvent(object sender, bool isConnected) => SetConnectSwitch(isConnected);

        /// <summary>不触发开关事件地同步实际连接状态。</summary>
        private void SetConnectSwitch(bool isConnected)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<bool>(SetConnectSwitch), isConnected); }
                catch (InvalidOperationException) { }
                return;
            }
            uiSwitchConnect.ValueChanged -= uiSwitchConnect_ValueChanged;
            uiSwitchConnect.Active = isConnected;
            uiSwitchConnect.ValueChanged += uiSwitchConnect_ValueChanged;
        }

        /// <summary>控件句柄创建后按相机实际状态刷新连接开关。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Camera.ConnectStatusEvent -= Camera_ConnectStatusEvent;
            Camera.ConnectStatusEvent += Camera_ConnectStatusEvent;
            SetConnectSwitch(Camera != null && Camera.IsOpen);
        }

        /// <summary>选择当前线扫相机。</summary>
        private void SingleLineScanCamera_MouseClick(object sender, MouseEventArgs e)
        {
            foreach (SingleLineScanCamera control in CameraControls) control.tableLayoutPanelMain.BackColor = Color.CadetBlue;
            tableLayoutPanelMain.BackColor = Color.LightSeaGreen;
            SelectedChange?.Invoke(this, this);
        }

        /// <summary>请求移除当前线扫相机。</summary>
        private void menuRemove_Click(object sender, EventArgs e) => RemoveRequested?.Invoke(this, this);

        /// <summary>释放控件时取消相机事件订阅。</summary>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (Camera != null)
                Camera.ConnectStatusEvent -= Camera_ConnectStatusEvent;
            base.OnHandleDestroyed(e);
        }
    }
}
