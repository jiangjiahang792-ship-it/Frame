using Logger;
using System;
using System.Collections.Generic;

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
                    StartupProgressContext.ReportFailure("放行TDAI模型加载", ex);
                    LogHelper.AddLog(MsgLevel.Exception, $"放行TDAI模型加载失败：{pendingLoad.Description}，原因：{ex.Message}", true);
                }
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
