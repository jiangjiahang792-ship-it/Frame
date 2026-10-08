using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>文本对齐的属性、持久化、复制与真实像素回归。</summary>
internal static class RuntimeTextAlignmentTests
{
    /// <summary>对齐属性反射入口，用于旧代码RED阶段。</summary>
    private static PropertyInfo alignment;
    /// <summary>断言计数。</summary>
    private static int checks;
    /// <summary>运行独立测试。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            alignment = typeof(RuntimeLayoutElement).GetProperty("TextAlignment");
            Check(alignment != null, "文本控件缺少文本对齐属性");
            PropertiesAndHistory(); PixelsAndCache();
            Console.WriteLine("通过：文本对齐，" + checks + " 项断言。"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>使用稳定枚举名称设置属性。</summary>
    private static void Set(RuntimeLayoutElement element, string value) => alignment.SetValue(element, Enum.Parse(alignment.PropertyType, value));
    /// <summary>获得当前枚举名称。</summary>
    private static string Get(RuntimeLayoutElement element) => alignment.GetValue(element).ToString();
    /// <summary>属性显示和文档事务完整保留对齐。</summary>
    private static void PropertiesAndHistory()
    {
        var element = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text };
        Check(Get(element) == "Left", "旧布局默认左对齐");
        var document = new RuntimeLayoutDocument(); document.Elements.Add(element);
        var oldJson = JObject.Parse(RuntimeLayoutSerializer.Serialize(document));
        ((JObject)oldJson["Elements"][0]).Property("TextAlignment").Remove();
        Check(Get(RuntimeLayoutSerializer.Deserialize(oldJson.ToString()).Elements[0]) == "Left", "无新字段的旧模板保持左对齐");
        var descriptor = new RuntimeElementProperties(element).GetProperties()["TextAlignment"];
        Check(descriptor != null && descriptor.DisplayName == "文本对齐", "文本框属性栏展示中文对齐属性");
        string[] values = { "Left", "Center", "Right" }, names = { "左对齐", "居中", "右对齐" };
        for (int i = 0; i < values.Length; i++)
        {
            Set(element, values[i]);
            Check(descriptor.Converter.ConvertToString(alignment.GetValue(element)) == names[i], "枚举选项显示中文");
            Check(descriptor.Converter.ConvertFromString(names[i]).ToString() == values[i], "中文选项还原枚举");
            Check(Get(RuntimeLayoutSerializer.Deserialize(RuntimeLayoutSerializer.Serialize(document)).Elements[0]) == values[i], "模板往返保留对齐");
            Check(Get(RuntimeWidgetClipboardCodec.Decode(RuntimeWidgetClipboardCodec.Encode(document, element))) == values[i], "复制粘贴保留对齐");
        }
        foreach (RuntimeWidgetKind kind in Enum.GetValues(typeof(RuntimeWidgetKind)))
            if (kind != RuntimeWidgetKind.Text)
                Check(new RuntimeElementProperties(new RuntimeLayoutElement { Kind = kind }).GetProperties()["TextAlignment"] == null, "非文本控件不暴露无效对齐选项");
        var session = new RuntimeLayoutSession(document);
        var draft = session.Document.Elements[0].Copy(); Set(draft, "Center"); session.UpdateElement(draft);
        session.Undo(); Check(Get(session.Document.Elements[0]) == "Right", "撤销恢复对齐");
        session.Redo(); Check(Get(session.Document.Elements[0]) == "Center", "重做恢复对齐");
        Check(Get(session.Duplicate(draft.Id)) == "Center", "工具栏复制保留对齐");
        alignment.SetValue(element, Enum.ToObject(alignment.PropertyType, 999));
        bool rejected = false;
        try { RuntimeLayoutSerializer.Serialize(document); } catch (Exception) { rejected = true; }
        Check(rejected, "无效对齐枚举拒绝保存");
    }
    /// <summary>像素位置和缓存重建均随对齐变化，字体与边距保持不变。</summary>
    private static void PixelsAndCache()
    {
        var element = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, Width = 300, Height = 100, Text = "MMMM", FontSize = 24, Foreground = "#FFFFFF", Background = "#000000" };
        using (var renderer = new RuntimeTextRenderer())
        using (var cache = new RuntimeWidgetSurfaceCache())
        {
            Set(element, "Left"); Rectangle left = Render(renderer, cache, element);
            Set(element, "Center"); Rectangle center = Render(renderer, cache, element);
            Set(element, "Right"); Rectangle right = Render(renderer, cache, element);
            Check(left.Width > 0 && left.X < center.X && center.X < right.X, "实际文字像素按左中右移动，缓存不残留旧对齐");
            Check(Math.Abs(left.Width - right.Width) <= 2 && Math.Abs(left.Height - center.Height) <= 1, "改变对齐不改变字号和内容尺寸");
            Check(Math.Abs(center.Left + center.Width / 2f - 150) < 5, "居中文字位于内容区中心");
            Check(right.Right <= 290 && left.Left >= 12, "保留文本内边距且不越界");
            element.Text = "MM\nMMMM";
            foreach (string value in new[] { "Left", "Center", "Right" })
            {
                Set(element, value); Rectangle bounds = Render(renderer, cache, element);
                Check(bounds.Top >= 6 && bounds.Bottom <= 94 && bounds.Left >= 12 && bounds.Right <= 290, "多行文本按对齐绘制且不越界");
            }
        }
    }
    /// <summary>走真实表面缓存绘制，统计白色文字像素边界。</summary>
    private static Rectangle Render(RuntimeTextRenderer renderer, RuntimeWidgetSurfaceCache cache, RuntimeLayoutElement element)
    {
        using (var bitmap = new Bitmap(element.Width, element.Height))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            cache.BeginFrame(); cache.Draw(graphics, renderer, element, bitmap.Size, 1, false);
            int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
            for (int y = 2; y < bitmap.Height - 2; y++) for (int x = 2; x < bitmap.Width - 2; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.R < 210 || color.G < 210 || color.B < 210) continue;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
    }
    /// <summary>检查具体预期。</summary>
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
}
