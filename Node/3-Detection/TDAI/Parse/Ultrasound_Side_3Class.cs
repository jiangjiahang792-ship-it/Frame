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
    /// 超声波焊接侧面三类结果解析：类别0为线芯，类别1为焊接区域，类别2为飞丝。
    /// </summary>
    public class Ultrasound_Side_3Class
    {
        /// <summary>按节点参数保存学习会话，避免不同流程互相混用计数和样本。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> StudySessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>单个节点当前检测配置的学习样本。</summary>
        private sealed class StudySession
        {
            /// <summary>当前学习的检测项配置引用，切换配置时重新采样。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已经采集的图像批次数。</summary>
            public int Count;
            /// <summary>以检测项存储键索引的学习值。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>
        /// 解析一批检测结果，生成检测明细、当前值及按检测项判定的矩形和文本。
        /// </summary>
        /// <param name="results">已应用ROI偏移的完整检测列表，空列表或null均作为未检出处理。</param>
        /// <param name="resultCount">保留原调用签名；以列表中的实际结果为准。</param>
        /// <param name="param">节点检测配置、尺寸换算及自动学习参数。</param>
        /// <param name="nodeResult">接收本轮算法结果的节点输出。</param>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var items) || items == null)
                throw new InvalidOperationException("超声波焊接检测项配置不存在，请先导入并选择检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("超声波焊接尺寸换算比例必须是大于0的有限数值。");

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

            // 只扫描一次检测列表；线芯按中心X选最左和最右，避免每个尺寸项重新排序。
            var leftCores = new List<DetResult>(1);
            var rightCores = new List<DetResult>(1);
            var welds = new List<DetResult>();
            var flyingWires = new List<DetResult>();
            DetResult? left = null;
            DetResult? right = null;
            int coreCount = 0;
            if (results != null)
            {
                foreach (var detection in results)
                {
                    if (detection.Box.Width <= 0 || detection.Box.Height <= 0) continue;
                    switch (detection.ClassId)
                    {
                        case 0:
                            coreCount++;
                            if (!left.HasValue || CenterX(detection) < CenterX(left.Value)) left = detection;
                            if (!right.HasValue || CenterX(detection) > CenterX(right.Value)) right = detection;
                            break;
                        case 1:
                            welds.Add(detection);
                            break;
                        case 2:
                            flyingWires.Add(detection);
                            break;
                    }
                }
            }
            if (left.HasValue) leftCores.Add(left.Value);
            // 沿用原文件规则：只有一个线芯时归左侧；相同中心不能同时充当左右线芯。
            if (coreCount >= 2 && CenterX(right.Value) > CenterX(left.Value)) rightCores.Add(right.Value);

            nodeResult.AlgorithmResult = new AlgorithmResult();
            var addedRectKeys = new HashSet<string>();
            foreach (var item in items)
            {
                if (item == null || !item.Enable) continue;
                List<DetResult> matches;
                bool useWidth = true;
                bool isFlyingWire = false;
                switch (item.Name)
                {
                    case "左线芯长度": matches = leftCores; break;
                    case "左线芯宽度": matches = leftCores; useWidth = false; break;
                    case "右线芯长度": matches = rightCores; break;
                    case "右线芯宽度": matches = rightCores; useWidth = false; break;
                    case "焊接区域长度": matches = welds; break;
                    case "焊接区域宽度": matches = welds; useWidth = false; break;
                    case "飞丝": matches = flyingWires; isFlyingWire = true; break;
                    default:
                        throw new InvalidOperationException($"超声波焊接侧面三类模型不支持检测项：{ParseCommon.GetDisplayName(item.Name)}。");
                }

                var singleResults = new List<SingleDetectResult>();
                // 飞丝始终是数量，禁止把缺陷数量乘上尺寸比例。
                if (isFlyingWire || item.IsCountItem)
                {
                    bool isOk = AddValue(item, matches.Count, true, param, study, singleResults);
                    foreach (var match in matches)
                        ParseCommon.AddRect(addedRectKeys, match.Box, isOk, ref nodeResult);
                }
                else if (matches.Count == 0)
                {
                    // 未检出更新为0，并按当前上下限判定；没有实测尺寸不参与学习。
                    AddValue(item, 0, false, param, null, singleResults);
                }
                else
                {
                    foreach (var match in matches)
                    {
                        float value = useWidth ? match.Box.Width : match.Box.Height;
                        if (param.NeedConvert) value *= param.Scale;
                        bool isOk = AddValue(item, value, false, param, study, singleResults);
                        ParseCommon.AddRect(addedRectKeys, match.Box, isOk, ref nodeResult);
                    }
                }

                item.CurValue = string.Join(",", singleResults.Select(value => value.Value));
                nodeResult.AlgorithmResult.DetectResults[item.Name] = singleResults;
                nodeResult.AlgorithmResult.Texts.Add(new ColorText(
                    $"{ParseCommon.GetDisplayName(item.Name)}:{item.CurValue}",
                    singleResults.All(value => value.IsOk) ? Color.Green : Color.Red));
            }

            if (study != null && ++study.Count >= param.StudyNum)
            {
                try
                {
                    ParseCommon.SetDetectItemMinMax(items, study.Values, param.StudyPercentage);
                    param.IsAutoStudy = false;
                    ParseCommon.CloseAutoStudyEvent?.Invoke(null, param.NodeName);
                }
                finally
                {
                    StudySessions.Remove(param);
                }
            }
        }

        /// <summary>使用双精度中心坐标避免大图像整数乘法溢出。</summary>
        private static double CenterX(DetResult result)
        {
            return result.Box.X + result.Box.Width / 2.0;
        }

        /// <summary>统一上下限、学习采样和结果格式，返回供矩形绘制使用的判定。</summary>
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
