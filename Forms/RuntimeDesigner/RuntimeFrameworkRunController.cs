using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TDJS_Vision.ResourceManagement;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>主窗口持有的生产运行适配器；不拥有设备，不因预览关闭而停止或释放宿主。</summary>
    internal sealed class RuntimeFrameworkRunController : IRuntimeRunController
    {
        /// <summary>保护启动占位、停止门闩及进入框架核心的先后顺序。</summary>
        private readonly object sync = new object();
        /// <summary>复用原单次运行准入器，不排队积压触发。</summary>
        private readonly SolutionRunAdmissionController admission;
        /// <summary>读取框架真实运行状态，包含本控制器以外的运行入口。</summary>
        private readonly Func<bool> hostRunning;
        /// <summary>读取框架全局取消状态。</summary>
        private readonly Func<bool> hostStopping;
        /// <summary>读取当前方案目录，不缓存生产流程对象。</summary>
        private readonly Func<IReadOnlyList<RuntimeProcessChoice>> processes;
        /// <summary>宿主执行完整校验、信号、流程快照及结束信号链路。</summary>
        private readonly Func<int?, bool, bool, Func<Func<Task>, Task>, Task> run;
        /// <summary>宿主请求全局停止，超时不伪造真实运行结束。</summary>
        private readonly Func<Task> stop;
        /// <summary>通知主窗口刷新原有运行按钮。</summary>
        private readonly Action stateChanged;
        /// <summary>覆盖准备信号、执行与结束信号全部阶段。</summary>
        private bool pending;
        /// <summary>停止请求保持到准备链和真实运行都退出。</summary>
        private bool stopping;
        /// <summary>合并同时到达的停止请求。</summary>
        private TaskCompletionSource<bool> stopCompletion;

        /// <summary>注入生产宿主边界，使准入和停止竞态可独立于设备验证。</summary>
        internal RuntimeFrameworkRunController(SolutionRunAdmissionController admission,
            Func<bool> hostRunning, Func<bool> hostStopping,
            Func<IReadOnlyList<RuntimeProcessChoice>> processes,
            Func<int?, bool, bool, Func<Func<Task>, Task>, Task> run,
            Func<Task> stop, Action stateChanged)
        {
            this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
            this.hostRunning = hostRunning ?? throw new ArgumentNullException(nameof(hostRunning));
            this.hostStopping = hostStopping ?? throw new ArgumentNullException(nameof(hostStopping));
            this.processes = processes ?? throw new ArgumentNullException(nameof(processes));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.stop = stop ?? throw new ArgumentNullException(nameof(stop));
            this.stateChanged = stateChanged ?? throw new ArgumentNullException(nameof(stateChanged));
        }

        /// <summary>准备与清理期间也保持忙碌，外部运行状态始终以框架为准。</summary>
        public bool IsRunning
        {
            get { lock (sync) return pending || stopCompletion != null || hostRunning(); }
        }

        /// <summary>停止超时仍保持门闩；真正退出后才自动恢复。</summary>
        public bool IsStopping
        {
            get { lock (sync) { RefreshStopping(); return stopping || hostStopping(); } }
        }

        /// <summary>每次取得当前方案的流程目录。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => processes();

        /// <summary>正式控件入口，重复启动明确失败，不回退全流程或排队。</summary>
        public Task RunAsync(int? processId, bool continuous) => RunAsync(processId, continuous, false);

        /// <summary>自动启动复用同一门禁，并保留原有心跳启动行为。</summary>
        internal Task RunAsync(int? processId, bool continuous, bool automatic)
        {
            if (!TryRunAsync(processId, continuous, automatic, out Task task))
                throw new InvalidOperationException("方案正在运行或停止，请等待当前任务退出。");
            return task;
        }

        /// <summary>原固定触发入口使用布尔准入结果，仍保留已有忙碌拒绝统计。</summary>
        internal bool TryRunAsync(int? processId, bool continuous, bool automatic, out Task task)
        {
            SolutionRunAdmissionLease lease;
            lock (sync)
            {
                RefreshStopping();
                if (stopping || hostStopping() || (!pending && hostRunning()) ||
                    !admission.TryAcquire(out lease))
                {
                    task = Task.CompletedTask;
                    return false;
                }
                pending = true;
            }
            task = ExecuteAsync(processId, continuous, automatic, lease);
            return true;
        }

        /// <summary>租约覆盖准备、执行和收尾，异常与停止也必定释放。</summary>
        private async Task ExecuteAsync(int? processId, bool continuous, bool automatic, SolutionRunAdmissionLease lease)
        {
            bool succeeded = false;
            try
            {
                stateChanged();
                await run(processId, continuous, automatic, StartHostRun);
                succeeded = true;
            }
            catch (OperationCanceledException) when (IsStopping)
            {
                // 准备信号等待期间收到停止请求，不再进入框架运行核心。
            }
            finally
            {
                lock (sync)
                {
                    lease.Complete(succeeded);
                    pending = false;
                    RefreshStopping();
                }
                stateChanged();
            }
        }

        /// <summary>停止请求与核心同步启动互斥，消除检查取消后又迟到启动的窗口。</summary>
        private Task StartHostRun(Func<Task> start)
        {
            lock (sync)
            {
                if (stopping || hostStopping()) throw new OperationCanceledException("启动已停止。");
                if (hostRunning()) throw new InvalidOperationException("已有其它入口正在运行方案。");
                return start();
            }
        }

        /// <summary>只调用框架全局停止；返回超时不等同于运行已经结束。</summary>
        public Task StopAsync()
        {
            TaskCompletionSource<bool> completion;
            lock (sync)
            {
                if (stopCompletion != null) return stopCompletion.Task;
                stopping = true;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                stopCompletion = completion;
            }
            _ = CompleteStopAsync(completion);
            return completion.Task;
        }

        /// <summary>先完成停止清理再结束共享任务，隔离同步回调和异步回调异常。</summary>
        private async Task CompleteStopAsync(TaskCompletionSource<bool> completion)
        {
            Exception failure = null;
            try { stateChanged(); await stop(); }
            catch (Exception ex) { failure = ex; }
            finally
            {
                lock (sync)
                {
                    stopCompletion = null;
                    RefreshStopping();
                }
                try { stateChanged(); }
                catch (Exception ex) { if (failure == null) failure = ex; }
            }
            if (failure == null) completion.TrySetResult(true);
            else completion.TrySetException(failure);
        }

        /// <summary>只在所有本地阶段和真实运行均已退出时解除停止门闩；调用方持锁。</summary>
        private void RefreshStopping()
        {
            if (!pending && stopCompletion == null && !hostRunning()) stopping = false;
        }
    }
}
