using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json.Linq;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Forms.RuntimeDesigner;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

/// <summary>ROI订阅的无设备回归入口，不启动生产方案。</summary>
internal static class RuntimeRoiBindingTests
{
    /// <summary>成功断言计数。</summary>
    private static int checks;

    /// <summary>执行文档和独立刷新回归。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Check(!new ProcessRunResult(false, false, "旧通知").IsCancelled &&
                typeof(ProcessRunResult).GetConstructor(new[] { typeof(bool), typeof(bool), typeof(string) }) != null,
                "旧三参数构造仍存在且失败通知默认不是取消");
            Check(new ProcessRunResult(false, false, "取消通知", isCancelled: true).IsCancelled,
                "只有显式标记的通知才表示取消");
            VerifyDocument();
            VerifyScopedRoiCapture();
            VerifyStoppedRoiRetention();
            VerifyStoppedImageAndElapsed();
            VerifyCancelledPartialRound();
            VerifyIndependentDisplayUpdates();
            VerifyStaticAndSnapshotPixels();
            VerifyLargeImage();
            VerifyDynamicAndInvalidRoi();
            VerifyLateClose();
            var solution = (Lazy<Solution>)typeof(Solution).GetField("_lazy", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(!solution.IsValueCreated, "后端测试及停止状态判断不能触发生产方案单例初始化");
            Console.WriteLine("PASS: " + checks + " ROI binding assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>半轮仅图像成功时，不能用新图像搭配上一轮ROI，包括上一轮仍在收件槽的情形。</summary>
    private static void VerifyCancelledPartialRound()
    {
        foreach (bool consumed in new[] { true, false })
        {
            var process = EmptyProcess(14);
            using (var imageNode = new RoiNode(process, 8))
            using (var roiNode = new RoiNode(process, 9))
            {
                process.Nodes.Add(imageNode); process.Nodes.Add(roiNode);
                imageNode.ReplaceResult(new NodeResultImageSource());
                var document = new RuntimeLayoutDocument();
                document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image,
                    Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 14, NodePath = new[] { 8 }, PropertyPath = "OutputImage" },
                    RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 14, NodePath = new[] { 9 }, PropertyPath = "Result" } });
                document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                    Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.NodeElapsed, ProcessId = 14, NodePath = new[] { 8 } } });
                document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                    Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 14 } });
                using (var source = new RuntimeBindingDataSource(document, new[] { process }))
                {
                    DisposeUpdates(source.Drain());
                    imageNode.ReplaceResult(new NodeResultImageSource { OutputImage = OutputImage.FromOwnedSingleImage(
                        new OpenCvSharp.Mat(20, 40, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(142))) });
                    imageNode.SetStatus(NodeStatus.Successful, "5"); roiNode.Publish();
                    typeof(Process).GetProperty("RunTime").SetValue(process, 35L); Complete(process);
                    var displayed = consumed ? source.Drain() : new RuntimeValueUpdate[0];
                    try
                    {
                        imageNode.BeginProcessRun(2); roiNode.BeginProcessRun(2);
                        imageNode.ReplaceResult(new NodeResultImageSource { OutputImage = OutputImage.FromOwnedSingleImage(
                            new OpenCvSharp.Mat(20, 40, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(211))) });
                        imageNode.SetStatus(NodeStatus.Successful, "9");
                        typeof(Process).GetProperty("RunTime").SetValue(process, 9L);
                        Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "半轮取消", isCancelled: true));
                        ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                        var updates = source.Drain();
                        try
                        {
                            Check(consumed ? updates.Count == 0 : updates.Count == 4,
                                "整轮取消不能发布半轮成功图像或覆盖收件槽的完整上一轮");
                            var retained = consumed ? displayed : updates;
                            Check(retained[0].Image.GetPixel(1, 1).R == 142 && retained[1].Roi != null
                                && retained[2].ElapsedMilliseconds == 5 && retained[3].ElapsedMilliseconds == 35,
                                "图像成功但ROI未执行时仍保留上一轮完整图像、ROI与两种耗时");
                        }
                        finally { DisposeUpdates(updates); }
                        roiNode.SetStatus(NodeStatus.Failed, "2");
                        Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "真实失败"));
                        var failed = source.Drain();
                        try
                        {
                            Check(failed.Any(update => update.IsRoiUpdate && update.Roi == null && update.Status == "节点运行失败")
                                && failed.Any(update => update.Status == "流程运行失败"), "整轮过滤不能隐藏实际ROI失败及流程失败状态");
                        }
                        finally { DisposeUpdates(failed); }
                        Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "取消且ROI失败", isCancelled: true));
                        failed = source.Drain();
                        try
                        {
                            Check(failed.Count == 2 && failed.Any(update => update.IsRoiUpdate && update.Status == "节点运行失败")
                                && failed.Any(update => update.Status == "流程运行失败"), "并行取消与明确节点失败并存时不能隐藏失败，也不能发布半轮成功图像");
                        }
                        finally { DisposeUpdates(failed); }
                    }
                    finally { DisposeUpdates(displayed); }
                }
            }
        }
    }

    /// <summary>源码编译入口同步覆盖图像租约和两种耗时，不依赖主程序重构建。</summary>
    private static void VerifyStoppedImageAndElapsed()
    {
        var process = EmptyProcess(13);
        using (var node = new RoiNode(process, 8))
        using (var output = OutputImage.FromOwnedSingleImage(new OpenCvSharp.Mat(20, 40,
            OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(142))))
        {
            process.Nodes.Add(node);
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 13, NodePath = new[] { 8 }, PropertyPath = "OutputImage" } });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.NodeElapsed, ProcessId = 13, NodePath = new[] { 8 } } });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 13 } });
            node.ReplaceResult(new NodeResultImageSource());
            using (var source = new RuntimeBindingDataSource(document, new[] { process }))
            {
                DisposeUpdates(source.Drain());
                node.ReplaceResult(new NodeResultImageSource { OutputImage = output }); node.SetStatus(NodeStatus.Successful, "5");
                typeof(Process).GetProperty("RunTime").SetValue(process, 35L); Complete(process);
                var shown = source.Drain();
                try
                {
                    Check(shown.Count == 3 && shown[0].Image.GetPixel(1, 1).R == 142
                        && shown[1].ElapsedMilliseconds == 5 && shown[2].ElapsedMilliseconds == 35, "源码成功轮次包含图像与两种耗时");
                    node.BeginProcessRun(2); node.ReplaceResult(new NodeResultImageSource());
                    typeof(Process).GetProperty("RunTime").SetValue(process, 0L);
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "停止", isCancelled: true));
                    var stopped = source.Drain();
                    try { Check(stopped.Count == 0, "停止空通知不得覆盖已显示图像和两种耗时"); }
                    finally { DisposeUpdates(stopped); }
                    Check(shown[0].Image.GetPixel(1, 1).R == 142 && shown[2].ElapsedMilliseconds == 35, "停止保持上一帧像素和耗时");
                }
                finally { DisposeUpdates(shown); }
                var pendingMat = new OpenCvSharp.Mat(20, 40, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(211));
                using (var pendingOutput = OutputImage.FromOwnedSingleImage(pendingMat))
                {
                    node.ReplaceResult(new NodeResultImageSource { OutputImage = pendingOutput }); node.SetStatus(NodeStatus.Successful, "6");
                    typeof(Process).GetProperty("RunTime").SetValue(process, 48L); Complete(process);
                    node.BeginProcessRun(3); node.ReplaceResult(new NodeResultImageSource());
                    typeof(Process).GetProperty("RunTime").SetValue(process, 0L);
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "停止", isCancelled: true));
                    Check(!pendingMat.IsDisposed, "停止不能释放仍在收件槽中的成功图像租约");
                    var pending = source.Drain();
                    Check(pending.Count == 3 && pending[0].Image.GetPixel(1, 1).R == 211 && pending[1].ElapsedMilliseconds == 6
                        && pending[2].ElapsedMilliseconds == 48, "停止前待消费成功结果完整保留");
                    DisposeUpdates(pending);
                    Check(pendingMat.IsDisposed, "转换完成正常释放源图像");
                }
                node.SetStatus(NodeStatus.Failed, "2");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "失败"));
                var failed = source.Drain();
                Check(failed.Count == 3 && failed.All(update => update.Status.Contains("失败")) && failed[0].Image == null,
                    "真实节点失败仍更新图像及两种耗时的失败状态");
                DisposeUpdates(failed);
                node.SetStatus(NodeStatus.Running, "*");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "资源准入异常"));
                failed = source.Drain();
                Check(failed.Count == 3 && failed.All(update => update.Status == "流程运行失败")
                    && failed[0].Image == null && failed[1].ElapsedMilliseconds == null,
                    "没有Failed节点的真实异常不能被误判为取消并隐藏");
                DisposeUpdates(failed);
                process.Nodes.Clear();
                var removed = source.Drain();
                Check(removed.Count == 2 && removed.All(update => update.Status.Contains("失效")), "停止后节点删除不保留过期图像或节点耗时");
                DisposeUpdates(removed);
            }
        }
    }

    /// <summary>停止和未执行只保留上一ROI，明确失败、空成功结果及失效仍清除。</summary>
    private static void VerifyStoppedRoiRetention()
    {
        var process = EmptyProcess(12);
        using (var node = new RoiNode(process, 8))
        {
            process.Nodes.Add(node);
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image,
                RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi, ProcessId = 12, NodePath = new[] { 8 }, PropertyPath = "Result" } });
            using (var source = new RuntimeBindingDataSource(document, new[] { process }))
            {
                DisposeUpdates(source.Drain()); node.Publish(); Complete(process);
                var displayed = source.Drain();
                try
                {
                    Check(displayed.Count == 1 && displayed[0].Roi != null, "成功轮次提供独立ROI快照");
                    node.SetStatus(NodeStatus.Unexecuted, "*"); Complete(process);
                    var skipped = source.Drain();
                    try { Check(skipped.Count == 0, "成功流程中跳过ROI节点时仍保留上次显示而不发布空结果"); }
                    finally { DisposeUpdates(skipped); }
                    foreach (var status in new[] { NodeStatus.Unexecuted, NodeStatus.Running })
                    {
                        node.ReplaceResult(new RoiResult()); node.SetStatus(status, "*");
                        Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "停止", isCancelled: true));
                        ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                        var stopped = source.Drain();
                        try { Check(stopped.Count == 0, "停止未完成轮次不能清空已显示ROI"); }
                        finally { DisposeUpdates(stopped); }
                    }
                    using (var bitmap = new Bitmap(400, 200))
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        displayed[0].Roi.Draw(graphics, new RectangleF(0, 0, 400, 200), bitmap.Size, bitmap.Size);
                        Check(bitmap.GetPixel(200, 100).G > 200, "停止后上一ROI仍可绘制");
                    }
                }
                finally { DisposeUpdates(displayed); }
                node.Publish(); Complete(process);
                node.SetStatus(NodeStatus.Unexecuted, "*"); node.ReplaceResult(new RoiResult());
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "停止", isCancelled: true));
                var updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi != null && updates[0].Status == null, "紧随成功结果的停止不能覆盖未消费ROI");
                DisposeUpdates(updates);
                node.SetStatus(NodeStatus.Failed, "2");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "真实失败"));
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "节点运行失败", "真实ROI节点失败必须清除旧区域");
                DisposeUpdates(updates);
                node.SetStatus(NodeStatus.Running, "*");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "资源准入异常"));
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "流程运行失败",
                    "ROI节点停留Running的非取消异常也必须清除旧显示并报告失败");
                DisposeUpdates(updates);
                node.Publish(); Complete(process); DisposeUpdates(source.Drain());
                node.ReplaceResult(new RoiResult()); node.SetStatus(NodeStatus.Successful, "2"); Complete(process);
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "等待ROI", "成功但空ROI是有效清除而非停止保留");
                DisposeUpdates(updates);
                node.Publish(); Complete(process); DisposeUpdates(source.Drain());
                node.SetStatus(NodeStatus.Unexecuted, "*"); process.Nodes.Clear();
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status.Contains("失效"), "停止后来源删除仍清除ROI");
                DisposeUpdates(updates);
            }
        }
    }

    /// <summary>静态图叠加和框架原始绘制逐像素等价，复制后生产列表清空不影响画面。</summary>
    private static void VerifyStaticAndSnapshotPixels()
    {
        var result = new AlgorithmResult();
        result.Lines.Add(new ColorLine(new PointF(100, 100), new PointF(300, 100), Color.Lime) { ShowCenterCross = false });
        result.Circles.Add(new ColorCircle(new PointF(220, 130), 20, Color.Red));
        result.Arcs.Add(new ColorArc(new PointF(300, 130), 20, 20, 100, Color.Yellow));
        result.Ellipses.Add(new ColorEllipse(new PointF(80, 130), 40, 20, 30, Color.Blue));
        var points = new List<PointF> { new PointF(310, 30), new PointF(320, 40) };
        result.Contours.Add(new ColorContour(points, Color.Magenta));
        result.Texts.Add(new ColorText("text", Color.White) { UseImagePosition = true, ImagePosition = new PointF(10, 10) });
        using (var expected = new Bitmap(400, 200))
        using (var actual = new Bitmap(400, 200))
        using (var snapshot = RuntimeRoiSnapshot.Create(result))
        {
            ShowImageControl.DrawDisplayResultToBitmap(expected, result);
            points.Clear(); result.Clear();
            using (var graphics = Graphics.FromImage(actual))
                snapshot.Draw(graphics, new RectangleF(0, 0, 400, 200), new Size(400, 200), new Size(400, 200));
            bool same = true;
            for (int y = 0; y < 200 && same; y++)
                for (int x = 0; x < 400; x++) if (expected.GetPixel(x, y) != actual.GetPixel(x, y)) { same = false; break; }
            Check(same, "snapshot must match framework shape pixels and survive clearing original results");
        }
        var line = new AlgorithmResult();
        line.Lines.Add(new ColorLine(new PointF(100, 100), new PointF(300, 100), Color.Lime) { ShowCenterCross = false });
        using (var source = new Bitmap(400, 200))
        using (var memory = new System.IO.MemoryStream())
        using (var snapshot = RuntimeRoiSnapshot.Create(line))
        {
            using (var graphics = Graphics.FromImage(source)) graphics.Clear(Color.Black);
            source.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            var element = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Width = 204, Height = 204,
                ImageData = Convert.ToBase64String(memory.ToArray()), RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi } };
            var value = new RuntimeValueUpdate { Roi = snapshot };
            using (var renderer = new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Image, id => value))
            using (var bitmap = new Bitmap(204, 204))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                renderer.Draw(graphics, element);
                Check(bitmap.GetPixel(102, 102).G > 200 && bitmap.GetPixel(102, 102).R < 50,
                    "ROI-only binding draws on static image at centered half-scale coordinates");
                value.Roi = null;
                renderer.Draw(graphics, element);
                Check(bitmap.GetPixel(102, 102).ToArgb() == Color.Black.ToArgb(), "empty ROI clears overlay while static image remains");
            }
        }
    }

    /// <summary>真实Mat转换到1600显示帧时必须保留原图尺寸，叠加不能拿缩略图当坐标系。</summary>
    private static void VerifyLargeImage()
    {
        var process = EmptyProcess(3);
        using (var node = new RoiNode(process, 7))
        using (var output = OutputImage.FromOwnedSingleImage(new OpenCvSharp.Mat(2001, 4000,
            OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(0))))
        {
            process.Nodes.Add(node);
            node.ReplaceResult(new NodeResultImageSource { OutputImage = output });
            node.SetStatus(NodeStatus.Successful, "1");
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image,
                Binding = new RuntimeDataBinding { ProcessId = 3, NodePath = new[] { 7 }, Kind = RuntimeBindingKind.Image, PropertyPath = "OutputImage" } });
            using (var source = new RuntimeBindingDataSource(document, new[] { process }))
            {
                var updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Image?.Size == new Size(1600, 800)
                    && updates[0].ImageSourceSize == new Size(4000, 2001), "actual downsample preserves original noninteger-aspect dimensions");
                var result = new AlgorithmResult();
                result.Lines.Add(new ColorLine(new PointF(1000, 1000.5f), new PointF(3000, 1000.5f), Color.Lime) { ShowCenterCross = false });
                updates[0].Roi = RuntimeRoiSnapshot.Create(result);
                var element = document.Elements[0]; element.Width = 204; element.Height = 204;
                element.RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi };
                using (var renderer = new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Image, id => updates[0]))
                using (var bitmap = new Bitmap(204, 204))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    renderer.Draw(graphics, element);
                    Check(bitmap.GetPixel(102, 102).G > 200 && bitmap.GetPixel(102, 102).R < 50,
                        "large source ROI must align after integer-rounded downsampling");
                }
                DisposeUpdates(updates);
            }
        }
    }

    /// <summary>动态ROI只接受显式类型，删除、空值和异常均产生清除增量。</summary>
    private static void VerifyDynamicAndInvalidRoi()
    {
        var process = EmptyProcess(4);
        using (var node = new DynamicRoiNode(process))
        {
            process.Nodes.Add(node);
            var catalog = new RuntimeBindingCatalog(() => new[] { process });
            var choices = Flatten(catalog.GetRoiSources()).Where(c => c.Binding != null).ToArray();
            Check(choices.Length == 1 && choices[0].Binding.PropertyPath == "$variable:region", "declared dynamic ROI available before first execution");
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, RoiBinding = choices[0].Binding });
            using (var source = new RuntimeBindingDataSource(document, new[] { process }))
            {
                DisposeUpdates(source.Drain());
                node.Value = new AlgorithmResult(); node.SetStatus(NodeStatus.Successful, "1"); Complete(process);
                var updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi != null && updates[0].IsRoiUpdate, "empty declared ROI is a valid clearing snapshot");
                DisposeUpdates(updates);
                node.SetStatus(NodeStatus.Unexecuted, "*"); node.Available = false;
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status.Contains("失效"), "deleted dynamic output invalidates without new execution");
                DisposeUpdates(updates);
                node.Available = true; node.Value = null; node.SetStatus(NodeStatus.Successful, "1"); Complete(process);
                updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "等待ROI", "null ROI publishes explicit clear");
                DisposeUpdates(updates);
                node.Value = new AlgorithmResult();
                node.Value.Lines.Add(new ColorLine(new PointF(float.NaN, 0), new PointF(10, 10), Color.Lime));
                Complete(process); updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "读取结果失败", "invalid ROI clears old data instead of retaining stale geometry");
                DisposeUpdates(updates);
                node.Value = new AlgorithmResult();
                node.Value.Contours.Add(new ColorContour(Enumerable.Repeat(PointF.Empty, 65537).ToList(), Color.Red));
                Complete(process); updates = source.Drain();
                Check(updates.Count == 1 && updates[0].Roi == null && updates[0].Status == "读取结果失败", "oversized ROI rejected before copying unbounded points");
                DisposeUpdates(updates);
                node.DeclaredType = typeof(object);
                Check(!Flatten(catalog.GetRoiSources()).Any(c => c.Binding != null), "untyped dynamic object cannot masquerade as ROI");
            }
        }
    }

    /// <summary>关闭不等待工作线程，迟到的ROI由转换线程释放。</summary>
    private static void VerifyLateClose()
    {
        using (var canvas = new RuntimeDesignCanvas())
        using (var source = new BlockedFeed())
        {
            var controller = new RuntimeLiveViewController(canvas, source);
            controller.Tick();
            Check(source.Entered.Wait(5000), "background ROI drain entered");
            controller.Dispose(); source.Release.Set();
            Check(SpinWait.SpinUntil(() => source.Overlay.Disposed, 5000), "late ROI after closing must be disposed");
        }
    }

    /// <summary>带显式类型声明的无设备动态输出节点。</summary>
    private sealed class DynamicRoiNode : NodeBase, IDynamicResultVariableProvider, IDynamicResultVariableTypeProvider
    {
        /// <summary>是否声明输出。</summary>
        internal bool Available = true;
        /// <summary>当前声明的输出类型。</summary>
        internal Type DeclaredType = typeof(AlgorithmResult);
        /// <summary>动态值存储。</summary>
        internal AlgorithmResult Value { get => ((DynamicRoiResult)Result).Value; set => ((DynamicRoiResult)Result).Value = value; }
        /// <summary>只建立结果，不创建参数界面。</summary>
        internal DynamicRoiNode(Process process)
        {
            typeof(NodeBase).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, 8);
            Process = process; NodeName = "动态ROI"; Result = new DynamicRoiResult();
        }
        /// <summary>提供配置声明，删除后不回退旧结果。</summary>
        public IEnumerable<string> GetDynamicResultVariableNames() => Available ? new[] { "region" } : new string[0];
        /// <summary>返回已声明的契约类型。</summary>
        public bool TryGetDynamicResultVariableType(string name, out Type valueType) { valueType = DeclaredType; return true; }
    }

    /// <summary>仅内存中的动态算法结果。</summary>
    private sealed class DynamicRoiResult : INodeResult, IDynamicResultVariables
    {
        /// <summary>算法结果。</summary>
        internal AlgorithmResult Value;
        /// <summary>测试耗时。</summary>
        public int RunTime { get; set; }
        /// <summary>公开变量名。</summary>
        public IEnumerable<string> GetDynamicVariableNames() => new[] { "region" };
        /// <summary>获取当前值，包括空值。</summary>
        public bool TryGetDynamicVariable(string name, out object value) { value = Value; return true; }
    }

    /// <summary>固定关闭与结果到达顺序的内存来源。</summary>
    private sealed class BlockedFeed : IRuntimeBindingDataSource
    {
        /// <summary>转换已经开始。</summary>
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        /// <summary>允许转换返回。</summary>
        internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        /// <summary>迟到的资源。</summary>
        internal readonly ProbeOverlay Overlay = new ProbeOverlay();
        /// <summary>等待测试释放，不调用框架调度。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain()
        { Entered.Set(); Release.Wait(5000); return new[] { new RuntimeValueUpdate { ElementId = "view", IsRoiUpdate = true, Roi = Overlay } }; }
        /// <summary>测试屏障仍留给测试线程控制。</summary>
        public void Dispose() { }
    }

    /// <summary>ROI独立字段不能在模板往返时丢失或覆盖图像来源。</summary>
    private static void VerifyDocument()
    {
        var document = new RuntimeLayoutDocument();
        document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image,
            Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Image, ProcessId = 1,
                NodePath = new[] { 8 }, PropertyPath = "OutputImage" } });
        var json = JObject.Parse(RuntimeLayoutSerializer.Serialize(document));
        json["Elements"][0]["RoiBinding"] = JObject.Parse(
            "{'Kind':'Roi','ProcessId':2,'NodePath':[62,8],'PropertyPath':'Result','DisplayPath':'工位2 / 模块 / 绘制结果'}");
        var restored = JObject.Parse(RuntimeLayoutSerializer.Serialize(RuntimeLayoutSerializer.Deserialize(json.ToString())));
        Check(restored["Elements"][0]["RoiBinding"] != null, "ROI binding must survive template round trip independently");
        Check((int)restored["Elements"][0]["Binding"]["ProcessId"] == 1, "ROI binding must not replace image binding");
        var model = RuntimeLayoutSerializer.Deserialize(json.ToString());
        var copy = model.Elements[0].Copy();
        copy.RoiBinding.NodePath[0] = 99;
        Check(model.Elements[0].RoiBinding.NodePath[0] == 62, "ROI copy must deep copy its scoped path");
        Check(RuntimeLayoutSerializer.Clone(model).Elements[0].RoiBinding.Kind == RuntimeBindingKind.Roi,
            "ROI survives independent solution persistence clone");
        model.Elements[0].Kind = RuntimeWidgetKind.Text;
        RejectDocument(model, "non-image widgets cannot subscribe ROI");
        model.Elements[0].Kind = RuntimeWidgetKind.Image;
        model.Elements[0].RoiBinding.Kind = RuntimeBindingKind.Image;
        RejectDocument(model, "ROI slot cannot accept an image binding");
        model.Elements[0].RoiBinding.Kind = RuntimeBindingKind.Roi;
        model.Elements[0].RoiBinding.PropertyPath = "";
        RejectDocument(model, "ROI needs an explicit output contract");
    }

    /// <summary>组合模块内同编号节点必须按父路径隔离，ROI绑定必须真正创建独立槽。</summary>
    private static void VerifyScopedRoiCapture()
    {
        var root = EmptyProcess(1);
        var inner = EmptyProcess(1);
        using (var outside = new RoiNode(root, 8))
        using (var nested = new RoiNode(inner, 8))
        using (var module = new NodeCompositeModule(root, 62, inner))
        {
            root.Nodes.Add(outside); root.Nodes.Add(module); inner.Nodes.Add(nested);
            var catalog = new RuntimeBindingCatalog(() => new[] { root });
            var choices = Flatten(catalog.GetRoiSources()).Where(c => c.Binding != null).ToArray();
            Check(choices.Length == 2 && choices.Any(c => c.Binding.NodePath.SequenceEqual(new[] { 62, 8 })),
                "ROI catalog must retain full nested path and exclude numeric outputs");
            var binding = choices.Single(c => c.Binding.NodePath.Length == 2).Binding;
            var doc = new RuntimeLayoutDocument();
            doc.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, RoiBinding = binding });
            using (var source = new RuntimeBindingDataSource(doc, new[] { root }))
            {
                var initial = source.Drain();
                Check(initial.Count == 1, "ROI-only binding must create an independent live slot");
                DisposeUpdates(initial);
                outside.Publish(); Complete(root);
                Check(source.Drain().Count == 0, "same ID outside module must not update nested ROI");
                nested.Publish(); Complete(inner);
                var update = source.Drain();
                Check(update.Count == 1 && update[0].Image == null && update[0].Status == null,
                    "completed ROI publishes independently without inventing an image");
                Check(typeof(RuntimeValueUpdate).GetProperty("Roi").GetValue(update[0]) != null, "ROI update owns a display snapshot");
                DisposeUpdates(update);
                var replacement = EmptyProcess(1);
                using (var rebuilt = new RoiNode(replacement, 8))
                {
                    replacement.Nodes.Add(rebuilt); module.ExistingRuntimeProcess = replacement;
                    rebuilt.Publish(); Complete(replacement);
                    update = source.Drain();
                    Check(update.Count == 1 && update[0].Roi != null && update[0].Status == null,
                        "rebuilt module first completed ROI must not be overwritten by identity invalidation");
                    DisposeUpdates(update);
                    nested.Publish(); Complete(inner);
                    Check(source.Drain().Count == 0, "old module completion cannot publish into rebuilt scope");
                    module.ExistingRuntimeProcess = inner;
                }
                inner.Nodes.Clear();
                update = source.Drain();
                Check(update.Count == 1 && update[0].Status.Contains("失效"), "deleted ROI source must invalidate old overlay");
                DisposeUpdates(update);
            }
        }
    }

    /// <summary>两路结果先后到达时不能互相清空，ROI失败和预算拒绝也必须保留图像。</summary>
    private static void VerifyIndependentDisplayUpdates()
    {
        using (var canvas = new RuntimeDesignCanvas())
        using (var feed = new Feed())
        using (var controller = new RuntimeLiveViewController(canvas, feed))
        {
            var image = new Bitmap(200, 100);
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", Image = image, ImageSourceSize = new Size(400, 200) });
            Pump(controller, () => Displayed(controller)?.Image == image);
            var roi = new ProbeOverlay();
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", IsRoiUpdate = true, Roi = roi });
            Pump(controller, () => Displayed(controller)?.Roi == roi);
            Check(ReferenceEquals(Displayed(controller).Image, image), "ROI update must not clear the separately subscribed image");
            var nextImage = new Bitmap(160, 80);
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", Image = nextImage, ImageSourceSize = new Size(4000, 2000) });
            Pump(controller, () => Displayed(controller)?.Image == nextImage);
            Check(ReferenceEquals(Displayed(controller).Roi, roi) && !roi.Disposed, "image refresh must not clear independent ROI");
            var element = new RuntimeLayoutElement { Id = "view", Kind = RuntimeWidgetKind.Image, Width = 204, Height = 204,
                Binding = new RuntimeDataBinding(), RoiBinding = new RuntimeDataBinding { Kind = RuntimeBindingKind.Roi } };
            using (var renderer = new RuntimeLiveWidgetRenderer(RuntimeWidgetKind.Image, id => Displayed(controller)))
            using (var bitmap = new Bitmap(204, 204))
            using (var graphics = Graphics.FromImage(bitmap))
                renderer.Draw(graphics, element);
            Check(roi.SourceSize == new Size(4000, 2000), "ROI coordinates must use source image dimensions before downsampling");
            Check(roi.ImageBounds == new RectangleF(2, 52, 200, 100), "ROI must share centered image bounds including border and letterboxing");
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", IsRoiUpdate = true, Status = "数据源已失效" });
            Pump(controller, () => Displayed(controller)?.RoiStatus == "数据源已失效");
            Check(roi.Disposed && Displayed(controller).Roi == null && ReferenceEquals(Displayed(controller).Image, nextImage),
                "ROI invalidation must release overlay without clearing image");
            var excessive = new ProbeOverlay { Bytes = 128L * 1024 * 1024 };
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", IsRoiUpdate = true, Roi = excessive });
            Pump(controller, () => excessive.Disposed);
            Check(ReferenceEquals(Displayed(controller).Image, nextImage), "ROI budget rejection must preserve independent image");
            var replacementRoi = new ProbeOverlay();
            feed.Enqueue(new RuntimeValueUpdate { ElementId = "view", IsRoiUpdate = true, Roi = replacementRoi });
            Pump(controller, () => Displayed(controller)?.Roi == replacementRoi);
            controller.Dispose();
            Check(replacementRoi.Disposed, "closing view releases displayed ROI as well as its image");
        }
    }

    /// <summary>读取显示侧最终状态，不依赖窗口或消息循环。</summary>
    private static RuntimeValueUpdate Displayed(RuntimeLiveViewController controller)
    {
        var map = (Dictionary<string, RuntimeValueUpdate>)typeof(RuntimeLiveViewController)
            .GetField("displayed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        RuntimeValueUpdate value;
        return map.TryGetValue("view", out value) ? value : null;
    }

    /// <summary>驱动后台任务到可观测条件，超时即失败，不控制桌面。</summary>
    private static void Pump(RuntimeLiveViewController controller, Func<bool> condition)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && elapsed.ElapsedMilliseconds < 5000) { controller.Tick(); Thread.Sleep(1); }
        Check(condition(), "display update must complete without losing either channel");
    }

    /// <summary>受控独立增量源。</summary>
    private sealed class Feed : IRuntimeBindingDataSource
    {
        /// <summary>测试事件队列，不包含设备数据。</summary>
        private readonly ConcurrentQueue<RuntimeValueUpdate> queue = new ConcurrentQueue<RuntimeValueUpdate>();
        /// <summary>投递一份显示结果。</summary>
        internal void Enqueue(RuntimeValueUpdate value) => queue.Enqueue(value);
        /// <summary>每次只返回一个增量以固定交错顺序。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain()
        { RuntimeValueUpdate value; return queue.TryDequeue(out value) ? new[] { value } : new RuntimeValueUpdate[0]; }
        /// <summary>清理未消费的结果。</summary>
        public void Dispose() { RuntimeValueUpdate value; while (queue.TryDequeue(out value)) value.Dispose(); }
    }

    /// <summary>记录ROI实际使用的图像坐标和释放行为。</summary>
    private sealed class ProbeOverlay : IRuntimeRoiOverlay
    {
        /// <summary>可配置预算占用。</summary>
        internal long Bytes = 128;
        /// <summary>是否释放。</summary>
        internal bool Disposed;
        /// <summary>收到的原始图像尺寸。</summary>
        internal Size SourceSize;
        /// <summary>收到的等比显示区域。</summary>
        internal RectangleF ImageBounds;
        /// <summary>反馈资源预算。</summary>
        public long EstimatedBytes => Bytes;
        /// <summary>记录坐标，不调用设备或窗口。</summary>
        public void Draw(Graphics graphics, RectangleF imageBounds, Size sourceImageSize, Size controlSize)
        { SourceSize = sourceImageSize; ImageBounds = imageBounds; }
        /// <summary>记录释放。</summary>
        public void Dispose() { Disposed = true; }
    }

    /// <summary>非法绑定必须在模板进入运行态前拒绝。</summary>
    private static void RejectDocument(RuntimeLayoutDocument document, string message)
    {
        try { RuntimeLayoutSerializer.Validate(document); }
        catch (System.IO.InvalidDataException) { Check(true, message); return; }
        throw new Exception(message);
    }

    /// <summary>不初始化设备的流程壳，仅模拟已有结果完成通知。</summary>
    private static Process EmptyProcess(int id)
    {
        var process = (Process)FormatterServices.GetUninitializedObject(typeof(Process));
        process.ID = id; process.ProcessName = "测试流程";
        typeof(Process).GetField("_nodes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(process, new List<NodeBase>());
        return process;
    }

    /// <summary>只发布通知，不调用流程调度。</summary>
    private static void Complete(Process process) => Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, true, "测试"));

    /// <summary>遍历目录叶子。</summary>
    private static IEnumerable<RuntimeBindingChoice> Flatten(IEnumerable<RuntimeBindingChoice> choices)
    { foreach (var choice in choices) { yield return choice; foreach (var child in Flatten(choice.Children)) yield return child; } }

    /// <summary>释放测试提取结果。</summary>
    private static void DisposeUpdates(IEnumerable<RuntimeValueUpdate> updates)
    { foreach (var update in updates) update.Dispose(); }

    /// <summary>声明真实显示契约的无设备结果。</summary>
    private sealed class RoiResult : INodeResult
    {
        /// <summary>测试耗时。</summary>
        public int RunTime { get; set; }
        /// <summary>框架可订阅的ROI输出。</summary>
        [SubscriptionOutput, DisplayName("绘制结果")]
        public AlgorithmResult Result { get; set; }
        /// <summary>普通值不能误列为ROI。</summary>
        [SubscriptionOutput, DisplayName("计数")]
        public int Count => 3;
    }

    /// <summary>只替换结果，不运行算法或设备的节点。</summary>
    private sealed class RoiNode : NodeBase
    {
        /// <summary>仅替换内存结果，模拟流程完成前的结果提交。</summary>
        internal void ReplaceResult(INodeResult result) { Result = result; }
        /// <summary>构造已有结果契约的节点。</summary>
        internal RoiNode(Process process, int id)
        {
            typeof(NodeBase).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, id);
            Process = process; NodeName = "ROI节点"; Result = new RoiResult();
        }
        /// <summary>生成受控线段结果。</summary>
        internal void Publish()
        {
            var result = new AlgorithmResult();
            result.Lines.Add(new ColorLine(new PointF(100, 100), new PointF(300, 100), Color.Lime) { ShowCenterCross = false });
            Result = new RoiResult { Result = result };
            SetStatus(NodeStatus.Successful, "1");
        }
    }

    /// <summary>失败时立即终止。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
}
