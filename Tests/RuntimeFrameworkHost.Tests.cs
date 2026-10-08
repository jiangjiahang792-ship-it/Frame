using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.RuntimeDesigner;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Forms.Login;
using TDJS_Vision.ResourceManagement;

/// <summary>链接真实主窗口运行分部，用记录型替身隔离设备、窗体及执行调度。</summary>
internal static class RuntimeFrameworkHostTests
{
    /// <summary>断言计数。</summary>
    private static int checks;

    /// <summary>验证指定目标、信号模式、准备期停止与方案替换。</summary>
    internal static async Task<int> RunAsync()
    {
        var main = new FormMain();
        var enabled = new Process { ID = 1, Enable = true };
        var disabled = new Process { ID = 2 };
        var passive = new Process { ID = 3, Enable = true, IsPassiveTriggered = true };
        Solution.Instance.AllProcesses = new List<Process> { enabled, disabled, passive };
        IRuntimeRunController controller = main.Controller;
        Check(ReferenceEquals(controller, main.Controller), "主窗口必须共享同一控制器");
        Check(controller.GetProcesses().Select(p => p.Id).SequenceEqual(new[] { 1 }), "目录只包含可主动运行的根流程");

        foreach (int invalidId in new[] { 2, 3, 404 })
        {
            FormGlobalSignal.Reset();
            await Reject(() => controller.RunAsync(invalidId, false));
            Check(FormGlobalSignal.Events.Count == 0, "失效或非主动目标不得发送设备信号");
        }
        Solution.Instance.AllProcesses.Add(new Process { ID = 1, Enable = true });
        await Reject(() => controller.RunAsync(1, false));
        Solution.Instance.AllProcesses.RemoveAt(3);

        FormGlobalSignal.Reset();
        await controller.RunAsync(1, false);
        Check(Solution.Instance.Selected.SequenceEqual(new[] { enabled }), "指定目标只能传入所选对象");
        Check(!Solution.Instance.Continuous, "单次标志必须保留");
        Check(FormGlobalSignal.Events.SequenceEqual(new[] { "online", "listen", "unlisten", "offline" }), "单次信号时序必须保持");

        FormGlobalSignal.Reset();
        await controller.RunAsync(null, true);
        Check(Solution.Instance.Selected.SequenceEqual(new[] { enabled, disabled, passive }), "全流程必须把完整快照交给原核心过滤，不能重写主动被动逻辑");
        Check(Solution.Instance.Continuous, "循环标志必须保留");
        Check(FormGlobalSignal.Events.SequenceEqual(new[] { "online", "listen", "unlisten" }), "普通循环的正常退出不能擅自添加离线信号");

        FormGlobalSignal.Reset();
        await main.StartAutomatic();
        Check(FormGlobalSignal.Events.SequenceEqual(new[] { "online", "listen", "heartbeat", "unlisten", "offline" }), "自动启动必须保留心跳与结束信号");

        var prepare = new TaskCompletionSource<bool>();
        FormGlobalSignal.Reset();
        FormGlobalSignal.Prepared = prepare.Task;
        int before = Solution.Instance.Starts;
        Task pending = controller.RunAsync(1, false);
        Check(main.UiRunning && controller.IsRunning, "准备 await 前必须锁住主窗口");
        Solution.Instance.AllProcesses = new List<Process> { new Process { ID = 1, Enable = true } };
        prepare.SetResult(true);
        await Reject(() => pending);
        Check(Solution.Instance.Starts == before, "同ID新方案不得接管旧启动快照");

        prepare = new TaskCompletionSource<bool>();
        FormGlobalSignal.Reset();
        FormGlobalSignal.Prepared = prepare.Task;
        pending = controller.RunAsync(1, true);
        await controller.StopAsync();
        Check(controller.IsStopping, "信号仍未返回时停止门闩必须保留");
        prepare.SetResult(true);
        await pending;
        Check(Solution.Instance.Starts == before, "准备阶段停止不得迟到执行流程");
        Check(FormGlobalSignal.Events.Last() == "offline", "迟到的准备信号必须由离线信号收尾");
        Check(!controller.IsRunning && !main.UiRunning, "收尾结束才解锁主窗口");

        FormGlobalSignal.Reset();
        Solution.Instance.Running = true;
        Solution.Instance.Stopped = false;
        try { await controller.StopAsync(); throw new Exception("停止超时未报告"); }
        catch (TimeoutException)
        {
            Check(controller.IsRunning && controller.IsStopping, "超时异常反馈不能清除真实运行态");
            Check(FormGlobalSignal.Events.SequenceEqual(new[] { "unlisten", "offline" }), "超时仍保留原停止信号链路");
        }
        await Reject(() => controller.RunAsync(null, false));
        Solution.Instance.Running = false;
        Solution.Instance.Stopped = true;
        Check(!controller.IsRunning && !controller.IsStopping, "超时后真实退出才解除门闩");

        Solution.Instance.RuntimeInterface = new RuntimeLayoutDocument();
        Solution.Instance.RuntimeInterface.Elements.Add(new object());
        main.OpenRuntimeView();
        var view = RuntimeInterfacePreviewForm.LastCreated;
        Check(view != null && view.Current(), "正式运行窗口绑定当前方案");
        Check(view.LogSource != null && view.LogSource.Roots.SequenceEqual(Solution.Instance.AllProcesses), "日志源绑定全部根流程实例快照");
        Check(view.TuningController != null && view.TuningController.CanUse, "正式窗口注入真实调参控制器适配器");
        view.TuningController.Execute(main, RuntimeWidgetKind.ManualParameters, 1);
        Check(!TDJS_Vision.Forms.SolRunParam.ProcessTuningService.Learning &&
            TDJS_Vision.Forms.SolRunParam.ProcessTuningService.Target == 1, "手动参数按显式目标转交服务");
        view.TuningController.Execute(main, RuntimeWidgetKind.OneClickLearning, 1);
        Check(TDJS_Vision.Forms.SolRunParam.ProcessTuningService.Learning, "学习按钮转交原学习服务");
        Check(TDJS_Vision.Forms.SolRunParam.ProcessTuningService.Roots.SequenceEqual(Solution.Instance.AllProcesses),
            "调参服务捕获正式窗口根流程实例");
        FormGlobalSignal.Reset();
        await view.Controller.RunAsync(1, false);
        Check(Solution.Instance.Starts == before + 1, "有效运行窗口调用框架真实入口");
        before = Solution.Instance.Starts;
        var oldRoots = Solution.Instance.AllProcesses;
        Solution.Instance.AllProcesses = new List<Process> { new Process { ID = 1, Enable = true } };
        FormGlobalSignal.Reset();
        Check(!view.Current(), "同编号新流程使旧运行窗口失效");
        Check(!view.TuningController.CanUse, "方案切换后旧调参控制器立即失效");
        await Reject(() => { view.TuningController.Execute(main, RuntimeWidgetKind.OneClickLearning, 1); return Task.CompletedTask; });
        await Reject(() => view.Controller.RunAsync(1, false));
        await Reject(() => view.Controller.RunAsync(null, true));
        await Reject(() => view.Controller.StopAsync());
        Check(Solution.Instance.Starts == before && FormGlobalSignal.Events.Count == 0,
            "定时关闭前旧按钮也不能控制新方案或发送信号");

        // 恢复流程对象后，仅替换布局也必须使旧窗口失效。
        Solution.Instance.AllProcesses = oldRoots;
        var oldLayout = Solution.Instance.RuntimeInterface;
        Solution.Instance.RuntimeInterface = new RuntimeLayoutDocument();
        await Reject(() => view.Controller.RunAsync(null, false));
        Solution.Instance.RuntimeInterface = oldLayout;
        view.Dispose();
        Check(FormGlobalSignal.Events.Count == 0, "关闭显示窗口不发送停机信号");
        await Reject(() => view.Controller.RunAsync(null, false));
        await Reject(() => view.Controller.StopAsync());

        main.OpenRuntimeView();
        Check(!ReferenceEquals(view, RuntimeInterfacePreviewForm.LastCreated), "重新打开创建新的有效宿主窗口");
        await RuntimeInterfacePreviewForm.LastCreated.Controller.RunAsync(1, false);
        Check(Solution.Instance.Starts == before + 1, "重新打开后仍可调用同一框架运行入口");

        before = Solution.Instance.Starts;
        main.Role = UserRole.Unknown;
        FormGlobalSignal.Reset();
        await Reject(() => RuntimeInterfacePreviewForm.LastCreated.Controller.RunAsync(null, false));
        await Reject(() => controller.RunAsync(1, true));
        Check(FormGlobalSignal.Events.Count == 0 && Solution.Instance.Starts == before,
            "注销后所有手动启动入口都不得发送信号或执行节点");
        // 停止不受登录限制，不能因为注销而妨碍停机。
        await controller.StopAsync();
        Check(FormGlobalSignal.Events.SequenceEqual(new[] { "unlisten", "offline" }), "注销后仍允许停止");
        main.Role = UserRole.Low;
        prepare = new TaskCompletionSource<bool>();
        FormGlobalSignal.Reset();
        FormGlobalSignal.Prepared = prepare.Task;
        pending = controller.RunAsync(1, false);
        main.Role = UserRole.Unknown;
        prepare.SetResult(true);
        await Reject(() => pending);
        Check(Solution.Instance.Starts == before && FormGlobalSignal.Events.Last() == "offline",
            "准备阶段注销必须取消进入执行核心并恢复离线信号");
        main.Role = UserRole.Low;
        FormGlobalSignal.Reset();
        await controller.RunAsync(1, false);
        Check(Solution.Instance.Starts == before + 1, "登录恢复后操作员仍可正常启动");

        Solution.Instance.RuntimeInterfaces = new List<RuntimeInterfacePage>
        {
            new RuntimeInterfacePage { Id = "first", Layout = new RuntimeLayoutDocument { Name = "工位一" } },
            new RuntimeInterfacePage { Id = "second", Layout = new RuntimeLayoutDocument { Name = "工位二" } }
        };
        main.OpenRuntimeView();
        var active = RuntimeInterfacePreviewForm.Created.Where(w => !w.IsDisposed).ToArray();
        Check(active.Length == 2, "两个界面配置必须打开两个独立窗体，并关闭旧方案窗口");
        Check(active.Select(w => w.RuntimeName).SequenceEqual(new[] { "工位一", "工位二" }), "窗口名称对应各自布局");
        Check(active.Select(w => w.PlacementPageId).SequenceEqual(new[] { "first", "second" }) &&
            active.All(w => w.PlacementSolutionPath == Solution.Instance.SolFileName), "正式窗口按方案路径和独立页面编号接入本机位置存储");
        main.OpenRuntimeView();
        Check(RuntimeInterfacePreviewForm.Created.Count(w => !w.IsDisposed) == 2, "重复打开不能复制已有窗口");
        before = Solution.Instance.Starts;
        FormGlobalSignal.Reset();
        active[0].Close();
        Check(!active[1].IsDisposed && FormGlobalSignal.Events.Count == 0, "关闭一个窗口不影响另一个窗口或生产信号");
        await active[1].Controller.RunAsync(1, false);
        Check(Solution.Instance.Starts == before + 1, "剩余窗口仍可使用共享运行入口");
        main.OpenRuntimeView();
        Check(RuntimeInterfacePreviewForm.Created.Count(w => !w.IsDisposed) == 2 && !active[1].IsDisposed,
            "重新打开仅恢复关闭的窗口，不替换仍有效的其它窗口");
        var firstWindow = RuntimeInterfacePreviewForm.LastCreated;
        Solution.Instance.RuntimeInterfaces[1].Layout = new RuntimeLayoutDocument { Name = "新的工位二" };
        Check(firstWindow.Current() && !active[1].Current(), "替换单个布局仅使对应窗口失效");
        await Reject(() => active[1].Controller.RunAsync(null, false));
        main.CloseRuntimeViews();
        Check(RuntimeInterfacePreviewForm.Created.All(w => w.IsDisposed), "默认视图入口可关闭全部自定义窗体");
        return checks;
    }

