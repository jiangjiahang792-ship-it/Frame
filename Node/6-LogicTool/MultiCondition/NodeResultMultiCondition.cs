using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Linq;

namespace TDJS_Vision.Node._6_LogicTool.MultiCondition
{
    public class NodeResultMultiCondition : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        /// <summary>单项布尔输出前缀，与节点的总条件结果区分。</summary>
        public const string ItemResultPrefix = "单项结果：";

        /// <summary>只发布本轮启用且名称唯一的条件，避免重名时误读另一行。</summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            return (Details ?? new List<NodeConditionEvaluation>())
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Name))
                .GroupBy(d => d.Name, StringComparer.Ordinal)
                .Where(g => g.Count() == 1 && g.First().IsEnabled && !g.First().SourceDisabled)
                .Select(g => ItemResultPrefix + g.Key);
        }

        /// <summary>按条件名称读取单项判定，不将禁用条件的兼容True作为检测结果输出。</summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            string name = DynamicResultVariableResolver.ExtractVariableName(variableName);
            if (name == null || !name.StartsWith(ItemResultPrefix, StringComparison.Ordinal))
                return false;
            string conditionName = name.Substring(ItemResultPrefix.Length);
            var matches = (Details ?? new List<NodeConditionEvaluation>())
                .Where(d => d != null && d.Name == conditionName).Take(2).ToList();
            if (matches.Count != 1 || !matches[0].IsEnabled || matches[0].SourceDisabled)
                return false;
            value = matches[0].IsMatched;
            return true;
        }

        /// <summary>声明单项输出为布尔类型，让布尔订阅选择器可以筛选。</summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = typeof(bool);
            return GetDynamicVariableNames().Contains(DynamicResultVariableResolver.ExtractVariableName(variableName));
        }

        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 多条件最终判断结果。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("条件结果")]
        public bool ConditionResult { get; set; }

        /// <summary>
        /// 多条件每一行的运行明细。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("条件明细")]
        public List<NodeConditionEvaluation> Details { get; set; } = new List<NodeConditionEvaluation>();

        /// <summary>
        /// 多条件运行诊断文本，用于定位空值、未运行和动态变量缺失等问题。
        /// </summary>
        [SubscriptionOutput(Visibility = SubscriptionOutputVisibility.Hidden)]
        [DisplayName("诊断信息")]
        public string DiagnosticsText { get; set; }
    }
}
