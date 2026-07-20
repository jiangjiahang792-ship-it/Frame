using TDJS_Vision.Device.Camera;
using System;
using TDJS_Vision.Device._3D;
using System.Windows.Forms;
using Logger;
using System.Diagnostics;
using TDJS_Vision.Forms.LightAdd;
using System.Linq;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Startup;

namespace TDJS_Vision.Forms.CameraAdd
{
    public partial class FrmCameraListView : FormBase
    {
        /// <summary>
        /// 添加相机时通过快捷键保存方案的事件
        /// </summary>
        public event EventHandler OnShotKeySavePressed;
        /// <summary>
        /// 相机管理窗口关闭事件
        /// </summary>
        public static event EventHandler OnCameraListViewClosed;
        /// <summary>
        /// 反序列化完成相机后开始反序列化PLC
        /// </summary>
        public static event EventHandler<bool> OnCameraDeserializationCompletionEvent;
        /// <summary>
        /// 设备信息弹窗
        /// </summary>
        FrmCameraInfo _infoWnd = new FrmCameraInfo();

        /// <summary>
        /// 上一次选中的 2D 相机，用于再次打开窗口时恢复预览。
        /// </summary>
        private static ICamera _lastSelectedCamera;

        /// <summary>
        /// 上一次选中的 3D 相机，用于再次打开窗口时恢复预览。
        /// </summary>
        private static I3DCamera _lastSelectedCamera3D;

        /// <summary>
        /// 上一次选择是否为 3D 相机，保证 2D 和 3D 同名时仍按最后点击类型恢复。
        /// </summary>
        private static bool _lastSelectionIs3D;

        public FrmCameraListView()
        {
            InitializeComponent();
            FrmCameraInfo.AddCameraDevEvent += FrmCameraInfo_AddCameraDevEvent;
            FrmCameraInfo.AddCamera3DDevEvent += FrmCameraInfo_AddCamera3DDevEvent;
            SingleCamera.SelectedChange += SingleCamera_SingleCameraSelectedChanged;
            SingleCamera.SingleCameraRemoveEvent += SingleCamera_SingleCameraRemoveEvent;
            SingleCamera3D.SelectedChange += SingleCamera3D_SelectedChange;
            SingleCamera3D.SingleCamera3DRemoveEvent += SingleCamera3D_SingleCamera3DRemoveEvent;
            FrmLightListView.OnLightDeserializationCompletionEvent += Deserialization;
            Shown += FrmCameraListView_Shown;
            this.KeyPreview = true;
        }

        /// <summary>
        /// 窗口显示后自动恢复最近选择的相机预览。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void FrmCameraListView_Shown(object sender, EventArgs e)
        {
            ShowLastSelectedCameraOrFirst();
        }

        /// <summary>
        /// 反序列化相机设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Deserialization(object sender,  bool e)
        {
            try
            {
                // 先移除旧方案的相机控件
                flowLayoutPanel1.Controls.Clear();
                // 添加新的相机
                if (e)
                    LogHelper.AddLog(MsgLevel.Debug, $"================================================= 正在加载【相机设备列表】=================================================", true);
                var cameras = ConfigHelper.SolConfig.Devices
                    .Where(dev => dev is I3DCamera || dev is ICamera)
                    .ToList();
                if (cameras.Count == 0)
                    StartupProgressContext.ReportItem("正在恢复相机设备", "没有需要恢复的相机", 0, 0, 43, 55);
                for (int index = 0; index < cameras.Count; index++)
                {
                    var dev = cameras[index];
                    StartupProgressContext.ReportItem("正在恢复相机设备", dev.DevName, index + 1, cameras.Count, 43, 55);
                    if (dev is I3DCamera camera3D)
                    {
                        camera3D.CreateDevice(); // 创建 3D 相机，必要的
                        SingleCamera3D singleCamera3D = new SingleCamera3D(camera3D);
                        singleCamera3D.Anchor = AnchorStyles.Left;
                        singleCamera3D.Anchor = AnchorStyles.Right;
                        flowLayoutPanel1.Controls.Add(singleCamera3D);
                        if (e)
                            LogHelper.AddLog(MsgLevel.Info, $"3D相机设备【{dev.DevName}】已加载！", true);
                    }
                    else if (dev is ICamera camera)
                    {
                        camera.CreateDevice(); // 创建相机，必要的
                        SingleCamera singleCamera = new SingleCamera(camera);
                        singleCamera.Anchor = AnchorStyles.Left;
                        singleCamera.Anchor = AnchorStyles.Right;
                        flowLayoutPanel1.Controls.Add(singleCamera);
                        if (e)
                            LogHelper.AddLog(MsgLevel.Info, $"相机设备【{dev.DevName}】已加载！", true);
                    }

                }
                if (e)
                    LogHelper.AddLog(MsgLevel.Debug, $"================================================【相机设备列表】已加载完成 ================================================", true);

            }
            catch (Exception ex)
            {
                StartupProgressContext.ReportFailure("恢复相机设备", ex);
                LogHelper.AddLog(MsgLevel.Exception, $"恢复相机设备失败：{ex}", true);
            }
            finally
            {
                // 触发相机反序列化完成事件
                OnCameraDeserializationCompletionEvent?.Invoke(this, e);
            }
        }

