using Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public class NodeMultiCondition : NodeBase, INodeSubscriptionDependencyProvider
    {
        public NodeMultiCondition(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormMultiCondition();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultMultiCondition();
        }

        /// <summary>
        /// 获取多条件参数中订阅的源节点 ID，供图运行器建立隐藏等待依赖。
        /// </summary>
        public IEnumerable<int> GetSubscriptionDependencyNodeIds()
        {
            HashSet<int> nodeIds = new HashSet<int>();
            NodeParamMultiCondition param = ParamForm == null ? null : ParamForm.Params as NodeParamMultiCondition;
            if (param == null || param.Conditions == null)
                return nodeIds;

            foreach (MultiConditionItem condition in param.Conditions)
            {
                if (condition == null || condition.SourceNodeId <= 0 || condition.SourceNodeId == ID)
                    continue;

                nodeIds.Add(condition.SourceNodeId);
            }

            return nodeIds;
        }

        public override async Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                int unexecutedTime = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = unexecutedTime;
                return new NodeReturn(NodeRunFlag.StopRun);
            }

            if (!(ParamForm.Params is NodeParamMultiCondition param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                await base.CheckTokenCancel(token);

                List<NodeConditionEvaluation> details;
                bool conditionResult = MultiConditionEvaluator.Evaluate(this, param, out details);

                NodeResultMultiCondition result = Result as NodeResultMultiCondition;
                if (result != null)
                {
                    result.ConditionResult = conditionResult;
                    result.Details = details;
                    result.DiagnosticsText = MultiConditionEvaluator.BuildDiagnosticsText(details);
                }

                int time = SetRunResult(startTime, NodeStatus.Successful);
                Result.RunTime = time;
                if (showLog)
                {
                    string branchText = conditionResult ? "True" : "False";
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功，分支：{branchText}！({time} ms)", true);
                }

                return new NodeReturn(
                    NodeRunFlag.ContinueRun,
                    conditionResult ? ProcessConnectionBranch.True : ProcessConnectionBranch.False);
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int canceledTime = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = canceledTime;
                throw new OperationCanceledException($"节点({ID}.{NodeName})运行取消！");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因:{ex.Message}", true);
                int failedTime = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = failedTime;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }
    }
}
