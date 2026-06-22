using TDJS_Vision.Device.Camera;
using System;
using TDJS_Vision.Device._3D;
using System.Windows.Forms;
using Logger;
using System.Diagnostics;
using TDJS_Vision.Forms.LightAdd;
using System.Linq;
using TDJS_Vision.Forms.YTMessageBox;

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
            this.KeyPreview = true;
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
                foreach (var dev in ConfigHelper.SolConfig.Devices)
                {
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
            catch (Exception) { }
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
            panel1.Controls.Clear();
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
            panel1.Controls.Clear();
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
            panel1.Controls.Clear();
            var control = new CameraParamsShowControl(e.Camera);
            control.Dock = DockStyle.Fill;
            panel1.Controls.Add(control);
        }

        /// <summary>
        /// 处理 3D 相机选中事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">当前选中的 3D 相机控件。</param>
        private void SingleCamera3D_SelectedChange(object sender, SingleCamera3D e)
        {
            panel1.Controls.Clear();
            var control = new Camera3DParamsShowControl(e.Camera3D);
            control.Dock = DockStyle.Fill;
            panel1.Controls.Add(control);
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
        /// 窗口关闭关掉所有相机
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmCameraListView_FormClosing(object sender, FormClosingEventArgs e)
        {
            try 
            {
                foreach (var item in Solution.Instance.CameraDevices)
                {
                    if (item.IsOpen)
                    {
                        item.SetTriggerMode(TriggerModel.On);
                        item.SetTriggerSource(TriggerSource.LINE0); // 默认设置触发源为LINE0
                    }
                }
            } 
            catch(Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, ex.Message, true);    
            }
        }
    }
}
