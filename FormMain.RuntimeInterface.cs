using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Logger;
using TDJS_Vision.Forms.RuntimeDesigner;
using TDJS_Vision.Forms.GlobalSignalSettings;
using TDJS_Vision.Forms.Login;

namespace TDJS_Vision
{
    /// <summary>主窗口的自定义运行视图入口，不更改原检测和图像窗口。</summary>
    public partial class FormMain
    {
        /// <summary>按稳定界面编号管理独立窗体，不复用其它界面的显示订阅。</summary>
        private readonly Dictionary<string, RuntimeInterfacePreviewForm> runtimeInterfaceWindows =
            new Dictionary<string, RuntimeInterfacePreviewForm>(StringComparer.Ordinal);

        /// <summary>主窗口唯一生产控制器；预览窗口只借用，不负责释放或停机。</summary>
        private RuntimeFrameworkRunController runtimeRunController;

        /// <summary>只向创建窗口时的方案转发命令，阻止定时关闭前的过期点击。</summary>
        private sealed class RuntimeViewRunController : IRuntimeRunController
        {
            /// <summary>主窗口共享的实际执行入口，此适配器不拥有它。</summary>
            private readonly IRuntimeRunController controller;
            /// <summary>窗口存活及方案快照的有效性判断。</summary>
            private readonly Func<bool> isCurrent;

            /// <summary>绑定共享入口和窗口作用域，不创建新的执行队列。</summary>
            public RuntimeViewRunController(IRuntimeRunController controller, Func<bool> isCurrent)
            { this.controller = controller; this.isCurrent = isCurrent; }

            /// <summary>保留主窗口真实运行状态。</summary>
            public bool IsRunning => controller.IsRunning;
            /// <summary>保留主窗口真实停止状态。</summary>
            public bool IsStopping => controller.IsStopping;
            /// <summary>失效窗口不再显示新方案的流程目录。</summary>
            public IReadOnlyList<RuntimeProcessChoice> GetProcesses() => isCurrent()
                ? controller.GetProcesses() : new RuntimeProcessChoice[0];

            /// <summary>启动前同步校验，不能依赖下一次界面定时刷新。</summary>
            public Task RunAsync(int? processId, bool continuous)
            { EnsureCurrent(); return controller.RunAsync(processId, continuous); }

            /// <summary>旧窗口同样不能停止新方案。</summary>
            public Task StopAsync()
            { EnsureCurrent(); return controller.StopAsync(); }

            /// <summary>在任何设备信号或执行入口之前拒绝过期命令。</summary>
            private void EnsureCurrent()
            {
                if (!isCurrent())
                    throw new InvalidOperationException("运行界面对应的方案已变更或窗口已关闭，请重新打开运行界面。");
            }
        }

        /// <summary>原按钮、自动启动及所有运行控件共用同一准入和真实状态。</summary>
        private RuntimeFrameworkRunController RuntimeRunController => runtimeRunController ??
            (runtimeRunController = new RuntimeFrameworkRunController(
                _singleRunAdmissionController,
                () => Solution.Instance.IsRunning,
                () => Solution.Instance.IsRunning && Solution.Instance.CancellationToken.IsCancellationRequested,
                GetRuntimeProcesses, RunRuntimeFrameworkAsync, StopRuntimeFrameworkAsync,
                RefreshRuntimeRunStatus));

        /// <summary>读取根流程目录，仅返回可主动运行的流程，不暴露生产实例。</summary>
        private IReadOnlyList<RuntimeProcessChoice> GetRuntimeProcesses()
        {
            return (Solution.Instance.AllProcesses ?? new List<Process>())
                .Where(p => p != null && p.Enable && !p.IsPassiveTriggered)
                .Select(p => new RuntimeProcessChoice { Id = p.ID, Name = p.ProcessName }).ToArray();
        }

        /// <summary>冻结执行目标并复用现有校验、全局信号、运行核心及模式对应的收尾行为。</summary>
        private async Task RunRuntimeFrameworkAsync(int? processId, bool continuous, bool automatic,
            Func<Func<Task>, Task> start)
        {
            EnsureRuntimeRunPermission(automatic);
            var solution = Solution.Instance;
            var roots = (solution.AllProcesses ?? new List<Process>()).ToArray();
            Process[] selected = roots;
            if (processId.HasValue)
            {
                selected = roots.Where(p => p != null && p.ID == processId.Value).ToArray();
                if (selected.Length != 1 || !selected[0].Enable || selected[0].IsPassiveTriggered)
                    throw new InvalidOperationException("指定流程不存在、编号重复、已禁用或为被动流程，请重新选择。");
            }
            if (!automatic && !ValidateCameraConfigurationBeforeRun(selected)) return;

            bool online = false;
            bool started = false;
            try
            {
                await FormGlobalSignal.SendGlobalSignals(this, true);
                online = true;
                await start(() =>
                {
                    // 等待通信返回期间可能发生注销，进入执行核心前再次检查。
                    EnsureRuntimeRunPermission(automatic);
                    // 信号等待期间方案可能被其它入口替换；绝不按旧ID回退到新方案或全部流程。
                    if (!ReferenceEquals(solution, Solution.Instance) ||
                        !roots.SequenceEqual(solution.AllProcesses ?? new List<Process>()))
                        throw new InvalidOperationException("方案流程已变更，请重新启动。");
                    if (processId.HasValue && (!selected[0].Enable || selected[0].IsPassiveTriggered))
                        throw new InvalidOperationException("指定流程已禁用或改为被动流程，请重新选择。");
                    FormGlobalSignal.StartListenSignals();
                    if (automatic) FormGlobalSignal.StartHeartbeatSignals();
                    started = true;
                    return solution.RunProcessesAsync(selected, continuous);
                });
            }
            finally
            {
                FormGlobalSignal.StopListenSignals();
                // 单次原本总发送结束信号，自动运行仅在准备信号成功后发送；普通循环仍由停止入口发送。
                // 准备阶段被停止时补发离线，避免迟到的准备信号覆盖已发送的停机信号。
                if ((!continuous && !automatic) || (automatic && online) || (online && !started))
                    await FormGlobalSignal.SendGlobalSignals(this, false);
            }
        }

