using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._7_ResultProcessing.NamedImageSave
{
    /// <summary>基于统一订阅目录发现结果，并用稳定ID读取当前上游结果。</summary>
    public static class NamedImageSubscriptions
    {
        /// <summary>缓存每种结果类型的可读属性，避免逐帧反射扫描。</summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> Properties =
            new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

        /// <summary>获取可作为名称或输出图像的端口，包含尚未运行的PLC和共享变量动态结果。</summary>
        public static IEnumerable<SubscriptionOutputDescriptor> GetOutputs(NodeBase node)
        {
            // 图像端口在统一目录中属于高级输出，展开后仍严格筛选图像和可命名的基础值。
            return SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.AnyVisible(), true, null)
                .Where(port => port.ValueType == typeof(OutputImage) ||
                    (port.Multiplicity == SubscriptionValueMultiplicity.Single &&
                     (port.ValueType == typeof(string) || port.ValueType == typeof(char) ||
                      port.ValueType == typeof(bool) || NamedImagePathBuilder.IsNumber(port.ValueType))));
        }

        /// <summary>从一次遍历得到的上游映射中读取，运行期拒绝本轮失效值；预览允许上次成功结果。</summary>
        public static object Read(NodeBase owner, NamedImageSource source, IReadOnlyDictionary<int, NodeBase> upstream, bool requireCurrentRun)
        {
            NodeBase node;
            if (source == null || !upstream.TryGetValue(source.SourceNodeId, out node) || !node.Active)
                throw new InvalidOperationException("订阅源已删除、禁用或不再位于上游。");
            if (requireCurrentRun && owner.Process.CurrentRunId > 0 && !node.HasSuccessfulResultForRun(owner.Process.CurrentRunId))
                throw new InvalidOperationException("订阅源本轮尚未成功产生结果：" + node.ID + "." + node.NodeName);
            if (node.Result == null) throw new InvalidOperationException("订阅结果为空：" + node.NodeName);
            object value;
            if (DynamicResultVariableResolver.IsDynamicVariablePath(source.PropertyPath))
            {
                if (!DynamicResultVariableResolver.TryGetValue(node.Result, source.PropertyPath, out value))
                    throw new InvalidOperationException("订阅的动态值不存在：" + source.DisplayName);
            }
            else
            {
                var properties = Properties.GetOrAdd(node.Result.GetType(), type => type.GetProperties()
                    .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                    .GroupBy(property => property.Name).ToDictionary(group => group.Key, group => group.First()));
                PropertyInfo selectedProperty;
                if (source.PropertyPath == null || !properties.TryGetValue(source.PropertyPath, out selectedProperty))
                    throw new InvalidOperationException("订阅属性不存在：" + source.DisplayName);
                value = selectedProperty.GetValue(node.Result);
            }
            if (value == null) throw new InvalidOperationException("订阅结果为空：" + source.DisplayName);
            return value;
        }
    }
}
