using System.Collections.Generic;
using System.ComponentModel;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算节点运行结果。
    /// </summary>
    public class NodeResultArithmeticOperation : INodeResult
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
    }
}
