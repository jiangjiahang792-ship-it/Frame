using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._4_Measurement.Common;

namespace TDJS_Vision.Node._7_ResultProcessing.ResultSend
{
    /// <summary>复用输出目录发现四则运算、检测、测量和条件结果，并严格读取本轮结果。</summary>
    public static class ResultSendSourceReader
    {
        /// <summary>缓存反射属性，避免逐帧重复扫描类型。</summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> Properties =
            new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();

        /// <summary>判断可通过协议发送的基础结果类型。</summary>
        public static bool IsScalar(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(bool) || type == typeof(string) || type == typeof(decimal) ||
                type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(uint) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(byte) || type == typeof(sbyte);
        }

        /// <summary>在配置阶段发现标量、动态变量、检测项和集合成员，不要求先执行节点。</summary>
        public static List<ResultSendSource> GetSources(NodeBase node)
        {
            var sources = new List<ResultSendSource>();
            if (node?.Result == null) return sources;
            var outputs = SubscriptionPortCatalog.GetOutputs(node, SubscriptionInputContract.AnyVisible(), true, null);
            foreach (var output in outputs)
            {
                var source = new ResultSendSource { NodeId = node.ID, Path = output.PropertyPath,
                    DisplayName = node.ID + "." + node.NodeName + " → " + output.DisplayName };
                if (IsScalar(output.ValueType))
                {
                    source.Boolean = output.ValueType == typeof(bool) || output.ValueType == typeof(bool?);
                    sources.Add(source);
                }
                else if (output.ValueType == typeof(AlgorithmResult))
                {
                    var names = GetConfiguredAiItemNames(node);
                    var algorithm = ReadProperty(node.Result, output.PropertyPath) as AlgorithmResult;
                    if (algorithm?.DetectResults != null)
                        foreach (var name in algorithm.DetectResults.Keys)
                            if (!string.IsNullOrWhiteSpace(name)) names.Add(DetectItemLanguage.NormalizeName(name));
                    // 配置尚未载入时保留模板入口，用户可以填写检测项的中文名称键。
                    if (names.Count == 0) names.Add(string.Empty);
                    foreach (var name in names.Where(name => name != null).OrderBy(name => name, StringComparer.Ordinal))
                    {
                        foreach (var kind in new[] { ResultSendSourceKind.AiValues, ResultSendSourceKind.AiFlags, ResultSendSourceKind.AiJudgment })
                        {
                            var ai = source.Copy();
                            ai.ItemName = name;
                            ai.Kind = kind;
                            ai.Multiple = kind != ResultSendSourceKind.AiJudgment;
                            ai.Boolean = kind != ResultSendSourceKind.AiValues;
                            ai.DisplayName += " / " + (name.Length == 0 ? "指定检测项" : DetectItemLanguage.GetDisplayName(name)) + " / " +
                                (kind == ResultSendSourceKind.AiValues ? "明细检测值" : kind == ResultSendSourceKind.AiFlags ? "明细判定" : "检测项判定");
                            sources.Add(ai);
                        }
                    }
                }
                else
                {
                    Type element = GetElementType(output.ValueType);
                    if (element == null) continue;
                    if (IsScalar(element))
                    {
                        source.Multiple = true;
                        source.Boolean = element == typeof(bool);
                        sources.Add(source);
                        continue;
                    }
                    foreach (var member in GetProperties(element).Values.Where(property => IsScalar(property.PropertyType)))
                    {
                        var itemSource = source.Copy();
                        itemSource.Kind = ResultSendSourceKind.Items;
                        itemSource.Multiple = true;
                        itemSource.Member = member.Name;
                        itemSource.Boolean = member.PropertyType == typeof(bool);
                        var rootOutput = outputs.FirstOrDefault(candidate => candidate.PropertyPath == member.Name);
                        var display = member.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>();
                        itemSource.DisplayName += " / " + (display?.DisplayName ?? rootOutput?.DisplayName ?? TranslateMember(member.Name));
                        sources.Add(itemSource);
                    }
                }
            }
            return sources;
        }

        /// <summary>直接读取方案中的检测项定义，包含未启用项，不等待模型加载或首次运行。</summary>
        private static HashSet<string> GetConfiguredAiItemNames(NodeBase node)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var parameter = node.ParamForm?.Params as NodeParamTDAI;
            if (parameter == null)
            {
                // 兼容仅提供运行时目录的扩展节点；标准AI节点以自己的方案配置为准，避免同名节点串项。
                List<DetectItemInfo> cached;
                if (TDAI.DetectItemMap.TryGetValue(node.NodeName, out cached)) AddAiItemNames(names, cached);
                return names;
            }

            var configurations = new HashSet<string>(StringComparer.Ordinal);
            if (parameter.IsFixed)
                configurations.Add(string.IsNullOrWhiteSpace(parameter.DetectItemName1) ? parameter.CurDetectItemName : parameter.DetectItemName1);
            else
            {
                // 通信还没有选中当前配置时，仍能订阅默认模板及所有已配置的切换项。
                configurations.Add(parameter.DetectItemName2);
                configurations.Add(parameter.CurDetectItemName);
                if (parameter.TDAICommuntionParams != null)
                    foreach (var mapping in parameter.TDAICommuntionParams)
                        if (mapping != null) configurations.Add(mapping.DetectionName);
            }

            var definitions = Solution.Instance.DetectItemDic;
            if (definitions == null) return names;
            foreach (var configuration in configurations)
            {
                List<DetectItemInfo> items;
                if (!string.IsNullOrWhiteSpace(configuration) && definitions.TryGetValue(configuration.Trim(), out items))
                    AddAiItemNames(names, items);
            }
            return names;
        }

