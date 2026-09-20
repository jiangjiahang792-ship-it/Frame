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
    /// AST工位1 DET/640结果解析，按RL12Parse的检测项提取、判定、绘制和学习结构实现。
    /// 类别顺序：0绝缘胶、1屏蔽网、2铜环、3胶皮、4飞丝。
    /// </summary>
    public class ASTStation1Parse
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
            /// <summary>按中文检测项名称保存的实测样本。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>解析检测框，输出九项测量、上下限判定、带颜色的矩形和文本。</summary>
        /// <param name="results">已还原到原图坐标的检测列表。</param>
        /// <param name="resultCount">兼容RL12调用签名，以列表实际数量为准。</param>
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
                throw new InvalidOperationException("AST工位1检测项配置不存在，请先导入并选择检测项模板。");
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

            // 一次过滤无效框并收集胶皮，避免每个检测项反复分配依赖列表。
            results = results == null ? new List<DetResult>() :
                results.Where(r => r.Box.Width > 0 && r.Box.Height > 0).ToList();
            var rubbers = results.Where(r => r.ClassId == 3).ToList();
            var detectResults = new Dictionary<string, List<SingleDetectResult>>();
            var texts = new List<ColorText>();
            var addedRectKeys = new HashSet<string>();
            nodeResult.AlgorithmResult = new AlgorithmResult();

            foreach (var item in deteItems)
            {
                if (item == null || !item.Enable) continue;
                string itemName = item.Name;
                bool isCountItem = item.IsCountItem;
                bool isDistanceItem = false;

                #region 提取函数定义，某个检测项的值取自哪个ClassId结果的什么属性

                Func<DetResult, float?> extractValueFunc;
                switch (itemName)
                {
                    case "胶皮到铜环的距离":
                        isDistanceItem = true;
                        extractValueFunc = r => r.ClassId == 2 ? GetRubberDistance(r.Box, rubbers) : null;
                        break;
                    case "胶皮到屏蔽网的距离":
                        isDistanceItem = true;
                        extractValueFunc = r => r.ClassId == 1 ? GetRubberDistance(r.Box, rubbers) : null;
                        break;
                    case "胶皮到绝缘胶的位置":
                        isDistanceItem = true;
                        extractValueFunc = r => r.ClassId == 0 ? GetRubberDistance(r.Box, rubbers) : null;
                        break;
                    case "屏蔽网高度":
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Height : null;
                        break;
                    case "屏蔽网宽度":
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Width : null;
                        break;
                    case "铜环高度":
                        extractValueFunc = r => r.ClassId == 2 ? (float?)r.Box.Height : null;
                        break;
                    case "铜环宽度":
                        extractValueFunc = r => r.ClassId == 2 ? (float?)r.Box.Width : null;
                        break;
                    case "绝缘胶高度":
                        extractValueFunc = r => r.ClassId == 0 ? (float?)r.Box.Height : null;
                        break;
                    case "绝缘胶宽度":
                        extractValueFunc = r => r.ClassId == 0 ? (float?)r.Box.Width : null;
                        break;
                    default:
                        throw new InvalidOperationException($"AST工位1模型不支持检测项：{ParseCommon.GetDisplayName(itemName)}。");
                }

                #endregion

                #region 匹配结果提取

                // 将提取结果一起保留，避免重复执行距离计算。
                var matchedResults = results.Select(r => new { Result = r, Value = extractValueFunc(r) })
                    .Where(r => r.Value.HasValue).ToList();
                var singleResults = new List<SingleDetectResult>();

                #endregion

                #region 正常情况：检测项已启用，模型有检出该项结果

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
                        if (isDistanceItem)
                        {
                            // 距离由两个框共同决定，任一距离NG时两个参与框均标红。
                            Rect? rubber = FindNearestRubber(match.Result.Box, rubbers);
                            if (rubber.HasValue)
                                ParseCommon.AddRect(addedRectKeys, rubber.Value, isOk, ref nodeResult);
                        }
                    }
                }

                #endregion

                #region 特殊情况处理：检测项已启用，但模型未检出目标或缺少胶皮依赖

                if (matchedResults.Count == 0 && !isCountItem)
                {
                    // 沿用RL12的缺失零值上下限判定；无实测值时不加入自动学习样本。
                    AddValue(item, 0F, false, param, null, singleResults);
                }

                #endregion

                string valueStr = string.Join(",", singleResults.Select(r => r.Value));
                detectResults[itemName] = singleResults;
                ParseCommon.SetDetectItemCurValue(ref param, itemName, valueStr);
                texts.Add(new ColorText(
                    $"{ParseCommon.GetDisplayName(itemName)}: {valueStr} 设定值: [{item.MinValue}, {item.MaxValue}]",
                    singleResults.All(r => r.IsOk) ? Color.Green : Color.Red));
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;
            bool allOk = detectResults.Values.SelectMany(list => list).All(r => r.IsOk);
            if (allOk) process.OKNumber++;
            else process.NGNumber++;
            texts.Add(new ColorText(LanguageManager.Format("Result.TotalCount", process.OKNumber + process.NGNumber), Color.Green));
            texts.Add(new ColorText(LanguageManager.Format("Result.OKCount", process.OKNumber), Color.Green));
            texts.Add(new ColorText(LanguageManager.Format("Result.Yield",
                (process.OKNumber * 100.0 / (process.OKNumber + process.NGNumber)).ToString("F2")), Color.Green));
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

        /// <summary>保存单项判定和有效学习样本，学习期间与RL12一致默认显示OK。</summary>
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

        /// <summary>沿图像Y轴测上下相邻边缘间距；Y投影重叠或接触为0，缺少胶皮返回空值。</summary>
        private static float? GetRubberDistance(Rect target, List<DetResult> rubbers)
        {
            Rect? rubber = FindNearestRubber(target, rubbers);
            if (!rubber.HasValue) return null;
            return Math.Max(0, Math.Max(target.Top - rubber.Value.Bottom, rubber.Value.Top - target.Bottom));
        }

        /// <summary>为每个部件选择中心最近的胶皮；同距时按坐标稳定选择，避免依赖模型输出顺序。</summary>
        private static Rect? FindNearestRubber(Rect target, List<DetResult> rubbers)
        {
            Rect? nearest = null;
            double minDistance = double.MaxValue;
            foreach (var detection in rubbers)
            {
                Rect box = detection.Box;
                double dx = box.X + box.Width / 2.0 - target.X - target.Width / 2.0;
                double dy = box.Y + box.Height / 2.0 - target.Y - target.Height / 2.0;
                double distance = dx * dx + dy * dy;
                if (distance < minDistance || (distance == minDistance && nearest.HasValue &&
                    (box.X < nearest.Value.X || (box.X == nearest.Value.X && box.Y < nearest.Value.Y))))
                {
                    minDistance = distance;
                    nearest = box;
                }
            }
            return nearest;
        }
    }
}
