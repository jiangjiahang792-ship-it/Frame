using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>勒刚端子侧面15类OBB解析，沿用原模型的类别编号及飞丝过滤规则。</summary>
    public static class TerminalSide_C15_Obb
    {
        /// <summary>按节点隔离学习及历史框；节点释放时缓存自动回收。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, ParseState> States =
            new ConditionalWeakTable<NodeParamTDAI, ParseState>();

        /// <summary>一个节点当前配置的解析状态。</summary>
        private sealed class ParseState
        {
            /// <summary>检测配置引用，切换配置时清空历史。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已学习的图像批次数。</summary>
            public int Count;
            /// <summary>按中文检测项名称保存学习样本。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
            /// <summary>按检测项记录最近检出的框，用于未检出且NG时补红框。</summary>
            public readonly Dictionary<string, List<ObbResult>> History = new Dictionary<string, List<ObbResult>>();
        }

        /// <summary>解析已恢复到原图坐标的结果，更新检测项、旋转框、文本及学习状态。</summary>
        /// <param name="results">完整检出列表；空值按未检出处理。</param>
        /// <param name="resultCount">兼容框架签名，实际以列表数量为准。</param>
        /// <param name="param">当前节点的模型及检测配置。</param>
        /// <param name="nodeResult">本轮结果输出。</param>
        public static void Parse(List<ObbResult> results, int resultCount, NodeParamTDAI param, ref NodeResultTDAI nodeResult)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var items) || items == null)
                throw new InvalidOperationException("勒刚侧面检测项配置不存在，请先导入并选择检测项模板。");
            if (param.NeedConvert && (!IsFinite(param.Scale) || param.Scale <= 0))
                throw new InvalidOperationException("勒刚侧面换算比例必须是大于0的有限数值。");
            if (param.IsAutoStudy && (param.StudyNum <= 0 || !IsFinite(param.StudyPercentage) || param.StudyPercentage < 0))
                throw new InvalidOperationException("自动学习次数必须大于0，浮动比例必须是非负有限数值。");

            var state = States.GetValue(param, key => new ParseState());
            // 同节点的学习及缓存更新保持原子性，不同节点互不等待。
            lock (state)
            {
                if (!ReferenceEquals(state.Items, items))
                {
                    state.Items = items;
                    state.Count = 0;
                    state.Values.Clear();
                    state.History.Clear();
                }
                if (!param.IsAutoStudy)
                {
                    state.Count = 0;
                    state.Values.Clear();
                }

                var rivets = new List<ObbResult>();
                var wires = new List<ObbResult>();
                var defects = new List<ObbResult>();
                bool hasTail = false;
                // 一次分类，避免为每个检测项反复扫描整个推理结果。
                if (results != null)
                    foreach (var detection in results)
                    {
                        if (!IsFinite(detection.center_x) || !IsFinite(detection.center_y) ||
                            !IsFinite(detection.width) || !IsFinite(detection.height) || !IsFinite(detection.angle) ||
                            detection.width <= 0 || detection.height <= 0) continue;
                        switch (detection.class_id)
                        {
                            case 4: rivets.Add(detection); break;
                            case 8: wires.Add(detection); break;
                            case 10: hasTail = true; break;
                            case 11: defects.Add(detection); break;
                        }
                    }
                // 保留原算法：首个后铆脚框左扩100、上扩60、右扩10、下扩40像素。
                // 缺少后铆脚时禁止用原点的空框过滤真实飞丝。
                if (rivets.Count > 0)
                {
                    var rivet = rivets[0];
                    wires.RemoveAll(wire => wire.center_x >= rivet.center_x - rivet.width / 2 - 100 &&
                        wire.center_x < rivet.center_x + rivet.width / 2 + 10 &&
                        wire.center_y >= rivet.center_y - rivet.height / 2 - 60 &&
                        wire.center_y < rivet.center_y + rivet.height / 2 + 40);
                }

                nodeResult.AlgorithmResult = new AlgorithmResult();
                var keys = new HashSet<string>();
                foreach (var item in items)
                {
                    if (item == null || !item.Enable) continue;
                    List<ObbResult> matches;
                    bool countItem;
                    switch (item.Name)
                    {
                        case "后铆脚面积（侧面）": matches = rivets; countItem = false; break;
                        case "飞丝存在（侧面）": matches = wires; countItem = true; break;
                        case "防水栓破损（侧面）":
                            if (param.IsAutoStudy && !hasTail)
                            {
                                item.Enable = false;
                                item.CurValue = "0";
                                state.Values.Remove(item.Name);
                                continue;
                            }
                            matches = defects; countItem = true; break;
                        default: throw new InvalidOperationException("勒刚侧面模型不支持检测项：" + item.Name);
                    }
                    // 类型由业务决定，防止旧模板把缺陷数量当成尺寸换算。
                    item.IsCountItem = countItem;
                    var values = new List<SingleDetectResult>();
                    if (countItem)
                    {
                        bool ok = AddValue(item, matches.Count, true, param, state, values);
                        foreach (var match in matches) ParseCommon.AddRect(keys, new RotatedRect(match), ok, ref nodeResult);
                    }
                    else if (matches.Count == 0)
                    {
                        // 未检出使用0判定，禁止补偿或把缺失样本用于面积学习。
                        AddValue(item, 0, false, param, null, values);
                    }
                    else
                    {
                        foreach (var match in matches)
                        {
                            float area = match.width * match.height;
                            // 兼容原勒刚方案：面积乘一次Scale，避免已有上下限换算口径改变。
                            if (param.NeedConvert) area *= param.Scale;
                            area = ParseCommon.ApplyMeasurementCompensation(item, area);
                            bool ok = AddValue(item, area, false, param, state, values);
                            ParseCommon.AddRect(keys, new RotatedRect(match), ok, ref nodeResult);
                        }
                    }
                    bool allOk = values.All(value => value.IsOk);
                    if (matches.Count > 0) state.History[item.Name] = new List<ObbResult>(matches);
                    else if (!allOk && state.History.TryGetValue(item.Name, out var previous))
                        foreach (var match in previous) ParseCommon.AddRect(keys, new RotatedRect(match), false, ref nodeResult);
                    item.CurValue = string.Join(",", values.Select(value => value.Value));
                    nodeResult.AlgorithmResult.DetectResults[item.Name] = values;
                    nodeResult.AlgorithmResult.Texts.Add(new ColorText(item.Name + ":" + item.CurValue, allOk ? Color.Green : Color.Red));
                }
                if (param.IsAutoStudy && ++state.Count >= param.StudyNum)
                {
                    try
                    {
                        ParseCommon.SetDetectItemMinMax(items, state.Values, param.StudyPercentage);
                        param.IsAutoStudy = false;
                        ParseCommon.CloseAutoStudyEvent?.Invoke(null, param.NodeName);
                    }
                    finally
                    {
                        state.Count = 0;
                        state.Values.Clear();
                    }
                }
            }
        }

        /// <summary>拒绝非有限的模型几何和配置数值。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>统一两位小数、上下限判定和学习采样，保证显示及判定使用同一数值。</summary>
        private static bool AddValue(DetectItemInfo item, float value, bool countItem, NodeParamTDAI param,
            ParseState state, List<SingleDetectResult> values)
        {
            if (!IsFinite(value)) throw new InvalidOperationException("勒刚侧面检测值无效：" + item.Name);
            string text = countItem || value == 0 ? value.ToString("0") : value.ToString("F2");
            value = float.Parse(text);
            bool ok = param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value);
            values.Add(new SingleDetectResult { Name = item.Name, Value = text, IsOk = ok });
            if (param.IsAutoStudy && state != null)
            {
                if (!state.Values.TryGetValue(item.Name, out var samples))
                {
                    samples = new List<float>();
                    state.Values.Add(item.Name, samples);
                }
                samples.Add(value);
            }
            return ok;
        }
    }
}
