using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;
using TDJS_Vision.Node._6_LogicTool.ProcessSignal;
using TDJS_Vision.Node._6_LogicTool.WaitProcessComplete;
using TDJS_Vision.Forms.ProcessNew;
using System.Windows.Forms;

/// <summary>验证未打开编辑器的组合模块能异步结束并响应全局停止。</summary>
internal static class StopLifecycleTests
{
    /// <summary>在界面线程克隆参数时暂停，精确复现构建期间由后台发出停止。</summary>
    public sealed class BlockingSignalParam : NodeParamProcessSignal
    {
        /// <summary>进入参数克隆阶段的通知。</summary>
        public static readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        /// <summary>允许本轮参数克隆退出。</summary>
        public static readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        /// <summary>测试界面线程编号。</summary>
        public static int UiThreadId;
        /// <summary>保证只阻塞一次。</summary>
        private static int _blocked;
        /// <summary>通过真实快照克隆触发测试屏障。</summary>
        public string ConstructionProbe
        {
            get
            {
                if (Thread.CurrentThread.ManagedThreadId == UiThreadId && Interlocked.Exchange(ref _blocked, 1) == 0)
                {
                    Entered.Set();
                    if (!Release.Wait(5000)) throw new TimeoutException("构建取消测试没有释放屏障");
                }
                return "构建取消测试";
            }
            set { }
        }
    }

