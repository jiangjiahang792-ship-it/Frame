using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>运行数据绑定的文档、目录与资源回归测试。</summary>
internal static class RuntimeDataBindingTests
{
    /// <summary>本轮成功执行的断言数量。</summary>
    private static int checks;
    /// <summary>执行不访问生产设备的绑定测试。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image });
            var json = JObject.Parse(RuntimeLayoutSerializer.Serialize(document));
            json["Elements"][0]["Binding"] = JObject.Parse("{ 'Kind':'Image', 'ProcessId':1, 'NodePath':[62,8], 'PropertyPath':'OutputImage', 'DisplayPath':'工位1 / 组合模块 / 图像源 / 输出图像' }");
            var roundTrip = JObject.Parse(RuntimeLayoutSerializer.Serialize(RuntimeLayoutSerializer.Deserialize(json.ToString())));
            Check(roundTrip["Elements"][0]["Binding"] != null, "saved image binding must survive template round trip");
            VerifyCopies();
            VerifyStopRetainsCompletedData();
            VerifyCatalogAndRuntime();
            VerifyDynamicOutputAndNodeTime();
            VerifyLiveRendering();
            VerifyRefreshFailureIsolation();
            VerifyConcurrentReplacement();
            VerifyCloseDuringRead();
            Console.WriteLine("PASS: " + checks + " runtime data binding assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>成功轮次后停止或未执行不覆盖显示结果，明确失败与来源删除仍必须更新。</summary>
    private static void VerifyStopRetainsCompletedData()
    {
        var process = EmptyProcess(9, "停止保持测试");
        using (var node = new TestImageNode(process, 7, "图像"))
        {
            process.Nodes.Add(node);
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(9, 7) });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.NodeElapsed, ProcessId = 9, NodePath = new[] { 7 } } });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed,
                Binding = new RuntimeDataBinding { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 9 } });
            using (var source = new RuntimeBindingDataSource(document, new[] { process }))
            {
                DisposeUpdates(source.Drain());
                node.Publish(142); Complete(process, 35);
                var last = source.Drain();
                try
                {
                    Check(last.Count == 3 && last[0].Image.GetPixel(1, 1).R == 142
                        && last[1].ElapsedMilliseconds == 5 && last[2].ElapsedMilliseconds == 35, "成功轮次提供图像、节点和流程耗时");
                    node.BeginProcessRun(2);
                    node.ReleaseOutput();
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(true, false, process.ProcessName));
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName, isCancelled: true));
                    ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Cancelled);
                    var stopped = source.Drain();
                    try { Check(stopped.Count == 0, "停止时未执行空通知不得覆盖上轮图像及两种耗时"); }
                    finally { DisposeUpdates(stopped); }
                    Check(last[0].Image.GetPixel(1, 1).R == 142, "停止不释放显示方已经接管的上一帧");
                    node.SetStatus(NodeStatus.Running, "*");
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName, isCancelled: true));
                    var interrupted = source.Drain();
                    try { Check(interrupted.Count == 0, "取消时停留在运行态的节点也不能发布空结果"); }
                    finally { DisposeUpdates(interrupted); }
                    node.Publish(200);
                    Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "半轮取消", isCancelled: true));
                    var partial = source.Drain();
                    try { Check(partial.Count == 0, "整轮未完成时不能发布其中已经成功节点的新图像和耗时"); }
                    finally { DisposeUpdates(partial); }
                }
                finally { DisposeUpdates(last); }
                var pending = node.Publish(211); Complete(process, 48);
                node.BeginProcessRun(3); node.ReleaseOutput();
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName, isCancelled: true));
                Check(!pending.IsDisposed, "停止通知不能提前释放尚未转换的成功帧租约");
                var updates = source.Drain();
                Check(updates.Count == 3 && updates[0].Image.GetPixel(1, 1).R == 211
                    && updates[2].ElapsedMilliseconds == 48, "未消费成功帧及耗时不会被紧随其后的停止通知覆盖");
                DisposeUpdates(updates);
                Check(pending.IsDisposed, "成功帧提取完成后仍正确释放源租约");
                node.SetStatus(NodeStatus.Failed, "2");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, process.ProcessName));
                ProcessEvents.OnProcessEnded(process.ID, ProcessEndStatus.Failed, new InvalidOperationException("测试失败"));
                updates = source.Drain();
                Check(updates.Count == 3 && updates.All(update => update.Status.Contains("失败"))
                    && updates[0].Image == null && updates[1].ElapsedMilliseconds == null, "真实节点失败不能被停止保留规则隐藏");
                DisposeUpdates(updates);
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "并行取消且节点失败", isCancelled: true));
                updates = source.Drain();
                Check(updates.Count == 3 && updates.All(update => update.Status.Contains("失败")), "明确取消也不能隐藏同轮实际节点失败");
                DisposeUpdates(updates);
                node.SetStatus(NodeStatus.Running, "*");
                Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, false, "资源准入异常"));
                updates = source.Drain();
                Check(updates.Count == 3 && updates.All(update => update.Status == "流程运行失败")
                    && updates[0].Image == null && updates[1].ElapsedMilliseconds == null,
                    "非取消异常即使节点仍为Running也必须显示真实流程失败");
                DisposeUpdates(updates);
                node.Publish(70); Complete(process, 15); DisposeUpdates(source.Drain());
                node.BeginProcessRun(4); process.Nodes.Clear();
                updates = source.Drain();
                Check(updates.Count == 2 && updates.All(update => update.Status.Contains("失效")), "停止后删除节点仍清除图像和节点耗时绑定");
                DisposeUpdates(updates);
            }
        }
    }

    /// <summary>组合模块动态图像输出在成功状态之后替换，也必须取到新结果。</summary>
    private static void VerifyDynamicOutputAndNodeTime()
    {
        var root = EmptyProcess(2, "工位2");
        using (var module = new NodeCompositeModule(50, "配方模块", root, NodeType.CompositeModule))
        using (var output = OutputImage.FromOwnedSingleImage(new Mat(20, 30, MatType.CV_8UC3, Scalar.All(173))))
        {
            root.Nodes.Add(module);
            var parameters = new NodeParamCompositeModule();
            parameters.OutputPorts.Add(new CompositeOutputPortDefinition { Name = "处理图像", ValueType = CompositePortValueType.Image });
            module.ParamForm.Params = parameters;
            SetField(module, "_runtimeProcess", EmptyProcess(50, "模块内部")); SetField(module, "_cachedParam", parameters);
            var catalog = new RuntimeBindingCatalog(() => new[] { root });
            var binding = Flatten(catalog.GetSources(RuntimeWidgetKind.Image)).Single(c => c.Binding != null).Binding;
            Check(binding.PropertyPath.Contains("处理图像"), "module must expose declared dynamic image before first run");
            var image = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = binding };
            var elapsed = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed, Binding = new RuntimeDataBinding
                { Kind = RuntimeBindingKind.NodeElapsed, ProcessId = 2, NodePath = new[] { 50 } } };
            var document = new RuntimeLayoutDocument { Elements = new List<RuntimeLayoutElement> { image, elapsed } };
            using (var source = new RuntimeBindingDataSource(document, new[] { root }))
            {
                DisposeUpdates(source.Drain());
                module.SetStatus(NodeStatus.Successful, "8");
                var result = new NodeResultCompositeModule();
                result.OutputValues["处理图像"] = output;
                result.OutputValueTypes["处理图像"] = CompositePortValueType.Image;
                typeof(NodeBase).GetProperty("Result").SetValue(module, result);
                Complete(root, 18);
                var updates = source.Drain();
                Check(updates.Single(v => v.ElementId == image.Id).Image.GetPixel(1, 1).R == 173, "completion must capture updated dynamic result, not previous status-time result");
                Check(updates.Single(v => v.ElementId == elapsed.Id).ElapsedMilliseconds == 8, "node binding must use node time instead of process time");
                DisposeUpdates(updates);
                module.SetStatus(NodeStatus.Failed, "3"); Complete(root, 9);
                updates = source.Drain();
                Check(updates.All(v => v.Image == null && v.Status.Contains("失败")), "failed run must not label old result as fresh success");
                DisposeUpdates(updates);
            }
            SetField(module, "_runtimeProcess", null);
        }
    }

    /// <summary>在行为不满足预期时终止测试。</summary>
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    /// <summary>绑定数组不能跨复制和撤销共享，非法类型不得进入模板。</summary>
    private static void VerifyCopies()
    {
        var element = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(1, 62, 8) };
        var copy = element.Copy();
        copy.Binding.NodePath[0] = 99;
        Check(element.Binding.NodePath[0] == 62, "element copy must isolate nested binding path");
        var document = new RuntimeLayoutDocument(); document.Elements.Add(element);
        var session = new RuntimeLayoutSession(document);
        session.Edit(d => d.Elements[0].Binding = null);
        session.Undo();
        Check(session.Document.Elements[0].Binding.NodePath.SequenceEqual(new[] { 62, 8 }), "undo must restore full scope");
        session.Redo(); Check(session.Document.Elements[0].Binding == null, "redo must clear binding");
        copy.Binding = ImageBinding(1, 8); copy.Kind = RuntimeWidgetKind.Elapsed;
        bool rejected = false;
        try { RuntimeLayoutSerializer.Serialize(new RuntimeLayoutDocument { Elements = new List<RuntimeLayoutElement> { copy } }); }
        catch (System.IO.InvalidDataException) { rejected = true; }
        Check(rejected, "elapsed widget must reject image binding");
        Check(RuntimeLayoutSerializer.Deserialize("{\"Width\":640,\"Height\":480,\"Elements\":[]}").Elements.Count == 0,
            "old templates without bindings remain supported");
    }

    /// <summary>使用真实节点结果和资源租约，手动发布完成通知，不执行相机和 PLC。</summary>
    private static void VerifyCatalogAndRuntime()
    {
        var root = EmptyProcess(1, "工位1");
        var inner = EmptyProcess(1, "内部流程");
        using (var outside = new TestImageNode(root, 8, "外部图像"))
        using (var nested = new TestImageNode(inner, 8, "内部图像"))
        using (var module = new NodeCompositeModule(62, "组合模块", root, NodeType.CompositeModule))
        {
            root.Nodes.Add(outside); root.Nodes.Add(module); inner.Nodes.Add(nested);
            var parameters = new NodeParamCompositeModule();
            module.ParamForm.Params = parameters;
            SetField(module, "_runtimeProcess", inner); SetField(module, "_cachedParam", parameters);
            var catalog = new RuntimeBindingCatalog(() => new[] { root });
            var choices = Flatten(catalog.GetSources(RuntimeWidgetKind.Image)).Where(c => c.Binding != null).ToArray();
            Check(choices.Any(c => c.Binding.NodePath.SequenceEqual(new[] { 8 })), "catalog must include root image");
            Check(choices.Any(c => c.Binding.NodePath.SequenceEqual(new[] { 62, 8 })), "catalog must include scoped nested image");
            nested.NodeName = "改名图像";
            Check(catalog.Describe(ImageBinding(1, 62, 8)).Contains("改名图像"), "renaming must preserve ID binding");
            var document = new RuntimeLayoutDocument();
            var image = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(1, 62, 8) };
            var elapsed = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Elapsed, Binding = new RuntimeDataBinding
                { Kind = RuntimeBindingKind.ProcessElapsed, ProcessId = 1 } };
            document.Elements.Add(image); document.Elements.Add(elapsed);
            int before = Process.UpdateRunStatus?.GetInvocationList().Length ?? 0;
            using (var source = new RuntimeBindingDataSource(document, new[] { root }))
            {
                var initial = source.Drain();
                Check(initial.Count == 2 && initial.All(v => v.Image == null && v.ElapsedMilliseconds == null), "initial view must not invent sample data");
                DisposeUpdates(initial);
                outside.Publish(230);
                Complete(root, 37);
                var rootUpdate = source.Drain();
                Check(rootUpdate.All(v => v.Image == null), "same node ID in root scope must not update nested widget");
                Check(rootUpdate.Single(v => v.ElementId == elapsed.Id).ElapsedMilliseconds == 37, "capture completion time before next reset");
                DisposeUpdates(rootUpdate);
                Mat previous = null;
                for (int i = 0; i < 40; i++)
                {
                    var mat = nested.Publish((byte)i);
                    Complete(inner, i);
                    if (previous != null) Check(previous.IsDisposed, "replaced image lease must release old frame");
                    previous = mat;
                }
                nested.ReleaseOutput();
                Check(!previous.IsDisposed, "pending display lease must protect released node result");
                var frame = source.Drain();
                Check(frame.Count == 1 && frame[0].Image.GetPixel(2, 2).R == 39, "drain must deliver only latest nested frame");
                Check(previous.IsDisposed, "conversion must release source lease");
                DisposeUpdates(frame);
                inner.Nodes.Clear();
                var removed = source.Drain();
                Check(removed.Count == 1 && removed[0].Status.Contains("失效"), "removed node must invalidate binding");
                DisposeUpdates(removed);
                Check(catalog.Describe(image.Binding).StartsWith("已失效"), "missing path must not fall back to same ID elsewhere");
                var replacement = EmptyProcess(1, "重新创建的内部流程");
                using (var nextNode = new TestImageNode(replacement, 8, "新内部图像"))
                {
                    replacement.Nodes.Add(nextNode);
                    SetField(module, "_runtimeProcess", replacement);
                    nextNode.Publish(123); Complete(replacement, 4);
                    var rebuilt = source.Drain();
                    Check(rebuilt.Count == 1 && rebuilt[0].Image != null && rebuilt[0].Image.GetPixel(2, 2).R == 123,
                        "first completed result after module rebuild must not be replaced by waiting state");
                    DisposeUpdates(rebuilt);
                }
            }
            Check((Process.UpdateRunStatus?.GetInvocationList().Length ?? 0) == before, "closing view must unsubscribe");
            SetField(module, "_runtimeProcess", null);
        }
    }

    /// <summary>运行结果绘制不污染布局，绑定中的示例耗时不作为真实结果显示。</summary>
    private static void VerifyLiveRendering()
    {
        var document = new RuntimeLayoutDocument { Width = 640, Height = 480 };
        var item = new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Width = 640, Height = 480, Binding = ImageBinding(1, 8) };
        document.Elements.Add(item);
        string before = RuntimeLayoutSerializer.Serialize(document);
        using (var canvas = new RuntimeDesignCanvas { PreviewMode = true, Size = new System.Drawing.Size(640, 480) })
        using (var feed = new TestFeed(item.Id))
        using (var controller = new RuntimeLiveViewController(canvas, feed))
        {
            canvas.Bind(new RuntimeLayoutSession(document));
            controller.Tick();
            Check(feed.ReadStarted.WaitOne(5000), "background result reader must start");
            feed.ReleaseRead.Set();
            Check(feed.ReadFinished.WaitOne(5000), "background result reader must finish");
            bool red = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!red && watch.ElapsedMilliseconds < 5000)
            {
                controller.Tick();
                using (var bitmap = new Bitmap(640, 480))
                {
                    canvas.DrawToBitmap(bitmap, canvas.ClientRectangle);
                    red = bitmap.GetPixel(320, 240).R > 240;
                }
                System.Threading.Thread.Yield();
            }
            Check(red, "real image must reach canvas without static cache retaining placeholder");
            Check(RuntimeLayoutSerializer.Serialize(document) == before, "live refresh must not rewrite layout or template image data");
        }
    }

    /// <summary>后一个目录解析异常不能丢弃或泄漏此前已转换的图像。</summary>
    private static void VerifyRefreshFailureIsolation()
    {
        var root = EmptyProcess(11, "正常流程");
        var broken = EmptyProcess(12, "正在编辑流程");
        using (var node = new TestImageNode(root, 8, "图像"))
        using (var other = new TestImageNode(broken, 9, "图像"))
        {
            root.Nodes.Add(node); broken.Nodes.Add(other);
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(11, 8) });
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(12, 9) });
            using (var source = new RuntimeBindingDataSource(document, new[] { root, broken }))
            {
                node.Publish(142); Complete(root, 1);
                SetField(broken, "_nodes", null);
                var updates = source.Drain();
                Check(updates.Count == 2 && updates[0].Image.GetPixel(1, 1).R == 142,
                    "later resolver failure must preserve earlier converted frame");
                Check(updates[1].Status == "数据源暂不可用", "resolver failure must remain local to its slot");
                DisposeUpdates(updates);
                SetField(broken, "_nodes", new List<NodeBase>());
            }
        }
    }

    /// <summary>用收件槽屏障固定重建与提取交错顺序，避免等待状态覆盖第一帧。</summary>
    private static void VerifyConcurrentReplacement()
    {
        var root = EmptyProcess(13, "重建流程");
        using (var oldNode = new TestImageNode(root, 8, "旧图像"))
        using (var nextNode = new TestImageNode(root, 8, "新图像"))
        {
            root.Nodes.Add(oldNode);
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(new RuntimeLayoutElement { Kind = RuntimeWidgetKind.Image, Binding = ImageBinding(13, 8) });
            using (var source = new RuntimeBindingDataSource(document, new[] { root }))
            {
                DisposeUpdates(source.Drain());
                var slots = (Array)typeof(RuntimeBindingDataSource).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
                var slot = slots.GetValue(0);
                var sync = slot.GetType().GetField("Sync").GetValue(slot);
                IReadOnlyList<RuntimeValueUpdate> updates = null;
                Exception error = null;
                var worker = new System.Threading.Thread(() => { try { updates = source.Drain(); } catch (Exception ex) { error = ex; } });
                lock (sync)
                {
                    root.Nodes.Clear(); root.Nodes.Add(nextNode);
                    worker.Start();
                    bool blocked = System.Threading.SpinWait.SpinUntil(() =>
                        (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, 5000);
                    Check(blocked, "reader must reach controlled slot barrier");
                    nextNode.Publish(211); Complete(root, 2);
                }
                Check(worker.Join(5000) && error == null, "concurrent reader must complete without exception");
                Check(updates.Count == 1 && updates[0].Image != null && updates[0].Image.GetPixel(1, 1).R == 211,
                    "waiting state must not replace a concurrently published first frame");
                DisposeUpdates(updates);
                root.Nodes.Clear(); root.Nodes.Add(oldNode);
                oldNode.SetStatus(NodeStatus.Failed, "2"); Complete(root, 2);
                var failed = source.Drain();
                Check(failed.Count == 1 && failed[0].Status == "节点运行失败",
                    "first failure after node replacement must not become waiting state");
                DisposeUpdates(failed);
            }
        }
    }

    /// <summary>窗口关闭不等待后台，迟到图像由控制器释放而不是交给已关闭画布。</summary>
    private static void VerifyCloseDuringRead()
    {
        using (var canvas = new RuntimeDesignCanvas())
        using (var feed = new TestFeed("closed"))
        {
            var controller = new RuntimeLiveViewController(canvas, feed);
            controller.Tick();
            Check(feed.ReadStarted.WaitOne(5000), "late reader must start");
            controller.Dispose();
            Check(feed.ReadFinished.WaitOne(5000), "late reader must finish after close");
            Check(System.Threading.SpinWait.SpinUntil(() => feed.LastUpdate != null && feed.LastUpdate.Image == null, 5000),
                "image returned after close must be disposed");
        }
    }

    /// <summary>构造不触发方案单例或设备初始化的空流程，仅用于测试。</summary>
    private static Process EmptyProcess(int id, string name)
    {
        var process = (Process)FormatterServices.GetUninitializedObject(typeof(Process));
        process.ID = id; process.ProcessName = name;
        SetField(process, "_nodes", new List<NodeBase>());
        SetField(process, "_connections", new List<ProcessConnection>());
        return process;
    }
    /// <summary>设置测试对象内部状态，不执行真实流程。</summary>
    private static void SetField(object target, string name, object value)
    { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value); }
    /// <summary>模拟框架完成通知。</summary>
    private static void Complete(Process process, long milliseconds)
    {
        typeof(Process).GetProperty("RunTime").SetValue(process, milliseconds);
        Process.UpdateRunStatus?.Invoke(process, new ProcessRunResult(false, true, process.ProcessName));
        typeof(Process).GetProperty("RunTime").SetValue(process, 0L);
    }
    /// <summary>创建图像类型的绑定。</summary>
    private static RuntimeDataBinding ImageBinding(int process, params int[] path) => new RuntimeDataBinding
        { ProcessId = process, NodePath = path, Kind = RuntimeBindingKind.Image, PropertyPath = "OutputImage", DisplayPath = "工位 / 图像" };
    /// <summary>遍历选择目录。</summary>
    private static IEnumerable<RuntimeBindingChoice> Flatten(IEnumerable<RuntimeBindingChoice> choices)
    { foreach (var item in choices) { yield return item; foreach (var child in Flatten(item.Children)) yield return child; } }
    /// <summary>释放提取出的测试图像。</summary>
    private static void DisposeUpdates(IEnumerable<RuntimeValueUpdate> updates)
    { foreach (var value in updates) value.Dispose(); }

    /// <summary>只构造结果、不执行算法和设备访问的图像节点。</summary>
    private sealed class TestImageNode : NodeBase
    {
        /// <summary>初始化无设备的测试节点。</summary>
        public TestImageNode(Process process, int id, string name)
        {
            typeof(NodeBase).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, id);
            Process = process; NodeName = name; Result = new NodeResultImageSource();
        }
        /// <summary>模拟成功结果和真实图像资源所有权。</summary>
        public Mat Publish(byte intensity)
        {
            var mat = new Mat(32, 64, MatType.CV_8UC3, Scalar.All(intensity));
            Result = new NodeResultImageSource { OutputImage = OutputImage.FromOwnedSingleImage(mat) };
            SetStatus(NodeStatus.Successful, "5");
            return mat;
        }
        /// <summary>模拟下一轮替换结果，由框架释放旧资源。</summary>
        public void ReleaseOutput() { Result = new NodeResultImageSource(); }
    }

    /// <summary>可控的后台显示数据源，验证后台转换和界面绘图隔离。</summary>
    private sealed class TestFeed : IRuntimeBindingDataSource
    {
        /// <summary>读取开始通知。</summary>
        public readonly System.Threading.ManualResetEvent ReadStarted = new System.Threading.ManualResetEvent(false);
        /// <summary>允许后台读取完成。</summary>
        public readonly System.Threading.ManualResetEvent ReleaseRead = new System.Threading.ManualResetEvent(false);
        /// <summary>读取完成通知。</summary>
        public readonly System.Threading.ManualResetEvent ReadFinished = new System.Threading.ManualResetEvent(false);
        /// <summary>目标控件编号。</summary>
        private readonly string id;
        /// <summary>只发送一帧。</summary>
        private int reads;
        /// <summary>最近返回的结果，用于确认关闭后的所有权释放。</summary>
        public RuntimeValueUpdate LastUpdate;
        /// <summary>建立测试结果源。</summary>
        public TestFeed(string id) { this.id = id; }
        /// <summary>等待测试放行后返回独立红色图像。</summary>
        public IReadOnlyList<RuntimeValueUpdate> Drain()
        {
            if (System.Threading.Interlocked.Increment(ref reads) != 1) return new RuntimeValueUpdate[0];
            ReadStarted.Set(); ReleaseRead.WaitOne();
            var bitmap = new Bitmap(100, 100);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Red);
            LastUpdate = new RuntimeValueUpdate { ElementId = id, Image = bitmap };
            ReadFinished.Set();
            return new[] { LastUpdate };
        }
        /// <summary>放行可能正在等待的后台线程。</summary>
        public void Dispose() { ReleaseRead.Set(); }
    }
}
