using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TDJS_Vision
{
    /// <summary>
    /// 这个类是为了替换Form不能更改标题栏背景颜色而定义的
    /// 提供一个基础窗体类，包含自定义标题栏和拖动功能。
    /// </summary>
    public partial class FormBase : Form
    {
        private const int ResizeBorderWidth = 8;
        /// <summary>
        /// Windows 请求窗口最大/最小尺寸信息的消息。
        /// </summary>
        private const int WmGetMinMaxInfo = 0x0024;
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        /// <summary>
        /// 标题栏是否正在拖动。
        /// </summary>
        private bool dragging = false;

        /// <summary>
        /// 标题栏拖动时的鼠标偏移。
        /// </summary>
        private Point offset;

        /// <summary>
        /// 是否正在应用最大化边界，避免 Resize 与消息处理递归。
        /// </summary>
        private bool _applyingMaximizedBounds;

        /// <summary>
        /// Win32 POINT 结构，用于 WM_GETMINMAXINFO。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            /// <summary>
            /// X 坐标。
            /// </summary>
            public int X;

            /// <summary>
            /// Y 坐标。
            /// </summary>
            public int Y;
        }

        /// <summary>
        /// Win32 MINMAXINFO 结构，用于控制无边框窗体最大化尺寸。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            /// <summary>
            /// 保留字段。
            /// </summary>
            public NativePoint Reserved;

            /// <summary>
            /// 最大化后的尺寸。
            /// </summary>
            public NativePoint MaxSize;

            /// <summary>
            /// 最大化后的左上角位置。
            /// </summary>
            public NativePoint MaxPosition;

            /// <summary>
            /// 最小拖拽尺寸。
            /// </summary>
            public NativePoint MinTrackSize;

            /// <summary>
            /// 最大拖拽尺寸。
            /// </summary>
            public NativePoint MaxTrackSize;
        }

        /// <summary>
        /// 初始化基础窗体。
        /// </summary>
        public FormBase()
        {
            InitializeComponent();
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 确保此时 Icon 已经被子类设置
            pictureBoxIcon.Image = Properties.Resources.TDJSLog;
            using (MemoryStream ms = new MemoryStream(Properties.Resources.TDJSIco))
            {
                this.Icon = new Icon(ms);
            }
            labelTitle.Text = Text; // 设置标题栏文本为窗体标题
            labelMinBox.Visible = MinimizeBox;
            labelMaxBox.Visible = MaximizeBox;
            FormBorderStyle = FormBorderStyle.None; // 去掉默认边框
            ApplyMaximizedBounds();
            UpdateMaximizeButtonText();
        }

        /// <summary>
        /// 窗体首次显示后再次刷新最大化边界。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyMaximizedBounds();
            UpdateMaximizeButtonText();
        }

        /// <summary>
        /// 窗体尺寸变化时同步最大化按钮，并处理最小化恢复后的边界刷新。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateMaximizeButtonText();

            if (WindowState != FormWindowState.Maximized || !IsHandleCreated || IsDisposed)
                return;

            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                    ApplyMaximizedBounds();
            }));
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WmGetMinMaxInfo)
            {
                ApplyMaximizedBoundsToMessage(m.LParam);
                return;
            }

            if (m.Msg != WmNcHitTest ||
                WindowState == FormWindowState.Maximized ||
                FormBorderStyle != FormBorderStyle.None)
            {
                return;
            }

            Point cursor = PointToClient(GetPointFromLParam(m.LParam));
            bool left = cursor.X <= ResizeBorderWidth;
            bool right = cursor.X >= ClientSize.Width - ResizeBorderWidth;
            bool top = cursor.Y <= ResizeBorderWidth;
            bool bottom = cursor.Y >= ClientSize.Height - ResizeBorderWidth;

            if (left && top)
                m.Result = (IntPtr)HtTopLeft;
            else if (right && top)
                m.Result = (IntPtr)HtTopRight;
            else if (left && bottom)
                m.Result = (IntPtr)HtBottomLeft;
            else if (right && bottom)
                m.Result = (IntPtr)HtBottomRight;
            else if (left)
                m.Result = (IntPtr)HtLeft;
            else if (right)
                m.Result = (IntPtr)HtRight;
            else if (top)
                m.Result = (IntPtr)HtTop;
            else if (bottom)
                m.Result = (IntPtr)HtBottom;
            else if (m.Result == (IntPtr)HtClient)
                m.Result = (IntPtr)HtClient;
        }

        private static Point GetPointFromLParam(IntPtr lParam)
        {
            int value = unchecked((int)lParam.ToInt64());
            int x = (short)(value & 0xFFFF);
            int y = (short)((value >> 16) & 0xFFFF);
            return new Point(x, y);
        }

        /// <summary>
        /// 应用最大化边界；有 owner 时覆盖 owner，无 owner 时使用当前屏幕工作区。
        /// </summary>
        protected void ApplyMaximizedBounds()
        {
            if (_applyingMaximizedBounds || IsDisposed)
                return;

            _applyingMaximizedBounds = true;
            try
            {
                Rectangle targetBounds = GetMaximizedTargetBounds();
                if (targetBounds.Width <= 0 || targetBounds.Height <= 0)
                    return;

                if (MaximizedBounds != targetBounds)
                    MaximizedBounds = targetBounds;
            }
            finally
            {
                _applyingMaximizedBounds = false;
            }
        }

        /// <summary>
        /// 获取最大化目标区域；弹窗优先覆盖 owner，主窗体使用当前屏幕工作区。
        /// </summary>
        /// <returns>最大化目标区域。</returns>
        protected virtual Rectangle GetMaximizedTargetBounds()
        {
            if (Owner != null && !Owner.IsDisposed && Owner.Visible && Owner.WindowState != FormWindowState.Minimized)
            {
                return Owner.Bounds;
            }

            Screen screen = IsHandleCreated ? Screen.FromHandle(Handle) : Screen.FromControl(this);
            return screen.WorkingArea;
        }

        /// <summary>
        /// 将最大化目标区域写入 WM_GETMINMAXINFO，解决无边框窗体恢复最大化后的尺寸漂移。
        /// </summary>
        /// <param name="lParam">消息参数指针。</param>
        private void ApplyMaximizedBoundsToMessage(IntPtr lParam)
        {
            Rectangle targetBounds = GetMaximizedTargetBounds();
            if (targetBounds.Width <= 0 || targetBounds.Height <= 0)
                return;

            Rectangle monitorBounds = Screen.FromRectangle(targetBounds).Bounds;
            var minMaxInfo = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));
            minMaxInfo.MaxPosition.X = targetBounds.Left - monitorBounds.Left;
            minMaxInfo.MaxPosition.Y = targetBounds.Top - monitorBounds.Top;
            minMaxInfo.MaxSize.X = targetBounds.Width;
            minMaxInfo.MaxSize.Y = targetBounds.Height;
            minMaxInfo.MaxTrackSize.X = Math.Max(minMaxInfo.MaxTrackSize.X, targetBounds.Width);
            minMaxInfo.MaxTrackSize.Y = Math.Max(minMaxInfo.MaxTrackSize.Y, targetBounds.Height);

            if (!MinimumSize.IsEmpty)
            {
                minMaxInfo.MinTrackSize.X = Math.Max(minMaxInfo.MinTrackSize.X, MinimumSize.Width);
                minMaxInfo.MinTrackSize.Y = Math.Max(minMaxInfo.MinTrackSize.Y, MinimumSize.Height);
            }

            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        /// <summary>
        /// 同步自定义最大化按钮图标文本。
        /// </summary>
        private void UpdateMaximizeButtonText()
        {
            if (labelMaxBox != null)
                labelMaxBox.Text = WindowState == FormWindowState.Maximized ? "🗗" : "🗖";
        }

        /// <summary>
        /// 切换最大化与还原状态。
        /// </summary>
        private void ToggleMaximizedState()
        {
            if (WindowState == FormWindowState.Normal)
            {
                ApplyMaximizedBounds();
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                WindowState = FormWindowState.Normal;
            }

            UpdateMaximizeButtonText();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (labelTitle != null)
                labelTitle.Text = Text;
        }
        /// <summary>
        /// 最小化窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        /// <summary>
        /// 最大化或还原窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_Click(object sender, EventArgs e)
        {
            ToggleMaximizedState();
        }
        /// <summary>
        /// 关闭窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label3_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        /// <summary>
        /// 鼠标在标题栏按下事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tableLayoutPanel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                offset = new Point(e.X, e.Y);
            }
        }
        /// <summary>
        /// 鼠标在标题栏移动事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tableLayoutPanel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point currentScreenPos = PointToScreen(e.Location);
                Location = new Point(currentScreenPos.X - offset.X, currentScreenPos.Y - offset.Y);
            }
        }
        /// <summary>
        /// 鼠标在标题栏松开事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tableLayoutPanel1_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
        /// <summary>
        /// 双击标题栏切换窗口状态（最大化/还原）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tableLayoutPanel1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                if(!labelMaxBox.Visible)
                    return;
                ToggleMaximizedState();
            }
        }
    }
}