    /// <summary>后台停止不能让尚未结束的界面构建脱离会话管理。</summary>
    private static void CheckConstructionCancellation(Solution solution)
    {
        BlockingSignalParam.UiThreadId = Thread.CurrentThread.ManagedThreadId;
        var root = new TDJS_Vision.Process("构建期间取消");
        var module = new NodeCompositeModule(902, "构建期间取消", root, NodeType.CompositeModule);
        module.ParamForm.Params = new NodeParamCompositeModule {
            NodeInfos = new System.Collections.Generic.List<NodeConfig> {
                new NodeConfig { ID = 901, NodeName = "等待信号", NodeType = NodeType.ProcessSignal, Active = true,
                    NodeParam = new BlockingSignalParam { WaitSignalName = "停止回归" } } } };
        root.AddNode(module); solution.AllProcesses.Add(root);
        bool endedDuringConstruction = false;
        var run = Task.Run(() => solution.Run(false));
        var cancellation = Task.Run(() =>
        {
            try
            {
                if (!BlockingSignalParam.Entered.Wait(5000)) throw new Exception("没有进入界面参数构建");
                solution.Stop();
                Thread.Sleep(250);
                endedDuringConstruction = run.IsCompleted || !solution.IsRunning;
            }
            finally { BlockingSignalParam.Release.Set(); }
        });
        Await(run); Await(cancellation);
        if (endedDuringConstruction) throw new Exception("构建尚未退出，运行会话已经被取消回调提前释放");
        if (solution.IsRunning) throw new Exception("构建退出后会话未清理");
    }
    /// <summary>模拟设备调用收到停止后仍需短暂清理，验证界面不能假装已经停止。</summary>
    private sealed class CleanupNode : NodeBase
    {
        /// <summary>构造没有设备副作用的测试节点。</summary>
        public CleanupNode(TDJS_Vision.Process process) : base(904, "清理中的节点", process, NodeType.ProcessSignal) { }
        /// <summary>取消后保留清理阶段。</summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            try { await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false); }
            finally { await Task.Delay(250).ConfigureAwait(false); }
            return new NodeReturn(NodeRunFlag.ContinueRun);
        }
    }

    /// <summary>模拟在等待节点启动之前已完成的本轮依赖。</summary>
    private sealed class CompletedNode : NodeBase
    {
        /// <summary>创建立即完成的无副作用节点。</summary>
        public CompletedNode(TDJS_Vision.Process process) : base(914, "立即完成", process, NodeType.ProcessSignal)
        { Result = new NodeResultProcessSignal(); }
        /// <summary>返回本轮成功结果。</summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            SetRunResult(DateTime.Now, NodeStatus.Successful);
            return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
        }
    }

    /// <summary>读取真实主窗体按钮状态。</summary>
    private static System.Windows.Forms.ToolStripItem Button(FormMain form, string name)
    {
        return (System.Windows.Forms.ToolStripItem)typeof(FormMain).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(form);
    }

    /// <summary>清理超时必须保留运行状态，完成后才允许重新运行。</summary>
    private static void CheckDrain(Solution solution)
    {
        var process = new TDJS_Vision.Process("清理超时回归");
        process.AddNode(new CleanupNode(process)); solution.AllProcesses.Add(process);
        using (var form = new FormMain())
        {
            var run = Task.Run(() => solution.Run(false)); PumpFor(300);
            var stop = solution.StopAsync(0); Await(stop);
            if (stop.Result || !solution.IsRunning) throw new Exception("清理未完成时错误报告停止成功");
            form.SetRunStatus(false);
            if (Button(form, "tsbt_SolRunOnce").Enabled || !Button(form, "tsbt_SolRunStop").Enabled)
                throw new Exception("清理未完成时主窗体提前解锁");
            stop = solution.StopAsync(); Await(stop); Await(run);
            form.SetRunStatus(false);
            if (!stop.Result || !Button(form, "tsbt_SolRunOnce").Enabled || Button(form, "tsbt_SolRunStop").Enabled)
                throw new Exception("清理完成后主窗体未恢复");
        }
    }

    /// <summary>等待节点先取消、依赖流程延迟结束时，旧结束通知不能放行下一轮。</summary>
    private static void CheckLateDependencyCompletion(Solution solution)
    {
        var dependency = new TDJS_Vision.Process("延迟退出的依赖") { ID = 911 };
        dependency.AddNode(new CleanupNode(dependency));
        var waiting = new TDJS_Vision.Process("等待本轮依赖") { ID = 910 };
        var node = new NodeWaitProcessComplete(912, "等待依赖", waiting, NodeType.WaitProcessComplete);
        node.ParamForm.Params = new NodeParamWaitProcessComplete { ProcessIDs = new System.Collections.Generic.List<int> { 911 } };
        waiting.AddNode(node);
        solution.AllProcesses.Add(waiting); solution.AllProcesses.Add(dependency);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            var run = Task.Run(() => solution.Run(false)); PumpFor(300);
            bool skippedWait = !waiting.IsRuning;
            var stop = solution.StopAsync(); Await(stop); Await(run);
            if (skippedWait) throw new Exception("旧依赖的取消通知使第 " + cycle + " 轮跳过等待");
        }
        dependency.Nodes.Clear(); dependency.AddNode(new CompletedNode(dependency));
        dependency.RunLv = ProcessLvEnum.Lv1;
        var completedRun = Task.Run(() => solution.Run(false));
        try
        {
            Await(completedRun);
            if (!dependency.Success || !waiting.Success) throw new Exception("本轮提前完成的依赖没有成功传递");
        }
        finally
        {
            if (solution.IsRunning) { var stop = solution.StopAsync(); Await(stop); Await(completedRun); }
        }
    }

    /// <summary>提供真实流程编辑器，人工操作启动、停止和双击节点。</summary>
    private static void ShowPreview(Solution solution)
    {
        solution.ProcessSignalDic["停止回归"] = new CountdownEvent(1);
        using (var window = new FormNewProcessWizard())
        using (var editor = new ProcessEditPanel("停止与节点编辑回归"))
        {
            var root = editor.RootProcess;
            var module = new NodeCompositeModule(902, "组合模块_停止验证", root, NodeType.CompositeModule);
            module.CanvasLocation = new System.Drawing.Point(160, 90);
            module.ParamForm.Params = new NodeParamCompositeModule {
                NodeInfos = new System.Collections.Generic.List<NodeConfig> {
                    new NodeConfig { ID = 901, NodeName = "等待信号", NodeType = NodeType.ProcessSignal,
                        Active = true, HasCanvasLayout = true, CanvasX = 160, CanvasY = 90,
                        NodeParam = new NodeParamProcessSignal { WaitSignalName = "停止回归" } } } };
            root.AddNode(module);
            var tabs = (TabControl)typeof(FormNewProcessWizard).GetField("tabControl1",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            var page = new TabPage("停止回归"); editor.Dock = DockStyle.Fill; page.Controls.Add(editor); tabs.TabPages.Add(page);
            window.Text = "停止与节点编辑验证（隔离方案）";
            window.Size = new System.Drawing.Size(1250, 820);
            Application.Run(window);
            if (solution.IsRunning) throw new Exception("关闭测试窗口时仍有运行任务");
        }
    }
    /// <summary>模拟真实界面消息循环，确保后台创建请求可以回到界面线程。</summary>
    private static void PumpFor(int milliseconds)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < milliseconds)
        { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(5); }
    }

    /// <summary>保持界面响应地等待真实任务，超时视为失败。</summary>
    private static void Await(Task task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timer.ElapsedMilliseconds < 6000) PumpFor(10);
        if (!task.IsCompleted) throw new Exception("任务在信号/停止后仍未退出");
        task.GetAwaiter().GetResult();
    }
    /// <summary>独立进程运行一种场景，避免失败任务影响其他验证。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        try
        {
            string mode = args.Length == 0 ? "complete" : args[0];
            var solution = Solution.Instance;
            if (mode == "latecompletion") { CheckLateDependencyCompletion(solution); Console.WriteLine("PASS latecompletion"); return 0; }
            if (mode == "constructcancel") { CheckConstructionCancellation(solution); Console.WriteLine("PASS constructcancel"); return 0; }
            if (mode == "preview") { ShowPreview(solution); return 0; }
            if (mode == "drain") { CheckDrain(solution); Console.WriteLine("PASS drain"); return 0; }
            var signal = new CountdownEvent(1);
            solution.ProcessSignalDic["停止回归"] = signal;
            var inner = new TDJS_Vision.Process("异步内部流程");
            NodeBase node = new NodeProcessSignal(901, "等待信号", inner, NodeType.ProcessSignal);
            node.ParamForm.Params = new NodeParamProcessSignal { WaitSignalName = "停止回归", IsSendSignal = false };
            if (mode == "dependency")
            {
                solution.AllProcesses.Add(new TDJS_Vision.Process("不会结束的依赖") { ID = 999, Enable = false });
                node = new NodeWaitProcessComplete(901, "等待流程", inner, NodeType.WaitProcessComplete);
                node.ParamForm.Params = new NodeParamWaitProcessComplete { ProcessIDs = new System.Collections.Generic.List<int> { 999 } };
            }
            inner.AddNode(node);
            var root = new TDJS_Vision.Process("停止回归主流程");
            if (mode == "signal") root.AddNode(node);
            else
            {
                var module = new NodeCompositeModule(902, "未打开的组合模块", root, NodeType.CompositeModule);
                module.ParamForm.Params = new NodeParamCompositeModule { NodeInfos = new System.Collections.Generic.List<NodeConfig> { new NodeConfig { ID = 901, NodeName = "等待信号", NodeType = node.NodeType, Active = true, NodeParam = node.ParamForm.Params } } };
                root.AddNode(module);
            }
            solution.AllProcesses.Add(root);
            if (mode == "nested")
            {
                var outer = (NodeCompositeModule)root.Nodes[0];
                var snapshot = (NodeParamCompositeModule)outer.ParamForm.Params;
                outer.ParamForm.Params = new NodeParamCompositeModule {
                    NodeInfos = new System.Collections.Generic.List<NodeConfig> {
                        new NodeConfig { ID = 903, NodeName = "内层组合", NodeType = NodeType.CompositeModule,
                            Active = true, NodeParam = snapshot } } };
            }
            var run = Task.Run(() => solution.Run(false));
            if (mode == "queued") Thread.Sleep(500); else PumpFor(500);
            if (run.IsCompleted) throw new Exception("等待信号前任务不应结束");
            if (mode == "complete") signal.Signal();
            else { var stop = solution.StopAsync(); Await(stop); if (!stop.Result) throw new Exception("停止等待超时"); }
            Await(run);
            if (solution.IsRunning || root.IsRuning) throw new Exception("停止后运行标志未清理");
            if (mode == "queued" && typeof(NodeCompositeModule).GetField("_runtimeProcess",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(root.Nodes[0]) != null)
                throw new Exception("已经取消的界面准备回调仍创建了内部流程");
            if (mode != "signal")
            {
                var module = (NodeCompositeModule)root.Nodes[0];
                var restored = module.GetEditableProcess();
                var form = (System.Windows.Forms.Form)restored.Nodes[0].ParamForm;
                form.Show(); System.Windows.Forms.Application.DoEvents(); form.Hide();
                if (form.IsDisposed) throw new Exception("停止后参数窗口不可编辑");
            }
            for (int cycle = 0; cycle < 10; cycle++)
            {
                // 再次进入真实方案运行入口，验证停止门闩、令牌和组合运行标志都已恢复。
                var rerun = Task.Run(() => solution.Run(true));
                PumpFor(60);
                if (rerun.IsCompleted) throw new Exception("重新运行被旧状态拒绝");
                var stopping = solution.StopAsync(); Await(stopping); Await(rerun);
                if (!stopping.Result || solution.IsRunning)
                    throw new Exception("重复停止失败，第 " + cycle + " 轮");
            }
            Console.WriteLine("PASS " + mode);
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}


