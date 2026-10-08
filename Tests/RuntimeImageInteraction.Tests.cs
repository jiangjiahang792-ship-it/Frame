using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>真实画布消息与像素测试，不接触用户方案和生产设备。</summary>
internal static class RuntimeImageInteractionTests
{
    /// <summary>检查计数。</summary>
    private static int checks;
    /// <summary>执行运行态与设计态交互。</summary>
    [STAThread]
    private static int Main()
    {
        try { Verify(true); Verify(false); VerifyRebind(); VerifyMenuOrder(); Console.WriteLine("通过：图像交互，" + checks + " 项断言。"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>生成红色中心块，缩放前后可通过像素统计区分。</summary>
    private static string Sample()
    {
        using (var image = new Bitmap(100, 100))
        using (var graphics = Graphics.FromImage(image))
        using (var stream = new MemoryStream())
        {
            graphics.Clear(Color.White); graphics.FillRectangle(Brushes.Red, 40, 40, 20, 20);
            image.Save(stream, ImageFormat.Png); return Convert.ToBase64String(stream.ToArray());
        }
    }
    /// <summary>输入操作必须改变图像视图，而不是控件布局或原始图数据。</summary>
    private static void Verify(bool preview)
    {
        var document = new RuntimeLayoutDocument { Width = 400, Height = 300 };
        document.Elements.Add(new RuntimeLayoutElement { Id = "image", Kind = RuntimeWidgetKind.Image, X = 20, Y = 20, Width = 200, Height = 200, ImageData = Sample() });
        var session = new RuntimeLayoutSession(document);
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.PreviewMode = preview; canvas.Bind(session);
            if (preview) canvas.Size = new Size(400, 300);
            canvas.CreateControl();
            Point center = Position(canvas, new Point(120, 120), preview);
            int originalPixels = RedPixels(canvas);
            Mouse(canvas, "OnMouseWheel", center, MouseButtons.None, 120);
            int zoomedPixels = RedPixels(canvas);
            Check(zoomedPixels > originalPixels * 1.2, "滚轮应该放大图像而非保持原图不变");
            Check(session.Find("image").Width == 200 && !session.CanUndo, "图像缩放不改变控件尺寸或产生布局撤销");
            Mouse(canvas, "OnMouseDoubleClick", center, MouseButtons.Left, 0, 2);
            Check(Math.Abs(RedPixels(canvas) - originalPixels) < 8, "双击恢复适应居中图像");
            Mouse(canvas, "OnMouseWheel", center, MouseButtons.None, 120);
            MouseButtons panButton = preview ? MouseButtons.Left : MouseButtons.Middle;
            Mouse(canvas, "OnMouseDown", center, panButton);
            Mouse(canvas, "OnMouseMove", new Point(center.X + 30, center.Y + 15), panButton);
            Mouse(canvas, "OnMouseUp", new Point(center.X + 30, center.Y + 15), panButton);
            using (var bitmap = Render(canvas))
            {
                Check(IsRed(bitmap.GetPixel(center.X + 30, center.Y + 15)), "拖动平移放大图像");
                Check(!IsRed(bitmap.GetPixel(center.X - 15, center.Y - 15)), "平移不留旧图残影");
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, preview ? "运行图像缩放.png" : "设计图像缩放.png"));
            }
            Check(!session.CanUndo && session.Find("image").X == 20, "图像平移不移动控件");
            Mouse(canvas, "OnMouseDoubleClick", center, MouseButtons.Left, 0, 2);
            Check(Math.Abs(RedPixels(canvas) - originalPixels) < 8, "平移后双击恢复居中");
            var clear = typeof(RuntimeDesignCanvas).GetMethod("ClearImageDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(clear != null, "清除图像入口存在");
            clear.Invoke(canvas, new object[] { "image" });
            Check(RedPixels(canvas) == 0, "清除不再绘制旧图像");
            if (preview)
                Check(session.Find("image").ImageData != null && !session.CanUndo, "运行清除不改静态方案或撤销历史");
            else
            {
                Check(session.Find("image").ImageData == null && session.CanUndo, "设计清除示例图可撤销");
                session.Undo();
                Check(Math.Abs(RedPixels(canvas) - originalPixels) < 8, "撤销清除恢复示例图");
            }
            var menu = typeof(RuntimeDesignCanvas).GetField("clearImageMenuItem", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(menu != null && ((ToolStripMenuItem)menu.GetValue(canvas)).Text == "清除图像", "右键菜单使用中文清除图像命令");
            Check(document.Elements[0].ImageData != null, "原始方案文档未被界面操作修改");
        }
    }
    /// <summary>重新绑定重置图像视图时，旧的放大表面必须同时失效。</summary>
    private static void VerifyRebind()
    {
        var doc = new RuntimeLayoutDocument { Width = 400, Height = 300 };
        doc.Elements.Add(new RuntimeLayoutElement { Id = "same", Kind = RuntimeWidgetKind.Image, X = 20, Y = 20, Width = 200, Height = 200, ImageData = Sample() });
        var session = new RuntimeLayoutSession(doc);
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.Bind(session); canvas.CreateControl();
            int before = RedPixels(canvas);
            Mouse(canvas, "OnMouseWheel", Position(canvas, new Point(120, 120), false), MouseButtons.None, 120);
            Check(RedPixels(canvas) > before, "重新绑定测试先建立放大缓存");
            canvas.Bind(session);
            Check(RedPixels(canvas) == before, "同ID重新绑定后不得显示旧倍率缓存");
        }
    }
    /// <summary>复现WinForms先关闭下拉菜单再触发Click的顺序，不显示桌面菜单。</summary>
    private static void VerifyMenuOrder()
    {
        var doc = new RuntimeLayoutDocument();
        doc.Elements.Add(new RuntimeLayoutElement { Id = "target", Kind = RuntimeWidgetKind.Image, ImageData = Sample() });
        var session = new RuntimeLayoutSession(doc);
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.Bind(session);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RuntimeDesignCanvas).GetField("contextImageId", flags).SetValue(canvas, "target");
            var menu = (ContextMenuStrip)typeof(RuntimeDesignCanvas).GetField("imageMenu", flags).GetValue(canvas);
            typeof(ToolStripDropDown).GetMethod("OnClosed", flags).Invoke(menu, new object[] { new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.ItemClicked) });
            ((ToolStripMenuItem)typeof(RuntimeDesignCanvas).GetField("clearImageMenuItem", flags).GetValue(canvas)).PerformClick();
            Check(session.Find("target").ImageData == null && session.CanUndo, "菜单关闭后Click仍能清除正确目标");
            Check(typeof(RuntimeDesignCanvas).GetField("contextImageId", flags).GetValue(canvas) == null, "命令完成后释放右键目标");
            typeof(RuntimeDesignCanvas).GetField("contextImageId", flags).SetValue(canvas, "target");
            var next = new RuntimeLayoutSession(doc);
            canvas.Bind(next);
            ((ToolStripMenuItem)typeof(RuntimeDesignCanvas).GetField("clearImageMenuItem", flags).GetValue(canvas)).PerformClick();
            Check(next.Find("target").ImageData != null && !next.CanUndo, "切页后旧菜单命令不得清除新页面的同ID图像");
        }
    }
    /// <summary>绘制画布副本。</summary>
    private static Bitmap Render(RuntimeDesignCanvas canvas) { var bitmap = new Bitmap(canvas.Width, canvas.Height); canvas.DrawToBitmap(bitmap, canvas.ClientRectangle); return bitmap; }
    /// <summary>统计红色块实际面积。</summary>
    private static int RedPixels(RuntimeDesignCanvas canvas)
    { int count = 0; using (var bitmap = Render(canvas)) for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) if (IsRed(bitmap.GetPixel(x, y))) count++; return count; }
    /// <summary>严格区分红色图片与橙色控件边框。</summary>
    private static bool IsRed(Color color) => color.R > 180 && color.G < 60 && color.B < 60;
    /// <summary>获得同源渲染坐标。</summary>
    private static Point Position(RuntimeDesignCanvas canvas, Point logical, bool preview)
    {
        if (preview) return logical;
        return Point.Round(((RectangleF)typeof(RuntimeDesignCanvas).GetMethod("ToScreen", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(canvas, new object[] { new Rectangle(logical, new Size(1, 1)) })).Location);
    }
    /// <summary>派发原生控件事件。</summary>
    private static void Mouse(RuntimeDesignCanvas canvas, string method, Point point, MouseButtons button, int delta = 0, int clicks = 1)
    { typeof(RuntimeDesignCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new MouseEventArgs(button, clicks, point.X, point.Y, delta) }); }
    /// <summary>断言行为。</summary>
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
}
