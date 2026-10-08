using System;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>不连接设备的运行控制回归入口。</summary>
internal static class RuntimeRunControlTests
{
    /// <summary>测试断言数。</summary>
    private static int checks;
    /// <summary>先验证文档能力，再执行界面状态测试。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            Check(Enum.GetNames(typeof(RuntimeWidgetKind)).Contains("RunControl"), "运行控制必须注册为独立可拖入控件");
            VerifyDocument();
            VerifyButtons();
            VerifyStaleStop();
            VerifyPreviewIsolation();
            Console.WriteLine("PASS: " + checks + " run control assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    /// <summary>不符合预期时立即失败。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    /// <summary>目标编号往返、复制和撤销保持独立，旧文档缺省为全部流程。</summary>
    private static void VerifyDocument()
    {
        var session = new RuntimeLayoutSession(new RuntimeLayoutDocument());
        var element = session.Add(RuntimeWidgetKind.RunControl, Point.Empty);
        Check(element.RunProcessId == null, "新控件默认全流程");
        session.Edit(doc => doc.Elements[0].RunProcessId = 42);
        Check(RuntimeLayoutSerializer.Clone(session.Document).Elements[0].RunProcessId == 42, "模板保存指定流程编号");
        var copy = session.Document.Elements[0].Copy(); copy.RunProcessId = 99;
        Check(session.Document.Elements[0].RunProcessId == 42, "复制配置不污染原配置");
        session.Undo(); Check(session.Document.Elements[0].RunProcessId == null, "撤销恢复全流程");
        session.Redo(); Check(session.Document.Elements[0].RunProcessId == 42, "重做恢复指定流程");
        bool rejected = false;
        try { session.Edit(doc => doc.Elements[0].RunProcessId = -1); }
        catch (System.IO.InvalidDataException) { rejected = true; }
        Check(rejected && session.Document.Elements[0].RunProcessId == 42, "非法编号不能改变已有目标");
    }

    /// <summary>原生按钮正确派发参数、切换停止状态并拒绝重复启动。</summary>
    private static void VerifyButtons()
    {
        using (var form = new Form())
        using (var widget = new RuntimeRunControlWidget())
        {
            var fake = new RecordingController();
            widget.Bind(fake, null);
            form.Controls.Add(widget); form.Show(); Application.DoEvents();
            var once = Field<Button>(widget, "onceButton");
            var loop = Field<Button>(widget, "loopButton");
            once.PerformClick();
            Check(fake.Starts == 1 && fake.Target == null && !fake.Continuous, "单次按钮默认执行全部");
            Check(!once.Enabled && loop.AccessibleName == "停止运行", "执行中禁用单次并允许停止");
            once.PerformClick(); Check(fake.Starts == 1, "重复点击单次不得重复派发");
            fake.Finish(); Pump(() => once.Enabled);
            widget.Bind(fake, 42); loop.PerformClick();
            Check(fake.Starts == 2 && fake.Target == 42 && fake.Continuous, "循环按钮正确派发指定流程");
            loop.PerformClick();
            Check(fake.Stops == 1 && !loop.Enabled && loop.AccessibleName == "正在停止", "停止未完成不能继续启动");
            fake.Finish(); Pump(() => once.Enabled);
            Check(loop.AccessibleName == "循环运行", "停止完成恢复循环按钮");
            widget.Bind(fake, 99);
            Check(!once.Enabled && !loop.Enabled, "不存在的流程不得回退到全流程");
            widget.Bind(fake, null);
            for (int size = 0; size < 3; size++)
            {
                widget.Size = new Size(size == 0 ? 48 : size == 1 ? 128 : 384, size == 0 ? 32 : size == 1 ? 56 : 112);
                Application.DoEvents();
                Check(once.Width > 0 && loop.Width > 0 && !once.Bounds.IntersectsWith(loop.Bounds), "按钮缩放后不重叠");
                using (var bitmap = new Bitmap(widget.Width, widget.Height)) widget.DrawToBitmap(bitmap, widget.ClientRectangle);
            }
            form.Close();
        }
    }

    /// <summary>设计预览仅使用本地状态机，关闭预览取消本地循环。</summary>
    private static void VerifyPreviewIsolation()
    {
        var choices = new[] { new RuntimeProcessChoice { Id = 42, Name = "工位42" } };
        using (var controller = new RuntimePreviewRunController(choices))
        {
            Task loop = controller.RunAsync(42, true);
            Check(controller.IsRunning && !loop.IsCompleted, "模拟循环持续到停止");
            bool busy = false;
            try { controller.RunAsync(null, false); } catch (InvalidOperationException) { busy = true; }
            Check(busy, "模拟预览也拒绝重入");
            Task stop = controller.StopAsync(); Pump(() => stop.IsCompleted);
            Check(!controller.IsRunning && !controller.IsStopping, "模拟停止回到空闲");
            bool missing = false;
            try { controller.RunAsync(99, true); } catch (InvalidOperationException) { missing = true; }
            Check(missing, "模拟目录失效不启动全部流程");
            var document = new RuntimeLayoutDocument();
            document.Elements.Add(RuntimeLayoutSession.CreateElement(RuntimeWidgetKind.RunControl));
            using (var preview = new RuntimeInterfacePreviewForm(document))
            {
                preview.AttachRunController(controller);
                preview.AttachRunController(controller);
                var active = controller.RunAsync(null, true);
                preview.Dispose(); Pump(() => active.IsCompleted);
                Check(!controller.IsRunning, "关闭窗口结束模拟循环");
            }
        }
    }

    /// <summary>另一个控件尚未刷新时，过期停止按钮不能反向启动新一轮。</summary>
    private static void VerifyStaleStop()
    {
        using (var form = new Form())
        using (var first = new RuntimeRunControlWidget())
        using (var second = new RuntimeRunControlWidget { Left = 140 })
        {
            var controller = new RecordingController();
            first.Bind(controller, null); second.Bind(controller, null);
            form.Controls.Add(first); form.Controls.Add(second); form.Show(); Application.DoEvents();
            Field<Button>(first, "loopButton").PerformClick(); second.RefreshState();
            Check(Field<Button>(second, "loopButton").AccessibleName == "停止运行", "第二控件显示当前运行状态");
            controller.Finish();
            Field<Button>(second, "loopButton").PerformClick();
            Check(controller.Starts == 1 && !controller.IsRunning, "过期停止点击绝不能重新启动循环");
            Check(Field<Button>(second, "loopButton").AccessibleName == "循环运行", "过期停止点击只同步空闲状态");
            var secondLoop = Field<Button>(second, "loopButton");
            secondLoop.PerformClick(); first.RefreshState();
            var firstLoop = Field<Button>(first, "loopButton");
            typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(firstLoop,
                new object[] { new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0) });
            controller.Finish(); first.RefreshState();
            firstLoop.PerformClick();
            Check(controller.Starts == 2 && !controller.IsRunning, "按下停止后刷新为空闲仍不能反向启动");
            form.Close();
        }
    }

