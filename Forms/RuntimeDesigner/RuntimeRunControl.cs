using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>运行控制可选择的根流程，不携带设备或流程实例。</summary>
    public sealed class RuntimeProcessChoice
    {
        /// <summary>方案中的稳定编号。</summary>
        public int Id { get; set; }
        /// <summary>界面显示的流程名称。</summary>
        public string Name { get; set; }
    }

    /// <summary>设计器与实际执行解耦的运行入口，由宿主管理生命周期。</summary>
    public interface IRuntimeRunController
    {
        /// <summary>启动、执行或停止尚未完成时为真。</summary>
        bool IsRunning { get; }
        /// <summary>是否正在等待停止完成。</summary>
        bool IsStopping { get; }
        /// <summary>取得可供配置选择的根流程快照。</summary>
        IReadOnlyList<RuntimeProcessChoice> GetProcesses();
        /// <summary>按方案或指定流程启动，任务到本次执行真正结束才完成。</summary>
        Task RunAsync(int? processId, bool continuous);
        /// <summary>请求现有执行入口停止，等待实际退出。</summary>
        Task StopAsync();
    }

    /// <summary>编辑预览专用控制器，仅模拟按钮状态，永不调用生产执行链路。</summary>
    public sealed class RuntimePreviewRunController : IRuntimeRunController, IDisposable
    {
        /// <summary>独立的流程目录快照。</summary>
        private readonly RuntimeProcessChoice[] processes;
        /// <summary>当前模拟运行的取消来源。</summary>
        private CancellationTokenSource cancellation;
        /// <summary>当前运行任务，停止时等待其退出。</summary>
        private Task active = Task.CompletedTask;
        /// <summary>是否已经关闭，关闭后不再允许启动。</summary>
        private bool disposed;
        /// <summary>模拟执行状态，所有命令由界面线程调用。</summary>
        public bool IsRunning { get; private set; }
        /// <summary>模拟停止状态。</summary>
        public bool IsStopping { get; private set; }
        /// <summary>复制可选流程，不持有生产引用。</summary>
        public RuntimePreviewRunController(IReadOnlyList<RuntimeProcessChoice> choices)
        {
            processes = (choices ?? new RuntimeProcessChoice[0]).Select(p => new RuntimeProcessChoice { Id = p.Id, Name = p.Name }).ToArray();
        }
        /// <summary>返回独立目录，避免调用方修改模拟目标。</summary>
        public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => processes.Select(p => new RuntimeProcessChoice { Id = p.Id, Name = p.Name }).ToArray();
        /// <summary>单次短暂运行，循环持续到明确停止。</summary>
        public Task RunAsync(int? processId, bool continuous)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RuntimePreviewRunController));
            if (IsRunning || IsStopping) throw new InvalidOperationException("当前正在运行，请先停止。");
            if (processId.HasValue && !processes.Any(p => p.Id == processId.Value))
                throw new InvalidOperationException("指定流程已失效，请重新选择。");
            cancellation = new CancellationTokenSource();
            IsRunning = true;
            active = SimulateAsync(continuous, cancellation);
            return active;
        }
        /// <summary>只等待模拟延迟，不执行任何设备操作。</summary>
        private async Task SimulateAsync(bool continuous, CancellationTokenSource owner)
        {
            try { await Task.Delay(continuous ? Timeout.Infinite : 450, owner.Token); }
            catch (OperationCanceledException) { }
            finally
            {
                IsRunning = false;
                if (ReferenceEquals(cancellation, owner)) cancellation = null;
                owner.Dispose();
            }
        }
        /// <summary>取消模拟等待，直到本次任务完成才恢复按钮。</summary>
        public async Task StopAsync()
        {
            if (IsStopping) { await active; return; }
            IsStopping = true;
            try { cancellation?.Cancel(); await active; }
            finally { IsStopping = false; }
        }
        /// <summary>预览关闭仅取消本地模拟任务，不影响正式运行。</summary>
        public void Dispose() { disposed = true; cancellation?.Cancel(); }
    }
}
