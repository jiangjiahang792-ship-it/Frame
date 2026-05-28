using System;
using System.Collections.Generic;
using System.Linq;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 动态结果变量接口，用于让节点结果在固定属性之外发布可订阅变量。
    /// </summary>
    public interface IDynamicResultVariables
    {
        /// <summary>
        /// 获取当前结果中已经生成的动态变量名称。
        /// </summary>
        IEnumerable<string> GetDynamicVariableNames();

        /// <summary>
        /// 尝试读取指定动态变量的值。
        /// </summary>
        bool TryGetDynamicVariable(string variableName, out object value);
    }

    /// <summary>
    /// 动态结果变量名称提供接口，用于参数配置阶段预先暴露将要输出的变量名。
    /// </summary>
    public interface IDynamicResultVariableProvider
    {
        /// <summary>
        /// 获取节点配置中声明的动态结果变量名称。
        /// </summary>
        IEnumerable<string> GetDynamicResultVariableNames();
    }

    /// <summary>
    /// 动态结果变量类型提供接口，用于让参数界面按真实值类型选择默认条件操作。
    /// </summary>
    public interface IDynamicResultVariableTypeProvider
    {
        /// <summary>
        /// 尝试获取指定动态变量的值类型。
        /// </summary>
        bool TryGetDynamicResultVariableType(string variableName, out Type valueType);
    }

    /// <summary>
    /// 节点订阅依赖提供接口，用于让图运行器等待参数中引用的上游结果节点。
    /// </summary>
    public interface INodeSubscriptionDependencyProvider
    {
        /// <summary>
        /// 获取当前节点参数中订阅的源节点 ID 集合。
        /// </summary>
        IEnumerable<int> GetSubscriptionDependencyNodeIds();
    }

    /// <summary>
    /// 动态结果变量路径解析工具。
    /// </summary>
    public static class DynamicResultVariableResolver
    {
        /// <summary>
        /// 动态变量在属性路径中的前缀。
        /// </summary>
        public const string PathPrefix = "$variable:";

        /// <summary>
        /// 动态变量在普通订阅下拉框中的显示前缀。
        /// </summary>
        public const string DisplayPrefix = "变量.";

        /// <summary>
        /// 将变量名转换为内部属性路径。
        /// </summary>
        public static string ToPropertyPath(string variableName)
        {
            return PathPrefix + NormalizeName(variableName);
        }

        /// <summary>
        /// 将变量名转换为订阅下拉框显示文本。
        /// </summary>
        public static string ToDisplayName(string variableName)
        {
            return DisplayPrefix + NormalizeName(variableName);
        }

        /// <summary>
        /// 从节点配置和最近一次结果中收集动态变量名。
        /// </summary>
        public static List<string> GetVariableNames(NodeBase node)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (node == null)
                return new List<string>();

            IDynamicResultVariableProvider provider = node as IDynamicResultVariableProvider;
            if (provider != null)
                AddNames(names, provider.GetDynamicResultVariableNames());

            IDynamicResultVariables resultVariables = node.Result as IDynamicResultVariables;
            if (resultVariables != null)
                AddNames(names, resultVariables.GetDynamicVariableNames());

            return names.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 获取动态变量的值类型；未知时按数字值处理。
        /// </summary>
        public static Type GetVariableValueType(NodeBase node, string variableName)
        {
            Type valueType;
            IDynamicResultVariableTypeProvider nodeTypeProvider = node as IDynamicResultVariableTypeProvider;
            if (nodeTypeProvider != null &&
                nodeTypeProvider.TryGetDynamicResultVariableType(variableName, out valueType) &&
                valueType != null)
            {
                return valueType;
            }

            IDynamicResultVariableTypeProvider resultTypeProvider = node == null
                ? null
                : node.Result as IDynamicResultVariableTypeProvider;
            if (resultTypeProvider != null &&
                resultTypeProvider.TryGetDynamicResultVariableType(variableName, out valueType) &&
                valueType != null)
            {
                return valueType;
            }

            return typeof(double);
        }

        /// <summary>
        /// 判断属性路径是否指向动态变量。
        /// </summary>
        public static bool IsDynamicVariablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(DisplayPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从路径或显示文本中提取变量名。
        /// </summary>
        public static string ExtractVariableName(string pathOrDisplayName)
        {
            string text = NormalizeName(pathOrDisplayName);
            if (text.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase))
                return text.Substring(PathPrefix.Length).Trim();
            if (text.StartsWith(DisplayPrefix, StringComparison.OrdinalIgnoreCase))
                return text.Substring(DisplayPrefix.Length).Trim();

            return text;
        }

        /// <summary>
        /// 尝试从结果对象中读取动态变量。
        /// </summary>
        public static bool TryGetValue(object result, string pathOrDisplayName, out object value)
        {
            value = null;
            IDynamicResultVariables dynamicVariables = result as IDynamicResultVariables;
            if (dynamicVariables == null || string.IsNullOrWhiteSpace(pathOrDisplayName))
                return false;

            string variableName = ExtractVariableName(pathOrDisplayName);
            return dynamicVariables.TryGetDynamicVariable(variableName, out value);
        }

        /// <summary>
        /// 校验动态变量名称是否适合持久化和订阅。
        /// </summary>
        public static bool IsValidVariableName(string variableName, out string message)
        {
            message = string.Empty;
            string name = NormalizeName(variableName);
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "变量名不能为空。";
                return false;
            }

            if (name.IndexOfAny(new[] { '.', ':', ';', '\t', '\r', '\n' }) >= 0)
            {
                message = $"变量名“{name}”不能包含 . : ; 或换行制表符。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 批量加入变量名。
        /// </summary>
        private static void AddNames(HashSet<string> names, IEnumerable<string> sourceNames)
        {
            if (sourceNames == null)
                return;

            foreach (string name in sourceNames)
            {
                string normalized = NormalizeName(name);
                if (!string.IsNullOrWhiteSpace(normalized))
                    names.Add(normalized);
            }
        }

        /// <summary>
        /// 清理变量名首尾空白。
        /// </summary>
        private static string NormalizeName(string variableName)
        {
            return variableName == null ? string.Empty : variableName.Trim();
        }
    }
}
