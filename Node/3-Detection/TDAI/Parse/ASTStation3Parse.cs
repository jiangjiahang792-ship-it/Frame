using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// AST工位3 DET/640结果解析。
    /// 类别顺序：0露线芯、1压接区域、2飞丝。
    /// </summary>
    public class ASTStation3Parse
    {
        /// <summary>以节点参数隔离学习数据，节点释放后会话可自动回收。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> StudySessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>单个节点的学习计数和检测项样本。</summary>
        private sealed class StudySession
        {
            /// <summary>当前学习的配置，切换配置后重新计数。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已经采集的图像批次数。</summary>
            public int Count;
            /// <summary>按检测项名称保存的实测样本。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>解析检测框，输出五项测量、上下限判定、带颜色的矩形和文本。</summary>
        /// <param name="results">已还原到原图坐标的检测列表。</param>
        /// <param name="resultCount">兼容统一调用签名，以列表实际数量为准。</param>
        /// <param name="param">检测配置、物理比例及学习参数。</param>
        /// <param name="process">接收OK/NG统计的所属流程。</param>
        /// <param name="nodeResult">接收本次解析结果。</param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var deteItems) || deteItems == null)
                throw new InvalidOperationException("AST工位3检测项配置不存在，请先导入并选择检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("尺寸换算比例必须是大于0的有限数值。");

            ParseCommon.NormalizeDetectItems(deteItems);
            StudySession study = null;
            if (param.IsAutoStudy)
            {
                if (param.StudyNum <= 0 || param.StudyPercentage < 0 ||
                    float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                    throw new InvalidOperationException("自动学习次数必须大于0，浮动比例必须是非负有限数值。");
                study = StudySessions.GetValue(param, key => new StudySession());
                if (!ReferenceEquals(study.Items, deteItems))
                {
                    study.Items = deteItems;
                    study.Count = 0;
                    study.Values.Clear();
                }
            }
            else StudySessions.Remove(param);

            results = results == null ? new List<DetResult>() :
                results.Where(r => r.Box.Width > 0 && r.Box.Height > 0).ToList();
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            var addedRectKeys = new HashSet<string>();
            nodeResult.AlgorithmResult = new AlgorithmResult();

            int measurementNumber = 0;
            foreach (var item in deteItems)
            {
                if (item == null || !item.Enable) continue;
                int number = ++measurementNumber;
                string itemName = item.Name;
                bool isCountItem = item.IsCountItem;
                Func<DetResult, float?> extractValueFunc;

                switch (itemName)
                {
                    case "露线芯宽度":
                        extractValueFunc = r => r.ClassId == 0 ? (float?)r.Box.Width : null;
                        break;
                    case "露线芯高度":
                        extractValueFunc = r => r.ClassId == 0 ? (float?)r.Box.Height : null;
                        break;
                    case "压接区宽度":
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Width : null;
                        break;
                    case "压接区域高度":
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Height : null;
                        break;
                    case "飞丝":
                    // 公共归一化会将旧中文名称转为内部键，保持已有方案/订阅兼容。
                    case "DetectItem.FlyingWire":
                        // 飞丝是数量型检测项，模板配置错误时也按数量统计，避免输出单个框的伪尺寸。
                        isCountItem = true;
                        extractValueFunc = r => r.ClassId == 2 ? (float?)1F : null;
                        break;
                    default:
                        throw new InvalidOperationException($"AST工位3模型不支持检测项：{ParseCommon.GetDisplayName(itemName)}。");
                }

                var matchedResults = results.Select(r => new { Result = r, Value = extractValueFunc(r) })
                    .Where(r => r.Value.HasValue).ToList();
                var singleResults = new List<SingleDetectResult>();

                if (isCountItem)
                {
                    bool isOk = AddValue(item, matchedResults.Count, true, param, study, singleResults);
                    foreach (var match in matchedResults)
                        ParseCommon.AddRect(addedRectKeys, match.Result.Box, isOk, ref nodeResult);
                }
                else
                {
                    foreach (var match in matchedResults)
                    {
                        float value = match.Value.Value;
                        if (param.NeedConvert) value *= param.Scale;
                        bool isOk = AddValue(item, value, false, param, study, singleResults);
                        ParseCommon.AddRect(addedRectKeys, match.Result.Box, isOk, ref nodeResult);
                        ParseCommon.AddAstMeasurementArrow(nodeResult.AlgorithmResult, match.Result.Box,
                            null, itemName.EndsWith("宽度"), false, false,
                            number, deteItems.Count);
                    }
                }

                if (matchedResults.Count == 0 && !isCountItem)
                    AddValue(item, 0F, false, param, null, singleResults);

                string valueStr = string.Join(",", singleResults.Select(r => r.Value));
                detectResults[itemName] = singleResults;
                ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr);
                texts.Add(new ColorText(
                    $"{number}. {ParseCommon.GetDisplayName(itemName)}: {valueStr}{(isCountItem ? string.Empty : "mm")}",
                    singleResults.All(r => r.IsOk) ? Color.Green : Color.Red));
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;
            bool allOk = detectResults.Values.SelectMany(list => list).All(r => r.IsOk);
            if (allOk) process.OKNumber++;
            else process.NGNumber++;
            // AST画面只显示最终判定结果，内部OK/NG计数仍保留给流程统计使用。
            texts.Add(new ColorText(LanguageManager.Format("Result.ProductResult", allOk ? "OK" : "NG"), allOk ? Color.Green : Color.Red));
            nodeResult.AlgorithmResult.Texts = texts;

            if (study != null && ++study.Count >= param.StudyNum)
            {
                try
                {
                    ParseCommon.SetDetectItemMinMax(deteItems, study.Values, param.StudyPercentage);
                    param.IsAutoStudy = false;
                    ParseCommon.CloseAutoStudyEvent?.Invoke(param, param.NodeName);
                }
                finally { StudySessions.Remove(param); }
            }
        }

        /// <summary>保存单项判定和有效学习样本，学习期间默认显示OK。</summary>
        private static bool AddValue(DetectItemInfo item, float value, bool isCountItem, NodeParamTDAI param,
            StudySession study, List<SingleDetectResult> singleResults)
        {
            bool isOk = param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value);
            singleResults.Add(new SingleDetectResult {
                Name = item.Name, Value = value == 0 || isCountItem ? value.ToString() : value.ToString("F2"), IsOk = isOk });
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
