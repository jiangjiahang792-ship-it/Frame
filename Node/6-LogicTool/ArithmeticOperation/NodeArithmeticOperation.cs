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
    public class NodeArithmeticOperation : NodeBase, IDynamicResultVariableProvider
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
                    LogHelper.AddLog(MsgLevel.Info, $"节点({ID}.{NodeName})运行成功！变量：{nodeResult.VariablesText}，{time} ms", true);

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

            double? value = measureResult.Success
                ? (double?)RoundOutputNumber(measureResult.Value)
                : null;
            Dictionary<string, double> variables = RoundOutputVariables(measureResult.Variables);

            return new NodeResultArithmeticOperation
            {
                IsOk = measureResult.Success,
                JudgeOk = measureResult.Success,
                Value = value,
                ValueText = measureResult.ValueText,
                ExpressionText = measureResult.ExpressionText,
                OperandCount = measureResult.OperandCount,
                OperationCount = measureResult.OperationCount,
                DefaultVariableName = measureResult.DefaultVariableName,
                Message = measureResult.Message,
                Variables = variables,
                VariablesText = BuildVariablesText(variables, decimalPlaces)
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
                JudgeOk = false,
                Value = null,
                ValueText = string.Empty,
                ExpressionText = string.Empty,
                OperandCount = 0,
                OperationCount = 0,
                DefaultVariableName = string.Empty,
                Message = message,
                Variables = new Dictionary<string, double>(),
                VariablesText = string.Empty
            };
        }

        /// <summary>
        /// 获取参数中声明的动态输出变量名，供下游订阅配置阶段使用。
        /// </summary>
        public IEnumerable<string> GetDynamicResultVariableNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NodeParamArithmeticOperation param = ParamForm == null ? null : ParamForm.Params as NodeParamArithmeticOperation;
            if (param != null && param.Rows != null)
            {
                foreach (ArithmeticOperationRow row in param.Rows)
                {
                    if (row == null || !row.Enabled)
                        continue;

                    string name = (row.OutputVariableName ?? row.ResultVariableName ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name);
                }
            }

            return names.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
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
                .Select(pair => pair.Key + "=" + ArithmeticOperationAlgorithm.FormatNumber(pair.Value, NodeParamArithmeticOperation.OutputDecimalPlaces)));
        }

        /// <summary>
        /// 将四则运算对外发布的数值统一舍入到三位小数，内部计算仍保留原始精度。
        /// </summary>
        private static double RoundOutputNumber(double value)
        {
            return Math.Round(value, NodeParamArithmeticOperation.OutputDecimalPlaces, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 将四则运算动态变量表统一舍入到三位小数，保证下游订阅值和显示文本一致。
        /// </summary>
        private static Dictionary<string, double> RoundOutputVariables(Dictionary<string, double> variables)
        {
            var roundedVariables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (variables == null)
                return roundedVariables;

            foreach (KeyValuePair<string, double> pair in variables)
                roundedVariables[pair.Key] = RoundOutputNumber(pair.Value);

            return roundedVariables;
        }
    }
}
