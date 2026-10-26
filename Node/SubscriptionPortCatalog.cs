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
            var allOutputs = GetDeclaredOutputs(node);

            SubscriptionInputContract contract = inputContract ?? SubscriptionInputContract.AnyVisible();
            var candidates = new List<SubscriptionOutputDescriptor>();
            foreach (SubscriptionOutputDescriptor output in allOutputs)
            {
                string reason;
                if (!contract.Accepts(output, out reason))
                    continue;

                bool isSelected = MatchesPath(output, selectedPath);
                if (output.Visibility == SubscriptionOutputVisibility.Hidden)
                {
                    if (!isSelected) continue;
                    output.IsLegacySelection = true;
                }
                // 兼容的高级结果也允许直接订阅；高级级别不再充当数据可用性的过滤条件。
                AddDistinct(candidates, output);
            }
            if (!string.IsNullOrWhiteSpace(selectedPath) &&
                !allOutputs.Any(output => MatchesPath(output, selectedPath)))
            {
                candidates.Add(new SubscriptionOutputDescriptor
                {
                    SourceNode = node, PropertyPath = selectedPath, DisplayName = selectedPath,
                    ValueType = typeof(object), Category = SubscriptionDataCategory.Unknown,
                    Visibility = SubscriptionOutputVisibility.Hidden, IsMissing = true
                });
            }
            return candidates;
        }

        /// <summary>获取输出声明的独立副本，供候选筛选和原因说明共享，避免两套规则不一致。</summary>
        private static List<SubscriptionOutputDescriptor> GetDeclaredOutputs(NodeBase node)
        {
            var allOutputs = new List<SubscriptionOutputDescriptor>();
            // 模块作用域输入只公开声明变量，避免把适配器运行状态误当作布尔输入。
            if (node != null && node.Result != null && node.Process?.ModuleInputSource?.SubscriptionNode.ID != node.ID)
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

            return allOutputs;
        }

        /// <summary>说明全部已声明输出的可用性；不创建模块内部窗体，也不改变订阅配置。</summary>
        public static string ExplainOutputs(NodeBase node, SubscriptionInputContract inputContract, string selectedPath)
        {
            if (node == null) return "请先选择有效的来源节点；节点不存在或不在当前可订阅范围时，无法读取输出。";
            var contract = inputContract ?? SubscriptionInputContract.AnyVisible();
            var outputs = GetDeclaredOutputs(node);
            var lines = new List<string>();
            if (node is _6_LogicTool.CompositeModule.NodeCompositeModule)
                lines.Add("组合模块仅公开已配置的输出端口。内部节点存在某个结果，并不代表模块已经导出该结果。");
            if (outputs.Count == 0) lines.Add("该节点尚未声明可订阅输出，请检查输出配置。");
            foreach (var output in outputs)
            {
                string reason;
                bool accepted = contract.Accepts(output, out reason);
                if (accepted && output.Visibility == SubscriptionOutputVisibility.Hidden)
                    reason = MatchesPath(output, selectedPath) ? "仅保留已有订阅，不支持新选。" : "内部或诊断结果，不对新订阅开放。";
                lines.Add(output.DisplayName + "：" + (string.IsNullOrEmpty(reason) ? "可订阅。" : reason));
            }
            if (!string.IsNullOrWhiteSpace(selectedPath) && !outputs.Any(output => MatchesPath(output, selectedPath)))
                lines.Add("原订阅“" + selectedPath + "”已不存在，请检查端口是否被删除或改名；不会自动替换为其他结果。");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>清空静态输出缓存。</summary>
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
