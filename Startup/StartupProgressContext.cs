using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 在当前异步启动链中保存进度接口和关键失败，避免修改既有设备事件签名。
    /// </summary>
    internal static class StartupProgressContext
    {
        /// <summary>
        /// 当前异步控制流对应的启动状态。
        /// </summary>
        private static readonly AsyncLocal<StartupProgressState> CurrentState = new AsyncLocal<StartupProgressState>();

        /// <summary>
        /// 获取当前代码是否正在启动进度作用域内执行。
        /// </summary>
        public static bool IsActive => CurrentState.Value != null;

        /// <summary>
        /// 建立一个启动进度作用域。
        /// </summary>
        /// <param name="reporter">接收进度的接口实现。</param>
        /// <returns>释放后恢复上一层状态的作用域。</returns>
        public static IDisposable Begin(IStartupProgressReporter reporter)
        {
            if (reporter == null)
                throw new ArgumentNullException(nameof(reporter));

            StartupProgressState previousState = CurrentState.Value;
            CurrentState.Value = new StartupProgressState(reporter);
            return new StartupProgressScope(previousState);
        }

        /// <summary>
        /// 上报一个不带对象序号的启动阶段。
        /// </summary>
        /// <param name="stageName">阶段名称。</param>
        /// <param name="itemName">当前对象名称。</param>
        /// <param name="percentage">整体完成百分比。</param>
        public static void ReportStage(string stageName, string itemName, int percentage)
        {
            StartupProgressState state = CurrentState.Value;
            state?.Reporter.Report(new StartupProgressInfo(stageName, itemName, 0, 0, percentage));
        }

        /// <summary>
        /// 上报某一阶段内正在加载的具体对象，并换算整体百分比。
        /// </summary>
        /// <param name="stageName">阶段名称。</param>
        /// <param name="itemName">当前对象名称。</param>
        /// <param name="current">当前对象序号，从 1 开始。</param>
        /// <param name="total">对象总数。</param>
        /// <param name="stageStartPercentage">阶段起始百分比。</param>
        /// <param name="stageEndPercentage">阶段结束百分比。</param>
        public static void ReportItem(
            string stageName,
            string itemName,
            int current,
            int total,
            int stageStartPercentage,
            int stageEndPercentage)
        {
            StartupProgressState state = CurrentState.Value;
            if (state == null)
                return;

            int safeTotal = Math.Max(0, total);
            int safeCurrent = safeTotal == 0 ? 0 : Math.Max(1, Math.Min(safeTotal, current));
            int range = Math.Max(0, stageEndPercentage - stageStartPercentage);
            int percentage = safeTotal == 0
                ? stageEndPercentage
                : stageStartPercentage + (int)Math.Round(range * safeCurrent / (double)safeTotal);

            state.Reporter.Report(new StartupProgressInfo(stageName, itemName, safeCurrent, safeTotal, percentage));
        }

        /// <summary>
        /// 收集启动期间出现的关键失败，等待当前反序列化事件链结束后统一处理。
        /// </summary>
        /// <param name="stageName">失败阶段名称。</param>
        /// <param name="exception">原始异常。</param>
        public static void ReportFailure(string stageName, Exception exception)
        {
            StartupProgressState state = CurrentState.Value;
            if (state == null || exception == null)
                return;

            lock (state.Failures)
            {
                state.Failures.Add(new StartupFailure(stageName, exception));
            }
        }

        /// <summary>
        /// 读取并清空当前作用域中收集到的失败，供非阻断数据加载流程降级为日志。
        /// </summary>
        /// <returns>当前作用域中尚未处理的失败集合。</returns>
        public static IReadOnlyList<StartupFailure> DrainFailures()
        {
            StartupProgressState state = CurrentState.Value;
            if (state == null)
                return Array.Empty<StartupFailure>();

            lock (state.Failures)
            {
                StartupFailure[] failures = state.Failures.ToArray();
                state.Failures.Clear();
                return failures;
            }
        }

        /// <summary>
        /// 如果当前启动作用域收集到关键失败，则抛出聚合后的启动异常。
        /// </summary>
        public static void ThrowIfFailures()
        {
            IReadOnlyList<StartupFailure> failures = DrainFailures();

            if (failures.Count > 0)
                throw new StartupInitializationException(failures);
        }

        /// <summary>
        /// 释放当前作用域并恢复进入前的异步状态。
        /// </summary>
        private sealed class StartupProgressScope : IDisposable
        {
            /// <summary>
            /// 进入当前作用域前保存的状态。
            /// </summary>
            private readonly StartupProgressState previousState;

            /// <summary>
            /// 标记当前作用域是否已经释放。
            /// </summary>
            private bool disposed;

            /// <summary>
            /// 初始化启动进度作用域。
            /// </summary>
            /// <param name="previousState">需要在释放时恢复的状态。</param>
            public StartupProgressScope(StartupProgressState previousState)
            {
                this.previousState = previousState;
            }

            /// <summary>
            /// 恢复上一层启动进度状态。
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                CurrentState.Value = previousState;
            }
        }
    }

    /// <summary>
    /// 保存一个启动进度作用域中的接口和失败集合。
    /// </summary>
    internal sealed class StartupProgressState
    {
        /// <summary>
        /// 初始化启动进度状态。
        /// </summary>
        /// <param name="reporter">接收进度的接口。</param>
        public StartupProgressState(IStartupProgressReporter reporter)
        {
            Reporter = reporter;
            Failures = new List<StartupFailure>();
        }

        /// <summary>
        /// 获取进度接收接口。
        /// </summary>
        public IStartupProgressReporter Reporter { get; }

        /// <summary>
        /// 获取当前作用域收集到的关键失败。
        /// </summary>
        public List<StartupFailure> Failures { get; }
    }

    /// <summary>
    /// 表示启动期间某一阶段出现的关键失败。
    /// </summary>
    internal sealed class StartupFailure
    {
        /// <summary>
        /// 初始化启动关键失败。
        /// </summary>
        /// <param name="stageName">失败阶段。</param>
        /// <param name="exception">原始异常。</param>
        public StartupFailure(string stageName, Exception exception)
        {
            StageName = stageName ?? "启动初始化";
            Exception = exception;
        }

        /// <summary>
        /// 获取失败阶段名称。
        /// </summary>
        public string StageName { get; }

        /// <summary>
        /// 获取原始异常。
        /// </summary>
        public Exception Exception { get; }
    }

    /// <summary>
    /// 表示一个或多个关键启动步骤失败。
    /// </summary>
    internal sealed class StartupInitializationException : Exception
    {
        /// <summary>
        /// 初始化聚合启动异常。
        /// </summary>
        /// <param name="failures">关键失败集合。</param>
        public StartupInitializationException(IReadOnlyList<StartupFailure> failures)
            : base(CreateMessage(failures), failures?.FirstOrDefault()?.Exception)
        {
            Failures = failures ?? Array.Empty<StartupFailure>();
        }

        /// <summary>
        /// 获取本次启动过程中收集到的关键失败。
        /// </summary>
        public IReadOnlyList<StartupFailure> Failures { get; }

        /// <summary>
        /// 生成适合启动页显示的简体中文错误信息。
        /// </summary>
        /// <param name="failures">关键失败集合。</param>
        /// <returns>不包含完整路径的错误摘要。</returns>
        private static string CreateMessage(IReadOnlyList<StartupFailure> failures)
        {
            if (failures == null || failures.Count == 0)
                return "启动初始化失败。";

            StartupFailure first = failures[0];
            string suffix = failures.Count > 1 ? $"，另有 {failures.Count - 1} 项失败" : string.Empty;
            return $"{first.StageName}失败：{first.Exception.Message}{suffix}";
        }
    }
}
