using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TDJS_Vision.Startup
{
    /// <summary>收集主线程创建的设备连接动作，并在流程恢复前并发连接、统一等待。</summary>
    internal sealed class StartupDeviceConnectionBatch : IDisposable
    {
        /// <summary>仅方案加载线程持有当前批次，避免手动连接或其他线程加入。</summary>
        [ThreadStatic]
        private static StartupDeviceConnectionBatch current;

        /// <summary>当前批次待执行的连接任务，不在后台修改设备集合或创建控件。</summary>
        private readonly List<Action> connections = new List<Action>();

        /// <summary>当前批次连接失败的设备数量，由并发连接任务安全累加。</summary>
        private int failedConnectionCount;

        /// <summary>开始设备连接收集作用域。</summary>
        public StartupDeviceConnectionBatch()
        {
            if (current != null)
                throw new InvalidOperationException("不能嵌套恢复设备连接。");
            current = this;
        }

        /// <summary>方案恢复时排队连接，单独添加设备时保持同步连接行为。</summary>
        public static void RunOrQueue(string name, Action connect)
        {
            if (current == null)
            {
                connect();
                return;
            }
            // 捕获加载线程的批次实例，后台任务不能访问 ThreadStatic 的 current。
            StartupDeviceConnectionBatch batch = current;
            batch.connections.Add(() =>
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    connect();
                    LogHelper.AddLog(MsgLevel.Info, $"设备并发恢复完成：{name}，耗时={watch.ElapsedMilliseconds}ms。", true);
                }
                catch (Exception ex)
                {
                    System.Threading.Interlocked.Increment(ref batch.failedConnectionCount);
                    // 设备离线不等于方案数据无效，保留设备对象供流程绑定及后续重连。
                    LogHelper.AddLog(MsgLevel.Warn, $"设备连接恢复失败，继续加载方案：{name}，耗时={watch.ElapsedMilliseconds}ms，原因：{ex.Message}", true);
                }
            });
        }

        /// <summary>等待所有连接尝试结束；设备连接失败仅记录警告，不阻止方案及流程恢复。</summary>
        public void Complete()
        {
            var watch = Stopwatch.StartNew();
            failedConnectionCount = 0;
            // 阻塞式SDK连接使用独立工作线程，避免线程池缓慢扩容抵消并发收益。
            Task[] tasks = connections.Select(action => Task.Factory.StartNew(action,
                System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default)).ToArray();
            Task.WhenAll(tasks).GetAwaiter().GetResult();
            LogHelper.AddLog(failedConnectionCount == 0 ? MsgLevel.Info : MsgLevel.Warn,
                $"设备并发恢复批次完成：数量={tasks.Length}，失败={failedConnectionCount}，总耗时={watch.ElapsedMilliseconds}ms；继续恢复方案流程。", true);
            connections.Clear();
        }

        /// <summary>释放当前收集作用域；尚未执行的连接不再启动。</summary>
        public void Dispose()
        {
            if (ReferenceEquals(current, this))
                current = null;
            connections.Clear();
        }
    }
}
