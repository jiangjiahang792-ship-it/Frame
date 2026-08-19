using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 由包含非托管资源的特殊节点结果实现，用于覆盖默认的图像属性释放规则。
    /// </summary>
    public interface INodeResultResourceOwner
    {
        /// <summary>
        /// 释放当前结果明确拥有的资源；实现必须支持重复调用。
        /// </summary>
        void ReleaseResources();
    }

    /// <summary>
    /// 表示对一份输出图像资源的临时使用权，释放租约后资源才允许最终回收。
    /// </summary>
    public interface IImageResourceLease : IDisposable
    {
        /// <summary>
        /// 当前租约保护的输出图像；租约释放后返回空。
        /// </summary>
        OutputImage Image { get; }
    }

    /// <summary>
    /// 统一释放节点结果资源，兼容现有大量结果类型并为特殊结果保留可插拔接口。
    /// </summary>
    public static class NodeResultResourceManager
    {
        /// <summary>
        /// 缓存每种结果类型中直接公开的输出图像属性，避免每轮替换结果时重复反射扫描。
        /// </summary>
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> OutputImagePropertyCache =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// 释放节点结果拥有的资源；同一个输出图像被多个属性引用时只调用一次释放。
        /// </summary>
        /// <param name="result">待释放的节点结果。</param>
        public static void Release(INodeResult result)
        {
            if (result == null)
                return;

            if (result is INodeResultResourceOwner resourceOwner)
            {
                resourceOwner.ReleaseResources();
                return;
            }

            PropertyInfo[] imageProperties = OutputImagePropertyCache.GetOrAdd(
                result.GetType(),
                FindOutputImageProperties);
            HashSet<OutputImage> releasedImages = new HashSet<OutputImage>(ReferenceComparer<OutputImage>.Instance);
            foreach (PropertyInfo property in imageProperties)
            {
                try
                {
                    OutputImage outputImage = property.GetValue(result, null) as OutputImage;
                    if (outputImage != null && releasedImages.Add(outputImage))
                        outputImage.Dispose();
                }
                catch
                {
                    // 单个兼容属性读取失败不能阻断节点替换、方案关闭或其余资源释放。
                }
            }
        }

        /// <summary>
        /// 查找结果类型上不带索引参数的公开输出图像属性。
        /// </summary>
        /// <param name="resultType">节点结果运行时类型。</param>
        /// <returns>可读取的输出图像属性。</returns>
        private static PropertyInfo[] FindOutputImageProperties(Type resultType)
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            foreach (PropertyInfo property in resultType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.CanRead &&
                    property.GetIndexParameters().Length == 0 &&
                    typeof(OutputImage).IsAssignableFrom(property.PropertyType))
                {
                    properties.Add(property);
                }
            }

            return properties.ToArray();
        }

        /// <summary>
        /// 使用对象引用而不是重载相等运算符进行资源去重。
        /// </summary>
        /// <typeparam name="T">引用对象类型。</typeparam>
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            /// <summary>
            /// 当前引用比较器单例。
            /// </summary>
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            /// <summary>
            /// 判断两个对象是否为同一个引用。
            /// </summary>
            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// 获取不受对象重载影响的引用哈希值。
            /// </summary>
            public int GetHashCode(T obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
