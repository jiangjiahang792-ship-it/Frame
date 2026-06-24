using Logger;
using System;
using System.Diagnostics;
using System.Threading;

namespace TDJS_Vision.Diagnostics
{
    /// <summary>
    /// 线程池诊断工具，记录任务提交到真正开始执行之间的排队耗时和线程池快照。
    /// </summary>
    public static class ThreadPoolDiagnostics
    {
        /// <summary>
        /// 线程池任务排队超过该阈值时，日志标记为疑似线程池排队或饥饿。
        /// </summary>
        private const double StarvationSuspectMilliseconds = 100;

        /// <summary>
        /// 取得高精度计时戳，用于计算线程池任务排队耗时。
        /// </summary>
        /// <returns>当前高精度计时戳。</returns>
        public static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// 计算从指定计时戳到当前时刻的毫秒数。
        /// </summary>
        /// <param name="startTimestamp">开始计时戳。</param>
        /// <returns>经过的毫秒数。</returns>
        public static double GetElapsedMilliseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            return elapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>
        /// 构建当前线程池状态快照文本。
        /// </summary>
        /// <returns>线程池工作线程、IO线程的活动数量、可用数量、最小数量和最大数量。</returns>
        public static string BuildSnapshotText()
        {
            int availableWorkerThreads;
            int availableCompletionPortThreads;
            int minWorkerThreads;
            int minCompletionPortThreads;
            int maxWorkerThreads;
            int maxCompletionPortThreads;

            ThreadPool.GetAvailableThreads(out availableWorkerThreads, out availableCompletionPortThreads);
            ThreadPool.GetMinThreads(out minWorkerThreads, out minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out maxWorkerThreads, out maxCompletionPortThreads);

            int activeWorkerThreads = Math.Max(0, maxWorkerThreads - availableWorkerThreads);
            int activeCompletionPortThreads = Math.Max(0, maxCompletionPortThreads - availableCompletionPortThreads);

            return $"线程池快照: 活动工作线程={activeWorkerThreads}, 最小工作线程={minWorkerThreads}, 可用工作线程={availableWorkerThreads}, 最大工作线程={maxWorkerThreads}, 活动IO线程={activeCompletionPortThreads}, 最小IO线程={minCompletionPortThreads}, 可用IO线程={availableCompletionPortThreads}, 最大IO线程={maxCompletionPortThreads}";
        }

        /// <summary>
        /// 记录线程池状态快照日志。
        /// </summary>
        /// <param name="message">诊断场景说明。</param>
        public static void LogSnapshot(string message)
        {
            LogHelper.AddLog(MsgLevel.Exception, $"【线程池诊断】{message}；{BuildSnapshotText()}", true);
        }

        /// <summary>
        /// 记录任务从提交线程池到开始执行的排队耗时。
        /// </summary>
        /// <param name="message">诊断场景说明。</param>
        /// <param name="queuedTimestamp">任务提交线程池前取得的计时戳。</param>
        public static void LogQueuedTaskStarted(string message, long queuedTimestamp)
        {
            double queueMilliseconds = GetElapsedMilliseconds(queuedTimestamp);
            string starvationHint = queueMilliseconds >= StarvationSuspectMilliseconds
                ? "，疑似线程池排队/饥饿"
                : string.Empty;
            LogHelper.AddLog(MsgLevel.Exception, $"【线程池诊断】{message}，排队耗时:{queueMilliseconds:F1}ms{starvationHint}；{BuildSnapshotText()}", true);
        }
    }
}
