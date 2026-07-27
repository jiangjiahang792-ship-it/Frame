using Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public class NodeParamMultiCondition : INodeParam
    {
        public MultiConditionMatchMode MatchMode { get; set; } = MultiConditionMatchMode.All;

        public List<MultiConditionItem> Conditions { get; set; } = new List<MultiConditionItem>();
    }

    public class MultiConditionItem
    {
        /// <summary>
        /// 当前条件是否参与检测。默认启用可兼容没有保存该字段的旧方案。
        /// </summary>
        public bool Enabled { get; set; } = true;

        public string Name { get; set; }

        public string Note { get; set; }

        public int SourceNodeId { get; set; }

        public string SourceNodeText { get; set; }

        public string PropertyPath { get; set; }

        public string PropertyDisplayName { get; set; }

        public string ValueTypeName { get; set; }

        public MultiConditionOperator Operator { get; set; } = MultiConditionOperator.Equals;

        public string Value1 { get; set; }

        public string Value2 { get; set; }

        /// <summary>
        /// 是否把当前条件加入运行参数表，便于在单图调试时快速修改上下限。
        /// </summary>
        public bool EnableRunParamAdjust { get; set; }
    }

    public enum MultiConditionMatchMode
    {
        All,
        Any
    }

    public enum MultiConditionOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        NotContains,
        IsTrue,
        IsFalse,
        Between,
        NotBetween,
        IsNull,
        IsNotNull
    }

    public class NodeConditionEvaluation
    {
        /// <summary>
        /// 当前条件本次是否参与检测；禁用条件仍保留明细位置，避免界面行索引错位。
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 条件显示名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 条件订阅的源节点文本。
        /// </summary>
        public string SourceNodeText { get; set; }

        /// <summary>
        /// 条件读取的结果属性路径。
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 条件读取的结果属性显示名称。
        /// </summary>
        public string PropertyDisplayName { get; set; }

        /// <summary>
        /// 条件操作符文本。
        /// </summary>
        public string OperatorText { get; set; }

        /// <summary>
        /// 条件期望值文本。
        /// </summary>
        public string ExpectedValue { get; set; }

        /// <summary>
        /// 条件本次读取到的实际值文本。
        /// </summary>
        public string ActualValue { get; set; }

        /// <summary>
        /// 实际值为空时的原因说明。
        /// </summary>
        public string NullReason { get; set; }

        /// <summary>
        /// 源节点本次运行状态摘要。
        /// </summary>
        public string SourceRunState { get; set; }

        /// <summary>
        /// 源节点是否在当前流程批次成功产生结果。
        /// </summary>
        public bool SourceHasCurrentRunResult { get; set; }

        /// <summary>
        /// 本条件的完整诊断信息。
        /// </summary>
        public string DiagnosticText { get; set; }

        /// <summary>
        /// 条件是否匹配。
        /// </summary>
        public bool IsMatched { get; set; }

        /// <summary>
        /// 本条件是否已把匹配结果回写到源结果的判定状态。
        /// </summary>
        public bool JudgeWriteBackApplied { get; set; }

        /// <summary>
        /// 判定状态回写说明，用于运行诊断和后续排查。
        /// </summary>
        public string JudgeWriteBackText { get; set; }
    }

    internal static class MultiConditionEvaluator
    {
        public static bool Evaluate(
            NodeBase currentNode,
            NodeParamMultiCondition param,
            out List<NodeConditionEvaluation> evaluations)
        {
            evaluations = new List<NodeConditionEvaluation>();
            if (currentNode == null)
                throw new Exception("多条件节点未绑定到流程节点！");
            if (currentNode.Process == null)
                throw new Exception("多条件节点未绑定到流程！");
            if (param == null || param.Conditions == null || param.Conditions.Count == 0)
                throw new Exception("至少需要配置一个条件！");

            int enabledConditionCount = 0;
            for (int index = 0; index < param.Conditions.Count; index++)
            {
                MultiConditionItem condition = param.Conditions[index];
                string conditionName = GetConditionName(condition, index);
                if (condition == null || !condition.Enabled)
                {
                    evaluations.Add(new NodeConditionEvaluation
                    {
                        IsEnabled = false,
                        Name = conditionName,
                        SourceNodeText = condition == null ? string.Empty : condition.SourceNodeText,
                        PropertyPath = condition == null ? string.Empty : condition.PropertyPath,
                        PropertyDisplayName = condition == null ? string.Empty : condition.PropertyDisplayName,
                        OperatorText = condition == null ? string.Empty : condition.Operator.ToString(),
                        ExpectedValue = condition == null ? string.Empty : BuildExpectedText(condition),
                        ActualValue = "已禁用",
                        DiagnosticText = "当前条件已禁用，不参与本次判断。",
                        IsMatched = true
                    });
                    continue;
                }

                enabledConditionCount++;
                NodeBase sourceNode = FindSourceNode(currentNode, condition);
                if (!sourceNode.Active)
                {
                    evaluations.Add(new NodeConditionEvaluation
                    {
                        IsEnabled = true,
                        Name = conditionName,
                        SourceNodeText = GetNodeText(sourceNode),
                        PropertyPath = condition.PropertyPath,
                        PropertyDisplayName = condition.PropertyDisplayName,
                        OperatorText = condition.Operator.ToString(),
                        ExpectedValue = BuildExpectedText(condition),
                        ActualValue = "源节点已禁用",
                        NullReason = string.Empty,
                        SourceRunState = BuildSourceRunState(currentNode, sourceNode),
                        SourceHasCurrentRunResult = sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId),
                        DiagnosticText = "源节点已禁用，按当前兼容逻辑视为已满足。",
                        IsMatched = true
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(condition.PropertyPath))
                    throw new Exception($"条件“{conditionName}”没有选择结果属性！");

                ConditionValueReadResult valueResult = ReadConditionValue(currentNode, sourceNode, condition);
                bool matched = CompareCondition(valueResult.Value, condition);
                bool judgeWriteBackApplied;
                string judgeWriteBackText;
                ApplyJudgeWriteBack(sourceNode, matched, out judgeWriteBackApplied, out judgeWriteBackText);
                NodeConditionEvaluation evaluation = new NodeConditionEvaluation
                {
                    IsEnabled = true,
                    Name = conditionName,
                    SourceNodeText = GetNodeText(sourceNode),
                    PropertyPath = condition.PropertyPath,
                    PropertyDisplayName = condition.PropertyDisplayName,
                    OperatorText = condition.Operator.ToString(),
                    ExpectedValue = BuildExpectedText(condition),
                    ActualValue = FormatValue(valueResult.Value),
                    NullReason = valueResult.NullReason,
                    SourceRunState = valueResult.SourceRunState,
                    SourceHasCurrentRunResult = valueResult.SourceHasCurrentRunResult,
                    DiagnosticText = valueResult.DiagnosticText,
                    IsMatched = matched,
                    JudgeWriteBackApplied = judgeWriteBackApplied,
                    JudgeWriteBackText = judgeWriteBackText
                };
                evaluations.Add(evaluation);
                LogNullConditionIfNeeded(currentNode, evaluation);
            }

            // 工艺约定：全部条件禁用表示当前没有需要拦截的检测项，因此总体判定为 OK。
            if (enabledConditionCount == 0)
                return true;

            if (param.MatchMode == MultiConditionMatchMode.Any)
            {
                foreach (NodeConditionEvaluation evaluation in evaluations)
                {
                    if (evaluation.IsEnabled && evaluation.IsMatched)
                        return true;
                }

                return false;
            }

            foreach (NodeConditionEvaluation evaluation in evaluations)
            {
                if (evaluation.IsEnabled && !evaluation.IsMatched)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 构建条件明细诊断文本，便于运行后直接查看每一行条件的取值来源。
        /// </summary>
        public static string BuildDiagnosticsText(List<NodeConditionEvaluation> evaluations)
        {
            if (evaluations == null || evaluations.Count == 0)
                return string.Empty;

            List<string> lines = new List<string>();
            foreach (NodeConditionEvaluation evaluation in evaluations)
            {
                if (evaluation == null)
                    continue;

                string stateText = !evaluation.IsEnabled
                    ? "已禁用"
                    : evaluation.IsMatched ? "通过" : "不通过";
                string valueText = string.IsNullOrEmpty(evaluation.ActualValue) ? "空" : evaluation.ActualValue;
                string reasonText = string.IsNullOrWhiteSpace(evaluation.NullReason)
                    ? string.Empty
                    : "，空值原因：" + evaluation.NullReason;
                string writeBackText = string.IsNullOrWhiteSpace(evaluation.JudgeWriteBackText)
                    ? string.Empty
                    : "，" + evaluation.JudgeWriteBackText;
                lines.Add(evaluation.Name + "：" + stateText + "，实际值=" + valueText + reasonText + writeBackText);
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 将当前条件的匹配结果回写到源结果的判定状态；多个条件命中同一结果时按与逻辑累积。
        /// </summary>
        private static void ApplyJudgeWriteBack(
            NodeBase sourceNode,
            bool matched,
            out bool applied,
            out string writeBackText)
        {
            applied = false;
            writeBackText = string.Empty;
            object result = sourceNode == null ? null : sourceNode.Result;
            if (result == null)
                return;

            IJudgmentResult judgmentResult = result as IJudgmentResult;
            if (judgmentResult != null)
            {
                judgmentResult.JudgeOk = judgmentResult.JudgeOk && matched;
                applied = true;
                writeBackText = "判定回写=" + (judgmentResult.JudgeOk ? "OK" : "NG");
                return;
            }

            bool currentValue;
            if (TryReadWritableBooleanProperty(result, "JudgeOk", out PropertyInfo property, out currentValue))
            {
                bool nextValue = currentValue && matched;
                property.SetValue(result, nextValue, null);
                applied = true;
                writeBackText = "判定回写=" + (nextValue ? "OK" : "NG");
            }
        }

        /// <summary>
        /// 查找可写布尔属性，便于兼容未来没有显式实现接口但暴露同名判定属性的结果。
        /// </summary>
        private static bool TryReadWritableBooleanProperty(
            object result,
            string propertyName,
            out PropertyInfo property,
            out bool value)
        {
            property = null;
            value = false;
            if (result == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            property = result.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null ||
                !property.CanRead ||
                !property.CanWrite ||
                property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (valueType != typeof(bool))
                return false;

            object rawValue = property.GetValue(result, null);
            value = rawValue == null || Convert.ToBoolean(rawValue);
            return true;
        }

        /// <summary>
        /// 读取条件值并生成诊断信息，优先支持动态变量路径。
        /// </summary>
        private static ConditionValueReadResult ReadConditionValue(
            NodeBase currentNode,
            NodeBase sourceNode,
            MultiConditionItem condition)
        {
            ConditionValueReadResult readResult = new ConditionValueReadResult();
            readResult.SourceRunState = BuildSourceRunState(currentNode, sourceNode);
            readResult.SourceHasCurrentRunResult = sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId);
            readResult.PropertyPath = condition.PropertyPath;
            readResult.PropertyDisplayName = condition.PropertyDisplayName;

            if (sourceNode.Result == null)
            {
                readResult.NullReason = "源节点没有运行结果对象。";
                readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
                return readResult;
            }

            if (currentNode.Process.IsRuning && !readResult.SourceHasCurrentRunResult)
            {
                readResult.NullReason = "源节点本次流程尚未成功产生结果。";
                readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
                return readResult;
            }

            if (DynamicResultVariableResolver.IsDynamicVariablePath(condition.PropertyPath))
                return ReadDynamicConditionValue(sourceNode, condition, readResult);

            object dynamicValue;
            if (DynamicResultVariableResolver.TryGetValue(sourceNode.Result, condition.PropertyPath, out dynamicValue))
            {
                readResult.Value = dynamicValue;
                if (dynamicValue == null)
                    readResult.NullReason = "动态变量值为空。";
                readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
                return readResult;
            }

            object value = MultiConditionReflection.GetMemberPathValue(sourceNode.Result, condition.PropertyPath);
            readResult.Value = value;
            if (value == null)
                readResult.NullReason = BuildMemberNullReason(sourceNode.Result, condition);
            readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
            return readResult;
        }

        /// <summary>
        /// 读取动态变量条件值，并区分“不支持动态变量”和“变量未生成”。
        /// </summary>
        private static ConditionValueReadResult ReadDynamicConditionValue(
            NodeBase sourceNode,
            MultiConditionItem condition,
            ConditionValueReadResult readResult)
        {
            string variableName = DynamicResultVariableResolver.ExtractVariableName(condition.PropertyPath);
            readResult.DynamicVariableName = variableName;
            IDynamicResultVariables dynamicVariables = sourceNode.Result as IDynamicResultVariables;
            if (dynamicVariables == null)
            {
                readResult.NullReason = "源节点结果不支持动态输出变量。";
                readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
                return readResult;
            }

            object value;
            if (!dynamicVariables.TryGetDynamicVariable(variableName, out value))
            {
                readResult.NullReason = "动态变量“" + variableName + "”不存在或本次未生成。";
                readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
                return readResult;
            }

            readResult.Value = value;
            if (value == null)
                readResult.NullReason = "动态变量“" + variableName + "”的值为空。";
            readResult.DiagnosticText = BuildDiagnosticText(sourceNode, readResult);
            return readResult;
        }

        /// <summary>
        /// 构建普通属性为空时的原因文本。
        /// </summary>
        private static string BuildMemberNullReason(object result, MultiConditionItem condition)
        {
            object isOkValue;
            string propertyText = string.IsNullOrWhiteSpace(condition.PropertyDisplayName)
                ? condition.PropertyPath
                : condition.PropertyDisplayName;

            if (MultiConditionReflection.TryGetMemberValue(result, "IsOk", out isOkValue) &&
                isOkValue is bool &&
                !(bool)isOkValue)
            {
                return "源结果 IsOk=False，属性“" + propertyText + "”为空属于当前结果模型的正常 NG 输出。";
            }

            if (MultiConditionReflection.TryGetMemberValue(result, "IsAllOk", out isOkValue) &&
                isOkValue is bool &&
                !(bool)isOkValue)
            {
                return "源结果 IsAllOk=False，属性“" + propertyText + "”为空属于当前结果模型的正常 NG 输出。";
            }

            return "属性“" + propertyText + "”读取结果为空。";
        }

        /// <summary>
        /// 构建源节点运行状态摘要。
        /// </summary>
        private static string BuildSourceRunState(NodeBase currentNode, NodeBase sourceNode)
        {
            if (currentNode == null || currentNode.Process == null || sourceNode == null)
                return string.Empty;

            return "当前批次=" + currentNode.Process.CurrentRunId +
                "，源结果批次=" + sourceNode.LastSuccessfulRunId +
                "，源状态=" + sourceNode.RuntimeStatus +
                "，本次成功=" + sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId);
        }

        /// <summary>
        /// 构建单条条件的完整诊断文本。
        /// </summary>
        private static string BuildDiagnosticText(NodeBase sourceNode, ConditionValueReadResult readResult)
        {
            string resultType = sourceNode == null || sourceNode.Result == null
                ? "null"
                : sourceNode.Result.GetType().Name;
            string sourceText = sourceNode == null ? "未知" : GetNodeText(sourceNode);
            string propertyText = string.IsNullOrWhiteSpace(readResult.PropertyDisplayName)
                ? readResult.PropertyPath
                : readResult.PropertyDisplayName;
            string dynamicText = string.IsNullOrWhiteSpace(readResult.DynamicVariableName)
                ? string.Empty
                : "，动态变量=" + readResult.DynamicVariableName;
            string reasonText = string.IsNullOrWhiteSpace(readResult.NullReason)
                ? string.Empty
                : "，空值原因=" + readResult.NullReason;

            return "源节点=" + sourceText +
                "，结果类型=" + resultType +
                "，属性=" + propertyText +
                "，路径=" + readResult.PropertyPath +
                dynamicText +
                "，" + readResult.SourceRunState +
                reasonText;
        }

        /// <summary>
        /// 空值条件写入日志，帮助定位多条件运行期订阅问题。
        /// </summary>
        private static void LogNullConditionIfNeeded(NodeBase currentNode, NodeConditionEvaluation evaluation)
        {
            if (currentNode == null ||
                evaluation == null ||
                evaluation.IsMatched ||
                !string.IsNullOrEmpty(evaluation.ActualValue) ||
                string.IsNullOrWhiteSpace(evaluation.NullReason))
            {
                return;
            }

            LogHelper.AddLog(
                MsgLevel.Warn,
                "节点(" + currentNode.ID + "." + currentNode.NodeName + ")多条件“" +
                evaluation.Name + "”实际值为空：" + evaluation.DiagnosticText,
                true);
        }

        private static NodeBase FindSourceNode(NodeBase currentNode, MultiConditionItem condition)
        {
            List<NodeBase> upstreamNodes = currentNode.Process.GetUpstreamNodes(currentNode);
            foreach (NodeBase node in upstreamNodes)
            {
                if (condition.SourceNodeId > 0 && node.ID == condition.SourceNodeId)
                    return node;
            }

            if (!string.IsNullOrWhiteSpace(condition.SourceNodeText))
            {
                foreach (NodeBase node in upstreamNodes)
                {
                    if (string.Equals(GetNodeText(node), condition.SourceNodeText, StringComparison.OrdinalIgnoreCase))
                        return node;
                }
            }

            throw new Exception($"条件“{condition.Name}”引用的上游节点已不存在或未连到当前节点！");
        }

        private static bool CompareCondition(object actualValue, MultiConditionItem condition)
        {
            MultiConditionOperator conditionOperator = NormalizeConditionOperator(actualValue, condition);
            switch (conditionOperator)
            {
                case MultiConditionOperator.IsNull:
                    return actualValue == null;
                case MultiConditionOperator.IsNotNull:
                    return actualValue != null;
            }

            if (actualValue == null)
                return false;

            switch (conditionOperator)
            {
                case MultiConditionOperator.IsTrue:
                    return ToBoolean(actualValue);
                case MultiConditionOperator.IsFalse:
                    return !ToBoolean(actualValue);
                case MultiConditionOperator.Equals:
                    return ValuesEqual(actualValue, condition.Value1);
                case MultiConditionOperator.NotEquals:
                    return !ValuesEqual(actualValue, condition.Value1);
                case MultiConditionOperator.Contains:
                    return FormatValue(actualValue).IndexOf(condition.Value1 ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                case MultiConditionOperator.NotContains:
                    return FormatValue(actualValue).IndexOf(condition.Value1 ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0;
                case MultiConditionOperator.GreaterThan:
                    return Compare(actualValue, condition.Value1) > 0;
                case MultiConditionOperator.GreaterThanOrEqual:
                    return Compare(actualValue, condition.Value1) >= 0;
                case MultiConditionOperator.LessThan:
                    return Compare(actualValue, condition.Value1) < 0;
                case MultiConditionOperator.LessThanOrEqual:
                    return Compare(actualValue, condition.Value1) <= 0;
                case MultiConditionOperator.Between:
                    return Compare(actualValue, condition.Value1) >= 0 && Compare(actualValue, condition.Value2) <= 0;
                case MultiConditionOperator.NotBetween:
                    return Compare(actualValue, condition.Value1) < 0 || Compare(actualValue, condition.Value2) > 0;
                default:
                    throw new Exception($"不支持的条件操作符：{conditionOperator}");
            }
        }

        /// <summary>
        /// 兼容早期配置或误选操作：数字条件填了值2时，文本包含按范围条件处理。
        /// </summary>
        private static MultiConditionOperator NormalizeConditionOperator(object actualValue, MultiConditionItem condition)
        {
            if (condition == null)
                return MultiConditionOperator.Equals;

            if ((condition.Operator != MultiConditionOperator.Contains &&
                 condition.Operator != MultiConditionOperator.NotContains) ||
                string.IsNullOrWhiteSpace(condition.Value2))
            {
                return condition.Operator;
            }

            decimal actualDecimal;
            decimal minValue;
            decimal maxValue;
            if (!TryToDecimal(actualValue, out actualDecimal) ||
                !TryParseDecimal(condition.Value1, out minValue) ||
                !TryParseDecimal(condition.Value2, out maxValue))
            {
                return condition.Operator;
            }

            return condition.Operator == MultiConditionOperator.Contains
                ? MultiConditionOperator.Between
                : MultiConditionOperator.NotBetween;
        }

        private static bool ValuesEqual(object actualValue, string expectedText)
        {
            if (actualValue == null)
                return string.IsNullOrEmpty(expectedText);

            bool actualBoolean;
            bool expectedBoolean;
            if (TryToBoolean(actualValue, out actualBoolean) &&
                TryParseBoolean(expectedText, out expectedBoolean))
            {
                return actualBoolean == expectedBoolean;
            }

            decimal actualDecimal;
            decimal expectedDecimal;
            if (TryToDecimal(actualValue, out actualDecimal) &&
                TryParseDecimal(expectedText, out expectedDecimal))
            {
                return actualDecimal == expectedDecimal;
            }

            DateTime actualDateTime;
            DateTime expectedDateTime;
            if (TryToDateTime(actualValue, out actualDateTime) &&
                TryParseDateTime(expectedText, out expectedDateTime))
            {
                return actualDateTime == expectedDateTime;
            }

            Type actualType = actualValue.GetType();
            if (actualType.IsEnum)
            {
                try
                {
                    object expectedEnum = Enum.Parse(actualType, expectedText, true);
                    return actualValue.Equals(expectedEnum);
                }
                catch
                {
                    return false;
                }
            }

            return string.Equals(FormatValue(actualValue), expectedText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static int Compare(object actualValue, string expectedText)
        {
            if (actualValue == null)
                throw new Exception("实际值为空，不能进行大小比较！");

            decimal actualDecimal;
            decimal expectedDecimal;
            if (TryToDecimal(actualValue, out actualDecimal) &&
                TryParseDecimal(expectedText, out expectedDecimal))
            {
                return actualDecimal.CompareTo(expectedDecimal);
            }

            DateTime actualDateTime;
            DateTime expectedDateTime;
            if (TryToDateTime(actualValue, out actualDateTime) &&
                TryParseDateTime(expectedText, out expectedDateTime))
            {
                return actualDateTime.CompareTo(expectedDateTime);
            }

            return string.Compare(FormatValue(actualValue), expectedText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ToBoolean(object value)
        {
            bool result;
            if (TryToBoolean(value, out result))
                return result;

            throw new Exception($"值“{FormatValue(value)}”不能转换为布尔条件！");
        }

        private static bool TryToBoolean(object value, out bool result)
        {
            result = false;
            if (value == null)
                return false;

            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            object isAllOkValue;
            if (MultiConditionReflection.TryGetMemberValue(value, "IsAllOk", out isAllOkValue) ||
                MultiConditionReflection.TryGetMemberValue(value, "IsOk", out isAllOkValue))
            {
                return TryToBoolean(isAllOkValue, out result);
            }

            decimal decimalValue;
            if (TryToDecimal(value, out decimalValue))
            {
                result = decimalValue != 0M;
                return true;
            }

            return TryParseBoolean(Convert.ToString(value, CultureInfo.CurrentCulture), out result);
        }

        private static bool TryParseBoolean(string text, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.Trim();
            if (bool.TryParse(normalized, out result))
                return true;

            switch (normalized.ToUpperInvariant())
            {
                case "1":
                case "OK":
                case "YES":
                case "Y":
                case "TRUE":
                    result = true;
                    return true;
                case "0":
                case "NG":
                case "NO":
                case "N":
                case "FALSE":
                    result = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryToDecimal(object value, out decimal result)
        {
            result = 0M;
            if (value == null)
                return false;

            if (value is IConvertible)
            {
                try
                {
                    result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                }
            }

            return TryParseDecimal(Convert.ToString(value, CultureInfo.CurrentCulture), out result);
        }

        private static bool TryParseDecimal(string text, out decimal result)
        {
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return true;

            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }

        private static bool TryToDateTime(object value, out DateTime result)
        {
            result = DateTime.MinValue;
            if (value is DateTime)
            {
                result = (DateTime)value;
                return true;
            }

            return TryParseDateTime(Convert.ToString(value, CultureInfo.CurrentCulture), out result);
        }

        private static bool TryParseDateTime(string text, out DateTime result)
        {
            return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out result) ||
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        private static string GetConditionName(MultiConditionItem condition, int index)
        {
            if (condition != null && !string.IsNullOrWhiteSpace(condition.Name))
                return condition.Name;

            return "条件" + (index + 1);
        }

        private static string GetNodeText(NodeBase node)
        {
            return node.ID + "." + node.NodeName;
        }

        private static string BuildExpectedText(MultiConditionItem condition)
        {
            if (condition.Operator == MultiConditionOperator.Between ||
                condition.Operator == MultiConditionOperator.NotBetween)
            {
                return (condition.Value1 ?? string.Empty) + " ~ " + (condition.Value2 ?? string.Empty);
            }

            return condition.Value1 ?? string.Empty;
        }

        private static string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            return Convert.ToString(value, CultureInfo.CurrentCulture);
        }
    }

    /// <summary>
    /// 条件值读取结果，集中携带实际值和运行期诊断信息。
    /// </summary>
    internal sealed class ConditionValueReadResult
    {
        /// <summary>
        /// 条件读取到的实际对象值。
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 实际值为空时的原因说明。
        /// </summary>
        public string NullReason { get; set; }

        /// <summary>
        /// 源节点本次运行状态摘要。
        /// </summary>
        public string SourceRunState { get; set; }

        /// <summary>
        /// 源节点是否在当前流程批次成功产生结果。
        /// </summary>
        public bool SourceHasCurrentRunResult { get; set; }

        /// <summary>
        /// 当前条件的结果属性路径。
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 当前条件的结果属性显示名称。
        /// </summary>
        public string PropertyDisplayName { get; set; }

        /// <summary>
        /// 动态变量名称，普通属性路径时为空。
        /// </summary>
        public string DynamicVariableName { get; set; }

        /// <summary>
        /// 完整诊断文本。
        /// </summary>
        public string DiagnosticText { get; set; }
    }

    internal static class MultiConditionReflection
    {
        public static List<MemberInfo> GetReadableMembers(Type type)
        {
            List<MemberInfo> members = new List<MemberInfo>();
            if (type == null)
                return members;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                members.Add(property);
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                members.Add(field);

            return members;
        }

        public static Type GetMemberType(MemberInfo member)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null)
                return property.PropertyType;

            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.FieldType;

            return typeof(object);
        }

        public static string GetDisplayName(MemberInfo member)
        {
            DisplayNameAttribute displayName = member.GetCustomAttribute<DisplayNameAttribute>();
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.DisplayName))
                return displayName.DisplayName;

            return member.Name;
        }

        public static object GetMemberValue(object target, MemberInfo member)
        {
            if (target == null || member == null)
                return null;

            PropertyInfo property = member as PropertyInfo;
            if (property != null)
                return property.GetValue(target, null);

            FieldInfo field = member as FieldInfo;
            if (field != null)
                return field.GetValue(target);

            return null;
        }

        public static bool TryGetMemberValue(object target, string memberName, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return false;

            MemberInfo member = FindReadableMember(target.GetType(), memberName);
            if (member == null)
                return false;

            value = GetMemberValue(target, member);
            return true;
        }

        public static object GetMemberPathValue(object target, string memberPath)
        {
            if (target == null)
                return null;
            if (string.IsNullOrWhiteSpace(memberPath))
                throw new Exception("属性路径为空！");

            object current = target;
            string[] segments = memberPath.Split('.');
            foreach (string segment in segments)
            {
                if (current == null)
                    return null;

                MemberInfo member = FindReadableMember(current.GetType(), segment);
                if (member == null)
                    throw new Exception($"结果类型“{current.GetType().Name}”中找不到属性或字段“{segment}”！");

                current = GetMemberValue(current, member);
            }

            return current;
        }

        public static bool IsSelectableMemberType(Type type)
        {
            if (type == null)
                return false;
            if (type == typeof(object))
                return true;

            Type valueType = Nullable.GetUnderlyingType(type) ?? type;
            return valueType.IsPrimitive ||
                valueType.IsEnum ||
                valueType == typeof(string) ||
                valueType == typeof(decimal) ||
                valueType == typeof(DateTime) ||
                valueType == typeof(TimeSpan) ||
                valueType == typeof(Guid);
        }

        public static bool CanInspectMemberType(Type type)
        {
            if (type == null ||
                IsSelectableMemberType(type) ||
                IsIgnoredMemberType(type) ||
                IsEnumerableMemberType(type))
            {
                return false;
            }

            return true;
        }

        public static bool IsIgnoredMemberType(Type type)
        {
            if (type == null)
                return true;
            if (type == typeof(Type) || typeof(Delegate).IsAssignableFrom(type))
                return true;

            string fullName = type.FullName ?? type.Name;
            return fullName == "System.Drawing.Image" ||
                fullName == "System.Drawing.Bitmap" ||
                fullName.IndexOf("OpenCvSharp.Mat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEnumerableMemberType(Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static MemberInfo FindReadableMember(Type type, string name)
        {
            foreach (MemberInfo member in GetReadableMembers(type))
            {
                if (member.Name == name)
                    return member;
            }

            foreach (MemberInfo member in GetReadableMembers(type))
            {
                if (string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
                    return member;
            }

            return null;
        }
    }
}