    /// <summary>取得设计器定义的私有控件。</summary>
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    /// <summary>泵送界面消息直到异步状态完成，超时即判定失败。</summary>
    private static void Pump(Func<bool> finished)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!finished() && watch.ElapsedMilliseconds < 5000) { Application.DoEvents(); System.Threading.Thread.Sleep(1); }
        Check(finished(), "界面状态应在超时前完成");
    }

    /// <summary>用可控完成点记录真实按钮派发，不访问任何生产设备。</summary>
    private sealed class RecordingController : IRuntimeRunController
    {
        /// <summary>启动次数。</summary>
        public int Starts;
        /// <summary>停止次数。</summary>
        public int Stops;
        /// <summary>最新目标。</summary>
        public int? Target;
        /// <summary>最新模式。</summary>
        public bool Continuous;
        /// <summary>运行状态。</summary>
        public bool IsRunning { get; private set; }
        /// <summary>停止状态。</summary>
        public bool IsStopping { get; private set; }
        /// <summary>当前命令完成屏障。</summary>
        private TaskCompletionSource<bool> completion;
        /// <summary>测试目录。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => new[] { new RuntimeProcessChoice { Id = 42, Name = "工位42" } };
        /// <summary>记录控件发送的命令并保持运行态。</summary>
        public Task RunAsync(int? processId, bool continuous)
        { Starts++; Target = processId; Continuous = continuous; IsRunning = true; completion = new TaskCompletionSource<bool>(); return completion.Task; }
        /// <summary>保持停止中直到测试主动放行。</summary>
        public Task StopAsync() { Stops++; IsStopping = true; return completion.Task; }
        /// <summary>结束正在进行的执行和停止。</summary>
        public void Finish() { IsRunning = false; IsStopping = false; completion.SetResult(true); }
    }
}
