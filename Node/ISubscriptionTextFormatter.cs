namespace TDJS_Vision.Node
{
    /// <summary>由结果类型提供订阅值的显示格式，不改变下游数值计算的类型与精度。</summary>
    public interface ISubscriptionTextFormatter
    {
        /// <summary>按中文输出名或属性名格式化订阅值；不支持该输出时返回 false。</summary>
        bool TryFormatSubscriptionText(string outputName, object value, out string text);
    }
}
