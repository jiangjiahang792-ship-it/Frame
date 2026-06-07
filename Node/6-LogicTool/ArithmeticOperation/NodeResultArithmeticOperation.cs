using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算节点运行结果。
    /// </summary>
    public class NodeResultArithmeticOperation : INodeResult, IDynamicResultVariables, IJudgmentResult
    {
        /// <summary>
        /// 节点运行耗时。
        /// </summary>
        public int RunTime { get; set; }

        /// <summary>
        /// 当前计算是否成功。
        /// </summary>
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 后续多条件判定后的OK/NG状态，默认OK，绘制工具按该值选择颜色。
        /// </summary>
        [DisplayName("判定OK")]
        public bool JudgeOk { get; set; } = true;

        /// <summary>
        /// 最终计算结果。
        /// </summary>
        [DisplayName("计算结果")]
        public double? Value { get; set; }

        /// <summary>
        /// 按参数格式化后的结果文本。
        /// </summary>
        [DisplayName("结果文本")]
        public string ValueText { get; set; }

        /// <summary>
        /// 运算过程摘要。
        /// </summary>
        [DisplayName("表达式")]
        public string ExpressionText { get; set; }

        /// <summary>
        /// 实际参与计算的操作数数量。
        /// </summary>
        [DisplayName("操作数数量")]
        public int OperandCount { get; set; }

        /// <summary>
        /// 实际启用的运算行数量。
        /// </summary>
        [DisplayName("运算行数量")]
        public int OperationCount { get; set; }

        /// <summary>
        /// 默认输出变量名称，等于最后一个启用运算行输出变量。
        /// </summary>
        [DisplayName("默认变量名")]
        public string DefaultVariableName { get; set; }

        /// <summary>
        /// 运行信息或失败原因。
        /// </summary>
        [DisplayName("信息")]
        public string Message { get; set; }

        /// <summary>
        /// 当前节点内部生成的变量表。
        /// </summary>
        [DisplayName("变量表")]
        public Dictionary<string, double> Variables { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// 变量表文本，方便后续消息、日志或条件节点订阅。
        /// </summary>
        [DisplayName("变量表文本")]
        public string VariablesText { get; set; }

        /// <summary>
        /// 获取当前结果中已经生成的动态变量名称。
        /// </summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            if (Variables == null)
                return new List<string>();

            return Variables.Keys;
        }

        /// <summary>
        /// 尝试读取指定动态变量的值。
        /// </summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (Variables == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            string normalizedName = variableName.Trim();
            double doubleValue;
            if (Variables.TryGetValue(normalizedName, out doubleValue))
            {
                value = doubleValue;
                return true;
            }

            // 反序列化或旧结果可能丢失忽略大小写的 Dictionary 比较器，这里兜底按名称忽略大小写匹配。
            foreach (KeyValuePair<string, double> pair in Variables)
            {
                if (!string.Equals(pair.Key, normalizedName, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = pair.Value;
                return true;
            }

            return false;
        }
    }
}
