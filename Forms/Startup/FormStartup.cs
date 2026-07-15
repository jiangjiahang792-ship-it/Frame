using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Startup;

namespace TDJS_Vision.Forms.Startup
{
    /// <summary>
    /// 显示软件启动状态、真实加载对象和关键错误处理入口。
    /// </summary>
    internal partial class FormStartup : Form
    {
        /// <summary>
        /// 机器人图片初始纵向位置。
        /// </summary>
        private int mascotBaseTop;

        /// <summary>
        /// 当前动画帧序号。
        /// </summary>
        private int animationFrame;

        /// <summary>
        /// 用户单击“重试”按钮时触发。
        /// </summary>
        public event EventHandler RetryRequested;

        /// <summary>
        /// 用户单击“退出软件”按钮时触发。
        /// </summary>
        public event EventHandler ExitRequested;

        /// <summary>
        /// 初始化轻盈科技风格启动窗体。
        /// </summary>
        public FormStartup()
        {
            InitializeComponent();
            ConfigureAnimationOverlays();
            DoubleBuffered = true;
            mascotBaseTop = pictureBoxMascot.Top;
        }

        /// <summary>
        /// 将天线呼吸灯裁剪为圆形，使叠加动画与机器人图像自然融合。
        /// </summary>
        private void ConfigureAnimationOverlays()
        {
            using (var antennaPath = new GraphicsPath())
            {
                antennaPath.AddEllipse(panelAntennaGlow.ClientRectangle);
                panelAntennaGlow.Region = new Region(antennaPath);
            }

            panelBlinkMask.BringToFront();
            panelAntennaGlow.BringToFront();
        }

        /// <summary>
        /// 使用最新进度快照更新启动页。
        /// </summary>
        /// <param name="progress">最新启动进度。</param>
        public void UpdateProgress(StartupProgressInfo progress)
        {
            if (progress == null)
                return;

            labelStatus.Text = string.IsNullOrWhiteSpace(progress.StageName)
                ? "正在启动软件……"
                : progress.StageName;
            labelDetail.Text = progress.GetDetailText();
            labelPercent.Text = $"{progress.Percentage}%";

            int availableWidth = Math.Max(0, panelProgressTrack.ClientSize.Width);
            panelProgressValue.Width = (int)Math.Round(availableWidth * progress.Percentage / 100D);
            panelProgressValue.Visible = progress.Percentage > 0;
        }

        /// <summary>
        /// 显示关键启动失败，并允许用户重试或退出软件。
        /// </summary>
        /// <param name="message">适合用户查看的简体中文错误信息。</param>
        public void ShowFailure(string message)
        {
            timerAnimation.Stop();
            labelStatus.Text = "软件启动失败";
            labelDetail.Text = "请检查错误信息后重试，或退出软件。";
            labelError.Text = string.IsNullOrWhiteSpace(message) ? "启动初始化失败。" : message;
            panelError.Visible = true;
            panelError.BringToFront();
        }

        /// <summary>
        /// 恢复启动动画并隐藏上一次失败信息。
        /// </summary>
        public void ResetForRetry()
        {
            panelError.Visible = false;
            labelStatus.Text = "正在重新加载……";
            labelDetail.Text = "正在准备运行环境";
            timerAnimation.Start();
        }

        /// <summary>
        /// 绘制浅蓝白渐变背景和机器人呼吸光晕。
        /// </summary>
        /// <param name="e">绘制事件参数。</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var backgroundBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(250, 253, 255),
                Color.FromArgb(235, 242, 255),
                35F))
            {
                e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
            }

            int glowAlpha = 22 + (int)(10 * (Math.Sin(animationFrame * 0.08D) + 1D));
            using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 21, 151, 218)))
            {
                Rectangle glowBounds = new Rectangle(
                    pictureBoxMascot.Left - 25,
                    pictureBoxMascot.Top + pictureBoxMascot.Height - 42,
                    pictureBoxMascot.Width + 50,
                    52);
                e.Graphics.FillEllipse(glowBrush, glowBounds);
            }
        }

        /// <summary>
        /// 更新机器人浮动、眨眼和天线呼吸灯动画。
        /// </summary>
        /// <param name="sender">动画定时器。</param>
        /// <param name="e">事件参数。</param>
        private void TimerAnimation_Tick(object sender, EventArgs e)
        {
            animationFrame = (animationFrame + 1) % 3600;
            int offset = (int)Math.Round(Math.Sin(animationFrame * 0.10D) * 6D);
            pictureBoxMascot.Top = mascotBaseTop + offset;
            panelBlinkMask.Top = pictureBoxMascot.Top + 116;
            panelAntennaGlow.Top = pictureBoxMascot.Top + 45;

            int blinkFrame = animationFrame % 125;
            panelBlinkMask.Visible = blinkFrame >= 116 && blinkFrame <= 121;

            int pulse = 170 + (int)Math.Round((Math.Sin(animationFrame * 0.09D) + 1D) * 35D);
            panelAntennaGlow.BackColor = Color.FromArgb(Math.Min(240, pulse), 75, 67);
            Invalidate();
        }

        /// <summary>
        /// 将重试按钮操作转发给启动控制器。
        /// </summary>
        /// <param name="sender">重试按钮。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonRetry_Click(object sender, EventArgs e)
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 将退出按钮操作转发给启动控制器。
        /// </summary>
        /// <param name="sender">退出按钮。</param>
        /// <param name="e">事件参数。</param>
        private void ButtonExit_Click(object sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