    /// <summary>必须明确拒绝，不能无声回退或成功完成。</summary>
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); }
        catch (InvalidOperationException) { Check(true, "已拒绝非法目标或过期快照"); return; }
        throw new Exception("非法目标未拒绝");
    }

    /// <summary>失败时终止非设备测试。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
}

namespace TDJS_Vision
{
    /// <summary>主窗口替身，不创建真实窗体或窗口句柄。</summary>
    public partial class FormMain : IWin32Window
    {
        /// <summary>真实准入器。</summary>
        private readonly SolutionRunAdmissionController _singleRunAdmissionController = new SolutionRunAdmissionController();
        /// <summary>不创建窗口句柄。</summary>
        public IntPtr Handle => IntPtr.Zero;
        /// <summary>测试宿主始终有效。</summary>
        public bool IsDisposed => false;
        /// <summary>测试宿主不会销毁界面。</summary>
        public bool Disposing => false;
        /// <summary>主窗口显示的运行态。</summary>
        public bool UiRunning { get; private set; }
        /// <summary>模拟正式主窗口的当前用户身份。</summary>
        public UserRole Role { get; set; } = UserRole.Low;
        /// <summary>暴露实际生产分部构造的控制器。</summary>
        public IRuntimeRunController Controller => RuntimeRunController;
        /// <summary>调用自动运行重载，保留同一实际主窗口执行链。</summary>
        public Task StartAutomatic() => RuntimeRunController.RunAsync(null, true, true);
        /// <summary>调用真实正式运行窗口入口，窗口本身由非设备替身记录。</summary>
        public void OpenRuntimeView() => 自定义运行界面ToolStripMenuItem_Click(this, EventArgs.Empty);
        /// <summary>通过正式分部关闭全部自定义窗口。</summary>
        public void CloseRuntimeViews() => GetType().GetMethod("CloseRuntimeInterfaceWindows",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(this, null);
        /// <summary>代替相机校验，不连接硬件。</summary>
        private bool ValidateCameraConfigurationBeforeRun(IEnumerable<Process> processes) => true;
        /// <summary>记录主窗口锁定状态。</summary>
        private void SetRunStatus(bool running) { UiRunning = running; }
    }

