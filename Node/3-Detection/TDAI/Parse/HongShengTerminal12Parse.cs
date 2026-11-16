using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Logger;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 宏晟端子12类模型解析器；模型类别编号与RL_12类线芯模型保持一致。
    /// </summary>
    public static class HongShengTerminal12Parse
    {
        /// <summary>检测项的测量方式。</summary>
        private enum MeasureKind { Width, Height, Area, Count, Bite }

        /// <summary>检测项与模型类别、测量方式的映射定义。</summary>
        private sealed class Definition
        {
            /// <summary>模型类别编号。</summary>
            public int ClassId { get; set; }
            /// <summary>测量方式。</summary>
            public MeasureKind Kind { get; set; }
        }

        /// <summary>宏晟模型支持的检测项定义。</summary>
        private static readonly Dictionary<string, Definition> Definitions = CreateDefinitions();

        /// <summary>按节点隔离的一键学习状态，防止不同流程之间混入样本。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> StudySessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>单个节点当前一轮学习所累积的有效尺寸样本。</summary>
        private sealed class StudySession
        {
            /// <summary>本轮检测项集合引用。</summary>
            public List<DetectItemInfo> Items { get; set; }
            /// <summary>已完成的学习帧数。</summary>
            public int Count { get; set; }
            /// <summary>学习开关代次。</summary>
            public int Generation { get; set; }
            /// <summary>按检测项保存的有效数值样本。</summary>
            public Dictionary<string, List<float>> Values { get; } = new Dictionary<string, List<float>>();
        }

        /// <summary>创建宏晟检测项注册表。</summary>
        private static Dictionary<string, Definition> CreateDefinitions()
        {
            var definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
            Action<string, int, MeasureKind> add = (name, classId, kind) =>
                definitions.Add(DetectItemLanguage.NormalizeName(name),
                    new Definition { ClassId = classId, Kind = kind });
            add("前铆露线芯高度", 0, MeasureKind.Height);
            add("前铆露线芯长度", 0, MeasureKind.Width);
            add("前铆露线芯面积", 0, MeasureKind.Area);
            add("后铆露线芯高度", 2, MeasureKind.Height);
            add("后铆露线芯长度", 2, MeasureKind.Width);
            add("后铆露线芯面积", 2, MeasureKind.Area);
            add("飞丝", 8, MeasureKind.Count);
            add("前铆脚面积", 1, MeasureKind.Area);
            add("后铆脚面积", 4, MeasureKind.Area);
            add("咬胶长度", 2, MeasureKind.Bite);
            add("防水栓反装", 10, MeasureKind.Count);
            add("正向防水栓面积", 5, MeasureKind.Area);
            add("防水栓破损", 11, MeasureKind.Count);
            return definitions;
        }

        /// <summary>
        /// 解析模型检测框并生成检测项结果、绘制框和中文文本。
        /// </summary>
        /// <param name="results">模型检测结果。</param>
        /// <param name="resultCount">模型检测结果数量。</param>
        /// <param name="param">目标分类节点参数。</param>
        /// <param name="process">当前流程。</param>
        /// <param name="nodeResult">待写入的节点结果。</param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out List<DetectItemInfo> items) || items == null)
                throw new InvalidOperationException("请先导入并选择宏晟端子12类模型检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("宏晟端子12类模型尺寸比例必须是大于0的有限数值。");

            ParseCommon.NormalizeDetectItems(items);
            List<DetResult>[] groups = Enumerable.Range(0, 12).Select(index => new List<DetResult>()).ToArray();
            foreach (DetResult result in results ?? new List<DetResult>())
                if (result.ClassId >= 0 && result.ClassId < groups.Length && result.Box.Width > 0 && result.Box.Height > 0)
                    groups[result.ClassId].Add(result);

            ApplyInitialCommunicationRecipeItemRules(groups[5].Count > 0, param, items);
            nodeResult.AlgorithmResult = new AlgorithmResult();
            HashSet<string> rectKeys = new HashSet<string>();
            foreach (DetectItemInfo item in items.Where(candidate => candidate != null && candidate.Enable))
            {
                if (!Definitions.TryGetValue(item.Name, out Definition definition))
                    throw new InvalidOperationException("宏晟端子12类模型不支持检测项：" + item.Name);

                bool isCount = definition.Kind == MeasureKind.Count;
                item.IsCountItem = isCount;
                List<DetResult> matches = groups[definition.ClassId];
                List<SingleDetectResult> values = new List<SingleDetectResult>();
                if (isCount)
                {
                    int count = matches.Count;
                    bool ok = param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, count);
                    values.Add(new SingleDetectResult { Name = item.Name, Value = count.ToString(), IsOk = ok });
                    item.CurValue = count.ToString();
                    foreach (DetResult match in matches)
                        ParseCommon.AddRect(rectKeys, match.Box, ok, ref nodeResult);
                }
                else
                {
                    foreach (DetResult match in matches)
                    {
                        bool valid = TryMeasure(definition.Kind, match.Box, groups[4], out float value);
                        if (valid && param.NeedConvert) value *= param.Scale;
                        if (valid) value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool ok = valid && (param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value));
                        values.Add(new SingleDetectResult { Name = item.Name, Value = value.ToString("F2"), IsOk = ok });
                        ParseCommon.AddRect(rectKeys, match.Box, ok, ref nodeResult);
                    }
                    if (values.Count == 0)
                        values.Add(new SingleDetectResult { Name = item.Name, Value = "0", IsOk = false });
                    item.CurValue = string.Join(",", values.Select(value => value.Value));
                }

                nodeResult.AlgorithmResult.DetectResults[item.Name] = values;
                string displayName = ParseCommon.GetDisplayName(item.Name);
                string text = isCount
                    ? displayName + ": " + item.CurValue
                    : displayName + ": " + item.CurValue + " 范围值: [" + item.MinValue + ", " + item.MaxValue + "]";
                nodeResult.AlgorithmResult.Texts.Add(new ColorText(text, values.All(value => value.IsOk) ? Color.Green : Color.Red));
            }

            CompleteManualStudyIfNeeded(param, items, nodeResult.AlgorithmResult.DetectResults);
        }

        /// <summary>累计手动一键学习的尺寸样本，并在达到设定帧数后生成上下限。</summary>
        private static void CompleteManualStudyIfNeeded(NodeParamTDAI param, List<DetectItemInfo> items,
            Dictionary<string, List<SingleDetectResult>> detectResults)
        {
            if (!param.IsAutoStudy)
            {
                StudySessions.Remove(param);
                return;
            }

            if (param.StudyNum <= 0 || param.StudyPercentage < 0 || float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                throw new InvalidOperationException("宏晟端子12类模型学习次数或浮动比例无效。");

            StudySession session = StudySessions.GetValue(param, key => new StudySession());
            if (!ReferenceEquals(session.Items, items) || session.Generation != param.StudyGeneration)
            {
                session.Items = items;
                session.Generation = param.StudyGeneration;
                session.Count = 0;
                session.Values.Clear();
            }

            foreach (KeyValuePair<string, List<SingleDetectResult>> pair in detectResults)
            {
                DetectItemInfo item = items.FirstOrDefault(candidate => candidate != null && candidate.Name == pair.Key);
                if (item == null || item.IsCountItem || pair.Value == null)
                    continue;
                foreach (SingleDetectResult result in pair.Value.Where(value => value != null && value.IsOk))
                    if (float.TryParse(result.Value, out float value))
                    {
                        if (!session.Values.TryGetValue(pair.Key, out List<float> samples))
                            session.Values[pair.Key] = samples = new List<float>();
                        samples.Add(value);
                    }
            }

            session.Count++;
            if (session.Count < param.StudyNum)
                return;
            if (session.Values.Count == 0)
                throw new InvalidOperationException("宏晟端子12类模型学习失败：当前图像没有有效尺寸数据。");

            ParseCommon.SetDetectItemMinMax(items, session.Values, param.StudyPercentage);
            ParseCommon.CloseAutoStudyEvent?.Invoke(null, param.NodeName);
            StudySessions.Remove(param);
        }

        /// <summary>计算普通尺寸、面积或咬胶水平X距离。</summary>
        private static bool TryMeasure(MeasureKind kind, Rect box, List<DetResult> backRivets, out float value)
        {
            value = 0;
            switch (kind)
            {
                case MeasureKind.Width: value = box.Width; return true;
                case MeasureKind.Height: value = box.Height; return true;
                case MeasureKind.Area: value = box.Width * box.Height; return true;
                case MeasureKind.Bite:
                    if (backRivets == null || backRivets.Count == 0) return false;
                    DetResult nearest = backRivets.OrderBy(result => Math.Abs(box.Left - result.Box.Right)).FirstOrDefault();
                    value = Math.Abs(box.Left - nearest.Box.Right);
                    return true;
                default: return false;
            }
        }

        /// <summary>
        /// 通信新配方首次学习时按防水栓检出结果选择互斥检测项。
        /// 未检出防水栓时停用防水栓项；检出时停用后铆露线芯、后铆脚面积及咬胶长度。
        /// </summary>
        private static void ApplyInitialCommunicationRecipeItemRules(bool hasWaterproofPlug,
            NodeParamTDAI param, IEnumerable<DetectItemInfo> items)
        {
            if (param.ModelName != ModelName.宏晟端子12类模型 || !param.IsAutoStudy ||
                !items.Any(item => item != null && item.NeedsInitialAutoStudy))
                return;

            foreach (DetectItemInfo item in items)
            {
                if (item == null)
                    continue;
                if (hasWaterproofPlug ? IsBackCoreOrBiteItem(item.Name) : IsWaterproofPlugItem(item.Name))
                    item.Enable = false;
            }

            LogHelper.AddLog(MsgLevel.Info,
                hasWaterproofPlug
                    ? $"宏晟通信新配方({param.CurDetectItemName})首次学习检出防水栓，已停用后铆露线芯、后铆脚面积及咬胶检测项。"
                    : $"宏晟通信新配方({param.CurDetectItemName})首次学习未检出防水栓，已停用全部防水栓相关检测项。",
                true);
        }

        /// <summary>判断检测项是否属于检出防水栓后应关闭的后铆露线芯、后铆脚面积或咬胶项目。</summary>
        private static bool IsBackCoreOrBiteItem(string itemName)
        {
            return itemName == DetectItemLanguage.NormalizeName("后铆露线芯高度") ||
                   itemName == DetectItemLanguage.NormalizeName("后铆露线芯长度") ||
                   itemName == DetectItemLanguage.NormalizeName("后铆露线芯面积") ||
                   itemName == DetectItemLanguage.NormalizeName("后铆脚面积") ||
                   itemName == DetectItemLanguage.NormalizeName("咬胶长度");
        }

        /// <summary>判断检测项是否属于宏晟防水栓相关项目。</summary>
        private static bool IsWaterproofPlugItem(string itemName)
        {
            return itemName == DetectItemLanguage.NormalizeName("防水栓反装") ||
                   itemName == DetectItemLanguage.NormalizeName("正向防水栓面积") ||
                   itemName == DetectItemLanguage.NormalizeName("防水栓破损");
        }
    }
}
