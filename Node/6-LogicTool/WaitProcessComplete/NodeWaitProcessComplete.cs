using Logger;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.WaitProcessComplete
{
    public class NodeWaitProcessComplete : NodeBase
    {
        /// <summary>
        /// 自己控制任务完成
        /// </summary>
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>串行保护结束通知、取消清理和下一轮等待源切换。</summary>
        private readonly object _completionSync = new object();
        /// <summary>本轮取消后忽略仍在退出的依赖流程通知，直到下一次运行。</summary>
        private bool _discardCompletions;
        /// <summary>取消时的方案令牌，用于识别重新启动后的真实新一轮通知。</summary>
        private CancellationToken _cancelledSessionToken;
        /// <summary>
        /// 每个节点拥有独立的 tracker
        /// </summary>
        private readonly ProcessCompletionTracker _tracker;

        public NodeWaitProcessComplete(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormWaitProcessComplete(process);
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultWaitProcessComplete();
            _tracker = new ProcessCompletionTracker(); // 实例化专属 tracker
            _tracker.ProcessCompleted += OnProcessCompleted; // 订阅自己的事件

            ProcessEvents.ProcessEnded += (id, status, ex) =>
            {
                lock (_completionSync)
                {
                    if (_discardCompletions)
                    {
                        if (_cancelledSessionToken == Solution.Instance.CancellationToken) return;
                        // 新方案会话可能先完成高优先级依赖，再运行本等待节点。
                        // 这些新通知必须保留，不能等到节点启动后才开放接收。
                        _tracker.Reset();
                        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        _discardCompletions = false;
                    }
                    _tracker.MarkCompleted(id, status, ex);
                }
            };
        }

        /// <summary>
        /// 每当有流程完成时调用此方法，检查是否所有依赖的流程都已完成
        /// </summary>
        /// <param name="id"></param>
        private void OnProcessCompleted(int id)
        {
            if (ParamForm is NodeParamFormWaitProcessComplete form)
            {
                if (form.Params is NodeParamWaitProcessComplete param)
                {
                    if (param.ProcessIDs.Contains(id))
                    {
                        if (_tracker.AreAllCompleted(param.ProcessIDs))
                        {
                            _tcs.TrySetResult(true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 节点运行
        /// </summary>
        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(startTime, NodeStatus.Unexecuted);
                return new NodeReturn(NodeRunFlag.StopRun);
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(startTime, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is NodeParamFormWaitProcessComplete form)
            {
                if (form.Params is NodeParamWaitProcessComplete param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);
                        lock (_completionSync)
                        {
                            if (_discardCompletions)
                            {
                                _tracker.Reset();
                                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                _discardCompletions = false;
                            }
                        }

                        // 判断流程是否不存在了
                        foreach (var id in param.ProcessIDs)
                        {
                            if (!Solution.Instance.AllProcesses.Exists(p => p.ID == id))
                            {
                                throw new Exception($"ID为{id}的流程已不存在！");
                            }
                        }

                        // 取消回调绑定本轮完成源，不能误取消下一轮等待。
                        TaskCompletionSource<bool> completion = _tcs;
                        try
                        {
                            using (token.Register(() => completion.TrySetCanceled()))
                                await completion.Task.ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                        }
                        finally
                        {
                            // 取消和正常完成都必须清空本轮状态，保证停止后可以重新运行。
                            lock (_completionSync)
                            {
                                _discardCompletions = token.IsCancellationRequested;
                                _cancelledSessionToken = Solution.Instance.CancellationToken;
                                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                _tracker.Reset();
                            }
                        }

                        startTime = DateTime.Now;
                        var time = SetRunResult(startTime, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return new NodeReturn(NodeRunFlag.ContinueRun);
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        SetRunResult(startTime, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        SetRunResult(startTime, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return new NodeReturn(NodeRunFlag.StopRun);
        }
    }
}
