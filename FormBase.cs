using System;
using System.Drawing;
using System.IO;
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

        private bool dragging = false;
        private Point offset;
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
            labelMaxBox.Text = WindowState == FormWindowState.Maximized ? "🗗" : "🗖";
            MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
            FormBorderStyle = FormBorderStyle.None; // 去掉默认边框
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
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
            if (this.WindowState == FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Maximized;
                labelMaxBox.Text = "🗗"; // 可选：还原图标
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
                labelMaxBox.Text = "🗖"; // 可选：最大化图标
            }
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
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.WindowState = FormWindowState.Maximized;
                    labelMaxBox.Text = "🗗"; // 可选：还原图标
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                    labelMaxBox.Text = "🗖"; // 可选：最大化图标
                }
            }
        }
    }
}