    /// <summary>仅保留流程目录属性的记录型替身。</summary>
    public sealed class Process
    {
        /// <summary>流程编号。</summary>
        public int ID;
        /// <summary>启用状态。</summary>
        public bool Enable;
        /// <summary>被动状态。</summary>
        public bool IsPassiveTriggered;
        /// <summary>名称。</summary>
        public string ProcessName = "测试流程";
    }

    /// <summary>仅记录主窗口交付的快照，不执行节点、资源事务或设备。</summary>
    public sealed class Solution
    {
        /// <summary>测试方案唯一实例。</summary>
        public static readonly Solution Instance = new Solution();
        /// <summary>用于位置配置隔离的方案路径，不读取实际文件。</summary>
        public string SolFileName = @"C:\测试\方案.Sol";
        /// <summary>实际流程目录。</summary>
        public List<Process> AllProcesses = new List<Process>();
        /// <summary>未运行设备。</summary>
        public bool IsRunning => Running;
        /// <summary>模拟停止超时后的真实运行态。</summary>
        public bool Running;
        /// <summary>模拟框架停止是否超时。</summary>
        public bool Stopped = true;
        /// <summary>未触发全局取消。</summary>
        public CancellationToken CancellationToken => CancellationToken.None;
        /// <summary>预览入口编译所需布局替身。</summary>
        public RuntimeLayoutDocument RuntimeInterface
        {
            get => RuntimeInterfaces.FirstOrDefault()?.Layout;
            set
            {
                if (value == null) RuntimeInterfaces.Clear();
                else if (RuntimeInterfaces.Count == 0) RuntimeInterfaces.Add(new RuntimeInterfacePage { Layout = value });
                else RuntimeInterfaces[0].Layout = value;
            }
        }
        /// <summary>正式方案的独立运行窗体列表。</summary>
        public List<RuntimeInterfacePage> RuntimeInterfaces = new List<RuntimeInterfacePage>();
        /// <summary>最后传入的流程对象。</summary>
        public Process[] Selected;
        /// <summary>最后传入的循环模式。</summary>
        public bool Continuous;
        /// <summary>进入执行核心的次数。</summary>
        public int Starts;
        /// <summary>只记录快照，立即返回。</summary>
        public Task RunProcessesAsync(IEnumerable<Process> selected, bool continuous)
        { Selected = selected.ToArray(); Continuous = continuous; Starts++; return Task.CompletedTask; }
        /// <summary>停止替身不访问设备。</summary>
        public Task<bool> StopAsync() => Task.FromResult(Stopped);
    }
}

namespace TDJS_Vision.Forms.Login
{
    /// <summary>仅保留宿主权限比较所需角色，不加载登录窗体。</summary>
    public enum UserRole { Unknown, Low, Medium, Hight }
}

namespace TDJS_Vision.Forms.GlobalSignalSettings
{
    /// <summary>全局信号记录器，不包含通信实现。</summary>
    internal static class FormGlobalSignal
    {
        /// <summary>顺序事件记录。</summary>
        internal static readonly List<string> Events = new List<string>();
        /// <summary>可阻塞的准备信号。</summary>
        internal static Task Prepared = Task.CompletedTask;
        /// <summary>重置场景。</summary>
        internal static void Reset() { Events.Clear(); Prepared = Task.CompletedTask; }
        /// <summary>记录信号并等待测试屏障。</summary>
        internal static async Task SendGlobalSignals(object owner, bool online)
        { if (online) await Prepared; Events.Add(online ? "online" : "offline"); }
        /// <summary>记录监听启动。</summary>
        internal static void StartListenSignals() { Events.Add("listen"); }
        /// <summary>记录心跳启动。</summary>
        internal static void StartHeartbeatSignals() { Events.Add("heartbeat"); }
        /// <summary>记录监听结束。</summary>
        internal static void StopListenSignals() { Events.Add("unlisten"); }
    }
}

namespace Logger
{
    /// <summary>日志级别替身。</summary>
    internal enum MsgLevel { Exception }
    /// <summary>隔离测试日志不访问生产文件。</summary>
    internal static class LogHelper
    {
        /// <summary>忽略仅用于未执行窗口入口的日志。</summary>
        internal static void AddLog(MsgLevel level, string message, bool visible) { }
    }
}

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>仅用于编译真实调参控制器的控件类型。</summary>
    public enum RuntimeWidgetKind
    {
        /// <summary>手动参数入口。</summary>
        ManualParameters,
        /// <summary>一键学习入口。</summary>
        OneClickLearning
    }
    /// <summary>仅保留正式窗口目录需要的标识与布局。</summary>
    public sealed class RuntimeInterfacePage
    {
        /// <summary>稳定编号。</summary>
        public string Id = Guid.NewGuid().ToString("N");
        /// <summary>独立布局对象。</summary>
        public RuntimeLayoutDocument Layout;
    }
    /// <summary>供未执行的预览入口通过编译，不包含真实布局实现。</summary>
    public sealed class RuntimeLayoutDocument
    {
        /// <summary>窗体名称。</summary>
        public string Name = "测试界面";
        /// <summary>空元素集合。</summary>
        public List<object> Elements = new List<object>();
    }
    /// <summary>不创建任何订阅或设备。</summary>
    internal sealed class RuntimeBindingDataSource : IDisposable
    {
        /// <summary>窗口入口替身构造。</summary>
        public RuntimeBindingDataSource(RuntimeLayoutDocument layout, Process[] roots) { }
        /// <summary>无资源需要释放。</summary>
        public void Dispose() { }
    }
    /// <summary>隔离生产日志管线，仅记录正式入口传递的流程快照。</summary>
    internal sealed class RuntimeFrameworkLogSource
    {
        /// <summary>正式宿主注入的根流程实例。</summary>
        internal readonly Process[] Roots;
        /// <summary>复制根流程目录，不创建真实订阅。</summary>
        public RuntimeFrameworkLogSource(IEnumerable<Process> roots) { Roots = roots.ToArray(); }
    }
    /// <summary>无窗口替身，仅帮助链接真实主窗口分部。</summary>
    internal sealed class RuntimeInterfacePreviewForm : IDisposable
    {
        /// <summary>关闭通知占位。</summary>
        public event EventHandler FormClosed;
        /// <summary>本机位置异常记录入口，不执行文件读写。</summary>
        public event Action<string> PlacementWarning;
        /// <summary>正式宿主传递的方案路径。</summary>
        internal string PlacementSolutionPath;
        /// <summary>正式宿主传递的页面编号。</summary>
        internal string PlacementPageId;
        /// <summary>记录窗口释放状态。</summary>
        public bool IsDisposed { get; private set; }
        /// <summary>最近创建的正式宿主窗口替身。</summary>
        internal static RuntimeInterfacePreviewForm LastCreated;
        /// <summary>所有窗口的非设备创建记录。</summary>
        internal static readonly List<RuntimeInterfacePreviewForm> Created = new List<RuntimeInterfacePreviewForm>();
        /// <summary>正式宿主设置的独立窗口名称。</summary>
        internal string RuntimeName;
        /// <summary>立即检查绑定方案有效性。</summary>
        public bool IsCurrentDocument => Current();
        /// <summary>真实主窗口传入的控制器。</summary>
        internal IRuntimeRunController Controller;
        /// <summary>正式宿主注入的调参适配器。</summary>
        internal IRuntimeTuningController TuningController;
        /// <summary>正式宿主注入的日志订阅工厂。</summary>
        internal RuntimeFrameworkLogSource LogSource;
        /// <summary>真实主窗口传入的方案有效性检查。</summary>
        internal Func<bool> Current;
        /// <summary>保存接口形状，不创建控件。</summary>
        public RuntimeInterfacePreviewForm(RuntimeLayoutDocument document, RuntimeBindingDataSource source, Func<bool> current)
        { Current = current; LastCreated = this; Created.Add(this); }
        /// <summary>仅记录配置名，不移动真实窗口。</summary>
        public void ConfigureRuntimeWindow(string name, int index, int count) { RuntimeName = name; }
        /// <summary>仅记录接线参数，禁止隔离测试访问本机配置目录。</summary>
        public void AttachLocalPlacement(string solutionPath, string pageId)
        { PlacementSolutionPath = solutionPath; PlacementPageId = pageId; }
        /// <summary>激活替身，不显示真实窗口。</summary>
        public void Activate() { }
        /// <summary>显示替身，不连接设备。</summary>
        public void Show(object owner) { }
        /// <summary>预览接线编译验证。</summary>
        public void AttachRunController(IRuntimeRunController controller) { Controller = controller; }
        /// <summary>只记录注入，不创建调参控件或调用学习。</summary>
        public void AttachTuningController(IRuntimeTuningController controller) { TuningController = controller; }
        /// <summary>检查日志源接入，不创建生产日志订阅。</summary>
        public void AttachLogSource(RuntimeFrameworkLogSource source) { LogSource = source; }
        /// <summary>关闭替身并通知主窗口释放引用。</summary>
        public void Dispose() { IsDisposed = true; FormClosed?.Invoke(this, EventArgs.Empty); }
        /// <summary>模拟用户关闭窗口。</summary>
        public void Close() { Dispose(); }
    }
}

namespace TDJS_Vision.Forms.SolRunParam
{
    /// <summary>宿主接线测试替身，服务真实保护由专用后端测试验证。</summary>
    public sealed class ProcessTuningService
    {
        /// <summary>正式宿主捕获的根流程。</summary>
        internal static Process[] Roots;
        /// <summary>最后分发的操作。</summary>
        internal static bool Learning;
        /// <summary>最后显式绑定的目标。</summary>
        internal static int? Target;
        /// <summary>实际窗口身份检查。</summary>
        private readonly Func<bool> isCurrent;
        /// <summary>记录真实适配器传入的快照与身份校验。</summary>
        public ProcessTuningService(IEnumerable<Process> roots, Func<bool> isCurrent)
        { Roots = roots.ToArray(); this.isCurrent = isCurrent; }
        /// <summary>只验证窗口有效性，不加载真实权限服务。</summary>
        public bool CanUse => isCurrent();
        /// <summary>仅记录动作，不调用任何生产实现。</summary>
        public void Execute(IWin32Window owner, bool learning, int? processId)
        { Learning = learning; Target = processId; }
    }
}
