using Logger;
using System;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource3D
{
    /// <summary>
    /// 3D 图像源节点参数窗体，用于选择 3D 相机和配置取帧参数。
    /// </summary>
    public partial class ParamFormImageSource3D : FormBase, INodeParamForm
    {
        /// <summary>
        /// 当前参数窗体所属节点。
        /// </summary>
        private NodeBase _node;

        /// <summary>
        /// 节点运行参数。
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// 创建 3D 图像源参数窗体。
        /// </summary>
        public ParamFormImageSource3D()
        {
            InitializeComponent();
            comboBoxImageMode.SelectedIndex = 0;
            RefreshCameraList(string.Empty);
        }

        /// <summary>
        /// 设置参数窗体所属节点。
        /// </summary>
        /// <param name="node">所属节点。</param>
        public void SetNodeBelong(NodeBase node)
        {
            _node = node;
        }

        /// <summary>
        /// 将反序列化参数恢复到窗体。
        /// </summary>
        public void SetParam2Form()
        {
            if (!(Params is NodeParamImageSource3D param))
                return;

            RefreshCameraList(param.CameraName);
            comboBoxImageMode.Text = GetImageModeDisplayText(param.ImageMode);
            numericUpDownTimeOut.Value = ClampDecimal(param.TimeOut, numericUpDownTimeOut.Minimum, numericUpDownTimeOut.Maximum);
            numericUpDownMaxPointCount.Value = ClampDecimal(param.MaxDisplayPointCount, numericUpDownMaxPointCount.Minimum, numericUpDownMaxPointCount.Maximum);
            checkBoxAutoOpen.Checked = param.AutoOpenCamera;
            checkBoxAutoStartGrabbing.Checked = param.AutoStartGrabbing;
            checkBoxNativeCache.Checked = param.EnableNativePointCloudCache;
            checkBoxManagedPointCloud.Checked = param.BuildManagedPointCloudFallback;
            param.Camera = FindCameraByName(param.CameraName);
        }

        /// <summary>
        /// 刷新按钮单击事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            RefreshCameraList(comboBoxCamera.Text);
        }

        /// <summary>
        /// 保存按钮单击事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>
        /// 保存当前窗体参数。
        /// </summary>
        /// <returns>保存成功返回 true。</returns>
        private bool SaveParams()
        {
            if (string.IsNullOrWhiteSpace(comboBoxCamera.Text) || comboBoxCamera.Text == "[未设置]")
            {
                MessageBoxTD.Show("3D相机为空！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LogHelper.AddLog(MsgLevel.Fatal, "3D图像源未选择3D相机！", true);
                return false;
            }

            I3DCamera camera = FindCameraByName(comboBoxCamera.Text);
            if (camera == null)
            {
                MessageBoxTD.Show("未找到选择的3D相机！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LogHelper.AddLog(MsgLevel.Fatal, $"未找到3D相机：{comboBoxCamera.Text}", true);
                return false;
            }

            NodeParamImageSource3D param = new NodeParamImageSource3D
            {
                Camera = camera,
                CameraName = comboBoxCamera.Text,
                ImageMode = ParseImageMode(comboBoxImageMode.Text),
                TimeOut = (uint)numericUpDownTimeOut.Value,
                AutoOpenCamera = checkBoxAutoOpen.Checked,
                AutoStartGrabbing = checkBoxAutoStartGrabbing.Checked,
                EnableNativePointCloudCache = checkBoxNativeCache.Checked,
                BuildManagedPointCloudFallback = checkBoxManagedPointCloud.Checked,
                MaxDisplayPointCount = (int)numericUpDownMaxPointCount.Value
            };

            Params = param;
            return true;
        }

        /// <summary>
        /// 刷新 3D 相机下拉框。
        /// </summary>
        /// <param name="selectedName">希望选中的相机名称。</param>
        private void RefreshCameraList(string selectedName)
        {
            string currentName = string.IsNullOrWhiteSpace(selectedName) ? comboBoxCamera.Text : selectedName;
            comboBoxCamera.Items.Clear();
            comboBoxCamera.Items.Add("[未设置]");

            foreach (I3DCamera camera in Solution.Instance.Camera3DDevices)
            {
                if (!string.IsNullOrWhiteSpace(camera.UserDefinedName))
                    comboBoxCamera.Items.Add(camera.UserDefinedName);
            }

            int index = comboBoxCamera.Items.IndexOf(currentName);
            comboBoxCamera.SelectedIndex = index == -1 ? 0 : index;
        }

        /// <summary>
        /// 根据相机名称查找 3D 相机对象。
        /// </summary>
        /// <param name="cameraName">相机名称。</param>
        /// <returns>3D 相机对象。</returns>
        private static I3DCamera FindCameraByName(string cameraName)
        {
            foreach (I3DCamera camera in Solution.Instance.Camera3DDevices)
            {
                if (camera.UserDefinedName == cameraName)
                    return camera;
            }

            return null;
        }

        /// <summary>
        /// 获取图像模式显示文本。
        /// </summary>
        /// <param name="imageMode">图像模式。</param>
        /// <returns>显示文本。</returns>
        private static string GetImageModeDisplayText(Camera3DImageMode imageMode)
        {
            switch (imageMode)
            {
                case Camera3DImageMode.Origin:
                    return "原始图";
                case Camera3DImageMode.PointCloud:
                    return "点云图";
                case Camera3DImageMode.Intensity:
                    return "亮度图";
                case Camera3DImageMode.Range:
                default:
                    return "深度图";
            }
        }

        /// <summary>
        /// 将图像模式显示文本解析为枚举。
        /// </summary>
        /// <param name="text">显示文本。</param>
        /// <returns>图像模式。</returns>
        private static Camera3DImageMode ParseImageMode(string text)
        {
            switch (text)
            {
                case "原始图":
                    return Camera3DImageMode.Origin;
                case "点云图":
                    return Camera3DImageMode.PointCloud;
                case "亮度图":
                    return Camera3DImageMode.Intensity;
                case "深度图":
                default:
                    return Camera3DImageMode.Range;
            }
        }

        /// <summary>
        /// 将数值限制到控件允许范围内。
        /// </summary>
        /// <param name="value">原始数值。</param>
        /// <param name="minimum">最小值。</param>
        /// <param name="maximum">最大值。</param>
        /// <returns>限制后的数值。</returns>
        private static decimal ClampDecimal(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
        }
    }
}
