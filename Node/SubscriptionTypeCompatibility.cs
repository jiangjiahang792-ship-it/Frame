using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 统一判断订阅数据类别、CLR 类型兼容性并执行受控数值转换。
    /// </summary>
    public static class SubscriptionTypeCompatibility
    {
        /// <summary>框架支持的基础数值类型集合。</summary>
        private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };

        /// <summary>不会缩小表示范围的显式安全扩宽映射。</summary>
        private static readonly Dictionary<Type, HashSet<Type>> SafeWideningTargets =
            new Dictionary<Type, HashSet<Type>>
            {
                { typeof(byte), new HashSet<Type> { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(sbyte), new HashSet<Type> { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(short), new HashSet<Type> { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(ushort), new HashSet<Type> { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(int), new HashSet<Type> { typeof(long), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(uint), new HashSet<Type> { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) } },
                { typeof(long), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) } },
                { typeof(ulong), new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) } },
                { typeof(float), new HashSet<Type> { typeof(double) } }
            };

        /// <summary>
        /// 根据 CLR 类型解析统一数据类别。
        /// </summary>
        /// <param name="valueType">待分类 CLR 类型。</param>
        /// <returns>统一数据类别。</returns>
        public static SubscriptionDataCategory ResolveCategory(Type valueType)
        {
            Type type = NormalizeType(valueType);
            if (type == null)
                return SubscriptionDataCategory.Unknown;
            if (type == typeof(bool))
                return SubscriptionDataCategory.Boolean;
            if (IsNumericType(type))
                return SubscriptionDataCategory.Number;
            if (type == typeof(string) || type == typeof(char))
                return SubscriptionDataCategory.Text;

            string name = type.Name;
            if (name == "OutputImage")
                return SubscriptionDataCategory.Image;
            if (name == "Point" || name == "PointF")
                return SubscriptionDataCategory.Point;
            if (name == "AlgorithmResult")
                return SubscriptionDataCategory.AlgorithmResult;
            if (name == "TemplateMatchPose")
                return SubscriptionDataCategory.Pose;
            if (name == "PositionCorrectionInfo")
                return SubscriptionDataCategory.PositionCorrection;
            if (name.IndexOf("Line", StringComparison.OrdinalIgnoreCase) >= 0)
                return SubscriptionDataCategory.Line;
            if (name.IndexOf("Ellipse", StringComparison.OrdinalIgnoreCase) >= 0)
                return SubscriptionDataCategory.Ellipse;
            if (name.IndexOf("Circle", StringComparison.OrdinalIgnoreCase) >= 0)
                return SubscriptionDataCategory.Circle;
            if (name.IndexOf("Rect", StringComparison.OrdinalIgnoreCase) >= 0)
                return SubscriptionDataCategory.Rectangle;

            Type elementType = TryGetCollectionElementType(type);
            if (elementType != null)
            {
                SubscriptionDataCategory elementCategory = ResolveCategory(elementType);
                if (elementCategory == SubscriptionDataCategory.Point)
                    return SubscriptionDataCategory.PointCollection;
                if (elementCategory == SubscriptionDataCategory.Pose)
                    return SubscriptionDataCategory.Pose;
                if (elementCategory == SubscriptionDataCategory.PositionCorrection)
                    return SubscriptionDataCategory.PositionCorrection;
                if (elementType == typeof(string))
                    return SubscriptionDataCategory.Text;
            }

            return SubscriptionDataCategory.StructuredObject;
        }

        /// <summary>
        /// 判断 CLR 类型是否属于框架支持的基础数值类型。
        /// </summary>
        /// <param name="valueType">待检查类型。</param>
        /// <returns>属于数值类型时返回 true。</returns>
        public static bool IsNumericType(Type valueType)
        {
            Type type = NormalizeType(valueType);
            return type != null && NumericTypes.Contains(type);
        }

        /// <summary>
        /// 判断输出类型能否赋给输入类型。
        /// </summary>
        /// <param name="sourceType">输出实际类型。</param>
        /// <param name="targetType">输入目标类型；为空表示接受任意类型。</param>
        /// <param name="numericConversionMode">数值转换方式。</param>
        /// <returns>兼容时返回 true。</returns>
        public static bool CanAssign(
            Type sourceType,
            Type targetType,
            NumericConversionMode numericConversionMode)
        {
            if (targetType == null)
                return true;
            if (sourceType == null)
                return false;

            Type source = NormalizeType(sourceType);
            Type target = NormalizeType(targetType);
            if (target == typeof(object) || target.IsAssignableFrom(source))
                return true;
            if (!IsNumericType(source) || !IsNumericType(target))
                return false;
            if (numericConversionMode == NumericConversionMode.Checked)
                return true;
            if (numericConversionMode != NumericConversionMode.SafeWidening)
                return false;

            HashSet<Type> targets;
            return SafeWideningTargets.TryGetValue(source, out targets) && targets.Contains(target);
        }

        /// <summary>
        /// 把订阅值转换为输入目标类型。
        /// </summary>
        /// <param name="value">订阅实际值。</param>
        /// <param name="targetType">输入目标类型。</param>
        /// <param name="numericConversionMode">数值转换方式。</param>
        /// <returns>转换后的值。</returns>
        public static object ConvertValue(
            object value,
            Type targetType,
            NumericConversionMode numericConversionMode)
        {
            if (targetType == null || targetType == typeof(object))
                return value;

            Type target = NormalizeType(targetType);
            if (value == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return null;
                return Activator.CreateInstance(target);
            }

            Type source = NormalizeType(value.GetType());
            if (target.IsAssignableFrom(source))
                return value;
            if (!CanAssign(source, target, numericConversionMode))
                throw BuildInvalidCastException(source, target, null);

            try
            {
                return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                if (ex is OverflowException || ex is FormatException || ex is InvalidCastException)
                    throw BuildInvalidCastException(source, target, ex);
                throw;
            }
        }

        /// <summary>
        /// 剥离可空类型包装。
        /// </summary>
        /// <param name="type">原始类型。</param>
        /// <returns>规范化后的类型。</returns>
        private static Type NormalizeType(Type type)
        {
            return type == null ? null : (Nullable.GetUnderlyingType(type) ?? type);
        }

        /// <summary>
        /// 尝试获取数组或泛型集合的元素类型。
        /// </summary>
        /// <param name="type">集合类型。</param>
        /// <returns>元素类型；不是集合时返回 null。</returns>
        private static Type TryGetCollectionElementType(Type type)
        {
            if (type == null || type == typeof(string))
                return null;
            if (type.IsArray)
                return type.GetElementType();
            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1 && typeof(IEnumerable).IsAssignableFrom(type))
                    return arguments[0];
            }
            return null;
        }

        /// <summary>
        /// 创建包含实际类型和目标类型的统一中文转换异常。
        /// </summary>
        /// <param name="sourceType">实际类型。</param>
        /// <param name="targetType">目标类型。</param>
        /// <param name="innerException">原始转换异常。</param>
        /// <returns>统一转换异常。</returns>
        private static InvalidCastException BuildInvalidCastException(
            Type sourceType,
            Type targetType,
            Exception innerException)
        {
            string message = string.Format(
                "订阅值类型不匹配或数值超出范围，实际类型为{0}，目标类型为{1}。",
                sourceType == null ? "未知" : sourceType.Name,
                targetType == null ? "未知" : targetType.Name);
            return innerException == null
                ? new InvalidCastException(message)
                : new InvalidCastException(message, innerException);
        }
    }
}
