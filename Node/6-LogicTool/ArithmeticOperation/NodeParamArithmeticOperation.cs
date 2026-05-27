using System.Collections.Generic;

namespace TDJS_Vision.Node._6_LogicTool.ArithmeticOperation
{
    /// <summary>
    /// 四则运算节点参数。
    /// </summary>
    public class NodeParamArithmeticOperation : INodeParam
    {
        /// <summary>
        /// 运算行集合，按界面顺序依次执行。
        /// </summary>
        public List<ArithmeticOperationRow> Rows { get; set; } = new List<ArithmeticOperationRow>();

        /// <summary>
        /// 输出文本保留的小数位数。
        /// </summary>
        public int DecimalPlaces { get; set; } = 3;
    }

    /// <summary>
    /// 四则运算的一行操作配置。
    /// </summary>
    public class ArithmeticOperationRow
    {
        /// <summary>
        /// 当前行是否参与计算。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 当前行与前面累计值之间的运算符；第一条启用行作为起始值。
        /// </summary>
        public ArithmeticOperator Operator { get; set; } = ArithmeticOperator.Add;

        /// <summary>
        /// 当前操作数来源模式。
        /// </summary>
        public ArithmeticOperandSourceMode SourceMode { get; set; } = ArithmeticOperandSourceMode.Constant;

        /// <summary>
        /// 常量文本，运行时按数值格式解析。
        /// </summary>
        public string ConstantText { get; set; } = "0";

        /// <summary>
        /// 订阅源节点ID，用于节点重命名后仍能定位。
        /// </summary>
        public int SourceNodeId { get; set; }

        /// <summary>
        /// 订阅源节点显示文本，作为兼容旧参数的备用定位。
        /// </summary>
        public string SourceNodeText { get; set; }

        /// <summary>
        /// 订阅结果属性路径。
        /// </summary>
        public string PropertyPath { get; set; }

        /// <summary>
        /// 订阅结果属性显示名称。
        /// </summary>
        public string PropertyDisplayName { get; set; }

        /// <summary>
        /// 订阅结果值类型名称，主要用于界面提示。
        /// </summary>
        public string ValueTypeName { get; set; }

        /// <summary>
        /// 引用的内部变量名称。
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// 当前行计算完成后发布的内部变量名称。
        /// </summary>
        public string ResultVariableName { get; set; }

        /// <summary>
        /// 行注释，便于后续维护者识别计算含义。
        /// </summary>
        public string Note { get; set; }
    }

    /// <summary>
    /// 操作数来源模式。
    /// </summary>
    public enum ArithmeticOperandSourceMode
    {
        /// <summary>
        /// 常量输入。
        /// </summary>
        Constant,

        /// <summary>
        /// 订阅上游节点结果。
        /// </summary>
        Subscription,

        /// <summary>
        /// 引用当前节点前面行生成的内部变量。
        /// </summary>
        Variable
    }

    /// <summary>
    /// 四则运算符。
    /// </summary>
    public enum ArithmeticOperator
    {
        /// <summary>
        /// 加法。
        /// </summary>
        Add,

        /// <summary>
        /// 减法。
        /// </summary>
        Subtract,

        /// <summary>
        /// 乘法。
        /// </summary>
        Multiply,

        /// <summary>
        /// 除法。
        /// </summary>
        Divide
    }
}
