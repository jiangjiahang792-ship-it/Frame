using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.SleepTool
{
    /// <summary>
    /// 延迟执行节点，用于在流程中按指定毫秒数等待。
    /// </summary>
    public class NodeSleepTool : NodeBase
    {
        /// <summary>
        /// 高精度等待最后阶段的自旋阈值，单位毫秒。
        /// </summary>
        private const double SpinWaitThresholdMilliseconds = 4.0;

        /// <summary>
        /// 分段休眠的最大时间，单位毫秒，用于兼顾取消响应和运行效率。
        /// </summary>
        private const int MaxSleepSliceMilliseconds = 50;

        /// <summary>
        /// 进入自旋等待前预留的安全时间，单位毫秒。
        /// </summary>
        private const int SleepReserveMilliseconds = 3;

        /// <summary>
        /// 初始化延迟执行节点。
        /// </summary>
        /// <param name="nodeId">节点编号。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="process">所属流程。</param>
        /// <param name="nodeType">节点类型。</param>
        public NodeSleepTool(int nodeId, string nodeName, Process process, NodeType nodeType) : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormSleepTool();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultSleepTool();
        }

        /// <summary>
        /// 节点运行。
        /// </summary>
        /// <param name="token">流程取消令牌。</param>
        /// <param name="showLog">是否输出节点运行日志。</param>
        /// <returns>节点运行返回值。</returns>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            Stopwatch runStopwatch = Stopwatch.StartNew();

            // 参数合法性校验
            if (!Active)
            {
                SetRunResult(runStopwatch, NodeStatus.Unexecuted);
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }
            if (ParamForm.Params == null)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                SetRunResult(runStopwatch, NodeStatus.Failed);
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            if (ParamForm is NodeParamFormSleepTool form)
            {
                if (form.Params is NodeParamSleepTool param)
                {
                    try
                    {
                        SetStatus(NodeStatus.Unexecuted, "*");
                        base.CheckTokenCancel(token);

                        // 使用高精度等待减少 Task.Delay 在线程池恢复时带来的毫秒级抖动。
                        ExecutePreciseSleep(param.Time, token);
                        runStopwatch.Stop();
                        var time = SetRunResult(runStopwatch, NodeStatus.Successful);
                        Result.RunTime = time;
                        if (showLog)
                            LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！({time} ms)", true);
                        return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
                    }
                    catch (OperationCanceledException)
                    {
                        LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                        runStopwatch.Stop();
                        SetRunResult(runStopwatch, NodeStatus.Unexecuted);
                        throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                        runStopwatch.Stop();
                        SetRunResult(runStopwatch, NodeStatus.Failed);
                        throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
                    }
                }
            }
            return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
        }

        /// <summary>
        /// 执行高精度等待，先分段休眠降低CPU占用，接近目标时间后短自旋贴近指定毫秒数。
        /// </summary>
        /// <param name="timeInMilliseconds">需要等待的时间，单位毫秒。</param>
        /// <param name="token">流程取消令牌。</param>
        private static void ExecutePreciseSleep(int timeInMilliseconds, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (timeInMilliseconds <= 0)
                return;

            long startTimestamp = Stopwatch.GetTimestamp();
            long targetTimestamp = startTimestamp + ConvertMillisecondsToTimestampTicks(timeInMilliseconds);
            while (true)
            {
                token.ThrowIfCancellationRequested();

                long remainingTicks = targetTimestamp - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0)
                    return;

                double remainingMilliseconds = remainingTicks * 1000.0 / Stopwatch.Frequency;
                if (remainingMilliseconds > SpinWaitThresholdMilliseconds)
                {
                    int sleepMilliseconds = Math.Min(
                        MaxSleepSliceMilliseconds,
                        Math.Max(1, (int)remainingMilliseconds - SleepReserveMilliseconds));
                    Thread.Sleep(sleepMilliseconds);
                    continue;
                }

                Thread.SpinWait(64);
            }
        }

        /// <summary>
        /// 将毫秒数转换为 Stopwatch 时间戳刻度。
        /// </summary>
        /// <param name="milliseconds">毫秒数。</param>
        /// <returns>对应的 Stopwatch 时间戳刻度。</returns>
        private static long ConvertMillisecondsToTimestampTicks(int milliseconds)
        {
            return (long)Math.Ceiling(milliseconds * Stopwatch.Frequency / 1000.0);
        }
    }
}
