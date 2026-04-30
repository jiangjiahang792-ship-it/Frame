using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision
{
    /// <summary>
    /// 流程运行结束跟踪器
    /// </summary>
    public class ProcessCompletionTracker
    {
        // 移除 static，改为实例字段
        private readonly ConcurrentDictionary<int, ProcessCompletedEventArgs> _completedProcesses
            = new ConcurrentDictionary<int, ProcessCompletedEventArgs>();

        // 移除 static，改为实例事件
        public event Action<int> ProcessCompleted; // 通知监听者

        // 流程结束后调用
        public void MarkCompleted(int id, ProcessEndStatus status, Exception ex = null)
        {
            var args = new ProcessCompletedEventArgs(id, status, ex);
            _completedProcesses[id] = args;
            ProcessCompleted?.Invoke(id); // 触发事件
        }

        // 查询某个流程是否已结束
        public bool IsCompleted(int id)
        {
            return _completedProcesses.ContainsKey(id);
        }

        // 批量检查多个流程是否都已完成
        public bool AreAllCompleted(List<int> ids)
        {
            return ids.All(IsCompleted);
        }

        // 重置（用于当前节点重启时）
        public void Reset()
        {
            _completedProcesses.Clear();
        }
    }

    // 全局事件（可选）
    public static class ProcessEvents
    {
        public static event Action<int, ProcessEndStatus, Exception> ProcessEnded;

        public static void OnProcessEnded(int id, ProcessEndStatus status, Exception ex = null)
        {
            ProcessEnded?.Invoke(id, status, ex);
        }
    }

    /// <summary>
    /// 流程结束状态
    /// </summary>
    public enum ProcessEndStatus
    {
        /// <summary>
        /// 成功完成
        /// </summary>
        Completed,
        /// <summary>
        /// 失败
        /// </summary>
        Failed,
        /// <summary>
        /// 取消
        /// </summary>
        Cancelled
    }
    /// <summary>
    /// 流程完成事件参数
    /// </summary>
    public class ProcessCompletedEventArgs : EventArgs
    {
        public int ProcessID { get; }
        public ProcessEndStatus Status { get; }
        public Exception Exception { get; }

        public ProcessCompletedEventArgs(int id, ProcessEndStatus status, Exception ex = null)
        {
            ProcessID = id;
            Status = status;
            Exception = ex;
        }
    }
}
