using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>多线芯剥皮OBB模型解析器：类别0为线芯line，类别1为胶皮rubber。</summary>
    public static class MultiCoreParse
    {
        /// <summary>按节点参数隔离自动学习数据，防止多个流程之间串用样本。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> StudySessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>单个节点的一键学习会话。</summary>
        private sealed class StudySession
        {
            /// <summary>当前检测项配置引用。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已经采集的图像批次数。</summary>
            public int Count;
            /// <summary>按检测项名称保存的学习样本。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>
        /// 兼容现场已有的DET多线芯配置；普通水平框转换为角度0的旋转框后复用统一判定逻辑。
        /// </summary>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            var compatibleResults = new List<ObbResult>();
            foreach (var result in results ?? new List<DetResult>())
            {
                if (result.Box.Width <= 0 || result.Box.Height <= 0) continue;
                compatibleResults.Add(new ObbResult
                {
                    center_x = result.Box.X + result.Box.Width / 2F,
                    center_y = result.Box.Y + result.Box.Height / 2F,
                    width = result.Box.Width,
                    height = result.Box.Height,
                    angle = 0F,
                    class_id = result.ClassId,
                    confidence = result.Score
                });
            }
            Parse(compatibleResults, compatibleResults.Count, param, process, ref nodeResult);
        }

        /// <summary>解析线芯、胶皮的宽高和数量，并生成逐框判定结果。</summary>
        public static void Parse(List<ObbResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var items) || items == null)
                throw new InvalidOperationException("多线芯模型检测项配置不存在，请先导入并选择检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("多线芯模型尺寸换算比例必须是大于0的有限数值。");

            ParseCommon.NormalizeDetectItems(items);
            StudySession study = PrepareStudySession(param, items);

            // 推理结果仅遍历一次并按类别分组，检测项计算直接复用列表，降低多目标场景开销。
            var cores = new List<ObbResult>();
            var rubbers = new List<ObbResult>();
            foreach (var result in results ?? new List<ObbResult>())
            {
                if (!IsFinite(result.center_x) || !IsFinite(result.center_y) ||
                    !IsFinite(result.width) || !IsFinite(result.height) || !IsFinite(result.angle) ||
                    result.width <= 0 || result.height <= 0) continue;
                if (result.class_id == 0) cores.Add(result);
                else if (result.class_id == 1) rubbers.Add(result);
            }

            nodeResult.AlgorithmResult = new AlgorithmResult();
            var addedRectKeys = new HashSet<string>();
            int number = 0;
            foreach (var item in items)
            {
                if (item == null || !item.Enable) continue;
                number++;
                List<ObbResult> matches;
                bool isCount;
                bool useWidth = true;
                switch (item.Name)
                {
                    case "线芯宽度": matches = cores; isCount = false; break;
                    case "线芯高度":
                    // 公共检测项归一化会把该中文名称转换为内部键，兼容模板和旧方案两种写法。
                    case "DetectItem.CoreHeight": matches = cores; isCount = false; useWidth = false; break;
                    case "胶皮宽度": matches = rubbers; isCount = false; break;
                    case "胶皮高度": matches = rubbers; isCount = false; useWidth = false; break;
                    case "线序数量":
                    case "DetectItem.CoreCount": matches = cores; isCount = true; break;
                    case "胶皮数量": matches = rubbers; isCount = true; break;
                    default:
                        throw new InvalidOperationException("多线芯模型不支持检测项：" + ParseCommon.GetDisplayName(item.Name));
                }

                item.IsCountItem = isCount;
                var values = new List<SingleDetectResult>();
                if (isCount)
                {
                    bool isOk = AddValue(item, matches.Count, true, param, study, values);
                    foreach (var match in matches)
                        ParseCommon.AddRect(addedRectKeys, new RotatedRect(match), isOk, ref nodeResult);
                }
                else if (matches.Count == 0)
                {
                    AddValue(item, 0F, false, param, null, values);
                }
                else
                {
                    foreach (var match in matches)
                    {
                        // OBB结果已由公共入口归一化方向，宽高分别代表图像水平方向和竖直方向尺寸。
                        float value = useWidth ? match.width : match.height;
                        if (param.NeedConvert) value *= param.Scale;
                        value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool isOk = AddValue(item, value, false, param, study, values);
                        ParseCommon.AddRect(addedRectKeys, new RotatedRect(match), isOk, ref nodeResult);
                    }
                }

                string currentValue = string.Join(",", values.Select(value => value.Value));
                ParseCommon.SetDetectItemCurValue(ref param, item.Name, currentValue);
                nodeResult.AlgorithmResult.DetectResults[item.Name] = values;
                nodeResult.AlgorithmResult.Texts.Add(new ColorText(
                    $"{number}. {ParseCommon.GetDisplayName(item.Name)}: {currentValue}{(isCount ? string.Empty : "mm")}",
                    values.All(value => value.IsOk) ? Color.Green : Color.Red));
            }

            bool allOk = nodeResult.AlgorithmResult.DetectResults.Values.SelectMany(value => value).All(value => value.IsOk);
            if (allOk) process.OKNumber++;
            else process.NGNumber++;

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

        /// <summary>校验并取得当前节点的学习会话；未学习时清理旧会话。</summary>
        private static StudySession PrepareStudySession(NodeParamTDAI param, List<DetectItemInfo> items)
        {
            if (!param.IsAutoStudy)
            {
                StudySessions.Remove(param);
                return null;
            }
            if (param.StudyNum <= 0 || param.StudyPercentage < 0 ||
                float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                throw new InvalidOperationException("自动学习次数必须大于0，浮动比例必须是非负有限数值。");
            StudySession study = StudySessions.GetValue(param, key => new StudySession());
            if (!ReferenceEquals(study.Items, items))
            {
                study.Items = items;
                study.Count = 0;
                study.Values.Clear();
            }
            return study;
        }

        /// <summary>写入单项结果并在学习期间收集样本。</summary>
        private static bool AddValue(DetectItemInfo item, float value, bool isCount, NodeParamTDAI param,
            StudySession study, List<SingleDetectResult> results)
        {
            bool isOk = param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value);
            results.Add(new SingleDetectResult
            {
                Name = item.Name,
                Value = isCount || value == 0 ? value.ToString("0") : value.ToString("F2"),
                IsOk = isOk
            });
            if (study != null)
            {
                if (!study.Values.TryGetValue(item.Name, out var samples))
                {
                    samples = new List<float>();
                    study.Values.Add(item.Name, samples);
                }
                samples.Add(value);
            }
            return isOk;
        }

        /// <summary>判断模型输出几何是否为可参与计算的有限数值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
