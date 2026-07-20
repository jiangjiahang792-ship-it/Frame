using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>
    /// 单个 3D 相机控件，用于在相机管理列表中显示和连接 3D 相机设备。
    /// </summary>
    public partial class SingleCamera3D : UserControl
    {
        /// <summary>
        /// 当前控件绑定的 3D 相机对象。
        /// </summary>
        public I3DCamera Camera3D;

        /// <summary>
        /// 当前控件是否被选中。
        /// </summary>
        public bool IsSelected;

        /// <summary>
        /// 3D 相机显示名称。
        /// </summary>
        public string CameraName
        {
            get => labelCameraName.Text;
            set => labelCameraName.Text = value;
        }

        /// <summary>
        /// 当前类实例选中改变事件。
        /// </summary>
        public static event EventHandler<SingleCamera3D> SelectedChange;

        /// <summary>
        /// 移除当前 3D 相机控件事件。
        /// </summary>
        public static event EventHandler<SingleCamera3D> SingleCamera3DRemoveEvent;

        /// <summary>
        /// 保存所有 3D 相机控件实例，便于统一清除选中状态。
        /// </summary>
        public static List<SingleCamera3D> SingleCamera3DList { get; } = new List<SingleCamera3D>();

        /// <summary>
        /// 使用反序列化后的 3D 相机对象创建控件。
        /// </summary>
        /// <param name="camera">3D 相机对象。</param>
        public SingleCamera3D(I3DCamera camera)
        {
            InitializeComponent();
            Camera3D = camera ?? throw new ArgumentNullException(nameof(camera));
            Camera3D.ConnectStatusEvent += Camera3D_ConnectStatusEvent;
            labelCameraName.Text = Camera3D.UserDefinedName;
            Solution.Instance.AllDevices.Add(Camera3D);
            SingleCamera3DList.Add(this);
            StartAutoConnectIfNeeded();
        }

        /// <summary>
        /// 使用添加窗口参数创建 3D 相机控件。
        /// </summary>
        /// <param name="parms">3D 相机添加参数。</param>
        public SingleCamera3D(Camera3DParam parms)
        {
            InitializeComponent();
            Camera3D = new CameraHik3D(parms.DevInfo, parms.UserDefinedName);
            Camera3D.ConnectStatusEvent += Camera3D_ConnectStatusEvent;
            labelCameraName.Text = parms.UserDefinedName;
            Solution.Instance.AllDevices.Add(Camera3D);
            SingleCamera3DList.Add(this);
        }

        /// <summary>
        /// 连接状态变化后同步开关状态。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">是否已连接。</param>
        private void Camera3D_ConnectStatusEvent(object sender, bool e)
        {
            SetConnectSwitch(e);
        }

        /// <summary>
        /// 根据保存配置在方案加载后自动连接 3D 相机。
        /// </summary>
        private async void StartAutoConnectIfNeeded()
        {
            if (Camera3D == null || !Camera3D.AutoConnectOnLoad)
                return;

            await ConnectCameraAsync();
        }

        /// <summary>
        /// 异步打开 3D 相机并开始采集。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async Task ConnectCameraAsync()
        {
            if (Camera3D == null)
                return;

            Camera3D.AutoConnectOnLoad = true;

            try
            {
                await Task.Run(() =>
                {
                    Camera3D.Open();
                    Camera3D.StartGrabbing();
                });
                LogHelper.AddLog(MsgLevel.Info, $"{Camera3D.UserDefinedName}已打开！", true);
            }
            catch (Exception ex)
            {
                CloseCameraAfterConnectFailure();
                SetConnectSwitch(false);
                LogHelper.AddLog(MsgLevel.Fatal, $"{Camera3D.UserDefinedName}打开失败！原因：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 连接失败时释放可能已经打开的 SDK 句柄。
        /// </summary>
        private void CloseCameraAfterConnectFailure()
        {
            try
            {
                Camera3D?.Close();
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"{Camera3D?.UserDefinedName}连接失败后释放资源异常：{ex.Message}", true);
            }
        }

        /// <summary>
        /// 不触发开关事件地同步连接开关状态。
        /// </summary>
        /// <param name="isConnected">是否已连接。</param>
        private void SetConnectSwitch(bool isConnected)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(SetConnectSwitch), isConnected);
                return;
            }

            uiSwitchConnect.ValueChanged -= uiSwitchConnect_ValueChanged;
            uiSwitchConnect.Active = isConnected;
            uiSwitchConnect.ValueChanged += uiSwitchConnect_ValueChanged;
        }

        /// <summary>
        /// 单击控件后设置为选中状态。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">鼠标事件参数。</param>
        private void SingleCamera3D_MouseClick(object sender, MouseEventArgs e)
        {
            SetSelected();
            SelectedChange?.Invoke(this, this);
        }

        /// <summary>
        /// 从相机管理窗口恢复最近选择时标记当前 3D 相机为选中。
        /// </summary>
        public void SelectInList()
        {
            SetSelected();
        }

        /// <summary>
        /// 设置当前控件为选中状态，并清除其它 3D 相机控件选中状态。
        /// </summary>
        private void SetSelected()
        {
            foreach (SingleCamera3D item in SingleCamera3DList)
            {
                item.tableLayoutPanelMain.BackColor = Color.LightSteelBlue;
                item.labelCameraName.BackColor = Color.LightSteelBlue;
                item.IsSelected = false;
            }

            tableLayoutPanelMain.BackColor = Color.CornflowerBlue;
            labelCameraName.BackColor = Color.CornflowerBlue;
            IsSelected = true;
        }

        /// <summary>
        /// 连接开关变化时打开或关闭 3D 相机。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="value">开关值。</param>
        private async void uiSwitchConnect_ValueChanged(object sender, bool value)
        {
            if (Camera3D == null)
                return;

            if (value)
            {
                await ConnectCameraAsync();
            }
            else
            {
                try
                {
                    Camera3D.AutoConnectOnLoad = false;
                    Camera3D.Close();
                    LogHelper.AddLog(MsgLevel.Info, $"{Camera3D.UserDefinedName}已关闭！", true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"{Camera3D.UserDefinedName}关闭失败！原因：{ex.Message}", true);
                }
            }
        }

        /// <summary>
        /// 右键菜单移除当前 3D 相机控件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void 移除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsSelected)
                SingleCamera3DRemoveEvent?.Invoke(this, this);
        }
    }
}
