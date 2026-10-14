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
            current.connections.Add(() =>
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    connect();
                    LogHelper.AddLog(MsgLevel.Info, $"设备并发恢复完成：{name}，耗时={watch.ElapsedMilliseconds}ms。", true);
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Exception, $"设备并发恢复失败：{name}，耗时={watch.ElapsedMilliseconds}ms，原因：{ex.Message}", true);
                    throw;
                }
            });
        }

        /// <summary>等待所有连接结束；任一失败阻止恢复流程及发送准备信号。</summary>
        public void Complete()
        {
            var watch = Stopwatch.StartNew();
            // 阻塞式SDK连接使用独立工作线程，避免线程池缓慢扩容抵消并发收益。
            Task[] tasks = connections.Select(action => Task.Factory.StartNew(action,
                System.Threading.CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default)).ToArray();
            Task.WhenAll(tasks).GetAwaiter().GetResult();
            LogHelper.AddLog(MsgLevel.Info, $"设备并发恢复批次完成：数量={tasks.Length}，总耗时={watch.ElapsedMilliseconds}ms。", true);
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
