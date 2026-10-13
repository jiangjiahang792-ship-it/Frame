using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TDJS_Vision;
using TDJS_Vision.Forms.Login;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Node;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

/// <summary>隔离运行调参后端，不执行生产学习、设备或方案写入。</summary>
internal static class RuntimeTuningBackendTests
{
    /// <summary>已完成断言数。</summary>
    private static int assertions;
    /// <summary>验证条件。</summary>
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    /// <summary>验证拒绝路径没有打开参数窗体或开始学习。</summary>
    private static void Reject(Action action, string message)
    {
        int before = FormSolRunParam.Shows + SolRunParamControl.Starts;
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
        Check(before == FormSolRunParam.Shows + SolRunParamControl.Starts, message + "不可分发");
    }
    /// <summary>执行真实后端服务与隔离替身的行为测试。</summary>
    private static int Main()
    {
        try
        {
            bool current = true;
            var first = new NodeTDAI { ID = 7 };
            var second = new NodeTDAI { ID = 7 };
            var child1 = new Process { ID = 3, Nodes = new List<NodeBase> { first } };
            var child2 = new Process { ID = 3, Nodes = new List<NodeBase> { second } };
            var module1 = new NodeCompositeModule { Inner = child1 };
            var module2 = new NodeCompositeModule { Inner = child2 };
            var root = new Process { ID = 1, Nodes = new List<NodeBase> { module1, module2 } };
            var other = new Process { ID = 2 };
            var service = new ProcessTuningService(new[] { root, other }, () => current);
            Action manual = () => service.Execute(null, false, 1);
            Action learn = () => service.Execute(null, true, 1);
            UserPermissionContext.CanUseManualTuning = false;
            Check(!service.CanUse, "权限不可用");
            Reject(manual, "拒绝无权限手动调参");
            Reject(learn, "拒绝无权限学习");
            UserPermissionContext.CanUseManualTuning = true;
            Reject(() => service.Execute(null, false, null), "拒绝未绑定");
            Reject(() => service.Execute(null, true, 404), "拒绝失效编号");
            Reject(() => new ProcessTuningService(new[] { root, new Process { ID = 1 } }, () => true).Execute(null, false, 1), "拒绝重复根编号");
            current = false;
            Check(!service.CanUse, "旧方案不可用");
            Reject(manual, "拒绝旧方案");
            current = true;
            root.IsRuning = true;
            Reject(manual, "拒绝运行中手动调参");
            Reject(learn, "拒绝运行中学习");
            root.IsRuning = false;
            child1.IsRuning = true;
            int created = FormSolRunParam.Constructions;
            int initialized = SolRunParamControl.Initializations;
            Reject(manual, "根流程空闲但子流程运行时拒绝整流程参数");
            Reject(learn, "根流程空闲但子流程运行时拒绝学习");
            Check(FormSolRunParam.Constructions == created && SolRunParamControl.Initializations == initialized,
                "已有运行子流程时不得构造参数窗体或初始化学习参数");
            child1.IsRuning = false;
            var grandchild = new Process { IsRuning = true };
            var nestedModule = new NodeCompositeModule { Inner = grandchild };
            child1.Nodes.Add(nestedModule);
            Reject(manual, "深层子流程运行时拒绝整流程参数");
            child1.Nodes.Remove(nestedModule);
            other.IsRuning = true;
            manual();
            Check(FormSolRunParam.LastProcess == root, "只打开指定根流程");
            Check(FormSolRunParam.LastFilter == null && FormSolRunParam.LastTarget == null, "手动调参覆盖整个指定流程");
            FrmSelectAiNode.Pick = nodes => { Check(nodes.Count == 2, "同编号模块AI保留两个实例"); return second; };
            learn();
            Check(SolRunParamControl.LastProcess == child2 && SolRunParamControl.LastTarget == second, "组合AI按实例所属流程学习");
            Check(SolRunParamControl.Starts == 1, "只调用一次原学习核心");
            Check(SolRunParamControl.Disposals == 0 && SolRunParamControl.Initializations == 0, "学习不创建或初始化临时控件");
            FrmSelectAiNode.Pick = nodes => { current = false; return first; };
            Reject(learn, "选择后切方案拒绝"); current = true;
            FrmSelectAiNode.Pick = nodes => { UserPermissionContext.CanUseManualTuning = false; return first; };
            Reject(learn, "选择后降权拒绝"); UserPermissionContext.CanUseManualTuning = true;
            FrmSelectAiNode.Pick = nodes => { root.IsRuning = true; return first; };
            Reject(learn, "选择后根流程启动拒绝"); root.IsRuning = false;
            FrmSelectAiNode.Pick = nodes => { child1.IsRuning = true; return first; };
            Reject(learn, "选择后内部流程启动拒绝"); child1.IsRuning = false;
            FrmSelectAiNode.Pick = nodes => { module1.Inner = new Process(); return first; };
            Reject(learn, "选择后模块重建拒绝"); module1.Inner = child1;
            FrmSelectAiNode.Pick = nodes => new NodeTDAI { ID = 7 };
            Reject(learn, "选择器返回同编号外部节点拒绝");
            FrmSelectAiNode.Pick = nodes => null;
            int starts = SolRunParamControl.Starts;
            learn(); Check(starts == SolRunParamControl.Starts, "取消选择不学习");
            Reject(() => service.Execute(null, true, 2), "无AI不回退其它流程");
            FrmSelectAiNode.Pick = nodes => first;
            FormSolRunParam.OnCreate = () => current = false;
            Reject(manual, "参数窗体构造后方案失效不得显示");
            current = true;
            FormSolRunParam.OnCreate = null;
            SolRunParamControl.Succeed = false;
            bool failure = false;
            try { learn(); } catch (InvalidOperationException) { failure = true; }
            Check(failure, "原学习核心拒绝需反馈");
            SolRunParamControl.Succeed = true;
            var filter = new RunParamDisplayFilter();
            ProcessTuningService.ShowManualParameters(null, root, first, filter);
            Check(FormSolRunParam.LastFilter == filter && FormSolRunParam.LastTarget == first, "原图像窗口ROI过滤对象原样转交");
            child1.Nodes.Add(new NodeTDAI { ID = 7 });
            Reject(learn, "原核心按编号匹配存在歧义时禁止误学习");
            Console.WriteLine("RuntimeTuningBackend: " + assertions + " assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

namespace TDJS_Vision
{
    /// <summary>只保存流程身份及运行状态，不包含执行能力。</summary>
    public sealed class Process
    {
        /// <summary>流程编号。</summary>
        public int ID;
        /// <summary>流程名称。</summary>
        public string ProcessName = "同名流程";
        /// <summary>测试运行状态。</summary>
        public bool IsRuning;
        /// <summary>隔离节点目录。</summary>
        public List<NodeBase> Nodes = new List<NodeBase>();
    }
}
namespace TDJS_Vision.Node
{
    /// <summary>节点身份替身。</summary>
    public class NodeBase
    {
        /// <summary>节点编号。</summary>
        public int ID;
    }
}
namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>不包含模型与学习逻辑的AI身份。</summary>
    public sealed class NodeTDAI : NodeBase { }
}
namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>组合模块目录替身。</summary>
    public sealed class NodeCompositeModule : NodeBase
    {
        /// <summary>当前内部流程实例。</summary>
        public Process Inner;
        /// <summary>只返回内存目录。</summary>
        public Process GetEditableProcess() => Inner;
    }
}
namespace TDJS_Vision.Forms.Login
{
    /// <summary>测试权限状态，不初始化登录服务。</summary>
    public static class UserPermissionContext
    {
        /// <summary>当前测试是否允许调参。</summary>
        public static bool CanUseManualTuning;
    }
}
namespace TDJS_Vision.Forms.ImageViewer
{
    /// <summary>选择器替身，不创建窗口。</summary>
    public static class FrmSelectAiNode
    {
        /// <summary>可模拟选择期间的方案切换。</summary>
        public static Func<IReadOnlyList<NodeTDAI>, NodeTDAI> Pick;
        /// <summary>同步调用测试选择行为。</summary>
        public static NodeTDAI SelectNode(IWin32Window owner, string name, IEnumerable<NodeTDAI> nodes) => Pick(nodes.ToArray());
    }
}
namespace TDJS_Vision.Forms.SolRunParam
{
    /// <summary>只验证ROI过滤引用透传。</summary>
    public sealed class RunParamDisplayFilter { }
    /// <summary>手动参数窗体替身，无控件和存盘行为。</summary>
    public sealed class FormSolRunParam : IDisposable
    {
        /// <summary>构造次数，验证无效目标不会进入窗体初始化。</summary>
        public static int Constructions;
        /// <summary>模拟构造期间发生方案切换。</summary>
        public static Action OnCreate;
        /// <summary>打开次数。</summary>
        public static int Shows;
        /// <summary>最后选择的流程。</summary>
        public static Process LastProcess;
        /// <summary>最后选择的AI。</summary>
        public static NodeTDAI LastTarget;
        /// <summary>最后提供的ROI过滤。</summary>
        public static RunParamDisplayFilter LastFilter;
        /// <summary>只记录调用参数。</summary>
        public FormSolRunParam(Process process, NodeTDAI node, RunParamDisplayFilter filter)
        { Constructions++; LastProcess = process; LastTarget = node; LastFilter = filter; OnCreate?.Invoke(); }
        /// <summary>仅计数，不显示界面。</summary>
        public void ShowModeless(IWin32Window owner, Action validate) { validate(); Shows++; }
        /// <summary>替身无需释放资源。</summary>
        public void Dispose() { }
    }
    /// <summary>学习核心替身，不包含生产算法或保存代码。</summary>
    public sealed class SolRunParamControl : IDisposable
    {
        /// <summary>参数初始化次数。</summary>
        public static int Initializations;
        /// <summary>模拟控件构造期间发生方案切换。</summary>
        public static Action OnCreate;
        /// <summary>只调用测试屏障，不构造真实控件。</summary>
        public SolRunParamControl() { OnCreate?.Invoke(); }
        /// <summary>开始次数。</summary>
        public static int Starts;
        /// <summary>释放次数。</summary>
        public static int Disposals;
        /// <summary>原核心是否接受学习。</summary>
        public static bool Succeed = true;
        /// <summary>初始化期间模拟状态变化。</summary>
        public static Action OnInit;
        /// <summary>传入的确切流程。</summary>
        public static Process LastProcess;
        /// <summary>传入的确切AI节点。</summary>
        public static NodeTDAI LastTarget;
        /// <summary>只记录参数并执行测试屏障。</summary>
        public void Init(Process process, NodeTDAI target)
        { Initializations++; LastProcess = process; LastTarget = target; OnInit?.Invoke(); }
        /// <summary>仅记录调度，不执行学习。</summary>
        public bool StartAutoLearning() { Starts++; return Succeed; }
        /// <summary>模拟不创建控件的学习入口，仅记录精确目标。</summary>
        internal static bool StartAutoLearningForNode(Process process, NodeTDAI target)
        { LastProcess = process; LastTarget = target; Starts++; return Succeed; }
        /// <summary>记录临时控件释放。</summary>
        public void Dispose() { Disposals++; }
    }
}
