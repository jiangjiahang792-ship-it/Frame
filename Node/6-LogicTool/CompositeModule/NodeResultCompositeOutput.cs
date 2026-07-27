using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合输出节点运行结果。
    /// </summary>
    public class NodeResultCompositeOutput : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 输出发布是否成功。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 运行信息。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("信息")]
        public string Message { get; set; }

        /// <summary>
        /// 输出变量值表。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("变量表")]
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 输出变量类型表。
        /// </summary>
        public Dictionary<string, CompositePortValueType> ValueTypes { get; set; } = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 输出变量文本摘要。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("变量表文本")]
        public string ValuesText { get; set; }

        /// <summary>
        /// 创建失败结果，避免下游读到旧值。
        /// </summary>
        /// <param name="message">失败信息。</param>
        /// <returns>失败结果。</returns>
        public static NodeResultCompositeOutput BuildFailure(string message)
        {
            return new NodeResultCompositeOutput
            {
                IsOk = false,
                Message = message,
                Values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                ValueTypes = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase),
                ValuesText = string.Empty
            };
        }

        /// <summary>
        /// 构建变量表文本。
        /// </summary>
        /// <param name="values">变量值表。</param>
        /// <returns>变量文本。</returns>
        public static string BuildValuesText(Dictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;

            return string.Join("; ", values
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + "=" + FormatValue(pair.Value)));
        }

        /// <summary>
        /// 获取当前结果中已经生成的动态变量名称。
        /// </summary>
        /// <returns>动态变量名称集合。</returns>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            if (Values == null)
                return new string[0];

            return Values.Keys;
        }

        /// <summary>
        /// 尝试读取指定动态变量的值。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="value">变量值。</param>
        /// <returns>读取成功时返回 true。</returns>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (Values == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            if (Values.TryGetValue(name, out value))
                return true;

            foreach (KeyValuePair<string, object> pair in Values)
            {
                if (!string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = pair.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试获取动态变量类型。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="valueType">值类型。</param>
        /// <returns>找到类型时返回 true。</returns>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            if (ValueTypes == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            CompositePortValueType portValueType;
            if (!ValueTypes.TryGetValue(name, out portValueType))
                return false;

            valueType = CompositePortValueHelper.GetRuntimeType(portValueType);
            return true;
        }

        /// <summary>
        /// 将变量值转成可读文本。
        /// </summary>
        /// <param name="value">变量值。</param>
        /// <returns>显示文本。</returns>
        private static string FormatValue(object value)
        {
            if (value == null)
                return "空";

            if (value is double)
                return ((double)value).ToString("0.###", CultureInfo.InvariantCulture);
            if (value is float)
                return ((float)value).ToString("0.###", CultureInfo.InvariantCulture);
            if (value is decimal)
                return ((decimal)value).ToString("0.###", CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.CurrentCulture);
        }
    }
}
