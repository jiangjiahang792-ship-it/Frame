using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Logger;
using TDJS_Vision;
using TDJS_Vision.Forms.RuntimeDesigner;

/// <summary>运行日志后端的无设备回归入口，不创建窗体、不执行流程、不写用户配置。</summary>
internal static class RuntimeFrameworkLogTests
{
    /// <summary>已通过的行为断言数。</summary>
    private static int assertions;
    /// <summary>反射调用真实内部流，兼容源码编译和正式程序集测试。</summary>
    private static readonly Type stream = typeof(RuntimeLogContext).Assembly.GetType("Logger.RuntimeLogStream", true);
    /// <summary>执行全部后端测试。</summary>
    private static int Main(string[] args)
    {
        try
        {
            TestClearDetachesPendingState();
            TestConcurrentDrainWithoutOverflow().GetAwaiter().GetResult();
            TestLogHelper();
            TestIdentityAndScopes().GetAwaiter().GetResult();
            TestDelayedChildren().GetAwaiter().GetResult();
            TestBoundedSubscriptions();
            TestConcurrentSubscribers().GetAwaiter().GetResult();
            TestSourceHooks(args[0]);
            Console.WriteLine("PASS: " + assertions + " runtime framework log assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex); return 1; }
    }

    /// <summary>只生成字段壳，禁止构造流程时触发生产单例。</summary>
    private static Process Root(int id = 7, string name = "相同流程")
    {
        var value = (Process)FormatterServices.GetUninitializedObject(typeof(Process));
        value.ID = id; value.ProcessName = name;
        return value;
    }

    /// <summary>不经过文件路径的内存发布。</summary>
    private static void Publish(string message, MsgLevel level = MsgLevel.Info)
    { stream.GetMethod("Publish", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { level, message, DateTime.Now }); }

    /// <summary>失败立即返回明确的业务断言。</summary>
    private static void Check(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); Interlocked.Increment(ref assertions); }

    /// <summary>父作用域退出后，已有子任务仍持有原来源；退订不接收迟到消息。</summary>
    private static async Task TestDelayedChildren()
    {
        var root = Root();
        var factory = new RuntimeFrameworkLogSource(new[] { root });
        using (var first = factory.Subscribe(null, 100))
        using (var observer = factory.Subscribe(null, 100))
        {
            for (int round = 0; round < 2; round++)
            {
                var entered = new TaskCompletionSource<bool>();
                var release = new TaskCompletionSource<bool>();
                Task child;
                using (RuntimeLogContext.BeginProcess(root, "延迟根流程"))
                using (RuntimeLogContext.BeginNode(root, "后台节点"))
                {
                    child = Task.Run(async () =>
                    {
                        entered.SetResult(true);
                        await release.Task.ConfigureAwait(false);
                        Publish("迟到消息");
                    });
                    await entered.Task;
                }
                Publish("父作用域外消息");
                Check(observer.Drain(100).Count == 0, "父作用域退出必须恢复为空，不能误继承日志归属");
                if (round == 1) first.Dispose();
                release.SetResult(true);
                await child;
                var observed = observer.Drain(100);
                Check(observed.Count == 1 && observed[0].Source == "延迟根流程 / 后台节点", "已启动子任务延迟发布仍归属原根实例和路径");
                Check(first.Drain(100).Count == (round == 0 ? 1 : 0), "退订后拒绝迟到子任务，不影响另一窗口");
            }
        }
    }

    /// <summary>验证拒绝条件没有静默放宽范围。</summary>
    private static void Reject<T>(Action action, string description) where T : Exception
    {
        try { action(); } catch (T) { Check(true, description); return; }
        throw new InvalidOperationException(description);
    }

    /// <summary>入队旁路必须先遵守等级，再独立于文件线程和主日志显示开关交付。</summary>
    private static void TestLogHelper()
    {
        var root = Root();
        using (var subscription = new RuntimeFrameworkLogSource(new[] { root }).Subscribe(null, 10))
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        using (var exited = new ManualResetEventSlim())
        {
            BlockingCollection<Action> queue;
            bool workerHasContext = false;
            using (RuntimeLogContext.BeginProcess(root, "根流程"))
            {
                queue = (BlockingCollection<Action>)typeof(LogHelper).GetField("_logQueue", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                queue.Add(() =>
                {
                    workerHasContext = typeof(RuntimeLogContext).GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) != null;
                    entered.Set(); release.Wait(); exited.Set();
                });
            }
            Check(entered.Wait(5000), "文件工作线程屏障未进入");
            var enabled = typeof(LogLevelSettings).GetField("_infoEnabled", BindingFlags.Static | BindingFlags.NonPublic);
            var onlyExceptions = typeof(LogHelper).GetField("OnlyLogException", BindingFlags.Static | BindingFlags.NonPublic);
            bool previousEnabled = (bool)enabled.GetValue(null), previousOnly = (bool)onlyExceptions.GetValue(null);
            try
            {
                onlyExceptions.SetValue(null, false);
                using (RuntimeLogContext.BeginProcess(root, "根流程"))
                {
                    enabled.SetValue(null, false);
                    LogHelper.AddLog(MsgLevel.Info, "禁用消息", true);
                    Check(subscription.Drain(10).Count == 0 && queue.Count == 0, "禁用等级必须同时禁止文件及运行界面日志");
                    enabled.SetValue(null, true);
                    using (RuntimeLogContext.BeginNode(root, "节点")) LogHelper.AddLog(MsgLevel.Info, "非主界面消息", false);
                    var hidden = subscription.Drain(10);
                    Check(hidden.Count == 1 && hidden[0].Message == "非主界面消息" && hidden[0].Source == "根流程 / 节点", "文件线程阻塞时，isDisplay=false 的启用日志仍须立即交付并冻结节点路径");
                    Check(queue.Count == 1, "旁路不能抢走原文件任务");
                    LogHelper.AddLog(MsgLevel.Info, "主界面消息", true);
                    Check(subscription.Drain(10).Single().Message == "主界面消息" && queue.Count == 2, "原主界面日志仍进入文件队列");
                    onlyExceptions.SetValue(null, true);
                    LogHelper.AddLog(MsgLevel.Info, "仅异常模式", false);
                    Check(subscription.Drain(10).Count == 0 && queue.Count == 2, "仅异常模式不得被运行日志旁路绕过");
                }
                Check(!workerHasContext, "长期文件线程不得捕获首次调用的根流程实例");
            }
            finally
            {
                enabled.SetValue(null, previousEnabled); onlyExceptions.SetValue(null, previousOnly);
                // 屏障仍阻塞文件线程，丢弃本测试任务，避免创建生产日志文件。
                Action pending; while (queue.TryTake(out pending)) { }
                release.Set(); exited.Wait(5000);
            }
        }
    }

    /// <summary>异步分支、重名重号和显式组合模块归属不能互相串流。</summary>
    private static async Task TestIdentityAndScopes()
    {
        var a = Root(); var b = Root(); var child = Root(); var replacement = Root();
        var supplied = new List<Process> { a, b };
        var allSource = new RuntimeFrameworkLogSource(supplied);
        supplied.Clear(); supplied.Add(replacement);
        using (var onlyA = new RuntimeFrameworkLogSource(new[] { a }).Subscribe(7, 100))
        using (var onlyB = new RuntimeFrameworkLogSource(new[] { b }).Subscribe(null, 100))
        using (var all = allSource.Subscribe(null, 100))
        {
            using (var ambiguous = allSource.Subscribe(7, 3))
            using (var missing = allSource.Subscribe(999, 3))
            {
                Check(ambiguous.Drain(10).Single().Level == RuntimeLogLevel.Exception, "同号根实例只报告一次错误，不关闭整个运行窗口");
                Check(missing.Drain(10).Single().Level == RuntimeLogLevel.Exception, "失效编号只报告一次错误，不抛出窗口级异常");
                using (RuntimeLogContext.BeginProcess(a, "根A")) Publish("失效订阅不能接收");
                Check(ambiguous.Drain(10).Count == 0 && missing.Drain(10).Count == 0, "失效订阅不能回退全流程或反复报告");
                onlyA.Clear(); all.Clear();
            }
            using (RuntimeLogContext.BeginProcess(a, "7.相同流程"))
            {
                using (RuntimeLogContext.BeginNode(a, "2.组合"))
                using (RuntimeLogContext.BeginComposite(child, "2.组合"))
                using (RuntimeLogContext.BeginProcess(child, "内部流程"))
                using (RuntimeLogContext.BeginNode(child, "3.内部节点"))
                {
                    await Task.Yield(); Publish("模块");
                    // 被内部节点触发的另一个根流程不能继承组合模块的根身份。
                    using (RuntimeLogContext.BeginProcess(b, "7.另一个根"))
                    { await Task.Yield(); Publish("被触发根"); }
                    Publish("模块恢复");
                }
                Publish("父恢复");
                await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(async () =>
                {
                    using (RuntimeLogContext.BeginNode(a, "并行" + index))
                    { await Task.Delay(2); Publish("分支" + index); }
                })));
                Publish("并行后父恢复");
            }
            using (RuntimeLogContext.BeginProcess(replacement, "7.相同流程")) Publish("同号新实例");
            Publish("无归属消息");
            var aEntries = onlyA.Drain(100);
            Check(aEntries.Count == 12, "只订阅根A不能接收根B、重建实例或无归属日志");
            Check(aEntries[0].Source == "7.相同流程 / 2.组合 / 3.内部节点", "嵌套模块必须保留外层根路径");
            Check(aEntries[1].Source == aEntries[0].Source, "根B返回后必须恢复模块路径");
            Check(aEntries[2].Source == "7.相同流程" && aEntries.Last().Source == "7.相同流程", "节点释放及并行完成必须恢复父作用域");
            for (int index = 0; index < 8; index++)
                Check(aEntries.Single(value => value.Message == "分支" + index).Source == "7.相同流程 / 并行" + index, "异步并行节点不能串用来源");
            var bEntries = onlyB.Drain(100);
            Check(bEntries.Count == 1 && bEntries[0].Message == "被触发根" && bEntries[0].Source == "7.另一个根", "另一个根必须独立归属，不能当成模块");
            Check(all.Drain(100).Count == 13, "全部模式也必须限定到构造时捕获的实例集合");
        }
        Check(typeof(RuntimeLogContext).GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) == null, "全部作用域退出后不得遗留来源");
    }

    /// <summary>每个窗口独立保留最近N条，单条文本及批次均有界。</summary>
    private static void TestBoundedSubscriptions()
    {
        var root = Root(); var source = new RuntimeFrameworkLogSource(new[] { root });
        Reject<ArgumentOutOfRangeException>(() => source.Subscribe(null, 0), "零容量必须拒绝");
        Reject<ArgumentOutOfRangeException>(() => source.Subscribe(null, 10001), "无限容量必须拒绝");
        using (var first = source.Subscribe(null, 3))
        using (var second = source.Subscribe(null, 5))
        using (RuntimeLogContext.BeginProcess(root, "来源"))
        {
            for (int index = 0; index < 1000; index++) Publish(index.ToString());
            Check(first.Drain(0).Count == 0, "零批次不能消费");
            Check(string.Join(",", first.Drain(2).Select(value => value.Message)) == "997,998", "容量3只保留最后3条且遵守批次上限");
            Check(first.Drain(int.MaxValue).Single().Message == "999", "后续批次顺序正确");
            Check(second.Drain(int.MaxValue).Count == 5, "独立订阅不得互相抢占消费");
            Publish("清理前"); first.Clear(); Publish("清理后");
            Check(first.Drain(10).Single().Message == "清理后", "清空后新记录仍可显示");
            Check(second.Drain(10).Count == 2, "清空只影响当前订阅");
            using (RuntimeLogContext.BeginNode(root, new string('源', 2000))) Publish(new string('文', 10000));
            var bounded = first.Drain(10).Single();
            Check(bounded.Source.Length <= 1024 && bounded.Message.Length <= 4096, "来源和正文必须在入队前截断");
            second.Clear();
            foreach (MsgLevel level in Enum.GetValues(typeof(MsgLevel))) Publish(level.ToString(), level);
            var levels = second.Drain(10);
            Check(levels.Count == 5 && levels.Select(value => value.Level.ToString()).SequenceEqual(levels.Select(value => value.Message)), "五种等级必须显式映射正确");
            first.Dispose(); first.Dispose(); Publish("退订后");
            Check(first.Drain(10).Count == 0, "退订幂等且不再交付");
            Check(second.Drain(10).Single().Message == "退订后", "一个窗口关闭不影响其它窗口");
        }
    }

    /// <summary>获取当前队列，兼容旧实现以先复现不能分离清空边界的问题。</summary>
    private static object PendingQueue(object subscription)
    {
        var stateField = subscription.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        object owner = stateField == null ? subscription : stateField.GetValue(subscription);
        return owner?.GetType().GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    }

    /// <summary>清空以替换状态为边界，不逐条追赶生产者，也不能让旧写入污染新队列。</summary>
    private static void TestClearDetachesPendingState()
    {
        var root = Root();
        using (var subscription = new RuntimeFrameworkLogSource(new[] { root }).Subscribe(null, 4))
        using (RuntimeLogContext.BeginProcess(root, "清空边界"))
        {
            object backend = subscription.GetType().GetField("source", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(subscription);
            Publish("清空前");
            object oldQueue = PendingQueue(backend);
            object[] oldEntries = (object[])oldQueue.GetType().GetMethod("ToArray").Invoke(oldQueue, null);
            subscription.Clear();
            Check(!ReferenceEquals(oldQueue, PendingQueue(backend)), "清空必须原子分离旧队列，不能逐条追赶持续生产者");
            // 模拟已捕获旧队列、在清空后才提交的迟到生产者；不得进入新一代队列。
            oldQueue.GetType().GetMethod("Enqueue").Invoke(oldQueue, new[] { oldEntries[0] });
            Publish("清空后");
            Check(subscription.Drain(4).Single().Message == "清空后", "旧队列迟到提交不能重新出现在清空后的日志中");
            subscription.Dispose(); subscription.Clear();
            Check(PendingQueue(backend) == null, "退订后的清空不得重新创建或持有队列");
            Check(((Array)backend.GetType().GetField("roots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(backend)).Length == 0,
                "已关闭订阅不得通过根实例数组保留方案");
        }
    }

    /// <summary>容量充足时，并发取走记录不能把尚未发布的槽误当作可丢弃空洞。</summary>
    private static async Task TestConcurrentDrainWithoutOverflow()
    {
        var root = Root();
        using (var subscription = new RuntimeFrameworkLogSource(new[] { root }).Subscribe(null, 10000))
        using (var start = new ManualResetEventSlim())
        {
            int done = 0;
            var consumer = Task.Run(() =>
            {
                var seen = new List<string>(); start.Wait();
                while (Volatile.Read(ref done) == 0)
                    seen.AddRange(subscription.Drain(37).Select(value => value.Message));
                seen.AddRange(subscription.Drain(10000).Select(value => value.Message));
                return seen;
            });
            var producers = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                start.Wait();
                using (RuntimeLogContext.BeginProcess(root, "并发完整性"))
                    for (int index = 0; index < 1000; index++) Publish(worker + ":" + index);
            })).ToArray();
            start.Set();
            try { await Task.WhenAll(producers); }
            finally { Volatile.Write(ref done, 1); }
            var records = await consumer;
            Check(records.Count == 8000 && records.Distinct().Count() == 8000,
                "容量未满并发发布/消费必须恰好交付8000条，实际=" + records.Count);
        }
    }

    /// <summary>并发洪峰、清理和退订均不无限扩容或漏注册。</summary>
    private static async Task TestConcurrentSubscribers()
    {
        var root = Root(); var source = new RuntimeFrameworkLogSource(new[] { root });
        using (var subscription = source.Subscribe(null, 64))
        {
            object internalSource = subscription.GetType().GetField("source", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(subscription);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
            {
                using (RuntimeLogContext.BeginProcess(root, "并发" + worker))
                    for (int index = 0; index < 1000; index++) Publish(worker + ":" + index);
            })));
            var burst = subscription.Drain(int.MaxValue);
            Check(burst.Count > 0 && burst.Count <= 64 && burst.Select(value => value.Message).Distinct().Count() == burst.Count, "并发洪峰内存有界且不能重复消费");
            var tasks = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
            {
                using (RuntimeLogContext.BeginProcess(root, "关闭并发"))
                    for (int index = 0; index < 500; index++) Publish("关闭期间");
            })).ToList();
            tasks.Add(Task.Run(() => { for (int index = 0; index < 100; index++) subscription.Clear(); subscription.Dispose(); }));
            await Task.WhenAll(tasks);
            Check(subscription.Drain(64).Count == 0, "关闭与发布竞争后不再保留记录");
            Check(PendingQueue(internalSource) == null, "关闭后必须分离整个队列状态");
        }
        Check(((Array)stream.GetField("subscribers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length == 0, "所有订阅释放后静态注册表不能持有窗口或根流程");
    }

    /// <summary>不执行设备，通过源码检查所有生产入口和唯一组合继承边界。</summary>
    private static void TestSourceHooks(string root)
    {
        string process = File.ReadAllText(Path.Combine(root, "Process.cs"));
        Check(process.Split(new[] { "RuntimeLogContext.BeginProcess(" }, StringSplitOptions.None).Length - 1 == 5, "五个执行入口必须建立流程日志来源");
        Check(process.Contains("RuntimeLogContext.BeginNode(process,"), "节点统一执行入口必须建立节点路径");
        string composite = File.ReadAllText(Path.Combine(root, "Node/6-LogicTool/CompositeModule/NodeCompositeModule.cs"));
        Check(composite.Contains("RuntimeLogContext.BeginComposite(runtimeProcess,"), "模块内部执行必须显式继承外层根");
    }
}
