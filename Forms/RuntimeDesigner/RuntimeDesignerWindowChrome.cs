using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>复用独立窗口的原生移动、缩放和标题按钮行为，不创建固定控件。</summary>
    internal sealed class RuntimeDesignerWindowChrome : IDisposable
    {
        /// <summary>接收原生窗口操作的宿主。</summary>
        private readonly Form window;
        /// <summary>可拖动的标题区域。</summary>
        private readonly Control title;
        /// <summary>最小化按钮。</summary>
        private readonly Button minimize;
        /// <summary>最大化和还原按钮。</summary>
        private readonly Button maximize;
        /// <summary>关闭按钮。</summary>
        private readonly Button close;
        /// <summary>设计器拥有的中文悬停提示。</summary>
        private readonly ToolTip toolTip;
        /// <summary>由此辅助对象拥有的标准窗口图标。</summary>
        private readonly Image[] icons;
        /// <summary>避免重复释放图标和事件。</summary>
        private bool disposed;
        /// <summary>最近一次已处理的窗口状态，用于仅在离开普通状态时校正还原尺寸。</summary>
        private FormWindowState previousWindowState;

        /// <summary>连接设计器已有控件和原生窗口行为。</summary>
        public RuntimeDesignerWindowChrome(Form window, Control title, Button minimize, Button maximize, Button close, ToolTip toolTip)
        {
            this.window = window;
            this.title = title;
            this.minimize = minimize;
            this.maximize = maximize;
            this.close = close;
            this.toolTip = toolTip;
            previousWindowState = window.WindowState;
            icons = new[] { RuntimeDesignerIcons.Create(0xE921, Color.White), RuntimeDesignerIcons.Create(0xE922, Color.White),
                RuntimeDesignerIcons.Create(0xE923, Color.White), RuntimeDesignerIcons.Create(0xE8BB, Color.White) };
            minimize.Image = icons[0];
            close.Image = icons[3];
            minimize.Click += MinimizeClicked;
            maximize.Click += MaximizeClicked;
            close.Click += CloseClicked;
            title.MouseDown += TitleMouseDown;
            window.Resize += WindowResized;
            window.TextChanged += WindowTextChanged;
            UpdateState();
            title.Text = window.Text;
        }

        /// <summary>保留系统缩放和任务栏窗口操作，同时由客户端绘制全部外观。</summary>
        public static CreateParams ConfigureStyles(CreateParams parameters)
        {
            parameters.Style |= 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
            return parameters;
        }

        /// <summary>处理无边框窗口的非客户区消息；返回是否已经完成处理。</summary>
        public static bool ProcessMessage(Form window, ref Message message)
        {
            switch (message.Msg)
            {
                case 0x0083: // WM_NCCALCSIZE：系统边框全部交由客户端绘制。
                    message.Result = IntPtr.Zero;
                    return true;
                case 0x0086: // WM_NCACTIVATE：保留系统激活状态处理，但禁止覆盖自绘边缘。
                    // 官方约定 lParam=-1 时 DefWindowProc 不重画非客户区；最小化图标仍交给系统。
                    // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-ncactivate
                    if (window.WindowState != FormWindowState.Minimized) message.LParam = new IntPtr(-1);
                    return false;
                case 0x0085: // WM_NCPAINT：边缘已属于客户区，无需系统再画一层边框。
                    if (window.WindowState == FormWindowState.Minimized) return false;
                    message.Result = IntPtr.Zero;
                    return true;
                case 0x0084: // WM_NCHITTEST：只占用留白边缘，不影响内部控件。
                    if (window.WindowState != FormWindowState.Normal) return false;
                    long packed = message.LParam.ToInt64();
                    var point = window.PointToClient(new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff))));
                    int hit = HitTestEdge(window.ClientSize, point, Math.Max(6, window.Padding.Left));
                    if (hit == 1) return false;
                    message.Result = new IntPtr(hit);
                    return true;
                case 0x0024: // WM_GETMINMAXINFO：按窗口所在显示器的工作区最大化。
                    var screen = Screen.FromHandle(window.Handle);
                    var limits = (MinMaxInfo)Marshal.PtrToStructure(message.LParam, typeof(MinMaxInfo));
                    limits.MaxPosition = new Point(screen.WorkingArea.X - screen.Bounds.X, screen.WorkingArea.Y - screen.Bounds.Y);
                    limits.MaxSize = screen.WorkingArea.Size;
                    limits.MinTrackSize = window.MinimumSize;
                    limits.MaxTrackSize = new Size(Math.Max(SystemInformation.MaxWindowTrackSize.Width, window.MinimumSize.Width),
                        Math.Max(SystemInformation.MaxWindowTrackSize.Height, window.MinimumSize.Height));
                    Marshal.StructureToPtr(limits, message.LParam, false);
                    message.Result = IntPtr.Zero;
                    return true;
                case 0x0046: // WM_WINDOWPOSCHANGING：系统完成跨屏尺寸补偿后约束实际最大化边界。
                    ConstrainMaximizedPosition(window, message.LParam);
                    return false;
                case 0x00A3: // WM_NCLBUTTONDBLCLK：原生标题拖动后的第二次点击。
                    if (message.WParam.ToInt32() != 2) return false;
                    ToggleMaximized(window);
                    message.Result = IntPtr.Zero;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>仅校正最大化的最终原生位置，避免不同分辨率及DPI的副屏被系统再次放大。</summary>
        private static void ConstrainMaximizedPosition(Form window, IntPtr data)
        {
            if (data == IntPtr.Zero || !IsZoomed(window.Handle) || IsIconic(window.Handle)) return;
            var position = (WindowPosition)Marshal.PtrToStructure(data, typeof(WindowPosition));
            const uint noSize = 0x0001;
            const uint noMove = 0x0002;
            if ((position.Flags & (noSize | noMove)) == (noSize | noMove)) return;
            // MINMAXINFO 的尺寸仍会被窗口管理器按主副屏差异补偿；此处读取的是补偿后的提案。
            // 以提案位置选择目标屏幕，不能使用尚未移动的旧窗口位置。
            var proposed = new Rectangle((position.Flags & noMove) != 0 ? window.Left : position.X,
                (position.Flags & noMove) != 0 ? window.Top : position.Y,
                (position.Flags & noSize) != 0 ? window.Width : position.Width,
                (position.Flags & noSize) != 0 ? window.Height : position.Height);
            if (proposed.Width <= 0 || proposed.Height <= 0) return;
            Rectangle area = Screen.FromRectangle(proposed).WorkingArea;
            position.X = area.X;
            position.Y = area.Y;
            position.Width = area.Width;
            position.Height = area.Height;
            position.Flags &= ~(noSize | noMove);
            Marshal.StructureToPtr(position, data, false);
        }

        /// <summary>按边缘和角落返回系统缩放方向，内部区域保留普通客户区命中。</summary>
        private static int HitTestEdge(Size size, Point point, int border)
        {
            if (point.X < 0 || point.Y < 0 || point.X >= size.Width || point.Y >= size.Height) return 1;
            bool left = point.X < border;
            bool right = point.X >= size.Width - border;
            bool top = point.Y < border;
            bool bottom = point.Y >= size.Height - border;
            if (top) return left ? 13 : right ? 14 : 12;
            if (bottom) return left ? 16 : right ? 17 : 15;
            return left ? 10 : right ? 11 : 1;
        }

        /// <summary>同步外观，最大化时撤掉缩放留白以完整利用工作区。</summary>
        private void UpdateState()
        {
            FormWindowState currentState = window.WindowState;
            bool leavingNormal = previousWindowState == FormWindowState.Normal && currentState != FormWindowState.Normal;
            previousWindowState = currentState;
            if (leavingNormal)
            {
                // WinForms 默认缓存 ClientSize，并在还原时按 WS_THICKFRAME 加回边框。
                // 本窗口的 NCCALCSIZE 已去掉边框，因此通过公开 Bounds 接口将缓存标记为外部尺寸。
                // 非普通状态下设置 Bounds 只更新还原边界，不改变当前最大化或最小化的位置。
                window.Bounds = window.RestoreBounds;
            }
            bool maximized = window.WindowState == FormWindowState.Maximized;
            maximize.Image = icons[maximized ? 2 : 1];
            maximize.AccessibleName = maximized ? "还原" : "最大化";
            toolTip.SetToolTip(maximize, maximize.AccessibleName);
            if (window.WindowState == FormWindowState.Minimized) return;
            var padding = maximized ? Padding.Empty : new Padding(Math.Max(6, (int)Math.Round(6f * window.DeviceDpi / 96f)));
            if (window.Padding != padding) window.Padding = padding;
        }

        /// <summary>系统移动循环负责拖动、贴靠，以及从最大化状态拖动还原。</summary>
        private void TitleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (e.Clicks >= 2) { ToggleMaximized(window); return; }
            ReleaseCapture();
            Point position = title.PointToScreen(e.Location);
            SendMessage(window.Handle, 0x00A1, new IntPtr(2), new IntPtr(unchecked((position.Y << 16) | (position.X & 0xffff))));
        }

        /// <summary>切换最大化状态，保留系统维护的还原位置。</summary>
        private static void ToggleMaximized(Form window) => window.WindowState = window.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        /// <summary>最小化窗口到任务栏。</summary>
        private void MinimizeClicked(object sender, EventArgs e) => window.WindowState = FormWindowState.Minimized;
        /// <summary>执行最大化或还原。</summary>
        private void MaximizeClicked(object sender, EventArgs e) { ToggleMaximized(window); UpdateState(); }
        /// <summary>通过标准关闭流程保留宿主的脏数据确认和取消行为。</summary>
        private void CloseClicked(object sender, EventArgs e) => window.Close();
        /// <summary>外部窗口状态变化时更新图标和边缘留白。</summary>
        private void WindowResized(object sender, EventArgs e) => UpdateState();
        /// <summary>将窗体标题同步到自定义标题区域。</summary>
        private void WindowTextChanged(object sender, EventArgs e) => title.Text = window.Text;

        /// <summary>解除事件并释放图标，控件仍由各窗体设计器拥有。</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            minimize.Click -= MinimizeClicked;
            maximize.Click -= MaximizeClicked;
            close.Click -= CloseClicked;
            title.MouseDown -= TitleMouseDown;
            window.Resize -= WindowResized;
            window.TextChanged -= WindowTextChanged;
            minimize.Image = maximize.Image = close.Image = null;
            foreach (var icon in icons) icon.Dispose();
        }

        /// <summary>原生窗口尺寸约束，字段顺序与 MINMAXINFO 保持一致。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            /// <summary>系统保留位置。</summary>
            public Point Reserved;
            /// <summary>最大化外部尺寸。</summary>
            public Size MaxSize;
            /// <summary>最大化相对显示器的位置。</summary>
            public Point MaxPosition;
            /// <summary>最小拖动尺寸。</summary>
            public Size MinTrackSize;
            /// <summary>最大拖动尺寸。</summary>
            public Size MaxTrackSize;
        }

        /// <summary>WINDOWPOS 的原生布局，指针字段在64位进程中保持正确偏移。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPosition
        {
            /// <summary>被调整的窗口句柄。</summary>
            public IntPtr Handle;
            /// <summary>系统维护的层级插入位置。</summary>
            public IntPtr InsertAfter;
            /// <summary>目标横坐标。</summary>
            public int X;
            /// <summary>目标纵坐标。</summary>
            public int Y;
            /// <summary>目标宽度。</summary>
            public int Width;
            /// <summary>目标高度。</summary>
            public int Height;
            /// <summary>本次位置调整的原生标志。</summary>
            public uint Flags;
        }

        /// <summary>读取原生最大化状态，不依赖尚未收到Resize事件的托管状态。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr window);
        /// <summary>最小化期间保留系统安排，不把任务栏窗口拉回工作区。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr window);

        /// <summary>释放标题控件捕获，使系统接管窗口移动。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseCapture();
        /// <summary>将标题鼠标按下交给系统移动循环。</summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr data);
    }
}
