using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 骏伟塑料模型解析器。类别0～2为视角目标，按检测框面积输出；类别3～6为缺陷目标，按检测框数量输出。
    /// </summary>
    public static class JunweiPlasticParse
    {
        /// <summary>按节点参数隔离自动学习会话，避免不同节点共享样本。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> StudySessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>单个节点的自动学习状态。</summary>
        private sealed class StudySession
        {
            /// <summary>当前检测项配置引用。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已采集的图像批次。</summary>
            public int Count;
            /// <summary>按检测项保存的有效样本。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>模型类别顺序，必须与AI配置中的ClassNames保持一致。</summary>
        private static readonly string[] ClassNames =
        {
            "front", "side", "bottom", "surface_roughness", "broken_post", "damage", "other_ng"
        };

        /// <summary>解析模型检测框并生成面积/数量检测结果。</summary>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var items) || items == null)
                throw new InvalidOperationException("骏伟塑料模型检测项配置不存在，请先导入并选择检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("面积换算比例必须是大于0的有限数值。");

            ParseCommon.NormalizeDetectItems(items);
            StudySession study = null;
            if (param.IsAutoStudy)
            {
                if (param.StudyNum <= 0 || param.StudyPercentage < 0 ||
                    float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                    throw new InvalidOperationException("自动学习次数必须大于0，浮动比例必须是非负有限数值。");
                study = StudySessions.GetValue(param, key => new StudySession());
                if (!ReferenceEquals(study.Items, items))
                {
                    study.Items = items;
                    study.Count = 0;
                    study.Values.Clear();
                }
            }
            else
            {
                StudySessions.Remove(param);
            }

            results = results == null ? new List<DetResult>() :
                results.Where(result => result.Box.Width > 0 && result.Box.Height > 0).ToList();
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            var addedRectKeys = new HashSet<string>();
            nodeResult.AlgorithmResult = new AlgorithmResult();
            int measurementNumber = 0;

            foreach (var item in items)
            {
                if (item == null || !item.Enable) continue;
                int number = ++measurementNumber;
                string itemName = item.Name;
                int classId = Array.IndexOf(ClassNames, itemName);
                if (classId < 0)
                    throw new InvalidOperationException($"骏伟塑料模型不支持检测项：{ParseCommon.GetDisplayName(itemName)}。");

                bool isAreaItem = classId <= 2;
                // 面积型标签由模型框面积计算；缺陷标签统一按目标框数量统计。
                item.IsCountItem = !isAreaItem;
                var matched = results.Where(result => result.ClassId == classId).ToList();
                var singleResults = new List<SingleDetectResult>();
                if (isAreaItem)
                {
                    foreach (var result in matched)
                    {
                        float value = result.Box.Width * result.Box.Height;
                        if (param.NeedConvert) value *= param.Scale * param.Scale;
                        value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool isOk = AddValue(item, value, false, param, study, singleResults);
                        ParseCommon.AddRect(addedRectKeys, result.Box, isOk, ref nodeResult);
                    }
                    if (matched.Count == 0)
                        AddValue(item, 0F, false, param, null, singleResults);
                }
                else
                {
                    bool isOk = AddValue(item, matched.Count, true, param, study, singleResults);
                    foreach (var result in matched)
                        ParseCommon.AddRect(addedRectKeys, result.Box, isOk, ref nodeResult);
                }

                string valueText = string.Join(",", singleResults.Select(result => result.Value));
                detectResults[itemName] = singleResults;
                ParseCommon.SetDetectItemCurValue(ref param, itemName, valueText);
                texts.Add(new ColorText($"{number}. {ParseCommon.GetDisplayName(itemName)}: {valueText}{(isAreaItem ? string.Empty : "个")}",
                    singleResults.All(result => result.IsOk) ? Color.Green : Color.Red));
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;
            nodeResult.AlgorithmResult.Texts = texts;
            if (detectResults.Values.SelectMany(values => values).All(result => result.IsOk))
                process.OKNumber++;
            else
                process.NGNumber++;

            if (study != null && ++study.Count >= param.StudyNum)
            {
                try
                {
                    ParseCommon.SetDetectItemMinMax(items, study.Values, param.StudyPercentage);
                    param.IsAutoStudy = false;
                    ParseCommon.CloseAutoStudyEvent?.Invoke(param, param.NodeName);
                }
                finally
                {
                    StudySessions.Remove(param);
                }
            }
        }

        /// <summary>记录单项结果并在自动学习期间保存样本。</summary>
        private static bool AddValue(DetectItemInfo item, float value, bool isCountItem, NodeParamTDAI param,
            StudySession study, List<SingleDetectResult> singleResults)
        {
            bool isOk = param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value);
            singleResults.Add(new SingleDetectResult
            {
                Name = item.Name,
                Value = value == 0 || isCountItem ? value.ToString() : value.ToString("F2"),
                IsOk = isOk
            });
            if (study != null)
            {
                if (!study.Values.TryGetValue(item.Name, out var values))
                {
                    values = new List<float>();
                    study.Values.Add(item.Name, values);
                }
                values.Add(value);
            }
            return isOk;
        }
    }
}
