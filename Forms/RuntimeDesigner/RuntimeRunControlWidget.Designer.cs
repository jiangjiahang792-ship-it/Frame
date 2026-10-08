using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行控制的固定按钮布局，可在 WinForms 设计器中查看。</summary>
    partial class RuntimeRunControlWidget
    {
        /// <summary>组件容器。</summary>
        private System.ComponentModel.IContainer components;
        /// <summary>两个按钮保持相同比例的布局。</summary>
        private TableLayoutPanel buttons;
        /// <summary>单次运行按钮。</summary>
        private Button onceButton;
        /// <summary>循环运行和停止共用的按钮。</summary>
        private Button loopButton;
        /// <summary>图标按钮的中文提示。</summary>
        private ToolTip commandToolTip;
        /// <summary>释放仅由控件拥有的图标和组件，不释放宿主运行控制器。</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                onceButton.Image = null; loopButton.Image = null;
                onceIcon?.Dispose(); loopIcon?.Dispose(); stopIcon?.Dispose();
                var ownedRegion = Region;
                Region = null;
                ownedRegion?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
        /// <summary>初始化左右并排的两个原生按钮，支持键盘焦点和无障碍名称。</summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            commandToolTip = new ToolTip(components);
            commandToolTip.Active = false;
            commandToolTip.ShowAlways = false;
            commandToolTip.InitialDelay = 500;
            commandToolTip.ReshowDelay = 100;
            commandToolTip.AutoPopDelay = 2500;
            buttons = new TableLayoutPanel();
            onceButton = new Button();
            loopButton = new Button();
            SuspendLayout();
            buttons.Dock = DockStyle.Fill;
            buttons.Margin = Padding.Empty;
            buttons.Padding = new Padding(1);
            buttons.ColumnCount = 2;
            buttons.RowCount = 1;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            onceButton.Dock = DockStyle.Fill;
            onceButton.Margin = new Padding(1);
            onceButton.FlatStyle = FlatStyle.Flat;
            onceButton.FlatAppearance.BorderSize = 1;
            onceButton.FlatAppearance.BorderColor = Color.FromArgb(66, 68, 71);
            onceButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 66, 69);
            onceButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 64, 40);
            onceButton.UseVisualStyleBackColor = false;
            onceButton.BackColor = Color.FromArgb(40, 40, 40);
            onceButton.ForeColor = Color.WhiteSmoke;
            onceButton.AccessibleName = "单次运行";
            commandToolTip.SetToolTip(onceButton, "单次运行");
            loopButton.Dock = DockStyle.Fill;
            loopButton.Margin = new Padding(1);
            loopButton.FlatStyle = FlatStyle.Flat;
            loopButton.FlatAppearance.BorderSize = 1;
            loopButton.FlatAppearance.BorderColor = Color.FromArgb(66, 68, 71);
            loopButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 66, 69);
            loopButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(91, 64, 40);
            loopButton.UseVisualStyleBackColor = false;
            loopButton.BackColor = Color.FromArgb(40, 40, 40);
            loopButton.ForeColor = Color.WhiteSmoke;
            loopButton.AccessibleName = "循环运行";
            commandToolTip.SetToolTip(loopButton, "循环运行");
            buttons.Controls.Add(onceButton, 0, 0);
            buttons.Controls.Add(loopButton, 1, 0);
            Controls.Add(buttons);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(39, 39, 39);
            Size = new Size(128, 56);
            ResumeLayout(false);
        }
    }
}
