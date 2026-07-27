using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace TDJS_Vision.Node
{
    /// <summary>
    /// 统一发现、缓存并筛选节点的静态结果属性和动态结果变量。
    /// </summary>
    public static class SubscriptionPortCatalog
    {
        /// <summary>按结果类型缓存静态输出描述，避免每帧重复反射。</summary>
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<SubscriptionOutputDescriptor>> StaticCache =
            new ConcurrentDictionary<Type, IReadOnlyList<SubscriptionOutputDescriptor>>();

        /// <summary>
        /// 获取指定结果类型已经明确公开的静态输出描述。
        /// </summary>
        /// <param name="resultType">节点结果类型。</param>
        /// <returns>按属性声明顺序排列且已缓存的只读描述集合。</returns>
        public static IReadOnlyList<SubscriptionOutputDescriptor> GetStaticOutputs(Type resultType)
        {
            if (resultType == null)
                throw new ArgumentNullException("resultType");

            return StaticCache.GetOrAdd(resultType, BuildStaticOutputs);
        }

        /// <summary>
        /// 根据输入契约获取节点当前可选择的输出结果。
        /// </summary>
        /// <param name="node">订阅来源节点。</param>
        /// <param name="inputContract">调用方输入契约。</param>
        /// <param name="includeAdvanced">是否展开高级结果。</param>
        /// <param name="selectedPath">旧方案或当前已经保存的结果路径。</param>
        /// <returns>兼容、可见并包含必要旧选择占位的输出集合。</returns>
        public static IReadOnlyList<SubscriptionOutputDescriptor> GetOutputs(
            NodeBase node,
            SubscriptionInputContract inputContract,
            bool includeAdvanced,
            string selectedPath)
        {
            var allOutputs = new List<SubscriptionOutputDescriptor>();
            if (node != null && node.Result != null)
            {
                foreach (SubscriptionOutputDescriptor descriptor in GetStaticOutputs(node.Result.GetType()))
                {
                    SubscriptionOutputDescriptor copy = Clone(descriptor);
                    copy.SourceNode = node;
                    allOutputs.Add(copy);
                }
            }

            if (node != null)
                allOutputs.AddRange(DynamicResultVariableResolver.GetDescriptors(node));

            SubscriptionInputContract contract = inputContract ?? SubscriptionInputContract.AnyVisible();
            var candidates = new List<SubscriptionOutputDescriptor>();
            foreach (SubscriptionOutputDescriptor output in allOutputs)
            {
                string reason;
                if (!contract.Accepts(output, out reason))
                    continue;

                bool isSelected = MatchesPath(output, selectedPath);
                bool shouldShow = output.Visibility == SubscriptionOutputVisibility.Core ||
                    (output.Visibility == SubscriptionOutputVisibility.Advanced &&
                        (includeAdvanced || ShouldAutoIncludeAdvanced(contract, output.Category))) ||
                    isSelected;
                if (!shouldShow)
                    continue;

                if (output.Visibility == SubscriptionOutputVisibility.Hidden)
                {
                    if (!isSelected)
                        continue;
                    output.IsLegacySelection = true;
                }

                AddDistinct(candidates, output);
            }

            if (!string.IsNullOrWhiteSpace(selectedPath) &&
                !allOutputs.Any(output => MatchesPath(output, selectedPath)))
            {
                candidates.Add(new SubscriptionOutputDescriptor
                {
                    SourceNode = node,
                    PropertyPath = selectedPath,
                    DisplayName = selectedPath,
                    ValueType = typeof(object),
                    Category = SubscriptionDataCategory.Unknown,
                    Multiplicity = SubscriptionValueMultiplicity.Single,
                    Visibility = SubscriptionOutputVisibility.Hidden,
                    IsMissing = true
                });
            }

            return candidates;
        }

        /// <summary>
        /// 清空静态输出缓存，供结构测试和运行时插件重新加载时使用。
        /// </summary>
        public static void ClearCache()
        {
            StaticCache.Clear();
        }

        /// <summary>
        /// 反射构建一个结果类型的静态输出描述。
        /// </summary>
        /// <param name="resultType">节点结果类型。</param>
        /// <returns>静态输出描述集合。</returns>
        private static IReadOnlyList<SubscriptionOutputDescriptor> BuildStaticOutputs(Type resultType)
        {
            return resultType
                .GetProperties()
                .Where(property => property.CanRead)
                .Select(property => new
                {
                    Property = property,
                    Display = property.GetCustomAttribute<DisplayNameAttribute>(),
                    Output = property.GetCustomAttribute<SubscriptionOutputAttribute>()
                })
                .Where(item => item.Display != null && item.Output != null)
                .OrderBy(item => item.Property.MetadataToken)
                .Select(item => new SubscriptionOutputDescriptor
                {
                    PropertyPath = item.Property.Name,
                    DisplayName = item.Display.DisplayName,
                    ValueType = item.Property.PropertyType,
                    Category = item.Output.Category == SubscriptionDataCategory.Unknown
                        ? SubscriptionTypeCompatibility.ResolveCategory(item.Property.PropertyType)
                        : item.Output.Category,
                    Multiplicity = item.Output.Multiplicity,
                    Visibility = ResolveVisibility(item.Output.Visibility, item.Output.Category == SubscriptionDataCategory.Unknown
                        ? SubscriptionTypeCompatibility.ResolveCategory(item.Property.PropertyType)
                        : item.Output.Category),
                    Property = item.Property
                })
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// 根据数据类别确定默认显示级别；复杂视觉和结构化对象默认归入高级结果。
        /// </summary>
        /// <param name="declaredVisibility">属性显式声明的显示级别。</param>
        /// <param name="category">已经解析的数据类别。</param>
        /// <returns>最终显示级别。</returns>
        private static SubscriptionOutputVisibility ResolveVisibility(
            SubscriptionOutputVisibility declaredVisibility,
            SubscriptionDataCategory category)
        {
            if (declaredVisibility != SubscriptionOutputVisibility.Core)
                return declaredVisibility;
            if (category == SubscriptionDataCategory.Boolean ||
                category == SubscriptionDataCategory.Number ||
                category == SubscriptionDataCategory.Text)
            {
                return SubscriptionOutputVisibility.Core;
            }

            return SubscriptionOutputVisibility.Advanced;
        }

        /// <summary>
        /// 判断明确的复杂类型输入是否应自动看到相同类别的高级结果。
        /// </summary>
        /// <param name="contract">输入契约。</param>
        /// <param name="category">输出数据类别。</param>
        /// <returns>应自动显示时返回 true。</returns>
        private static bool ShouldAutoIncludeAdvanced(
            SubscriptionInputContract contract,
            SubscriptionDataCategory category)
        {
            if (contract == null || contract.AcceptedCategories.Count == 0 ||
                !contract.AcceptedCategories.Contains(category))
                return false;

            return contract.AcceptedCategories.All(item =>
                item != SubscriptionDataCategory.Boolean &&
                item != SubscriptionDataCategory.Number &&
                item != SubscriptionDataCategory.Text &&
                item != SubscriptionDataCategory.Unknown);
        }

        /// <summary>
        /// 判断输出描述是否对应已经保存的属性路径或显示名称。
        /// </summary>
        /// <param name="output">输出描述。</param>
        /// <param name="path">保存路径或显示名称。</param>
        /// <returns>对应时返回 true。</returns>
        private static bool MatchesPath(SubscriptionOutputDescriptor output, string path)
        {
            if (output == null || string.IsNullOrWhiteSpace(path))
                return false;

            return string.Equals(output.PropertyPath, path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output.DisplayName, path, StringComparison.OrdinalIgnoreCase) ||
                (output.IsDynamic && string.Equals(
                    DynamicResultVariableResolver.ExtractVariableName(output.PropertyPath),
                    DynamicResultVariableResolver.ExtractVariableName(path),
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 按稳定路径把输出加入集合，避免静态和动态来源重复。
        /// </summary>
        /// <param name="outputs">目标集合。</param>
        /// <param name="output">待加入输出。</param>
        private static void AddDistinct(
            ICollection<SubscriptionOutputDescriptor> outputs,
            SubscriptionOutputDescriptor output)
        {
            if (outputs.Any(item => string.Equals(
                item.PropertyPath,
                output.PropertyPath,
                StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            outputs.Add(output);
        }

        /// <summary>
        /// 克隆缓存描述，避免为当前节点写入来源时污染全局缓存。
        /// </summary>
        /// <param name="source">缓存描述。</param>
        /// <returns>独立描述副本。</returns>
        private static SubscriptionOutputDescriptor Clone(SubscriptionOutputDescriptor source)
        {
            return new SubscriptionOutputDescriptor
            {
                SourceNode = source.SourceNode,
                PropertyPath = source.PropertyPath,
                DisplayName = source.DisplayName,
                ValueType = source.ValueType,
                Category = source.Category,
                Multiplicity = source.Multiplicity,
                Visibility = source.Visibility,
                IsDynamic = source.IsDynamic,
                IsLegacySelection = source.IsLegacySelection,
                IsMissing = source.IsMissing,
                Property = source.Property
            };
        }
    }
}
