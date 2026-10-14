using Logger;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device.Camera;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>
    /// 单个相机控件
    /// </summary>
    public partial class SingleCamera : UserControl
    {
        /// <summary>
        /// 相机对象
        /// </summary>
        public ICamera Camera;
        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected = false;
        /// <summary>
        /// 相机名称
        /// </summary>
        public string CameraName { get => label1.Text; set => label1.Text = value; }
        /// <summary>
        /// 当前类实例选中改变事件
        /// </summary>
        public static event EventHandler<SingleCamera> SelectedChange;
        /// <summary>
        /// 移除当前实例
        /// </summary>
        public static event EventHandler<SingleCamera> SingleCameraRemoveEvent;
        /// <summary>
        /// 保存所有的当前类实例
        /// </summary>
        public static List<SingleCamera> SingleCameraList = new List<SingleCamera>();

        /// <summary>
        /// 反序列化用
        /// </summary>
        /// <param name="camera"></param>
        public SingleCamera(Device.Camera.ICamera camera)
        {
            InitializeComponent();
            Camera = camera;
            Camera.ConnectStatusEvent += Camera_ConnectStatusEvent;
            // 批量恢复已提前登记；单独创建控件也必须在取流前登记，且不能重复加入。
            if (!Solution.Instance.AllDevices.Contains(Camera))
                Solution.Instance.AllDevices.Add(Camera);
            if (camera.IsOpen || camera.RestoreConnectionRequested)
            {
                TDJS_Vision.Startup.StartupDeviceConnectionBatch.RunOrQueue(camera.UserDefinedName, () =>
                {
                    Camera.CreateDevice();
                    Camera.Open();
                    Solution.Instance.TryApplyImageSourceParametersForCamera(Camera);
                    Camera.StartGrabbing();
                    Camera.SetTriggerMode(Camera.GetTriggerMode());
                    if (!Camera.IsOpen)
                        throw new InvalidOperationException($"相机（{camera.UserDefinedName}）连接失败。");
                });
            }
            else
                TDJS_Vision.Startup.StartupDeviceConnectionBatch.RunOrQueue(camera.UserDefinedName, Camera.CreateDevice);
            this.label1.Text = camera.UserDefinedName;
            // 保存所有的实例
            SingleCameraList.Add(this);
        }

        private void Camera_ConnectStatusEvent(object sender, bool e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => Camera_ConnectStatusEvent(sender, e))); }
                catch (InvalidOperationException) { }
                return;
            }
            uiSwitch1.ValueChanged -= uiSwitch1_ValueChanged;
            uiSwitch1.Active = Camera?.IsOpen == true;
            uiSwitch1.ValueChanged += uiSwitch1_ValueChanged;
        }

        /// <summary>首次创建窗口句柄时刷新实际连接状态。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Camera_ConnectStatusEvent(Camera, Camera?.IsOpen == true);
        }

        public SingleCamera(CameraParam parms)
        {
            InitializeComponent();

            this.label1.Text =  parms.UserDefinedName;

            try
            {
                Camera = new CameraHik(parms.DevInfo.cameraInfo, parms.UserDefinedName);
                Camera.ConnectStatusEvent += Camera_ConnectStatusEvent;
                Solution.Instance.AllDevices.Add(Camera);
                // 保存所有的实例
                SingleCameraList.Add(this);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// 点击选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SinglePLCInfo_MouseClick(object sender, MouseEventArgs e)
        {
            SetSelected();
            SelectedChange?.Invoke(this, this);
        }

        /// <summary>
        /// 从相机管理窗口恢复最近选择时标记当前相机为选中。
        /// </summary>
        public void SelectInList()
        {
            SetSelected();
        }

        /// <summary>
        /// 设置控件选中状态
        /// </summary>
        /// <param name="flag"></param>
        private void SetSelected()
        {
            //先清除所有选中状态
            foreach (var item in SingleCameraList)
            {
                item.tableLayoutPanel1.BackColor = Color.LightSteelBlue;
                item.label1.BackColor = Color.LightSteelBlue;
                item.IsSelected = false;
            }
            
            // 设置当前选中的样式
            this.tableLayoutPanel1.BackColor = Color.CornflowerBlue;
            this.label1.BackColor = Color.CornflowerBlue;
            IsSelected = true;
        }

        /// <summary>
        /// 连接相机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private async void uiSwitch1_ValueChanged(object sender, bool value)
        {
            if (value)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        Camera.Open();
                        Solution.Instance.TryApplyImageSourceParametersForCamera(Camera);
                        Camera.SetTriggerMode(Camera.GetTriggerMode());
                        Camera.StartGrabbing();
                    });
                    LogHelper.AddLog(MsgLevel.Info, $"{Camera.UserDefinedName}已打开！", true);
                }
                catch (Exception e)
                {
                    uiSwitch1.Active = false;
                    LogHelper.AddLog(MsgLevel.Fatal, $"{Camera.UserDefinedName}打开失败！原因：{e.Message}", true);
                }
            }
            else
            {
                try
                {
                    Camera.Close();
                    LogHelper.AddLog(MsgLevel.Info, $"{Camera.UserDefinedName}已关闭！", true);
                }
                catch (Exception e)
                {
                    LogHelper.AddLog(MsgLevel.Fatal, $"{Camera.UserDefinedName}关闭失败！", true);
                }
            }
            
        }

        /// <summary>
        /// 右击移除当前实例
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void 移除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IsSelected)
                SingleCameraRemoveEvent?.Invoke(this, this);
        }
    }
}
