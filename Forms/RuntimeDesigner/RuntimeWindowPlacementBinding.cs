using System;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>将本机位置存储连接到正式窗口生命周期，移动时只更新内存，关闭才保存。</summary>
    internal sealed class RuntimeWindowPlacementBinding : IDisposable
    {
        /// <summary>当前独立运行窗体。</summary>
        private readonly Form window;
        /// <summary>可替换的本机配置存储。</summary>
        private readonly IRuntimeWindowPlacementStore store;
        /// <summary>打开时捕获的方案路径，不随方案切换写入其它方案记录。</summary>
        private readonly string solutionPath;
        /// <summary>重命名和排序后不变的页面编号。</summary>
        private readonly string pageId;
        /// <summary>配置读写异常只报告给宿主，不阻止开关运行窗口。</summary>
        private readonly Action<string> warn;
        /// <summary>当前显示器快照来源，测试可模拟不同分辨率和断开显示器。</summary>
        private readonly Func<RuntimeDisplayInfo[]> getDisplays;
        /// <summary>取得窗口所在显示器，最小化时不调用。</summary>
        private readonly Func<Form, string> getDisplayName;
        /// <summary>窗口正常显示后才开始记录，构造及恢复期间的临时尺寸不落盘。</summary>
        private bool tracking;
        /// <summary>恢复过程防止位置事件覆盖正在应用的数据。</summary>
        private bool restoring;
        /// <summary>避免重复关闭事件再次写入。</summary>
        private bool saved;
        /// <summary>最后一次非最小化的位置，最小化关闭仍使用此记录。</summary>
        private RuntimeWindowPlacement latest;

        /// <summary>接入事件，不立即创建文件或读取屏幕；加载时主窗口Owner已经确定。</summary>
        public RuntimeWindowPlacementBinding(Form window, IRuntimeWindowPlacementStore store, string solutionPath,
            string pageId, Action<string> warn, Func<RuntimeDisplayInfo[]> getDisplays = null, Func<Form, string> getDisplayName = null)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.solutionPath = solutionPath;
            this.pageId = pageId;
            this.warn = warn;
            this.getDisplays = getDisplays ?? (() => Screen.AllScreens.Select(screen => new RuntimeDisplayInfo
                { DeviceName = screen.DeviceName, WorkingArea = screen.WorkingArea }).ToArray());
            this.getDisplayName = getDisplayName ?? (form => Screen.FromControl(form).DeviceName);
            window.Load += Restore;
            window.Shown += BeginTracking;
            window.LocationChanged += Track;
            window.Resize += Track;
            window.FormClosing += BeforeClose;
            window.FormClosed += Save;
        }

        /// <summary>在显示前恢复到实际可用屏幕，缺失或损坏记录使用主窗口所在屏幕。</summary>
        private void Restore(object sender, EventArgs args)
        {
            restoring = true;
            try
            {
                RuntimeWindowPlacement stored = null;
                try { stored = store.Load(solutionPath, pageId); }
                catch (Exception error) { warn?.Invoke("读取运行窗口本机位置失败，使用默认位置：" + error.Message); }
                var displays = getDisplays();
                string fallback = getDisplayName(window.Owner ?? window);
                latest = RuntimeWindowPlacementGeometry.Resolve(stored, displays, fallback, window.MinimumSize);
                window.StartPosition = FormStartPosition.Manual;
                window.WindowState = FormWindowState.Normal;
                // 小屏幕不能被原最小尺寸挤出工作区。
                window.MinimumSize = new System.Drawing.Size(Math.Min(window.MinimumSize.Width, latest.WorkingArea.Width),
                    Math.Min(window.MinimumSize.Height, latest.WorkingArea.Height));
                window.Bounds = latest.NormalBounds;
                window.WindowState = latest.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }
            catch (Exception error) { warn?.Invoke("恢复运行窗口本机位置失败：" + error.Message); }
            finally { restoring = false; }
        }

        /// <summary>初次显示完成后记录最终位置，不记录加载中的过渡状态。</summary>
        private void BeginTracking(object sender, EventArgs args)
        {
            tracking = true;
            Track(sender, args);
        }

        /// <summary>普通窗口记录真实边界，最大化跨屏时将还原边界换算到新工作区。</summary>
        private void Track(object sender, EventArgs args)
        {
            if (!tracking || restoring || window.IsDisposed || window.WindowState == FormWindowState.Minimized) return;
            try
            {
                var displays = getDisplays();
                string deviceName = getDisplayName(window);
                var current = displays.FirstOrDefault(display => display.DeviceName == deviceName) ?? displays.FirstOrDefault();
                if (current == null) return;
                if (window.WindowState == FormWindowState.Normal)
                {
                    latest = new RuntimeWindowPlacement { DeviceName = current.DeviceName, WorkingArea = current.WorkingArea,
                        NormalBounds = window.Bounds, Maximized = false };
                }
                else
                {
                    // 强制以当前最大化窗口所在屏幕为目标，而不是上一次的屏幕编号。
                    latest = RuntimeWindowPlacementGeometry.Resolve(latest, new[] { current }, current.DeviceName, window.MinimumSize);
                    latest.Maximized = true;
                }
            }
            catch (Exception) { /* 显示器切换的短暂不可用期间保留最后有效位置，不在移动事件反复报错。 */ }
        }

        /// <summary>关闭前保留最终状态，取消关闭不会触发保存。</summary>
        private void BeforeClose(object sender, FormClosingEventArgs args) { Track(sender, args); }

        /// <summary>仅成功关闭已显示的正式窗口时保存，失败不阻止其它窗口关闭。</summary>
        private void Save(object sender, FormClosedEventArgs args)
        {
            if (saved || !tracking || latest == null) return;
            saved = true;
            try { store.Save(solutionPath, pageId, latest.Copy()); }
            catch (Exception error) { warn?.Invoke("保存运行窗口本机位置失败：" + error.Message); }
        }

        /// <summary>解除所有事件，构造失败或单纯Dispose不保存未显示窗口的默认位置。</summary>
        public void Dispose()
        {
            window.Load -= Restore;
            window.Shown -= BeginTracking;
            window.LocationChanged -= Track;
            window.Resize -= Track;
            window.FormClosing -= BeforeClose;
            window.FormClosed -= Save;
            tracking = false;
        }
    }
}
