using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算执行器，负责把二元运算行转换成命名变量结果。
    /// </summary>
    internal static class ArithmeticOperationAlgorithm
    {
        /// <summary>
        /// 执行四则运算。
        /// </summary>
        public static ArithmeticOperationMeasureResult Execute(NodeBase currentNode, NodeParamArithmeticOperation param)
        {
            if (currentNode == null)
                throw new Exception("四则运算节点未绑定到流程节点！");
            if (currentNode.Process == null)
                throw new Exception("四则运算节点未绑定到流程！");
            if (param == null || param.Rows == null || param.Rows.Count == 0)
                throw new Exception("至少需要配置一行运算。");

            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var expressionParts = new List<string>();
            double lastValue = 0;
            string lastVariableName = string.Empty;
            int operationCount = 0;
            int outputDecimalPlaces = NodeParamArithmeticOperation.OutputDecimalPlaces;

            for (int index = 0; index < param.Rows.Count; index++)
            {
                ArithmeticOperationRow row = NormalizeRow(param.Rows[index], index);
                if (row == null || !row.Enabled)
                    continue;

                string variableName = (row.OutputVariableName ?? string.Empty).Trim();
                ValidateOutputVariableName(variableName, variables, index);

                double value1 = ResolveOperand(currentNode, row.Value1, variables, index, "值1");
                double value2 = ResolveOperand(currentNode, row.Value2, variables, index, "值2");
                double result = ApplyOperator(value1, value2, row.Operator, index);
                variables[variableName] = result;
                lastValue = result;
                lastVariableName = variableName;
                operationCount++;

                expressionParts.Add(BuildExpressionText(variableName, row, value1, value2, result, outputDecimalPlaces));
            }

            if (operationCount == 0)
                throw new Exception("没有启用的运算行。");

            string valueText = lastVariableName + "=" + FormatNumber(lastValue, outputDecimalPlaces);
            string expressionText = string.Join("; ", expressionParts);

            return new ArithmeticOperationMeasureResult
            {
                Success = true,
                Value = lastValue,
                ValueText = valueText,
                ExpressionText = expressionText,
                OperandCount = operationCount * 2,
                OperationCount = operationCount,
                DefaultVariableName = lastVariableName,
                Message = "计算成功",
                Variables = variables
            };
        }

        /// <summary>
        /// 判断类型是否可作为数值订阅源。
        /// </summary>
        public static bool IsSelectableNumericMemberType(Type type)
        {
            if (type == null)
                return false;

            Type valueType = Nullable.GetUnderlyingType(type) ?? type;
            if (valueType == typeof(string))
                return true;

            switch (Type.GetTypeCode(valueType))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 判断类型是否适合继续展开子属性。
        /// </summary>
        public static bool CanInspectMemberType(Type type)
        {
            if (type == null ||
                IsSelectableNumericMemberType(type) ||
                IsIgnoredMemberType(type) ||
                IsEnumerableMemberType(type))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取可读属性和字段。
        /// </summary>
        public static List<MemberInfo> GetReadableMembers(Type type)
        {
            var members = new List<MemberInfo>();
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

        /// <summary>
        /// 获取成员显示名称。
        /// </summary>
        public static string GetDisplayName(MemberInfo member)
        {
            DisplayNameAttribute displayName = member.GetCustomAttribute<DisplayNameAttribute>();
            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.DisplayName))
                return displayName.DisplayName;

            return member.Name;
        }

        /// <summary>
        /// 获取成员类型。
        /// </summary>
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

        /// <summary>
        /// 读取成员路径对应的对象值，支持动态变量路径。
        /// </summary>
        public static object GetMemberPathValue(object target, string memberPath)
        {
            if (target == null)
                return null;
            if (string.IsNullOrWhiteSpace(memberPath))
                throw new Exception("属性路径为空。");

            object dynamicValue;
            if (DynamicResultVariableResolver.TryGetValue(target, memberPath, out dynamicValue))
                return dynamicValue;

            object current = target;
            string[] segments = memberPath.Split('.');
            foreach (string segment in segments)
            {
                if (current == null)
                    return null;

                MemberInfo member = FindReadableMember(current.GetType(), segment);
                if (member == null)
                    throw new Exception($"结果类型“{current.GetType().Name}”中找不到属性或字段“{segment}”。");

                current = GetMemberValue(current, member);
            }

            return current;
        }

        /// <summary>
        /// 将任意支持的值转换为 double。
        /// </summary>
        public static double ConvertToDouble(object value, string label)
        {
            if (value == null)
                throw new Exception($"{label}为空，不能参与四则运算。");

            if (value is double)
                return EnsureFinite((double)value, label);
            if (value is float)
                return EnsureFinite((float)value, label);
            if (value is decimal)
                return EnsureFinite(Convert.ToDouble((decimal)value, CultureInfo.InvariantCulture), label);

            if (value is IConvertible)
            {
                try
                {
                    return EnsureFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture), label);
                }
                catch
                {
                }
            }

            double parsed;
            string text = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ||
                double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
            {
                return EnsureFinite(parsed, label);
            }

            throw new Exception($"{label}的值“{text}”不能转换为数值。");
        }

        /// <summary>
        /// 按小数位格式化数值。
        /// </summary>
        public static string FormatNumber(double value, int decimalPlaces)
        {
            int safePlaces = Math.Max(0, Math.Min(decimalPlaces, 12));
            return value.ToString("F" + safePlaces, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 获取操作符中文文本。
        /// </summary>
        public static string GetOperatorText(ArithmeticOperator arithmeticOperator)
        {
            switch (arithmeticOperator)
            {
                case ArithmeticOperator.Add:
                    return "加";
                case ArithmeticOperator.Subtract:
                    return "减";
                case ArithmeticOperator.Multiply:
                    return "乘";
                case ArithmeticOperator.Divide:
                    return "除";
                default:
                    return arithmeticOperator.ToString();
            }
        }

        /// <summary>
        /// 获取操作数来源中文文本。
        /// </summary>
        public static string GetSourceModeText(ArithmeticOperandSourceMode sourceMode)
        {
            switch (sourceMode)
            {
                case ArithmeticOperandSourceMode.Constant:
                    return "常量";
                case ArithmeticOperandSourceMode.Subscription:
                    return "订阅值";
                case ArithmeticOperandSourceMode.Variable:
                    return "内部变量";
                default:
                    return sourceMode.ToString();
            }
        }

        /// <summary>
        /// 创建操作数显示文本。
        /// </summary>
        public static string BuildOperandSummary(ArithmeticOperand operand)
        {
            if (operand == null)
                return string.Empty;

            switch (operand.SourceMode)
            {
                case ArithmeticOperandSourceMode.Constant:
                    return operand.ConstantText ?? string.Empty;
                case ArithmeticOperandSourceMode.Subscription:
                    return (operand.SourceNodeText ?? string.Empty) + "." + (operand.PropertyDisplayName ?? operand.PropertyPath ?? string.Empty);
                case ArithmeticOperandSourceMode.Variable:
                    return operand.VariableName ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 标准化旧版累计行配置。
        /// </summary>
        private static ArithmeticOperationRow NormalizeRow(ArithmeticOperationRow row, int index)
        {
            if (row == null)
                return null;

            if (row.Value1 == null)
                row.Value1 = new ArithmeticOperand();
            if (row.Value2 == null)
                row.Value2 = new ArithmeticOperand();

            // 旧版行只有一个操作数和输出变量时，自动迁移成 0 + 旧操作数。
            if (HasLegacyOperand(row) && string.IsNullOrWhiteSpace(row.OutputVariableName))
            {
                row.Value1 = new ArithmeticOperand
                {
                    SourceMode = ArithmeticOperandSourceMode.Constant,
                    ConstantText = "0"
                };
                row.Value2 = new ArithmeticOperand
                {
                    SourceMode = row.SourceMode,
                    ConstantText = string.IsNullOrWhiteSpace(row.ConstantText) ? "0" : row.ConstantText,
                    SourceNodeId = row.SourceNodeId,
                    SourceNodeText = row.SourceNodeText,
                    PropertyPath = row.PropertyPath,
                    PropertyDisplayName = row.PropertyDisplayName,
                    ValueTypeName = row.ValueTypeName,
                    VariableName = row.VariableName
                };
                row.Operator = ArithmeticOperator.Add;
                row.OutputVariableName = string.IsNullOrWhiteSpace(row.ResultVariableName)
                    ? "V" + (index + 1)
                    : row.ResultVariableName;
            }

            return row;
        }

        /// <summary>
        /// 判断行是否带有旧版累计模型字段。
        /// </summary>
        private static bool HasLegacyOperand(ArithmeticOperationRow row)
        {
            return row.SourceNodeId > 0 ||
                   !string.IsNullOrWhiteSpace(row.SourceNodeText) ||
                   !string.IsNullOrWhiteSpace(row.PropertyPath) ||
                   !string.IsNullOrWhiteSpace(row.VariableName) ||
                   !string.IsNullOrWhiteSpace(row.ResultVariableName) ||
                   !string.IsNullOrWhiteSpace(row.ConstantText);
        }

        /// <summary>
        /// 校验输出变量名。
        /// </summary>
        private static void ValidateOutputVariableName(
            string variableName,
            Dictionary<string, double> variables,
            int rowIndex)
        {
            string message;
            if (!DynamicResultVariableResolver.IsValidVariableName(variableName, out message))
                throw new Exception($"第{rowIndex + 1}行输出变量名无效：{message}");

            if (variables.ContainsKey(variableName))
                throw new Exception($"第{rowIndex + 1}行输出变量名“{variableName}”重复。");
        }

        /// <summary>
        /// 从操作数配置读取数值。
        /// </summary>
        private static double ResolveOperand(
            NodeBase currentNode,
            ArithmeticOperand operand,
            Dictionary<string, double> variables,
            int rowIndex,
            string operandLabel)
        {
            if (operand == null)
                throw new Exception($"第{rowIndex + 1}行{operandLabel}未配置。");

            switch (operand.SourceMode)
            {
                case ArithmeticOperandSourceMode.Constant:
                    return ConvertToDouble(operand.ConstantText, $"第{rowIndex + 1}行{operandLabel}常量");
                case ArithmeticOperandSourceMode.Subscription:
                    return ResolveSubscribedOperand(currentNode, operand, rowIndex, operandLabel);
                case ArithmeticOperandSourceMode.Variable:
                    return ResolveVariableOperand(operand, variables, rowIndex, operandLabel);
                default:
                    throw new Exception($"第{rowIndex + 1}行{operandLabel}不支持的来源模式：{operand.SourceMode}");
            }
        }

        /// <summary>
        /// 读取上游订阅操作数。
        /// </summary>
        private static double ResolveSubscribedOperand(
            NodeBase currentNode,
            ArithmeticOperand operand,
            int rowIndex,
            string operandLabel)
        {
            NodeBase sourceNode = FindSourceNode(currentNode, operand);
            if (!sourceNode.Active)
                throw new Exception($"第{rowIndex + 1}行{operandLabel}订阅的源节点“{GetNodeText(sourceNode)}”已禁用。");
            if (sourceNode.Result == null)
                throw new Exception($"第{rowIndex + 1}行{operandLabel}订阅的源节点“{GetNodeText(sourceNode)}”还没有运行结果。");
            if (currentNode.Process.IsRuning && !sourceNode.HasSuccessfulResultForRun(currentNode.Process.CurrentRunId))
                throw new Exception($"第{rowIndex + 1}行{operandLabel}订阅的源节点“{GetNodeText(sourceNode)}”本次流程未成功运行。");

            if (string.IsNullOrWhiteSpace(operand.PropertyPath))
                throw new Exception($"第{rowIndex + 1}行{operandLabel}没有选择订阅属性。");

            object value = GetMemberPathValue(sourceNode.Result, operand.PropertyPath);
            string label = string.IsNullOrWhiteSpace(operand.PropertyDisplayName)
                ? $"第{rowIndex + 1}行{operandLabel}订阅值"
                : $"第{rowIndex + 1}行{operandLabel}订阅值“{operand.PropertyDisplayName}”";
            return ConvertToDouble(value, label);
        }

        /// <summary>
        /// 读取内部变量操作数。
        /// </summary>
        private static double ResolveVariableOperand(
            ArithmeticOperand operand,
            Dictionary<string, double> variables,
            int rowIndex,
            string operandLabel)
        {
            string variableName = (operand.VariableName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(variableName))
                throw new Exception($"第{rowIndex + 1}行{operandLabel}没有选择内部变量。");
            if (!variables.ContainsKey(variableName))
                throw new Exception($"第{rowIndex + 1}行{operandLabel}引用的内部变量“{variableName}”尚未生成。");

            return variables[variableName];
        }

        /// <summary>
        /// 应用当前行运算符。
        /// </summary>
        private static double ApplyOperator(double value1, double value2, ArithmeticOperator arithmeticOperator, int rowIndex)
        {
            switch (arithmeticOperator)
            {
                case ArithmeticOperator.Add:
                    return EnsureFinite(value1 + value2, $"第{rowIndex + 1}行加法结果");
                case ArithmeticOperator.Subtract:
                    return EnsureFinite(value1 - value2, $"第{rowIndex + 1}行减法结果");
                case ArithmeticOperator.Multiply:
                    return EnsureFinite(value1 * value2, $"第{rowIndex + 1}行乘法结果");
                case ArithmeticOperator.Divide:
                    if (Math.Abs(value2) < 1e-12)
                        throw new DivideByZeroException($"第{rowIndex + 1}行除数为0。");
                    return EnsureFinite(value1 / value2, $"第{rowIndex + 1}行除法结果");
                default:
                    throw new Exception($"第{rowIndex + 1}行不支持的运算符：{arithmeticOperator}");
            }
        }

        /// <summary>
        /// 创建表达式显示文本。
        /// </summary>
        private static string BuildExpressionText(
            string variableName,
            ArithmeticOperationRow row,
            double value1,
            double value2,
            double result,
            int decimalPlaces)
        {
            return variableName + " = " +
                   BuildOperandSummary(row.Value1) + "(" + FormatNumber(value1, decimalPlaces) + ") " +
                   GetOperatorSymbol(row.Operator) + " " +
                   BuildOperandSummary(row.Value2) + "(" + FormatNumber(value2, decimalPlaces) + ") = " +
                   FormatNumber(result, decimalPlaces);
        }

        /// <summary>
        /// 查找订阅的源节点。
        /// </summary>
        private static NodeBase FindSourceNode(NodeBase currentNode, ArithmeticOperand operand)
        {
            List<NodeBase> upstreamNodes = currentNode.Process.GetUpstreamNodes(currentNode);
            foreach (NodeBase node in upstreamNodes)
            {
                if (operand.SourceNodeId > 0 && node.ID == operand.SourceNodeId)
                    return node;
            }

            if (!string.IsNullOrWhiteSpace(operand.SourceNodeText))
            {
                foreach (NodeBase node in upstreamNodes)
                {
                    if (string.Equals(GetNodeText(node), operand.SourceNodeText, StringComparison.OrdinalIgnoreCase))
                        return node;
                }
            }

            throw new Exception($"订阅的上游节点“{operand.SourceNodeText}”已不存在或未连接到当前节点。");
        }

        /// <summary>
        /// 获取成员值。
        /// </summary>
        private static object GetMemberValue(object target, MemberInfo member)
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

        /// <summary>
        /// 按成员名称查找可读成员。
        /// </summary>
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

        /// <summary>
        /// 判断是否忽略不适合订阅的重型类型。
        /// </summary>
        private static bool IsIgnoredMemberType(Type type)
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

        /// <summary>
        /// 判断是否为集合类型。
        /// </summary>
        private static bool IsEnumerableMemberType(Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// 获取节点显示文本。
        /// </summary>
        private static string GetNodeText(NodeBase node)
        {
            return node.ID + "." + node.NodeName;
        }

        /// <summary>
        /// 获取操作符符号。
        /// </summary>
        private static string GetOperatorSymbol(ArithmeticOperator arithmeticOperator)
        {
            switch (arithmeticOperator)
            {
                case ArithmeticOperator.Add:
                    return "+";
                case ArithmeticOperator.Subtract:
                    return "-";
                case ArithmeticOperator.Multiply:
                    return "*";
                case ArithmeticOperator.Divide:
                    return "/";
                default:
                    return "?";
            }
        }

        /// <summary>
        /// 确保结果不是 NaN 或无穷大。
        /// </summary>
        private static double EnsureFinite(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new Exception($"{label}不是有效数值。");

            return value;
        }
    }

    /// <summary>
    /// 四则运算算法结果。
    /// </summary>
    internal class ArithmeticOperationMeasureResult
    {
        /// <summary>
        /// 运算是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 默认输出数值，取最后一个启用运算行的结果。
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 格式化后的默认输出数值。
        /// </summary>
        public string ValueText { get; set; }

        /// <summary>
        /// 运算表达式摘要。
        /// </summary>
        public string ExpressionText { get; set; }

        /// <summary>
        /// 参与计算的操作数数量。
        /// </summary>
        public int OperandCount { get; set; }

        /// <summary>
        /// 实际启用的运算行数量。
        /// </summary>
        public int OperationCount { get; set; }

        /// <summary>
        /// 默认输出变量名称。
        /// </summary>
        public string DefaultVariableName { get; set; }

        /// <summary>
        /// 运行信息。
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 计算过程中发布的变量表。
        /// </summary>
        public Dictionary<string, double> Variables { get; set; } = new Dictionary<string, double>();
    }
}
