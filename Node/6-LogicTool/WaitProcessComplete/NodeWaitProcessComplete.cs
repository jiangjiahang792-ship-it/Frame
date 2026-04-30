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
        private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
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
                _tracker.MarkCompleted(id, status, ex); // 转发
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

                        // 判断流程是否不存在了
                        foreach (var id in param.ProcessIDs)
                        {
                            if (!Solution.Instance.AllProcesses.Exists(p => p.ID == id))
                            {
                                throw new Exception($"ID为{id}的流程已不存在！");
                            }
                        }

                        // 真正的“即时唤醒”等待
                        await _tcs.Task;

                        // 重置 TaskCompletionSource 以便下次运行
                        _tcs = new TaskCompletionSource<bool>();
                        _tracker.Reset();

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
