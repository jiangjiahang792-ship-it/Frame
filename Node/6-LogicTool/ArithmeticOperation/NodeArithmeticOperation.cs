using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算逻辑节点。
    /// </summary>
    public class NodeArithmeticOperation : NodeBase
    {
        /// <summary>
        /// 创建四则运算节点。
        /// </summary>
        public NodeArithmeticOperation(int nodeId, string nodeName, Process process, NodeType nodeType)
            : base(nodeId, nodeName, process, nodeType)
        {
            ParamForm = new NodeParamFormArithmeticOperation();
            ParamForm.SetNodeBelong(this);
            Result = new NodeResultArithmeticOperation();
        }

        /// <summary>
        /// 运行四则运算节点。
        /// </summary>
        public override Task<NodeReturn> Run(CancellationToken token, bool showLog)
        {
            DateTime startTime = DateTime.Now;

            if (!Active)
            {
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                return Task.FromResult(new NodeReturn(NodeRunFlag.StopRun));
            }

            if (!(ParamForm.Params is NodeParamArithmeticOperation param))
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行参数未设置或保存！", true);
                Result = BuildFailureResult("运行参数未设置或保存！");
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行参数未设置或保存！");
            }

            try
            {
                SetStatus(NodeStatus.Unexecuted, "*");
                base.CheckTokenCancel(token);

                ArithmeticOperationMeasureResult measureResult = ArithmeticOperationAlgorithm.Execute(this, param);
                NodeResultArithmeticOperation nodeResult = BuildResult(measureResult, param.DecimalPlaces);
                int time = SetRunResult(startTime, NodeStatus.Successful);
                nodeResult.RunTime = time;
                Result = nodeResult;

                if (showLog)
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！结果：{nodeResult.ValueText}，{time} ms", true);

                return Task.FromResult(new NodeReturn(NodeRunFlag.ContinueRun));
            }
            catch (OperationCanceledException)
            {
                LogHelper.AddLog(MsgLevel.Warn, $"节点({ID}.{NodeName})运行取消！", true);
                int time = SetRunResult(startTime, NodeStatus.Unexecuted);
                Result.RunTime = time;
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Fatal, $"节点({ID}.{NodeName})运行失败！原因：{ex.Message}", true);
                Result = BuildFailureResult(ex.Message);
                int time = SetRunResult(startTime, NodeStatus.Failed);
                Result.RunTime = time;
                throw new Exception($"节点({ID}.{NodeName})运行失败，原因：{ex.Message}");
            }
        }

        /// <summary>
        /// 将算法结果转换为节点结果。
        /// </summary>
        internal static NodeResultArithmeticOperation BuildResult(
            ArithmeticOperationMeasureResult measureResult,
            int decimalPlaces)
        {
            if (measureResult == null)
                return BuildFailureResult("计算结果为空。");

            return new NodeResultArithmeticOperation
            {
                IsOk = measureResult.Success,
                Value = measureResult.Success ? (double?)measureResult.Value : null,
                ValueText = measureResult.ValueText,
                ExpressionText = measureResult.ExpressionText,
                OperandCount = measureResult.OperandCount,
                Message = measureResult.Message,
                Variables = measureResult.Variables ?? new Dictionary<string, double>(),
                VariablesText = BuildVariablesText(measureResult.Variables, decimalPlaces)
            };
        }

        /// <summary>
        /// 创建失败结果，避免下游订阅到上一次成功的旧值。
        /// </summary>
        internal static NodeResultArithmeticOperation BuildFailureResult(string message)
        {
            return new NodeResultArithmeticOperation
            {
                IsOk = false,
                Value = null,
                ValueText = string.Empty,
                ExpressionText = string.Empty,
                OperandCount = 0,
                Message = message,
                Variables = new Dictionary<string, double>(),
                VariablesText = string.Empty
            };
        }

        /// <summary>
        /// 构建变量表文本。
        /// </summary>
        private static string BuildVariablesText(Dictionary<string, double> variables, int decimalPlaces)
        {
            if (variables == null || variables.Count == 0)
                return string.Empty;

            return string.Join("; ", variables
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + "=" + ArithmeticOperationAlgorithm.FormatNumber(pair.Value, decimalPlaces)));
        }
    }
}
