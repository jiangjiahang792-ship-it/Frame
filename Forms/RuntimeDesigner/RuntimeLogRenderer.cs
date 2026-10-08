using System.Drawing;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>设计画布中的日志框外观，不订阅生产日志。</summary>
    internal sealed class RuntimeLogRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>对应日志框类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Log;
        /// <summary>按固定表头和列宽绘制示例，所有内容受组件边界裁剪。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Background(graphics, element);
            Color foreground = RuntimeLayoutSerializer.ParseColor(element.Foreground);
            for (int i = 0; i < 3; i++)
                using (var icon = RuntimeDesignerIcons.Create(i == 0 ? 0xE74D : i == 1 ? 0xE769 : 0xE74B, foreground, 16))
                    graphics.DrawImage(icon, 8 + i * 30, 7, 16, 16);
            Text(graphics, "全部等级", element, new RectangleF(104, 2, 90, 26), 12, foreground);
            using (var brush = new SolidBrush(Color.FromArgb(54, 56, 59)))
                graphics.FillRectangle(brush, 1, 32, element.Width - 2, 25);
            string[] headings = { "时间", "等级", "来源", "内容" };
            int[] lefts = { 8, 116, 172, 338 };
            int[] widths = { 100, 48, 158, element.Width - 346 };
            for (int i = 0; i < headings.Length; i++)
                Text(graphics, headings[i], element, new RectangleF(lefts[i], 32, widths[i], 24), 12, foreground);
            string[] values = { "12:00:00.000", "信息", "流程1 / 检测模块", "运行成功" };
            for (int i = 0; i < values.Length; i++)
                Text(graphics, values[i], element, new RectangleF(lefts[i], 59, widths[i], 24), 12, foreground);
        }
    }
}
