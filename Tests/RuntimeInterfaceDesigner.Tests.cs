using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>验证编辑器的真实文档和编辑行为，预览入口不创建生产方案单例。</summary>
internal static class RuntimeInterfaceDesignerTests
{
    /// <summary>测试断言总数。</summary>
    private static int assertions;

    /// <summary>启动行为测试或独立的实际编辑器窗口。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        if (args.Contains("--preview"))
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设计器体验.runtime-ui.json");
            RuntimeLayoutDocument document = File.Exists(path) ? RuntimeLayoutSerializer.Load(path) : new RuntimeLayoutDocument();
            Application.Run(new RuntimeInterfaceEditorForm(document, "设计器体验（独立预览）", layout =>
            {
                RuntimeLayoutSerializer.Save(path, layout);
                return true;
            }));
            return 0;
        }
        try
        {
            Console.WriteLine("TestSession"); TestSession();
            Console.WriteLine("TestSerialization"); TestSerialization();
            Console.WriteLine("TestImage"); TestImage();
            Console.WriteLine("TestImageWithoutHeading"); TestImageWithoutHeading();
            Console.WriteLine("TestSolutionConfig"); TestSolutionConfig();
            Console.WriteLine("TestKeyboardLimits"); TestKeyboardLimits();
            Console.WriteLine("TestCanvasInteraction"); TestCanvasInteraction();
            Console.WriteLine("TestSmartAlignment"); TestSmartAlignment();
            Console.WriteLine("TestDropFeedback"); TestDropFeedback();
            Console.WriteLine("TestResizeFeedback"); TestResizeFeedback();
            Console.WriteLine("TestForm"); TestForm();
            Console.WriteLine("TestPreviewFill"); TestPreviewFill();
            Console.WriteLine("TestPreviewImageAspect"); TestPreviewImageAspect();
            Console.WriteLine("PASS: " + assertions + " runtime designer assertions");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            return 1;
        }
    }

    /// <summary>图像和空图占位均不绘制标题，也不为旧标题预留顶部区域。</summary>
    private static void TestImageWithoutHeading()
    {
        string imageData;
        using (var source = new Bitmap(120, 120))
        using (var graphics = Graphics.FromImage(source))
        using (var stream = new MemoryStream())
        {
            graphics.Clear(Color.Red);
            source.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            imageData = Convert.ToBase64String(stream.ToArray());
        }
        foreach (string data in new[] { imageData, null })
        using (var renderer = new RuntimeImageRenderer())
        using (var withText = new Bitmap(120, 120))
        using (var withoutText = new Bitmap(120, 120))
        {
            var element = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Width = 120, Height = 120,
                Text = "图像显示", ImageData = data };
            using (var graphics = Graphics.FromImage(withText)) renderer.Draw(graphics, element);
            Assert(element.Text == "图像显示" && element.Width == 120 && element.Height == 120,
                "rendering must not rewrite persisted image properties");
            element.Text = string.Empty;
            using (var graphics = Graphics.FromImage(withoutText)) renderer.Draw(graphics, element);
            bool identical = true;
            for (int y = 0; y < 120; y++)
                for (int x = 0; x < 120; x++)
                    if (withText.GetPixel(x, y) != withoutText.GetPixel(x, y)) identical = false;
            Assert(identical, "image widgets must not draw heading text, with or without an image");
            foreach (float zoom in new[] { 0.5f, 1f, 2f })
            using (var framed = new Bitmap((int)(120 * zoom), (int)(120 * zoom)))
            {
                using (var graphics = Graphics.FromImage(framed))
                {
                    graphics.ScaleTransform(zoom, zoom);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    renderer.Draw(graphics, element);
                }
                int edge = (int)(2 * zoom) - 1, middle = framed.Width / 2;
                Color left = framed.GetPixel(edge, middle), top = framed.GetPixel(middle, edge);
                Color right = framed.GetPixel(framed.Width - 1 - edge, middle);
                Color bottom = framed.GetPixel(middle, framed.Height - 1 - edge);
                Assert(left.ToArgb() == top.ToArgb() && right.ToArgb() == bottom.ToArgb() &&
                    left.R >= 20 && left.R < right.R && right.R <= 85 &&
                    left.R == left.G && left.G == left.B && right.R == right.G && right.G == right.B,
                    "image rim must have dark top/left and subtle lighter bottom/right at zoom " + zoom);
                if (zoom >= 1)
                {
                    Assert(framed.GetPixel(0, middle).R < left.R &&
                        framed.GetPixel(framed.Width - 1, middle).R > right.R,
                        "image rim must retain separate outer and inner bevel tones at zoom " + zoom);
                }
            }
            if (data != null)
                Assert(withText.GetPixel(60, 4).R > 240 && withText.GetPixel(60, 4).G < 10 &&
                    withText.GetPixel(60, 115).R > 240 && withText.GetPixel(60, 115).G < 10,
                    "square image must use both top and bottom interior without heading space");
        }
    }

    /// <summary>预览铺满宽屏和竖屏时，图像内容不能被横纵不同缩放拉伸。</summary>
    private static void TestPreviewImageAspect()
    {
        var document = new RuntimeLayoutDocument { Width = 640, Height = 480 };
        using (var source = new Bitmap(1000, 500))
        using (var graphics = Graphics.FromImage(source))
        using (var stream = new MemoryStream())
        {
            graphics.Clear(Color.Red);
            source.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Width = 640, Height = 480,
                ImageData = Convert.ToBase64String(stream.ToArray()), Text = string.Empty });
        }
        using (var canvas = new RuntimeDesignCanvas { PreviewMode = true })
        {
            canvas.Bind(new RuntimeLayoutSession(document));
            foreach (var size in new[] { new Size(1000, 500), new Size(500, 1000) })
            {
                canvas.Size = size;
                using (var bitmap = new Bitmap(size.Width, size.Height))
                {
                    canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                    int redWidth = 0, redHeight = 0;
                    for (int x = 0; x < size.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, size.Height / 2);
                        if (pixel.R > 240 && pixel.G < 10 && pixel.B < 10) redWidth++;
                    }
                    for (int y = 0; y < size.Height; y++)
                    {
                        var pixel = bitmap.GetPixel(size.Width / 2, y);
                        if (pixel.R > 240 && pixel.G < 10 && pixel.B < 10) redHeight++;
                    }
                    Assert(redHeight > 100 && Math.Abs(redWidth - 2 * redHeight) <= 4,
                        "image pixels must retain the original 2:1 aspect in filled preview: " + size + ", red=" + redWidth + "x" + redHeight);
                }
            }
        }
    }

    /// <summary>预览随窗口铺满，不保留编辑器留白，不改变保存的逻辑坐标。</summary>
    private static void TestPreviewFill()
    {
        var document = new RuntimeLayoutDocument { Width = 640, Height = 480, Background = "#303030" };
        document.Elements.Add(new RuntimeLayoutElement
        {
            Kind = RuntimeWidgetKind.Text, X = 320, Y = 240, Width = 320, Height = 240,
            Background = "#00AA00", Text = string.Empty
        });
        string before = RuntimeLayoutSerializer.Serialize(document);
        using (var canvas = new RuntimeDesignCanvas { PreviewMode = true })
        {
            canvas.Bind(new RuntimeLayoutSession(document));
            foreach (var size in new[] { new Size(640, 480), new Size(1280, 720), new Size(1920, 1080), new Size(800, 1000), new Size(3200, 2400) })
            {
                canvas.Size = size;
                using (var bitmap = new Bitmap(size.Width, size.Height))
                {
                    canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                    Assert(bitmap.GetPixel(2, 2).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb(), "preview starts at the client origin without margins");
                    Assert(bitmap.GetPixel(size.Width - 4, size.Height - 4).ToArgb() == Color.FromArgb(0, 170, 0).ToArgb(), "preview widget fills the scaled bottom-right region");
                    Assert(bitmap.GetPixel(size.Width / 2 - 4, size.Height - 4).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb(), "preview horizontal layout tracks the viewport independently");
                    Assert(bitmap.GetPixel(size.Width - 4, size.Height / 2 - 4).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb(), "preview vertical layout tracks the viewport independently");
                }
            }
        }
        Assert(RuntimeLayoutSerializer.Serialize(document) == before, "preview resizing must not mutate the saved document");
    }

    /// <summary>阻止编辑草稿污染方案，验证边界和撤销后的内容恢复。</summary>
    private static void TestSession()
    {
        var original = new RuntimeLayoutDocument();
        var session = new RuntimeLayoutSession(original);
        var image = session.Add(RuntimeWidgetKind.Image, new Point(100, 100));
        Assert(original.Elements.Count == 0, "editing must not mutate solution");
        session.SetBounds(image.Id, new Rectangle(-90, -20, 4000, 3000));
        Assert(image.X == 0 && image.Y == 0 && image.Width == 1280 && image.Height == 720, "bounds must stay inside page");
        session.Undo();
        Assert(session.Document.Elements[0].X == 100, "undo restores geometry");
        session.Redo();
        Assert(session.Document.Elements[0].Width == 1280, "redo restores geometry");
        session.Duplicate(image.Id);
        Assert(session.Document.Elements.Select(e => e.Id).Distinct().Count() == 2, "duplicates have independent IDs");
        session.Remove(image.Id);
        Assert(session.Document.Elements.Count == 1, "remove affects selected element only");
        session.Undo();
        Assert(session.Document.Elements.Count == 2 && session.Document.Elements[0].Id == image.Id, "undo restores original layer order");
        string before = RuntimeLayoutSerializer.Serialize(session.Document);
        MustReject(() => session.Edit(doc => doc.Elements[0].Width = -8));
        Assert(RuntimeLayoutSerializer.Serialize(session.Document) == before, "invalid property edit restores document");
        session.Replace(new RuntimeLayoutDocument { Name = "新模板" });
        session.Undo();
        Assert(RuntimeLayoutSerializer.Serialize(session.Document) == before, "undo import restores previous document");
    }

    /// <summary>模板必须保留全部组件，拒绝不合法配置并保证原子保存失败不覆盖旧文件。</summary>
    private static void TestSerialization()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        foreach (RuntimeWidgetKind kind in Enum.GetValues(typeof(RuntimeWidgetKind)))
            session.Add(kind, new Point(20, 20));
        session.Document.Elements[2].Text = "工位一\n检测结果";
        string json = RuntimeLayoutSerializer.Serialize(session.Document);
        var copy = RuntimeLayoutSerializer.Deserialize(json);
        Assert(copy.Elements.Count == Enum.GetValues(typeof(RuntimeWidgetKind)).Length && copy.Elements[2].Text == "工位一\n检测结果", "portable template preserves content");
        Assert(copy.Elements.Single(e => e.Kind == RuntimeWidgetKind.RunControl).RunProcessId == null, "portable run control defaults to all processes");
        MustReject(() => RuntimeLayoutSerializer.Deserialize("{\"Version\":99}"));
        MustReject(() => RuntimeLayoutSerializer.Deserialize("null"));
        MustReject(() => RuntimeLayoutSerializer.Deserialize("{\"Elements\":null}"));
        copy.Elements[0].Width = -1;
        MustReject(() => RuntimeLayoutSerializer.Serialize(copy));
        copy = RuntimeLayoutSerializer.Deserialize(json);
        copy.Elements[1].Id = copy.Elements[0].Id;
        MustReject(() => RuntimeLayoutSerializer.Serialize(copy));
        copy = RuntimeLayoutSerializer.Deserialize(json);
        copy.Elements[0].Kind = (RuntimeWidgetKind)100;
        MustReject(() => RuntimeLayoutSerializer.Serialize(copy));
        string directory = Path.Combine(Path.GetTempPath(), "RuntimeDesigner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "template.json");
        try
        {
            RuntimeLayoutSerializer.Save(path, session.Document);
            string before = File.ReadAllText(path);
            MustReject(() => RuntimeLayoutSerializer.Save(path, copy));
            Assert(File.ReadAllText(path) == before, "invalid save leaves original untouched");
            Assert(RuntimeLayoutSerializer.Load(path).Elements.Count == Enum.GetValues(typeof(RuntimeWidgetKind)).Length, "file load preserves widgets");
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>图片字节嵌入模板，原图文件不再需要。</summary>
    private static void TestImage()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        using (var bitmap = new Bitmap(80, 40))
        {
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Red);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var element = session.Add(RuntimeWidgetKind.Image, Point.Empty);
        try { element.ImageData = RuntimeLayoutSerializer.ReadImage(path); }
        finally { File.Delete(path); }
        var restored = RuntimeLayoutSerializer.Deserialize(RuntimeLayoutSerializer.Serialize(session.Document));
        using (var image = RuntimeLayoutSerializer.DecodeImage(restored.Elements[0].ImageData))
            Assert(image.Width == 80 && image.Height == 40, "template image works without source file");
    }

    /// <summary>验证新增字段不会改变旧方案的其他值，且布局随方案 JSON 往返。</summary>
    private static void TestSolutionConfig()
    {
        var old = Newtonsoft.Json.JsonConvert.DeserializeObject<TDJS_Vision.SolConfig>("{\"SolName\":\"station.Sol\",\"RunInterval\":35,\"NodeCount\":17}");
        Assert(old.RuntimeInterface == null, "legacy solution keeps default interface");
        string oldJson = Newtonsoft.Json.JsonConvert.SerializeObject(old);
        Assert(Newtonsoft.Json.Linq.JObject.Parse(oldJson)["RuntimeInterface"] == null, "unused layout is omitted for old solutions");
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        session.Add(RuntimeWidgetKind.Text, new Point(40, 40));
        old.RuntimeInterface = session.Document;
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(old);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<TDJS_Vision.SolConfig>(json);
        Assert(restored.RunInterval == 35 && restored.NodeCount == 17 && restored.SolName == "station.Sol", "interface persistence preserves unrelated solution values");
        Assert(restored.RuntimeInterface.Elements.Count == 1 && restored.RuntimeInterface.Elements[0].X == 40, "layout persists inside solution");
        var damaged = Newtonsoft.Json.Linq.JObject.Parse(json);
        damaged["RuntimeInterface"]["Elements"][0]["ImageData"] = "AA==";
        var oversized = new RuntimeLayoutDocument();
        for (int index = 0; index < 6; index++)
            oversized.Elements.Add(new RuntimeLayoutElement { ImageData = new string('A', 3 * 1024 * 1024) });
        string oversizedJson = Newtonsoft.Json.JsonConvert.SerializeObject(new TDJS_Vision.SolConfig { RuntimeInterface = oversized });
        foreach (string invalidJson in new[] { damaged.ToString(), oversizedJson })
        {
            bool rejected = false;
            try { Newtonsoft.Json.JsonConvert.DeserializeObject<TDJS_Vision.SolConfig>(invalidJson); }
            catch (Exception ex)
            {
                for (Exception current = ex; current != null; current = current.InnerException)
                    if (current is InvalidDataException) rejected = true;
            }
            Assert(rejected, "damaged image or oversized layout must be rejected before solution reset");
        }
        foreach (string invalid in new[] { "{\"Version\":99}", "{\"Elements\":null}" })
        {
            bool rejected = false;
            try { Newtonsoft.Json.JsonConvert.DeserializeObject<TDJS_Vision.SolConfig>("{\"RuntimeInterface\":" + invalid + "}"); }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                rejected = ex is InvalidDataException;
            }
            Assert(rejected, "invalid solution layout is rejected during deserialization before reset");
        }
    }

    /// <summary>复制快捷键必须处理数量边界，长名称仍可正常复制。</summary>
    private static void TestKeyboardLimits()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var element = session.Add(RuntimeWidgetKind.Text, Point.Empty);
        session.Edit(doc => doc.Elements[0].Name = new string('A', 120));
        var copy = session.Duplicate(element.Id);
        Assert(copy.Name.Length <= 120 && copy.Name.EndsWith(" 副本"), "duplicate truncates long source name");
        session.Edit(doc =>
        {
            while (doc.Elements.Count < 200) doc.Elements.Add(new RuntimeLayoutElement());
        });
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.Bind(session);
            canvas.SelectElement(element.Id);
            string error = null;
            canvas.EditError += message => error = message;
            var method = typeof(RuntimeDesignCanvas).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object result = method.Invoke(canvas, new object[] { new Message(), Keys.Control | Keys.D });
            Assert((bool)result && !string.IsNullOrEmpty(error), "keyboard limit is reported without unhandled exception");
            Assert(session.Document.Elements.Count == 200, "failed keyboard edit preserves document");
        }
    }

    /// <summary>使用真实窗体布局和绘制，在常见分辨率检查初始化和预览切换。</summary>
    private static void TestForm()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        session.Add(RuntimeWidgetKind.Image, new Point(40, 40));
        session.Add(RuntimeWidgetKind.Elapsed, new Point(520, 40));
        session.Add(RuntimeWidgetKind.Text, new Point(520, 180));
        session.Add(RuntimeWidgetKind.Signal, new Point(520, 280));
        Console.WriteLine("Form: construct");
        using (var form = new RuntimeInterfaceEditorForm(session.Document, "验证方案", document => true))
        {
            Console.WriteLine("Form: constructed");
            foreach (var size in new[] { new Size(1440, 900), new Size(1100, 720) })
            {
                form.Size = size;
                Console.WriteLine("Form: show " + size.Width);
                form.Show();
                Console.WriteLine("Form: shown");
                Application.DoEvents();
                var workspace = (Panel)typeof(RuntimeInterfaceEditorForm).GetField("workspace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form);
                Assert(!workspace.HorizontalScroll.Visible && !workspace.VerticalScroll.Visible, "fit page must not retain unnecessary scrollbars");
                using (var screenshot = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, form.Size));
                    screenshot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设计器-" + size.Width + ".png"));
                }
                Assert(form.Controls.Count > 0, "editor creates real controls");
                var zoom = (ToolStripComboBox)typeof(RuntimeInterfaceEditorForm).GetField("zoomCombo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form);
                zoom.SelectedItem = "100%";
                Application.DoEvents();
                Assert(workspace.HorizontalScroll.Visible, "fixed zoom permits scrolling larger page");
                zoom.SelectedIndex = 0;
                Application.DoEvents();
                Assert(!workspace.HorizontalScroll.Visible && !workspace.VerticalScroll.Visible, "returning to fit clears both scrollbars");
            }
            form.Close();
        }
    }

    /// <summary>真实画布移动必须落在绝对网格上，不能按相对增量错开格线。</summary>
    private static void TestCanvasInteraction()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var element = session.Add(RuntimeWidgetKind.Text, new Point(43, 53));
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.Bind(session);
            canvas.SetZoom(1);
            canvas.CreateControl();
            var convert = typeof(RuntimeDesignCanvas).GetMethod("ToScreen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rectangle = (RectangleF)convert.Invoke(canvas, new object[] { element.Bounds });
            int x = (int)rectangle.X + 20, y = (int)rectangle.Y + 20;
            CanvasMouse(canvas, "OnMouseDown", x, y);
            CanvasMouse(canvas, "OnMouseMove", x + 10, y + 10);
            Assert(element.X == 43 && element.Y == 53, "drag preview must not mutate saved bounds");
            CanvasMouse(canvas, "OnMouseUp", x + 10, y + 10);
            Assert(element.X == 50 && element.Y == 60, "move snaps absolute coordinates to visible ten-pixel grid");
            session.Undo();
            Assert(session.Document.Elements[0].X == 43, "whole drag is one undo transaction");
        }
    }

    /// <summary>调用真实画布消息处理，在不依赖物理鼠标的测试中验证交互事务。</summary>
    private static void CanvasMouse(RuntimeDesignCanvas canvas, string method, int x, int y)
    {
        typeof(RuntimeDesignCanvas).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
    }

    /// <summary>检查边缘、中心、缩放阈值与固定边，防止视觉对齐与提交坐标分离。</summary>
    private static void TestSmartAlignment()
    {
        var doc = new RuntimeLayoutDocument();
        doc.Elements.Add(new RuntimeLayoutElement { X = 100, Y = 100, Width = 200, Height = 100 });
        var start = new Rectangle(103, 250, 80, 40);
        var result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, true);
        Assert(result.Bounds.X == 100 && result.Guides.Any(g => g.Vertical && g.Position == 100), "left edge alignment emits vertical guide");
        start = new Rectangle(400, 103, 80, 40);
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, true);
        Assert(result.Bounds.Y == 100 && result.Guides.Any(g => !g.Vertical && g.Position == 100), "top edge alignment emits horizontal guide");
        start = new Rectangle(157, 250, 80, 40);
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, true);
        Assert(result.Bounds.X == 160 && result.Guides.Any(g => g.Vertical && g.Position == 200), "center alignment uses actual control center");
        start = new Rectangle(223, 250, 80, 40);
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, true);
        Assert(result.Bounds.X == 220, "right edge alignment preserves width");
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, false);
        Assert(result.Bounds == start, "disabled snapping permits exact free movement");
        var original = new Rectangle(50, 123, 200, 100);
        result = RuntimeLayoutAlignment.Resolve(doc, null, Rectangle.FromLTRB(102, 123, 250, 223), original, 7, 1, true);
        Assert(result.Bounds == new Rectangle(100, 123, 150, 100), "left resize anchors right top and bottom edges");
        result = RuntimeLayoutAlignment.Resolve(doc, null, Rectangle.FromLTRB(249, 123, 250, 223), original, 7, 1, true);
        Assert(result.Bounds.Right == 250 && result.Bounds.Width == 48, "minimum width never shifts fixed edge");
        start = new Rectangle(1277, 711, 80, 40);
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 1, true);
        Assert(result.Bounds == new Rectangle(1200, 680, 80, 40), "clamping preserves full widget inside page");
        doc.Elements[0].X = 103;
        start = new Rectangle(107, 250, 80, 40);
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 2, true);
        Assert(result.Bounds.X == 110, "zoomed in snap excludes target eight screen pixels away");
        result = RuntimeLayoutAlignment.Resolve(doc, null, start, start, -1, 0.5f, true);
        Assert(result.Bounds.X == 103, "zoomed out snap accepts target two screen pixels away");
    }

    /// <summary>系统拖放期间只显示预览；离开不添加，松手落点与辅助反馈相同。</summary>
    private static void TestDropFeedback()
    {
        using (var host = new Form())
        using (var canvas = new RuntimeDesignCanvas())
        {
            var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
            session.Add(RuntimeWidgetKind.Text, new Point(100, 100));
            canvas.Bind(session);
            canvas.SetZoom(1);
            host.Controls.Add(canvas);
            host.CreateControl(); canvas.CreateControl();
            var data = new DataObject();
            data.SetData(RuntimeDesignCanvas.DragFormat, RuntimeWidgetKind.Signal.ToString());
            var convert = typeof(RuntimeDesignCanvas).GetMethod("ToScreen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rect = (RectangleF)convert.Invoke(canvas, new object[] { new Rectangle(103, 300, 200, 80) });
            Point location = canvas.PointToScreen(Point.Round(rect.Location));
            var drag = new DragEventArgs(data, 0, location.X, location.Y, DragDropEffects.Copy, DragDropEffects.None);
            CanvasEvent(canvas, "OnDragEnter", drag);
            Assert(drag.Effect == DragDropEffects.Copy && session.Document.Elements.Count == 1, "drag enter previews without document changes");
            CanvasEvent(canvas, "OnDragOver", drag);
            using (var screenshot = new Bitmap(canvas.Width, canvas.Height))
            {
                canvas.DrawToBitmap(screenshot, canvas.ClientRectangle);
                screenshot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设计器-拖入反馈.png"));
                int guidePixels = 0;
                for (int y = 0; y < screenshot.Height; y += 2)
                    for (int x = 0; x < screenshot.Width; x += 2)
                    {
                        Color pixel = screenshot.GetPixel(x, y);
                        if (pixel.B > 180 && pixel.G > 160 && pixel.R < 130) guidePixels++;
                    }
                Assert(guidePixels > 10, "alignment feedback actually renders cyan guide pixels");
            }
            CanvasEvent(canvas, "OnDragLeave", EventArgs.Empty);
            Assert(session.Document.Elements.Count == 1, "drag leave does not create widget");
            CanvasEvent(canvas, "OnDragDrop", drag);
            Assert(session.Document.Elements.Count == 2 && session.Document.Elements[1].X == 100 && session.Document.Elements[1].Y == 300,
                "drop commits the snapped preview location");
            session.Undo();
            Assert(session.Document.Elements.Count == 1, "drop is a single undoable operation");
            location = canvas.PointToScreen(Point.Empty);
            drag = new DragEventArgs(data, 0, location.X, location.Y, DragDropEffects.Copy, DragDropEffects.None);
            CanvasEvent(canvas, "OnDragDrop", drag);
            Assert(drag.Effect == DragDropEffects.None && session.Document.Elements.Count == 1, "outside workspace cannot accept widgets");
        }
    }

    /// <summary>向真实画布传递系统拖放事件。</summary>
    private static void CanvasEvent(RuntimeDesignCanvas canvas, string method, EventArgs args)
    {
        typeof(RuntimeDesignCanvas).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(canvas, new object[] { args });
    }

    /// <summary>真实缩放在不同显示比例下固定左上边，提交后可以单步撤销。</summary>
    private static void TestResizeFeedback()
    {
        foreach (float zoom in new[] { 0.5f, 1f, 2f })
        {
            var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
            var element = session.Add(RuntimeWidgetKind.Text, new Point(100, 100));
            session.Add(RuntimeWidgetKind.Text, new Point(500, 300));
            using (var canvas = new RuntimeDesignCanvas())
            {
                canvas.Bind(session);
                canvas.SetViewport(new Size(1440, 900));
                canvas.SetZoom(zoom);
                canvas.SelectElement(element.Id);
                canvas.CreateControl();
                var convert = typeof(RuntimeDesignCanvas).GetMethod("ToScreen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rect = (RectangleF)convert.Invoke(canvas, new object[] { element.Bounds });
                int x = (int)Math.Round(rect.Right), y = (int)Math.Round(rect.Top + rect.Height / 2);
                CanvasMouse(canvas, "OnMouseDown", x, y);
                CanvasMouse(canvas, "OnMouseMove", x + (int)Math.Round(398 * zoom), y);
                Assert(element.Width == 280 && element.X == 100, "resize preview leaves stored geometry unchanged");
                if (zoom == 1)
                    using (var screenshot = new Bitmap(canvas.Width, canvas.Height))
                    {
                        canvas.DrawToBitmap(screenshot, canvas.ClientRectangle);
                        screenshot.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设计器-缩放反馈.png"));
                    }
                CanvasMouse(canvas, "OnMouseUp", x + (int)Math.Round(398 * zoom), y);
                Assert(element.Bounds == new Rectangle(100, 100, 680, 80), "resize aligns matching right edges at every zoom");
                session.Undo();
                Assert(session.Document.Elements[0].Bounds == new Rectangle(100, 100, 280, 80), "one undo restores original resize bounds");
            }
        }
    }

    /// <summary>验证输入拒绝，避免把其他运行时异常视为校验成功。</summary>
    private static void MustReject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { assertions++; return; }
        throw new Exception("Invalid template was accepted");
    }

    /// <summary>报告失败的用户可见行为。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }
}