        /// <summary>所有手动运行入口遵守登录状态，保留现有自动运行及无登录停机行为。</summary>
        private void EnsureRuntimeRunPermission(bool automatic)
        {
            if (!automatic && Role == UserRole.Unknown)
                throw new InvalidOperationException("当前用户已注销，请登录后再启动运行。");
        }

        /// <summary>沿用原全局停止和离线信号；不承诺仅停止所选流程。</summary>
        private async Task StopRuntimeFrameworkAsync()
        {
            FormGlobalSignal.StopListenSignals();
            bool stopped = await Solution.Instance.StopAsync();
            await FormGlobalSignal.SendGlobalSignals(this, false);
            if (!stopped)
                throw new TimeoutException("停止等待超时，仍在等待节点退出；请勿重新启动，运行状态将在实际退出后恢复。");
        }

        /// <summary>准备与停止收尾期间也锁住原运行和编辑入口。</summary>
        private void RefreshRuntimeRunStatus()
        {
            if (!IsDisposed && !Disposing)
                SetRunStatus(runtimeRunController?.IsRunning == true || Solution.Instance.IsRunning);
        }

        /// <summary>非模态打开方案布局，只监听现有结果，关闭后仍回到原运行界面。</summary>
        private void 自定义运行界面ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 打开入口也立即清理失效窗口，不依赖下一次 50ms 刷新。
            foreach (var existing in runtimeInterfaceWindows.Values.ToArray())
                if (!existing.IsDisposed && !existing.IsCurrentDocument) existing.Close();
            var pages = (Solution.Instance.RuntimeInterfaces ?? new List<RuntimeInterfacePage>()).ToArray();
            if (pages.Length == 0)
            {
                MessageBox.Show(this, "当前方案尚未配置运行界面，请先在运行界面设计中新增并保存。", "运行界面",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (int index = 0; index < pages.Length; index++)
                OpenRuntimeInterfaceWindow(pages[index], index, pages.Length);
        }

        /// <summary>每个配置对应一个独立窗口，已有有效窗口只激活，不重新订阅。</summary>
        private void OpenRuntimeInterfaceWindow(RuntimeInterfacePage page, int index, int count)
        {
            RuntimeInterfacePreviewForm existing;
            if (runtimeInterfaceWindows.TryGetValue(page.Id, out existing) && !existing.IsDisposed)
            { existing.Activate(); return; }
            RuntimeBindingDataSource source = null;
            RuntimeInterfacePreviewForm window = null;
            try
            {
                var solution = Solution.Instance;
                var document = page.Layout;
                var roots = solution.AllProcesses.ToArray();
                source = new RuntimeBindingDataSource(document, roots);
                Func<bool> isCurrent = () => !IsDisposed && !Disposing && window?.IsDisposed != true &&
                    ReferenceEquals(solution, Solution.Instance) &&
                    (solution.RuntimeInterfaces?.Any(item => ReferenceEquals(item, page)) == true) &&
                    ReferenceEquals(document, page.Layout) &&
                    roots.SequenceEqual(solution.AllProcesses);
                window = new RuntimeInterfacePreviewForm(document, source, isCurrent);
                source = null;
                runtimeInterfaceWindows[page.Id] = window;
                window.ConfigureRuntimeWindow(document.Name, index, count);
                window.PlacementWarning += message => LogHelper.AddLog(MsgLevel.Exception, message, true);
                window.AttachLocalPlacement(solution.SolFileName, page.Id);
                window.AttachRunController(new RuntimeViewRunController(RuntimeRunController, isCurrent));
                window.AttachTuningController(CreateRuntimeTuningController(roots, isCurrent));
                window.AttachLogSource(new RuntimeFrameworkLogSource(roots));
                window.FormClosed += (s, args) =>
                {
                    RuntimeInterfacePreviewForm current;
                    if (runtimeInterfaceWindows.TryGetValue(page.Id, out current) && ReferenceEquals(current, window))
                        runtimeInterfaceWindows.Remove(page.Id);
                };
                window.Show(this);
            }
            catch (Exception ex)
            {
                source?.Dispose();
                window?.Dispose();
                runtimeInterfaceWindows.Remove(page.Id);
                LogHelper.AddLog(MsgLevel.Exception, "自定义运行界面打开失败：" + ex.Message, true);
                MessageBox.Show(this, "自定义运行界面打开失败：" + ex.Message, "运行界面", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>返回原视图仅关闭显示窗口，不发送生产停止或设备指令。</summary>
        private void CloseRuntimeInterfaceWindows()
        {
            foreach (var window in runtimeInterfaceWindows.Values.ToArray()) window.Close();
            runtimeInterfaceWindows.Clear();
        }
    }
}
