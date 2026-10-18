using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using OpenCvSharp;
using Logger;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>新端子12类解析器：尺寸沿用RL12规则，前后防水栓分别测量，缺陷独立计数。</summary>
    public static class NewTerminal12Parse
    {
        /// <summary>测量类型；方向值为反向组数，咬胶值为百分比。</summary>
        private enum MeasureKind { Width, Height, Area, Count, Bite, Direction, TerminalCount, ColorPerimeter }

        /// <summary>颜色测量服务；与解析和公差学习职责分离。</summary>
        private static readonly IPlugColorPerimeter ColorMeasurement = new PlugColorPerimeter();

        /// <summary>可扩展的检测项定义，集中维护类别映射及数量策略。</summary>
        private sealed class Definition
        {
            /// <summary>模型类别编号。</summary>
            public int ClassId;
            /// <summary>测量方式。</summary>
            public MeasureKind Kind;
            /// <summary>是否独立于端子公共数量校验。</summary>
            public bool Independent;
        }

        /// <summary>按中文名称定义、按系统兼容键查询的检测项注册表。</summary>
        private static readonly Dictionary<string, Definition> Definitions = CreateDefinitions();
        /// <summary>程序集构建标识，缓存后用于现场日志区分实际运行文件。</summary>
        private static readonly string BuildIdentity = typeof(NewTerminal12Parse).Module.ModuleVersionId.ToString("N");
        /// <summary>按节点隔离学习状态，防止不同流程的样本互相污染。</summary>
        private static readonly ConditionalWeakTable<NodeParamTDAI, StudySession> Sessions =
            new ConditionalWeakTable<NodeParamTDAI, StudySession>();

        /// <summary>一个节点的一轮学习数据。</summary>
        private sealed class StudySession
        {
            /// <summary>本轮检测项配置引用。</summary>
            public List<DetectItemInfo> Items;
            /// <summary>已学习帧数。</summary>
            public int Count;
            /// <summary>学习开关轮次，取消后再次学习不会混入旧样本。</summary>
            public int Generation;
            /// <summary>本轮暂存颜色，全部有效学习完成后才替换正式配置。</summary>
            public readonly Dictionary<string, PlugColorModel> Colors = new Dictionary<string, PlugColorModel>();
            /// <summary>各尺寸项的有效实测值。</summary>
            public readonly Dictionary<string, List<float>> Values = new Dictionary<string, List<float>>();
        }

        /// <summary>创建27项定义及2个旧名称别名；新项目使用中文键，已有项目复用系统名称兼容层。</summary>
        private static Dictionary<string, Definition> CreateDefinitions()
        {
            var map = new Dictionary<string, Definition>();
            Action<string, int, MeasureKind, bool> add = (name, id, kind, independent) =>
                map.Add(DetectItemLanguage.NormalizeName(name), new Definition { ClassId = id, Kind = kind, Independent = independent });
            add("端子数量", -1, MeasureKind.TerminalCount, true);
            add("前铆脚露线芯长度", 0, MeasureKind.Width, false);
            add("前铆脚露线芯高度", 0, MeasureKind.Height, false);
            add("前铆脚面积", 1, MeasureKind.Area, false);
            add("后铆脚露线芯长度", 2, MeasureKind.Width, false);
            add("后铆脚面积", 4, MeasureKind.Area, false);
            add("咬胶长度", 3, MeasureKind.Bite, false);
            foreach (var pair in new[] { new { Name = "前防水栓", Id = 5 }, new { Name = "后防水栓", Id = 6 } })
            {
                add(pair.Name + "面积", pair.Id, MeasureKind.Area, false);
                add(pair.Name + "长度", pair.Id, MeasureKind.Width, false);
                add(pair.Name + "高度", pair.Id, MeasureKind.Height, false);
                add(pair.Name + "数量", pair.Id, MeasureKind.Count, true);
                add(pair.Name + "破损", pair.Id, MeasureKind.ColorPerimeter, false);
            }
            add("线芯长度", 8, MeasureKind.Width, false);
            add("线芯高度", 8, MeasureKind.Height, false);
            add("线芯数量", 8, MeasureKind.Count, true);
            add("胶皮数量", 7, MeasureKind.Count, true);
            add("胶皮宽度", 7, MeasureKind.Width, false);
            add("胶皮高度", 7, MeasureKind.Height, false);
            // 旧键继续解析以兼容既有方案和结果订阅，新配置使用穿塞尺寸名称。
            add("穿塞宽度", 7, MeasureKind.Width, false);
            add("穿塞高度", 7, MeasureKind.Height, false);
            add("飞丝", 9, MeasureKind.Count, true);
            add("飞胶", 10, MeasureKind.Count, true);
            add("防水栓破损", 11, MeasureKind.Count, true);
            add("防水栓正反", -1, MeasureKind.Direction, true);
            return map;
        }

        /// <summary>解析模型结果，输出逐项判定、框、文本、当前值及流程计数。</summary>
        public static void Parse(List<DetResult> results, int resultCount, NodeParamTDAI param,
            Process process, ref NodeResultTDAI nodeResult, OutputImage inputImage = null)
        {
            if (param == null) throw new ArgumentNullException(nameof(param));
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (nodeResult == null) throw new ArgumentNullException(nameof(nodeResult));
            if (string.IsNullOrWhiteSpace(param.CurDetectItemName) ||
                !Solution.Instance.DetectItemDic.TryGetValue(param.CurDetectItemName, out var items) || items == null)
                throw new InvalidOperationException("请先导入并选择新端子12类检测项模板。");
            if (param.NeedConvert && (param.Scale <= 0 || float.IsNaN(param.Scale) || float.IsInfinity(param.Scale)))
                throw new InvalidOperationException("尺寸比例必须为大于0的有限数值。");
            ParseCommon.NormalizeDetectItems(items);
            var enabled = items.Where(i => i != null && i.Enable).ToList();
            foreach (var item in enabled)
                if (!Definitions.ContainsKey(item.Name))
                    throw new InvalidOperationException("新端子12类不支持检测项：" + item.Name);
            var groups = Enumerable.Range(0, 12).Select(i => new List<DetResult>()).ToArray();
            foreach (var box in results ?? new List<DetResult>())
                if (box.ClassId >= 0 && box.ClassId < 12 && box.Box.Width > 0 && box.Box.Height > 0)
                    groups[box.ClassId].Add(box);
            // 所有新12类工位统一筛选，先过滤再进行数量、尺寸、绘图和学习，避免各项目结果不一致。
            var originalRubbers = groups[7];
            groups[7] = FilterRubberBetweenCoreAndPlug(originalRubbers, groups[8], groups[5]);
            LogHelper.AddLog(MsgLevel.Debug,
                $"[新12类胶皮筛选V2] 构建={BuildIdentity} 流程={process.ProcessName} 节点={param.NodeName} " +
                $"模型入口={param.ModelName} AI配置={param.ConfigPath} 检测项={param.CurDetectItemName} " +
                $"胶皮={originalRubbers.Count}->{groups[7].Count} " +
                $"线芯8={DescribeBoxes(groups[8])} 前防水栓5={DescribeBoxes(groups[5])} " +
                $"原胶皮7={DescribeBoxes(originalRubbers)} 保留胶皮7={DescribeBoxes(groups[7])}", true);
            var terminal = enabled.FirstOrDefault(i => Definitions[i.Name].Kind == MeasureKind.TerminalCount);
            int requiredCount = GetRequiredTerminalCount(terminal);
            int terminalCount = enabled.Where(i => !Definitions[i.Name].Independent)
                .Select(i => groups[Definitions[i.Name].ClassId].Count).DefaultIfEmpty(0).Max();
            StudySession study = null;
            if (param.IsAutoStudy)
            {
                if (param.StudyNum <= 0 || param.StudyPercentage < 0 || float.IsNaN(param.StudyPercentage) || float.IsInfinity(param.StudyPercentage))
                    throw new InvalidOperationException("学习次数和浮动比例无效。");
                study = Sessions.GetValue(param, key => new StudySession());
                if (!ReferenceEquals(study.Items, items) || study.Generation != param.StudyGeneration)
                {
                    study.Items = items; study.Count = 0; study.Values.Clear(); study.Colors.Clear();
                    study.Generation = param.StudyGeneration;
                }
            }
            else Sessions.Remove(param);
            // 先完成所有颜色项校验，失败时不提交颜色、公差或本帧其它尺寸样本。
            var perimeterResults = new Dictionary<string, List<PlugPerimeterRegion>>();
            try
            {
                foreach (var item in enabled.Where(i => Definitions[i.Name].Kind == MeasureKind.ColorPerimeter))
                {
                    var targets = groups[Definitions[item.Name].ClassId];
                    if (study != null && terminal != null && !ParseCommon.IsValueWithinLimits(terminal, targets.Count))
                        throw new InvalidOperationException(item.Name + "目标数量不符合端子数量要求，本轮学习未保存。");
                    PlugColorModel color = item.PlugColor;
                    if (study != null) study.Colors.TryGetValue(item.Name, out color);
                    else if (color == null) throw new InvalidOperationException(item.Name + "尚未学习颜色，请先执行一键学习。");
                    var measured = ColorMeasurement.Measure(inputImage, targets, ref color);
                    if (study != null)
                    {
                        if (measured.Count == 0 || measured.Any(r => r.Contour.Length == 0))
                            throw new InvalidOperationException(item.Name + "没有有效颜色区域（面积至少2000像素²），本轮学习未保存，请检查图像后重新学习。");
                        study.Colors[item.Name] = color;
                    }
                    perimeterResults[item.Name] = measured;
                }
            }
            catch { Sessions.Remove(param); throw; }
            nodeResult.AlgorithmResult = new AlgorithmResult();
            var texts = nodeResult.AlgorithmResult.Texts;
            var rectKeys = new HashSet<string>();
            foreach (var item in enabled)
            {
                var definition = Definitions[item.Name];
                bool countItem = definition.Kind == MeasureKind.Count || definition.Kind == MeasureKind.Direction || definition.Kind == MeasureKind.TerminalCount;
                item.IsCountItem = countItem;
                var matches = definition.ClassId < 0 ? new List<DetResult>() : groups[definition.ClassId];
                bool quantityOk = definition.Independent || terminal == null || ParseCommon.IsValueWithinLimits(terminal, matches.Count);
                var values = new List<SingleDetectResult>();
                if (definition.Kind == MeasureKind.ColorPerimeter)
                {
                    foreach (var region in perimeterResults[item.Name])
                    {
                        bool valid = region.Contour.Length > 0;
                        float value = valid ? (float)region.Perimeter : 0;
                        if (valid && param.NeedConvert) value *= param.Scale;
                        if (valid) value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool ok = valid && (param.IsAutoStudy || ParseCommon.IsValueWithinLimits(item, value));
                        values.Add(new SingleDetectResult { Name = item.Name, Value = value.ToString("F2"), IsOk = ok });
                        if (valid) nodeResult.AlgorithmResult.Contours.Add(new ColorContour(
                            region.Contour.Select(p => new PointF(p.X, p.Y)).ToList(), ok ? Color.Green : Color.Red) { IsClosed = true });
                        else ParseCommon.AddRect(rectKeys, region.Bounds, false, ref nodeResult);
                        if (study != null && valid)
                        {
                            if (!study.Values.TryGetValue(item.Name, out var samples)) study.Values[item.Name] = samples = new List<float>();
                            samples.Add(value);
                        }
                    }
                    // 多区域不作为端子数量；缺失目标单独补零NG，不能用其它目标的碎片抵消。
                    for (int missing = matches.Count; missing < requiredCount; missing++)
                        values.Add(new SingleDetectResult { Name = item.Name, Value = "0", IsOk = false });
                    if (values.Count == 0) values.Add(new SingleDetectResult { Name = item.Name, Value = "0", IsOk = false });
                    if (!quantityOk && matches.Count >= requiredCount)
                        foreach (var value in values) value.IsOk = false;
                }
                else if (countItem)
                {
                    int count = definition.Kind == MeasureKind.TerminalCount ? terminalCount :
                        definition.Kind == MeasureKind.Direction ? CountReversed(groups[5], groups[6], groups[8]) : matches.Count;
                    bool ok = ParseCommon.IsValueWithinLimits(item, count);
                    values.Add(new SingleDetectResult { Name = item.Name, Value = count.ToString(), IsOk = param.IsAutoStudy || ok });
                    if (definition.Kind == MeasureKind.Direction) matches = groups[5].Concat(groups[6]).ToList();
                    foreach (var box in matches) ParseCommon.AddRect(rectKeys, box.Box, param.IsAutoStudy || ok, ref nodeResult);
                    // 数量及缺陷判定不参与尺寸自动学习，保持用户设置的0～0等约束。
                }
                else
                {
                    foreach (var box in matches)
                    {
                        float value;
                        bool valid = TryMeasure(definition.Kind, box.Box, groups, out value);
                        // 沿用RL12面积比例规则；咬胶为百分比，不乘物理比例。
                        if (valid && param.NeedConvert && definition.Kind != MeasureKind.Bite) value *= param.Scale;
                        if (valid) value = ParseCommon.ApplyMeasurementCompensation(item, value);
                        bool measurementOk = valid && ParseCommon.IsValueWithinLimits(item, value);
                        // 缺件由独立的补零结果承担NG，不能把已检出的合格部件误标为尺寸不良。
                        bool ok = measurementOk && (quantityOk || matches.Count < requiredCount);
                        values.Add(new SingleDetectResult { Name = item.Name, Value = value.ToString("F2"), IsOk = param.IsAutoStudy || ok });
                        ParseCommon.AddRect(rectKeys, box.Box, param.IsAutoStudy || measurementOk, ref nodeResult);
                        if (study != null && valid)
                        {
                            if (!study.Values.TryGetValue(item.Name, out var samples)) study.Values[item.Name] = samples = new List<float>();
                            samples.Add(value);
                        }
                    }
                    // 仅补到公共数量下限，不按上限制造缺件；补零不产生矩形或学习样本。
                    for (int missing = matches.Count; missing < requiredCount; missing++)
                        values.Add(new SingleDetectResult { Name = item.Name, Value = "0", IsOk = param.IsAutoStudy });
                    if (values.Count == 0) values.Add(new SingleDetectResult { Name = item.Name, Value = "0",
                        IsOk = param.IsAutoStudy || (quantityOk && ParseCommon.IsValueWithinLimits(item, 0)) });
                }
                nodeResult.AlgorithmResult.DetectResults[item.Name] = values;
                item.CurValue = string.Join(",", values.Select(v => v.Value));
                texts.Add(new ColorText(LanguageManager.Format("{0}: {1} 设定值: [{2}, {3}]",
                    GetMeasurementDisplayName(item.Name), item.CurValue, item.MinValue, item.MaxValue), values.All(v => v.IsOk) ? Color.Green : Color.Red));
            }
            bool allOk = nodeResult.AlgorithmResult.DetectResults.Values.SelectMany(v => v).All(v => v.IsOk);
            if (allOk) process.OKNumber++; else process.NGNumber++;
            texts.Add(new ColorText(LanguageManager.Format("Result.TotalCount", process.OKNumber + process.NGNumber), Color.Green));
            texts.Add(new ColorText(LanguageManager.Format("Result.OKCount", process.OKNumber), Color.Green));
            texts.Add(new ColorText(LanguageManager.Format("Result.Yield", (100.0 * process.OKNumber / (process.OKNumber + process.NGNumber)).ToString("F2")), Color.Green));
            texts.Add(new ColorText(LanguageManager.Format("Result.ProductResult", allOk ? "OK" : "NG"), allOk ? Color.Green : Color.Red));
            if (study != null && ++study.Count >= param.StudyNum)
            {
                try
                {
                    ParseCommon.SetDetectItemMinMax(items, study.Values, param.StudyPercentage);
                    foreach (var item in enabled)
                        if (study.Colors.TryGetValue(item.Name, out var learnedColor)) item.PlugColor = learnedColor.Clone();
                    param.IsAutoStudy = false;
                    ParseCommon.CloseAutoStudyEvent?.Invoke(param, param.NodeName);
                }
                finally { Sessions.Remove(param); }
            }
        }

        /// <summary>旧配置保留原订阅键，但新模型的算法文字统一显示穿塞尺寸名称。</summary>
        private static string GetMeasurementDisplayName(string name)
        {
            if (name == "胶皮宽度") name = "穿塞宽度";
            else if (name == "胶皮高度") name = "穿塞高度";
            return ParseCommon.GetDisplayName(name);
        }

        /// <summary>公共数量下限向上取整为最低检出数量；未启用或无正下限时不补零。</summary>
        private static int GetRequiredTerminalCount(DetectItemInfo terminal)
        {
            if (terminal == null || !double.TryParse(terminal.MinValue, out double minimum) || minimum <= 0) return 0;
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum > 10000)
                throw new InvalidOperationException("端子数量下限必须是0～10000范围内的有效数值。");
            return (int)Math.Ceiling(minimum);
        }

        /// <summary>输出原图坐标的框摘要，最多64个，防止异常多框时日志无限增长。</summary>
        private static string DescribeBoxes(List<DetResult> boxes)
        {
            return "[" + string.Join(";", boxes.Take(64).Select(r =>
                $"{r.Box.X},{r.Box.Y},{r.Box.Width},{r.Box.Height}")) +
                (boxes.Count > 64 ? ";其余省略，总数=" + boxes.Count : "") + "]";
        }

        /// <summary>
        /// 保留线芯与前防水栓之间的胶皮框。参考框按总距离最小一对一配对，支持横向或竖向摆放。
        /// 胶皮中心位于相向边缘之间及共同纵向范围内才保留，允许边框轻微越界，但不裁剪原测量尺寸。
        /// 缺少参考框、参考框重叠或没有共同纵向范围时，不推测胶皮位置。
        /// </summary>
        /// <param name="rubbers">only_rubber类别的有效检测框。</param>
        /// <param name="cores">only_line类别的有效检测框。</param>
        /// <param name="plugs">waterproof_plug_front类别的有效检测框。</param>
        /// <returns>满足位置条件的原始胶皮检测框。</returns>
        private static List<DetResult> FilterRubberBetweenCoreAndPlug(
            List<DetResult> rubbers, List<DetResult> cores, List<DetResult> plugs)
        {
            var retained = new List<DetResult>();
            if (rubbers.Count == 0 || cores.Count == 0 || plugs.Count == 0) return retained;
            cores = cores.OrderBy(r => r.Box.Y).ThenBy(r => r.Box.X).ThenBy(r => r.Box.Width).ThenBy(r => r.Box.Height).ToList();
            plugs = plugs.OrderBy(r => r.Box.Y).ThenBy(r => r.Box.X).ThenBy(r => r.Box.Width).ThenBy(r => r.Box.Height).ToList();
            // 沿分离轴取相向边缘，垂直于分离轴取交集，避免把不同产品之间的背景当作胶皮区域。
            var pairs = (from c in Enumerable.Range(0, cores.Count)
                         from p in Enumerable.Range(0, plugs.Count)
                         let core = cores[c].Box
                         let plug = plugs[p].Box
                         let gap = GetRubberGap(core, plug)
                         where gap.HasValue
                         select new { Core = c, Plug = p, Left = gap.Value.Left, Right = gap.Value.Right,
                             Top = gap.Value.Top, Bottom = gap.Value.Bottom,
                             Distance = Distance(core, plug) })
                .ToList();
            if (pairs.Count == 0) return retained;
            int size = Math.Max(cores.Count, plugs.Count);
            // 无效配对的惩罚超过所有有效距离之和：先最大化有效配对数，再最小化总距离。
            double penalty = (pairs.Max(pair => pair.Distance) + 1) * (size + 1);
            var costs = new double[size, size];
            for (int row = 0; row < size; row++)
                for (int column = 0; column < size; column++) costs[row, column] = penalty;
            foreach (var pair in pairs) costs[pair.Core, pair.Plug] = pair.Distance;
            int[] assignment = FindMinimumCostAssignment(costs);
            var keep = new bool[rubbers.Count];
            foreach (var pair in pairs)
            {
                if (assignment[pair.Core] != pair.Plug) continue;
                for (int i = 0; i < rubbers.Count; i++)
                {
                    if (keep[i]) continue;
                    Rect box = rubbers[i].Box;
                    double x = box.X + box.Width * 0.5;
                    double y = box.Y + box.Height * 0.5;
                    keep[i] = x >= pair.Left && x <= pair.Right && y >= pair.Top && y <= pair.Bottom;
                }
            }
            // 按模型原顺序输出，不修改检测列表及框对象，避免影响其它类别或订阅。
            for (int i = 0; i < rubbers.Count; i++)
                if (keep[i]) retained.Add(rubbers[i]);
            return retained;
        }

        /// <summary>取得参考框之间的水平或垂直间隔；参考框重叠、接触或斜向完全错开时不生成区域。</summary>
        private static Rect? GetRubberGap(Rect core, Rect plug)
        {
            int overlapLeft = Math.Max(core.Left, plug.Left);
            int overlapRight = Math.Min(core.Right, plug.Right);
            int overlapTop = Math.Max(core.Top, plug.Top);
            int overlapBottom = Math.Min(core.Bottom, plug.Bottom);
            if (overlapTop < overlapBottom)
            {
                int left = Math.Min(core.Right, plug.Right);
                int right = Math.Max(core.Left, plug.Left);
                if (left < right) return new Rect(left, overlapTop, right - left, overlapBottom - overlapTop);
            }
            if (overlapLeft < overlapRight)
            {
                int top = Math.Min(core.Bottom, plug.Bottom);
                int bottom = Math.Max(core.Top, plug.Top);
                if (top < bottom) return new Rect(overlapLeft, top, overlapRight - overlapLeft, bottom - top);
            }
            return null;
        }

        /// <summary>
        /// 使用匈牙利算法求方阵最小费用指派，返回每行对应的列。
        /// 时间复杂度O(n³)、辅助空间O(n)，避免最近邻贪心占用相邻端子的参考框。
        /// </summary>
        /// <param name="costs">有限非负费用矩阵，已填充虚拟行列及无效配对惩罚。</param>
        /// <returns>每行唯一对应的列下标。</returns>
        private static int[] FindMinimumCostAssignment(double[,] costs)
        {
            int size = costs.GetLength(0);
            var rowPotential = new double[size + 1];
            var columnPotential = new double[size + 1];
            var matchedRow = new int[size + 1];
            var previousColumn = new int[size + 1];
            var minimum = new double[size + 1];
            var used = new bool[size + 1];
            for (int row = 1; row <= size; row++)
            {
                matchedRow[0] = row;
                int column = 0;
                Array.Clear(used, 0, used.Length);
                for (int j = 1; j <= size; j++) minimum[j] = double.PositiveInfinity;
                do
                {
                    used[column] = true;
                    int currentRow = matchedRow[column];
                    double delta = double.PositiveInfinity;
                    int nextColumn = 0;
                    for (int j = 1; j <= size; j++)
                    {
                        if (used[j]) continue;
                        double reduced = costs[currentRow - 1, j - 1] - rowPotential[currentRow] - columnPotential[j];
                        if (reduced < minimum[j]) { minimum[j] = reduced; previousColumn[j] = column; }
                        if (minimum[j] < delta) { delta = minimum[j]; nextColumn = j; }
                    }
                    for (int j = 0; j <= size; j++)
                    {
                        if (used[j]) { rowPotential[matchedRow[j]] += delta; columnPotential[j] -= delta; }
                        else minimum[j] -= delta;
                    }
                    column = nextColumn;
                } while (matchedRow[column] != 0);
                // 沿最短增广路径重新分配已占用的列，最终仍保证每个框只参与一次。
                do
                {
                    int previous = previousColumn[column];
                    matchedRow[column] = matchedRow[previous];
                    column = previous;
                } while (column != 0);
            }
            var assignment = new int[size];
            for (int column = 1; column <= size; column++) assignment[matchedRow[column] - 1] = column - 1;
            return assignment;
        }

        /// <summary>测量尺寸；咬胶使用最近的前后铆脚，缺依赖或间距为零返回无效。</summary>
        private static bool TryMeasure(MeasureKind kind, Rect box, List<DetResult>[] groups, out float value)
        {
            value = 0;
            if (kind == MeasureKind.Width) value = box.Width;
            else if (kind == MeasureKind.Height) value = box.Height;
            else if (kind == MeasureKind.Area) value = (float)box.Width * box.Height;
            else if (kind == MeasureKind.Bite)
            {
                if (groups[1].Count == 0 || groups[4].Count == 0) return false;
                var front = groups[1].OrderBy(r => Distance(box, r.Box)).First().Box;
                var back = groups[4].OrderBy(r => Distance(box, r.Box)).First().Box;
                double gap = (double)front.Left - back.Right;
                if (gap == 0) return false;
                value = (float)Math.Abs(box.Width / gap * 100);
            }
            return true;
        }

        /// <summary>中心距离平方，不求平方根，供位置配对使用。</summary>
        private static double Distance(Rect a, Rect b)
        {
            double x = a.X + a.Width * 0.5 - b.X - b.Width * 0.5;
            double y = a.Y + a.Height * 0.5 - b.Y - b.Height * 0.5;
            return x * x + y * y;
        }

        /// <summary>横向输入以线芯所在侧为前方；各部件一对一匹配，缺框或方向不明确按反计数。</summary>
        private static int CountReversed(List<DetResult> fronts, List<DetResult> backs, List<DetResult> cores)
        {
            if (fronts.Count == 0 && backs.Count == 0) return 1;
            if (fronts.Count == 0 || backs.Count == 0) return Math.Max(fronts.Count, backs.Count);
            var frontBoxes = OrderBoxes(fronts);
            var backBoxes = OrderBoxes(backs);
            int[] backMatches = MatchHorizontalBoxes(frontBoxes, backBoxes);
            var pairs = new List<(Rect Front, Rect Back)>();
            var pairBoxes = new List<Rect>();
            for (int i = 0; i < frontBoxes.Count; i++)
            {
                if (backMatches[i] < 0) continue;
                Rect front = frontBoxes[i];
                Rect back = backBoxes[backMatches[i]];
                pairs.Add((front, back));
                // 使用共同纵向范围匹配本根线芯，避免其它行的线芯提供错误方向。
                int top = Math.Max(front.Top, back.Top);
                int bottom = Math.Min(front.Bottom, back.Bottom);
                int left = Math.Min(front.Left, back.Left);
                pairBoxes.Add(new Rect(left, top, Math.Max(front.Right, back.Right) - left, bottom - top));
            }
            int reversed = fronts.Count + backs.Count - 2 * pairs.Count;
            var coreBoxes = OrderBoxes(cores);
            int[] coreMatches = MatchHorizontalBoxes(pairBoxes, coreBoxes);
            for (int i = 0; i < pairs.Count; i++)
            {
                if (coreMatches[i] < 0) { reversed++; continue; }
                double frontX = pairs[i].Front.X + pairs[i].Front.Width * 0.5;
                double backX = pairs[i].Back.X + pairs[i].Back.Width * 0.5;
                Rect core = coreBoxes[coreMatches[i]];
                double coreX = core.X + core.Width * 0.5;
                // 用部件中心确定顺序，避免框宽差异让右边缘比较颠倒；线芯必须在前后部件的同一外侧。
                bool forward = (coreX > Math.Max(frontX, backX) && frontX > backX)
                    || (coreX < Math.Min(frontX, backX) && frontX < backX);
                if (!forward) reversed++;
            }
            return reversed;
        }

        /// <summary>稳定排列参考框，使输入检测列表顺序不影响距离相同的配对。</summary>
        private static List<Rect> OrderBoxes(List<DetResult> boxes)
        {
            return boxes.Select(r => r.Box).OrderBy(r => r.Y).ThenBy(r => r.X)
                .ThenBy(r => r.Width).ThenBy(r => r.Height).ToList();
        }

        /// <summary>共同纵向范围内进行最小总距离配对；返回目标下标，缺失或不相交记为-1。</summary>
        private static int[] MatchHorizontalBoxes(List<Rect> sources, List<Rect> targets)
        {
            var matches = Enumerable.Repeat(-1, sources.Count).ToArray();
            if (sources.Count == 0 || targets.Count == 0) return matches;
            int size = Math.Max(sources.Count, targets.Count);
            var costs = new double[size, size];
            double maxDistance = 0;
            foreach (var source in sources)
                foreach (var target in targets) maxDistance = Math.Max(maxDistance, Distance(source, target));
            double penalty = (maxDistance + 1) * (size + 1);
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    costs[i, j] = i < sources.Count && j < targets.Count &&
                        Math.Max(sources[i].Top, targets[j].Top) < Math.Min(sources[i].Bottom, targets[j].Bottom)
                        ? Distance(sources[i], targets[j]) : penalty;
            int[] assignment = FindMinimumCostAssignment(costs);
            for (int i = 0; i < sources.Count; i++)
                if (assignment[i] < targets.Count && costs[i, assignment[i]] < penalty) matches[i] = assignment[i];
            return matches;
        }
    }
}
