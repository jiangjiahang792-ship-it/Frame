using System;
using WeifenLuo.WinFormsUI.Docking;
using TDJS_Vision.Device._3D;
using TDJS_Vision.Node._1_Acquisition.ImageShow3D;

namespace TDJS_Vision.Forms.ImageViewer
{
    /// <summary>
    /// 单个 3D 图像显示窗口，用于显示 3D 相机点云或深度投影。
    /// </summary>
    public partial class FrmSingle3DImage : DockContent
    {
        /// <summary>
        /// 窗口内部键名。
        /// </summary>
        public string FormName;

        /// <summary>
        /// 创建 3D 图像显示窗口。
        /// </summary>
        /// <param name="name">窗口名称。</param>
        public FrmSingle3DImage(string name)
        {
            InitializeComponent();
            FormName = NormalizeWindowKey(name);
            Text = GetWindowDisplayName(FormName);
            NodeImageShow3D.Image3DShowChanged += NodeImageShow3D_Image3DShowChanged;
            NodeImageShow3D.Image3DShowWindowNameChanged += NodeImageShow3D_Image3DShowWindowNameChanged;
            FormClosed += FrmSingle3DImage_FormClosed;
        }

        /// <summary>
        /// 获取 3D 图像窗口键名。
        /// </summary>
        /// <param name="index">窗口序号。</param>
        /// <returns>窗口键名。</returns>
        public static string GetWindowKey(int index)
        {
            return "Image3DWindow" + index;
        }

        /// <summary>
        /// 规范化 3D 图像窗口键名。
        /// </summary>
        /// <param name="windowName">窗口名称。</param>
        /// <returns>规范化后的窗口键名。</returns>
        public static string NormalizeWindowKey(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
                return string.Empty;

            if (windowName.StartsWith("Image3DWindow", StringComparison.OrdinalIgnoreCase))
                return windowName;

            bool isWindowName = windowName.StartsWith("3D图像窗口", StringComparison.OrdinalIgnoreCase);
            if (!isWindowName)
                return windowName;

            string numberText = string.Empty;
            foreach (char c in windowName)
            {
                if (char.IsDigit(c))
                    numberText += c;
            }

            if (int.TryParse(numberText, out int index) && index > 0)
                return GetWindowKey(index);

            return windowName;
        }

        /// <summary>
        /// 获取窗口显示名称。
        /// </summary>
        /// <param name="windowName">窗口键名。</param>
        /// <returns>窗口显示名称。</returns>
        public static string GetWindowDisplayName(string windowName)
        {
            string key = NormalizeWindowKey(windowName);
            if (key.StartsWith("Image3DWindow", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(key.Substring("Image3DWindow".Length), out int index))
            {
                return $"3D图像窗口{index}";
            }

            return windowName;
        }

        /// <summary>
        /// 设置当前窗口显示的 3D 帧。
        /// </summary>
        /// <param name="frameData">3D 帧数据。</param>
        public void SetFrame(Camera3DFrameData frameData)
        {
            if (IsDisposed)
                return;

            Action updateFrame = () => pointCloudPreviewControl.SetFrame(frameData);
            if (InvokeRequired)
                BeginInvoke(updateFrame);
            else
                updateFrame();
        }

        /// <summary>
        /// 3D 显示节点推送帧数据事件。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">3D 显示参数。</param>
        private void NodeImageShow3D_Image3DShowChanged(object sender, Image3DShowParam e)
        {
            if (e == null || FormName != NormalizeWindowKey(e.WinName))
                return;

            SetFrame(e.FrameData);
        }

        /// <summary>
        /// 3D 显示节点绑定窗口名称变化事件。
        /// </summary>
        /// <param name="process">流程。</param>
        /// <param name="winName">窗口名称。</param>
        private void NodeImageShow3D_Image3DShowWindowNameChanged(Process process, string winName)
        {
            if (FormName != NormalizeWindowKey(winName) || process == null)
                return;

            Action updateText = () => Text = $"{process.ProcessName}-3D";
            if (InvokeRequired)
                BeginInvoke(updateText);
            else
                updateText();
        }

        /// <summary>
        /// 窗口关闭时取消事件订阅。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void FrmSingle3DImage_FormClosed(object sender, EventArgs e)
        {
            NodeImageShow3D.Image3DShowChanged -= NodeImageShow3D_Image3DShowChanged;
            NodeImageShow3D.Image3DShowWindowNameChanged -= NodeImageShow3D_Image3DShowWindowNameChanged;
        }

        /// <summary>
        /// 关闭窗口时改为隐藏，保持显示窗口实例可复用。
        /// </summary>
        /// <param name="e">关闭参数。</param>
        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnFormClosing(e);
        }
    }
}
