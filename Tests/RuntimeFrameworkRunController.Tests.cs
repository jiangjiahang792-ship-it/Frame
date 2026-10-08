using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDJS_Vision.Forms.RuntimeDesigner;
using TDJS_Vision.ResourceManagement;

/// <summary>以可控任务替代信号和设备，验证实际后端控制器的准入与停机。</summary>
internal static class RuntimeFrameworkRunControllerTests
{
    /// <summary>累计成功断言。</summary>
    private static int checks;

    /// <summary>非设备测试入口。</summary>
    private static int Main()
    {
        try
        {
            TestAsync().GetAwaiter().GetResult();
            checks += RuntimeFrameworkHostTests.RunAsync().GetAwaiter().GetResult();
            Console.WriteLine("PASS: " + checks + " backend assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    /// <summary>控制信号等待、运行退出及停止超时，覆盖真实控制器状态转换。</summary>
    private static async Task TestAsync()
    {
        var prepared = Signal();
        var executed = Signal();
        bool hostRunning = false;
        bool hostStopping = false;
        int starts = 0, stops = 0, changed = 0;
        int? receivedId = null;
        bool receivedContinuous = false;
        var admission = new SolutionRunAdmissionController();
        var controller = new RuntimeFrameworkRunController(
            admission, () => hostRunning, () => hostStopping,
            () => new[] { new RuntimeProcessChoice { Id = 17, Name = "流程17" } },
            async (id, continuous, automatic, start) =>
            {
                receivedId = id; receivedContinuous = continuous;
                await prepared.Task;
                await start(async () => { starts++; hostRunning = true; await executed.Task; hostRunning = false; });
            },
            () => { stops++; hostStopping = hostRunning; return Task.CompletedTask; },
            () => changed++);

        Task first = controller.RunAsync(17, true);
        Check(controller.IsRunning && !hostRunning, "准备阶段必须同步占位");
        Check(receivedId == 17 && receivedContinuous, "指定流程与循环模式不得丢失");
        Check(controller.GetProcesses()[0].Id == 17, "目录必须来自宿主");
        Reject(() => controller.RunAsync(null, false), "准备信号 await 中必须拒绝重复启动");
        await controller.StopAsync();
        Check(controller.IsStopping && controller.IsRunning && stops == 1, "准备未退出时停止状态不得提前清除");
        Reject(() => controller.RunAsync(null, true), "准备退出前禁止重启");
        prepared.SetResult(true);
        await first;
        Check(starts == 0 && !controller.IsRunning && !controller.IsStopping, "停止后迟到准备信号不得启动设备");

        prepared = Signal(); executed = Signal(); prepared.SetResult(true);
        Task second = controller.RunAsync(null, false);
        Check(hostRunning && starts == 1 && receivedId == null && !receivedContinuous, "全流程单次必须原样转交");
        await controller.StopAsync();
        Check(controller.IsRunning && controller.IsStopping, "停止超时必须保留真实运行及停止态");
        Reject(() => controller.RunAsync(null, false), "超时后仍不得重启");
        executed.SetResult(true);
        await second;
        hostStopping = false;
        Check(!controller.IsRunning && !controller.IsStopping, "真实退出后必须恢复空闲");
        Check(!admission.GetSnapshot().IsActive, "执行和取消路径必须释放原准入租约");

        hostRunning = true;
        Check(controller.IsRunning, "必须恢复外部运行状态");
        Reject(() => controller.RunAsync(null, true), "外部运行时不得启动");
        hostStopping = true;
        Check(controller.IsStopping, "必须恢复外部停止状态");
        hostRunning = false; hostStopping = false;
        Check(!controller.IsRunning && !controller.IsStopping, "外部退出后恢复空闲");
        Check(changed > 0, "状态变化必须通知宿主");

        var stopGate = Signal();
        var stopController = new RuntimeFrameworkRunController(admission, () => false, () => false,
            () => new RuntimeProcessChoice[0], (id, continuous, automatic, start) => Task.CompletedTask,
            () => stopGate.Task, () => { });
        Task stop1 = stopController.StopAsync();
        Task stop2 = stopController.StopAsync();
        Check(ReferenceEquals(stop1, stop2), "重复停止必须合并到同一任务");
        Reject(() => stopController.RunAsync(null, false), "停止回调期间即使宿主空闲也不得启动");
        stopGate.SetResult(true);
        await stop1;
        await stopController.RunAsync(null, false);
        Check(!stopController.IsRunning && !stopController.IsStopping, "同步完成不得遗留忙碌态");

        var broken = new RuntimeFrameworkRunController(admission, () => false, () => false,
            () => new RuntimeProcessChoice[0], (id, continuous, automatic, start) => { throw new InvalidOperationException("prepare"); },
            () => { throw new InvalidOperationException("stop"); }, () => { });
        try { await broken.RunAsync(null, false); throw new Exception("准备异常未传播"); }
        catch (InvalidOperationException) { Check(!broken.IsRunning, "准备异常必须释放门禁"); }
        try { await broken.StopAsync(); throw new Exception("停止异常未传播"); }
        catch (InvalidOperationException) { Check(!broken.IsStopping, "停止异常且宿主空闲时释放停止态"); }
    }

    /// <summary>构造可确定控制的异步屏障。</summary>
    private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>确认启动被立即拒绝，而不是排队延后执行。</summary>
    private static void Reject(Func<Task> action, string message)
    {
        try { action(); } catch (InvalidOperationException) { Check(true, message); return; }
        throw new Exception(message);
    }

    /// <summary>失败时终止测试。</summary>
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
}
