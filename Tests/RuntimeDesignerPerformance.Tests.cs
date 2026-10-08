using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>独立计时设计器交互，不启动生产框架、不保存体验模板。</summary>
internal static class RuntimeDesignerPerformanceTests
{
    /// <summary>访问被测控件事件，仅在内存测试窗体内执行。</summary>
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>读取指定模板并记录选择、绘制、提交三段耗时。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            var document = RuntimeLayoutSerializer.Load(args[0]);
            Console.WriteLine("Template: widgets={0}, imageChars={1}", document.Elements.Count,
                document.Elements.Sum(e => e.ImageData == null ? 0 : e.ImageData.Length));
            using (var form = new RuntimeInterfaceEditorForm(document, "性能验证", null))
            {
                var canvas = (RuntimeDesignCanvas)typeof(RuntimeInterfaceEditorForm).GetField("canvas", PrivateInstance).GetValue(form);
                var session = (RuntimeLayoutSession)typeof(RuntimeInterfaceEditorForm).GetField("session", PrivateInstance).GetValue(form);
                canvas.SetViewport(new Size(1440, 900));
                canvas.SetZoom(1);
                using (var bitmap = new Bitmap(canvas.Width, canvas.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var paint = typeof(RuntimeDesignCanvas).GetMethod("OnPaint", PrivateInstance);
                    Action draw = () => paint.Invoke(canvas, new object[] { new PaintEventArgs(graphics, canvas.ClientRectangle) });
                    var id = session.Document.Elements.Last().Id;
                    Measure("Select widget", 12, () => { canvas.SelectElement(null); canvas.SelectElement(id); });
                    Measure("Paint warm", 60, draw);
                    canvas.ShowGrid = false;
                    Measure("Paint without workspace grid", 60, draw);
                    canvas.ShowGrid = true;
                    var element = session.Find(id);
                    Rectangle bounds = element.Bounds;
                    Measure("Commit bounds", 12, () => session.SetBounds(id, new Rectangle(bounds.X == session.Find(id).X ? Math.Max(0, bounds.X - 10) : bounds.X,
                        bounds.Y, bounds.Width, bounds.Height)));
                    canvas.SelectElement(id);
                    typeof(RuntimeDesignCanvas).GetField("dragging", PrivateInstance).SetValue(canvas, true);
                    typeof(RuntimeDesignCanvas).GetField("dragBounds", PrivateInstance).SetValue(canvas, bounds);
                    Measure("Drag paint warm", 60, draw);
                    int frame = 0;
                    Measure("Move geometry and paint", 60, () =>
                    {
                        Rectangle proposed = bounds;
                        proposed.Offset((frame++ % 10) * 2, 0);
                        var feedback = RuntimeLayoutAlignment.Resolve(session.Document, id, proposed, bounds, -1, 1, true);
                        typeof(RuntimeDesignCanvas).GetField("alignment", PrivateInstance).SetValue(canvas, feedback);
                        typeof(RuntimeDesignCanvas).GetField("dragBounds", PrivateInstance).SetValue(canvas, feedback.Bounds);
                        draw();
                    });
                    var image = session.Document.Elements.First(e => e.Kind == RuntimeWidgetKind.Image);
                    canvas.SelectElement(image.Id);
                    typeof(RuntimeDesignCanvas).GetField("handleIndex", PrivateInstance).SetValue(canvas, 4);
                    Measure("Resize image and paint", 60, () =>
                    {
                        var proposed = new Rectangle(image.X, image.Y, image.Width + (frame++ % 10), image.Height);
                        var feedback = RuntimeLayoutAlignment.Resolve(session.Document, image.Id, proposed, image.Bounds, 4, 1, false);
                        typeof(RuntimeDesignCanvas).GetField("alignment", PrivateInstance).SetValue(canvas, feedback);
                        typeof(RuntimeDesignCanvas).GetField("dragBounds", PrivateInstance).SetValue(canvas, feedback.Bounds);
                        draw();
                    });
                    typeof(RuntimeDesignCanvas).GetField("dragging", PrivateInstance).SetValue(canvas, false);
                }
            }
            VerifyRendering();
            VerifyCacheLifetime();
            VerifyClickDoesNotMove();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>选中未对齐网格的控件时不允许零位移鼠标消息触发吸附。</summary>
    private static void VerifyClickDoesNotMove()
    {
        var document = new RuntimeLayoutDocument();
        document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, X = 23, Y = 17 });
        var session = new RuntimeLayoutSession(document);
        using (var canvas = new RuntimeDesignCanvas())
        {
            canvas.Bind(session);
            var origin = (PointF)typeof(RuntimeDesignCanvas).GetProperty("PageOrigin", PrivateInstance).GetValue(canvas);
            var mouse = new MouseEventArgs(MouseButtons.Left, 1, (int)origin.X + 50, (int)origin.Y + 50, 0);
            foreach (string method in new[] { "OnMouseDown", "OnMouseMove", "OnMouseUp" })
                typeof(RuntimeDesignCanvas).GetMethod(method, PrivateInstance).Invoke(canvas, new object[] { mouse });
            if (session.CanUndo || session.Document.Elements[0].Bounds != document.Elements[0].Bounds)
                throw new Exception("A stationary click must not snap or create undo history.");
            Console.WriteLine("PASS: stationary selection preserves original geometry");
        }
    }

    /// <summary>通过像素和绘制次数验证纯色页面与重复绘制缓存。</summary>
    private static void VerifyRendering()
    {
        var document = new RuntimeLayoutDocument { Width = 640, Height = 480 };
        document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text, X = 20, Y = 20 });
        using (var canvas = new RuntimeDesignCanvas())
        using (var bitmap = new Bitmap(800, 650))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var session = new RuntimeLayoutSession(document);
            var renderer = new CountingRenderer();
            canvas.Register(renderer);
            canvas.Bind(session);
            canvas.SetViewport(bitmap.Size);
            canvas.SetZoom(1);
            var paint = typeof(RuntimeDesignCanvas).GetMethod("OnPaint", PrivateInstance);
            Action draw = () => paint.Invoke(canvas, new object[] { new PaintEventArgs(graphics, canvas.ClientRectangle) });
            draw();
            var origin = (PointF)typeof(RuntimeDesignCanvas).GetProperty("PageOrigin", PrivateInstance).GetValue(canvas);
            Color gray = RuntimeLayoutSerializer.ParseColor(document.Background);
            for (int x = 300; x < 330; x++)
                if (bitmap.GetPixel((int)origin.X + x, (int)origin.Y + 300).ToArgb() != gray.ToArgb())
                    throw new Exception("Runtime page must have no grid.");
            draw(); draw();
            if (renderer.Count != 1) throw new Exception("Unchanged widgets must reuse rendered surfaces.");
            session.SetBounds(session.Document.Elements[0].Id, new Rectangle(40, 30, 240, 80));
            draw();
            if (renderer.Count != 1) throw new Exception("Moving must reuse the unchanged widget surface.");
            session.Edit(doc => doc.Elements[0].Text = "已更新");
            draw();
            if (renderer.Count != 2) throw new Exception("Text edits must invalidate the widget surface.");
            canvas.SetZoom(0.5f); draw();
            if (renderer.Count != 3) throw new Exception("Zoom must rerender at screen resolution.");
            session.Undo(); draw();
            if (renderer.Count != 4) throw new Exception("Undo must render the restored text.");
            session.Redo(); draw();
            if (renderer.Count != 5) throw new Exception("Redo must render the updated text.");
            canvas.Register(renderer = new CountingRenderer()); draw();
            if (renderer.Count != 1) throw new Exception("Renderer replacement must clear surfaces.");
            var element = session.Document.Elements[0];
            var copy = element.Copy();
            copy.Text = "草稿";
            if (element.Text == copy.Text || !ReferenceEquals(copy.ImageData, element.ImageData))
                throw new Exception("Draft must isolate scalar edits and share immutable image content.");
            foreach (var property in typeof(RuntimeLayoutElement).GetProperties().Where(p => p.CanWrite))
                if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string) && property.PropertyType != typeof(RuntimeDataBinding))
                    throw new Exception("Copy must be extended for mutable property: " + property.Name);
            element.Binding = new RuntimeDataBinding { ProcessId = 1, NodePath = new[] { 62, 8 }, PropertyPath = "OutputImage" };
            copy = element.Copy();
            copy.Binding.NodePath[0] = 99;
            if (ReferenceEquals(copy.Binding, element.Binding) || element.Binding.NodePath[0] != 62)
                throw new Exception("Draft must deep copy the binding and its mutable scope path.");
            Console.WriteLine("PASS: plain runtime background and rendering cache");
        }
    }

    /// <summary>覆盖缓存预算、快速缩放结束、外观更新、删除和释放。</summary>
    private static void VerifyCacheLifetime()
    {
        using (var cache = new RuntimeWidgetSurfaceCache())
        using (var bitmap = new Bitmap(800, 650))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var renderer = new CountingRenderer();
            var item = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Text };
            Action<bool> draw = fast => cache.Draw(graphics, renderer, item, new Size(240, 80), 1, fast);
            draw(false); draw(false);
            if (renderer.Count != 1) throw new Exception("Surface cache miss on identical content.");
            draw(true); draw(false);
            if (renderer.Count != 3) throw new Exception("Resize release must restore final quality.");
            item.Foreground = "#FF0000"; draw(false);
            if (bitmap.GetPixel(20, 20).R != 255) throw new Exception("Appearance edit was not rendered.");
            item.ImageData = "first"; draw(false);
            item.ImageData = "second"; draw(false);
            if (renderer.Count != 6) throw new Exception("Image changes must invalidate the surface.");
            item.ImageData = null;
            for (int i = 0; i < 10; i++)
            {
                item.Id = i.ToString();
                cache.Draw(graphics, renderer, item, new Size(1600, 1200), 1, false);
            }
            long retained = (long)typeof(RuntimeWidgetSurfaceCache).GetField("bytes", PrivateInstance).GetValue(cache);
            if (retained > 32L * 1024 * 1024) throw new Exception("Surface cache exceeded 32 MB.");
            cache.Synchronize(new RuntimeLayoutDocument());
            retained = (long)typeof(RuntimeWidgetSurfaceCache).GetField("bytes", PrivateInstance).GetValue(cache);
            if (retained != 0) throw new Exception("Deleted widgets retained surfaces.");
            cache.Dispose(); cache.Dispose();
            renderer = new CountingRenderer();
            for (int frame = 0; frame < 3; frame++)
            {
                cache.BeginFrame();
                for (int i = 0; i < 9; i++)
                {
                    item.Id = i.ToString();
                    cache.Draw(graphics, renderer, item, new Size(1024, 1024), 1, false);
                }
                if (renderer.Count != 9 + frame)
                    throw new Exception("Over-budget workset must not rebuild all nine widgets each frame.");
            }
            var dynamicRenderer = new DynamicRenderer();
            cache.Draw(graphics, dynamicRenderer, item, new Size(240, 80), 1, false);
            cache.Draw(graphics, dynamicRenderer, item, new Size(240, 80), 1, false);
            if (dynamicRenderer.Count != 2) throw new Exception("Extensions must opt in to surface caching.");
            Console.WriteLine("PASS: surface invalidation, resize quality, memory budget and cleanup");
        }
    }

    /// <summary>未选择缓存的动态扩展绘制器，每帧都必须得到调用。</summary>
    private sealed class DynamicRenderer : RuntimeWidgetRenderer
    {
        /// <summary>收到的帧数。</summary>
        public int Count { get; private set; }
        /// <summary>扩展组件类型。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Text;
        /// <summary>每帧更新自己的运行时状态。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element) { Count++; }
    }

    /// <summary>记录真实绘制器调用次数，避免用不稳定的耗时阈值判断缓存正确性。</summary>
    private sealed class CountingRenderer : RuntimeWidgetRenderer, IRuntimeCacheableWidgetRenderer
    {
        /// <summary>真实绘制次数。</summary>
        public int Count { get; private set; }
        /// <summary>替换文本绘制器用于隔离验证。</summary>
        public override RuntimeWidgetKind Kind => RuntimeWidgetKind.Text;
        /// <summary>绘制固定色块并递增调用次数。</summary>
        public override void Draw(Graphics graphics, RuntimeLayoutElement element)
        {
            Count++;
            using (var brush = new SolidBrush(RuntimeLayoutSerializer.ParseColor(element.Foreground)))
                graphics.FillRectangle(brush, 0, 0, element.Width, element.Height);
        }
    }

    /// <summary>预热一次后报告平均和尾部延迟，不将计时与真实显示帧率混为一谈。</summary>
    private static void Measure(string name, int count, Action action)
    {
        action();
        var times = new double[count];
        for (int i = 0; i < count; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            times[i] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        Console.WriteLine("{0}: n={1}, avg={2:F2} ms, p95={3:F2} ms, max={4:F2} ms", name, count,
            times.Average(), times[(int)Math.Ceiling(count * 0.95) - 1], times[count - 1]);
    }
}
