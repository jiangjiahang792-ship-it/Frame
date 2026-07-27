using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._4_Measurement.Common
{
    /// <summary>
    /// 表示一个带模板目标编号的测量值，供不同测量节点按目标身份组合使用。
    /// </summary>
    /// <typeparam name="TValue">测量值类型。</typeparam>
    public sealed class IndexedMeasurementValue<TValue>
    {
        /// <summary>
        /// 创建带目标编号的测量值。
        /// </summary>
        /// <param name="targetIndex">从一开始的模板目标编号。</param>
        /// <param name="isOk">当前目标是否测量成功。</param>
        /// <param name="value">当前目标测量值。</param>
        /// <param name="errorMessage">当前目标失败原因。</param>
        public IndexedMeasurementValue(int targetIndex, bool isOk, TValue value, string errorMessage)
            : this(targetIndex, isOk, value, errorMessage, null)
        {
        }

        /// <summary>
        /// 创建带目标编号和来源上下文的测量值。
        /// </summary>
        /// <param name="targetIndex">从一开始的模板目标编号。</param>
        /// <param name="isOk">当前目标是否测量成功。</param>
        /// <param name="value">当前目标测量值。</param>
        /// <param name="errorMessage">当前目标失败原因。</param>
        /// <param name="context">需要向组合结果传递的来源上下文。</param>
        public IndexedMeasurementValue(int targetIndex, bool isOk, TValue value, string errorMessage, object context)
        {
            TargetIndex = targetIndex;
            IsOk = isOk;
            Value = value;
            ErrorMessage = errorMessage ?? string.Empty;
            Context = context;
        }

        /// <summary>获取当前测量值对应的模板目标编号。</summary>
        public int TargetIndex { get; private set; }

        /// <summary>获取当前目标测量是否成功。</summary>
        public bool IsOk { get; private set; }

        /// <summary>获取当前目标测量值。</summary>
        public TValue Value { get; private set; }

        /// <summary>获取当前目标失败原因。</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>获取来源结果携带的上下文，例如位置修正信息。</summary>
        public object Context { get; private set; }
    }

    /// <summary>
    /// 表示两个来源按照同一个模板目标编号形成的测量值组合。
    /// </summary>
    /// <typeparam name="TFirst">第一输入值类型。</typeparam>
    /// <typeparam name="TSecond">第二输入值类型。</typeparam>
    public sealed class IndexedMeasurementPair<TFirst, TSecond>
    {
        /// <summary>
        /// 创建目标测量值组合。
        /// </summary>
        /// <param name="targetIndex">模板目标编号。</param>
        /// <param name="first">第一输入。</param>
        /// <param name="second">第二输入。</param>
        internal IndexedMeasurementPair(
            int targetIndex,
            IndexedMeasurementValue<TFirst> first,
            IndexedMeasurementValue<TSecond> second)
        {
            TargetIndex = targetIndex;
            First = first;
            Second = second;
            IsOk = first != null && second != null && first.IsOk && second.IsOk;
            ErrorMessage = BuildErrorMessage(first, second);
        }

        /// <summary>获取模板目标编号。</summary>
        public int TargetIndex { get; private set; }

        /// <summary>获取第一输入。</summary>
        public IndexedMeasurementValue<TFirst> First { get; private set; }

        /// <summary>获取第二输入。</summary>
        public IndexedMeasurementValue<TSecond> Second { get; private set; }

        /// <summary>获取两侧输入是否都成功。</summary>
        public bool IsOk { get; private set; }

        /// <summary>获取两侧输入合并后的失败原因。</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 合并两个输入的失败原因。
        /// </summary>
        private static string BuildErrorMessage(
            IndexedMeasurementValue<TFirst> first,
            IndexedMeasurementValue<TSecond> second)
        {
            string firstError = first == null || first.IsOk ? string.Empty : first.ErrorMessage;
            string secondError = second == null || second.IsOk ? string.Empty : second.ErrorMessage;
            if (string.IsNullOrWhiteSpace(firstError))
                return secondError ?? string.Empty;
            if (string.IsNullOrWhiteSpace(secondError))
                return firstError;
            return firstError + "；" + secondError;
        }
    }

    /// <summary>
    /// 按模板目标编号配对两个多目标测量集合，避免失败项或执行顺序改变目标对应关系。
    /// </summary>
    public static class MultiTargetMeasurementPairer
    {
        /// <summary>
        /// 按 <see cref="IndexedMeasurementValue{TValue}.TargetIndex"/> 配对两个集合。
        /// </summary>
        /// <typeparam name="TFirst">第一输入值类型。</typeparam>
        /// <typeparam name="TSecond">第二输入值类型。</typeparam>
        /// <param name="firstItems">第一输入集合。</param>
        /// <param name="secondItems">第二输入集合。</param>
        /// <param name="firstName">第一输入中文名称。</param>
        /// <param name="secondName">第二输入中文名称。</param>
        /// <returns>
        /// 按目标编号升序排列的配对集合；一侧只有一个结果时广播到另一侧全部目标，
        /// 双方均为多目标时对缺失编号创建失败占位。
        /// </returns>
        public static List<IndexedMeasurementPair<TFirst, TSecond>> PairByTargetIndex<TFirst, TSecond>(
            IReadOnlyList<IndexedMeasurementValue<TFirst>> firstItems,
            IReadOnlyList<IndexedMeasurementValue<TSecond>> secondItems,
            string firstName,
            string secondName)
        {
            string normalizedFirstName = string.IsNullOrWhiteSpace(firstName) ? "第一输入" : firstName;
            string normalizedSecondName = string.IsNullOrWhiteSpace(secondName) ? "第二输入" : secondName;
            Dictionary<int, IndexedMeasurementValue<TFirst>> firstMap = BuildMap(firstItems, normalizedFirstName);
            Dictionary<int, IndexedMeasurementValue<TSecond>> secondMap = BuildMap(secondItems, normalizedSecondName);
            bool broadcastFirst = firstMap.Count == 1 && secondMap.Count > 1;
            bool broadcastSecond = secondMap.Count == 1 && firstMap.Count > 1;
            IndexedMeasurementValue<TFirst> firstSingleton = broadcastFirst ? GetSingleValue(firstMap) : null;
            IndexedMeasurementValue<TSecond> secondSingleton = broadcastSecond ? GetSingleValue(secondMap) : null;
            SortedSet<int> indexes = BuildTargetIndexes(firstMap, secondMap, broadcastFirst, broadcastSecond);

            var pairs = new List<IndexedMeasurementPair<TFirst, TSecond>>(indexes.Count);
            foreach (int targetIndex in indexes)
            {
                IndexedMeasurementValue<TFirst> first;
                if (!firstMap.TryGetValue(targetIndex, out first))
                {
                    first = broadcastFirst
                        ? CopyForTarget(firstSingleton, targetIndex)
                        : CreateMissingValue<TFirst>(targetIndex, normalizedFirstName);
                }

                IndexedMeasurementValue<TSecond> second;
                if (!secondMap.TryGetValue(targetIndex, out second))
                {
                    second = broadcastSecond
                        ? CopyForTarget(secondSingleton, targetIndex)
                        : CreateMissingValue<TSecond>(targetIndex, normalizedSecondName);
                }

                pairs.Add(new IndexedMeasurementPair<TFirst, TSecond>(targetIndex, first, second));
            }

            return pairs;
        }

        /// <summary>
        /// 根据广播模式确定最终目标编号；广播时完全采用多目标一侧编号集合。
        /// </summary>
        private static SortedSet<int> BuildTargetIndexes<TFirst, TSecond>(
            Dictionary<int, IndexedMeasurementValue<TFirst>> firstMap,
            Dictionary<int, IndexedMeasurementValue<TSecond>> secondMap,
            bool broadcastFirst,
            bool broadcastSecond)
        {
            if (broadcastFirst)
                return new SortedSet<int>(secondMap.Keys);
            if (broadcastSecond)
                return new SortedSet<int>(firstMap.Keys);

            var indexes = new SortedSet<int>(firstMap.Keys);
            indexes.UnionWith(secondMap.Keys);
            return indexes;
        }

        /// <summary>
        /// 获取已经确认仅包含一项的字典值。
        /// </summary>
        private static IndexedMeasurementValue<TValue> GetSingleValue<TValue>(
            Dictionary<int, IndexedMeasurementValue<TValue>> map)
        {
            foreach (IndexedMeasurementValue<TValue> value in map.Values)
                return value;
            return null;
        }

        /// <summary>
        /// 将公共单结果复制到指定目标编号，保留原始值、状态、失败原因和来源上下文。
        /// </summary>
        private static IndexedMeasurementValue<TValue> CopyForTarget<TValue>(
            IndexedMeasurementValue<TValue> source,
            int targetIndex)
        {
            return new IndexedMeasurementValue<TValue>(
                targetIndex,
                source.IsOk,
                source.Value,
                source.ErrorMessage,
                source.Context);
        }

        /// <summary>
        /// 创建双方都为多目标时的缺失编号失败占位。
        /// </summary>
        private static IndexedMeasurementValue<TValue> CreateMissingValue<TValue>(
            int targetIndex,
            string sourceName)
        {
            return new IndexedMeasurementValue<TValue>(
                targetIndex,
                false,
                default(TValue),
                string.Format("{0}缺少目标{1}结果。", sourceName, targetIndex));
        }

        /// <summary>
        /// 将输入集合转换为目标编号字典，并拒绝无效或重复编号。
        /// </summary>
        private static Dictionary<int, IndexedMeasurementValue<TValue>> BuildMap<TValue>(
            IReadOnlyList<IndexedMeasurementValue<TValue>> items,
            string sourceName)
        {
            var map = new Dictionary<int, IndexedMeasurementValue<TValue>>();
            if (items == null)
                return map;

            for (int i = 0; i < items.Count; i++)
            {
                IndexedMeasurementValue<TValue> item = items[i];
                if (item == null)
                    throw new InvalidOperationException(string.Format("{0}第{1}项为空。", sourceName, i + 1));
                if (item.TargetIndex <= 0)
                    throw new InvalidOperationException(string.Format("{0}存在无效目标编号{1}。", sourceName, item.TargetIndex));
                if (map.ContainsKey(item.TargetIndex))
                    throw new InvalidOperationException(string.Format("{0}存在重复的目标编号{1}。", sourceName, item.TargetIndex));
                map.Add(item.TargetIndex, item);
            }

            return map;
        }
    }
}
