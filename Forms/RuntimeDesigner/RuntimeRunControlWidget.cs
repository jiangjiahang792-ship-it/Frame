using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>只负责交互反馈，实际执行由宿主注入的控制器完成。</summary>
    public partial class RuntimeRunControlWidget : UserControl
    {
        /// <summary>借用的宿主控制器，多个控件共享真实运行状态。</summary>
        private IRuntimeRunController controller;
        /// <summary>配置的根流程编号；空值表示全流程。</summary>
        private int? processId;
        /// <summary>单次或循环命令尚未返回，防止本控件重复启动。</summary>
        private bool pendingStart;
        /// <summary>停止请求未结束。</summary>
        private bool pendingStop;
        /// <summary>界面当前显示的命令，过期停止点击不得变成启动。</summary>
        private bool loopShowsStop;
        /// <summary>按下时锁定用户看到的命令，避免按下到松开之间刷新改变动作。</summary>
        private bool? pressedLoopStop;
        /// <summary>单次运行图标。</summary>
        private Bitmap onceIcon;
        /// <summary>循环运行图标。</summary>
        private Bitmap loopIcon;
        /// <summary>停止图标。</summary>
        private Bitmap stopIcon;
        /// <summary>最近生成图标的尺寸，避免刷新时重复分配位图。</summary>
        private int iconSize;
        /// <summary>布局指定的图标前景色。</summary>
        private Color iconColor = Color.WhiteSmoke;
        /// <summary>将命令异常交给宿主呈现，不吞掉执行失败。</summary>
        public event Action<string> CommandFailed;
        /// <summary>创建固定按钮，运行时才注入执行入口。</summary>
        public RuntimeRunControlWidget()
        {
            InitializeComponent();
            onceButton.Click += OnceClicked;
            loopButton.Click += LoopClicked;
            loopButton.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) pressedLoopStop = loopShowsStop; };
            loopButton.MouseUp += (s, e) => { if (!loopButton.ClientRectangle.Contains(e.Location)) pressedLoopStop = null; };
            loopButton.KeyDown += (s, e) => { if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && !pressedLoopStop.HasValue) pressedLoopStop = loopShowsStop; };
            loopButton.LostFocus += (s, e) => pressedLoopStop = null;
            onceButton.MouseEnter += EnableCommandToolTips;
            loopButton.MouseEnter += EnableCommandToolTips;
            onceButton.MouseLeave += DismissCommandToolTips;
            loopButton.MouseLeave += DismissCommandToolTips;
            onceButton.MouseDown += DismissCommandToolTips;
            loopButton.MouseDown += DismissCommandToolTips;
            onceButton.KeyDown += DismissCommandToolTips;
            loopButton.KeyDown += DismissCommandToolTips;
            onceButton.LostFocus += DismissCommandToolTips;
            loopButton.LostFocus += DismissCommandToolTips;
            SizeChanged += (s, e) => UpdateIcons();
            UpdateIcons();
            RefreshState();
        }
        /// <summary>绑定稳定流程编号，不因目标丢失回退到所有流程。</summary>
        public void Bind(IRuntimeRunController value, int? target)
        { controller = value; processId = target; RefreshState(); }
        /// <summary>正式按钮沿用布局外观，停止态仍保留橙色强调。</summary>
        public void ApplyAppearance(Color background, Color foreground)
        {
            BackColor = onceButton.BackColor = loopButton.BackColor = background;
            onceButton.ForeColor = loopButton.ForeColor = foreground;
            if (iconColor == foreground) return;
            iconColor = foreground; iconSize = 0; UpdateIcons();
        }
        /// <summary>尺寸变化时更新清晰图标，最小控件也不会被固定图标挤压。</summary>
        private void UpdateIcons()
        {
            int size = Math.Max(8, Math.Min(40, Math.Min(Width / 2 - 10, Height - 10)));
            if (size == iconSize) return;
            iconSize = size;
            onceButton.Image = null; loopButton.Image = null;
            onceIcon?.Dispose(); loopIcon?.Dispose(); stopIcon?.Dispose();
            onceIcon = RuntimeDesignerIcons.Create(0xE768, iconColor, size);
            loopIcon = RuntimeDesignerIcons.Create(0xE8EE, iconColor, size);
            stopIcon = RuntimeDesignerIcons.Create(0xE71A, Color.FromArgb(255, 128, 0), size);
            RefreshState();
        }
        /// <summary>根据真实执行状态更新按钮，外部启动或停止也能正确反映。</summary>
        public void RefreshState()
        {
            if (IsDisposed || Disposing) return;
            bool running = controller != null && controller.IsRunning;
            bool stopping = pendingStop || (controller != null && controller.IsStopping);
            bool targetValid = controller != null && (!processId.HasValue || controller.GetProcesses().Any(p => p.Id == processId.Value));
            bool onceEnabled = targetValid && !running && !stopping && !pendingStart;
            bool loopEnabled = !stopping && (running || (targetValid && !pendingStart));
            if (onceButton.Enabled != onceEnabled) onceButton.Enabled = onceEnabled;
            if (loopButton.Enabled != loopEnabled) loopButton.Enabled = loopEnabled;
            if (!ReferenceEquals(onceButton.Image, onceIcon)) onceButton.Image = onceIcon;
            var loopImage = running || stopping ? stopIcon : loopIcon;
            if (!ReferenceEquals(loopButton.Image, loopImage)) loopButton.Image = loopImage;
            loopShowsStop = running || stopping;
            string action = stopping ? "正在停止" : running ? "停止运行" : "循环运行";
            if (loopButton.AccessibleName != action) loopButton.AccessibleName = action;
            Color border = running || stopping ? Color.FromArgb(255, 128, 0) : Color.FromArgb(66, 68, 71);
            if (loopButton.FlatAppearance.BorderColor != border) loopButton.FlatAppearance.BorderColor = border;
            UpdateCommandToolTip(loopButton, targetValid || running ? action : "指定流程已失效");
            UpdateCommandToolTip(onceButton, targetValid ? "单次运行" : "指定流程已失效");
        }

        /// <summary>只在文案变化时更新提示；重复设置相同文案仍会发送原生提示更新消息。</summary>
        private void UpdateCommandToolTip(Control control, string text)
        {
            if (commandToolTip.GetToolTip(control) == text) return;
            commandToolTip.Hide(control);
            commandToolTip.SetToolTip(control, text);
        }

        /// <summary>提示只在重新进入按钮且未按住鼠标时恢复，不由状态轮询重新激活。</summary>
        private void EnableCommandToolTips(object sender, EventArgs e)
        {
            if (!IsDisposed && !Disposing && MouseButtons == MouseButtons.None) commandToolTip.Active = true;
        }

        /// <summary>离开、点击或失焦立即关闭提示，不改变按下锁定的启动停止命令。</summary>
        private void DismissCommandToolTips(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !commandToolTip.Active) return;
            commandToolTip.Active = false;
            commandToolTip.Hide(onceButton);
            commandToolTip.Hide(loopButton);
        }
        /// <summary>单次运行复用相同的命令异常和状态处理。</summary>
        private async void OnceClicked(object sender, EventArgs e) { DismissCommandToolTips(sender, e); await StartAsync(false); }
        /// <summary>运行中点击同一按钮请求停止，空闲时启动循环。</summary>
        private async void LoopClicked(object sender, EventArgs e)
        {
            bool stopRequested = pressedLoopStop ?? loopShowsStop;
            pressedLoopStop = null;
            DismissCommandToolTips(sender, e);
            if (controller == null || pendingStop || controller.IsStopping) return;
            if (!stopRequested) { await StartAsync(true); return; }
            if (!controller.IsRunning) { RefreshState(); return; }
            pendingStop = true;
            RefreshState();
            try { await controller.StopAsync(); }
            catch (Exception ex) { if (!IsDisposed) CommandFailed?.Invoke(ex.Message); }
            finally { pendingStop = false; if (!IsDisposed) RefreshState(); }
        }
        /// <summary>发起一次执行，等待结束期间允许通过右按钮停止，不重复发出启动命令。</summary>
        private async Task StartAsync(bool continuous)
        {
            if (controller == null || pendingStart || pendingStop || controller.IsRunning || controller.IsStopping) return;
            pendingStart = true;
            try
            {
                Task run = controller.RunAsync(processId, continuous);
                RefreshState();
                await run;
            }
            catch (Exception ex) { if (!IsDisposed) CommandFailed?.Invoke(ex.Message); }
            finally { pendingStart = false; if (!IsDisposed) RefreshState(); }
        }
    }

    /// <summary>编辑画布中的无副作用外观，真实运行按钮使用同一组内置图标。</summary>
    internal sealed class RuntimeRunControlRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>对应运行控制组件。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.RunControl;
        /// <summary>在固定的两列按钮范围内居中绘制图标。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            int size = Math.Max(8, Math.Min(40, Math.Min(element.Width / 2 - 10, element.Height - 10)));
            for (int index = 0; index < 2; index++)
            {
                int left = index * element.Width / 2;
                using (var pen = new Pen(Color.FromArgb(66, 68, 71)))
                    graphics.DrawRectangle(pen, left + 2, 2, element.Width / 2 - 5, element.Height - 5);
                using (var icon = RuntimeDesignerIcons.Create(index == 0 ? 0xE768 : 0xE8EE, RuntimeLayoutSerializer.ParseColor(element.Foreground), size))
                    graphics.DrawImage(icon, left + (element.Width / 2 - size) / 2, (element.Height - size) / 2, size, size);
            }
        }
    }
}
