using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TDJS_Vision.Node._6_LogicTool.CompositeModule
{
    /// <summary>
    /// 组合模块节点运行结果。
    /// </summary>
    public class NodeResultCompositeModule : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 组合模块节点自身运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 本次运行的模块名称。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("模块名称")]
        public string ModuleName { get; set; }

        /// <summary>
        /// 内部流程累计运行耗时。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("内部耗时")]
        public long InternalRunTime { get; set; }

        /// <summary>
        /// 内部快照节点数量。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("内部节点数")]
        public int InternalNodeCount { get; set; }

        /// <summary>
        /// 内部流程是否运行成功。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("内部是否成功")]
        public bool InternalSuccess { get; set; }

        /// <summary>
        /// 运行失败时记录的错误信息。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("信息")]
        public string Message { get; set; }

        /// <summary>
        /// 对外发布的组合模块输出变量。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("输出变量表")]
        public Dictionary<string, object> OutputValues { get; set; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 对外发布的组合模块输出变量类型。
        /// </summary>
        public Dictionary<string, CompositePortValueType> OutputValueTypes { get; set; } = new Dictionary<string, CompositePortValueType>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 输出变量文本摘要。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("输出变量文本")]
        public string OutputValuesText { get; set; }

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
            if (OutputValues == null)
                return new string[0];

            return OutputValues.Keys;
        }

        /// <summary>
        /// 尝试读取指定动态变量值。
        /// </summary>
        /// <param name="variableName">变量名。</param>
        /// <param name="value">变量值。</param>
        /// <returns>读取成功时返回 true。</returns>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (OutputValues == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            if (OutputValues.TryGetValue(name, out value))
                return true;

            foreach (KeyValuePair<string, object> pair in OutputValues)
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
        /// <returns>找到变量类型时返回 true。</returns>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(object);
            if (OutputValueTypes == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            CompositePortValueType portValueType;
            if (!OutputValueTypes.TryGetValue(name, out portValueType))
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
