using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>共用布局渲染器的运行窗口；编辑预览使用模拟入口，正式宿主显式注入框架入口。</summary>
    public partial class RuntimeInterfacePreviewForm : Form
    {
        /// <summary>与编辑器一致的窗口外观交互。</summary>
        private readonly RuntimeDesignerWindowChrome windowChrome;
        /// <summary>可选的实时显示控制器，静态预览不创建订阅。</summary>
        private RuntimeLiveViewController liveController;
        /// <summary>宿主用于判断当前方案是否仍匹配此窗口。</summary>
        private Func<bool> isCurrent;
        /// <summary>正式运行窗体的独立名称，刷新状态不覆盖它。</summary>
        private string runtimeTitle = "运行界面";
        /// <summary>宿主在重复打开时同步判断旧窗口是否仍属于当前配置。</summary>
        public bool IsCurrentDocument => isCurrent == null || isCurrent();
        /// <summary>与画布共用的布局快照。</summary>
        private RuntimeLayoutDocument layout;
        /// <summary>当前页面的原生运行按钮，按控件编号对应布局。</summary>
        private readonly Dictionary<string, RuntimeRunControlWidget> runWidgets = new Dictionary<string, RuntimeRunControlWidget>();
        /// <summary>各日志框独立订阅、显示和清空，关闭窗口时退订。</summary>
        private readonly Dictionary<string, RuntimeLogWidget> logWidgets = new Dictionary<string, RuntimeLogWidget>();
        /// <summary>按控件编号保存的调参按钮；正式宿主才注入可执行入口。</summary>
        private readonly Dictionary<string, RuntimeTuningWidget> tuningWidgets = new Dictionary<string, RuntimeTuningWidget>();
        /// <summary>只拥有编辑预览的模拟执行入口，不拥有生产控制器。</summary>
        private RuntimePreviewRunController previewRunController;
        /// <summary>设计器默认构造函数。</summary>
        public RuntimeInterfacePreviewForm() : this(new RuntimeLayoutDocument()) { }
        /// <summary>创建不可编辑的文档快照预览。</summary>
        public RuntimeInterfacePreviewForm(RuntimeLayoutDocument document)
        {
            InitializeComponent();
            windowChrome = new RuntimeDesignerWindowChrome(this, titleLabel, minimizeButton, maximizeButton, closeButton, windowToolTip);
            var session = new RuntimeLayoutSession(document);
            layout = session.Document;
            canvas.Bind(session);
            canvas.SizeChanged += (s, e) => ArrangeRunControls();
            refreshTimer.Tick += RefreshLive;
            AttachRunController(new RuntimePreviewRunController(new RuntimeProcessChoice[0]));
            AttachLogSource(new RuntimePreviewLogSource());
            AttachTuningController(null);
            refreshTimer.Start();
        }

        /// <summary>打开真实结果视图，数据源所有权转移给窗口；显示操作不会启动检测。</summary>
        public RuntimeInterfacePreviewForm(RuntimeLayoutDocument document, IRuntimeBindingDataSource source, Func<bool> isCurrent)
            : this(document)
        {
            this.isCurrent = isCurrent;
            AttachLogSource(null);
            liveController = new RuntimeLiveViewController(canvas, source);
            Text = titleLabel.Text = "运行界面（实时数据）";
        }

        /// <summary>正式运行窗口默认最大化，多窗口仍保留独立名称及错开的还原位置。</summary>
        public void ConfigureRuntimeWindow(string name, int index, int count)
        {
            runtimeTitle = string.IsNullOrWhiteSpace(name) ? "运行界面" : name;
            Text = titleLabel.Text = runtimeTitle + "（实时数据）";
            WindowState = FormWindowState.Normal;
            if (count > 1)
            {
                StartPosition = FormStartPosition.Manual;
                // 普通状态下设置还原边界，再统一最大化，避免隐藏句柄缓存过时的显示状态。
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Max(MinimumSize.Width, area.Width * 4 / 5), Math.Max(MinimumSize.Height, area.Height * 4 / 5));
                int offset = index % 6 * 24;
                Location = new Point(area.Left + Math.Max(0, Math.Min(24 + offset, area.Width - Width)),
                    area.Top + Math.Max(0, Math.Min(24 + offset, area.Height - Height)));
            }
            WindowState = FormWindowState.Maximized;
        }

        /// <summary>宿主显式注入执行入口；未注入时仅模拟交互，打开窗口不会自动运行。</summary>
        public void AttachRunController(IRuntimeRunController controller)
        {
            if (previewRunController != null && ReferenceEquals(previewRunController, controller)) return;
            previewRunController?.Dispose();
            previewRunController = controller as RuntimePreviewRunController;
            foreach (var widget in runWidgets.Values) widget.Dispose();
            runWidgets.Clear();
            foreach (var element in layout.Elements.Where(item => item.Kind == RuntimeWidgetKind.RunControl))
            {
                var widget = new RuntimeRunControlWidget();
                widget.ApplyAppearance(RuntimeLayoutSerializer.ParseColor(element.Background), RuntimeLayoutSerializer.ParseColor(element.Foreground));
                widget.Bind(controller, element.RunProcessId);
                widget.CommandFailed += ShowCommandError;
                canvas.Controls.Add(widget);
                runWidgets.Add(element.Id, widget);
            }
            ArrangeRunControls();
        }

        /// <summary>正式宿主注入调参入口；设计预览留空，点击不会调用学习代码。</summary>
        public void AttachTuningController(IRuntimeTuningController controller)
        {
            foreach (var widget in tuningWidgets.Values) widget.Dispose();
            tuningWidgets.Clear();
            foreach (var element in layout.Elements.Where(item => RuntimeTuningController.Supports(item.Kind)))
            {
                var widget = new RuntimeTuningWidget();
                try
                {
                    widget.Bind(controller, element);
                    widget.CommandFailed += ShowCommandError;
                    canvas.Controls.Add(widget);
                    tuningWidgets.Add(element.Id, widget);
                }
                catch { widget.Dispose(); throw; }
            }
            ArrangeRunControls();
        }

        /// <summary>注入方案范围日志源，每个控件独立订阅；空源不显示示例或系统无归属日志。</summary>
        public void AttachLogSource(IRuntimeLogSource source)
        {
            foreach (var widget in logWidgets.Values) widget.Dispose();
            logWidgets.Clear();
            foreach (var element in layout.Elements.Where(item => item.Kind == RuntimeWidgetKind.Log))
            {
                var widget = new RuntimeLogWidget();
                try
                {
                    widget.ApplyAppearance(RuntimeLayoutSerializer.ParseColor(element.Background), RuntimeLayoutSerializer.ParseColor(element.Foreground));
                    widget.Bind((source ?? new RuntimePreviewLogSource(false)).Subscribe(element.LogProcessId, element.LogCapacity),
                        element.LogCapacity, element.LogMinimumLevel, element.LogAutoScroll);
                    canvas.Controls.Add(widget);
                    logWidgets.Add(element.Id, widget);
                }
                catch { widget.Dispose(); throw; }
            }
            ArrangeRunControls();
        }

        /// <summary>显示命令失败原因，不能悄悄退回空闲状态掩盖执行错误。</summary>
        private void ShowCommandError(string message)
        {
            if (!IsDisposed)
                MessageBox.Show(this, message, "运行控制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>原生按钮和日志框随页面填充缩放，并按画布层级裁剪遮挡区域。</summary>
        private void ArrangeRunControls()
        {
            if (layout == null) return;
            for (int index = 0; index < layout.Elements.Count; index++)
            {
                var element = layout.Elements[index];
                RuntimeRunControlWidget runWidget;
                RuntimeLogWidget logWidget;
                RuntimeTuningWidget tuningWidget;
                Control widget = runWidgets.TryGetValue(element.Id, out runWidget) ? (Control)runWidget :
                    logWidgets.TryGetValue(element.Id, out logWidget) ? (Control)logWidget :
                    tuningWidgets.TryGetValue(element.Id, out tuningWidget) ? tuningWidget : null;
                if (widget == null) continue;
                var bounds = canvas.GetPreviewBounds(element);
                widget.Bounds = bounds;
                var region = new Region(new Rectangle(Point.Empty, bounds.Size));
                for (int later = index + 1; later < layout.Elements.Count; later++)
                {
                    Rectangle covered = canvas.GetPreviewBounds(layout.Elements[later]);
                    covered.Offset(-bounds.X, -bounds.Y);
                    region.Exclude(covered);
                }
                var previous = widget.Region;
                widget.Region = region;
                previous?.Dispose();
                // 裁剪区域为空仍可能参与Tab导航，调参按钮必须同时退出键盘选择范围。
                if (widget is RuntimeTuningWidget)
                    using (var matrix = new System.Drawing.Drawing2D.Matrix())
                        widget.Visible = region.GetRegionScans(matrix).Length > 0;
            }
        }

        /// <summary>合并刷新；切换方案后关闭旧窗口，不继续展示旧数据。</summary>
        private void RefreshLive(object sender, EventArgs e)
        {
            if (!IsCurrentDocument) { Close(); return; }
            if (WindowState == FormWindowState.Minimized || !Visible) return;
            liveController?.Tick();
            foreach (var widget in runWidgets.Values) widget.RefreshState();
            foreach (var widget in logWidgets.Values) widget.RefreshLogs();
            foreach (var widget in tuningWidgets.Values) widget.RefreshState();
            if (liveController != null)
            {
                string error = liveController.LastError ?? string.Empty;
                string title = runtimeTitle + (string.IsNullOrEmpty(error) ? "（实时数据）" : "（刷新异常）");
                if (Text != title) Text = title;
                if (titleLabel.Text != title) titleLabel.Text = title;
                if (windowToolTip.GetToolTip(titleLabel) != error) windowToolTip.SetToolTip(titleLabel, error);
            }
        }

        /// <summary>启用系统窗口移动、缩放和任务栏命令。</summary>
        protected override CreateParams CreateParams => RuntimeDesignerWindowChrome.ConfigureStyles(base.CreateParams);

        /// <summary>处理无边框命中测试和显示器工作区约束。</summary>
        protected override void WndProc(ref Message message)
        {
            if (!RuntimeDesignerWindowChrome.ProcessMessage(this, ref message)) base.WndProc(ref message);
        }
    }
}
