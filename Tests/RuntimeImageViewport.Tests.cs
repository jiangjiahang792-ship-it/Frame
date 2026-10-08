using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>图像临时视口与显示清除的无设备测试。</summary>
internal static class RuntimeImageViewportTests
{
    /// <summary>已通过的行为断言数。</summary>
    private static int assertions;
    /// <summary>使用屏幕外位图和可控后台来源，不启动窗体或生产设备。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            TestGeometry();
            TestRenderers();
            TestNullDrain();
            TestClearCompletedAndInflight();
            TestDisposeLateResult();
            Console.WriteLine("PASS: " + assertions + " image viewport backend assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); return 1; }
    }

    /// <summary>失败时报告具体行为。</summary>
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); assertions++; }
    /// <summary>容许浮点计算的一小部分像素误差。</summary>
    private static bool Near(float a, float b) => Math.Abs(a - b) < .002f;
    /// <summary>比较整个显示矩形。</summary>
    private static bool Near(RectangleF a, RectangleF b) => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Width, b.Width) && Near(a.Height, b.Height);

    /// <summary>验证适应、鼠标锚点、倍率上下限、归一化偏移和清除生命周期。</summary>
    private static void TestGeometry()
    {
        var view = new RuntimeImageViewport(); var control = new Size(204, 204); var image = new Size(400, 200);
        Check(view.Zoom == 1 && !view.IsCleared && Near(view.GetImageBounds(image, control), new RectangleF(2, 52, 200, 100)), "初始等比居中并扣除两像素边框");
        PointF anchor = new PointF(52, 82); var before = view.GetImageBounds(image, control);
        view.ZoomAt(2, anchor, control); var after = view.GetImageBounds(image, control);
        Check(Near((anchor.X - before.X) / before.Width, (anchor.X - after.X) / after.Width)
            && Near((anchor.Y - before.Y) / before.Height, (anchor.Y - after.Y) / after.Height), "缩放必须保持鼠标下原图位置不变");
        Check(view.Zoom == 2 && Near(after.Width / after.Height, 2), "缩放保持图像比例");
        view.Pan(new PointF(20, -10), control);
        Check(Near(view.GetImageBounds(image, control).X, after.X + 20) && Near(view.GetImageBounds(image, control).Y, after.Y - 10), "平移采用控件本地像素");
        var panned = view.GetImageBounds(image, control); var resized = view.GetImageBounds(image, new Size(404, 404));
        Check(Near(resized.X - 2, (panned.X - 2) * 2) && Near(resized.Y - 2, (panned.Y - 2) * 2), "控件尺寸变化按内容区比例保留偏移");
        view.ZoomAt(float.MaxValue, anchor, control); Check(view.Zoom == 32, "缩放上限32");
        view.ZoomAt(float.Epsilon, anchor, control); Check(Near(view.Zoom, .1f), "缩放下限0.1");
        view.Clear(); Check(view.IsCleared && view.Zoom == 1 && Near(view.GetImageBounds(image, control), before), "清除同时恢复初始几何");
        view.Reset(); Check(view.IsCleared, "居中不能恢复已清除的旧图");
        view.ZoomAt(3, anchor, control); var hiddenBounds = view.GetImageBounds(image, control);
        view.NewImage(); Check(!view.IsCleared && view.Zoom == 3 && Near(view.GetImageBounds(image, control), hiddenBounds), "新图恢复显示但不重置当前缩放平移");
        Check(view.GetImageBounds(Size.Empty, control).IsEmpty && view.GetImageBounds(image, new Size(4, 4)).IsEmpty, "空图和空内容区返回空几何");
    }

    /// <summary>在内存生成纯红图片，不读取用户文件。</summary>
    private static Bitmap RedImage(int width = 200, int height = 100)
    { var image = new Bitmap(width, height); using (var g = Graphics.FromImage(image)) g.Clear(Color.Red); return image; }
    /// <summary>内存图片编码供静态绘制路径验证。</summary>
    private static string EncodeImage(Bitmap bitmap)
    { using (var stream = new MemoryStream()) { bitmap.Save(stream, ImageFormat.Png); return Convert.ToBase64String(stream.ToArray()); } }
    /// <summary>绘制到屏幕外位图，绝不调用桌面窗口。</summary>
    private static Bitmap Render(IRuntimeWidgetRenderer renderer, RuntimeLayoutElement element)
    {
        var bitmap = new Bitmap(element.Width, element.Height);
        using (var graphics = Graphics.FromImage(bitmap)) { graphics.InterpolationMode = InterpolationMode.NearestNeighbor; renderer.Draw(graphics, element); }
        return bitmap;
    }

    /// <summary>静态图、实时图和ROI几何一致，清除不留下占位符、文字或越界叠加。</summary>
    private static void TestRenderers()
    {
        var view = new RuntimeImageViewport();
        var element = new RuntimeLayoutElement { Id = "A", Kind = RuntimeWidgetKind.Image, Width = 204, Height = 204, Background = "#303030" };
        using (var original = RedImage())
        using (var staticRenderer = new RuntimeImageRenderer(id => view))
        using (var oldRenderer = new RuntimeImageRenderer())
        {
            element.ImageData = EncodeImage(original);
            using (var normal = Render(staticRenderer, element))
            using (var legacy = Render(oldRenderer, element))
                Check(normal.GetPixel(100, 52).R == legacy.GetPixel(100, 52).R && normal.GetPixel(100, 102).R == 255, "无视口回调时保持旧静态图行为");
            view.ZoomAt(2, new PointF(102, 102), new Size(204, 204));
            var overlay = new Overlay { PaintOutside = true };
            using (var value = new RuntimeValueUpdate { ElementId = "A", Image = RedImage(), ImageSourceSize = new Size(4000, 2000), Roi = overlay })
            using (var live = new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Image, id => value, id => view))
            {
                element.Binding = new RuntimeDataBinding(); element.RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi };
                using (var rendered = Render(live, element))
                {
                    Check(Near(overlay.Bounds, view.GetImageBounds(value.Image.Size, new Size(204, 204))) && overlay.Source == new Size(4000, 2000), "实时ROI使用同一缩放边界及降采样前原图坐标");
                    Check(rendered.GetPixel(0, 0).ToArgb() == Color.FromArgb(28, 28, 28).ToArgb()
                        && rendered.GetPixel(1, 1).ToArgb() == Color.FromArgb(41, 41, 41).ToArgb()
                        && rendered.GetPixel(2, 2).ToArgb() == Color.Lime.ToArgb(), "ROI即使画满控件也不能覆盖两层边框");
                }
                element.Binding = null;
                using (var rendered = Render(live, element))
                    Check(Near(overlay.Bounds, view.GetImageBounds(original.Size, new Size(204, 204))) && overlay.Source == original.Size, "静态底图加实时ROI也使用视口几何");
                element.Binding = new RuntimeDataBinding(); value.Status = "失败状态不应出现";
                view.Clear(); int draws = overlay.Draws;
                using (var cleared = Render(live, element))
                    Check(cleared.GetPixel(102, 102).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb() && overlay.Draws == draws, "清除后不显示旧图、ROI或状态文字");
                element.ImageData = null;
                using (var cleared = Render(staticRenderer, element))
                    Check(cleared.GetPixel(102, 102).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb()
                        && cleared.GetPixel(25, 25).ToArgb() == Color.FromArgb(48, 48, 48).ToArgb(), "清除后不显示棋盘或占位图标");
            }
        }
    }

    /// <summary>只生成合法绑定的显示页面，不依赖实际节点。</summary>
    private static RuntimeDesignCanvas Canvas()
    {
        var document = new RuntimeLayoutDocument();
        foreach (string id in new[] { "A", "B" }) document.Elements.Add(new RuntimeLayoutElement
        { Id = id, Kind = RuntimeWidgetKind.Image, Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, NodePath = new[] { 1 }, PropertyPath = "Image" } });
        var canvas = new RuntimeDesignCanvas(); canvas.Bind(new RuntimeLayoutSession(document)); return canvas;
    }
    /// <summary>读取控制器的纯显示结果，用于资源所有权断言。</summary>
    private static RuntimeValueUpdate Displayed(RuntimeLiveViewController controller, string id)
    {
        var map = (Dictionary<string, RuntimeValueUpdate>)typeof(RuntimeLiveViewController).GetField("displayed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
        RuntimeValueUpdate value; return map.TryGetValue(id, out value) ? value : null;
    }
    /// <summary>反射读取预算和在途标志，不执行生产逻辑。</summary>
    private static T Field<T>(RuntimeLiveViewController controller, string name) => (T)typeof(RuntimeLiveViewController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
    /// <summary>模拟画布清除事件，不触发菜单或桌面输入。</summary>
    private static void Clear(RuntimeDesignCanvas canvas, string id)
    {
        canvas.GetImageViewport(id).Clear();
        var action = (Action<string>)typeof(RuntimeDesignCanvas).GetField("ImageClearRequested", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(canvas);
        action?.Invoke(id);
    }
    /// <summary>驱动显示定时器直到状态就绪，后台转换始终由控制器安排。</summary>
    private static void Pump(RuntimeLiveViewController controller, Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < 5000) { controller.Tick(); Thread.Sleep(1); }
        Check(condition(), "后台显示条件须在五秒内完成");
    }
    /// <summary>等待后台批次结束但不消费，精确制造清除前已完成的批次。</summary>
    private static void WaitComplete(RuntimeLiveViewController controller)
    { Check(SpinWait.SpinUntil(() => Field<int>(controller, "working") == 0, 5000), "后台转换须结束"); }

    /// <summary>旧来源允许用空引用表示无增量，不能被新的批次过滤改成错误。</summary>
    private static void TestNullDrain()
    {
        using (var canvas = Canvas())
        using (var source = new Feed { ReturnNullWhenEmpty = true })
        using (var controller = new RuntimeLiveViewController(canvas, source))
        {
            controller.Tick(); WaitComplete(controller);
            Check(controller.LastError == null && Displayed(controller, "A") == null, "Drain返回null应保持无增量语义，不报告异常");
            var frame = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() }; var bitmap = frame.Image;
            source.Add(new Batch(frame)); Pump(controller, () => ReferenceEquals(Displayed(controller, "A")?.Image, bitmap));
            WaitComplete(controller); controller.Tick(); WaitComplete(controller);
            Check(controller.LastError == null && ReferenceEquals(Displayed(controller, "A")?.Image, bitmap), "空批次前后仍能显示新图且不会清空已显示结果");
        }
    }

    /// <summary>清除已显示、已完成和在途旧帧；保留其它控件并允许下一批新图恢复。</summary>
    private static void TestClearCompletedAndInflight()
    {
        using (var canvas = Canvas())
        using (var source = new Feed())
        using (var controller = new RuntimeLiveViewController(canvas, source))
        {
            var first = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() };
            var firstRoi = new Overlay(); var otherRoi = new Overlay();
            source.Add(new Batch(first, new RuntimeValueUpdate { ElementId = "A", IsRoiUpdate = true, Roi = firstRoi },
                new RuntimeValueUpdate { ElementId = "B", Image = RedImage() }, new RuntimeValueUpdate { ElementId = "B", IsRoiUpdate = true, Roi = otherRoi }));
            Pump(controller, () => Displayed(controller, "A")?.Roi == firstRoi && Displayed(controller, "B")?.Roi == otherRoi);
            var displayedA = Displayed(controller, "A"); var displayedB = Displayed(controller, "B");
            var pendingImage = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() }; var pendingRoi = new Overlay();
            var pending = new Batch(pendingImage, new RuntimeValueUpdate { ElementId = "A", IsRoiUpdate = true, Roi = pendingRoi });
            source.Add(pending); Pump(controller, () => pending.Entered.IsSet); WaitComplete(controller);
            Clear(canvas, "A");
            Check(Displayed(controller, "A") == null && displayedA.Image == null && firstRoi.Disposed, "清除必须释放当前图像和ROI显示帧");
            Check(pendingImage.Image == null && pendingRoi.Disposed, "清除必须剔除并释放已完成但未显示的目标帧");
            Check(ReferenceEquals(Displayed(controller, "B"), displayedB) && !otherRoi.Disposed && !source.Disposed, "清除不能影响其它控件或取消源订阅");
            Check(Field<long>(controller, "imageBytes") == 200 * 100 * 4 && Field<long>(controller, "roiBytes") == 128, "清除后图像和ROI内存计数只保留其它控件");
            var stale = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() }; var staleRoi = new Overlay();
            var otherNew = new RuntimeValueUpdate { ElementId = "B", Image = RedImage(100, 50) };
            var held = new Batch(stale, new RuntimeValueUpdate { ElementId = "A", IsRoiUpdate = true, Roi = staleRoi }, otherNew) { Block = true };
            source.Add(held); Pump(controller, () => held.Entered.IsSet);
            Clear(canvas, "A"); held.Release.Set(); WaitComplete(controller); controller.Tick();
            Check(stale.Image == null && staleRoi.Disposed && Displayed(controller, "A") == null && canvas.GetImageViewport("A").IsCleared,
                "清除前开始的在途旧图和ROI不得立即刷回");
            Check(Displayed(controller, "B").Image.Width == 100, "过滤在途目标帧不得丢弃同批其它控件");
            canvas.GetImageViewport("A").ZoomAt(2, new PointF(40, 30), new Size(240, 80));
            var fresh = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() }; var freshBitmap = fresh.Image;
            source.Add(new Batch(fresh)); Pump(controller, () => ReferenceEquals(Displayed(controller, "A")?.Image, freshBitmap));
            Check(!canvas.GetImageViewport("A").IsCleared && canvas.GetImageViewport("A").Zoom == 2, "下一轮有效图像恢复显示且保留用户缩放");
        }
    }

    /// <summary>关闭期间的迟到结果仍释放，并取消画布清除事件订阅。</summary>
    private static void TestDisposeLateResult()
    {
        using (var canvas = Canvas())
        using (var source = new Feed())
        {
            var late = new RuntimeValueUpdate { ElementId = "A", Image = RedImage() }; var roi = new Overlay();
            var held = new Batch(late, new RuntimeValueUpdate { ElementId = "A", IsRoiUpdate = true, Roi = roi }) { Block = true };
            source.Add(held); var controller = new RuntimeLiveViewController(canvas, source);
            Pump(controller, () => held.Entered.IsSet); controller.Dispose(); held.Release.Set(); WaitComplete(controller);
            Check(late.Image == null && roi.Disposed && source.Disposed, "关闭后转换完成的图像和ROI必须释放");
            var handlers = (Action<string>)typeof(RuntimeDesignCanvas).GetField("ImageClearRequested", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(canvas);
            Check(handlers == null || handlers.GetInvocationList().All(value => !ReferenceEquals(value.Target, controller)), "关闭必须解除画布清除事件，不能保留控制器");
        }
    }

    /// <summary>可暂停在Drain内部的纯显示批次。</summary>
    private sealed class Batch
    {
        /// <summary>本批独立结果。</summary>
        internal readonly RuntimeValueUpdate[] Values;
        /// <summary>后台已进入Drain。</summary>
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        /// <summary>确定性放行后台。</summary>
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        /// <summary>是否等待测试放行。</summary>
        internal bool Block;
        /// <summary>捕获结果所有权。</summary>
        internal Batch(params RuntimeValueUpdate[] values) { Values = values; }
    }
    /// <summary>只提供测试位图和ROI，不含任何生产引用。</summary>
    private sealed class Feed : IRuntimeBindingDataSource
    {
        /// <summary>待处理批次。</summary>
        private readonly ConcurrentQueue<Batch> queue = new ConcurrentQueue<Batch>();
        /// <summary>源是否被控制器关闭。</summary>
        internal bool Disposed;
        /// <summary>模拟既有来源用空引用表示本轮没有变化。</summary>
        internal bool ReturnNullWhenEmpty;
        /// <summary>加入下轮结果。</summary>
        internal void Add(Batch batch) { queue.Enqueue(batch); }
        /// <summary>逐批读取，允许测试暂停某次已开始的转换。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain()
        {
            Batch batch; if (!queue.TryDequeue(out batch)) return ReturnNullWhenEmpty ? null : new RuntimeValueUpdate[0];
            batch.Entered.Set(); if (batch.Block && !batch.Release.Wait(5000)) throw new TimeoutException("测试批次未放行");
            return batch.Values;
        }
        /// <summary>仅清理尚未由Drain接管的结果。</summary>
        public void Dispose() { Disposed = true; Batch batch; while (queue.TryDequeue(out batch)) foreach (var value in batch.Values) value.Dispose(); }
    }
    /// <summary>记录ROI几何和释放状态，可故意越界绘制以验证裁剪。</summary>
    private sealed class Overlay : IRuntimeRoiOverlay
    {
        /// <summary>记录释放状态。</summary>
        internal bool Disposed;
        /// <summary>是否尝试绘制到边框之外。</summary>
        internal bool PaintOutside;
        /// <summary>绘制次数。</summary>
        internal int Draws;
        /// <summary>收到的图像矩形。</summary>
        internal RectangleF Bounds;
        /// <summary>收到的原图尺寸。</summary>
        internal Size Source;
        /// <summary>可核对的预算。</summary>
        public long EstimatedBytes => 128;
        /// <summary>只画测试颜色，不调用框架算法。</summary>
        public void Draw(Graphics graphics, RectangleF bounds, Size source, Size control)
        { Draws++; Bounds = bounds; Source = source; if (PaintOutside) graphics.FillRectangle(Brushes.Lime, new Rectangle(Point.Empty, control)); }
        /// <summary>记录独立结果已释放。</summary>
        public void Dispose() { Disposed = true; }
    }
}
