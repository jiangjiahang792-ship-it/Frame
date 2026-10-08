using System;
using System.Drawing;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行时调参按钮，只转发明确的流程绑定，不触发检测或设备信号。</summary>
    public partial class RuntimeTuningWidget : UserControl
    {
        /// <summary>宿主拥有的调参入口，预览中始终为空。</summary>
        private IRuntimeTuningController controller;
        /// <summary>布局的独立属性副本。</summary>
        private RuntimeLayoutElement element;
        /// <summary>防止模态选择窗口消息循环中的重复点击。</summary>
        private bool executing;
        /// <summary>跟随填充比例的字体，尺寸变化时才替换。</summary>
        private Font ownedFont;
        /// <summary>同一状态查询错误只报告一次，恢复后清空。</summary>
        private string stateError;
        /// <summary>向窗口统一报告命令失败。</summary>
        public event Action<string> CommandFailed;
        /// <summary>初始化原生按钮，不创建生产控制器。</summary>
        public RuntimeTuningWidget()
        {
            InitializeComponent();
            actionButton.Click += ExecuteClicked;
            SizeChanged += (sender, args) => RefreshFont();
        }
        /// <summary>绑定动作与外观，传入空控制器时保持无副作用预览。</summary>
        public void Bind(IRuntimeTuningController value, RuntimeLayoutElement layoutElement)
        {
            if (layoutElement == null || !RuntimeTuningController.Supports(layoutElement.Kind))
                throw new ArgumentException("请绑定手动参数或一键学习控件。", nameof(layoutElement));
            controller = value;
            element = layoutElement.Copy();
            actionButton.Text = element.Text;
            actionButton.AccessibleName = RuntimeLayoutSession.GetName(element.Kind);
            actionButton.AccessibleDescription = controller == null ? "预览不执行调参或学习" :
                element.ActionProcessId.HasValue ? "绑定流程 " + element.ActionProcessId.Value : "未绑定流程";
            actionButton.BackColor = RuntimeLayoutSerializer.ParseColor(element.Background);
            actionButton.ForeColor = RuntimeLayoutSerializer.ParseColor(element.Foreground);
            RefreshFont();
            RefreshState();
        }
        /// <summary>只在启用状态实际变化时更新，避免刷新循环重复重绘或创建提示框。</summary>
        public void RefreshState()
        {
            bool enabled = false;
            string error = null;
            try { enabled = !executing && element?.ActionProcessId.HasValue == true && controller?.CanUse == true; }
            catch (Exception ex) { error = "调参入口状态读取失败：" + ex.Message; }
            if (actionButton.Enabled != enabled) actionButton.Enabled = enabled;
            bool report = error != null && stateError != error;
            stateError = error;
            if (report) CommandFailed?.Invoke(error);
        }
        /// <summary>只在尺寸或文字属性变化时计算适合按钮内容区的字号。</summary>
        private void RefreshFont()
        {
            if (element == null || Width <= 0 || Height <= 0) return;
            float scale = Math.Min((float)Width / element.Width, (float)Height / element.Height);
            float size = Math.Max(6, Math.Min(element.FontSize * scale, Math.Min(Math.Max(6, Height - 8),
                Math.Max(6, Width - 12) / (float)Math.Max(1, element.Text.Length))));
            FontStyle style = element.Bold ? FontStyle.Bold : FontStyle.Regular;
            if (ownedFont != null && Math.Abs(ownedFont.Size - size) < 0.1f && ownedFont.Style == style) return;
            var previous = ownedFont;
            ownedFont = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel);
            actionButton.Font = ownedFont;
            previous?.Dispose();
        }
        /// <summary>点击时再次检查权限并由宿主校验绑定，异常显示后允许重试。</summary>
        private void ExecuteClicked(object sender, EventArgs args)
        {
            if (executing || controller == null || element?.ActionProcessId.HasValue != true) return;
            executing = true;
            RefreshState();
            try { controller.Execute(FindForm(), element.Kind, element.ActionProcessId); }
            catch (Exception ex) { if (!IsDisposed) CommandFailed?.Invoke(ex.Message); }
            finally { executing = false; if (!IsDisposed) RefreshState(); }
        }
    }

    /// <summary>编辑画布中只绘制外观的调参按钮，不执行任何操作。</summary>
    internal sealed class RuntimeTuningRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>绘制器支持的调参控件类型。</summary>
        public override RuntimeWidgetKind Kind { get; }
        /// <summary>分别注册手动参数和一键学习绘制器。</summary>
        public RuntimeTuningRenderer(RuntimeWidgetKind kind) { Kind = kind; }
        /// <summary>按钮文字保持居中，超出内容区时沿用共用缩放策略。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            Text(graphics, element.Text, element, new RectangleF(6, 4, element.Width - 12, element.Height - 8),
                element.FontSize, RuntimeLayoutSerializer.ParseColor(element.Foreground), true);
        }
    }
}
