using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>验证设备恢复批次的并发执行、等待和失败传播。</summary>
internal static class StartupDeviceConnectionBatchTests
{
    /// <summary>执行独立于真实设备的并发恢复测试。</summary>
    private static int Main()
    {
        Type batchType = typeof(TDJS_Vision.ConfigHelper).Assembly.GetType(
            "TDJS_Vision.Startup.StartupDeviceConnectionBatch", true);
        MethodInfo queue = batchType.GetMethod("RunOrQueue", BindingFlags.Public | BindingFlags.Static);
        MethodInfo complete = batchType.GetMethod("Complete", BindingFlags.Public | BindingFlags.Instance);

        using (IDisposable batch = (IDisposable)Activator.CreateInstance(batchType, true))
        using (var started = new CountdownEvent(2))
        {
            int finished = 0;
            Action work = () =>
            {
                started.Signal();
                if (!started.Wait(TimeSpan.FromSeconds(3)))
                    throw new TimeoutException("连接任务没有并发启动。");
                Interlocked.Increment(ref finished);
            };
            queue.Invoke(null, new object[] { "相机", work });
            queue.Invoke(null, new object[] { "PLC", work });
            if (finished != 0)
                throw new Exception("连接任务在批次完成前提前运行。");
            complete.Invoke(batch, null);
            if (finished != 2)
                throw new Exception("批次完成后仍有连接任务未结束。");
        }

        Type gateType = typeof(TDJS_Vision.ConfigHelper).Assembly.GetType(
            "TDJS_Vision.Startup.StartupAiRuntimeLoadGate", true);
        MethodInfo beginGate = gateType.GetMethod("BeginTDAIDeferral", BindingFlags.Public | BindingFlags.Static);
        MethodInfo defer = gateType.GetMethod("RunOrDeferTDAILoad", BindingFlags.Public | BindingFlags.Static);
        MethodInfo register = gateType.GetMethod("RegisterModelLoad", BindingFlags.Public | BindingFlags.Static);
        MethodInfo flush = gateType.GetMethod("FlushDeferredTDAILoads", BindingFlags.Public | BindingFlags.Static);
        using ((IDisposable)beginGate.Invoke(null, null))
        {
            var modelFinished = new TaskCompletionSource<bool>();
            defer.Invoke(null, new object[] { "测试模型", new Action(() =>
            {
                register.Invoke(null, new object[]
                    { "测试模型", modelFinished.Task, new Func<string>(() => null) });
                Task.Run(async () =>
                {
                    await Task.Delay(150);
                    modelFinished.SetResult(true);
                });
            }) });
            var watch = System.Diagnostics.Stopwatch.StartNew();
            flush.Invoke(null, null);
            if (!modelFinished.Task.IsCompleted || watch.ElapsedMilliseconds < 120)
                throw new Exception("方案加载没有等待模型完成。");
        }

        using (IDisposable batch = (IDisposable)Activator.CreateInstance(batchType, true))
        {
            queue.Invoke(null, new object[] { "失败设备", new Action(() =>
                { throw new InvalidOperationException("连接失败"); }) });
            try
            {
                complete.Invoke(batch, null);
                throw new Exception("设备连接失败后批次仍然成功。");
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException == null || !ex.InnerException.Message.Contains("连接失败"))
                    throw;
            }
        }

        Console.WriteLine("设备并发恢复测试通过。");
        return 0;
    }
}
