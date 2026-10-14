using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TDJS_Vision.Startup
{
    /// <summary>
    /// 协调方案恢复期间的 AI 模型自动加载顺序，避免不同 native 环境并发抢占 DLL 搜索路径。
    /// </summary>
    internal static class StartupAiRuntimeLoadGate
    {
        /// <summary>
        /// 保护延迟队列和延迟层级的同步对象。
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 当前 TDAI 自动加载延迟层级，大于 0 表示方案恢复期正在延迟加载。
        /// </summary>
        private static int tdaiDeferralDepth;

        /// <summary>
        /// 方案恢复期间暂存的 TDAI 自动加载动作。
        /// </summary>
        private static readonly Queue<DeferredRuntimeLoad> DeferredTDAILoads = new Queue<DeferredRuntimeLoad>();

        /// <summary>本次方案恢复启动的模型任务及失败检查。</summary>
        private static readonly List<ModelLoadCompletion> ModelLoadCompletions = new List<ModelLoadCompletion>();

        /// <summary>
        /// 开始延迟 TDAI 自动加载，通常覆盖方案反序列化到 AI 预加载完成这一段。
        /// </summary>
        /// <returns>释放后结束当前延迟作用域的对象。</returns>
        public static IDisposable BeginTDAIDeferral()
        {
            lock (SyncRoot)
            {
                tdaiDeferralDepth++;
            }

            return new TDAIDeferralScope();
        }

        /// <summary>
        /// 运行或暂存 TDAI 模型加载动作。
        /// </summary>
        /// <param name="description">用于日志记录的加载对象描述。</param>
        /// <param name="loadAction">真正启动模型加载的动作。</param>
        public static void RunOrDeferTDAILoad(string description, Action loadAction)
        {
            if (loadAction == null)
                return;

            bool shouldRunImmediately;
            lock (SyncRoot)
            {
                shouldRunImmediately = tdaiDeferralDepth <= 0;
                if (!shouldRunImmediately)
                {
                    DeferredTDAILoads.Enqueue(new DeferredRuntimeLoad(description, loadAction));
                    LogHelper.AddLog(MsgLevel.Info, $"TDAI模型加载已延后到AI预加载完成后执行：{description}", true);
                    return;
                }
            }

            loadAction();
        }

        /// <summary>
        /// 放行并执行方案恢复期间暂存的 TDAI 自动加载动作。
        /// </summary>
        public static void FlushDeferredTDAILoads()
        {
            DeferredRuntimeLoad[] pendingLoads;
            lock (SyncRoot)
            {
                pendingLoads = DeferredTDAILoads.ToArray();
                DeferredTDAILoads.Clear();
            }

            int dispatchFailures = 0;
            for (int i = 0; i < pendingLoads.Length; i++)
            {
                DeferredRuntimeLoad pendingLoad = pendingLoads[i];
                try
                {
                    LogHelper.AddLog(MsgLevel.Info, $"AI预加载完成，开始放行TDAI模型加载：{pendingLoad.Description}", true);
                    pendingLoad.LoadAction();
                }
                catch (Exception ex)
                {
                    dispatchFailures++;
                    StartupProgressContext.ReportFailure("放行TDAI模型加载", ex);
                    LogHelper.AddLog(MsgLevel.Exception, $"放行TDAI模型加载失败：{pendingLoad.Description}，原因：{ex.Message}", true);
                }
            }

            ModelLoadCompletion[] completions;
            lock (SyncRoot)
            {
                completions = ModelLoadCompletions.ToArray();
                ModelLoadCompletions.Clear();
            }
            // 准备信号和自动运行必须在每个模型句柄确实可用后才能开始。
            Task.WhenAll(completions.Select(item => item.Task)).GetAwaiter().GetResult();
            foreach (ModelLoadCompletion completion in completions)
            {
                string error = completion.GetError();
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException($"方案模型加载失败：{completion.Description}，原因：{error}");
            }
            if (dispatchFailures > 0)
                throw new InvalidOperationException($"方案模型启动失败：{dispatchFailures}个加载任务未能启动。");
            LogHelper.AddLog(MsgLevel.Info, $"TDAI模型全部加载完成：数量={completions.Length}。", true);
        }

        /// <summary>登记方案恢复期的模型加载任务。</summary>
        public static void RegisterModelLoad(string description, Task task, Func<string> getError)
        {
            lock (SyncRoot)
            {
                if (tdaiDeferralDepth > 0)
                    ModelLoadCompletions.Add(new ModelLoadCompletion(description, task, getError));
            }
        }

        /// <summary>
        /// 结束一个 TDAI 自动加载延迟作用域。
        /// </summary>
        private static void EndTDAIDeferral()
        {
            lock (SyncRoot)
            {
                if (tdaiDeferralDepth > 0)
                    tdaiDeferralDepth--;

                if (tdaiDeferralDepth == 0 && DeferredTDAILoads.Count > 0)
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"TDAI延迟加载作用域结束时仍有{DeferredTDAILoads.Count}个加载请求未放行，已丢弃。", true);
                    DeferredTDAILoads.Clear();
                }
                if (tdaiDeferralDepth == 0)
                    ModelLoadCompletions.Clear();
            }
        }

        /// <summary>
        /// 保存一个延迟执行的模型加载动作。
        /// </summary>
        private sealed class DeferredRuntimeLoad
        {
            /// <summary>
            /// 初始化延迟模型加载动作。
            /// </summary>
            /// <param name="description">加载对象描述。</param>
            /// <param name="loadAction">模型加载动作。</param>
            public DeferredRuntimeLoad(string description, Action loadAction)
            {
                Description = string.IsNullOrWhiteSpace(description) ? "未命名TDAI模型" : description;
                LoadAction = loadAction ?? throw new ArgumentNullException(nameof(loadAction));
            }

            /// <summary>
            /// 获取加载对象描述。
            /// </summary>
            public string Description { get; }

            /// <summary>
            /// 获取模型加载动作。
            /// </summary>
            public Action LoadAction { get; }
        }

        /// <summary>一个模型任务以及任务完成后读取错误的回调。</summary>
        private sealed class ModelLoadCompletion
        {
            /// <summary>模型加载描述。</summary>
            public string Description { get; }
            /// <summary>实际模型加载任务。</summary>
            public Task Task { get; }
            /// <summary>模型内部捕获异常后的错误读取器。</summary>
            public Func<string> GetError { get; }

            /// <summary>创建一个模型任务记录。</summary>
            public ModelLoadCompletion(string description, Task task, Func<string> getError)
            {
                Description = description;
                Task = task;
                GetError = getError;
            }
        }

        /// <summary>
        /// 表示一次 TDAI 自动加载延迟作用域。
        /// </summary>
        private sealed class TDAIDeferralScope : IDisposable
        {
            /// <summary>
            /// 标记当前作用域是否已经释放。
            /// </summary>
            private bool disposed;

            /// <summary>
            /// 释放当前延迟作用域。
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                EndTDAIDeferral();
            }
        }
    }
}
