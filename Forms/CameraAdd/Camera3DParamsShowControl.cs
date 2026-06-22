using System;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;

namespace TDJS_Vision.Forms.CameraAdd
{
    /// <summary>
    /// 3D 相机参数显示控件。
    /// </summary>
    public partial class Camera3DParamsShowControl : UserControl
    {
        /// <summary>
        /// 当前显示的 3D 相机。
        /// </summary>
        private readonly I3DCamera _camera;

        /// <summary>
        /// 创建 3D 相机参数显示控件。
        /// </summary>
        /// <param name="camera">3D 相机对象。</param>
        public Camera3DParamsShowControl(I3DCamera camera)
        {
            InitializeComponent();
            _camera = camera;
            if (_camera != null)
                _camera.ConnectStatusEvent += Camera_ConnectStatusEvent;
            SetInfo();
        }

        /// <summary>
        /// 连接状态变化时刷新参数显示。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">是否已连接。</param>
        private void Camera_ConnectStatusEvent(object sender, bool e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(SetInfo));
                return;
            }

            SetInfo();
        }

        /// <summary>
        /// 刷新 3D 相机参数显示。
        /// </summary>
        private void SetInfo()
        {
            labelNameValue.Text = GetText(_camera?.DevName, "未获取到设备名");
            labelSnValue.Text = GetText(_camera?.SN, "未获取到相机SN");
            labelIpValue.Text = GetText(_camera?.IP, "未获取到相机IP");
            labelImageModeValue.Text = GetImageModeText(_camera?.GrabConfig?.ImageMode);
            labelTriggerValue.Text = GetTriggerText(_camera?.GrabConfig?.TriggerSource);
            labelPointCloudValue.Text = _camera?.GrabConfig?.EnableNativePointCloudCache == true ? "启用" : "关闭";
            labelStatusValue.Text = _camera != null && _camera.IsOpen ? "已连接" : "未连接";
        }

        /// <summary>
        /// 获取显示文本，空值时使用默认文本。
        /// </summary>
        /// <param name="value">原始文本。</param>
        /// <param name="defaultText">默认文本。</param>
        /// <returns>显示文本。</returns>
        private static string GetText(string value, string defaultText)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultText : value;
        }

        /// <summary>
        /// 获取图像模式中文显示文本。
        /// </summary>
        /// <param name="imageMode">图像模式。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetImageModeText(Camera3DImageMode? imageMode)
        {
            switch (imageMode)
            {
                case Camera3DImageMode.Origin:
                    return "原始图";
                case Camera3DImageMode.PointCloud:
                    return "点云图";
                case Camera3DImageMode.Range:
                    return "深度图";
                case Camera3DImageMode.Intensity:
                    return "亮度图";
                default:
                    return "未设置";
            }
        }

        /// <summary>
        /// 获取触发源中文显示文本。
        /// </summary>
        /// <param name="triggerSource">触发源。</param>
        /// <returns>中文显示文本。</returns>
        private static string GetTriggerText(Camera3DTriggerSource? triggerSource)
        {
            switch (triggerSource)
            {
                case Camera3DTriggerSource.Auto:
                    return "自动触发";
                case Camera3DTriggerSource.Soft:
                    return "软件触发";
                case Camera3DTriggerSource.Line0:
                case Camera3DTriggerSource.Line1:
                case Camera3DTriggerSource.Line2:
                case Camera3DTriggerSource.Line3:
                    return $"外部触发({triggerSource})";
                default:
                    return "未设置";
            }
        }
    }
}
