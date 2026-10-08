using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>手动参数与一键学习控件共用的固定按钮布局。</summary>
    partial class RuntimeTuningWidget
    {
        /// <summary>可在设计器中查看的操作按钮。</summary>
        private Button actionButton;
        /// <summary>释放本控件拥有的字体和裁剪区域，不释放宿主控制器。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var oldRegion = Region;
                Region = null;
                oldRegion?.Dispose();
                ownedFont?.Dispose();
                ownedFont = null;
                controller = null;
            }
            base.Dispose(disposing);
        }
        /// <summary>设置中性背景、细边框和键盘可达的中文按钮。</summary>
        private void InitializeComponent()
        {
            actionButton = new Button();
            SuspendLayout();
            actionButton.Dock = DockStyle.Fill;
            actionButton.Margin = Padding.Empty;
            actionButton.Text = "手动参数";
            actionButton.FlatStyle = FlatStyle.Flat;
            actionButton.FlatAppearance.BorderSize = 1;
            actionButton.FlatAppearance.BorderColor = Color.FromArgb(76, 80, 85);
            actionButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 66, 69);
            actionButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 64, 40);
            actionButton.UseVisualStyleBackColor = false;
            actionButton.BackColor = Color.FromArgb(40, 40, 40);
            actionButton.ForeColor = Color.WhiteSmoke;
            actionButton.TextAlign = ContentAlignment.MiddleCenter;
            actionButton.AutoEllipsis = true;
            actionButton.Enabled = false;
            Controls.Add(actionButton);
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(160, 52);
            ResumeLayout(false);
        }
    }
}
