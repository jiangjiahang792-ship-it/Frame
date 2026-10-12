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
    /// <summary>AST工位1五类SEG解析器，测量取真实掩膜，绝不以检测框替代缺失掩膜。</summary>
    public static class ASTStation1SegParse
    {
        /// <summary>与训练标签行顺序一致的类别名称。</summary>
        public static readonly IReadOnlyList<string> ClassNames = Array.AsReadOnly(new[]
            { "insulation_glue", "shielding_mesh", "copper_ring", "rubber", "fly_line" });
        /// <summary>按节点隔离学习会话，避免四个AI节点交叉累计。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> Sessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>当前检测配置的学习进度。</summary>
        private sealed class StudySession
        {
            /// <summary>本轮学习使用的检测项集合。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已经处理的图像批次数。</summary>
            public int Count;
            /// <summary>仅保存有效实测值，缺失目标不参与公差学习。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>解析分割边界，输出测量判定、目标矩形框、黄色箭头和学习数据。</summary>
        public static void Parse(List<SegResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            List<DetectItemInfo> items;
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out items) || items == null)
                throw new InvalidOperationException("AST工位1分割模型检测项配置不存在，请先选择检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("尺寸换算比例必须是大于0的有限数值。");
            ParseCommon.NormalizeDetectItems(items);
            StudySession study = null;
            if (param.IsAutoStudy)
            {
                if (param.StudyNum <= 0 || param.StudyPercentage < 0 ||
                    float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                    throw new InvalidOperationException("自动学习次数必须大于0，浮动比例必须是非负有限数值。");
                study = Sessions.GetValue(param, key => new StudySession());
                if (!ReferenceEquals(study.Items, items))
                {
                    study.Items = items; study.Count = 0; study.Values.Clear();
                }
            }
            else Sessions.Remove(param);

            var valid = (results ?? new List<SegResult>()).Where(r => r.ClassId >= 0 && r.ClassId < ClassNames.Count &&
                r.MaskGeometry != null && r.MaskGeometry.Bounds.Width > 0 && r.MaskGeometry.Bounds.Height > 0).ToList();
            // 单产品测量采用每类最高置信度实例，与SEG测试程序一致；飞丝数量使用全部有效实例。
            var best = valid.GroupBy(r => r.ClassId).ToDictionary(g => g.Key,
                g => g.OrderByDescending(r => r.Score).ThenBy(r => r.MaskGeometry.Bounds.X)
                    .ThenBy(r => r.MaskGeometry.Bounds.Y).First());
            var colors = valid.Select(r => r.ClassId).Distinct().ToDictionary(id => id, id => Color.Green);
            var output = new AlgorithmResult();
            int number = 0;
            foreach (DetectItemInfo item in items)
            {
                if (item == null || !item.Enable) continue;
                int id, dependency;
                bool width, count, lowerTop;
                if (!Resolve(item.Name, out id, out dependency, out width, out count, out lowerTop))
                {
                    LogHelper.AddLog(MsgLevel.Warn, $"AST工位1分割模型检测项“{ParseCommon.GetDisplayName(item.Name)}”在代码中没有判断逻辑，已跳过，其它检测项继续检测。", true);
                    continue;
                }
                number++;
                SegResult target, other;
                bool hasTarget = best.TryGetValue(id, out target);
                bool hasOther = best.TryGetValue(dependency, out other);
                bool hasValue = count
                    ? !(results ?? new List<SegResult>()).Any(r => r.ClassId == id &&
                        (r.MaskGeometry == null || r.MaskGeometry.Bounds.Width <= 0 || r.MaskGeometry.Bounds.Height <= 0))
                    : hasTarget && (dependency < 0 || hasOther);
                if (lowerTop && hasValue)
                    hasValue = target.MaskGeometry.UpperEdgeLowestY >= 0 && other.MaskGeometry.UpperEdgeLowestY >= 0;
                float value = 0;
                int startY = 0, endY = 0;
                if (count) value = valid.Count(r => r.ClassId == id);
                else if (hasValue)
                {
                    Rect bounds = target.MaskGeometry.Bounds;
                    if (dependency < 0) value = width ? bounds.Width : bounds.Height;
                    else
                    {
                        Rect otherBounds = other.MaskGeometry.Bounds;
                        if (lowerTop)
                        {
                            startY = target.MaskGeometry.UpperEdgeLowestY;
                            endY = other.MaskGeometry.UpperEdgeLowestY;
                        }
                        else if (id == 0 && dependency == 2)
                        {
                            startY = bounds.Top; endY = otherBounds.Bottom - 1;
                        }
                        else
                        {
                            // 兼容原工位1胶皮间距项：重叠为0，否则取相邻有效边缘行。
                            bool targetAbove = bounds.Bottom <= otherBounds.Top;
                            bool otherAbove = otherBounds.Bottom <= bounds.Top;
                            startY = targetAbove ? bounds.Bottom - 1 : bounds.Top;
                            endY = targetAbove ? otherBounds.Top : otherAbove ? otherBounds.Bottom - 1 : startY;
                        }
                        value = Math.Abs(endY - startY);
                    }
                    if (param.NeedConvert) value *= param.Scale;
                }
                bool isOk = hasValue && (param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value));
                string valueText = count || value == 0 ? value.ToString() : value.ToString("F2");
                output.DetectResults[item.Name] = new List<SingleDetectResult> {
                    new SingleDetectResult { Name = item.Name, Value = valueText, IsOk = isOk } };
                ParseCommon.SetDetectItemCurValue(ref param, item.Name, valueText);
                // 现场界面统一以毫米展示尺寸项；数量型检测项保持纯数字，便于直接读取数量。
                bool isCountItem = count || item.IsCountItem;
                string displayValue = isCountItem ? valueText : (hasValue ? valueText : "0") + "mm";
                output.Texts.Add(new ColorText($"{number}. {ParseCommon.GetDisplayName(item.Name)}: {displayValue}",
                    isOk ? Color.Green : Color.Red));
                if (!isOk)
                {
                    if (colors.ContainsKey(id)) colors[id] = Color.Red;
                    if (colors.ContainsKey(dependency)) colors[dependency] = Color.Red;
                }
                if (hasValue && !count)
                {
                    Rect bounds = target.MaskGeometry.Bounds;
                    if (dependency < 0)
                        ParseCommon.AddAstMeasurementArrow(output, bounds, null, width, false, false, number, items.Count);
                    else
                        // 用真实采样行构造参考边，箭头与距离取值使用完全相同的端点。
                        ParseCommon.AddAstMeasurementArrow(output, new Rect(bounds.X, startY, bounds.Width, 1),
                            new Rect(other.MaskGeometry.Bounds.X, endY, other.MaskGeometry.Bounds.Width, 1),
                            false, false, false, number, items.Count);
                }
                if (study != null && hasValue)
                {
                    List<float> values;
                    if (!study.Values.TryGetValue(item.Name, out values))
                        study.Values[item.Name] = values = new List<float>();
                    values.Add(value);
                }
            }
            // 显示层使用模型检测框；掩膜仅用于前面的测量和有效性判定。
            foreach (SegResult result in valid)
                output.Rects.Add(new ColorRotatedRect(result.Rect, colors[result.ClassId]));
            nodeResult.AlgorithmResult = output;
            bool allOk = output.IsAllOk;
            if (allOk) process.OKNumber++; else process.NGNumber++;
            // AST画面只显示最终判定结果，内部OK/NG计数仍保留给流程统计使用。
            output.Texts.Add(new ColorText(LanguageManager.Format("Result.ProductResult", allOk ? "OK" : "NG"), allOk ? Color.Green : Color.Red));
            if (study != null && ++study.Count >= param.StudyNum)
            {
                try
                {
                    ParseCommon.SetDetectItemMinMax(items, study.Values, param.StudyPercentage);
                    param.IsAutoStudy = false;
                    ParseCommon.CloseAutoStudyEvent?.Invoke(param, param.NodeName);
                }
                finally { Sessions.Remove(param); }
            }
        }

        /// <summary>中文检测项映射到训练类别、依赖部件及掩膜测量方式。</summary>
        private static bool Resolve(string name, out int id, out int dependency, out bool width, out bool count, out bool lowerTop)
        {
            id = -1; dependency = -1; width = false; count = false; lowerTop = false;
            switch (name)
            {
                case "绝缘胶顶部最低点到屏蔽网顶部最低点的距离": id = 0; dependency = 1; lowerTop = true; break;
                case "绝缘胶顶部到铜环底部的距离": id = 0; dependency = 2; break;
                case "L1-S2":
                case "胶皮到铜环的距离": id = 2; dependency = 3; break;
                case "L1-S3":
                case "胶皮到屏蔽网的距离": id = 1; dependency = 3; break;
                case "L1-S4":
                case "胶皮到绝缘胶的位置": id = 0; dependency = 3; lowerTop = true; break;
                case "屏蔽网高度": id = 1; break;
                case "屏蔽网宽度": id = 1; width = true; break;
                case "铜环高度": id = 2; break;
                case "铜环宽度": id = 2; width = true; break;
                case "绝缘胶高度": id = 0; break;
                case "绝缘胶宽度": id = 0; width = true; break;
                case "胶皮高度": id = 3; break;
                case "胶皮宽度": id = 3; width = true; break;
                case "飞丝":
                case "DetectItem.FlyingWire": id = 4; count = true; break;
                default: return false;
            }
            return true;
        }
    }
}
