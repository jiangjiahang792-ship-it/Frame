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
        public string Name { get; set; }

        public string SourceNodeText { get; set; }

        public string PropertyPath { get; set; }

        public string PropertyDisplayName { get; set; }

        public string OperatorText { get; set; }

        public string ExpectedValue { get; set; }

        public string ActualValue { get; set; }

        public bool IsMatched { get; set; }
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

            for (int index = 0; index < param.Conditions.Count; index++)
            {
                MultiConditionItem condition = param.Conditions[index];
                string conditionName = GetConditionName(condition, index);
                NodeBase sourceNode = FindSourceNode(currentNode, condition);
                if (!sourceNode.Active)
                {
                    evaluations.Add(new NodeConditionEvaluation
                    {
                        Name = conditionName,
                        SourceNodeText = GetNodeText(sourceNode),
                        PropertyPath = condition.PropertyPath,
                        PropertyDisplayName = condition.PropertyDisplayName,
                        OperatorText = condition.Operator.ToString(),
                        ExpectedValue = BuildExpectedText(condition),
                        ActualValue = "源节点已禁用",
                        IsMatched = true
                    });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(condition.PropertyPath))
                    throw new Exception($"条件“{conditionName}”没有选择结果属性！");

                object actualValue = sourceNode.Result == null ||
                    (currentNode.Process.IsRuning &&
                    !sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId))
                    ? null
                    : MultiConditionReflection.GetMemberPathValue(sourceNode.Result, condition.PropertyPath);
                bool matched = CompareCondition(actualValue, condition);
                evaluations.Add(new NodeConditionEvaluation
                {
                    Name = conditionName,
                    SourceNodeText = GetNodeText(sourceNode),
                    PropertyPath = condition.PropertyPath,
                    PropertyDisplayName = condition.PropertyDisplayName,
                    OperatorText = condition.Operator.ToString(),
                    ExpectedValue = BuildExpectedText(condition),
                    ActualValue = FormatValue(actualValue),
                    IsMatched = matched
                });
            }

            if (param.MatchMode == MultiConditionMatchMode.Any)
            {
                foreach (NodeConditionEvaluation evaluation in evaluations)
                {
                    if (evaluation.IsMatched)
                        return true;
                }

                return false;
            }

            foreach (NodeConditionEvaluation evaluation in evaluations)
            {
                if (!evaluation.IsMatched)
                    return false;
            }

            return true;
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
            switch (condition.Operator)
            {
                case MultiConditionOperator.IsNull:
                    return actualValue == null;
                case MultiConditionOperator.IsNotNull:
                    return actualValue != null;
            }

            if (actualValue == null)
                return false;

            switch (condition.Operator)
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
                    throw new Exception($"不支持的条件操作符：{condition.Operator}");
            }
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