        /// <summary>按稳定键合并全部检测项，启用状态仅控制检测执行，不限制订阅目录。</summary>
        private static void AddAiItemNames(HashSet<string> names, IEnumerable<DetectItemInfo> items)
        {
            if (items == null) return;
            foreach (var item in items)
                if (item != null && !string.IsNullOrWhiteSpace(item.Name)) names.Add(DetectItemLanguage.NormalizeName(item.Name));
        }

        /// <summary>常见目标字段的中文名称；算法扩展字段保留其原标识。</summary>
        private static string TranslateMember(string name)
        {
            switch (name)
            {
                case "TargetIndex": return "目标序号";
                case "IsOk": return "检测成功";
                case "JudgeOk": return "判定OK";
                case "ErrorMessage": return "错误信息";
                case "AlgorithmMs": return "算法耗时";
                default: return name;
            }
        }

        /// <summary>获取数组或泛型枚举的元素类型。</summary>
        private static Type GetElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            var enumerable = type.GetInterfaces().Concat(new[] { type })
                .FirstOrDefault(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable?.GetGenericArguments()[0];
        }

        /// <summary>获取类型的可读属性缓存。</summary>
        private static Dictionary<string, PropertyInfo> GetProperties(Type type) => Properties.GetOrAdd(type,
            value => value.GetProperties().Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .GroupBy(property => property.Name).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));

        /// <summary>仅允许读取实际存在的属性，不执行脚本或表达式。</summary>
        private static object ReadProperty(object value, string name)
        {
            if (value == null) throw new InvalidOperationException("订阅结果为空。");
            PropertyInfo property;
            if (name == null || !GetProperties(value.GetType()).TryGetValue(name, out property))
                throw new InvalidOperationException("订阅属性不存在：" + name);
            return property.GetValue(value);
        }

        /// <summary>读取来源并冻结为独立列表；生产发送要求本轮成功，预览允许读取最近结果。</summary>
        public static List<object> Read(NodeBase owner, ResultSendSource source, bool requireCurrentRun, IReadOnlyDictionary<int, NodeBase> upstream = null)
        {
            if (owner?.Process == null || source == null) throw new InvalidOperationException("订阅配置不完整。");
            NodeBase node;
            if (upstream != null) upstream.TryGetValue(source.NodeId, out node);
            else node = owner.Process.GetUpstreamNodes(owner).FirstOrDefault(item => item.ID == source.NodeId);
            if (node == null || !node.Active) throw new InvalidOperationException("订阅源已删除、禁用或不再是上游节点。");
            if (requireCurrentRun && !node.HasSuccessfulResultForRun(owner.Process.CurrentRunId))
                throw new InvalidOperationException("上游节点“" + node.NodeName + "”本轮尚未成功产生结果。");
            object value;
            if (DynamicResultVariableResolver.IsDynamicVariablePath(source.Path))
            {
                if (!DynamicResultVariableResolver.TryGetValue(node.Result, source.Path, out value))
                    throw new InvalidOperationException("动态变量本轮未生成：" + source.DisplayName);
            }
            else value = ReadProperty(node.Result, source.Path);
            // 空结果交由转换层按写入类型补默认值，仍保留上游本轮成功检查。
            if (value == null) return new List<object>();
            if (source.Kind == ResultSendSourceKind.AiValues || source.Kind == ResultSendSourceKind.AiFlags || source.Kind == ResultSendSourceKind.AiJudgment)
            {
                var algorithm = value as AlgorithmResult;
                List<SingleDetectResult> items;
                if (algorithm == null) throw new InvalidOperationException("订阅结果已经不是AI检测结果。");
                if (string.IsNullOrWhiteSpace(source.ItemName)) throw new InvalidOperationException("请选择AI检测项。");
                if (algorithm.DetectResults == null) return new List<object>();
                if (!algorithm.DetectResults.TryGetValue(source.ItemName, out items))
                {
                    // 兼容旧方案中的中文名称与现有稳定键，界面翻译不改变订阅含义。
                    string key = DetectItemLanguage.NormalizeName(source.ItemName);
                    items = algorithm.DetectResults.FirstOrDefault(pair => DetectItemLanguage.NormalizeName(pair.Key) == key).Value;
                }
                if (items == null || items.Count == 0) return new List<object>();
                if (source.Kind == ResultSendSourceKind.AiJudgment)
                {
                    var detected = items.Where(item => item != null).ToList();
                    return detected.Count == 0 ? new List<object>() : new List<object> { detected.All(item => item.IsOk) };
                }
                return items.Select(item => item == null ? null : source.Kind == ResultSendSourceKind.AiValues ?
                    (string.IsNullOrWhiteSpace(item.Value) ? null : (object)item.Value) : item.IsOk).ToList();
            }
            if (source.Kind == ResultSendSourceKind.Items)
            {
                var enumerable = value as IEnumerable;
                if (enumerable == null) throw new InvalidOperationException("订阅结果已经不是集合。");
                var values = new List<object>();
                foreach (var item in enumerable)
                {
                    var measurement = item as IMultiTargetMeasurementItem;
                    // 未测得的目标保留原位置，以所选类型的默认值发送；判定和错误字段仍读取实际值。
                    if (measurement != null && !measurement.IsOk && source.Member != "IsOk" && source.Member != "ErrorMessage" && source.Member != "TargetIndex")
                        values.Add(null);
                    else values.Add(item == null ? null : ReadProperty(item, source.Member));
                }
                return values;
            }
            if (value is string || IsScalar(value.GetType())) return new List<object> { value };
            var collection = value as IEnumerable;
            if (collection == null) throw new InvalidOperationException("订阅结果不是可发送的基础值。");
            return collection.Cast<object>().ToList();
        }
    }
}
