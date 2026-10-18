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
    /// AST工位2 DET/640结果解析，按RL12Parse的检测项提取、判定、绘制和学习结构实现。
    /// 类别顺序：0端子头、1露线芯、2绝缘胶、3屏蔽网、4铜环、5胶皮、6飞丝、7铝箔、8线芯。
    /// </summary>
    public class ASTStation2Parse
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

        /// <summary>解析检测框，输出工位2尺寸、数量、上下限判定、带颜色的矩形和文本。</summary>
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
                throw new InvalidOperationException("AST工位2检测项配置不存在，请先导入并选择检测项模板。");
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

            // 一次过滤无效框并收集距离依赖，避免每个检测项反复分配依赖列表。
            results = results == null ? new List<DetResult>() :
                results.Where(r => r.Box.Width > 0 && r.Box.Height > 0).ToList();
            var copperRings = results.Where(r => r.ClassId == 4).ToList();
            var rubbers = results.Where(r => r.ClassId == 5).ToList();
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
                bool isDistanceItem = false;
                bool isWidthMeasurement = false;
                bool useTargetBottom = false;
                bool useDependencyBottom = false;
                Func<Rect, Rect?> findDependencyRectFunc = null;

                #region 提取函数定义，某个检测项的值取自哪个ClassId结果的什么属性

                Func<DetResult, float?> extractValueFunc;
                switch (itemName)
                {
                    case "L1-S2(端子先端位置)":
                    case "铜环底部到端子头顶部的距离":
                        isDistanceItem = true;
                        useDependencyBottom = true;
                        findDependencyRectFunc = target => FindNearestCopperRing(target, copperRings);
                        extractValueFunc = r => r.ClassId == 0 ? GetCopperRingDistance(r.Box, copperRings) : null;
                        break;
                    case "出线芯宽度":
                    case "露线芯宽度":
                        isWidthMeasurement = true;
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Width : null;
                        break;
                    case "出线芯长度":
                    case "露线芯长度":
                    // 公共归一化会将旧中文名称转为内部键，保持已有方案/订阅兼容。
                    case "DetectItem.ExposedCoreLength":
                        extractValueFunc = r => r.ClassId == 1 ? (float?)r.Box.Height : null;
                        break;
                    case "线芯长度":
                    case "DetectItem.CoreLength":
                        // line 与露线芯为不同类别，长度沿当前图像Y方向测量。
                        extractValueFunc = r => r.ClassId == 8 ? (float?)r.Box.Height : null;
                        break;
                    case "线线宽度":
                        // 保留检测项配置的中文名称，宽度沿当前图像X方向测量。
                        isWidthMeasurement = true;
                        extractValueFunc = r => r.ClassId == 8 ? (float?)r.Box.Width : null;
                        break;
                    case "L6_箔剥宽度":
                        isWidthMeasurement = true;
                        extractValueFunc = r => r.ClassId == 2 ? (float?)r.Box.Width : null;
                        break;
                    case "L6_箔剥长度":
                        // 工艺长度对应当前图像的Y方向，仍取绝缘胶检测框的Height。
                        extractValueFunc = r => r.ClassId == 2 ? (float?)r.Box.Height : null;
                        break;
                    case "编组反转代-1":
                        isDistanceItem = true;
                        findDependencyRectFunc = target => FindNearestRubber(target, rubbers);
                        extractValueFunc = r => r.ClassId == 3 ? GetRubberTopDistance(r.Box, rubbers, false) : null;
                        break;
                    case "编组反转代-2":
                        isDistanceItem = true;
                        useTargetBottom = true;
                        findDependencyRectFunc = target => FindNearestRubber(target, rubbers);
                        extractValueFunc = r => r.ClassId == 3 ? GetRubberTopDistance(r.Box, rubbers, true) : null;
                        break;
                    case "L5_绝缘体长度":
                        isDistanceItem = true;
                        findDependencyRectFunc = target => FindNearestRubber(target, rubbers);
                        extractValueFunc = r => r.ClassId == 2 ? GetRubberTopDistance(r.Box, rubbers, false) : null;
                        break;
                    case "飞丝":
                    // 公共归一化会将旧中文名称转为内部键，保持已有方案/订阅兼容。
                    case "DetectItem.FlyingWire":
                        // 飞丝是数量型检测项，模板配置错误时也按数量统计，避免输出单个框的伪尺寸。
                        isCountItem = true;
                        extractValueFunc = r => r.ClassId == 6 ? (float?)1F : null;
                        break;
                    default:
                        LogUnsupportedDetectItem(itemName, param);
                        continue;
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
                        value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool isOk = AddValue(item, value, false, param, study, singleResults);
                        ParseCommon.AddRect(addedRectKeys, match.Result.Box, isOk, ref nodeResult);
                        Rect? dependencyRect = null;
                        if (isDistanceItem)
                        {
                            // 距离由两个框共同决定，任一距离NG时两个参与框均标红。
                            dependencyRect = findDependencyRectFunc?.Invoke(match.Result.Box);
                            if (dependencyRect.HasValue)
                                ParseCommon.AddRect(addedRectKeys, dependencyRect.Value, isOk, ref nodeResult);
                        }
                        if (!isDistanceItem || dependencyRect.HasValue)
                            ParseCommon.AddAstMeasurementArrow(nodeResult.AlgorithmResult, match.Result.Box,
                                dependencyRect, isWidthMeasurement, useTargetBottom, useDependencyBottom, number, deteItems.Count);
                    }
                }

                #endregion

                #region 特殊情况处理：检测项已启用，但模型未检出目标或缺少距离依赖

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
                    $"{number}. {ParseCommon.GetDisplayName(itemName)}: {valueStr}{(isCountItem ? string.Empty : "mm")}",
                    singleResults.All(r => r.IsOk) ? Color.Green : Color.Red));
            }

            nodeResult.AlgorithmResult.DetectResults = detectResults;
            bool allOk = detectResults.Values.SelectMany(list => list).All(r => r.IsOk);
            if (allOk) process.OKNumber++;
            else process.NGNumber++;
            // 仅输出检测项文本，整体判定由ROI布尔文本订阅显示；保留流程OK/NG统计。
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

        /// <summary>固定测铜环底边与目标顶边的Y坐标差绝对值；不因检测框重叠改换边缘，缺少铜环返回空值。</summary>
        private static float? GetCopperRingDistance(Rect target, List<DetResult> copperRings)
        {
            Rect? copperRing = FindNearestCopperRing(target, copperRings);
            if (!copperRing.HasValue) return null;
            return (float)Math.Abs((double)copperRing.Value.Bottom - target.Top);
        }

        /// <summary>固定测最近胶皮顶边与目标顶边或底边的Y坐标差绝对值；缺少胶皮返回空值。</summary>
        private static float? GetRubberTopDistance(Rect target, List<DetResult> rubbers, bool useTargetBottom)
        {
            Rect? rubber = FindNearestRubber(target, rubbers);
            if (!rubber.HasValue) return null;
            int targetEdge = useTargetBottom ? target.Bottom : target.Top;
            return (float)Math.Abs((double)rubber.Value.Top - targetEdge);
        }

        /// <summary>为每个部件选择中心最近的铜环；同距时按坐标稳定选择，避免依赖模型输出顺序。</summary>
        private static Rect? FindNearestCopperRing(Rect target, List<DetResult> copperRings)
        {
            return FindNearestBox(target, copperRings);
        }

        /// <summary>为每个部件选择中心最近的胶皮；同距时按坐标稳定选择，避免依赖模型输出顺序。</summary>
        private static Rect? FindNearestRubber(Rect target, List<DetResult> rubbers)
        {
            return FindNearestBox(target, rubbers);
        }

        /// <summary>按中心距离选择最近依赖框；同距时优先左上角坐标更小的框。</summary>
        private static Rect? FindNearestBox(Rect target, List<DetResult> detections)
        {
            Rect? nearest = null;
            double minDistance = double.MaxValue;
            foreach (var detection in detections)
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

        /// <summary>记录工位2解析器尚未接入的检测项，并允许其它检测项继续解析。</summary>
        private static void LogUnsupportedDetectItem(string itemName, NodeParamTDAI param)
        {
            string displayName = ParseCommon.GetDisplayName(itemName);
            string configName = param == null ? string.Empty : param.CurDetectItemName;
            LogHelper.AddLog(MsgLevel.Warn,
                $"AST工位2检测项“{displayName}”代码里面没有判断，已跳过该检测项，其它检测项继续检测。检测项配置：{configName}",
                true);
        }
    }
}