        /// <summary>
        /// 按下保存快捷键
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.S)
            {
                // 触发保存方案事件
                OnShotKeySavePressed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 移除一个相机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingleCamera_SingleCameraRemoveEvent(object sender, SingleCamera e)
        {
            SingleCamera.SingleCameraList.Remove(e);
            ClearPreviewPanel();
            if (ReferenceEquals(_lastSelectedCamera, e.Camera))
                _lastSelectedCamera = null;
            e.Camera.Dispose();
            //然后移除掉方案中的全局相机并释放相机内存
            Solution.Instance.AllDevices.Remove(e.Camera);
            //最后移除掉单个相机控件
            flowLayoutPanel1.Controls.Remove(e);
            LogHelper.AddLog(MsgLevel.Info, $"相机设备（{e.Camera.DevName}）已成功移除！", true);
        }

        /// <summary>
        /// 移除一个 3D 相机。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">待移除的 3D 相机控件。</param>
        private void SingleCamera3D_SingleCamera3DRemoveEvent(object sender, SingleCamera3D e)
        {
            SingleCamera3D.SingleCamera3DList.Remove(e);
            ClearPreviewPanel();
            if (ReferenceEquals(_lastSelectedCamera3D, e.Camera3D))
                _lastSelectedCamera3D = null;
            e.Camera3D.Dispose();
            Solution.Instance.AllDevices.Remove(e.Camera3D);
            flowLayoutPanel1.Controls.Remove(e);
            LogHelper.AddLog(MsgLevel.Info, $"3D相机设备（{e.Camera3D.DevName}）已成功移除！", true);
        }

        /// <summary>
        /// 处理相机设备添加事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmCameraInfo_AddCameraDevEvent(object sender, CameraParam e)
        {
            SingleCamera singleCamera = null;
            try
            {
                singleCamera = new SingleCamera(e);
                singleCamera.Anchor = AnchorStyles.Left;
                singleCamera.Anchor = AnchorStyles.Right;
                flowLayoutPanel1.Controls.Add(singleCamera);
            }
            catch (Exception ex)
            {
                SingleCamera.SingleCameraList.Remove(singleCamera);
                LogHelper.AddLog(MsgLevel.Fatal, $"添加相机失败:{ex.Message}", true);
                MessageBoxTD.Show("添加失败！原因：" + ex.Message);
            }
        }

        /// <summary>
        /// 处理 3D 相机设备添加事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">3D 相机添加参数。</param>
        private void FrmCameraInfo_AddCamera3DDevEvent(object sender, Camera3DParam e)
        {
            SingleCamera3D singleCamera = null;
            try
            {
                singleCamera = new SingleCamera3D(e);
                singleCamera.Anchor = AnchorStyles.Left;
                singleCamera.Anchor = AnchorStyles.Right;
                flowLayoutPanel1.Controls.Add(singleCamera);
            }
            catch (Exception ex)
            {
                if (singleCamera != null)
                    SingleCamera3D.SingleCamera3DList.Remove(singleCamera);
                LogHelper.AddLog(MsgLevel.Fatal, $"添加3D相机失败:{ex.Message}", true);
                MessageBoxTD.Show("添加失败！原因：" + ex.Message);
            }
        }
        
        /// <summary>aq
        /// 处理选中事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingleCamera_SingleCameraSelectedChanged(object sender, SingleCamera e)
        {
            Select2DCameraForPreview(e);
        }

        /// <summary>
        /// 处理 3D 相机选中事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">当前选中的 3D 相机控件。</param>
        private void SingleCamera3D_SelectedChange(object sender, SingleCamera3D e)
        {
            Select3DCameraForPreview(e);
        }

        /// <summary>
        /// 自动显示上一次选中的相机；没有历史选择时显示列表中的第一台相机。
        /// </summary>
        private void ShowLastSelectedCameraOrFirst()
        {
            if (_lastSelectionIs3D)
            {
                if (TrySelectLast3DCamera() || TrySelectFirst3DCamera() || TrySelectLast2DCamera() || TrySelectFirst2DCamera())
                    return;
            }
            else
            {
                if (TrySelectLast2DCamera() || TrySelectFirst2DCamera() || TrySelectLast3DCamera() || TrySelectFirst3DCamera())
                    return;
            }

            ClearPreviewPanel();
        }

        /// <summary>
        /// 尝试恢复上一次选中的 2D 相机。
        /// </summary>
        /// <returns>恢复成功返回 true。</returns>
        private bool TrySelectLast2DCamera()
        {
            if (_lastSelectedCamera == null)
                return false;

            SingleCamera cameraControl = SingleCamera.SingleCameraList.FirstOrDefault(item => ReferenceEquals(item.Camera, _lastSelectedCamera));
            if (cameraControl == null)
                return false;

            Select2DCameraForPreview(cameraControl);
            return true;
        }

        /// <summary>
        /// 尝试选择第一台 2D 相机。
        /// </summary>
        /// <returns>选择成功返回 true。</returns>
        private bool TrySelectFirst2DCamera()
        {
            SingleCamera cameraControl = SingleCamera.SingleCameraList.FirstOrDefault();
            if (cameraControl == null)
                return false;

            Select2DCameraForPreview(cameraControl);
            return true;
        }

        /// <summary>
        /// 尝试恢复上一次选中的 3D 相机。
        /// </summary>
        /// <returns>恢复成功返回 true。</returns>
        private bool TrySelectLast3DCamera()
        {
            if (_lastSelectedCamera3D == null)
                return false;

            SingleCamera3D cameraControl = SingleCamera3D.SingleCamera3DList.FirstOrDefault(item => ReferenceEquals(item.Camera3D, _lastSelectedCamera3D));
            if (cameraControl == null)
                return false;

            Select3DCameraForPreview(cameraControl);
            return true;
        }

        /// <summary>
        /// 尝试选择第一台 3D 相机。
        /// </summary>
        /// <returns>选择成功返回 true。</returns>
        private bool TrySelectFirst3DCamera()
        {
            SingleCamera3D cameraControl = SingleCamera3D.SingleCamera3DList.FirstOrDefault();
            if (cameraControl == null)
                return false;

            Select3DCameraForPreview(cameraControl);
            return true;
        }

        /// <summary>
        /// 选择 2D 相机并显示实时调试预览。
        /// </summary>
        /// <param name="cameraControl">2D 相机列表控件。</param>
        private void Select2DCameraForPreview(SingleCamera cameraControl)
        {
            if (cameraControl?.Camera == null)
                return;

            _lastSelectedCamera = cameraControl.Camera;
            _lastSelectionIs3D = false;
            cameraControl.SelectInList();
            ShowPreviewControl(new CameraLiveDebugControl(cameraControl.Camera));
        }

        /// <summary>
        /// 选择 3D 相机并显示实时调试预览。
        /// </summary>
        /// <param name="cameraControl">3D 相机列表控件。</param>
        private void Select3DCameraForPreview(SingleCamera3D cameraControl)
        {
            if (cameraControl?.Camera3D == null)
                return;

            _lastSelectedCamera3D = cameraControl.Camera3D;
            _lastSelectionIs3D = true;
            cameraControl.SelectInList();
            ShowPreviewControl(new CameraLiveDebugControl(cameraControl.Camera3D));
        }

        /// <summary>
        /// 显示右侧相机预览调试控件。
        /// </summary>
        /// <param name="control">需要显示的相机调试控件。</param>
        private void ShowPreviewControl(UserControl control)
        {
            ClearPreviewPanel();
            control.Dock = DockStyle.Fill;
            panel1.Controls.Add(control);
        }

        /// <summary>
        /// 清空并释放右侧预览区域控件，避免旧相机回调继续刷新。
        /// </summary>
        private void ClearPreviewPanel()
        {
            foreach (Control control in panel1.Controls)
            {
                control.Dispose();
            }
            panel1.Controls.Clear();
        }

        /// <summary>
        /// 点击添加单个相机控件（左侧）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            _infoWnd.ShowDialog();
        }

        /// <summary>
        /// 窗口关闭时释放实时预览控件，并由预览控件恢复相机调试前状态。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmCameraListView_FormClosing(object sender, FormClosingEventArgs e)
        {
            ClearPreviewPanel();
            OnCameraListViewClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}
