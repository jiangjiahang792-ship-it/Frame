using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 根据订阅调用方声明的值类型筛选节点结果属性，避免旧订阅名称落到不兼容的结果上。
    /// </summary>
    public static class SubscriptionValueTypeFilter
    {
        /// <summary>
        /// 获取具有显示名称且与期望值类型兼容的可读属性。
        /// </summary>
        /// <param name="resultType">节点运行结果类型。</param>
        /// <param name="expectedValueType">调用方期望的订阅值类型；为空时不过滤类型。</param>
        /// <returns>按结果类型原始声明顺序排列的兼容属性。</returns>
        public static IReadOnlyList<PropertyInfo> GetDisplayProperties(Type resultType, Type expectedValueType)
        {
            if (resultType == null)
                throw new ArgumentNullException("resultType");

            return resultType
                .GetProperties()
                .Where(property => property.CanRead)
                .Where(property => property.GetCustomAttribute<DisplayNameAttribute>() != null)
                .Where(property => IsCompatible(property.PropertyType, expectedValueType))
                .ToList();
        }

        /// <summary>
        /// 判断结果属性类型能否赋值给订阅调用方期望的值类型。
        /// </summary>
        /// <param name="valueType">结果属性实际类型。</param>
        /// <param name="expectedValueType">调用方期望类型；为空时接受任意类型。</param>
        /// <returns>类型兼容时返回 <see langword="true"/>。</returns>
        public static bool IsCompatible(Type valueType, Type expectedValueType)
        {
            if (expectedValueType == null)
                return true;
            if (valueType == null)
                return false;

            return SubscriptionTypeCompatibility.CanAssign(
                valueType,
                expectedValueType,
                NumericConversionMode.SafeWidening);
        }
    }
}
